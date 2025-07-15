using System;
using System.Threading.Tasks;
using System.Windows;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using BinanceFuturesTrader.Views.AutoMonitor.Controllers;
using BinanceFuturesTrader.Views.AutoMonitor.Components;
using BinanceFuturesTrader.Views.AutoMonitor.Services;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views.AutoMonitor
{
    /// <summary>
    /// 快速开始示例
    /// 展示如何使用新的模块化架构
    /// </summary>
    public partial class QuickStartExample : Window
    {
        // 核心模块
        private readonly AutoMonitorDataModel _dataModel;
        private readonly UIStateModel _uiStateModel;
        private readonly AutoMonitorController _controller;
        private readonly UIComponentManager _uiManager;
        
        public QuickStartExample()
        {
            // 第1步：创建基础依赖
            var logger = CreateLogger();
            var autoMonitorService = CreateAutoMonitorService();
            
            // 第2步：创建数据模型
            _dataModel = new AutoMonitorDataModel();
            _uiStateModel = new UIStateModel();
            
            // 第3步：创建控制器
            _controller = new AutoMonitorController(
                autoMonitorService, 
                logger, 
                _dataModel, 
                _uiStateModel);
            
            // 第4步：创建UI管理器
            _uiManager = new UIComponentManager(
                _dataModel, 
                _uiStateModel, 
                logger);
            
            // 第5步：初始化窗口
            InitializeWindow();
        }
        
        /// <summary>
        /// 初始化窗口
        /// </summary>
        private void InitializeWindow()
        {
            // 设置窗口属性
            Title = "自动盯盘 - 快速开始示例";
            Width = 1000;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            
            // 设置数据绑定
            DataContext = _dataModel;
            
            // 初始化UI组件
            _uiManager.InitializeComponents(this);
            
            // 创建主布局
            Content = _uiManager.CreateMainLayout();
            
            // 设置事件处理
            SetupEventHandlers();
            
            // 加载初始数据
            _ = LoadInitialDataAsync();
        }
        
        /// <summary>
        /// 设置事件处理
        /// </summary>
        private void SetupEventHandlers()
        {
            // 控制器事件
            _controller.TimerController.DataRefreshRequested += async (s, e) =>
            {
                await _controller.RefreshDataAsync();
                _uiManager.UpdateAllComponents();
            };
            
            // UI管理器事件
            _uiManager.ToggleMonitoringRequested += async (s, e) =>
            {
                await _controller.ToggleMonitoringAsync();
            };
            
            _uiManager.RefreshDataRequested += async (s, e) =>
            {
                await _controller.RefreshDataAsync();
            };
            
            _uiManager.ClearLogRequested += (s, e) =>
            {
                _controller.LoggingService.ClearRealTimeLog();
            };
        }
        
        /// <summary>
        /// 加载初始数据
        /// </summary>
        private async Task LoadInitialDataAsync()
        {
            try
            {
                // 记录启动日志
                await _controller.LoggingService.LogOperationAsync("系统启动");
                
                // 加载配置
                await _controller.LoadConfigurationAsync();
                
                // 刷新数据
                await _controller.RefreshDataAsync();
                
                // 启动定时器
                _controller.TimerController.StartRefreshTimer();
                
                // 记录完成日志
                await _controller.LoggingService.LogOperationAsync("初始化完成");
            }
            catch (Exception ex)
            {
                await _controller.LoggingService.LogErrorAsync("初始化失败", ex);
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 创建日志记录器 (示例)
        /// </summary>
        private ILogger CreateLogger()
        {
            // 这里使用简单的控制台日志记录器作为示例
            // 在实际项目中，应该使用你的日志记录器配置
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            });
            
            return loggerFactory.CreateLogger<QuickStartExample>();
        }
        
        /// <summary>
        /// 创建自动监控服务 (示例)
        /// </summary>
        private BinanceFuturesTrader.Services.AutoMonitorService CreateAutoMonitorService()
        {
            // 这里需要根据你的实际情况创建AutoMonitorService
            // 这只是一个示例，返回null作为占位符
            // 在实际使用时需要传入真实的服务实例
            return null; // 临时返回null，实际使用时需要创建真实实例
        }
        
        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 停止监控
                if (_dataModel.MonitorStatus == "运行中")
                {
                    _ = _controller.StopMonitoringAsync();
                }
                
                // 清理资源
                _controller?.Dispose();
                _uiManager?.Cleanup();
            }
            catch (Exception ex)
            {
                // 记录清理异常，但不影响关闭
                Console.WriteLine($"清理资源时发生异常: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}

// ========================================
// 使用方法示例
// ========================================

/*
// 1. 在你的主窗口或应用程序中创建实例
var quickStartExample = new QuickStartExample();
quickStartExample.Show();

// 2. 或者作为对话框显示
quickStartExample.ShowDialog();

// 3. 或者集成到现有窗口中
var quickStart = new QuickStartExample();
var content = quickStart.Content;
// 将content添加到你的现有窗口中
*/

// ========================================
// 高级使用示例
// ========================================

namespace BinanceFuturesTrader.Views.AutoMonitor.Examples
{
    /// <summary>
    /// 高级使用示例
    /// 展示如何自定义和扩展模块化架构
    /// </summary>
    public class AdvancedUsageExample
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly UIStateModel _uiStateModel;
        private readonly AutoMonitorController _controller;
        private readonly UIComponentManager _uiManager;
        
        /// <summary>
        /// 示例1：自定义数据模型
        /// </summary>
        public void CustomizeDataModel()
        {
            // 添加自定义数据
            _dataModel.ConfigName = "我的自定义配置";
            _dataModel.MonitorStatus = "自定义状态";
            
            // 监听数据变化
            _dataModel.PropertyChanged += (s, e) =>
            {
                Console.WriteLine($"数据属性 {e.PropertyName} 已更改");
            };
            
            // 重置数据
            _dataModel.ClearAllData();
            _dataModel.ResetStatistics();
        }
        
        /// <summary>
        /// 示例2：自定义UI状态
        /// </summary>
        public void CustomizeUIState()
        {
            // 设置不同的状态
            _uiStateModel.SetMonitoringState();
            _uiStateModel.SetStoppedState();
            _uiStateModel.SetErrorState();
            _uiStateModel.SetLoadingState();
            
            // 应用主题
            // _uiManager.ApplyTheme("Dark");
        }
        
        /// <summary>
        /// 示例3：自定义业务逻辑
        /// </summary>
        public async Task CustomizeBusinessLogic()
        {
            // 启动监控
            var success = await _controller.StartMonitoringAsync();
            if (success)
            {
                Console.WriteLine("监控启动成功");
                
                // 刷新数据
                await _controller.RefreshDataAsync();
                
                // 记录日志
                await _controller.LoggingService.LogOperationAsync("自定义操作完成");
            }
            
            // 停止监控
            await _controller.StopMonitoringAsync();
        }
        
        /// <summary>
        /// 示例4：自定义定时器
        /// </summary>
        public void CustomizeTimers()
        {
            // 设置自定义刷新间隔
            _controller.TimerController.SetRefreshInterval(10); // 10秒
            
            // 启动指定的定时器
            _controller.TimerController.StartRefreshTimer();
            
            // 监听定时器事件
            _controller.TimerController.DataRefreshRequested += async (s, e) =>
            {
                Console.WriteLine("定时器触发数据刷新");
                await _controller.RefreshDataAsync();
            };
        }
        
        /// <summary>
        /// 示例5：自定义日志
        /// </summary>
        public async Task CustomizeLogging()
        {
            // 添加不同类型的日志
            await _controller.LoggingService.LogOperationAsync("用户操作");
            await _controller.LoggingService.LogWarningAsync("警告信息");
            await _controller.LoggingService.LogErrorAsync("错误信息");
            
            // 清理过期日志
            await _controller.LoggingService.CleanupOldLogsAsync(30);
            
            // 导出日志
            await _controller.LoggingService.ExportLogsAsync(@"C:\Logs\Export");
        }
        
        /// <summary>
        /// 示例6：扩展功能
        /// </summary>
        public void ExtendFunctionality()
        {
            // 🔧 修复：在UI线程中安全添加数据
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // 添加自定义合约数据
                _dataModel.ContractMonitors.Add(new ContractMonitorModel
                {
                    Symbol = "BTCUSDT",
                    IsEnabled = true,
                    IsActive = true
                    // 其他属性...
                });
                
                // 添加自定义工作日志
                _dataModel.WorkLogs.Add(new WorkLog
                {
                    Timestamp = DateTime.Now,
                    Level = "Info",
                    Message = "自定义日志消息",
                    Category = "用户操作"
                });
            });
            
            // 触发UI更新
            _uiManager.UpdateAllComponents();
        }
    }
} 