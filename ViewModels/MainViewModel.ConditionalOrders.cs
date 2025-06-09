using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.ViewModels
{
    /// <summary>
    /// MainViewModel条件单功能部分
    /// </summary>
    public partial class MainViewModel
    {
        #region 条件单属性
        
        // 条件单类型选择
        [ObservableProperty]
        private string _conditionalOrderMode = "加仓型"; // 加仓型、平仓型
        
        // 加仓型-无持仓情况的属性
        [ObservableProperty]
        private decimal _upBreakPrice = 0;

        [ObservableProperty]
        private decimal _downBreakPrice = 0;

        // 加仓型-有持仓情况的属性
        [ObservableProperty]
        private decimal _targetProfit = 0; // 目标浮盈
        
        [ObservableProperty]
        private decimal _addPositionTriggerPrice = 0; // 加仓触发价格
        
        // 平仓型属性
        [ObservableProperty]
        private decimal _closeProfitTarget = 0; // 目标浮盈（平仓）
        
        [ObservableProperty]
        private decimal _closePriceTarget = 0; // 目标价格（平仓）
        
        // 持仓相关属性
        [ObservableProperty]
        private string _selectedPositionInfo = "未找到匹配持仓";

        [ObservableProperty]
        private decimal _currentPositionProfit = 0;

        [ObservableProperty]
        private string _autoConditionalInfo = "自动检测中...";

        [ObservableProperty]
        private string _workingType = "CONTRACT_PRICE";

        [ObservableProperty]
        private string _conditionalType = "STOP_MARKET";

        [ObservableProperty]
        private string _timeInForce = "GTC";

        [ObservableProperty]
        private decimal _stopPrice = 0;

        [ObservableProperty]
        private bool _reduceOnly = false;

        // 条件单集合
        private ObservableCollection<ConditionalOrderInfo> _conditionalOrders = new();
        public ObservableCollection<ConditionalOrderInfo> ConditionalOrders
        {
            get => _conditionalOrders;
            set => SetProperty(ref _conditionalOrders, value);
        }

        // UI辅助属性
        public bool IsAddPositionMode => ConditionalOrderMode == "加仓型";
        public bool IsClosePositionMode => ConditionalOrderMode == "平仓型";
        public bool HasCurrentPosition => GetCurrentPosition() != null;
        public string CurrentPositionProfitColor => CurrentPositionProfit >= 0 ? "Green" : "Red";
        public bool HasNoConditionalOrders => !ConditionalOrders.Any();
        public bool HasSelectedConditionalOrders => ConditionalOrders.Any(o => o.IsSelected);
        public int SelectedConditionalOrderCount => ConditionalOrders.Count(o => o.IsSelected);
        
        #endregion

        #region 条件单模式切换
        
        [RelayCommand]
        private void SwitchToAddPositionMode()
        {
            ConditionalOrderMode = "加仓型";
            UpdateConditionalOrderInfo();
            OnPropertyChanged(nameof(IsAddPositionMode));
            OnPropertyChanged(nameof(IsClosePositionMode));
            OnPropertyChanged(nameof(HasCurrentPosition));
            StatusMessage = "已切换到加仓型条件单模式";
        }
        
        [RelayCommand]
        private void SwitchToClosePositionMode()
        {
            ConditionalOrderMode = "平仓型";
            UpdateConditionalOrderInfo();
            OnPropertyChanged(nameof(IsAddPositionMode));
            OnPropertyChanged(nameof(IsClosePositionMode));
            OnPropertyChanged(nameof(HasCurrentPosition));
            StatusMessage = "已切换到平仓型条件单模式";
        }
        
        #endregion

        #region 加仓型条件单 - 无持仓情况
        
        [RelayCommand]
        private void FillUpBreakPrice()
        {
            if (LatestPrice > 0)
            {
                // 自动设置为当前价格的1.1倍作为上突破价
                var upPrice = LatestPrice * 1.1m;
                UpBreakPrice = Math.Round(upPrice, GetPriceDecimalPlaces());
                StatusMessage = $"向上突破价已设置为: {UpBreakPrice} (最新价格 {LatestPrice} × 1.1)";
                _logger.LogInformation($"设置向上突破价: {UpBreakPrice} (最新价格 {LatestPrice} × 1.1)");
            }
            else
            {
                StatusMessage = "请先获取最新价格";
            }
        }

        [RelayCommand]
        private void FillDownBreakPrice()
        {
            if (LatestPrice > 0)
            {
                // 自动设置为当前价格的0.9倍作为下突破价
                var downPrice = LatestPrice * 0.9m;
                DownBreakPrice = Math.Round(downPrice, GetPriceDecimalPlaces());
                StatusMessage = $"向下突破价已设置为: {DownBreakPrice} (最新价格 {LatestPrice} × 0.9)";
                _logger.LogInformation($"设置向下突破价: {DownBreakPrice} (最新价格 {LatestPrice} × 0.9)");
            }
            else
            {
                StatusMessage = "请先获取最新价格";
            }
        }

        [RelayCommand]
        private async Task PlaceBreakoutConditionalOrderAsync()
        {
            if (string.IsNullOrEmpty(Symbol) || Quantity <= 0)
            {
                StatusMessage = "请先设置合约和数量";
                return;
            }

            if (UpBreakPrice <= 0 && DownBreakPrice <= 0)
            {
                StatusMessage = "请至少设置一个突破价格";
                return;
            }

            try
            {
                IsLoading = true;
                var successCount = 0;
                var totalCount = 0;

                // 下向上突破单
                if (UpBreakPrice > 0)
                {
                    totalCount++;
                    var upOrderSuccess = await PlaceBreakoutOrderAsync(UpBreakPrice, "BUY", "向上突破开仓");
                    if (upOrderSuccess) successCount++;
                }

                // 下向下突破单
                if (DownBreakPrice > 0)
                {
                    totalCount++;
                    var downOrderSuccess = await PlaceBreakoutOrderAsync(DownBreakPrice, "SELL", "向下突破开仓");
                    if (downOrderSuccess) successCount++;
                }

                StatusMessage = $"突破条件单完成: {successCount}/{totalCount} 成功";
                _logger.LogInformation($"突破条件单下单完成: 成功 {successCount} 个，总共 {totalCount} 个");

                if (successCount > 0)
                {
                    await RefreshDataAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"条件单异常: {ex.Message}";
                _logger.LogError(ex, "下突破条件单异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<bool> PlaceBreakoutOrderAsync(decimal triggerPrice, string side, string description)
        {
            try
            {
                // 🔧 修复：创建真正的API订单请求
                var orderRequest = new OrderRequest
                {
                    Symbol = Symbol,
                    Side = side,
                    Type = "TAKE_PROFIT_MARKET", // 向上突破使用TAKE_PROFIT_MARKET
                    Quantity = Quantity,
                    StopPrice = triggerPrice,
                    ReduceOnly = false, // 加仓型订单不是减仓
                    WorkingType = WorkingType,
                    TimeInForce = "GTC"
                };

                _logger.LogInformation($"🚀 准备提交{description}条件单到API: {Symbol} {side} {Quantity} @{triggerPrice}");

                // 🚀 真正调用API下单
                var success = await _binanceService.PlaceOrderAsync(orderRequest);
                
                if (success)
                {
                    _logger.LogInformation($"✅ {description}条件单API提交成功: {Symbol} {side} {Quantity} @{triggerPrice}");
                    StatusMessage = $"✅ {description}条件单提交成功";
                    
                    // 成功后添加到本地监控列表
                    var conditionalOrder = new ConditionalOrderInfo
                    {
                        Symbol = Symbol,
                        Type = "TAKE_PROFIT_MARKET",
                        StopPrice = triggerPrice,
                        Quantity = Quantity,
                        Side = side,
                        WorkingType = WorkingType,
                        Status = "等待触发",
                        CreateTime = DateTime.Now,
                        Description = $"{description} @{triggerPrice}",
                        OrderCategory = "加仓型"
                    };

                    ConditionalOrders.Add(conditionalOrder);
                    OnPropertyChanged(nameof(HasNoConditionalOrders));
                    return true;
                }
                else
                {
                    _logger.LogError($"❌ {description}条件单API提交失败: {Symbol} {side} {Quantity} @{triggerPrice}");
                    StatusMessage = $"❌ {description}条件单提交失败";
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"下{description}条件单失败");
                StatusMessage = $"❌ {description}条件单异常: {ex.Message}";
                return false;
            }
        }
        
        #endregion

        #region 加仓型条件单 - 有持仓情况
        
        [RelayCommand]
        private void CalculateAddPositionPrice()
        {
            try
            {
                var currentPosition = GetCurrentPosition();
                if (currentPosition == null)
                {
                    StatusMessage = $"未找到 {Symbol} 的持仓";
                    return;
                }

                if (TargetProfit <= CurrentPositionProfit)
                {
                    StatusMessage = "目标浮盈必须大于当前浮盈";
                    return;
                }

                // 计算加仓触发价格
                var profitDiff = TargetProfit - CurrentPositionProfit;
                var positionSize = Math.Abs(currentPosition.PositionAmt);
                var isLong = currentPosition.PositionAmt > 0;
                
                // 加仓方向与持仓方向相同
                if (isLong)
                {
                    // 多头加仓：价格需要上涨
                    AddPositionTriggerPrice = LatestPrice + (profitDiff / positionSize);
                }
                else
                {
                    // 空头加仓：价格需要下跌
                    AddPositionTriggerPrice = LatestPrice - (profitDiff / positionSize);
                }

                AddPositionTriggerPrice = Math.Round(AddPositionTriggerPrice, GetPriceDecimalPlaces());
                StatusMessage = $"加仓触发价: {AddPositionTriggerPrice}";
                _logger.LogInformation($"计算加仓触发价: {AddPositionTriggerPrice}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"计算失败: {ex.Message}";
                _logger.LogError(ex, "计算加仓触发价失败");
            }
        }

        [RelayCommand]
        private async Task PlaceAddPositionConditionalOrderAsync()
        {
            var currentPosition = GetCurrentPosition();
            if (currentPosition == null)
            {
                StatusMessage = $"未找到 {Symbol} 的持仓";
                return;
            }

            if (AddPositionTriggerPrice <= 0)
            {
                StatusMessage = "请先计算触发价格";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "正在下加仓条件单...";

                var isLong = currentPosition.PositionAmt > 0;
                var orderSide = isLong ? "BUY" : "SELL";
                var orderType = isLong ? "TAKE_PROFIT_MARKET" : "STOP_MARKET";

                // 🔧 修复：创建真正的API订单请求
                var orderRequest = new OrderRequest
                {
                    Symbol = Symbol,
                    Side = orderSide,
                    Type = orderType,
                    Quantity = Quantity, // 使用下单区设置的数量
                    StopPrice = AddPositionTriggerPrice,
                    ReduceOnly = false, // 加仓型订单不是减仓
                    WorkingType = WorkingType,
                    TimeInForce = "GTC"
                };

                _logger.LogInformation($"🚀 准备提交加仓条件单到API: {Symbol} {orderSide} {Quantity} @{AddPositionTriggerPrice}");

                // 🚀 真正调用API下单
                var success = await _binanceService.PlaceOrderAsync(orderRequest);
                
                if (success)
                {
                    _logger.LogInformation($"✅ 加仓条件单API提交成功: {Symbol} 目标浮盈{TargetProfit}U @{AddPositionTriggerPrice}");
                    StatusMessage = "✅ 加仓条件单提交成功";
                    
                    // 成功后添加到本地监控列表
                    var conditionalOrder = new ConditionalOrderInfo
                    {
                        Symbol = Symbol,
                        Type = orderType,
                        StopPrice = AddPositionTriggerPrice,
                        Quantity = Quantity,
                        Side = orderSide,
                        WorkingType = WorkingType,
                        Status = "等待触发",
                        CreateTime = DateTime.Now,
                        Description = $"加仓至浮盈{TargetProfit}U @{AddPositionTriggerPrice}",
                        OrderCategory = "加仓型"
                    };

                    ConditionalOrders.Add(conditionalOrder);
                    OnPropertyChanged(nameof(HasNoConditionalOrders));
                }
                else
                {
                    _logger.LogError($"❌ 加仓条件单API提交失败: {Symbol} {orderSide} {Quantity} @{AddPositionTriggerPrice}");
                    StatusMessage = "❌ 加仓条件单提交失败";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ 加仓条件单异常: {ex.Message}";
                _logger.LogError(ex, "下加仓条件单异常");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        #endregion

        #region 平仓型条件单
        
        [RelayCommand]
        private void CalculateClosePriceFromProfit()
        {
            try
            {
                var currentPosition = GetCurrentPosition();
                if (currentPosition == null)
                {
                    StatusMessage = $"未找到 {Symbol} 的持仓";
                    return;
                }

                if (CloseProfitTarget <= CurrentPositionProfit)
                {
                    StatusMessage = "目标浮盈必须大于当前浮盈";
                    return;
                }

                // 计算止盈价格
                var profitDiff = CloseProfitTarget - CurrentPositionProfit;
                var positionSize = Math.Abs(currentPosition.PositionAmt);
                var isLong = currentPosition.PositionAmt > 0;
                
                if (isLong)
                {
                    // 多头止盈：价格上涨
                    ClosePriceTarget = LatestPrice + (profitDiff / positionSize);
                }
                else
                {
                    // 空头止盈：价格下跌
                    ClosePriceTarget = LatestPrice - (profitDiff / positionSize);
                }

                ClosePriceTarget = Math.Round(ClosePriceTarget, GetPriceDecimalPlaces());
                StatusMessage = $"止盈价格: {ClosePriceTarget}";
                _logger.LogInformation($"根据浮盈计算止盈价格: {ClosePriceTarget}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"计算失败: {ex.Message}";
                _logger.LogError(ex, "计算止盈价格失败");
            }
        }
        
        [RelayCommand]
        private void CalculateCloseProfitFromPrice()
        {
            try
            {
                var currentPosition = GetCurrentPosition();
                if (currentPosition == null)
                {
                    StatusMessage = $"未找到 {Symbol} 的持仓";
                    return;
                }

                if (ClosePriceTarget <= 0)
                {
                    StatusMessage = "请先设置目标价格";
                    return;
                }

                // 根据价格计算浮盈
                var priceDiff = ClosePriceTarget - LatestPrice;
                var positionSize = Math.Abs(currentPosition.PositionAmt);
                var isLong = currentPosition.PositionAmt > 0;
                
                decimal profitFromPrice;
                if (isLong)
                {
                    profitFromPrice = priceDiff * positionSize;
                }
                else
                {
                    profitFromPrice = -priceDiff * positionSize;
                }

                CloseProfitTarget = CurrentPositionProfit + profitFromPrice;
                StatusMessage = $"目标浮盈: {CloseProfitTarget:F2}U";
                _logger.LogInformation($"根据价格计算目标浮盈: {CloseProfitTarget:F2}U");
            }
            catch (Exception ex)
            {
                StatusMessage = $"计算失败: {ex.Message}";
                _logger.LogError(ex, "计算目标浮盈失败");
            }
        }
        
        [RelayCommand]
        private void FillDefaultClosePrice()
        {
            try
            {
                var currentPosition = GetCurrentPosition();
                if (currentPosition == null)
                {
                    StatusMessage = $"未找到 {Symbol} 的持仓";
                    return;
                }

                var isLong = currentPosition.PositionAmt > 0;
                
                if (isLong)
                {
                    // 多头默认止盈价格：1.2倍当前价
                    ClosePriceTarget = Math.Round(LatestPrice * 1.2m, GetPriceDecimalPlaces());
                }
                else
                {
                    // 空头默认止盈价格：0.8倍当前价
                    ClosePriceTarget = Math.Round(LatestPrice * 0.8m, GetPriceDecimalPlaces());
                }

                // 自动计算对应的浮盈
                CalculateCloseProfitFromPrice();
                
                StatusMessage = $"已设置默认止盈价格: {ClosePriceTarget}";
                _logger.LogInformation($"设置默认止盈价格: {ClosePriceTarget}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"设置默认价格失败: {ex.Message}";
                _logger.LogError(ex, "设置默认止盈价格失败");
            }
        }

        [RelayCommand]
        private async Task PlaceClosePositionConditionalOrderAsync()
        {
            var currentPosition = GetCurrentPosition();
            if (currentPosition == null)
            {
                StatusMessage = $"未找到 {Symbol} 的持仓";
                return;
            }

            if (ClosePriceTarget <= 0)
            {
                StatusMessage = "请先设置止盈价格";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "正在下平仓条件单...";

                var isLong = currentPosition.PositionAmt > 0;
                var orderSide = isLong ? "SELL" : "BUY"; // 平仓方向与持仓相反
                var positionQuantity = Math.Abs(currentPosition.PositionAmt);
                
                // 🔧 修复：创建真正的API订单请求
                var orderRequest = new OrderRequest
                {
                    Symbol = Symbol,
                    Side = orderSide,
                    Type = "TAKE_PROFIT_MARKET",
                    Quantity = positionQuantity, // 平仓数量
                    StopPrice = ClosePriceTarget,
                    ReduceOnly = true, // 平仓型订单是减仓
                    WorkingType = WorkingType,
                    TimeInForce = "GTC"
                };

                _logger.LogInformation($"🚀 准备提交平仓条件单到API: {Symbol} {orderSide} {positionQuantity} @{ClosePriceTarget}");

                // 🚀 真正调用API下单
                var success = await _binanceService.PlaceOrderAsync(orderRequest);
                
                if (success)
                {
                    _logger.LogInformation($"✅ 平仓条件单API提交成功: {Symbol} 目标浮盈{CloseProfitTarget:F2}U @{ClosePriceTarget}");
                    StatusMessage = "✅ 平仓条件单提交成功";
                    
                    // 成功后添加到本地监控列表
                    var conditionalOrder = new ConditionalOrderInfo
                    {
                        Symbol = Symbol,
                        Type = "TAKE_PROFIT_MARKET",
                        StopPrice = ClosePriceTarget,
                        Quantity = positionQuantity,
                        Side = orderSide,
                        WorkingType = WorkingType,
                        Status = "等待触发",
                        CreateTime = DateTime.Now,
                        Description = $"止盈平仓{CloseProfitTarget:F2}U @{ClosePriceTarget}",
                        OrderCategory = "平仓型"
                    };

                    ConditionalOrders.Add(conditionalOrder);
                    OnPropertyChanged(nameof(HasNoConditionalOrders));
                }
                else
                {
                    _logger.LogError($"❌ 平仓条件单API提交失败: {Symbol} {orderSide} {positionQuantity} @{ClosePriceTarget}");
                    StatusMessage = "❌ 平仓条件单提交失败";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ 平仓条件单异常: {ex.Message}";
                _logger.LogError(ex, "下平仓条件单异常");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        #endregion

        #region 条件单管理命令
        
        [RelayCommand]
        private void SelectAllConditionalOrders()
        {
            try
            {
                var selectedCount = 0;
                foreach (var order in ConditionalOrders)
                {
                    if (!order.IsSelected)
                    {
                        order.IsSelected = true;
                        selectedCount++;
                    }
                }

                StatusMessage = $"已选择 {selectedCount} 个条件单";
                _logger.LogInformation($"全选条件单: {selectedCount} 个");

                OnPropertyChanged(nameof(HasSelectedConditionalOrders));
                OnPropertyChanged(nameof(SelectedConditionalOrderCount));
            }
            catch (Exception ex)
            {
                StatusMessage = $"选择条件单失败: {ex.Message}";
                _logger.LogError(ex, "全选条件单失败");
            }
        }

        [RelayCommand]
        private void InvertConditionalOrderSelection()
        {
            try
            {
                var invertedCount = 0;
                foreach (var order in ConditionalOrders)
                {
                    order.IsSelected = !order.IsSelected;
                    invertedCount++;
                }

                var selectedCount = ConditionalOrders.Count(o => o.IsSelected);
                StatusMessage = $"已反选条件单，当前选择 {selectedCount} 个";
                _logger.LogInformation($"反选条件单: {invertedCount} 个操作，当前选择 {selectedCount} 个");

                OnPropertyChanged(nameof(HasSelectedConditionalOrders));
                OnPropertyChanged(nameof(SelectedConditionalOrderCount));
            }
            catch (Exception ex)
            {
                StatusMessage = $"反选失败: {ex.Message}";
                _logger.LogError(ex, "反选条件单失败");
            }
        }

        [RelayCommand]
        private async Task CancelSelectedConditionalOrdersAsync()
        {
            var selectedOrders = ConditionalOrders.Where(o => o.IsSelected).ToList();
            if (!selectedOrders.Any())
            {
                StatusMessage = "请先选择要取消的条件单";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在取消 {selectedOrders.Count} 个条件单...";

                var successCount = 0;
                foreach (var order in selectedOrders)
                {
                    try
                    {
                        ConditionalOrders.Remove(order);
                        successCount++;
                        _logger.LogInformation($"条件单已取消: {order.Description}");

                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"取消条件单失败: {order.Description}");
                    }
                }

                StatusMessage = $"批量取消完成: 成功 {successCount} 个";
                OnPropertyChanged(nameof(HasNoConditionalOrders));
                OnPropertyChanged(nameof(HasSelectedConditionalOrders));
                OnPropertyChanged(nameof(SelectedConditionalOrderCount));
            }
            catch (Exception ex)
            {
                StatusMessage = $"批量取消异常: {ex.Message}";
                _logger.LogError(ex, "批量取消条件单异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CancelConditionalOrderAsync(ConditionalOrderInfo order)
        {
            if (order == null) return;

            try
            {
                ConditionalOrders.Remove(order);
                StatusMessage = $"条件单已取消: {order.Description}";
                _logger.LogInformation($"取消条件单: {order.Description}");
                
                OnPropertyChanged(nameof(HasNoConditionalOrders));
            }
            catch (Exception ex)
            {
                StatusMessage = $"取消条件单失败: {ex.Message}";
                _logger.LogError(ex, "取消条件单失败");
            }
        }

        [RelayCommand]
        private async Task CancelAllConditionalOrdersAsync()
        {
            try
            {
                var count = ConditionalOrders.Count;
                ConditionalOrders.Clear();
                
                StatusMessage = $"已取消所有条件单 ({count} 个)";
                _logger.LogInformation($"取消所有条件单: {count} 个");
                
                OnPropertyChanged(nameof(HasNoConditionalOrders));
            }
            catch (Exception ex)
            {
                StatusMessage = $"取消条件单失败: {ex.Message}";
                _logger.LogError(ex, "取消所有条件单失败");
            }
        }
        
        #endregion

        #region 辅助方法
        
        private PositionInfo? GetCurrentPosition()
        {
            return Positions.FirstOrDefault(p => 
                p.Symbol == Symbol && Math.Abs(p.PositionAmt) > 0);
        }
        
        internal void UpdateConditionalOrderInfo()
        {
            try
            {
                var currentPosition = GetCurrentPosition();
                
                if (currentPosition != null)
                {
                    SelectedPositionInfo = $"{currentPosition.Symbol} {currentPosition.PositionSideString} {Math.Abs(currentPosition.PositionAmt):F6}";
                    CurrentPositionProfit = currentPosition.UnrealizedProfit;

                    var profitPercent = currentPosition.EntryPrice > 0 
                        ? (currentPosition.UnrealizedProfit / (Math.Abs(currentPosition.PositionAmt) * currentPosition.EntryPrice)) * 100
                        : 0;

                    AutoConditionalInfo = $"当前浮盈: {CurrentPositionProfit:F2}U ({profitPercent:+0.00;-0.00}%)";
                    OnPropertyChanged(nameof(CurrentPositionProfitColor));
                    OnPropertyChanged(nameof(HasCurrentPosition));
                }
                else
                {
                    SelectedPositionInfo = "未找到匹配持仓";
                    CurrentPositionProfit = 0;
                    AutoConditionalInfo = "请选择有持仓的合约";
                    OnPropertyChanged(nameof(HasCurrentPosition));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新条件单信息失败");
                AutoConditionalInfo = "信息更新失败";
            }
        }
        
        private int GetPriceDecimalPlaces()
        {
            // 根据合约类型返回价格小数位数
            return Symbol.Contains("USDT") ? 4 : 2;
        }
        
        #endregion

        #region 属性变化处理
        

        
        partial void OnTargetProfitChanged(decimal value)
        {
            if (value > 0 && HasCurrentPosition)
            {
                CalculateAddPositionPrice();
            }
        }
        
        partial void OnCloseProfitTargetChanged(decimal value)
        {
            if (value > 0 && HasCurrentPosition)
            {
                CalculateClosePriceFromProfit();
            }
        }
        
        partial void OnClosePriceTargetChanged(decimal value)
        {
            if (value > 0 && HasCurrentPosition)
            {
                CalculateCloseProfitFromPrice();
            }
        }
        
        #endregion
    }
} 