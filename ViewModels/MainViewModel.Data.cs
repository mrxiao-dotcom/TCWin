using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace BinanceFuturesTrader.ViewModels
{
    /// <summary>
    /// MainViewModel数据管理部分
    /// </summary>
    public partial class MainViewModel
    {
        #region 数据刷新功能
        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            if (SelectedAccount == null)
            {
                StatusMessage = "请先选择账户";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "正在刷新数据...";

                await RefreshAccountDataWithSelectionPreservation();

                StatusMessage = "数据刷新完成";
                _logger.LogInformation("手动数据刷新完成");
            }
            catch (Exception ex)
            {
                StatusMessage = $"数据刷新失败: {ex.Message}";
                _logger.LogError(ex, "数据刷新失败");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 刷新账户数据并保持选择状态
        /// </summary>
        private async Task RefreshAccountDataWithSelectionPreservation()
        {
            // 保存当前选择状态
            var selectedOrderIds = new HashSet<long>();
            var selectedPositionSymbols = new HashSet<string>();
            
            foreach (var order in FilteredOrders.Where(o => o.IsSelected))
            {
                selectedOrderIds.Add(order.OrderId);
            }
            
            foreach (var position in Positions.Where(p => p.IsSelected))
            {
                var positionKey = $"{position.Symbol}_{position.PositionSideString}";
                selectedPositionSymbols.Add(positionKey);
            }

            // 更新账户信息
            var accountInfo = await _binanceService.GetAccountInfoAsync();
            if (accountInfo != null)
            {
                AccountInfo = accountInfo;
            }

            // 更新持仓信息
            var positions = await _binanceService.GetPositionsAsync();
            
            Positions.Clear();
            int restoredPositionCount = 0;
            foreach (var position in positions)
            {
                // 恢复持仓选择状态
                var positionKey = $"{position.Symbol}_{position.PositionSideString}";
                if (selectedPositionSymbols.Contains(positionKey))
                {
                    position.IsSelected = true;
                    restoredPositionCount++;
                }
                Positions.Add(position);
            }

            // 计算保证金占用
            if (AccountInfo != null)
            {
                AccountInfo.CalculateMarginUsed(Positions);
                OnPropertyChanged(nameof(TotalMarginBalance));
                OnPropertyChanged(nameof(TotalWalletBalance));
            }

            // 更新订单信息
            var orders = await _binanceService.GetOpenOrdersAsync();
            
            Orders.Clear();
            int restoredOrderCount = 0;
            foreach (var order in orders)
            {
                // 恢复订单选择状态
                if (selectedOrderIds.Contains(order.OrderId))
                {
                    order.IsSelected = true;
                    restoredOrderCount++;
                }
                Orders.Add(order);
            }

            // 如果有选中的持仓，更新过滤的订单
            if (SelectedPosition != null)
            {
                FilterOrdersForPosition(SelectedPosition.Symbol);
                
                // 恢复过滤订单的选择状态
                foreach (var order in FilteredOrders)
                {
                    if (selectedOrderIds.Contains(order.OrderId))
                    {
                        order.IsSelected = true;
                    }
                }
            }
            else
            {
                // 没有选中持仓，显示所有委托单
                FilterOrdersForPosition(); // 不传参数，显示所有委托单
                
                // 恢复所有订单的选择状态
                foreach (var order in FilteredOrders)
                {
                    if (selectedOrderIds.Contains(order.OrderId))
                    {
                        order.IsSelected = true;
                    }
                }
            }

            // 强制通知选择状态属性更新
            OnPropertyChanged(nameof(SelectedOrders));
            OnPropertyChanged(nameof(HasSelectedOrders));
            OnPropertyChanged(nameof(SelectedOrderCount));
            OnPropertyChanged(nameof(SelectedPositions));
            OnPropertyChanged(nameof(HasSelectedPositions));
            OnPropertyChanged(nameof(SelectedPositionCount));

            // 重新加载条件单数据（从API订单中识别条件单）
            LoadConditionalOrdersFromApiOrders();

            // 自动计算可用风险金
            if (SelectedAccount != null)
            {
                CalculateMaxRiskCapital();
            }

            _logger.LogDebug($"数据刷新完成，恢复了 {restoredPositionCount} 个持仓选择，{restoredOrderCount} 个订单选择");
        }

        /// <summary>
        /// 从API订单中加载条件单数据
        /// </summary>
        private void LoadConditionalOrdersFromApiOrders()
        {
            try
            {
                // 从API订单中筛选出条件单类型的订单
                var conditionalOrderTypes = new[] { "STOP_MARKET", "TAKE_PROFIT_MARKET", "TRAILING_STOP_MARKET" };
                var apiConditionalOrders = Orders.Where(o => conditionalOrderTypes.Contains(o.Type)).ToList();

                // 只保留程序内部创建的条件单，移除已不存在的API条件单
                var existingApiOrderIds = apiConditionalOrders.Select(o => o.OrderId).ToHashSet();
                var toRemove = ConditionalOrders.Where(c => c.OrderId > 0 && !existingApiOrderIds.Contains(c.OrderId)).ToList();
                
                foreach (var order in toRemove)
                {
                    ConditionalOrders.Remove(order);
                }

                // 添加新的API条件单到条件单监控（只添加加仓型）
                foreach (var apiOrder in apiConditionalOrders)
                {
                    // 检查是否已存在
                    if (!ConditionalOrders.Any(c => c.OrderId == apiOrder.OrderId))
                    {
                        var orderCategory = DetermineOrderCategory(apiOrder);
                        
                        // 只有加仓型条件单才添加到条件单监控
                        if (orderCategory == "加仓型")
                        {
                            var conditionalOrder = new Models.ConditionalOrderInfo
                            {
                                OrderId = apiOrder.OrderId,
                                Symbol = apiOrder.Symbol,
                                Type = apiOrder.Type,
                                Side = apiOrder.Side,
                                StopPrice = apiOrder.StopPrice,
                                Price = apiOrder.Price > 0 ? apiOrder.Price : null,
                                Quantity = apiOrder.OrigQty,
                                Status = MapOrderStatusToConditionalStatus(apiOrder.Status),
                                WorkingType = apiOrder.WorkingType,
                                CreateTime = apiOrder.Time,
                                Description = $"API条件单 - {apiOrder.Type}",
                                OrderCategory = orderCategory
                            };

                            ConditionalOrders.Add(conditionalOrder);
                        }
                    }
                }

                // 更新条件单相关UI属性
                OnPropertyChanged(nameof(HasNoConditionalOrders));
                OnPropertyChanged(nameof(HasSelectedConditionalOrders));
                OnPropertyChanged(nameof(SelectedConditionalOrderCount));

                _logger.LogDebug($"条件单数据加载完成，当前共有 {ConditionalOrders.Count} 个条件单");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载条件单数据失败");
            }
        }

        /// <summary>
        /// 映射API订单状态到条件单状态
        /// </summary>
        private string MapOrderStatusToConditionalStatus(string apiStatus)
        {
            return apiStatus switch
            {
                "NEW" => "待触发",
                "PARTIALLY_FILLED" => "部分成交",
                "FILLED" => "已成交",
                "CANCELED" => "已取消",
                "REJECTED" => "已拒绝",
                "EXPIRED" => "已过期",
                _ => apiStatus
            };
        }

        /// <summary>
        /// 确定订单分类（加仓型/平仓型）
        /// </summary>
        private string DetermineOrderCategory(Models.OrderInfo order)
        {
            // 如果是ReduceOnly订单，通常是平仓型
            if (order.ReduceOnly || order.ClosePosition)
            {
                return "平仓型";
            }
            
            // 其他情况默认为加仓型
            return "加仓型";
        }
        #endregion

        #region 数据过滤功能
        /// <summary>
        /// 根据持仓过滤订单并分类显示
        /// </summary>
        private void FilterOrdersForPosition(string? symbol = null)
        {
            try
            {
                FilteredOrders.Clear();
                ReduceOnlyOrders.Clear();

                var ordersToShow = string.IsNullOrEmpty(symbol) 
                    ? Orders.ToList() 
                    : Orders.Where(o => o.Symbol == symbol).ToList();

                int reduceOnlyCount = 0;
                int addPositionCount = 0;

                foreach (var order in ordersToShow)
                {
                    // 减仓型订单显示在上方委托单列表
                    if (order.ReduceOnly || order.ClosePosition)
                    {
                        ReduceOnlyOrders.Add(order);
                        reduceOnlyCount++;
                    }
                    else
                    {
                        // 加仓型订单显示在下方条件单列表
                        FilteredOrders.Add(order);
                        addPositionCount++;
                    }
                }

                // 通知相关属性更新
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));
                OnPropertyChanged(nameof(HasSelectedStopOrders));
                OnPropertyChanged(nameof(SelectedStopOrderCount));

                _logger.LogDebug($"订单过滤完成，减仓型订单: {reduceOnlyCount} 个，加仓型订单: {addPositionCount} 个" + 
                    (string.IsNullOrEmpty(symbol) ? "（全部）" : $"（{symbol}）"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订单过滤失败");
            }
        }

        /// <summary>
        /// 持仓选择变化处理
        /// </summary>
        partial void OnSelectedPositionChanged(PositionInfo? value)
        {
            try
            {
                if (value != null)
                {
                    // 切换合约
                    if (!string.IsNullOrEmpty(value.Symbol) && value.Symbol != Symbol)
                    {
                        Symbol = value.Symbol;
                    }

                    // 过滤该持仓的订单
                    FilterOrdersForPosition(value.Symbol);
                    
                    _logger.LogDebug($"选择持仓: {value.Symbol} {value.PositionSideString} {value.PositionAmt}");
                }
                else
                {
                    // 取消选择，显示所有订单
                    FilterOrdersForPosition();
                    _logger.LogDebug("取消持仓选择，显示所有订单");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "持仓选择变化处理失败");
            }
        }
        #endregion

        #region 定时器控制命令
        [RelayCommand]
        private void ToggleTimers()
        {
            if (_priceTimer.IsEnabled)
            {
                StopTimers();
                StatusMessage = "自动更新已停止";
            }
            else
            {
                StartTimers();
                StatusMessage = "自动更新已启动";
            }
        }

        [RelayCommand]
        private void ToggleAutoRefresh()
        {
            AutoRefreshEnabled = !AutoRefreshEnabled;
            StatusMessage = AutoRefreshEnabled ? "自动刷新已启用" : "自动刷新已禁用";
            _logger.LogInformation($"自动刷新状态: {(AutoRefreshEnabled ? "启用" : "禁用")}");
        }
        #endregion

        #region 最近合约功能
        [RelayCommand]
        private async Task SelectRecentContractAsync(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return;

            try
            {
                Symbol = symbol;
                StatusMessage = $"已切换到合约: {symbol}";
                
                // 自动获取价格
                await UpdateLatestPriceAsync();
                
                // 刷新该合约的持仓和订单
                await RefreshDataAsync();
                
                _logger.LogInformation($"切换到最近合约: {symbol}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"切换合约失败: {ex.Message}";
                _logger.LogError(ex, $"切换到合约 {symbol} 失败");
            }
        }
        #endregion

        #region 账户管理功能
        [RelayCommand]
        private void ConfigureAccount()
        {
            try
            {
                // 创建新的账户配置ViewModel
                var accountConfigViewModel = new AccountConfigViewModel(_accountService);
                
                // 创建窗口并传入ViewModel
                var accountConfigWindow = new Views.AccountConfigWindow(accountConfigViewModel);
                
                // 设置窗口所有者为主窗口
                accountConfigWindow.Owner = System.Windows.Application.Current.MainWindow;
                
                // 显示模态对话框
                var result = accountConfigWindow.ShowDialog();
                
                if (result == true)
                {
                    // 用户保存了配置，重新加载账户列表
                    LoadAccounts();
                    StatusMessage = "新账户配置已保存";
                    _logger.LogInformation("新账户配置已保存");
                }
                else
                {
                    StatusMessage = "账户配置已取消";
                    _logger.LogInformation("账户配置已取消");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开账户配置失败");
                StatusMessage = $"打开账户配置失败: {ex.Message}";
            }
        }

        [RelayCommand]
        private void EditCurrentAccount()
        {
            if (SelectedAccount == null)
            {
                StatusMessage = "请先选择账户";
                return;
            }

            try
            {
                // 创建编辑现有账户的ViewModel
                var accountConfigViewModel = new AccountConfigViewModel(_accountService, SelectedAccount);
                
                // 创建窗口并传入ViewModel
                var accountConfigWindow = new Views.AccountConfigWindow(accountConfigViewModel);
                
                // 设置窗口所有者为主窗口
                accountConfigWindow.Owner = System.Windows.Application.Current.MainWindow;
                
                // 显示模态对话框
                var result = accountConfigWindow.ShowDialog();
                
                if (result == true)
                {
                    // 用户保存了配置，重新加载账户列表
                    LoadAccounts();
                    
                    // 尝试重新选择当前编辑的账户
                    var updatedAccount = Accounts.FirstOrDefault(a => a.Name == SelectedAccount.Name);
                    if (updatedAccount != null)
                    {
                        SelectedAccount = updatedAccount;
                    }
                    
                    StatusMessage = $"账户 '{SelectedAccount.Name}' 配置已更新";
                    _logger.LogInformation($"账户 '{SelectedAccount.Name}' 配置已更新");
                }
                else
                {
                    StatusMessage = "取消编辑账户配置";
                    _logger.LogInformation("取消编辑账户配置");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "编辑账户失败");
                StatusMessage = $"编辑账户失败: {ex.Message}";
            }
        }
        #endregion

        #region 历史数据查询
        [RelayCommand]
        private async Task CheckOrderHistoryAsync()
        {
            if (string.IsNullOrEmpty(Symbol))
            {
                StatusMessage = "请先输入合约名称";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "正在查询订单历史...";

                var history = await _binanceService.GetAllOrdersAsync(Symbol, 100);
                
                if (history.Any())
                {
                    StatusMessage = $"查询到 {history.Count} 条历史订单";
                    _logger.LogInformation($"查询到 {Symbol} 的 {history.Count} 条历史订单");
                }
                else
                {
                    StatusMessage = "未找到历史订单";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"查询失败: {ex.Message}";
                _logger.LogError(ex, "查询订单历史失败");
            }
            finally
            {
                IsLoading = false;
            }
        }
        #endregion
    }
} 