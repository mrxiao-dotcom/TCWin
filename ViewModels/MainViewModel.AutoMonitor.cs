using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace BinanceFuturesTrader.ViewModels
{
    /// <summary>
    /// MainViewModel - 自动监控功能扩展
    /// </summary>
    public partial class MainViewModel
    {
        /// <summary>
        /// 切换自动监控命令
        /// </summary>
        [RelayCommand]
        private async Task ToggleAutoMonitorAsync()
        {
            try
            {
                _logger.LogInformation($"🔄 自动盯盘按钮被点击，当前状态: {(IsAutoMonitorRunning ? "运行中" : "未运行")}");
                
                if (IsAutoMonitorRunning)
                {
                    // 停止自动监控
                    _logger.LogInformation("准备停止自动监控...");
                    await StopAutoMonitorAsync();
                }
                else
                {
                    // 启动自动监控
                    _logger.LogInformation("准备启动自动监控...");
                    await StartAutoMonitorAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "切换自动监控状态时发生错误");
                MessageBox.Show($"操作失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 启动自动监控
        /// </summary>
        private async Task StartAutoMonitorAsync()
        {
            try
            {
                _logger.LogInformation("🚀 开始启动自动监控流程...");
                
                // 🔧 修复空引用异常：先检查账户是否已选择
                if (SelectedAccount == null)
                {
                    _logger.LogWarning("没有选择账户，无法启动自动盯盘");
                    MessageBox.Show("请先选择账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 获取账户信息用于生成智能默认配置
                var accountEquity = AccountInfo?.TotalEquity ?? 1000m;
                var riskCapitalTimes = SelectedAccount.RiskCapitalTimes;
                
                // 打开配置对话框（使用智能默认配置）
                _logger.LogInformation("准备创建配置对话框...");
                var configDialog = new AutoMonitorConfigDialog(accountEquity, riskCapitalTimes);
                _logger.LogInformation($"配置对话框创建成功（权益{accountEquity:F0}U，风险金倍数{riskCapitalTimes}）");
                
                // 从当前账户的配置中加载设置
                if (_accountAutoMonitorConfigs.TryGetValue(SelectedAccount.Name, out var accountConfig))
                {
                    _logger.LogInformation($"从账户 {SelectedAccount.Name} 加载现有配置...");
                    configDialog.SetConfig(accountConfig);
                    _currentAutoMonitorConfig = accountConfig;
                }
                else
                {
                    _logger.LogInformation($"账户 {SelectedAccount.Name} 没有现有配置，使用智能默认配置");
                }

                _logger.LogInformation("准备显示配置对话框...");
                var result = configDialog.ShowDialog();
                _logger.LogInformation($"配置对话框返回结果: {result}");
                
                if (result != true || configDialog.ConfigResult == null)
                {
                    _logger.LogInformation("用户取消了配置或配置结果为空");
                    return; // 用户取消了配置
                }

                // 保存配置到当前账户
                _currentAutoMonitorConfig = configDialog.ConfigResult;
                _accountAutoMonitorConfigs[SelectedAccount.Name] = _currentAutoMonitorConfig;
                _logger.LogInformation($"配置已保存到账户 {SelectedAccount.Name}: {_currentAutoMonitorConfig.Name}");

                // 创建自动监控服务
                if (_autoMonitorService == null)
                {
                    _logger.LogInformation("创建自动监控服务...");
                    var serviceLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
                        builder.AddConsole()).CreateLogger<AutoMonitorService>();
                    _autoMonitorService = new AutoMonitorService(_binanceService, this, serviceLogger);
                    
                    // 订阅事件
                    _autoMonitorService.MonitorStatusChanged += OnAutoMonitorStatusChanged;
                    _autoMonitorService.ExecutionCompleted += OnAutoMonitorExecutionCompleted;
                    _logger.LogInformation("自动监控服务创建完成");
                }

                // 启动监控
                _logger.LogInformation("启动监控服务...");
                
                // 🔍 启动前先验证配置
                try
                {
                    // 使用新的重载方法，传入配置对象进行验证
                    var validationResult = await _autoMonitorService.ValidateConfigAsync(_currentAutoMonitorConfig, ValidationMode.Strict, true);
                    if (validationResult.Errors.Any())
                    {
                        var errorDetails = string.Join("\n", validationResult.Errors.Select(e => $"• {e.Message}"));
                        _logger.LogError($"配置验证失败:\n{errorDetails}");
                        MessageBox.Show($"配置验证失败，无法启动自动监控：\n\n{errorDetails}", 
                            "配置错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    if (validationResult.AutoFixes.Any())
                    {
                        _logger.LogInformation("配置验证通过，已自动修复部分问题");
                    }
                    else
                    {
                        _logger.LogInformation("配置验证通过");
                    }
                }
                catch (Exception validateEx)
                {
                    _logger.LogError(validateEx, "配置验证时发生异常");
                    MessageBox.Show($"配置验证失败：{validateEx.Message}", 
                        "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var success = await _autoMonitorService.StartMonitoringAsync(_currentAutoMonitorConfig);
                
                if (success)
                {
                    _logger.LogInformation("监控服务启动成功，更新UI状态...");
                    UpdateAutoMonitorUI(true, "自动盯盘运行中", "停止盯盘", "#E74C3C");
                    _logger.LogInformation("自动监控已启动");
                    _logger.LogInformation($"🎯 自动盯盘启动成功 - 配置: {_currentAutoMonitorConfig.Name}");
                    MessageBox.Show($"自动盯盘已启动！\n配置: {_currentAutoMonitorConfig.Name}", "启动成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // 🔧 新增：通知监控界面刷新数据
                    NotifyAutoMonitorDashboardRefresh();
                }
                else
                {
                    _logger.LogError("监控服务启动失败，查看详细日志获取错误信息");
                    
                    // 🔍 获取详细的失败原因
                    var detailedError = "未知错误";
                    try
                    {
                                                 // 检查服务状态
                         if (_autoMonitorService.CurrentConfig == null)
                         {
                             detailedError = "配置对象为空";
                         }
                         else if (SelectedAccount == null)
                         {
                             detailedError = "未选择交易账户";
                         }
                         else
                         {
                             // 尝试检查API连接状态
                             try
                             {
                                 var accountInfo = await _binanceService.GetAccountInfoAsync();
                                 if (accountInfo == null)
                                 {
                                     detailedError = "币安API连接失败，无法获取账户信息，请检查网络和API密钥";
                                 }
                                 else
                                 {
                                     detailedError = "服务启动内部错误，请查看日志文件获取详细信息";
                                 }
                             }
                             catch (Exception apiEx)
                             {
                                 detailedError = $"币安API连接错误：{apiEx.Message}";
                             }
                         }
                    }
                    catch (Exception diagEx)
                    {
                        detailedError = $"诊断错误：{diagEx.Message}";
                    }
                    
                    MessageBox.Show($"自动监控启动失败\n\n可能原因：{detailedError}\n\n建议：\n" +
                        "1. 检查网络连接和API密钥\n" +
                        "2. 确认已选择正确的交易账户\n" +
                        "3. 重新配置自动盯盘参数\n" +
                        "4. 查看程序日志获取详细错误信息", 
                        "启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动自动监控时发生错误");
                MessageBox.Show($"启动失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 停止自动监控
        /// </summary>
        /// <param name="clearConfig">是否清空当前配置（账户切换时为true，用户手动停止时为false）</param>
        private async Task StopAutoMonitorAsync(bool clearConfig = false)
        {
            try
            {
                if (_autoMonitorService != null)
                {
                    await _autoMonitorService.StopMonitoringAsync();
                }

                // 如果是账户切换导致的停止，清空当前配置
                if (clearConfig)
                {
                    _currentAutoMonitorConfig = null;
                    _logger.LogInformation("自动监控已停止，配置已清空（账户切换）");
                }
                else
                {
                    _logger.LogInformation("自动监控已停止（用户手动停止）");
                }

                UpdateAutoMonitorUI(false, "自动盯盘已停止", "启动盯盘", "#27AE60");
                _logger.LogInformation("自动监控已停止");
                
                // 🔧 新增：通知监控界面刷新数据
                NotifyAutoMonitorDashboardRefresh();

                await Task.CompletedTask; // 保持异步签名
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止自动监控时发生错误");
                MessageBox.Show($"停止失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新自动监控UI状态
        /// </summary>
        private void UpdateAutoMonitorUI(bool isRunning, string statusMessage, string buttonText, string buttonColor)
        {
            IsAutoMonitorRunning = isRunning;
            AutoMonitorStatusMessage = statusMessage;
            AutoMonitorButtonText = buttonText;
            AutoMonitorButtonColor = buttonColor;
        }

        /// <summary>
        /// 自动监控状态变化事件处理
        /// </summary>
        private void OnAutoMonitorStatusChanged(object? sender, MonitorStatusChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AutoMonitorStatusMessage = e.Message;
                
                if (!e.IsRunning && IsAutoMonitorRunning)
                {
                    // 监控意外停止
                    UpdateAutoMonitorUI(false, "已停止", "自动盯盘", "#4A90E2");
                }
            });
        }

        /// <summary>
        /// 自动监控执行完成事件处理
        /// </summary>
        private void OnAutoMonitorExecutionCompleted(object? sender, ExecutionResultEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var message = $"[{e.Symbol}] {e.ExecutionType}: {e.Message}";
                _logger.LogInformation(message);
                
                // 在状态栏显示执行结果
                StatusMessage = message;
                
                // 如果是重要操作，可以显示通知
                if (e.ExecutionType.Contains("保本") || e.ExecutionType.Contains("推仓"))
                {
                    // 这里可以添加系统通知或者其他提示方式
                    System.Diagnostics.Debug.WriteLine($"自动监控执行：{message}");
                }
            });
        }

        /// <summary>
        /// 查看监控命令
        /// </summary>
        [RelayCommand]
        private async Task ViewMonitorStatusAsync()
        {
            try
            {
                _logger.LogInformation("🖥️ 查看监控按钮被点击，准备打开监控面板");
                
                if (SelectedAccount == null)
                {
                    MessageBox.Show("请先选择账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 检查监控服务是否已初始化
                if (_autoMonitorService == null)
                {
                    _logger.LogInformation("💡 监控服务未初始化，正在从依赖注入容器获取服务...");
                    
                    // 🔧 从依赖注入容器获取服务实例，而不是创建新的
                    try
                    {
                        _autoMonitorService = _serviceProvider.GetRequiredService<AutoMonitorService>();
                        _logger.LogInformation("✅ 成功从依赖注入容器获取AutoMonitorService");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ 无法从依赖注入容器获取AutoMonitorService，创建临时实例");
                        var serviceLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
                            builder.AddConsole()).CreateLogger<AutoMonitorService>();
                        _autoMonitorService = new AutoMonitorService(_binanceService, this, serviceLogger);
                    }
                }

                // 🔗 使用正在运行的服务中的状态管理器，确保数据一致性
                _logger.LogInformation("🔗 使用正在运行的服务中的状态管理器");

                // 创建并显示监控面板（使用简化的构造函数）
                var monitorDashboard = new AutoMonitorDashboard(
                    _autoMonitorService, 
                    _logger);

                _logger.LogInformation("🖥️ 监控面板创建成功，使用简化模式");
                monitorDashboard.Show();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开监控面板时发生错误");
                MessageBox.Show($"打开监控面板失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 验证当前配置命令
        /// </summary>
        [RelayCommand]
        private async Task ValidateConfigAsync()
        {
            if (_autoMonitorService == null)
            {
                MessageBox.Show("自动监控服务未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var validationResult = await _autoMonitorService.ValidateConfigAsync(ValidationMode.Strict, false);
                
                var message = $"📊 配置验证结果\n\n{validationResult.Summary}\n\n";
                
                if (validationResult.Errors.Any())
                {
                    message += "❌ 错误:\n";
                    foreach (var error in validationResult.Errors.Take(5))
                    {
                        message += $"• {error.Message}\n";
                    }
                    if (validationResult.Errors.Count > 5)
                    {
                        message += $"... 还有 {validationResult.Errors.Count - 5} 个错误\n";
                    }
                    message += "\n";
                }
                
                if (validationResult.Warnings.Any())
                {
                    message += "⚠️ 警告:\n";
                    foreach (var warning in validationResult.Warnings.Take(5))
                    {
                        message += $"• {warning.Message}\n";
                    }
                    if (validationResult.Warnings.Count > 5)
                    {
                        message += $"... 还有 {validationResult.Warnings.Count - 5} 个警告\n";
                    }
                    message += "\n";
                }
                
                if (validationResult.Suggestions.Any())
                {
                    message += "💡 建议:\n";
                    foreach (var suggestion in validationResult.Suggestions.Take(3))
                    {
                        message += $"• {suggestion.Reason}\n";
                    }
                    if (validationResult.Suggestions.Count > 3)
                    {
                        message += $"... 还有 {validationResult.Suggestions.Count - 3} 个建议\n";
                    }
                }

                var icon = validationResult.IsValid ? MessageBoxImage.Information : MessageBoxImage.Warning;
                MessageBox.Show(message, "配置验证结果", MessageBoxButton.OK, icon);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"配置验证失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取配置建议命令
        /// </summary>
        [RelayCommand]
        private async Task GetConfigSuggestionsAsync()
        {
            if (_autoMonitorService == null)
            {
                MessageBox.Show("自动监控服务未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var suggestions = await _autoMonitorService.GetConfigSuggestionsAsync();
                
                if (!suggestions.Any())
                {
                    MessageBox.Show("当前配置没有优化建议。\n✅ 配置看起来很不错！", "配置建议", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var message = $"💡 配置优化建议 ({suggestions.Count} 条):\n\n";
                
                // 按优先级分组显示
                var highPriority = suggestions.Where(s => s.Priority >= SuggestionPriority.High).ToList();
                var mediumPriority = suggestions.Where(s => s.Priority == SuggestionPriority.Medium).ToList();
                var lowPriority = suggestions.Where(s => s.Priority == SuggestionPriority.Low).ToList();

                if (highPriority.Any())
                {
                    message += "🔴 高优先级建议:\n";
                    foreach (var suggestion in highPriority)
                    {
                        message += $"• {suggestion.Reason}\n";
                        if (suggestion.SuggestedValue != null)
                        {
                            message += $"  建议值: {suggestion.SuggestedValue}\n";
                        }
                    }
                    message += "\n";
                }

                if (mediumPriority.Any())
                {
                    message += "🟡 中等优先级建议:\n";
                    foreach (var suggestion in mediumPriority.Take(3))
                    {
                        message += $"• {suggestion.Reason}\n";
                    }
                    if (mediumPriority.Count > 3)
                    {
                        message += $"... 还有 {mediumPriority.Count - 3} 个建议\n";
                    }
                    message += "\n";
                }

                if (lowPriority.Any())
                {
                    message += $"🟢 低优先级建议: {lowPriority.Count} 条（点击查看详情）\n";
                }

                MessageBox.Show(message, "配置优化建议", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取配置建议失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取自动监控执行历史
        /// </summary>
        public async Task ShowAutoMonitorHistoryAsync()
        {
            try
            {
                if (_autoMonitorService == null)
                {
                    MessageBox.Show("自动监控服务未初始化", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var history = _autoMonitorService.GetExecutionHistory();
                var profiles = _autoMonitorService.GetPositionProfiles();

                // 这里可以创建一个历史查看对话框
                var historyMessage = "执行历史：\n";
                foreach (var item in history.Skip(Math.Max(0, history.Count - 10))) // 显示最近10条
                {
                    historyMessage += $"{item.ExecutionTime:HH:mm:ss} [{item.Symbol}] {item.ExecutionType} - {(item.IsSuccess ? "成功" : "失败")}\n";
                }

                MessageBox.Show(historyMessage, "自动监控历史", MessageBoxButton.OK, MessageBoxImage.Information);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查看自动监控历史时发生错误");
                MessageBox.Show($"查看历史失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清理自动监控资源
        /// </summary>
        private void CleanupAutoMonitor()
        {
            try
            {
                if (_autoMonitorService != null)
                {
                    _autoMonitorService.MonitorStatusChanged -= OnAutoMonitorStatusChanged;
                    _autoMonitorService.ExecutionCompleted -= OnAutoMonitorExecutionCompleted;
                    _autoMonitorService.Dispose();
                    _autoMonitorService = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理自动监控资源时发生错误");
            }
        }

        /// <summary>
        /// 清理指定合约的自动盯盘历史状态
        /// </summary>
        [RelayCommand]
        private async Task ClearContractAutoMonitorHistoryAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(Symbol))
                {
                    MessageBox.Show("请先选择要清理的合约", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var result = MessageBox.Show(
                    $"确定要清理合约 {Symbol} 的自动盯盘历史状态吗？\n\n" +
                    "这将清理该合约所有的推仓、保本、止盈触发记录，\n" +
                    "清理后如果重新开仓该合约，将可以重新触发所有阶梯。\n\n" +
                    "建议在确认该合约已完全平仓后再执行此操作。",
                    "确认清理历史状态",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result != MessageBoxResult.Yes) return;
                
                if (_autoMonitorService == null)
                {
                    MessageBox.Show("自动监控服务未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 获取当前持仓信息，确定要清理的持仓方向
                var positions = await _binanceService.GetPositionsAsync();
                var contractPositions = positions?.Where(p => p.Symbol == Symbol && Math.Abs(p.PositionAmt) > 0.0001m).ToList();
                
                var contractsToClear = new List<(string symbol, string positionSide)>();
                var hasActivePositions = contractPositions?.Any() == true;
                
                if (hasActivePositions)
                {
                    var positionInfo = string.Join(", ", contractPositions!.Select(p => $"{p.PositionSideString}({p.PositionAmt:F4})"));
                    var confirmResult = MessageBox.Show(
                        $"检测到合约 {Symbol} 仍有活跃持仓:\n{positionInfo}\n\n" +
                        "是否仍要清理历史状态？\n" +
                        "清理后，当前持仓的触发记录也会被重置。",
                        "检测到活跃持仓",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (confirmResult != MessageBoxResult.Yes) return;
                    
                    // 清理所有方向的状态
                    contractsToClear.AddRange(contractPositions.Select(p => (p.Symbol, p.PositionSideString)));
                }
                else
                {
                    // 没有活跃持仓，清理所有可能的方向（LONG和SHORT）
                    contractsToClear.Add((Symbol, "LONG"));
                    contractsToClear.Add((Symbol, "SHORT"));
                }
                
                // 执行清理
                var persistenceService = new AutoMonitorPersistenceService();
                persistenceService.BatchCleanupContractHistory(contractsToClear, "手动清理");
                
                _logger.LogInformation($"✅ 用户手动清理合约历史状态: {Symbol}");
                
                MessageBox.Show(
                    $"合约 {Symbol} 的自动盯盘历史状态已清理完成！\n\n" +
                    "相关说明:\n" +
                    "• 已清理所有推仓、保本、止盈的触发记录\n" +
                    "• 已清理相关的执行历史记录\n" +
                    "• 如果重新开仓该合约，将可以重新触发所有阶梯\n" +
                    "• 清理操作已记录到系统日志中",
                    "清理完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                    
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"清理合约自动盯盘历史状态时发生错误: {Symbol}");
                MessageBox.Show($"清理失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清理所有合约的自动盯盘历史状态
        /// </summary>
        [RelayCommand]
        private async Task ClearAllAutoMonitorHistoryAsync()
        {
            try
            {
                var result = MessageBox.Show(
                    "确定要清理所有合约的自动盯盘历史状态吗？\n\n" +
                    "⚠️ 警告：这是一个高风险操作！\n\n" +
                    "这将清理所有合约的推仓、保本、止盈触发记录，\n" +
                    "包括当前有活跃持仓的合约！\n\n" +
                    "清理后所有合约都将可以重新触发所有阶梯，\n" +
                    "可能导致重复执行交易操作！\n\n" +
                    "建议只在确认所有合约都已平仓后执行。",
                    "⚠️ 确认清理所有历史状态",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result != MessageBoxResult.Yes) return;
                
                // 二次确认
                var confirmResult = MessageBox.Show(
                    "最后确认：真的要清理所有合约的历史状态吗？\n\n" +
                    "此操作不可撤销！",
                    "最后确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Stop);
                
                if (confirmResult != MessageBoxResult.Yes) return;
                
                // 执行全部清理
                var persistenceService = new AutoMonitorPersistenceService();
                persistenceService.ClearAllData();
                
                _logger.LogWarning("⚠️ 用户手动清理所有自动盯盘历史状态");
                
                MessageBox.Show(
                    "所有合约的自动盯盘历史状态已清理完成！\n\n" +
                    "相关说明:\n" +
                    "• 已清理所有合约的触发记录和执行历史\n" +
                    "• 所有合约重新开仓时都将可以重新触发阶梯\n" +
                    "• 建议重新启动程序以确保状态同步\n" +
                    "• 清理操作已记录到系统日志中",
                    "全部清理完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                    
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理所有自动盯盘历史状态时发生错误");
                MessageBox.Show($"清理失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 🧪 测试用户配置验证 - 调试保本4U + 推仓10U配置
        /// </summary>
        [RelayCommand]
        private async Task TestUserConfigValidationAsync()
        {
            try
            {
                _logger.LogInformation("🧪 开始测试用户配置验证: 保本4U + 推仓10U");
                
                // 创建测试配置
                var testConfig = new AutoMonitorConfig
                {
                    Name = "测试配置 - 保本4U推仓10U",
                    ScanIntervalSeconds = 5,
                    BreakEvenConfig = new AutoBreakEvenConfig
                    {
                        IsEnabled = true,
                        TriggerProfitAmount = 4m
                    },
                    AddPositionConfig = new AutoAddPositionConfig
                    {
                        IsEnabled = true,
                        Tiers = new List<AddPositionTier>
                        {
                            new AddPositionTier { TierIndex = 1, TriggerProfitAmount = 10m, RiskMultiplier = 1.0m, StopLossRatio = 0.10m },
                            new AddPositionTier { TierIndex = 2, TriggerProfitAmount = 0m, RiskMultiplier = 1.0m, StopLossRatio = 0.10m },
                            new AddPositionTier { TierIndex = 3, TriggerProfitAmount = 0m, RiskMultiplier = 1.0m, StopLossRatio = 0.10m },
                            new AddPositionTier { TierIndex = 4, TriggerProfitAmount = 0m, RiskMultiplier = 1.0m, StopLossRatio = 0.10m }
                        }
                    },
                    ProfitProtectionConfig = new AutoProfitProtectionConfig
                    {
                        IsEnabled = false
                    }
                };
                
                _logger.LogInformation($"🧪 测试配置详情:");
                _logger.LogInformation($"   📋 配置名称: {testConfig.Name}");
                _logger.LogInformation($"   ⏱️ 扫描间隔: {testConfig.ScanIntervalSeconds}秒");
                _logger.LogInformation($"   💰 保本配置: {(testConfig.BreakEvenConfig.IsEnabled ? "启用" : "禁用")}, 触发: {testConfig.BreakEvenConfig.TriggerProfitAmount}U");
                _logger.LogInformation($"   📈 推仓配置: {(testConfig.AddPositionConfig.IsEnabled ? "启用" : "禁用")}");
                foreach (var tier in testConfig.AddPositionConfig.Tiers.Where(t => t.TriggerProfitAmount > 0))
                {
                    _logger.LogInformation($"     🎯 阶梯{tier.TierIndex}: {tier.TriggerProfitAmount}U");
                }
                
                // 创建配置验证服务
                var validationLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
                    builder.AddConsole()).CreateLogger<ConfigValidationService>();
                var validationService = new ConfigValidationService(validationLogger);
                
                // 执行验证
                _logger.LogInformation("🔍 开始执行配置验证...");
                var result = await validationService.ValidateAsync(testConfig, ValidationMode.Strict, true);
                
                // 显示验证结果
                var resultText = new StringBuilder();
                resultText.AppendLine("🧪 配置验证测试结果:");
                resultText.AppendLine($"验证状态: {(result.IsValid ? "✅ 通过" : "❌ 失败")}");
                resultText.AppendLine($"错误数量: {result.Errors.Count}");
                resultText.AppendLine($"警告数量: {result.Warnings.Count}");
                resultText.AppendLine($"建议数量: {result.Suggestions.Count}");
                
                if (result.Errors.Any())
                {
                    resultText.AppendLine("\n❌ 错误详情:");
                    foreach (var error in result.Errors)
                    {
                        resultText.AppendLine($"• [{error.ErrorCode}] {error.ConfigKey}: {error.Message}");
                    }
                }
                
                if (result.Warnings.Any())
                {
                    resultText.AppendLine("\n⚠️ 警告详情:");
                    foreach (var warning in result.Warnings)
                    {
                        resultText.AppendLine($"• [{warning.WarningCode}] {warning.ConfigKey}: {warning.Message}");
                    }
                }
                
                if (result.Suggestions.Any())
                {
                    resultText.AppendLine("\n💡 建议详情:");
                    foreach (var suggestion in result.Suggestions)
                    {
                        resultText.AppendLine($"• [{suggestion.SuggestionCode}] {suggestion.ConfigKey}: {suggestion.Reason}");
                    }
                }
                
                MessageBox.Show(resultText.ToString(), "配置验证测试结果", MessageBoxButton.OK, 
                    result.IsValid ? MessageBoxImage.Information : MessageBoxImage.Warning);
                
                _logger.LogInformation("🧪 配置验证测试完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🧪 配置验证测试失败");
                MessageBox.Show($"测试失败: {ex.Message}", "测试错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 🔍 调试自动盯盘状态命令 - 诊断保本执行问题
        /// </summary>
        [RelayCommand]
        private async Task DebugAutoMonitorStatusAsync()
        {
            try
            {
                if (_autoMonitorService == null)
                {
                    MessageBox.Show("自动盯盘服务未初始化", "调试信息", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var debugInfo = new StringBuilder();
                                 debugInfo.AppendLine("🔍 自动盯盘状态调试报告");
                 debugInfo.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                 debugInfo.AppendLine(new string('=', 50));

                // 1. 基础状态信息
                debugInfo.AppendLine("\n📊 基础状态信息:");
                debugInfo.AppendLine($"• 监控服务运行状态: {(_autoMonitorService.IsRunning ? "运行中" : "已停止")}");
                debugInfo.AppendLine($"• UI显示状态: {(IsAutoMonitorRunning ? "运行中" : "已停止")}");
                debugInfo.AppendLine($"• 当前配置: {(_autoMonitorService.CurrentConfig?.Name ?? "无")}");
                
                // 2. 配置信息
                var config = _autoMonitorService.CurrentConfig;
                if (config != null)
                {
                    debugInfo.AppendLine("\n⚙️ 配置信息:");
                    debugInfo.AppendLine($"• 扫描间隔: {config.ScanIntervalSeconds}秒");
                    debugInfo.AppendLine($"• 自动保本: {(config.BreakEvenConfig.IsEnabled ? "启用" : "禁用")}");
                    if (config.BreakEvenConfig.IsEnabled)
                    {
                        debugInfo.AppendLine($"  - 触发金额: {config.BreakEvenConfig.TriggerProfitAmount:F2}U");
                    }
                    debugInfo.AppendLine($"• 推仓功能: {(config.AddPositionConfig.IsEnabled ? "启用" : "禁用")}");
                    if (config.AddPositionConfig.IsEnabled)
                    {
                        debugInfo.AppendLine($"  - 启用阶梯数: {config.AddPositionConfig.Tiers.Count}");
                    }
                }

                // 3. 持仓档案信息
                debugInfo.AppendLine("\n📋 持仓档案信息:");
                var profiles = _autoMonitorService.GetPositionProfiles();
                debugInfo.AppendLine($"• 档案总数: {profiles.Count}");
                
                foreach (var kvp in profiles.Take(5)) // 只显示前5个
                {
                    var profile = kvp.Value;
                    debugInfo.AppendLine($"• {profile.Symbol}_{profile.PositionSide}:");
                    debugInfo.AppendLine($"  - 创建时间: {profile.CreateTime:HH:mm:ss}");
                    debugInfo.AppendLine($"  - 最后更新: {profile.LastUpdateTime:HH:mm:ss}");
                    debugInfo.AppendLine($"  - 触发记录数: {profile.TriggerRecords.Count}");
                    
                    if (profile.TriggerRecords.Any())
                    {
                                                 debugInfo.AppendLine("  - 已触发操作:");
                         foreach (var trigger in profile.TriggerRecords.Take(3))
                         {
                             debugInfo.AppendLine($"    * {trigger.Key}: {trigger.Value.TriggerTime:HH:mm:ss} ({(trigger.Value.IsExecuted ? "已执行" : "未执行")})");
                         }
                    }
                }

                // 4. 执行历史
                debugInfo.AppendLine("\n📜 执行历史 (最近10条):");
                var history = _autoMonitorService.GetExecutionHistory();
                debugInfo.AppendLine($"• 历史记录总数: {history.Count}");
                
                foreach (var item in history.TakeLast(10))
                {
                    debugInfo.AppendLine($"• {item.ExecutionTime:HH:mm:ss} [{item.Symbol}] {item.ExecutionType} - {(item.IsSuccess ? "✅" : "❌")} (浮盈:{item.TriggerPnl:F2}U)");
                }

                // 5. 统一状态管理器状态
                debugInfo.AppendLine("\n🔄 统一状态管理器:");
                var unifiedStats = _autoMonitorService.GetUnifiedStateStatistics();
                debugInfo.AppendLine($"• 总合约数: {unifiedStats.TotalContracts}");
                debugInfo.AppendLine($"• 活跃合约数: {unifiedStats.ActiveContracts}");
                debugInfo.AppendLine($"• 总操作数: {unifiedStats.TotalOperations}");
                debugInfo.AppendLine($"• 最后同步: {unifiedStats.LastSyncTime:HH:mm:ss}");

                                 // 6. 冷却期状态
                 debugInfo.AppendLine("\n🛡️ 冷却期状态:");
                 var cooldownStats = _autoMonitorService.GetCooldownStatistics();
                 var activeCooldowns = _autoMonitorService.GetActiveCooldowns();
                 debugInfo.AppendLine($"• 总执行次数: {cooldownStats.TotalExecutions}");
                 debugInfo.AppendLine($"• 阻止次数: {cooldownStats.CooldownBlocks}");
                 debugInfo.AppendLine($"• 阻止率: {cooldownStats.BlockRate:F1}%");
                 debugInfo.AppendLine($"• 当前活跃冷却: {activeCooldowns.Count}个");

                 foreach (var cooldown in activeCooldowns.Take(5))
                 {
                     debugInfo.AppendLine($"  - {cooldown.OperationKey} ({cooldown.OperationType}): 剩余{cooldown.RemainingTime.TotalSeconds:F0}秒");
                 }

                // 7. 止损单状态
                debugInfo.AppendLine("\n🛡️ 止损单管理状态:");
                var stopOrderStats = _autoMonitorService.StopOrderManager.Statistics;
                debugInfo.AppendLine($"• 总创建数: {stopOrderStats.TotalCreated}");
                debugInfo.AppendLine($"• 总取消数: {stopOrderStats.TotalCancelled}");
                debugInfo.AppendLine($"• 创建失败数: {stopOrderStats.CreateFailures}");
                debugInfo.AppendLine($"• 成功率: {stopOrderStats.SuccessRate:F1}%");
                debugInfo.AppendLine($"• 最后创建: {(stopOrderStats.LastCreateTime?.ToString("HH:mm:ss") ?? "无")}");

                // 8. 当前持仓检查
                debugInfo.AppendLine("\n💰 当前持仓分析:");
                var positions = await _binanceService.GetPositionsAsync();
                var validPositions = positions?.Where(p => Math.Abs(p.PositionAmt) > 0).ToList();
                
                if (validPositions?.Any() == true)
                {
                    debugInfo.AppendLine($"• 有效持仓数: {validPositions.Count}");
                    
                    foreach (var pos in validPositions.Take(5))
                    {
                        debugInfo.AppendLine($"• {pos.Symbol}_{pos.PositionSideString}:");
                        debugInfo.AppendLine($"  - 浮盈: {pos.UnrealizedProfit:F2}U");
                        debugInfo.AppendLine($"  - 成本价: {pos.EntryPrice:F4}");
                        debugInfo.AppendLine($"  - 数量: {pos.PositionAmt:F4}");
                        
                        // 检查是否满足保本条件
                        if (config?.BreakEvenConfig.IsEnabled == true)
                        {
                            var meetsBreakEven = pos.UnrealizedProfit > config.BreakEvenConfig.TriggerProfitAmount;
                            debugInfo.AppendLine($"  - 满足保本条件: {(meetsBreakEven ? "是" : "否")} (需要>{config.BreakEvenConfig.TriggerProfitAmount:F2}U)");
                            
                            // 检查是否已执行保本
                            var contractKey = $"{pos.Symbol}_{pos.PositionSideString}";
                            var isExecuted = _autoMonitorService.UnifiedStateManager.IsExecuted(pos.Symbol, pos.PositionSideString, ExecutionType.BreakEven);
                            debugInfo.AppendLine($"  - 已执行保本: {(isExecuted ? "是" : "否")}");
                        }
                    }
                }
                else
                {
                    debugInfo.AppendLine("• 当前无有效持仓");
                }

                // 显示调试信息
                var debugWindow = new Window
                {
                    Title = "自动盯盘状态调试",
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new ScrollViewer
                    {
                        Content = new TextBox
                        {
                            Text = debugInfo.ToString(),
                            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                            FontSize = 12,
                            IsReadOnly = true,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            Margin = new Thickness(10)
                        }
                    }
                };
                
                debugWindow.Show();
                
                // 同时记录到日志
                _logger.LogInformation("🔍 自动盯盘调试信息已生成");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成调试信息时发生错误");
                MessageBox.Show($"调试失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

// 扩展现有的MonitorStatusChangedEventArgs和ExecutionResultEventArgs
// 如果AutoMonitorService.cs文件中已经定义了这些类，这里就不需要重复定义了 
// 如果AutoMonitorService.cs文件中已经定义了这些类，这里就不需要重复定义了 