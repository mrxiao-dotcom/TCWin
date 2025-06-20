using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        /// 刷新账户数据（保持选择状态）
        /// </summary>
        private async Task RefreshAccountDataWithSelectionPreservation()
        {
            try
            {
                // 🔧 优化：尝试智能更新，如果失败再进行完整重建
                var intelligentUpdateSuccess = await TryIntelligentDataUpdate();
                if (intelligentUpdateSuccess)
                {
                    _logger.LogDebug("智能数据更新成功，选择状态完全保持");
                    return;
                }
                
                _logger.LogDebug("智能更新失败，执行完整数据重建");
                
                // 保存当前选择状态
                var selectedPositionSymbols = Positions.Where(p => p.IsSelected).Select(p => p.Symbol).ToHashSet();
                var selectedOrderIds = Orders.Where(o => o.IsSelected).Select(o => o.OrderId).ToHashSet();
                
                // 🔧 保存当前过滤状态，防止切换窗口时丢失
                var currentSymbolFilter = SelectedPosition?.Symbol;
                var hasReduceOnlyOrders = ReduceOnlyOrders.Count > 0;
                var hasFilteredOrders = FilteredOrders.Count > 0;

                // 获取新数据
                var newAccountInfo = await _binanceService.GetAccountInfoAsync();
                var newPositions = await _binanceService.GetPositionsAsync();
                var newOrders = await _binanceService.GetOpenOrdersAsync();

                if (newAccountInfo != null && newPositions != null && newOrders != null)
                {
                    // 使用Dispatcher确保UI更新在主线程批量进行
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        // 🔧 防闪烁关键：先计算市值数据，再更新UI
                        // 预先计算市值数据，避免显示中间的0值状态
                        newAccountInfo.CalculateMarginUsed(newPositions);
                        
                        // 现在可以安全地更新AccountInfo，不会出现0值闪烁
                        AccountInfo = newAccountInfo;
                        
                        // 清空并重新填充集合
                        Positions.Clear();
                        Orders.Clear();
                        
                        foreach (var position in newPositions)
                        {
                            Positions.Add(position);
                        }
                        
                        foreach (var order in newOrders)
                        {
                            Orders.Add(order);
                        }
                        
                        // 恢复选择状态
                        int restoredPositionCount = 0;
                        int restoredOrderCount = 0;
                        
                        foreach (var position in Positions)
                        {
                            if (selectedPositionSymbols.Contains(position.Symbol))
                            {
                                position.IsSelected = true;
                                restoredPositionCount++;
                            }
                        }
                        
                        // 恢复订单选择状态
                        foreach (var order in Orders)
                        {
                            if (selectedOrderIds.Contains(order.OrderId))
                            {
                                order.IsSelected = true;
                                restoredOrderCount++;
                            }
                        }
                        
                        // 🔧 修复：为所有订单添加选择状态监听
                        foreach (var order in Orders)
                        {
                            order.SelectionChanged += OnOrderSelectionChanged;
                        }
                        
                        // 强制通知选择状态属性更新
                        OnPropertyChanged(nameof(SelectedOrders));
                        OnPropertyChanged(nameof(HasSelectedOrders));
                        OnPropertyChanged(nameof(SelectedOrderCount));
                        OnPropertyChanged(nameof(SelectedPositions));
                        OnPropertyChanged(nameof(HasSelectedPositions));
                        OnPropertyChanged(nameof(SelectedPositionCount));
                        
                        // 🔧 新增：通知移动止损按钮工具提示更新
                        OnPropertyChanged(nameof(TrailingStopButtonTooltip));
                        
                        // 重新加载条件单数据（从API订单中识别条件单）
                        LoadConditionalOrdersFromApiOrders();
                        
                        // 🔧 重要：强制重新应用订单过滤，确保减仓型委托单正确显示
                        // 使用保存的过滤条件来恢复正确的显示状态
                        if (!string.IsNullOrEmpty(currentSymbolFilter))
                        {
                            _logger.LogDebug($"🔄 恢复按合约过滤: {currentSymbolFilter}");
                            FilterOrdersForPosition(currentSymbolFilter);
                        }
                        else
                        {
                            _logger.LogDebug("🔄 恢复显示所有订单");
                            FilterOrdersForPosition();
                        }
                        
                        // 🔧 额外验证：如果之前有减仓型订单，现在没有了，输出警告
                        if (hasReduceOnlyOrders && ReduceOnlyOrders.Count == 0)
                        {
                            _logger.LogWarning("⚠️ 检测到减仓型订单在刷新后消失，可能存在显示问题");
                        }
                        
                        // 自动计算可用风险金
                        if (SelectedAccount != null)
                        {
                            CalculateMaxRiskCapital();
                        }
                        
                        _logger.LogDebug($"完整数据重建完成，恢复了 {restoredPositionCount} 个持仓选择，{restoredOrderCount} 个订单选择");
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新账户数据时发生错误");
            }
        }

        /// <summary>
        /// 智能数据更新：只更新数值，不重建集合
        /// </summary>
        private async Task<bool> TryIntelligentDataUpdate()
        {
            try
            {
                // 获取新数据
                var newAccountInfo = await _binanceService.GetAccountInfoAsync();
                var newPositions = await _binanceService.GetPositionsAsync();
                var newOrders = await _binanceService.GetOpenOrdersAsync();

                if (newAccountInfo == null || newPositions == null || newOrders == null)
                    return false;

                // 检查数据结构是否发生重大变化（新增或删除项目）
                if (!IsDataStructureCompatible(newPositions, newOrders))
                {
                    _logger.LogDebug("检测到数据结构变化，无法进行智能更新");
                    return false;
                }

                // 在UI线程中执行智能更新
                bool updateResult = false;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    updateResult = PerformIntelligentUpdate(newAccountInfo, newPositions, newOrders);
                });

                return updateResult;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "智能数据更新失败，将执行完整重建");
                return false;
            }
        }

        /// <summary>
        /// 检查数据结构是否兼容智能更新
        /// </summary>
        private bool IsDataStructureCompatible(List<PositionInfo> newPositions, List<OrderInfo> newOrders)
        {
            // 检查持仓数量和合约是否匹配
            var currentPositionSymbols = Positions.Where(p => p.PositionAmt != 0).Select(p => p.Symbol).OrderBy(s => s).ToList();
            var newPositionSymbols = newPositions.Where(p => p.PositionAmt != 0).Select(p => p.Symbol).OrderBy(s => s).ToList();
            
            if (!currentPositionSymbols.SequenceEqual(newPositionSymbols))
            {
                _logger.LogDebug($"持仓合约发生变化：{string.Join(",", currentPositionSymbols)} -> {string.Join(",", newPositionSymbols)}");
                return false;
            }

            // 检查订单ID是否匹配（允许状态变化，但不允许新增或删除）
            var currentOrderIds = Orders.Select(o => o.OrderId).OrderBy(id => id).ToList();
            var newOrderIds = newOrders.Select(o => o.OrderId).OrderBy(id => id).ToList();
            
            if (!currentOrderIds.SequenceEqual(newOrderIds))
            {
                _logger.LogDebug($"委托单发生变化：{Orders.Count} -> {newOrders.Count} 个订单");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 执行智能更新：只更新数值，保持选择状态和集合结构
        /// </summary>
        private bool PerformIntelligentUpdate(AccountInfo newAccountInfo, List<PositionInfo> newPositions, List<OrderInfo> newOrders)
        {
            try
            {
                // 更新账户信息（直接替换，不影响选择）
                newAccountInfo.CalculateMarginUsed(newPositions);
                AccountInfo = newAccountInfo;

                // 智能更新持仓数据：只更新数值，保持选择状态和对象引用
                foreach (var currentPosition in Positions)
                {
                    var newPosition = newPositions.FirstOrDefault(p => p.Symbol == currentPosition.Symbol);
                    if (newPosition != null)
                    {
                        var wasSelected = currentPosition.IsSelected; // 保存选择状态
                        
                        // 更新数值属性
                        currentPosition.PositionAmt = newPosition.PositionAmt;
                        currentPosition.EntryPrice = newPosition.EntryPrice;
                        currentPosition.MarkPrice = newPosition.MarkPrice;
                        currentPosition.UnrealizedProfit = newPosition.UnrealizedProfit;
                        currentPosition.UpdateTime = newPosition.UpdateTime;
                        currentPosition.Leverage = newPosition.Leverage;
                        currentPosition.IsolatedMargin = newPosition.IsolatedMargin;
                        
                        // 恢复选择状态
                        currentPosition.IsSelected = wasSelected;
                    }
                }

                // 智能更新订单数据：只更新状态和数值，保持选择状态
                foreach (var currentOrder in Orders)
                {
                    var newOrder = newOrders.FirstOrDefault(o => o.OrderId == currentOrder.OrderId);
                    if (newOrder != null)
                    {
                        var wasSelected = currentOrder.IsSelected; // 保存选择状态
                        
                        // 更新可能变化的属性
                        currentOrder.Status = newOrder.Status;
                        currentOrder.ExecutedQty = newOrder.ExecutedQty;
                        currentOrder.CumQty = newOrder.CumQty;
                        currentOrder.CumQuote = newOrder.CumQuote;
                        currentOrder.UpdateTime = newOrder.UpdateTime;
                        
                        // 恢复选择状态
                        currentOrder.IsSelected = wasSelected;
                    }
                }

                // 重新计算可用风险金
                if (SelectedAccount != null)
                {
                    CalculateMaxRiskCapital();
                }
                
                // 🔧 修复：确保所有订单都有选择状态监听
                foreach (var order in Orders)
                {
                    order.SelectionChanged += OnOrderSelectionChanged;
                }

                _logger.LogDebug("智能数据更新完成，选择状态完全保持");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "智能更新执行失败");
                return false;
            }
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
            // 🔧 修复：正确判断订单分类，应该基于ReduceOnly属性而不是订单类型
            // ReduceOnly=true 或 ClosePosition=true 的订单是平仓型
            if (order.ReduceOnly || order.ClosePosition)
            {
                return "平仓型";
            }
            
            // ReduceOnly=false 的条件单是加仓型
            // 包括用于突破开仓的TAKE_PROFIT_MARKET、STOP_MARKET等
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
                // 🔧 修复：先移除旧订单的选择状态监听
                foreach (var order in FilteredOrders)
                {
                    order.SelectionChanged -= OnOrderSelectionChanged;
                }
                foreach (var order in ReduceOnlyOrders)
                {
                    order.SelectionChanged -= OnOrderSelectionChanged;
                }
                
                FilteredOrders.Clear();
                ReduceOnlyOrders.Clear();

                var ordersToShow = string.IsNullOrEmpty(symbol) 
                    ? Orders.ToList() 
                    : Orders.Where(o => o.Symbol == symbol).ToList();

                int reduceOnlyCount = 0;
                int addPositionCount = 0;

                _logger.LogDebug($"🔍 开始过滤订单，总订单数: {ordersToShow.Count}，过滤条件: {(string.IsNullOrEmpty(symbol) ? "全部" : symbol)}");

                foreach (var order in ordersToShow)
                {
                    _logger.LogDebug($"   检查订单: {order.Symbol} {order.Type} ReduceOnly={order.ReduceOnly} ClosePosition={order.ClosePosition}");
                    
                    // 🔧 修复：正确的订单分类逻辑
                    // 减仓型订单（ReduceOnly=true 或 ClosePosition=true）显示在上方委托单列表  
                    if (order.ReduceOnly || order.ClosePosition)
                    {
                        ReduceOnlyOrders.Add(order);
                        // 🔧 新增：为减仓型订单添加选择状态监听
                        order.SelectionChanged += OnOrderSelectionChanged;
                        reduceOnlyCount++;
                        _logger.LogDebug($"   ✅ 识别为减仓型订单: {order.Symbol} {order.Type}");
                    }
                    else
                    {
                        // 加仓型订单（ReduceOnly=false）显示在下方条件单列表
                        // 包括用于开仓的TAKE_PROFIT_MARKET、STOP_MARKET等条件单
                        FilteredOrders.Add(order);
                        // 🔧 新增：为加仓型订单添加选择状态监听
                        order.SelectionChanged += OnOrderSelectionChanged;
                        addPositionCount++;
                        _logger.LogDebug($"   ➕ 识别为加仓型订单: {order.Symbol} {order.Type}");
                    }
                }

                // 通知相关属性更新
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));
                OnPropertyChanged(nameof(HasSelectedStopOrders));
                OnPropertyChanged(nameof(SelectedStopOrderCount));
                OnPropertyChanged(nameof(ReduceOnlyOrders));

                _logger.LogDebug($"📊 订单过滤完成，减仓型订单: {reduceOnlyCount} 个，加仓型订单: {addPositionCount} 个" + 
                    (string.IsNullOrEmpty(symbol) ? "（全部）" : $"（{symbol}）"));
                _logger.LogDebug($"📋 ReduceOnlyOrders集合当前包含 {ReduceOnlyOrders.Count} 个订单");
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
                    
                    // 🔧 自动刷新风险管理按钮状态
                    try
                    {
                        AddProfitProtectionStopLossCommand?.NotifyCanExecuteChanged();
                        AddBreakEvenStopLossCommand?.NotifyCanExecuteChanged();
                        PartialProfitTakingCommand?.NotifyCanExecuteChanged();
                        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                    }
                    catch (Exception refreshEx)
                    {
                        _logger.LogWarning(refreshEx, "刷新命令状态失败");
                    }
                    
                    _logger.LogDebug($"选择持仓: {value.Symbol} {value.PositionSideString} {value.PositionAmt} 盈亏:{value.UnrealizedProfit:F2}U");
                }
                else
                {
                    // 取消选择，显示所有订单
                    FilterOrdersForPosition();
                    
                    // 🔧 取消选择时也刷新命令状态
                    try
                    {
                        AddProfitProtectionStopLossCommand?.NotifyCanExecuteChanged();
                        AddBreakEvenStopLossCommand?.NotifyCanExecuteChanged();
                        PartialProfitTakingCommand?.NotifyCanExecuteChanged();
                        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                    }
                    catch (Exception refreshEx)
                    {
                        _logger.LogWarning(refreshEx, "刷新命令状态失败");
                    }
                    
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

        #region 手动刷新功能
        /// <summary>
        /// 强制刷新订单分类显示
        /// </summary>
        [RelayCommand]
        private void ForceRefreshOrderDisplay()
        {
            try
            {
                _logger.LogInformation("🔄 手动强制刷新订单分类显示...");
                
                // 强制重新执行订单过滤
                if (SelectedPosition != null)
                {
                    _logger.LogDebug($"按选中持仓过滤: {SelectedPosition.Symbol}");
                    FilterOrdersForPosition(SelectedPosition.Symbol);
                }
                else
                {
                    _logger.LogDebug("显示所有订单");
                    FilterOrdersForPosition();
                }
                
                // 强制通知UI更新
                OnPropertyChanged(nameof(ReduceOnlyOrders));
                OnPropertyChanged(nameof(FilteredOrders));
                OnPropertyChanged(nameof(Orders));
                
                StatusMessage = $"✅ 订单显示已刷新 - 减仓型: {ReduceOnlyOrders.Count}, 加仓型: {FilteredOrders.Count}";
                _logger.LogInformation($"手动刷新完成 - 减仓型订单: {ReduceOnlyOrders.Count}，加仓型订单: {FilteredOrders.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "强制刷新订单显示失败");
                StatusMessage = $"❌ 刷新失败: {ex.Message}";
            }
        }
        #endregion
    }
} 