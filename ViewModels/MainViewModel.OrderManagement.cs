using System;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.ViewModels
{
    /// <summary>
    /// MainViewModel订单管理部分
    /// </summary>
    public partial class MainViewModel
    {
        #region 订单管理命令
        [RelayCommand]
        private void SelectAllOrders()
        {
            try
            {
                var selectedCount = 0;
                
                // 选择FilteredOrders中的所有订单
                foreach (var order in FilteredOrders)
                {
                    if (!order.IsSelected)
                    {
                        order.IsSelected = true;
                        selectedCount++;
                    }
                }
                
                // 选择ReduceOnlyOrders中的所有订单
                foreach (var order in ReduceOnlyOrders)
                {
                    if (!order.IsSelected)
                    {
                        order.IsSelected = true;
                        selectedCount++;
                    }
                }

                StatusMessage = $"已选择 {selectedCount} 个委托单";
                _logger.LogInformation($"全选订单: {selectedCount} 个");

                // 通知选择状态属性更新
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));
                OnPropertyChanged(nameof(HasSelectedStopOrders));
                OnPropertyChanged(nameof(SelectedStopOrderCount));
            }
            catch (Exception ex)
            {
                StatusMessage = $"选择订单失败: {ex.Message}";
                _logger.LogError(ex, "全选订单失败");
            }
        }

        [RelayCommand]
        private void UnselectAllOrders()
        {
            try
            {
                var unselectedCount = 0;
                
                // 取消选择FilteredOrders中的所有订单
                foreach (var order in FilteredOrders)
                {
                    if (order.IsSelected)
                    {
                        order.IsSelected = false;
                        unselectedCount++;
                    }
                }
                
                // 取消选择ReduceOnlyOrders中的所有订单
                foreach (var order in ReduceOnlyOrders)
                {
                    if (order.IsSelected)
                    {
                        order.IsSelected = false;
                        unselectedCount++;
                    }
                }

                StatusMessage = $"已取消选择 {unselectedCount} 个委托单";
                _logger.LogInformation($"取消全选订单: {unselectedCount} 个");

                // 通知选择状态属性更新
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));
                OnPropertyChanged(nameof(HasSelectedStopOrders));
                OnPropertyChanged(nameof(SelectedStopOrderCount));
            }
            catch (Exception ex)
            {
                StatusMessage = $"取消选择失败: {ex.Message}";
                _logger.LogError(ex, "取消全选订单失败");
            }
        }

        [RelayCommand]
        private void InvertOrderSelection()
        {
            try
            {
                var invertedCount = 0;
                
                // 反选FilteredOrders中的所有订单
                foreach (var order in FilteredOrders)
                {
                    order.IsSelected = !order.IsSelected;
                    invertedCount++;
                }
                
                // 反选ReduceOnlyOrders中的所有订单
                foreach (var order in ReduceOnlyOrders)
                {
                    order.IsSelected = !order.IsSelected;
                    invertedCount++;
                }

                var selectedCount = SelectedOrderCount;
                StatusMessage = $"已反选委托单，当前选择 {selectedCount} 个";
                _logger.LogInformation($"反选订单: {invertedCount} 个操作，当前选择 {selectedCount} 个");

                // 通知选择状态属性更新
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));
                OnPropertyChanged(nameof(HasSelectedStopOrders));
                OnPropertyChanged(nameof(SelectedStopOrderCount));
            }
            catch (Exception ex)
            {
                StatusMessage = $"反选失败: {ex.Message}";
                _logger.LogError(ex, "反选订单失败");
            }
        }

        [RelayCommand]
        private async Task CancelSelectedOrdersAsync()
        {
            // 使用SelectedOrders属性，它会自动包含FilteredOrders和ReduceOnlyOrders中选中的订单
            var selectedOrders = SelectedOrders.ToList();
            if (!selectedOrders.Any())
            {
                StatusMessage = "请先选择委托单";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在取消 {selectedOrders.Count} 个订单...";
                _logger.LogInformation($"开始执行清除选定委托操作，共 {selectedOrders.Count} 个订单");

                var successCount = 0;
                var failureCount = 0;

                foreach (var order in selectedOrders)
                {
                    try
                    {
                        var success = await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                        if (success)
                        {
                            successCount++;
                            _logger.LogInformation($"订单取消成功: {order.Symbol} #{order.OrderId}");
                        }
                        else
                        {
                            failureCount++;
                            _logger.LogWarning($"订单取消失败: {order.Symbol} #{order.OrderId}");
                        }

                        // 每个操作之间稍微延迟
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, $"取消订单 {order.OrderId} 时发生异常");
                    }
                }

                StatusMessage = $"批量取消完成: 成功 {successCount} 个，失败 {failureCount} 个";
                
                // 刷新数据
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"批量取消异常: {ex.Message}";
                _logger.LogError(ex, "批量取消订单过程中发生异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CancelSelectedStopOrdersAsync()
        {
            // 使用SelectedOrders属性，然后筛选止损订单
            var selectedStopOrders = SelectedOrders
                .Where(o => o.Type == "STOP_MARKET" || o.Type == "TAKE_PROFIT_MARKET")
                .ToList();

            if (!selectedStopOrders.Any())
            {
                StatusMessage = "请先选择止损订单";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在取消 {selectedStopOrders.Count} 个止损订单...";

                var successCount = 0;
                var failureCount = 0;

                foreach (var order in selectedStopOrders)
                {
                    try
                    {
                        var success = await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                        if (success)
                        {
                            successCount++;
                            _logger.LogInformation($"止损订单取消成功: {order.Symbol} #{order.OrderId} @{order.StopPrice}");
                        }
                        else
                        {
                            failureCount++;
                            _logger.LogWarning($"止损订单取消失败: {order.Symbol} #{order.OrderId}");
                        }

                        // 每个操作之间稍微延迟
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, $"取消止损订单 {order.OrderId} 时发生异常");
                    }
                }

                StatusMessage = $"止损订单取消完成: 成功 {successCount} 个，失败 {failureCount} 个";
                
                // 刷新数据
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"取消止损订单异常: {ex.Message}";
                _logger.LogError(ex, "批量取消止损订单过程中发生异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CancelAllOrdersAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在取消所有订单...";
                _logger.LogInformation("开始执行清理委托单操作");

                var success = await _binanceService.CancelAllOrdersAsync();
                if (success)
                {
                    StatusMessage = "所有订单取消成功";
                    _logger.LogInformation("所有订单取消成功");
                    
                    // 刷新数据
                    await RefreshDataAsync();
                }
                else
                {
                    StatusMessage = "取消订单失败";
                    _logger.LogWarning("取消所有订单失败");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"取消订单异常: {ex.Message}";
                _logger.LogError(ex, "取消所有订单过程中发生异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddBreakEvenStopLossForSelectedOrdersAsync()
        {
            var selectedOrders = SelectedOrders.ToList();
            if (!selectedOrders.Any())
            {
                StatusMessage = "请先选择要添加保本止损的订单";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在为 {selectedOrders.Count} 个订单添加保本止损...";

                var successCount = 0;
                var failureCount = 0;

                foreach (var order in selectedOrders)
                {
                    try
                    {
                        // 跳过已经是止损单的订单
                        if (order.Type == "STOP_MARKET" || order.Type == "TAKE_PROFIT_MARKET")
                        {
                            _logger.LogInformation($"跳过止损单: {order.Symbol} #{order.OrderId}");
                            continue;
                        }

                        // 计算保本价格（订单价格）
                        var stopPrice = order.Price > 0 ? order.Price : order.StopPrice;
                        if (stopPrice <= 0)
                        {
                            _logger.LogWarning($"无法确定保本价格: {order.Symbol} #{order.OrderId}");
                            failureCount++;
                            continue;
                        }

                        var side = order.Side == "BUY" ? "SELL" : "BUY";

                        var stopLossRequest = new OrderRequest
                        {
                            Symbol = order.Symbol,
                            Side = side,
                            Type = "STOP_MARKET",
                            Quantity = order.OrigQty,
                            StopPrice = stopPrice,
                            ReduceOnly = true,
                            PositionSide = order.PositionSide,
                            WorkingType = "CONTRACT_PRICE"
                        };

                        var success = await _binanceService.PlaceOrderAsync(stopLossRequest);
                        if (success)
                        {
                            successCount++;
                            _logger.LogInformation($"保本止损添加成功: {order.Symbol} @{stopPrice}");
                        }
                        else
                        {
                            failureCount++;
                            _logger.LogWarning($"保本止损添加失败: {order.Symbol}");
                        }

                        // 每个操作之间稍微延迟
                        await Task.Delay(200);
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, $"为订单 {order.OrderId} 添加保本止损时发生异常");
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
        private void OpenLogFile()
        {
            try
            {
                var logPath = LogService.GetLogFilePath();
                if (System.IO.File.Exists(logPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = logPath,
                        UseShellExecute = true
                    });
                    StatusMessage = "日志文件已打开";
                    _logger.LogInformation($"打开日志文件: {logPath}");
                }
                else
                {
                    StatusMessage = "日志文件不存在";
                    _logger.LogWarning("日志文件不存在");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"打开日志失败: {ex.Message}";
                _logger.LogError(ex, "打开日志文件失败");
            }
        }

        [RelayCommand]
        private void ClearLogFile()
        {
            try
            {
                LogService.ClearLogFile();
                StatusMessage = "日志文件已清空";
                _logger.LogInformation("日志文件已清空");
            }
            catch (Exception ex)
            {
                StatusMessage = $"清空日志失败: {ex.Message}";
                _logger.LogError(ex, "清空日志文件失败");
            }
        }

        [RelayCommand]
        private async Task QueryContractInfoAsync()
        {
            if (string.IsNullOrEmpty(Symbol))
            {
                StatusMessage = "请先输入合约名称";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在查询 {Symbol} 合约信息...";

                var exchangeInfo = await _binanceService.GetRealExchangeInfoAsync(Symbol);
                if (!string.IsNullOrEmpty(exchangeInfo))
                {
                    // 这里可以解析并显示合约信息
                    StatusMessage = $"{Symbol} 合约信息查询完成";
                    _logger.LogInformation($"查询到 {Symbol} 合约信息");
                }
                else
                {
                    StatusMessage = $"未找到 {Symbol} 合约信息";
                    _logger.LogWarning($"未找到 {Symbol} 合约信息");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"查询失败: {ex.Message}";
                _logger.LogError(ex, "查询合约信息失败");
            }
            finally
            {
                IsLoading = false;
            }
        }
        #endregion
    }
} 