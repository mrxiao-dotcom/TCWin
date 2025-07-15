using System;
using System.Linq;
using System.Threading;
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
        /// 🔧 新增：操作冷却期管理，防止频繁点击
        /// </summary>
        private DateTime _lastOperationTime = DateTime.MinValue;
        private readonly TimeSpan _operationCooldown = TimeSpan.FromSeconds(3);
        
        /// <summary>
        /// 🔧 新增：操作锁，防止并发操作
        /// </summary>
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private bool _isOperationInProgress = false;
        
        /// <summary>
        /// 处理自动盯盘按钮点击命令 - 总是打开管理面板
        /// </summary>
        [RelayCommand]
        private async Task HandleAutoMonitorButtonAsync()
        {
            try
            {
                _logger.LogInformation("🔄 自动盯盘按钮被点击");
                
                if (SelectedAccount == null)
                {
                    MessageBox.Show("请先选择账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 🔧 修改：无论运行状态如何，都打开自动盯盘管理面板
                _logger.LogInformation("🚀 打开自动盯盘管理面板");
                await OpenAutoMonitorDashboardAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理自动盯盘按钮点击时发生错误");
                MessageBox.Show($"操作失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 切换自动监控命令
        /// </summary>
        [RelayCommand]
        private async Task ToggleAutoMonitorAsync()
        {
            // 🔧 操作锁：防止并发操作
            if (_isOperationInProgress)
            {
                var concurrentMsg = "⚠️ 检测到并发操作，忽略本次点击";
                LogService.LogWarning(concurrentMsg);
                MessageBox.Show("操作正在进行中，请稍候...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!await _operationLock.WaitAsync(100)) // 100ms超时
            {
                var lockTimeoutMsg = "⚠️ 获取操作锁超时，忽略本次点击";
                LogService.LogWarning(lockTimeoutMsg);
                MessageBox.Show("系统忙碌，请稍后重试...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _isOperationInProgress = true;
                var operationId = Guid.NewGuid().ToString("N")[..8];
                var operationStartMsg = $"🔒 开始操作 {operationId} - 时间: {DateTime.Now:HH:mm:ss.fff}";
                LogService.LogInfo(operationStartMsg);
                
                // 🔧 关键修复：捕获操作时的状态，避免异步竞争条件
                var operationTimestamp = DateTime.Now;
                var isCurrentlyRunning = IsAutoMonitorRunning;
                var buttonText = AutoMonitorButtonText;
                
                // 🔧 双重日志：记录操作决策状态
                var statusInfo = $"🔄 自动盯盘按钮被点击 - 时间: {operationTimestamp:HH:mm:ss.fff}";
                _logger.LogInformation(statusInfo);
                LogService.LogInfo(statusInfo);
                
                var detailInfo = $"📊 操作决策状态快照:\n" +
                    $"  • IsAutoMonitorRunning (决策依据): {isCurrentlyRunning}\n" +
                    $"  • AutoMonitorButtonText: {buttonText}\n" +
                    $"  • IsAutoMonitorButtonEnabled: {IsAutoMonitorButtonEnabled}\n" +
                    $"  • AutoMonitorStatusMessage: {AutoMonitorStatusMessage}\n" +
                    $"  • _autoMonitorService == null: {_autoMonitorService == null}";
                
                if (_autoMonitorService != null)
                {
                    detailInfo += $"\n  • _autoMonitorService.IsRunning: {_autoMonitorService.IsRunning}";
                    detailInfo += $"\n  • _autoMonitorService.CurrentConfig != null: {_autoMonitorService.CurrentConfig != null}";
                }
                
                _logger.LogInformation(detailInfo);
                LogService.LogInfo(detailInfo);
                
                // 🔧 修复：冷却期检查，防止频繁点击导致并发问题
                var timeSinceLastOperation = operationTimestamp - _lastOperationTime;
                if (timeSinceLastOperation < _operationCooldown)
                {
                    var remainingCooldown = _operationCooldown - timeSinceLastOperation;
                    var cooldownMsg = $"⏰ 操作过于频繁，请等待 {remainingCooldown.TotalSeconds:0.1} 秒后再试";
                    _logger.LogWarning(cooldownMsg);
                    LogService.LogWarning(cooldownMsg);
                    MessageBox.Show($"操作过于频繁，请等待 {remainingCooldown.TotalSeconds:0.1} 秒后再试", 
                        "操作限制", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                _lastOperationTime = operationTimestamp;
                
                // 🔧 关键修复：基于快照状态进行操作决策，而非实时状态
                if (isCurrentlyRunning)
                {
                    // 停止自动监控
                    var stopMsg = $"🛑 用户选择停止自动监控（基于快照状态: {isCurrentlyRunning}）...";
                    _logger.LogInformation(stopMsg);
                    LogService.LogInfo(stopMsg);
                    await StopAutoMonitorAsync();
                }
                else
                {
                    // 启动自动监控
                    var startMsg = $"🚀 用户选择启动自动监控（基于快照状态: {isCurrentlyRunning}）...";
                    _logger.LogInformation(startMsg);
                    LogService.LogInfo(startMsg);
                    await StartAutoMonitorAsync();
                }
            }
            catch (Exception ex)
            {
                // 🔧 双重异常日志：确保异常信息被记录
                var exceptionInfo = $"❌ 切换自动监控状态时发生异常: {ex.GetType().Name}\n消息: {ex.Message}\n堆栈: {ex.StackTrace}";
                _logger.LogError(ex, "❌ 切换自动监控状态时发生异常");
                LogService.LogError("切换自动监控状态异常", ex);
                LogService.LogError(exceptionInfo);
                
                // 异常时恢复按钮状态
                UpdateAutoMonitorUI(false, "操作异常", "自动盯盘", "#27AE60", true);
                MessageBox.Show($"操作失败：{ex.Message}\n\n详细信息已记录到日志文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 🔧 释放操作锁
                _isOperationInProgress = false;
                _operationLock.Release();
                var operationEndMsg = $"🔓 操作结束 - 时间: {DateTime.Now:HH:mm:ss.fff}";
                LogService.LogInfo(operationEndMsg);
            }
        }

        /// <summary>
        /// 配置自动盯盘参数命令（只保存配置，不启动）
        /// </summary>
        [RelayCommand]
        private Task ConfigureAutoMonitorAsync()
        {
            try
            {
                var operationTimestamp = DateTime.Now;
                var statusInfo = $"🔧 配置自动盯盘参数按钮被点击 - 时间: {operationTimestamp:HH:mm:ss.fff}";
                _logger.LogInformation(statusInfo);
                LogService.LogInfo(statusInfo);

                if (SelectedAccount == null)
                {
                    _logger.LogWarning("没有选择账户，无法配置自动盯盘");
                    MessageBox.Show("请先选择账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return Task.CompletedTask;
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
                    return Task.CompletedTask; // 用户取消了配置
                }

                // 🎯 关键修改：只保存配置，不启动服务
                _currentAutoMonitorConfig = configDialog.ConfigResult;
                _accountAutoMonitorConfigs[SelectedAccount.Name] = _currentAutoMonitorConfig;
                _logger.LogInformation($"✅ 配置已保存到账户 {SelectedAccount.Name}: {_currentAutoMonitorConfig.Name}");
                
                // 🔧 新增：持久化配置到文件
                try
                {
                    _configPersistenceService.SaveSingleAccountConfig(SelectedAccount.Name, _currentAutoMonitorConfig);
                    _logger.LogInformation($"💾 配置已持久化到文件: {SelectedAccount.Name}");
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, $"❌ 配置持久化失败: {SelectedAccount.Name}");
                }

                // 显示保存成功的提示
                var configDetails = $"配置名称：{_currentAutoMonitorConfig.Name}\n" +
                                   $"扫描间隔：{_currentAutoMonitorConfig.ScanIntervalSeconds}秒\n" +
                                   $"保本设置：{(_currentAutoMonitorConfig.BreakEvenConfig.IsEnabled ? "已启用" : "已禁用")}\n" +
                                   $"推仓阶梯：{(_currentAutoMonitorConfig.AddPositionConfig.IsEnabled ? _currentAutoMonitorConfig.AddPositionConfig.Tiers.Count + "个" : "已禁用")}\n" +
                                   $"止盈阶梯：{(_currentAutoMonitorConfig.ProfitProtectionConfig.IsEnabled ? _currentAutoMonitorConfig.ProfitProtectionConfig.Tiers.Count + "个" : "已禁用")}";

                MessageBox.Show($"✅ 自动盯盘配置已更新！\n\n{configDetails}\n\n📝 说明：\n• 基础参数配置已更新到本地文件\n• 现在可以在【自动盯盘】面板中直接启动监控\n• 如需调整具体合约设置，可在盯盘面板中重新加载配置", 
                               "配置更新成功", MessageBoxButton.OK, MessageBoxImage.Information);

                // 通知监控面板刷新数据（如果已打开）
                NotifyAutoMonitorDashboardRefresh();
                
                // 🎯 新增：通知配置同步管理器处理配置变化
                NotifyConfigurationSyncManager(_currentAutoMonitorConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 配置自动盯盘参数时发生错误");
                LogService.LogError("配置自动盯盘参数异常", ex);
                MessageBox.Show($"配置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// 启动自动监控
        /// </summary>
        private async Task StartAutoMonitorAsync()
        {
            // 🔧 测试日志：验证日志系统是否正常工作
            LogService.LogInfo($"📝 测试日志 - StartAutoMonitorAsync开始 - {DateTime.Now:HH:mm:ss.fff}");
            LogService.LogInfo($"📍 日志文件路径: {LogService.GetLogFilePath()}");
            
            try
            {
                _logger.LogInformation("🚀 开始启动自动监控流程...");
                LogService.LogInfo("🚀 开始启动自动监控流程...");
                
                // 🔧 修复空引用异常：先检查账户是否已选择
                if (SelectedAccount == null)
                {
                    _logger.LogWarning("没有选择账户，无法启动自动盯盘");
                    MessageBox.Show("请先选择账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 🎯 检查是否已有保存的配置
                if (!_accountAutoMonitorConfigs.TryGetValue(SelectedAccount.Name, out var accountConfig))
                {
                    _logger.LogWarning($"账户 {SelectedAccount.Name} 没有配置，无法启动");
                    MessageBox.Show("请先配置自动盯盘参数！\n\n点击【盯盘参数配置】按钮进行设置。", "配置缺失", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 使用已保存的配置
                _currentAutoMonitorConfig = accountConfig;
                _logger.LogInformation($"✅ 使用账户 {SelectedAccount.Name} 的配置: {_currentAutoMonitorConfig.Name}");

                // 🔧 关键修复：每次启动都重新创建服务实例，避免通道关闭问题
                if (_autoMonitorService != null)
                {
                    _logger.LogInformation("🗑️ 检测到现有服务实例，准备完全重新创建...");
                    LogService.LogInfo("🗑️ 检测到现有服务实例，准备完全重新创建...");
                    
                    _logger.LogInformation($"  • 当前服务运行状态: {_autoMonitorService.IsRunning}");
                    _logger.LogInformation($"  • 当前配置: {(_autoMonitorService.CurrentConfig?.Name ?? "无")}");
                    
                    // 完全清理现有服务实例
                    try
                    {
                        if (_autoMonitorService.IsRunning)
                        {
                            _logger.LogWarning("⚠️ 服务仍在运行，先停止...");
                            await _autoMonitorService.StopMonitoringAsync();
                            await Task.Delay(500);
                        }
                        
                        _logger.LogInformation("🔗 取消事件订阅...");
                        _autoMonitorService.MonitorStatusChanged -= OnAutoMonitorStatusChanged;
                        _autoMonitorService.ExecutionCompleted -= OnAutoMonitorExecutionCompleted;
                        
                        _logger.LogInformation("🗑️ 销毁现有服务实例...");
                        _autoMonitorService.Dispose();
                        _autoMonitorService = null;
                        
                        _logger.LogInformation("✅ 现有服务实例已完全清理");
                        LogService.LogInfo("✅ 现有服务实例已完全清理");
                        
                        // 等待确保所有资源释放
                        await Task.Delay(1000);
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx, "❌ 清理现有服务实例时发生异常");
                        LogService.LogError("清理现有服务实例异常", cleanupEx);
                        // 继续创建新实例
                    }
                }
                
                // 🔧 测试BinanceService连接状态
                _logger.LogInformation("🔗 测试BinanceService连接状态...");
                LogService.LogInfo("🔗 测试BinanceService连接状态...");
                try
                {
                    // 通过获取账户信息测试连接
                    var accountInfo = await _binanceService.GetAccountInfoAsync();
                    if (accountInfo == null)
                    {
                        throw new InvalidOperationException("无法获取账户信息，BinanceService连接异常");
                    }
                    _logger.LogInformation("✅ BinanceService连接测试成功");
                    LogService.LogInfo("✅ BinanceService连接测试成功");
                }
                catch (Exception testEx)
                {
                    _logger.LogError(testEx, "❌ BinanceService连接测试失败");
                    LogService.LogError("BinanceService连接测试失败", testEx);
                    
                    // 如果是通道关闭错误，尝试重新初始化连接
                    if (testEx.Message.Contains("channel has been closed", StringComparison.OrdinalIgnoreCase) ||
                        testEx.Message.Contains("channel", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("🔄 检测到通道关闭错误，尝试重新初始化BinanceService...");
                        LogService.LogInfo("🔄 检测到通道关闭错误，尝试重新初始化BinanceService...");
                        
                        // 等待一段时间让连接完全关闭
                        await Task.Delay(2000);
                        
                        // 再次测试连接
                        try
                        {
                            var retryAccountInfo = await _binanceService.GetAccountInfoAsync();
                            if (retryAccountInfo == null)
                            {
                                throw new InvalidOperationException("重试后仍无法获取账户信息");
                            }
                            _logger.LogInformation("✅ BinanceService重新连接成功");
                            LogService.LogInfo("✅ BinanceService重新连接成功");
                        }
                        catch (Exception retryEx)
                        {
                            _logger.LogError(retryEx, "❌ BinanceService重新连接失败");
                            LogService.LogError("BinanceService重新连接失败", retryEx);
                            throw new InvalidOperationException($"BinanceService连接失败: {retryEx.Message}", retryEx);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"BinanceService连接测试失败: {testEx.Message}", testEx);
                    }
                }
                
                // 创建全新的服务实例
                _logger.LogInformation("📦 创建全新的自动监控服务实例...");
                LogService.LogInfo("📦 创建全新的自动监控服务实例...");
                try
                {
                    var serviceLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
                        builder.AddConsole()).CreateLogger<AutoMonitorService>();
                    _autoMonitorService = new AutoMonitorService(_binanceService, this, serviceLogger);
                    _logger.LogInformation("✅ 全新自动监控服务实例创建成功");
                    LogService.LogInfo("✅ 全新自动监控服务实例创建成功");
                }
                catch (Exception createEx)
                {
                    _logger.LogError(createEx, "❌ 创建全新自动监控服务实例时发生异常");
                    LogService.LogError("创建全新自动监控服务实例异常", createEx);
                    throw; // 重新抛出异常
                }
                
                // 🔧 增强日志：事件订阅过程
                _logger.LogInformation("🔗 设置事件订阅...");
                try
                {
                    // 先取消已有的订阅（如果存在）
                    _autoMonitorService.MonitorStatusChanged -= OnAutoMonitorStatusChanged;
                    _autoMonitorService.ExecutionCompleted -= OnAutoMonitorExecutionCompleted;
                    _logger.LogInformation("  • 已取消旧的事件订阅");
                    
                    // 重新订阅事件
                    _autoMonitorService.MonitorStatusChanged += OnAutoMonitorStatusChanged;
                    _autoMonitorService.ExecutionCompleted += OnAutoMonitorExecutionCompleted;
                    _logger.LogInformation("✅ 事件订阅设置完成");
                }
                catch (Exception eventEx)
                {
                    _logger.LogError(eventEx, "❌ 设置事件订阅时发生异常");
                }

                // 🔧 增强日志：UI状态更新
                _logger.LogInformation("🎛️ 更新UI状态为启动中...");
                UpdateAutoMonitorUI(false, "正在启动服务...", "正在启动服务", "#F39C12", false);
                _logger.LogInformation("✅ UI状态更新完成");
                
                // 🔍 启动前先验证配置
                try
                {
                    // 使用新的重载方法，传入配置对象进行验证
                    var validationResult = await _autoMonitorService.ValidateConfigAsync(_currentAutoMonitorConfig, ValidationMode.Strict, true);
                    if (validationResult.Errors.Any())
                    {
                        var errorDetails = string.Join("\n", validationResult.Errors.Select(e => $"• {e.Message}"));
                        _logger.LogError($"配置验证失败:\n{errorDetails}");
                        
                        // 🔧 修复：配置验证失败时恢复按钮状态
                        UpdateAutoMonitorUI(false, "配置验证失败", "自动盯盘", "#27AE60", true);
                        
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
                    
                    // 🔧 修复：配置验证异常时恢复按钮状态
                    UpdateAutoMonitorUI(false, "验证异常", "自动盯盘", "#27AE60", true);
                    
                    MessageBox.Show($"配置验证失败：{validateEx.Message}", 
                        "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 🔧 增强日志：服务启动过程
                _logger.LogInformation("🚀 开始启动自动监控服务...");
                _logger.LogInformation($"  • 配置名称: {_currentAutoMonitorConfig.Name}");
                _logger.LogInformation($"  • 扫描间隔: {_currentAutoMonitorConfig.ScanIntervalSeconds}秒");
                _logger.LogInformation($"  • 保本配置: {(_currentAutoMonitorConfig.BreakEvenConfig.IsEnabled ? "启用" : "禁用")}");
                _logger.LogInformation($"  • 推仓配置: {(_currentAutoMonitorConfig.AddPositionConfig.IsEnabled ? "启用" : "禁用")}");
                _logger.LogInformation($"  • 保盈配置: {(_currentAutoMonitorConfig.ProfitProtectionConfig.IsEnabled ? "启用" : "禁用")}");
                
                bool success = false;
                string startupError = "";
                Exception startupException = null;
                
                try
                {
                    _logger.LogInformation("📡 调用StartMonitoringAsync方法...");
                    success = await _autoMonitorService.StartMonitoringAsync(_currentAutoMonitorConfig);
                    _logger.LogInformation($"📡 StartMonitoringAsync返回结果: {success}");
                    
                    // 验证服务状态
                    if (success)
                    {
                        _logger.LogInformation("🔍 验证服务启动后状态...");
                        _logger.LogInformation($"  • _autoMonitorService.IsRunning: {_autoMonitorService.IsRunning}");
                        _logger.LogInformation($"  • _autoMonitorService.CurrentConfig != null: {_autoMonitorService.CurrentConfig != null}");
                        if (_autoMonitorService.CurrentConfig != null)
                        {
                            _logger.LogInformation($"  • CurrentConfig.Name: {_autoMonitorService.CurrentConfig.Name}");
                        }
                    }
                }
                catch (Exception startEx)
                {
                    success = false;
                    startupError = startEx.Message;
                    startupException = startEx;
                    
                    // 🔧 双重异常日志：确保启动异常被记录
                    _logger.LogError(startEx, "❌ 自动监控服务启动时发生异常");
                    LogService.LogError("自动监控服务启动异常", startEx);
                    
                    var startExceptionInfo = $"❌ 启动异常详情:\n" +
                        $"异常类型: {startEx.GetType().Name}\n" +
                        $"异常消息: {startEx.Message}\n" +
                        $"内部异常: {(startEx.InnerException?.Message ?? "无")}\n" +
                        $"堆栈跟踪: {startEx.StackTrace}";
                    
                    _logger.LogError(startExceptionInfo);
                    LogService.LogError(startExceptionInfo);
                }
                
                if (success)
                {
                    _logger.LogInformation("✅ 监控服务启动成功！");
                    _logger.LogInformation("🎛️ 更新UI状态为运行中...");
                    UpdateAutoMonitorUI(true, "自动盯盘运行中", "停止盯盘", "#E74C3C", true);
                    _logger.LogInformation($"🎯 自动盯盘启动完成 - 配置: {_currentAutoMonitorConfig.Name}");
                    
                    // 通知监控界面刷新数据
                    try
                    {
                        NotifyAutoMonitorDashboardRefresh();
                        _logger.LogInformation("✅ 已通知监控界面刷新");
                    }
                    catch (Exception notifyEx)
                    {
                        _logger.LogWarning(notifyEx, "⚠️ 通知监控界面刷新时发生异常");
                    }
                    
                    MessageBox.Show($"自动盯盘已启动！\n配置: {_currentAutoMonitorConfig.Name}", "启动成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _logger.LogError($"❌ 监控服务启动失败");
                    _logger.LogError($"❌ 失败原因: {startupError}");
                    
                    // 🔧 启动失败时清理服务实例，避免状态污染
                    _logger.LogInformation("🗑️ 启动失败，清理服务实例...");
                    LogService.LogInfo("🗑️ 启动失败，清理服务实例...");
                    try
                    {
                        if (_autoMonitorService != null)
                        {
                            _autoMonitorService.MonitorStatusChanged -= OnAutoMonitorStatusChanged;
                            _autoMonitorService.ExecutionCompleted -= OnAutoMonitorExecutionCompleted;
                            _autoMonitorService.Dispose();
                            _autoMonitorService = null;
                            _logger.LogInformation("✅ 启动失败后服务实例已清理");
                            LogService.LogInfo("✅ 启动失败后服务实例已清理");
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx, "❌ 清理失败服务实例时发生异常");
                        LogService.LogError("清理失败服务实例异常", cleanupEx);
                    }
                    
                    // 启动失败后恢复按钮正常状态
                    _logger.LogInformation("🎛️ 恢复UI状态为未运行...");
                    UpdateAutoMonitorUI(false, "自动盯盘启动失败", "自动盯盘", "#27AE60", true);
                    
                    // 构建错误消息
                    var errorMessage = "自动监控启动失败\n\n建议操作：\n• 检查网络连接和API密钥\n• 确认已选择正确的交易账户\n• 重新配置自动盯盘参数";
                    
                    if (!string.IsNullOrEmpty(startupError))
                    {
                        errorMessage += $"\n\n错误详情：{startupError}";
                    }
                    
                    if (startupException != null && startupException.GetType().Name.Contains("InvalidOperation"))
                    {
                        errorMessage += "\n\n💡 提示：可能是服务状态冲突，请稍等片刻后重试";
                    }
                    
                    // 🔧 特殊处理通道关闭错误
                    if (startupError.Contains("channel has been closed", StringComparison.OrdinalIgnoreCase))
                    {
                        errorMessage += "\n\n🔧 检测到连接通道问题，建议：\n• 等待10-15秒后重试\n• 确保网络连接稳定\n• 检查API访问权限";
                    }
                    
                    MessageBox.Show(errorMessage, "启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // 🔧 双重异常日志：确保顶层异常被记录
                var topExceptionInfo = $"❌ 启动自动监控时发生顶层异常:\n" +
                    $"异常类型: {ex.GetType().Name}\n" +
                    $"异常消息: {ex.Message}\n" +
                    $"内部异常: {(ex.InnerException?.Message ?? "无")}\n" +
                    $"堆栈跟踪: {ex.StackTrace}";
                
                _logger.LogError(ex, "❌ 启动自动监控时发生顶层异常");
                LogService.LogError("启动自动监控顶层异常", ex);
                LogService.LogError(topExceptionInfo);
                
                // 启动异常时恢复按钮状态
                _logger.LogInformation("🎛️ 顶层异常处理：恢复UI状态...");
                LogService.LogInfo("顶层异常处理：恢复UI状态");
                UpdateAutoMonitorUI(false, "启动异常", "自动盯盘", "#27AE60", true);
                
                MessageBox.Show($"启动失败：{ex.Message}\n\n详细信息已记录到日志文件:\n{LogService.GetLogFilePath()}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 停止自动监控
        /// </summary>
        /// <param name="clearConfig">是否清空当前配置（账户切换时为true，用户手动停止时为false）</param>
        private async Task StopAutoMonitorAsync(bool clearConfig = false)
        {
            // 🔧 增强日志：停止过程开始
            _logger.LogInformation($"🛑 开始停止自动监控流程 - 时间: {DateTime.Now:HH:mm:ss.fff}");
            _logger.LogInformation($"📊 停止前状态检查:");
            _logger.LogInformation($"  • clearConfig: {clearConfig}");
            _logger.LogInformation($"  • IsAutoMonitorRunning: {IsAutoMonitorRunning}");
            _logger.LogInformation($"  • _autoMonitorService == null: {_autoMonitorService == null}");
            if (_autoMonitorService != null)
            {
                _logger.LogInformation($"  • _autoMonitorService.IsRunning: {_autoMonitorService.IsRunning}");
                _logger.LogInformation($"  • _autoMonitorService.CurrentConfig != null: {_autoMonitorService.CurrentConfig != null}");
            }
            
            // 立即更新按钮状态为停止中，防止用户重复点击
            _logger.LogInformation("🎛️ 更新UI状态为停止中...");
            UpdateAutoMonitorUI(IsAutoMonitorRunning, "正在停止服务...", "正在停止服务", "#F39C12", false);
            _logger.LogInformation("✅ UI状态更新完成");
            
            try
            {
                if (_autoMonitorService != null)
                {
                    _logger.LogInformation("📡 调用监控服务停止方法...");
                    try
                    {
                        await _autoMonitorService.StopMonitoringAsync();
                        _logger.LogInformation("✅ StopMonitoringAsync调用完成");
                    }
                    catch (Exception stopEx)
                    {
                        _logger.LogError(stopEx, "❌ 调用StopMonitoringAsync时发生异常");
                        throw;
                    }
                    
                    // 验证停止后的状态
                    _logger.LogInformation("🔍 验证服务停止后状态...");
                    _logger.LogInformation($"  • _autoMonitorService.IsRunning: {_autoMonitorService.IsRunning}");
                    
                    // 等待服务完全停止，确保内部状态清理完成
                    _logger.LogInformation("⏱️ 等待服务完全停止（500ms）...");
                    await Task.Delay(500);
                    _logger.LogInformation("✅ 服务停止等待完成");
                    
                    // 取消事件订阅，避免后续重复订阅问题
                    _logger.LogInformation("🔗 取消事件订阅...");
                    try
                    {
                        _autoMonitorService.MonitorStatusChanged -= OnAutoMonitorStatusChanged;
                        _autoMonitorService.ExecutionCompleted -= OnAutoMonitorExecutionCompleted;
                        _logger.LogInformation("✅ 事件订阅取消完成");
                    }
                    catch (Exception eventEx)
                    {
                        _logger.LogWarning(eventEx, "⚠️ 取消事件订阅时发生异常");
                    }
                    
                    // 再次等待确保事件处理完成
                    _logger.LogInformation("⏱️ 等待事件处理完成（200ms）...");
                    await Task.Delay(200);
                    _logger.LogInformation("✅ 事件处理等待完成");
                }
                else
                {
                    _logger.LogInformation("ℹ️ 自动监控服务实例为空，无需停止");
                }

                // 处理配置清理
                if (clearConfig)
                {
                    _logger.LogInformation("🗑️ 清理配置和服务实例（账户切换）...");
                    _currentAutoMonitorConfig = null;
                    
                    if (_autoMonitorService != null)
                    {
                        try
                        {
                            _autoMonitorService.Dispose();
                            _autoMonitorService = null;
                            _logger.LogInformation("✅ 服务实例已清理");
                        }
                        catch (Exception disposeEx)
                        {
                            _logger.LogWarning(disposeEx, "⚠️ 清理服务实例时发生异常");
                        }
                    }
                    
                    _logger.LogInformation("✅ 配置清理完成（账户切换）");
                }
                else
                {
                    _logger.LogInformation("ℹ️ 保留配置和服务实例（用户手动停止）");
                }

                // 最终等待确保所有异步操作完成
                _logger.LogInformation("⏱️ 最终等待UI更新完成（100ms）...");
                await Task.Delay(100);
                
                // 🔧 最终状态验证
                var finalStatusCheck = $"🔍 停止完成后最终状态检查:\n" +
                    $"  • _autoMonitorService?.IsRunning: {(_autoMonitorService?.IsRunning.ToString() ?? "N/A")}\n" +
                    $"  • 当前UI IsAutoMonitorRunning: {IsAutoMonitorRunning}";
                _logger.LogInformation(finalStatusCheck);
                LogService.LogInfo(finalStatusCheck);
                
                // 只有在确认停止完成后才更新按钮状态
                _logger.LogInformation("🎛️ 更新UI状态为已停止...");
                LogService.LogInfo("🎛️ 更新UI状态为已停止...");
                UpdateAutoMonitorUI(false, "自动盯盘已停止", "自动盯盘", "#27AE60", true);
                _logger.LogInformation("✅ UI状态恢复完成");
                LogService.LogInfo("✅ UI状态恢复完成");
                
                // 通知监控界面刷新数据
                try
                {
                    NotifyAutoMonitorDashboardRefresh();
                    _logger.LogInformation("✅ 已通知监控界面刷新");
                }
                catch (Exception notifyEx)
                {
                    _logger.LogWarning(notifyEx, "⚠️ 通知监控界面刷新时发生异常");
                }
                
                _logger.LogInformation("🎉 自动监控完全停止，流程结束");
            }
            catch (Exception ex)
            {
                // 🔧 双重异常日志：确保停止异常被记录
                var stopExceptionInfo = $"❌ 停止自动监控时发生异常:\n" +
                    $"异常类型: {ex.GetType().Name}\n" +
                    $"异常消息: {ex.Message}\n" +
                    $"内部异常: {(ex.InnerException?.Message ?? "无")}\n" +
                    $"堆栈跟踪: {ex.StackTrace}";
                
                _logger.LogError(ex, "❌ 停止自动监控时发生异常");
                LogService.LogError("停止自动监控异常", ex);
                LogService.LogError(stopExceptionInfo);
                
                // 基础异常处理：恢复按钮状态
                _logger.LogInformation("🎛️ 异常处理：恢复UI状态...");
                LogService.LogInfo("停止异常处理：恢复UI状态");
                UpdateAutoMonitorUI(false, "停止异常", "自动盯盘", "#27AE60", true);
                MessageBox.Show($"停止失败：{ex.Message}\n\n详细信息已记录到日志文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新自动监控UI状态
        /// </summary>
        private void UpdateAutoMonitorUI(bool isRunning, string statusMessage, string buttonText, string buttonColor, bool? buttonEnabled = null)
        {
            // 🔧 优化按钮文本显示，根据运行状态自动添加状态信息
            string finalButtonText = GetButtonTextWithStatus(buttonText, isRunning);
            
            // 🔧 增强日志：记录UI状态更新
            var beforeUpdate = $"🎛️ UI状态更新前:\n" +
                $"  • IsAutoMonitorRunning: {IsAutoMonitorRunning} → {isRunning}\n" +
                $"  • AutoMonitorStatusMessage: {AutoMonitorStatusMessage} → {statusMessage}\n" +
                $"  • AutoMonitorButtonText: {AutoMonitorButtonText} → {finalButtonText}\n" +
                $"  • AutoMonitorButtonColor: {AutoMonitorButtonColor} → {buttonColor}\n" +
                $"  • IsAutoMonitorButtonEnabled: {IsAutoMonitorButtonEnabled} → {(buttonEnabled?.ToString() ?? "不变")}";
            
            _logger.LogInformation(beforeUpdate);
            LogService.LogInfo(beforeUpdate);
            
            IsAutoMonitorRunning = isRunning;
            // 设置状态消息和按钮提示 - 无论运行状态如何都打开管理面板
            AutoMonitorStatusMessage = isRunning ? "点击打开自动盯盘管理面板" : "点击打开自动盯盘配置和监控面板";
            AutoMonitorButtonText = finalButtonText;
            AutoMonitorButtonColor = buttonColor;
            
            // 如果提供了buttonEnabled参数，则更新按钮启用状态
            if (buttonEnabled.HasValue)
            {
                IsAutoMonitorButtonEnabled = buttonEnabled.Value;
            }
            
            var afterUpdate = $"✅ UI状态更新完成 - 时间: {DateTime.Now:HH:mm:ss.fff}";
            _logger.LogInformation(afterUpdate);
            LogService.LogInfo(afterUpdate);
        }

        /// <summary>
        /// 🔧 修改：根据运行状态生成按钮文本 - 总是打开管理面板
        /// </summary>
        private string GetButtonTextWithStatus(string baseText, bool isRunning)
        {
            // 处理特殊的操作状态文本（如"正在启动服务"等）
            if (baseText.Contains("正在"))
            {
                return baseText; // 保持原状态文本
            }
            
            // 🔧 修改：无论运行状态如何，都表示打开管理面板的功能
            if (isRunning)
            {
                return "盯盘管理";
            }
            else
            {
                return "自动盯盘";
            }
        }

        /// <summary>
        /// 自动监控状态变化事件处理
        /// </summary>
        private void OnAutoMonitorStatusChanged(object? sender, MonitorStatusChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 🔧 增强日志：记录状态变化事件
                var eventMsg = $"📡 收到状态变化事件 - IsRunning: {e.IsRunning}, Message: {e.Message}";
                _logger.LogInformation(eventMsg);
                LogService.LogInfo(eventMsg);
                
                var beforeChangeInfo = $"📊 状态变化前UI状态:\n" +
                    $"  • IsAutoMonitorRunning: {IsAutoMonitorRunning}\n" +
                    $"  • AutoMonitorStatusMessage: {AutoMonitorStatusMessage}\n" +
                    $"  • AutoMonitorButtonText: {AutoMonitorButtonText}";
                
                _logger.LogInformation(beforeChangeInfo);
                LogService.LogInfo(beforeChangeInfo);
                
                AutoMonitorStatusMessage = e.Message;
                
                if (!e.IsRunning && IsAutoMonitorRunning)
                {
                    // 监控意外停止，确保按钮启用状态正确
                    var unexpectedStopMsg = "⚠️ 检测到监控意外停止，更新UI状态";
                    _logger.LogWarning(unexpectedStopMsg);
                    LogService.LogWarning(unexpectedStopMsg);
                    
                    UpdateAutoMonitorUI(false, "已停止", "自动盯盘", "#27AE60", true);
                    
                    var afterChangeMsg = "✅ 意外停止后UI状态已更新";
                    _logger.LogInformation(afterChangeMsg);
                    LogService.LogInfo(afterChangeMsg);
                }
                else
                {
                    var normalUpdateMsg = $"ℹ️ 正常状态消息更新: {e.Message}";
                    _logger.LogInformation(normalUpdateMsg);
                    LogService.LogInfo(normalUpdateMsg);
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
        /// 启动持仓变化监听集成
        /// </summary>
        private async Task StartPositionChangeMonitoringIntegrationAsync(Views.AutoMonitor.AutoMonitorDashboard_Refactored dashboard)
        {
            try
            {
                _logger.LogInformation("🔄 开始集成持仓变化监听...");
                
                // 获取监控面板的控制器
                var controller = dashboard.GetController();
                if (controller == null)
                {
                    _logger.LogWarning("⚠️ 无法获取监控面板控制器，跳过持仓变化监听集成");
                    return;
                }
                
                // 启动持仓变化监听
                controller.StartPositionChangeMonitoring();
                
                // 如果已经在运行监控，同时启动带持仓同步的监控
                if (_autoMonitorService?.IsRunning == true)
                {
                    _logger.LogInformation("🔄 当前监控服务正在运行，启动持仓同步监控...");
                    await controller.StartMonitoringWithPositionSyncAsync();
                }
                
                _logger.LogInformation("✅ 持仓变化监听集成成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 持仓变化监听集成失败");
                // 不中断整个流程，仅记录错误
            }
        }

        /// <summary>
        /// 打开自动盯盘配置界面命令
        /// </summary>
        [RelayCommand]
        private async Task OpenAutoMonitorConfigAsync()
        {
            try
            {
                var operationTimestamp = DateTime.Now;
                var statusInfo = $"🔧 自动盯盘配置按钮被点击 - 时间: {operationTimestamp:HH:mm:ss.fff}";
                _logger.LogInformation(statusInfo);
                LogService.LogInfo(statusInfo);

                if (SelectedAccount == null)
                {
                    _logger.LogWarning("没有选择账户，无法配置自动盯盘");
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

                // 保存配置
                _currentAutoMonitorConfig = configDialog.ConfigResult;
                _accountAutoMonitorConfigs[SelectedAccount.Name] = _currentAutoMonitorConfig;
                
                // 更新UI状态
                var configName = _currentAutoMonitorConfig?.Name ?? "无名称";
                var successMsg = $"✅ 配置\"{configName}\"已保存成功";
                _logger.LogInformation(successMsg);
                LogService.LogInfo(successMsg);
                
                StatusMessage = $"自动盯盘配置已保存：{configName}";
                MessageBox.Show($"配置已保存：{configName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                var exceptionInfo = $"❌ 打开自动盯盘配置界面时发生异常: {ex.GetType().Name}\n消息: {ex.Message}";
                _logger.LogError(ex, "❌ 打开自动盯盘配置界面时发生异常");
                LogService.LogError("打开配置界面异常", ex);
                
                MessageBox.Show($"打开配置界面失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开自动盯盘监控界面命令
        /// </summary>
        [RelayCommand]
        private async Task OpenAutoMonitorDashboardAsync()
        {
            try
            {
                _logger.LogInformation("🖥️ 自动盯盘监控按钮被点击，准备打开监控面板");
                
                if (SelectedAccount == null)
                {
                    MessageBox.Show("请先选择账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 🔧 优先使用现有的AutoMonitorService，如果没有则创建新的
                if (_autoMonitorService == null)
                {
                    _logger.LogInformation("💡 监控服务未初始化，创建新实例用于监控面板...");
                    var serviceLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
                        builder.AddConsole()).CreateLogger<AutoMonitorService>();
                    _autoMonitorService = new AutoMonitorService(_binanceService, this, serviceLogger);
                    _logger.LogInformation("✅ 监控服务实例创建完成");
                }

                // 🆕 创建符合需求文档的自动盯盘配置窗口
                _logger.LogInformation("🏗️ 创建符合需求文档的自动盯盘配置窗口...");
                var configWindow = new Views.AutoMonitorConfigWindowSimple(
                    _autoMonitorService,
                    _logger,
                    this,
                    _binanceService);

                // 🔧 设置主窗口为配置窗口的Owner
                if (Application.Current?.MainWindow != null)
                {
                    configWindow.Owner = Application.Current.MainWindow;
                    _logger.LogInformation("✅ 配置窗口已设置主窗口为Owner");
                }
                else
                {
                    _logger.LogWarning("⚠️ 无法获取主窗口引用，配置窗口可能不会跟随主窗口关闭");
                }

                _logger.LogInformation("🖥️ 自动盯盘配置窗口创建成功，符合需求文档的三个区域结构");
                
                configWindow.Show();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 打开监控面板时发生异常");
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
        /// 简化的自动监控资源清理
        /// </summary>
        private void CleanupAutoMonitor()
        {
            if (_autoMonitorService != null)
            {
                try 
                {
                    _autoMonitorService.MonitorStatusChanged -= OnAutoMonitorStatusChanged;
                    _autoMonitorService.ExecutionCompleted -= OnAutoMonitorExecutionCompleted;
                }
                catch { /* 忽略事件取消订阅异常 */ }
                
                try
                {
                    _autoMonitorService.Dispose();
                }
                catch { /* 忽略销毁异常 */ }
                
                _autoMonitorService = null;
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