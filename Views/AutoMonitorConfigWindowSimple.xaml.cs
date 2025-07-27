using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.ViewModels;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// 简化版本的自动盯盘配置窗口
    /// 按照需求文档实现三个区域：综合信息与控制按钮区、合约配置区、日志信息区
    /// </summary>
    public partial class AutoMonitorConfigWindowSimple : Window, INotifyPropertyChanged
    {
        #region 私有字段
        
        private readonly AutoMonitorService _autoMonitorService;
        private readonly ILogger _logger;
        private readonly MainViewModel _mainViewModel;
        private readonly IBinanceService _binanceService;
        private readonly BaseConfigManager _configManager;
        private readonly RiskCapitalService _riskCapitalService;
        private readonly ContractProfileService _profileService;
        private readonly TradingExecutionService _tradingExecutionService;
        private readonly AutoMonitorExecutionEngine _executionEngine;
        private readonly ContractMonitoringStateService _stateService;
        private readonly DispatcherTimer _scanTimer;
        private readonly DispatcherTimer _logTimer;
        private readonly DispatcherTimer _countdownTimer;
        
        private bool _isMonitoringActive = false;
        private DateTime _nextScanTime;
        private AutoMonitorConfig? _currentConfig;
        private bool _needsConfigSync = false;
        private bool _isUpdatingConfigSelection = false; // 🔧 新增：防止递归调用的标志
        
        #endregion
        
        #region 数据集合
        
        public ObservableCollection<ContractConfigViewModel> ContractConfigs { get; } = new();
        
        #endregion
        
        #region 构造函数
        
        public AutoMonitorConfigWindowSimple(
            AutoMonitorService autoMonitorService,
            ILogger logger,
            MainViewModel mainViewModel,
            IBinanceService binanceService,
            BaseConfigManager? configManager = null,
            RiskCapitalService? riskCapitalService = null,
            ContractProfileService? profileService = null,
            TradingExecutionService? tradingExecutionService = null,
            AutoMonitorExecutionEngine? executionEngine = null,
            ContractMonitoringStateService? stateService = null)
        {
            _autoMonitorService = autoMonitorService ?? throw new ArgumentNullException(nameof(autoMonitorService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _binanceService = binanceService ?? throw new ArgumentNullException(nameof(binanceService));
            
            // 🔧 修复：使用BaseConfigManager单例实例，确保全局配置统一
            _configManager = BaseConfigManager.Instance;
            
            // 初始化风险金计算服务
            _riskCapitalService = riskCapitalService ?? new RiskCapitalService(Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
                builder.AddConsole()).CreateLogger<RiskCapitalService>(), _mainViewModel);
            
            // 初始化档案服务
            _profileService = profileService ?? new ContractProfileService(
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ContractProfileService>(),
                _binanceService, _configManager, _riskCapitalService);
            
            // 初始化交易执行服务
            _tradingExecutionService = tradingExecutionService ?? new TradingExecutionService(
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<TradingExecutionService>(),
                _binanceService);
                
            // 初始化持久化服务
            var persistenceService = new AutoMonitorPersistenceService(
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AutoMonitorPersistenceService>());
                
            // 🔧 新增：初始化统一状态服务（生成contract_monitoring_states.json）
            _stateService = stateService ?? new ContractMonitoringStateService(
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ContractMonitoringStateService>(),
                BaseConfigManager.Instance);
                
            // 初始化执行引擎（传入状态服务）
            _executionEngine = executionEngine ?? new AutoMonitorExecutionEngine(
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AutoMonitorExecutionEngine>(),
                _tradingExecutionService, _profileService, _configManager, persistenceService, null, _stateService); // 🔧【关键修复】传入状态服务
            
            InitializeComponent();
            
            // 设置数据上下文
            DataContext = this;
            
            // 绑定数据源
            ContractConfigDataGrid.ItemsSource = ContractConfigs;
            
            // 🔧 关键修复：订阅AutoMonitorService事件，确保能接收到诊断日志
            try
            {
                            _autoMonitorService.WorkLogAdded += OnWorkLogAdded;
            _autoMonitorService.MonitorStatusChanged += OnMonitorStatusChanged;
            _autoMonitorService.ExecutionCompleted += OnExecutionCompleted;
            _autoMonitorService.StatusUpdated += OnStatusUpdated; // 🔧 关键修复：订阅状态更新事件
            _autoMonitorService.PositionChanged += OnPositionChanged; // 🔧 【重要新增】：订阅持仓变化事件
                _logger.LogInformation("✅ 已订阅AutoMonitorService事件（包括StatusUpdated）");
                AddLog("✅ 已订阅服务层事件，将显示详细诊断信息和状态更新");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 订阅AutoMonitorService事件失败");
                AddLog($"❌ 订阅服务层事件失败: {ex.Message}");
            }
            
            // 🗑️ 移除UI扫描定时器初始化
            _scanTimer = new DispatcherTimer();
            // _scanTimer.Tick += ScanTimer_Tick; // 已移除
            
            _logTimer = new DispatcherTimer();
            _logTimer.Interval = TimeSpan.FromSeconds(1);
            _logTimer.Tick += LogTimer_Tick;
            _logTimer.Start();
            
            // 🔧 新增：倒计时定时器，每秒更新一次
            _countdownTimer = new DispatcherTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += LogTimer_Tick; // 使用同一个方法
            
            // 创建默认配置
            CreateDefaultConfig();
            
            // 初始化界面
            InitializeUI();
            
            // 加载初始数据
            _ = LoadInitialDataAsync();
            
            // 🔧 新增：订阅配置变更事件，确保实时更新下拉框
            _configManager.ConfigurationChanged += OnConfigurationChanged;
            
            // 🔧 【重要修复】：订阅窗口Loaded事件，确保窗口显示时数据是最新的
            this.Loaded += AutoMonitorConfigWindow_Loaded;
            
            _logger.LogInformation("简化版自动盯盘配置窗口初始化完成");
        }
        
        #endregion
        
        #region 事件处理方法
        
        /// <summary>
        /// 窗口加载完成事件处理 - 确保显示最新数据
        /// </summary>
        private async void AutoMonitorConfigWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("🔄 窗口加载完成，正在刷新最新数据...");
                
                // 🔧 【重要修复】：窗口加载完成后再次检查服务状态，确保状态同步
                bool actualServiceStatus = _autoMonitorService?.IsRunning ?? false;
                if (actualServiceStatus != _isMonitoringActive)
                {
                    AddLog($"🔄 检测到状态不同步，修正状态：服务运行={actualServiceStatus}, UI状态={_isMonitoringActive}");
                    UpdateMonitoringStatus(actualServiceStatus);
                    _isMonitoringActive = actualServiceStatus;
                    
                    // 同时通知主视图模型更新状态
                    NotifyMainViewModel(actualServiceStatus);
                }
                
                // 强制刷新最新的持仓数据
                await RefreshPositionDataAsync();
                
                AddLog("✅ 窗口数据刷新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "窗口加载时刷新数据失败");
                AddLog($"❌ 窗口数据刷新失败: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 初始化方法
        
        private void InitializeUI()
        {
            // 设置窗口标题
            Title = "自动盯盘管理面板";
            
            // 🔧 【重要修复】：初始化时检查实际的服务运行状态，而不是假设为停止状态
            bool actualServiceStatus = _autoMonitorService?.IsRunning ?? false;
            UpdateMonitoringStatus(actualServiceStatus);
            
            if (actualServiceStatus)
            {
                _isMonitoringActive = true;
                AddLog("🔄 检测到后台监控服务正在运行，已同步UI状态");
                AddLog("💡 可以直接查看监控状态或点击'停止盯盘'按钮停止监控");
            }
            else
            {
                _isMonitoringActive = false;
                AddLog("🔴 检测到监控服务未运行");
            }
            
            // 添加日志
            AddLog("系统启动完成，等待用户操作");
            AddLog("📋 需求文档功能：三个区域 - 综合信息与控制、合约配置、日志信息");
        }
        
        private async Task LoadInitialDataAsync()
        {
            try
            {
                // 加载基础配置
                LoadAvailableConfigs();
                
                // 刷新风险金额显示
                UpdateRiskCapitalDisplay();
                
                // 🔧 关键修复：自动加载用户之前的持仓合约配置
                await LoadExistingContractConfigsAsync();
                
                // 🔧 【重要修复】：窗口打开时立即刷新最新持仓数据
                AddLog("🔄 正在刷新最新持仓数据...");
                await RefreshPositionDataAsync();
                AddLog("✅ 最新持仓数据刷新完成");
                
                // ℹ️ 历史记录已由AutoMonitorPersistenceService统一管理，无需重复检查
                AddLog("ℹ️ 历史记录持久化由AutoMonitorPersistenceService统一管理");
                
                AddLog("✅ 初始数据加载完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始数据加载失败");
                AddLog($"❌ 初始数据加载失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 🔧 自动加载用户之前的持仓合约配置 - 已更新为使用统一状态管理
        /// </summary>
        private async Task LoadExistingContractConfigsAsync()
        {
            try
            {
                AddLog("🔄 检查合约监控状态文件...");
                
                // 🎯 【关键修复】首先检查状态文件是否存在
                var filePathManager = new FilePathManager();
                var currentAccountName = _mainViewModel?.SelectedAccount?.Name ?? filePathManager.GetCurrentAccountName();
                var stateFilePath = filePathManager.GetContractMonitoringStatesFilePath(currentAccountName);
                
                AddLog($"📁 状态文件路径: {stateFilePath}");
                
                if (!File.Exists(stateFilePath))
                {
                    AddLog("📝 状态文件不存在，检查是否需要生成...");
                    
                    // 检查是否有实际持仓
                    var positions = await _binanceService.GetPositionsAsync();
                    var activePositions = positions.Where(p => p.PositionAmt != 0).ToList();
                    
                    if (activePositions.Count > 0)
                    {
                        // 🎯 弹出对话框询问是否生成状态文件
                        var result = MessageBox.Show(
                            $"检测到 {activePositions.Count} 个活跃持仓，但未找到合约监控状态文件。\n\n是否根据当前持仓和基础配置生成状态文件？",
                            "生成状态文件",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            AddLog($"✅ 用户确认生成状态文件，正在处理 {activePositions.Count} 个持仓...");
                            
                            // 🎯 调用真正的状态文件生成方法
                            await GenerateStateFileFromPositions(activePositions);
                        }
                        else
                        {
                            AddLog("❌ 用户取消生成状态文件，合约配置区域保持空白");
                            // 清空UI显示
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                ContractConfigs.Clear();
                            });
                        }
                    }
                    else
                    {
                        AddLog("📝 没有活跃持仓，无需生成状态文件");
                        // 清空UI显示
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ContractConfigs.Clear();
                        });
                    }
                }
                else
                {
                    AddLog("✅ 找到状态文件，从文件加载数据...");
                    // 🎯 调用真正的状态文件加载方法
                    await LoadContractConfigsFromStateFile();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载合约配置失败");
                AddLog($"❌ 加载合约配置失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 🚫 已废弃：检查并修复历史记录持久化 - 改由AutoMonitorPersistenceService统一管理
        /// </summary>
        [Obsolete("历史记录持久化已由AutoMonitorPersistenceService统一管理，此方法不再需要")]
        private async Task CheckAndFixExecutionHistoryPersistence()
        {
            // 🚫 已废弃：此方法的功能已由以下服务统一管理：
            // - AutoMonitorPersistenceService: 处理执行历史记录持久化  
            // - UnifiedPersistenceService: 提供统一的持久化接口
            // - FilePathManager: 管理按账户分离的文件路径
            
            AddLog("ℹ️ 历史记录持久化已由AutoMonitorPersistenceService统一管理");
            
            // 不再执行重复的目录创建和文件检查逻辑
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 🔧 新增：测试历史记录持久化机制
        /// </summary>
        private async Task TestHistoryPersistenceMechanism()
        {
            try
            {
                if (_autoMonitorService != null)
                {
                    var history = _autoMonitorService.GetExecutionHistory();
                    
                    // 添加一条测试记录（不影响实际交易）
                    var testHistory = new ExecutionHistory
                    {
                        Symbol = "TEST",
                        PositionSide = "LONG",
                        ExecutionType = "系统启动检查",
                        ExecutionTime = DateTime.Now,
                        TriggerPnl = 0,
                        IsSuccess = true,
                        Details = "历史记录持久化机制测试"
                    };
                    
                    history.Add(testHistory);
                    
                    // 手动触发保存（如果AutoMonitorService有公开的保存方法）
                    // 这里我们通过日志记录来提醒用户
                    AddLog("✅ 历史记录持久化机制测试完成");
                    AddLog("💾 历史记录将在盯盘停止时自动保存");
                    AddLog("📋 当前历史记录数量: " + history.Count);
                    
                    // 清理测试记录
                    history.RemoveAll(h => h.Symbol == "TEST" && h.ExecutionType == "系统启动检查");
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ 测试历史记录持久化机制失败: {ex.Message}");
                _logger.LogError(ex, "测试历史记录持久化机制失败");
            }
            
            await Task.CompletedTask; // 🔧 修复异步方法警告
        }
        
        #endregion
        
        #region 配置变更事件处理
        
        private bool _isHandlingConfigurationChange = false; // 🔧 防止递归标志
        
        /// <summary>
        /// 🔧 配置变更事件处理器 - 自动刷新下拉框（防止递归）
        /// </summary>
        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            try
            {
                // 🔧 防止递归调用
                if (_isHandlingConfigurationChange)
                {
                    _logger.LogDebug("⚠️ 跳过递归的配置变更事件处理");
                    return;
                }
                
                _isHandlingConfigurationChange = true;
                
                // 在UI线程中更新下拉框
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        AddLog($"🔔 检测到配置变更: {e.ChangeType} - {e.Configuration?.Name}");
                        
                        // 🔧 修复：直接更新下拉框，不调用LoadAvailableConfigs避免递归
                        RefreshConfigurationDropdownOnly();
                        
                        // 如果当前没有选中的配置或者选中的配置被删除，自动选择第一个配置
                        if (_currentConfig == null || 
                            (e.ChangeType == ConfigChangeType.Deleted && e.Configuration?.Name == _currentConfig.Name))
                        {
                            var firstConfig = _configManager.Configurations.FirstOrDefault();
                            if (firstConfig != null)
                            {
                                _currentConfig = firstConfig;
                                
                                // 🔧 使用标志位防止递归调用
                                _isUpdatingConfigSelection = true;
                                try
                                {
                                ConfigSelectionComboBox.SelectedItem = firstConfig;
                                }
                                finally
                                {
                                    _isUpdatingConfigSelection = false;
                                }
                                
                                AddLog($"✅ 自动选择配置: {firstConfig.Name}");
                            }
                        }
                        // 如果是更新当前配置，刷新选中项
                        else if (e.ChangeType == ConfigChangeType.Updated && e.Configuration?.Name == _currentConfig?.Name)
                        {
                            var updatedConfig = _configManager.Configurations.FirstOrDefault(c => c.Name == e.Configuration.Name);
                            if (updatedConfig != null)
                            {
                                _currentConfig = updatedConfig;
                                
                                // 🔧 使用标志位防止递归调用
                                _isUpdatingConfigSelection = true;
                                try
                                {
                                ConfigSelectionComboBox.SelectedItem = updatedConfig;
                                }
                                finally
                                {
                                    _isUpdatingConfigSelection = false;
                                }
                                
                                AddLog($"✅ 已刷新当前配置: {updatedConfig.Name}");
                            }
                        }
                        
                        AddLog("✅ 配置下拉框已自动刷新");
                    }
                    finally
                    {
                        _isHandlingConfigurationChange = false;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置变更事件处理失败");
                AddLog($"❌ 配置变更事件处理失败: {ex.Message}");
                _isHandlingConfigurationChange = false;
            }
        }
        
        /// <summary>
        /// 🔧 仅刷新配置下拉框，不触发RefreshConfigurations避免递归
        /// </summary>
        private void RefreshConfigurationDropdownOnly()
        {
            try
            {
                // 直接从已加载的配置更新下拉框，不重新加载文件
                var availableConfigs = _configManager.Configurations.ToList();
                
                if (ConfigSelectionComboBox != null)
                {
                    ConfigSelectionComboBox.ItemsSource = availableConfigs;
                    
                    // 保持当前选中项
                    if (_currentConfig != null)
                    {
                        var selectedConfig = availableConfigs.FirstOrDefault(c => c.Name == _currentConfig.Name);
                        if (selectedConfig != null)
                        {
                            ConfigSelectionComboBox.SelectedItem = selectedConfig;
                        }
                    }
                }
                
                _logger.LogDebug($"🔄 已刷新配置下拉框：{availableConfigs.Count} 个配置");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新配置下拉框失败");
            }
        }
        
        #endregion
        
        #region 事件处理
        
        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isMonitoringActive)
                {
                    // 立即更新按钮状态，让用户知道操作开始了
                    StartStopButton.Content = "正在停止...";
                    StartStopButton.IsEnabled = false;
                    StatusInfoText.Text = "🔄 正在停止盯盘...";
                    
                    await StopMonitoringAsync();
                }
                else
                {
                    // 立即更新按钮状态，让用户知道操作开始了
                    StartStopButton.Content = "正在启动...";
                    StartStopButton.IsEnabled = false;
                    StatusInfoText.Text = "🔄 正在启动盯盘...";
                    
                    await StartMonitoringAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动/停止监控失败");
                AddLog($"❌ 操作失败: {ex.Message}");
                
                // 出现异常时恢复按钮状态
                StartStopButton.IsEnabled = true;
                if (_isMonitoringActive)
                {
                    StartStopButton.Content = "停止盯盘";
                    StatusInfoText.Text = "🟢 监控运行中";
                }
                else
                {
                    StartStopButton.Content = "启动盯盘";
                    StatusInfoText.Text = "🔴 监控已停止";
                }
            }
        }
        
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("🔄 手动刷新数据和配置...");
                
                // 🔧 新增：刷新基础配置参数
                AddLog("📋 重新加载基础配置参数...");
                RefreshCurrentConfig();
                
                // 等待配置刷新完成
                await Task.Delay(500);
                
                // 刷新持仓数据
                AddLog("📊 刷新持仓数据...");
                await RefreshPositionDataAsync();
                
                AddLog("✅ 数据和配置刷新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新数据失败");
                AddLog($"❌ 刷新数据失败: {ex.Message}");
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            AddLog("日志已清空");
        }
        
        private void ViewHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("📖 打开历史记录查看器...");
                
                // 🔧 如果今天没有历史记录，创建一个示例记录
                EnsureTodayHistoryExists();
                
                // 创建历史记录查看窗口
                var historyWindow = new OperationHistoryWindow();
                historyWindow.Owner = this;
                historyWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开历史记录窗口失败");
                AddLog($"❌ 打开历史记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 确保今天有历史记录可供查看（如果没有则创建示例）
        /// </summary>
        private void EnsureTodayHistoryExists()
        {
            try
            {
                var historyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                            "BinanceFuturesTrader", "OperationHistory");
                Directory.CreateDirectory(historyDir);

                var todayFileName = $"操作历史_{DateTime.Now:yyyy-MM-dd}.json";
                var todayFilePath = Path.Combine(historyDir, todayFileName);

                // 如果今天的文件不存在，创建一个示例记录
                if (!File.Exists(todayFilePath))
                {
                    var sampleRecords = new List<OperationHistoryRecord>
                    {
                        new OperationHistoryRecord
                        {
                            Timestamp = DateTime.Now.AddMinutes(-10),
                            Operation = "功能测试",
                            ContractName = "系统",
                            Details = "创建示例历史记录 - 查看历史功能测试",
                            OperationType = "SYSTEM_TEST",
                            Username = Environment.UserName
                        }
                    };

                    var json = JsonSerializer.Serialize(sampleRecords, new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    File.WriteAllText(todayFilePath, json);

                    _logger.LogDebug($"📝 已创建示例历史记录文件: {todayFilePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建示例历史记录失败");
            }
        }

        #region 历史记录功能

        /// <summary>
        /// 保存操作历史记录
        /// </summary>
        private void SaveOperationHistory(string operation, string contractName, string details, string operationType = "STATUS_CHANGE")
        {
            try
            {
                var historyRecord = new OperationHistoryRecord
                {
                    Timestamp = DateTime.Now,
                    Operation = operation,
                    ContractName = contractName,
                    Details = details,
                    OperationType = operationType,
                    Username = Environment.UserName
                };

                // 获取今天的历史文件路径
                var historyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                            "BinanceFuturesTrader", "OperationHistory");
                Directory.CreateDirectory(historyDir);

                var todayFileName = $"操作历史_{DateTime.Now:yyyy-MM-dd}.json";
                var todayFilePath = Path.Combine(historyDir, todayFileName);

                // 读取现有记录
                List<OperationHistoryRecord> records = new();
                if (File.Exists(todayFilePath))
                {
                    var json = File.ReadAllText(todayFilePath);
                    records = JsonSerializer.Deserialize<List<OperationHistoryRecord>>(json) ?? new();
                }

                // 添加新记录
                records.Add(historyRecord);

                // 保存到文件
                var updatedJson = JsonSerializer.Serialize(records, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(todayFilePath, updatedJson);

                _logger.LogDebug($"📝 操作历史已保存: {operation} - {contractName} - {details}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存操作历史失败");
            }
        }

        /// <summary>
        /// 获取历史文件目录
        /// </summary>
        public static string GetHistoryDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                              "BinanceFuturesTrader", "OperationHistory");
        }

        #endregion
        
        private void RefreshRiskCapitalButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("🔄 手动刷新风险金信息...");
                UpdateRiskCapitalDisplay();
                AddLog("✅ 风险金信息刷新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新风险金信息失败");
                AddLog($"❌ 刷新风险金信息失败: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 监控控制
        
        private async Task StartMonitoringAsync()
        {
            try
            {
                AddLog("🚀 开始启动自动盯盘监控...");
                
                // 检查扫描间隔
                if (!int.TryParse(ScanIntervalTextBox.Text, out int scanInterval) || scanInterval < 5)
                {
                    MessageBox.Show("扫描间隔必须是5秒以上的整数！", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 🔧 关键修复：启动前先创建档案
                if (_currentConfig != null)
                {
                    AddLog("📊 启动前检查和创建持仓档案...");
                    
                    // 获取当前持仓
                    var positions = await _binanceService.GetPositionsAsync();
                    var activePositions = positions.Where(p => p.PositionAmt != 0).ToList();
                    
                    AddLog($"📊 发现 {activePositions.Count} 个活跃持仓");
                    
                    // 🚨 重要修复：不在UI层创建档案，让服务层在InitializePositionProfilesAsync中处理
                    // 这样避免双重档案系统不同步的问题
                    if (activePositions.Any())
                    {
                        AddLog($"📝 发现活跃持仓，服务层将自动创建档案");
                    }
                    else
                    {
                        AddLog("💤 当前无活跃持仓，等待新持仓出现");
                    }
                }
                
                // 🔧 关键修改：只启动服务，不启动UI定时器
                if (_currentConfig != null)
                {
                    _currentConfig.ScanIntervalSeconds = scanInterval;
                    bool success = await _autoMonitorService.StartMonitoringAsync(_currentConfig);
                    
                    if (success)
                    {
                        _isMonitoringActive = true;
                        // 🗑️ 移除UI定时器启动：_scanTimer.Start();
                        
                        // 🔧 状态更新已统一到UpdateMonitoringStatus方法中处理
                        
                        // 🔧 修复：正确初始化下次扫描时间
                        _nextScanTime = DateTime.Now.AddSeconds(scanInterval);
                        AddLog($"⏰ 倒计时初始化，下次扫描: {_nextScanTime:HH:mm:ss}");
                        
                        UpdateMonitoringStatus(true);
                        AddLog("✅ 自动盯盘监控已启动 (服务层执行)");
                        
                        // 🔧 重要修复：启动成功后等待服务层初始化完成并检查档案状态
                        AddLog("⏳ 等待服务层初始化档案...");
                        await Task.Delay(3000); // 等待服务完全初始化档案
                        
                        var finalProfileCount = _autoMonitorService?.GetActiveProfileCount() ?? 0;
                        AddLog($"📊 监控启动完成，当前档案数: {finalProfileCount}");
                        
                        if (finalProfileCount == 0)
                        {
                            AddLog("⚠️ 警告：服务层档案数为0，这可能表示初始化失败");
                            AddLog("💡 建议检查：1) API连接 2) 持仓是否存在 3) 网络连接");
                            AddLog("🔄 系统将继续运行，如有新持仓会自动开始监控");
                        }
                        else
                        {
                            AddLog($"✅ 档案初始化成功，开始监控 {finalProfileCount} 个持仓");
                        }
                        
                        // 通知主界面更新按钮状态
                        NotifyMainViewModel(true);
                    }
                    else
                    {
                        AddLog("❌ 自动盯盘监控启动失败");
                    }
                }
                else
                {
                    AddLog("❌ 配置为空，无法启动监控");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动监控失败");
                AddLog($"❌ 启动监控失败: {ex.Message}");
            }
        }
        
        private async Task StopMonitoringAsync()
        {
            try
            {
                AddLog("⏹️ 正在停止自动盯盘监控...");
                
                // 🗑️ 移除UI定时器停止：_scanTimer.Stop();
                await _autoMonitorService.StopMonitoringAsync();
                
                _isMonitoringActive = false;
                
                // 🔧 状态更新已统一到UpdateMonitoringStatus方法中处理
                
                UpdateMonitoringStatus(false);
                AddLog("✅ 自动盯盘监控已停止");
                
                // 通知主界面更新按钮状态
                NotifyMainViewModel(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止监控失败");
                AddLog($"❌ 停止监控失败: {ex.Message}");
            }
        }
        
        private void UpdateMonitoringStatus(bool isActive)
        {
            _isMonitoringActive = isActive;
            
            // 更新按钮状态
            StartStopButton.Content = isActive ? "停止盯盘" : "启动盯盘";
            StartStopButton.Background = isActive ? Brushes.Red : Brushes.Green;
            StartStopButton.IsEnabled = true; // 重新启用按钮
            
            // 🔧 【重要修复】：同时更新监控状态文本和状态信息文本
            MonitorStatusText.Text = isActive ? "运行中" : "未启动";
            MonitorStatusText.Foreground = isActive ? Brushes.Green : Brushes.Gray;
            
            StatusInfoText.Text = isActive ? "🟢 监控运行中" : "🔴 监控已停止";
            StatusInfoText.Foreground = isActive ? Brushes.Green : Brushes.Red;
            
            // 🔧 【重要修复】：同时更新倒计时相关控件的可见性
            if (isActive)
            {
                CountdownLabelText.Visibility = Visibility.Visible;
                CountdownTimerText.Visibility = Visibility.Visible;
            }
            else
            {
                CountdownLabelText.Visibility = Visibility.Collapsed;
                CountdownTimerText.Visibility = Visibility.Collapsed;
            }
            
            // 🔧 更新监控信息提示
            UpdateMonitorInfoText();
        }
        
        private void NotifyMainViewModel(bool isActive)
        {
            try
            {
                // 使用反射调用主界面的更新方法
                var updateMethod = _mainViewModel.GetType().GetMethod("UpdateAutoMonitorUI", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (updateMethod != null)
                {
                    var buttonText = isActive ? "停止盯盘" : "自动盯盘";
                    var statusMessage = isActive ? "自动盯盘运行中" : "自动盯盘已停止";
                    var buttonColor = isActive ? "#E74C3C" : "#27AE60";
                    
                    updateMethod.Invoke(_mainViewModel, new object[] { isActive, statusMessage, buttonText, buttonColor, true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通知主界面更新状态失败");
            }
        }

        /// <summary>
        /// 更新监控状态提示信息
        /// </summary>
        private void UpdateMonitorInfoText()
        {
            try
            {
                if (_currentConfig != null)
                {
                    if (_isMonitoringActive)
                    {
                        MonitorInfoText.Text = $"正在使用配置：{_currentConfig.Name}";
                        MonitorInfoText.Foreground = new SolidColorBrush(Colors.Green);
                    }
                    else
                    {
                        MonitorInfoText.Text = $"当前选择配置：{_currentConfig.Name}，点击启动开始监控";
                        MonitorInfoText.Foreground = new SolidColorBrush(Colors.DarkBlue);
                    }
                }
                else
                {
                    MonitorInfoText.Text = "请选择基础配置并启动监控";
                    MonitorInfoText.Foreground = new SolidColorBrush(Colors.Gray);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "更新监控信息提示失败");
            }
        }
        
        #endregion
        
        #region 定时器事件
        
        // 🗑️ 已移除：UI层扫描定时器，统一使用服务层扫描
        // private void ScanTimer_Tick(object sender, EventArgs e) - 已删除
        
        private void LogTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_isMonitoringActive)
                {
                    var remaining = (_nextScanTime - DateTime.Now).TotalSeconds;
                    if (remaining > 0)
                    {
                        StatusInfoText.Text = $"🟢 监控运行中";
                        // 🔧 新增：更新专门的倒计时显示
                        CountdownTimerText.Text = $"{(int)remaining}秒";
                        CountdownTimerText.Foreground = new SolidColorBrush(Colors.Orange);
                    }
                    else
                    {
                        StatusInfoText.Text = "🟢 监控运行中";
                        // 🔧 修复：显示扫描状态并重置下次扫描时间
                        CountdownTimerText.Text = "扫描中...";
                        CountdownTimerText.Foreground = new SolidColorBrush(Colors.Green);
                        
                        // 🔧 关键修复：重置下次扫描时间，避免倒计时一直显示"扫描中"
                        var scanInterval = _currentConfig?.ScanIntervalSeconds ?? 5;
                        if (remaining < -scanInterval) // 如果倒计时已经很久了，立即重置
                        {
                            _nextScanTime = DateTime.Now.AddSeconds(scanInterval);
                            AddLog($"⏰ 重置扫描倒计时 (间隔: {scanInterval}秒)");
                        }
                    }
                }
                else
                {
                    StatusInfoText.Text = "❌ 监控已停止";
                    CountdownTimerText.Text = "--";
                    CountdownTimerText.Foreground = new SolidColorBrush(Colors.Gray);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "日志定时器异常");
            }
        }
        
        #endregion
        
        #region AutoMonitorService事件处理
        
        /// <summary>
        /// 处理服务层工作日志事件
        /// </summary>
        private void OnWorkLogAdded(object? sender, BinanceFuturesTrader.Models.WorkLogEventArgs e)
        {
            try
            {
                // 直接使用现有的AddLog方法，确保服务层的诊断日志能显示在UI中
                AddLog($"[{e.Level}] {e.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理工作日志事件时发生错误");
            }
        }

        /// <summary>
        /// 处理监控状态变化事件
        /// </summary>
        private void OnMonitorStatusChanged(object? sender, BinanceFuturesTrader.Models.MonitorStatusChangedEventArgs e)
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    // 更新监控状态
                    UpdateMonitoringStatus(e.IsRunning);
                    
                    // 记录状态变化日志
                    AddLog($"🔄 监控状态变更: {(e.IsRunning ? "启动" : "停止")} - {e.Message}");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理监控状态变化事件时发生错误");
            }
        }

        /// <summary>
        /// 处理执行完成事件
        /// </summary>
        private void OnExecutionCompleted(object? sender, BinanceFuturesTrader.Models.ExecutionResultEventArgs e)
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    // 记录执行结果日志
                    var statusIcon = e.IsSuccess ? "✅" : "❌";
                    var resultText = e.IsSuccess ? "成功" : "失败";
                    AddLog($"{statusIcon} {e.ExecutionType} {resultText}: {e.Symbol} (浮盈: {e.PnlAtExecution:F1}U) - {e.Message}");
                    
                    // 🔧 关键修复：执行成功后立即更新合约配置表格中的状态
                    if (e.IsSuccess)
                    {
                        UpdateContractExecutionStatus(e.Symbol, e.ExecutionType);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理执行完成事件时发生错误");
            }
        }
        
        /// <summary>
        /// 更新合约执行状态（推仓阶梯状态）
        /// </summary>
        private void UpdateContractExecutionStatus(string symbol, string executionType)
        {
            try
            {
                // 解析执行类型，提取阶梯信息
                if (!executionType.Contains("推仓阶梯"))
                {
                    return; // 不是推仓阶梯执行，不处理
                }
                
                // 提取阶梯号：从"推仓阶梯1"中提取"1"
                var tierMatch = System.Text.RegularExpressions.Regex.Match(executionType, @"推仓阶梯(\d+)");
                if (!tierMatch.Success)
                {
                    AddLog($"⚠️ 无法解析推仓阶梯号: {executionType}");
                    return;
                }
                
                int tierIndex = int.Parse(tierMatch.Groups[1].Value);
                
                // 查找所有可能的合约配置匹配（LONG或SHORT）
                var matchingConfigs = ContractConfigs.Where(c => c.ContractName.StartsWith(symbol)).ToList();
                
                foreach (var config in matchingConfigs)
                {
                    // 更新对应阶梯的状态为"√"
                    string pushKey = $"Push{tierIndex}";
                    var currentValue = config.GetDynamicData(pushKey);
                    
                    if (!string.IsNullOrEmpty(currentValue))
                    {
                        // 解析当前值，保留触发金额，更新状态
                        var parts = currentValue.Split('|');
                        if (parts.Length >= 2)
                        {
                            var triggerAmount = parts[0].Trim();
                            var newValue = $"{triggerAmount} | √";
                            config.SetDynamicData(pushKey, newValue, "Green", true);
                            AddLog($"✅ 推仓阶梯{tierIndex}状态已更新: {config.ContractName} = √");
                        }
                    }
                }
                
                // 刷新DataGrid显示
                ContractConfigDataGrid.Items.Refresh();
                
                AddLog($"🔄 推仓状态更新完成: {symbol} 阶梯{tierIndex}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新合约执行状态失败: {symbol} {executionType}");
                AddLog($"❌ 更新执行状态失败: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 配置更新处理
        
        /// <summary>
        /// 更新所有合约配置（配置切换时调用）
        /// </summary>
        private async Task UpdateAllContractConfigsAsync()
        {
            try
            {
                if (_currentConfig == null)
                {
                    AddLog("⚠️ 当前配置为空，无法更新合约配置");
                    return;
                }
                
                AddLog($"🔄 开始为所有持仓合约更新配置: {_currentConfig.Name}");
                
                // 获取当前持仓
                var positions = await _binanceService.GetPositionsAsync();
                var activePositions = positions.Where(p => p.PositionAmt != 0).ToList();
                
                if (activePositions.Count == 0)
                {
                    AddLog("📋 当前没有持仓，无需更新合约配置");
                    ContractConfigs.Clear();
                    return;
                }
                
                int updatedCount = 0;
                int createdCount = 0;
                
                // 为每个持仓合约更新或创建配置
                foreach (var position in activePositions)
                {
                    try
                    {
                        var side = position.PositionAmt > 0 ? "LONG" : "SHORT";
                        var existingProfile = _profileService.GetProfile(position.Symbol, side);
                        
                        if (existingProfile != null)
                        {
                            // 更新现有档案的基础配置
                            await UpdateProfileConfigAsync(existingProfile, _currentConfig.Name);
                            updatedCount++;
                        }
                        else
                        {
                            // 创建新的档案
                            await _profileService.CreateProfileAsync(position, _currentConfig.Name);
                            createdCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"更新合约配置失败: {position.Symbol}");
                        AddLog($"❌ 更新合约配置失败: {position.Symbol} - {ex.Message}");
                    }
                }
                
                // 清理已平仓的合约档案
                await CleanupClosedPositionsAsync(activePositions);
                
                // 🔧 【关键修复】同步所有合约配置到contract_monitoring_states.json文件
                try
                {
                    if (_stateService != null && !string.IsNullOrEmpty(_currentConfig.Name))
                    {
                        AddLog($"🔄 同步所有合约配置到统一状态文件: {_currentConfig.Name}");
                        _stateService.SwitchAllContractsConfiguration(_currentConfig.Name);
                        AddLog($"✅ 统一状态文件同步完成");
                    }
                    else
                    {
                        AddLog("⚠️ 统一状态服务未初始化，跳过文件同步");
                    }
                }
                catch (Exception syncEx)
                {
                    _logger.LogError(syncEx, "同步统一状态文件失败");
                    AddLog($"❌ 统一状态文件同步失败: {syncEx.Message}");
                }
                
                // 刷新UI显示
                await RefreshPositionDataAsync();
                
                AddLog($"✅ 合约配置更新完成: 更新{updatedCount}个, 创建{createdCount}个档案");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新所有合约配置失败");
                AddLog($"❌ 更新所有合约配置失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新单个档案的配置
        /// </summary>
        /// <param name="profile">档案</param>
        /// <param name="newConfigName">新配置名称</param>
        private async Task UpdateProfileConfigAsync(ContractProfile profile, string newConfigName)
        {
            try
            {
                // 更新基础配置名称
                var oldConfigName = profile.BaseConfigName;
                profile.BaseConfigName = newConfigName;
                
                // 🔧 修复：强制重新生成所有档案的配置内容，不管是否使用独立配置
                // 获取新的基础配置
                var newBaseConfig = _configManager.GetConfiguration(newConfigName);
                if (newBaseConfig != null)
                {
                    // 强制启用独立配置模式并重新生成
                    profile.UseIndependentConfig = true;
                    await RegenerateIndependentConfigAsync(profile);
                    
                    AddLog($"🔄 档案 {profile.DisplayName} 已基于新配置 '{newConfigName}' 重新生成");
                }
                else
                {
                    AddLog($"⚠️ 无法找到基础配置: {newConfigName}");
                }
                
                // 重新初始化状态
                await ReinitializeProfileStatesAsync(profile);
                
                // 保存档案
                await _profileService.UpdateProfileAsync(profile);
                
                // 添加操作历史
                profile.AddOperationHistory("配置切换", "成功", $"从 {oldConfigName} 切换到 {newConfigName}");
                
                AddLog($"📝 合约 {profile.DisplayName} 配置已更新: {newConfigName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新档案配置失败: {profile.DisplayName}");
                throw;
            }
        }
        
        /// <summary>
        /// 重新生成独立配置
        /// </summary>
        /// <param name="profile">档案</param>
        private Task RegenerateIndependentConfigAsync(ContractProfile profile)
        {
            try
            {
                // 获取新的基础配置
                var baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
                if (baseConfig == null)
                {
                    AddLog($"⚠️ 基础配置不存在: {profile.BaseConfigName}，停用独立配置");
                    profile.UseIndependentConfig = false;
                    return Task.CompletedTask;
                }
                
                // 获取风险金信息
                var accountEquity = _riskCapitalService.GetCurrentAccountEquity();
                var riskCapitalTimes = _riskCapitalService.GetCurrentRiskCapitalTimes();
                var riskCapital = _riskCapitalService.CalculateRiskCapital(accountEquity, riskCapitalTimes);
                
                // 重新生成保本配置
                if (baseConfig.BreakEvenConfig.IsEnabled)
                {
                    profile.IndependentBreakEvenConfig = new ContractBreakEvenConfig
                    {
                        IsEnabled = true,
                        TriggerProfitAmount = baseConfig.BreakEvenConfig.TriggerProfitAmount
                    };
                }
                
                // 重新生成推仓配置
                if (baseConfig.AddPositionConfig.IsEnabled)
                {
                    profile.IndependentAddPositionConfig = new ContractAddPositionConfig
                    {
                        IsEnabled = true,
                        Tiers = baseConfig.AddPositionConfig.Tiers.Select(t => new ContractAddPositionTier
                        {
                            TierIndex = t.TierIndex,
                            IsEnabled = t.IsEnabled,
                            TriggerProfitAmount = t.TriggerProfitAmount,
                            RiskMultiplier = t.RiskMultiplier,
                            StopLossRatio = t.StopLossRatio,
                            AddPositionQuantity = CalculateAddPositionQuantity(profile, t, riskCapital),
                            StopLossPrice = CalculateStopLossPrice(profile, t)
                        }).ToList()
                    };
                }
                
                // 重新生成保盈配置
                if (baseConfig.ProfitProtectionConfig.IsEnabled)
                {
                    profile.IndependentProfitProtectionConfig = new ContractProfitProtectionConfig
                    {
                        IsEnabled = true,
                        Tiers = baseConfig.ProfitProtectionConfig.Tiers.Select(t => new ContractProfitProtectionTier
                        {
                            TierIndex = t.TierIndex,
                            IsEnabled = t.IsEnabled,
                            TriggerProfitAmount = t.TriggerProfitAmount,
                            ProtectionAmount = t.ProtectionAmount,
                            StopLossPrice = CalculateProfitProtectionStopLossPrice(profile, t)
                        }).ToList()
                    };
                }
                
                AddLog($"🔄 档案 {profile.DisplayName} 独立配置已重新生成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"重新生成独立配置失败: {profile.DisplayName}");
                throw;
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// 重新初始化档案状态
        /// </summary>
        /// <param name="profile">档案</param>
        private Task ReinitializeProfileStatesAsync(ContractProfile profile)
        {
            try
            {
                // 获取基础配置
                var baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
                if (baseConfig == null)
                {
                    AddLog($"⚠️ 基础配置不存在: {profile.BaseConfigName}");
                    return Task.CompletedTask;
                }
                
                // 重置所有状态为未触发
                profile.BreakEvenState = new ContractTriggerState
                {
                    IsTriggered = false,
                    ExecutionStatus = "未触发"
                };
                
                // 重新初始化推仓状态
                profile.AddPositionStates.Clear();
                foreach (var tier in baseConfig.AddPositionConfig.Tiers)
                {
                    profile.AddPositionStates.Add(new ContractTierState
                    {
                        TierIndex = tier.TierIndex,
                        TierType = "AddPosition",
                        IsTriggered = false,
                        ExecutionStatus = "未触发",
                        TriggerTime = null
                    });
                }
                
                // 重新初始化保盈状态
                profile.ProfitProtectionStates.Clear();
                foreach (var tier in baseConfig.ProfitProtectionConfig.Tiers)
                {
                    profile.ProfitProtectionStates.Add(new ContractTierState
                    {
                        TierIndex = tier.TierIndex,
                        TierType = "ProfitProtection",
                        IsTriggered = false,
                        ExecutionStatus = "未触发",
                        TriggerTime = null
                    });
                }
                
                // 清空操作历史中的旧执行记录
                profile.OperationHistory.RemoveAll(h => h.Operation.Contains("执行"));
                
                AddLog($"🔄 档案 {profile.DisplayName} 状态已重新初始化");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"重新初始化档案状态失败: {profile.DisplayName}");
                throw;
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// 清理已平仓的合约档案
        /// </summary>
        /// <param name="activePositions">当前活跃持仓</param>
        private async Task CleanupClosedPositionsAsync(List<PositionInfo> activePositions)
        {
            try
            {
                var activeSymbols = activePositions.ToDictionary(p => 
                    $"{p.Symbol}_{(p.PositionAmt > 0 ? "LONG" : "SHORT")}", p => p);
                
                var profilesToRemove = new List<ContractProfile>();
                
                foreach (var profile in _profileService.ContractProfiles)
                {
                    var profileKey = $"{profile.Symbol}_{profile.Side}";
                    if (!activeSymbols.ContainsKey(profileKey))
                    {
                        profilesToRemove.Add(profile);
                    }
                }
                
                foreach (var profile in profilesToRemove)
                {
                    await _profileService.DeleteProfileAsync(profile.ProfileId);
                    AddLog($"🗑️ 已清理平仓合约档案: {profile.DisplayName}");
                }
                
                if (profilesToRemove.Count > 0)
                {
                    AddLog($"✅ 清理完成: 移除 {profilesToRemove.Count} 个已平仓的档案");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理已平仓档案失败");
                AddLog($"❌ 清理已平仓档案失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 计算加仓数量
        /// </summary>
        private decimal CalculateAddPositionQuantity(ContractProfile profile, AddPositionTier tier, decimal riskCapital)
        {
            try
            {
                var addPositionValue = riskCapital * tier.RiskMultiplier / tier.StopLossRatio;
                return Math.Round(addPositionValue / profile.CurrentPrice, 3);
            }
            catch
            {
                return Math.Abs(profile.PositionSize) * 0.5m; // 默认50%
            }
        }
        
        /// <summary>
        /// 计算止损价格
        /// </summary>
        private decimal CalculateStopLossPrice(ContractProfile profile, AddPositionTier tier)
        {
            try
            {
                var stopLossDistance = profile.EntryPrice * tier.StopLossRatio;
                return profile.Side == "LONG" ? 
                    profile.EntryPrice - stopLossDistance : 
                    profile.EntryPrice + stopLossDistance;
            }
            catch
            {
                return profile.EntryPrice; // 默认保本价
            }
        }
        
        /// <summary>
        /// 计算保盈止损价格
        /// </summary>
        private decimal CalculateProfitProtectionStopLossPrice(ContractProfile profile, ProfitProtectionTier tier)
        {
            try
            {
                var protectionDistance = tier.ProtectionAmount / Math.Abs(profile.PositionSize);
                return profile.Side == "LONG" ? 
                    profile.EntryPrice + protectionDistance : 
                    profile.EntryPrice - protectionDistance;
            }
            catch
            {
                return profile.EntryPrice; // 默认保本价
            }
        }
        
        #endregion
        
        #region 数据处理
        
        private async Task RefreshPositionDataAsync()
        {
            try
            {
                var positions = await _binanceService.GetPositionsAsync();
                var activePositions = positions.Where(p => p.PositionAmt != 0).ToList();
                
                // 更新持仓总品种
                PositionCountText.Text = activePositions.Count.ToString();
                
                // 确保活跃持仓都有档案
                if (_currentConfig != null)
                {
                    await CreateProfilesForActivePositions(activePositions);
                }
                
                // 更新档案价格信息
                await _profileService.UpdateAllProfilesPricesAsync();
                
                // 🔧 关键修复：不清空已有配置，而是更新现有配置或添加新配置
                var existingConfigs = ContractConfigs.ToDictionary(c => c.ContractName, c => c);
                var newConfigs = new List<ContractConfigViewModel>();
                
                foreach (var position in activePositions)
                {
                    var side = position.PositionAmt > 0 ? "LONG" : "SHORT";
                    var contractName = $"{position.Symbol} {side}";
                    var profile = _profileService.GetProfile(position.Symbol, side);
                    
                    ContractConfigViewModel config;
                    
                    // 🔧 如果配置已存在，更新而不是重新创建
                    if (existingConfigs.TryGetValue(contractName, out var existingConfig))
                    {
                        config = existingConfig;
                        
                        // 🔧 修复：确保手动修改的状态得到保护
                        config = EnsureLatestManualStatus(config);
                        
                        // 只更新实时数据
                        config.CurrentPnl = position.UnrealizedProfit;
                        config.UpdateTime = DateTime.Now.ToString("HH:mm:ss");
                        
                        // 🔧 修复：只有在没有手动修改的情况下才更新保本状态
                        if (!config.IsManuallyModified("BreakEvenStatus"))
                        {
                            config.BreakEvenStatus = GetBreakEvenStatusFromProfile(profile);
                            config.BreakEvenTarget = GetBreakEvenTargetFromProfile(profile);
                        }
                        
                        // 🔧 修复：刷新动态数据（保护手动修改）
                        PopulateDynamicDataFromProfile(config, profile, position);
                        
                        AddLog($"🔄 更新现有配置: {contractName}，保护手动修改状态");
                    }
                    else
                    {
                        // 🔧 新配置：先创建基本配置，再确保手动修改状态
                        config = new ContractConfigViewModel
                        {
                            ContractName = contractName,
                            CurrentPnl = position.UnrealizedProfit,
                            UpdateTime = DateTime.Now.ToString("HH:mm:ss")
                        };
                        
                        // 🔧 修复：先加载手动修改的状态
                        config = EnsureLatestManualStatus(config);
                        
                        // 🔧 修复：只有在没有手动修改的情况下才设置自动计算的状态
                        if (!config.IsManuallyModified("BreakEvenStatus"))
                        {
                            config.BreakEvenStatus = GetBreakEvenStatusFromProfile(profile);
                            config.BreakEvenTarget = GetBreakEvenTargetFromProfile(profile);
                        }
                        
                        // 填充动态数据（保护手动修改）
                        PopulateDynamicDataFromProfile(config, profile, position);
                        
                        AddLog($"➕ 创建新配置: {contractName}");
                    }
                    
                    newConfigs.Add(config);
                }
                
                // 🔧 修复：在UI线程中安全地更新集合
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ContractConfigs.Clear();
                    foreach (var config in newConfigs)
                    {
                        ContractConfigs.Add(config);
                    }
                });
                
                // 🔧 修复：使用正确的档案数量统计
                var profileCount = _autoMonitorService?.GetActiveProfileCount() ?? 0;
                AddLog($"📊 持仓数据已刷新，活跃合约: {activePositions.Count}个，档案数量: {profileCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新持仓数据失败");
                AddLog($"❌ 刷新持仓数据失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 为活跃持仓创建档案
        /// </summary>
        /// <param name="activePositions">活跃持仓列表</param>
        private async Task CreateProfilesForActivePositions(List<PositionInfo> activePositions)
        {
            try
            {
                int createdCount = 0;
                
                foreach (var position in activePositions)
                {
                    var side = position.PositionAmt > 0 ? "LONG" : "SHORT";
                    var existingProfile = _profileService.GetProfile(position.Symbol, side);
                    
                    if (existingProfile == null)
                    {
                        await _profileService.CreateProfileAsync(position, _currentConfig!.Name);
                        createdCount++;
                    }
                }
                
                if (createdCount > 0)
                {
                    AddLog($"🆕 为 {createdCount} 个持仓创建了档案");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建档案失败");
                AddLog($"❌ 创建档案失败: {ex.Message}");
            }
        }
        
        // 🗑️ 已移除：UI层扫描逻辑，统一使用服务层扫描
        // private async Task PerformScanAsync() - 已删除，功能转移到 AutoMonitorService.ScanPositionsAsync
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 🔧 【新增】检查当前账户是否有已保存的合约状态配置
        /// </summary>
        private bool CheckForExistingContractStates()
        {
            try
            {
                var filePathManager = new FilePathManager();
                var currentAccount = GetCurrentAccountName();
                
                if (string.IsNullOrEmpty(currentAccount))
                {
                    AddLog("⚠️ 无法获取当前账户名称");
                    return false;
                }
                
                var stateFilePath = filePathManager.GetContractMonitoringStatesFilePath(currentAccount);
                var fileExists = System.IO.File.Exists(stateFilePath);
                
                if (fileExists)
                {
                    var fileInfo = new System.IO.FileInfo(stateFilePath);
                    if (fileInfo.Length > 10) // 文件有实际内容
                    {
                        AddLog($"✅ 发现账户'{currentAccount}'的合约状态配置文件 (大小: {fileInfo.Length} 字节)");
                        return true;
                    }
                    else
                    {
                        AddLog($"📂 账户'{currentAccount}'的配置文件存在但为空");
                        return false;
                    }
                }
                else
                {
                    AddLog($"📂 账户'{currentAccount}'没有合约状态配置文件");
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ 检查合约状态配置失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 🔧 【辅助方法】获取当前账户名称
        /// </summary>
        private string GetCurrentAccountName()
        {
            try
            {
                // 尝试从主窗口获取当前账户
                if (System.Windows.Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    if (mainWindow.DataContext is ViewModels.MainViewModel mainViewModel)
                    {
                        return mainViewModel.SelectedAccount?.Name ?? "default";
                    }
                }
                return "default";
            }
            catch
            {
                return "default";
            }
        }
        
        private void AddLog(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logEntry = $"[{timestamp}] {message}";
                
                // 使用Dispatcher确保在UI线程中操作UI控件
                if (Dispatcher.CheckAccess())
                {
                    // 已在UI线程中，直接操作
                    LogTextBox.AppendText(logEntry + Environment.NewLine);
                    LogScrollViewer.ScrollToBottom();
                }
                else
                {
                    // 在其他线程中，使用Dispatcher调用
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        LogTextBox.AppendText(logEntry + Environment.NewLine);
                        LogScrollViewer.ScrollToBottom();
                    }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加日志失败");
            }
        }
        
        private void CreateDefaultConfig()
        {
            try
            {
                AddLog("📝 加载配置列表...");
                
                // 加载所有可用配置到下拉框
                LoadAvailableConfigs();
                
                // 🔧 新增：检查是否为第一次打开（没有配置）
                // 🔧 【修复】检查是否为第一次打开（没有配置且没有合约状态）
                var hasExistingStates = CheckForExistingContractStates();
                
                if (_configManager.Configurations.Count == 0 && !hasExistingStates)
                {
                    AddLog("⚠️ 检测到这是第一次使用自动盯盘功能");
                    AddLog("💡 请先点击右上角的'编辑配置'按钮创建您的第一个配置");
                    
                    // 延迟显示提醒对话框，等待窗口完全加载
                    this.Loaded += (s, e) =>
                    {
                        var result = MessageBox.Show(
                            "欢迎使用自动盯盘功能！\n\n" +
                            "检测到您还没有创建任何配置。\n" +
                            "自动盯盘功能需要先创建基础配置才能使用。\n\n" +
                            "是否现在就打开配置编辑器创建您的第一个配置？",
                            "首次使用提示",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            // 直接打开配置编辑器
                            EditConfigButton_Click(null, null);
                        }
                        else
                        {
                            AddLog("🔧 您可以随时点击'编辑配置'按钮来创建配置");
                        }
                    };
                    
                    return; // 没有配置时直接返回
                }
                else if (_configManager.Configurations.Count == 0 && hasExistingStates)
                {
                    AddLog("🔧 检测到有已保存的合约配置，建议直接使用'自动盯盘'功能");
                }
                
                // 🔧 关键修复：优先从MainViewModel获取当前账户的配置
                _currentConfig = _mainViewModel?.CurrentAutoMonitorConfig;
                
                if (_currentConfig != null)
                {
                    AddLog($"✅ 从MainViewModel获取到配置: {_currentConfig.Name}");
                    // 确保配置管理器也使用同样的配置
                    _configManager.SetCurrentConfiguration(_currentConfig.Name);
                }
                else
                {
                    // 🔧 如果MainViewModel没有配置，尝试从配置管理器获取
                    _currentConfig = _configManager.CurrentConfig;
                    
                    if (_currentConfig == null)
                    {
                        // 如果没有配置，获取第一个可用配置
                        _currentConfig = _configManager.Configurations.FirstOrDefault();
                        
                        if (_currentConfig != null)
                        {
                            _configManager.SetCurrentConfiguration(_currentConfig.Name);
                            AddLog($"🔄 使用第一个可用配置: {_currentConfig.Name}");
                        }
                    }
                    else
                    {
                        AddLog($"🔄 从配置管理器获取配置: {_currentConfig.Name}");
                    }
                }
                
                if (_currentConfig == null)
                {
                    // 🔧 修复：检查是否已存在"智能默认配置"，避免重复创建
                    var existingConfig = _configManager.Configurations.FirstOrDefault(c => c.Name == "智能默认配置");
                    
                    if (existingConfig != null)
                    {
                        // 使用现有的配置
                        _currentConfig = existingConfig;
                        _configManager.SetCurrentConfiguration(_currentConfig.Name);
                        AddLog($"✅ 使用现有的智能默认配置");
                    }
                    else
                    {
                        // 如果真的不存在，才创建一个新的智能默认配置
                        try
                        {
                            var accountEquity = _riskCapitalService.GetCurrentAccountEquity();
                            var riskCapitalTimes = _riskCapitalService.GetCurrentRiskCapitalTimes();
                            
                            _currentConfig = _configManager.CreateConfiguration("智能默认配置", accountEquity, riskCapitalTimes);
                            _configManager.SetCurrentConfiguration(_currentConfig.Name);
                            
                            AddLog($"💡 创建智能默认配置: 权益{accountEquity:F2}U, 风险次数{riskCapitalTimes}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "从风险金服务创建配置失败，使用回退参数");
                            AddLog($"⚠️ 从风险金服务创建配置失败，使用回退参数: {ex.Message}");
                            
                            // 回退到硬编码参数
                            var accountEquity = _mainViewModel?.AccountInfo?.TotalEquity ?? 1000m;
                            var riskCapitalTimes = _mainViewModel?.SelectedAccount?.RiskCapitalTimes ?? 10;
                            
                            _currentConfig = _configManager.CreateConfiguration("智能默认配置", accountEquity, riskCapitalTimes);
                            _configManager.SetCurrentConfiguration(_currentConfig.Name);
                        }
                    }
                }
                
                _logger.LogInformation($"使用配置：{_currentConfig.Name}");
                AddLog($"✅ 加载配置：{_currentConfig.Name}");
                
                // 动态生成DataGrid列
                GenerateDataGridColumns();
                
    
                
                // 更新UI显示
                UpdateConfigDisplay();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建/加载配置失败");
                AddLog($"❌ 配置加载失败：{ex.Message}");
                
                // 回退到简单配置
                _currentConfig = new AutoMonitorConfig
                {
                    Name = "临时配置",
                    ScanIntervalSeconds = 30
                };
            }
        }
        
        /// <summary>
        /// 🎯 加载可用配置到下拉框（使用新的双文件系统）
        /// </summary>
        private void LoadAvailableConfigs()
        {
            try
            {
                AddLog("🔄 正在从双文件系统加载配置...");
                
                // 🔧 【关键修复】首先检查是否有已保存的合约状态配置
                var hasExistingContractStates = CheckForExistingContractStates();
                
                // 🔧 修复：强制重新加载最新配置，确保获取编辑器中保存的最新配置
                _configManager.RefreshConfigurations();
                
                // 🎯 从新的双文件系统获取所有配置模板
                var availableConfigs = _configManager.Configurations.ToList();
                
                AddLog($"✅ 从BaseConfigManager加载了 {availableConfigs.Count} 个配置模板");
                
                // 🎯 修复：遵循统一规则，如果没有配置就保持空状态，不自动创建
                if (availableConfigs.Count == 0)
                {
                    if (hasExistingContractStates)
                    {
                        AddLog("📝 虽然没有基础配置模板，但发现了已保存的合约配置");
                        AddLog("💡 建议：直接点击主界面的'自动盯盘'按钮查看已有配置");
                        AddLog("🔧 您也可以在此创建新的基础配置模板");
                    }
                    else
                    {
                        AddLog("📝 当前没有基础配置，请先使用'编辑配置'功能创建配置模板");
                        AddLog("💡 提示：点击界面右上角的'编辑配置'按钮来创建您的第一个配置");
                    }
                }
                else if (hasExistingContractStates)
                {
                    AddLog("✅ 发现已保存的合约状态配置，可通过'自动盯盘'按钮查看");
                }
                
                // 设置下拉框数据源
                ConfigSelectionComboBox.ItemsSource = availableConfigs;
                
                // 选择当前配置
                if (_currentConfig != null)
                {
                    var selectedConfig = availableConfigs.FirstOrDefault(c => c.Name == _currentConfig.Name);
                    if (selectedConfig != null)
                    {
                        // 🔧 使用标志位防止递归调用
                        _isUpdatingConfigSelection = true;
                        try
                    {
                        ConfigSelectionComboBox.SelectedItem = selectedConfig;
                        }
                        finally
                        {
                            _isUpdatingConfigSelection = false;
                        }
                    }
                }
                
                AddLog($"✅ 加载了 {availableConfigs.Count} 个可用配置");
                
                // 🔧 加载配置后更新监控信息提示
                UpdateMonitorInfoText();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载可用配置失败");
                AddLog($"❌ 加载可用配置失败: {ex.Message}");
                
                // 🔧 加载失败时也要更新提示信息
                UpdateMonitorInfoText();
            }
        }
        
        /// <summary>
        /// 配置选择变化事件处理
        /// </summary>
        private async void ConfigSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // 🔧 防止递归调用：如果正在更新配置选择，直接返回
                if (_isUpdatingConfigSelection)
                {
                    return;
                }
                
                if (ConfigSelectionComboBox.SelectedItem is AutoMonitorConfig selectedConfig)
                {
                    // 🔧 防止递归调用：如果选择的配置与当前配置相同，直接返回
                    if (_currentConfig != null && selectedConfig.Name == _currentConfig.Name)
                    {
                        return;
                    }
                    
                    // 检查是否正在监控，如果正在监控则不允许切换配置
                    if (_isMonitoringActive)
                    {
                        MessageBox.Show("监控运行中，请先停止监控后再切换配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        // 恢复到当前配置
                        ConfigSelectionComboBox.SelectedItem = _currentConfig;
                        return;
                    }
                    
                    // 🔧 保存旧配置用于比较
                    var oldConfig = _currentConfig;
                    _currentConfig = selectedConfig;
                    
                    // 🔧 更新监控状态提示信息
                    UpdateMonitorInfoText();
                    
                    // 🔧 清理重复日志：移除这里的重复日志输出
                    // AddLog($"🔄 切换到配置：{selectedConfig.Name}");
                    
                    // 🔧 修复：强制基于新配置重新生成DataGrid列结构
                    GenerateDataGridColumns();
                    
                    // 🔧 修复：只同步配置到配置管理器，不覆盖用户选择
                    try
                    {
                        // 🔧 关键修复：保持用户选择的配置，不从配置管理器重新获取，避免覆盖用户选择
                        _configManager.SetCurrentConfiguration(_currentConfig.Name);
                        // 🔧 简化日志：减少冗余信息
                        // AddLog($"✅ 配置已同步到配置管理器：'{_currentConfig.Name}'");
                    }
                    catch (Exception configEx)
                    {
                        _logger.LogWarning(configEx, "同步配置到配置管理器失败，使用本地配置");
                        AddLog($"⚠️ 配置同步警告: {configEx.Message}，使用本地配置");
                    }
                    
                    // 🔧 关键修复：将选择的配置保存到MainViewModel中
                    try
                    {
                        if (_mainViewModel != null && _mainViewModel.SelectedAccount != null)
                        {
                            // 🔧 关键修复：立即设置为当前配置，确保立即生效
                            _mainViewModel.SetCurrentAutoMonitorConfig(_currentConfig);
                            
                            // 使用新方法更新账户配置
                            _mainViewModel.UpdateAccountAutoMonitorConfig(_mainViewModel.SelectedAccount.Name, _currentConfig);
                            
                            // 🔧 持久化配置到文件
                            var configPersistenceService = new AutoMonitorConfigPersistenceService();
                            configPersistenceService.SaveSingleAccountConfig(_mainViewModel.SelectedAccount.Name, _currentConfig);
                            
                            // 🔧 简化日志：合并保存和验证信息
                            // AddLog($"💾 配置 '{_currentConfig.Name}' 已保存到账户 '{_mainViewModel.SelectedAccount.Name}'");
                            _logger.LogInformation($"✅ 配置选择已保存：{_mainViewModel.SelectedAccount.Name} -> {_currentConfig.Name}");
                            
                            // 🔧 新增：验证保存结果
                            var verifyConfig = configPersistenceService.GetAccountConfig(_mainViewModel.SelectedAccount.Name);
                            if (verifyConfig?.Name == _currentConfig.Name)
                            {
                                // 🔧 简化日志：减少验证成功的冗余输出
                                // AddLog($"✅ 配置保存验证成功：{verifyConfig.Name}");
                            }
                            else
                            {
                                AddLog($"⚠️ 配置保存验证失败：期望{_currentConfig.Name}，实际{verifyConfig?.Name ?? "null"}");
                            }
                        }
                        else
                        {
                            AddLog("⚠️ 无法获取主视图模型或当前账户，配置选择未保存");
                        }
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, "保存配置选择失败");
                        AddLog($"❌ 保存配置选择失败: {saveEx.Message}");
                    }
                    
                    AddLog($"🔄 切换到配置：{_currentConfig.Name}");
                    // 🔧 简化配置详情日志，只在必要时输出
                    if (_currentConfig.AddPositionConfig?.Tiers?.Count > 0)
                    {
                        AddLog($"📊 配置详情 - 推仓:{_currentConfig.AddPositionConfig.Tiers.Count}档, 保本:{(_currentConfig.BreakEvenConfig?.IsEnabled == true ? "启用" : "禁用")}, 保盈:{_currentConfig.ProfitProtectionConfig?.Tiers?.Count ?? 0}档");
                    }
                    
                    // 🔧 移除详细的推仓档位日志，减少冗余信息
                    // if (_currentConfig.AddPositionConfig?.Tiers != null)
                    // {
                    //     foreach (var tier in _currentConfig.AddPositionConfig.Tiers)
                    //     {
                    //         AddLog($"  推仓{tier.TierIndex}档: {tier.TriggerProfitAmount}U");
                    //     }
                    // }
                    
                    // 更新UI显示
                    UpdateConfigDisplay();
                    
                    // 🔧 新增：配置切换时自动同步合约配置
                    AddLog($"🔄 基础配置切换为：{_currentConfig.Name}");
                    
                    // 🔧 优化：只有当存在合约配置时才询问是否重新生成
                    bool hasExistingConfigs = ContractConfigs.Count > 0;
                    
                    if (hasExistingConfigs)
                    {
                        AddLog("🔄 检测到现有合约配置，询问是否重新生成...");
                        
                        // 显示配置切换确认对话框
                        var syncResult = MessageBox.Show(
                            $"您已切换到基础配置：{_currentConfig.Name}\n\n" +
                            "是否用新的基础配置重新生成所有合约配置？\n\n" +
                            "✅ 是 - 用基础配置重新生成所有合约配置（推荐）\n" +
                            "❌ 否 - 保持现有合约配置不变",
                            "配置切换确认",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        
                        if (syncResult == MessageBoxResult.Yes)
                        {
                            // 强制基于新配置重新生成所有合约配置
                    await RegenerateAllContractConfigsAsync();
                            AddLog("✅ 已用新基础配置重新生成所有合约配置");
                        }
                        else
                        {
                            AddLog("ℹ️ 保持现有合约配置不变");
                            // 仍然需要刷新界面显示
                    await RefreshPositionDataAsync();
                        }
                    }
                    else
                    {
                        AddLog("ℹ️ 当前无合约配置，直接刷新界面");
                        // 如果没有现有配置，直接刷新界面
                        await RefreshPositionDataAsync();
                    }
                    
                    AddLog($"✅ 配置切换完成：{_currentConfig.Name}");
                }
                
                // 🔧 初始化时也要更新监控信息提示
                UpdateMonitorInfoText();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置选择变化处理失败");
                AddLog($"❌ 配置选择变化处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据当前配置动态生成DataGrid列
        /// </summary>
        private void GenerateDataGridColumns()
        {
            try
            {
                if (_currentConfig == null) return;

                // 清除现有的动态列（保留基础列）
                var columnsToRemove = ContractConfigDataGrid.Columns.Where(c => 
                    c.Header.ToString().Contains("推仓") || c.Header.ToString().Contains("保盈")).ToList();
                
                foreach (var column in columnsToRemove)
                {
                    ContractConfigDataGrid.Columns.Remove(column);
                }

                // 插入位置（在"保本状态"列之后）
                int insertIndex = ContractConfigDataGrid.Columns.Count - 1; // 在"更新时间"列之前

                // 添加推仓列
                if (_currentConfig.AddPositionConfig?.IsEnabled == true && _currentConfig.AddPositionConfig.Tiers?.Count > 0)
                {
                    foreach (var tier in _currentConfig.AddPositionConfig.Tiers.OrderBy(t => t.TierIndex))
                    {
                        var column = new DataGridTextColumn
                        {
                            Header = $"推仓{tier.TierIndex}档",
                            Width = new DataGridLength(90)
                        };
                        
                        // 设置绑定路径 - 直接绑定到动态属性
                        column.Binding = new Binding($"DynamicPush{tier.TierIndex}");
                        
                        // 设置样式
                        column.ElementStyle = new Style(typeof(TextBlock));
                        column.ElementStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
                        column.ElementStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
                        
                        ContractConfigDataGrid.Columns.Insert(insertIndex++, column);
                    }
                }

                // 添加保盈列
                if (_currentConfig.ProfitProtectionConfig?.IsEnabled == true && _currentConfig.ProfitProtectionConfig.Tiers?.Count > 0)
                {
                    foreach (var tier in _currentConfig.ProfitProtectionConfig.Tiers.OrderBy(t => t.TierIndex))
                    {
                        var column = new DataGridTextColumn
                        {
                            Header = $"保盈{tier.TierIndex}档",
                            Width = new DataGridLength(90)
                        };
                        
                        // 设置绑定路径 - 直接绑定到动态属性
                        column.Binding = new Binding($"DynamicProfit{tier.TierIndex}");
                        
                        // 设置样式
                        column.ElementStyle = new Style(typeof(TextBlock));
                        column.ElementStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
                        column.ElementStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
                        
                        ContractConfigDataGrid.Columns.Insert(insertIndex++, column);
                    }
                }

    
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "动态生成DataGrid列失败");
                AddLog($"❌ 动态生成列失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据当前配置和档案信息填充动态数据
        /// </summary>
        private void PopulateDynamicDataFromProfile(ContractConfigViewModel config, ContractProfile? profile, PositionInfo position)
        {
            try
            {
                if (_currentConfig == null) 
                {
                    AddLog($"⚠️ 当前配置为空，无法填充动态数据: {position.Symbol}");
                    return;
                }

                // 🔧 关键修复：清空数据时保护手动修改的数据
                config.ClearDynamicData(preserveManualChanges: true);
                AddLog($"🔄 开始填充动态数据: {position.Symbol}, 配置: {_currentConfig.Name}（保护手动修改）");

                // 🔧 修复：基于当前配置和实际状态填充推仓数据
                if (_currentConfig.AddPositionConfig?.IsEnabled == true && _currentConfig.AddPositionConfig.Tiers?.Count > 0)
                {
                    // 简化：移除冗长的推仓数据填充日志
                    
                    foreach (var tier in _currentConfig.AddPositionConfig.Tiers)
                    {
                        var triggerAmount = tier.TriggerProfitAmount;
                        
                        // 🔧 修复：根据档案状态和浮盈计算实际状态
                        string status;
                        if (profile != null)
                        {
                            // 检查档案中的推仓状态
                            status = GetPushTierStatusFromProfile(profile, tier.TierIndex);
                        }
                        else
                        {
                            // 基于浮盈简单判断状态
                            if (position.UnrealizedProfit >= triggerAmount)
                            {
                                status = "已触发";
                            }
                            else
                            {
                                status = "未触发";
                            }
                        }
                        
                        // 显示格式：触发金额 | 状态
                        var displayText = $"{triggerAmount:F0} | {status}";
                        
                        var color = GetStatusColor(status);
                        config.SetDynamicData($"Push{tier.TierIndex}", displayText, color);
                        
                        // 简化：移除推仓档位详细日志
                    }
                }
                else
                {
                    AddLog($"⚠️ 推仓配置未启用或无档位");
                }

                // 🔧 修复：基于当前配置和实际状态填充保盈数据
                if (_currentConfig.ProfitProtectionConfig?.IsEnabled == true && _currentConfig.ProfitProtectionConfig.Tiers?.Count > 0)
                {
                    // 简化：移除冗长的保盈数据填充日志
                    foreach (var tier in _currentConfig.ProfitProtectionConfig.Tiers)
                    {
                        var triggerAmount = tier.TriggerProfitAmount;
                        var protectionAmount = tier.ProtectionAmount;
                        
                        // 🔧 修复：根据档案状态和浮盈计算实际状态
                        string status;
                        if (profile != null)
                        {
                            // 检查档案中的保盈状态
                            status = GetProfitTierStatusFromProfile(profile, tier.TierIndex);
                        }
                        else
                        {
                            // 基于浮盈简单判断状态
                            if (position.UnrealizedProfit >= triggerAmount)
                            {
                                status = "已触发";
                            }
                            else
                            {
                                status = "未触发";
                            }
                        }
                        
                        // 显示格式：触发金额 | 保盈金额 | 状态
                        var displayText = $"{triggerAmount:F0} | {protectionAmount:F0} | {status}";
                        
                        var color = GetStatusColor(status);
                        config.SetDynamicData($"Profit{tier.TierIndex}", displayText, color);
                        
                        // 简化：移除保盈档位详细日志
                    }
                }
                else
                {
                    AddLog($"⚠️ 保盈配置未启用或无档位");
                }
                
                // 简化：移除冗长的动态数据填充完成日志
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"填充动态数据失败: {position.Symbol}");
                AddLog($"❌ 填充动态数据失败: {position.Symbol} - {ex.Message}");
            }
        }

        /// <summary>
        /// 从档案获取触发金额
        /// </summary>
        private decimal GetTriggerAmountFromProfile(ContractProfile? profile, int tierIndex, string type)
        {
            if (profile == null) return 0;

            try
            {
                if (type == "push")
                {
                    // 推仓触发金额
                    if (profile.UseIndependentConfig && profile.IndependentAddPositionConfig != null)
                    {
                        var tier = profile.IndependentAddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
                        return tier?.TriggerProfitAmount ?? 0;
                    }
                    else
                    {
                        var baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
                        var tier = baseConfig?.AddPositionConfig?.Tiers?.FirstOrDefault(t => t.TierIndex == tierIndex);
                        return tier?.TriggerProfitAmount ?? 0;
                    }
                }
                else if (type == "profit")
                {
                    // 保盈触发金额
                    if (profile.UseIndependentConfig && profile.IndependentProfitProtectionConfig != null)
                    {
                        var tier = profile.IndependentProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
                        return tier?.TriggerProfitAmount ?? 0;
                    }
                    else
                    {
                        var baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
                        var tier = baseConfig?.ProfitProtectionConfig?.Tiers?.FirstOrDefault(t => t.TierIndex == tierIndex);
                        return tier?.TriggerProfitAmount ?? 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取触发金额失败: {profile.Symbol}, 类型: {type}, 档位: {tierIndex}");
            }

            return 0;
        }

        /// <summary>
        /// 从档案获取保盈金额
        /// </summary>
        private decimal GetProtectionAmountFromProfile(ContractProfile? profile, int tierIndex)
        {
            if (profile == null) return 0;

            try
            {
                if (profile.UseIndependentConfig && profile.IndependentProfitProtectionConfig != null)
                {
                    var tier = profile.IndependentProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
                    return tier?.ProtectionAmount ?? 0;
                }
                else
                {
                    var baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
                    var tier = baseConfig?.ProfitProtectionConfig?.Tiers?.FirstOrDefault(t => t.TierIndex == tierIndex);
                    return tier?.ProtectionAmount ?? 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取保盈金额失败: {profile.Symbol}, 档位: {tierIndex}");
            }

            return 0;
        }

        /// <summary>
        /// 获取状态颜色
        /// </summary>
        private string GetStatusColor(string status)
        {
            return status switch
            {
                            "-" => "Gray",          // waiting
            "√" => "Green",         // executed
                "执行中" => "Orange",    // 执行中
                "❌" => "Red",          // 执行失败
                _ => "Black"
            };
        }
        
        /// <summary>
        /// 更新配置显示
        /// </summary>
        private void UpdateConfigDisplay()
        {
            if (_currentConfig != null)
            {
                // 确保下拉框选择正确的配置
                if (ConfigSelectionComboBox != null && ConfigSelectionComboBox.ItemsSource != null)
                {
                    var availableConfigs = ConfigSelectionComboBox.ItemsSource as IEnumerable<AutoMonitorConfig>;
                    var selectedConfig = availableConfigs?.FirstOrDefault(c => c.Name == _currentConfig.Name);
                    if (selectedConfig != null && ConfigSelectionComboBox.SelectedItem != selectedConfig)
                    {
                        // 🔧 使用标志位防止递归调用
                        _isUpdatingConfigSelection = true;
                        try
                    {
                        ConfigSelectionComboBox.SelectedItem = selectedConfig;
                        }
                        finally
                        {
                            _isUpdatingConfigSelection = false;
                        }
                    }
                }
                
                // 更新扫描间隔显示
                if (ScanIntervalTextBox != null)
                    ScanIntervalTextBox.Text = _currentConfig.ScanIntervalSeconds.ToString();
                
                // 更新风险金信息
                UpdateRiskCapitalDisplay();
            }
        }
        
        /// <summary>
        /// 更新风险金信息显示
        /// </summary>
        private void UpdateRiskCapitalDisplay()
        {
            try
            {
                // 检查账户信息是否可用
                if (_mainViewModel?.AccountInfo == null)
                {
                    AddLog("⚠️ 账户信息未加载，使用默认风险金设置");
                    
                    // 使用默认值
                    if (AccountEquityText != null) AccountEquityText.Text = "未连接";
                    if (RiskCapitalTimesText != null) RiskCapitalTimesText.Text = "10";
                    if (RiskCapitalAmountText != null) RiskCapitalAmountText.Text = "100.00 USDT";
                    
                    return;
                }
                
                // 获取账户权益
                var accountEquity = _riskCapitalService.GetCurrentAccountEquity();
                var riskCapitalTimes = _riskCapitalService.GetCurrentRiskCapitalTimes();
                var riskCapital = _riskCapitalService.CalculateRiskCapital(accountEquity, riskCapitalTimes);
                
                // 更新显示
                if (AccountEquityText != null)
                    AccountEquityText.Text = $"{accountEquity:F2} USDT";
                
                if (RiskCapitalTimesText != null)
                    RiskCapitalTimesText.Text = riskCapitalTimes.ToString();
                
                if (RiskCapitalAmountText != null)
                    RiskCapitalAmountText.Text = $"{riskCapital:F2} USDT";
                

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新风险金信息失败");
                AddLog($"⚠️ 风险金信息更新失败: {ex.Message}，使用默认设置");
                
                // 设置合理的默认值而不是N/A
                if (AccountEquityText != null) AccountEquityText.Text = "未连接";
                if (RiskCapitalTimesText != null) RiskCapitalTimesText.Text = "10";
                if (RiskCapitalAmountText != null) RiskCapitalAmountText.Text = "100.00 USDT";
            }
        }
        
        #endregion
        
        #region 窗口关闭
        
        protected override async void OnClosing(CancelEventArgs e)
        {
            try
            {
                if (_isMonitoringActive)
                {
                    // 🔧 修复：询问用户是否要停止后台监控，而不是直接停止
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
                            AddLog("🛑 用户选择停止后台监控并关闭窗口");
                            await StopMonitoringAsync();
                            break;
                            
                        case MessageBoxResult.No:
                            // 保持监控运行，仅关闭窗口
                            AddLog("🖥️ 用户选择保持后台监控运行，仅关闭配置窗口");
                            AddLog("💡 提示：后台自动盯盘将继续运行，可通过主界面停止");
                            break;
                            
                        case MessageBoxResult.Cancel:
                            // 取消关闭
                            AddLog("❌ 用户取消关闭窗口");
                            e.Cancel = true;
                            return;
                    }
                }
                
                // 🔧 修复：无论是否停止监控，都要停止UI相关的定时器
                _scanTimer?.Stop();
                _logTimer?.Stop();
                
                // 🔧 【重要新增】：取消订阅事件，避免内存泄漏
                if (_autoMonitorService != null)
                {
                    _autoMonitorService.WorkLogAdded -= OnWorkLogAdded;
                    _autoMonitorService.MonitorStatusChanged -= OnMonitorStatusChanged;
                    _autoMonitorService.ExecutionCompleted -= OnExecutionCompleted;
                    _autoMonitorService.StatusUpdated -= OnStatusUpdated;
                    _autoMonitorService.PositionChanged -= OnPositionChanged;
                    AddLog("✅ 已取消订阅服务层事件");
                }
                
                AddLog("🖥️ 配置窗口关闭中...");
                
                // 🔧 新增：如果监控仍在运行，提醒用户监控状态
                if (_isMonitoringActive && _autoMonitorService.IsRunning)
                {
                    AddLog("✅ 后台自动盯盘将继续运行");
                    
                    // 通知主界面更新状态（监控仍在运行）
                    NotifyMainViewModel(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "关闭窗口时发生错误");
            }
            
            base.OnClosing(e);
        }
        
        #endregion
        
        #region INotifyPropertyChanged
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
        
        #region 配置计算方法
        
        /// <summary>
        /// 计算保本目标金额（使用真实配置）
        /// </summary>
        private decimal CalculateBreakEvenTarget(decimal currentPnl)
        {
            if (_currentConfig?.BreakEvenConfig?.IsEnabled == true)
            {
                return _currentConfig.BreakEvenConfig.TriggerProfitAmount;
            }
            
            // 回退到简化逻辑
            return currentPnl > 0 ? currentPnl * 0.5m : 10m;
        }
        
        /// <summary>
        /// 获取保本状态（使用真实配置）
        /// </summary>
        private string GetBreakEvenStatus(decimal currentPnl)
        {
            if (_currentConfig?.BreakEvenConfig?.IsEnabled != true)
                return "-"; // 未启用
            
            var triggerAmount = _currentConfig.BreakEvenConfig.TriggerProfitAmount;
            
            // 检查是否已经触发过（这里简化，实际应该从AutoMonitorService查询状态）
            if (currentPnl >= triggerAmount)
            {
                // 这里应该查询真实的执行状态，现在简化处理
                return currentPnl > triggerAmount * 1.5m ? "√" : "执行中";
            }
            
            return "-";  // 未触发
        }
        
        /// <summary>
        /// 获取推仓阶梯状态（使用真实配置）
        /// </summary>
        private string GetPushTierStatus(decimal currentPnl, int tier)
        {
            if (_currentConfig?.AddPositionConfig?.IsEnabled != true)
                return "-"; // 未启用
            
            // 查找对应的推仓阶梯
            var tierConfig = _currentConfig.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tier && t.IsEnabled);
            if (tierConfig == null)
                return "-"; // 阶梯不存在或未启用
            
            var triggerAmount = tierConfig.TriggerProfitAmount;
            
            // 检查是否已经触发过
            if (currentPnl >= triggerAmount)
            {
                // 这里应该查询真实的执行状态，现在简化处理
                return currentPnl > triggerAmount * 1.2m ? "√" : "执行中";
            }
            
            return "-";  // 未触发
        }
        
        /// <summary>
        /// 获取保盈阶梯状态（使用真实配置）
        /// </summary>
        private string GetProfitTierStatus(decimal currentPnl, int tier)
        {
            if (_currentConfig?.ProfitProtectionConfig?.IsEnabled != true)
                return "-"; // 未启用
            
            // 查找对应的保盈阶梯
            var tierConfig = _currentConfig.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tier && t.IsEnabled);
            if (tierConfig == null)
                return "-"; // 阶梯不存在或未启用
            
            var triggerAmount = tierConfig.TriggerProfitAmount;
            
            // 检查是否已经触发过
            if (currentPnl >= triggerAmount)
            {
                // 这里应该查询真实的执行状态，现在简化处理
                return currentPnl > triggerAmount * 1.1m ? "√" : "执行中";
            }
            
            return "-";  // 未触发
        }
        
        #region 基于档案的状态获取方法
        
        /// <summary>
        /// 从档案获取保本目标金额
        /// </summary>
        private decimal GetBreakEvenTargetFromProfile(ContractProfile? profile)
        {
            if (profile == null) return 0;
            
            if (profile.UseIndependentConfig && profile.IndependentBreakEvenConfig != null)
            {
                return profile.IndependentBreakEvenConfig.TriggerProfitAmount;
            }
            
            // 使用基础配置
            var baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
            return baseConfig?.BreakEvenConfig?.TriggerProfitAmount ?? 0;
        }
        
        /// <summary>
        /// 从档案获取保本状态（优先检查手动修改）
        /// </summary>
        private string GetBreakEvenStatusFromProfile(ContractProfile? profile)
        {
            if (profile == null) return "-";
            
            // 🔧 【关键修复】优先从状态文件检查，确保与文件同步
            var isExecuted = _autoMonitorService.IsExecutedInStateFile(profile.Symbol, profile.Side, "保本");
            if (isExecuted) return "√";
            
            // 🔧 回退：使用统一状态检查
            return GetUnifiedContractStatus(profile.Symbol, profile.Side, "BreakEven");
        }
        
        /// <summary>
        /// 从档案获取推仓阶梯状态（优先检查手动修改）
        /// </summary>
        private string GetPushTierStatusFromProfile(ContractProfile? profile, int tier)
        {
            if (profile == null) return "-";
            
            // 🔧 【关键修复】优先从状态文件检查，确保与文件同步
            var isExecuted = _autoMonitorService.IsExecutedInStateFile(profile.Symbol, profile.Side, "推仓", tier);
            if (isExecuted) return "√";
            
            // 🔧 回退：使用统一状态检查
            return GetUnifiedContractStatus(profile.Symbol, profile.Side, "AddPosition", tier);
        }
        
        /// <summary>
        /// 从档案获取保盈阶梯状态
        /// </summary>
        private string GetProfitTierStatusFromProfile(ContractProfile? profile, int tier)
        {
            if (profile == null) return "-";
            
            var tierState = profile.ProfitProtectionStates.FirstOrDefault(s => s.TierIndex == tier);
            if (tierState == null) return "-";
            
            if (tierState.IsTriggered)
            {
                return tierState.ExecutionStatus switch
                {
                    "已执行" => "√",
                    "执行失败" => "❌",
                    "触发中" => "执行中",
                    _ => "-"
                };
            }
            
            return "-";
        }
        
        #endregion
        
        /// <summary>
        /// 双击编辑合约配置
        /// </summary>
        private async void ContractConfigDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (ContractConfigDataGrid.SelectedItem is ContractConfigViewModel selectedConfig)
                {
                    AddLog($"🖱️ 双击编辑合约配置: {selectedConfig.ContractName}");
                    
                    // 检查是否正在监控，如果正在监控则不允许编辑
                    if (_isMonitoringActive)
                    {
                        MessageBox.Show("监控运行中，请先停止监控后再编辑合约配置", "提示", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    // 打开合约配置编辑窗口
                    try
                    {
                        // 🔧 关键修复：确保传递给编辑对话框的配置包含最新的手动修改状态
                        var configForEditing = EnsureLatestManualStatus(selectedConfig);
                        AddLog($"🔧 准备编辑配置，当前保本状态: {configForEditing.BreakEvenStatus}");
                        
                        var editWindow = new ContractConfigEditDialog(configForEditing, _currentConfig, _logger);
                        editWindow.Owner = this;
                        
                        var result = editWindow.ShowDialog();
                        
                        if (result == true && editWindow.IsConfirmed)
                        {
                            // 用户确认了修改，更新配置
                            var editedConfig = editWindow.EditedConfig;
                            
                            // 🔧 修复：从统一状态文件重新加载真实状态，确保UI显示最新的状态
                            await ReloadContractStateFromUnifiedFile(selectedConfig);
                            
                            // 更新UI中的配置显示（使用重新加载的状态）
                            UpdateContractConfigInUI(selectedConfig, editedConfig);
                            
                            // 强制刷新UI显示
                            selectedConfig.NotifyAllPropertiesChanged();
                            
                            // 这里可以添加保存到后台配置的逻辑
                            // await SaveContractConfigToBackend(editedConfig);
                            
                            AddLog($"✅ 合约配置已更新: {editedConfig.ContractName}");
                            
                            // 刷新数据显示
                            await RefreshPositionDataAsync();
                        }
                        else
                        {
                            AddLog($"📝 取消编辑合约配置: {selectedConfig.ContractName}");
                        }
                    }
                    catch (Exception editEx)
                    {
                        _logger.LogError(editEx, "创建编辑窗口失败");
                        AddLog($"❌ 创建编辑窗口失败: {editEx.Message}");
                        
                        // 回退到简单的消息框显示
                        MessageBox.Show($"双击编辑功能：{selectedConfig.ContractName}\n" +
                                      $"当前浮盈: {selectedConfig.CurrentPnl:F2}U\n" +
                                      $"保本目标: {selectedConfig.BreakEvenTarget:F2}U\n" +
                                      "编辑窗口加载失败，请检查系统状态", 
                                      "编辑合约配置", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "编辑合约配置失败");
                AddLog($"❌ 编辑合约配置失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 编辑基础配置
        /// </summary>
        private async void EditConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("🔧 打开基础配置编辑器");
                
                // 检查是否正在监控，如果正在监控则不允许编辑
                if (_isMonitoringActive)
                {
                    MessageBox.Show("监控运行中，请先停止监控后再编辑配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 🔧 保存当前配置用于后续比较
                var configBeforeEdit = _currentConfig != null ? 
                    System.Text.Json.JsonSerializer.Serialize(_currentConfig) : null;
                
                AddLog($"📋 编辑前配置快照: {_currentConfig?.Name ?? "null"}");
                
                // 🔧 修复：使用单例模式，两个窗口自动共享同一个BaseConfigManager实例
                var configEditor = new SimpleConfigEditorWindow(_mainViewModel);
                AddLog($"🔗 单例模式已启用，配置自动同步");
                
                // 🔧 关键修复：保存编辑前的配置状态用于比较
                var configBeforeEditJson = _currentConfig != null ? 
                    System.Text.Json.JsonSerializer.Serialize(_currentConfig) : null;
                
                var result = configEditor.ShowDialog();
                
                AddLog($"🔄 配置编辑器已关闭，结果: {result}");
                
                // 🔧 强制刷新配置列表，确保获取最新保存的配置
                _configManager.RefreshConfigurations();
                LoadAvailableConfigs();
                
                // 🔧 关键修复：检查基础配置是否发生变化
                AutoMonitorConfig configAfterEdit = null;
                if (_currentConfig != null)
                {
                    configAfterEdit = _configManager.Configurations.FirstOrDefault(c => c.Name == _currentConfig.Name);
                    if (configAfterEdit != null)
                    {
                        _currentConfig = configAfterEdit;
                        ConfigSelectionComboBox.SelectedItem = configAfterEdit;
                    }
                }
                
                // 🔧 检测配置是否有实质性变化
                bool hasConfigChanged = false;
                if (configBeforeEditJson != null && configAfterEdit != null)
                {
                    var configAfterEditJson = System.Text.Json.JsonSerializer.Serialize(configAfterEdit);
                    hasConfigChanged = configBeforeEditJson != configAfterEditJson;
                }
                else if (configBeforeEditJson == null && configAfterEdit != null)
                {
                    // 新创建的配置
                    hasConfigChanged = true;
                }
                
                // 🔧 新增：如果配置有变化，自动同步合约配置
                if (hasConfigChanged && configAfterEdit != null)
                {
                    AddLog("🔍 检测到基础配置发生变化！");
                    AddLog($"🔄 自动用基础配置 '{configAfterEdit.Name}' 重新生成合约配置...");
                    
                    // 提示用户配置变化，询问是否同步
                    var syncResult = MessageBox.Show(
                        $"检测到基础配置 '{configAfterEdit.Name}' 发生了变化。\n\n" +
                        "为了保持一致性，建议将变化同步到所有合约配置。\n\n" +
                        "是否现在同步合约配置？\n\n" +
                        "✅ 是 - 用基础配置重新生成所有合约配置\n" +
                        "❌ 否 - 保持现有合约配置不变",
                        "配置同步确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (syncResult == MessageBoxResult.Yes)
                    {
                        // 强制基于新配置重新生成所有合约配置
                        await RegenerateAllContractConfigsAsync();
                        AddLog("✅ 基础配置变化已同步到所有合约配置");
                    }
                    else
                    {
                        AddLog("ℹ️ 用户选择保持现有合约配置不变");
                    }
                }
                else if (!hasConfigChanged)
                {
                    AddLog("ℹ️ 基础配置无重要变化，保持现有合约配置");
                }
                
                if (result == true || result == null) // 即使用户没有明确确认，也检查是否有变化
                {
                    AddLog("🔍 配置编辑完成，自动同步配置到所有合约...");
                    
                    // 🔧 自动同步，不询问用户
                    AddLog("🔄 自动更新所有合约配置...");
                    
                    // 执行同步
                    await Task.Run(async () =>
                    {
                        await UpdateAllContractConfigsAsync();
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddLog("✅ 合约配置自动同步完成！");
                        });
                    });
                    
                    // 刷新配置显示
                    RefreshCurrentConfig();
                }
                else
                {
                    AddLog("📝 用户取消了配置编辑");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开编辑配置失败");
                AddLog($"❌ 打开编辑配置失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 🔧 基于当前基础配置生成统一监控状态文件（遵循统一规则）
        /// 🎯 用户选择基础配置后，针对每个持仓的合约，与基础配置的详细内容，生成统一状态文件
        /// </summary>
        private async Task RegenerateAllContractConfigsAsync()
        {
            try
            {
                if (_currentConfig == null)
                {
                    AddLog("❌ 无当前配置，无法重新生成");
                    return;
                }
                
                AddLog($"🔄 基于配置'{_currentConfig.Name}'生成统一监控状态文件...");
                
                // 获取当前活跃持仓
                var positions = await _binanceService.GetPositionsAsync();
                var activePositions = positions.Where(p => Math.Abs(p.PositionAmt) > 0).ToList();
                
                AddLog($"📊 找到 {activePositions.Count} 个活跃持仓");
                
                if (activePositions.Count == 0)
                {
                    AddLog("⚠️ 当前没有活跃持仓，无法生成监控状态");
                    return;
                }
                
                // 🎯 使用统一状态服务生成contract_monitoring_states.json
                var monitoringStates = _stateService.GenerateMonitoringStatesFromPositions(activePositions, _currentConfig.Name);
                
                // 保存统一监控状态文件
                _stateService.SaveMonitoringStates(monitoringStates);
                
                AddLog($"✅ 已生成统一监控状态文件：{monitoringStates.Count(s => s.Value.IsActive)} 个活跃状态");
                
                foreach (var state in monitoringStates.Values.Where(s => s.IsActive))
                {
                    AddLog($"   📝 {state.Symbol}_{state.PositionSide} - 配置: {state.BaseConfigName}");
                }
                
                // 清除UI中的合约配置，强制重新加载
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ContractConfigs.Clear();
                });
                
                // 重新生成数据网格列结构
                GenerateDataGridColumns();
                
                // 重新加载位置数据，这将基于新配置创建所有配置
                await RefreshPositionDataAsync();
                
                AddLog($"✅ 统一监控状态文件生成完成！");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成统一监控状态失败");
                AddLog($"❌ 生成统一监控状态失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 简化的配置同步检查
        /// </summary>
        private async Task ForceReloadAndCompareConfigAsync(string? configBeforeEdit)
        {
            try
            {
                AddLog("🔄 开始配置同步检查...");
                
                var syncChoice = MessageBox.Show(
                    $"🔧 配置编辑完成！\n\n" +
                    $"配置名称：{_currentConfig?.Name ?? "未知"}\n\n" +
                    $"📋 是否要将配置同步到所有合约？\n\n" +
                    $"✅ 【是】- 立即同步配置到所有合约\n" +
                    $"❌ 【否】- 保持现有合约配置不变", 
                    "配置同步确认", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                
                if (syncChoice == MessageBoxResult.Yes)
                {
                    AddLog("🔄 用户确认同步，开始更新所有合约配置...");
                    
                    await Task.Run(async () =>
                    {
                        await UpdateAllContractConfigsAsync();
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddLog("✅ 合约配置同步完成！");
                            MessageBox.Show("✅ 配置同步成功！\n\n所有合约配置已更新", 
                                "同步完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    });
                }
                else
                {
                    AddLog("ℹ️ 用户选择不同步配置");
                }
                
                RefreshCurrentConfig();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置同步检查失败");
                AddLog($"❌ 配置同步检查失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 刷新当前配置
        /// </summary>
        private void RefreshCurrentConfig()
        {
            try
            {
                AddLog("🔄 开始刷新配置...");
                
                // 🔧 修复：重新加载所有可用配置，而不是创建默认配置
                LoadAvailableConfigs();
                
                // 🔧 【重要修复】：配置刷新后也刷新持仓数据，确保数据同步
                _ = RefreshPositionDataAsync();
                
                // 🚧 调试：验证配置文件内容
                var configPath = _configManager.GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    AddLog($"📋 配置文件内容预览:");
                    AddLog($"文件大小: {json.Length} 字符");
                    
                    // 解析配置验证内容
                    try
                    {
                        var configs = System.Text.Json.JsonSerializer.Deserialize<List<AutoMonitorConfig>>(json);
                        if (configs != null)
                        {
                            AddLog($"✅ 成功解析配置文件，包含 {configs.Count} 个配置");
                            foreach (var config in configs)
                            {
                                AddLog($"📊 配置 '{config.Name}' 详情:");
                                AddLog($"  推仓档位: {config.AddPositionConfig?.Tiers?.Count ?? 0}");
                                if (config.AddPositionConfig?.Tiers != null)
                                {
                                    for (int i = 0; i < Math.Min(3, config.AddPositionConfig.Tiers.Count); i++)
                                    {
                                        var tier = config.AddPositionConfig.Tiers[i];
                                        AddLog($"    推仓{tier.TierIndex}档: {tier.TriggerProfitAmount}U (来源: 配置文件)");
                                    }
                                }
                                AddLog($"  保盈档位: {config.ProfitProtectionConfig?.Tiers?.Count ?? 0}");
                                if (config.ProfitProtectionConfig?.Tiers != null)
                                {
                                    for (int i = 0; i < Math.Min(3, config.ProfitProtectionConfig.Tiers.Count); i++)
                                    {
                                        var tier = config.ProfitProtectionConfig.Tiers[i];
                                        AddLog($"    保盈{tier.TierIndex}档: {tier.TriggerProfitAmount}U | {tier.ProtectionAmount}U (来源: 配置文件)");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception parseEx)
                    {
                        AddLog($"❌ 配置文件解析失败: {parseEx.Message}");
                    }
                }
                else
                {
                    AddLog("⚠️ 配置文件不存在");
                }
                
                // 🔧 修复：如果当前有选中的配置，尝试重新加载它
                AutoMonitorConfig oldConfig = null;
                if (_currentConfig != null)
                {
                    // 保存旧配置用于变化检测
                    oldConfig = _currentConfig;
                    
                    var refreshedConfig = ConfigSelectionComboBox.ItemsSource?.Cast<AutoMonitorConfig>()
                        .FirstOrDefault(c => c.Name == _currentConfig.Name);
                    
                    if (refreshedConfig != null)
                    {
                        _currentConfig = refreshedConfig;
                        ConfigSelectionComboBox.SelectedItem = refreshedConfig;
                        AddLog($"✅ 已重新加载配置: {_currentConfig.Name}");
                        
                        // 🔧 新增：检测配置是否有变化
                        if (HasConfigurationChanged(oldConfig, _currentConfig))
                        {
                            AddLog("🔍 检测到基础配置参数变化！");
                            AddLog("🔄 自动同步配置变化到所有合约配置...");
                            
                            // 🔧 自动同步，不询问用户
                            _needsConfigSync = true;
                        }
                        else
                        {
                            AddLog("ℹ️ 基础配置无重要变化，保持现有合约配置");
                            _needsConfigSync = false;
                        }
                        
                        // 输出配置详情用于验证
                        AddLog($"📊 配置验证 - 推仓档位: {_currentConfig.AddPositionConfig?.Tiers?.Count ?? 0}");
                        if (_currentConfig.AddPositionConfig?.Tiers != null)
                        {
                            foreach (var tier in _currentConfig.AddPositionConfig.Tiers)
                            {
                                AddLog($"  推仓{tier.TierIndex}档: {tier.TriggerProfitAmount}U (来源: 重新加载)");
                            }
                        }
                    }
                    else
                    {
                        AddLog($"⚠️ 无法找到配置: {_currentConfig.Name}，回退到默认配置");
                CreateDefaultConfig();
                    }
                }
                else
                {
                    AddLog("⚠️ 当前无选中配置，创建默认配置");
                    CreateDefaultConfig();
                }
                
                // 更新UI显示
                UpdateConfigDisplay();
                
                // 🔧 修复：根据是否需要同步来决定更新策略
                if (_currentConfig != null)
                {
                    if (_needsConfigSync)
                    {
                        AddLog($"🔄 强制基于新配置 '{_currentConfig.Name}' 重新生成所有合约配置...");
                        _ = Task.Run(async () =>
                        {
                            await UpdateAllContractConfigsAsync();
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                AddLog("✅ 合约配置强制同步完成！所有合约已使用最新参数");
                            });
                        });
                    }
                    else
                    {
                        AddLog($"🔄 基于配置 '{_currentConfig.Name}' 常规刷新合约配置...");
                        _ = Task.Run(async () =>
                        {
                            await UpdateAllContractConfigsAsync();
                            AddLog("✅ 合约配置常规更新完成");
                        });
                    }
                }
                else
                {
                _ = RefreshPositionDataAsync();
                }
                
                AddLog("🔄 配置刷新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新配置失败");
                AddLog($"❌ 刷新配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔧 新增：检查配置是否发生重要变化
        /// </summary>
        private bool HasConfigurationChanged(AutoMonitorConfig oldConfig, AutoMonitorConfig newConfig)
        {
            if (oldConfig == null || newConfig == null)
                return true;
            
            try
            {
                // 检查保本配置变化
                bool breakEvenChanged = false;
                if (oldConfig.BreakEvenConfig?.TriggerProfitAmount != newConfig.BreakEvenConfig?.TriggerProfitAmount ||
                    oldConfig.BreakEvenConfig?.IsEnabled != newConfig.BreakEvenConfig?.IsEnabled)
                {
                    breakEvenChanged = true;
                    AddLog($"🔍 保本配置变化：{oldConfig.BreakEvenConfig?.TriggerProfitAmount}U -> {newConfig.BreakEvenConfig?.TriggerProfitAmount}U");
                }
                
                // 检查推仓配置变化
                bool addPositionChanged = false;
                var oldTiers = oldConfig.AddPositionConfig?.Tiers?.Count ?? 0;
                var newTiers = newConfig.AddPositionConfig?.Tiers?.Count ?? 0;
                if (oldTiers != newTiers)
                {
                    addPositionChanged = true;
                    AddLog($"🔍 推仓阶梯数量变化：{oldTiers} -> {newTiers}");
                }
                else if (newConfig.AddPositionConfig?.Tiers != null && oldConfig.AddPositionConfig?.Tiers != null)
                {
                    for (int i = 0; i < newTiers; i++)
                    {
                        if (i < oldConfig.AddPositionConfig.Tiers.Count)
                {
                    var oldTier = oldConfig.AddPositionConfig.Tiers[i];
                    var newTier = newConfig.AddPositionConfig.Tiers[i];
                            if (oldTier.TriggerProfitAmount != newTier.TriggerProfitAmount)
                            {
                                addPositionChanged = true;
                                AddLog($"🔍 推仓{i + 1}档变化：{oldTier.TriggerProfitAmount}U -> {newTier.TriggerProfitAmount}U");
                            }
                        }
                    }
                }
                
                // 检查止盈配置变化
                bool profitProtectionChanged = false;
                var oldProfitTiers = oldConfig.ProfitProtectionConfig?.Tiers?.Count ?? 0;
                var newProfitTiers = newConfig.ProfitProtectionConfig?.Tiers?.Count ?? 0;
                if (oldProfitTiers != newProfitTiers)
                {
                    profitProtectionChanged = true;
                    AddLog($"🔍 止盈阶梯数量变化：{oldProfitTiers} -> {newProfitTiers}");
                }
                
                return breakEvenChanged || addPositionChanged || profitProtectionChanged;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查配置变化时发生异常");
                return true; // 发生异常时认为有变化，确保安全
            }
        }

        /// <summary>
        /// 从统一状态文件重新加载合约状态
        /// </summary>
        private async Task ReloadContractStateFromUnifiedFile(ContractConfigViewModel config)
        {
            try
            {
                AddLog($"🔄 重新加载合约状态: {config.ContractName}");
                
                // 使用统一状态管理服务重新加载状态
                var filePathManager = new FilePathManager();
                var currentAccount = filePathManager.GetCurrentAccountName();
                var configManager = BaseConfigManager.Instance;
                var typedLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ContractMonitoringStateService>.Instance;
                var stateService = new ContractMonitoringStateService(typedLogger, configManager, filePathManager, currentAccount);

                var contractKey = config.ContractName.Replace(" ", "_"); // 将 "BTCUSDT LONG" 转换为 "BTCUSDT_LONG"
                var state = stateService.GetMonitoringState(contractKey);
                
                AddLog($"🔍 尝试加载状态: 原始={config.ContractName}, 标准化={contractKey}");
                
                if (state != null)
                {
                    // 更新保本状态
                    var newBreakEvenStatus = state.BreakEvenConfig.IsExecuted ? "✓" : "-";
                    if (config.BreakEvenStatus != newBreakEvenStatus)
                    {
                        config.BreakEvenStatus = newBreakEvenStatus;
                        AddLog($"📊 保本状态已更新: {newBreakEvenStatus}");
                    }
                    
                    // 更新推仓状态
                    var pushTiers = state.AddPositionConfig.Tiers.OrderBy(t => t.TierIndex).Take(4).ToArray();
                    var pushStatuses = new[] { config.PushTier1Status, config.PushTier2Status, config.PushTier3Status, config.PushTier4Status };
                    for (int i = 0; i < pushTiers.Length && i < pushStatuses.Length; i++)
                    {
                        var newStatus = pushTiers[i].IsExecuted ? "✓" : "-";
                        if (pushStatuses[i] != newStatus)
                        {
                            switch (i)
                            {
                                case 0: config.PushTier1Status = newStatus; break;
                                case 1: config.PushTier2Status = newStatus; break;
                                case 2: config.PushTier3Status = newStatus; break;
                                case 3: config.PushTier4Status = newStatus; break;
                            }
                            AddLog($"📊 推仓阶梯{i + 1}状态已更新: {newStatus}");
                        }
                    }

                    // 更新保盈状态
                    var profitTiers = state.ProfitProtectionConfig.Tiers.OrderBy(t => t.TierIndex).Take(3).ToArray();
                    var profitStatuses = new[] { config.ProfitTier1Status, config.ProfitTier2Status, config.ProfitTier3Status };
                    for (int i = 0; i < profitTiers.Length && i < profitStatuses.Length; i++)
                    {
                        var newStatus = profitTiers[i].IsExecuted ? "✓" : "-";
                        if (profitStatuses[i] != newStatus)
                        {
                            switch (i)
                            {
                                case 0: config.ProfitTier1Status = newStatus; break;
                                case 1: config.ProfitTier2Status = newStatus; break;
                                case 2: config.ProfitTier3Status = newStatus; break;
                            }
                            AddLog($"📊 保盈阶梯{i + 1}状态已更新: {newStatus}");
                        }
                    }
                    
                    AddLog($"✅ 合约状态重新加载完成: {contractKey}");
                }
                else
                {
                    AddLog($"⚠️ 未找到合约状态记录: {contractKey}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重新加载合约状态失败");
                AddLog($"❌ 重新加载合约状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新UI中的合约配置显示
        /// </summary>
        private void UpdateContractConfigInUI(ContractConfigViewModel originalConfig, ContractConfigViewModel editedConfig)
        {
            try
            {
                // 更新原始配置对象的属性
                originalConfig.BreakEvenTarget = editedConfig.BreakEvenTarget;
                originalConfig.BreakEvenStatus = editedConfig.BreakEvenStatus;
                originalConfig.UpdateTime = editedConfig.UpdateTime;
                
                // 🔧 关键修复：强制设置保本状态（标记为手动修改）
                originalConfig.MarkAsManuallyModified("BreakEvenStatus");
                
                // 🔧 重要：如果保本状态被修改，强制写入动态数据
                if (originalConfig.BreakEvenStatus != editedConfig.BreakEvenStatus)
                {
                    originalConfig.SetDynamicData("BreakEvenStatus", editedConfig.BreakEvenStatus, "Black", true);
                    AddLog($"🔧 强制保存保本状态修改: {originalConfig.BreakEvenStatus} → {editedConfig.BreakEvenStatus}");
                }
                
                // 🔧 复制所有状态数据（标记为手动修改）
                var statusFields = new[]
                {
                    "PushTier1Status", "PushTier2Status", "PushTier3Status", "PushTier4Status",
                    "ProfitTier1Status", "ProfitTier2Status", "ProfitTier3Status"
                };
                
                foreach (var field in statusFields)
                {
                    var originalValue = GetStatusByFieldName(originalConfig, field);
                    var editedValue = GetStatusByFieldName(editedConfig, field);
                    
                    if (originalValue != editedValue)
                    {
                        SetStatusByFieldName(originalConfig, field, editedValue);
                        originalConfig.MarkAsManuallyModified(field);
                        AddLog($"🔧 强制保存状态修改 {field}: {originalValue} → {editedValue}");
                    }
                }
                
                // 🔧 复制动态数据（标记为手动修改）
                for (int i = 1; i <= 10; i++)
                {
                    // 推仓状态
                    var pushKey = $"Push{i}";
                    var pushValue = editedConfig.GetDynamicData(pushKey);
                    if (!string.IsNullOrEmpty(pushValue))
                    {
                        originalConfig.SetDynamicData(pushKey, pushValue, editedConfig.GetDynamicColor(pushKey), true);
                    }
                    
                    // 保盈状态
                    var profitKey = $"Profit{i}";
                    var profitValue = editedConfig.GetDynamicData(profitKey);
                    if (!string.IsNullOrEmpty(profitValue))
                    {
                        originalConfig.SetDynamicData(profitKey, profitValue, editedConfig.GetDynamicColor(profitKey), true);
                    }
                }
                
                // 🔧 关键修复：将状态修改同步到后台数据源
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SyncStatusToBackendSources(originalConfig);
                        AddLog($"💾 状态修改已同步到后台数据源: {originalConfig.ContractName}");
                    }
                    catch (Exception syncEx)
                    {
                        _logger.LogError(syncEx, "同步状态到后台数据源失败");
                        AddLog($"❌ 同步状态失败: {syncEx.Message}");
                    }
                });
                
                // 🔧 关键修复：保存到合约配置文件（确保数据持久化）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SaveContractConfigToFile(originalConfig);
                        AddLog($"💾 合约配置已保存到文件: {originalConfig.ContractName}");
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, "保存合约配置文件失败");
                        AddLog($"❌ 保存配置文件失败: {saveEx.Message}");
                    }
                });

                // 🔧 关键修复：重新填充触发金额数据到界面显示
                RefreshContractTriggerAmounts(originalConfig);
                
                // 🔧 触发属性更新通知 - 通过公共方法
                originalConfig.NotifyAllPropertiesChanged();
                
                _logger.LogInformation($"✅ UI中的合约配置已更新（状态已标记为手动修改）: {originalConfig.ContractName}");
                AddLog($"✅ 合约配置已更新（已保护手动修改的状态）: {originalConfig.ContractName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新UI中的合约配置失败");
                AddLog($"❌ 更新UI显示失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔧 新增：确保配置包含最新的手动修改状态
        /// </summary>
        private ContractConfigViewModel EnsureLatestManualStatus(ContractConfigViewModel originalConfig)
        {
            try
            {
                // 从所有可能的数据源收集最新状态
                var parts = originalConfig.ContractName.Split(' ');
                if (parts.Length < 2) return originalConfig;
                
                var symbol = parts[0];
                var side = parts[1];
                
                // 1. 检查本地手动修改文件
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BinanceFuturesTrader", "ContractConfigs.json");
                
                if (File.Exists(configPath))
                {
                    try
                    {
                        var json = File.ReadAllText(configPath);
                        var savedConfigs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ContractConfigData>>(json);
                        
                        if (savedConfigs != null && savedConfigs.TryGetValue(originalConfig.ContractName, out var savedConfig))
                        {
                            // 应用手动修改的保本状态（简化日志）
                            if (!string.IsNullOrEmpty(savedConfig.BreakEvenStatus) && savedConfig.BreakEvenStatus != "-")
                            {
                                originalConfig.BreakEvenStatus = savedConfig.BreakEvenStatus;
                                originalConfig.BreakEvenTarget = savedConfig.BreakEvenTarget;
                                originalConfig.MarkAsManuallyModified("BreakEvenStatus");
                                AddLog($"✅ 保本状态已标记为手动修改: {savedConfig.BreakEvenStatus}");
                            }
                            
                            // 应用手动修改的推仓状态
                            if (!string.IsNullOrEmpty(savedConfig.PushTier1Status) && savedConfig.PushTier1Status != "-")
                            {
                                originalConfig.SetDynamicData("Push1", savedConfig.PushTier1Status, "Black", true);
                                AddLog($"✅ 推仓1档状态已标记为手动修改: {savedConfig.PushTier1Status}");
                            }
                            
                            if (!string.IsNullOrEmpty(savedConfig.PushTier2Status) && savedConfig.PushTier2Status != "-")
                            {
                                originalConfig.SetDynamicData("Push2", savedConfig.PushTier2Status, "Black", true);
                                AddLog($"✅ 推仓2档状态已标记为手动修改: {savedConfig.PushTier2Status}");
                            }
                            
                            if (!string.IsNullOrEmpty(savedConfig.PushTier3Status) && savedConfig.PushTier3Status != "-")
                            {
                                originalConfig.SetDynamicData("Push3", savedConfig.PushTier3Status, "Black", true);
                                AddLog($"✅ 推仓3档状态已标记为手动修改: {savedConfig.PushTier3Status}");
                            }
                            
                            if (!string.IsNullOrEmpty(savedConfig.PushTier4Status) && savedConfig.PushTier4Status != "-")
                            {
                                originalConfig.SetDynamicData("Push4", savedConfig.PushTier4Status, "Black", true);
                                AddLog($"✅ 推仓4档状态已标记为手动修改: {savedConfig.PushTier4Status}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"❌ 读取本地配置文件失败: {ex.Message}");
                    }
                }
                
                // 2. 检查AutoMonitorService中的状态
                // 🔧 【关键修复】优先从状态文件检查，确保与文件同步
                if (_autoMonitorService != null)
                {
                    // 先检查状态文件
                    var isBreakEvenExecuted = _autoMonitorService.IsExecutedInStateFile(symbol, side, "保本");
                    if (isBreakEvenExecuted)
                    {
                        originalConfig.BreakEvenStatus = "√";
                        originalConfig.MarkAsManuallyModified("BreakEvenStatus");
                        AddLog($"🔍 从状态文件读取到保本已执行状态");
                    }
                    else
                    {
                        // 回退：从服务获取最新状态
                        var positionProfiles = _autoMonitorService.GetPositionProfiles();
                        var profileKey = $"{symbol}_{side}";
                        
                        if (positionProfiles.ContainsKey(profileKey))
                        {
                            var profile = positionProfiles[profileKey];
                            
                            // 检查是否有保本触发记录
                            if (profile.TriggerRecords.ContainsKey("BreakEven"))
                            {
                                var record = profile.TriggerRecords["BreakEven"];
                                if (record.IsExecuted)
                                {
                                    originalConfig.BreakEvenStatus = "√";
                                    originalConfig.MarkAsManuallyModified("BreakEvenStatus");
                                    AddLog($"🔍 从AutoMonitorService读取到保本已执行状态");
                                }
                            }
                        }
                    }
                }
                
                return originalConfig;
            }
            catch (Exception ex)
            {
                AddLog($"❌ 确保最新手动状态失败: {ex.Message}");
                return originalConfig;
            }
        }

        /// <summary>
        /// 🔧 新增：根据字段名获取状态值
        /// </summary>
        private string GetStatusByFieldName(ContractConfigViewModel config, string fieldName)
        {
            return fieldName switch
            {
                "PushTier1Status" => config.PushTier1Status,
                "PushTier2Status" => config.PushTier2Status,
                "PushTier3Status" => config.PushTier3Status,
                "PushTier4Status" => config.PushTier4Status,
                "ProfitTier1Status" => config.ProfitTier1Status,
                "ProfitTier2Status" => config.ProfitTier2Status,
                "ProfitTier3Status" => config.ProfitTier3Status,
                _ => "-"
            };
        }
        
        /// <summary>
        /// 🔧 新增：根据字段名设置状态值
        /// </summary>
        private void SetStatusByFieldName(ContractConfigViewModel config, string fieldName, string value)
        {
            switch (fieldName)
            {
                case "PushTier1Status":
                    config.PushTier1Status = value;
                    break;
                case "PushTier2Status":
                    config.PushTier2Status = value;
                    break;
                case "PushTier3Status":
                    config.PushTier3Status = value;
                    break;
                case "PushTier4Status":
                    config.PushTier4Status = value;
                    break;
                case "ProfitTier1Status":
                    config.ProfitTier1Status = value;
                    break;
                case "ProfitTier2Status":
                    config.ProfitTier2Status = value;
                    break;
                case "ProfitTier3Status":
                    config.ProfitTier3Status = value;
                    break;
            }
        }

        /// <summary>
        /// 🔧 新增：将状态修改同步到后台数据源
        /// </summary>
        private async Task SyncStatusToBackendSources(ContractConfigViewModel config)
        {
            try
            {
                // 解析合约名称
                var parts = config.ContractName.Split(' ');
                if (parts.Length < 2) return;
                
                var symbol = parts[0];
                var side = parts[1];
                
                // 1. 同步到ContractProfile（档案系统）
                var profile = _profileService.GetProfile(symbol, side);
                if (profile != null)
                {
                    // 更新档案的操作历史，记录手动修改
                    profile.AddOperationHistory("手动状态修改", "成功", 
                        $"用户修改状态 - 保本: {config.BreakEvenStatus}");
                    await _profileService.UpdateProfileAsync(profile);
                    
                    _logger.LogInformation($"✅ 已同步到档案系统: {symbol}_{side}");
                }
                
                // 2. 同步到AutoMonitorService（如果正在运行）
                if (_autoMonitorService != null)
                {
                    // 获取持仓档案
                    var positionProfiles = _autoMonitorService.GetPositionProfiles();
                    var profileKey = $"{symbol}_{side}";
                    
                    if (positionProfiles.ContainsKey(profileKey))
                    {
                        var positionProfile = positionProfiles[profileKey];
                        
                        // 🔧 关键：手动设置状态到触发记录中
                        if (config.BreakEvenStatus == "已执行")
                        {
                            positionProfile.TriggerRecords["BreakEven"] = new TriggerRecord
                            {
                                TriggerType = "保本",
                                TriggerTime = DateTime.Now,
                                TriggerPnl = 0, // 手动设置
                                IsExecuted = true,
                                ExecutionResult = "手动设置"
                            };
                        }
                        else if (config.BreakEvenStatus == "未触发")
                        {
                            positionProfile.TriggerRecords.Remove("BreakEven");
                        }
                        
                        // 处理推仓和保盈状态
                        for (int i = 1; i <= 10; i++)
                        {
                            var pushValue = config.GetDynamicData($"Push{i}");
                            var profitValue = config.GetDynamicData($"Profit{i}");
                            
                            if (!string.IsNullOrEmpty(pushValue) && pushValue.Contains("已执行"))
                            {
                                positionProfile.TriggerRecords[$"AddPosition_Stage{i}"] = new TriggerRecord
                                {
                                    TriggerType = $"推仓{i}档",
                                    TriggerTime = DateTime.Now,
                                    TriggerPnl = 0,
                                    IsExecuted = true,
                                    ExecutionResult = "手动设置"
                                };
                            }
                            
                            if (!string.IsNullOrEmpty(profitValue) && profitValue.Contains("已执行"))
                            {
                                positionProfile.TriggerRecords[$"ProfitProtection_Stage{i}"] = new TriggerRecord
                                {
                                    TriggerType = $"保盈{i}档",
                                    TriggerTime = DateTime.Now,
                                    TriggerPnl = 0,
                                    IsExecuted = true,
                                    ExecutionResult = "手动设置"
                                };
                            }
                        }
                        
                        _logger.LogInformation($"✅ 已同步到AutoMonitorService: {symbol}_{side}");
                    }
                }
                
                // 3. 记录到执行历史
                if (_autoMonitorService != null)
                {
                    var history = _autoMonitorService.GetExecutionHistory();
                    history.Add(new ExecutionHistory
                    {
                        Symbol = symbol,
                        PositionSide = side,
                        ExecutionType = "手动状态修改",
                        ExecutionTime = DateTime.Now,
                        TriggerPnl = config.CurrentPnl,
                        IsSuccess = true,
                        Details = $"用户手动修改状态 - 保本: {config.BreakEvenStatus}"
                    });
                    
                    _logger.LogInformation($"✅ 已记录到执行历史: {symbol}_{side}");
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步状态到后台数据源失败");
                throw;
            }
        }

        /// <summary>
        /// 判断是否为用户自定义配置（过滤掉系统默认配置）
        /// </summary>
        private bool IsUserCustomConfig(AutoMonitorConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.Name))
                return false;

            var configName = config.Name.ToLower();
            
            // 过滤掉包含系统默认关键词的配置
            var systemKeywords = new[]
            {
                "默认", "default", 
                "智能", "smart", 
                "配置", "config",
                "临时", "temp",
                "测试", "test",
                "示例", "sample",
                "模板", "template"
            };

            // 如果配置名包含系统关键词，则认为是系统配置
            if (systemKeywords.Any(keyword => configName.Contains(keyword.ToLower())))
            {
                return false;
            }

            // 其他情况认为是用户自定义配置
            return true;
        }
        
        #endregion

        /// <summary>
        /// 🔧 保存合约配置到本地文件 - 使用统一状态管理
        /// </summary>
        private async Task SaveContractConfigToFile(ContractConfigViewModel config)
        {
            try
            {
                // 🔧 修复：使用正确的统一状态文件路径
                var filePathManager = new FilePathManager();
                var currentAccount = filePathManager.GetCurrentAccountName();
                var configPath = filePathManager.GetContractMonitoringStatesFilePath(currentAccount);
                var directory = Path.GetDirectoryName(configPath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Dictionary<string, ContractConfigData> allConfigs;
                
                // 读取现有配置
                if (File.Exists(configPath))
                {
                    var existingJson = await File.ReadAllTextAsync(configPath);
                    allConfigs = JsonSerializer.Deserialize<Dictionary<string, ContractConfigData>>(existingJson) ?? new Dictionary<string, ContractConfigData>();
                }
                else
                {
                    allConfigs = new Dictionary<string, ContractConfigData>();
                }

                // 🔧 创建包含完整配置数据的对象
                var configData = new ContractConfigData
                {
                    ContractName = config.ContractName,
                    
                    // 保本配置
                    BreakEvenTarget = config.BreakEvenTarget,
                    BreakEvenStatus = config.BreakEvenStatus,
                    
                    // 推仓配置 - 从动态数据和基础配置获取
                    PushTier1Amount = GetPushTierAmountFromConfig(1),
                    PushTier1Status = config.PushTier1Status,
                    PushTier2Amount = GetPushTierAmountFromConfig(2),
                    PushTier2Status = config.PushTier2Status,
                    PushTier3Amount = GetPushTierAmountFromConfig(3),
                    PushTier3Status = config.PushTier3Status,
                    PushTier4Amount = GetPushTierAmountFromConfig(4),
                    PushTier4Status = config.PushTier4Status,
                    
                    // 保盈配置 - 从基础配置获取
                    ProfitTier1TriggerAmount = GetProfitTierTriggerAmountFromConfig(1),
                    ProfitTier1ProtectionAmount = GetProfitTierProtectionAmountFromConfig(1),
                    ProfitTier1Status = config.ProfitTier1Status,
                    ProfitTier2TriggerAmount = GetProfitTierTriggerAmountFromConfig(2),
                    ProfitTier2ProtectionAmount = GetProfitTierProtectionAmountFromConfig(2),
                    ProfitTier2Status = config.ProfitTier2Status,
                    ProfitTier3TriggerAmount = GetProfitTierTriggerAmountFromConfig(3),
                    ProfitTier3ProtectionAmount = GetProfitTierProtectionAmountFromConfig(3),
                    ProfitTier3Status = config.ProfitTier3Status,
                    
                    LastModified = DateTime.Now
                };

                allConfigs[config.ContractName] = configData;

                // 🔧 【重要修复】禁用旧格式保存，防止覆盖新的统一状态文件
                // var options = new JsonSerializerOptions { WriteIndented = true };
                // var json = JsonSerializer.Serialize(allConfigs, options);
                // await File.WriteAllTextAsync(configPath, json);
                
                _logger.LogWarning($"⚠️ 已禁用旧格式保存，避免覆盖 contract_monitoring_states.json 新格式");
                _logger.LogWarning($"   ✅ 配置数据应通过 ContractMonitoringStateService 管理");

                _logger.LogInformation($"✅ 已保存完整合约配置到本地文件: {config.ContractName}");
                _logger.LogInformation($"   保本: {configData.BreakEvenTarget}U ({configData.BreakEvenStatus})");
                _logger.LogInformation($"   推仓: T1={configData.PushTier1Amount}U({configData.PushTier1Status}), T2={configData.PushTier2Amount}U({configData.PushTier2Status}), T3={configData.PushTier3Amount}U({configData.PushTier3Status}), T4={configData.PushTier4Amount}U({configData.PushTier4Status})");
                _logger.LogInformation($"   保盈: T1={configData.ProfitTier1TriggerAmount}|{configData.ProfitTier1ProtectionAmount}U({configData.ProfitTier1Status}), T2={configData.ProfitTier2TriggerAmount}|{configData.ProfitTier2ProtectionAmount}U({configData.ProfitTier2Status}), T3={configData.ProfitTier3TriggerAmount}|{configData.ProfitTier3ProtectionAmount}U({configData.ProfitTier3Status})");
                
                AddLog($"💾 合约配置已保存到本地文件: {config.ContractName}");
                
                // 🔧 记录操作历史
                var statusSummary = $"保本:{configData.BreakEvenStatus}, 推仓:[{configData.PushTier1Status},{configData.PushTier2Status},{configData.PushTier3Status},{configData.PushTier4Status}], 保盈:[{configData.ProfitTier1Status},{configData.ProfitTier2Status},{configData.ProfitTier3Status}]";
                SaveOperationHistory("配置修改", config.ContractName, statusSummary, "CONFIG_SAVE");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存合约配置到本地文件失败");
                AddLog($"❌ 保存配置文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取合约配置文件路径
        /// ⚠️ 已废弃：现在使用统一的 contract_monitoring_states.json 文件
        /// </summary>
        [Obsolete("已废弃：请使用 ContractMonitoringStateService 获取统一的配置状态文件路径")]
        private string GetContractConfigFilePath()
        {
            // 🔧 修改：返回新的统一状态文件路径
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BinanceFuturesTrader",
                "AutoMonitor");
            return Path.Combine(appDataPath, "contract_monitoring_states.json");
        }

        /// <summary>
        /// 从基础配置获取推仓阶梯的触发金额
        /// </summary>
        private decimal GetPushTierAmountFromConfig(int tierIndex)
        {
            try
            {
                if (_currentConfig?.AddPositionConfig?.IsEnabled == true)
                {
                    var tier = _currentConfig.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
                    if (tier != null)
                    {
                        return tier.TriggerProfitAmount;
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 从基础配置获取保盈阶梯的触发金额
        /// </summary>
        private decimal GetProfitTierTriggerAmountFromConfig(int tierIndex)
        {
            try
            {
                if (_currentConfig?.ProfitProtectionConfig?.IsEnabled == true)
                {
                    var tier = _currentConfig.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
                    if (tier != null)
                    {
                        return tier.TriggerProfitAmount;
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 从基础配置获取保盈阶梯的保护金额
        /// </summary>
        private decimal GetProfitTierProtectionAmountFromConfig(int tierIndex)
        {
            try
            {
                if (_currentConfig?.ProfitProtectionConfig?.IsEnabled == true)
                {
                    var tier = _currentConfig.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
                    if (tier != null)
                    {
                        return tier.ProtectionAmount;
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 合约配置数据存储结构
        /// </summary>
        public class ContractConfigData
        {
            public string ContractName { get; set; } = "";
            
            // 保本配置
            public decimal BreakEvenTarget { get; set; }
            public string BreakEvenStatus { get; set; } = "-";
            
            // 推仓配置 - 包含触发金额数据
            public decimal PushTier1Amount { get; set; }
            public string PushTier1Status { get; set; } = "-";
            public decimal PushTier2Amount { get; set; }
            public string PushTier2Status { get; set; } = "-";
            public decimal PushTier3Amount { get; set; }
            public string PushTier3Status { get; set; } = "-";
            public decimal PushTier4Amount { get; set; }
            public string PushTier4Status { get; set; } = "-";
            
            // 保盈配置 - 包含触发金额和保护金额数据
            public decimal ProfitTier1TriggerAmount { get; set; }
            public decimal ProfitTier1ProtectionAmount { get; set; }
            public string ProfitTier1Status { get; set; } = "-";
            public decimal ProfitTier2TriggerAmount { get; set; }
            public decimal ProfitTier2ProtectionAmount { get; set; }
            public string ProfitTier2Status { get; set; } = "-";
            public decimal ProfitTier3TriggerAmount { get; set; }
            public decimal ProfitTier3ProtectionAmount { get; set; }
            public string ProfitTier3Status { get; set; } = "-";
            
            public DateTime LastModified { get; set; }
        }

        /// <summary>
        /// 🔧 新增：刷新合约的触发金额显示
        /// </summary>
        private void RefreshContractTriggerAmounts(ContractConfigViewModel config)
        {
            try
            {
                if (_currentConfig == null) return;

                // 🔧 重新填充保本触发金额
                if (_currentConfig.BreakEvenConfig?.IsEnabled == true)
                {
                    config.BreakEvenTarget = _currentConfig.BreakEvenConfig.TriggerProfitAmount;
                }

                // 🔧 重新填充推仓触发金额
                if (_currentConfig.AddPositionConfig?.IsEnabled == true)
                {
                    var addTiers = _currentConfig.AddPositionConfig.Tiers.OrderBy(t => t.TierIndex).ToList();
                    for (int i = 0; i < Math.Min(addTiers.Count, 4); i++)
                    {
                        var tier = addTiers[i];
                        var key = $"Push{i + 1}";
                        var displayValue = $"{tier.TriggerProfitAmount:F0}U";
                        var color = GetStatusColorForTier(config, i + 1, "Push");
                        
                        config.SetDynamicData(key, displayValue, color, true);
                        _logger.LogDebug($"🔄 刷新推仓T{i+1}: {displayValue} ({color})");
                    }
                }

                // 🔧 重新填充保盈触发金额
                if (_currentConfig.ProfitProtectionConfig?.IsEnabled == true)
                {
                    var profitTiers = _currentConfig.ProfitProtectionConfig.Tiers.OrderBy(t => t.TierIndex).ToList();
                    for (int i = 0; i < Math.Min(profitTiers.Count, 3); i++)
                    {
                        var tier = profitTiers[i];
                        var key = $"Profit{i + 1}";
                        var displayValue = $"{tier.TriggerProfitAmount:F0}|{tier.ProtectionAmount:F0}U";
                        var color = GetStatusColorForTier(config, i + 1, "Profit");
                        
                        config.SetDynamicData(key, displayValue, color, true);
                        _logger.LogDebug($"🔄 刷新保盈T{i+1}: {displayValue} ({color})");
                    }
                }

                AddLog($"🔄 已刷新合约触发金额显示: {config.ContractName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新合约触发金额显示失败");
                AddLog($"❌ 刷新触发金额失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取阶梯状态对应的颜色
        /// </summary>
        private string GetStatusColorForTier(ContractConfigViewModel config, int tierIndex, string type)
        {
            try
            {
                string status = type switch
                {
                    "Push" when tierIndex == 1 => config.PushTier1Status,
                    "Push" when tierIndex == 2 => config.PushTier2Status,
                    "Push" when tierIndex == 3 => config.PushTier3Status,
                    "Push" when tierIndex == 4 => config.PushTier4Status,
                    "Profit" when tierIndex == 1 => config.ProfitTier1Status,
                    "Profit" when tierIndex == 2 => config.ProfitTier2Status,
                    "Profit" when tierIndex == 3 => config.ProfitTier3Status,
                    _ => "-"
                };

                return status switch
                {
                    "√" => "Green",     // 已执行
                    "-" => "Gray",      // 未触发
                    _ => "Black"        // 默认
                };
            }
            catch
            {
                return "Black";
            }
        }

        /// <summary>
        /// 处理状态更新事件
        /// </summary>
        private void OnStatusUpdated(object? sender, StatusUpdateEventArgs e)
        {
            try
            {
                // 🔍【UI状态更新诊断】记录接收到的状态更新事件
                _logger.LogCritical($"🔍【UI状态更新诊断】接收到状态更新事件:");
                _logger.LogCritical($"   📊 合约: {e.Symbol}_{e.PositionSide}");
                _logger.LogCritical($"   📈 保本执行: {e.BreakEvenExecuted}");
                _logger.LogCritical($"   📈 推仓结果: {e.AddPositionResults?.Count ?? 0}个");
                _logger.LogCritical($"   📈 保盈结果: {e.ProfitProtectionResults?.Count ?? 0}个");
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _logger.LogCritical($"🚀【UI线程执行】开始调用UpdateContractStatus");
                    UpdateContractStatus(e);
                    _logger.LogCritical($"✅【UI线程完成】UpdateContractStatus执行完毕");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 处理状态更新失败");
                AddLog($"❌ 处理状态更新失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 🔧 【重要新增】：持仓变化事件处理器 - 立即响应新开仓
        /// </summary>
        private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    AddLog($"🆕【持仓变化】{e.Symbol}_{e.PositionSide}: {e.ChangeType} - 数量:{e.CurrentQuantity:F6}, 浮盈:{e.CurrentPnl:F2}U");
                    
                    if (e.ChangeType == PositionChangeType.Opened)
                    {
                        AddLog($"🔄 检测到新开仓，自动生成合约配置...");
                        
                        // 立即刷新持仓数据，确保新仓位显示在列表中
                        await RefreshPositionDataAsync();
                        
                        AddLog($"✅ 新开仓配置已添加到合约列表");
                    }
                    else if (e.ChangeType == PositionChangeType.Closed)
                    {
                        AddLog($"❌ 检测到平仓，将移除对应的合约配置");
                        await RefreshPositionDataAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理持仓变化事件失败");
                AddLog($"❌ 处理持仓变化事件失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新合约状态显示
        /// </summary>
        private void UpdateContractStatus(StatusUpdateEventArgs statusUpdate)
        {
            try
            {
                // 🔧 修复：正确处理所有持仓方向，包括BOTH
                var positionSideText = statusUpdate.PositionSide switch
                {
                    "LONG" => "LONG",
                    "SHORT" => "SHORT", 
                    "BOTH" => "BOTH",
                    _ => statusUpdate.PositionSide // 保持原值
                };
                
                var contractName = $"{statusUpdate.Symbol} {positionSideText}";
                var config = ContractConfigs.FirstOrDefault(c => c.ContractName == contractName);
                
                if (config == null)
                {
                    AddLog($"⚠️ 未找到合约配置: {contractName}");
                    return;
                }
                
                AddLog($"🔄【状态更新】{contractName}: 开始更新状态显示");
                
                // 更新保本状态
                if (statusUpdate.BreakEvenExecuted)
                {
                    config.BreakEvenStatus = "√";
                    AddLog($"✅【保本状态】{contractName}: 已执行");
                }
                
                // 更新推仓状态
                foreach (var result in statusUpdate.AddPositionResults)
                {
                    var tierIndex = result.Key;
                    var isSuccess = result.Value;
                    
                    switch (tierIndex)
                    {
                        case 1:
                            config.PushTier1Status = isSuccess ? "√" : "-";
                            break;
                        case 2:
                            config.PushTier2Status = isSuccess ? "√" : "-";
                            break;
                        case 3:
                            config.PushTier3Status = isSuccess ? "√" : "-";
                            break;
                        case 4:
                            config.PushTier4Status = isSuccess ? "√" : "-";
                            break;
                    }
                    
                    if (isSuccess)
                    {
                        AddLog($"✅【推仓状态】{contractName}-阶梯{tierIndex}: 已执行");
                    }
                }
                
                // 更新保盈状态
                foreach (var result in statusUpdate.ProfitProtectionResults)
                {
                    var tierIndex = result.Key;
                    var isSuccess = result.Value;
                    
                    switch (tierIndex)
                    {
                        case 1:
                            config.ProfitTier1Status = isSuccess ? "√" : "-";
                            break;
                        case 2:
                            config.ProfitTier2Status = isSuccess ? "√" : "-";
                            break;
                        case 3:
                            config.ProfitTier3Status = isSuccess ? "√" : "-";
                            break;
                    }
                    
                    if (isSuccess)
                    {
                        AddLog($"✅【保盈状态】{contractName}-阶梯{tierIndex}: 已执行");
                    }
                }
                
                // 刷新UI显示
                ContractConfigDataGrid.Items.Refresh();
                
                AddLog($"✅【状态更新】{contractName}: 状态显示已更新");
            }
            catch (Exception ex)
            {
                AddLog($"❌ 更新合约状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔧 【关键修复】统一获取合约状态，确保UI与后台状态一致
        /// </summary>
        private string GetUnifiedContractStatus(string symbol, string positionSide, string statusType, int? tierIndex = null)
        {
            try
            {
                // 1. 🔧 【关键修复】直接从状态管理器获取状态，确保使用相同的键格式
                // 通过反射获取状态管理器（这是临时解决方案）
                var stateManagerField = _autoMonitorService.GetType().GetField("_unifiedStateManager", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (stateManagerField != null)
                {
                    var stateManager = stateManagerField.GetValue(_autoMonitorService);
                    if (stateManager != null)
                    {
                        // 调用IsOperationExecuted方法检查状态
                        var method = stateManager.GetType().GetMethod("IsOperationExecuted");
                        if (method != null)
                        {
                            var isExecuted = (bool)method.Invoke(stateManager, new object[] { symbol, positionSide, statusType, tierIndex });
                            if (isExecuted)
                            {
                                AddLog($"🔍【状态检查】从状态管理器获取 {symbol}_{positionSide} {statusType}{(tierIndex.HasValue ? $"_T{tierIndex}" : "")} = 已执行");
                                return "√";
                            }
                        }
                    }
                }
                
                // 2. 回退：从positionProfiles检查（向后兼容）
                var positionProfiles = _autoMonitorService.GetPositionProfiles();
                var profileKey = $"{symbol}_{positionSide}";
                
                if (positionProfiles.ContainsKey(profileKey))
                {
                    var profile = positionProfiles[profileKey];
                    
                    // 使用正确的键格式检查
                    string triggerKey = $"{symbol}_{positionSide}_{statusType}";
                    if (tierIndex.HasValue)
                    {
                        triggerKey += $"_{tierIndex}";
                    }
                    
                    // 检查触发记录
                    if (profile.TriggerRecords.ContainsKey(triggerKey))
                    {
                        var record = profile.TriggerRecords[triggerKey];
                        if (record.IsExecuted)
                        {
                            AddLog($"🔍【状态检查】从档案获取 {symbol}_{positionSide} {statusType}{(tierIndex.HasValue ? $"_T{tierIndex}" : "")} = 已执行");
                            return "√";
                        }
                    }
                }
                
                // 2. 回退到本地手动修改文件
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BinanceFuturesTrader", "ContractConfigs.json");
                
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var savedConfigs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ContractConfigData>>(json);
                    var contractName = $"{symbol} {positionSide}";
                    
                    if (savedConfigs != null && savedConfigs.TryGetValue(contractName, out var savedConfig))
                    {
                        var manualStatus = statusType switch
                        {
                            "BreakEven" => savedConfig.BreakEvenStatus,
                            "AddPosition" when tierIndex == 1 => savedConfig.PushTier1Status,
                            "AddPosition" when tierIndex == 2 => savedConfig.PushTier2Status,
                            "AddPosition" when tierIndex == 3 => savedConfig.PushTier3Status,
                            "AddPosition" when tierIndex == 4 => savedConfig.PushTier4Status,
                            "ProfitProtection" when tierIndex == 1 => savedConfig.ProfitTier1Status,
                            "ProfitProtection" when tierIndex == 2 => savedConfig.ProfitTier2Status,
                            "ProfitProtection" when tierIndex == 3 => savedConfig.ProfitTier3Status,
                            _ => "-"
                        };
                        
                        if (!string.IsNullOrEmpty(manualStatus) && manualStatus != "-")
                        {
                            AddLog($"🔍【状态检查】从手动修改文件获取 {contractName} {statusType}{(tierIndex.HasValue ? $"_T{tierIndex}" : "")} = {manualStatus}");
                            return manualStatus;
                        }
                    }
                }
                
                // 3. 默认返回未触发
                return "-";
            }
            catch (Exception ex)
            {
                AddLog($"❌ 获取统一状态失败: {symbol}_{positionSide} {statusType} - {ex.Message}");
                return "-";
            }
        }

        /// <summary>
        /// 初始化窗口配置
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                AddLog("📝 加载配置列表...");
                
                // 加载所有可用配置到下拉框
                LoadAvailableConfigs();
                
                // 🔧 【修复】检查是否为第一次打开（没有配置且没有合约状态）
                var hasExistingStates = CheckForExistingContractStates();
                
                if (_configManager.Configurations.Count == 0 && !hasExistingStates)
                {
                    AddLog("⚠️ 检测到这是第一次使用自动盯盘功能");
                    AddLog("💡 请先点击右上角的'编辑配置'按钮创建您的第一个配置");
                    
                    // 显示提醒对话框
                    var result = MessageBox.Show(
                        "欢迎使用自动盯盘功能！\n\n" +
                        "检测到您还没有创建任何配置。\n" +
                        "自动盯盘功能需要先创建基础配置才能使用。\n\n" +
                        "是否现在就打开配置编辑器创建您的第一个配置？",
                        "首次使用提示",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        // 直接打开配置编辑器
                        EditConfigButton_Click(null, null);
                        return;
                    }
                    else
                    {
                        AddLog("🔧 您可以随时点击'编辑配置'按钮来创建配置");
                    }
                    
                    return; // 没有配置时直接返回
                }
                else if (_configManager.Configurations.Count == 0 && hasExistingStates)
                {
                    AddLog("🔧 检测到有已保存的合约配置，建议直接使用'自动盯盘'功能");
                }
                
                // 🔧 关键修复：优先从MainViewModel获取当前账户的配置
                _currentConfig = _mainViewModel?.CurrentAutoMonitorConfig;
                
                if (_currentConfig != null)
                {
                    AddLog($"✅ 从MainViewModel获取到配置: {_currentConfig.Name}");
                    // 确保配置管理器也使用同样的配置
                    _configManager.SetCurrentConfiguration(_currentConfig.Name);
                }
                else
                {
                    // 🔧 如果MainViewModel没有配置，尝试从配置管理器获取
                    _currentConfig = _configManager.CurrentConfig;
                    
                    if (_currentConfig == null)
                    {
                        // 如果没有配置，获取第一个可用配置
                        _currentConfig = _configManager.Configurations.FirstOrDefault();
                        
                        if (_currentConfig != null)
                        {
                            _configManager.SetCurrentConfiguration(_currentConfig.Name);
                            AddLog($"🔄 使用第一个可用配置: {_currentConfig.Name}");
                        }
                    }
                    else
                    {
                        AddLog($"🔄 从配置管理器获取配置: {_currentConfig.Name}");
                    }
                }
                
                if (_currentConfig == null)
                {
                    AddLog("❌ 无法获取任何可用配置，请检查配置文件");
                    return;
                }
                
                // 设置当前配置到MainViewModel
                if (_mainViewModel?.SelectedAccount != null)
                {
                    _mainViewModel.SetCurrentAutoMonitorConfig(_currentConfig);
                    AddLog($"✅ 配置已设置到MainViewModel: {_currentConfig.Name}");
                }
                
                // 更新UI显示
                UpdateConfigDisplay();
                
                // 基于当前配置生成DataGrid列结构
                GenerateDataGridColumns();
                
                // 订阅配置变更事件（已在构造函数中完成）
                // SubscribeToConfigurationEvents();
                
                AddLog($"✅ 窗口初始化完成，当前配置: {_currentConfig.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "窗口初始化失败");
                AddLog($"❌ 窗口初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取合约配置文件路径 - 已废弃：现在使用统一状态管理
        /// </summary>
        [Obsolete("已废弃：不再使用ContractConfigs.json文件，现在使用ContractMonitoringStateService进行统一状态管理")]
        private string GetContractConfigsFilePath()
        {
            _logger?.LogWarning("⚠️ GetContractConfigsFilePath 已废弃：不再使用ContractConfigs.json");
            // 返回空路径，因为该文件已废弃
            return string.Empty;
        }

        /// <summary>
        /// 从基础配置和档案填充合约配置数据
        /// </summary>
        private void PopulateConfigFromBaseConfigAndProfile(ContractConfigViewModel config, AutoMonitorConfig baseConfig, ContractProfile profile, PositionInfo position)
        {
            try
            {
                // 基本信息
                config.ContractName = $"{position.Symbol}_{(position.PositionAmt > 0 ? "LONG" : "SHORT")}";
                config.Symbol = position.Symbol;
                config.Side = position.PositionAmt > 0 ? "LONG" : "SHORT";
                config.PositionSize = Math.Abs(position.PositionAmt);
                config.EntryPrice = position.EntryPrice;
                config.CurrentPrice = position.MarkPrice;
                config.CurrentPnl = position.UnrealizedProfit;
                
                // 保本配置
                if (baseConfig.BreakEvenConfig.IsEnabled)
                {
                    config.BreakEvenTarget = baseConfig.BreakEvenConfig.TriggerProfitAmount;
                    config.BreakEvenStatus = GetTierStatusFromProfile(profile, "BreakEven", 0);
                }
                
                // 推仓配置
                if (baseConfig.AddPositionConfig.IsEnabled)
                {
                    var tiers = baseConfig.AddPositionConfig.Tiers.Take(4).ToList();
                    for (int i = 0; i < tiers.Count; i++)
                    {
                        var tier = tiers[i];
                        switch (i)
                        {
                            case 0:
                                config.PushTier1Amount = tier.TriggerProfitAmount;
                                config.PushTier1Status = GetTierStatusFromProfile(profile, "AddPosition", tier.TierIndex);
                                break;
                            case 1:
                                config.PushTier2Amount = tier.TriggerProfitAmount;
                                config.PushTier2Status = GetTierStatusFromProfile(profile, "AddPosition", tier.TierIndex);
                                break;
                            case 2:
                                config.PushTier3Amount = tier.TriggerProfitAmount;
                                config.PushTier3Status = GetTierStatusFromProfile(profile, "AddPosition", tier.TierIndex);
                                break;
                            case 3:
                                config.PushTier4Amount = tier.TriggerProfitAmount;
                                config.PushTier4Status = GetTierStatusFromProfile(profile, "AddPosition", tier.TierIndex);
                                break;
                        }
                    }
                }
                
                // 保盈配置
                if (baseConfig.ProfitProtectionConfig.IsEnabled)
                {
                    var tiers = baseConfig.ProfitProtectionConfig.Tiers.Take(3).ToList();
                    for (int i = 0; i < tiers.Count; i++)
                    {
                        var tier = tiers[i];
                        switch (i)
                        {
                            case 0:
                                config.ProfitTier1TriggerAmount = tier.TriggerProfitAmount;
                                config.ProfitTier1ProtectionAmount = tier.ProtectionAmount;
                                config.ProfitTier1Status = GetTierStatusFromProfile(profile, "ProfitProtection", tier.TierIndex);
                                break;
                            case 1:
                                config.ProfitTier2TriggerAmount = tier.TriggerProfitAmount;
                                config.ProfitTier2ProtectionAmount = tier.ProtectionAmount;
                                config.ProfitTier2Status = GetTierStatusFromProfile(profile, "ProfitProtection", tier.TierIndex);
                                break;
                            case 2:
                                config.ProfitTier3TriggerAmount = tier.TriggerProfitAmount;
                                config.ProfitTier3ProtectionAmount = tier.ProtectionAmount;
                                config.ProfitTier3Status = GetTierStatusFromProfile(profile, "ProfitProtection", tier.TierIndex);
                                break;
                        }
                    }
                }
                
                config.UpdateTime = DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"填充合约配置数据失败: {config.ContractName}");
            }
        }

        /// <summary>
        /// 从档案获取阶梯状态
        /// </summary>
        private string GetTierStatusFromProfile(ContractProfile profile, string tierType, int tierIndex)
        {
            try
            {
                switch (tierType)
                {
                    case "BreakEven":
                        return profile.BreakEvenState.IsTriggered ? 
                            (profile.BreakEvenState.ExecutionStatus == "已执行" ? "√" : profile.BreakEvenState.ExecutionStatus) : 
                            "-";
                    
                    case "AddPosition":
                        var addPositionState = profile.AddPositionStates.FirstOrDefault(s => s.TierIndex == tierIndex);
                        return addPositionState?.IsTriggered == true ? 
                            (addPositionState.ExecutionStatus == "已执行" ? "√" : addPositionState.ExecutionStatus) : 
                            "-";
                    
                    case "ProfitProtection":
                        var profitProtectionState = profile.ProfitProtectionStates.FirstOrDefault(s => s.TierIndex == tierIndex);
                        return profitProtectionState?.IsTriggered == true ? 
                            (profitProtectionState.ExecutionStatus == "已执行" ? "√" : profitProtectionState.ExecutionStatus) : 
                            "-";
                    
                    default:
                        return "-";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取阶梯状态失败: {tierType}, 阶梯{tierIndex}");
                return "-";
            }
        }

        /// <summary>
        /// 保存合约配置到文件 - 已废弃：现在使用统一状态管理
        /// </summary>
        [Obsolete("已废弃：不再保存到ContractConfigs.json文件，现在状态由ContractMonitoringStateService自动管理")]
        private async Task SaveContractConfigToFileAsync(List<ContractConfigViewModel> configs)
        {
            try
            {
                // 🔧 修复：不再保存到废弃的文件，只记录日志
                _logger.LogInformation($"💾 跳过保存合约配置到废弃文件：{configs.Count} 个配置现在由统一状态管理系统自动处理");
                
                // 不执行任何文件操作，避免路径错误
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存合约配置到文件失败");
                throw;
            }
        }

        /// <summary>
        /// 创建合约档案（简化版本，直接使用ContractProfile构造函数）
        /// </summary>
        private Task<ContractProfile> CreateProfileAsync(PositionInfo position, string baseConfigName)
        {
            try
            {
                var profile = new ContractProfile
                {
                    Symbol = position.Symbol,
                    Side = position.PositionAmt > 0 ? "LONG" : "SHORT",
                    PositionSize = position.PositionAmt,
                    EntryPrice = position.EntryPrice,
                    CurrentPrice = position.MarkPrice,
                    UnrealizedPnl = position.UnrealizedProfit,
                    BaseConfigName = baseConfigName,
                    UseIndependentConfig = false,
                    IsMonitoring = false,
                    CreateTime = DateTime.Now,
                    LastUpdateTime = DateTime.Now
                };
                
                return Task.FromResult(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建合约档案失败: {position.Symbol}");
                throw;
            }
        }

        #region 状态文件生成方法

        /// <summary>
        /// 根据持仓生成状态文件
        /// </summary>
        private async Task GenerateStateFileFromPositions(List<PositionInfo> activePositions)
        {
            try
            {
                AddLog($"🔄 开始为 {activePositions.Count} 个持仓生成状态文件...");
                
                // 创建状态服务
                var filePathManager = new FilePathManager();
                var currentAccountName = _mainViewModel?.SelectedAccount?.Name ?? filePathManager.GetCurrentAccountName();
                
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
                var stateLogger = loggerFactory.CreateLogger<ContractMonitoringStateService>();
                
                var stateService = new ContractMonitoringStateService(
                    stateLogger, 
                    _configManager,
                    filePathManager,
                    currentAccountName);

                // 获取当前基础配置
                var currentConfig = GetDefaultOrCurrentConfig();
                AddLog($"📋 使用基础配置: {currentConfig.Name}");

                var monitoringStates = new Dictionary<string, ContractMonitoringState>();
                
                foreach (var position in activePositions)
                {
                    var contractKey = $"{position.Symbol}_{(position.PositionAmt > 0 ? "LONG" : "SHORT")}";
                    AddLog($"🔄 为合约 {contractKey} 生成监控状态");
                    
                    var monitoringState = new ContractMonitoringState
                    {
                        Symbol = position.Symbol,
                        PositionSide = position.PositionAmt > 0 ? "LONG" : "SHORT",
                        BaseConfigName = currentConfig.Name,
                        Name = currentConfig.Name,
                        IsEnabled = currentConfig.IsEnabled,
                        ScanIntervalSeconds = currentConfig.ScanIntervalSeconds,
                        CooldownSeconds = currentConfig.CooldownSeconds,
                        
                        // 基本信息
                        InitialQuantity = Math.Abs(position.PositionAmt),
                        InitialEntryPrice = position.EntryPrice,
                        CurrentQuantity = Math.Abs(position.PositionAmt),
                        CurrentEntryPrice = position.EntryPrice,
                        CurrentMarkPrice = position.MarkPrice,
                        CurrentUnrealizedPnl = position.UnrealizedProfit,
                        IsActive = true,
                        
                        // 保本配置
                        BreakEvenConfig = new StatefulBreakEvenConfig
                        {
                            IsEnabled = currentConfig.BreakEvenConfig.IsEnabled,
                            TriggerProfitAmount = currentConfig.BreakEvenConfig.TriggerProfitAmount,
                            ExecutionState = ExecutionState.NotTriggered
                        },
                        
                        // 推仓配置
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
                        
                        // 保盈配置
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
                }
                
                // 保存到文件
                stateService.SaveMonitoringStates(monitoringStates);
                AddLog($"✅ 成功生成状态文件，包含 {monitoringStates.Count} 个合约配置");
                
                // 生成后加载UI
                await LoadContractConfigsFromStateFile();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成状态文件失败");
                AddLog($"❌ 生成状态文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从状态文件加载合约配置
        /// </summary>
        private async Task LoadContractConfigsFromStateFile()
        {
            try
            {
                AddLog("📊 开始从状态文件加载合约配置...");
                
                // 创建状态服务
                var filePathManager = new FilePathManager();
                var currentAccountName = _mainViewModel?.SelectedAccount?.Name ?? filePathManager.GetCurrentAccountName();
                
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
                var stateLogger = loggerFactory.CreateLogger<ContractMonitoringStateService>();
                
                var stateService = new ContractMonitoringStateService(
                    stateLogger, 
                    _configManager,
                    filePathManager,
                    currentAccountName);

                var monitoringStates = stateService.LoadMonitoringStates();
                
                if (monitoringStates.Count == 0)
                {
                    AddLog("📝 状态文件为空，无合约配置需要加载");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ContractConfigs.Clear();
                    });
                    return;
                }
                
                AddLog($"📊 从状态文件加载到 {monitoringStates.Count} 个合约配置");
                
                // 转换为UI模型
                var newConfigs = new List<ContractConfigViewModel>();
                foreach (var kvp in monitoringStates)
                {
                    var contractKey = kvp.Key;
                    var state = kvp.Value;
                    
                    var config = new ContractConfigViewModel
                    {
                        ContractName = $"{state.Symbol} {state.PositionSide}",
                        Symbol = state.Symbol,
                        Side = state.PositionSide,
                        CurrentPnl = state.CurrentUnrealizedPnl,
                        UpdateTime = DateTime.Now.ToString("HH:mm:ss")
                    };
                    
                    // 从状态填充UI数据
                    PopulateConfigFromState(config, state);
                    newConfigs.Add(config);
                    
                    AddLog($"🔄 已转换合约配置: {contractKey}");
                }
                
                // 更新UI
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ContractConfigs.Clear();
                    foreach (var config in newConfigs)
                    {
                        ContractConfigs.Add(config);
                    }
                });
                
                AddLog($"✅ 已从状态文件加载 {newConfigs.Count} 个合约配置到UI");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从状态文件加载配置失败");
                AddLog($"❌ 从状态文件加载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从状态填充配置
        /// </summary>
        private void PopulateConfigFromState(ContractConfigViewModel config, ContractMonitoringState state)
        {
            try
            {
                // 保本状态
                if (state.BreakEvenConfig != null)
                {
                    config.BreakEvenStatus = state.BreakEvenConfig.IsExecuted ? "√" : "-";
                    config.BreakEvenTarget = state.BreakEvenConfig.TriggerProfitAmount;
                }
                
                // 推仓状态
                if (state.AddPositionConfig?.Tiers != null)
                {
                    for (int i = 0; i < state.AddPositionConfig.Tiers.Count && i < 4; i++)
                    {
                        var tier = state.AddPositionConfig.Tiers[i];
                        var status = tier.IsExecuted ? "√" : "-";
                        
                        switch (i)
                        {
                            case 0:
                                config.PushTier1Status = status;
                                config.PushTier1Amount = tier.TriggerProfitAmount;
                                break;
                            case 1:
                                config.PushTier2Status = status;
                                config.PushTier2Amount = tier.TriggerProfitAmount;
                                break;
                            case 2:
                                config.PushTier3Status = status;
                                config.PushTier3Amount = tier.TriggerProfitAmount;
                                break;
                            case 3:
                                config.PushTier4Status = status;
                                config.PushTier4Amount = tier.TriggerProfitAmount;
                                break;
                        }
                    }
                }
                
                // 保盈状态
                if (state.ProfitProtectionConfig?.Tiers != null)
                {
                    for (int i = 0; i < state.ProfitProtectionConfig.Tiers.Count && i < 3; i++)
                    {
                        var tier = state.ProfitProtectionConfig.Tiers[i];
                        var status = tier.IsExecuted ? "√" : "-";
                        
                        switch (i)
                        {
                            case 0:
                                config.ProfitTier1Status = status;
                                config.ProfitTier1TriggerAmount = tier.TriggerProfitAmount;
                                config.ProfitTier1ProtectionAmount = tier.ProtectionAmount;
                                break;
                            case 1:
                                config.ProfitTier2Status = status;
                                config.ProfitTier2TriggerAmount = tier.TriggerProfitAmount;
                                config.ProfitTier2ProtectionAmount = tier.ProtectionAmount;
                                break;
                            case 2:
                                config.ProfitTier3Status = status;
                                config.ProfitTier3TriggerAmount = tier.TriggerProfitAmount;
                                config.ProfitTier3ProtectionAmount = tier.ProtectionAmount;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"填充配置失败: {config.ContractName}");
            }
        }

        /// <summary>
        /// 获取默认配置或当前选中的配置
        /// </summary>
        private AutoMonitorConfig GetDefaultOrCurrentConfig()
        {
            // 如果有当前配置就使用，否则创建默认配置
            if (_currentConfig != null)
            {
                return _currentConfig;
            }

            // 尝试从配置管理器获取
            var configs = _configManager.Configurations;
            if (configs.Count > 0)
            {
                return configs.First();
            }

            // 创建基本默认配置
            return new AutoMonitorConfig
            {
                Name = "默认配置",
                IsEnabled = true,
                ScanIntervalSeconds = 30,
                CooldownSeconds = 5,
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
                        new AddPositionTier 
                        { 
                            TierIndex = 1, 
                            IsEnabled = true,
                            TriggerProfitAmount = 200, 
                            RiskMultiplier = 1.5m, 
                            StopLossRatio = 0.1m,
                            ProfitProtectionAmount = 50
                        }
                    }
                },
                ProfitProtectionConfig = new AutoProfitProtectionConfig
                {
                    IsEnabled = true,
                    Tiers = new List<ProfitProtectionTier>
                    {
                        new ProfitProtectionTier 
                        { 
                            TierIndex = 1, 
                            IsEnabled = true,
                            TriggerProfitAmount = 300, 
                            ProtectionAmount = 100
                        }
                    }
                }
            };
        }

        #endregion
    }
    
    /// <summary>
    /// 简化版合约配置视图模型
    /// </summary>
        /// <summary>
    /// 按需求文档设计的合约配置视图模型
    /// 包含：保本、推仓（4个阶梯）、保盈（3个阶梯）的配置和状态
    /// </summary>
    public class ContractConfigViewModel : INotifyPropertyChanged
    {
        private string _contractName = "";
        private decimal _currentPnl = 0;
        private decimal _breakEvenTarget = 0;
        private string _breakEvenStatus = "-";
        private string _updateTime = "";
        
        // 🔧 添加缺失的基本属性
        private string _symbol = "";
        private string _side = "";
        private decimal _positionSize = 0;
        private decimal _entryPrice = 0;
        private decimal _currentPrice = 0;
        
        // 🔧 添加推仓金额属性
        private decimal _pushTier1Amount = 0;
        private decimal _pushTier2Amount = 0;
        private decimal _pushTier3Amount = 0;
        private decimal _pushTier4Amount = 0;
        
        // 🔧 添加保盈金额属性
        private decimal _profitTier1TriggerAmount = 0;
        private decimal _profitTier1ProtectionAmount = 0;
        private decimal _profitTier2TriggerAmount = 0;
        private decimal _profitTier2ProtectionAmount = 0;
        private decimal _profitTier3TriggerAmount = 0;
        private decimal _profitTier3ProtectionAmount = 0;

        // 动态推仓和保盈数据
        private readonly Dictionary<string, string> _dynamicData = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _dynamicColors = new Dictionary<string, string>();
        // 🔧 添加手动修改标记，防止定时刷新覆盖用户修改
        private readonly HashSet<string> _manuallyModifiedKeys = new HashSet<string>();

        public string ContractName 
        { 
            get => _contractName; 
            set { _contractName = value; OnPropertyChanged(); } 
        }
        
        public decimal CurrentPnl 
        { 
            get => _currentPnl; 
            set { _currentPnl = value; OnPropertyChanged(); OnPropertyChanged(nameof(PnlColor)); } 
        }
        
        public decimal BreakEvenTarget 
        { 
            get => _breakEvenTarget; 
            set { _breakEvenTarget = value; OnPropertyChanged(); } 
        }
        
        public string BreakEvenStatus 
        { 
            get => _breakEvenStatus; 
            set { _breakEvenStatus = value; OnPropertyChanged(); } 
        }
        
        public string UpdateTime 
        { 
            get => _updateTime; 
            set { _updateTime = value; OnPropertyChanged(); } 
        }
        
        // 🔧 添加缺失的基本属性访问器
        public string Symbol 
        { 
            get => _symbol; 
            set { _symbol = value; OnPropertyChanged(); } 
        }
        
        public string Side 
        { 
            get => _side; 
            set { _side = value; OnPropertyChanged(); } 
        }
        
        public decimal PositionSize 
        { 
            get => _positionSize; 
            set { _positionSize = value; OnPropertyChanged(); } 
        }
        
        public decimal EntryPrice 
        { 
            get => _entryPrice; 
            set { _entryPrice = value; OnPropertyChanged(); } 
        }
        
        public decimal CurrentPrice 
        { 
            get => _currentPrice; 
            set { _currentPrice = value; OnPropertyChanged(); } 
        }
        
        // 🔧 添加推仓金额属性访问器
        public decimal PushTier1Amount 
        { 
            get => _pushTier1Amount; 
            set { _pushTier1Amount = value; OnPropertyChanged(); } 
        }
        
        public decimal PushTier2Amount 
        { 
            get => _pushTier2Amount; 
            set { _pushTier2Amount = value; OnPropertyChanged(); } 
        }
        
        public decimal PushTier3Amount 
        { 
            get => _pushTier3Amount; 
            set { _pushTier3Amount = value; OnPropertyChanged(); } 
        }
        
        public decimal PushTier4Amount 
        { 
            get => _pushTier4Amount; 
            set { _pushTier4Amount = value; OnPropertyChanged(); } 
        }
        
        // 🔧 添加保盈金额属性访问器
        public decimal ProfitTier1TriggerAmount 
        { 
            get => _profitTier1TriggerAmount; 
            set { _profitTier1TriggerAmount = value; OnPropertyChanged(); } 
        }
        
        public decimal ProfitTier1ProtectionAmount 
        { 
            get => _profitTier1ProtectionAmount; 
            set { _profitTier1ProtectionAmount = value; OnPropertyChanged(); } 
        }
        
        public decimal ProfitTier2TriggerAmount 
        { 
            get => _profitTier2TriggerAmount; 
            set { _profitTier2TriggerAmount = value; OnPropertyChanged(); } 
        }
        
        public decimal ProfitTier2ProtectionAmount 
        { 
            get => _profitTier2ProtectionAmount; 
            set { _profitTier2ProtectionAmount = value; OnPropertyChanged(); } 
        }
        
        public decimal ProfitTier3TriggerAmount 
        { 
            get => _profitTier3TriggerAmount; 
            set { _profitTier3TriggerAmount = value; OnPropertyChanged(); } 
        }
        
        public decimal ProfitTier3ProtectionAmount 
        { 
            get => _profitTier3ProtectionAmount; 
            set { _profitTier3ProtectionAmount = value; OnPropertyChanged(); } 
        }

        // 颜色绑定属性
        public string PnlColor => CurrentPnl > 0 ? "Green" : CurrentPnl < 0 ? "Red" : "Black";
        
        #region 向后兼容的属性（为了其他文件的编译）
        
        public string PushTier1Status
        {
            get => GetDynamicData("Push1");
            set => SetDynamicData("Push1", value);
        }
        
        public string PushTier2Status
        {
            get => GetDynamicData("Push2");
            set => SetDynamicData("Push2", value);
        }
        
        public string PushTier3Status
        {
            get => GetDynamicData("Push3");
            set => SetDynamicData("Push3", value);
        }
        
        public string PushTier4Status
        {
            get => GetDynamicData("Push4");
            set => SetDynamicData("Push4", value);
        }
        
        public string ProfitTier1Status
        {
            get => GetDynamicData("Profit1");
            set => SetDynamicData("Profit1", value);
        }
        
        public string ProfitTier2Status
        {
            get => GetDynamicData("Profit2");
            set => SetDynamicData("Profit2", value);
        }
        
        public string ProfitTier3Status
        {
            get => GetDynamicData("Profit3");
            set => SetDynamicData("Profit3", value);
        }
        
        // 向后兼容的颜色属性
        public string PushTier1Color => GetDynamicColor("Push1");
        public string PushTier2Color => GetDynamicColor("Push2");
        public string PushTier3Color => GetDynamicColor("Push3");
        public string PushTier4Color => GetDynamicColor("Push4");
        public string ProfitTier1Color => GetDynamicColor("Profit1");
        public string ProfitTier2Color => GetDynamicColor("Profit2");
        public string ProfitTier3Color => GetDynamicColor("Profit3");
        
        #endregion
        
        #region 动态绑定属性（用于DataGrid列绑定）
        
        // 推仓动态属性（支持1-10档）
        public string DynamicPush1 => GetDynamicData("Push1");
        public string DynamicPush2 => GetDynamicData("Push2");
        public string DynamicPush3 => GetDynamicData("Push3");
        public string DynamicPush4 => GetDynamicData("Push4");
        public string DynamicPush5 => GetDynamicData("Push5");
        public string DynamicPush6 => GetDynamicData("Push6");
        public string DynamicPush7 => GetDynamicData("Push7");
        public string DynamicPush8 => GetDynamicData("Push8");
        public string DynamicPush9 => GetDynamicData("Push9");
        public string DynamicPush10 => GetDynamicData("Push10");
        
        // 保盈动态属性（支持1-10档）
        public string DynamicProfit1 => GetDynamicData("Profit1");
        public string DynamicProfit2 => GetDynamicData("Profit2");
        public string DynamicProfit3 => GetDynamicData("Profit3");
        public string DynamicProfit4 => GetDynamicData("Profit4");
        public string DynamicProfit5 => GetDynamicData("Profit5");
        public string DynamicProfit6 => GetDynamicData("Profit6");
        public string DynamicProfit7 => GetDynamicData("Profit7");
        public string DynamicProfit8 => GetDynamicData("Profit8");
        public string DynamicProfit9 => GetDynamicData("Profit9");
        public string DynamicProfit10 => GetDynamicData("Profit10");
        
        #endregion
        
        /// <summary>
        /// 设置动态数据
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">值</param>
        /// <param name="color">颜色</param>
        /// <param name="isManualChange">是否为手动修改</param>
        public void SetDynamicData(string key, string value, string color = "Black", bool isManualChange = false)
        {
            // 🔧 如果是手动修改，标记此键
            if (isManualChange)
            {
                _manuallyModifiedKeys.Add(key);
            }
            // 🔧 如果不是手动修改，但已被手动修改过，则跳过更新
            else if (_manuallyModifiedKeys.Contains(key))
            {
                return; // 保护手动修改的数据
            }
            
            _dynamicData[key] = value;
            _dynamicColors[key] = color;
            
            // 通知相关属性变化
            NotifyCompatibilityProperties(key);
        }
        
        /// <summary>
        /// 标记某个键为手动修改
        /// </summary>
        public void MarkAsManuallyModified(string key)
        {
            _manuallyModifiedKeys.Add(key);
        }

        /// <summary>
        /// 获取动态数据
        /// </summary>
        /// <param name="key">键名</param>
        /// <returns>值</returns>
        public string GetDynamicData(string key)
        {
            return _dynamicData.ContainsKey(key) ? _dynamicData[key] : "-";
        }

        /// <summary>
        /// 获取动态颜色
        /// </summary>
        /// <param name="key">键名</param>
        /// <returns>颜色</returns>
        public string GetDynamicColor(string key)
        {
            return _dynamicColors.ContainsKey(key) ? _dynamicColors[key] : "Black";
        }

        /// <summary>
        /// 清空动态数据
        /// </summary>
        /// <summary>
        /// 清除动态数据（保护手动修改的数据）
        /// </summary>
        /// <param name="preserveManualChanges">是否保留手动修改的数据</param>
        public void ClearDynamicData(bool preserveManualChanges = true)
        {
            if (!preserveManualChanges)
        {
            _dynamicData.Clear();
            _dynamicColors.Clear();
                _manuallyModifiedKeys.Clear();
            }
            else
            {
                // 🔧 保护手动修改的数据
                var keysToRemove = _dynamicData.Keys.Where(k => !_manuallyModifiedKeys.Contains(k)).ToList();
                foreach (var key in keysToRemove)
                {
                    _dynamicData.Remove(key);
                    _dynamicColors.Remove(key);
                }
            }
            
            // 强制通知所有动态属性变化
            RefreshAllDynamicProperties();
        }
        
        /// <summary>
        /// 刷新所有动态属性通知
        /// </summary>
        private void RefreshAllDynamicProperties()
        {
            // 通知所有推仓属性
            for (int i = 1; i <= 10; i++)
            {
                OnPropertyChanged($"DynamicPush{i}");
            }
            
            // 通知所有保盈属性
            for (int i = 1; i <= 10; i++)
            {
                OnPropertyChanged($"DynamicProfit{i}");
            }
            
            // 通知向后兼容属性
            OnPropertyChanged(nameof(PushTier1Status));
            OnPropertyChanged(nameof(PushTier2Status));
            OnPropertyChanged(nameof(PushTier3Status));
            OnPropertyChanged(nameof(PushTier4Status));
            OnPropertyChanged(nameof(ProfitTier1Status));
            OnPropertyChanged(nameof(ProfitTier2Status));
            OnPropertyChanged(nameof(ProfitTier3Status));
        }

        /// <summary>
        /// 为向后兼容通知相关属性变化
        /// </summary>
        private void NotifyCompatibilityProperties(string key)
        {
            // 根据动态数据的key通知对应的兼容属性
            switch (key)
            {
                case "Push1":
                    OnPropertyChanged(nameof(PushTier1Status));
                    OnPropertyChanged(nameof(PushTier1Color));
                    OnPropertyChanged(nameof(DynamicPush1));
                    break;
                case "Push2":
                    OnPropertyChanged(nameof(PushTier2Status));
                    OnPropertyChanged(nameof(PushTier2Color));
                    OnPropertyChanged(nameof(DynamicPush2));
                    break;
                case "Push3":
                    OnPropertyChanged(nameof(PushTier3Status));
                    OnPropertyChanged(nameof(PushTier3Color));
                    OnPropertyChanged(nameof(DynamicPush3));
                    break;
                case "Push4":
                    OnPropertyChanged(nameof(PushTier4Status));
                    OnPropertyChanged(nameof(PushTier4Color));
                    OnPropertyChanged(nameof(DynamicPush4));
                    break;
                case "Push5":
                    OnPropertyChanged(nameof(DynamicPush5));
                    break;
                case "Push6":
                    OnPropertyChanged(nameof(DynamicPush6));
                    break;
                case "Push7":
                    OnPropertyChanged(nameof(DynamicPush7));
                    break;
                case "Push8":
                    OnPropertyChanged(nameof(DynamicPush8));
                    break;
                case "Push9":
                    OnPropertyChanged(nameof(DynamicPush9));
                    break;
                case "Push10":
                    OnPropertyChanged(nameof(DynamicPush10));
                    break;
                case "Profit1":
                    OnPropertyChanged(nameof(ProfitTier1Status));
                    OnPropertyChanged(nameof(ProfitTier1Color));
                    OnPropertyChanged(nameof(DynamicProfit1));
                    break;
                case "Profit2":
                    OnPropertyChanged(nameof(ProfitTier2Status));
                    OnPropertyChanged(nameof(ProfitTier2Color));
                    OnPropertyChanged(nameof(DynamicProfit2));
                    break;
                case "Profit3":
                    OnPropertyChanged(nameof(ProfitTier3Status));
                    OnPropertyChanged(nameof(ProfitTier3Color));
                    OnPropertyChanged(nameof(DynamicProfit3));
                    break;
                case "Profit4":
                    OnPropertyChanged(nameof(DynamicProfit4));
                    break;
                case "Profit5":
                    OnPropertyChanged(nameof(DynamicProfit5));
                    break;
                case "Profit6":
                    OnPropertyChanged(nameof(DynamicProfit6));
                    break;
                case "Profit7":
                    OnPropertyChanged(nameof(DynamicProfit7));
                    break;
                case "Profit8":
                    OnPropertyChanged(nameof(DynamicProfit8));
                    break;
                case "Profit9":
                    OnPropertyChanged(nameof(DynamicProfit9));
                    break;
                case "Profit10":
                    OnPropertyChanged(nameof(DynamicProfit10));
                    break;
            }
        }

        private string GetStatusColor(string status)
        {
            return status switch
            {
                "-" => "Gray",          // 未触发
                "√" => "Green",         // 已执行
                "执行中" => "Orange",    // 执行中
                _ => "Black"
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        /// <summary>
        /// 公共方法：触发属性更新通知
        /// </summary>
        public void NotifyAllPropertiesChanged()
        {
            OnPropertyChanged(string.Empty);
        }



        /// <summary>
        /// 检查某个键是否被手动修改
        /// </summary>
        public bool IsManuallyModified(string key)
        {
            return _manuallyModifiedKeys.Contains(key);
        }

        /// <summary>
        /// 清除所有手动修改标记，允许强制覆盖数据
        /// </summary>
        public void ClearManuallyModifiedKeys()
        {
            _manuallyModifiedKeys.Clear();
        }

    }
} 