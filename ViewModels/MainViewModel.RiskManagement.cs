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
        #region 移动止损配置信息属性
        /// <summary>
        /// 移动止损配置信息描述
        /// </summary>
        public string TrailingStopConfigInfo
        {
            get
            {
                if (TrailingStopConfig == null)
                    return "未配置";

                var modeDescription = TrailingStopConfig.Mode switch
                {
                    TrailingStopMode.Replace => "替换模式",
                    TrailingStopMode.Coexist => "并存模式",
                    TrailingStopMode.SmartLayering => "智能分层模式",
                    _ => "未知模式"
                };

                var scopeDescription = TrailingStopConfig.OnlyForProfitablePositions ? "仅盈利持仓" : "所有持仓";
                
                return $"{modeDescription} | {scopeDescription} | 回调率{TrailingStopConfig.CallbackRate:F1}%";
            }
        }

        /// <summary>
        /// 移动止损按钮工具提示
        /// </summary>
        public string TrailingStopButtonTooltip
        {
            get
            {
                if (TrailingStopEnabled)
                {
                    return $"关闭移动止损功能\n当前配置: {TrailingStopConfigInfo}";
                }
                else
                {
                    var hasSelected = Positions.Any(p => p.IsSelected && p.PositionAmt != 0);
                    var targetInfo = hasSelected ? "将只对勾选的持仓" : 
                        (TrailingStopConfig?.OnlyForProfitablePositions == true ? "将对盈利持仓" : "将对所有持仓");
                    
                    return $"启动移动止损功能\n{targetInfo}设置移动止损\n当前配置: {TrailingStopConfigInfo}";
                }
            }
        }
        #endregion

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

                // 计算保本价格（入场价格 + 1U缓冲）
                var stopPrice = targetPosition.EntryPrice;
                var side = targetPosition.PositionAmt > 0 ? "SELL" : "BUY";

                // 🔧 修复：考虑滑点和手续费损耗，在开仓价基础上加1U缓冲
                var bufferPerUnit = 1.0m / quantity; // 1U分摊到每个单位的价格缓冲
                
                if (targetPosition.PositionAmt > 0) // 多头
                {
                    // 多头保本止损：止损价 = 开仓价 + 缓冲
                    stopPrice = targetPosition.EntryPrice + bufferPerUnit;
                }
                else // 空头
                {
                    // 空头保本止损：止损价 = 开仓价 - 缓冲
                    stopPrice = targetPosition.EntryPrice - bufferPerUnit;
                }
                
                // 调整价格精度（保留4位小数）
                stopPrice = Math.Round(stopPrice, 4);

                _logger.LogInformation($"📊 保本止损参数: 合约={symbol}, 方向={direction}, 数量={quantity:F8}, 原开仓价={targetPosition.EntryPrice:F4}, 缓冲={bufferPerUnit:F6}, 最终止损价={stopPrice:F4}");

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

        #region 分仓止盈功能
        


        /// <summary>
        /// 分仓止盈命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecutePartialProfitTaking))]
        private async Task PartialProfitTakingAsync()
        {
            var targetPosition = SelectedPosition;
            
            if (targetPosition == null)
            {
                StatusMessage = "请先选择要设置分仓止盈的持仓";
                return;
            }

            try
            {
                _logger.LogInformation($"📊 开始设置分仓止盈: {targetPosition.Symbol}, 浮盈: {targetPosition.UnrealizedProfit:F2}U");

                // 🚨 增强调试：详细检查传入参数
                var direction = targetPosition.PositionAmt > 0 ? "做多" : "做空";
                var quantity = Math.Abs(targetPosition.PositionAmt);
                
                _logger.LogInformation($"🔍 分仓止盈参数检查:");
                _logger.LogInformation($"  原始PositionAmt: {targetPosition.PositionAmt}");
                _logger.LogInformation($"  计算后Quantity: {quantity}");
                _logger.LogInformation($"  Direction: {direction}");
                _logger.LogInformation($"  Symbol: {targetPosition.Symbol}");
                _logger.LogInformation($"  EntryPrice: {targetPosition.EntryPrice}");
                _logger.LogInformation($"  UnrealizedProfit: {targetPosition.UnrealizedProfit}");
                _logger.LogInformation($"  MarkPrice: {targetPosition.MarkPrice}");
                
                // 💡 预先验证参数，避免在对话框构造函数中出错
                if (quantity <= 0)
                {
                    var errorMsg = $"持仓数量异常: 原始={targetPosition.PositionAmt}, 计算后={quantity}";
                    _logger.LogError(errorMsg);
                    StatusMessage = errorMsg;
                    System.Windows.MessageBox.Show(errorMsg, "参数错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                
                if (targetPosition.UnrealizedProfit <= 0)
                {
                    var errorMsg = $"浮盈不足: {targetPosition.UnrealizedProfit:F2}，分仓止盈要求有浮盈";
                    _logger.LogError(errorMsg);
                    StatusMessage = errorMsg;
                    System.Windows.MessageBox.Show(errorMsg, "浮盈不足", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                
                _logger.LogInformation($"🔍 创建分仓止盈对话框参数: Symbol={targetPosition.Symbol}, Direction={direction}, Quantity={quantity}, EntryPrice={targetPosition.EntryPrice}, UnrealizedProfit={targetPosition.UnrealizedProfit}, MarkPrice={targetPosition.MarkPrice}");
                
                var dialog = new Views.PartialProfitDialog(
                    targetPosition.Symbol,
                    direction,
                    quantity,
                    targetPosition.EntryPrice,
                    targetPosition.UnrealizedProfit,
                    targetPosition.MarkPrice
                );

                // 设置窗口所有者
                dialog.Owner = Application.Current.MainWindow;

                // 显示对话框
                var result = dialog.ShowDialog();

                if (result == true && dialog.ProfitRequests.Any())
                {
                    IsLoading = true;
                    StatusMessage = $"正在提交 {dialog.ProfitRequests.Count} 个分仓止盈订单...";

                    var successCount = 0;
                    var totalRequests = dialog.ProfitRequests.Count;

                    foreach (var request in dialog.ProfitRequests)
                    {
                        try
                        {
                            var orderRequest = new OrderRequest
                            {
                                Symbol = request.Symbol,
                                Side = request.Side,
                                Type = "STOP_MARKET", // 使用止损单，回落触发到目标价出场
                                Quantity = request.Quantity,
                                StopPrice = request.Price, // 止损触发价格
                                ReduceOnly = true,
                                PositionSide = targetPosition.PositionSideString,
                                WorkingType = "CONTRACT_PRICE" // 基于合约价格触发
                            };

                            _logger.LogInformation($"🚀 准备提交分仓止盈订单 #{request.OrderIndex}: {request.Symbol} {request.Side} {request.Quantity:F6} @{request.Price:F4}，目标浮盈 {request.TargetProfit:F2}U");
                            _logger.LogInformation($"📋 订单详细参数 #{request.OrderIndex}: Type={orderRequest.Type}, ReduceOnly={orderRequest.ReduceOnly}, PositionSide={orderRequest.PositionSide}, WorkingType={orderRequest.WorkingType}");

                            var success = await _binanceService.PlaceOrderAsync(orderRequest);

                            if (success)
                            {
                                successCount++;
                                _logger.LogInformation($"✅ 分仓止盈订单 #{request.OrderIndex} 提交成功 - 成功数: {successCount}/{totalRequests}");
                            }
                            else
                            {
                                _logger.LogError($"❌ 分仓止盈订单 #{request.OrderIndex} 提交失败 - 成功数: {successCount}/{totalRequests}");
                                _logger.LogError($"❌ 失败订单详情: {request.Symbol} {request.Side} {request.Quantity:F6} @{request.Price:F4}");
                                
                                // 检查是否是API配置问题
                                if (SelectedAccount == null)
                                {
                                    _logger.LogError($"❌ 账户配置为空，可能是API配置问题");
                                }
                            }

                            // 更新进度状态
                            StatusMessage = $"分仓止盈进度: {successCount + (totalRequests - dialog.ProfitRequests.ToList().IndexOf(request) - 1)}/{totalRequests} 完成";

                            // 避免API频率限制
                            await Task.Delay(200);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"❌ 分仓止盈订单 #{request.OrderIndex} 提交异常: {ex.Message}");
                            _logger.LogError($"❌ 异常订单详情: {request.Symbol} {request.Side} {request.Quantity:F6} @{request.Price:F4}");
                            _logger.LogError($"❌ 完整异常信息: {ex}");
                        }
                    }

                    StatusMessage = $"✅ 分仓止盈设置完成: {successCount}/{totalRequests} 个订单成功提交";
                    _logger.LogInformation($"分仓止盈执行完成: 成功 {successCount}/{totalRequests} 个订单");

                    // 刷新数据显示新的委托单
                    await Task.Delay(1000);
                    await RefreshDataAsync();
                }
                else
                {
                    StatusMessage = "分仓止盈设置已取消";
                    _logger.LogInformation("用户取消分仓止盈设置");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"分仓止盈设置失败: {ex.Message}";
                _logger.LogError(ex, "分仓止盈设置过程异常");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 检查是否可以执行分仓止盈
        /// </summary>
        private bool CanExecutePartialProfitTaking()
        {
            try
            {
                // 检查是否正在加载
                if (IsLoading)
                {
                    _logger.LogDebug("🔍 分仓止盈CanExecute: IsLoading=true");
                    return false;
                }

                // 检查是否选中了持仓
                if (SelectedPosition == null)
                {
                    _logger.LogDebug("🔍 分仓止盈CanExecute: 未选中持仓");
                    return false;
                }

                // 检查是否有持仓数量
                if (SelectedPosition.PositionAmt == 0)
                {
                    _logger.LogDebug($"🔍 分仓止盈CanExecute: {SelectedPosition.Symbol} 无持仓 (数量={SelectedPosition.PositionAmt})");
                    return false;
                }

                // 检查是否有浮盈
                if (SelectedPosition.UnrealizedProfit <= 0)
                {
                    _logger.LogDebug($"🔍 分仓止盈CanExecute: {SelectedPosition.Symbol} 盈利不足 ({SelectedPosition.UnrealizedProfit:F2}U)");
                    return false;
                }

                _logger.LogInformation($"✅ 分仓止盈CanExecute: {SelectedPosition.Symbol} 盈利={SelectedPosition.UnrealizedProfit:F2}U，数量={SelectedPosition.PositionAmt:F6} - 可执行");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分仓止盈CanExecute检查异常");
                return false;
            }
        }
        #endregion

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
                PartialProfitTakingCommand.NotifyCanExecuteChanged();
                
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
                if (TrailingStopEnabled)
                {
                    // 如果已启用，直接关闭
                    TrailingStopEnabled = false;
                    OnPropertyChanged(nameof(TrailingStopButtonTooltip));
                    StatusMessage = "移动止损已关闭";
                    _logger.LogInformation("移动止损功能已关闭");
                    return;
                }

                // 🔧 新增：启动前先弹出配置对话框
                var selectedPositions = Positions.Where(p => p.IsSelected && p.PositionAmt != 0).ToList();
                var targetInfo = selectedPositions.Any() ? 
                    $"检测到 {selectedPositions.Count} 个勾选的持仓" : 
                    "将按配置规则处理持仓";

                // 显示配置确认对话框
                var configResult = ShowTrailingStopConfigDialog(targetInfo);
                if (!configResult)
                {
                    StatusMessage = "移动止损设置已取消";
                    return;
                }

                // 用户确认配置后，启动移动止损
                TrailingStopEnabled = true;
                OnPropertyChanged(nameof(TrailingStopButtonTooltip));
                
                StatusMessage = "移动止损已启动，开始监控持仓...";
                _logger.LogInformation("移动止损功能已启动");
                
                // 立即处理一次移动止损
                await ProcessTrailingStopAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"移动止损切换失败: {ex.Message}";
                _logger.LogError(ex, "切换移动止损失败");
                TrailingStopEnabled = false; // 出错时重置状态
                
                // 🔧 出错时也要通知属性更新
                OnPropertyChanged(nameof(TrailingStopButtonTooltip));
            }
        }

        /// <summary>
        /// 显示移动止损配置对话框
        /// </summary>
        private bool ShowTrailingStopConfigDialog(string targetInfo)
        {
            try
            {
                _logger.LogInformation("弹出移动止损配置确认对话框...");
                
                // 确保TrailingStopConfig不为null
                if (TrailingStopConfig == null)
                {
                    _logger.LogWarning("TrailingStopConfig为null，创建默认配置");
                    TrailingStopConfig = new TrailingStopConfig();
                }

                // 获取目标持仓信息
                var selectedPositions = Positions.Where(p => p.IsSelected && p.PositionAmt != 0).ToList();
                var targetPositions = selectedPositions.Any() ? selectedPositions : 
                    (TrailingStopConfig.OnlyForProfitablePositions 
                        ? Positions.Where(p => p.PositionAmt != 0 && p.UnrealizedProfit > 0).ToList()
                        : Positions.Where(p => p.PositionAmt != 0).ToList());

                // 🔧 简化：先显示确认对话框
                var modeDescription = TrailingStopConfig.Mode switch
                {
                    TrailingStopMode.Replace => "替换模式",
                    TrailingStopMode.Coexist => "并存模式",
                    TrailingStopMode.SmartLayering => "智能分层模式",
                    _ => "未知模式"
                };

                var scopeDescription = TrailingStopConfig.OnlyForProfitablePositions ? "仅盈利持仓" : "所有持仓";
                var positionDetails = targetPositions.Any() ? 
                    string.Join(", ", targetPositions.Select(p => $"{p.Symbol}({p.Direction})")) :
                    "无符合条件的持仓";

                var confirmMessage = $"确认启动移动止损功能？\n\n" +
                                   $"📋 目标范围: {targetInfo}\n" +
                                   $"📊 当前配置: {modeDescription} | {scopeDescription} | 回调率 {TrailingStopConfig.CallbackRate:F1}%\n" +
                                   $"🎯 目标持仓: {positionDetails}\n" +
                                   $"📈 将处理 {targetPositions.Count} 个持仓\n\n" +
                                   $"点击\"是\"使用当前配置启动，点击\"否\"打开配置设置";

                var result = MessageBox.Show(confirmMessage, "移动止损确认", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 直接使用当前配置启动
                    StatusMessage = $"✅ 使用当前配置启动移动止损: {modeDescription}";
                    _logger.LogInformation($"用户确认使用当前配置启动移动止损: {modeDescription}");
                    return true;
                }
                else if (result == MessageBoxResult.No)
                {
                    // 打开配置设置
                    return OpenConfigurationDialog();
                }
                else
                {
                    // 取消
                    StatusMessage = "移动止损启动已取消";
                    _logger.LogInformation("用户取消了移动止损启动");
                    return false;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"显示配置对话框失败: {ex.Message}";
                StatusMessage = errorMsg;
                _logger.LogError(ex, "显示移动止损配置对话框失败");
                
                // 显示错误信息
                try
                {
                    MessageBox.Show($"配置对话框显示失败:\n\n错误: {ex.Message}\n\n将使用当前配置继续", 
                                  "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return true; // 使用当前配置继续
                }
                catch
                {
                    _logger.LogError("无法显示错误对话框");
                    return false;
                }
            }
        }

        /// <summary>
        /// 打开配置设置对话框
        /// </summary>
        private bool OpenConfigurationDialog()
        {
            try
            {
                // 检查主窗口
                if (Application.Current?.MainWindow == null)
                {
                    _logger.LogError("Application.Current.MainWindow为null");
                    StatusMessage = "❌ 无法获取主窗口引用";
                    return false;
                }

                _logger.LogInformation("创建配置对话框窗口...");
                
                // 使用原有的配置对话框
                var configWindow = new Views.TrailingStopConfigWindow(TrailingStopConfig)
                {
                    Owner = Application.Current.MainWindow
                };

                _logger.LogInformation("显示配置对话框...");
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

                    StatusMessage = $"✅ 移动止损配置已更新并启动: {modeDescription}";
                    _logger.LogInformation($"移动止损配置已更新: {modeDescription}, 回调率: {TrailingStopConfig.CallbackRate:F1}%");
                    
                    // 🔧 通知配置信息属性更新
                    OnPropertyChanged(nameof(TrailingStopConfigInfo));
                    OnPropertyChanged(nameof(TrailingStopButtonTooltip));
                    
                    return true;
                }
                else
                {
                    StatusMessage = "移动止损配置已取消";
                    _logger.LogInformation("用户取消了移动止损配置");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开配置对话框失败");
                StatusMessage = $"配置对话框失败: {ex.Message}";
                return false;
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
                
                // 🔧 修改：优先处理勾选的持仓，如果没有勾选则处理所有符合条件的持仓
                List<PositionInfo> targetPositions;
                var selectedPositions = Positions.Where(p => p.IsSelected && p.PositionAmt != 0).ToList();
                
                if (selectedPositions.Any())
                {
                    // 有勾选的持仓，只处理勾选的
                    targetPositions = selectedPositions;
                    _logger.LogInformation($"检测到 {selectedPositions.Count} 个勾选的持仓，将只对勾选的持仓设置移动止损");
                    StatusMessage = $"正在为 {selectedPositions.Count} 个勾选的持仓设置移动止损...";
                }
                else
                {
                    // 没有勾选的持仓，按配置处理
                    targetPositions = TrailingStopConfig.OnlyForProfitablePositions 
                        ? Positions.Where(p => p.PositionAmt != 0 && p.UnrealizedProfit > 0).ToList()
                        : Positions.Where(p => p.PositionAmt != 0).ToList();
                    _logger.LogInformation($"没有勾选持仓，按配置处理 {targetPositions.Count} 个持仓");
                }

                // 🔧 新增：展示当前配置信息
                var modeDescription = TrailingStopConfig.Mode switch
                {
                    TrailingStopMode.Replace => "替换模式",
                    TrailingStopMode.Coexist => "并存模式", 
                    TrailingStopMode.SmartLayering => "智能分层模式",
                    _ => "未知模式"
                };
                
                var scopeDescription = selectedPositions.Any() ? "勾选持仓" : 
                    (TrailingStopConfig.OnlyForProfitablePositions ? "仅盈利持仓" : "所有持仓");
                
                _logger.LogInformation($"📋 移动止损配置 - 模式: {modeDescription}, 处理范围: {scopeDescription}, 回调率: {TrailingStopConfig.CallbackRate:F1}%");
                if (mode == TrailingStopMode.Coexist)
                {
                    _logger.LogInformation($"📋 分配比例: {TrailingStopConfig.AllocationRatio * 100:F1}%用于移动止损");
                }
                else if (mode == TrailingStopMode.SmartLayering)
                {
                    _logger.LogInformation($"📋 分层比例: 固定止损{TrailingStopConfig.FixedStopRatio * 100:F0}%, 移动止损{TrailingStopConfig.TrailingStopRatio * 100:F0}%");
                }
                
                var processedCount = 0;
                
                foreach (var position in targetPositions)
                {
                    bool success = false;
                    
                    try
                    {
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
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"处理持仓失败: {position.Symbol}");
                        success = false;
                    }
                    
                    if (success)
                        processedCount++;
                    
                    // 避免API频率限制
                    if (processedCount > 0)
                        await Task.Delay(300);
                }
                
                if (processedCount > 0)
                {
                    var processingInfo = selectedPositions.Any() ? "勾选持仓" : "符合条件的持仓";
                    StatusMessage = $"✅ 移动止损设置完成 - {modeDescription}，共处理 {processedCount} 个{processingInfo}，回调率 {TrailingStopConfig.CallbackRate:F1}%";
                    _logger.LogInformation($"移动止损处理完成（{modeDescription}），共处理 {processedCount} 个持仓");
                }
                else
                {
                    var processingInfo = selectedPositions.Any() ? "勾选的持仓" : "符合条件的持仓";
                    StatusMessage = $"ℹ️ 没有需要处理的{processingInfo} - {modeDescription}，回调率 {TrailingStopConfig.CallbackRate:F1}%";
                    _logger.LogInformation($"没有找到需要设置移动止损的持仓（{modeDescription}）");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"处理移动止损失败: {ex.Message}";
                _logger.LogError(ex, "处理移动止损失败");
            }
        }

        /// <summary>
        /// 替换模式：直接用移动止损替换现有止损单
        /// </summary>
        private async Task<bool> ProcessReplaceMode(PositionInfo position)
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
                return false;
            }

            // 取消现有的普通止损单
            var existingStopOrders = Orders.Where(o => 
                o.Symbol == position.Symbol && 
                o.Type == "STOP_MARKET" && 
                o.Status == "NEW" &&
                o.ReduceOnly).ToList();
            
            foreach (var stopOrder in existingStopOrders)
            {
                await _binanceService.CancelOrderAsync(stopOrder.Symbol, stopOrder.OrderId);
                await Task.Delay(100); // 等待取消完成
            }

            // 创建移动止损单（使用全部持仓数量）
            var side = position.PositionAmt > 0 ? "SELL" : "BUY";
            var trailingStopRequest = new OrderRequest
            {
                Symbol = position.Symbol,
                Side = side,
                Type = "TRAILING_STOP_MARKET",
                Quantity = Math.Abs(position.PositionAmt),
                CallbackRate = TrailingStopConfig.CallbackRate,
                ReduceOnly = true,
                PositionSide = position.PositionSideString,
                WorkingType = "CONTRACT_PRICE"
            };

            var success = await _binanceService.PlaceOrderAsync(trailingStopRequest);
            if (success)
            {
                _logger.LogInformation($"移动止损单创建成功(替换模式): {position.Symbol} 数量{Math.Abs(position.PositionAmt):F4} 回调率{TrailingStopConfig.CallbackRate:F2}%");
                
                // 🔧 新增：保存移动止损状态
                await SaveTrailingStopStatusAsync(position, null, TrailingStopMode.Replace);
            }
            else
            {
                _logger.LogWarning($"移动止损单创建失败(替换模式): {position.Symbol}");
            }
            
            return success;
        }

        /// <summary>
        /// 并存模式：按分配比例创建移动止损单，与现有止损单并存
        /// </summary>
        private async Task<bool> ProcessCoexistMode(PositionInfo position)
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
                return false;
            }

            // 计算移动止损数量（使用分配比例）
            var trailingQuantity = Math.Abs(position.PositionAmt) * TrailingStopConfig.AllocationRatio;
            
            // 数量精度调整
            var (stepSize, _) = await _binanceService.GetSymbolPrecisionAsync(position.Symbol);
            trailingQuantity = Math.Round(trailingQuantity / stepSize) * stepSize;
            
            if (trailingQuantity <= 0)
            {
                _logger.LogWarning($"移动止损数量太小，跳过 {position.Symbol}: {trailingQuantity:F8}");
                return false;
            }

            // 创建移动止损单
            var side = position.PositionAmt > 0 ? "SELL" : "BUY";
            var trailingStopRequest = new OrderRequest
            {
                Symbol = position.Symbol,
                Side = side,
                Type = "TRAILING_STOP_MARKET",
                Quantity = trailingQuantity,
                CallbackRate = TrailingStopConfig.CallbackRate,
                ReduceOnly = true,
                PositionSide = position.PositionSideString,
                WorkingType = "CONTRACT_PRICE"
            };

            var success = await _binanceService.PlaceOrderAsync(trailingStopRequest);
            if (success)
            {
                var percentageUsed = TrailingStopConfig.AllocationRatio * 100;
                _logger.LogInformation($"移动止损单创建成功(并存模式): {position.Symbol} 数量{trailingQuantity:F4}({percentageUsed:F1}%) 回调率{TrailingStopConfig.CallbackRate:F2}%");
            }
            else
            {
                _logger.LogWarning($"移动止损单创建失败(并存模式): {position.Symbol}");
            }
            
            return success;
        }

        /// <summary>
        /// 智能分层模式：同时创建固定止损和移动止损
        /// </summary>
        private async Task<bool> ProcessSmartLayeringMode(PositionInfo position)
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
                return false;
            }

            var absolutePositionAmt = Math.Abs(position.PositionAmt);
            var side = position.PositionAmt > 0 ? "SELL" : "BUY";
            var isLong = position.PositionAmt > 0;
            
            // 获取精度信息
            var (stepSize, tickSize) = await _binanceService.GetSymbolPrecisionAsync(position.Symbol);
            
            // 计算分层数量
            var fixedQuantity = absolutePositionAmt * TrailingStopConfig.FixedStopRatio;
            var trailingQuantity = absolutePositionAmt * TrailingStopConfig.TrailingStopRatio;
            
            // 数量精度调整
            fixedQuantity = Math.Round(fixedQuantity / stepSize) * stepSize;
            trailingQuantity = Math.Round(trailingQuantity / stepSize) * stepSize;
            
            if (fixedQuantity <= 0 && trailingQuantity <= 0)
            {
                _logger.LogWarning($"分层数量都太小，跳过 {position.Symbol}");
                return false;
            }

            var fixedSuccess = true;
            var trailingSuccess = true;

            // 创建固定止损单（如果数量大于0）
            if (fixedQuantity > 0)
            {
                // 计算固定止损价格（相对保守，比如5%）
                var fixedStopLossRatio = 10.0m; // 10%固定止损
                var fixedStopPrice = isLong 
                    ? position.EntryPrice * (1 - fixedStopLossRatio / 100)
                    : position.EntryPrice * (1 + fixedStopLossRatio / 100);
                
                // 价格精度调整
                fixedStopPrice = Math.Round(fixedStopPrice / tickSize) * tickSize;
                
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

                fixedSuccess = await _binanceService.PlaceOrderAsync(fixedStopRequest);
                if (fixedSuccess)
                {
                    _logger.LogInformation($"固定止损单创建成功: {position.Symbol} 数量{fixedQuantity:F4} 止损价{fixedStopPrice:F4}");
                }
                else
                {
                    _logger.LogWarning($"固定止损单创建失败: {position.Symbol}");
                }
                
                await Task.Delay(200); // API间隔
            }

            // 创建移动止损单（如果数量大于0）
            if (trailingQuantity > 0)
            {
                var trailingStopRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = side,
                    Type = "TRAILING_STOP_MARKET",
                    Quantity = trailingQuantity,
                    CallbackRate = TrailingStopConfig.CallbackRate,
                    ReduceOnly = true,
                    PositionSide = position.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                trailingSuccess = await _binanceService.PlaceOrderAsync(trailingStopRequest);
                if (trailingSuccess)
                {
                    _logger.LogInformation($"移动止损单创建成功: {position.Symbol} 数量{trailingQuantity:F4} 回调率{TrailingStopConfig.CallbackRate:F2}%");
                }
                else
                {
                    _logger.LogWarning($"移动止损单创建失败: {position.Symbol}");
                }
            }

            var success = (fixedQuantity <= 0 || fixedSuccess) && (trailingQuantity <= 0 || trailingSuccess);
            if (success)
            {
                var fixedPct = TrailingStopConfig.FixedStopRatio * 100;
                var trailingPct = TrailingStopConfig.TrailingStopRatio * 100;
                _logger.LogInformation($"智能分层创建成功: {position.Symbol} 固定{fixedPct:F0}%({fixedQuantity:F4}) + 移动{trailingPct:F0}%({trailingQuantity:F4})");
            }
            
            return success;
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

        /// <summary>
        /// 保存移动止损状态到持久化存储
        /// </summary>
        private async Task SaveTrailingStopStatusAsync(PositionInfo position, long? fixedOrderId, TrailingStopMode mode)
        {
            try
            {
                // 获取最新创建的移动止损单ID
                var latestOrders = await _binanceService.GetOpenOrdersAsync();
                var trailingOrder = latestOrders
                    .Where(o => o.Symbol == position.Symbol && 
                               o.Type == "TRAILING_STOP_MARKET" && 
                               o.Status == "NEW" && 
                               o.ReduceOnly)
                    .OrderByDescending(o => o.OrderId)
                    .FirstOrDefault();

                if (trailingOrder == null)
                {
                    _logger.LogWarning($"未找到新创建的移动止损单: {position.Symbol}");
                    return;
                }

                var status = new TrailingStopStatus
                {
                    Symbol = position.Symbol,
                    TrailingOrderId = trailingOrder.OrderId,
                    FixedOrderId = fixedOrderId,
                    CreatedTime = DateTime.Now,
                    TrailingQuantity = trailingOrder.OrigQty,
                    FixedQuantity = fixedOrderId.HasValue ? (decimal?)0 : null, // 如果有固定订单，稍后需要获取数量
                    CallbackRate = TrailingStopConfig.CallbackRate,
                    Mode = mode,
                    Status = "活跃"
                };

                // 添加到状态集合
                var existingStatus = TrailingStopStatuses.FirstOrDefault(s => s.Symbol == position.Symbol);
                if (existingStatus != null)
                {
                    TrailingStopStatuses.Remove(existingStatus);
                }
                TrailingStopStatuses.Add(status);

                // 保存到文件
                await _trailingStopPersistenceService.AddOrUpdateStatusAsync(status, TrailingStopStatuses);
                
                _logger.LogInformation($"💾 移动止损状态已保存: {position.Symbol} 订单#{trailingOrder.OrderId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"保存移动止损状态失败: {position?.Symbol}");
            }
        }

        #endregion
    }
}