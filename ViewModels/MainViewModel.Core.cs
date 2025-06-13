using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

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
            IServiceProvider serviceProvider)
        {
            _binanceService = binanceService;
            _calculationService = calculationService;
            _accountService = accountService;
            _tradingSettingsService = tradingSettingsService;
            _recentContractsService = recentContractsService;
            _logger = logger;
            _serviceProvider = serviceProvider;
            
            // 初始化定时器
            _priceTimer = new DispatcherTimer();
            _priceTimer.Interval = TimeSpan.FromSeconds(2);
            _priceTimer.Tick += PriceTimer_Tick;

            _accountTimer = new DispatcherTimer();
            _accountTimer.Interval = TimeSpan.FromSeconds(5);
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
                
                _isInitializing = false;
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
            if (SelectedAccount == null || !AutoRefreshEnabled)
                return;

            try
            {
                await RefreshAccountDataWithSelectionPreservation();
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
                
                _logger.LogInformation("MainViewModel清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainViewModel清理失败");
            }
        }
        #endregion
    }
} 