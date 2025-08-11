using System;
using System.Windows;
using BinanceFuturesTrader.Services;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.ViewModels
{
    /// <summary>
    /// MainViewModel全局模式管理部分
    /// </summary>
    public partial class MainViewModel
    {
        #region 全局模式管理
        
        /// <summary>
        /// 切换全局模式命令
        /// </summary>
        [RelayCommand]
        private void ToggleGlobalMode()
        {
            try
            {
                var globalMode = GlobalModeManager.Instance;
                var currentMode = globalMode.IsSimulationMode ? "模拟模式" : "实盘模式";
                var targetMode = globalMode.IsSimulationMode ? "实盘模式" : "模拟模式";
                
                // 如果要切换到实盘模式，需要确认
                if (globalMode.IsSimulationMode)
                {
                    var result = MessageBox.Show(
                        $"确认要从【{currentMode}】切换到【{targetMode}】吗？\n\n" +
                        "⚠️ 切换到实盘模式后，所有交易操作将使用真实资金！\n\n" +
                        "请确保：\n" +
                        "• API配置正确且具有交易权限\n" +
                        "• 已充分测试交易策略\n" +
                        "• 明确承担交易风险\n\n" +
                        "建议先在模拟模式下充分测试后再切换到实盘模式。",
                        "模式切换确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
                else
                {
                    // 切换到模拟模式的简单确认
                    var result = MessageBox.Show(
                        $"确认要从【{currentMode}】切换到【{targetMode}】吗？\n\n" +
                        "切换到模拟模式后，所有交易操作仅做模拟，不会产生真实资金变动。",
                        "模式切换确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
                
                // 执行切换
                globalMode.ToggleMode();
                
                // 更新UI显示
                UpdateGlobalModeDisplay();
                
                // 记录日志
                _logger?.LogInformation($"🔄 用户手动切换全局模式: {currentMode} → {targetMode}");
                
                // 显示成功提示
                MessageBox.Show(
                    $"✅ 模式切换成功！\n\n当前模式：【{globalMode.ModeDisplayText}】\n\n{globalMode.ModeDescription}",
                    "切换成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 切换全局模式失败");
                MessageBox.Show(
                    $"❌ 模式切换失败：{ex.Message}",
                    "切换失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 初始化全局模式管理
        /// </summary>
        private void InitializeGlobalMode()
        {
            try
            {
                var globalMode = GlobalModeManager.Instance;
                
                // 订阅模式变更事件
                globalMode.ModeChanged += OnGlobalModeChanged;
                
                // 初始化UI显示
                UpdateGlobalModeDisplay();
                
                _logger?.LogInformation($"🔧 全局模式管理器已初始化: {globalMode.ModeDisplayText}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 初始化全局模式管理器失败");
            }
        }
        
        /// <summary>
        /// 全局模式变更事件处理
        /// </summary>
        private void OnGlobalModeChanged(object? sender, ModeChangedEventArgs e)
        {
            try
            {
                // 在UI线程中更新显示
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    UpdateGlobalModeDisplay();
                });
                
                _logger?.LogInformation($"🔄 全局模式已变更: {(e.IsSimulationMode ? "模拟模式" : "实盘模式")}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 处理全局模式变更事件失败");
            }
        }
        
        /// <summary>
        /// 更新全局模式显示
        /// </summary>
        private void UpdateGlobalModeDisplay()
        {
            try
            {
                var globalMode = GlobalModeManager.Instance;
                
                IsGlobalSimulationMode = globalMode.IsSimulationMode;
                GlobalModeDescription = globalMode.ModeDescription;
                
                // 更新状态消息
                var modeText = globalMode.IsSimulationMode ? "模拟模式" : "实盘模式";
                StatusMessage = $"就绪 - {modeText}";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 更新全局模式显示失败");
            }
        }
        
        #endregion
    }
} 