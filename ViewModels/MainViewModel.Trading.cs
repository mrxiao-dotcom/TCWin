using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.ViewModels
{
    /// <summary>
    /// MainViewModel交易功能部分
    /// </summary>
    public partial class MainViewModel
    {
        #region 交易参数属性
        [ObservableProperty]
        private string _symbol = "BTCUSDT";

        [ObservableProperty]
        private string _side = "BUY";

        [ObservableProperty]
        private string _positionSide = "BOTH";

        [ObservableProperty]
        private int _leverage = 3;

        [ObservableProperty]
        private decimal _quantity = 0;

        [ObservableProperty]
        private decimal _latestPrice = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLimitConditionalOrder))]
        [NotifyPropertyChangedFor(nameof(IsConditionalOrderVisible))]
        private string _orderType = "MARKET";

        [ObservableProperty]
        private string _marginType = "ISOLATED";

        [ObservableProperty]
        private decimal _stopLossRatio = 5;

        [ObservableProperty]
        private decimal _stopLossPrice = 0;

        [ObservableProperty]
        private decimal _stopLossAmount = 0;

        // 可用风险金
        [ObservableProperty]
        private decimal _availableRiskCapital = 0;

        // 限价单价格
        [ObservableProperty]
        private decimal _price = 0;

        // 价格有效性标志
        [ObservableProperty]
        private bool _isPriceValid = true;

        // 价格警告信息
        [ObservableProperty]
        private string _priceWarningMessage = "";

        // 添加存储计算详情的属性
        [ObservableProperty]
        private string _riskCapitalCalculationDetail = "";
        #endregion

        #region 条件单相关属性
        public bool IsLimitConditionalOrder => OrderType == "STOP" || OrderType == "TAKE_PROFIT";
        public bool IsConditionalOrderVisible => OrderType == "条件单";
        #endregion

        #region 交易UI绑定属性
        public bool IsBuySelected
        {
            get => Side == "BUY";
            set
            {
                if (value)
                {
                    Side = "BUY";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSellSelected));
                }
            }
        }

        public bool IsSellSelected
        {
            get => Side == "SELL";
            set
            {
                if (value)
                {
                    Side = "SELL";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsBuySelected));
                }
            }
        }

        public bool IsMarketOrderSelected
        {
            get => OrderType == "MARKET";
            set
            {
                if (value)
                {
                    OrderType = "MARKET";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsLimitOrderSelected));
                }
            }
        }

        public bool IsLimitOrderSelected
        {
            get => OrderType == "LIMIT";
            set
            {
                if (value)
                {
                    OrderType = "LIMIT";
                    // 选择限价单时自动填入最新价格
                    if (LatestPrice > 0)
                    {
                        Price = LatestPrice;
                        _logger.LogDebug($"选择限价单，自动填入价格: {Price}");
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsMarketOrderSelected));
                }
            }
        }

        public bool IsLimitOrder => OrderType == "LIMIT";

        public bool IsIsolatedMarginSelected
        {
            get => MarginType == "ISOLATED";
            set
            {
                if (value)
                {
                    MarginType = "ISOLATED";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCrossedMarginSelected));
                }
            }
        }

        public bool IsCrossedMarginSelected
        {
            get => MarginType == "CROSSED";
            set
            {
                if (value)
                {
                    MarginType = "CROSSED";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsIsolatedMarginSelected));
                }
            }
        }

        // 计算属性：是否可以下单
        public bool CanPlaceOrder
        {
            get
            {
                var basicCondition = SelectedAccount != null &&
                       !string.IsNullOrEmpty(Symbol) &&
                       Quantity > 0 &&
                       LatestPrice > 0 &&
                       !IsLoading;

                // 对限价单增加价格验证
                var limitOrderPriceValid = OrderType != "LIMIT" || (Price > 0 && IsPriceValid);
                
                var canPlace = basicCondition && limitOrderPriceValid;
                
                // 调试信息
                if (!canPlace)
                {
                    var reason = "";
                    if (SelectedAccount == null) reason += "缺少账户配置;";
                    if (string.IsNullOrEmpty(Symbol)) reason += "缺少交易合约;";
                    if (Quantity <= 0) reason += "交易数量无效;";
                    if (LatestPrice <= 0) reason += "缺少最新价格;";
                    if (IsLoading) reason += "正在加载中;";
                    if (OrderType == "LIMIT" && Price <= 0) reason += "限价单价格无效;";
                    if (OrderType == "LIMIT" && !IsPriceValid) reason += "限价单价格偏离过大;";
                    
                    _logger?.LogDebug($"下单按钮不可用: {reason}");
                }
                
                return canPlace;
            }
        }
        #endregion

        #region 交易命令
        [RelayCommand]
        private async Task PlaceOrderAsync()
        {
            if (!CanPlaceOrder)
            {
                StatusMessage = "下单条件不满足";
                return;
            }

            try
            {
                // 根据用户选择的订单类型决定下单方式
                var orderType = OrderType; // 使用用户选择的订单类型
                
                // 验证限价单价格
                if (orderType == "LIMIT" && Price <= 0)
                {
                    StatusMessage = "限价单必须设置价格";
                    return;
                }
                
                // 显示下单确认对话框
                var confirmationData = new Views.OrderConfirmationModel
                {
                    Symbol = Symbol,
                    Side = Side,
                    OrderType = orderType,
                    Quantity = Quantity,
                    Price = orderType == "LIMIT" ? Price : LatestPrice, // 限价单使用设置的价格，市价单使用最新价格显示
                    Leverage = Leverage,
                    MarginType = MarginType,
                    StopLossRatio = StopLossRatio,
                    StopLossPrice = StopLossPrice
                };

                var confirmDialog = new OrderConfirmationDialog(confirmationData)
                {
                    Owner = Application.Current.MainWindow
                };

                confirmDialog.ShowDialog();

                // 用户取消下单
                if (!confirmDialog.IsConfirmed)
                {
                    StatusMessage = "用户取消下单";
                    return;
                }

                IsLoading = true;
                StatusMessage = orderType == "LIMIT" ? "正在下限价单..." : "正在下市价单...";

                // 创建订单请求
                var request = new OrderRequest
                {
                    Symbol = Symbol,
                    Side = Side,
                    Type = orderType,
                    Quantity = Quantity,
                    Price = orderType == "LIMIT" ? Price : 0, // 限价单需要价格，市价单不需要
                    TimeInForce = orderType == "LIMIT" ? "GTC" : null // 限价单设置时效，市价单不需要
                };

                // 验证订单参数
                var (isValid, errorMessage) = await _calculationService.ValidateOrderParametersAsync(request);
                if (!isValid)
                {
                    StatusMessage = $"订单验证失败: {errorMessage}";
                    return;
                }

                // 设置杠杆和保证金模式
                await _binanceService.SetLeverageAsync(Symbol, Leverage);
                await _binanceService.SetMarginTypeAsync(Symbol, MarginType);

                // 下主单
                var success = await _binanceService.PlaceOrderAsync(request);
                if (success)
                {
                    var orderDescription = orderType == "LIMIT" ? $"限价单(价格:{Price})" : "市价单";
                    StatusMessage = $"{orderDescription}下单成功";
                    _logger.LogInformation($"{orderDescription}下单成功: {Symbol} {Side} {Quantity}");

                    // 🚀 关键功能：如果设置了止损比例，立即下止损委托单
                    if (StopLossRatio > 0 && StopLossPrice > 0)
                    {
                        StatusMessage = "正在下止损委托单...";
                        
                        // 对于限价单，使用限价单价格计算止损；对于市价单，使用最新价格
                        var referencePrice = orderType == "LIMIT" ? Price : LatestPrice;
                        var stopLossPrice = _calculationService.CalculateStopLossPrice(referencePrice, StopLossRatio, Side);
                        
                        var stopRequest = new OrderRequest
                        {
                            Symbol = Symbol,
                            Side = Side == "BUY" ? "SELL" : "BUY", // 止损方向与开仓方向相反
                            Type = "STOP_MARKET",
                            Quantity = Quantity, // 止损数量与开仓数量相同
                            StopPrice = stopLossPrice,
                            ReduceOnly = true, // 止损单必须是减仓
                            WorkingType = "CONTRACT_PRICE"
                        };
                        
                        var stopSuccess = await _binanceService.PlaceOrderAsync(stopRequest);
                        if (stopSuccess)
                        {
                            StatusMessage = $"{orderDescription}成功，已设置止损委托(止损价:{stopLossPrice:F4})";
                            _logger.LogInformation($"止损委托单设置成功: {Symbol} 止损价格 {stopLossPrice:F4}");
                        }
                        else
                        {
                            StatusMessage = $"{orderDescription}成功，止损委托失败";
                            _logger.LogWarning($"止损委托单设置失败: {Symbol}");
                        }
                    }
                    else
                    {
                        StatusMessage = $"{orderDescription}下单成功";
                    }

                    // 刷新数据
                    await RefreshDataAsync();
                }
                else
                {
                    var orderDescription = orderType == "LIMIT" ? "限价单" : "市价单";
                    StatusMessage = $"{orderDescription}下单失败";
                    _logger.LogWarning($"{orderDescription}下单失败: {Symbol} {Side} {Quantity}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"下单异常: {ex.Message}";
                _logger.LogError(ex, "下单过程中发生异常");
            }
            finally
            {
                IsLoading = false;
            }
        }
        #endregion

        #region 一键保本加仓功能命令
        [RelayCommand]
        private async Task OneClickBreakEvenAddPositionAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("开始执行一键保本加仓操作");

                // 1. 基础条件检查
                if (SelectedAccount == null)
                {
                    StatusMessage = "❌ 请先选择账户配置";
                    return;
                }

                if (string.IsNullOrEmpty(Symbol))
                {
                    StatusMessage = "❌ 请先选择交易合约";
                    return;
                }

                if (AccountInfo == null)
                {
                    StatusMessage = "❌ 请先刷新账户信息";
                    return;
                }

                var normalizedSymbol = NormalizeSymbol(Symbol);
                
                // 2. 获取当前合约的持仓
                var currentPosition = Positions.FirstOrDefault(p => 
                    p.Symbol == normalizedSymbol && Math.Abs(p.PositionAmt) > 0);

                if (currentPosition == null)
                {
                    StatusMessage = $"❌ 当前合约 {normalizedSymbol} 无持仓，无法执行保本加仓";
                    return;
                }

                // 3. 检查是否有浮盈
                if (currentPosition.UnrealizedProfit <= 0)
                {
                    StatusMessage = $"❌ 当前持仓浮盈为 {currentPosition.UnrealizedProfit:F2}U，需要有浮盈才能执行保本加仓";
                    return;
                }

                // 4. 计算风险金
                var accountEquity = AccountInfo.TotalEquity;
                var riskTimes = SelectedAccount.RiskCapitalTimes;
                var riskCapital = accountEquity / riskTimes;
                var minRequiredProfit = riskCapital * 0.5m; // 0.5倍风险金

                if (currentPosition.UnrealizedProfit < minRequiredProfit)
                {
                    StatusMessage = $"❌ 当前浮盈 {currentPosition.UnrealizedProfit:F2}U < 0.5倍风险金({minRequiredProfit:F2}U)，条件不满足";
                    return;
                }

                _logger.LogInformation($"条件检查通过 - 持仓: {currentPosition.Symbol}, 浮盈: {currentPosition.UnrealizedProfit:F2}U, 要求浮盈: {minRequiredProfit:F2}U");

                // 5. 获取最新价格
                var latestPrice = await _binanceService.GetLatestPriceAsync(normalizedSymbol);
                if (latestPrice <= 0)
                {
                    StatusMessage = $"❌ 无法获取 {normalizedSymbol} 的最新价格";
                    return;
                }

                // 6. 确定加仓方向（与当前持仓方向一致）
                var addPositionSide = currentPosition.PositionAmt > 0 ? "BUY" : "SELL";
                
                                 // 7. 根据风险金和止损比例计算加仓数量
                if (StopLossRatio <= 0)
                {
                    StatusMessage = "❌ 请先设置有效的止损比例";
                    return;
                }

                // 风险金 = 允许单笔亏损的金额
                var riskAmount = riskCapital;
                
                // 止损比例转换为小数
                var stopLossPercentage = StopLossRatio / 100m;
                
                // 可以持仓的货值 = 风险金 ÷ 止损比例
                var positionValue = riskAmount / stopLossPercentage;
                
                // 加仓数量 = 货值 ÷ 最新价格
                var addQuantity = positionValue / latestPrice;

                if (addQuantity <= 0)
                {
                    StatusMessage = "❌ 无法计算有效的加仓数量";
                    return;
                }

                // 检查交易规则和精度
                try
                {
                    var (minQty, maxQty, stepSize, tickSize, maxLeverage) = await _binanceService.GetSymbolTradingRulesAsync(normalizedSymbol);
                    
                    // 调整数量到正确的精度
                    addQuantity = Math.Floor(addQuantity / stepSize) * stepSize;
                    
                    if (addQuantity < minQty)
                    {
                        StatusMessage = $"❌ 计算的加仓数量 {addQuantity:F6} 小于最小交易数量 {minQty:F6}";
                        return;
                    }
                    
                    if (addQuantity > maxQty)
                    {
                        StatusMessage = $"❌ 计算的加仓数量 {addQuantity:F6} 超过最大交易数量 {maxQty:F6}";
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"获取交易规则失败，使用默认精度处理: {normalizedSymbol}");
                    // 使用默认精度处理（保留6位小数）
                    addQuantity = Math.Round(addQuantity, 6);
                }

                                 _logger.LogInformation($"计算加仓参数 - 方向: {addPositionSide}, 风险金: {riskAmount:F2}U, 止损比例: {StopLossRatio}%, 货值: {positionValue:F2}U, 价格: {latestPrice:F4}, 计算数量: {addQuantity:F6}");

                // 8. 执行加仓下单
                var addOrderRequest = new OrderRequest
                {
                    Symbol = normalizedSymbol,
                    Side = addPositionSide,
                    Type = "MARKET",
                    Quantity = addQuantity,
                    TimeInForce = "GTC"
                };

                var addOrderSuccess = await _binanceService.PlaceOrderAsync(addOrderRequest);
                if (!addOrderSuccess)
                {
                    StatusMessage = "❌ 加仓下单失败";
                    return;
                }

                                 StatusMessage = $"✅ 加仓下单成功 - {addPositionSide} {addQuantity:F6} (货值: {positionValue:F2}U) @ 市价";
                _logger.LogInformation($"加仓下单成功: {addOrderRequest.Symbol} {addOrderRequest.Side} {addOrderRequest.Quantity:F6}, 货值: {positionValue:F2}U");

                // 9. 等待一段时间让订单执行和数据更新
                await Task.Delay(2000);
                
                // 10. 刷新持仓数据获取新的综合开仓价
                await RefreshDataAsync();
                
                // 11. 获取更新后的持仓信息
                var updatedPosition = Positions.FirstOrDefault(p => 
                    p.Symbol == normalizedSymbol && Math.Abs(p.PositionAmt) > 0);

                if (updatedPosition == null)
                {
                    StatusMessage = "⚠️ 加仓完成但无法获取更新后的持仓信息";
                    return;
                }

                // 12. 清除旧的止损委托（仅删除止损类型，保留止盈和条件单）
                var oldStopOrders = Orders.Where(o => 
                    o.Symbol == normalizedSymbol && 
                    (o.Type == "STOP_MARKET" || o.Type == "STOP_LIMIT") &&
                    o.ReduceOnly).ToList();

                foreach (var order in oldStopOrders)
                {
                    try
                    {
                        var cancelSuccess = await _binanceService.CancelOrderAsync(normalizedSymbol, order.OrderId);
                        if (cancelSuccess)
                        {
                            _logger.LogInformation($"已取消旧止损单: {order.OrderId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"取消旧止损单失败: {order.OrderId}");
                    }
                }

                // 13. 设置新的保本止损委托
                var newStopPrice = updatedPosition.EntryPrice; // 使用综合开仓价作为止损价
                var stopOrderSide = updatedPosition.PositionAmt > 0 ? "SELL" : "BUY"; // 平仓方向
                var stopQuantity = Math.Abs(updatedPosition.PositionAmt); // 全部持仓数量

                var stopOrderRequest = new OrderRequest
                {
                    Symbol = normalizedSymbol,
                    Side = stopOrderSide,
                    Type = "STOP_MARKET",
                    Quantity = stopQuantity,
                    StopPrice = newStopPrice,
                    TimeInForce = "GTC",
                    ReduceOnly = true
                };

                var stopOrderSuccess = await _binanceService.PlaceOrderAsync(stopOrderRequest);
                if (stopOrderSuccess)
                {
                    StatusMessage = $"🎯 保本止损设置成功 - {stopOrderSide} {stopQuantity:F6} @ {newStopPrice:F4} (保本价)";
                    _logger.LogInformation($"保本止损设置成功: {stopOrderRequest.Symbol} {stopOrderRequest.Side} {stopOrderRequest.Quantity:F6} @ {stopOrderRequest.StopPrice:F4}");
                }
                else
                {
                    StatusMessage = "⚠️ 加仓成功但保本止损设置失败，请手动设置";
                    _logger.LogWarning("保本止损设置失败");
                }

                // 14. 最终数据刷新
                await RefreshDataAsync();

                StatusMessage = $"✅ 一键保本加仓完成 - 浮盈: {currentPosition.UnrealizedProfit:F2}U → 保本价: {newStopPrice:F4}";
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "一键保本加仓操作失败");
                StatusMessage = $"❌ 一键保本加仓失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        #endregion

        #region 计算功能命令
        [RelayCommand]
        private async Task CalculateMaxRiskCapital()
        {
            try
            {
                if (AccountInfo == null)
                {
                    StatusMessage = "请先刷新账户信息";
                    RiskCapitalCalculationDetail = "❌ 缺少账户信息";
                    return;
                }

                if (SelectedAccount == null)
                {
                    StatusMessage = "请先选择账户配置";
                    RiskCapitalCalculationDetail = "❌ 缺少账户配置";
                    return;
                }

                if (string.IsNullOrEmpty(Symbol))
                {
                    StatusMessage = "请先选择交易合约";
                    RiskCapitalCalculationDetail = "❌ 缺少交易合约";
                    return;
                }

                // 记录当前正在计算的账户信息
                _logger.LogInformation($"开始计算风险金: 账户={SelectedAccount.Name}, 合约={Symbol}, 持仓数={Positions.Count}, 委托数={Orders.Count}");

                // 获取账户权益
                var accountEquity = AccountInfo.TotalEquity; // 账户权益
                var riskTimes = SelectedAccount.RiskCapitalTimes; // 风险机会次数
                
                if (riskTimes <= 0)
                {
                    StatusMessage = "风险机会次数必须大于0";
                    RiskCapitalCalculationDetail = "❌ 风险机会次数无效";
                    return;
                }

                // 1. 计算标准风险金
                var standardRiskCapital = accountEquity / riskTimes;
                
                // 2. 计算当前合约的盈亏风险金
                decimal totalProfitLossRiskCapital = 0;
                var profitLossDetailMessage = "";
                
                // 获取当前合约的持仓
                var currentPositions = Positions.Where(p => 
                    p.Symbol == Symbol && Math.Abs(p.PositionAmt) > 0).ToList();
                
                // 获取当前合约的委托订单（包括普通委托和止损委托）
                var currentOrders = Orders.Where(o => o.Symbol == Symbol).ToList();
                var stopOrders = currentOrders.Where(o => 
                    o.Type == "STOP_MARKET" && o.ReduceOnly).ToList();
                var normalOrders = currentOrders.Where(o => 
                    o.Type != "STOP_MARKET" && !o.ReduceOnly).ToList();
                
                if (currentPositions.Any() || normalOrders.Any())
                {
                    profitLossDetailMessage = $"\n🔄 {Symbol} 盈亏风险金计算:";
                    _logger.LogInformation($"开始计算 {Symbol} 盈亏风险金: 持仓{currentPositions.Count}个, 普通委托{normalOrders.Count}个, 止损委托{stopOrders.Count}个");
                    
                    // 收集所有需要计算风险的仓位（持仓 + 委托）
                    var allPositions = new List<(decimal quantity, decimal entryPrice, string side, string source)>();
                    
                    // 添加现有持仓
                    foreach (var position in currentPositions)
                    {
                        allPositions.Add((
                            Math.Abs(position.PositionAmt),
                            position.EntryPrice,
                            position.PositionAmt > 0 ? "BUY" : "SELL",
                            $"持仓"
                        ));
                    }
                    
                    // 添加委托订单
                    foreach (var order in normalOrders)
                    {
                        allPositions.Add((
                            order.OrigQty,
                            order.Price > 0 ? order.Price : LatestPrice,
                            order.Side,
                            $"委托#{order.OrderId}"
                        ));
                    }
                    
                    if (allPositions.Any() && stopOrders.Any())
                    {
                        // 按照止损触发顺序排序止损委托
                        // 多头止损：止损价从高到低排序（先触发价格高的）
                        // 空头止损：止损价从低到高排序（先触发价格低的）
                        var sortedStopOrders = stopOrders.OrderBy(o => 
                        {
                            // 根据止损单的方向确定排序方式
                            if (o.Side == "SELL") // 多头止损（卖出平仓）
                                return -o.StopPrice; // 价格高的排前面
                            else // 空头止损（买入平仓）
                                return o.StopPrice; // 价格低的排前面
                        }).ToList();
                        
                        profitLossDetailMessage += $"\n📋 排序后的止损委托:";
                        foreach (var stop in sortedStopOrders)
                        {
                            var direction = stop.Side == "SELL" ? "多头止损" : "空头止损";
                            profitLossDetailMessage += $"\n  🛑 {direction} @{stop.StopPrice:F4}, 数量:{stop.OrigQty:F6}";
                        }
                        
                        // 将仓位数量与止损委托匹配，计算盈亏
                        decimal remainingQuantity = allPositions.Sum(p => p.quantity);
                        profitLossDetailMessage += $"\n💼 总仓位数量: {remainingQuantity:F6}";
                        
                        foreach (var stop in sortedStopOrders)
                        {
                            if (remainingQuantity <= 0) break;
                            
                            // 确定这个止损单能覆盖多少数量
                            var coverQuantity = Math.Min(stop.OrigQty, remainingQuantity);
                            
                            // 计算这部分数量的平均进场价
                            decimal avgEntryPrice = 0;
                            decimal coveredQuantity = 0;
                            
                            foreach (var position in allPositions)
                            {
                                if (coveredQuantity >= coverQuantity) break;
                                
                                var takeQuantity = Math.Min(position.quantity, coverQuantity - coveredQuantity);
                                avgEntryPrice += position.entryPrice * takeQuantity;
                                coveredQuantity += takeQuantity;
                            }
                            
                            if (coveredQuantity > 0)
                            {
                                avgEntryPrice /= coveredQuantity;
                                
                                // 计算盈亏
                                decimal profitLoss = 0;
                                if (stop.Side == "SELL") // 多头止损
                                {
                                    // 盈亏 = (止损价 - 进场价) * 数量
                                    profitLoss = (stop.StopPrice - avgEntryPrice) * coveredQuantity;
                                }
                                else // 空头止损
                                {
                                    // 盈亏 = (进场价 - 止损价) * 数量
                                    profitLoss = (avgEntryPrice - stop.StopPrice) * coveredQuantity;
                                }
                                
                                totalProfitLossRiskCapital += profitLoss;
                                
                                var direction = stop.Side == "SELL" ? "多头" : "空头";
                                var profitLossStr = profitLoss >= 0 ? $"+{profitLoss:F2}" : $"{profitLoss:F2}";
                                profitLossDetailMessage += $"\n  💰 {direction}止损 @{stop.StopPrice:F4}: ({stop.StopPrice:F4} - {avgEntryPrice:F4}) × {coveredQuantity:F6} = {profitLossStr}U";
                                
                                remainingQuantity -= coveredQuantity;
                                
                                _logger.LogDebug($"盈亏计算: {direction}止损, 止损价={stop.StopPrice:F4}, 平均进场价={avgEntryPrice:F4}, 数量={coveredQuantity:F6}, 盈亏={profitLoss:F2}U");
                            }
                        }
                        
                        if (remainingQuantity > 0)
                        {
                            profitLossDetailMessage += $"\n⚠️ 剩余未匹配数量: {remainingQuantity:F6} (无对应止损单)";
                        }
                        
                        profitLossDetailMessage += $"\n📊 总盈亏风险金: {totalProfitLossRiskCapital:F2}U";
                    }
                    else if (!stopOrders.Any())
                    {
                        profitLossDetailMessage += $"\n⚠️ 无止损委托，盈亏风险金为0";
                    }
                    else
                    {
                        profitLossDetailMessage += $"\n⚠️ 无持仓或委托，盈亏风险金为0";
                    }
                }
                else
                {
                    profitLossDetailMessage = $"\n🔄 {Symbol} 盈亏风险金计算: 无持仓和委托，盈亏风险金为0U";
                }

                // 3. 计算最终可用风险金 = 标准风险金 + 盈亏风险金
                var totalRiskCapital = standardRiskCapital + totalProfitLossRiskCapital;
                var result = Math.Ceiling(totalRiskCapital); // 向上取整

                // 更新可用风险金属性
                AvailableRiskCapital = result;
                
                // 构建详细的计算过程
                var profitLossSign = totalProfitLossRiskCapital >= 0 ? "+" : "";
                var calculationDetail = $"💰 风险金计算公式: 标准风险金 + 浮盈风险金" +
                                       $"\n📈 标准风险金: {accountEquity:F2}U (账户权益) ÷ {riskTimes} (风险次数) = {standardRiskCapital:F2}U" +
                                       $"\n📊 浮盈风险金: {profitLossSign}{totalProfitLossRiskCapital:F2}U" +
                                       profitLossDetailMessage +
                                       $"\n✅ 最终可用风险金: {standardRiskCapital:F2}U {profitLossSign} {Math.Abs(totalProfitLossRiskCapital):F2}U = {totalRiskCapital:F2}U → {result:F0}U (向上取整)";
                
                RiskCapitalCalculationDetail = calculationDetail;
                
                var finalProfitLossStr = totalProfitLossRiskCapital >= 0 ? $"+{totalProfitLossRiskCapital:F2}" : $"{totalProfitLossRiskCapital:F2}";
                StatusMessage = $"✅ 可用风险金: {result:F0}U (标准{standardRiskCapital:F2} + 浮盈{finalProfitLossStr})";
                _logger.LogInformation($"计算可用风险金: {result:F0}U，标准风险金{standardRiskCapital:F2} + 盈亏风险金{totalProfitLossRiskCapital:F2}");
                
                // 🚀 自动填写止损金额并执行以损定量
                await AutoSetStopLossAmountAndCalculateQuantityAsync(result);
            }
            catch (Exception ex)
            {
                StatusMessage = $"计算失败: {ex.Message}";
                RiskCapitalCalculationDetail = $"❌ 计算失败: {ex.Message}";
                _logger.LogError(ex, "计算可用风险金失败");
            }
        }

        [RelayCommand]
        private async Task CalculateQuantityFromLossAsync()
        {
            try
            {
                if (StopLossAmount <= 0 || LatestPrice <= 0 || StopLossRatio <= 0)
                {
                    StatusMessage = "请先设置止损金额、当前价格和止损比例";
                    return;
                }

                var quantity = await _calculationService.CalculateQuantityFromLossAsync(
                    StopLossAmount, LatestPrice, StopLossRatio, Symbol);

                if (quantity > 0)
                {
                    Quantity = quantity;
                    StatusMessage = $"计算数量: {quantity:F6} (基于止损{StopLossAmount}U，比例{StopLossRatio}%)";
                    _logger.LogInformation($"以损定量计算成功: {quantity:F6}");
                }
                else
                {
                    StatusMessage = "计算数量失败，请检查参数";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"计算失败: {ex.Message}";
                _logger.LogError(ex, "以损定量计算失败");
            }
        }

        [RelayCommand]
        private void CalculateStopLossPrice()
        {
            try
            {
                if (LatestPrice <= 0 || StopLossRatio <= 0)
                {
                    StatusMessage = "请先设置当前价格和止损比例";
                    return;
                }

                var stopPrice = _calculationService.CalculateStopLossPrice(LatestPrice, StopLossRatio, Side);
                StopLossPrice = stopPrice;
                
                StatusMessage = $"止损价: {stopPrice:F4} ({Side}方向，{StopLossRatio}%止损)";
                _logger.LogInformation($"计算止损价: {stopPrice:F4}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"计算失败: {ex.Message}";
                _logger.LogError(ex, "计算止损价失败");
            }
        }

        [RelayCommand]
        private void SetLeverage(object parameter)
        {
            if (parameter is string leverageStr && int.TryParse(leverageStr, out int leverageValue))
            {
                var oldLeverage = Leverage;
                Leverage = leverageValue;
                _logger.LogDebug($"杠杆设置为: {leverageValue}x");
                SaveTradingSettings();

                // 如果有选中的合约，且杠杆从低调到高，尝试自动释放保证金
                if (!string.IsNullOrEmpty(Symbol) && leverageValue > oldLeverage)
                {
                    _ = Task.Run(async () => await AutoReleaseExcessMarginAsync(Symbol, leverageValue, oldLeverage));
                }
            }
        }

        /// <summary>
        /// 自动释放多余保证金
        /// </summary>
        private async Task AutoReleaseExcessMarginAsync(string symbol, int newLeverage, int oldLeverage)
        {
            try
            {
                _logger.LogInformation($"🎯 检测到杠杆调整: {symbol} {oldLeverage}x → {newLeverage}x，检查是否需要释放保证金");

                // 获取当前持仓信息
                var positions = await _binanceService.GetPositionsAsync();
                var targetPosition = positions.FirstOrDefault(p => 
                    p.Symbol == symbol && 
                    Math.Abs(p.PositionAmt) > 0 && 
                    p.MarginType == "ISOLATED"); // 只处理逐仓模式

                if (targetPosition == null)
                {
                    _logger.LogInformation($"💡 {symbol} 没有逐仓持仓，无需释放保证金");
                    return;
                }

                // 应用新杠杆到币安
                _logger.LogInformation($"🎚️ 正在设置 {symbol} 杠杆为 {newLeverage}x...");
                var leverageSetSuccess = await _binanceService.SetLeverageAsync(symbol, newLeverage);
                
                if (!leverageSetSuccess)
                {
                    _logger.LogWarning($"❌ 设置杠杆失败，跳过保证金释放");
                    return;
                }

                _logger.LogInformation($"✅ 杠杆设置成功，开始计算保证金释放");

                // 计算新杠杆下所需的保证金
                var positionValue = Math.Abs(targetPosition.PositionAmt) * targetPosition.MarkPrice;
                var requiredMarginNewLeverage = positionValue / newLeverage;
                var requiredMarginOldLeverage = positionValue / oldLeverage;
                var currentMargin = targetPosition.IsolatedMargin;

                _logger.LogInformation($"📊 保证金计算:");
                _logger.LogInformation($"   持仓价值: {positionValue:F2} USDT");
                _logger.LogInformation($"   当前保证金: {currentMargin:F2} USDT");
                _logger.LogInformation($"   旧杠杆({oldLeverage}x)所需: {requiredMarginOldLeverage:F2} USDT");
                _logger.LogInformation($"   新杠杆({newLeverage}x)所需: {requiredMarginNewLeverage:F2} USDT");

                // 计算可释放的保证金（保留一些缓冲）
                var excessMargin = currentMargin - requiredMarginNewLeverage;
                var bufferRatio = 0.1m; // 保留10%缓冲
                var releasableMargin = excessMargin * (1 - bufferRatio);

                if (releasableMargin <= 1) // 少于1 USDT不值得释放
                {
                    _logger.LogInformation($"💡 可释放保证金太少 ({releasableMargin:F2} USDT)，跳过释放");
                    return;
                }

                _logger.LogInformation($"💰 可释放保证金: {releasableMargin:F2} USDT (已扣除{bufferRatio:P0}缓冲)");

                // 确定持仓方向
                var positionSide = "BOTH"; // 默认单向持仓
                var dualSidePosition = await _binanceService.GetPositionModeAsync();
                if (dualSidePosition)
                {
                    positionSide = targetPosition.PositionSideString;
                }

                // 释放保证金 (type=2表示减少保证金)
                _logger.LogInformation($"🔄 正在释放 {symbol} 保证金 {releasableMargin:F2} USDT...");
                var releaseSuccess = await _binanceService.AdjustIsolatedMarginAsync(
                    symbol, positionSide, releasableMargin, 2);

                if (releaseSuccess)
                {
                    _logger.LogInformation($"✅ 成功释放保证金: {releasableMargin:F2} USDT");
                    
                    // 在UI线程中更新状态消息
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        StatusMessage = $"🎉 杠杆调整完成，已自动释放 {releasableMargin:F0}U 保证金";
                    });
                    
                    // 刷新数据以显示最新的保证金状态
                    await RefreshDataAsync();
                }
                else
                {
                    _logger.LogWarning($"❌ 释放保证金失败");
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        StatusMessage = $"⚠️ 杠杆调整成功，但保证金释放失败";
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"自动释放保证金异常: {symbol}");
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    StatusMessage = $"❌ 保证金释放异常: {ex.Message}";
                });
            }
        }

        /// <summary>
        /// 手动释放多余保证金命令
        /// </summary>
        [RelayCommand]
        private async Task ReleaseExcessMarginAsync()
        {
            if (string.IsNullOrEmpty(Symbol))
            {
                StatusMessage = "请先选择合约";
                return;
            }

            if (IsLoading)
            {
                StatusMessage = "正在处理中，请稍候...";
                return;
            }

            IsLoading = true;
            try
            {
                StatusMessage = "正在检查保证金释放...";
                
                // 获取当前持仓信息
                var positions = await _binanceService.GetPositionsAsync();
                var isolatedPositions = positions.Where(p => 
                    p.Symbol == Symbol && 
                    Math.Abs(p.PositionAmt) > 0 && 
                    p.MarginType == "ISOLATED").ToList();

                if (!isolatedPositions.Any())
                {
                    StatusMessage = $"💡 {Symbol} 没有逐仓持仓，无需释放保证金";
                    return;
                }

                int releasedCount = 0;
                decimal totalReleased = 0;

                foreach (var position in isolatedPositions)
                {
                    // 计算当前杠杆下所需的保证金
                    var positionValue = Math.Abs(position.PositionAmt) * position.MarkPrice;
                    var requiredMargin = positionValue / position.Leverage;
                    var currentMargin = position.IsolatedMargin;

                    _logger.LogInformation($"📊 分析持仓: {position.Symbol} {position.PositionSideString}");
                    _logger.LogInformation($"   持仓价值: {positionValue:F2} USDT");
                    _logger.LogInformation($"   当前保证金: {currentMargin:F2} USDT");
                    _logger.LogInformation($"   所需保证金: {requiredMargin:F2} USDT");

                    // 计算可释放的保证金（保留一些缓冲）
                    var excessMargin = currentMargin - requiredMargin;
                    var bufferRatio = 0.15m; // 手动释放时保留更多缓冲(15%)
                    var releasableMargin = excessMargin * (1 - bufferRatio);

                    if (releasableMargin <= 2) // 少于2 USDT不值得释放
                    {
                        _logger.LogInformation($"💡 {position.Symbol} 可释放保证金太少 ({releasableMargin:F2} USDT)，跳过");
                        continue;
                    }

                    // 确定持仓方向
                    var positionSide = "BOTH"; // 默认单向持仓
                    var dualSidePosition = await _binanceService.GetPositionModeAsync();
                    if (dualSidePosition)
                    {
                        positionSide = position.PositionSideString;
                    }

                    // 释放保证金 (type=2表示减少保证金)
                    _logger.LogInformation($"🔄 释放 {position.Symbol} 保证金 {releasableMargin:F2} USDT...");
                    var releaseSuccess = await _binanceService.AdjustIsolatedMarginAsync(
                        position.Symbol, positionSide, releasableMargin, 2);

                    if (releaseSuccess)
                    {
                        releasedCount++;
                        totalReleased += releasableMargin;
                        _logger.LogInformation($"✅ 成功释放 {position.Symbol} 保证金: {releasableMargin:F2} USDT");
                    }
                    else
                    {
                        _logger.LogWarning($"❌ 释放 {position.Symbol} 保证金失败");
                    }

                    // 添加短暂延迟避免API频率限制
                    await Task.Delay(200);
                }

                if (releasedCount > 0)
                {
                    StatusMessage = $"🎉 成功释放 {releasedCount} 个持仓的保证金，共 {totalReleased:F0}U";
                    
                    // 刷新数据以显示最新的保证金状态
                    await RefreshDataAsync();
                }
                else
                {
                    StatusMessage = "💡 没有可释放的多余保证金";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ 释放保证金异常: {ex.Message}";
                _logger.LogError(ex, "手动释放保证金异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void SetStopLossRatio(object parameter)
        {
            if (parameter is string ratioStr && decimal.TryParse(ratioStr, out decimal ratioValue))
            {
                StopLossRatio = ratioValue;
                _logger.LogDebug($"止损比例设置为: {ratioValue}%");
                
                // 自动计算止损价格
                if (LatestPrice > 0)
                {
                    StopLossPrice = _calculationService.CalculateStopLossPrice(LatestPrice, StopLossRatio, Side);
                }
                
                SaveTradingSettings();
            }
        }

        [RelayCommand]
        private void SetStopLossAmountRatio(object parameter)
        {
            try
            {
                if (parameter is string ratioStr && decimal.TryParse(ratioStr, out decimal ratio))
                {
                    if (AvailableRiskCapital <= 0)
                    {
                        StatusMessage = "请先计算可用风险金";
                        return;
                    }

                    var amount = AvailableRiskCapital * (ratio / 100);
                    StopLossAmount = Math.Ceiling(amount); // 向上取整
                    
                    StatusMessage = $"止损金额已设置为可用风险金的{ratio}%: {StopLossAmount:F0}U";
                    _logger.LogInformation($"设置止损金额比例: {ratio}%, 可用风险金: {AvailableRiskCapital:F0}U, 止损金额: {StopLossAmount:F0}U");
                }
                else
                {
                    StatusMessage = "无效的比例参数";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"设置止损金额失败: {ex.Message}";
                _logger.LogError(ex, "设置止损金额比例失败");
            }
        }

        /// <summary>
        /// 自动设置止损金额并执行以损定量计算
        /// </summary>
        private async Task AutoSetStopLossAmountAndCalculateQuantityAsync(decimal riskCapital)
        {
            try
            {
                // 自动设置止损金额为可用风险金的100%
                StopLossAmount = riskCapital;
                
                StatusMessage = $"🎯 自动设置止损金额: {riskCapital:F0}U，正在计算交易数量...";
                _logger.LogInformation($"自动设置止损金额: {riskCapital:F0}U");
                
                // 自动执行以损定量计算
                await CalculateQuantityFromLossAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动设置止损金额和计算数量失败");
            }
        }
        #endregion

        #region 私有辅助方法
        private string NormalizeSymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return symbol;

            var upperSymbol = symbol.ToUpper();
            
            // 如果已经包含USDT，直接返回
            if (upperSymbol.EndsWith("USDT"))
                return upperSymbol;

            // 预定义的币种列表
            var knownCoins = new[]
            {
                "BTC", "ETH", "BNB", "ADA", "DOT", "XRP", "LTC", "BCH",
                "LINK", "SOL", "DOGE", "MATIC", "AVAX", "UNI", "ATOM"
            };

            // 如果是已知币种或长度大于等于2，添加USDT后缀
            if (knownCoins.Contains(upperSymbol) || upperSymbol.Length >= 2)
            {
                return upperSymbol + "USDT";
            }

            return upperSymbol;
        }

        private void AddToRecentContracts(string symbol)
        {
            if (string.IsNullOrEmpty(symbol) || !symbol.Contains("USDT"))
                return;

            try
            {
                var contracts = _recentContractsService.AddRecentContract(symbol, RecentContracts);
                RecentContracts.Clear();
                foreach (var contract in contracts)
                {
                    RecentContracts.Add(contract);
                }
                SaveRecentContracts();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加最近合约失败");
            }
        }
        #endregion

        #region 属性变化处理
        partial void OnLatestPriceChanged(decimal value)
        {
            // 如果当前是限价单且有最新价格，自动更新价格输入框
            if (value > 0 && OrderType == "LIMIT")
            {
                Price = value;
                _logger.LogDebug($"最新价格更新，限价单价格自动更新为: {Price}");
            }

            // 自动重新计算止损价
            if (value > 0 && StopLossRatio > 0)
            {
                var newStopLossPrice = _calculationService.CalculateStopLossPrice(value, StopLossRatio, Side);
                if (Math.Abs(newStopLossPrice - StopLossPrice) > 0.01m) // 避免微小变化的频繁更新
                {
                    StopLossPrice = newStopLossPrice;
                }
            }

            // 重新验证限价单价格（当最新价格变化时）
            if (OrderType == "LIMIT")
            {
                ValidateLimitOrderPrice();
            }

            // 通知可下单状态变化
            OnPropertyChanged(nameof(CanPlaceOrder));
        }

        partial void OnSymbolChanged(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            // 自动补齐USDT后缀
            var normalizedSymbol = NormalizeSymbol(value);
            if (normalizedSymbol != value)
            {
                Symbol = normalizedSymbol;
                return;
            }

            // 🔄 切换品种时重置交易数量为0，避免错误
            Quantity = 0;
            _logger.LogInformation($"切换品种到 {value}，交易数量已重置为0");

            // 添加到最近合约
            AddToRecentContracts(value);

            // 自动获取最新价格
            _ = Task.Run(async () =>
            {
                try
                {
                    await UpdateLatestPriceAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"自动获取 {value} 价格失败");
                }
            });

            // 更新条件单信息
            UpdateConditionalOrderInfo();

            // 通知可下单状态变化
            OnPropertyChanged(nameof(CanPlaceOrder));
        }

        partial void OnQuantityChanged(decimal value)
        {
            OnPropertyChanged(nameof(CanPlaceOrder));
        }

        partial void OnSelectedAccountChanged(AccountConfig? value)
        {
            if (value != null)
            {
                // 🔄 切换账户时重置交易数量为0，避免错误
                Quantity = 0;
                _logger.LogInformation($"切换账户到 {value.Name}，交易数量已重置为0");

                // 清空之前账户的条件单和订单数据
                ConditionalOrders.Clear();
                Positions.Clear();
                Orders.Clear();
                FilteredOrders.Clear();
                ReduceOnlyOrders.Clear();
                OnPropertyChanged(nameof(HasNoConditionalOrders));
                OnPropertyChanged(nameof(HasSelectedConditionalOrders));
                OnPropertyChanged(nameof(SelectedConditionalOrderCount));
                _logger.LogInformation("已清空所有数据，准备加载新账户数据");

                _binanceService.SetAccount(value);
                
                // 立即刷新数据，确保界面同步更新
                _ = Task.Run(async () => 
                {
                    try
                    {
                        await RefreshDataAsync();
                        await CalculateMaxRiskCapital();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "账户切换后数据刷新失败");
                        StatusMessage = $"账户切换后数据刷新失败: {ex.Message}";
                    }
                });
                
                StartTimers();
                OnPropertyChanged(nameof(CanPlaceOrder));
            }
            else
            {
                StopTimers();
                
                // 清空所有数据
                AvailableRiskCapital = 0;
                Quantity = 0;
                ConditionalOrders.Clear();
                Positions.Clear();
                Orders.Clear();
                FilteredOrders.Clear();
                ReduceOnlyOrders.Clear();
                AccountInfo = null;
                
                // 通知UI更新
                OnPropertyChanged(nameof(HasNoConditionalOrders));
                OnPropertyChanged(nameof(HasSelectedConditionalOrders));
                OnPropertyChanged(nameof(SelectedConditionalOrderCount));
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));
                OnPropertyChanged(nameof(SelectedPositions));
                OnPropertyChanged(nameof(HasSelectedPositions));
                OnPropertyChanged(nameof(SelectedPositionCount));
                
                _logger.LogInformation("清空账户选择，所有数据已重置");
            }
        }

        partial void OnStopLossRatioChanged(decimal value)
        {
            if (value > 0 && LatestPrice > 0)
            {
                StopLossPrice = _calculationService.CalculateStopLossPrice(LatestPrice, value, Side);
            }
            SaveTradingSettings();
        }

        partial void OnSideChanged(string value)
        {
            // 方向改变时重新计算止损价
            if (StopLossRatio > 0 && LatestPrice > 0)
            {
                StopLossPrice = _calculationService.CalculateStopLossPrice(LatestPrice, StopLossRatio, value);
            }
            SaveTradingSettings();
        }

        partial void OnStopLossAmountChanged(decimal value)
        {
            // 🚀 止损金额变化时自动执行以损定量计算
            if (value > 0 && LatestPrice > 0 && StopLossRatio > 0 && !string.IsNullOrEmpty(Symbol))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await CalculateQuantityFromLossAsync();
                        _logger.LogDebug($"止损金额变更为{value:F0}U，自动执行以损定量计算");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "止损金额变化自动计算数量失败");
                    }
                });
            }
        }

        partial void OnLeverageChanged(int value)
        {
            SaveTradingSettings();
        }

        partial void OnMarginTypeChanged(string value)
        {
            SaveTradingSettings();
        }

        partial void OnOrderTypeChanged(string value)
        {
            OnPropertyChanged(nameof(IsLimitConditionalOrder));
            OnPropertyChanged(nameof(IsConditionalOrderVisible));
            
            // 当订单类型变化时重新验证价格
            ValidateLimitOrderPrice();
            
            SaveTradingSettings();
        }

        partial void OnPriceChanged(decimal value)
        {
            ValidateLimitOrderPrice();
        }
        
        /// <summary>
        /// 验证限价单价格是否在合理范围内
        /// </summary>
        private void ValidateLimitOrderPrice()
        {
            if (OrderType != "LIMIT" || LatestPrice <= 0 || Price <= 0)
            {
                IsPriceValid = true;
                PriceWarningMessage = "";
                return;
            }

            // 计算价格偏离百分比
            var deviationPercentage = Math.Abs((Price - LatestPrice) / LatestPrice) * 100;
            var maxDeviationPercentage = 30m; // 最大偏离30%

            if (deviationPercentage > maxDeviationPercentage)
            {
                IsPriceValid = false;
                var direction = Price > LatestPrice ? "高于" : "低于";
                PriceWarningMessage = $"⚠️ 价格{direction}市价{deviationPercentage:F1}%，超过30%限制";
                _logger.LogWarning($"限价单价格偏离过大: 设置价格={Price}, 当前价格={LatestPrice}, 偏离={deviationPercentage:F1}%");
            }
            else if (deviationPercentage > 10m) // 偏离超过10%时给出提醒
            {
                IsPriceValid = true;
                var direction = Price > LatestPrice ? "高于" : "低于";
                PriceWarningMessage = $"💡 价格{direction}市价{deviationPercentage:F1}%，请确认无误";
            }
            else
            {
                IsPriceValid = true;
                PriceWarningMessage = "";
            }
            
            // 通知下单按钮状态可能发生变化
            OnPropertyChanged(nameof(CanPlaceOrder));
        }
        #endregion

        #region 更新价格命令
        [RelayCommand]
        private async Task UpdateLatestPriceAsync()
        {
            if (string.IsNullOrEmpty(Symbol))
                return;

            try
            {
                var price = await _binanceService.GetLatestPriceAsync(Symbol);
                if (price > 0)
                {
                    LatestPrice = price;
                    StatusMessage = $"{Symbol} 价格已更新: {price}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"价格更新失败: {ex.Message}";
                _logger.LogError(ex, "更新价格失败");
            }
        }
        #endregion
    }
} 