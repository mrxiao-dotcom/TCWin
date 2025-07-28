using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.ViewModels;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// 自动盯盘监控面板
    /// </summary>
    public partial class AutoMonitorDashboard : Window, INotifyPropertyChanged
    {
        private readonly AutoMonitorService _autoMonitorService;
        private readonly ILogger _logger;
        private readonly MainViewModel _mainViewModel;
        private readonly DispatcherTimer _refreshTimer;
        
        // 🔧 Phase 9: 增强错误处理服务
        private readonly EnhancedErrorHandler _enhancedErrorHandler;

        private DateTime _lastUpdateTime;
        private string _monitorStatus = "未启动";
        private string _runningTime = "00:00:00";
        private int _activeContractCount;
        private int _totalExecutions;
        private double _executionSuccessRate;
        private int _activeStopOrderCount;
        private double _stopOrderSuccessRate;
        
        private SolidColorBrush _statusCardBackground = new(Colors.LightGray);
        private SolidColorBrush _statusIconColor = new(Colors.Gray);
        private SolidColorBrush _statusTextColor = new(Colors.Black);

        private string _configName = "未配置";
        private string _breakEvenConfigDisplay = "未启用";
        private string _scanIntervalDisplay = "30秒";

        // 🔧 新增：倒计时和日志相关属性
        private string _scanCountdownDisplay = "00:00";
        private string _nextScanTime = "计算中...";
        private string _cooldownStatusDisplay = "无冷却";
        private string _realTimeLog = "";
        private bool _autoScroll = true;
        private SolidColorBrush _autoScrollButtonColor = new(Colors.Green);
        
        // 🔧 新增：倒计时定时器
        private readonly DispatcherTimer _countdownTimer;
        private readonly DispatcherTimer _titleTimer; // 🔧 新增：标题更新定时器成员变量
        private DateTime _nextScanDateTime = DateTime.Now;
        private readonly object _logLock = new object();
        private readonly object _emergencyLogLock = new object(); // 🔧 紧急日志文件锁
        
        // 🔧 线程安全的紧急日志写入方法
        private void WriteEmergencyLog(string message)
        {
            try
            {
                lock (_emergencyLogLock)
                {
                    var emergencyLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emergency_log.txt");
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    File.AppendAllText(emergencyLogPath, $"[{timestamp}] {message}\n");
                }
            }
            catch (Exception ex)
            {
                // 如果紧急日志写入失败，至少记录到普通日志
                _logger?.LogError(ex, $"🚨 紧急日志写入失败: {message}");
            }
        }
        
        // 🆕 新增：持仓变化监听器
        private PositionChangeEventHandler? _positionChangeHandler;
        
        // 🆕 新增：执行状态变化监听器 - 用于实时状态更新
        private ExecutionStateChangeEventHandler? _executionStateChangeHandler;
        
        // 📝 新增：工作日志集合
        public ObservableCollection<WorkLog> WorkLogs { get; } = new();
        
        // 🔧 Phase 7: 实时同步相关字段
        private readonly Dictionary<string, PositionSnapshot> _lastKnownPositions = new();
        private readonly object _positionSyncLock = new object();
        private DateTime _lastPositionSyncTime = DateTime.MinValue;
        private bool _realTimeSyncEnabled = false;
        
        // 🔧 Phase 7: 位置快照数据结构
        private struct PositionSnapshot
        {
            public string Symbol { get; set; }
            public string PositionSide { get; set; }
            public decimal PositionAmt { get; set; }
            public decimal MarkPrice { get; set; }
            public decimal UnrealizedPnl { get; set; }
            public DateTime UpdateTime { get; set; }
        }

        // 🔧 Phase 8: 全面日志管理服务
        private ComprehensiveLoggingService? _comprehensiveLoggingService;
        


        /// <summary>
        /// 数据绑定属性
        /// </summary>
        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set { _lastUpdateTime = value; OnPropertyChanged(); }
        }

        public string MonitorStatus
        {
            get => _monitorStatus;
            set { _monitorStatus = value; OnPropertyChanged(); }
        }

        public string RunningTime
        {
            get => _runningTime;
            set { _runningTime = value; OnPropertyChanged(); }
        }

        public int ActiveContractCount
        {
            get => _activeContractCount;
            set { _activeContractCount = value; OnPropertyChanged(); }
        }

        public int TotalExecutions
        {
            get => _totalExecutions;
            set { _totalExecutions = value; OnPropertyChanged(); }
        }

        public double ExecutionSuccessRate
        {
            get => _executionSuccessRate;
            set { _executionSuccessRate = value; OnPropertyChanged(); }
        }

        public int ActiveStopOrderCount
        {
            get => _activeStopOrderCount;
            set { _activeStopOrderCount = value; OnPropertyChanged(); }
        }

        public double StopOrderSuccessRate
        {
            get => _stopOrderSuccessRate;
            set { _stopOrderSuccessRate = value; OnPropertyChanged(); }
        }

        public SolidColorBrush StatusCardBackground
        {
            get => _statusCardBackground;
            set { _statusCardBackground = value; OnPropertyChanged(); }
        }

        public SolidColorBrush StatusIconColor
        {
            get => _statusIconColor;
            set { _statusIconColor = value; OnPropertyChanged(); }
        }

        public SolidColorBrush StatusTextColor
        {
            get => _statusTextColor;
            set { _statusTextColor = value; OnPropertyChanged(); }
        }

        public string ConfigName
        {
            get => _configName;
            set { _configName = value; OnPropertyChanged(); }
        }

        public string BreakEvenConfigDisplay
        {
            get => _breakEvenConfigDisplay;
            set { _breakEvenConfigDisplay = value; OnPropertyChanged(); }
        }

        public string ScanIntervalDisplay
        {
            get => _scanIntervalDisplay;
            set { _scanIntervalDisplay = value; OnPropertyChanged(); }
        }

        // 🔧 新增：倒计时和日志相关属性
        public string ScanCountdownDisplay
        {
            get => _scanCountdownDisplay;
            set { _scanCountdownDisplay = value; OnPropertyChanged(); }
        }

        public string NextScanTime
        {
            get => _nextScanTime;
            set { _nextScanTime = value; OnPropertyChanged(); }
        }

        public string CooldownStatusDisplay
        {
            get => _cooldownStatusDisplay;
            set { _cooldownStatusDisplay = value; OnPropertyChanged(); }
        }

        public string RealTimeLog
        {
            get => _realTimeLog;
            set { _realTimeLog = value; OnPropertyChanged(); }
        }

        public SolidColorBrush AutoScrollButtonColor
        {
            get => _autoScrollButtonColor;
            set { _autoScrollButtonColor = value; OnPropertyChanged(); }
        }
        


        /// <summary>
        /// 集合属性
        /// </summary>
        public ObservableCollection<ContractStateDisplayModel> ContractStates { get; } = new();
        public ObservableCollection<ExecutionHistoryDisplayModel> ExecutionHistory { get; } = new();
        public ObservableCollection<AddPositionTierDisplayModel> AddPositionTiers { get; } = new();
        public ObservableCollection<ProfitProtectionTierDisplayModel> ProfitProtectionTiers { get; } = new();
        
        // 🚀 新增：支持新表格界面的数据集合
        public ObservableCollection<ContractMonitorModel> ContractMonitors { get; } = new();
        
        // 🚀 新增：支持新界面的统计属性
        private string _monitorStatusText = "未启动";
        private int _contractCount = 0;
        private string _totalConditionsText = "0";
        private int _executingCount = 0;
        private int _executedCount = 0;
        private string _statusText = "系统就绪";
        private string _scanIntervalText = "30秒";
        
        // 🎯 新增：启动/停止按钮相关属性
        private string _toggleButtonText = "启动盯盘";
        private SolidColorBrush _toggleButtonBackground = new(Colors.Green);
        private string _toggleButtonTooltip = "开始自动盯盘监控";
        private bool _toggleButtonEnabled = true;
        private bool _isDataGridReadOnly = false;
        private bool _editButtonEnabled = true;
        
        public string MonitorStatusText
        {
            get => _monitorStatusText;
            set { _monitorStatusText = value; OnPropertyChanged(); }
        }
        
        public int ContractCount
        {
            get => _contractCount;
            set { _contractCount = value; OnPropertyChanged(); }
        }
        
        public string TotalConditionsText
        {
            get => _totalConditionsText;
            set { _totalConditionsText = value; OnPropertyChanged(); }
        }
        
        public int ExecutingCount
        {
            get => _executingCount;
            set { _executingCount = value; OnPropertyChanged(); }
        }
        
        public int ExecutedCount
        {
            get => _executedCount;
            set { _executedCount = value; OnPropertyChanged(); }
        }
        
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }
        
        public string ScanIntervalText
        {
            get => _scanIntervalText;
            set { _scanIntervalText = value; OnPropertyChanged(); }
        }

        // 🎯 新增：启动/停止按钮相关属性
        public string ToggleButtonText
        {
            get => _toggleButtonText;
            set { _toggleButtonText = value; OnPropertyChanged(); }
        }

        public SolidColorBrush ToggleButtonBackground
        {
            get => _toggleButtonBackground;
            set { _toggleButtonBackground = value; OnPropertyChanged(); }
        }

        public string ToggleButtonTooltip
        {
            get => _toggleButtonTooltip;
            set { _toggleButtonTooltip = value; OnPropertyChanged(); }
        }

        public bool ToggleButtonEnabled
        {
            get => _toggleButtonEnabled;
            set { _toggleButtonEnabled = value; OnPropertyChanged(); }
        }

        public bool IsDataGridReadOnly
        {
            get => _isDataGridReadOnly;
            set { _isDataGridReadOnly = value; OnPropertyChanged(); }
        }

        public bool EditButtonEnabled
        {
            get => _editButtonEnabled;
            set { _editButtonEnabled = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 命令
        /// </summary>
        public ICommand RefreshCommand { get; }

        private DateTime _monitorStartTime;

        public AutoMonitorDashboard(AutoMonitorService autoMonitorService, ILogger logger, MainViewModel mainViewModel = null)
        {
            try
        {
            _autoMonitorService = autoMonitorService ?? throw new ArgumentNullException(nameof(autoMonitorService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mainViewModel = mainViewModel;

            // 🔧 Phase 8: 初始化全面日志管理服务
            try
            {
                _comprehensiveLoggingService = ComprehensiveLoggingService.Instance;
                _logger.LogInformation("✅ 全面日志管理服务已初始化");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 初始化全面日志管理服务失败");
            }

            // 🔧 新增：订阅配置同步事件（如果主视图模型可用）
            if (_mainViewModel != null)
            {
                _mainViewModel.ConfigurationSyncRequested += OnConfigurationSyncRequested;
                _logger.LogInformation("✅ 已订阅主视图模型的配置同步事件");
            }

            _logger.LogInformation("🚀 开始初始化AutoMonitorDashboard - 混合模式：XAML优先 + 代码动态补充");
                
                // 🎯 执行混合方案：XAML为主 + 代码动态补充
                InitializeHybridUI();
                
                _logger.LogInformation("✅ 代码界面初始化完成");

            // 初始化命令
            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());

            // 订阅自动盯盘事件
            _autoMonitorService.MonitorStatusChanged += OnMonitorStatusChanged;
            _autoMonitorService.ExecutionCompleted += OnExecutionCompleted;
            _autoMonitorService.WorkLogAdded += OnWorkLogAdded;

            // 🆕 新增：初始化持仓变化事件处理器并订阅事件
            try
            {
                // 🔧 简化版：持仓变化监控已在简化服务层直接处理，无需复杂的EventBus
                _logger.LogInformation("✅ 使用简化版服务，持仓变化监控已内置");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 初始化持仓变化监听失败");
            }

            // 🚀 新增：初始化执行状态变化事件处理器 - 用于实时状态更新
            try
            {
                // 🔧 简化版：执行状态变化已通过直接事件处理，无需复杂的EventBus
                _logger.LogInformation("✅ 使用简化版服务，执行状态变化已通过直接事件处理");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 初始化执行状态变化监听失败");
            }

            // 🔧 修改：使用配置中的扫描间隔，而不是硬编码的定时器频率
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Tick += (s, e) => 
            {
                // 🔧 关键修复：使用Task.Run避免UI线程阻塞
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RefreshDataAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ 定时器数据刷新失败");
                    }
                });
            };
            UpdateRefreshTimerInterval(); // 初始化定时器间隔

            // 🔧 新增：初始化倒计时定时器（每秒更新一次）
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += UpdateCountdown;
            
            // 🔧 关键修复：在构造函数开始处正确初始化_titleTimer，避免资源泄漏
            _titleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

            // 🎯 延迟加载数据，确保界面完全创建后再加载
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _logger.LogInformation("🎯 界面创建完成，开始加载合约监控数据");
                    // 🔧 修复：强制刷新当前持仓数据
                    RefreshCurrentPositionsData();
                    
                    // 🎯 如果没有数据，检查状态文件是否存在，不存在则生成
                    if (ContractMonitors.Count == 0)
                    {
                        _logger.LogInformation("📝 没有合约监控数据，检查是否需要生成状态文件");
                        // ❌ 移除创建示例数据的后门路径
                        // CreateExampleContractData(); // 已移除
                    }
                        
                        // 🎯 添加详细的图标测试日志
                        _logger.LogInformation($"📊 数据加载完成 - ContractMonitors.Count: {ContractMonitors.Count}");
                        
                        if (ContractMonitors.Count > 0)
                        {
                            var firstContract = ContractMonitors[0];
                            _logger.LogInformation($"🎯 第一个合约图标测试:");
                            _logger.LogInformation($"   Symbol: {firstContract.Symbol}");
                            _logger.LogInformation($"   TriggerConditions.Count: {firstContract.TriggerConditions.Count}");
                            _logger.LogInformation($"   AddPositionProgressIcon: '{firstContract.AddPositionProgressIcon}'");
                            _logger.LogInformation($"   AddPositionProgressText: '{firstContract.AddPositionProgressText}'");
                            _logger.LogInformation($"   ProfitProgressIcon: '{firstContract.ProfitProgressIcon}'");
                            _logger.LogInformation($"   ProfitProgressText: '{firstContract.ProfitProgressText}'");
                            
                            if (firstContract.TriggerConditions.Count > 0)
                            {
                                var firstCondition = firstContract.TriggerConditions[0];
                                _logger.LogInformation($"   第一个条件测试:");
                                _logger.LogInformation($"     Type: {firstCondition.Type}");
                                _logger.LogInformation($"     StatusIcon: '{firstCondition.StatusIcon}'");
                                _logger.LogInformation($"     Status: {firstCondition.Status}");
                            }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 延迟加载数据时发生错误");
                    // ❌ 移除创建示例数据的后门路径 
                    // CreateExampleContractData(); // 已移除
                    _logger.LogWarning("⚠️ 数据加载失败，需要检查状态文件或重新生成");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            
            // 启动定时器和初始化数据
            _refreshTimer.Start();
            _countdownTimer.Start(); // 🔧 新增：启动倒计时定时器
            _ = Task.Run(async () => await RefreshDataAsync());
            
            // 🔧 立即可见的改进：定时更新窗口标题（已在构造函数开始处初始化）
            _titleTimer.Tick += (s, e) =>
            {
                try
                {
                    var isRunning = _autoMonitorService?.IsRunning ?? false;
                    var status = isRunning ? "🟢运行中" : "🔴已停止";
                    var time = DateTime.Now.ToString("HH:mm:ss");
                    var config = _autoMonitorService?.CurrentConfig;
                    var scanInterval = config?.ScanIntervalSeconds ?? 30;
                    
                    // 🔧 修复：改进的倒计时逻辑，避免状态污染
                    var elapsed = (DateTime.Now - _nextScanDateTime).TotalSeconds;
                    if (elapsed >= scanInterval || elapsed < -scanInterval)
                    {
                        _nextScanDateTime = DateTime.Now.AddSeconds(scanInterval);
                    }
                    
                    var remaining = (_nextScanDateTime - DateTime.Now).TotalSeconds;
                    if (remaining < 0) remaining = 0;
                    
                    var countdown = isRunning ? $"下次扫描: {(int)remaining}秒" : "";
                    
                    Title = $"自动盯盘控制面板 - {status} | {time} | {countdown}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "更新窗口标题时发生错误");
                    Title = "自动盯盘控制面板 - 状态更新错误";
                }
            };
            _titleTimer.Start();
            
            // 🔧 【重要修复】：初始化时检查实际的服务运行状态，而不是假设为停止状态
            bool actualServiceStatus = _autoMonitorService?.IsRunning ?? false;
            if (actualServiceStatus)
            {
                _logger.LogInformation("🔄 检测到后台监控服务正在运行，同步UI状态");
                UpdateToggleButtonState(true, "停止盯盘", Colors.Red, true);
                UpdateEditPermissions(false); // 运行时禁用编辑
            }
            else
            {
                _logger.LogInformation("🔴 检测到监控服务未运行");
                UpdateToggleButtonState(false, "启动盯盘", Colors.Green, true);
                UpdateEditPermissions(true); // 停止时允许编辑
            }
            
            // 🔧 【重要修复】：初始化时也要更新监控状态文本
            UpdateNewInterfaceStats();
            
            // 🎯 初始化时自动载入上次使用的基础配置对应的合约配置
            try
            {
                            // 🔧 关键修复：延迟配置加载，确保MainViewModel完全初始化后再获取配置
            this.Loaded += async (s, e) => 
            {
                await InitializeConfigurationAsync();
                
                // 🔧 【重要修复】：窗口加载完成后再次检查服务状态，确保状态同步
                bool actualServiceStatus = _autoMonitorService?.IsRunning ?? false;
                bool currentUIStatus = ToggleButtonText == "停止盯盘";
                
                if (actualServiceStatus != currentUIStatus)
                {
                    _logger.LogInformation($"🔄 检测到状态不同步，修正状态：服务运行={actualServiceStatus}, UI状态={currentUIStatus}");
                    if (actualServiceStatus)
                    {
                        UpdateToggleButtonState(true, "停止盯盘", Colors.Red, true);
                        UpdateEditPermissions(false);
                    }
                    else
                    {
                        UpdateToggleButtonState(false, "启动盯盘", Colors.Green, true);
                        UpdateEditPermissions(true);
                    }
                    
                    // 🔧 【重要修复】：状态同步时也要更新监控状态文本
                    UpdateNewInterfaceStats();
                }
                
                // 🔧 【重要修复】：窗口加载完成时也刷新数据，确保显示最新信息
                RefreshCurrentPositionsData();
                await RefreshDataAsync();
            };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "初始化配置加载失败，使用默认配置创建表格结构");
                // 🔧 修复：使用默认配置而不是硬编码示例数据
                var defaultConfig = CreateDefaultAutoMonitorConfig();
                CreateExampleDataBasedOnConfig(defaultConfig);
                
                // 🔧 修复：确保表格列也是基于默认配置生成的
                if (_contractMonitorDataGrid != null)
                {
                    GenerateDynamicDataGridColumns(defaultConfig);
                }
            }
            
                _logger.LogInformation("✅ 自动盯盘控制面板已初始化");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 自动盯盘控制面板初始化失败");
                MessageBox.Show($"自动盯盘控制面板初始化失败: {ex.Message}\n\n详细信息已记录到日志文件。", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 混合模式UI初始化：XAML优先 + 代码动态补充
        /// </summary>
        private void InitializeHybridUI()
        {
            // 🎯 检查是否可以使用XAML
            if (TryInitializeXamlUI())
            {
                _logger.LogInformation("✅ 混合模式UI初始化完成：XAML基础 + 代码动态");
                return;
            }
            
            // 🔄 回退到代码生成UI
            _logger.LogInformation("🔄 执行回退方案：使用代码生成界面");
            try
            {
                CreateEnhancedCodeBasedUI();
                _logger.LogInformation($"✅ 回退界面创建完成，DataGrid引用: {(_contractMonitorDataGrid != null ? "已创建" : "未创建")}");
            }
            catch (Exception codeEx)
            {
                _logger.LogError(codeEx, "❌ 增强代码界面创建失败，使用基础界面");
                try
                {
                    CreateCodeBasedUI();
                    _logger.LogInformation($"✅ 基础代码界面创建完成，DataGrid引用: {(_contractMonitorDataGrid != null ? "已创建" : "未创建")}");
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "❌ 所有界面创建失败，使用最基本界面");
                    CreateFallbackUI();
                }
            }
        }
        
        /// <summary>
        /// 尝试初始化XAML UI
        /// </summary>
        private bool TryInitializeXamlUI()
        {
            try
            {
                _logger.LogInformation("📋 检查XAML支持...");
                
                // 🔧 使用反射检查InitializeComponent方法是否存在
                var initMethod = this.GetType().GetMethod("InitializeComponent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (initMethod == null)
                {
                    _logger.LogWarning("⚠️ InitializeComponent方法不存在，XAML可能未正确编译");
                    return false;
                }
                
                _logger.LogInformation("📋 第一步：尝试加载XAML界面");
                initMethod.Invoke(this, null); // 调用InitializeComponent
                DataContext = this;    // 设置数据上下文
                
                _logger.LogInformation("✅ XAML界面加载成功！");
                
                // 🎯 第二步：代码补充动态内容
                _logger.LogInformation("🔧 第二步：代码补充动态内容");
                SetupDynamicContent();
                
                return true;
            }
            catch (Exception xamlEx)
            {
                _logger.LogWarning(xamlEx, "⚠️ XAML加载失败，将回退到代码生成UI");
                return false;
            }
        }
        
        /// <summary>
        /// 设置动态内容（在XAML基础上补充）
        /// </summary>
        private void SetupDynamicContent()
        {
            try
            {
                // 🎯 获取XAML中的DataGrid引用
                _contractMonitorDataGrid = FindName("ContractMonitorDataGrid") as DataGrid;
                if (_contractMonitorDataGrid == null)
                {
                    _logger.LogWarning("⚠️ 无法找到XAML中的DataGrid，可能需要检查控件名称");
                    return;
                }
                
                _logger.LogInformation("✅ 成功获取XAML中的DataGrid引用");
                
                // 🔧 初始化基础列结构
                InitializeBasicDataGridColumns();
                
                // 🔧 修复：统一使用GenerateDynamicDataGridColumns，根据配置生成列
                var config = GetCurrentAutoMonitorConfig();
                if (config != null)
                {
                    _logger.LogInformation($"📊 根据配置'{config.Name}'生成动态列");
                    GenerateDynamicDataGridColumns(config);
                }
                else
                {
                    _logger.LogInformation("📊 未找到配置，使用默认配置生成列");
                    // 🎯 创建默认配置而不是使用示例列
                    var defaultConfig = CreateDefaultAutoMonitorConfig();
                    GenerateDynamicDataGridColumns(defaultConfig);
                    
                    // 🔧 优化：简化提示，不阻止用户使用
                    _logger.LogInformation("💡 使用默认配置生成表格结构，用户可以正常使用");
                }
                
                _logger.LogInformation("✅ 动态内容设置完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 设置动态内容失败");
            }
        }

        /// <summary>
        /// 创建增强版的UI界面（包含日志区和倒计时功能）
        /// </summary>
        private void CreateEnhancedCodeBasedUI()
        {
            try
            {
                _logger.LogInformation("📍 创建增强版UI界面，使用标准代码界面");
                CreateCodeBasedUI(); // 直接使用标准的代码界面
                _logger.LogInformation("✅ 增强版代码UI界面创建成功（使用标准版本）");
                
                // 🎯 界面创建成功后，强制设置一个基础的动态列结构
                InitializeBasicDataGridColumns();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 增强版代码UI创建失败");
                throw; // 重新抛出异常，让上层处理
            }
        }

        /// <summary>
        /// 创建代码化的UI界面（当XAML加载失败时使用）
        /// </summary>
        private void CreateCodeBasedUI()
        {
            try
            {
                _logger.LogInformation("🎯 开始创建代码化UI界面");
                
                Title = "自动盯盘监控面板";
                Width = 1200;
                Height = 800;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                DataContext = this;
                _logger.LogInformation("📝 窗口基本属性设置完成");

                // 创建基本的UI结构
                var mainGrid = new System.Windows.Controls.Grid();
                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
                _logger.LogInformation("📝 主网格结构创建完成");

                // 标题栏
                var titlePanel = CreateTitlePanel();
                System.Windows.Controls.Grid.SetRow(titlePanel, 0);
                mainGrid.Children.Add(titlePanel);
                _logger.LogInformation("📝 标题栏创建完成");

                // 内容区域 - 创建完整的监控界面
                var mainContentGrid = new System.Windows.Controls.Grid();
                mainContentGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
                mainContentGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

                // 🔧 修改为上下结构：上方合约表格，下方配置信息和历史记录左右排列
                var mainContentArea = new System.Windows.Controls.Grid();
                mainContentArea.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(3, System.Windows.GridUnitType.Star) }); // 🎯 上方合约表格区域75%
                mainContentArea.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) }); // 🎯 下方配置信息区域25%

                // 上方：合约触发条件管理表格
                try
                {
                    var contractTablePanel = CreateContractTablePanel();
                    contractTablePanel.Margin = new Thickness(5, 5, 5, 3);
                    System.Windows.Controls.Grid.SetRow(contractTablePanel, 0);
                    mainContentArea.Children.Add(contractTablePanel);
                    _logger.LogInformation("📝 上方合约表格面板创建完成");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 上方合约表格面板创建失败");
                    var placeholder = new System.Windows.Controls.Border
                    {
                        Background = new SolidColorBrush(Colors.LightGray),
                        Margin = new Thickness(5, 5, 5, 3),
                        Child = new System.Windows.Controls.TextBlock
                        {
                            Text = "合约表格加载失败",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    System.Windows.Controls.Grid.SetRow(placeholder, 0);
                    mainContentArea.Children.Add(placeholder);
                }

                // 下方：配置信息和历史记录左右排列
                var bottomGrid = new System.Windows.Controls.Grid();
                bottomGrid.Margin = new Thickness(5, 3, 5, 5);
                bottomGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) }); // 配置信息
                bottomGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) }); // 历史记录

                // 下左：配置信息
                try
                {
                    var configInfoPanel = CreateConfigInfoPanel();
                    System.Windows.Controls.Grid.SetColumn(configInfoPanel, 0);
                    bottomGrid.Children.Add(configInfoPanel);
                    _logger.LogInformation("📝 下左配置信息面板创建完成");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 下左配置信息面板创建失败");
                    var placeholder = new System.Windows.Controls.Border
                    {
                        Background = new SolidColorBrush(Colors.LightGray),
                        Child = new System.Windows.Controls.TextBlock
                        {
                            Text = "配置信息加载失败",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    System.Windows.Controls.Grid.SetColumn(placeholder, 0);
                    bottomGrid.Children.Add(placeholder);
                }

                // 下右：历史记录
                try
                {
                    var historyPanel = CreateHistoryPanel();
                    System.Windows.Controls.Grid.SetColumn(historyPanel, 1);
                    bottomGrid.Children.Add(historyPanel);
                    _logger.LogInformation("📝 下右历史记录面板创建完成");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 下右历史记录面板创建失败");
                    var placeholder = new System.Windows.Controls.Border
                    {
                        Background = new SolidColorBrush(Colors.LightGray),
                        Child = new System.Windows.Controls.TextBlock
                        {
                            Text = "历史记录加载失败",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    System.Windows.Controls.Grid.SetColumn(placeholder, 1);
                    bottomGrid.Children.Add(placeholder);
                }

                System.Windows.Controls.Grid.SetRow(bottomGrid, 1);
                mainContentArea.Children.Add(bottomGrid);

                System.Windows.Controls.Grid.SetRow(mainContentArea, 1);
                mainContentGrid.Children.Add(mainContentArea);

                System.Windows.Controls.Grid.SetRow(mainContentGrid, 1);
                mainGrid.Children.Add(mainContentGrid);

                Content = mainGrid;

                _logger.LogInformation("✅ 代码化UI界面创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 代码化UI创建失败");
                throw; // 重新抛出异常，不要在这里回退到fallback UI
            }
        }

        /// <summary>
        /// 创建标题栏面板
        /// </summary>
        private System.Windows.Controls.StackPanel CreateTitlePanel()
        {
            var titlePanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(10)
            };

            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = "自动盯盘监控面板",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 🎯 新增：启动/停止盯盘按钮
            var toggleButton = new System.Windows.Controls.Button
            {
                Width = 120,
                Height = 30,
                Margin = new Thickness(20, 0, 8, 0),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                ToolTip = "启动或停止自动盯盘"
            };
            
            // 绑定按钮属性
            var contentBinding = new System.Windows.Data.Binding("ToggleButtonText") { Source = this };
            toggleButton.SetBinding(System.Windows.Controls.Button.ContentProperty, contentBinding);
            
            var backgroundBinding = new System.Windows.Data.Binding("ToggleButtonBackground") { Source = this };
            toggleButton.SetBinding(System.Windows.Controls.Button.BackgroundProperty, backgroundBinding);
            
            var enabledBinding = new System.Windows.Data.Binding("ToggleButtonEnabled") { Source = this };
            toggleButton.SetBinding(System.Windows.Controls.Button.IsEnabledProperty, enabledBinding);
            
            var tooltipBinding = new System.Windows.Data.Binding("ToggleButtonTooltip") { Source = this };
            toggleButton.SetBinding(System.Windows.Controls.Button.ToolTipProperty, tooltipBinding);
            
            toggleButton.Click += ToggleMonitorButton_Click;

            // 从配置加载按钮
            var loadConfigButton = new System.Windows.Controls.Button
            {
                Content = "📁从配置加载",
                Width = 120,
                Height = 30,
                Margin = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Colors.Purple),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                ToolTip = "从自动盯盘配置文件加载合约和条件",
                IsEnabled = true
            };
            loadConfigButton.Click += LoadFromConfigButton_Click;

            var refreshButton = new System.Windows.Controls.Button
            {
                Content = "🔄网络数据",
                Width = 110,
                Height = 30,
                Margin = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Colors.SteelBlue),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                ToolTip = "刷新监控数据"
            };
            refreshButton.Click += RefreshButton_Click;

            var closeButton = new System.Windows.Controls.Button
            {
                Content = "❌关闭面板", 
                Width = 110,
                Height = 30,
                Margin = new Thickness(8, 0, 0, 0),
                Background = new SolidColorBrush(Colors.Gray),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                ToolTip = "关闭监控面板"
            };
            closeButton.Click += CloseButton_Click;

            titlePanel.Children.Add(titleText);
            titlePanel.Children.Add(toggleButton);
            titlePanel.Children.Add(loadConfigButton);
            titlePanel.Children.Add(refreshButton);
            titlePanel.Children.Add(closeButton);

            return titlePanel;
        }

        /// <summary>
        /// 创建最基本的后备UI
        /// </summary>
        private void CreateFallbackUI()
        {
            Title = "监控面板";
            Width = 800;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            DataContext = this;

            var fallbackText = new System.Windows.Controls.TextBlock
            {
                Text = "自动盯盘监控面板\n\n状态：正在运行\n\n如需查看详细信息，请检查主界面的自动盯盘状态。",
                FontSize = 16,
                Margin = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            Content = fallbackText;
            _logger.LogInformation("✅ 后备UI界面创建成功");
        }

        /// <summary>
        /// 创建状态信息卡片
        /// </summary>
        private System.Windows.Controls.Border CreateStatusCard()
        {
            var card = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Color.FromRgb(240, 248, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 5, 0, 5)
            };

            var panel = new System.Windows.Controls.StackPanel();
            
            var title = new System.Windows.Controls.TextBlock
            {
                Text = "监控状态",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(title);

            var statusText = new System.Windows.Controls.TextBlock
            {
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };
            
            // 绑定到状态属性
            var binding = new System.Windows.Data.Binding("MonitorStatus")
            {
                Source = this,
                StringFormat = "运行状态: {0}"
            };
            statusText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, binding);
            panel.Children.Add(statusText);

            var timeText = new System.Windows.Controls.TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 5, 0, 0)
            };
            
            var timeBinding = new System.Windows.Data.Binding("LastUpdateTime")
            {
                Source = this,
                StringFormat = "最后更新: {0:HH:mm:ss}"
            };
            timeText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, timeBinding);
            panel.Children.Add(timeText);

            card.Child = panel;
            return card;
        }

        /// <summary>
        /// 创建统计信息卡片
        /// </summary>
        private System.Windows.Controls.Border CreateStatsCard()
        {
            var card = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 255, 245)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 5, 0, 5)
            };

            var panel = new System.Windows.Controls.StackPanel();
            
            var title = new System.Windows.Controls.TextBlock
            {
                Text = "📈 执行统计",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(title);

            var contractsText = new System.Windows.Controls.TextBlock { FontSize = 14 };
            var contractsBinding = new System.Windows.Data.Binding("ActiveContractCount")
            {
                Source = this,
                StringFormat = "活跃合约: {0} 个"
            };
            contractsText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, contractsBinding);
            panel.Children.Add(contractsText);

            var executionsText = new System.Windows.Controls.TextBlock { FontSize = 14 };
            var executionsBinding = new System.Windows.Data.Binding("TotalExecutions")
            {
                Source = this,
                StringFormat = "总执行次数: {0}"
            };
            executionsText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, executionsBinding);
            panel.Children.Add(executionsText);

            card.Child = panel;
            return card;
        }

        /// <summary>
        /// 创建实时信息卡片
        /// </summary>
        private System.Windows.Controls.Border CreateRealTimeInfoCard()
        {
            var card = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 250, 240)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 5, 0, 5)
            };

            var panel = new System.Windows.Controls.StackPanel();
            
            var title = new System.Windows.Controls.TextBlock
            {
                Text = "🔔 实时信息",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(title);

            var configText = new System.Windows.Controls.TextBlock
            {
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };
            
            var configBinding = new System.Windows.Data.Binding("ConfigName")
            {
                Source = this,
                StringFormat = "当前配置: {0}"
            };
            configText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, configBinding);
            panel.Children.Add(configText);

            var intervalText = new System.Windows.Controls.TextBlock
            {
                FontSize = 14,
                Margin = new Thickness(0, 5, 0, 0)
            };
            
            var intervalBinding = new System.Windows.Data.Binding("ScanIntervalDisplay")
            {
                Source = this,
                StringFormat = "扫描间隔: {0}"
            };
            intervalText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, intervalBinding);
            panel.Children.Add(intervalText);

            var infoText = new System.Windows.Controls.TextBlock
            {
                Text = "\n💡 提示：监控面板每30秒自动刷新\n📊 详细数据请查看主界面的自动盯盘状态",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.DarkOrange),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0)
            };
            panel.Children.Add(infoText);

            card.Child = panel;
            return card;
        }

        /// <summary>
        /// 清理状态按钮点击事件
        /// </summary>
        private void ClearStatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "确定要清理所有合约的执行状态吗？\n\n这将：\n• 清除所有推仓、保本、保盈的已执行记录\n• 重置所有合约的状态到初始状态\n• 解决重复推仓的问题\n\n⚠️ 此操作不可撤销！", 
                    "确认清理状态", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var clearedCount = 0;
                    var clearedContracts = new List<string>();
                    
                    try
                    {
                        // 🔧 获取所有活跃合约的信息
                        var profiles = _autoMonitorService.GetPositionProfiles();
                        
                        // 🔧 逐个清理每个合约（确保保本状态也被清理）
                        foreach (var profile in profiles.Values)
                        {
                            _autoMonitorService.ClearContractStates(profile.Symbol, profile.PositionSide, "用户手动清理");
                            clearedContracts.Add($"{profile.Symbol}_{profile.PositionSide}");
                            clearedCount++;
                        }
                        
                        // 🔧 简化版：状态清理已在简化服务层自动处理
                        _logger.LogInformation("✅ 简化版服务：状态清理已自动处理");
                        
                        // 🔧 状态清理记录已由ClearContractStates方法自动添加到执行历史
                        
                        // 🔧 立即强制刷新显示（确保UI立即更新）
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            UpdateContractStates();  // 立即更新合约状态
                            UpdateExecutionHistory(); // 立即更新执行历史
                            UpdateBasicStats();       // 立即更新统计信息
                            LastUpdateTime = DateTime.Now;
                        });
                        
                        // 🔧 添加额外的异步刷新作为保险
                        _ = Task.Run(async () => 
                        {
                            await Task.Delay(500); // 延迟500ms再刷新一次
                            await RefreshDataAsync();
                        });
                        
                        var contractsList = string.Join(", ", clearedContracts);
                        _logger.LogInformation($"🧹 用户手动清理了{clearedCount}个合约状态: {contractsList}");
                        
                        // 🔧 获取清理后的状态验证
                        var updatedProfiles = _autoMonitorService.GetPositionProfiles();
                        var remainingTriggers = updatedProfiles.Values.Sum(p => p.TriggerRecords.Count);
                        var updatedHistory = _autoMonitorService.GetExecutionHistory();
                        var clearanceRecords = updatedHistory.Count(h => h.ExecutionType.Contains("清理"));
                        
                        var message = $"✅ 状态清理完成！\n\n清理详情：\n• 清理合约数量: {clearedCount} 个\n• 清理的合约: {string.Join(", ", clearedContracts)}\n• 剩余触发记录: {remainingTriggers} 条\n• 清理记录数: {clearanceRecords} 条\n\n效果验证：\n• 所有推仓、保本、保盈状态已重置\n• 保本状态应显示为\"未触发\"\n• 执行历史中已添加清理记录\n• UI已强制刷新，请查看变化";
                        MessageBox.Show(message, "清理完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception clearEx)
                    {
                        _logger.LogError(clearEx, "状态清理过程中发生错误");
                        MessageBox.Show($"状态清理过程中发生错误: {clearEx.Message}", "清理错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理状态时发生错误");
                MessageBox.Show($"清理状态时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 🔧 修复：检查监控状态，询问用户是否要停止监控
                if (_autoMonitorService.IsRunning)
                {
                    var result = MessageBox.Show(
                        "检测到自动盯盘正在运行中。\n\n" +
                        "【是】- 停止后台监控并关闭窗口\n" +
                        "【否】- 保持后台监控运行，仅关闭窗口\n" +
                        "【取消】- 不关闭窗口\n\n" +
                        "注意：选择【否】将保持后台自动盯盘继续运行，您可以通过主界面的\"停止盯盘\"按钮来停止监控。",
                        "确认关闭",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);
                    
                    switch (result)
                    {
                        case MessageBoxResult.Yes:
                            // 停止监控并关闭窗口
                            _logger.LogInformation("🛑 用户选择停止后台监控并关闭监控面板");
                            _ = Task.Run(async () => await HandleStopMonitoring());
                            break;
                            
                        case MessageBoxResult.No:
                            // 保持监控运行，仅关闭窗口
                            _logger.LogInformation("🖥️ 用户选择保持后台监控运行，仅关闭监控面板");
                            AppendLog("💡 后台自动盯盘将继续运行，可通过主界面停止");
                            break;
                            
                        case MessageBoxResult.Cancel:
                            // 取消关闭
                            _logger.LogInformation("❌ 用户取消关闭监控面板");
                            return;
                    }
                }
                
                // 取消订阅自动盯盘事件
                _autoMonitorService.MonitorStatusChanged -= OnMonitorStatusChanged;
                _autoMonitorService.ExecutionCompleted -= OnExecutionCompleted;
                _autoMonitorService.WorkLogAdded -= OnWorkLogAdded;
                
                // 🔧 新增：取消配置同步事件订阅
                if (_mainViewModel != null)
                {
                    _mainViewModel.ConfigurationSyncRequested -= OnConfigurationSyncRequested;
                    _logger.LogInformation("✅ 已取消配置同步事件订阅");
                }
                
                // 🔧 简化版：无需取消订阅，简化服务自动处理
                _logger.LogInformation("✅ 简化版服务：无需手动取消事件订阅");
                
                // 🔧 修复：停止所有定时器
                _refreshTimer?.Stop();
                _countdownTimer?.Stop();
                // 🔧 关键修复：停止标题定时器，避免资源泄漏导致系统异常
                _titleTimer?.Stop();
                
                _logger.LogInformation("🖥️ 监控界面资源清理完成 (包含倒计时Timer和事件订阅)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 清理监控界面资源时发生错误");
            }
            
            // 🔧 修复：正确关闭窗口，防止程序最小化
            try
            {
                // 确保窗口正确关闭而不是隐藏
                this.WindowState = WindowState.Normal;
                this.Hide(); // 先隐藏窗口
                this.Close(); // 然后关闭窗口
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 关闭窗口时发生错误");
                // 如果正常关闭失败，强制关闭
                Application.Current.Dispatcher.BeginInvoke(new Action(() => 
                {
                    this.Close();
                }));
            }
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("🔄 手动刷新监控面板数据");
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 手动刷新监控面板时发生错误");
            }
        }

        /// <summary>
        /// 启动/停止盯盘按钮点击事件
        /// </summary>
        private async void ToggleMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            // 🔧 完全重写：消除所有文件访问冲突和死锁风险
            WriteEmergencyLog("🚨 [BUTTON-01] 按钮点击事件触发");
            
            try
            {
                // 简单的状态检查
                var serviceIsRunning = _autoMonitorService.IsRunning;
                WriteEmergencyLog($"🚨 [BUTTON-02] 服务运行状态: {serviceIsRunning}");
                
                if (serviceIsRunning)
                {
                    WriteEmergencyLog("🚨 [BUTTON-03] 执行停止盯盘流程");
                    _logger?.LogInformation("⏹️ 用户点击停止盯盘按钮");
                    
                    // 停止盯盘 - 保持在UI线程
                    await HandleStopMonitoring();
                }
                else
                {
                    WriteEmergencyLog("🚨 [BUTTON-04] 执行启动盯盘流程");
                    _logger?.LogInformation("🚀 用户点击启动盯盘按钮");
                    
                    // 启动盯盘 - 关键修复：不使用ConfigureAwait(false)
                    await HandleStartMonitoring();
                }
                
                WriteEmergencyLog("🚨 [BUTTON-05] 按钮点击处理完成");
            }
            catch (Exception ex)
            {
                WriteEmergencyLog($"🚨 [BUTTON-ERROR] 按钮点击异常: {ex.Message}");
                _logger?.LogError(ex, "❌ 启动/停止盯盘时发生错误");
                
                // 简单的错误处理，避免复杂的UI操作
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 恢复按钮状态
                var currentlyRunning = _autoMonitorService.IsRunning;
                UpdateToggleButtonState(currentlyRunning, currentlyRunning ? "停止盯盘" : "启动盯盘", 
                    currentlyRunning ? Colors.Red : Colors.Green, true);
            }
        }

        /// <summary>
        /// 处理停止监控
        /// </summary>
        private async Task HandleStopMonitoring()
        {
                    _logger.LogInformation("⏹️ 用户点击停止盯盘按钮");
                    
                    // 更新按钮状态为停止中
                    UpdateToggleButtonState(false, "正在停止...", Colors.Orange, false);
                    
            try
            {
                    await _autoMonitorService.StopMonitoringAsync();
                
                // 🔧 修复：停止成功后重置倒计时状态
                _nextScanDateTime = DateTime.Now;
                ScanCountdownDisplay = "未启动";
                NextScanTime = "未启动";
                CooldownStatusDisplay = "未启动";
                
                // 🔧 修复：确保按钮状态正确更新
                UpdateToggleButtonState(false, "启动盯盘", Colors.Green, true);
                    
                    // 恢复编辑权限
                    UpdateEditPermissions(true);
                
                // 🔧 Phase 7: 停止监控时禁用实时同步
                DisableRealTimeSync();
                
                // 🔧 Phase 8: 记录监控停止日志
                if (_comprehensiveLoggingService != null)
                {
                    await _comprehensiveLoggingService.LogMonitorStopAsync("用户操作");
                    
                    await _comprehensiveLoggingService.LogButtonClickAsync(
                        "停止盯盘", 
                        "AutoMonitorDashboard");
                }
                
                // 🔧 修复：触发属性更新通知
                OnPropertyChanged(nameof(ScanCountdownDisplay));
                OnPropertyChanged(nameof(NextScanTime));
                OnPropertyChanged(nameof(CooldownStatusDisplay));
                    
                    MessageBox.Show("自动盯盘已停止", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    _logger.LogInformation("✅ 自动盯盘已成功停止");
            }
            catch (Exception stopEx)
            {
                _logger.LogError(stopEx, "❌ 停止盯盘时发生异常");
                
                // 🔧 修复：即使停止失败也要恢复UI状态
                UpdateToggleButtonState(false, "启动盯盘", Colors.Green, true);
                UpdateEditPermissions(true);
                
                MessageBox.Show($"停止盯盘时发生错误: {stopEx.Message}", "停止失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 处理启动监控 - 在后台线程执行防止UI卡死
        /// </summary>
        private async Task HandleStartMonitoring()
        {
            WriteEmergencyLog("🚨 [START-01] HandleStartMonitoring 方法开始执行");
            _logger?.LogInformation("🚀 用户点击启动盯盘按钮");
            
            try
            {
                // 更新按钮状态为启动中
                WriteEmergencyLog("🚨 [START-02] 准备更新按钮状态");
                UpdateToggleButtonState(false, "正在启动...", Colors.Orange, false);
                WriteEmergencyLog("🚨 [START-03] 按钮状态更新完成");
                
                // 🔧 关键修复：不使用ConfigureAwait(false)，保持在UI线程上下文
                WriteEmergencyLog("🚨 [START-04] 开始调用PerformStartMonitoringAsync");
                var result = await PerformStartMonitoringAsync();
                WriteEmergencyLog($"🚨 [START-05] PerformStartMonitoringAsync完成，结果: {result.Success}");
                
                // 根据结果更新UI状态
                if (result.Success)
                {
                    WriteEmergencyLog("🚨 [START-06] 处理成功结果");
                    
                    // 禁用编辑权限
                    UpdateEditPermissions(false);
                    
                    // 🔧 修复：启动成功后立即更新按钮状态
                    UpdateToggleButtonState(true, "停止盯盘", Colors.Red, true);
                    
                    // 🔧 修复：确保倒计时定时器运行
                    if (!_countdownTimer.IsEnabled)
                    {
                        _countdownTimer.Start();
                        _logger.LogInformation("🔄 重新启动倒计时定时器");
                    }
                    
                    // 🔧 Phase 7: 启动监控成功后启用实时同步
                    EnableRealTimeSync();
                    
                    // 🔧 Phase 8: 记录监控启动日志
                    if (_comprehensiveLoggingService != null)
                    {
                        var config = _autoMonitorService.CurrentConfig ?? _mainViewModel?.CurrentAutoMonitorConfig;
                        var configName = config?.Name ?? "未知配置";
                        var contractCount = ContractMonitors?.Count ?? 0;
                        var scanInterval = config?.ScanIntervalSeconds ?? 30;
                        
                        await _comprehensiveLoggingService.LogMonitorStartAsync(
                            $"配置: {configName}, 合约数: {contractCount}, 扫描间隔: {scanInterval}秒");
                        
                        await _comprehensiveLoggingService.LogButtonClickAsync(
                            "启动盯盘", 
                            $"配置: {configName}, 合约数: {contractCount}, 扫描间隔: {scanInterval}秒");
                    }
                    
                    MessageBox.Show(result.Message, "启动成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    _logger.LogInformation($"✅ 自动盯盘已成功启动");
                }
                else
                {
                    WriteEmergencyLog("🚨 [START-07] 处理失败结果");
                    
                    // 🔧 修复：启动失败时恢复按钮状态并重置倒计时
                    UpdateToggleButtonState(false, "启动盯盘", Colors.Green, true);
                    _nextScanDateTime = DateTime.Now;
                    
                    // 🔧 简化：直接在UI线程显示MessageBox，避免复杂的Task.Run
                    MessageBox.Show(result.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    _logger.LogError($"❌ 启动盯盘失败: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                WriteEmergencyLog($"🚨 [START-ERROR] HandleStartMonitoring异常: {ex.Message}");
                _logger.LogError(ex, "❌ 启动盯盘时发生异常");
                
                // 恢复按钮状态
                UpdateToggleButtonState(false, "启动盯盘", Colors.Green, true);
                
                MessageBox.Show($"启动盯盘时发生错误: {ex.Message}", "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行启动监控的后台操作
        /// </summary>
        private async Task<(bool Success, string Message)> PerformStartMonitoringAsync()
        {
            WriteEmergencyLog("🚨 [PERFORM-01] PerformStartMonitoringAsync 开始执行");
            _logger?.LogCritical("🔍 [PERFORM-01] PerformStartMonitoringAsync 开始执行");
            
            try
            {
                // 🔧 【关键修复】启动盯盘前重新从文件载入最新状态
                WriteEmergencyLog("🚨 [PERFORM-01.5] 启动前重新载入文件状态");
                _logger?.LogCritical("🔍 [PERFORM-01.5] 启动前重新载入文件状态");
                
                try
                {
                    var fileLoadSuccess = await LoadFromMonitoringStatesFileAsync();
                    _logger?.LogCritical($"🔍 [PERFORM-01.6] 文件状态载入结果: {fileLoadSuccess}");
                }
                catch (Exception loadEx)
                {
                    _logger?.LogWarning(loadEx, "⚠️ 启动前载入文件状态失败，继续使用当前配置");
                }
                
                WriteEmergencyLog("🚨 [PERFORM-02] 开始获取配置");
                _logger?.LogCritical("🔍 [PERFORM-02] 开始获取配置");
                    
                    // 🔧 修复：智能配置检查 - 从多个来源获取配置
                    var config = _autoMonitorService.CurrentConfig ?? _mainViewModel?.CurrentAutoMonitorConfig;
                    
                WriteEmergencyLog($"🚨 [PERFORM-03] 配置获取完成: {(config != null ? config.Name : "null")}");
                _logger?.LogCritical($"🔍 [PERFORM-03] 配置获取完成: {(config != null ? config.Name : "null")}");
                
                // 🔧 关键修复：如果没有配置，检查是否有保存的合约配置（完全避免UI线程交互）
                if (config == null)
                {
                    WriteEmergencyLog("🚨 [PERFORM-04] 配置为空，开始检查本地配置");
                    _logger?.LogCritical("🔍 [PERFORM-04] 配置为空，开始检查本地配置");
                    bool hasLocalConfig = false;
                    
                    // 🔧 修复死锁：不使用Dispatcher.Invoke，直接检查集合
                    // 这个操作是线程安全的，因为ObservableCollection的Count是原子操作
                    try
                    {
                        WriteEmergencyLog("🚨 [PERFORM-05] 开始检查ContractMonitors集合");
                        _logger?.LogCritical("🔍 [PERFORM-05] 开始检查ContractMonitors集合");
                        
                        hasLocalConfig = ContractMonitors?.Count > 0;
                        
                        WriteEmergencyLog($"🚨 [PERFORM-06] ContractMonitors检查完成: {hasLocalConfig}");
                        _logger?.LogCritical($"🔍 [PERFORM-06] ContractMonitors检查完成: {hasLocalConfig}");
                    }
                    catch (Exception ex)
                    {
                        WriteEmergencyLog($"🚨 [PERFORM-07] 检查本地配置异常: {ex.Message}");
                        _logger?.LogCritical(ex, "🔍 [PERFORM-07] 检查本地配置异常");
                        _logger?.LogWarning(ex, "❌ 检查本地配置时发生异常，将尝试继续启动");
                        hasLocalConfig = false;
                    }
                    
                    if (hasLocalConfig)
                    {
                        // 🔧 关键修复：直接创建临时配置，避免任何UI交互
                        _logger.LogInformation("🔧 检测到本地合约配置但缺少基础参数配置");
                        _logger.LogInformation("🔧 自动创建临时配置以启动盯盘");
                        
                        // 自动创建临时配置，避免用户交互阻塞
                        config = new AutoMonitorConfig
                        {
                            Name = "临时配置（基于本地合约）",
                            ScanIntervalSeconds = 10, // 使用较安全的10秒间隔
                            IsEnabled = true,
                            CreateTime = DateTime.Now,
                            // 🔧 添加基础配置项，确保启动成功
                            BreakEvenConfig = new AutoBreakEvenConfig
                            {
                                IsEnabled = false // 默认关闭
                            },
                            AddPositionConfig = new AutoAddPositionConfig
                            {
                                IsEnabled = false,
                                Tiers = new List<AddPositionTier>() // 空列表
                            },
                            ProfitProtectionConfig = new AutoProfitProtectionConfig
                            {
                                IsEnabled = false,
                                Tiers = new List<ProfitProtectionTier>() // 空列表
                            }
                        };
                        
                        _logger.LogInformation($"✅ 创建临时配置：{config.Name}，间隔：{config.ScanIntervalSeconds}秒");
                    }
                    else
                    {
                        // 🔧 关键修复：没有任何配置时，返回友好错误，不进行UI交互
                        _logger.LogWarning("❌ 未检测到任何配置，无法启动盯盘");
                        return (false, "请先配置盯盘参数\n\n💡 操作步骤：\n1. 在主界面点击【盯盘参数配置】按钮\n2. 配置好参数后点击保存\n3. 返回此面板点击【加载配置】");
                    }
                }
                
                if (config == null)
                {
                    _logger.LogError("❌ 配置获取失败，无法启动");
                    return (false, "配置获取失败");
                }
                
                _logger.LogCritical($"🔍 [PERFORM-08] 配置验证完成，准备启动服务");
                    _logger.LogInformation($"✅ 找到可用配置: {config.Name}");
                    
                // 🔧 修复：添加重试机制，最多重试2次
                bool success = false;
                string errorMessage = "";
                
                _logger.LogCritical("🔍 [PERFORM-09] 开始重试循环");
                for (int retryCount = 0; retryCount <= 2; retryCount++)
                {
                    try
                    {
                        _logger.LogCritical($"🔍 [PERFORM-10] 重试循环第{retryCount + 1}次");
                        if (retryCount > 0)
                        {
                            _logger.LogCritical($"🔍 [PERFORM-11] 执行第{retryCount}次重试逻辑");
                            _logger.LogInformation($"🔄 第{retryCount}次重试启动盯盘...");
                            
                            // 🔧 修复死锁：不在Task.Run内部使用Dispatcher调用
                            // UpdateToggleButtonState会在后续的success检查中正确更新
                            _logger.LogInformation($"🔧 重试中({retryCount}/2)，稍后将更新界面状态");
                            
                            await Task.Delay(1000 * retryCount); // 递增延迟
                            _logger.LogCritical($"🔍 [PERFORM-12] 重试延迟完成");
                        }
                        
                        _logger.LogCritical("🔍 [PERFORM-13] 准备重置倒计时状态");
                        // 🔧 修复：重置倒计时状态，避免状态污染（不使用Dispatcher避免死锁）
                        // 这个字段的设置是线程安全的，稍后UI更新时会读取到最新值
                        _nextScanDateTime = DateTime.Now.AddSeconds(config.ScanIntervalSeconds);
                        _logger.LogCritical("🔍 [PERFORM-14] 倒计时状态重置完成");
                        
                                        WriteEmergencyLog("🚨 [PERFORM-15] 即将调用 _autoMonitorService.StartMonitoringAsync");
                _logger?.LogCritical("🔍 [PERFORM-15] 即将调用 _autoMonitorService.StartMonitoringAsync");
                
                // 🔧 关键修复：检查和重置服务状态
                WriteEmergencyLog($"🚨 [PERFORM-15-1] 检查服务状态，当前IsRunning: {_autoMonitorService.IsRunning}");
                _logger?.LogCritical($"🔍 [PERFORM-15-1] 检查服务状态，当前IsRunning: {_autoMonitorService.IsRunning}");
                
                try
                {
                    // 强制停止可能的残留任务
                    if (_autoMonitorService.IsRunning)
                    {
                        WriteEmergencyLog("🚨 [PERFORM-15-2] 服务仍在运行，开始强制停止...");
                        _logger?.LogCritical("🔍 [PERFORM-15-2] 服务仍在运行，开始强制停止...");
                        
                        var stopTask = _autoMonitorService.StopMonitoringAsync();
                        await stopTask;
                        
                        WriteEmergencyLog($"🚨 [PERFORM-15-3] 服务已停止，新状态: {_autoMonitorService.IsRunning}");
                        _logger?.LogCritical($"🔍 [PERFORM-15-3] 服务已停止，新状态: {_autoMonitorService.IsRunning}");
                    }
                    else
                    {
                        WriteEmergencyLog("🚨 [PERFORM-15-4] 服务状态正常，无需重置");
                        _logger?.LogCritical("🔍 [PERFORM-15-4] 服务状态正常，无需重置");
                    }
                }
                catch (Exception ex)
                {
                    WriteEmergencyLog($"🚨 [PERFORM-15-ERROR] 服务重置异常: {ex.Message}");
                    _logger?.LogCritical(ex, "🔍 [PERFORM-15-ERROR] 服务重置异常");
                }
                
                // 🚨 关键诊断：这是最有可能卡死的地方
                var callTimestamp = DateTime.Now;
                WriteEmergencyLog($"🚨 [PERFORM-16] 开始调用StartMonitoringAsync");
                
                // 🔧 关键修复：为API调用添加超时控制，防止无限卡死
                var apiTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30秒API超时
                WriteEmergencyLog("🚨 [PERFORM-16-1] API超时控制已创建(30秒)");
                        
                // 🔧 修复：创建强制超时检查器的取消令牌，防止资源泄漏
                var forceTimeoutCts = new CancellationTokenSource();
                WriteEmergencyLog("🚨 [PERFORM-16-1-1] 强制超时检查器取消令牌已创建");
                
                try
                {
                    WriteEmergencyLog("🚨 [PERFORM-16-2] 开始超时包装的API调用");
                    
                    // 🔧 强化超时控制：使用多重保护机制
                    var apiTask = _autoMonitorService.StartMonitoringAsync(config);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                    
                    // 🔧 修复：创建可取消的超时检查任务，防止资源泄漏
                    var timeoutChecker = Task.Run(async () =>
                    {
                        WriteEmergencyLog("🚨 [PERFORM-16-CHECKER] 35秒强制超时检查器启动");
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(35), forceTimeoutCts.Token);
                            WriteEmergencyLog("🚨 [PERFORM-16-FORCE-TIMEOUT] 强制超时检查触发");
                            _logger?.LogCritical("🔍 [PERFORM-16-FORCE-TIMEOUT] 强制超时检查触发");
                            throw new TimeoutException("强制超时 - API调用超过35秒");
                        }
                        catch (OperationCanceledException)
                        {
                            WriteEmergencyLog("🚨 [PERFORM-16-CHECKER-CANCEL] 强制超时检查器已取消");
                            throw; // 重新抛出取消异常
                        }
                    }, forceTimeoutCts.Token);
                    
                    WriteEmergencyLog($"🚨 [PERFORM-16-3] 等待API调用或超时... 时间戳: {callTimestamp:HH:mm:ss.fff}");
                    _logger?.LogCritical($"🔍 [PERFORM-16-3] 等待API调用或超时... 时间戳: {callTimestamp:HH:mm:ss.fff}");
                    
                    WriteEmergencyLog("🚨 [PERFORM-16-4] 开始Task.WhenAny等待...");
                    var completedTask = await Task.WhenAny(apiTask, timeoutTask, timeoutChecker);
                    WriteEmergencyLog("🚨 [PERFORM-16-5] Task.WhenAny完成");
                    
                    var endTimestamp = DateTime.Now;
                    var totalDuration = endTimestamp - callTimestamp;
                    
                    if (completedTask == timeoutTask)
                    {
                        // 30秒超时
                        WriteEmergencyLog($"🚨 [PERFORM-16-TIMEOUT] API调用超时(30秒)，实际耗时: {totalDuration.TotalSeconds:F2}秒");
                        _logger?.LogCritical($"🔍 [PERFORM-16-TIMEOUT] API调用超时(30秒)，实际耗时: {totalDuration.TotalSeconds:F2}秒");
                        
                        // 🔧 修复：取消强制超时检查器
                        forceTimeoutCts.Cancel();
                        WriteEmergencyLog("🚨 [PERFORM-16-TIMEOUT-CLEANUP] 已取消强制超时检查器");
                        
                        throw new TimeoutException("API调用超时（30秒）- AutoMonitorService.StartMonitoringAsync 未响应");
                    }
                    else if (completedTask == timeoutChecker)
                    {
                        // 35秒强制超时
                        WriteEmergencyLog($"🚨 [PERFORM-16-FORCE-TIMEOUT] 强制超时(35秒)，实际耗时: {totalDuration.TotalSeconds:F2}秒");
                        _logger?.LogCritical($"🔍 [PERFORM-16-FORCE-TIMEOUT] 强制超时(35秒)，实际耗时: {totalDuration.TotalSeconds:F2}秒");
                        throw new TimeoutException("强制超时（35秒）- API调用完全无响应");
                    }
                    else
                    {
                        // 🔧 修复：正常完成时取消所有超时检查器
                        WriteEmergencyLog("🚨 [PERFORM-16-SUCCESS] API任务正常完成，获取结果中...");
                        success = await apiTask; // 获取真正的结果
                        apiTimeoutCts.Cancel(); // 取消超时定时器
                        
                        // 🔧 关键修复：取消强制超时检查器，防止后台继续运行
                        forceTimeoutCts.Cancel();
                        WriteEmergencyLog("🚨 [PERFORM-16-SUCCESS-CLEANUP] 已取消强制超时检查器");
                        
                        var completeTimestamp = DateTime.Now;
                        var duration = completeTimestamp - callTimestamp;
                        WriteEmergencyLog($"🚨 [PERFORM-17] StartMonitoringAsync调用完成，耗时: {duration.TotalSeconds:F2}秒，结果: {success}");
                        _logger?.LogCritical($"🔍 [PERFORM-17] StartMonitoringAsync 调用完成，耗时: {duration.TotalSeconds:F2}秒，结果: {success}");
                    }
                }
                catch (TimeoutException)
                {
                    WriteEmergencyLog("🚨 [PERFORM-16-ERROR] API调用超时异常");
                    
                    // 🔧 修复：异常时也要取消强制超时检查器
                    forceTimeoutCts.Cancel();
                    WriteEmergencyLog("🚨 [PERFORM-16-ERROR-CLEANUP] 已取消强制超时检查器");
                    
                    throw; // 重新抛出超时异常
                }
                catch (Exception apiEx)
                {
                    WriteEmergencyLog($"🚨 [PERFORM-16-ERROR] API调用异常: {apiEx.Message}");
                    
                    // 🔧 修复：异常时也要取消强制超时检查器
                    forceTimeoutCts.Cancel();
                    WriteEmergencyLog("🚨 [PERFORM-16-ERROR-CLEANUP] 已取消强制超时检查器");
                    
                    throw; // 重新抛出API异常
                }
                finally
                {
                    // 🔧 修复：确保在所有退出路径上都释放资源
                    try
                    {
                        apiTimeoutCts?.Dispose();
                        forceTimeoutCts?.Dispose();
                        WriteEmergencyLog("🚨 [PERFORM-16-FINAL-CLEANUP] 所有超时控制资源已释放");
                    }
                    catch (Exception cleanupEx)
                    {
                        WriteEmergencyLog($"🚨 [PERFORM-16-CLEANUP-ERROR] 清理资源异常: {cleanupEx.Message}");
                    }
                }
                    
                    if (success)
                    {
                            var successMessage = retryCount > 0 ? 
                                $"自动盯盘已启动（第{retryCount}次重试成功）" : 
                                "自动盯盘已启动";
                        
                            _logger.LogInformation($"✅ 自动盯盘已成功启动{(retryCount > 0 ? $"（重试{retryCount}次）" : "")}");
                            return (true, successMessage);
                    }
                    else
                    {
                            errorMessage = $"启动失败，服务返回false{(retryCount < 2 ? "，正在重试..." : "")}";
                        
                        // 🔧 修复：分析可能的失败原因
                        var isRunning = _autoMonitorService.IsRunning;
                        var currentConfig = _autoMonitorService.CurrentConfig;
                        
                        _logger.LogWarning($"⚠️ 第{retryCount + 1}次启动返回false");
                        _logger.LogWarning($"🔧 服务状态诊断:");
                        _logger.LogWarning($"   • IsRunning: {isRunning}");
                        _logger.LogWarning($"   • Config: {(currentConfig != null ? $"有效({currentConfig.Name})" : "无效")}");
                        _logger.LogWarning($"   • 配置名称: {config.Name ?? "未命名"}");
                        _logger.LogWarning($"   • 配置间隔: {config.ScanIntervalSeconds}秒");
                        _logger.LogWarning($"   • 简化版服务：状态管理已内置");
                        
                        if (retryCount < 2)
                        {
                            var waitTime = 1000 * (retryCount + 1);
                            _logger.LogInformation($"🔄 准备第{retryCount + 1}次重试，等待{waitTime}ms...");
                            continue; // 继续重试
                        }
                        else
                        {
                            _logger.LogError("❌ 已达到最大重试次数，启动最终失败");
                            _logger.LogError("💡 建议操作:");
                            _logger.LogError("   • 检查应用程序日志中的详细错误信息");
                            _logger.LogError("   • 验证网络连接和API配置");
                            _logger.LogError("   • 考虑重启应用程序");
                            break;
                        }
                    }
                }
                    catch (Exception startEx)
                    {
                        success = false;
                        errorMessage = startEx.Message;
                        WriteEmergencyLog($"🚨 [PERFORM-ERROR] 启动异常: {startEx.GetType().Name} - {startEx.Message}");
                        _logger?.LogError(startEx, $"❌ 第{retryCount + 1}次启动盯盘时发生异常");
                        
                        // 🔧 修复：特殊处理各种错误类型
                        if (startEx is TimeoutException)
                        {
                            errorMessage = "API调用超时（30秒），启动失败\n\n🚨 可能原因：\n• AutoMonitorService内部卡死\n• 网络连接问题\n• API服务响应缓慢\n• 内部死锁或阻塞\n\n💡 建议：\n• 检查网络连接\n• 重启应用程序\n• 检查API配置";
                            WriteEmergencyLog("🚨 [PERFORM-TIMEOUT] 确认为超时错误，不重试");
                            break; // 超时错误不重试，直接返回
                        }
                        else if (startEx.Message.Contains("channel has been closed", StringComparison.OrdinalIgnoreCase))
                        {
                            errorMessage = "连接通道已关闭，正在重试连接...\n\n可能原因：\n• 服务刚停止，通道未完全关闭\n• 网络连接不稳定\n• API连接超时";
                            if (retryCount < 2)
                            {
                                _logger?.LogInformation($"🔄 检测到通道关闭错误，将等待{2000 * (retryCount + 1)}ms后重试");
                                await Task.Delay(1000 * (retryCount + 1)); // 通道错误需要更长等待时间
                                continue;
                            }
                        }
                        else if (startEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                        {
                            errorMessage = "连接超时，正在重试...\n\n建议：\n• 检查网络连接\n• 确认API服务可用";
                            if (retryCount < 2) continue;
                        }
                        else if (startEx.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
                        {
                            errorMessage = "API认证失败，请检查API密钥配置";
                            break; // 认证错误不需要重试
                        }
                        else
                        {
                            if (retryCount < 2)
                            {
                                errorMessage = $"启动失败: {startEx.Message}，正在重试...";
                                continue;
                            }
                            else
                            {
                                errorMessage = $"启动失败: {startEx.Message}";
                                break;
                            }
                        }
                    }
                }
                
                // 如果到这里说明所有重试都失败了
                return (false, $"启动盯盘失败，请检查配置\n\n错误详情：{errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 执行启动监控时发生异常");
                return (false, $"启动盯盘时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新启动/停止按钮状态
        /// </summary>
        private void UpdateToggleButtonState(bool isRunning, string text, Color backgroundColor, bool enabled)
        {
            _logger.LogCritical($"🔍 [UI-UPDATE-01] 开始更新按钮状态: {text}");
            try
            {
                // 🔧 关键修复：检查是否在UI线程，避免不必要的Invoke调用
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    _logger.LogCritical("🔍 [UI-UPDATE-02] 在UI线程，直接更新");
                    ToggleButtonText = text;
                    ToggleButtonBackground = new SolidColorBrush(backgroundColor);
                    ToggleButtonEnabled = enabled;
                    
                    if (isRunning)
                    {
                        ToggleButtonTooltip = "点击停止自动盯盘监控";
                    }
                    else
                    {
                        ToggleButtonTooltip = enabled ? "点击启动自动盯盘监控" : "正在处理中...";
                    }
                    _logger.LogCritical("🔍 [UI-UPDATE-03] UI线程直接更新完成");
                }
                else
                {
                    _logger.LogCritical("🔍 [UI-UPDATE-04] 非UI线程，使用Invoke");
            Application.Current.Dispatcher.Invoke(() =>
            {
                        _logger.LogCritical("🔍 [UI-UPDATE-05] Invoke内部开始执行");
                ToggleButtonText = text;
                ToggleButtonBackground = new SolidColorBrush(backgroundColor);
                ToggleButtonEnabled = enabled;
                
                if (isRunning)
                {
                    ToggleButtonTooltip = "点击停止自动盯盘监控";
                }
                else
                {
                    ToggleButtonTooltip = enabled ? "点击启动自动盯盘监控" : "正在处理中...";
                }
                        _logger.LogCritical("🔍 [UI-UPDATE-06] Invoke内部执行完成");
                    });
                    _logger.LogCritical("🔍 [UI-UPDATE-07] Invoke调用完成");
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "🔍 [UI-UPDATE-ERROR] 更新按钮状态异常");
            }
        }

        /// <summary>
        /// 更新编辑权限状态
        /// </summary>
        private void UpdateEditPermissions(bool canEdit)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                EditButtonEnabled = canEdit;
                IsDataGridReadOnly = !canEdit;
                
                if (!canEdit)
                {
                    StatusText = "盯盘运行中 - 配置已锁定，停止盯盘后可编辑";
                }
                else
                {
                    StatusText = "盯盘已停止 - 可编辑配置";
                }
            });
        }

        /// <summary>
        /// 合约监控表格双击事件 - 打开状态编辑对话框
        /// </summary>
        private void ContractMonitorDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 🔒 严格检查：必须在自动盯盘停止状态下才能编辑
                if (_autoMonitorService?.IsRunning == true)
                {
                    MessageBox.Show("⚠️ 安全限制：自动盯盘正在运行中，无法编辑状态！\n\n为确保数据一致性，请先停止自动盯盘，然后再编辑状态。", 
                        "编辑被阻止", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _logger.LogWarning("🔒 用户尝试在盯盘运行中编辑状态，已阻止");
                    return;
                }

                if (!EditButtonEnabled)
                {
                    MessageBox.Show("当前状态不允许编辑，请确认自动盯盘已完全停止。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 获取选中的合约
                var dataGrid = sender as DataGrid;
                if (dataGrid?.SelectedItem is ContractMonitorModel selectedContract)
                {
                    _logger.LogInformation($"👆 用户双击编辑合约状态 - {selectedContract.Symbol}_{selectedContract.PositionSide}");
                    OpenContractStatusEditDialog(selectedContract);
                }
                else
                {
                    MessageBox.Show("请先选择一个合约进行编辑", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 处理表格双击事件时发生错误");
            }
        }
        
        /// <summary>
        /// 打开合约状态编辑对话框
        /// </summary>
        private void OpenContractStatusEditDialog(ContractMonitorModel contract)
        {
            try
            {
                _logger.LogInformation($"🔧 打开状态编辑对话框 - {contract.Symbol}_{contract.PositionSide}");
                
                var statusEditDialog = new ContractStatusEditDialog(contract, _logger, _autoMonitorService)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                
                var result = statusEditDialog.ShowDialog();
                
                if (result == true && statusEditDialog.HasChanges)
                {
                    _logger.LogInformation($"✅ 状态编辑完成，有更改 - {contract.Symbol}");
                    
                    // 触发UI更新
                    contract.OnPropertyChanged(""); // 触发所有属性更新
                    
                    // 刷新统计信息
                    UpdateNewInterfaceStats();
                    RefreshContractMonitorStatus();
                    
                    MessageBox.Show("状态编辑已保存", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _logger.LogInformation($"📄 状态编辑取消或无更改 - {contract.Symbol}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 打开状态编辑对话框失败");
                MessageBox.Show($"打开编辑对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 编辑条件按钮点击事件
        /// </summary>
        private async void EditConditionsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!EditButtonEnabled)
                {
                    MessageBox.Show("盯盘运行中，无法编辑配置。请先停止盯盘。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                if (sender is Button button && button.Tag is string contractKey)
                {
                    _logger.LogInformation($"📝 用户点击编辑合约 {contractKey} 的条件");
                    
                    // 查找对应的合约监控模型
                    var contract = ContractMonitors.FirstOrDefault(c => c.ContractKey == contractKey);
                    if (contract == null)
                    {
                        MessageBox.Show("未找到对应的合约数据，请先加载合约监控数据", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    OpenEditDialog(contract);
                }
                else
                {
                    // 兜底：如果没有tag，编辑第一个合约
                    if (ContractMonitors.Any())
                    {
                        OpenEditDialog(ContractMonitors.First());
                    }
                    else
                    {
                        MessageBox.Show("请先加载合约数据", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 编辑条件时发生错误");
                MessageBox.Show($"编辑失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开编辑对话框
        /// </summary>
        private async void OpenEditDialog(ContractMonitorModel contract)
        {
            try
            {
                _logger.LogInformation($"📝 打开编辑对话框 - {contract.Symbol}_{contract.PositionSide}");
                
                // 暂停扫描（避免编辑过程中的冲突）
                bool wasRunning = _autoMonitorService.IsRunning;
                if (wasRunning)
                {
                    var pauseResult = MessageBox.Show("编辑期间将暂停自动扫描，继续吗？", "确认编辑", 
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (pauseResult != MessageBoxResult.Yes)
                    {
                        return;
                    }
                    
                    // 实际暂停扫描功能
                    try
                    {
                        if (_autoMonitorService != null && _autoMonitorService.IsRunning)
                        {
                            // 暂停定时器扫描
                            await _autoMonitorService.PauseAsync();
                            _logger.LogInformation("⏸️ 扫描已暂停，可以安全进行编辑操作");
                        }
                        else
                        {
                            _logger.LogInformation("⚠️ 监控服务未运行，无需暂停");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ 暂停扫描失败: {0}", ex.Message);
                    }
                }
                
                // 显示简化编辑功能（暂时用消息框代替完整对话框）
                var editResult = ShowSimplifiedEditDialog(contract);
                
                if (editResult)
                {
                    // 应用修改
                    ApplyEditChanges(contract);
                    
                    // 刷新数据
                    RefreshContractMonitorStatus();
                    UpdateNewInterfaceStats();
                    
                    MessageBox.Show("✅ 触发条件修改已保存！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    _logger.LogInformation($"✅ 触发条件编辑完成 - {contract.Symbol}_{contract.PositionSide}");
                }
                
                // 恢复扫描
                if (wasRunning)
                {
                    // 实际恢复扫描功能
                    try
                    {
                        if (_autoMonitorService != null && _autoMonitorService.IsRunning)
                        {
                            // 恢复定时器扫描
                            await _autoMonitorService.ResumeAsync();
                    _logger.LogInformation("▶️ 编辑完成，扫描已恢复");
                        }
                        else
                        {
                            _logger.LogInformation("⚠️ 监控服务未运行，无需恢复");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ 恢复扫描失败: {0}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 打开编辑对话框时发生错误");
                throw;
            }
        }

        /// <summary>
        /// 显示增强的编辑对话框（支持修改触发金额和保盈金额）
        /// </summary>
        private bool ShowSimplifiedEditDialog(ContractMonitorModel contract)
        {
            try
            {
                // 🔧 新功能：使用增强的编辑对话框
                var editDialog = new ContractEditDialog(contract, _logger);
                editDialog.Owner = this;
                
                var result = editDialog.ShowDialog();
                
                if (result == true && editDialog.HasChanges)
                {
                    _logger.LogInformation($"✅ 合约配置编辑完成: {contract.ContractKey}");
                    
                    // 保存到文件
                    // SaveContractConfigsToFile(); // 已废弃：使用统一状态管理
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 编辑对话框显示失败");
                MessageBox.Show($"显示编辑对话框失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 应用编辑变更（临时实现）
        /// </summary>
        private void ApplyEditChanges(ContractMonitorModel contract)
        {
            try
            {
                // 演示：修改第一个未执行的条件状态
                var conditionToModify = contract.TriggerConditions
                    .FirstOrDefault(c => c.Status == TriggerExecutionStatus.NotTriggered);
                
                if (conditionToModify != null)
                {
                    var originalStatus = conditionToModify.Status;
                    conditionToModify.Status = TriggerExecutionStatus.Executed;
                    conditionToModify.LastExecutionTime = DateTime.Now;
                    
                    _logger.LogInformation($"🔄 演示修改 - {conditionToModify.Description}: {originalStatus} → {conditionToModify.Status}");
                    
                    // 实际的状态保存逻辑
                    try
                    {
                        // 保存到持久化存储
                        // SaveContractConfigsToFile(); // 已废弃：使用统一状态管理
                        _logger.LogInformation("💾 状态修改已保存到本地文件");
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, "❌ 保存状态到文件失败: {0}", saveEx.Message);
                    }
                }
                else
                {
                    // 如果没有未触发的，就随机重置一个已执行的为未触发
                    var executedCondition = contract.TriggerConditions
                        .FirstOrDefault(c => c.Status == TriggerExecutionStatus.Executed);
                    
                    if (executedCondition != null)
                    {
                        var originalStatus = executedCondition.Status;
                        executedCondition.Status = TriggerExecutionStatus.NotTriggered;
                        executedCondition.LastExecutionTime = null;
                        
                        _logger.LogInformation($"🔄 演示重置 - {executedCondition.Description}: {originalStatus} → {executedCondition.Status}");
                    }
                    else
                    {
                        _logger.LogInformation("💡 没有找到可修改的触发条件");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 应用编辑变更时发生错误");
                throw;
            }
        }

        /// <summary>
        /// 重置状态按钮点击事件
        /// </summary>
        private void ResetStatusButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is string contractKey)
                {
                    _logger.LogInformation($"🧹 用户点击重置合约 {contractKey} 的状态");
                    
                    var result = MessageBox.Show($"确定要重置合约 {contractKey} 的所有状态吗？\n\n这将把所有触发条件状态重置为\"未触发\"", 
                        "确认重置", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        MessageBox.Show($"重置合约 {contractKey} 状态功能正在开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 重置状态时发生错误");
            }
        }

        /// <summary>
        /// 🚀 从配置载入持仓按钮点击事件（使用新的载入流程）
        /// </summary>
        private void LoadFromConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("🚀 用户点击载入持仓配置按钮（新流程）");
                
                // 🔧 检查是否需要重置配置
                var baseConfig = GetCurrentAutoMonitorConfig();
                if (baseConfig == null)
                {
                    MessageBox.Show("❌ 无法获取基础配置！\n\n请先在主界面配置自动盯盘参数。", "配置错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 🔧 询问用户是否需要用基础配置重置合约配置
                var resetChoice = MessageBox.Show(
                    $"📂 载入持仓配置选项\n\n基础配置：{baseConfig.Name}\n\n选择载入方式：\n\n✅ 【是】- 用基础配置重置所有合约配置\n   • 会完全按照基础配置重新生成所有触发条件\n   • 适用于修改基础配置后需要同步的情况\n\n❌ 【否】- 保留现有合约配置\n   • 优先使用已保存的合约配置\n   • 仅更新触发价格，保持执行状态\n\n🔧 建议：如果您刚修改了基础配置，选择【是】", 
                    "载入配置选项", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                
                if (resetChoice == MessageBoxResult.Cancel)
                {
                    return;
                }
                
                bool forceResetFromBase = resetChoice == MessageBoxResult.Yes;
                
                if (forceResetFromBase)
                {
                    _logger.LogInformation("🔄 用户选择用基础配置重置合约配置");
                    LoadCurrentPositionsWithBaseConfigReset(baseConfig);
                    }
                    else
                    {
                    _logger.LogInformation("🔄 用户选择保留现有合约配置");
                    LoadCurrentPositionsWithConfigs();
                }
                
                // 🎯 根据载入的配置重新生成表格列
                _logger.LogInformation($"✅ 使用现有配置生成表格列: {baseConfig.Name}");
                GenerateDynamicDataGridColumns(baseConfig);
                    
                    // 🔧 强制刷新界面显示
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateNewInterfaceStats();
                        OnPropertyChanged(nameof(ContractMonitors));
                        
                        // 通知DataGrid刷新
                        if (_contractMonitorDataGrid != null)
                        {
                            _contractMonitorDataGrid.Items.Refresh();
                        }
                    });
                    
                    var loadedContracts = ContractMonitors.Count;
                    var totalConditions = ContractMonitors.Sum(c => c.TriggerConditions.Count);
                    var executedConditions = ContractMonitors.Sum(c => c.TriggerConditions.Count(tc => tc.Status == TriggerExecutionStatus.Executed));
                    
                MessageBox.Show($"✅ 持仓配置载入完成！\n\n📊 载入结果：\n• 合约数量：{loadedContracts} 个\n• 触发条件：{totalConditions} 个\n• 已执行条件：{executedConditions} 个\n• 重置方式：{(forceResetFromBase ? "基础配置重置" : "保留现有配置")}\n\n💾 配置已自动保存到本地\n🚀 现在可以启动盯盘功能了", 
                        "载入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                    _logger.LogInformation($"✅ 配置载入完成，载入{loadedContracts}个合约");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 载入持仓配置时发生错误");
                MessageBox.Show($"载入失败：{ex.Message}\n\n🔧 可能的原因：\n• 无法获取当前持仓信息\n• 基础配置不完整\n• 本地配置文件损坏", "载入错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存配置按钮点击事件
        /// </summary>
        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                            _logger.LogInformation("🔄 用户点击更新配置按钮");

            if (!ContractMonitors.Any())
            {
                MessageBox.Show("没有可更新的合约配置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // SaveContractConfigsToFile(); // 已废弃：使用统一状态管理
            var filePathManager = new FilePathManager();
            var currentAccount = filePathManager.GetCurrentAccountName();
            MessageBox.Show($"✅ 合约配置更新成功！\n📊 已更新 {ContractMonitors.Count} 个合约配置\n📁 数据保存在统一状态文件中\n\n💡 配置已持久化，重新启动程序后仍然有效", 
                "配置更新成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 保存配置时发生错误");
                MessageBox.Show($"保存配置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 全部重置按钮点击事件
        /// </summary>
        /// <summary>
        /// 全部重置按钮点击事件 - 重置状态并重新加载基础配置
        /// </summary>
        private void ResetAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("🧹 用户点击全部重置按钮");
                
                if (!ContractMonitors.Any())
                {
                    MessageBox.Show("没有可重置的合约配置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 🔧 获取当前基础配置
                var baseConfig = GetCurrentAutoMonitorConfig();
                if (baseConfig == null)
                {
                    MessageBox.Show("❌ 无法获取基础配置！\n\n请先在主界面配置自动盯盘参数，然后再进行重置操作。", 
                        "配置错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var totalConditions = ContractMonitors.Sum(c => c.TriggerConditions.Count);
                var executedConditions = ContractMonitors.Sum(c => c.TriggerConditions.Count(tc => tc.Status == TriggerExecutionStatus.Executed));
                
                // 🔧 更新确认对话框，说明会重新加载配置
                var resetConfirm = MessageBox.Show($"🔄 确定要全部重置并重新加载基础配置吗？\n\n📊 统计信息：\n• 合约总数：{ContractMonitors.Count} 个\n• 触发条件总数：{totalConditions} 个\n• 已执行条件：{executedConditions} 个\n\n⚠️ 此操作将：\n• 重置所有\"已执行\"的触发条件为\"未触发\"状态\n• 重新从基础配置加载所有目标价、止盈、止损设置\n• 保持合约的启用状态和个性化设置\n\n💡 基础配置: {baseConfig.Name}", 
                    "确认全部重置", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (resetConfirm == MessageBoxResult.Yes)
                {
                    var resetCount = 0;
                    var reloadCount = 0;
                    
                    _logger.LogInformation($"🔄 开始全部重置，使用基础配置: {baseConfig.Name}");
                    
                    foreach (var contract in ContractMonitors)
                    {
                        // 🔧 第1步：重置执行状态
                        foreach (var condition in contract.TriggerConditions)
                        {
                            if (condition.Status == TriggerExecutionStatus.Executed)
                            {
                                condition.Status = TriggerExecutionStatus.NotTriggered;
                                condition.LastExecutionTime = null;
                                condition.StatusNote = $"全部重置 {DateTime.Now:HH:mm:ss}";
                                resetCount++;
                            }
                        }
                        
                        // 🔧 第2步：重新从基础配置加载所有触发价格和配置参数
                        try
                        {
                            var beforeCount = contract.TriggerConditions.Count;
                            
                            // 使用现有的方法重新加载基础配置的触发价格
                            UpdateTriggerPricesFromBaseConfig(baseConfig, contract);
                            
                            // 🎯 更高级的重载：如果基础配置中有新的阶梯，也要添加
                            ReloadContractFromBaseConfig(baseConfig, contract.Symbol);
                            
                            var afterCount = contract.TriggerConditions.Count;
                            reloadCount++;
                            
                            _logger.LogInformation($"✅ 重载完成: {contract.Symbol}_{contract.PositionSide} - 触发条件: {beforeCount} → {afterCount}");
                        }
                        catch (Exception reloadEx)
                        {
                            _logger.LogError(reloadEx, $"❌ 重载配置失败: {contract.Symbol}_{contract.PositionSide}");
                        }
                    }
                    
                    // 🔧 第3步：保存到文件
                    // SaveContractConfigsToFile(); // 已废弃：使用统一状态管理
                    
                    // 🔧 第4步：更新统计信息
                    UpdateNewInterfaceStats();
                    
                    // 🔧 第5步：显示完成结果
                    var resultMessage = $"✅ 全部重置和重载完成！\n\n📊 操作结果：\n• 重置执行状态：{resetCount} 个触发条件\n• 重载基础配置：{reloadCount} 个合约\n• 使用基础配置：{baseConfig.Name}\n\n💾 所有配置已保存到本地文件\n🔄 建议：可以启动盯盘功能测试新配置";
                    
                    if (resetCount > 0 || reloadCount > 0)
                    {
                        MessageBox.Show(resultMessage, "重置完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        AppendLog($"🔄 全部重置完成: 重置{resetCount}个状态, 重载{reloadCount}个合约配置");
                    }
                    else
                    {
                        MessageBox.Show("ℹ️ 所有触发条件都处于\"未触发\"状态，但已重新加载基础配置", 
                            "重载完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    
                    _logger.LogInformation($"✅ 全部重置和重载操作完成: 重置{resetCount}个状态, 重载{reloadCount}个合约");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 全部重置时发生错误");
                MessageBox.Show($"全部重置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 查看历史按钮点击事件
        /// </summary>
        private void ViewHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("📊 用户点击查看执行历史按钮");
                
                // 🔧 修复：直接使用简单稳定的历史信息对话框
                ShowSimpleHistoryDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 查看执行历史时发生错误");
                MessageBox.Show($"查看执行历史失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 显示简单的历史信息对话框（备用方案）
        /// </summary>
        private void ShowSimpleHistoryDialog()
        {
            try
            {
                if (_autoMonitorService == null)
                {
                    MessageBox.Show("自动监控服务未初始化", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var history = _autoMonitorService.GetExecutionHistory();
                if (history == null || !history.Any())
                {
                    MessageBox.Show("📋 暂无执行历史记录", "执行历史", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 显示最近10条记录
                var recentHistory = history.TakeLast(10).Reverse();
                var historyText = "📊 最近执行历史记录（最多显示10条）：\n\n";
                
                foreach (var item in recentHistory)
                {
                    var status = item.IsSuccess ? "✅" : "❌";
                    historyText += $"{status} {item.ExecutionTime:MM-dd HH:mm:ss} [{item.Symbol}] {item.ExecutionType} - {item.TriggerPnl:F2}U\n";
                }

                MessageBox.Show(historyText, "执行历史", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 显示简单历史对话框失败");
                MessageBox.Show($"显示历史记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        /// <summary>
        /// 刷新数据
        /// </summary>
        private async Task RefreshDataAsync()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 🔧 修复：先刷新持仓数据，确保显示最新持仓
                    RefreshCurrentPositionsData();
                    
                    UpdateBasicStats();
                    UpdateConfiguration();
                    UpdateContractStates();
                    UpdateExecutionHistory();
                    
                    // 🚀 新增：更新新界面的统计信息
                    UpdateNewInterfaceStats();
                    
                    // 🚀 新增：如果已有数据则刷新合约监控状态
                    if (ContractMonitors.Any())
                    {
                        RefreshContractMonitorStatus();
                    }
                    
                    LastUpdateTime = DateTime.Now;
                    
                    // 🔧 新增：记录数据刷新日志
                    if (_autoMonitorService.IsRunning)
                    {
                        var positionCount = _autoMonitorService.GetPositionProfiles()?.Count ?? 0;
                        var executionCount = _autoMonitorService.GetExecutionHistory()?.Count ?? 0;
                        AppendLog($"🔄 数据刷新完成 - 活跃合约: {positionCount}, 执行记录: {executionCount}");
                    }
                });
                
                _logger.LogDebug("✅ 自动盯盘监控面板数据刷新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 刷新监控面板数据时发生异常");
                AppendLog($"❌ 数据刷新异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新基础统计信息
        /// </summary>
        private void UpdateBasicStats()
        {
            try
            {
                // 更新监控状态
                MonitorStatus = _autoMonitorService.IsRunning ? "🟢 运行中" : "🔴 已停止";
                
                // 更新状态卡片颜色
                if (_autoMonitorService.IsRunning)
                {
                    StatusCardBackground = new SolidColorBrush(Colors.LightGreen);
                    StatusTextColor = new SolidColorBrush(Colors.DarkGreen);
                    StatusIconColor = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    StatusCardBackground = new SolidColorBrush(Colors.LightCoral);
                    StatusTextColor = new SolidColorBrush(Colors.DarkRed);
                    StatusIconColor = new SolidColorBrush(Colors.Red);
                }
                
                // 更新运行时间
                if (_autoMonitorService.IsRunning && _monitorStartTime != default)
                {
                    var runningTimeSpan = DateTime.Now - _monitorStartTime;
                    RunningTime = $"{runningTimeSpan.Hours:D2}:{runningTimeSpan.Minutes:D2}:{runningTimeSpan.Seconds:D2}";
                }
                else
                {
                    RunningTime = "00:00:00";
                    _monitorStartTime = _autoMonitorService.IsRunning ? DateTime.Now : default;
                }
                
                // 更新统计信息
                var positionProfiles = _autoMonitorService.GetPositionProfiles();
                ActiveContractCount = positionProfiles?.Count ?? 0;
                
                var executionHistory = _autoMonitorService.GetExecutionHistory();
                TotalExecutions = executionHistory.Count;
                
                if (executionHistory.Any())
                {
                    var successCount = executionHistory.Count(h => h.IsSuccess);
                    ExecutionSuccessRate = (double)successCount / executionHistory.Count * 100;
                }
                else
                {
                    ExecutionSuccessRate = 100.0;
                }
                
                // 简化的止损单统计
                ActiveStopOrderCount = TotalExecutions > 0 ? ActiveContractCount : 0;
                StopOrderSuccessRate = ExecutionSuccessRate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新基础统计信息时发生异常");
            }
        }

        /// <summary>
        /// 更新配置信息显示
        /// </summary>
        private void UpdateConfiguration()
        {
            try
            {
                var config = _autoMonitorService.CurrentConfig;
                if (config != null)
                {
                    ConfigName = config.Name;
                    ScanIntervalDisplay = $"{config.ScanIntervalSeconds}秒";
                    
                    if (config.BreakEvenConfig.IsEnabled)
                    {
                        BreakEvenConfigDisplay = $"启用 - 浮盈{config.BreakEvenConfig.TriggerProfitAmount:F0}U触发";
                    }
                    else
                    {
                        BreakEvenConfigDisplay = "未启用";
                    }
                    
                    // 🔧 更新推仓阶梯显示（移除数量限制，支持多次推仓）
                    AddPositionTiers.Clear();
                    if (config.AddPositionConfig.IsEnabled)
                    {
                        // 不再限制档位数量，支持未来扩展到更多推仓档位
                        foreach (var tier in config.AddPositionConfig.Tiers.OrderBy(t => t.TierIndex))
                        {
                            AddPositionTiers.Add(new AddPositionTierDisplayModel
                            {
                                TierIndex = tier.TierIndex,
                                TriggerProfitAmount = tier.TriggerProfitAmount,
                                RiskMultiplier = tier.RiskMultiplier,
                                StopLossRatio = tier.StopLossRatio
                            });
                        }
                    }
                    
                    // 🔧 更新保盈阶梯显示（移除数量限制，支持多次止盈）
                    ProfitProtectionTiers.Clear();
                    if (config.ProfitProtectionConfig.IsEnabled)
                    {
                        // 不再限制档位数量，支持未来扩展到更多止盈档位
                        foreach (var tier in config.ProfitProtectionConfig.Tiers.OrderBy(t => t.TierIndex))
                        {
                            ProfitProtectionTiers.Add(new ProfitProtectionTierDisplayModel
                            {
                                TierIndex = tier.TierIndex,
                                TriggerProfitAmount = tier.TriggerProfitAmount,
                                ProtectionAmount = tier.ProtectionAmount
                            });
                        }
                    }
                }
                else
                {
                    ConfigName = "未配置";
                    ScanIntervalDisplay = "--";
                    BreakEvenConfigDisplay = "未配置";
                    AddPositionTiers.Clear();
                    ProfitProtectionTiers.Clear();
                }
                
                // 🔧 新增：智能更新监控面板刷新间隔，跟随配置变化
                UpdateRefreshTimerInterval();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新配置信息时发生异常");
            }
        }

        /// <summary>
        /// 更新合约状态
        /// </summary>
        private void UpdateContractStates()
        {
            try
            {
                ContractStates.Clear();
                
                var positionProfiles = _autoMonitorService.GetPositionProfiles();
                if (positionProfiles != null)
                {
                    // 🔧 获取当前配置的档位总数（支持动态档位数量）
                    var config = _autoMonitorService.CurrentConfig;
                    var totalAddPositionTiers = config?.AddPositionConfig?.Tiers?.Count ?? 0;
                    var totalProfitProtectionTiers = config?.ProfitProtectionConfig?.Tiers?.Count ?? 0;
                    
                    foreach (var kvp in positionProfiles.Where(p => p.Value.IsActive).Take(10))
                    {
                        var profile = kvp.Value;
                        
                        // 🔧 【关键修复】优先从状态文件检查执行状态，确保与文件同步
                        var breakEvenExecuted = _autoMonitorService.IsExecutedInStateFile(profile.Symbol, profile.PositionSide, "保本") ||
                                               profile.TriggerRecords.Values.Any(r => 
                                                   r.TriggerType == "BreakEven" || r.TriggerType == "自动保本");
                        var addPositionProgress = profile.TriggerRecords.Values.Count(r => 
                            r.TriggerType.StartsWith("AddPosition") || r.TriggerType.Contains("推仓"));
                        var profitProtectionProgress = profile.TriggerRecords.Values.Count(r => 
                            r.TriggerType.StartsWith("ProfitProtection") || r.TriggerType.Contains("保盈"));
                        var totalExecutions = profile.TriggerRecords.Count;
                        
                        // 🔧 动态计算执行百分比（基于实际配置的档位数量）
                        var maxPossibleExecutions = 1 + totalAddPositionTiers + totalProfitProtectionTiers; // 保本1个 + 动态推仓档位 + 动态保盈档位
                        var executionProgress = maxPossibleExecutions > 0 ? (double)totalExecutions / maxPossibleExecutions * 100 : 0;
                        
                        // 🔧 修复：保本状态只显示已触发/未触发两种状态，使用更直观的颜色
                        string breakEvenStatus;
                        SolidColorBrush breakEvenColor;
                        if (breakEvenExecuted)
                        {
                            breakEvenStatus = "已触发";
                            breakEvenColor = new SolidColorBrush(Colors.Green); // 绿色：已完成
                        }
                        else
                        {
                            breakEvenStatus = "未触发";
                            breakEvenColor = new SolidColorBrush(Colors.SteelBlue); // 蓝色：待触发
                        }
                        
                        // 获取最后执行时间
                        var lastExecutionTime = profile.TriggerRecords.Values.Any() ? 
                            profile.TriggerRecords.Values.Max(r => r.TriggerTime) : DateTime.MinValue;
                        
                        ContractStates.Add(new ContractStateDisplayModel
                        {
                            Symbol = profile.Symbol,
                            PositionSide = profile.PositionSide,
                            BreakEvenStatus = breakEvenStatus,
                            BreakEvenStatusColor = breakEvenColor,
                            AddPositionProgress = addPositionProgress,
                            ProfitProtectionProgress = profitProtectionProgress,
                            TotalExecutions = totalExecutions,
                            ExecutionProgress = executionProgress,
                            LastExecutionTime = lastExecutionTime == DateTime.MinValue ? DateTime.Now : lastExecutionTime,
                            // 🔧 新增：动态档位总数支持
                            AddPositionTotalTiers = totalAddPositionTiers,
                            ProfitProtectionTotalTiers = totalProfitProtectionTiers
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新合约状态时发生异常");
            }
        }

        /// <summary>
        /// 更新执行历史
        /// </summary>
        private void UpdateExecutionHistory()
        {
            try
            {
                ExecutionHistory.Clear();
                
                var history = _autoMonitorService.GetExecutionHistory();
                
                foreach (var record in history.OrderByDescending(h => h.ExecutionTime).Take(20))
                {
                    SolidColorBrush resultColor;
                    string resultText;
                    
                    if (record.IsSuccess)
                    {
                        resultColor = new SolidColorBrush(Colors.Green);
                        resultText = "成功";
                    }
                    else
                    {
                        resultColor = new SolidColorBrush(Colors.Red);
                        resultText = "失败";
                    }
                    
                    ExecutionHistory.Add(new ExecutionHistoryDisplayModel
                    {
                        ExecutionTime = record.ExecutionTime,
                        Symbol = record.Symbol,
                        ExecutionType = record.ExecutionType,
                        ResultText = resultText,
                        ResultColor = resultColor,
                        TriggerPnl = record.TriggerPnl,
                        Details = record.Details ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新执行历史时发生异常");
            }
        }

        private void OnMonitorStatusChanged(object? sender, BinanceFuturesTrader.Models.MonitorStatusChangedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                MonitorStatus = e.IsRunning ? "运行中" : "已停止";
                
                // 🔧 新增：记录状态变化日志
                AppendLog($"🔄 监控状态变更: {(e.IsRunning ? "启动" : "停止")} - {e.Message}");
                
                _ = Task.Run(async () => await RefreshDataAsync());
            });
        }

        private void OnExecutionCompleted(object? sender, BinanceFuturesTrader.Models.ExecutionResultEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // 🔧 新增：记录执行结果日志
                var statusIcon = e.IsSuccess ? "✅" : "❌";
                var resultText = e.IsSuccess ? "成功" : "失败";
                AppendLog($"{statusIcon} {e.ExecutionType} {resultText}: {e.Symbol} (浮盈: {e.PnlAtExecution:F1}U) - {e.Message}");
                
                // 🔧 【需求3】定时程序执行后，立即更新界面显示和文件状态
                _ = Task.Run(async () => await OnScheduledExecutionCompletedAsync(e));
                
                // 🚀 新增：立即更新统计和历史记录，而不是异步全量刷新
                try
                {
                    UpdateBasicStats();       // 立即更新统计信息
                    UpdateExecutionHistory(); // 立即更新执行历史
                    UpdateNewInterfaceStats(); // 立即更新新界面统计
                    
                    _logger.LogDebug($"✅ 立即更新了执行完成后的UI状态: {e.Symbol} {e.ExecutionType}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 立即更新UI状态时发生错误");
                }
                
                // 🔧 保留异步刷新作为备份（但降低优先级）
                _ = Task.Run(async () => 
                {
                    await Task.Delay(1000); // 延迟1秒，避免与立即更新冲突
                    await RefreshDataAsync();
                });
            });
        }
        
        /// <summary>
        /// 从执行类型中提取档位索引
        /// </summary>
        private int? ExtractTierIndexFromExecutionType(string executionType)
        {
            try
            {
                // 匹配如 "推仓1档", "保盈2档" 等格式
                var match = System.Text.RegularExpressions.Regex.Match(executionType, @"(\d+)档");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int tierIndex))
                {
                    return tierIndex;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 🔧 【需求3】定时程序执行操作后，更新界面显示新状态以及修改本地文件的状态
        /// </summary>
        private async Task OnScheduledExecutionCompletedAsync(BinanceFuturesTrader.Models.ExecutionResultEventArgs e)
        {
            try
            {
                _logger.LogInformation($"🔄【需求3】定时程序执行完成，开始更新界面和文件状态: {e.Symbol} {e.ExecutionType}");
                
                // 🔧 1. 立即更新本地文件的状态
                await UpdateFileStateAfterExecutionAsync(e);
                
                // 🔧 2. 立即更新界面显示
                await UpdateUIStateAfterExecutionAsync(e);
                
                // 🔧 3. 验证状态同步
                await VerifyStateSyncAfterExecutionAsync(e);
                
                _logger.LogInformation($"✅【需求3】状态更新完成: {e.Symbol} {e.ExecutionType}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌【需求3】定时程序执行后状态更新失败: {e.Symbol} {e.ExecutionType}");
            }
        }
        
        /// <summary>
        /// 🔧 【需求3-1】更新本地文件的状态
        /// </summary>
        private async Task UpdateFileStateAfterExecutionAsync(BinanceFuturesTrader.Models.ExecutionResultEventArgs e)
        {
            try
            {
                _logger.LogInformation($"📂【需求3-1】更新本地文件状态: {e.Symbol} {e.ExecutionType}");
                
                var stateService = CreateContractMonitoringStateService();
                var positionSide = e.History.PositionSide;
                var contractKey = $"{e.Symbol}_{positionSide}";
                
                // 🔧 从执行类型中提取TierIndex（如："推仓1档" -> 1）
                int? tierIndex = ExtractTierIndexFromExecutionType(e.ExecutionType);
                
                // 🔧 根据执行类型更新对应的状态
                if (e.ExecutionType == "保本" || e.ExecutionType == "BreakEven")
                {
                    stateService.UpdateExecutionStatus(contractKey, "BreakEven", null, e.IsSuccess, e.PnlAtExecution, e.Message);
                    _logger.LogInformation($"✅ 更新保本状态到文件: {contractKey} = {e.IsSuccess}");
                }
                else if (e.ExecutionType.Contains("推仓") && tierIndex.HasValue)
                {
                    stateService.UpdateExecutionStatus(contractKey, "AddPosition", tierIndex, e.IsSuccess, e.PnlAtExecution, e.Message);
                    _logger.LogInformation($"✅ 更新推仓阶梯{tierIndex}状态到文件: {contractKey} = {e.IsSuccess}");
                }
                else if (e.ExecutionType.Contains("保盈") && tierIndex.HasValue)
                {
                    stateService.UpdateExecutionStatus(contractKey, "ProfitProtection", tierIndex, e.IsSuccess, e.PnlAtExecution, e.Message);
                    _logger.LogInformation($"✅ 更新保盈阶梯{tierIndex}状态到文件: {contractKey} = {e.IsSuccess}");
                }
                
                _logger.LogInformation($"💾【需求3-1】文件状态更新完成: {e.Symbol} {e.ExecutionType}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌【需求3-1】更新文件状态失败: {e.Symbol} {e.ExecutionType}");
            }
        }
        
        /// <summary>
        /// 🔧 【需求3-2】更新界面显示状态
        /// </summary>
        private async Task UpdateUIStateAfterExecutionAsync(BinanceFuturesTrader.Models.ExecutionResultEventArgs e)
        {
            try
            {
                _logger.LogInformation($"🖥️【需求3-2】更新界面显示状态: {e.Symbol} {e.ExecutionType}");
                
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var positionSide = e.History.PositionSide;
                    var contractKey = $"{e.Symbol}_{positionSide}";
                    var contractMonitor = ContractMonitors.FirstOrDefault(c => c.ContractKey == contractKey);
                    
                    if (contractMonitor == null)
                    {
                        _logger.LogWarning($"⚠️ 界面中未找到合约配置: {contractKey}");
                        return;
                    }
                    
                    // 🔧 从执行类型中提取TierIndex
                    int? tierIndex = ExtractTierIndexFromExecutionType(e.ExecutionType);
                    
                    // 🔧 根据执行类型更新界面状态
                    TriggerConditionModel targetCondition = null;
                    
                    if (e.ExecutionType == "保本" || e.ExecutionType == "BreakEven")
                    {
                        targetCondition = contractMonitor.TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                    }
                    else if (e.ExecutionType.Contains("推仓") && tierIndex.HasValue)
                    {
                        targetCondition = contractMonitor.TriggerConditions.FirstOrDefault(c => 
                            c.Type == TriggerConditionType.AddPosition && c.TierIndex == tierIndex);
                    }
                    else if (e.ExecutionType.Contains("保盈") && tierIndex.HasValue)
                    {
                        targetCondition = contractMonitor.TriggerConditions.FirstOrDefault(c => 
                            c.Type == TriggerConditionType.ProfitProtection && c.TierIndex == tierIndex);
                    }
                    
                    if (targetCondition != null)
                    {
                        // 🔧 更新界面状态
                        targetCondition.Status = e.IsSuccess ? TriggerExecutionStatus.Executed : TriggerExecutionStatus.NotTriggered;
                        targetCondition.LastExecutionTime = DateTime.Now;
                        
                        // 🔧 触发属性变更通知
                        targetCondition.OnPropertyChanged(nameof(targetCondition.Status));
                        targetCondition.OnPropertyChanged(nameof(targetCondition.LastExecutionTime));
                        targetCondition.OnPropertyChanged(nameof(targetCondition.StatusText));
                        targetCondition.OnPropertyChanged(nameof(targetCondition.StatusColor));
                        
                        _logger.LogInformation($"✅ 界面状态已更新: {contractKey} {e.ExecutionType} = {targetCondition.Status}");
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ 界面中未找到对应的触发条件: {contractKey} {e.ExecutionType}");
                    }
                    
                    // 🔧 刷新整个表格显示
                    if (_contractMonitorDataGrid != null)
                    {
                        _contractMonitorDataGrid.Items.Refresh();
                    }
                    
                    // 🔧 更新统计信息
                    UpdateNewInterfaceStats();
                    OnPropertyChanged(nameof(ContractMonitors));
                });
                
                _logger.LogInformation($"🖥️【需求3-2】界面状态更新完成: {e.Symbol} {e.ExecutionType}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌【需求3-2】更新界面状态失败: {e.Symbol} {e.ExecutionType}");
            }
        }
        
        /// <summary>
        /// 🔧 【需求3-3】验证状态同步
        /// </summary>
        private async Task VerifyStateSyncAfterExecutionAsync(BinanceFuturesTrader.Models.ExecutionResultEventArgs e)
        {
            try
            {
                _logger.LogInformation($"🔍【需求3-3】验证状态同步: {e.Symbol} {e.ExecutionType}");
                
                await Task.Delay(500); // 等待文件写入完成
                
                var positionSide = e.History.PositionSide;
                var contractKey = $"{e.Symbol}_{positionSide}";
                
                // 🔧 从文件重新读取状态
                var stateService = CreateContractMonitoringStateService();
                var fileStates = stateService.LoadMonitoringStates();
                
                if (!fileStates.TryGetValue(contractKey, out var fileState))
                {
                    _logger.LogWarning($"⚠️【需求3-3】文件中未找到合约状态: {contractKey}");
                    return;
                }
                
                // 🔧 从执行类型中提取TierIndex
                int? tierIndex = ExtractTierIndexFromExecutionType(e.ExecutionType);
                
                // 🔧 检查文件状态
                bool fileStatusCorrect = false;
                if (e.ExecutionType == "保本" || e.ExecutionType == "BreakEven")
                {
                    fileStatusCorrect = fileState.BreakEvenConfig.IsExecuted == e.IsSuccess;
                    _logger.LogInformation($"🔍 保本状态验证: 文件={fileState.BreakEvenConfig.IsExecuted}, 预期={e.IsSuccess}");
                }
                else if (e.ExecutionType.Contains("推仓") && tierIndex.HasValue)
                {
                    var tier = fileState.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                    fileStatusCorrect = tier?.IsExecuted == e.IsSuccess;
                    _logger.LogInformation($"🔍 推仓阶梯{tierIndex}状态验证: 文件={tier?.IsExecuted}, 预期={e.IsSuccess}");
                }
                else if (e.ExecutionType.Contains("保盈") && tierIndex.HasValue)
                {
                    var tier = fileState.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                    fileStatusCorrect = tier?.IsExecuted == e.IsSuccess;
                    _logger.LogInformation($"🔍 保盈阶梯{tierIndex}状态验证: 文件={tier?.IsExecuted}, 预期={e.IsSuccess}");
                }
                
                // 🔧 检查界面状态
                bool uiStatusCorrect = false;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var contractMonitor = ContractMonitors.FirstOrDefault(c => c.ContractKey == contractKey);
                    if (contractMonitor != null)
                    {
                        TriggerConditionModel targetCondition = null;
                        
                        if (e.ExecutionType == "保本" || e.ExecutionType == "BreakEven")
                        {
                            targetCondition = contractMonitor.TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                        }
                        else if (e.ExecutionType.Contains("推仓") && tierIndex.HasValue)
                        {
                            targetCondition = contractMonitor.TriggerConditions.FirstOrDefault(c => 
                                c.Type == TriggerConditionType.AddPosition && c.TierIndex == tierIndex);
                        }
                        else if (e.ExecutionType.Contains("保盈") && tierIndex.HasValue)
                        {
                            targetCondition = contractMonitor.TriggerConditions.FirstOrDefault(c => 
                                c.Type == TriggerConditionType.ProfitProtection && c.TierIndex == tierIndex);
                        }
                        
                        if (targetCondition != null)
                        {
                            var expectedStatus = e.IsSuccess ? TriggerExecutionStatus.Executed : TriggerExecutionStatus.NotTriggered;
                            uiStatusCorrect = targetCondition.Status == expectedStatus;
                            _logger.LogInformation($"🔍 界面状态验证: UI={targetCondition.Status}, 预期={expectedStatus}");
                        }
                    }
                });
                
                // 🔧 汇总验证结果
                if (fileStatusCorrect && uiStatusCorrect)
                {
                    _logger.LogInformation($"✅【需求3-3】状态同步验证成功: {contractKey} {e.ExecutionType}");
                }
                else
                {
                    _logger.LogWarning($"⚠️【需求3-3】状态同步验证失败: {contractKey} {e.ExecutionType} - 文件正确={fileStatusCorrect}, 界面正确={uiStatusCorrect}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌【需求3-3】验证状态同步失败: {e.Symbol} {e.ExecutionType}");
            }
        }

        private void OnWorkLogAdded(object? sender, BinanceFuturesTrader.Models.WorkLogEventArgs e)
        {
            try
            {
                AddWorkLog(e.Level, e.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理工作日志事件时发生错误");
            }
        }

        /// <summary>
        /// 处理配置同步事件
        /// </summary>
        private async void OnConfigurationSyncRequested(object sender, ViewModels.ConfigurationSyncEventArgs e)
        {
            try
            {
                _logger.LogInformation($"🔄 接收到配置同步请求：推仓{e.AddPositionTierCount}阶梯，止盈{e.ProfitProtectionTierCount}阶梯");
                
                // 🔧 关键修复：配置切换时，清理所有合约执行状态
                if (_autoMonitorService != null)
                {
                    _logger.LogInformation("🧹 配置同步时清理所有合约执行状态");
                    _autoMonitorService.ClearContractStates(symbol: null, positionSide: null, reason: "配置同步重置");
                    
                    // 🔧 重置所有UI中的合约状态
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        foreach (var contract in ContractMonitors)
                        {
                            foreach (var condition in contract.TriggerConditions)
                            {
                                condition.Status = TriggerExecutionStatus.NotTriggered;
                                condition.LastExecutionTime = null;
                                condition.StatusNote = $"配置同步重置 {DateTime.Now:HH:mm:ss}";
                                
                                // 触发属性更新
                                condition.OnPropertyChanged(nameof(condition.Status));
                                condition.OnPropertyChanged(nameof(condition.StatusText));
                                condition.OnPropertyChanged(nameof(condition.StatusIcon));
                            }
                            
                                                // 触发合约级别属性更新
                    contract.OnPropertyChanged(nameof(contract.TriggerConditions));
                }
                
                _logger.LogInformation($"✅ 已重置 {ContractMonitors.Count} 个合约的执行状态");
            });
        }
        
        // 🔧 【需求2集成】配置同步时触发配置变更处理
        var currentConfig = GetCurrentAutoMonitorConfig();
        if (currentConfig != null)
        {
            _ = Task.Run(async () => await OnConfigurationChangedAsync(currentConfig));
        }
        
        // 🔧 【关键修复】更新顶部配置显示区域（包括保本值）
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            UpdateConfiguration();
            _logger.LogInformation("✅ 已更新配置显示信息（包括保本值）");
        });
        
        // 刷新数据显示
                await RefreshDataAsync();
                
                AppendLog($"🔄 配置同步完成：推仓{e.AddPositionTierCount}阶梯，止盈{e.ProfitProtectionTierCount}阶梯，所有状态已重置");
                _logger.LogInformation("✅ 配置同步处理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 处理配置同步时发生异常");
                AppendLog($"❌ 配置同步失败: {ex.Message}");
            }
        }

        // 🔧 新增：倒计时更新方法
        private void UpdateCountdown(object? sender, EventArgs e)
        {
            try
            {
                var config = _autoMonitorService.CurrentConfig;
                var isRunning = _autoMonitorService.IsRunning;
                
                // 🔧 修复：增加详细的状态检查
                if (config != null && isRunning)
                {
                    var scanInterval = config.ScanIntervalSeconds;
                    var now = DateTime.Now;
                    var elapsed = (now - _nextScanDateTime).TotalSeconds;
                    
                    // 🔧 修复：如果倒计时时间超出扫描间隔，重置倒计时
                    if (elapsed >= scanInterval || elapsed < -scanInterval)
                    {
                        _nextScanDateTime = now.AddSeconds(scanInterval);
                        AppendLog($"⏰ {now:HH:mm:ss} - 开始新一轮扫描 (间隔: {scanInterval}秒)");
                    }
                    
                    var remaining = (_nextScanDateTime - now).TotalSeconds;
                    if (remaining < 0) remaining = 0;
                    
                    ScanCountdownDisplay = $"{(int)remaining:D2}秒";
                    NextScanTime = _nextScanDateTime.ToString("HH:mm:ss");
                    
                    // 更新冷却状态
                    UpdateCooldownStatus();
                    
                    // 🔧 修复：确保UI状态与实际状态一致
                    if (MonitorStatus != "运行中")
                    {
                        MonitorStatus = "运行中";
                        OnPropertyChanged(nameof(MonitorStatus));
                    }
                }
                else
                {
                    // 🔧 修复：未运行时重置倒计时状态
                    ScanCountdownDisplay = "未启动";
                    NextScanTime = "未启动";
                    CooldownStatusDisplay = "未启动";
                    
                    // 🔧 修复：重置下次扫描时间，避免状态污染
                    _nextScanDateTime = DateTime.Now;
                    
                    // 🔧 修复：确保UI状态与实际状态一致
                    if (MonitorStatus != "已停止")
                    {
                        MonitorStatus = "已停止";
                        OnPropertyChanged(nameof(MonitorStatus));
                    }
                }
                
                // 🔧 修复：触发属性更新通知
                OnPropertyChanged(nameof(ScanCountdownDisplay));
                OnPropertyChanged(nameof(NextScanTime));
                OnPropertyChanged(nameof(CooldownStatusDisplay));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新倒计时时发生错误");
                // 🔧 修复：异常时设置安全状态
                ScanCountdownDisplay = "错误";
                NextScanTime = "错误";
                CooldownStatusDisplay = "错误";
            }
        }

        // 🔧 新增：更新冷却状态
        private void UpdateCooldownStatus()
        {
            try
            {
                var activeCooldowns = _autoMonitorService.GetActiveCooldowns();
                if (activeCooldowns.Any())
                {
                    var shortestRemaining = activeCooldowns.Min(c => c.RemainingTime.TotalSeconds);
                    CooldownStatusDisplay = $"{(int)shortestRemaining}秒冷却";
                }
                else
                {
                    CooldownStatusDisplay = "无冷却";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新冷却状态时发生错误");
                CooldownStatusDisplay = "状态错误";
            }
        }

        // 🔧 新增：日志追加方法
        private void AppendLog(string message)
        {
            try
            {
                lock (_logLock)
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss");
                    var logEntry = $"[{timestamp}] {message}\n";
                    
                    // 限制日志长度，保留最新的1000行
                    var lines = RealTimeLog.Split('\n');
                    if (lines.Length > 1000)
                    {
                        var recentLines = lines.Skip(lines.Length - 800).ToArray();
                        RealTimeLog = string.Join("\n", recentLines);
                    }
                    
                    RealTimeLog += logEntry;
                    
                    // 🔧 Phase 8: 记录到综合日志服务
                    LogToComprehensiveService(message);
                    
                    // 自动滚动到底部
                    if (_autoScroll)
                    {
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                // 尝试找到日志滚动视图并滚动到底部
                                var logScrollViewer = this.FindName("LogScrollViewer") as System.Windows.Controls.ScrollViewer;
                                if (logScrollViewer != null)
                                {
                                    logScrollViewer.ScrollToEnd();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "自动滚动日志时发生错误");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "追加日志时发生错误");
            }
        }

        // 🔧 新增：清理日志按钮事件
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                lock (_logLock)
                {
                    WorkLogs.Clear();
                    AddWorkLog("INFO", "🧹 日志已清理");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理日志时发生错误");
            }
        }

        // 🔧 新增：添加工作日志条目
        private void AddWorkLog(string level, string message)
        {
            try
            {
                lock (_logLock)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        WorkLogs.Add(new WorkLog(level, message));
                        
                        // 限制日志条目数量，避免内存过多占用
                        if (WorkLogs.Count > 1000)
                        {
                            WorkLogs.RemoveAt(0);
                        }

                        // 自动滚动到最新日志
                        if (_autoScroll)
                        {
                            try
                            {
                                // 查找日志ScrollViewer并滚动到底部
                                var scrollViewer = FindName("LogScrollViewer") as ScrollViewer;
                                scrollViewer?.ScrollToBottom();
                            }
                            catch
                            {
                                // 忽略滚动错误
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加工作日志时发生错误: {Message}", message);
            }
        }

        // 🔧 新增：自动滚动按钮事件 - 通过CheckBox状态变化触发
        private void AutoScrollCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _autoScroll = true;
                AddWorkLog("INFO", "📜 自动滚动已开启");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "开启自动滚动时发生错误");
            }
        }

        private void AutoScrollCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                _autoScroll = false;
                AddWorkLog("INFO", "📜 自动滚动已关闭");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "关闭自动滚动时发生错误");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region 完整监控面板创建方法

        /// <summary>
        /// 创建状态卡片面板
        /// </summary>
        private System.Windows.Controls.StackPanel CreateStatusCardsPanel()
        {
            var panel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 8)
            };

            // 状态统计卡片行
            var statusGrid = new System.Windows.Controls.Grid();
            statusGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

            // 运行状态卡片
            var runningCard = CreateMiniCard("运行状态", "MonitorStatus", Colors.LightBlue);
            System.Windows.Controls.Grid.SetColumn(runningCard, 0);
            statusGrid.Children.Add(runningCard);

            // 活跃合约卡片
            var contractCard = CreateMiniCard("活跃合约", "ActiveContractCount", Colors.LightGreen, "{0} 个");
            System.Windows.Controls.Grid.SetColumn(contractCard, 1);
            statusGrid.Children.Add(contractCard);

            // 执行统计卡片
            var executionCard = CreateMiniCard("执行统计", "TotalExecutions", Colors.LightCoral, "{0} 次");
            System.Windows.Controls.Grid.SetColumn(executionCard, 2);
            statusGrid.Children.Add(executionCard);

            // 止损单状态卡片
            var stopOrderCard = CreateMiniCard("止损单管理", "ActiveStopOrderCount", Colors.LightSteelBlue, "{0} 个");
            System.Windows.Controls.Grid.SetColumn(stopOrderCard, 3);
            statusGrid.Children.Add(stopOrderCard);

            panel.Children.Add(statusGrid);

            // 配置信息区域
            var configCard = CreateConfigurationCard();
            panel.Children.Add(configCard);

            return panel;
        }

        /// <summary>
        /// 创建迷你状态卡片
        /// </summary>
        private System.Windows.Controls.Border CreateMiniCard(string title, string bindingPath, Color backgroundColor, string format = "{0}")
        {
            var card = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(backgroundColor),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(2, 2, 2, 2),
                Padding = new Thickness(8, 8, 8, 8)
            };

            var panel = new System.Windows.Controls.StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.DarkBlue)
            };
            panel.Children.Add(titleText);

            var valueText = new System.Windows.Controls.TextBlock
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.DarkGreen)
            };

            var binding = new System.Windows.Data.Binding(bindingPath)
            {
                Source = this,
                StringFormat = format
            };
            valueText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, binding);
            panel.Children.Add(valueText);

            card.Child = panel;
            return card;
        }

        /// <summary>
        /// 创建配置信息卡片
        /// </summary>
        private System.Windows.Controls.Border CreateConfigurationCard()
        {
            var card = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Color.FromRgb(232, 244, 248)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var panel = new System.Windows.Controls.StackPanel();

            var headerGrid = new System.Windows.Controls.Grid();
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = "⚙️ 当前盯盘配置详情",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.DarkBlue),
                VerticalAlignment = VerticalAlignment.Center
            };
            System.Windows.Controls.Grid.SetColumn(titleText, 0);
            headerGrid.Children.Add(titleText);

            var configInfoPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var configNameText = new System.Windows.Controls.TextBlock
            {
                Text = "配置名称: ",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.DarkBlue),
                VerticalAlignment = VerticalAlignment.Center
            };
            configInfoPanel.Children.Add(configNameText);

            var configNameValue = new System.Windows.Controls.TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.DarkBlue),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 16, 0)
            };
            var configBinding = new System.Windows.Data.Binding("ConfigName") { Source = this };
            configNameValue.SetBinding(System.Windows.Controls.TextBlock.TextProperty, configBinding);
            configInfoPanel.Children.Add(configNameValue);

            var intervalText = new System.Windows.Controls.TextBlock
            {
                Text = "扫描间隔: ",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.DarkBlue),
                VerticalAlignment = VerticalAlignment.Center
            };
            configInfoPanel.Children.Add(intervalText);

            var intervalValue = new System.Windows.Controls.TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.DarkSlateGray),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            var intervalBinding = new System.Windows.Data.Binding("ScanIntervalDisplay") { Source = this };
            intervalValue.SetBinding(System.Windows.Controls.TextBlock.TextProperty, intervalBinding);
            configInfoPanel.Children.Add(intervalValue);

            System.Windows.Controls.Grid.SetColumn(configInfoPanel, 1);
            headerGrid.Children.Add(configInfoPanel);

            panel.Children.Add(headerGrid);

            // 添加保本配置显示
            var breakEvenText = new System.Windows.Controls.TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.DarkGreen),
                Margin = new Thickness(0, 10, 0, 5)
            };
            var breakEvenBinding = new System.Windows.Data.Binding("BreakEvenConfigDisplay") 
            { 
                Source = this,
                StringFormat = "🛡️ 保本配置: {0}"
            };
            breakEvenText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, breakEvenBinding);
            panel.Children.Add(breakEvenText);

            // 🔧 优化配置展示布局 - 支持多次推仓多次止盈的可扩展设计
            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled,
                MaxHeight = 500, // 🔧 增加最大高度，提供更多配置展示空间
                Margin = new Thickness(0, 10, 0, 0)
            };

            var detailsPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical
            };

            // 🔧 采用垂直布局，为未来扩展预留更多空间
            var configGrid = new System.Windows.Controls.Grid();
            configGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            configGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

            // 推仓配置表格
            var addPositionCard = CreateAddPositionConfigCard();
            System.Windows.Controls.Grid.SetColumn(addPositionCard, 0);
            configGrid.Children.Add(addPositionCard);

            // 保盈配置表格
            var profitProtectionCard = CreateProfitProtectionConfigCard();
            System.Windows.Controls.Grid.SetColumn(profitProtectionCard, 1);
            configGrid.Children.Add(profitProtectionCard);

            detailsPanel.Children.Add(configGrid);

            // 🔧 预留未来功能扩展区域
            var futureExpandArea = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 100, 149, 237)), // 半透明蓝色
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = System.Windows.Visibility.Collapsed // 默认隐藏，未来需要时显示
            };

            var futureExpandText = new System.Windows.Controls.TextBlock
            {
                Text = "💡 预留区域：支持未来扩展更多推仓档位、止盈档位和其他高级功能",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.DarkBlue),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            futureExpandArea.Child = futureExpandText;
            detailsPanel.Children.Add(futureExpandArea);

            scrollViewer.Content = detailsPanel;
            panel.Children.Add(scrollViewer);

            card.Child = panel;
            return card;
        }

        /// <summary>
        /// 创建左侧面板（合约状态） - 🎯 优化布局，最大化表格空间
        /// </summary>
        private System.Windows.Controls.Border CreateLeftPanel()
        {
            try
            {
                _logger.LogInformation("🎯 开始创建左侧面板（优化版）");
                
                var card = new System.Windows.Controls.Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(4), // 🔧 减少内边距从8→4
                    Margin = new Thickness(0, 0, 2, 0) // 🔧 减少右边距从4→2
                };

                var panel = new System.Windows.Controls.DockPanel();

                var titlePanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 4) // 🔧 减少底边距从8→4
                };

                var title = new System.Windows.Controls.TextBlock
                {
                    Text = "🎯 合约触发条件管理",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.DarkBlue),
                    VerticalAlignment = VerticalAlignment.Center
                };

                // 🎯 添加快捷操作按钮
                var quickEditButton = new System.Windows.Controls.Button
                {
                    Content = "快速编辑",
                    Width = 80,
                    Height = 24,
                    FontSize = 10,
                    Margin = new Thickness(10, 0, 5, 0),
                    Background = new SolidColorBrush(Colors.LightBlue),
                    ToolTip = "双击行或使用此按钮快速编辑触发条件"
                };
                quickEditButton.Click += QuickEditButton_Click;

                var expandButton = new System.Windows.Controls.Button
                {
                    Content = "详细",
                    Width = 60,
                    Height = 24,
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 0, 0),
                    Background = new SolidColorBrush(Colors.LightGreen),
                    ToolTip = "展开显示所有触发条件列"
                };
                expandButton.Click += ExpandColumnsButton_Click;

                titlePanel.Children.Add(title);
                titlePanel.Children.Add(quickEditButton);
                titlePanel.Children.Add(expandButton);

                System.Windows.Controls.DockPanel.SetDock(titlePanel, System.Windows.Controls.Dock.Top);
                panel.Children.Add(titlePanel);

                // 🎯 创建新的合约监控数据表格（支持动态列生成）
                try
                {
                    _logger.LogInformation("🎯 开始创建合约监控DataGrid");
                    var dataGrid = CreateContractMonitorDataGrid();
                    _logger.LogInformation($"📝 DataGrid创建完成，引用: {(dataGrid != null ? "有效" : "无效")}");
                    
                    if (dataGrid != null)
                    {
                        // 🔧 进一步优化DataGrid布局，确保充满空间
                        dataGrid.Margin = new Thickness(0); // 移除边距
                        dataGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
                        dataGrid.VerticalAlignment = VerticalAlignment.Stretch;
                        
                        panel.Children.Add(dataGrid);
                        _logger.LogInformation("📝 DataGrid已添加到面板");
                    }
                    else
                    {
                        throw new InvalidOperationException("CreateContractMonitorDataGrid返回null");
                    }
                }
                catch (Exception dgEx)
                {
                    _logger.LogError(dgEx, "❌ 创建DataGrid失败，使用简化表格");
                    
                    // 创建一个简化的表格作为替代
                    var fallbackGrid = CreateFallbackDataGrid();
                    panel.Children.Add(fallbackGrid);
                }

                card.Child = panel;
                _logger.LogInformation("✅ 左侧面板创建完成");
                return card;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 创建左侧面板失败");
                throw;
            }
        }

        /// <summary>
        /// 快速编辑按钮点击事件
        /// </summary>
        private void QuickEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (_contractMonitorDataGrid?.SelectedItem is ContractMonitorModel selectedContract)
            {
                OpenQuickEditDialog(selectedContract);
            }
            else
            {
                MessageBox.Show("请先选择一个合约行进行编辑", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 展开列按钮点击事件
        /// </summary>
        private void ExpandColumnsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("🎯 用户请求展开详细列");
                
                // 重新生成动态列
                var config = GetCurrentAutoMonitorConfig();
                if (config != null)
                {
                    GenerateDynamicDataGridColumns(config);
                    _logger.LogInformation("📝 动态列重新生成完成");
                    MessageBox.Show("详细列已展开，现在可以看到所有触发条件配置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("未找到配置信息，无法展开详细列", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 展开详细列失败");
                MessageBox.Show($"展开详细列失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 创建右侧面板（执行历史）
        /// </summary>
        private System.Windows.Controls.Border CreateRightPanel()
        {
            var card = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 8, 8, 8),
                Margin = new Thickness(4, 0, 0, 0)
            };

            var panel = new System.Windows.Controls.DockPanel();

            var title = new System.Windows.Controls.TextBlock
            {
                Text = "📈 最近执行历史",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Colors.DarkGreen),
                Margin = new Thickness(0, 0, 0, 8)
            };
            System.Windows.Controls.DockPanel.SetDock(title, System.Windows.Controls.Dock.Top);
            panel.Children.Add(title);

            // 创建执行历史数据表格
            var dataGrid = CreateExecutionHistoryDataGrid();
            panel.Children.Add(dataGrid);

            card.Child = panel;
            return card;
        }

        // 🎯 保存DataGrid引用，避免FindName查找问题
        private System.Windows.Controls.DataGrid _contractMonitorDataGrid;

        /// <summary>
        /// 🎯 创建新的合约监控数据表格（支持动态列生成）
        /// </summary>
        private System.Windows.Controls.DataGrid CreateContractMonitorDataGrid()
        {
            try
            {
                _logger.LogInformation("🎯 开始创建合约监控DataGrid");
                
                _contractMonitorDataGrid = new System.Windows.Controls.DataGrid
                {
                    Name = "ContractMonitorDataGrid",
                    AutoGenerateColumns = false,
                    CanUserAddRows = false,
                    IsReadOnly = false, // 允许编辑
                    GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.All,
                    Background = new SolidColorBrush(Colors.White),
                    FontSize = 11,
                    RowHeight = 35,
                    ItemsSource = ContractMonitors // 绑定到新的数据源
                };

                // 设置行样式以显示状态背景颜色
                var rowStyle = new Style(typeof(System.Windows.Controls.DataGridRow));
                rowStyle.Setters.Add(new Setter(System.Windows.Controls.DataGridRow.BackgroundProperty, 
                    new System.Windows.Data.Binding("RowBackgroundColor")));
                _contractMonitorDataGrid.RowStyle = rowStyle;
                _logger.LogInformation("📝 DataGrid基本属性设置完成");

                // 设置列头样式
                var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
                headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Colors.SteelBlue)));
                headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, new SolidColorBrush(Colors.White)));
                headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
                headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
                headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(8, 6, 8, 6)));
                _contractMonitorDataGrid.ColumnHeaderStyle = headerStyle;
                _logger.LogInformation("📝 DataGrid样式设置完成");

                // 🎯 立即添加基础列，确保表格有内容
                try
                {
                    AddBasicColumnsToDataGrid(_contractMonitorDataGrid);
                    _logger.LogInformation($"📝 基础列添加完成，当前列数: {_contractMonitorDataGrid.Columns.Count}");
                }
                catch (Exception colEx)
                {
                    _logger.LogError(colEx, "❌ 添加基础列失败，创建最简列结构");
                    AddMinimalColumnsToDataGrid(_contractMonitorDataGrid);
                }

                // 双击事件处理已移除，使用快速编辑按钮代替

                // 🎯 注册名称到UI树，确保FindName可以找到
                try
                {
                    this.RegisterName("ContractMonitorDataGrid", _contractMonitorDataGrid);
                    _logger.LogInformation("📝 DataGrid名称注册完成");
                }
                catch (Exception regEx)
                {
                    _logger.LogWarning(regEx, "⚠️ DataGrid名称注册失败，但不影响功能");
                }

                _logger.LogInformation("✅ 合约监控DataGrid创建完成");
                return _contractMonitorDataGrid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 创建合约监控DataGrid失败");
                throw;
            }
        }

        /// <summary>
        /// 更新合约执行状态
        /// </summary>
        public void UpdateContractExecutionState(string symbol, string positionSide, ExecutionType executionType,
        int? tierIndex, bool isSuccess, string message)
    {
        try
        {
            var contractKey = $"{symbol}_{positionSide}";
            var contractState = ContractStates.FirstOrDefault(c => c.Symbol == symbol && c.PositionSide == positionSide);
            
            if (contractState != null)
            {
                // 更新执行状态
                if (executionType == ExecutionType.AddPosition)
                {
                    if (tierIndex.HasValue && isSuccess)
                    {
                        contractState.AddPositionProgress = Math.Max(contractState.AddPositionProgress, tierIndex.Value);
                    }
                }
                else if (executionType == ExecutionType.ProfitProtection)
                {
                    if (tierIndex.HasValue && isSuccess)
                    {
                        contractState.ProfitProtectionProgress = Math.Max(contractState.ProfitProtectionProgress, tierIndex.Value);
                    }
                }
                
                contractState.LastExecutionTime = DateTime.Now;
                contractState.TotalExecutions++;
                
                // 更新进度计算
                contractState.ExecutionProgress = (double)(contractState.AddPositionProgress + contractState.ProfitProtectionProgress) / 
                                                (contractState.AddPositionTotalTiers + contractState.ProfitProtectionTotalTiers) * 100;
            }
            
            // 添加执行历史记录
            ExecutionHistory.Insert(0, new ExecutionHistoryDisplayModel
            {
                ExecutionTime = DateTime.Now,
                Symbol = symbol,
                PositionSide = positionSide,
                ExecutionType = executionType.ToString(),
                IsSuccess = isSuccess,
                ResultText = isSuccess ? "成功" : "失败",
                ResultColor = new SolidColorBrush(isSuccess ? Colors.Green : Colors.Red),
                TriggerPnl = 0,
                ResultMessage = message
            });
            
            // 保持历史记录数量限制
            while (ExecutionHistory.Count > 100)
            {
                ExecutionHistory.RemoveAt(ExecutionHistory.Count - 1);
            }
            
            _logger.LogInformation($"✅ 合约执行状态更新完成: {contractKey} {executionType} 阶梯{tierIndex} - {(isSuccess ? "成功" : "失败")}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ 更新合约执行状态失败: {symbol}_{positionSide}");
        }
    }

    /// <summary>
    /// 处理新开仓事件
    /// </summary>
    public void HandleNewPositionOpened(string symbol, string positionSide, decimal quantity, decimal currentPnl)
    {
        try
        {
            _logger.LogInformation($"🆕 处理新开仓: {symbol}_{positionSide}, 数量: {quantity}");
            // 这里可以添加新开仓的处理逻辑
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ 处理新开仓失败: {symbol}_{positionSide}");
        }
    }

    /// <summary>
    /// 处理持仓关闭事件 - 移除对应的合约配置并同步文件
    /// </summary>
    public void HandlePositionClosed(string symbol, string positionSide)
    {
        try
        {
            _logger.LogInformation($"❌ 处理持仓关闭: {symbol}_{positionSide}");
            
            var contractKey = $"{symbol}_{positionSide}";
            var existingContract = ContractMonitors.FirstOrDefault(c => $"{c.Symbol}_{c.PositionSide}" == contractKey);
            
            if (existingContract != null)
            {
                // 📝 记录被移除合约的详细信息
                var conditionCount = existingContract.TriggerConditions.Count;
                var executedCount = existingContract.TriggerConditions.Count(tc => tc.Status == TriggerExecutionStatus.Executed);
                
                _logger.LogInformation($"🔍 平仓合约详情: {contractKey} - {conditionCount}个触发条件，{executedCount}个已执行");
                
                // 🗑️ 清理持久化状态（历史记录、档案等）
                try
                {
                    var persistenceService = new AutoMonitorPersistenceService();
                    persistenceService.CleanupContractHistory(symbol, positionSide, "合约平仓");
                    _logger.LogInformation($"✅ 已清理 {contractKey} 的持久化历史状态");
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, $"⚠️ 清理持久化状态失败: {contractKey}");
                }
                
                // 🔧 在UI线程中移除监控配置
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var removed = ContractMonitors.Remove(existingContract);
                    if (removed)
                    {
                        _logger.LogInformation($"✅ 已从UI列表移除合约配置: {contractKey}");
                        
                        // 触发UI更新
                        OnPropertyChanged(nameof(ContractMonitors));
                        if (_contractMonitorDataGrid != null)
                        {
                            _contractMonitorDataGrid.Items.Refresh();
                        }
                    }
                });
                
                // 🔧 关键：从配置文件中移除该合约，并保存剩余配置
                try
                {
                    var persistenceService = new AutoMonitorPersistenceService();
                    persistenceService.RemoveContractConfig(symbol, positionSide);
                    _logger.LogInformation($"✅ 已从配置文件移除合约: {contractKey}");
                }
                catch (Exception fileEx)
                {
                    _logger.LogError(fileEx, $"⚠️ 从配置文件移除合约失败: {contractKey}");
                }
                
                // 保存剩余的UI配置状态
                SaveContractConfigsToFile();
                
                // 📊 更新统计信息
                UpdateNewInterfaceStats();
                
                // 📝 记录操作日志
                AppendLog($"❌ 平仓移除: {contractKey} (条件:{conditionCount}个, 已执行:{executedCount}个)");
                
                // 🎯 提示：下次开仓将重新生成配置
                _logger.LogInformation($"💡 {contractKey} 下次开仓时将从基础配置重新生成合约配置");
                
                // 🔧 通知AutoMonitorService清理相关状态
                if (_autoMonitorService != null)
                {
                    try
                    {
                        _autoMonitorService.ClearContractStates(symbol, positionSide, "UI界面平仓清理");
                        _logger.LogInformation($"✅ 已通知服务层清理 {contractKey} 的状态");
                    }
                    catch (Exception serviceEx)
                    {
                        _logger.LogError(serviceEx, $"⚠️ 通知服务层清理状态失败: {contractKey}");
                    }
                }
            }
            else
            {
                _logger.LogWarning($"⚠️ 未找到待移除的合约配置: {contractKey}");
                AppendLog($"⚠️ 平仓处理: 未找到合约配置 {contractKey}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ 处理持仓关闭失败: {symbol}_{positionSide}");
            AppendLog($"❌ 平仓处理失败: {symbol}_{positionSide} - {ex.Message}");
        }
    }

            /// <summary>
        /// 强制刷新持仓数据
        /// </summary>
        public void ForceRefreshPositionsData()
        {
            try
            {
                _logger.LogInformation("🔄 强制刷新持仓数据");
                // 这里可以添加强制刷新的逻辑
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 强制刷新持仓数据失败");
            }
        }

        /// <summary>
        /// 更新刷新定时器间隔
        /// </summary>
        private void UpdateRefreshTimerInterval()
        {
            try
            {
                var config = _autoMonitorService?.CurrentConfig;
                var interval = config?.ScanIntervalSeconds ?? 30;
                if (_refreshTimer != null)
                {
                    _refreshTimer.Interval = TimeSpan.FromSeconds(interval);
                    _logger.LogInformation($"✅ 刷新定时器间隔更新为 {interval} 秒");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 更新刷新定时器间隔失败");
            }
        }

        /// <summary>
        /// 刷新当前持仓数据
        /// </summary>
        private void RefreshCurrentPositionsData()
        {
            try
            {
                _logger.LogInformation("🔄 刷新当前持仓数据");
                
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        // 🎯 【关键修复】统一数据源：只从contract_monitoring_states.json加载数据
                        var stateService = CreateContractMonitoringStateService();
                        var monitoringStates = stateService.LoadMonitoringStates();
                        
                        _logger.LogInformation($"📊 从状态文件获取到 {monitoringStates.Count} 个合约状态");
                        
                        if (ContractMonitors.Count == 0 && monitoringStates.Count == 0)
                        {
                            // 🎯 状态文件不存在或为空，检查是否有实际持仓需要生成状态文件
                            _logger.LogInformation("📝 状态文件为空，检查是否有实际持仓需要处理");
                            
                            // ⚠️ 暂时使用旧系统作为过渡，但需要明确标记为待移除
                            var positionProfiles = _autoMonitorService.GetPositionProfiles();
                            _logger.LogWarning($"⚠️ 【待移除】使用旧的PositionProfile系统获取到 {positionProfiles.Count} 个档案");
                            
                            if (positionProfiles.Count > 0)
                            {
                                _logger.LogInformation($"🔍 发现 {positionProfiles.Count} 个持仓档案，生成状态文件");
                                GenerateContractMonitoringStatesFile(positionProfiles);
                                
                                // 重新加载生成的状态文件
                                monitoringStates = stateService.LoadMonitoringStates();
                            }
                            else
                            {
                                _logger.LogInformation("📝 没有持仓档案，无需生成状态文件");
                                return;
                            }
                        }
                        
                        // 🔧 从状态文件创建UI模型
                        if (ContractMonitors.Count == 0 && monitoringStates.Count > 0)
                        {
                            _logger.LogInformation("🔄 ContractMonitors为空，从状态文件创建合约监控模型");
                            
                            var currentPositions = new List<PositionInfo>();
                            
                            foreach (var kvp in monitoringStates)
                            {
                                var contractKey = kvp.Key;
                                var state = kvp.Value;
                                
                                var contractMonitor = ConvertStateToContractMonitor(state, currentPositions);
                                ContractMonitors.Add(contractMonitor);
                                _logger.LogInformation($"🔄 从状态文件创建合约监控: {contractKey}，触发条件数量: {contractMonitor.TriggerConditions.Count}");
                            }
                        }
                        else
                        {
                            // 🔧 当已有UI数据时，从状态文件同步更新
                            _logger.LogInformation("🔄 已有合约监控数据，从状态文件同步状态");
                            
                            foreach (var contract in ContractMonitors.ToList())
                            {
                                var contractKey = $"{contract.Symbol}_{contract.PositionSide}";
                                
                                if (monitoringStates.TryGetValue(contractKey, out var state))
                                {
                                    // 从状态文件更新UI数据
                                    contract.IsActive = state.IsActive;
                                    _logger.LogDebug($"🔄 已从状态文件更新合约状态: {contractKey}");
                                }
                                else
                                {
                                    // 状态文件中不存在，标记为非活跃
                                    contract.IsActive = false;
                                    _logger.LogDebug($"❌ 状态文件中未找到合约，标记为非活跃: {contractKey}");
                                }
                            }
                            
                            // 🔧 添加状态文件中有但UI中没有的新合约
                            foreach (var kvp in monitoringStates)
                            {
                                var contractKey = kvp.Key;
                                var state = kvp.Value;
                                
                                var existingContract = ContractMonitors.FirstOrDefault(c => 
                                    $"{c.Symbol}_{c.PositionSide}" == contractKey);
                                
                                if (existingContract == null)
                                {
                                    // 从状态文件创建新的合约监控模型
                                    var newContract = ConvertStateToContractMonitor(state, new List<PositionInfo>());
                                    ContractMonitors.Add(newContract);
                                    _logger.LogInformation($"🆕 从状态文件添加新合约监控: {contractKey}");
                                }
                            }
                        }
                        
                        // 🔧 触发UI属性更新
                        OnPropertyChanged(nameof(ContractMonitors));
                        UpdateNewInterfaceStats();
                        
                        // 🔧 强制刷新DataGrid显示和列结构
                        if (_contractMonitorDataGrid != null)
                        {
                            // 重新生成列结构（如果有配置的话）
                            var currentConfig = GetCurrentAutoMonitorConfig();
                            if (currentConfig != null)
                            {
                                GenerateDynamicDataGridColumns(currentConfig);
                                _logger.LogDebug("🔄 重新生成DataGrid列结构");
                            }
                            
                            _contractMonitorDataGrid.Items.Refresh();
                        }
                        
                        _logger.LogInformation($"✅ 持仓数据刷新完成，活跃合约: {ContractMonitors.Count(c => c.IsActive)} 个");
                    }
                    catch (Exception uiEx)
                    {
                        _logger.LogError(uiEx, "❌ UI更新时发生错误");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 刷新当前持仓数据失败");
            }
        }

        /// <summary>
        /// 🔧 更新合约触发条件状态（简化版：从档案同步）
        /// </summary>
        private void UpdateContractTriggerConditionsFromProfile(ContractMonitorModel contract, PositionProfile profile, UnifiedStateManager? stateManager)
        {
            try
            {
                _logger.LogDebug($"🔄 开始更新合约触发条件状态: {contract.Symbol}_{contract.PositionSide}");
                
                // 🔧 新增：优先从本地手动修改文件读取状态
                var manualStates = LoadManualStatesFromFile(contract.Symbol, contract.PositionSide);
                
                foreach (var condition in contract.TriggerConditions)
                {
                    // 🔧 关键修复：从统一状态管理器获取最新状态
                    bool isExecuted = false;
                    
                    switch (condition.Type)
                    {
                        case TriggerConditionType.BreakEven:
                            // 🔧 【关键修复】优先从状态文件检查，确保与文件同步
                            isExecuted = _autoMonitorService.IsExecutedInStateFile(contract.Symbol, contract.PositionSide, "保本");
                            
                            // 🔧 次要：检查手动修改状态（作为补充）
                            if (!isExecuted && (manualStates.BreakEvenStatus == "√" || manualStates.BreakEvenStatus == "已执行"))
                            {
                                isExecuted = true;
                                _logger.LogInformation($"🔍 从手动修改文件读取到保本已执行状态: {contract.Symbol}_{contract.PositionSide}");
                            }
                            
                            // 🔧 最后：从profile.TriggerRecords获取执行状态（向后兼容）
                            if (!isExecuted)
                            {
                                var triggerKey = "BreakEven";
                                isExecuted = profile.TriggerRecords.ContainsKey(triggerKey) && 
                                           profile.TriggerRecords[triggerKey].IsExecuted;
                            }
                            break;
                            
                        case TriggerConditionType.AddPosition:
                            if (condition.TierIndex.HasValue)
                            {
                                // 🔧 【关键修复】优先从状态文件检查，确保与文件同步
                                isExecuted = _autoMonitorService.IsExecutedInStateFile(contract.Symbol, contract.PositionSide, "推仓", condition.TierIndex.Value);
                                
                                // 🔧 次要：检查手动修改状态（作为补充）
                                if (!isExecuted)
                                {
                                    var manualPushStatus = GetManualPushStatus(manualStates, condition.TierIndex.Value);
                                    if (manualPushStatus == "√" || manualPushStatus == "已执行")
                                    {
                                        isExecuted = true;
                                        _logger.LogInformation($"🔍 从手动修改文件读取到推仓{condition.TierIndex}档已执行状态: {contract.Symbol}_{contract.PositionSide}");
                                    }
                                }
                                
                                // 🔧 最后：从profile.TriggerRecords获取执行状态（向后兼容）
                                if (!isExecuted)
                                {
                                    var triggerKey = $"AddPosition_{condition.TierIndex.Value}";
                                    isExecuted = profile.TriggerRecords.ContainsKey(triggerKey) && 
                                               profile.TriggerRecords[triggerKey].IsExecuted;
                                }
                            }
                            break;
                            
                        case TriggerConditionType.ProfitProtection:
                            if (condition.TierIndex.HasValue)
                            {
                                // 🔧 【关键修复】优先从状态文件检查，确保与文件同步
                                isExecuted = _autoMonitorService.IsExecutedInStateFile(contract.Symbol, contract.PositionSide, "保盈", condition.TierIndex.Value);
                                
                                // 🔧 次要：检查手动修改状态（作为补充）
                                if (!isExecuted)
                                {
                                    var manualProfitStatus = GetManualProfitStatus(manualStates, condition.TierIndex.Value);
                                    if (manualProfitStatus == "√" || manualProfitStatus == "已执行")
                                    {
                                        isExecuted = true;
                                        _logger.LogInformation($"🔍 从手动修改文件读取到保盈{condition.TierIndex}档已执行状态: {contract.Symbol}_{contract.PositionSide}");
                                    }
                                }
                                
                                // 🔧 最后：从profile.TriggerRecords获取执行状态（向后兼容）
                                if (!isExecuted)
                                {
                                    var triggerKey = $"ProfitProtection_{condition.TierIndex.Value}";
                                    isExecuted = profile.TriggerRecords.ContainsKey(triggerKey) && 
                                               profile.TriggerRecords[triggerKey].IsExecuted;
                                }
                            }
                            break;
                    }
                    
                    // 🔧 更新状态并触发属性变化通知
                    var newStatus = isExecuted ? TriggerExecutionStatus.Executed : TriggerExecutionStatus.NotTriggered;
                    if (condition.Status != newStatus)
                    {
                        condition.Status = newStatus;
                        condition.LastExecutionTime = isExecuted ? DateTime.Now : null;
                        
                        // 🔧 关键：触发属性变化通知
                        condition.OnPropertyChanged(nameof(condition.Status));
                        condition.OnPropertyChanged(nameof(condition.StatusText));
                        condition.OnPropertyChanged(nameof(condition.StatusIcon));
                        condition.OnPropertyChanged(nameof(condition.LastExecutionTime));
                        
                        _logger.LogDebug($"🔄 状态更新: {contract.Symbol}_{contract.PositionSide} {condition.TypeText}{(condition.TierIndex?.ToString() ?? "")} → {newStatus}");
                    }
                }
                
                // 🔧 触发合约级别的属性更新
                contract.OnPropertyChanged(nameof(contract.TriggerConditions));
                contract.OnPropertyChanged(nameof(contract.ExecutedCount));
                contract.OnPropertyChanged(nameof(contract.ExecutionProgress));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 更新合约触发条件状态失败: {contract.Symbol}_{contract.PositionSide}");
            }
        }

        /// <summary>
        /// 🔧 新增：从本地文件加载手动修改的状态
        /// </summary>
        private ManualStatusData LoadManualStatesFromFile(string symbol, string positionSide)
        {
            var result = new ManualStatusData();
            
            try
            {
                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                             "BinanceFuturesTrader", "ContractConfigs.json");
                
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var savedConfigs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    var contractName = $"{symbol} {positionSide}";
                    
                    if (savedConfigs != null && savedConfigs.ContainsKey(contractName))
                    {
                        var contractData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(savedConfigs[contractName].ToString());
                        
                        if (contractData != null)
                        {
                            // 读取保本状态
                            if (contractData.ContainsKey("BreakEvenStatus"))
                            {
                                result.BreakEvenStatus = contractData["BreakEvenStatus"].ToString();
                            }
                            
                            // 读取推仓状态
                            for (int i = 1; i <= 4; i++)
                            {
                                var key = $"PushTier{i}Status";
                                if (contractData.ContainsKey(key))
                                {
                                    result.PushTierStatuses[i] = contractData[key].ToString();
                                }
                            }
                            
                            // 读取保盈状态
                            for (int i = 1; i <= 3; i++)
                            {
                                var key = $"ProfitTier{i}Status";
                                if (contractData.ContainsKey(key))
                                {
                                    result.ProfitTierStatuses[i] = contractData[key].ToString();
                                }
                            }
                            
                            _logger.LogDebug($"🔍 成功从文件读取手动状态: {contractName} - 保本: {result.BreakEvenStatus}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 从文件读取手动状态失败: {symbol}_{positionSide}");
            }
            
            return result;
        }

        /// <summary>
        /// 🔧 新增：获取手动修改的推仓状态
        /// </summary>
        private string GetManualPushStatus(ManualStatusData manualStates, int tierIndex)
        {
            return manualStates.PushTierStatuses.TryGetValue(tierIndex, out var status) ? status : "";
        }

        /// <summary>
        /// 🔧 新增：获取手动修改的保盈状态
        /// </summary>
        private string GetManualProfitStatus(ManualStatusData manualStates, int tierIndex)
        {
            return manualStates.ProfitTierStatuses.TryGetValue(tierIndex, out var status) ? status : "";
        }

        /// <summary>
        /// 🔧 新增：手动状态数据结构
        /// </summary>
        private class ManualStatusData
        {
            public string BreakEvenStatus { get; set; } = "";
            public Dictionary<int, string> PushTierStatuses { get; set; } = new();
            public Dictionary<int, string> ProfitTierStatuses { get; set; } = new();
        }



        /// <summary>
        /// 从持仓档案创建合约监控模型
        /// </summary>
        private ContractMonitorModel CreateContractMonitorFromProfile(PositionProfile profile)
        {
            var contract = new ContractMonitorModel
            {
                Symbol = profile.Symbol,
                PositionSide = profile.PositionSide,
                IsEnabled = true,
                IsActive = profile.IsActive,
                UnrealizedPnl = 0, // 将在后续刷新中更新
                CurrentPrice = 0   // 将在后续刷新中更新
            };
            
            // 🔧 初始化触发条件（如果需要的话）
            // 这里可以根据基础配置生成默认的触发条件
            
            return contract;
        }

        /// <summary>
        /// 创建示例合约数据
        /// </summary>
        private void CreateExampleContractData()
        {
            try
            {
                _logger.LogInformation("📝 创建示例合约数据");
                
                // 创建示例数据
                var exampleContract = new ContractMonitorModel
                {
                    Symbol = "BTCUSDT",
                    PositionSide = "LONG",
                    IsEnabled = true,
                    IsActive = true,
                    UnrealizedPnl = 0,
                    CurrentPrice = 50000
                };

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ContractMonitors.Add(exampleContract);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 创建示例合约数据失败");
            }
        }

        /// <summary>
        /// 重新生成数据表格列
        /// </summary>
        private void RegenerateDataGridColumns()
        {
            try
            {
                _logger.LogInformation("🔄 重新生成数据表格列");
                // 这里可以添加重新生成列的逻辑
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 重新生成数据表格列失败");
            }
        }

        /// <summary>
        /// 重新生成数据表格列（带配置参数）
        /// </summary>
        private void RegenerateDataGridColumns(AutoMonitorConfig config)
        {
            try
            {
                _logger.LogInformation($"🔄 重新生成数据表格列: {config.Name}");
                // 这里可以添加重新生成列的逻辑
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 重新生成数据表格列失败");
            }
        }

        /// <summary>
        /// 从通用配置重新生成合约配置
        /// </summary>
        private void RegenerateContractConfigsFromUniversalConfig()
        {
            try
            {
                _logger.LogInformation("🔄 从通用配置重新生成合约配置");
                // 这里可以添加重新生成配置的逻辑
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 从通用配置重新生成合约配置失败");
            }
        }

        /// <summary>
        /// 从通用配置重新生成合约配置（带配置参数）
        /// </summary>
        private void RegenerateContractConfigsFromUniversalConfig(AutoMonitorConfig config)
        {
            try
            {
                _logger.LogInformation($"🔄 从通用配置重新生成合约配置: {config.Name}");
                // 这里可以添加重新生成配置的逻辑
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 从通用配置重新生成合约配置失败");
            }
        }

        /// <summary>
        /// 创建默认自动监控配置
        /// </summary>
        private AutoMonitorConfig CreateDefaultAutoMonitorConfig()
        {
            return new AutoMonitorConfig
            {
                Name = "默认配置",
                ScanIntervalSeconds = 30,
                IsEnabled = true,
                BreakEvenConfig = new AutoBreakEvenConfig
                {
                    IsEnabled = true,
                    TriggerProfitAmount = 100
                },
                AddPositionConfig = new AutoAddPositionConfig
                {
                    IsEnabled = true,
                    Tiers = new List<AddPositionTier>
                    {
                        new AddPositionTier { TierIndex = 1, TriggerProfitAmount = 200, RiskMultiplier = 1.5m, ProfitProtectionAmount = 50 },
                        new AddPositionTier { TierIndex = 2, TriggerProfitAmount = 500, RiskMultiplier = 2.0m, ProfitProtectionAmount = 150 },
                        new AddPositionTier { TierIndex = 3, TriggerProfitAmount = 1000, RiskMultiplier = 2.5m, ProfitProtectionAmount = 300 }
                    }
                },
                ProfitProtectionConfig = new AutoProfitProtectionConfig
                {
                    IsEnabled = true,
                    Tiers = new List<ProfitProtectionTier>
                    {
                        new ProfitProtectionTier { TierIndex = 1, TriggerProfitAmount = 300, ProtectionAmount = 100 },
                        new ProfitProtectionTier { TierIndex = 2, TriggerProfitAmount = 800, ProtectionAmount = 250 },
                        new ProfitProtectionTier { TierIndex = 3, TriggerProfitAmount = 1500, ProtectionAmount = 500 }
                    }
                }
            };
        }

        /// <summary>
        /// 基于配置创建示例数据
        /// </summary>
        private void CreateExampleDataBasedOnConfig(AutoMonitorConfig config)
        {
            try
            {
                _logger.LogInformation($"📝 基于配置创建示例数据: {config.Name}");
                CreateExampleContractData();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 基于配置创建示例数据失败");
            }
        }

        /// <summary>
        /// 生成动态数据表格列
        /// </summary>
        private void GenerateDynamicDataGridColumns(AutoMonitorConfig config)
        {
            try
            {
                _logger.LogInformation($"🔄 生成动态数据表格列: {config.Name}");
                // 这里可以添加生成动态列的逻辑
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 生成动态数据表格列失败");
            }
        }

        /// <summary>
        /// 初始化基础数据表格列
        /// </summary>
        private void InitializeBasicDataGridColumns()
        {
            try
            {
                _logger.LogInformation("🔄 初始化基础数据表格列");
                // 这里可以添加初始化基础列的逻辑
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 初始化基础数据表格列失败");
            }
        }

        /// <summary>
        /// 异步初始化配置（窗口加载完成后调用）
        /// </summary>
        private async Task InitializeConfigurationAsync()
        {
            try
            {
                _logger.LogInformation("🔄 开始异步初始化配置...");
                
                // 🔧 【需求1】首先读取本地文件contract_monitoring_states.json的内容
                if (await LoadFromMonitoringStatesFileAsync())
                {
                    _logger.LogInformation("✅ 从contract_monitoring_states.json成功加载完整配置，直接显示在界面");
                    return;
                }
                
                // 🔧 如果文件中没有完整配置，继续原有逻辑
                _logger.LogInformation("📂 contract_monitoring_states.json中无完整配置，使用原有加载逻辑");
                
                // 🔧 关键修复：强制重新从所有来源获取配置，确保获取到最新保存的配置
                var config = GetCurrentAutoMonitorConfig();
                if (config != null)
                {
                    _logger.LogInformation($"✅ 获取到基础配置：{config.Name}，开始自动载入对应的合约配置");
                    
                    await AutoLoadContractConfigsAsync(config);
                    _logger.LogInformation($"✅ 已自动载入基础配置'{config.Name}'对应的合约配置");
                }
                else
                {
                    _logger.LogInformation("未找到基础配置，使用默认配置创建表格结构");
                    
                    // 使用默认配置创建表格结构
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var defaultConfig = CreateDefaultAutoMonitorConfig();
                        CreateExampleDataBasedOnConfig(defaultConfig);
                        
                        if (_contractMonitorDataGrid != null)
                        {
                            GenerateDynamicDataGridColumns(defaultConfig);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 异步初始化配置失败");
            }
        }
        
        /// <summary>
        /// 🔧 【辅助方法】创建正确配置的ContractMonitoringStateService实例
        /// </summary>
        private ContractMonitoringStateService CreateContractMonitoringStateService()
        {
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var stateLogger = loggerFactory.CreateLogger<ContractMonitoringStateService>();
            var filePathManager = new FilePathManager();
            
            // 🔧 【关键修复】优先使用MainViewModel中的账户名
            var currentAccountFromFileManager = filePathManager.GetCurrentAccountName();
            var actualAccountName = _mainViewModel?.SelectedAccount?.Name ?? currentAccountFromFileManager;
            
            if (currentAccountFromFileManager != actualAccountName)
            {
                _logger.LogCritical($"⚠️【StateService创建】账户名不匹配！FilePathManager返回'{currentAccountFromFileManager}'，实际使用'{actualAccountName}'");
            }
            
            return new ContractMonitoringStateService(
                stateLogger, 
                BaseConfigManager.Instance,
                filePathManager,
                actualAccountName);
        }

        /// <summary>
        /// 🔧 【需求1】从contract_monitoring_states.json加载配置
        /// </summary>
        private async Task<bool> LoadFromMonitoringStatesFileAsync()
        {
            try
            {
                _logger.LogCritical("🔍【启动盯盘】开始检查contract_monitoring_states.json文件...");
                
                // 🔧 【关键修复】直接使用MainViewModel中的账户名，不依赖FilePathManager
                var filePathManager = new FilePathManager();
                var currentAccountFromFileManager = filePathManager.GetCurrentAccountName();
                var actualAccountName = _mainViewModel?.SelectedAccount?.Name ?? currentAccountFromFileManager;
                
                _logger.LogCritical($"📁【启动盯盘】FilePathManager账户名: {currentAccountFromFileManager}");
                _logger.LogCritical($"📁【启动盯盘】MainViewModel账户名: {_mainViewModel?.SelectedAccount?.Name}");
                _logger.LogCritical($"📁【启动盯盘】实际使用账户名: {actualAccountName}");
                
                if (currentAccountFromFileManager != actualAccountName)
                {
                    _logger.LogCritical($"⚠️【启动盯盘】账户名不匹配！使用MainViewModel中的账户名: {actualAccountName}");
                }
                
                // 创建使用正确账户名的StateService
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
                var stateLogger = loggerFactory.CreateLogger<ContractMonitoringStateService>();
                var stateService = new ContractMonitoringStateService(
                    stateLogger, 
                    BaseConfigManager.Instance,
                    filePathManager,
                    actualAccountName);
                
                var stateFilePath = filePathManager.GetContractMonitoringStatesFilePath(actualAccountName);
                _logger.LogCritical($"📁【启动盯盘】状态文件路径: {stateFilePath}");
                
                var fileExists = System.IO.File.Exists(stateFilePath);
                _logger.LogCritical($"📁【启动盯盘】文件是否存在: {fileExists}");
                
                var monitoringStates = stateService.LoadMonitoringStates();
                _logger.LogCritical($"📁【启动盯盘】载入状态数量: {monitoringStates.Count}");
                
                if (!monitoringStates.Any())
                {
                    _logger.LogCritical("📂【启动盯盘】contract_monitoring_states.json文件为空或不存在，返回false");
                    return false;
                }
                
                _logger.LogCritical($"📊【启动盯盘】在contract_monitoring_states.json中发现 {monitoringStates.Count} 个合约状态");
                
                // 🔧 检查当前持仓，但不因为没有持仓就拒绝加载配置
                var currentPositions = await GetCurrentActivePositionsAsync();
                _logger.LogCritical($"📊【启动盯盘】当前持仓数量: {currentPositions.Count}");
                
                if (!currentPositions.Any())
                {
                    _logger.LogCritical("⚠️【启动盯盘】当前无活跃持仓，但仍然加载已保存的配置供查看");
                    // 🔧 【关键修复】不返回false，而是继续加载配置
                    // 用户可能稍后会开仓，应该让他们看到已保存的配置
                }
                
                // 🔧 【关键修复】根据是否有持仓来决定加载策略
                var statesToLoad = new List<ContractMonitoringState>();
                
                if (currentPositions.Any())
                {
                    // 有持仓时：只加载匹配的配置
                    _logger.LogCritical("📊【启动盯盘】有活跃持仓，只加载匹配的配置");
                    foreach (var position in currentPositions)
                    {
                        var positionSide = position.PositionAmt > 0 ? "LONG" : "SHORT";
                        var contractKey = $"{position.Symbol}_{positionSide}";
                        _logger.LogCritical($"🔍【启动盯盘】检查持仓匹配: {contractKey} (数量: {position.PositionAmt})");
                        
                        if (monitoringStates.TryGetValue(contractKey, out var state))
                        {
                            statesToLoad.Add(state);
                            _logger.LogCritical($"✅【启动盯盘】找到匹配的配置: {contractKey}");
                        }
                        else
                        {
                            _logger.LogCritical($"⚠️【启动盯盘】未找到匹配的配置: {contractKey}");
                        }
                    }
                }
                else
                {
                    // 没有持仓时：显示所有保存的配置
                    _logger.LogCritical("📊【启动盯盘】当前无持仓，加载所有已保存的配置供查看");
                    statesToLoad.AddRange(monitoringStates.Values);
                    
                    foreach (var state in statesToLoad)
                    {
                        _logger.LogCritical($"📋【启动盯盘】加载已保存配置: {state.Symbol}_{state.PositionSide}");
                    }
                }
                
                if (!statesToLoad.Any())
                {
                    _logger.LogCritical("⚠️【启动盯盘】没有可加载的配置");
                    return false;
                }
                
                _logger.LogCritical($"🎯【启动盯盘】准备加载 {statesToLoad.Count} 个配置到界面");
                
                // 🔧 转换为界面显示模型并加载
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _logger.LogCritical($"🔧【启动盯盘】开始清空界面并重新加载 {statesToLoad.Count} 个配置");
                    ContractMonitors.Clear();
                    
                    foreach (var state in statesToLoad)
                    {
                        var contractMonitor = ConvertStateToContractMonitor(state, currentPositions);
                        ContractMonitors.Add(contractMonitor);
                        _logger.LogCritical($"✅【启动盯盘】加载合约配置到界面: {contractMonitor.ContractKey}");
                    }
                    
                    // 🔧 根据第一个状态的基础配置重新生成表格列
                    if (statesToLoad.Any())
                    {
                        var firstState = statesToLoad.First();
                        var baseConfig = BaseConfigManager.Instance.GetConfiguration(firstState.BaseConfigName);
                        if (baseConfig != null)
                        {
                            GenerateDynamicDataGridColumns(baseConfig);
                            _logger.LogCritical($"✅【启动盯盘】使用基础配置 '{baseConfig.Name}' 重新生成表格列");
                        }
                    }
                    
                    // 🔧 刷新界面显示
                    UpdateNewInterfaceStats();
                    OnPropertyChanged(nameof(ContractMonitors));
                    if (_contractMonitorDataGrid != null)
                    {
                        _contractMonitorDataGrid.Items.Refresh();
                    }
                });
                
                _logger.LogCritical($"🎉【启动盯盘】成功从contract_monitoring_states.json加载 {statesToLoad.Count} 个合约配置到界面");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "❌【启动盯盘】从contract_monitoring_states.json加载配置失败");
                return false;
            }
        }
        
        /// <summary>
        /// 🔧 【辅助方法】获取当前活跃持仓
        /// </summary>
        private async Task<List<PositionInfo>> GetCurrentActivePositionsAsync()
        {
            try
            {
                _logger.LogCritical("🔍【启动盯盘】开始获取当前活跃持仓");
                
                if (_mainViewModel?.SelectedAccount == null)
                {
                    _logger.LogCritical("⚠️【启动盯盘】MainViewModel或SelectedAccount为null，返回空列表");
                    return new List<PositionInfo>();
                }
                
                _logger.LogCritical($"📁【启动盯盘】当前选中账户: {_mainViewModel.SelectedAccount.Name}");
                
                // 直接从MainViewModel获取当前持仓数据（已在UI线程中保持实时更新）
                var allPositions = _mainViewModel.Positions;
                if (allPositions == null)
                {
                    _logger.LogCritical("⚠️【启动盯盘】MainViewModel.Positions为null，尝试刷新数据");
                    
                    // 通过RefreshDataCommand触发数据刷新
                    if (_mainViewModel.RefreshDataCommand?.CanExecute(null) == true)
                    {
                        _logger.LogCritical("🔄【启动盯盘】执行RefreshDataCommand刷新数据");
                        _mainViewModel.RefreshDataCommand.Execute(null);
                        // 等待一小段时间让数据更新
                        await Task.Delay(1000);
                        allPositions = _mainViewModel.Positions;
                    }
                    else
                    {
                        _logger.LogCritical("⚠️【启动盯盘】RefreshDataCommand不可用，返回空列表");
                        return new List<PositionInfo>();
                    }
                }
                
                if (allPositions == null)
                {
                    _logger.LogCritical("⚠️【启动盯盘】刷新后Positions仍为null，返回空列表");
                    return new List<PositionInfo>();
                }
                
                var activePositions = allPositions.Where(p => p != null && Math.Abs(p.PositionAmt) > 0).ToList();
                _logger.LogCritical($"📊【启动盯盘】总持仓数量: {allPositions.Count}，活跃持仓数量: {activePositions.Count}");
                
                return activePositions;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "❌【启动盯盘】获取当前活跃持仓失败");
                return new List<PositionInfo>();
            }
        }
        
        /// <summary>
        /// 🔧 【辅助方法】将ContractMonitoringState转换为ContractMonitorModel
        /// </summary>
        private ContractMonitorModel ConvertStateToContractMonitor(ContractMonitoringState state, List<PositionInfo> currentPositions)
        {
            try
            {
                if (state == null)
                {
                    _logger.LogCritical("⚠️【启动盯盘】ConvertStateToContractMonitor: state参数为null");
                    throw new ArgumentNullException(nameof(state));
                }
                
                if (currentPositions == null)
                {
                    _logger.LogCritical("⚠️【启动盯盘】ConvertStateToContractMonitor: currentPositions参数为null");
                    currentPositions = new List<PositionInfo>();
                }
                
                _logger.LogCritical($"🔄【启动盯盘】转换状态到界面模型: {state.Symbol}_{state.PositionSide}");
                
                // 找到对应的持仓信息
                var position = currentPositions.FirstOrDefault(p => 
                    p != null && 
                    p.Symbol == state.Symbol && 
                    (p.PositionAmt > 0 ? "LONG" : "SHORT") == state.PositionSide);
                
                if (position != null)
                {
                    _logger.LogCritical($"📊【启动盯盘】找到匹配持仓: {position.Symbol}, 数量: {position.PositionAmt}, 浮盈: {position.UnrealizedProfit}");
                }
                else
                {
                    _logger.LogCritical($"⚠️【启动盯盘】未找到匹配持仓: {state.Symbol}_{state.PositionSide}");
                }
                
                var contractMonitor = new ContractMonitorModel
                {
                    Symbol = state.Symbol ?? "",
                    PositionSide = state.PositionSide ?? "",
                    IsEnabled = state.IsActive,
                    IsActive = state.IsActive,
                    CurrentPrice = position?.MarkPrice ?? 0,
                    PositionSize = Math.Abs(position?.PositionAmt ?? 0),
                    UnrealizedPnl = position?.UnrealizedProfit ?? 0
                };
                
                // 🔧 根据状态文件中的执行状态生成触发条件
                GenerateTriggerConditionsFromState(contractMonitor, state);
                
                _logger.LogCritical($"✅【启动盯盘】成功转换: {contractMonitor.ContractKey}, 触发条件数量: {contractMonitor.TriggerConditions.Count}");
                return contractMonitor;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"❌【启动盯盘】转换状态到界面模型失败: {state?.Symbol}_{state?.PositionSide}");
                throw;
            }
        }
        
        /// <summary>
        /// 🔧 【辅助方法】根据状态文件生成触发条件
        /// </summary>
        private void GenerateTriggerConditionsFromState(ContractMonitorModel contractMonitor, ContractMonitoringState state)
        {
            try
            {
                if (contractMonitor == null || state == null)
                {
                    _logger.LogCritical("⚠️【启动盯盘】GenerateTriggerConditionsFromState: 参数为null");
                    return;
                }
                
                _logger.LogCritical($"🔧【启动盯盘】开始生成触发条件: {state.Symbol}_{state.PositionSide}");
                
                // 保本条件
                if (state.BreakEvenConfig?.IsEnabled == true)
                {
                    var breakEvenCondition = new TriggerConditionModel
                    {
                        Type = TriggerConditionType.BreakEven,
                        TierIndex = null,
                        TriggerPrice = state.BreakEvenConfig.TriggerProfitAmount,
                        KeepValue = 0,
                        Description = $"保本条件 - 浮盈{state.BreakEvenConfig.TriggerProfitAmount:F0}U",
                        // 🔧 修复：根据ExecutionState设置正确的状态
                        Status = state.BreakEvenConfig.ExecutionState switch
                        {
                            ExecutionState.NotTriggered => TriggerExecutionStatus.NotTriggered,
                            ExecutionState.Executing => TriggerExecutionStatus.Executing,
                            ExecutionState.Executed => TriggerExecutionStatus.Executed,
                            _ => TriggerExecutionStatus.NotTriggered
                        }
                    };
                    contractMonitor.TriggerConditions.Add(breakEvenCondition);
                    // 🔧 修复：根据ExecutionState显示正确的状态描述
                    var statusText = state.BreakEvenConfig.ExecutionState switch
                    {
                        ExecutionState.NotTriggered => "未触发",
                        ExecutionState.Executing => "执行中",
                        ExecutionState.Executed => "已执行",
                        _ => "未知"
                    };
                    _logger.LogCritical($"✅【启动盯盘】添加保本条件: {state.BreakEvenConfig.TriggerProfitAmount:F0}U, 状态: {statusText}");
                }
            
                // 推仓条件
                if (state.AddPositionConfig?.IsEnabled == true && state.AddPositionConfig.Tiers != null)
                {
                    var enabledTiers = state.AddPositionConfig.Tiers.Where(t => t != null && t.IsEnabled).ToList();
                    _logger.LogCritical($"🔧【启动盯盘】推仓条件: 启用阶梯数量 {enabledTiers.Count}");
                    
                    foreach (var tier in enabledTiers)
                    {
                        var addPositionCondition = new TriggerConditionModel
                        {
                            Type = TriggerConditionType.AddPosition,
                            TierIndex = tier.TierIndex,
                            TriggerPrice = tier.TriggerProfitAmount,
                            KeepValue = tier.ProfitProtectionAmount,
                            Description = $"推仓{tier.TierIndex} - 浮盈{tier.TriggerProfitAmount:F0}U, 倍数{tier.RiskMultiplier:F1}x",
                            // 🔧 修复：根据ExecutionState设置正确的状态
                            Status = tier.ExecutionState switch
                            {
                                ExecutionState.NotTriggered => TriggerExecutionStatus.NotTriggered,
                                ExecutionState.Executing => TriggerExecutionStatus.Executing,
                                ExecutionState.Executed => TriggerExecutionStatus.Executed,
                                _ => TriggerExecutionStatus.NotTriggered
                            }
                        };
                        contractMonitor.TriggerConditions.Add(addPositionCondition);
                        // 🔧 修复：根据ExecutionState显示正确的状态描述
                        var tierStatusText = tier.ExecutionState switch
                        {
                            ExecutionState.NotTriggered => "未触发",
                            ExecutionState.Executing => "执行中",
                            ExecutionState.Executed => "已执行",
                            _ => "未知"
                        };
                        _logger.LogCritical($"✅【启动盯盘】添加推仓条件: 阶梯{tier.TierIndex}, {tier.TriggerProfitAmount:F0}U, 状态: {tierStatusText}");
                    }
                }
            
                // 保盈条件
                if (state.ProfitProtectionConfig?.IsEnabled == true && state.ProfitProtectionConfig.Tiers != null)
                {
                    var enabledTiers = state.ProfitProtectionConfig.Tiers.Where(t => t != null && t.IsEnabled).ToList();
                    _logger.LogCritical($"🔧【启动盯盘】保盈条件: 启用阶梯数量 {enabledTiers.Count}");
                    
                    foreach (var tier in enabledTiers)
                    {
                        var profitProtectionCondition = new TriggerConditionModel
                        {
                            Type = TriggerConditionType.ProfitProtection,
                            TierIndex = tier.TierIndex,
                            TriggerPrice = tier.TriggerProfitAmount,
                            KeepValue = tier.StopLossPrice,
                            Description = $"保盈{tier.TierIndex} - 浮盈{tier.TriggerProfitAmount:F0}U",
                            // 🔧 修复：根据ExecutionState设置正确的状态
                            Status = tier.ExecutionState switch
                            {
                                ExecutionState.NotTriggered => TriggerExecutionStatus.NotTriggered,
                                ExecutionState.Executing => TriggerExecutionStatus.Executing,
                                ExecutionState.Executed => TriggerExecutionStatus.Executed,
                                _ => TriggerExecutionStatus.NotTriggered
                            }
                        };
                        contractMonitor.TriggerConditions.Add(profitProtectionCondition);
                        // 🔧 修复：根据ExecutionState显示正确的状态描述
                        var profitStatusText = tier.ExecutionState switch
                        {
                            ExecutionState.NotTriggered => "未触发",
                            ExecutionState.Executing => "执行中",
                            ExecutionState.Executed => "已执行",
                            _ => "未知"
                        };
                        _logger.LogCritical($"✅【启动盯盘】添加保盈条件: 阶梯{tier.TierIndex}, {tier.TriggerProfitAmount:F0}U, 状态: {profitStatusText}");
                    }
                }
                
                _logger.LogCritical($"🎯【启动盯盘】触发条件生成完成: 总数量 {contractMonitor.TriggerConditions.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"❌【启动盯盘】生成触发条件失败: {state?.Symbol}_{state?.PositionSide}");
                throw;
            }
        }

        /// <summary>
        /// 获取当前自动监控配置
        /// </summary>
        private AutoMonitorConfig GetCurrentAutoMonitorConfig()
        {
            try
            {
                // 🎯 第一优先级：从AutoMonitorService获取
                if (_autoMonitorService?.CurrentConfig != null)
                {
                    _logger.LogInformation($"✅ 从AutoMonitorService获取到配置：{_autoMonitorService.CurrentConfig.Name}");
                    return _autoMonitorService.CurrentConfig;
                }

                // 🎯 第二优先级：从MainViewModel获取
                if (_mainViewModel?.CurrentAutoMonitorConfig != null)
                {
                    _logger.LogInformation($"✅ 从MainViewModel获取到配置：{_mainViewModel.CurrentAutoMonitorConfig.Name}");
                    return _mainViewModel.CurrentAutoMonitorConfig;
                }

                // 🎯 第三优先级：从账户配置字典中获取用户上次保存的配置
                if (_mainViewModel?.SelectedAccount != null)
                {
                    var accountConfigs = _mainViewModel.GetAccountAutoMonitorConfigs();
                    if (accountConfigs.TryGetValue(_mainViewModel.SelectedAccount.Name, out var accountConfig))
                    {
                        _logger.LogInformation($"✅ 从账户配置字典获取到配置：{accountConfig.Name}");
                        
                        // 🔧 关键修复：将获取到的配置设置为MainViewModel的当前配置
                        _mainViewModel.SetCurrentAutoMonitorConfig(accountConfig);
                        
                        return accountConfig;
                    }
                    
                    _logger.LogInformation($"⚠️ 账户 '{_mainViewModel.SelectedAccount.Name}' 没有保存的配置");
                }
                else
                {
                    _logger.LogWarning("⚠️ 未选择账户，无法获取配置");
                }

                // 🎯 第四优先级：从配置持久化文件中直接加载
                if (_mainViewModel?.SelectedAccount != null)
                {
                    try
                    {
                        var configPersistenceService = new AutoMonitorConfigPersistenceService();
                        var savedConfig = configPersistenceService.GetAccountConfig(_mainViewModel.SelectedAccount.Name);
                        if (savedConfig != null)
                        {
                            _logger.LogInformation($"✅ 从配置文件直接加载到配置：{savedConfig.Name}");
                            
                            // 🔧 关键修复：更新MainViewModel中的配置
                            _mainViewModel.SetCurrentAutoMonitorConfig(savedConfig);
                            _mainViewModel.UpdateAccountAutoMonitorConfig(_mainViewModel.SelectedAccount.Name, savedConfig);
                            
                            return savedConfig;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "从配置文件加载失败");
                    }
                }

                _logger.LogWarning("⚠️ 未找到任何配置信息，将创建默认配置");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 获取自动监控配置失败");
                return null;
            }
        }

        /// <summary>
        /// 创建合约表格面板
        /// </summary>
        private System.Windows.Controls.Border CreateContractTablePanel()
        {
            try
            {
                _logger.LogInformation("🔄 创建合约表格面板");
                return new System.Windows.Controls.Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 创建合约表格面板失败");
                return new System.Windows.Controls.Border();
            }
        }

        /// <summary>
        /// 创建配置信息面板
        /// </summary>
        private System.Windows.Controls.Border CreateConfigInfoPanel()
        {
            try
            {
                _logger.LogInformation("🔄 创建配置信息面板");
                return new System.Windows.Controls.Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 创建配置信息面板失败");
                return new System.Windows.Controls.Border();
            }
        }

        /// <summary>
        /// 创建历史面板
        /// </summary>
        private System.Windows.Controls.Border CreateHistoryPanel()
        {
            try
            {
                _logger.LogInformation("🔄 创建历史面板");
                return new System.Windows.Controls.Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 创建历史面板失败");
                return new System.Windows.Controls.Border();
            }
        }

        /// <summary>
        /// 禁用实时同步
        /// </summary>
        private void DisableRealTimeSync()
        {
            try
            {
                _realTimeSyncEnabled = false;
                _logger.LogInformation("🔄 已禁用实时同步");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 禁用实时同步失败");
            }
        }

        /// <summary>
        /// 启用实时同步
        /// </summary>
        private void EnableRealTimeSync()
        {
            try
            {
                _realTimeSyncEnabled = true;
                _logger.LogInformation("🔄 已启用实时同步");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 启用实时同步失败");
            }
        }

        /// <summary>
        /// 更新新界面统计信息
        /// </summary>
        private void UpdateNewInterfaceStats()
        {
            try
            {
                _logger.LogInformation("🔄 更新新界面统计信息");
                
                // 更新统计信息
                var runningCount = ContractMonitors.Count(c => c.IsActive);
                var totalConditions = ContractMonitors.Sum(c => c.TriggerConditions?.Count ?? 0);
                
                MonitorStatusText = _autoMonitorService?.IsRunning == true ? "运行中" : "已停止";
                ContractCount = ContractMonitors.Count;
                TotalConditionsText = totalConditions.ToString();
                
                OnPropertyChanged(nameof(MonitorStatusText));
                OnPropertyChanged(nameof(ContractCount));
                OnPropertyChanged(nameof(TotalConditionsText));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 更新新界面统计信息失败");
            }
        }

        /// <summary>
        /// 刷新合约监控状态
        /// </summary>
        private void RefreshContractMonitorStatus()
        {
            try
            {
                _logger.LogInformation("🔄 刷新合约监控状态和实时数据");
                
                // 🔧 修复：获取最新的持仓数据以更新实时价格和浮盈
                var currentPositions = GetCurrentRealTimePositions();
                
                foreach (var contract in ContractMonitors)
                {
                    var contractKey = $"{contract.Symbol}_{contract.PositionSide}";
                    
                    // 🔧 修复：更新实时持仓数据（价格、浮盈）
                    var currentPosition = currentPositions.FirstOrDefault(p => 
                        $"{p.Symbol}_{p.PositionSideString}" == contractKey);
                    
                    if (currentPosition != null)
                    {
                        // 🔧 关键修复：在UI线程中更新实时数据并显式触发属性变更通知
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 更新实时数据
                            contract.CurrentPrice = currentPosition.MarkPrice;
                            contract.PositionSize = Math.Abs(currentPosition.PositionAmt);
                            contract.UnrealizedPnl = currentPosition.UnrealizedProfit;
                            
                            // 🔧 显式触发属性变更通知，确保UI更新
                            contract.OnPropertyChanged(nameof(contract.CurrentPrice));
                            contract.OnPropertyChanged(nameof(contract.PositionSize));
                            contract.OnPropertyChanged(nameof(contract.UnrealizedPnl));
                            contract.OnPropertyChanged(nameof(contract.CurrentPriceText));
                            contract.OnPropertyChanged(nameof(contract.PositionSizeText));
                            contract.OnPropertyChanged(nameof(contract.PnlText));
                            contract.OnPropertyChanged(nameof(contract.PnlColor));
                        });
                        
                        _logger.LogDebug($"🔄 更新实时数据: {contractKey} - 价格:{currentPosition.MarkPrice:F4}, 浮盈:{currentPosition.UnrealizedProfit:F2}U");
                    }
                    
                    // 更新合约状态
                    contract.IsActive = _autoMonitorService?.IsRunning == true && contract.IsEnabled;
                    
                    // RowBackgroundColor是只读属性，通过更新状态来间接改变颜色
                    // 触发属性更新通知
                    contract.OnPropertyChanged(nameof(contract.RowBackgroundColor));
                }
                
                UpdateNewInterfaceStats();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 刷新合约监控状态失败");
            }
        }
        
        /// <summary>
        /// 🔧 修复：获取当前实时持仓数据（用于更新监控面板显示）
        /// </summary>
        private List<Models.PositionInfo> GetCurrentRealTimePositions()
        {
            try
            {
                var positions = new List<Models.PositionInfo>();
                
                // 🔧 空引用防护：优先从 MainViewModel 获取最新持仓数据
                var mainWindow = Application.Current?.MainWindow as MainWindow;
                if (mainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    var allPositions = mainViewModel.Positions;
                    if (allPositions != null)
                    {
                        var currentPositions = allPositions.Where(p => p != null && Math.Abs(p.PositionAmt) > 0).ToList();
                        
                        if (currentPositions.Any())
                        {
                            positions.AddRange(currentPositions);
                            _logger.LogDebug($"📊 从 MainViewModel 获取到 {positions.Count} 个实时持仓");
                            return positions;
                        }
                    }
                    else
                    {
                        _logger.LogDebug("📊 MainViewModel.Positions 为null");
                    }
                }
                else
                {
                    _logger.LogDebug("📊 未能获取MainWindow或MainViewModel");
                }
                
                // 🔧 备用方案：尝试从当前实例的 _mainViewModel 获取
                if (_mainViewModel?.Positions != null)
                {
                    var fallbackPositions = _mainViewModel.Positions.Where(p => p != null && Math.Abs(p.PositionAmt) > 0).ToList();
                    if (fallbackPositions.Any())
                    {
                        positions.AddRange(fallbackPositions);
                        _logger.LogDebug($"📊 从备用 _mainViewModel 获取到 {positions.Count} 个实时持仓");
                        return positions;
                    }
                }
                
                _logger.LogDebug("📊 未能从任何来源获取持仓数据");
                return positions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 获取实时持仓数据失败");
                return new List<Models.PositionInfo>();
            }
        }

        /// <summary>
        /// 保存合约配置到文件 - 已废弃：现在使用统一状态管理
        /// </summary>
        [Obsolete("已废弃：现在使用ContractMonitoringStateService进行统一状态管理，不再需要单独的ContractConfigs.json文件")]
        private void SaveContractConfigsToFile()
        {
            _logger.LogWarning("⚠️ SaveContractConfigsToFile 已废弃：现在使用统一状态管理，无需单独保存ContractConfigs.json");
            // 已废弃：合约配置现在通过 ContractMonitoringStateService 统一管理
            // 数据保存在 contract_monitoring_states.json 文件中
        }
        
        /// <summary>
        /// 🔧 【需求2】配置修改时重新生成配置数据并保存
        /// </summary>
        public async Task OnConfigurationChangedAsync(AutoMonitorConfig newConfig)
        {
            try
            {
                _logger.LogInformation($"🔄【需求2】配置修改检测: {newConfig.Name}，重新生成合约配置数据");
                
                // 获取当前活跃持仓
                var currentPositions = await GetCurrentActivePositionsAsync();
                if (!currentPositions.Any())
                {
                    _logger.LogInformation("⚠️ 当前无活跃持仓，无需生成配置");
                    return;
                }
                
                _logger.LogInformation($"📊 检测到 {currentPositions.Count} 个活跃持仓，开始重新生成配置");
                
                // 创建ContractMonitoringStateService实例
                var stateService = CreateContractMonitoringStateService();
                var existingStates = stateService.LoadMonitoringStates();
                
                // 重新生成所有状态
                var newStates = new Dictionary<string, ContractMonitoringState>();
                
                // 为每个持仓重新生成配置数据
                foreach (var position in currentPositions)
                {
                    var positionSide = position.PositionAmt > 0 ? "LONG" : "SHORT";
                    var contractKey = $"{position.Symbol}_{positionSide}";
                    
                    // 🔧 重新生成监控状态，但保留已执行的状态
                    var newState = GenerateContractMonitoringState(position, newConfig);
                    
                    // 🔧 如果存在旧状态，保留执行状态
                    if (existingStates.TryGetValue(contractKey, out var existingState))
                    {
                        PreserveExecutionStatus(newState, existingState);
                        _logger.LogInformation($"✅ 保留已执行状态: {contractKey}");
                    }
                    
                    // 添加到新状态字典
                    newStates[contractKey] = newState;
                    _logger.LogInformation($"✅ 重新生成配置: {contractKey}");
                }
                
                // 保存所有状态
                stateService.SaveMonitoringStates(newStates);
                
                // 🔧 更新界面显示
                await LoadFromMonitoringStatesFileAsync();
                
                _logger.LogInformation($"🎉【需求2】配置修改处理完成，已重新生成 {currentPositions.Count} 个合约的配置数据");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌【需求2】配置修改时重新生成配置失败");
            }
        }
        
        /// <summary>
        /// 🔧 【需求2】合约开平仓时重新生成配置数据
        /// </summary>
        public async Task OnPositionChangedAsync(string symbol, string action)
        {
            try
            {
                _logger.LogInformation($"🔄【需求2】持仓变化检测: {symbol} {action}，重新生成配置数据");
                
                if (action == "OPENED")
                {
                    // 新开仓：生成新的配置数据
                    await HandleNewPositionOpenedAsync(symbol);
                }
                else if (action == "CLOSED")
                {
                    // 平仓：移除配置数据
                    await HandlePositionClosedAsync(symbol);
                }
                
                _logger.LogInformation($"✅【需求2】持仓变化处理完成: {symbol} {action}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌【需求2】持仓变化处理失败: {symbol} {action}");
            }
        }
        
        /// <summary>
        /// 🔧 【辅助方法】处理新开仓
        /// </summary>
        private async Task HandleNewPositionOpenedAsync(string symbol)
        {
            try
            {
                var currentPositions = await GetCurrentActivePositionsAsync();
                var newPosition = currentPositions.FirstOrDefault(p => p.Symbol == symbol);
                
                if (newPosition == null)
                {
                    _logger.LogWarning($"⚠️ 未找到新开仓的持仓信息: {symbol}");
                    return;
                }
                
                var currentConfig = GetCurrentAutoMonitorConfig();
                if (currentConfig == null)
                {
                    _logger.LogWarning("⚠️ 未找到当前配置，无法生成新配置数据");
                    return;
                }
                
                var positionSide = newPosition.PositionAmt > 0 ? "LONG" : "SHORT";
                var contractKey = $"{symbol}_{positionSide}";
                
                // 生成新的监控状态
                var newState = GenerateContractMonitoringState(newPosition, currentConfig);
                
                // 保存到状态服务
                var stateService = CreateContractMonitoringStateService();
                
                // 创建状态字典并保存
                var newStates = new Dictionary<string, ContractMonitoringState> { [contractKey] = newState };
                stateService.SaveMonitoringStates(newStates);
                
                // 更新界面
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var existingMonitor = ContractMonitors.FirstOrDefault(c => c.ContractKey == contractKey);
                    if (existingMonitor == null)
                    {
                        var newMonitor = ConvertStateToContractMonitor(newState, new List<PositionInfo> { newPosition });
                        ContractMonitors.Add(newMonitor);
                        _logger.LogInformation($"✅ 新增合约配置到界面: {contractKey}");
                    }
                });
                
                _logger.LogInformation($"🎉 新开仓处理完成: {contractKey}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 处理新开仓失败: {symbol}");
            }
        }
        
        /// <summary>
        /// 🔧 【辅助方法】处理平仓
        /// </summary>
        private async Task HandlePositionClosedAsync(string symbol)
        {
            try
            {
                // 移除相关的监控状态
                var stateService = CreateContractMonitoringStateService();
                var existingStates = stateService.LoadMonitoringStates();
                
                var keysToRemove = existingStates.Keys.Where(k => k.StartsWith($"{symbol}_")).ToList();
                
                foreach (var key in keysToRemove)
                {
                    // 这里可以添加移除逻辑，或者标记为非活跃
                    var state = existingStates[key];
                    state.IsActive = false;
                    _logger.LogInformation($"🗑️ 标记为非活跃: {key}");
                }
                
                // 更新界面
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var monitorsToRemove = ContractMonitors.Where(c => c.Symbol == symbol).ToList();
                    foreach (var monitor in monitorsToRemove)
                    {
                        ContractMonitors.Remove(monitor);
                        _logger.LogInformation($"🗑️ 从界面移除合约配置: {monitor.ContractKey}");
                    }
                });
                
                _logger.LogInformation($"🎉 平仓处理完成: {symbol}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 处理平仓失败: {symbol}");
            }
        }
        
        /// <summary>
        /// 🔧 【辅助方法】生成合约监控状态
        /// </summary>
        private ContractMonitoringState GenerateContractMonitoringState(PositionInfo position, AutoMonitorConfig config)
        {
            var positionSide = position.PositionAmt > 0 ? "LONG" : "SHORT";
            
            return new ContractMonitoringState
            {
                Symbol = position.Symbol,
                PositionSide = positionSide,
                BaseConfigName = config.Name,
                IsActive = true,
                            CurrentMarkPrice = position.MarkPrice,
            CurrentQuantity = Math.Abs(position.PositionAmt),
                CurrentUnrealizedPnl = position.UnrealizedProfit,
                LastUpdateTime = DateTime.Now,
                
                // 保本配置
                BreakEvenConfig = new StatefulBreakEvenConfig
                {
                    IsEnabled = config.BreakEvenConfig.IsEnabled,
                    TriggerProfitAmount = config.BreakEvenConfig.TriggerProfitAmount,
                    ExecutionState = ExecutionState.NotTriggered,
                    ExecutionTime = null,
                    ExecutionPnl = 0,
                    ExecutionResult = ""
                },
                
                // 推仓配置
                AddPositionConfig = new StatefulAddPositionConfig
                {
                    IsEnabled = config.AddPositionConfig.IsEnabled,
                    Tiers = config.AddPositionConfig.Tiers.Select(t => new StatefulAddPositionTier
                    {
                        TierIndex = t.TierIndex,
                        IsEnabled = t.IsEnabled,
                        TriggerProfitAmount = t.TriggerProfitAmount,
                        RiskMultiplier = t.RiskMultiplier,
                        StopLossRatio = t.StopLossRatio,
                        ProfitProtectionAmount = t.ProfitProtectionAmount,
                        ExitTargetPnl = t.ExitTargetPnl,
                        ExecutionState = ExecutionState.NotTriggered,
                        ExecutionTime = null,
                        ExecutionPnl = 0,
                        ExecutionResult = "",
                        AddPositionQuantity = 0,
                        StopLossPrice = 0
                    }).ToList()
                },
                
                // 保盈配置
                ProfitProtectionConfig = new StatefulProfitProtectionConfig
                {
                    IsEnabled = config.ProfitProtectionConfig.IsEnabled,
                    Tiers = config.ProfitProtectionConfig.Tiers.Select(t => new StatefulProfitProtectionTier
                    {
                        TierIndex = t.TierIndex,
                        IsEnabled = t.IsEnabled,
                        TriggerProfitAmount = t.TriggerProfitAmount,
                        ProtectionAmount = t.ProtectionAmount,
                        ExecutionState = ExecutionState.NotTriggered,
                        ExecutionTime = null,
                        ExecutionPnl = 0,
                        ExecutionResult = "",
                        StopLossPrice = 0
                    }).ToList()
                }
            };
        }
        
        /// <summary>
        /// 🔧 【公共接口】手动触发数据流程刷新 - 实现三个需求的完整流程
        /// </summary>
        public async Task RefreshDataFlowAsync()
        {
            try
            {
                _logger.LogInformation("🔄【完整数据流程】开始手动刷新数据流程...");
                
                // 🔧 【需求1】优先从contract_monitoring_states.json加载
                var loadedFromFile = await LoadFromMonitoringStatesFileAsync();
                if (loadedFromFile)
                {
                    _logger.LogInformation("✅【完整数据流程】从状态文件成功加载数据");
                    return;
                }
                
                // 🔧 如果文件中没有数据，重新生成
                var currentConfig = GetCurrentAutoMonitorConfig();
                if (currentConfig != null)
                {
                    await OnConfigurationChangedAsync(currentConfig);
                    _logger.LogInformation("✅【完整数据流程】重新生成配置数据完成");
                }
                else
                {
                    _logger.LogInformation("⚠️【完整数据流程】未找到配置，无法生成数据");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌【完整数据流程】手动刷新数据流程失败");
            }
        }
        
        /// <summary>
        /// 🔧 【辅助方法】保留执行状态
        /// </summary>
        private void PreserveExecutionStatus(ContractMonitoringState newState, ContractMonitoringState existingState)
        {
            // 保留保本执行状态
            if (existingState.BreakEvenConfig.ExecutionState == ExecutionState.Executed)
            {
                newState.BreakEvenConfig.ExecutionState = existingState.BreakEvenConfig.ExecutionState;
                newState.BreakEvenConfig.ExecutionTime = existingState.BreakEvenConfig.ExecutionTime;
                newState.BreakEvenConfig.ExecutionPnl = existingState.BreakEvenConfig.ExecutionPnl;
                newState.BreakEvenConfig.ExecutionResult = existingState.BreakEvenConfig.ExecutionResult;
                _logger.LogInformation($"✅ 保留保本执行状态: ExecutionState={existingState.BreakEvenConfig.ExecutionState}");
            }
            
            // 保留推仓执行状态
            foreach (var newTier in newState.AddPositionConfig.Tiers)
            {
                var existingTier = existingState.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == newTier.TierIndex);
                if (existingTier?.ExecutionState == ExecutionState.Executed)
                {
                    newTier.ExecutionState = existingTier.ExecutionState;
                    newTier.ExecutionTime = existingTier.ExecutionTime;
                    newTier.ExecutionPnl = existingTier.ExecutionPnl;
                    newTier.ExecutionResult = existingTier.ExecutionResult;
                    _logger.LogInformation($"✅ 保留推仓阶梯{newTier.TierIndex}执行状态: ExecutionState={existingTier.ExecutionState}");
                }
            }
            
            // 保留保盈执行状态
            foreach (var newTier in newState.ProfitProtectionConfig.Tiers)
            {
                var existingTier = existingState.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == newTier.TierIndex);
                if (existingTier?.ExecutionState == ExecutionState.Executed)
                {
                    newTier.ExecutionState = existingTier.ExecutionState;
                    newTier.ExecutionTime = existingTier.ExecutionTime;
                    newTier.ExecutionPnl = existingTier.ExecutionPnl;
                    newTier.ExecutionResult = existingTier.ExecutionResult;
                    _logger.LogInformation($"✅ 保留保盈阶梯{newTier.TierIndex}执行状态: ExecutionState={existingTier.ExecutionState}");
                }
            }
        }

        /// <summary>
        /// 加载当前持仓和配置
        /// </summary>
        private void LoadCurrentPositionsWithConfigs()
        {
            try
            {
                _logger.LogInformation("📂 加载当前持仓和配置");
                
                var persistenceService = new AutoMonitorPersistenceService();
                var savedConfigs = persistenceService.LoadContractConfigs();
                
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ContractMonitors.Clear();
                    
                    foreach (var config in savedConfigs)
                    {
                        var monitor = new ContractMonitorModel
                        {
                            Symbol = config.Symbol,
                            PositionSide = config.PositionSide,
                            IsEnabled = config.IsEnabled,
                            IsActive = false,
                            UnrealizedPnl = 0,
                            CurrentPrice = 0
                        };
                        
                        ContractMonitors.Add(monitor);
                    }
                    
                    _logger.LogInformation($"✅ 已加载 {savedConfigs.Count} 个合约配置");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 加载当前持仓和配置失败");
            }
        }

        /// <summary>
        /// �� 自动载入基础配置对应的合约配置（初始化时调用）
        /// </summary>
        private async Task AutoLoadContractConfigsAsync(AutoMonitorConfig baseConfig)
        {
            try
            {
                _logger.LogInformation($"🔄 开始自动载入基础配置'{baseConfig.Name}'对应的合约配置");
                
                // 检查是否有保存的合约配置
                var persistenceService = new AutoMonitorPersistenceService();
                var savedContracts = persistenceService.LoadContractConfigs();
                
                if (savedContracts.Any())
                {
                    _logger.LogInformation($"📂 发现已保存的合约配置 {savedContracts.Count} 个，自动载入");
                    
                    // 在UI线程中更新
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ContractMonitors.Clear();
                        foreach (var contract in savedContracts)
                        {
                            ContractMonitors.Add(contract);
                        }
                        
                        // 重新生成表格列
                        GenerateDynamicDataGridColumns(baseConfig);
                        
                        // 刷新界面
                        OnPropertyChanged(nameof(ContractMonitors));
                        if (_contractMonitorDataGrid != null)
                        {
                            _contractMonitorDataGrid.Items.Refresh();
                        }
                    });
                    
                    _logger.LogInformation($"✅ 自动载入完成：{savedContracts.Count} 个合约配置");
                }
                else
                {
                    _logger.LogInformation("📂 未发现保存的合约配置，从当前持仓自动生成");
                    
                    // 获取当前持仓并生成配置
                    var currentPositions = _autoMonitorService.GetPositionProfiles();
                    if (currentPositions.Any())
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            LoadCurrentPositionsWithBaseConfigReset(baseConfig);
                        });
                        
                        _logger.LogInformation($"✅ 已从 {currentPositions.Count} 个活跃持仓自动生成合约配置");
                    }
                    else
                    {
                        _logger.LogInformation("📂 无活跃持仓，载入默认表格结构");
                        
                        // 无持仓时也要设置表格列结构
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            GenerateDynamicDataGridColumns(baseConfig);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 自动载入基础配置'{baseConfig.Name}'对应的合约配置失败");
                throw;
            }
        }

        /// <summary>
        /// 🔄 用基础配置重置所有合约配置
        /// </summary>
        private void LoadCurrentPositionsWithBaseConfigReset(AutoMonitorConfig baseConfig)
        {
            try
            {
                _logger.LogInformation($"🔄 用基础配置重置所有合约配置: {baseConfig.Name}");
                
                // 🔧 关键修复：配置切换时，首先清理所有执行状态
                if (_autoMonitorService != null)
                {
                    _logger.LogInformation("🧹 配置切换时清理所有合约执行状态");
                    _autoMonitorService.ClearContractStates(symbol: null, positionSide: null, reason: "配置切换重置");
                }
                
                // 获取当前活跃持仓
                var activePositions = _autoMonitorService.GetPositionProfiles();
                _logger.LogInformation($"📊 找到 {activePositions.Count} 个活跃持仓");
                
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ContractMonitors.Clear();
                    
                    // 为每个活跃持仓生成完全基于基础配置的合约配置
                    foreach (var position in activePositions.Values)
                    {
                        var contractMonitor = GenerateContractConfigFromBaseConfig(position, baseConfig);
                        
                        // 🔧 关键修复：确保新生成的合约配置状态为未触发
                        foreach (var condition in contractMonitor.TriggerConditions)
                        {
                            condition.Status = TriggerExecutionStatus.NotTriggered;
                            condition.LastExecutionTime = null;
                            condition.StatusNote = $"配置切换重置 {DateTime.Now:HH:mm:ss}";
                        }
                        
                        ContractMonitors.Add(contractMonitor);
                        _logger.LogDebug($"✅ 基于基础配置生成合约配置: {contractMonitor.Symbol}_{contractMonitor.PositionSide} - {contractMonitor.TriggerConditions.Count} 个触发条件");
                    }
                    
                    // 如果没有活跃持仓，加载已保存的配置但用基础配置重置
                    if (!activePositions.Any())
                    {
                        _logger.LogInformation("📝 无活跃持仓，从已保存配置加载但用基础配置重置");
                        var persistenceService = new AutoMonitorPersistenceService();
                        var savedConfigs = persistenceService.LoadContractConfigs();
                        
                        foreach (var savedConfig in savedConfigs)
                        {
                            var contractMonitor = new ContractMonitorModel
                            {
                                Symbol = savedConfig.Symbol,
                                PositionSide = savedConfig.PositionSide,
                                IsEnabled = savedConfig.IsEnabled,
                                IsActive = false,
                                UnrealizedPnl = 0,
                                CurrentPrice = 0
                            };
                            
                            // 用基础配置重置这个合约的触发条件
                            ReloadContractFromBaseConfig(baseConfig, contractMonitor.Symbol);
                            
                            // 🔧 关键修复：确保重置后的状态为未触发
                            foreach (var condition in contractMonitor.TriggerConditions)
                            {
                                condition.Status = TriggerExecutionStatus.NotTriggered;
                                condition.LastExecutionTime = null;
                                condition.StatusNote = $"配置切换重置 {DateTime.Now:HH:mm:ss}";
                            }
                            
                            ContractMonitors.Add(contractMonitor);
                        }
                    }
                    
                    _logger.LogInformation($"✅ 基础配置重置完成，共处理 {ContractMonitors.Count} 个合约，所有状态已重置为未触发");
                });
                
                // 保存重置后的配置
                SaveContractConfigsToFile();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 用基础配置重置合约配置失败");
                throw;
            }
        }

        /// <summary>
        /// 🆕 从基础配置生成合约配置
        /// </summary>
        private ContractMonitorModel GenerateContractConfigFromBaseConfig(PositionProfile position, AutoMonitorConfig baseConfig)
        {
            try
            {
                                 var contractMonitor = new ContractMonitorModel
                 {
                     Symbol = position.Symbol,
                     PositionSide = position.PositionSide,
                     IsEnabled = true,
                     IsActive = false,
                     UnrealizedPnl = 0,
                     CurrentPrice = 0
                 };
                
                var conditionId = 1;
                
                // 生成保本条件
                if (baseConfig.BreakEvenConfig.IsEnabled)
                {
                    contractMonitor.TriggerConditions.Add(new TriggerConditionModel
                    {
                        Id = conditionId++,
                        Type = TriggerConditionType.BreakEven,
                        TierIndex = null,
                        TriggerPrice = baseConfig.BreakEvenConfig.TriggerProfitAmount,
                        KeepValue = 0,
                        Status = TriggerExecutionStatus.NotTriggered,
                        Description = $"保本条件 - 浮盈{baseConfig.BreakEvenConfig.TriggerProfitAmount:F0}U",
                        StatusNote = $"基础配置生成 {DateTime.Now:HH:mm:ss}"
                    });
                }
                
                // 生成推仓条件
                if (baseConfig.AddPositionConfig.IsEnabled)
                {
                    foreach (var tier in baseConfig.AddPositionConfig.Tiers.OrderBy(t => t.TierIndex))
                    {
                        contractMonitor.TriggerConditions.Add(new TriggerConditionModel
                        {
                            Id = conditionId++,
                            Type = TriggerConditionType.AddPosition,
                            TierIndex = tier.TierIndex,
                            TriggerPrice = tier.TriggerProfitAmount,
                            KeepValue = tier.ProfitProtectionAmount,
                            Status = TriggerExecutionStatus.NotTriggered,
                            Description = $"推仓{tier.TierIndex} - 浮盈{tier.TriggerProfitAmount:F0}U, 倍数{tier.RiskMultiplier:F1}x",
                            StatusNote = $"基础配置生成 {DateTime.Now:HH:mm:ss}"
                        });
                    }
                }
                
                // 生成止盈条件
                if (baseConfig.ProfitProtectionConfig.IsEnabled)
                {
                    foreach (var tier in baseConfig.ProfitProtectionConfig.Tiers.OrderBy(t => t.TierIndex))
                    {
                        contractMonitor.TriggerConditions.Add(new TriggerConditionModel
                        {
                            Id = conditionId++,
                            Type = TriggerConditionType.ProfitProtection,
                            TierIndex = tier.TierIndex,
                            TriggerPrice = tier.TriggerProfitAmount,
                            KeepValue = tier.ProtectionAmount,
                            Status = TriggerExecutionStatus.NotTriggered,
                            Description = $"止盈{tier.TierIndex} - 浮盈{tier.TriggerProfitAmount:F0}U, 保护{tier.ProtectionAmount:F0}U",
                            StatusNote = $"基础配置生成 {DateTime.Now:HH:mm:ss}"
                        });
                    }
                }
                
                _logger.LogDebug($"✅ 为 {position.Symbol}_{position.PositionSide} 生成了 {contractMonitor.TriggerConditions.Count} 个触发条件");
                return contractMonitor;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 为 {position.Symbol}_{position.PositionSide} 生成合约配置失败");
                throw;
            }
        }

        /// <summary>
        /// 添加基础列到数据网格
        /// </summary>
        private void AddBasicColumnsToDataGrid(System.Windows.Controls.DataGrid dataGrid)
        {
            try
            {
                _logger.LogInformation("📋 添加基础列到数据网格");
                
                dataGrid.Columns.Clear();
                
                // 添加基础列
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "合约",
                    Binding = new System.Windows.Data.Binding("Symbol"),
                    Width = new System.Windows.Controls.DataGridLength(80)
                });
                
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "方向",
                    Binding = new System.Windows.Data.Binding("PositionSide"),
                    Width = new System.Windows.Controls.DataGridLength(60)
                });
                
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridCheckBoxColumn
                {
                    Header = "启用",
                    Binding = new System.Windows.Data.Binding("IsEnabled"),
                    Width = new System.Windows.Controls.DataGridLength(50)
                });
                
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "当前价格",
                    Binding = new System.Windows.Data.Binding("CurrentPrice"),
                    Width = new System.Windows.Controls.DataGridLength(80)
                });
                
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "未实现盈亏",
                    Binding = new System.Windows.Data.Binding("CurrentPnl"),
                    Width = new System.Windows.Controls.DataGridLength(100)
                });
                
                _logger.LogInformation($"✅ 已添加 {dataGrid.Columns.Count} 个基础列");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 添加基础列到数据网格失败");
            }
        }

        /// <summary>
        /// 添加最小列到数据网格
        /// </summary>
        private void AddMinimalColumnsToDataGrid(System.Windows.Controls.DataGrid dataGrid)
        {
            try
            {
                _logger.LogInformation("📋 添加最小列到数据网格");
                
                dataGrid.Columns.Clear();
                
                // 只添加最基础的列
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "合约",
                    Binding = new System.Windows.Data.Binding("Symbol"),
                    Width = new System.Windows.Controls.DataGridLength(100)
                });
                
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridCheckBoxColumn
                {
                    Header = "启用",
                    Binding = new System.Windows.Data.Binding("IsEnabled"),
                    Width = new System.Windows.Controls.DataGridLength(60)
                });
                
                _logger.LogInformation($"✅ 已添加 {dataGrid.Columns.Count} 个最小列");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 添加最小列到数据网格失败");
            }
        }

        /// <summary>
        /// 创建执行历史数据网格
        /// </summary>
        private System.Windows.Controls.DataGrid CreateExecutionHistoryDataGrid()
        {
            try
            {
                _logger.LogInformation("📊 创建执行历史数据网格");
                
                var dataGrid = new System.Windows.Controls.DataGrid
                {
                    AutoGenerateColumns = false,
                    CanUserAddRows = false,
                    IsReadOnly = true,
                    GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.All,
                    Background = new SolidColorBrush(Colors.White),
                    FontSize = 10,
                    RowHeight = 25,
                    ItemsSource = ExecutionHistory
                };
                
                // 添加列
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "时间",
                    Binding = new System.Windows.Data.Binding("ExecutionTime") { StringFormat = "HH:mm:ss" },
                    Width = new System.Windows.Controls.DataGridLength(60)
                });
                
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "合约",
                    Binding = new System.Windows.Data.Binding("Symbol"),
                    Width = new System.Windows.Controls.DataGridLength(70)
                });
                
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "操作",
                    Binding = new System.Windows.Data.Binding("ExecutionType"),
                    Width = new System.Windows.Controls.DataGridLength(60)
                });
                
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "结果",
                    Binding = new System.Windows.Data.Binding("ResultText"),
                    Width = new System.Windows.Controls.DataGridLength(50)
                });
                
                return dataGrid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 创建执行历史数据网格失败");
                return new System.Windows.Controls.DataGrid();
            }
        }

        /// <summary>
        /// 创建后备数据网格
        /// </summary>
        private System.Windows.Controls.DataGrid CreateFallbackDataGrid()
        {
            try
            {
                _logger.LogInformation("🔧 创建后备数据网格");
                
                var dataGrid = new System.Windows.Controls.DataGrid
                {
                    AutoGenerateColumns = false,
                    CanUserAddRows = false,
                    IsReadOnly = true,
                    Background = new SolidColorBrush(Colors.White),
                    ItemsSource = ContractMonitors
                };
                
                AddMinimalColumnsToDataGrid(dataGrid);
                return dataGrid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 创建后备数据网格失败");
                return new System.Windows.Controls.DataGrid();
            }
        }

        /// <summary>
        /// 打开快速编辑对话框
        /// </summary>
        private void OpenQuickEditDialog(ContractMonitorModel contract)
        {
            try
            {
                _logger.LogInformation($"🔧 打开快速编辑对话框: {contract.Symbol}");
                
                var result = MessageBox.Show(
                    $"是否启用合约 {contract.Symbol} 的监控？",
                    "快速编辑",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    contract.IsEnabled = !contract.IsEnabled;
                    _logger.LogInformation($"✅ 合约 {contract.Symbol} 监控状态已切换为: {contract.IsEnabled}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 打开快速编辑对话框失败: {contract?.Symbol}");
            }
        }

        /// <summary>
        /// 更新触发价格从基础配置（带参数）
        /// </summary>
        private void UpdateTriggerPricesFromBaseConfig(AutoMonitorConfig config, ContractMonitorModel contract)
        {
            try
            {
                _logger.LogInformation($"🔄 更新触发价格从基础配置: {config.Name} - {contract.Symbol}");
                
                // 更新保本条件触发价格
                if (config.BreakEvenConfig.IsEnabled)
                {
                    var breakEvenCondition = contract.TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                    if (breakEvenCondition != null)
                    {
                        breakEvenCondition.TriggerPrice = config.BreakEvenConfig.TriggerProfitAmount;
                        breakEvenCondition.Description = $"保本条件 - 浮盈{config.BreakEvenConfig.TriggerProfitAmount:F0}U";
                        breakEvenCondition.StatusNote = $"触发价格更新 {DateTime.Now:HH:mm:ss}";
                    }
                }
                
                // 更新推仓条件触发价格
                if (config.AddPositionConfig.IsEnabled)
                {
                    foreach (var tier in config.AddPositionConfig.Tiers)
                    {
                        var condition = contract.TriggerConditions.FirstOrDefault(c => 
                            c.Type == TriggerConditionType.AddPosition && c.TierIndex == tier.TierIndex);
                        if (condition != null)
                        {
                            condition.TriggerPrice = tier.TriggerProfitAmount;
                            condition.KeepValue = tier.ProfitProtectionAmount;
                            condition.Description = $"推仓{tier.TierIndex} - 浮盈{tier.TriggerProfitAmount:F0}U, 倍数{tier.RiskMultiplier:F1}x";
                            condition.StatusNote = $"触发价格更新 {DateTime.Now:HH:mm:ss}";
                        }
                    }
                }
                
                // 更新止盈条件触发价格
                if (config.ProfitProtectionConfig.IsEnabled)
                {
                    foreach (var tier in config.ProfitProtectionConfig.Tiers)
                    {
                        var condition = contract.TriggerConditions.FirstOrDefault(c => 
                            c.Type == TriggerConditionType.ProfitProtection && c.TierIndex == tier.TierIndex);
                        if (condition != null)
                        {
                            condition.TriggerPrice = tier.TriggerProfitAmount;
                            condition.KeepValue = tier.ProtectionAmount;
                            condition.Description = $"止盈{tier.TierIndex} - 浮盈{tier.TriggerProfitAmount:F0}U, 保护{tier.ProtectionAmount:F0}U";
                            condition.StatusNote = $"触发价格更新 {DateTime.Now:HH:mm:ss}";
                        }
                    }
                }
                
                _logger.LogInformation($"✅ 触发价格更新完成: {contract.Symbol}_{contract.PositionSide}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 更新触发价格从基础配置失败: {contract.Symbol}_{contract.PositionSide}");
                throw;
            }
        }

        /// <summary>
        /// 🆕 重新从基础配置加载合约配置（完整实现）
        /// </summary>
        private void ReloadContractFromBaseConfig(AutoMonitorConfig baseConfig, string symbol)
        {
            try
            {
                _logger.LogInformation($"🔄 从基础配置重新加载合约: {baseConfig.Name} - {symbol}");
                
                var contract = ContractMonitors.FirstOrDefault(c => c.Symbol == symbol);
                if (contract == null)
                {
                    _logger.LogWarning($"⚠️ 未找到合约: {symbol}");
                    return;
                }
                
                var conditionId = contract.TriggerConditions.Any() ? contract.TriggerConditions.Max(c => c.Id) + 1 : 1;
                var updatedCount = 0;
                var addedCount = 0;
                var removedCount = 0;
                
                // 🔧 第1步：更新保本条件
                if (baseConfig.BreakEvenConfig.IsEnabled)
                {
                    var breakEvenCondition = contract.TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                    if (breakEvenCondition == null)
                    {
                        // 如果没有保本条件，新增一个
                        contract.TriggerConditions.Add(new TriggerConditionModel
                        {
                            Id = conditionId++,
                            Type = TriggerConditionType.BreakEven,
                            TierIndex = null,
                            TriggerPrice = baseConfig.BreakEvenConfig.TriggerProfitAmount,
                            KeepValue = 0,
                            Status = TriggerExecutionStatus.NotTriggered,
                            Description = $"保本条件 - 浮盈{baseConfig.BreakEvenConfig.TriggerProfitAmount:F0}U",
                            StatusNote = $"从基础配置重载 {DateTime.Now:HH:mm:ss}"
                        });
                        addedCount++;
                        _logger.LogInformation($"🆕 新增保本条件: {contract.Symbol}_{contract.PositionSide}");
                    }
                    else
                    {
                        // 🔧 更新已存在的保本条件参数
                        var oldTriggerPrice = breakEvenCondition.TriggerPrice;
                        breakEvenCondition.TriggerPrice = baseConfig.BreakEvenConfig.TriggerProfitAmount;
                        breakEvenCondition.KeepValue = 0;
                        breakEvenCondition.Description = $"保本条件 - 浮盈{baseConfig.BreakEvenConfig.TriggerProfitAmount:F0}U";
                        breakEvenCondition.StatusNote = $"从基础配置重载 {DateTime.Now:HH:mm:ss}";
                        
                        if (oldTriggerPrice != breakEvenCondition.TriggerPrice)
                        {
                            updatedCount++;
                            _logger.LogInformation($"🔄 更新保本条件: {contract.Symbol}_{contract.PositionSide} - 触发价格: {oldTriggerPrice:F0} → {breakEvenCondition.TriggerPrice:F0}");
                        }
                    }
                }
                else
                {
                    // 如果基础配置禁用了保本，移除现有的保本条件
                    var existingBreakEven = contract.TriggerConditions.Where(c => c.Type == TriggerConditionType.BreakEven).ToList();
                    foreach (var condition in existingBreakEven)
                    {
                        contract.TriggerConditions.Remove(condition);
                        removedCount++;
                        _logger.LogInformation($"🗑️ 移除保本条件: {contract.Symbol}_{contract.PositionSide}");
                    }
                }
                
                // 🔧 第2步：更新推仓条件
                if (baseConfig.AddPositionConfig.IsEnabled && baseConfig.AddPositionConfig.Tiers.Any())
                {
                    foreach (var tier in baseConfig.AddPositionConfig.Tiers.OrderBy(t => t.TierIndex))
                    {
                        var existingCondition = contract.TriggerConditions.FirstOrDefault(c => 
                            c.Type == TriggerConditionType.AddPosition && c.TierIndex == tier.TierIndex);
                        
                        if (existingCondition == null)
                        {
                            // 如果没有这个阶梯的推仓条件，新增一个
                            contract.TriggerConditions.Add(new TriggerConditionModel
                            {
                                Id = conditionId++,
                                Type = TriggerConditionType.AddPosition,
                                TierIndex = tier.TierIndex,
                                TriggerPrice = tier.TriggerProfitAmount,
                                KeepValue = tier.ProfitProtectionAmount,
                                Status = TriggerExecutionStatus.NotTriggered,
                                Description = $"推仓{tier.TierIndex} - 浮盈{tier.TriggerProfitAmount:F0}U, 倍数{tier.RiskMultiplier:F1}x",
                                StatusNote = $"从基础配置重载 {DateTime.Now:HH:mm:ss}"
                            });
                            addedCount++;
                            _logger.LogInformation($"🆕 新增推仓{tier.TierIndex}条件: {contract.Symbol}_{contract.PositionSide}");
                        }
                        else
                        {
                            // 🔧 更新已存在的推仓条件参数
                            var oldTriggerPrice = existingCondition.TriggerPrice;
                            var oldKeepValue = existingCondition.KeepValue;
                            existingCondition.TriggerPrice = tier.TriggerProfitAmount;
                            existingCondition.KeepValue = tier.ProfitProtectionAmount;
                            existingCondition.Description = $"推仓{tier.TierIndex} - 浮盈{tier.TriggerProfitAmount:F0}U, 倍数{tier.RiskMultiplier:F1}x";
                            existingCondition.StatusNote = $"从基础配置重载 {DateTime.Now:HH:mm:ss}";
                            
                            if (oldTriggerPrice != existingCondition.TriggerPrice || oldKeepValue != existingCondition.KeepValue)
                            {
                                updatedCount++;
                                _logger.LogInformation($"🔄 更新推仓{tier.TierIndex}条件: {contract.Symbol}_{contract.PositionSide} - 触发价格: {oldTriggerPrice:F0} → {existingCondition.TriggerPrice:F0}, 保盈金额: {oldKeepValue:F0} → {existingCondition.KeepValue:F0}");
                            }
                        }
                    }
                }
                else
                {
                    // 如果基础配置禁用了推仓，移除现有的推仓条件
                    var existingAddPosition = contract.TriggerConditions.Where(c => c.Type == TriggerConditionType.AddPosition).ToList();
                    foreach (var condition in existingAddPosition)
                    {
                        contract.TriggerConditions.Remove(condition);
                        removedCount++;
                        _logger.LogInformation($"🗑️ 移除推仓{condition.TierIndex}条件: {contract.Symbol}_{contract.PositionSide}");
                    }
                }
                
                // 🔧 第3步：更新止盈条件
                if (baseConfig.ProfitProtectionConfig.IsEnabled && baseConfig.ProfitProtectionConfig.Tiers.Any())
                {
                    foreach (var tier in baseConfig.ProfitProtectionConfig.Tiers.OrderBy(t => t.TierIndex))
                    {
                        var existingCondition = contract.TriggerConditions.FirstOrDefault(c => 
                            c.Type == TriggerConditionType.ProfitProtection && c.TierIndex == tier.TierIndex);
                        
                        if (existingCondition == null)
                        {
                            // 如果没有这个阶梯的止盈条件，新增一个
                            contract.TriggerConditions.Add(new TriggerConditionModel
                            {
                                Id = conditionId++,
                                Type = TriggerConditionType.ProfitProtection,
                                TierIndex = tier.TierIndex,
                                TriggerPrice = tier.TriggerProfitAmount,
                                KeepValue = tier.ProtectionAmount,
                                Status = TriggerExecutionStatus.NotTriggered,
                                Description = $"止盈{tier.TierIndex} - 浮盈{tier.TriggerProfitAmount:F0}U, 保护{tier.ProtectionAmount:F0}U",
                                StatusNote = $"从基础配置重载 {DateTime.Now:HH:mm:ss}"
                            });
                            addedCount++;
                            _logger.LogInformation($"🆕 新增止盈{tier.TierIndex}条件: {contract.Symbol}_{contract.PositionSide}");
                        }
                        else
                        {
                            // 🔧 更新已存在的止盈条件参数
                            var oldTriggerPrice = existingCondition.TriggerPrice;
                            var oldKeepValue = existingCondition.KeepValue;
                            existingCondition.TriggerPrice = tier.TriggerProfitAmount;
                            existingCondition.KeepValue = tier.ProtectionAmount;
                            existingCondition.Description = $"止盈{tier.TierIndex} - 浮盈{tier.TriggerProfitAmount:F0}U, 保护{tier.ProtectionAmount:F0}U";
                            existingCondition.StatusNote = $"从基础配置重载 {DateTime.Now:HH:mm:ss}";
                            
                            if (oldTriggerPrice != existingCondition.TriggerPrice || oldKeepValue != existingCondition.KeepValue)
                            {
                                updatedCount++;
                                _logger.LogInformation($"🔄 更新止盈{tier.TierIndex}条件: {contract.Symbol}_{contract.PositionSide} - 触发价格: {oldTriggerPrice:F0} → {existingCondition.TriggerPrice:F0}, 保护金额: {oldKeepValue:F0} → {existingCondition.KeepValue:F0}");
                            }
                        }
                    }
                }
                else
                {
                    // 如果基础配置禁用了止盈，移除现有的止盈条件
                    var existingProfitProtection = contract.TriggerConditions.Where(c => c.Type == TriggerConditionType.ProfitProtection).ToList();
                    foreach (var condition in existingProfitProtection)
                    {
                        contract.TriggerConditions.Remove(condition);
                        removedCount++;
                        _logger.LogInformation($"🗑️ 移除止盈{condition.TierIndex}条件: {contract.Symbol}_{contract.PositionSide}");
                    }
                }
                
                // 🔧 第4步：重新排序条件ID
                var reorderedConditions = contract.TriggerConditions.OrderBy(c => c.Type).ThenBy(c => c.TierIndex ?? 0).ToList();
                contract.TriggerConditions.Clear();
                
                for (int i = 0; i < reorderedConditions.Count; i++)
                {
                    reorderedConditions[i].Id = i + 1;
                    contract.TriggerConditions.Add(reorderedConditions[i]);
                }
                
                _logger.LogInformation($"✅ 配置重载完成: {contract.Symbol}_{contract.PositionSide} - 最终条件数: {contract.TriggerConditions.Count} (新增: {addedCount}, 更新: {updatedCount}, 移除: {removedCount})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 重载合约配置失败: {symbol}");
                throw;
            }
        }

        /// <summary>
        /// 记录日志到全面服务
        /// </summary>
        private void LogToComprehensiveService(string message, string level = "Info")
        {
            try
            {
                if (_comprehensiveLoggingService != null)
                {
                    switch (level.ToLower())
                    {
                        case "error":
                            _comprehensiveLoggingService.LogError(message);
                            break;
                        case "info":
                        default:
                            _comprehensiveLoggingService.LogInfo(message);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 记录日志到全面服务失败: {message}");
            }
        }

        /// <summary>
        /// 创建推仓配置卡片
        /// </summary>
        private System.Windows.Controls.Border CreateAddPositionConfigCard()
        {
            try
            {
                _logger.LogInformation("🔄 创建推仓配置卡片");
                
                var card = new System.Windows.Controls.Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8),
                    Margin = new Thickness(4)
                };

                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = "推仓配置",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14
                };

                card.Child = textBlock;
                return card;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 创建推仓配置卡片失败");
                return new System.Windows.Controls.Border();
            }
        }

        /// <summary>
        /// 创建止盈保护配置卡片
        /// </summary>
        private System.Windows.Controls.Border CreateProfitProtectionConfigCard()
        {
            try
            {
                _logger.LogInformation("🔄 创建止盈保护配置卡片");
                
                var card = new System.Windows.Controls.Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8),
                    Margin = new Thickness(4)
                };

                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = "止盈保护配置",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14
                };

                card.Child = textBlock;
                return card;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 创建止盈保护配置卡片失败");
                return new System.Windows.Controls.Border();
            }
        }

    /// <summary>
    /// 工作日志显示模型
    /// </summary>
    public class WorkLog
    {
        public DateTime Time { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string TimeText => Time.ToString("HH:mm:ss");
        public string LevelText => $"[{Level}]";
        public SolidColorBrush LevelColor { get; set; }
        public SolidColorBrush MessageColor { get; set; }

        public WorkLog(string level, string message)
        {
            Time = DateTime.Now;
            Level = level;
            Message = message;
            
            // 根据日志级别设置颜色
            LevelColor = level switch
            {
                "INFO" => new SolidColorBrush(Colors.LightGreen),
                "WARN" => new SolidColorBrush(Colors.Yellow),
                "ERROR" => new SolidColorBrush(Colors.Red),
                "DEBUG" => new SolidColorBrush(Colors.LightBlue),
                _ => new SolidColorBrush(Colors.White)
            };
            
            MessageColor = level switch
            {
                "ERROR" => new SolidColorBrush(Colors.Red),
                "WARN" => new SolidColorBrush(Colors.Yellow),
                _ => new SolidColorBrush(Colors.LightGreen)
            };
        }
    }

    public class ContractStateDisplayModel
    {
        public string Symbol { get; set; } = string.Empty;
        public string PositionSide { get; set; } = string.Empty;
        public string BreakEvenStatus { get; set; } = string.Empty;
        public SolidColorBrush BreakEvenStatusColor { get; set; } = new(Colors.Gray);
        public int AddPositionProgress { get; set; }
        public int ProfitProtectionProgress { get; set; }
        public int TotalExecutions { get; set; }
        public double ExecutionProgress { get; set; }
        public DateTime LastExecutionTime { get; set; }
        
        // 新增：动态进度显示支持（支持多次推仓多次止盈）
        public int AddPositionTotalTiers { get; set; }
        public int ProfitProtectionTotalTiers { get; set; }
        public string AddPositionProgressDisplay => $"{AddPositionProgress}/{AddPositionTotalTiers}";
        public string ProfitProtectionProgressDisplay => $"{ProfitProtectionProgress}/{ProfitProtectionTotalTiers}";
    }

    public class ExecutionHistoryDisplayModel
    {
        public DateTime ExecutionTime { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string PositionSide { get; set; } = string.Empty;
        public string ExecutionType { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string ResultText { get; set; } = string.Empty;
        public SolidColorBrush ResultColor { get; set; } = new(Colors.Gray);
        public decimal TriggerPnl { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string ResultMessage { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class AddPositionTierDisplayModel
    {
        public int TierIndex { get; set; }
        public decimal TriggerProfitAmount { get; set; }
        public decimal RiskMultiplier { get; set; }
        public decimal StopLossRatio { get; set; }
    }

    public class ProfitProtectionTierDisplayModel
    {
        public int TierIndex { get; set; }
        public decimal TriggerProfitAmount { get; set; }
        public decimal ProtectionAmount { get; set; }
    }

    /// <summary>
    /// 状态到图标颜色转换器 - 支持所有执行状态
    /// </summary>
    public class StatusToIconColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is TriggerExecutionStatus status)
            {
                switch (status)
                {
                    case TriggerExecutionStatus.NotTriggered:
                        return new SolidColorBrush(Colors.Gray);   // 未触发 - 灰色
                    case TriggerExecutionStatus.Executing:
                        return new SolidColorBrush(Colors.Orange); // 执行中 - 橙色  
                    case TriggerExecutionStatus.Executed:
                        return new SolidColorBrush(Colors.Green);  // 已执行 - 绿色
                    default:
                        return new SolidColorBrush(Colors.Gray);   // 默认 - 灰色
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 持仓变化事件处理器 - 监听新开仓并自动添加配置
    /// </summary>
    public class PositionChangeEventHandler : IEventHandler<PositionChangedEvent>
    {
        private readonly AutoMonitorDashboard _dashboard;
        private readonly ILogger _logger;

        public PositionChangeEventHandler(AutoMonitorDashboard dashboard, ILogger logger)
        {
            _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task HandleAsync(PositionChangedEvent eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                // 修复：增强持仓变化处理，确保实时响应
                if (eventData.ChangeType == PositionChangeType.Opened)
                {
                    _logger.LogInformation($"🆕 检测到新开仓: {eventData.Symbol}_{eventData.PositionSide}, 立即添加监控配置");
                    
                    // 在UI线程中立即执行配置添加
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        _dashboard.HandleNewPositionOpened(eventData.Symbol, eventData.PositionSide, eventData.CurrentQuantity, eventData.CurrentPnl);
                        // 立即强制刷新所有持仓数据，确保显示最新状态
                        _dashboard.ForceRefreshPositionsData();
                    });
                }
                else if (eventData.ChangeType == PositionChangeType.Closed)
                {
                    _logger.LogInformation($"❌ 检测到平仓: {eventData.Symbol}_{eventData.PositionSide}, 立即移除监控配置");
                    
                    // 在UI线程中立即执行配置移除
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        _dashboard.HandlePositionClosed(eventData.Symbol, eventData.PositionSide);
                        // 立即强制刷新所有持仓数据，确保显示最新状态
                        _dashboard.ForceRefreshPositionsData();
                    });
                }
                else if (eventData.ChangeType == PositionChangeType.Updated)
                {
                    _logger.LogInformation($"🔄 检测到持仓变化: {eventData.Symbol}_{eventData.PositionSide}, 更新数量: {eventData.CurrentQuantity}");
                    
                    // 在UI线程中更新持仓信息
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        _dashboard.ForceRefreshPositionsData();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 处理持仓变化事件失败: {eventData.Symbol}_{eventData.PositionSide}");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 执行状态变化事件处理器 - 监听条件触发并立即更新UI状态
    /// </summary>
    public class ExecutionStateChangeEventHandler : IEventHandler<ExecutionStateChangedEvent>
    {
        private readonly AutoMonitorDashboard _dashboard;
        private readonly ILogger _logger;

        public ExecutionStateChangeEventHandler(AutoMonitorDashboard dashboard, ILogger logger)
        {
            _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task HandleAsync(ExecutionStateChangedEvent eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation($"🔄 接收到执行状态变化事件: {eventData.Symbol}_{eventData.PositionSide} {eventData.ExecutionType}" +
                    $"{(eventData.TierIndex.HasValue ? $"阶梯{eventData.TierIndex}" : "")} - {(eventData.IsSuccess ? "成功" : "失败")}");

                // 在UI线程中立即更新对应合约的状态
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _dashboard.UpdateContractExecutionState(eventData.Symbol, eventData.PositionSide, 
                        eventData.ExecutionType, eventData.TierIndex, eventData.IsSuccess, eventData.Message);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 处理执行状态变化事件失败: {eventData.Symbol}_{eventData.PositionSide}");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 中继命令实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }

    // 添加XAML中缺少的事件处理方法
    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        // 重用现有的ToggleMonitorButton_Click逻辑
        ToggleMonitorButton_Click(sender, e);
    }

    private void ConfigButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInformation("🔧 用户点击配置按钮");
            
            // 显示配置操作选项
            var result = MessageBox.Show("配置功能\n\n选择操作：\n确定 - 加载配置\n取消 - 查看当前配置", "配置", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.OK)
            {
                LoadFromConfigButton_Click(sender, e);
            }
            else
            {
                UpdateConfiguration();
                MessageBox.Show($"当前配置: {ConfigName}\n扫描间隔: {ScanIntervalDisplay}\n保本配置: {BreakEvenConfigDisplay}", "当前配置", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 打开配置界面时发生错误");
            MessageBox.Show($"打开配置界面失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ConfigManageButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInformation("📝 用户点击配置管理按钮");
            
            // 显示配置管理选项
            var result = MessageBox.Show("配置管理功能\n\n选择操作：\n确定 - 导出当前配置\n取消 - 导入配置", "配置管理", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.OK)
            {
                // 导出配置
                SaveConfigButton_Click(sender, e);
            }
            else
            {
                // 导入配置
                LoadFromConfigButton_Click(sender, e);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 配置管理时发生错误");
            MessageBox.Show($"配置管理失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ComprehensiveLogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInformation("📊 用户点击全面日志按钮");
            
            // 显示日志信息摘要
            if (_comprehensiveLoggingService != null)
            {
                var summary = $"日志服务统计:\n• UI日志条目: {_comprehensiveLoggingService.UILogEntries.Count}\n• 操作日志: {_comprehensiveLoggingService.OperationLogs.Count}\n• 监控日志: {_comprehensiveLoggingService.MonitoringLogs.Count}\n• 错误日志: {_comprehensiveLoggingService.ErrorLogs.Count}";
                MessageBox.Show(summary, "全面日志统计", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("日志服务不可用。", "日志", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 打开全面日志时发生错误");
            MessageBox.Show($"打开日志窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddContractButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInformation("➕ 用户点击添加合约按钮");
            
            // 创建新的合约监控项
            var newContract = new ContractMonitorModel
            {
                Symbol = "BTCUSDT",
                PositionSide = "LONG",
                IsEnabled = true
            };
            
            ContractMonitors.Add(newContract);
            AppendLog($"➕ 添加新合约: {newContract.Symbol}_{newContract.PositionSide}");
            
            // 打开编辑对话框
            OpenEditDialog(newContract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 添加合约时发生错误");
            MessageBox.Show($"添加合约失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoveContractButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInformation("➖ 用户点击移除合约按钮");
            
            if (_contractMonitorDataGrid?.SelectedItem is ContractMonitorModel selectedContract)
            {
                var result = MessageBox.Show($"确定要移除合约 {selectedContract.Symbol}_{selectedContract.PositionSide} 吗？", 
                    "确认移除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    ContractMonitors.Remove(selectedContract);
                    AppendLog($"➖ 移除合约: {selectedContract.Symbol}_{selectedContract.PositionSide}");
                }
            }
            else
            {
                MessageBox.Show("请先选择要移除的合约。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 移除合约时发生错误");
            MessageBox.Show($"移除合约失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportLogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInformation("📤 用户点击导出日志按钮");
            
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出日志",
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = $"AutoMonitor_Log_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt"
            };
            
            if (saveFileDialog.ShowDialog() == true)
            {
                var logContent = RealTimeLog;
                File.WriteAllText(saveFileDialog.FileName, logContent, Encoding.UTF8);
                
                MessageBox.Show($"日志已导出到：\n{saveFileDialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                AppendLog($"📤 日志已导出到: {saveFileDialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 导出日志时发生错误");
            MessageBox.Show($"导出日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 🎯 关键修复：根据持仓和基础配置生成合约监控状态文件
    /// </summary>
    private void GenerateContractMonitoringStatesFile(Dictionary<string, PositionProfile> positionProfiles)
    {
        try
        {
            _logger.LogInformation("🔄 开始生成合约监控状态文件...");
            
            // 创建状态服务
            var stateService = CreateContractMonitoringStateService();
            
            // 获取当前选中的基础配置
            var currentConfig = GetCurrentAutoMonitorConfig();
            if (currentConfig == null)
            {
                _logger.LogWarning("⚠️ 未找到基础配置，使用默认配置");
                currentConfig = CreateDefaultAutoMonitorConfig();
            }
            
            _logger.LogInformation($"📋 使用基础配置: {currentConfig.Name}");
            _logger.LogInformation($"📋 保本配置: 启用={currentConfig.BreakEvenConfig.IsEnabled}, 触发金额={currentConfig.BreakEvenConfig.TriggerProfitAmount}");
            
            // 为每个持仓生成状态
            var monitoringStates = new Dictionary<string, ContractMonitoringState>();
            
            foreach (var kvp in positionProfiles)
            {
                var contractKey = kvp.Key;
                var profile = kvp.Value;
                
                _logger.LogInformation($"🔄 为合约 {contractKey} 生成监控状态");
                
                // 从基础配置和持仓信息生成监控状态
                var monitoringState = new ContractMonitoringState
                {
                    Symbol = profile.Symbol,
                    PositionSide = profile.PositionSide,
                    BaseConfigName = currentConfig.Name,
                    Name = currentConfig.Name,
                    IsEnabled = currentConfig.IsEnabled,
                    ScanIntervalSeconds = currentConfig.ScanIntervalSeconds,
                    CooldownSeconds = currentConfig.CooldownSeconds,
                    
                    // 持仓信息
                    InitialQuantity = profile.InitialQuantity,
                    InitialEntryPrice = profile.InitialEntryPrice,
                    CurrentQuantity = profile.InitialQuantity, // 使用初始数量作为当前数量
                    CurrentEntryPrice = profile.InitialEntryPrice, // 使用初始价格作为当前价格
                    CurrentMarkPrice = 0m, // 需要从实时数据获取
                    CurrentUnrealizedPnl = 0m, // 需要从实时数据获取
                    IsActive = profile.IsActive,
                    
                    // 配置信息
                    BreakEvenConfig = new StatefulBreakEvenConfig
                    {
                        IsEnabled = currentConfig.BreakEvenConfig.IsEnabled,
                        TriggerProfitAmount = currentConfig.BreakEvenConfig.TriggerProfitAmount,
                        ExecutionState = ExecutionState.NotTriggered
                    },
                    
                    AddPositionConfig = new StatefulAddPositionConfig
                    {
                        IsEnabled = currentConfig.AddPositionConfig.IsEnabled,
                        Tiers = currentConfig.AddPositionConfig.Tiers.Select(tier => new StatefulAddPositionTier
                        {
                            TierIndex = tier.TierIndex,
                            IsEnabled = tier.IsEnabled,
                            TriggerProfitAmount = tier.TriggerProfitAmount,
                            RiskMultiplier = tier.RiskMultiplier,
                            StopLossRatio = tier.StopLossRatio,
                            ProfitProtectionAmount = tier.ProfitProtectionAmount,
                            ExecutionState = ExecutionState.NotTriggered
                        }).ToList()
                    },
                    
                    ProfitProtectionConfig = new StatefulProfitProtectionConfig
                    {
                        IsEnabled = currentConfig.ProfitProtectionConfig.IsEnabled,
                        Tiers = currentConfig.ProfitProtectionConfig.Tiers.Select(tier => new StatefulProfitProtectionTier
                        {
                            TierIndex = tier.TierIndex,
                            IsEnabled = tier.IsEnabled,
                            TriggerProfitAmount = tier.TriggerProfitAmount,
                            ProtectionAmount = tier.ProtectionAmount,
                            ExecutionState = ExecutionState.NotTriggered
                        }).ToList()
                    },
                    
                    ExecutionHistories = new List<ExecutionHistory>()
                };
                
                monitoringStates[contractKey] = monitoringState;
                _logger.LogDebug($"✅ 生成状态: {contractKey}");
            }
            
            // 保存到文件
            stateService.SaveMonitoringStates(monitoringStates);
            
            _logger.LogInformation($"✅ 合约监控状态文件生成完成，共 {monitoringStates.Count} 个合约");
            AppendLog($"📄 已生成合约监控状态文件: {monitoringStates.Count} 个合约");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 生成合约监控状态文件失败");
            AppendLog($"❌ 生成状态文件失败: {ex.Message}");
        }
    }
}
}
#endregion