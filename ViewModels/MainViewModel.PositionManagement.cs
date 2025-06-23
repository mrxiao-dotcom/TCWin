using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BinanceFuturesTrader.Models;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.ViewModels
{
    /// <summary>
    /// MainViewModel持仓管理部分
    /// </summary>
    public partial class MainViewModel
    {
        #region 持仓管理命令
        [RelayCommand]
        private async Task ClosePositionAsync()
        {
            if (SelectedPosition == null)
            {
                StatusMessage = "请先选择要平仓的持仓";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在平仓 {SelectedPosition.Symbol}...";

                var success = await _binanceService.ClosePositionAsync(
                    SelectedPosition.Symbol, 
                    SelectedPosition.PositionSideString);

                if (success)
                {
                    StatusMessage = $"持仓 {SelectedPosition.Symbol} 平仓成功";
                    _logger.LogInformation($"持仓平仓成功: {SelectedPosition.Symbol}");
                    
                    // 刷新数据
                    await RefreshDataAsync();
                }
                else
                {
                    StatusMessage = $"持仓 {SelectedPosition.Symbol} 平仓失败";
                    _logger.LogWarning($"持仓平仓失败: {SelectedPosition.Symbol}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"平仓异常: {ex.Message}";
                _logger.LogError(ex, "平仓过程中发生异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void SelectAllPositions()
        {
            try
            {
                var selectedCount = 0;
                foreach (var position in Positions)
                {
                    if (!position.IsSelected)
                    {
                        position.IsSelected = true;
                        selectedCount++;
                    }
                }

                StatusMessage = $"已选择 {selectedCount} 个持仓";
                _logger.LogInformation($"全选持仓: {selectedCount} 个");

                // 通知选择状态属性更新
                OnPropertyChanged(nameof(SelectedPositions));
                OnPropertyChanged(nameof(HasSelectedPositions));
                OnPropertyChanged(nameof(SelectedPositionCount));
                
                // 🔧 新增：通知移动止损按钮工具提示更新
                OnPropertyChanged(nameof(TrailingStopButtonTooltip));
            }
            catch (Exception ex)
            {
                StatusMessage = $"选择持仓失败: {ex.Message}";
                _logger.LogError(ex, "全选持仓失败");
            }
        }

        [RelayCommand]
        private void UnselectAllPositions()
        {
            try
            {
                var unselectedCount = 0;
                foreach (var position in Positions)
                {
                    if (position.IsSelected)
                    {
                        position.IsSelected = false;
                        unselectedCount++;
                    }
                }

                StatusMessage = $"已取消选择 {unselectedCount} 个持仓";
                _logger.LogInformation($"取消全选持仓: {unselectedCount} 个");

                // 通知选择状态属性更新
                OnPropertyChanged(nameof(SelectedPositions));
                OnPropertyChanged(nameof(HasSelectedPositions));
                OnPropertyChanged(nameof(SelectedPositionCount));
                
                // 🔧 新增：通知移动止损按钮工具提示更新
                OnPropertyChanged(nameof(TrailingStopButtonTooltip));
            }
            catch (Exception ex)
            {
                StatusMessage = $"取消选择失败: {ex.Message}";
                _logger.LogError(ex, "取消全选持仓失败");
            }
        }

        [RelayCommand]
        private void InvertPositionSelection()
        {
            try
            {
                var invertedCount = 0;
                foreach (var position in Positions)
                {
                    position.IsSelected = !position.IsSelected;
                    invertedCount++;
                }

                var selectedCount = Positions.Count(p => p.IsSelected);
                StatusMessage = $"已反选持仓，当前选择 {selectedCount} 个";
                _logger.LogInformation($"反选持仓: {invertedCount} 个操作，当前选择 {selectedCount} 个");

                // 通知选择状态属性更新
                OnPropertyChanged(nameof(SelectedPositions));
                OnPropertyChanged(nameof(HasSelectedPositions));
                OnPropertyChanged(nameof(SelectedPositionCount));
                
                // 🔧 新增：通知移动止损按钮工具提示更新
                OnPropertyChanged(nameof(TrailingStopButtonTooltip));
            }
            catch (Exception ex)
            {
                StatusMessage = $"反选失败: {ex.Message}";
                _logger.LogError(ex, "反选持仓失败");
            }
        }

        [RelayCommand]
        private async Task CloseSelectedPositionsAsync()
        {
            var selectedPositions = Positions.Where(p => p.IsSelected).ToList();
            if (!selectedPositions.Any())
            {
                StatusMessage = "请先选择要平仓的持仓";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在平仓 {selectedPositions.Count} 个持仓...";

                var successCount = 0;
                var failureCount = 0;

                foreach (var position in selectedPositions)
                {
                    try
                    {
                        var success = await _binanceService.ClosePositionAsync(
                            position.Symbol, 
                            position.PositionSideString);

                        if (success)
                        {
                            successCount++;
                            _logger.LogInformation($"持仓平仓成功: {position.Symbol} {position.PositionSideString}");
                        }
                        else
                        {
                            failureCount++;
                            _logger.LogWarning($"持仓平仓失败: {position.Symbol} {position.PositionSideString}");
                        }

                        // 每个操作之间稍微延迟
                        await Task.Delay(200);
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, $"平仓 {position.Symbol} 时发生异常");
                    }
                }

                StatusMessage = $"批量平仓完成: 成功 {successCount} 个，失败 {failureCount} 个";
                
                // 刷新数据
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"批量平仓异常: {ex.Message}";
                _logger.LogError(ex, "批量平仓过程中发生异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddBreakEvenStopLossForSelectedPositionsAsync()
        {
            var selectedPositions = Positions.Where(p => p.IsSelected).ToList();
            if (!selectedPositions.Any())
            {
                StatusMessage = "请先选择要添加保本止损的持仓";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在为 {selectedPositions.Count} 个持仓添加保本止损...";

                var successCount = 0;
                var failureCount = 0;

                foreach (var position in selectedPositions)
                {
                    try
                    {
                        // 计算保本价格（入场价格）
                        var stopPrice = position.EntryPrice;
                        var side = position.PositionAmt > 0 ? "SELL" : "BUY";

                        var stopLossRequest = new OrderRequest
                        {
                            Symbol = position.Symbol,
                            Side = side,
                            Type = "STOP_MARKET",
                            Quantity = Math.Abs(position.PositionAmt),
                            StopPrice = stopPrice,
                            ReduceOnly = true,
                            PositionSide = position.PositionSideString,
                            WorkingType = "CONTRACT_PRICE"
                        };

                        var success = await _binanceService.PlaceOrderAsync(stopLossRequest);
                        if (success)
                        {
                            successCount++;
                            _logger.LogInformation($"保本止损添加成功: {position.Symbol} @{stopPrice}");
                        }
                        else
                        {
                            failureCount++;
                            _logger.LogWarning($"保本止损添加失败: {position.Symbol}");
                        }

                        // 每个操作之间稍微延迟
                        await Task.Delay(200);
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, $"为 {position.Symbol} 添加保本止损时发生异常");
                    }
                }

                StatusMessage = $"批量保本止损完成: 成功 {successCount} 个，失败 {failureCount} 个";
                
                // 刷新数据
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"批量保本止损异常: {ex.Message}";
                _logger.LogError(ex, "批量保本止损过程中发生异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ClearAllPositionsAndOrdersAsync()
        {
            if (SelectedAccount == null)
            {
                StatusMessage = "请先选择账户";
                return;
            }

            // 🔧 新增：显示确认对话框
            try
            {
                var confirmDialog = new Views.ClearAllConfirmationDialog();
                confirmDialog.Owner = Application.Current.MainWindow;
                
                var result = confirmDialog.ShowDialog();
                if (result != true || !confirmDialog.IsConfirmed)
                {
                    StatusMessage = "一键清仓操作已取消";
                    _logger.LogInformation("用户取消了一键清仓操作");
                    return;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"显示确认对话框失败: {ex.Message}";
                _logger.LogError(ex, "显示一键清仓确认对话框失败");
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "正在执行一键清仓...";
                _logger.LogInformation("🚨 开始执行一键清仓操作");

                var successOperations = 0;
                var totalOperations = 0;

                // 🔧 改进：第一步 - 详细清理所有委托订单
                StatusMessage = "正在取消所有委托订单...";
                _logger.LogInformation("📋 第一步：取消所有委托订单");
                
                try
                {
                                         // 获取所有开放订单
                     var openOrders = await _binanceService.GetOpenOrdersAsync();
                     if (openOrders != null && openOrders.Count > 0)
                     {
                         _logger.LogInformation($"发现 {openOrders.Count} 个待取消的订单");
                        
                        foreach (var order in openOrders)
                        {
                            try
                            {
                                totalOperations++;
                                var cancelResult = await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                                if (cancelResult)
                                {
                                    successOperations++;
                                    _logger.LogInformation($"✅ 取消订单成功: {order.Symbol} #{order.OrderId} ({order.Type})");
                                }
                                else
                                {
                                    _logger.LogWarning($"❌ 取消订单失败: {order.Symbol} #{order.OrderId}");
                                }
                                
                                // 避免API限制
                                await Task.Delay(100);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"取消订单异常: {order.Symbol} #{order.OrderId}");
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("没有发现待取消的订单");
                    }

                    // 🔧 新增：使用全局取消作为备份
                    var globalCancelSuccess = await _binanceService.CancelAllOrdersAsync();
                    if (globalCancelSuccess)
                    {
                        _logger.LogInformation("✅ 全局订单取消成功");
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ 全局订单取消失败");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "取消订单过程中发生异常");
                }

                // 等待订单取消生效
                StatusMessage = "等待订单取消生效...";
                await Task.Delay(2000);

                // 🔧 改进：第二步 - 详细平掉所有持仓
                StatusMessage = "正在平掉所有持仓...";
                _logger.LogInformation("📊 第二步：平掉所有持仓");
                
                try
                {
                    // 获取当前所有持仓
                    var positions = await _binanceService.GetPositionsAsync();
                    var activePositions = positions?.Where(p => Math.Abs(p.PositionAmt) > 0.001m).ToList() ?? new List<PositionInfo>();
                    
                    if (activePositions.Any())
                    {
                        _logger.LogInformation($"发现 {activePositions.Count} 个待平仓的持仓");
                        
                        foreach (var position in activePositions)
                        {
                            try
                            {
                                totalOperations++;
                                var side = position.PositionAmt > 0 ? "SELL" : "BUY";
                                var quantity = Math.Abs(position.PositionAmt);
                                
                                var closeRequest = new OrderRequest
                                {
                                    Symbol = position.Symbol,
                                    Side = side,
                                    Type = "MARKET",
                                    Quantity = quantity,
                                    ReduceOnly = true,
                                    PositionSide = position.PositionSideString
                                };
                                
                                var closeResult = await _binanceService.PlaceOrderAsync(closeRequest);
                                if (closeResult)
                                {
                                    successOperations++;
                                    _logger.LogInformation($"✅ 平仓成功: {position.Symbol} {position.PositionSideString} {quantity:F6}");
                                }
                                else
                                {
                                    _logger.LogWarning($"❌ 平仓失败: {position.Symbol} {position.PositionSideString}");
                                }
                                
                                // 避免API限制
                                await Task.Delay(200);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"平仓异常: {position.Symbol} {position.PositionSideString}");
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("没有发现待平仓的持仓");
                    }

                    // 🔧 新增：使用全局平仓作为备份
                    var globalCloseSuccess = await _binanceService.CloseAllPositionsAsync();
                    if (globalCloseSuccess)
                    {
                        _logger.LogInformation("✅ 全局持仓平仓成功");
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ 全局持仓平仓失败");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "平仓过程中发生异常");
                }

                // 🔧 新增：第三步 - 最终验证和清理
                StatusMessage = "正在进行最终验证...";
                _logger.LogInformation("🔍 第三步：最终验证和清理");
                
                await Task.Delay(3000); // 等待所有操作生效
                
                // 验证清理结果
                var finalPositions = await _binanceService.GetPositionsAsync();
                var finalActivePositions = finalPositions?.Where(p => Math.Abs(p.PositionAmt) > 0.001m).ToList() ?? new List<PositionInfo>();
                
                                 var finalOrders = await _binanceService.GetOpenOrdersAsync();
                 var finalActiveOrders = finalOrders?.Where(o => o.Status == "NEW").ToList() ?? new List<OrderInfo>();

                 // 生成清理报告
                 var positionsCleaned = finalActivePositions.Count == 0;
                 var ordersCleaned = finalActiveOrders.Count == 0;
                
                if (positionsCleaned && ordersCleaned)
                {
                    StatusMessage = $"✅ 一键清仓完成！成功执行 {successOperations}/{totalOperations} 个操作";
                    _logger.LogInformation($"🎉 一键清仓完全成功！所有持仓和订单已清空");
                }
                else
                {
                    var remainingInfo = "";
                    if (!positionsCleaned) remainingInfo += $"剩余持仓: {finalActivePositions.Count}个 ";
                    if (!ordersCleaned) remainingInfo += $"剩余订单: {finalActiveOrders.Count}个";
                    
                    StatusMessage = $"⚠️ 清仓部分完成：{remainingInfo}";
                    _logger.LogWarning($"一键清仓部分完成：{remainingInfo}");
                    
                    // 记录剩余的持仓和订单
                    foreach (var pos in finalActivePositions)
                    {
                        _logger.LogWarning($"剩余持仓: {pos.Symbol} {pos.PositionSideString} {pos.PositionAmt:F6}");
                    }
                    foreach (var order in finalActiveOrders)
                    {
                        _logger.LogWarning($"剩余订单: {order.Symbol} #{order.OrderId} {order.Type}");
                    }
                }

                // 刷新数据
                await RefreshDataAsync();
                
                _logger.LogInformation($"🚨 一键清仓操作完成，成功率: {successOperations}/{totalOperations}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"一键清仓异常: {ex.Message}";
                _logger.LogError(ex, "一键清仓过程中发生异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void CheckAccountEquityComposition()
        {
            try
            {
                if (AccountInfo == null)
                {
                    StatusMessage = "请先刷新账户信息";
                    return;
                }

                var totalBalance = AccountInfo.TotalWalletBalance;
                var available = AccountInfo.AvailableBalance;
                var unrealizedPnl = AccountInfo.TotalUnrealizedProfit;
                var marginUsed = AccountInfo.ActualMarginUsed;

                var availablePercent = totalBalance > 0 ? (available / totalBalance * 100) : 0;
                var pnlPercent = totalBalance > 0 ? (unrealizedPnl / totalBalance * 100) : 0;
                var marginPercent = totalBalance > 0 ? (marginUsed / totalBalance * 100) : 0;

                StatusMessage = $"资产构成 - 可用:{available:F2}({availablePercent:F1}%) " +
                               $"浮盈:{unrealizedPnl:F2}({pnlPercent:F1}%) " +
                               $"保证金:{marginUsed:F2}({marginPercent:F1}%)";

                _logger.LogInformation($"账户资产构成分析: 总额={totalBalance:F2}, " +
                    $"可用={available:F2}({availablePercent:F1}%), " +
                    $"浮盈={unrealizedPnl:F2}({pnlPercent:F1}%), " +
                    $"保证金={marginUsed:F2}({marginPercent:F1}%)");
            }
            catch (Exception ex)
            {
                StatusMessage = $"分析失败: {ex.Message}";
                _logger.LogError(ex, "账户资产构成分析失败");
            }
        }
        #endregion
    }
} 