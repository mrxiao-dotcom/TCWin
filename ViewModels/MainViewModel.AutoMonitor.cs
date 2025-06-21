using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.Views;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;

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
                
                // 打开配置对话框
                _logger.LogInformation("准备创建配置对话框...");
                var configDialog = new AutoMonitorConfigDialog();
                _logger.LogInformation("配置对话框创建成功");
                
                // 从当前账户的配置中加载设置
                if (SelectedAccount != null)
                {
                    if (_accountAutoMonitorConfigs.TryGetValue(SelectedAccount.Name, out var accountConfig))
                    {
                        _logger.LogInformation($"从账户 {SelectedAccount.Name} 加载现有配置...");
                        configDialog.SetConfig(accountConfig);
                        _currentAutoMonitorConfig = accountConfig;
                    }
                    else
                    {
                        _logger.LogInformation($"账户 {SelectedAccount.Name} 没有现有配置，使用默认配置");
                    }
                }
                else
                {
                    _logger.LogWarning("没有选择账户，无法启动自动盯盘");
                    MessageBox.Show("请先选择账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
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
                if (SelectedAccount != null)
                {
                    _accountAutoMonitorConfigs[SelectedAccount.Name] = _currentAutoMonitorConfig;
                    _logger.LogInformation($"配置已保存到账户 {SelectedAccount.Name}: {_currentAutoMonitorConfig.Name}");
                }
                else
                {
                    _logger.LogError("没有选择账户，无法保存配置");
                    return;
                }

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
                var success = await _autoMonitorService.StartMonitoringAsync(_currentAutoMonitorConfig);
                
                if (success)
                {
                    _logger.LogInformation("监控服务启动成功，更新UI状态...");
                    UpdateAutoMonitorUI(true, "自动盯盘运行中", "停止盯盘", "#E74C3C");
                    _logger.LogInformation("自动监控已启动");
                }
                else
                {
                    _logger.LogWarning("监控服务启动失败");
                    MessageBox.Show("自动监控启动失败，请检查配置", "启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    _autoMonitorService.StopMonitoring();
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

                UpdateAutoMonitorUI(false, "未启动", "自动盯盘", "#4A90E2");

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


    }
}

// 扩展现有的MonitorStatusChangedEventArgs和ExecutionResultEventArgs
// 如果AutoMonitorService.cs文件中已经定义了这些类，这里就不需要重复定义了 