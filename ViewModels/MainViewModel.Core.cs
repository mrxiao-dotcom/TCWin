using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.Converters;
using BinanceFuturesTrader.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Input;

namespace BinanceFuturesTrader.ViewModels
{
    /// <summary>
    /// MainViewModel核心部分
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        #region 服务依赖
        private readonly IBinanceService _binanceService;
        private readonly ITradingCalculationService _calculationService;
        private readonly AccountConfigService _accountService;
        private readonly TradingSettingsService _tradingSettingsService;
        private readonly RecentContractsService _recentContractsService;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IServiceProvider _serviceProvider;
        private AutoMonitorService? _autoMonitorService;
        
        // 🔧 新增配置持久化服务
        private readonly AutoMonitorConfigPersistenceService _configPersistenceService;
        
        // 🔧 新增移动止损状态持久化服务
        private readonly TrailingStopPersistenceService _trailingStopPersistenceService;
        #endregion

        #region 定时器
        private readonly DispatcherTimer _priceTimer;
        private readonly DispatcherTimer _accountTimer;
        #endregion

        #region 基础属性
        private bool _isInitializing = true; // 避免初始化时保存设置

        [ObservableProperty]
        private ObservableCollection<AccountConfig> _accounts = new();

        [ObservableProperty]
        private AccountConfig? _selectedAccount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalWalletBalance))]
        [NotifyPropertyChangedFor(nameof(TotalMarginBalance))] 
        [NotifyPropertyChangedFor(nameof(TotalUnrealizedProfit))]
        [NotifyPropertyChangedFor(nameof(AvailableBalance))]
        [NotifyPropertyChangedFor(nameof(UnrealizedProfitColor))]
        private AccountInfo? _accountInfo;

        [ObservableProperty]
        private ObservableCollection<PositionInfo> _positions = new();

        [ObservableProperty]
        private ObservableCollection<OrderInfo> _orders = new();

        [ObservableProperty]
        private ObservableCollection<OrderInfo> _filteredOrders = new();

        // 减仓型订单集合（显示在上方委托单列表）
        [ObservableProperty]
        private ObservableCollection<OrderInfo> _reduceOnlyOrders = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedPosition))]
        private PositionInfo? _selectedPosition;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _statusMessage = "就绪";

        [ObservableProperty]
        private bool _autoRefreshEnabled = true;

        // 最近合约列表 - 最多保留10个
        [ObservableProperty]
        private ObservableCollection<string> _recentContracts = new();

        // 缺失的属性
        [ObservableProperty]
        private bool _trailingStopEnabled = false;

        [ObservableProperty]
        private OrderInfo? _selectedOrder;

        // 移动止损配置
        [ObservableProperty]
        private TrailingStopConfig _trailingStopConfig = new();

        // 移动止损状态监控
        [ObservableProperty]
        private ObservableCollection<TrailingStopStatus> _trailingStopStatuses = new();

        // 自动监控相关属性
        [ObservableProperty]
        private bool _isAutoMonitorRunning = false;

        [ObservableProperty]
        private string _autoMonitorButtonText = "自动盯盘";

        [ObservableProperty]
        private string _autoMonitorButtonColor = "#4A90E2";

        [ObservableProperty]
        private string _autoMonitorStatusMessage = "未启动";

        [ObservableProperty]
        private bool _isAutoMonitorButtonEnabled = true;

        // 每个账户独立的自动盯盘配置
        private readonly Dictionary<string, AutoMonitorConfig> _accountAutoMonitorConfigs = new();
        private AutoMonitorConfig? _currentAutoMonitorConfig;
        
        /// <summary>
        /// 获取当前自动监控配置
        /// </summary>
        public AutoMonitorConfig? CurrentAutoMonitorConfig => _currentAutoMonitorConfig;
        
        /// <summary>
        /// 设置当前自动监控配置
        /// </summary>
        /// <param name="config">配置对象</param>
        public void SetCurrentAutoMonitorConfig(AutoMonitorConfig? config)
        {
            _currentAutoMonitorConfig = config;
            OnPropertyChanged(nameof(CurrentAutoMonitorConfig));
            _logger?.LogInformation($"已设置当前自动监控配置: {config?.Name ?? "null"}");
        }
        
        /// <summary>
        /// 🔧 新增：更新账户的自动监控配置
        /// </summary>
        /// <param name="accountName">账户名称</param>
        /// <param name="config">配置对象</param>
        public void UpdateAccountAutoMonitorConfig(string accountName, AutoMonitorConfig config)
        {
            _accountAutoMonitorConfigs[accountName] = config;
            
            // 如果是当前选中的账户，也更新当前配置
            if (SelectedAccount?.Name == accountName)
            {
                _currentAutoMonitorConfig = config;
                OnPropertyChanged(nameof(CurrentAutoMonitorConfig));
            }
            
            _logger?.LogInformation($"已更新账户 '{accountName}' 的自动监控配置: {config.Name}");
        }
        
        /// <summary>
        /// 获取账户自动监控配置字典
        /// </summary>
        public IReadOnlyDictionary<string, AutoMonitorConfig> GetAccountAutoMonitorConfigs() 
            => _accountAutoMonitorConfigs;

        // 🔧 添加缺失的绑定属性
        [ObservableProperty]
        private string _autoSelectedPosition = "";

        [ObservableProperty]
        private string _autoCloseOrderInfo = "";
        #endregion

        #region 监控界面刷新事件
        public event EventHandler? AutoMonitorDashboardRefreshRequested;
        public event EventHandler<ConfigurationSyncEventArgs>? ConfigurationSyncRequested;

        /// <summary>
        /// 通知监控界面刷新数据
        /// </summary>
        protected void NotifyAutoMonitorDashboardRefresh()
        {
            try
            {
                AutoMonitorDashboardRefreshRequested?.Invoke(this, EventArgs.Empty);
                _logger.LogInformation("🔄 已通知监控界面刷新数据");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 通知监控界面刷新时发生错误");
            }
        }

        /// <summary>
        /// 通知配置同步管理器处理配置变化
        /// </summary>
        protected void NotifyConfigurationSyncManager(AutoMonitorConfig config)
        {
            try
            {
                if (config == null) return;
                
                _logger.LogInformation($"通知配置同步管理器处理配置变化：{config.Name}");
                
                // 计算推仓和止盈阶梯数
                var addPositionTiers = config.AddPositionConfig.IsEnabled ? 
                    config.AddPositionConfig.Tiers.Count : 0;
                var profitProtectionTiers = config.ProfitProtectionConfig.IsEnabled ? 
                    config.ProfitProtectionConfig.Tiers.Count : 0;
                
                // 触发配置同步事件
                ConfigurationSyncRequested?.Invoke(this, new ConfigurationSyncEventArgs
                {
                    AddPositionTierCount = addPositionTiers,
                    ProfitProtectionTierCount = profitProtectionTiers,
                    Config = config
                });
                
                _logger.LogInformation($"配置同步事件已触发：推仓{addPositionTiers}阶梯，止盈{profitProtectionTiers}阶梯");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通知配置同步管理器时发生异常");
            }
        }

        /// <summary>
        /// 恢复移动止损状态
        /// </summary>
        private async Task RecoverTrailingStopStatusAsync()
        {
            try
            {
                if (_trailingStopPersistenceService == null)
                {
                    _logger.LogWarning("移动止损持久化服务未初始化，跳过状态恢复");
                    return;
                }

                _logger.LogInformation("开始恢复移动止损状态...");
                var recoveredStatuses = await _trailingStopPersistenceService.RecoverTrailingStopStatusAsync();
                
                if (recoveredStatuses.Any())
                {
                    // 更新移动止损状态集合
                    TrailingStopStatuses.Clear();
                    foreach (var status in recoveredStatuses)
                    {
                        TrailingStopStatuses.Add(status);
                    }
                    
                    _logger.LogInformation($"✅ 移动止损状态恢复完成，恢复了 {recoveredStatuses.Count} 个状态");
                    StatusMessage = $"✅ 恢复了 {recoveredStatuses.Count} 个移动止损状态";
                }
                else
                {
                    _logger.LogInformation("没有需要恢复的移动止损状态");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "恢复移动止损状态失败");
                StatusMessage = $"恢复移动止损状态失败: {ex.Message}";
            }
        }
        #endregion

        #region 构造函数
        /// <summary>
        /// 依赖注入构造函数
        /// </summary>
        public MainViewModel(
            IBinanceService binanceService,
            ITradingCalculationService calculationService,
            AccountConfigService accountService,
            TradingSettingsService tradingSettingsService,
            RecentContractsService recentContractsService,
            ILogger<MainViewModel> logger,
            IServiceProvider serviceProvider,
            AutoMonitorConfigPersistenceService configPersistenceService)
        {
            _binanceService = binanceService;
            _calculationService = calculationService;
            _accountService = accountService;
            _tradingSettingsService = tradingSettingsService;
            _recentContractsService = recentContractsService;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configPersistenceService = configPersistenceService;
            
            // 🔧 新增移动止损状态持久化服务
            var trailingStopLogger = serviceProvider.GetService<ILogger<TrailingStopPersistenceService>>();
            _trailingStopPersistenceService = new TrailingStopPersistenceService(trailingStopLogger ?? logger as ILogger<TrailingStopPersistenceService>, binanceService);
            
            // 🔧 修改：适度优化定时器频率，保持实用性
            _priceTimer = new DispatcherTimer();
            _priceTimer.Interval = TimeSpan.FromSeconds(5); // 调整到5秒
            _priceTimer.Tick += PriceTimer_Tick;

            _accountTimer = new DispatcherTimer();
            _accountTimer.Interval = TimeSpan.FromSeconds(5); // 调整到5秒
            _accountTimer.Tick += AccountTimer_Tick;

            // 加载初始数据
            InitializeAsync();
        }

        /// <summary>
        /// 异步初始化
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("开始初始化MainViewModel");

                LoadAccounts();
                LoadTradingSettings();
                LoadRecentContracts();
                
                // 🔧 新增：加载自动盯盘配置
                LoadAutoMonitorConfigs();
                
                // 🔧 新增：恢复移动止损状态
                await RecoverTrailingStopStatusAsync();
                
                _isInitializing = false;
                
                // 🔧 新增：初始化完成后自动启动定时器，确保持仓数据能够实时刷新
                StartTimers();
                _logger.LogInformation("定时器已自动启动，确保5秒刷新频率");
                
                _logger.LogInformation("MainViewModel初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainViewModel初始化失败");
                StatusMessage = $"初始化失败: {ex.Message}";
            }
        }
        #endregion

        #region 账户财务计算属性
        // 使用TotalEquity作为账户权益，这个包含浮盈，类似币安APP的"预估总资产"
        public decimal TotalWalletBalance => AccountInfo?.TotalEquity ?? 0;

        public decimal TotalMarginBalance => AccountInfo?.ActualMarginUsed ?? 0;
        public decimal TotalUnrealizedProfit => AccountInfo?.TotalUnrealizedProfit ?? 0;
        public decimal AvailableBalance => AccountInfo?.AvailableBalance ?? 0;
        public string UnrealizedProfitColor => TotalUnrealizedProfit >= 0 ? "Green" : "Red";
        #endregion

        #region 选择状态属性
        public ObservableCollection<OrderInfo> SelectedOrders
        {
            get
            {
                var selected = new ObservableCollection<OrderInfo>();
                // 添加FilteredOrders中选中的订单
                foreach (var order in FilteredOrders.Where(o => o.IsSelected))
                {
                    selected.Add(order);
                }
                // 添加ReduceOnlyOrders中选中的订单
                foreach (var order in ReduceOnlyOrders.Where(o => o.IsSelected))
                {
                    selected.Add(order);
                }
                return selected;
            }
        }

        public bool HasSelectedOrders => FilteredOrders.Any(o => o.IsSelected) || ReduceOnlyOrders.Any(o => o.IsSelected);
        public int SelectedOrderCount => FilteredOrders.Count(o => o.IsSelected) + ReduceOnlyOrders.Count(o => o.IsSelected);
        public bool HasSelectedStopOrders => 
            FilteredOrders.Any(o => o.IsSelected && (o.Type == "STOP_MARKET" || o.Type == "TAKE_PROFIT_MARKET")) ||
            ReduceOnlyOrders.Any(o => o.IsSelected && (o.Type == "STOP_MARKET" || o.Type == "TAKE_PROFIT_MARKET"));
        public int SelectedStopOrderCount => 
            FilteredOrders.Count(o => o.IsSelected && (o.Type == "STOP_MARKET" || o.Type == "TAKE_PROFIT_MARKET")) +
            ReduceOnlyOrders.Count(o => o.IsSelected && (o.Type == "STOP_MARKET" || o.Type == "TAKE_PROFIT_MARKET"));

        public ObservableCollection<PositionInfo> SelectedPositions
        {
            get
            {
                var selected = new ObservableCollection<PositionInfo>();
                foreach (var position in Positions.Where(p => p.IsSelected))
                {
                    selected.Add(position);
                }
                return selected;
            }
        }

        public bool HasSelectedPositions => Positions.Any(p => p.IsSelected);
        public int SelectedPositionCount => Positions.Count(p => p.IsSelected);
        
        // 判断是否有选中的单个持仓（用于保本止损和保盈止损按钮）
        public bool HasSelectedPosition => SelectedPosition != null;

        // 🔧 新增：订单选择状态变化处理方法
        private void OnOrderSelectionChanged(object? sender, EventArgs e)
        {
            // 当任何订单的选择状态改变时，通知相关属性更新
            OnPropertyChanged(nameof(HasSelectedOrders));
            OnPropertyChanged(nameof(SelectedOrderCount));
            OnPropertyChanged(nameof(HasSelectedStopOrders));
            OnPropertyChanged(nameof(SelectedStopOrderCount));
            OnPropertyChanged(nameof(SelectedOrders));
            
            _logger.LogDebug($"订单选择状态变化，当前选中: {SelectedOrderCount} 个");
        }
        #endregion

        #region 测试方法
        /// <summary>
        /// 测试市值计算逻辑
        /// </summary>
        public void TestMarketValueCalculation()
        {
            if (AccountInfo != null)
            {
                AccountInfo.TestMarketValueCalculation();
            }
            else
            {
                Console.WriteLine("❌ AccountInfo为空，无法测试");
            }
        }

        /// <summary>
        /// 🔧 新增：测试GPSUSDT精度修复的命令
        /// </summary>
        public async Task TestGPSUSDTPrecisionAsync()
        {
            try
            {
                StatusMessage = "正在测试GPSUSDT精度修复...";
                IsLoading = true;
                
                var testResult = await _binanceService.TestGPSUSDTPrecisionAsync();
                
                // 使用MessageBox显示测试结果
                System.Windows.MessageBox.Show(
                    testResult,
                    "GPSUSDT精度修复验证结果",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                
                StatusMessage = "GPSUSDT精度测试完成";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GPSUSDT精度测试失败");
                StatusMessage = $"精度测试失败: {ex.Message}";
                
                System.Windows.MessageBox.Show(
                    $"测试失败: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        #endregion

        #region 数据加载方法
        private void LoadAccounts()
        {
            try
            {
                var accounts = _accountService.GetAllAccounts();
                Accounts.Clear();
                foreach (var account in accounts)
                {
                    Accounts.Add(account);
                }
                _logger.LogInformation($"加载了 {Accounts.Count} 个账户配置");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载账户配置失败");
            }
        }

        private void LoadTradingSettings()
        {
            try
            {
                var settings = _tradingSettingsService.LoadSettings();
                if (settings != null)
                {
                    // 应用设置到ViewModel属性
                    _logger.LogInformation("交易设置加载成功");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载交易设置失败");
            }
        }

        private void LoadRecentContracts()
        {
            try
            {
                var contracts = _recentContractsService.LoadRecentContracts();
                RecentContracts.Clear();
                foreach (var contract in contracts)
                {
                    RecentContracts.Add(contract);
                }
                _logger.LogInformation($"加载了 {RecentContracts.Count} 个最近合约");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载最近合约失败");
            }
        }

        public void SaveTradingSettings()
        {
            if (_isInitializing) return;

            try
            {
                // 创建设置对象，使用默认值，避免访问可能未初始化的属性
                var settings = new TradingSettings();
                _tradingSettingsService.SaveSettings(settings);
                _logger.LogDebug("交易设置已保存");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存交易设置失败");
            }
        }

        public void SaveRecentContracts()
        {
            try
            {
                _recentContractsService.SaveRecentContracts(RecentContracts);
                _logger.LogDebug("最近合约已保存");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存最近合约失败");
            }
        }

        /// <summary>
        /// 🔧 新增：加载自动盯盘配置
        /// </summary>
        private void LoadAutoMonitorConfigs()
        {
            try
            {
                _logger.LogCritical("🔍【初始加载】开始加载所有账户的自动盯盘配置");
                
                var configs = _configPersistenceService.LoadAccountConfigs();
                _accountAutoMonitorConfigs.Clear();
                
                _logger.LogCritical($"🔍【初始加载】从配置服务加载到 {configs.Count} 个账户配置");
                
                foreach (var kvp in configs)
                {
                    _accountAutoMonitorConfigs[kvp.Key] = kvp.Value;
                    _logger.LogCritical($"🔍【初始加载】账户配置: '{kvp.Key}' -> 配置名称: '{kvp.Value.Name}'");
                }
                
                _logger.LogCritical($"💾【初始加载】已加载 {configs.Count} 个账户的自动盯盘配置");
                
                // 🔧 关键修复：确保当前账户的配置被正确设置
                if (SelectedAccount != null)
                {
                    _logger.LogCritical($"🔍【初始加载】当前选中账户: '{SelectedAccount.Name}'");
                    
                    if (_accountAutoMonitorConfigs.TryGetValue(SelectedAccount.Name, out var currentConfig))
                    {
                        _currentAutoMonitorConfig = currentConfig;
                        _logger.LogCritical($"✅【初始加载】成功为当前账户 '{SelectedAccount.Name}' 加载配置: {currentConfig.Name}");
                        
                        // 🔧 强制通知配置变化，确保UI能获取到最新配置
                        OnPropertyChanged(nameof(CurrentAutoMonitorConfig));
                    }
                    else
                    {
                        _currentAutoMonitorConfig = null;
                        _logger.LogCritical($"⚠️【初始加载】账户 '{SelectedAccount.Name}' 没有找到保存的配置");
                        OnPropertyChanged(nameof(CurrentAutoMonitorConfig));
                    }
                }
                else
                {
                    _logger.LogCritical("⚠️【初始加载】当前没有选中的账户");
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "❌【初始加载】加载自动盯盘配置失败");
            }
        }
        #endregion

        #region 定时器事件处理
        private async void PriceTimer_Tick(object? sender, EventArgs e)
        {
            if (SelectedAccount == null || string.IsNullOrEmpty(Symbol))
                return;

            try
            {
                // 静默获取最新价格，不输出调试信息
                var newPrice = await _binanceService.GetLatestPriceAsync(Symbol);
                if (newPrice > 0)
                {
                    var oldPrice = LatestPrice;
                    LatestPrice = newPrice;
                    
                    // 只在价格有显著变化时（超过1%）才输出日志
                    if (Math.Abs(newPrice - oldPrice) > oldPrice * 0.01m) // 1% 变化
                    {
                        var formattedOldPrice = PriceFormatConverter.FormatPrice(oldPrice);
                        var formattedNewPrice = PriceFormatConverter.FormatPrice(newPrice);
                        _logger.LogDebug($"{Symbol} 价格大幅变化: {formattedOldPrice} → {formattedNewPrice}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "定时价格更新失败");
            }
        }

        private async void AccountTimer_Tick(object? sender, EventArgs e)
        {
            // 🔧 修改：如果没有启用自动刷新，直接返回
            if (!AutoRefreshEnabled)
            {
                return;
            }
            
            // 🔧 修改：如果没有选择账户，给出提示但不阻止定时器运行
            if (SelectedAccount == null)
            {
                // 只在第一次遇到这个情况时输出日志，避免重复日志
                _logger.LogDebug("定时器运行中，但未选择账户 - 请选择账户以开始数据刷新");
                return;
            }

            try
            {
                await RefreshAccountDataWithSelectionPreservation();
                _logger.LogDebug($"定时器自动刷新完成 - 下次刷新时间: {DateTime.Now.AddSeconds(5):HH:mm:ss}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "定时账户数据更新失败");
            }
        }
        #endregion

        #region 定时器控制
        private void StartTimers()
        {
            _priceTimer.Start();
            _accountTimer.Start();
            _logger.LogDebug("定时器已启动");
        }

        private void StopTimers()
        {
            _priceTimer.Stop();
            _accountTimer.Stop();
            _logger.LogDebug("定时器已停止");
        }

        public void Cleanup()
        {
            try
        {
            StopTimers();
                
                // 🔧 修复：移除所有订单的选择状态监听，避免内存泄漏
                foreach (var order in Orders)
                {
                    order.SelectionChanged -= OnOrderSelectionChanged;
                }
                foreach (var order in FilteredOrders)
                {
                    order.SelectionChanged -= OnOrderSelectionChanged;
                }
                foreach (var order in ReduceOnlyOrders)
                {
                    order.SelectionChanged -= OnOrderSelectionChanged;
                }
                
                // 🔧 新增：关闭所有相关的子窗口（监控窗口等）
                CloseAllChildWindows();
                
            _logger.LogInformation("MainViewModel清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainViewModel清理失败");
            }
        }

        /// <summary>
        /// 关闭所有子窗口
        /// </summary>
        private void CloseAllChildWindows()
        {
            try
            {
                if (Application.Current?.Windows != null)
                {
                    var childWindows = new List<Window>();
                    
                    // 收集需要关闭的子窗口
                    foreach (Window window in Application.Current.Windows)
                    {
                        // 跳过主窗口
                        if (window == Application.Current.MainWindow)
                            continue;
                            
                        // 关闭监控窗口和其他子窗口
                        if (window is Views.AutoMonitor.AutoMonitorDashboard_Refactored || 
                            window.Owner == Application.Current.MainWindow)
                        {
                            childWindows.Add(window);
                        }
                    }
                    
                    // 关闭收集到的子窗口
                    foreach (var window in childWindows)
                    {
                        try
                        {
                            _logger.LogInformation($"🔧 正在关闭子窗口: {window.Title}");
                            window.Close();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"⚠️ 关闭子窗口失败: {window.Title}");
                        }
                    }
                    
                    if (childWindows.Any())
                    {
                        _logger.LogInformation($"✅ 已关闭 {childWindows.Count} 个子窗口");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 关闭子窗口时发生错误");
            }
        }
        #endregion

        #region 缺失的命令
        /// <summary>
        /// 打开移动止损配置对话框命令
        /// </summary>
        public CommunityToolkit.Mvvm.Input.RelayCommand OpenTrailingStopConfigDialogCommand => new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteOpenTrailingStopConfigDialog);

        /// <summary>
        /// 价格转换到目标利润命令
        /// </summary>
        public CommunityToolkit.Mvvm.Input.RelayCommand ConvertPriceToTargetProfitCommand => new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteConvertPriceToTargetProfit);

        /// <summary>
        /// 执行打开移动止损配置对话框
        /// </summary>
        private void ExecuteOpenTrailingStopConfigDialog()
        {
            try
            {
                _logger.LogInformation("🔧 打开移动止损配置对话框");
                
                // 确保配置不为null
                if (TrailingStopConfig == null)
                {
                    TrailingStopConfig = new TrailingStopConfig();
                }

                var configWindow = new Views.TrailingStopConfigWindow(TrailingStopConfig)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                if (configWindow.ShowDialog() == true)
                {
                    TrailingStopConfig = configWindow.Config;
                    _logger.LogInformation($"✅ 移动止损配置已更新: 回调率 {TrailingStopConfig.CallbackRate:F1}%");
                    OnPropertyChanged(nameof(TrailingStopConfigInfo));
                    OnPropertyChanged(nameof(TrailingStopButtonTooltip));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 打开移动止损配置对话框失败");
                StatusMessage = $"配置失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 执行价格转换到目标利润计算
        /// </summary>
        private void ExecuteConvertPriceToTargetProfit()
        {
            try
            {
                if (SelectedPosition == null)
                {
                    StatusMessage = "请先选择持仓";
                    return;
                }

                var position = SelectedPosition;
                var currentPrice = position.MarkPrice;
                var entryPrice = position.EntryPrice;
                var quantity = Math.Abs(position.PositionAmt);
                
                // 计算当前浮盈
                var currentProfit = (currentPrice - entryPrice) * quantity * (position.PositionSideString == "LONG" ? 1 : -1);
                
                AutoSelectedPosition = $"{position.Symbol} {position.PositionSideString} {quantity:F4} 当前浮盈: {currentProfit:F2}U";
                
                _logger.LogInformation($"🔄 价格转换计算: {position.Symbol} 当前价格{currentPrice:F4} 入场价{entryPrice:F4} 浮盈{currentProfit:F2}U");
                StatusMessage = $"✅ 已计算 {position.Symbol} 当前浮盈: {currentProfit:F2}U";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 价格转换计算失败");
                StatusMessage = $"计算失败: {ex.Message}";
            }
        }
        #endregion
    }

    /// <summary>
    /// 配置同步事件参数
    /// </summary>
    public class ConfigurationSyncEventArgs : EventArgs
    {
        /// <summary>
        /// 推仓阶梯数
        /// </summary>
        public int AddPositionTierCount { get; set; }

        /// <summary>
        /// 止盈阶梯数
        /// </summary>
        public int ProfitProtectionTierCount { get; set; }

        /// <summary>
        /// 配置对象
        /// </summary>
        public AutoMonitorConfig Config { get; set; }
    }
} 