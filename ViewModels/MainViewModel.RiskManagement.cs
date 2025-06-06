using System;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.ViewModels
{
    /// <summary>
    /// MainViewModel风险管理部分
    /// </summary>
    public partial class MainViewModel
    {
        #region 风险管理命令
        [RelayCommand]
        private async Task AddBreakEvenStopLossAsync()
        {
            if (SelectedPosition == null)
            {
                StatusMessage = "请先选择要添加保本止损的持仓";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在为 {SelectedPosition.Symbol} 添加保本止损...";
                _logger.LogInformation($"开始为 {SelectedPosition.Symbol} 添加保本止损");

                // 计算保本价格（入场价格）
                var stopPrice = SelectedPosition.EntryPrice;
                var side = SelectedPosition.PositionAmt > 0 ? "SELL" : "BUY";

                // 清理该合约所有历史止损委托
                await CleanupAllStopOrdersAsync(SelectedPosition.Symbol);

                var stopLossRequest = new OrderRequest
                {
                    Symbol = SelectedPosition.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = Math.Abs(SelectedPosition.PositionAmt),
                    StopPrice = stopPrice,
                    ReduceOnly = true,
                    PositionSide = SelectedPosition.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                var success = await _binanceService.PlaceOrderAsync(stopLossRequest);
                if (success)
                {
                    StatusMessage = $"保本止损添加成功: {SelectedPosition.Symbol} @{stopPrice}";
                    _logger.LogInformation($"保本止损添加成功: {SelectedPosition.Symbol} @{stopPrice}");
                    
                    // 刷新数据
                    await RefreshDataAsync();
                }
                else
                {
                    StatusMessage = $"保本止损添加失败: {SelectedPosition.Symbol}";
                    _logger.LogWarning($"保本止损添加失败: {SelectedPosition.Symbol}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"添加保本止损异常: {ex.Message}";
                _logger.LogError(ex, "添加保本止损过程中发生异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddProfitProtectionStopLossAsync()
        {
            if (SelectedPosition == null)
            {
                StatusMessage = "请先选择要添加盈利保护止损的持仓";
                return;
            }

            if (SelectedPosition.UnrealizedProfit <= 0)
            {
                StatusMessage = "该持仓没有盈利，无需添加盈利保护";
                return;
            }

            try
            {
                // 显示保盈止损输入对话框
                var dialog = new Views.ProfitProtectionDialog(
                    SelectedPosition.Symbol,
                    SelectedPosition.PositionAmt > 0 ? "做多" : "做空",
                    Math.Abs(SelectedPosition.PositionAmt),
                    SelectedPosition.EntryPrice,
                    SelectedPosition.UnrealizedProfit,
                    SelectedPosition.MarkPrice);

                // 确保对话框在主窗口上显示
                if (System.Windows.Application.Current.MainWindow != null)
                {
                    dialog.Owner = System.Windows.Application.Current.MainWindow;
                }

                StatusMessage = "请在弹出的对话框中设置保盈止损...";
                _logger.LogInformation($"显示保盈止损设置对话框: {SelectedPosition.Symbol}");

                var dialogResult = dialog.ShowDialog();
                if (dialogResult != true)
                {
                    StatusMessage = "保盈止损设置已取消";
                    _logger.LogInformation("用户取消保盈止损设置");
                    return;
                }

                var protectionAmount = dialog.ProfitProtectionAmount;
                _logger.LogInformation($"用户设置保盈止损金额: {protectionAmount:F2}U");
                
                IsLoading = true;
                StatusMessage = $"正在为 {SelectedPosition.Symbol} 添加盈利保护止损...";

                // 根据保护金额计算止损价格
                var isLong = SelectedPosition.PositionAmt > 0;
                var entryPrice = SelectedPosition.EntryPrice;
                var quantity = Math.Abs(SelectedPosition.PositionAmt);
                
                decimal protectionPrice;
                if (isLong)
                {
                    // 多头：止损价 = 开仓价 + (保护盈利 / 持仓数量)
                    protectionPrice = entryPrice + (protectionAmount / quantity);
                }
                else
                {
                    // 空头：止损价 = 开仓价 - (保护盈利 / 持仓数量)
                    protectionPrice = entryPrice - (protectionAmount / quantity);
                }

                _logger.LogInformation($"计算保盈止损价: {protectionPrice:F4}, 入场价: {entryPrice:F4}, 保护金额: {protectionAmount:F2}U");

                // 清理该合约所有历史止损委托
                await CleanupAllStopOrdersAsync(SelectedPosition.Symbol);

                var side = isLong ? "SELL" : "BUY";

                var stopLossRequest = new OrderRequest
                {
                    Symbol = SelectedPosition.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = quantity,
                    StopPrice = protectionPrice,
                    ReduceOnly = true,
                    PositionSide = SelectedPosition.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                var success = await _binanceService.PlaceOrderAsync(stopLossRequest);
                if (success)
                {
                    StatusMessage = $"保盈止损添加成功: {SelectedPosition.Symbol} @{protectionPrice:F4} (保护{protectionAmount:F2}U盈利)";
                    _logger.LogInformation($"保盈止损添加成功: {SelectedPosition.Symbol} @{protectionPrice:F4}, 保护盈利: {protectionAmount:F2}U");
                    
                    // 刷新数据
                    await RefreshDataAsync();
                }
                else
                {
                    StatusMessage = $"保盈止损添加失败: {SelectedPosition.Symbol}";
                    _logger.LogWarning($"保盈止损添加失败: {SelectedPosition.Symbol}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"添加保盈止损异常: {ex.Message}";
                _logger.LogError(ex, "添加保盈止损过程中发生异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CleanupAllStopOrdersAsync(string symbol)
        {
            try
            {
                var stopOrders = Orders.Where(o => 
                    o.Symbol == symbol && 
                    o.Type == "STOP_MARKET" && 
                    o.ReduceOnly).ToList();

                if (stopOrders.Any())
                {
                    _logger.LogInformation($"发现 {stopOrders.Count} 个历史止损单，将全部清理: {symbol}");
                    StatusMessage = $"正在清理 {stopOrders.Count} 个历史止损单...";
                    
                    var canceledCount = 0;
                    foreach (var order in stopOrders)
                    {
                        try
                        {
                            var canceled = await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                            if (canceled)
                            {
                                canceledCount++;
                                _logger.LogInformation($"取消历史止损单: {order.Symbol} #{order.OrderId} @{order.StopPrice:F4}");
                            }
                            else
                            {
                                _logger.LogWarning($"取消历史止损单失败: {order.Symbol} #{order.OrderId}");
                            }
                            
                            // 避免API限制
                            await Task.Delay(100);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"取消止损单异常: {order.Symbol} #{order.OrderId}");
                        }
                    }
                    
                    _logger.LogInformation($"历史止损单清理完成: 成功取消 {canceledCount}/{stopOrders.Count} 个");
                    
                    // 等待订单取消生效
                    if (canceledCount > 0)
                    {
                        await Task.Delay(300);
                    }
                }
                else
                {
                    _logger.LogInformation($"合约 {symbol} 无历史止损单需要清理");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"清理历史止损单失败: {symbol}");
                // 不抛出异常，允许继续后续操作
            }
        }

        private async Task CleanupConflictingStopOrdersAsync(string symbol, decimal newStopPrice, bool isLong)
        {
            try
            {
                var conflictingOrders = Orders.Where(o => 
                    o.Symbol == symbol && 
                    o.Type == "STOP_MARKET" && 
                    o.ReduceOnly &&
                    ShouldReplaceStopOrder(o.StopPrice, newStopPrice, isLong)).ToList();

                if (conflictingOrders.Any())
                {
                    _logger.LogInformation($"发现 {conflictingOrders.Count} 个冲突的止损单，将被替换");
                    
                    foreach (var order in conflictingOrders)
                    {
                        await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                        _logger.LogInformation($"取消冲突止损单: {order.Symbol} #{order.OrderId} @{order.StopPrice}");
                        
                        // 稍微延迟以避免API限制
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理冲突止损单失败");
            }
        }

        private bool ShouldReplaceStopOrder(decimal existingStopPrice, decimal newStopPrice, bool isLong)
        {
            if (isLong)
            {
                // 多头：如果新止损价格更高（更好的保护），则替换
                return newStopPrice > existingStopPrice;
            }
            else
            {
                // 空头：如果新止损价格更低（更好的保护），则替换
                return newStopPrice < existingStopPrice;
            }
        }

        [RelayCommand]
        private async Task ToggleTrailingStopAsync()
        {
            try
            {
                TrailingStopEnabled = !TrailingStopEnabled;
                
                if (TrailingStopEnabled)
                {
                    StatusMessage = "移动止损已启动，开始监控持仓...";
                    _logger.LogInformation("移动止损功能已启动");
                    
                    // 立即处理一次移动止损
                    await ProcessTrailingStopAsync();
                }
                else
                {
                    StatusMessage = "移动止损已关闭";
                    _logger.LogInformation("移动止损功能已关闭");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"移动止损切换失败: {ex.Message}";
                _logger.LogError(ex, "切换移动止损失败");
                TrailingStopEnabled = false; // 出错时重置状态
            }
        }

        private async Task ProcessTrailingStopAsync()
        {
            try
            {
                if (!TrailingStopEnabled)
                    return;

                _logger.LogInformation("开始处理移动止损...");
                var processedCount = 0;
                
                // 获取所有有盈利的持仓
                var profitablePositions = Positions.Where(p => 
                    p.PositionAmt != 0 && p.UnrealizedProfit > 0).ToList();
                
                foreach (var position in profitablePositions)
                {
                    // 检查是否已有止损单
                    var existingStopOrder = Orders.FirstOrDefault(o => 
                        o.Symbol == position.Symbol && 
                        o.Type == "STOP_MARKET" && 
                        o.ReduceOnly);
                    
                    if (existingStopOrder != null)
                    {
                        // 如果已有普通止损单，转换为移动止损
                        var converted = await ConvertToTrailingStopAsync(existingStopOrder);
                        if (converted)
                            processedCount++;
                    }
                    else
                    {
                        // 如果没有止损单，直接创建移动止损
                        var created = await CreateTrailingStopOrderAsync(position);
                        if (created)
                            processedCount++;
                    }
                    
                    // 避免API频率限制
                    if (processedCount > 0)
                        await Task.Delay(300);
                }
                
                if (processedCount > 0)
                {
                    StatusMessage = $"移动止损处理完成，共处理 {processedCount} 个持仓";
                    _logger.LogInformation($"移动止损处理完成，共处理 {processedCount} 个持仓");
                }
                else
                {
                    StatusMessage = "没有需要处理的盈利持仓";
                    _logger.LogInformation("没有找到需要设置移动止损的盈利持仓");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"处理移动止损失败: {ex.Message}";
                _logger.LogError(ex, "处理移动止损失败");
            }
        }

        private async Task<bool> ConvertToTrailingStopAsync(OrderInfo stopOrder)
        {
            try
            {
                // 获取对应的持仓信息以计算开仓价
                var position = Positions.FirstOrDefault(p => p.Symbol == stopOrder.Symbol);
                if (position == null || position.PositionAmt == 0)
                {
                    _logger.LogWarning($"未找到对应持仓: {stopOrder.Symbol}");
                    return false;
                }

                // 计算原始止损比例作为回调率
                var callbackRate = CalculateStopLossRatio(position.EntryPrice, stopOrder.StopPrice, position.PositionAmt > 0);
                if (callbackRate <= 0)
                {
                    _logger.LogWarning($"无法计算有效回调率: {stopOrder.Symbol}, 开仓价={position.EntryPrice}, 止损价={stopOrder.StopPrice}");
                    return false;
                }

                // 取消现有止损单
                var cancelled = await _binanceService.CancelOrderAsync(stopOrder.Symbol, stopOrder.OrderId);
                if (!cancelled)
                {
                    _logger.LogWarning($"取消止损单失败: {stopOrder.Symbol}");
                    return false;
                }
                
                // 稍微等待确保订单取消完成
                await Task.Delay(100);
                
                // 下移动止损单
                var trailingStopRequest = new OrderRequest
                {
                    Symbol = stopOrder.Symbol,
                    Side = stopOrder.Side,
                    Type = "TRAILING_STOP_MARKET",
                    Quantity = stopOrder.OrigQty,
                    CallbackRate = callbackRate, // 使用计算出的回调率
                    ReduceOnly = true,
                    PositionSide = stopOrder.PositionSide,
                    WorkingType = "CONTRACT_PRICE"
                };

                var success = await _binanceService.PlaceOrderAsync(trailingStopRequest);
                if (success)
                {
                    _logger.LogInformation($"移动止损单创建成功: {stopOrder.Symbol} 回调率{callbackRate:F2}%");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"移动止损单创建失败: {stopOrder.Symbol}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"转换移动止损失败: {stopOrder.Symbol}");
                return false;
            }
        }

        private async Task<bool> CreateTrailingStopOrderAsync(PositionInfo position)
        {
            try
            {
                // 确定下单方向
                var side = position.PositionAmt > 0 ? "SELL" : "BUY";
                
                // 获取当前价格作为参考
                var currentPrice = await _binanceService.GetLatestPriceAsync(position.Symbol);
                
                // 计算合理的默认止损比例（基于盈利情况）
                var defaultStopLossRatio = CalculateDefaultStopLossRatio(position, currentPrice);
                
                // 创建移动止损单
                var trailingStopRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = side,
                    Type = "TRAILING_STOP_MARKET",
                    Quantity = Math.Abs(position.PositionAmt),
                    CallbackRate = defaultStopLossRatio, // 使用计算出的回调率
                    ReduceOnly = true,
                    PositionSide = position.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                var success = await _binanceService.PlaceOrderAsync(trailingStopRequest);
                if (success)
                {
                    _logger.LogInformation($"新移动止损单创建成功: {position.Symbol} 回调率{defaultStopLossRatio:F2}%");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"新移动止损单创建失败: {position.Symbol}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建移动止损失败: {position.Symbol}");
                return false;
            }
        }

        /// <summary>
        /// 计算止损比例（用于移动止损回调率）
        /// </summary>
        /// <param name="entryPrice">开仓价</param>
        /// <param name="stopPrice">止损价</param>
        /// <param name="isLong">是否多头</param>
        /// <returns>止损比例（百分比）</returns>
        private decimal CalculateStopLossRatio(decimal entryPrice, decimal stopPrice, bool isLong)
        {
            if (entryPrice <= 0 || stopPrice <= 0)
                return 0;

            decimal stopLossRatio;
            
            if (isLong)
            {
                // 多头：止损比例 = (开仓价 - 止损价) / 开仓价 * 100
                stopLossRatio = (entryPrice - stopPrice) / entryPrice * 100;
            }
            else
            {
                // 空头：止损比例 = (止损价 - 开仓价) / 开仓价 * 100
                stopLossRatio = (stopPrice - entryPrice) / entryPrice * 100;
            }

            // 确保回调率在合理范围内 (0.1% - 15%)
            stopLossRatio = Math.Max(0.1m, Math.Min(15.0m, stopLossRatio));
            
            _logger.LogInformation($"计算止损比例: 开仓价={entryPrice:F4}, 止损价={stopPrice:F4}, 方向={(isLong ? "多头" : "空头")}, 回调率={stopLossRatio:F2}%");
            
            return stopLossRatio;
        }

        /// <summary>
        /// 计算默认止损比例（用于无现有止损单的持仓）
        /// </summary>
        /// <param name="position">持仓信息</param>
        /// <param name="currentPrice">当前价格</param>
        /// <returns>默认止损比例（百分比）</returns>
        private decimal CalculateDefaultStopLossRatio(PositionInfo position, decimal currentPrice)
        {
            try
            {
                // 基于盈利百分比计算合理的回调率
                var profitRatio = Math.Abs(position.UnrealizedProfit) / (Math.Abs(position.PositionAmt) * position.EntryPrice) * 100;
                
                // 根据盈利情况设置回调率：盈利越多，回调率可以越小（更保守）
                decimal callbackRate = profitRatio switch
                {
                    > 10 => 1.0m,  // 盈利超过10%，使用1%回调率
                    > 5 => 1.5m,   // 盈利5-10%，使用1.5%回调率
                    > 2 => 2.0m,   // 盈利2-5%，使用2%回调率
                    _ => 2.5m      // 盈利小于2%，使用2.5%回调率
                };

                _logger.LogInformation($"计算默认止损比例: {position.Symbol} 盈利率={profitRatio:F2}%, 回调率={callbackRate:F2}%");
                
                return callbackRate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算默认止损比例失败: {position.Symbol}，使用2%默认值");
                return 2.0m; // 安全默认值
            }
        }

        private int GetMaxLeverageForSymbol(string symbol)
        {
            // 根据合约类型返回最大杠杆
            return symbol.ToUpper() switch
            {
                "BTCUSDT" => 125,
                "ETHUSDT" => 100,
                "BNBUSDT" => 75,
                "ADAUSDT" => 75,
                "DOGEUSDT" => 50,
                "WIFUSDT" => 50,
                "PEPEUSDT" => 25,
                "SHIBUSDT" => 25,
                _ => 50 // 默认最大杠杆
            };
        }

        [RelayCommand]
        private void AnalyzePortfolioRisk()
        {
            try
            {
                if (AccountInfo == null || !Positions.Any())
                {
                    StatusMessage = "没有持仓数据可分析";
                    return;
                }

                var totalBalance = AccountInfo.TotalWalletBalance;
                var totalMarginUsed = AccountInfo.ActualMarginUsed;
                var totalUnrealizedPnl = AccountInfo.TotalUnrealizedProfit;

                var marginUtilization = totalBalance > 0 ? (totalMarginUsed / totalBalance) * 100 : 0;
                var pnlPercent = totalBalance > 0 ? (totalUnrealizedPnl / totalBalance) * 100 : 0;

                var riskLevel = marginUtilization switch
                {
                    < 30 => "低风险",
                    < 60 => "中等风险",
                    < 80 => "高风险",
                    _ => "极高风险"
                };

                StatusMessage = $"风险分析 - {riskLevel}: 保证金占用{marginUtilization:F1}%, " +
                               $"总浮盈{pnlPercent:+0.00;-0.00}%, 持仓数量{Positions.Count}";

                _logger.LogInformation($"投资组合风险分析: {riskLevel}, 保证金占用{marginUtilization:F1}%, " +
                    $"浮盈{pnlPercent:F2}%, 持仓{Positions.Count}个");

                // 如果风险过高，提供建议
                if (marginUtilization > 80)
                {
                    _logger.LogWarning("⚠️ 保证金占用过高，建议降低杠杆或减少持仓");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"风险分析失败: {ex.Message}";
                _logger.LogError(ex, "投资组合风险分析失败");
            }
        }

        [RelayCommand]
        private async Task OptimizeStopLossLevelsAsync()
        {
            var positionsWithoutStopLoss = Positions.Where(p => 
                !Orders.Any(o => o.Symbol == p.Symbol && o.Type == "STOP_MARKET" && o.ReduceOnly))
                .ToList();

            if (!positionsWithoutStopLoss.Any())
            {
                StatusMessage = "所有持仓都已设置止损";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在为 {positionsWithoutStopLoss.Count} 个持仓优化止损...";

                var successCount = 0;
                foreach (var position in positionsWithoutStopLoss)
                {
                    try
                    {
                        // 根据波动率和风险偏好计算最佳止损位置
                        var stopLossPrice = CalculateOptimalStopLoss(position);
                        var side = position.PositionAmt > 0 ? "SELL" : "BUY";

                        var stopLossRequest = new OrderRequest
                        {
                            Symbol = position.Symbol,
                            Side = side,
                            Type = "STOP_MARKET",
                            Quantity = Math.Abs(position.PositionAmt),
                            StopPrice = stopLossPrice,
                            ReduceOnly = true,
                            PositionSide = position.PositionSideString,
                            WorkingType = "CONTRACT_PRICE"
                        };

                        var success = await _binanceService.PlaceOrderAsync(stopLossRequest);
                        if (success)
                        {
                            successCount++;
                            _logger.LogInformation($"优化止损设置成功: {position.Symbol} @{stopLossPrice:F4}");
                        }

                        await Task.Delay(200); // 避免API限制
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"为 {position.Symbol} 设置优化止损失败");
                    }
                }

                StatusMessage = $"止损优化完成: 成功设置 {successCount}/{positionsWithoutStopLoss.Count} 个止损";
                
                if (successCount > 0)
                {
                    await RefreshDataAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"止损优化异常: {ex.Message}";
                _logger.LogError(ex, "优化止损失败");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private decimal CalculateOptimalStopLoss(PositionInfo position)
        {
            // 基于ATR (平均真实波动率) 和风险偏好计算最佳止损位置
            var isLong = position.PositionAmt > 0;
            var entryPrice = position.EntryPrice;
            
            // 简化版本：使用固定的风险比例
            var riskRatio = StopLossRatio / 100; // 使用用户设定的止损比例
            
            if (isLong)
            {
                return entryPrice * (1 - riskRatio);
            }
            else
            {
                return entryPrice * (1 + riskRatio);
            }
        }

        [RelayCommand]
        private void CalculateMaxPositionSize()
        {
            try
            {
                if (AccountInfo == null)
                {
                    StatusMessage = "请先刷新账户信息";
                    return;
                }

                var maxRiskAmount = _calculationService.CalculateMaxRiskCapital(AccountInfo.AvailableBalance, 0.02m); // 2%风险
                
                if (LatestPrice > 0 && StopLossRatio > 0)
                {
                    var maxQuantity = maxRiskAmount / (LatestPrice * (StopLossRatio / 100));
                    
                    StatusMessage = $"最大建仓量: {maxQuantity:F6} (基于2%账户风险)";
                    _logger.LogInformation($"计算最大建仓量: {maxQuantity:F6}, 风险金额: {maxRiskAmount:F2}U");
                }
                else
                {
                    StatusMessage = "请先设置当前价格和止损比例";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"计算失败: {ex.Message}";
                _logger.LogError(ex, "计算最大建仓量失败");
            }
        }
        #endregion
    }
} 