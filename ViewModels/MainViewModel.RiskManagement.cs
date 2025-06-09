using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
            // ⚠️ 重要：保存选中持仓的引用，避免在异步操作过程中变成null
            var targetPosition = SelectedPosition;
            
            if (targetPosition == null)
            {
                StatusMessage = "请先选择要添加保本止损的持仓";
                return;
            }
            
            // 防止重复执行
            if (IsLoading)
            {
                StatusMessage = "正在处理中，请稍候...";
                return;
            }

            try
            {
                // 显示确认对话框
                var entryPrice = targetPosition.EntryPrice;
                var symbol = targetPosition.Symbol;
                var direction = targetPosition.PositionAmt > 0 ? "做多" : "做空";
                var quantity = Math.Abs(targetPosition.PositionAmt);
                
                var confirmMessage = $"确认为 {symbol} {direction} 持仓添加保本止损？\n\n" +
                                   $"持仓数量: {quantity:F8}\n" +
                                   $"入场价格: {entryPrice:F4}\n" +
                                   $"保本止损价: {entryPrice:F4}\n\n" +
                                   $"注意：这将清理该合约所有现有的止损委托！";

                var result = System.Windows.MessageBox.Show(
                    confirmMessage,
                    "确认保本止损",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    StatusMessage = "保本止损操作已取消";
                    _logger.LogInformation("用户取消保本止损操作");
                    return;
                }

                IsLoading = true;
                StatusMessage = $"正在为 {targetPosition.Symbol} 添加保本止损...";
                _logger.LogInformation($"🎯 开始为 {targetPosition.Symbol} 添加保本止损，用户已确认");

                // 计算保本价格（入场价格）
                var stopPrice = targetPosition.EntryPrice;
                var side = targetPosition.PositionAmt > 0 ? "SELL" : "BUY";

                _logger.LogInformation($"📊 保本止损参数: 合约={symbol}, 方向={direction}, 数量={quantity:F8}, 止损价={stopPrice:F4}");

                // 先刷新数据确保获取最新的订单信息
                StatusMessage = "正在刷新数据，获取最新委托单信息...";
                await RefreshDataAsync();
                
                // 清理该合约所有历史止损委托
                StatusMessage = $"正在清理 {targetPosition.Symbol} 的历史止损委托...";
                await CleanupAllStopOrdersAsync(targetPosition.Symbol);

                var stopLossRequest = new OrderRequest
                {
                    Symbol = targetPosition.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = quantity,
                    StopPrice = stopPrice,
                    ReduceOnly = true,
                    PositionSide = targetPosition.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                // 详细记录订单参数
                _logger.LogInformation($"📋 保本止损订单详细参数:");
                _logger.LogInformation($"   Symbol: {stopLossRequest.Symbol}");
                _logger.LogInformation($"   Side: {stopLossRequest.Side}");
                _logger.LogInformation($"   Type: {stopLossRequest.Type}");
                _logger.LogInformation($"   Quantity: {stopLossRequest.Quantity:F8}");
                _logger.LogInformation($"   StopPrice: {stopLossRequest.StopPrice:F4}");
                _logger.LogInformation($"   ReduceOnly: {stopLossRequest.ReduceOnly}");
                _logger.LogInformation($"   PositionSide: {stopLossRequest.PositionSide}");

                StatusMessage = $"正在提交保本止损单: {targetPosition.Symbol} @{stopPrice:F4}...";

                try 
                {
                    _logger.LogInformation($"🚀 准备调用BinanceService.PlaceOrderAsync...");
                    _logger.LogInformation($"📦 保本止损订单请求详情: Symbol={stopLossRequest.Symbol}, Side={stopLossRequest.Side}, Type={stopLossRequest.Type}, Quantity={stopLossRequest.Quantity:F8}, StopPrice={stopLossRequest.StopPrice:F4}, ReduceOnly={stopLossRequest.ReduceOnly}");
                    
                    var success = await _binanceService.PlaceOrderAsync(stopLossRequest);
                    _logger.LogInformation($"📬 保本止损PlaceOrderAsync返回结果: {success}");
                    

                    
                    if (success)
                    {
                        StatusMessage = $"✅ 保本止损提交成功: {targetPosition.Symbol} @{stopPrice:F4}";
                        _logger.LogInformation($"✅ 保本止损提交成功: {targetPosition.Symbol} @{stopPrice:F4}");
                        
                        // 等待一段时间让订单生效
                        StatusMessage = "等待订单生效...";
                        await Task.Delay(1000);
                        
                        // 刷新数据以显示新的止损单
                        StatusMessage = "正在刷新数据，显示新的止损委托...";
                        await RefreshDataAsync();
                        
                        // 🔧 修复：使用保存的targetPosition引用，避免SelectedPosition变成null
                        var newStopOrders = Orders.Where(o => 
                            o.Symbol == targetPosition.Symbol && 
                            o.Type == "STOP_MARKET" && 
                            o.ReduceOnly &&
                            Math.Abs(o.StopPrice - stopPrice) < 0.01m).ToList();
                            
                        if (newStopOrders.Any())
                        {
                            StatusMessage = $"✅ 保本止损设置完成: {targetPosition.Symbol} @{stopPrice:F4} - 委托单ID: {newStopOrders.First().OrderId}";
                            _logger.LogInformation($"✅ 验证成功：找到新的保本止损委托单 #{newStopOrders.First().OrderId}");
                        }
                        else
                        {
                            StatusMessage = $"⚠️ 止损单提交成功但未在委托列表中找到，请手动刷新查看";
                            _logger.LogWarning($"⚠️ 保本止损单提交成功但未在委托列表中找到");
                            
                            // 🔧 增强调试：显示当前所有订单信息
                            _logger.LogInformation($"🔍 调试信息 - 当前订单总数: {Orders.Count}");
                            _logger.LogInformation($"🔍 调试信息 - {targetPosition.Symbol}相关订单:");
                            foreach (var order in Orders.Where(o => o.Symbol == targetPosition.Symbol))
                            {
                                _logger.LogInformation($"  订单#{order.OrderId}: {order.Type} {order.Side} @{order.StopPrice:F4} ReduceOnly={order.ReduceOnly}");
                            }
                        }
                        
                        // 🚀 自动执行刷新操作
                        try
                        {
                            _logger.LogInformation("🔄 保本止损完成，自动执行刷新操作...");
                            await RefreshCommandStatesAsync();
                        }
                        catch (Exception refreshEx)
                        {
                            _logger.LogWarning(refreshEx, "自动刷新失败，请手动点击刷新按钮");
                        }
                    }
                    else
                    {
                        StatusMessage = $"❌ 保本止损提交失败: {targetPosition.Symbol} - 请检查网络连接和API权限";
                        _logger.LogError($"❌ 保本止损提交失败: {targetPosition.Symbol}, 参数: {side} @{stopPrice:F4}, 数量: {quantity:F8}");
                    }
                }
                catch (Exception orderEx)
                {
                    StatusMessage = $"❌ 保本止损下单过程异常: {orderEx.Message}";
                    _logger.LogError(orderEx, $"❌ 保本止损下单过程异常: {targetPosition.Symbol}");
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



        [RelayCommand(CanExecute = nameof(CanExecuteAddProfitProtectionStopLoss))]
        private async Task AddProfitProtectionStopLossAsync()
        {

            
            // ⚠️ 重要：保存选中持仓的引用，避免在异步操作过程中变成null
            var targetPosition = SelectedPosition;
            
            if (targetPosition == null)
            {
                StatusMessage = "请先选择要添加盈利保护止损的持仓";
                return;
            }
            
            // 防止重复执行
            if (IsLoading)
            {
                StatusMessage = "正在处理中，请稍候...";
                return;
            }
            
            _logger.LogInformation($"📊 当前选择持仓: {targetPosition.Symbol}, 盈亏: {targetPosition.UnrealizedProfit:F2}U");

            if (targetPosition.UnrealizedProfit <= 0)
            {
                StatusMessage = "该持仓没有盈利，无需添加盈利保护";
                _logger.LogWarning($"❌ 保盈止损失败: 持仓 {targetPosition.Symbol} 盈利不足 ({targetPosition.UnrealizedProfit:F2}U)");
                return;
            }
            
            _logger.LogInformation("✅ 保盈止损初始检查通过，准备显示对话框");

            try
            {
                // 显示保盈止损输入对话框
                _logger.LogInformation("🎨 正在创建保盈止损对话框...");
                var dialog = new Views.ProfitProtectionDialog(
                    targetPosition.Symbol,
                    targetPosition.PositionAmt > 0 ? "做多" : "做空",
                    Math.Abs(targetPosition.PositionAmt),
                    targetPosition.EntryPrice,
                    targetPosition.UnrealizedProfit,
                    targetPosition.MarkPrice);

                // 确保对话框在主窗口上显示
                if (System.Windows.Application.Current.MainWindow != null)
                {
                    dialog.Owner = System.Windows.Application.Current.MainWindow;
                }

                StatusMessage = "请在弹出的对话框中设置保盈止损...";
                _logger.LogInformation($"🎨 显示保盈止损设置对话框: {targetPosition.Symbol}");
                _logger.LogInformation($"📝 对话框参数: 合约={targetPosition.Symbol}, 方向={(targetPosition.PositionAmt > 0 ? "做多" : "做空")}, 数量={Math.Abs(targetPosition.PositionAmt):F8}, 入场价={targetPosition.EntryPrice:F4}, 盈利={targetPosition.UnrealizedProfit:F2}U, 当前价={targetPosition.MarkPrice:F4}");

                var dialogResult = dialog.ShowDialog();
                _logger.LogInformation($"🎨 对话框结果: {(dialogResult == true ? "用户确认" : "用户取消")}");
                

                
                if (dialogResult != true)
                {
                    StatusMessage = "保盈止损设置已取消";
                    _logger.LogInformation("❌ 用户取消保盈止损设置");
                    return;
                }

                var protectionAmount = dialog.ProfitProtectionAmount;
                _logger.LogInformation($"🎯 用户设置保盈止损金额: {protectionAmount:F2}U");
                

                
                IsLoading = true;
                StatusMessage = $"正在为 {targetPosition.Symbol} 添加盈利保护止损...";

                // 根据保护金额计算止损价格
                var isLong = targetPosition.PositionAmt > 0;
                var entryPrice = targetPosition.EntryPrice;
                var quantity = Math.Abs(targetPosition.PositionAmt);
                var currentPrice = targetPosition.MarkPrice;
                
                _logger.LogInformation($"📊 保盈止损计算参数: 方向={(isLong ? "多头" : "空头")}, 入场价={entryPrice:F4}, 当前价={currentPrice:F4}, 数量={quantity:F8}, 保护金额={protectionAmount:F2}U");
                
                decimal protectionPrice;
                if (isLong)
                {
                    // 多头：止损价 = 开仓价 + (保护盈利 / 持仓数量)
                    protectionPrice = entryPrice + (protectionAmount / quantity);
                    _logger.LogInformation($"💰 多头计算: {entryPrice:F4} + ({protectionAmount:F2} / {quantity:F8}) = {protectionPrice:F4}");
                }
                else
                {
                    // 空头：止损价 = 开仓价 - (保护盈利 / 持仓数量)
                    protectionPrice = entryPrice - (protectionAmount / quantity);
                    _logger.LogInformation($"💰 空头计算: {entryPrice:F4} - ({protectionAmount:F2} / {quantity:F8}) = {protectionPrice:F4}");
                }

                // 验证止损价的合理性
                bool isValidStopPrice = false;
                string validationMessage = "";
                
                if (isLong)
                {
                    isValidStopPrice = protectionPrice < currentPrice && protectionPrice > entryPrice;
                    validationMessage = isValidStopPrice ? "合理" : $"不合理(止损价应在 {entryPrice:F4} 到 {currentPrice:F4} 之间)";
                }
                else
                {
                    isValidStopPrice = protectionPrice > currentPrice && protectionPrice < entryPrice;
                    validationMessage = isValidStopPrice ? "合理" : $"不合理(止损价应在 {currentPrice:F4} 到 {entryPrice:F4} 之间)";
                }
                
                _logger.LogInformation($"🔍 止损价验证: {protectionPrice:F4} - {validationMessage}");
                
                if (!isValidStopPrice)
                {
                    StatusMessage = $"❌ 计算的止损价不合理: {protectionPrice:F4} ({validationMessage})";
                    _logger.LogError($"❌ 保盈止损价格不合理，终止操作");
                    return;
                }

                _logger.LogInformation($"💰 最终保盈止损价: {protectionPrice:F4}, 入场价: {entryPrice:F4}, 保护金额: {protectionAmount:F2}U, 方向: {(isLong ? "多头" : "空头")}");

                // 先刷新数据确保获取最新的订单信息
                StatusMessage = "正在刷新数据，获取最新委托单信息...";
                await RefreshDataAsync();
                
                // 清理该合约所有历史止损委托
                StatusMessage = $"正在清理 {targetPosition.Symbol} 的历史止损委托...";
                await CleanupAllStopOrdersAsync(targetPosition.Symbol);

                var side = isLong ? "SELL" : "BUY";
                
                // 验证止损单参数
                _logger.LogInformation($"🎯 准备下止损单: {targetPosition.Symbol} {side} @{protectionPrice:F4}, 数量: {quantity:F8}, PositionSide: {targetPosition.PositionSideString}");

                var stopLossRequest = new OrderRequest
                {
                    Symbol = targetPosition.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = quantity,
                    StopPrice = protectionPrice,
                    ReduceOnly = true,
                    PositionSide = targetPosition.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                // 详细记录订单参数
                _logger.LogInformation($"📋 保盈止损订单详细参数:");
                _logger.LogInformation($"   Symbol: {stopLossRequest.Symbol}");
                _logger.LogInformation($"   Side: {stopLossRequest.Side}");
                _logger.LogInformation($"   Type: {stopLossRequest.Type}");
                _logger.LogInformation($"   Quantity: {stopLossRequest.Quantity:F8}");
                _logger.LogInformation($"   StopPrice: {stopLossRequest.StopPrice:F4}");
                _logger.LogInformation($"   ReduceOnly: {stopLossRequest.ReduceOnly}");
                _logger.LogInformation($"   PositionSide: {stopLossRequest.PositionSide}");
                _logger.LogInformation($"   WorkingType: {stopLossRequest.WorkingType}");

                StatusMessage = $"正在提交保盈止损单: {targetPosition.Symbol} @{protectionPrice:F4}...";
                
                try 
                {
                    _logger.LogInformation($"🚀 调用BinanceService.PlaceOrderAsync...");
                    

                    
                    var success = await _binanceService.PlaceOrderAsync(stopLossRequest);
                    _logger.LogInformation($"📬 PlaceOrderAsync返回结果: {success}");
                    

                    
                    if (success)
                    {
                        StatusMessage = $"✅ 保盈止损提交成功: {targetPosition.Symbol} @{protectionPrice:F4} (保护{protectionAmount:F2}U盈利)";
                        _logger.LogInformation($"✅ 保盈止损提交成功: {targetPosition.Symbol} @{protectionPrice:F4}, 保护盈利: {protectionAmount:F2}U");
                        
                        // 等待一段时间让订单生效
                        StatusMessage = "等待订单生效...";
                        await Task.Delay(1000);
                        
                        // 刷新数据以显示新的止损单
                        StatusMessage = "正在刷新数据，显示新的止损委托...";
                        await RefreshDataAsync();
                        
                        // 验证止损单是否真的创建成功
                        var newStopOrders = Orders.Where(o => 
                            o.Symbol == targetPosition.Symbol && 
                            o.Type == "STOP_MARKET" && 
                            o.ReduceOnly &&
                            Math.Abs(o.StopPrice - protectionPrice) < 0.01m).ToList();
                            
                        if (newStopOrders.Any())
                        {
                            StatusMessage = $"✅ 保盈止损设置完成: {targetPosition.Symbol} @{protectionPrice:F4} (保护{protectionAmount:F2}U) - 委托单ID: {newStopOrders.First().OrderId}";
                            _logger.LogInformation($"✅ 验证成功：找到新的止损委托单 #{newStopOrders.First().OrderId}");
                        }
                        else
                        {
                            StatusMessage = $"⚠️ 止损单提交成功但未在委托列表中找到，请手动刷新查看";
                            _logger.LogWarning($"⚠️ 止损单提交成功但未在委托列表中找到");
                        }
                        
                        // 🚀 自动执行刷新操作
                        try
                        {
                            _logger.LogInformation("🔄 保盈止损完成，自动执行刷新操作...");
                            await RefreshCommandStatesAsync();
                        }
                        catch (Exception refreshEx)
                        {
                            _logger.LogWarning(refreshEx, "自动刷新失败，请手动点击刷新按钮");
                        }
                    }
                    else
                    {
                        StatusMessage = $"❌ 保盈止损提交失败: {targetPosition.Symbol} - 请检查网络连接和API权限";
                        _logger.LogError($"❌ 保盈止损提交失败: {targetPosition.Symbol}, 参数: {side} @{protectionPrice:F4}, 数量: {quantity:F8}");
                    }
                }
                catch (Exception orderEx)
                {
                    StatusMessage = $"❌ 下单过程异常: {orderEx.Message}";
                    _logger.LogError(orderEx, $"❌ 保盈止损下单过程异常: {targetPosition.Symbol}");
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
                _logger.LogInformation("🔚 保盈止损方法执行完成，IsLoading已重置");
            }
        }

        /// <summary>
        /// 检查是否可以执行保盈止损命令
        /// </summary>
        private bool CanExecuteAddProfitProtectionStopLoss()
        {
            // 基本条件检查
            if (SelectedPosition == null)
            {
                _logger.LogDebug("🔍 保盈止损CanExecute: SelectedPosition=null");
                return false;
            }
            
            if (IsLoading)
            {
                _logger.LogDebug("🔍 保盈止损CanExecute: IsLoading=true");
                return false;
            }
            
            // 盈利检查 - 保盈止损只对有盈利的持仓有效
            if (SelectedPosition.UnrealizedProfit <= 0)
            {
                _logger.LogDebug($"🔍 保盈止损CanExecute: {SelectedPosition.Symbol} 盈利不足 ({SelectedPosition.UnrealizedProfit:F2}U)");
                return false;
            }
            
            _logger.LogDebug($"✅ 保盈止损CanExecute: {SelectedPosition.Symbol} 盈利={SelectedPosition.UnrealizedProfit:F2}U - 可执行");
            return true;
        }

        /// <summary>
        /// 刷新状态和数据
        /// </summary>
        [RelayCommand]
        private async Task RefreshCommandStatesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在刷新数据和状态...";
                
                // 刷新数据
                await RefreshDataAsync();
                
                // 强制刷新所有RelayCommand的CanExecute状态
                AddProfitProtectionStopLossCommand.NotifyCanExecuteChanged();
                AddBreakEvenStopLossCommand.NotifyCanExecuteChanged();
                
                // 强制刷新WPF命令管理器
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                
                _logger.LogInformation($"🔄 刷新完成 - 持仓: {Positions?.Count ?? 0}, 订单: {Orders?.Count ?? 0}");
                StatusMessage = $"刷新完成 - 持仓: {Positions?.Count ?? 0}, 订单: {Orders?.Count ?? 0}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新过程失败");
                StatusMessage = $"刷新失败: {ex.Message}";
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
                _logger.LogInformation($"🧹 开始清理合约 {symbol} 的历史止损委托，当前订单总数: {Orders.Count}");
                
                // 筛选止损单 - 包括所有类型的止损委托
                var stopOrders = Orders.Where(o => 
                    o.Symbol == symbol && 
                    (o.Type == "STOP_MARKET" || o.Type == "TAKE_PROFIT_MARKET" || o.Type == "TRAILING_STOP_MARKET") && 
                    o.ReduceOnly).ToList();

                _logger.LogInformation($"🔍 筛选结果: 找到 {stopOrders.Count} 个止损类委托单");
                

                
                if (stopOrders.Any())
                {
                    foreach (var order in stopOrders)
                    {
                        _logger.LogInformation($"📋 待清理止损单: {order.Symbol} #{order.OrderId} {order.Type} @{order.StopPrice:F4} {order.Side} {order.OrigQty:F8}");
                    }
                    
                    StatusMessage = $"正在清理 {stopOrders.Count} 个历史止损委托...";
                    
                    var canceledCount = 0;
                    foreach (var order in stopOrders)
                    {
                        try
                        {
                            _logger.LogInformation($"🗑️ 尝试取消止损单: {order.Symbol} #{order.OrderId}");
                            var canceled = await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                            
                            if (canceled)
                            {
                                canceledCount++;
                                _logger.LogInformation($"✅ 成功取消止损单: {order.Symbol} #{order.OrderId} @{order.StopPrice:F4}");
                            }
                            else
                            {
                                _logger.LogWarning($"❌ 取消止损单失败: {order.Symbol} #{order.OrderId} (可能已执行或不存在)");
                            }
                            
                            // 避免API限制
                            await Task.Delay(150);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"❌ 取消止损单异常: {order.Symbol} #{order.OrderId} - {ex.Message}");
                        }
                    }
                    
                    _logger.LogInformation($"🧹 历史止损单清理完成: 成功取消 {canceledCount}/{stopOrders.Count} 个");
                    
                    // 等待订单取消生效
                    if (canceledCount > 0)
                    {
                        _logger.LogInformation("⏰ 等待订单取消生效...");
                        await Task.Delay(500);
                    }
                }
                else
                {
                    _logger.LogInformation($"✨ 合约 {symbol} 无历史止损委托需要清理");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 清理历史止损单过程异常: {symbol} - {ex.Message}");
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

                var mode = TrailingStopConfig.Mode;
                _logger.LogInformation($"开始处理移动止损（{mode}模式）...");
                var processedCount = 0;
                
                // 根据配置决定处理哪些持仓
                var targetPositions = TrailingStopConfig.OnlyForProfitablePositions 
                    ? Positions.Where(p => p.PositionAmt != 0 && p.UnrealizedProfit > 0).ToList()
                    : Positions.Where(p => p.PositionAmt != 0).ToList();
                
                foreach (var position in targetPositions)
                {
                    // 检查是否已有移动止损单
                    var existingTrailingStops = Orders.Where(o => 
                        o.Symbol == position.Symbol && 
                        o.Type == "TRAILING_STOP_MARKET" && 
                        o.Status == "NEW" &&
                        o.ReduceOnly).ToList();
                    
                    if (existingTrailingStops.Any())
                    {
                        _logger.LogInformation($"持仓 {position.Symbol} 已有移动止损单，跳过");
                        continue;
                    }

                    // 根据不同模式处理
                    bool success = false;
                    switch (mode)
                    {
                        case TrailingStopMode.Replace:
                            success = await ProcessReplaceMode(position);
                            break;
                        case TrailingStopMode.Coexist:
                            success = await ProcessCoexistMode(position);
                            break;
                        case TrailingStopMode.SmartLayering:
                            success = await ProcessSmartLayeringMode(position);
                            break;
                    }
                    
                    if (success)
                        processedCount++;
                    
                    // 避免API频率限制
                    if (processedCount > 0)
                        await Task.Delay(300);
                }
                
                if (processedCount > 0)
                {
                    StatusMessage = $"移动止损处理完成（{mode}模式），共处理 {processedCount} 个持仓";
                    _logger.LogInformation($"移动止损处理完成（{mode}模式），共处理 {processedCount} 个持仓");
                }
                else
                {
                    StatusMessage = $"没有需要处理的持仓（{mode}模式）";
                    _logger.LogInformation($"没有找到需要设置移动止损的持仓（{mode}模式）");
                }

                // 更新移动止损状态
                await UpdateTrailingStopStatusesAsync();
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
        /// 创建与固定止损并存的移动止损单
        /// </summary>
        private async Task<bool> CreateCoexistingTrailingStopAsync(PositionInfo position, List<OrderInfo> existingFixedStops)
        {
            try
            {
                // 确定下单方向
                var side = position.PositionAmt > 0 ? "SELL" : "BUY";
                
                // 获取当前价格作为参考
                var currentPrice = await _binanceService.GetLatestPriceAsync(position.Symbol);
                
                // 【并存模式关键】：计算移动止损数量，避免超过持仓总量
                var totalFixedStopQuantity = existingFixedStops.Sum(o => o.OrigQty);
                var totalPositionQuantity = Math.Abs(position.PositionAmt);
                
                // 移动止损使用剩余数量，但至少保持持仓的20%，最多50%
                var remainingQuantity = totalPositionQuantity - totalFixedStopQuantity;
                var minTrailingQuantity = totalPositionQuantity * 0.2m; // 最少20%
                var maxTrailingQuantity = totalPositionQuantity * 0.5m; // 最多50%
                
                var trailingQuantity = Math.Max(minTrailingQuantity, 
                    Math.Min(maxTrailingQuantity, remainingQuantity));
                
                if (trailingQuantity <= 0)
                {
                    _logger.LogWarning($"持仓 {position.Symbol} 的可用数量不足，跳过移动止损设置");
                    return false;
                }
                
                // 计算移动止损的回调率（相对保守一些）
                var profitPercentage = (position.UnrealizedProfit / position.NotionalValue) * 100;
                decimal callbackRate;
                
                if (profitPercentage >= 10) callbackRate = 2.0m; // 2%
                else if (profitPercentage >= 5) callbackRate = 1.5m; // 1.5%
                else if (profitPercentage >= 2) callbackRate = 1.0m; // 1%
                else callbackRate = 0.8m; // 0.8%
                
                // 创建移动止损单
                var trailingStopRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = side,
                    Type = "TRAILING_STOP_MARKET",
                    Quantity = trailingQuantity,
                    CallbackRate = callbackRate, // 使用百分比值
                    ReduceOnly = true,
                    PositionSide = position.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                _logger.LogInformation($"创建并存移动止损: {position.Symbol}, 持仓总量: {totalPositionQuantity:F8}, 固定止损量: {totalFixedStopQuantity:F8}, 移动止损量: {trailingQuantity:F8}, 回调率: {callbackRate:F2}%");

                var success = await _binanceService.PlaceOrderAsync(trailingStopRequest);
                if (success)
                {
                    _logger.LogInformation($"✅ 并存移动止损单创建成功: {position.Symbol} 回调率{callbackRate:F2}%");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"❌ 并存移动止损单创建失败: {position.Symbol}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建并存移动止损失败: {position.Symbol}");
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

        /// <summary>
        /// 测试保盈止损功能 - 用于调试
        /// </summary>
        [RelayCommand]
        public async Task TestProfitProtectionAsync()
        {
            try
            {
                _logger.LogInformation("🧪 开始测试保盈止损功能...");
                
                if (SelectedPosition == null)
                {
                    StatusMessage = "❌ 请先选择一个持仓进行测试";
                    return;
                }
                
                // 模拟保盈止损参数
                var testProtectionAmount = 10.0m; // 保护10U盈利
                var currentPrice = SelectedPosition.MarkPrice;
                var entryPrice = SelectedPosition.EntryPrice;
                var positionSize = Math.Abs(SelectedPosition.PositionAmt);
                
                _logger.LogInformation($"🧪 测试参数:");
                _logger.LogInformation($"   持仓: {SelectedPosition.Symbol}");
                _logger.LogInformation($"   方向: {SelectedPosition.PositionSideString}");
                _logger.LogInformation($"   入场价: {entryPrice:F4}");
                _logger.LogInformation($"   当前价: {currentPrice:F4}");
                _logger.LogInformation($"   持仓量: {positionSize:F8}");
                _logger.LogInformation($"   保护盈利: {testProtectionAmount:F2}U");
                
                // 计算止损价
                decimal protectionPrice;
                string side;
                
                if (SelectedPosition.PositionAmt > 0) // 多头
                {
                    protectionPrice = entryPrice + (testProtectionAmount / positionSize);
                    side = "SELL";
                    _logger.LogInformation($"🧪 多头止损价计算: {entryPrice:F4} + ({testProtectionAmount:F2} ÷ {positionSize:F8}) = {protectionPrice:F4}");
                }
                else // 空头
                {
                    protectionPrice = entryPrice - (testProtectionAmount / positionSize);
                    side = "BUY";
                    _logger.LogInformation($"🧪 空头止损价计算: {entryPrice:F4} - ({testProtectionAmount:F2} ÷ {positionSize:F8}) = {protectionPrice:F4}");
                }
                
                // 创建测试订单
                var testOrder = new OrderRequest
                {
                    Symbol = SelectedPosition.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = positionSize,
                    StopPrice = protectionPrice,
                    ReduceOnly = true,
                    PositionSide = SelectedPosition.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };
                
                _logger.LogInformation($"🧪 测试订单详情:");
                _logger.LogInformation($"   Symbol: {testOrder.Symbol}");
                _logger.LogInformation($"   Side: {testOrder.Side}");
                _logger.LogInformation($"   Type: {testOrder.Type}");
                _logger.LogInformation($"   Quantity: {testOrder.Quantity:F8}");
                _logger.LogInformation($"   StopPrice: {testOrder.StopPrice:F4}");
                _logger.LogInformation($"   ReduceOnly: {testOrder.ReduceOnly}");
                _logger.LogInformation($"   PositionSide: {testOrder.PositionSide}");
                
                StatusMessage = $"🧪 测试提交保盈止损单: {SelectedPosition.Symbol} @{protectionPrice:F4}...";
                
                var success = await _binanceService.PlaceOrderAsync(testOrder);
                
                if (success)
                {
                    StatusMessage = $"✅ 测试成功: 保盈止损单已提交 {SelectedPosition.Symbol} @{protectionPrice:F4}";
                    _logger.LogInformation($"✅ 测试成功: 保盈止损单提交成功");
                }
                else
                {
                    StatusMessage = $"❌ 测试失败: 保盈止损单提交失败";
                    _logger.LogError($"❌ 测试失败: 保盈止损单提交失败");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ 测试异常: {ex.Message}";
                _logger.LogError(ex, "🧪 测试保盈止损功能异常");
            }
        }

        /// <summary>
        /// 测试币安API是否支持多个止损单
        /// </summary>
        [RelayCommand]
        private async Task TestMultipleStopOrdersAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在测试币安API多止损单支持...";
                _logger.LogInformation("开始测试币安API是否支持多个止损单");

                // 找一个有持仓的合约进行测试
                var testPosition = Positions.FirstOrDefault(p => Math.Abs(p.PositionAmt) > 0);
                if (testPosition == null)
                {
                    StatusMessage = "❌ 测试失败：需要至少一个持仓进行测试";
                    _logger.LogWarning("无法测试：没有找到可用的持仓");
                    return;
                }

                var symbol = testPosition.Symbol;
                var isLong = testPosition.PositionAmt > 0;
                var side = isLong ? "SELL" : "BUY";
                var currentPrice = await _binanceService.GetLatestPriceAsync(symbol);
                
                // 计算两个不同的止损价格
                var stopPrice1 = isLong ? currentPrice * 0.95m : currentPrice * 1.05m; // 5%止损
                var stopPrice2 = isLong ? currentPrice * 0.98m : currentPrice * 1.02m; // 2%止损
                
                // 使用很小的测试数量
                var testQuantity = Math.Abs(testPosition.PositionAmt) * 0.01m; // 1%的仓位用于测试
                
                _logger.LogInformation($"测试参数: {symbol}, 方向={side}, 数量={testQuantity:F8}, 止损价1={stopPrice1:F4}, 止损价2={stopPrice2:F4}");

                // 第一个测试止损单
                var request1 = new OrderRequest
                {
                    Symbol = symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = testQuantity,
                    StopPrice = stopPrice1,
                    ReduceOnly = true,
                    PositionSide = testPosition.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                StatusMessage = "正在创建第一个测试止损单...";
                var success1 = await _binanceService.PlaceOrderAsync(request1);
                _logger.LogInformation($"第一个测试止损单结果: {success1}");
                
                if (!success1)
                {
                    StatusMessage = "❌ 测试失败：第一个止损单创建失败";
                    return;
                }

                await Task.Delay(1000); // 等待1秒

                // 第二个测试止损单
                var request2 = new OrderRequest
                {
                    Symbol = symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = testQuantity,
                    StopPrice = stopPrice2,
                    ReduceOnly = true,
                    PositionSide = testPosition.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                StatusMessage = "正在创建第二个测试止损单...";
                var success2 = await _binanceService.PlaceOrderAsync(request2);
                _logger.LogInformation($"第二个测试止损单结果: {success2}");

                // 分析测试结果
                if (success1 && success2)
                {
                    StatusMessage = "✅ 测试成功：币安API支持多个止损单！";
                    _logger.LogInformation("🎉 测试成功：币安API支持为同一持仓创建多个止损单");
                    
                    // 等待一下，然后清理测试订单
                    await Task.Delay(2000);
                    StatusMessage = "正在清理测试订单...";
                    await CleanupTestOrdersAsync(symbol);
                    
                    StatusMessage = "✅ 多止损单支持测试完成，可以启用并存模式";
                }
                else if (success1 && !success2)
                {
                    StatusMessage = "❌ 测试结果：币安API只允许一个止损单";
                    _logger.LogWarning("测试结果：第二个止损单创建失败，可能API只允许一个止损单");
                    
                    // 清理第一个测试订单
                    await CleanupTestOrdersAsync(symbol);
                }
                else
                {
                    StatusMessage = "❌ 测试失败：无法创建止损单";
                    _logger.LogError("测试失败：两个止损单都创建失败");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ 测试异常: {ex.Message}";
                _logger.LogError(ex, "测试多止损单支持时发生异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 处理替换模式（原有逻辑）
        /// </summary>
        private async Task<bool> ProcessReplaceMode(PositionInfo position)
        {
            var existingStopOrder = Orders.FirstOrDefault(o => 
                o.Symbol == position.Symbol && 
                o.Type == "STOP_MARKET" && 
                o.Status == "NEW" &&
                o.ReduceOnly);
            
            if (existingStopOrder != null)
            {
                // 转换现有止损单为移动止损
                return await ConvertToTrailingStopAsync(existingStopOrder);
            }
            else
            {
                // 创建新的移动止损单
                return await CreateTrailingStopOrderAsync(position);
            }
        }

        /// <summary>
        /// 处理并存模式
        /// </summary>
        private async Task<bool> ProcessCoexistMode(PositionInfo position)
        {
            var existingFixedStops = Orders.Where(o => 
                o.Symbol == position.Symbol && 
                o.Type == "STOP_MARKET" && 
                o.Status == "NEW" &&
                o.ReduceOnly).ToList();
            
            if (existingFixedStops.Any())
            {
                _logger.LogInformation($"持仓 {position.Symbol} 存在 {existingFixedStops.Count} 个固定止损单，将并存添加移动止损");
                return await CreateCoexistingTrailingStopAsync(position, existingFixedStops);
            }
            else
            {
                // 如果没有止损单，直接创建移动止损
                return await CreateTrailingStopOrderAsync(position);
            }
        }

        /// <summary>
        /// 处理智能分层模式
        /// </summary>
        private async Task<bool> ProcessSmartLayeringMode(PositionInfo position)
        {
            try
            {
                _logger.LogInformation($"开始智能分层模式处理: {position.Symbol}");
                
                // 先清理现有的止损单
                var existingStops = Orders.Where(o => 
                    o.Symbol == position.Symbol && 
                    (o.Type == "STOP_MARKET" || o.Type == "TRAILING_STOP_MARKET") &&
                    o.Status == "NEW" &&
                    o.ReduceOnly).ToList();

                foreach (var order in existingStops)
                {
                    await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                    await Task.Delay(100);
                }

                var totalQuantity = Math.Abs(position.PositionAmt);
                var fixedQuantity = totalQuantity * TrailingStopConfig.FixedStopRatio;
                var trailingQuantity = totalQuantity * TrailingStopConfig.TrailingStopRatio;
                
                var side = position.PositionAmt > 0 ? "SELL" : "BUY";
                var currentPrice = await _binanceService.GetLatestPriceAsync(position.Symbol);
                
                // 创建固定止损单（更严格的止损）
                var fixedStopPrice = position.PositionAmt > 0 
                    ? currentPrice * 0.95m  // 多头5%止损
                    : currentPrice * 1.05m; // 空头5%止损

                var fixedStopRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = fixedQuantity,
                    StopPrice = fixedStopPrice,
                    ReduceOnly = true,
                    PositionSide = position.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                // 创建移动止损单（更宽松的回调）
                var callbackRate = CalculateSmartCallbackRate(position);
                var trailingStopRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = side,
                    Type = "TRAILING_STOP_MARKET",
                    Quantity = trailingQuantity,
                    CallbackRate = callbackRate,
                    ReduceOnly = true,
                    PositionSide = position.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                // 先创建固定止损
                var fixedSuccess = await _binanceService.PlaceOrderAsync(fixedStopRequest);
                await Task.Delay(200);
                
                // 再创建移动止损
                var trailingSuccess = await _binanceService.PlaceOrderAsync(trailingStopRequest);
                
                if (fixedSuccess && trailingSuccess)
                {
                    _logger.LogInformation($"✅ 智能分层成功: {position.Symbol} 固定止损{fixedQuantity:F8} 移动止损{trailingQuantity:F8}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"❌ 智能分层失败: {position.Symbol} 固定:{fixedSuccess} 移动:{trailingSuccess}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"智能分层模式处理失败: {position.Symbol}");
                return false;
            }
        }

        /// <summary>
        /// 计算智能回调率
        /// </summary>
        private decimal CalculateSmartCallbackRate(PositionInfo position)
        {
            var profitPercentage = position.NotionalValue > 0 
                ? (position.UnrealizedProfit / position.NotionalValue) * 100 
                : 0;
            
            // 根据盈利情况动态调整回调率
            if (profitPercentage >= 15) return Math.Min(TrailingStopConfig.MaxCallbackRate, 3.0m);
            if (profitPercentage >= 10) return 2.5m;
            if (profitPercentage >= 5) return 2.0m;
            if (profitPercentage >= 2) return 1.5m;
            return Math.Max(TrailingStopConfig.MinCallbackRate, 1.0m);
        }

        /// <summary>
        /// 更新移动止损状态
        /// </summary>
        private async Task UpdateTrailingStopStatusesAsync()
        {
            try
            {
                var currentStatuses = new List<TrailingStopStatus>();
                
                foreach (var position in Positions.Where(p => p.PositionAmt != 0))
                {
                    var trailingStops = Orders.Where(o => 
                        o.Symbol == position.Symbol && 
                        o.Type == "TRAILING_STOP_MARKET" && 
                        o.Status == "NEW" &&
                        o.ReduceOnly).ToList();

                    var fixedStops = Orders.Where(o => 
                        o.Symbol == position.Symbol && 
                        o.Type == "STOP_MARKET" && 
                        o.Status == "NEW" &&
                        o.ReduceOnly).ToList();

                    foreach (var trailingOrder in trailingStops)
                    {
                        var status = new TrailingStopStatus
                        {
                            Symbol = position.Symbol,
                            TrailingOrderId = trailingOrder.OrderId,
                            TrailingQuantity = trailingOrder.OrigQty,
                            CallbackRate = trailingOrder.CallbackRate ?? 0,
                            Mode = TrailingStopConfig.Mode,
                            Status = "活跃"
                        };

                        if (fixedStops.Any())
                        {
                            var fixedOrder = fixedStops.First();
                            status.FixedOrderId = fixedOrder.OrderId;
                            status.FixedQuantity = fixedOrder.OrigQty;
                        }

                        currentStatuses.Add(status);
                    }
                }

                // 更新UI集合
                TrailingStopStatuses.Clear();
                foreach (var status in currentStatuses)
                {
                    TrailingStopStatuses.Add(status);
                }

                _logger.LogInformation($"移动止损状态更新完成，当前活跃: {currentStatuses.Count} 个");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新移动止损状态失败");
            }
        }

        /// <summary>
        /// 清理测试订单
        /// </summary>
        private async Task CleanupTestOrdersAsync(string symbol)
        {
            try
            {
                _logger.LogInformation($"开始清理 {symbol} 的测试订单");
                
                // 刷新订单数据
                await RefreshDataAsync();
                
                // 找到刚才的测试订单（根据时间和数量特征识别）
                var recentTestOrders = Orders.Where(o => 
                    o.Symbol == symbol && 
                    o.Type == "STOP_MARKET" && 
                    o.ReduceOnly &&
                    (DateTime.Now - o.Time).TotalMinutes < 5 && // 5分钟内创建的
                    o.OrigQty <= Math.Abs(Positions.FirstOrDefault(p => p.Symbol == symbol)?.PositionAmt ?? 0) * 0.02m // 小数量
                ).ToList();

                foreach (var order in recentTestOrders)
                {
                    try
                    {
                        var cancelled = await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                        if (cancelled)
                        {
                            _logger.LogInformation($"✅ 测试订单清理成功: {order.Symbol} #{order.OrderId}");
                        }
                        else
                        {
                            _logger.LogWarning($"⚠️ 测试订单清理失败: {order.Symbol} #{order.OrderId}");
                        }
                        await Task.Delay(200);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"清理测试订单异常: {order.OrderId}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理测试订单过程异常");
            }
        }
        /// <summary>
        /// 显示移动止损配置并提供修改选项
        /// </summary>
        [RelayCommand]
        private void OpenTrailingStopConfigDialog()
        {
            try
            {
                _logger.LogInformation("开始打开移动止损配置对话框...");
                
                // 确保TrailingStopConfig不为null
                if (TrailingStopConfig == null)
                {
                    _logger.LogWarning("TrailingStopConfig为null，创建默认配置");
                    TrailingStopConfig = new TrailingStopConfig();
                    StatusMessage = "⚠️ 初始化默认配置";
                }

                _logger.LogInformation($"当前配置: Mode={TrailingStopConfig.Mode}, MinCallback={TrailingStopConfig.MinCallbackRate}, MaxCallback={TrailingStopConfig.MaxCallbackRate}");
                
                // 检查主窗口
                if (Application.Current?.MainWindow == null)
                {
                    _logger.LogError("Application.Current.MainWindow为null");
                    StatusMessage = "❌ 无法获取主窗口引用";
                    return;
                }

                _logger.LogInformation("创建配置对话框窗口...");
                
                // 直接打开静态配置对话框
                var configWindow = new Views.TrailingStopConfigWindow(TrailingStopConfig)
                {
                    Owner = Application.Current.MainWindow
                };

                _logger.LogInformation("显示对话框...");
                var result = configWindow.ShowDialog();

                if (result == true && configWindow.IsConfirmed)
                {
                    // 应用新配置
                    TrailingStopConfig = configWindow.Config;
                    
                    var modeDescription = TrailingStopConfig.Mode switch
                    {
                        TrailingStopMode.Replace => "替换模式",
                        TrailingStopMode.Coexist => "并存模式",
                        TrailingStopMode.SmartLayering => "智能分层模式",
                        _ => "未知模式"
                    };

                    StatusMessage = $"✅ 移动止损配置已更新: {modeDescription}";
                    _logger.LogInformation($"移动止损配置已更新: {modeDescription}, 回调率: {TrailingStopConfig.MinCallbackRate:F1}%-{TrailingStopConfig.MaxCallbackRate:F1}%");
                }
                else
                {
                    StatusMessage = "配置修改已取消";
                    _logger.LogInformation("用户取消了配置修改");
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"打开配置对话框失败: {ex.Message}";
                StatusMessage = errorMsg;
                _logger.LogError(ex, "打开移动止损配置对话框失败，详细错误信息: {ErrorDetails}", ex.ToString());
                
                // 显示更详细的错误信息给用户
                try
                {
                    MessageBox.Show($"配置对话框打开失败:\n\n错误: {ex.Message}\n\n位置: {ex.StackTrace?.Split('\n').FirstOrDefault()}", 
                                  "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch
                {
                    // 如果连MessageBox都显示不了，至少记录到日志
                    _logger.LogError("无法显示错误对话框");
                }
            }
        }









        /// <summary>
        /// 查看移动止损状态
        /// </summary>
        [RelayCommand]
        private async Task ViewTrailingStopStatusAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在更新移动止损状态...";
                
                await UpdateTrailingStopStatusesAsync();
                
                var activeCount = TrailingStopStatuses.Count(s => s.IsActive);
                StatusMessage = $"移动止损状态更新完成，当前活跃: {activeCount} 个";
                
                // 输出详细状态到日志
                foreach (var status in TrailingStopStatuses)
                {
                    _logger.LogInformation($"移动止损状态: {status.Symbol} 模式:{status.Mode} 数量:{status.TrailingQuantity:F8} 回调率:{status.CallbackRate:F2}%");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"查看状态失败: {ex.Message}";
                _logger.LogError(ex, "查看移动止损状态失败");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 切换移动止损模式
        /// </summary>
        private void SwitchTrailingStopMode()
        {
            try
            {
                var currentMode = TrailingStopConfig.Mode;
                var nextMode = currentMode switch
                {
                    TrailingStopMode.Replace => TrailingStopMode.Coexist,
                    TrailingStopMode.Coexist => TrailingStopMode.SmartLayering,
                    TrailingStopMode.SmartLayering => TrailingStopMode.Replace,
                    _ => TrailingStopMode.Coexist
                };
                
                TrailingStopConfig.Mode = nextMode;
                var modeDescription = nextMode switch
                {
                    TrailingStopMode.Replace => "替换模式",
                    TrailingStopMode.Coexist => "并存模式",
                    TrailingStopMode.SmartLayering => "智能分层模式",
                    _ => "未知模式"
                };
                
                StatusMessage = $"移动止损模式已切换为: {modeDescription}";
                _logger.LogInformation($"移动止损模式切换: {currentMode} → {nextMode}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"切换模式失败: {ex.Message}";
                _logger.LogError(ex, "切换移动止损模式失败");
            }
        }

        /// <summary>
        /// 切换处理范围（仅盈利 / 所有持仓）
        /// </summary>
        private void ToggleProcessingScope()
        {
            try
            {
                var current = TrailingStopConfig.OnlyForProfitablePositions;
                TrailingStopConfig.OnlyForProfitablePositions = !current;
                
                var newScope = TrailingStopConfig.OnlyForProfitablePositions ? "仅盈利持仓" : "所有持仓";
                StatusMessage = $"移动止损处理范围已切换为: {newScope}";
                _logger.LogInformation($"处理范围切换: {(current ? "仅盈利" : "所有")} → {(!current ? "仅盈利" : "所有")}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"切换处理范围失败: {ex.Message}";
                _logger.LogError(ex, "切换处理范围失败");
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        private void ResetTrailingStopConfig()
        {
            try
            {
                TrailingStopConfig = new TrailingStopConfig();
                StatusMessage = "移动止损配置已重置为默认设置";
                _logger.LogInformation("移动止损配置已重置为默认设置");
            }
            catch (Exception ex)
            {
                StatusMessage = $"重置配置失败: {ex.Message}";
                _logger.LogError(ex, "重置移动止损配置失败");
            }
        }

        /// <summary>
        /// 显示简单的输入对话框
        /// </summary>
        private string ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            // 创建一个简单的输入窗口
            var inputWindow = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var textBlock = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(10),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(textBlock, 0);

            var textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(10, 0, 10, 10),
                Height = 25,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(textBox, 0);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var okButton = new Button
            {
                Content = "确定",
                Width = 60,
                Height = 25,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var cancelButton = new Button
            {
                Content = "取消",
                Width = 60,
                Height = 25
            };

            string result = null;

            okButton.Click += (s, e) =>
            {
                result = textBox.Text;
                inputWindow.DialogResult = true;
                inputWindow.Close();
            };

            cancelButton.Click += (s, e) =>
            {
                inputWindow.DialogResult = false;
                inputWindow.Close();
            };

            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    result = textBox.Text;
                    inputWindow.DialogResult = true;
                    inputWindow.Close();
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    inputWindow.DialogResult = false;
                    inputWindow.Close();
                }
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 1);

            grid.Children.Add(textBlock);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);

            inputWindow.Content = grid;
            textBox.Focus();
            textBox.SelectAll();

            bool? dialogResult = inputWindow.ShowDialog();
            return dialogResult == true ? result : null;
        }

        #endregion


    }
} 