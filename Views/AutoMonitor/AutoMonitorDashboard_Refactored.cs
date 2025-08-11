using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using BinanceFuturesTrader.Views.AutoMonitor.Controllers;
using BinanceFuturesTrader.Views.AutoMonitor.Components;
using BinanceFuturesTrader.Views.AutoMonitor.Services;
using BinanceFuturesTrader.Views.AutoMonitor.Commands;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.ViewModels;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace BinanceFuturesTrader.Views.AutoMonitor
{
    /// <summary>
    /// 重构后的自动盯盘主窗口
    /// 使用模块化架构，职责清晰
    /// </summary>
    public class AutoMonitorDashboard_Refactored : Window, INotifyPropertyChanged
    {
        #region 私有字段
        
        private readonly BinanceFuturesTrader.Services.AutoMonitorService _autoMonitorService;
        private readonly ILogger _logger;
        private readonly MainViewModel _mainViewModel;
        
        // 核心模块
        private readonly AutoMonitorDataModel _dataModel;
        private readonly UIStateModel _uiStateModel;
        private readonly AutoMonitorController _controller;
        private readonly UIComponentManager _uiManager;
        
        private readonly AsyncAutoMonitorController _asyncController;
        
        #endregion
        
        #region 构造函数
        
        public AutoMonitorDashboard_Refactored(
            BinanceFuturesTrader.Services.AutoMonitorService autoMonitorService,
            ILogger logger,
            MainViewModel mainViewModel = null)
        {
            _autoMonitorService = autoMonitorService ?? throw new ArgumentNullException(nameof(autoMonitorService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mainViewModel = mainViewModel;
            
            try
            {
                _logger.LogInformation("开始初始化自动盯盘界面");
                
                // 创建数据模型
                _dataModel = new AutoMonitorDataModel();
                _uiStateModel = new UIStateModel();
                
                // 创建控制器
                _controller = new AutoMonitorController(_autoMonitorService, _logger, _dataModel, _uiStateModel, _mainViewModel);
                
                // 创建UI管理器
                _uiManager = new UIComponentManager(_dataModel, _uiStateModel, _logger);
                
                // 🔧 【多线程修复】使用异步控制器替代同步控制器
                _asyncController = new AsyncAutoMonitorController(
                    _logger,
                    _mainViewModel,
                    _autoMonitorService);
                
                _logger.LogInformation("🔧 自动监控面板已切换到多线程异步架构");
                
                // 初始化窗口
                InitializeWindow();
                
                // 设置事件处理
                SetupEventHandlers();
                
                // 初始化UI组件
                InitializeUIComponents();
                
                // 加载初始数据
                _ = LoadInitialDataAsync();
                
                _logger.LogInformation("自动盯盘界面初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化自动盯盘界面时发生异常");
                throw;
            }
        }
        
        #endregion
        
        #region 公共属性
        
        public AutoMonitorDataModel DataModel => _dataModel;
        public UIStateModel UIStateModel => _uiStateModel;
        public AutoMonitorController Controller => _controller;
        
        #endregion
        
        #region 命令属性
        
        public ICommand ToggleMonitoringCommand { get; private set; }
        public ICommand RefreshDataCommand { get; private set; }
        public ICommand ClearLogCommand { get; private set; }
        public ICommand ExportLogCommand { get; private set; }
        public ICommand LoadConfigCommand { get; private set; }
        public ICommand SaveConfigCommand { get; private set; }
        
        #endregion
        
        #region 初始化方法
        
        private void InitializeWindow()
        {
            try
            {
                // 设置窗口基本属性
                Title = "自动盯盘监控面板";
                Width = 1200;
                Height = 800;
                MinWidth = 800;
                MinHeight = 600;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                
                // 设置数据上下文
                DataContext = this;
                
                _logger.LogDebug("窗口初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化窗口时发生异常");
                throw;
            }
        }
        
        private void SetupEventHandlers()
        {
            try
            {
                // 设置控制器事件处理
                _controller.TimerController.DataRefreshRequested += OnDataRefreshRequested;
                _controller.TimerController.TitleUpdateRequested += OnTitleUpdateRequested;
                
                // 设置UI管理器事件处理
                _uiManager.ToggleMonitoringRequested += OnToggleMonitoringRequested;
                _uiManager.RefreshDataRequested += OnRefreshDataRequested;
                _uiManager.ClearLogRequested += OnClearLogRequested;
                _uiManager.ExportLogRequested += OnExportLogRequested;
                _uiManager.EditRequested += OnEditRequested;
                _uiManager.DeleteRequested += OnDeleteRequested;
                _uiManager.AutoScrollToggled += OnAutoScrollToggled;
                
                // 设置窗口事件处理
                this.Closing += OnWindowClosing;
                this.Loaded += OnWindowLoaded;
                
                // 🎯 新增：监听主视图模型的配置同步事件
                if (_mainViewModel != null)
                {
                    _mainViewModel.ConfigurationSyncRequested += OnConfigurationSyncRequested;
                    _logger.LogDebug("已订阅主视图模型的配置同步事件");
                }
                
                // 创建命令
                CreateCommands();
                
                _logger.LogDebug("事件处理器设置完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置事件处理器时发生异常");
                throw;
            }
        }
        
        private void CreateCommands()
        {
            ToggleMonitoringCommand = new RelayCommand(async () => await _controller.ToggleMonitoringAsync());
            RefreshDataCommand = new RelayCommand(async () => await _controller.RefreshDataAsync());
            ClearLogCommand = new RelayCommand(() => _controller.LoggingService.ClearRealTimeLog());
            ExportLogCommand = new RelayCommand(async () => await ExportLogAsync());
            LoadConfigCommand = new RelayCommand(async () => await _controller.LoadConfigurationAsync());
            SaveConfigCommand = new RelayCommand(async () => await _controller.SaveConfigurationAsync());
        }
        
        private void InitializeUIComponents()
        {
            try
            {
                // 初始化UI组件管理器
                _uiManager.InitializeComponents(this);
                
                // 创建主布局
                var mainLayout = _uiManager.CreateMainLayout();
                Content = mainLayout;
                
                // 应用默认主题
                _uiManager.ApplyTheme("Default");
                
                _logger.LogDebug("UI组件初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化UI组件时发生异常");
                throw;
            }
        }
        
        private async System.Threading.Tasks.Task LoadInitialDataAsync()
        {
            try
            {
                _logger.LogInformation("🔄 开始加载初始数据...");
                
                // 加载配置
                await _controller.LoadConfigurationAsync();
                
                // 🔧 修复：从持仓加载合约配置
                _logger.LogInformation("📊 正在从持仓加载合约配置...");
                var loadResult = await _controller.LoadContractConfigurationsFromPositionsAsync();
                if (loadResult)
                {
                    _logger.LogInformation($"✅ 成功加载 {_dataModel.ContractMonitors.Count} 个合约配置");
                    await _controller.LoggingService.LogOperationAsync($"从持仓加载了 {_dataModel.ContractMonitors.Count} 个合约配置");
                }
                else
                {
                    _logger.LogWarning("⚠️ 未找到有效持仓，无合约配置加载");
                    await _controller.LoggingService.LogOperationAsync("当前无有效持仓，未加载合约配置");
                }
                
                // 刷新数据
                await _controller.RefreshDataAsync();
                
                // 更新UI显示
                _uiManager.UpdateAllComponents();
                
                // 记录日志
                await _controller.LoggingService.LogOperationAsync("✅ 界面初始化完成，数据加载成功");
                
                _logger.LogInformation("✅ 初始数据加载完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 加载初始数据时发生异常");
                await _controller.LoggingService.LogErrorAsync("❌ 初始数据加载失败", ex);
            }
        }
        
        #endregion
        
        #region 事件处理方法
        
        private async void OnDataRefreshRequested(object sender, EventArgs e)
        {
            try
            {
                await _controller.RefreshDataAsync();
                _uiManager.UpdateAllComponents();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新数据时发生异常");
            }
        }
        
        private void OnTitleUpdateRequested(object sender, TitleUpdateEventArgs e)
        {
            try
            {
                // 更新窗口标题
                var baseTitle = "自动盯盘监控面板";
                var status = _dataModel.MonitorStatus;
                var time = e.Time;
                
                Title = $"{baseTitle} - {status} - {time}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新标题时发生异常");
            }
        }
        
        /// <summary>
        /// 🔧 【多线程修复】异步启动监控，避免UI线程阻塞
        /// </summary>
        private async void OnToggleMonitoringRequested(object sender, EventArgs e)
        {
            try
            {
                // 简化状态检查 - 直接通过控制器获取状态
                var isCurrentlyMonitoring = _asyncController != null && _controller.UIStateModel != null;
                
                if (isCurrentlyMonitoring)
                {
                    // 异步停止监控
                    var stopped = await _asyncController.StopMonitoringAsync();
                    if (stopped)
                    {
                        _logger.LogInformation("⏹ 监控已停止");
                    }
                }
                else
                {
                    // 异步启动监控
                    _logger.LogInformation("🚀 正在启动监控...");
                    
                    var started = await _asyncController.StartMonitoringAsync();
                    if (started)
                    {
                        _logger.LogInformation("🚀 监控已启动");
                    }
                    else
                    {
                        _logger.LogWarning("❌ 监控启动失败");
                    }
                }
                
                // 订阅异步控制器的事件来更新UI
                if (_asyncController != null)
                {
                    _asyncController.OnLogMessage += (message) =>
                    {
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            _logger.LogInformation(message);
                        });
                    };
                    
                    _asyncController.OnMonitoringStateChanged += (isMonitoring) =>
                    {
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            _logger.LogInformation($"监控状态变更: {(isMonitoring ? "运行中" : "已停止")}");
                        });
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "切换监控状态失败");
            }
        }
        
        private async void OnRefreshDataRequested(object sender, EventArgs e)
        {
            try
            {
                await _controller.RefreshDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新数据时发生异常");
            }
        }
        
        private void OnClearLogRequested(object sender, EventArgs e)
        {
            try
            {
                _controller.LoggingService.ClearRealTimeLog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清空日志时发生异常");
            }
        }
        
        private async void OnExportLogRequested(object sender, EventArgs e)
        {
            try
            {
                await ExportLogAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出日志时发生异常");
            }
        }
        
        private void OnEditRequested(object sender, ContractEditEventArgs e)
        {
            try
            {
                // 暂时简化处理
                _logger.LogInformation($"编辑合约: {e.Contract?.Symbol}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "编辑合约时发生异常");
            }
        }
        
        private void OnDeleteRequested(object sender, ContractDeleteEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    $"确定要删除合约 {e.Contract?.Symbol} 吗？",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // 🔧 修复：确保在UI线程中执行集合操作
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _dataModel.ContractMonitors.Remove(e.Contract);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除合约时发生异常");
            }
        }
        
        private void OnAutoScrollToggled(object sender, AutoScrollEventArgs e)
        {
            try
            {
                _dataModel.AutoScroll = e.IsEnabled;
                _uiStateModel.UpdateAutoScrollButtonColor(e.IsEnabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "切换自动滚动时发生异常");
            }
        }
        
        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 窗口加载完成后的处理
                await _controller.LoggingService.LogOperationAsync("窗口加载完成");
                
                // 启动定时器
                _controller.TimerController.StartRefreshTimer();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "窗口加载完成处理时发生异常");
            }
        }
        
        private async void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 如果监控正在运行，询问是否确定关闭
                if (_dataModel.MonitorStatus == "运行中")
                {
                    var result = MessageBox.Show(
                        "监控正在运行，确定要关闭吗？",
                        "确认关闭",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                    
                    // 停止监控
                    await _controller.StopMonitoringAsync();
                }
                
                // 保存配置
                await _controller.SaveConfigurationAsync();
                
                // 🎯 新增：取消事件订阅
                if (_mainViewModel != null)
                {
                    _mainViewModel.ConfigurationSyncRequested -= OnConfigurationSyncRequested;
                    _logger.LogDebug("已取消主视图模型的配置同步事件订阅");
                }
                
                // 清理资源
                _controller.Dispose();
                _uiManager.Cleanup();
                
                await _controller.LoggingService.LogOperationAsync("窗口关闭");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "关闭窗口时发生异常");
            }
        }

        /// <summary>
        /// 🎯 新增：处理配置同步事件
        /// </summary>
        private async void OnConfigurationSyncRequested(object sender, ViewModels.ConfigurationSyncEventArgs e)
        {
            try
            {
                _logger.LogInformation($"🔄 接收到配置同步请求：推仓{e.AddPositionTierCount}阶梯，止盈{e.ProfitProtectionTierCount}阶梯");
                
                // 通过UI组件管理器处理基础配置变化
                _uiManager.HandleBaseConfigurationChange(e.AddPositionTierCount, e.ProfitProtectionTierCount);
                
                // 刷新数据显示
                await _controller.RefreshDataAsync();
                
                _logger.LogInformation("✅ 配置同步处理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 处理配置同步时发生异常");
            }
        }
        
        #endregion
        
        #region 辅助方法
        
        private async System.Threading.Tasks.Task ExportLogAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "日志文件 (*.log)|*.log|所有文件 (*.*)|*.*",
                    DefaultExt = "log",
                    FileName = $"AutoMonitor_Export_{DateTime.Now:yyyyMMdd_HHmmss}.log"
                };
                
                if (dialog.ShowDialog() == true)
                {
                    var directory = System.IO.Path.GetDirectoryName(dialog.FileName);
                    await _controller.LoggingService.ExportLogsAsync(directory);
                    
                    MessageBox.Show("日志导出成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出日志时发生异常");
                MessageBox.Show($"导出日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        #endregion
        
        #region INotifyPropertyChanged 实现
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
        
        #region 公开方法
        
        /// <summary>
        /// 获取AutoMonitorController实例
        /// </summary>
        public AutoMonitorController GetController()
        {
            return _controller;
        }
        
        /// <summary>
        /// 获取数据模型
        /// </summary>
        public AutoMonitorDataModel GetDataModel()
        {
            return _dataModel;
        }
        
        /// <summary>
        /// 获取UI状态模型
        /// </summary>
        public UIStateModel GetUIStateModel()
        {
            return _uiStateModel;
        }
        
        /// <summary>
        /// 刷新面板数据
        /// </summary>
        public async Task RefreshPanelDataAsync()
        {
            try
            {
                await _controller.RefreshDataAsync();
                _uiManager.UpdateAllComponents();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新面板数据失败");
            }
        }
        
        /// <summary>
        /// 启动集成监控（包含持仓变化监听）
        /// </summary>
        public async Task<bool> StartIntegratedMonitoringAsync()
        {
            try
            {
                var result = await _controller.StartMonitoringWithPositionSyncAsync();
                if (result)
                {
                    _logger.LogInformation("✅ 集成监控启动成功");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 启动集成监控失败");
                return false;
            }
        }
        
        /// <summary>
        /// 停止集成监控
        /// </summary>
        public async Task<bool> StopIntegratedMonitoringAsync()
        {
            try
            {
                var result = await _controller.StopMonitoringWithPositionSyncAsync();
                if (result)
                {
                    _logger.LogInformation("✅ 集成监控停止成功");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 停止集成监控失败");
                return false;
            }
        }
        
        #endregion
        
        #region 生命周期管理
        
        #endregion
    }
} 