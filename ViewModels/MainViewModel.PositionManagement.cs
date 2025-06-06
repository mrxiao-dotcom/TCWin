using System;
using System.Linq;
using System.Threading.Tasks;
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

            try
            {
                IsLoading = true;
                StatusMessage = "正在清理所有持仓和订单...";

                // 先取消所有订单
                var cancelSuccess = await _binanceService.CancelAllOrdersAsync();
                if (cancelSuccess)
                {
                    _logger.LogInformation("所有订单取消成功");
                }
                else
                {
                    _logger.LogWarning("取消订单失败");
                }

                await Task.Delay(1000); // 等待订单取消生效

                // 再平掉所有持仓
                var closeSuccess = await _binanceService.CloseAllPositionsAsync();
                if (closeSuccess)
                {
                    _logger.LogInformation("所有持仓平仓成功");
                    StatusMessage = "所有持仓和订单清理完成";
                }
                else
                {
                    _logger.LogWarning("平仓失败");
                    StatusMessage = "清理完成，但部分操作可能失败";
                }

                // 刷新数据
                await Task.Delay(2000); // 等待操作生效
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"清理异常: {ex.Message}";
                _logger.LogError(ex, "清理持仓和订单过程中发生异常");
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