using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using System.Linq;

namespace BinanceFuturesTrader.Views.AutoMonitor.Models
{
    /// <summary>
    /// 自动盯盘数据模型
    /// 包含所有必要的数据绑定属性
    /// </summary>
    public class AutoMonitorDataModel : INotifyPropertyChanged
    {
        #region 私有字段
        
        private string _monitorStatus = "未启动";
        private string _configName = "默认配置";
        private int _scanIntervalSeconds = 5;
        private bool _isAutoScrollEnabled = true;
        private DateTime _lastScanTime;
        private int _totalPositions = 0;
        private int _activePositions = 0;
        private decimal _totalUnrealizedPnl = 0;
        private decimal _dailyPnl = 0;
        private string _systemStatus = "正常";
        private string _connectionStatus = "已连接";
        private bool _isConfigLoaded = false;
        private string _currentTheme = "默认主题";
        private bool _autoScroll = true;
        private bool _showOnlyActiveContracts = false;
        private bool _enableSoundNotification = true;
        private int _logRetentionDays = 7;
        private int _maxLogFileSize = 100;
        private string _logLevel = "INFO";
        private bool _enableDetailedLogging = false;
        private string _lastOperationTime = "";
        private string _lastOperationResult = "";
        private int _scanCount = 0;
        private int _errorCount = 0;
        private int _warningCount = 0;
        private DateTime _startTime;
        private TimeSpan _uptime;
        private string _version = "2.0.0";
        private bool _isConnected = true;
        private string _apiStatus = "正常";
        private double _cpuUsage = 0;
        private double _memoryUsage = 0;
        private string _diskSpace = "充足";
        private string _networkStatus = "良好";
        private bool _isLoading = false;
        private string _loadingMessage = "";
        private bool _hasError = false;
        private string _errorMessage = "";
        private DateTime _lastUpdateTime;
        private DateTime _nextScanDateTime;
        private string _scanCountdownDisplay = "00:00";
        
        #endregion
        
        #region 构造函数
        
        public AutoMonitorDataModel()
        {
            // 初始化集合
            WorkLogs = new ObservableCollection<WorkLog>();
            ContractStateDisplays = new ObservableCollection<ContractStateDisplayModel>();
            ExecutionHistoryDisplays = new ObservableCollection<ExecutionHistoryDisplayModel>();
            AddPositionTierDisplays = new ObservableCollection<AddPositionTierDisplayModel>();
            ProfitProtectionTierDisplays = new ObservableCollection<ProfitProtectionTierDisplayModel>();
            ContractMonitors = new ObservableCollection<ContractMonitorModel>();
            
            // 初始化时间
            _startTime = DateTime.Now;
            _lastScanTime = DateTime.Now;
            _lastUpdateTime = DateTime.Now;
            _nextScanDateTime = DateTime.Now;
            
            // 监听集合变化
            WorkLogs.CollectionChanged += (s, e) => OnPropertyChanged(nameof(WorkLogs));
            ContractStateDisplays.CollectionChanged += (s, e) => OnPropertyChanged(nameof(ContractStateDisplays));
            ExecutionHistoryDisplays.CollectionChanged += (s, e) => OnPropertyChanged(nameof(ExecutionHistoryDisplays));
            AddPositionTierDisplays.CollectionChanged += (s, e) => OnPropertyChanged(nameof(AddPositionTierDisplays));
            ProfitProtectionTierDisplays.CollectionChanged += (s, e) => OnPropertyChanged(nameof(ProfitProtectionTierDisplays));
            ContractMonitors.CollectionChanged += (s, e) => OnPropertyChanged(nameof(ContractMonitors));
        }
        
        #endregion
        
        #region 核心状态属性
        
        /// <summary>
        /// 监控状态
        /// </summary>
        public string MonitorStatus
        {
            get => _monitorStatus;
            set { _monitorStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsMonitoringActive)); }
        }
        
        /// <summary>
        /// 是否正在监控
        /// </summary>
        public bool IsMonitoringActive
        {
            get => _monitorStatus == "运行中" || _monitorStatus == "正在扫描";
        }
        
        /// <summary>
        /// 配置名称
        /// </summary>
        public string ConfigName
        {
            get => _configName;
            set { _configName = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 扫描间隔秒数
        /// </summary>
        public int ScanIntervalSeconds
        {
            get => _scanIntervalSeconds;
            set { _scanIntervalSeconds = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 是否启用自动滚动
        /// </summary>
        public bool IsAutoScrollEnabled
        {
            get => _isAutoScrollEnabled;
            set { _isAutoScrollEnabled = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 上次扫描时间
        /// </summary>
        public DateTime LastScanTime
        {
            get => _lastScanTime;
            set { _lastScanTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastScanTimeText)); }
        }
        
        /// <summary>
        /// 总持仓数
        /// </summary>
        public int TotalPositions
        {
            get => _totalPositions;
            set { _totalPositions = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 活跃持仓数
        /// </summary>
        public int ActivePositions
        {
            get => _activePositions;
            set { _activePositions = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 总未实现盈亏
        /// </summary>
        public decimal TotalUnrealizedPnl
        {
            get => _totalUnrealizedPnl;
            set { _totalUnrealizedPnl = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalUnrealizedPnlText)); }
        }
        
        /// <summary>
        /// 日盈亏
        /// </summary>
        public decimal DailyPnl
        {
            get => _dailyPnl;
            set { _dailyPnl = value; OnPropertyChanged(); OnPropertyChanged(nameof(DailyPnlText)); }
        }
        
        /// <summary>
        /// 系统状态
        /// </summary>
        public string SystemStatus
        {
            get => _systemStatus;
            set { _systemStatus = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 连接状态
        /// </summary>
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }
        
        #endregion
        
        #region 数据集合属性
        
        /// <summary>
        /// 工作日志集合
        /// </summary>
        public ObservableCollection<WorkLog> WorkLogs { get; }
        
        /// <summary>
        /// 合约状态显示集合
        /// </summary>
        public ObservableCollection<ContractStateDisplayModel> ContractStateDisplays { get; }
        
        /// <summary>
        /// 执行历史显示集合
        /// </summary>
        public ObservableCollection<ExecutionHistoryDisplayModel> ExecutionHistoryDisplays { get; }
        
        /// <summary>
        /// 加仓档位显示集合
        /// </summary>
        public ObservableCollection<AddPositionTierDisplayModel> AddPositionTierDisplays { get; }
        
        /// <summary>
        /// 止盈保护档位显示集合
        /// </summary>
        public ObservableCollection<ProfitProtectionTierDisplayModel> ProfitProtectionTierDisplays { get; }
        
        /// <summary>
        /// 合约监控集合
        /// </summary>
        public ObservableCollection<ContractMonitorModel> ContractMonitors { get; }
        
        #endregion
        
        #region 配置和设置属性
        
        /// <summary>
        /// 配置是否已加载
        /// </summary>
        public bool IsConfigLoaded
        {
            get => _isConfigLoaded;
            set { _isConfigLoaded = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 当前主题
        /// </summary>
        public string CurrentTheme
        {
            get => _currentTheme;
            set { _currentTheme = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 自动滚动
        /// </summary>
        public bool AutoScroll
        {
            get => _autoScroll;
            set { _autoScroll = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 只显示活跃合约
        /// </summary>
        public bool ShowOnlyActiveContracts
        {
            get => _showOnlyActiveContracts;
            set { _showOnlyActiveContracts = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 启用声音通知
        /// </summary>
        public bool EnableSoundNotification
        {
            get => _enableSoundNotification;
            set { _enableSoundNotification = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 日志保留天数
        /// </summary>
        public int LogRetentionDays
        {
            get => _logRetentionDays;
            set { _logRetentionDays = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 最大日志文件大小
        /// </summary>
        public int MaxLogFileSize
        {
            get => _maxLogFileSize;
            set { _maxLogFileSize = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 日志级别
        /// </summary>
        public string LogLevel
        {
            get => _logLevel;
            set { _logLevel = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 启用详细日志
        /// </summary>
        public bool EnableDetailedLogging
        {
            get => _enableDetailedLogging;
            set { _enableDetailedLogging = value; OnPropertyChanged(); }
        }
        
        #endregion
        
        #region 运行时统计属性
        
        /// <summary>
        /// 扫描次数
        /// </summary>
        public int ScanCount
        {
            get => _scanCount;
            set { _scanCount = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 错误次数
        /// </summary>
        public int ErrorCount
        {
            get => _errorCount;
            set { _errorCount = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 警告次数
        /// </summary>
        public int WarningCount
        {
            get => _warningCount;
            set { _warningCount = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 启动时间
        /// </summary>
        public DateTime StartTime
        {
            get => _startTime;
            set { _startTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(StartTimeText)); }
        }
        
        /// <summary>
        /// 运行时间
        /// </summary>
        public TimeSpan Uptime
        {
            get => _uptime;
            set { _uptime = value; OnPropertyChanged(); OnPropertyChanged(nameof(UptimeText)); }
        }
        
        /// <summary>
        /// 版本
        /// </summary>
        public string Version
        {
            get => _version;
            set { _version = value; OnPropertyChanged(); }
        }
        
        #endregion
        
        #region 显示文本属性
        
        /// <summary>
        /// 上次扫描时间文本
        /// </summary>
        public string LastScanTimeText => LastScanTime.ToString("HH:mm:ss");
        
        /// <summary>
        /// 总未实现盈亏文本
        /// </summary>
        public string TotalUnrealizedPnlText => $"{TotalUnrealizedPnl:F2}";
        
        /// <summary>
        /// 日盈亏文本
        /// </summary>
        public string DailyPnlText => $"{DailyPnl:F2}";
        
        /// <summary>
        /// 启动时间文本
        /// </summary>
        public string StartTimeText => StartTime.ToString("MM-dd HH:mm:ss");
        
        /// <summary>
        /// 运行时间文本
        /// </summary>
        public string UptimeText => $"{Uptime.Days}天 {Uptime.Hours:D2}:{Uptime.Minutes:D2}:{Uptime.Seconds:D2}";
        
        /// <summary>
        /// 持仓统计文本
        /// </summary>
        public string PositionStatsText => $"总计: {TotalPositions}, 活跃: {ActivePositions}";
        
        /// <summary>
        /// 系统统计文本
        /// </summary>
        public string SystemStatsText => $"扫描: {ScanCount}, 错误: {ErrorCount}, 警告: {WarningCount}";
        
        #endregion
        
        #region TimerController 需要的属性
        
        /// <summary>
        /// 上次更新时间
        /// </summary>
        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set { _lastUpdateTime = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 下次扫描时间
        /// </summary>
        public DateTime NextScanDateTime
        {
            get => _nextScanDateTime;
            set { _nextScanDateTime = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 扫描倒计时显示
        /// </summary>
        public string ScanCountdownDisplay
        {
            get => _scanCountdownDisplay;
            set { _scanCountdownDisplay = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 更新运行时间
        /// </summary>
        public void UpdateRunningTime()
        {
            if (StartTime != default(DateTime))
            {
                Uptime = DateTime.Now - StartTime;
            }
        }
        
        /// <summary>
        /// 更新统计信息
        /// </summary>
        public void UpdateStatistics()
        {
            TotalPositions = ContractMonitors.Count;
            ActivePositions = ContractMonitors.Count(c => c.IsActive);
        }
        
        #endregion
        
        #region 清理方法
        
        /// <summary>
        /// 清理所有数据（线程安全版本）
        /// </summary>
        public void ClearAllData()
        {
            // 🔧 修复：确保在UI线程中执行所有集合清理操作
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            {
                // 已经在UI线程中
                ClearAllDataCore();
            }
            else
            {
                // 在非UI线程中，调度到UI线程执行
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    ClearAllDataCore();
                });
            }
        }

        /// <summary>
        /// 核心清理方法（必须在UI线程中调用）
        /// </summary>
        private void ClearAllDataCore()
        {
            WorkLogs.Clear();
            ContractStateDisplays.Clear();
            ExecutionHistoryDisplays.Clear();
            AddPositionTierDisplays.Clear();
            ProfitProtectionTierDisplays.Clear();
            ContractMonitors.Clear();
        }
        
        /// <summary>
        /// 重置统计数据
        /// </summary>
        public void ResetStatistics()
        {
            ScanCount = 0;
            ErrorCount = 0;
            WarningCount = 0;
            StartTime = DateTime.Now;
            Uptime = TimeSpan.Zero;
        }
        
        #endregion
        
        #region INotifyPropertyChanged 实现
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
    
    // 简化的显示模型类
    public class ContractStateDisplayModel
    {
        public string Symbol { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public decimal Pnl { get; set; }
    }
    
    public class ExecutionHistoryDisplayModel
    {
        public DateTime ExecutionTime { get; set; }
        public string Symbol { get; set; } = "";
        public string PositionSide { get; set; } = "";
        public string ExecutionType { get; set; } = "";
        public bool IsSuccess { get; set; }
        public string ResultText { get; set; } = "";
        public decimal TriggerPnl { get; set; }
        public string OrderId { get; set; } = "";
        public string ResultMessage { get; set; } = "";
        public string Details { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        
        // 兼容旧属性
        public DateTime Time
        {
            get => ExecutionTime;
            set => ExecutionTime = value;
        }
        
        public string Action
        {
            get => ExecutionType;
            set => ExecutionType = value;
        }
        
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public string Status
        {
            get => ResultText;
            set => ResultText = value;
        }
    }
    
    public class AddPositionTierDisplayModel
    {
        public int TierIndex { get; set; }
        public decimal TriggerPrice { get; set; }
        public decimal Quantity { get; set; }
        public string Status { get; set; } = "";
    }
    
    public class ProfitProtectionTierDisplayModel
    {
        public int TierIndex { get; set; }
        public decimal TriggerPrice { get; set; }
        public decimal KeepValue { get; set; }
        public string Status { get; set; } = "";
    }
}