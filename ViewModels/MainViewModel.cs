using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.Converters;
using BinanceFuturesTrader.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BinanceFuturesTrader.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AccountConfigService _accountService;
        private readonly BinanceService _binanceService;
        private readonly TradingSettingsService _tradingSettingsService;
        private readonly DispatcherTimer _priceTimer;
        private readonly DispatcherTimer _accountTimer;
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

        [ObservableProperty]
        private PositionInfo? _selectedPosition;

        [ObservableProperty]
        private string _symbol = "BTCUSDT";

        [ObservableProperty]
        private string _side = "BUY";

        [ObservableProperty]
        private string _positionSide = "BOTH";

        [ObservableProperty]
        private int _leverage = 3;

        [ObservableProperty]
        private decimal _quantity = 0;

        [ObservableProperty]
        private decimal _latestPrice = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLimitConditionalOrder))]
        [NotifyPropertyChangedFor(nameof(IsConditionalOrderVisible))]
        private string _orderType = "MARKET";

        [ObservableProperty]
        private string _marginType = "ISOLATED";

        [ObservableProperty]
        private decimal _stopLossRatio = 5;

        [ObservableProperty]
        private decimal _stopLossPrice = 0;

        [ObservableProperty]
        private decimal _stopLossAmount = 0;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _statusMessage = "就绪";

        [ObservableProperty]
        private bool _autoRefreshEnabled = true;

        // 条件单相关属性
        private ObservableCollection<ConditionalOrderInfo> _conditionalOrders = new();
        public ObservableCollection<ConditionalOrderInfo> ConditionalOrders
        {
            get => _conditionalOrders;
            set => SetProperty(ref _conditionalOrders, value);
        }

        public bool HasNoConditionalOrders => !ConditionalOrders.Any();

        private string _workingType = "CONTRACT_PRICE";
        public string WorkingType
        {
            get => _workingType;
            set => SetProperty(ref _workingType, value);
        }

        // 新增的条件单属性
        private string _conditionalType = "STOP";
        public string ConditionalType
        {
            get => _conditionalType;
            set => SetProperty(ref _conditionalType, value);
        }
        
        private string _timeInForce = "GTC";
        public string TimeInForce
        {
            get => _timeInForce;
            set => SetProperty(ref _timeInForce, value);
        }
        
        private decimal _stopPrice = 0;
        public decimal StopPrice
        {
            get => _stopPrice;
            set => SetProperty(ref _stopPrice, value);
        }
        
        private decimal _price = 0;
        public decimal Price
        {
            get => _price;
            set => SetProperty(ref _price, value);
        }
        
        private bool _reduceOnly = false;
        public bool ReduceOnly
        {
            get => _reduceOnly;
            set => SetProperty(ref _reduceOnly, value);
        }

        public bool IsLimitConditionalOrder
        {
            get
            {
                return OrderType == "STOP" || OrderType == "TAKE_PROFIT";
            }
        }

        // 条件单设置界面可见性
        public bool IsConditionalOrderVisible
        {
            get
            {
                return OrderType == "条件单";
            }
        }

        // 计算属性：是否可以下单
        public bool CanPlaceOrder
        {
            get
            {
                // 检查必要的数据是否已填写
                var canPlace = SelectedAccount != null &&
                              !string.IsNullOrWhiteSpace(Symbol) &&
                              LatestPrice > 0 &&
                              Quantity > 0 &&
                              !IsLoading;
                
                // 只在特定情况下更新状态提示，避免干扰其他功能
                if (!canPlace && StatusMessage == "就绪")
                {
                    if (SelectedAccount == null)
                    {
                        StatusMessage = "请选择交易账户";
                    }
                    else if (string.IsNullOrWhiteSpace(Symbol))
                    {
                        StatusMessage = "请输入合约名称（如：BTCUSDT）";
                    }
                    else if (LatestPrice <= 0)
                    {
                        StatusMessage = "正在获取最新价格...";
                    }
                    else if (Quantity <= 0)
                    {
                        StatusMessage = "请输入交易数量或使用'以损定量'计算";
                    }
                }
                
                return canPlace;
            }
        }

        // 选中订单相关属性
        public ObservableCollection<OrderInfo> SelectedOrders
        {
            get
            {
                var selected = new ObservableCollection<OrderInfo>();
                foreach (var order in FilteredOrders.Where(o => o.IsSelected))
                {
                    selected.Add(order);
                }
                return selected;
            }
        }

        public bool HasSelectedOrders => FilteredOrders.Any(o => o.IsSelected);
        
        public int SelectedOrderCount => FilteredOrders.Count(o => o.IsSelected);

        // 选中的止损单相关属性
        public bool HasSelectedStopOrders => FilteredOrders.Any(o => o.IsSelected && o.Type == "STOP_MARKET");
        
        public int SelectedStopOrderCount => FilteredOrders.Count(o => o.IsSelected && o.Type == "STOP_MARKET");

        // 选中持仓相关属性
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

        // 账户信息计算属性，用于UI绑定
        // 修复：使用totalMarginBalance作为账户权益，这个才是包含浮动盈亏的真正权益
        public decimal TotalWalletBalance => AccountInfo?.TotalMarginBalance ?? 0;
        
        // 修复：显示计算出的实际已用保证金，而不是API返回的保证金余额
        public decimal TotalMarginBalance => AccountInfo?.ActualMarginUsed ?? 0;
        public decimal TotalUnrealizedProfit => AccountInfo?.TotalUnrealizedProfit ?? 0;
        public decimal AvailableBalance => AccountInfo?.AvailableBalance ?? 0;
        
        // 浮动盈亏颜色
        public string UnrealizedProfitColor => TotalUnrealizedProfit >= 0 ? "Green" : "Red";

        // === 单选按钮绑定属性 ===
        
        // 交易方向单选按钮
        public bool IsBuySelected
        {
            get => Side == "BUY";
            set
            {
                if (value)
                {
                    Side = "BUY";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSellSelected));
                }
            }
        }

        public bool IsSellSelected
        {
            get => Side == "SELL";
            set
            {
                if (value)
                {
                    Side = "SELL";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsBuySelected));
                }
            }
        }

        // 订单类型单选按钮
        public bool IsMarketOrderSelected
        {
            get => OrderType == "MARKET";
            set
            {
                if (value)
                {
                    OrderType = "MARKET";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsLimitOrderSelected));
                }
            }
        }

        public bool IsLimitOrderSelected
        {
            get => OrderType == "LIMIT";
            set
            {
                if (value)
                {
                    OrderType = "LIMIT";
                    // 选择限价单时自动填入最新价格
                    if (LatestPrice > 0)
                    {
                        Price = LatestPrice;
                        Console.WriteLine($"💰 选择限价单，自动填入价格: {PriceFormatConverter.FormatPrice(Price)}");
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsMarketOrderSelected));
                }
            }
        }

        // 判断是否是限价单（用于UI绑定）
        public bool IsLimitOrder => OrderType == "LIMIT";

        // 保证金模式单选按钮
        public bool IsIsolatedMarginSelected
        {
            get => MarginType == "ISOLATED";
            set
            {
                if (value)
                {
                    MarginType = "ISOLATED";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCrossedMarginSelected));
                }
            }
        }

        public bool IsCrossedMarginSelected
        {
            get => MarginType == "CROSSED";
            set
            {
                if (value)
                {
                    MarginType = "CROSSED";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsIsolatedMarginSelected));
                }
            }
        }

        // 条件单类型单选按钮
        public bool IsStopSelected
        {
            get => ConditionalType == "STOP";
            set
            {
                if (value)
                {
                    ConditionalType = "STOP";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTakeProfitSelected));
                    OnPropertyChanged(nameof(IsStopMarketSelected));
                    OnPropertyChanged(nameof(IsTakeProfitMarketSelected));
                }
            }
        }

        public bool IsTakeProfitSelected
        {
            get => ConditionalType == "TAKE_PROFIT";
            set
            {
                if (value)
                {
                    ConditionalType = "TAKE_PROFIT";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsStopSelected));
                    OnPropertyChanged(nameof(IsStopMarketSelected));
                    OnPropertyChanged(nameof(IsTakeProfitMarketSelected));
                }
            }
        }

        public bool IsStopMarketSelected
        {
            get => ConditionalType == "STOP_MARKET";
            set
            {
                if (value)
                {
                    ConditionalType = "STOP_MARKET";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsStopSelected));
                    OnPropertyChanged(nameof(IsTakeProfitSelected));
                    OnPropertyChanged(nameof(IsTakeProfitMarketSelected));
                }
            }
        }

        public bool IsTakeProfitMarketSelected
        {
            get => ConditionalType == "TAKE_PROFIT_MARKET";
            set
            {
                if (value)
                {
                    ConditionalType = "TAKE_PROFIT_MARKET";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsStopSelected));
                    OnPropertyChanged(nameof(IsTakeProfitSelected));
                    OnPropertyChanged(nameof(IsStopMarketSelected));
                }
            }
        }

        // 触发方式单选按钮
        public bool IsContractPriceSelected
        {
            get => WorkingType == "CONTRACT_PRICE";
            set
            {
                if (value)
                {
                    WorkingType = "CONTRACT_PRICE";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsMarkPriceSelected));
                }
            }
        }

        public bool IsMarkPriceSelected
        {
            get => WorkingType == "MARK_PRICE";
            set
            {
                if (value)
                {
                    WorkingType = "MARK_PRICE";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsContractPriceSelected));
                }
            }
        }

        // 有效期单选按钮
        public bool IsGTCSelected
        {
            get => TimeInForce == "GTC";
            set
            {
                if (value)
                {
                    TimeInForce = "GTC";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsIOCSelected));
                    OnPropertyChanged(nameof(IsFOKSelected));
                }
            }
        }

        public bool IsIOCSelected
        {
            get => TimeInForce == "IOC";
            set
            {
                if (value)
                {
                    TimeInForce = "IOC";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGTCSelected));
                    OnPropertyChanged(nameof(IsFOKSelected));
                }
            }
        }

        public bool IsFOKSelected
        {
            get => TimeInForce == "FOK";
            set
            {
                if (value)
                {
                    TimeInForce = "FOK";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGTCSelected));
                    OnPropertyChanged(nameof(IsIOCSelected));
                }
            }
        }

        // 最近合约列表 - 最多保留10个
        [ObservableProperty]
        private ObservableCollection<string> _recentContracts = new();

        // 🚀 移动止损配置 - 智能版本
        [ObservableProperty]
        private bool _trailingStopEnabled = false;
        
        [ObservableProperty]
        private decimal _trailingStopCallbackRate = 1.0m; // 移动止损回调率，默认1.0%

        public MainViewModel()
        {
            _accountService = new AccountConfigService();
            _binanceService = new BinanceService();
            _tradingSettingsService = new TradingSettingsService();
            
            // 初始化定时器
            _priceTimer = new DispatcherTimer();
            _priceTimer.Interval = TimeSpan.FromSeconds(2);
            _priceTimer.Tick += PriceTimer_Tick;

            _accountTimer = new DispatcherTimer();
            _accountTimer.Interval = TimeSpan.FromSeconds(5);
            _accountTimer.Tick += AccountTimer_Tick;

            LoadAccounts();
            LoadTradingSettings();
            
            // 初始化时显示所有委托单
            FilterOrdersForPosition(); // 不传参数，显示所有委托单
        }

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
                        Console.WriteLine($"📊 {Symbol} 价格大幅变化: {formattedOldPrice} → {formattedNewPrice}");
                    }
                }
            }
            catch (Exception ex)
            {
                // 网络异常时不输出，避免刷屏
                // Console.WriteLine($"❌ 定时价格更新失败: {ex.Message}");
                // 不更新StatusMessage，避免干扰用户操作
            }
        }

        private async void AccountTimer_Tick(object? sender, EventArgs e)
        {
            if (SelectedAccount == null || !AutoRefreshEnabled)
                return;

            try
            {
                // 静默自动刷新，减少日志噪音
                
                // 保存当前选择状态
                var selectedOrderIds = new HashSet<long>();
                var selectedPositionSymbols = new HashSet<string>();
                
                foreach (var order in FilteredOrders.Where(o => o.IsSelected))
                {
                    selectedOrderIds.Add(order.OrderId);
                }
                
                foreach (var position in Positions.Where(p => p.IsSelected))
                {
                    var positionKey = $"{position.Symbol}_{position.PositionSideString}";
                    selectedPositionSymbols.Add(positionKey);
                }

                // 更新账户信息
                var accountInfo = await _binanceService.GetAccountInfoAsync();
                if (accountInfo != null)
                {
                    AccountInfo = accountInfo;
                }

                // 更新持仓信息
                var positions = await _binanceService.GetPositionsAsync();
                
                Positions.Clear();
                int restoredPositionCount = 0;
                foreach (var position in positions)
                {
                    // 恢复持仓选择状态
                    var positionKey = $"{position.Symbol}_{position.PositionSideString}";
                    if (selectedPositionSymbols.Contains(positionKey))
                    {
                        position.IsSelected = true;
                        restoredPositionCount++;
                    }
                    Positions.Add(position);
                }

                // 计算保证金占用
                if (AccountInfo != null)
                {
                    AccountInfo.CalculateMarginUsed(Positions);
                    OnPropertyChanged(nameof(AccountInfo.ActualMarginUsed));
                    // 强制通知已用保证金属性更新
                    OnPropertyChanged(nameof(TotalMarginBalance));
                    // 通知账户权益属性更新
                    OnPropertyChanged(nameof(TotalWalletBalance));
                }

                // 更新订单信息
                var orders = await _binanceService.GetOpenOrdersAsync();
                
                Orders.Clear();
                int restoredOrderCount = 0;
                foreach (var order in orders)
                {
                    // 恢复订单选择状态
                    if (selectedOrderIds.Contains(order.OrderId))
                    {
                        order.IsSelected = true;
                        restoredOrderCount++;
                    }
                    Orders.Add(order);
                }

                // 如果有选中的持仓，更新过滤的订单
                if (SelectedPosition != null)
                {
                    FilterOrdersForPosition(SelectedPosition.Symbol);
                    
                    // 恢复过滤订单的选择状态
                    int restoredFilteredOrderCount = 0;
                    foreach (var order in FilteredOrders)
                    {
                        if (selectedOrderIds.Contains(order.OrderId))
                        {
                            order.IsSelected = true;
                            restoredFilteredOrderCount++;
                        }
                    }
                }
                else
                {
                    // 没有选中持仓，显示所有委托单
                    FilterOrdersForPosition(); // 不传参数，显示所有委托单
                    
                    // 恢复所有订单的选择状态
                    int restoredFilteredOrderCount = 0;
                    foreach (var order in FilteredOrders)
                    {
                        if (selectedOrderIds.Contains(order.OrderId))
                        {
                            order.IsSelected = true;
                            restoredFilteredOrderCount++;
                        }
                    }
                }

                // 强制通知选择状态属性更新
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));
                OnPropertyChanged(nameof(SelectedPositions));
                OnPropertyChanged(nameof(HasSelectedPositions));
                OnPropertyChanged(nameof(SelectedPositionCount));
                OnPropertyChanged(nameof(HasSelectedStopOrders));
                OnPropertyChanged(nameof(SelectedStopOrderCount));

                // 🎯 移动止损检查
                if (TrailingStopEnabled && Positions.Any(p => Math.Abs(p.PositionAmt) > 0))
                {
                    await ProcessTrailingStopAsync();
                }

                StatusMessage = $"数据已更新 - {DateTime.Now:HH:mm:ss}";
                // 只在控制台输出简单的成功信息，不使用LogService
                // Console.WriteLine($"🔄 自动刷新完成 - {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"数据更新失败: {ex.Message}";
                LogService.LogError("❌ 自动刷新异常", ex);
            }
        }

        private void LoadAccounts()
        {
            var accounts = _accountService.GetAllAccounts();
            Accounts.Clear();
            foreach (var account in accounts)
            {
                Accounts.Add(account);
            }
        }

        private void LoadTradingSettings()
        {
            try
            {
                var settings = _tradingSettingsService.LoadSettings();
                
                // 应用设置到当前属性
                Symbol = settings.Symbol;
                Side = settings.Side;
                Leverage = settings.Leverage;
                MarginType = settings.MarginType;
                OrderType = settings.OrderType;
                StopLossRatio = settings.StopLossRatio;
                PositionSide = settings.PositionSide;
                
                StatusMessage = $"交易设置已加载 - {settings.LastSaved:yyyy-MM-dd HH:mm:ss}";
                Console.WriteLine("🔧 交易设置已应用到界面");
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载设置失败: {ex.Message}";
                Console.WriteLine($"❌ 加载交易设置异常: {ex.Message}");
            }
            finally
            {
                // 初始化完成，允许保存设置
                _isInitializing = false;
                
                // 手动触发UI属性通知，确保单选按钮正确显示默认状态
                Console.WriteLine($"🔧 触发UI属性通知，当前MarginType: {MarginType}");
                OnPropertyChanged(nameof(IsBuySelected));
                OnPropertyChanged(nameof(IsSellSelected));
                OnPropertyChanged(nameof(IsMarketOrderSelected));
                OnPropertyChanged(nameof(IsLimitOrderSelected));
                OnPropertyChanged(nameof(IsIsolatedMarginSelected));
                OnPropertyChanged(nameof(IsCrossedMarginSelected));
                OnPropertyChanged(nameof(IsContractPriceSelected));
                OnPropertyChanged(nameof(IsMarkPriceSelected));
                OnPropertyChanged(nameof(IsGTCSelected));
                OnPropertyChanged(nameof(IsIOCSelected));
                OnPropertyChanged(nameof(IsFOKSelected));
                OnPropertyChanged(nameof(IsStopSelected));
                OnPropertyChanged(nameof(IsTakeProfitSelected));
                OnPropertyChanged(nameof(IsStopMarketSelected));
                OnPropertyChanged(nameof(IsTakeProfitMarketSelected));
                
                Console.WriteLine($"✅ UI属性通知完成 - 逐仓模式选中状态: {IsIsolatedMarginSelected}");
            }
        }
        
        public void SaveTradingSettings()
        {
            try
            {
                var settings = new TradingSettings
                {
                    Symbol = Symbol,
                    Side = Side,
                    Leverage = Leverage,
                    MarginType = MarginType,
                    OrderType = OrderType,
                    StopLossRatio = StopLossRatio,
                    PositionSide = PositionSide,
                    LastSaved = DateTime.Now
                };
                
                _tradingSettingsService.SaveSettings(settings);
                Console.WriteLine("💾 交易设置已保存");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存交易设置异常: {ex.Message}");
            }
        }

        partial void OnSelectedAccountChanged(AccountConfig? value)
        {
            if (value != null)
            {
                _binanceService.SetAccount(value);
                
                // 启动定时器
                StartTimers();
                
                // 立即刷新一次数据
                _ = RefreshDataAsync();
            }
            else
            {
                // 停止定时器
                StopTimers();
                
                // 清空委托单显示
                FilteredOrders.Clear();
            }
            
            // 通知下单按钮状态更新
            OnPropertyChanged(nameof(CanPlaceOrder));
        }

        partial void OnSelectedPositionChanged(PositionInfo? value)
        {
            if (value != null)
            {
                // 选择了持仓，显示该合约的委托单
                FilterOrdersForPosition(value.Symbol);
                Symbol = value.Symbol;
                
                // 立即更新该合约的价格
                _ = UpdateLatestPriceAsync();
            }
            else
            {
                // 取消选择持仓，显示所有合约的委托单
                Console.WriteLine("🔍 取消选择持仓，显示所有委托单");
                FilterOrdersForPosition(); // 不传参数，显示所有委托单
            }
        }

        partial void OnSymbolChanged(string value)
        {
            // 自动补齐USDT后缀
            if (!string.IsNullOrWhiteSpace(value))
            {
                var upperValue = value.ToUpper().Trim();
                
                // 如果没有USDT后缀，自动添加
                if (!upperValue.EndsWith("USDT") && !upperValue.Contains("USDT"))
                {
                    // 检查是否是常见的币种符号
                    if (IsValidCoinSymbol(upperValue))
                    {
                        var newSymbol = upperValue + "USDT";
                        if (Symbol != newSymbol)
                        {
                            Symbol = newSymbol;
                            StatusMessage = $"已自动补齐为 {newSymbol}";
                            Console.WriteLine($"🔧 自动补齐合约名: {value} → {newSymbol}");
                            return; // 避免重复触发
                        }
                    }
                }
                else if (upperValue != value)
                {
                    // 统一转换为大写
                    Symbol = upperValue;
                    return; // 避免重复触发
                }
            }
            
            // 切换合约时，清空相关数量和止损设置，避免自动计算干扰用户操作
            if (!string.IsNullOrEmpty(value))
            {
                Console.WriteLine($"🔄 切换合约到 {value}，清空数量和止损设置");
                
                // 清空数量
                Quantity = 0;
                
                // 清空止损相关设置
                StopLossRatio = 0;
                StopLossPrice = 0;
                StopLossAmount = 0;
                
                Console.WriteLine("✅ 已清空数量和止损设置，用户可重新输入");
            }
            
            // 当合约名称改变时，立即更新价格
            if (SelectedAccount != null && !string.IsNullOrEmpty(value))
            {
                _ = UpdateLatestPriceAsync();
            }
            
            // 添加到最近合约列表
            if (!string.IsNullOrEmpty(value) && value.Contains("USDT"))
            {
                AddToRecentContracts(value);
            }
            
            // 通知下单按钮状态更新
            OnPropertyChanged(nameof(CanPlaceOrder));
        }

        private bool IsValidCoinSymbol(string symbol)
        {
            // 常见的币种符号列表
            var validSymbols = new[]
            {
                "BTC", "ETH", "BNB", "ADA", "DOT", "XRP", "LTC", "BCH", "LINK", "SOL",
                "DOGE", "MATIC", "AVAX", "UNI", "ATOM", "FIL", "TRX", "ETC", "THETA", "VET",
                "ICP", "FTT", "LUNA", "CRO", "NEAR", "ALGO", "MANA", "SAND", "AXS", "SHIB",
                "HBAR", "EGLD", "FLOW", "CAKE", "RUNE", "KSM", "XTZ", "WAVES", "COMP", "ZEC",
                "1INCH", "SUSHI", "SNX", "MKR", "AAVE", "GRT", "YFI", "CRV", "BAT", "ENJ"
            };
            
            return validSymbols.Contains(symbol) || symbol.Length >= 2;
        }

        partial void OnLatestPriceChanged(decimal value)
        {
            // 如果当前是限价单且有最新价格，自动更新价格输入框
            if (value > 0 && OrderType == "LIMIT")
            {
                Price = value;
                Console.WriteLine($"📊 最新价格更新，限价单价格自动更新为: {PriceFormatConverter.FormatPrice(Price)}");
            }
            
            // 当最新价格变化时，如果设置了止损比例，自动重新计算止损价
            if (value > 0 && StopLossRatio > 0)
            {
                CalculateStopLossPrice();
            }
            
            // 通知下单按钮状态更新
            OnPropertyChanged(nameof(CanPlaceOrder));
        }

        partial void OnQuantityChanged(decimal value)
        {
            // 数量变化时通知下单按钮状态更新
            OnPropertyChanged(nameof(CanPlaceOrder));
        }

        partial void OnIsLoadingChanged(bool value)
        {
            // 加载状态变化时通知下单按钮状态更新
            OnPropertyChanged(nameof(CanPlaceOrder));
        }

        private void StartTimers()
        {
            _priceTimer.Start();
            _accountTimer.Start();
            StatusMessage = "定时器已启动，开始自动更新数据...";
        }

        private void StopTimers()
        {
            _priceTimer.Stop();
            _accountTimer.Stop();
            StatusMessage = "定时器已停止";
        }

        private void FilterOrdersForPosition(string? symbol = null)
        {
            try
            {
                // 改进后的委托单过滤逻辑：
                // 1. 如果没有传入symbol参数（或为空），显示所有合约的委托单
                // 2. 如果传入了symbol，则只显示该合约的委托单
                
                if (string.IsNullOrEmpty(symbol))
                {
                    Console.WriteLine("🔍 显示所有合约的委托单");
                    
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            FilteredOrders.Clear();
                            
                            // 显示所有订单
                            foreach (var order in Orders)
                            {
                                if (order != null)
                                {
                                    FilteredOrders.Add(order);
                                }
                            }
                            
                            Console.WriteLine($"✅ 显示所有委托单: {FilteredOrders.Count} 个");
                        }
                        catch (Exception uiEx)
                        {
                            Console.WriteLine($"❌ UI集合操作异常: {uiEx.Message}");
                            try
                            {
                                FilteredOrders.Clear();
                            }
                            catch (Exception clearEx)
                            {
                                Console.WriteLine($"❌ 清空集合也失败: {clearEx.Message}");
                            }
                        }
                    });
                    return;
                }
                
                Console.WriteLine($"🔍 过滤显示合约 {symbol} 的委托单");

                if (Orders == null)
                {
                    Console.WriteLine($"❌ 订单列表为空");
                    return;
                }

                // 安全地创建过滤列表
                List<OrderInfo> filtered;
                try
                {
                    filtered = Orders.Where(o => o != null && o.Symbol == symbol).ToList();
                    Console.WriteLine($"📊 过滤结果: 找到 {filtered.Count} 个 {symbol} 的订单");
                }
                catch (Exception filterEx)
                {
                    Console.WriteLine($"❌ 订单过滤逻辑异常: {filterEx.Message}");
                    filtered = new List<OrderInfo>();
                }
                
                // 确保在UI线程上安全操作集合
                App.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        FilteredOrders.Clear();
                        
                        foreach (var order in filtered)
                        {
                            if (order != null)
                            {
                                FilteredOrders.Add(order);
                            }
                        }
                        
                        Console.WriteLine($"✅ UI更新完成: FilteredOrders现有 {FilteredOrders.Count} 个订单");
                    }
                    catch (Exception uiEx)
                    {
                        Console.WriteLine($"❌ UI集合操作异常: {uiEx.Message}");
                        // 尝试安全重置集合
                        try
                        {
                            FilteredOrders.Clear();
                        }
                        catch (Exception clearEx)
                        {
                            Console.WriteLine($"❌ 清空集合也失败: {clearEx.Message}");
                        }
                    }
                });
                
                Console.WriteLine($"🔍 订单过滤完成: {symbol}, 最终结果 {FilteredOrders.Count} 个订单");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 订单过滤顶层异常: {ex.Message}");
                Console.WriteLine($"❌ 异常类型: {ex.GetType().Name}");
                Console.WriteLine($"❌ 异常堆栈: {ex.StackTrace}");
                
                // 更新状态消息（安全方式）
                try
                {
                    StatusMessage = $"订单过滤失败: {ex.Message}";
                }
                catch (Exception statusEx)
                {
                    Console.WriteLine($"❌ 更新状态消息异常: {statusEx.Message}");
                }
                
                // 尝试安全清空过滤结果
                try
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            FilteredOrders.Clear();
                        }
                        catch (Exception clearEx)
                        {
                            Console.WriteLine($"❌ 异常恢复时清空集合失败: {clearEx.Message}");
                        }
                    });
                }
                catch (Exception dispatcherEx)
                {
                    Console.WriteLine($"❌ Dispatcher调用异常: {dispatcherEx.Message}");
                }
            }
        }

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            if (SelectedAccount == null)
                return;

            IsLoading = true;
            StatusMessage = "手动刷新数据中...";
            try
            {
                LogService.LogDebug("🔄 开始手动刷新数据...");
                
                // 保存当前选择状态
                var selectedOrderIds = new HashSet<long>();
                var selectedPositionSymbols = new HashSet<string>();
                
                LogService.LogDebug($"📊 当前FilteredOrders数量: {FilteredOrders.Count}, Positions数量: {Positions.Count}");
                
                foreach (var order in FilteredOrders.Where(o => o.IsSelected))
                {
                    selectedOrderIds.Add(order.OrderId);
                    LogService.LogDebug($"💾 保存订单选择: OrderId={order.OrderId}, Symbol={order.Symbol}");
                }
                
                foreach (var position in Positions.Where(p => p.IsSelected))
                {
                    var positionKey = $"{position.Symbol}_{position.PositionSideString}";
                    selectedPositionSymbols.Add(positionKey);
                    LogService.LogDebug($"💾 保存持仓选择: Key={positionKey}, Amount={position.PositionAmt}");
                }
                
                LogService.LogInfo($"选择状态保存完成: 订单{selectedOrderIds.Count}个, 持仓{selectedPositionSymbols.Count}个");

                // 获取账户信息
                AccountInfo = await _binanceService.GetAccountInfoAsync();
                LogService.LogDebug("✅ 账户信息更新完成");

                // 获取持仓信息
                var positions = await _binanceService.GetPositionsAsync();
                LogService.LogDebug($"📈 获取到{positions.Count}个持仓数据");
                
                Positions.Clear();
                int restoredPositionCount = 0;
                foreach (var position in positions)
                {
                    // 恢复持仓选择状态
                    var positionKey = $"{position.Symbol}_{position.PositionSideString}";
                    if (selectedPositionSymbols.Contains(positionKey))
                    {
                        position.IsSelected = true;
                        restoredPositionCount++;
                        LogService.LogDebug($"🔄 恢复持仓选择: Key={positionKey}, IsSelected=true");
                    }
                    Positions.Add(position);
                }
                LogService.LogInfo($"持仓选择状态恢复: {restoredPositionCount}/{selectedPositionSymbols.Count}个");

                // 计算保证金占用
                if (AccountInfo != null)
                {
                    AccountInfo.CalculateMarginUsed(Positions);
                    OnPropertyChanged(nameof(AccountInfo.ActualMarginUsed));
                    // 强制通知已用保证金属性更新
                    OnPropertyChanged(nameof(TotalMarginBalance));
                    // 通知账户权益属性更新
                    OnPropertyChanged(nameof(TotalWalletBalance));
                }

                // 获取订单信息
                var orders = await _binanceService.GetOpenOrdersAsync();
                LogService.LogDebug($"📋 获取到{orders.Count}个订单数据");
                
                Orders.Clear();
                int restoredOrderCount = 0;
                foreach (var order in orders)
                {
                    // 恢复订单选择状态
                    if (selectedOrderIds.Contains(order.OrderId))
                    {
                        order.IsSelected = true;
                        restoredOrderCount++;
                        LogService.LogDebug($"🔄 恢复订单选择: OrderId={order.OrderId}, IsSelected=true");
                    }
                    Orders.Add(order);
                }
                LogService.LogInfo($"订单选择状态恢复: {restoredOrderCount}/{selectedOrderIds.Count}个");

                // 更新最新价格
                if (!string.IsNullOrEmpty(Symbol))
                {
                    LatestPrice = await _binanceService.GetLatestPriceAsync(Symbol);
                }

                // 如果有选中的持仓，更新过滤的订单
                if (SelectedPosition != null)
                {
                    LogService.LogDebug($"🔍 当前选中持仓: {SelectedPosition.Symbol}, 开始过滤订单");
                    FilterOrdersForPosition(SelectedPosition.Symbol);
                    
                    // 恢复过滤订单的选择状态
                    int restoredFilteredOrderCount = 0;
                    foreach (var order in FilteredOrders)
                    {
                        if (selectedOrderIds.Contains(order.OrderId))
                        {
                            order.IsSelected = true;
                            restoredFilteredOrderCount++;
                            LogService.LogDebug($"🔄 恢复过滤订单选择: OrderId={order.OrderId}, IsSelected=true");
                        }
                    }
                    LogService.LogInfo($"过滤订单选择状态恢复: {restoredFilteredOrderCount}个");
                }
                else
                {
                    // 没有选中持仓，显示所有委托单
                    FilterOrdersForPosition(); // 不传参数，显示所有委托单
                    
                    // 恢复所有订单的选择状态
                    int restoredFilteredOrderCount = 0;
                    foreach (var order in FilteredOrders)
                    {
                        if (selectedOrderIds.Contains(order.OrderId))
                        {
                            order.IsSelected = true;
                            restoredFilteredOrderCount++;
                        }
                    }
                }

                // 强制通知选择状态属性更新
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));
                OnPropertyChanged(nameof(SelectedPositions));
                OnPropertyChanged(nameof(HasSelectedPositions));
                OnPropertyChanged(nameof(SelectedPositionCount));
                OnPropertyChanged(nameof(HasSelectedStopOrders));
                OnPropertyChanged(nameof(SelectedStopOrderCount));
                
                LogService.LogDebug("📢 选择状态属性通知已发送");

                // 验证最终状态
                var finalSelectedPositions = Positions.Count(p => p.IsSelected);
                var finalSelectedOrders = FilteredOrders.Count(o => o.IsSelected);
                LogService.LogInfo($"🎯 最终选择状态: 持仓{finalSelectedPositions}个, 订单{finalSelectedOrders}个");

                StatusMessage = $"数据刷新完成 - {DateTime.Now:HH:mm:ss}";
                LogService.LogSuccess("🔄 手动刷新完成");
            }
            catch (Exception ex)
            {
                StatusMessage = $"刷新失败: {ex.Message}";
                LogService.LogError("❌ 手动刷新异常", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ConfigureAccount()
        {
            var viewModel = new AccountConfigViewModel(_accountService);
            var window = new BinanceFuturesTrader.Views.AccountConfigWindow(viewModel);
            
            window.ShowDialog();
            LoadAccounts();
        }

        [RelayCommand]
        private void EditCurrentAccount()
        {
            if (SelectedAccount == null)
                return;

            var currentAccountName = SelectedAccount.Name;
            var viewModel = new AccountConfigViewModel(_accountService, SelectedAccount);
            var window = new BinanceFuturesTrader.Views.AccountConfigWindow(viewModel);
            
            var result = window.ShowDialog();
            if (result == true)
            {
                LoadAccounts();
                // 重新选择相同的账户（可能已更新）
                var updatedAccount = Accounts.FirstOrDefault(a => a.Name == currentAccountName);
                if (updatedAccount != null)
                {
                    SelectedAccount = updatedAccount;
                    StatusMessage = "账户信息已更新";
                }
            }
        }

        [RelayCommand]
        private async Task ClosePositionAsync()
        {
            if (SelectedAccount == null || SelectedPosition == null)
                return;

            IsLoading = true;
            StatusMessage = $"平仓 {SelectedPosition.Symbol}...";
            try
            {
                var success = await _binanceService.ClosePositionAsync(SelectedPosition.Symbol, SelectedPosition.PositionSideString);
                StatusMessage = success ? $"{SelectedPosition.Symbol} 平仓完成" : $"{SelectedPosition.Symbol} 平仓失败";
                
                if (success)
                {
                    await RefreshDataAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"平仓失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddBreakEvenStopLossAsync()
        {
            Console.WriteLine($"🛡️ 开始添加保本止损...");
            
            try
            {
                // 第一步：基本参数检查
                if (SelectedAccount == null)
                {
                    Console.WriteLine($"❌ 未选择账户");
                    StatusMessage = "请选择账户";
                    
                    try
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(
                                "请先选择一个交易账户",
                                "未选择账户",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        });
                    }
                    catch (Exception uiEx)
                    {
                        Console.WriteLine($"❌ UI显示错误消息异常: {uiEx.Message}");
                        StatusMessage = "未选择账户，请选择交易账户";
                    }
                    return;
                }

                if (SelectedPosition == null)
                {
                    Console.WriteLine($"❌ 未选择持仓");
                    StatusMessage = "请选择持仓";
                    
                    try
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(
                                "请先在持仓列表中选择一个持仓",
                                "未选择持仓",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        });
                    }
                    catch (Exception uiEx)
                    {
                        Console.WriteLine($"❌ UI显示错误消息异常: {uiEx.Message}");
                        StatusMessage = "未选择持仓，请在持仓列表中选择持仓";
                    }
                    return;
                }

                Console.WriteLine($"📊 检查持仓信息: {SelectedPosition.Symbol}, 数量: {SelectedPosition.PositionAmt}, 开仓价: {SelectedPosition.EntryPrice}");

                // 第二步：持仓数据有效性检查
                if (Math.Abs(SelectedPosition.PositionAmt) <= 0)
                {
                    Console.WriteLine($"❌ 持仓数量为0");
                    StatusMessage = "选中的持仓数量为0，无法设置止损";
                    
                    try
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(
                                $"选中的持仓 {SelectedPosition.Symbol} 数量为0，无法设置保本止损",
                                "持仓数量无效",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        });
                    }
                    catch (Exception uiEx)
                    {
                        Console.WriteLine($"❌ UI显示错误消息异常: {uiEx.Message}");
                    }
                    return;
                }

                if (SelectedPosition.EntryPrice <= 0)
                {
                    Console.WriteLine($"❌ 开仓价无效: {SelectedPosition.EntryPrice}");
                    StatusMessage = "持仓开仓价无效，无法设置保本止损";
                    
                    try
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(
                                $"持仓 {SelectedPosition.Symbol} 的开仓价无效（{SelectedPosition.EntryPrice}），无法设置保本止损",
                                "开仓价无效",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        });
                    }
                    catch (Exception uiEx)
                    {
                        Console.WriteLine($"❌ UI显示错误消息异常: {uiEx.Message}");
                    }
                    return;
                }

                // 第三步：计算止损参数
                string formattedEntryPrice;
                string positionDirection;
                string stopDirection;
                
                try
                {
                    formattedEntryPrice = PriceFormatConverter.FormatPrice(SelectedPosition.EntryPrice);
                    positionDirection = SelectedPosition.PositionAmt > 0 ? "做多" : "做空";
                    stopDirection = SelectedPosition.PositionAmt > 0 ? "卖出" : "买入";
                    
                    Console.WriteLine($"📝 止损参数: 开仓价={formattedEntryPrice}, 持仓方向={positionDirection}, 止损方向={stopDirection}");
                }
                catch (Exception calcEx)
                {
                    Console.WriteLine($"❌ 计算止损参数异常: {calcEx.Message}");
                    StatusMessage = "计算止损参数失败";
                    
                    try
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(
                                $"计算止损参数时发生异常：{calcEx.Message}",
                                "计算异常",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Error);
                        });
                    }
                    catch (Exception uiEx)
                    {
                        Console.WriteLine($"❌ UI显示错误消息异常: {uiEx.Message}");
                        StatusMessage = $"计算异常: {calcEx.Message}";
                    }
                    return;
                }

                // 第四步：显示确认对话框
                System.Windows.MessageBoxResult result;
                try
                {
                    Console.WriteLine($"🔔 显示确认对话框...");
                    
                    result = await App.Current.Dispatcher.InvokeAsync(() =>
                    {
                        return System.Windows.MessageBox.Show(
                            $"确定要为以下持仓添加保本止损单吗？\n\n" +
                            $"合约：{SelectedPosition.Symbol}\n" +
                            $"持仓方向：{positionDirection}\n" +
                            $"持仓数量：{Math.Abs(SelectedPosition.PositionAmt):F6}\n" +
                            $"开仓价：{formattedEntryPrice}\n\n" +
                            $"将下{stopDirection}市价止损单：\n" +
                            $"触发价：{formattedEntryPrice}（保本价）\n" +
                            $"数量：{Math.Abs(SelectedPosition.PositionAmt):F6}",
                            "保本止损确认",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Question);
                    });
                    
                    Console.WriteLine($"✅ 用户选择: {result}");
                }
                catch (Exception dialogEx)
                {
                    Console.WriteLine($"❌ 显示确认对话框异常: {dialogEx.Message}");
                    StatusMessage = "显示确认对话框失败";
                    
                    try
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(
                                $"显示确认对话框时发生异常：{dialogEx.Message}",
                                "界面异常",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Error);
                        });
                    }
                    catch (Exception uiEx)
                    {
                        Console.WriteLine($"❌ UI显示错误消息异常: {uiEx.Message}");
                        StatusMessage = $"界面异常: {dialogEx.Message}";
                    }
                    return;
                }

                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    Console.WriteLine($"🚫 用户取消操作");
                    StatusMessage = "用户取消了保本止损操作";
                    return;
                }

                // 第五步：执行下单操作
                IsLoading = true;
                StatusMessage = $"为 {SelectedPosition.Symbol} 添加保本止损单...";
                Console.WriteLine($"🚀 开始执行保本止损下单...");
                
                try
                {
                    // 构建保本止损单
                    var stopLossOrder = new OrderRequest
                    {
                        Symbol = SelectedPosition.Symbol,
                        Side = SelectedPosition.PositionAmt > 0 ? "SELL" : "BUY", // 反向操作
                        PositionSide = SelectedPosition.PositionSideString,
                        Type = "STOP_MARKET", // 市价止损单
                        Quantity = Math.Abs(SelectedPosition.PositionAmt), // 相同数量
                        StopPrice = SelectedPosition.EntryPrice, // 触发价=开仓价
                        ReduceOnly = true, // 只减仓
                        Leverage = SelectedPosition.Leverage,
                        MarginType = SelectedPosition.MarginType,
                        WorkingType = "CONTRACT_PRICE" // 使用合约价格触发
                    };

                    Console.WriteLine($"📋 止损单详情: {stopLossOrder.Side} {stopLossOrder.Quantity:F6} {stopLossOrder.Symbol} @ {formattedEntryPrice}");

                    var success = await _binanceService.PlaceOrderAsync(stopLossOrder);

                    if (success)
                    {
                        StatusMessage = $"保本止损单下单成功：{SelectedPosition.Symbol} @ {formattedEntryPrice}";
                        Console.WriteLine($"✅ 保本止损单下单成功");
                        
                        try
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                System.Windows.MessageBox.Show(
                                    $"保本止损单下单成功！\n\n" +
                                    $"✅ {stopDirection}止损单：{stopLossOrder.Quantity:F6} {SelectedPosition.Symbol}\n" +
                                    $"📊 触发价：{formattedEntryPrice}（保本价）\n" +
                                    $"🎯 当价格{(SelectedPosition.PositionAmt > 0 ? "跌至" : "涨至")}开仓价时自动平仓",
                                    "保本止损成功",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Information);
                            });
                        }
                        catch (Exception uiEx)
                        {
                            Console.WriteLine($"❌ 显示成功消息异常: {uiEx.Message}");
                        }

                        // 清理冲突的止损委托
                        Console.WriteLine($"🧹 开始清理无效的止损委托...");
                        var isLong = SelectedPosition.PositionAmt > 0;
                        await CleanupConflictingStopOrdersAsync(SelectedPosition.Symbol, SelectedPosition.EntryPrice, isLong);

                        // 刷新数据以显示新的委托单
                        try
                        {
                            Console.WriteLine("🔄 保本止损成功，开始刷新数据以显示新订单...");
                            await RefreshDataAsync();
                        }
                        catch (Exception refreshEx)
                        {
                            Console.WriteLine($"❌ 刷新数据异常: {refreshEx.Message}");
                            StatusMessage = "保本止损成功，但刷新数据失败";
                        }
                    }
                    else
                    {
                        StatusMessage = $"保本止损单下单失败：{SelectedPosition.Symbol}";
                        Console.WriteLine($"❌ 保本止损单下单失败");
                        
                        try
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                System.Windows.MessageBox.Show(
                                    $"保本止损单下单失败！\n\n❌ {SelectedPosition.Symbol}\n\n请检查账户状态和网络连接",
                                    "下单失败",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Error);
                            });
                        }
                        catch (Exception uiEx)
                        {
                            Console.WriteLine($"❌ 显示失败消息异常: {uiEx.Message}");
                        }
                    }
                }
                catch (Exception orderEx)
                {
                    StatusMessage = $"保本止损单下单异常: {orderEx.Message}";
                    Console.WriteLine($"❌ 保本止损单下单异常: {orderEx.Message}");
                    Console.WriteLine($"❌ 异常堆栈: {orderEx.StackTrace}");
                    
                    try
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(
                                $"保本止损单下单异常：\n\n{orderEx.Message}\n\n请查看控制台日志了解详细信息",
                                "下单异常",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Error);
                        });
                    }
                    catch (Exception uiEx)
                    {
                        Console.WriteLine($"❌ 显示异常消息异常: {uiEx.Message}");
                        StatusMessage = $"下单异常: {orderEx.Message}";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"保本止损功能异常: {ex.Message}";
                Console.WriteLine($"❌ 保本止损功能顶层异常: {ex.Message}");
                Console.WriteLine($"❌ 异常类型: {ex.GetType().Name}");
                Console.WriteLine($"❌ 异常堆栈: {ex.StackTrace}");
                
                try
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show(
                            $"保本止损功能发生未预期的异常：\n\n" +
                            $"类型：{ex.GetType().Name}\n" +
                            $"消息：{ex.Message}\n\n" +
                            $"请联系技术支持并提供控制台日志",
                            "系统异常",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    });
                }
                catch (Exception uiEx)
                {
                    Console.WriteLine($"❌ 显示系统异常消息异常: {uiEx.Message}");
                    StatusMessage = $"系统异常: {ex.Message}";
                }
            }
            finally
            {
                IsLoading = false;
                Console.WriteLine($"🏁 保本止损操作完成");
            }
        }

        [RelayCommand]
        private async Task CancelAllOrdersAsync()
        {
            if (SelectedAccount == null)
                return;

            IsLoading = true;
            StatusMessage = "清理委托单...";
            try
            {
                var success = await _binanceService.CancelAllOrdersAsync();
                StatusMessage = success ? "委托单清理完成" : "委托单清理失败";
                
                if (success)
                {
                    await RefreshDataAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"清理委托单失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void CalculateMaxRiskCapital()
        {
            if (AccountInfo == null || SelectedAccount == null)
                return;

            var availableRiskCapital = AccountInfo.AvailableRiskCapital(SelectedAccount.RiskCapitalTimes);
            
            // 向上取整，保留整数，只填写止损金额
            StopLossAmount = Math.Ceiling(availableRiskCapital);
            
            StatusMessage = $"已设置最大风险金: {StopLossAmount:F0} USDT (向上取整)";
            Console.WriteLine($"💰 最大风险金设置: {StopLossAmount:F0} USDT");
        }

        [RelayCommand]
        private async Task CalculateQuantityFromLossAsync()
        {
            if (LatestPrice <= 0)
            {
                StatusMessage = "请先获取最新价格";
                return;
            }

            if (StopLossRatio <= 0)
            {
                StatusMessage = "请设置止损比例";
                return;
            }

            if (StopLossAmount <= 0)
            {
                StatusMessage = "请设置止损金额";
                return;
            }

            try
            {
                // 正确的期货"以损定量"计算公式：
                // 方法1：数量 = 止损金额 / (当前价格 × 止损比例)
                // 方法2：货值 = 止损金额 / 止损比例，数量 = 货值 / 当前价格
                
                Console.WriteLine($"🧮 以损定量计算开始:");
                Console.WriteLine($"📊 输入参数: 价格={LatestPrice:F8}, 止损金额={StopLossAmount:F2}, 止损比例={StopLossRatio:F2}%");
                
                var stopLossDecimal = StopLossRatio / 100; // 将百分比转为小数
                Console.WriteLine($"💱 止损比例(小数): {stopLossDecimal:F6}");
                
                // 使用用户期望的计算方式：货值 = 止损金额 / 止损比例
                var notionalValue = StopLossAmount / stopLossDecimal;
                Console.WriteLine($"💰 计算货值: {StopLossAmount:F2} ÷ {stopLossDecimal:F6} = {notionalValue:F2}");
                
                // 数量 = 货值 / 价格
                var calculatedQuantity = notionalValue / LatestPrice;
                Console.WriteLine($"📦 计算数量: {notionalValue:F2} ÷ {LatestPrice:F8} = {calculatedQuantity:F8}");
                
                // 验证计算（方法1）
                var priceChange = LatestPrice * stopLossDecimal;
                var verifyQuantity = StopLossAmount / priceChange;
                Console.WriteLine($"✅ 验证计算: {StopLossAmount:F2} ÷ ({LatestPrice:F8} × {stopLossDecimal:F6}) = {StopLossAmount:F2} ÷ {priceChange:F8} = {verifyQuantity:F8}");
                
                if (Math.Abs(calculatedQuantity - verifyQuantity) > 0.000001m)
                {
                    Console.WriteLine($"⚠️ 警告：两种计算方法结果不一致！");
                }
                
                // 获取该合约的交易限制
                var (minQuantity, maxQuantity, maxLeverage, maxNotional, estimatedPrice) = await GetSymbolLimitsAsync(Symbol);
                Console.WriteLine($"📏 {Symbol} 限制: 最小={minQuantity}, 最大={maxQuantity}");
                
                // 根据合约精度调整数量
                Console.WriteLine($"🔧 精度调整前: {calculatedQuantity:F8}");
                var adjustedQuantity = await AdjustQuantityPrecisionAsync(calculatedQuantity, Symbol, minQuantity, maxQuantity);
                Console.WriteLine($"🔧 精度调整后: {adjustedQuantity:F8}");
                
                Quantity = adjustedQuantity;
                
                // 验算：计算实际止损金额
                var actualLoss = adjustedQuantity * LatestPrice * stopLossDecimal;
                Console.WriteLine($"🧾 验算实际止损: {adjustedQuantity:F8} × {LatestPrice:F8} × {stopLossDecimal:F6} = {actualLoss:F2} USDT");
                
                StatusMessage = $"已计算数量: {Quantity:F8} (目标止损{StopLossAmount:F2}U, 实际{actualLoss:F2}U, 比例{StopLossRatio}%)";
                Console.WriteLine($"✅ 以损定量完成: 数量={Quantity:F8}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"计算数量失败: {ex.Message}";
                Console.WriteLine($"❌ 以损定量计算异常: {ex.Message}");
            }
        }
        
        private async Task<decimal> AdjustQuantityPrecisionAsync(decimal quantity, string symbol, decimal minQuantity, decimal maxQuantity)
        {
            Console.WriteLine($"🔧 开始精度调整: 原始数量={quantity:F8}");
            
            try
            {
                // 获取真实的交易规则，特别是stepSize
                var (stepSize, tickSize) = await _binanceService.GetSymbolPrecisionAsync(symbol);
                
                Console.WriteLine($"📐 {symbol} 的stepSize: {stepSize}");
                
                // 1. 首先检查最小数量限制
                if (quantity < minQuantity)
                {
                    Console.WriteLine($"⚠️ 数量 {quantity:F8} 小于最小限制 {minQuantity}，调整为最小值");
                    quantity = minQuantity;
                }
                
                // 2. 根据stepSize调整精度
                if (stepSize > 0)
                {
                    // 确保数量是stepSize的整数倍
                    var steps = Math.Floor(quantity / stepSize);
                    var adjustedQuantity = steps * stepSize;
                    
                    Console.WriteLine($"📊 stepSize调整: {quantity:F8} → {steps} × {stepSize} = {adjustedQuantity:F8}");
                    
                    // 如果调整后的数量太小，增加一个stepSize
                    if (adjustedQuantity < minQuantity && (adjustedQuantity + stepSize) <= maxQuantity)
                    {
                        adjustedQuantity += stepSize;
                        Console.WriteLine($"💡 增加一个stepSize: {adjustedQuantity:F8}");
                    }
                    
                    quantity = adjustedQuantity;
                }
                else
                {
                    Console.WriteLine($"⚠️ stepSize无效，使用传统精度调整");
                    // 如果API没有返回有效stepSize，使用传统方法
                    quantity = AdjustQuantityPrecisionTraditional(quantity, symbol);
                }
                
                // 3. 再次检查最大数量限制
                if (quantity > maxQuantity)
                {
                    Console.WriteLine($"⚠️ 数量 {quantity:F8} 超过最大限制 {maxQuantity}，调整为最大值");
                    quantity = maxQuantity;
                    
                    // 确保最大值也符合stepSize
                    if (stepSize > 0)
                    {
                        var steps = Math.Floor(quantity / stepSize);
                        quantity = steps * stepSize;
                        Console.WriteLine($"📊 最大值stepSize调整: {quantity:F8}");
                    }
                }
                
                // 4. 最后检查调整后是否满足最小数量要求
                if (quantity < minQuantity)
                {
                    Console.WriteLine($"❌ 最终数量 {quantity:F8} 仍小于最小限制，无法满足交易要求");
                    quantity = minQuantity;
                }
                
                Console.WriteLine($"✅ 数量精度调整完成: {quantity:F8}");
                return quantity;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ API精度调整失败: {ex.Message}，使用传统方法");
                // API失败时使用传统方法
                return AdjustQuantityPrecisionTraditional(quantity, symbol);
            }
        }
        
        private decimal AdjustQuantityPrecisionTraditional(decimal quantity, string symbol)
        {
            // 传统的基于合约类型的精度调整
            var adjustedQuantity = symbol.ToUpper() switch
            {
                "BTCUSDT" => Math.Round(quantity, 3), // BTC: 3位小数
                "ETHUSDT" => Math.Round(quantity, 3), // ETH: 3位小数
                "BNBUSDT" => Math.Round(quantity, 2), // BNB: 2位小数
                "ADAUSDT" => Math.Round(quantity, 0), // ADA: 整数
                "DOGEUSDT" => Math.Round(quantity, 0), // DOGE: 整数
                "SOLUSDT" => Math.Round(quantity, 1), // SOL: 1位小数
                "DOTUSDT" => Math.Round(quantity, 1), // DOT: 1位小数
                "LINKUSDT" => Math.Round(quantity, 1), // LINK: 1位小数
                "LTCUSDT" => Math.Round(quantity, 2), // LTC: 2位小数
                "BCHUSDT" => Math.Round(quantity, 3), // BCH: 3位小数
                "XRPUSDT" => Math.Round(quantity, 0), // XRP: 整数
                "MATICUSDT" => Math.Round(quantity, 0), // MATIC: 整数
                "AVAXUSDT" => Math.Round(quantity, 1), // AVAX: 1位小数
                "UNIUSDT" => Math.Round(quantity, 1), // UNI: 1位小数
                "ATOMUSDT" => Math.Round(quantity, 1), // ATOM: 1位小数
                _ => Math.Round(quantity, 3) // 默认: 3位小数
            };
            
            Console.WriteLine($"🔧 传统精度调整: {quantity:F8} → {adjustedQuantity:F8}");
            return adjustedQuantity;
        }
        
        private async Task<(decimal minQuantity, decimal maxQuantity, int maxLeverage, decimal maxNotional, decimal estimatedPrice)> GetSymbolLimitsAsync(string symbol)
        {
            try
            {
                // 获取真实的交易规则信息
                var (minQuantity, maxQuantity, maxLeverage, maxNotional, estimatedPrice) = await GetExchangeInfoAsync(symbol);
                return (minQuantity, maxQuantity, maxLeverage, maxNotional, estimatedPrice);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取 {symbol} 交易规则异常: {ex.Message}");
                // 异常时使用动态计算的备选方案
                return GetDynamicLimits(LatestPrice);
            }
        }
        
        private (decimal minQuantity, decimal maxQuantity, int maxLeverage, decimal maxNotional, decimal estimatedPrice) GetDynamicLimits(decimal currentPrice)
        {
            // 根据价格动态计算合理的数量限制
            decimal minQuantity, maxQuantity;
            int maxLeverage;
            decimal maxNotional;
            
            if (currentPrice >= 1000m) // 高价币（如BTC）
            {
                minQuantity = 0.001m;
                maxQuantity = 1000m;
                maxLeverage = 125;
                maxNotional = 2000000m;
            }
            else if (currentPrice >= 100m) // 中高价币（如ETH）
            {
                minQuantity = 0.001m;
                maxQuantity = 10000m;
                maxLeverage = 100;
                maxNotional = 1000000m;
            }
            else if (currentPrice >= 10m) // 中价币（如BNB）
            {
                minQuantity = 0.01m;
                maxQuantity = 100000m;
                maxLeverage = 75;
                maxNotional = 500000m;
            }
            else if (currentPrice >= 1m) // 一般价币（如DOT）
            {
                minQuantity = 0.1m;
                maxQuantity = 1000000m;
                maxLeverage = 75;
                maxNotional = 200000m;
            }
            else if (currentPrice >= 0.1m) // 低价币（如ADA）
            {
                minQuantity = 1m;
                maxQuantity = 10000000m;  // 使用更大的最大值以适应真实交易需求
                maxLeverage = 75;
                maxNotional = 100000m;
            }
            else if (currentPrice >= 0.01m) // 很低价币（如DOGE）
            {
                minQuantity = 10m;
                maxQuantity = 100000000m;
                maxLeverage = 50;
                maxNotional = 100000m;
            }
            else // 超低价币（如PEPE、SHIB等）
            {
                minQuantity = 1000m;
                maxQuantity = 10000000000m;  // 超低价币需要极大的数量
                maxLeverage = 25;
                maxNotional = 25000m;
            }
            
            Console.WriteLine($"🎯 动态限制: 价格={currentPrice:F8}, 最小数量={minQuantity}, 最大数量={maxQuantity}");
            
            return (minQuantity, maxQuantity, maxLeverage, maxNotional, currentPrice);
        }

        // 智能计算止损价格
        [RelayCommand]
        private void CalculateStopLossPrice()
        {
            Console.WriteLine($"🎯 开始计算止损价...");
            Console.WriteLine($"📊 当前参数: 最新价={PriceFormatConverter.FormatPrice(LatestPrice)}, 止损比例={StopLossRatio:F2}%, 交易方向={Side}");
            
            // 详细调试Side属性
            Console.WriteLine($"🔍 Side属性调试信息:");
            Console.WriteLine($"   Side值: '{Side}'");
            Console.WriteLine($"   Side类型: {Side?.GetType()?.Name ?? "null"}");
            Console.WriteLine($"   Side长度: {Side?.Length ?? 0}");
            Console.WriteLine($"   Side是否为null: {Side == null}");
            Console.WriteLine($"   Side是否为空: {string.IsNullOrEmpty(Side)}");
            Console.WriteLine($"   Side == 'BUY': {Side == "BUY"}");
            Console.WriteLine($"   Side == 'SELL': {Side == "SELL"}");
            
            if (LatestPrice <= 0)
            {
                StatusMessage = "请先获取最新价格";
                Console.WriteLine($"❌ 最新价格无效: {LatestPrice}");
                return;
            }

            if (StopLossRatio <= 0)
            {
                StatusMessage = "请设置止损比例（0.1%-100%）";
                Console.WriteLine($"❌ 止损比例无效: {StopLossRatio}");
                return;
            }

            if (StopLossRatio < 0.1m || StopLossRatio > 100m)
            {
                StatusMessage = "止损比例超出范围，请输入0.1-100之间的数值";
                Console.WriteLine($"❌ 止损比例超出范围: {StopLossRatio:F2}%（有效范围：0.1%-100%）");
                return;
            }

            if (string.IsNullOrEmpty(Side) || (Side != "BUY" && Side != "SELL"))
            {
                StatusMessage = "请选择正确的交易方向(BUY/SELL)";
                Console.WriteLine($"❌ 交易方向无效: '{Side}'");
                return;
            }

            try
            {
                decimal calculatedStopLossPrice = 0;
                
                // 根据交易方向计算止损价
                if (Side == "BUY")
                {
                    // 买入时，止损价 = 当前价 × (1 - 止损比例%)
                    calculatedStopLossPrice = LatestPrice * (1 - StopLossRatio / 100);
                    Console.WriteLine($"💰 做多计算: {PriceFormatConverter.FormatPrice(LatestPrice)} × (1 - {StopLossRatio:F2}% / 100) = {PriceFormatConverter.FormatPrice(calculatedStopLossPrice)}");
                }
                else if (Side == "SELL")
                {
                    // 卖出时，止损价 = 当前价 × (1 + 止损比例%)
                    calculatedStopLossPrice = LatestPrice * (1 + StopLossRatio / 100);
                    Console.WriteLine($"💰 做空计算: {PriceFormatConverter.FormatPrice(LatestPrice)} × (1 + {StopLossRatio:F2}% / 100) = {PriceFormatConverter.FormatPrice(calculatedStopLossPrice)}");
                }

                // 确保计算结果有效
                if (calculatedStopLossPrice <= 0)
                {
                    StatusMessage = "止损价计算结果无效，请检查参数";
                    Console.WriteLine($"❌ 计算结果无效: {calculatedStopLossPrice}");
                    return;
                }

                // 设置止损价
                StopLossPrice = calculatedStopLossPrice;
                Console.WriteLine($"✅ 止损价已设置: {PriceFormatConverter.FormatPrice(StopLossPrice)}");

                // 计算预期亏损金额
                if (Quantity > 0)
                {
                    var priceChange = Math.Abs(LatestPrice - StopLossPrice);
                    StopLossAmount = priceChange * Quantity;
                    
                    // 手动计算时显示详细信息
                    var formattedStopLossPrice = PriceFormatConverter.FormatPrice(StopLossPrice);
                    StatusMessage = $"止损价已计算: {formattedStopLossPrice}, 预期亏损: {StopLossAmount:F2} USDT";
                    var formattedLatestPrice = PriceFormatConverter.FormatPrice(LatestPrice);
                    Console.WriteLine($"🎯 智能计算完成: {Side} 方向, 当前价 {formattedLatestPrice}, 止损比例 {StopLossRatio:F2}%, 止损价 {formattedStopLossPrice}");
                    Console.WriteLine($"💸 预期亏损: 价差={PriceFormatConverter.FormatPrice(priceChange)}, 数量={Quantity}, 亏损金额={StopLossAmount:F2} USDT");
                }
                else
                {
                    // 数量为0时只显示价格
                    var formattedLatestPrice = PriceFormatConverter.FormatPrice(LatestPrice);
                    var formattedStopLossPrice = PriceFormatConverter.FormatPrice(StopLossPrice);
                    StatusMessage = $"止损价已计算: {formattedStopLossPrice} (请设置交易数量以计算亏损金额)";
                    Console.WriteLine($"🤖 止损价计算完成: {Side} 方向, 当前价 {formattedLatestPrice}, 止损比例 {StopLossRatio:F2}%, 止损价 {formattedStopLossPrice}");
                }
                
                // 触发属性变化通知
                OnPropertyChanged(nameof(StopLossPrice));
                OnPropertyChanged(nameof(StopLossAmount));
            }
            catch (Exception ex)
            {
                StatusMessage = $"计算止损价失败: {ex.Message}";
                Console.WriteLine($"❌ 计算止损价异常: {ex.Message}");
                Console.WriteLine($"📍 异常堆栈: {ex.StackTrace}");
            }
        }

        [RelayCommand]
        private async Task PlaceOrderAsync()
        {
            if (SelectedAccount == null || string.IsNullOrEmpty(Symbol) || Quantity <= 0)
            {
                StatusMessage = "请确保选择了账户、输入了合约名称和数量";
                return;
            }

            // 🎯 确保有最新价格，特别是市价单需要准确的价格进行风险计算
            if (LatestPrice <= 0)
            {
                StatusMessage = "请先获取最新价格";
                return;
            }

            // 调试输出：显示当前的交易参数
            Console.WriteLine("\n🔍 下单前参数检查:");
            Console.WriteLine($"   当前Side值: '{Side}' (类型: {Side?.GetType()?.Name ?? "null"})");
            Console.WriteLine($"   IsBuySelected: {IsBuySelected}");
            Console.WriteLine($"   IsSellSelected: {IsSellSelected}");
            Console.WriteLine($"   将设置PositionSide为: {(Side == "BUY" ? "LONG" : "SHORT")}");

            // 构建订单请求对象
            var orderRequest = new OrderRequest
            {
                Symbol = Symbol,
                Side = Side,
                // 🎯 PositionSide设置逻辑：
                // 单向持仓模式(默认)：使用BOTH
                // 双向持仓模式：BUY→LONG，SELL→SHORT
                // 优先使用BOTH保证兼容性
                PositionSide = "BOTH", // 默认使用BOTH，兼容大多数账户的单向持仓模式
                Type = OrderType,
                Quantity = Quantity,
                // 🎯 限价单使用设置的Price，市价单设Price=0
                Price = OrderType == "LIMIT" ? Price : 0,
                StopPrice = OrderType.Contains("STOP") || OrderType.Contains("TAKE_PROFIT") ? StopLossPrice : 0,
                WorkingType = WorkingType,
                Leverage = Leverage,
                MarginType = MarginType,
                StopLossRatio = StopLossRatio,
                StopLossPrice = StopLossPrice,
                StopLossAmount = StopLossAmount
            };

            // 执行下单校验
            IsLoading = true;
            StatusMessage = "正在校验下单参数...";
            
            try
            {
                Console.WriteLine($"🔍 下单校验开始: {Side} {Quantity} {Symbol}");
                Console.WriteLine($"📊 订单类型: {OrderType}, 价格: {(OrderType == "LIMIT" ? LatestPrice.ToString() : "市价")}");
                Console.WriteLine($"🛡️ 止损设置: 价格={StopLossPrice}, 金额={StopLossAmount}, 比例={StopLossRatio}%");
                
                var (isValid, errorMessage) = await _binanceService.ValidateOrderAsync(orderRequest);
                
                if (!isValid)
                {
                    StatusMessage = $"下单校验失败: {errorMessage}";
                    System.Windows.MessageBox.Show(
                        $"下单校验失败：\n\n{errorMessage}\n\n请调整参数后重试",
                        "参数错误",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // 更新UI显示校验后的参数
                Leverage = orderRequest.Leverage;
                MarginType = orderRequest.MarginType;
                StopLossAmount = orderRequest.StopLossAmount;

                // 构建下单确认信息
                var priceDisplay = OrderType == "MARKET" ? "市价" : PriceFormatConverter.FormatPrice(LatestPrice);
                var orderInfo = $"合约：{Symbol}\n" +
                               $"方向：{(Side == "BUY" ? "买入开多" : "卖出开空")}\n" +
                               $"数量：{Quantity}\n" +
                               $"类型：{OrderType}\n" +
                               $"价格：{priceDisplay}\n" +
                               $"杠杆：{Leverage}x\n" +
                               $"保证金模式：{MarginType}";

                if (StopLossPrice > 0)
                {
                    var formattedStopLossPrice = PriceFormatConverter.FormatPrice(StopLossPrice);
                    orderInfo += $"\n止损价：{formattedStopLossPrice}";
                    orderInfo += $"\n风险金额：{StopLossAmount:F2} USDT";
                }

                // 🎯 强调风险控制信息
                if (StopLossAmount > 0)
                {
                    orderInfo += $"\n\n⚠️ 最大风险：{StopLossAmount:F2} USDT";
                    Console.WriteLine($"🎯 风险控制确认: 最大亏损 {StopLossAmount:F2} USDT");
                }

                // 显示确认对话框
                var result = System.Windows.MessageBox.Show(
                    $"确认下单信息：\n\n{orderInfo}\n\n✅ 参数校验已通过\n\n确定要下单吗？",
                    "下单确认",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return;

                if (orderRequest.IsConditionalOrder)
                {
                    StatusMessage = $"条件单下单中: {OrderType} {Symbol} {Side} {Quantity}...";
                }
                else
                {
                    StatusMessage = $"下单中: {Side} {Quantity} {Symbol}...";
                }
                
                // 下单
                var success = await _binanceService.PlaceOrderAsync(orderRequest);
                
                if (success)
                {
                    if (orderRequest.IsConditionalOrder)
                    {
                        StatusMessage = "条件单下单成功";
                        var formattedStopLossPrice = PriceFormatConverter.FormatPrice(StopLossPrice);
                        System.Windows.MessageBox.Show(
                            $"条件单下单成功！\n\n✅ {OrderType}: {Side} {Quantity} {Symbol}\n📊 触发价：{formattedStopLossPrice}",
                            "条件单成功",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                            
                        // 添加到条件单监控列表
                        ConditionalOrders.Add(new ConditionalOrderInfo
                        {
                            Symbol = Symbol,
                            Type = OrderType,
                            Side = Side,
                            StopPrice = StopLossPrice,
                            Price = orderRequest.IsLimitConditionalOrder ? LatestPrice : null,
                            Quantity = Quantity,
                            Status = "待触发",
                            WorkingType = WorkingType
                        });
                        OnPropertyChanged(nameof(HasNoConditionalOrders));
                    }
                    else
                    {
                        StatusMessage = "下单成功";
                        
                        Console.WriteLine("\n🔍 检查止损单下单条件:");
                        Console.WriteLine($"   StopLossPrice: {StopLossPrice}");
                        Console.WriteLine($"   StopLossPrice > 0: {StopLossPrice > 0}");
                        Console.WriteLine($"   IsConditionalOrder: {orderRequest.IsConditionalOrder}");
                        
                        // 如果设置了止损价格，自动下止损单
                        if (StopLossPrice > 0)
                        {
                            Console.WriteLine("✅ 满足止损单下单条件，开始下止损单...");
                            StatusMessage = "正在下止损单...";
                            await Task.Delay(500); // 短暂延迟，确保主单处理完成

                            var stopLossSuccess = await PlaceStopLossOrderAsync(orderRequest);
                            
                            if (stopLossSuccess)
                            {
                                StatusMessage = "下单完成：主单和止损单都已成功下单";
                                var formattedStopLossPrice = PriceFormatConverter.FormatPrice(StopLossPrice);
                                System.Windows.MessageBox.Show(
                                    $"下单成功！\n\n✅ 主单：{Side} {Quantity} {Symbol}\n✅ 止损单：{formattedStopLossPrice}\n💰 预期最大亏损：{StopLossAmount:F2} USDT",
                                    "下单成功",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Information);
                            }
                            else
                            {
                                StatusMessage = "主单成功，止损单失败";
                                System.Windows.MessageBox.Show(
                                    $"主单下单成功，但止损单下单失败！\n\n✅ 主单：{Side} {Quantity} {Symbol}\n❌ 止损单失败\n\n请手动设置止损！",
                                    "部分成功",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            Console.WriteLine("⚠️ 未设置止损价格，跳过止损单下单");
                            System.Windows.MessageBox.Show(
                                $"下单成功！\n\n✅ {Side} {Quantity} {Symbol}",
                                "下单成功",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information);
                        }
                    }

                    // 刷新数据
                    await RefreshDataAsync();
                }
                else
                {
                    StatusMessage = "下单失败";
                    System.Windows.MessageBox.Show(
                        "下单失败！\n\n请检查账户余额、合约参数等",
                        "下单失败",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"下单失败: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"下单失败：\n\n{ex.Message}",
                    "下单失败",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<bool> PlaceStopLossOrderAsync(OrderRequest originalOrder)
        {
            try
            {
                Console.WriteLine("\n" + "=".PadLeft(60, '='));
                Console.WriteLine("🛡️ 开始下止损单流程");
                Console.WriteLine("=".PadLeft(60, '='));
                
                Console.WriteLine("📋 原始订单信息:");
                Console.WriteLine($"   Symbol: {originalOrder.Symbol}");
                Console.WriteLine($"   Side: {originalOrder.Side}");
                Console.WriteLine($"   Type: {originalOrder.Type}");
                Console.WriteLine($"   Quantity: {originalOrder.Quantity}");
                Console.WriteLine($"   PositionSide: {originalOrder.PositionSide}");
                Console.WriteLine($"   Leverage: {originalOrder.Leverage}");
                Console.WriteLine($"   MarginType: {originalOrder.MarginType}");
                
                Console.WriteLine("\n🎯 止损单参数设置:");
                Console.WriteLine($"   当前StopLossPrice: {StopLossPrice}");
                Console.WriteLine($"   当前StopLossRatio: {StopLossRatio}%");
                Console.WriteLine($"   当前StopLossAmount: {StopLossAmount} USDT");
                
                // 验证止损价格是否有效
                if (StopLossPrice <= 0)
                {
                    Console.WriteLine("❌ 止损价格无效，无法下止损单");
                    return false;
                }
                
                // 构建止损单
                var stopLossOrder = new OrderRequest
                {
                    Symbol = originalOrder.Symbol,
                    Side = originalOrder.Side == "BUY" ? "SELL" : "BUY", // 反向操作
                    PositionSide = originalOrder.PositionSide,
                    Type = "STOP_MARKET", // 止损市价单
                    Quantity = originalOrder.Quantity, // 必须设置数量
                    StopPrice = StopLossPrice,
                    ReduceOnly = true, // 只减仓
                    Leverage = originalOrder.Leverage,
                    MarginType = originalOrder.MarginType,
                    WorkingType = "CONTRACT_PRICE" // 使用合约价格触发
                };

                Console.WriteLine("\n🔧 构建的止损单参数:");
                Console.WriteLine($"   Symbol: {stopLossOrder.Symbol}");
                Console.WriteLine($"   Side: {stopLossOrder.Side} (原单{originalOrder.Side}的反向)");
                Console.WriteLine($"   Type: {stopLossOrder.Type}");
                Console.WriteLine($"   Quantity: {stopLossOrder.Quantity} (必须设置)");
                Console.WriteLine($"   StopPrice: {stopLossOrder.StopPrice}");
                Console.WriteLine($"   PositionSide: {stopLossOrder.PositionSide}");
                Console.WriteLine($"   ReduceOnly: {stopLossOrder.ReduceOnly}");
                Console.WriteLine($"   WorkingType: {stopLossOrder.WorkingType}");
                Console.WriteLine($"   Leverage: {stopLossOrder.Leverage}");
                Console.WriteLine($"   MarginType: {stopLossOrder.MarginType}");

                Console.WriteLine($"\n🛡️ 下止损单: {stopLossOrder.Side} {stopLossOrder.Quantity} {stopLossOrder.Symbol} @ {PriceFormatConverter.FormatPrice(StopLossPrice)}");
                
                // 验证止损价格是否合理
                if (originalOrder.Side == "BUY" && StopLossPrice >= LatestPrice)
                {
                    Console.WriteLine($"⚠️ 警告: 做多止损价({StopLossPrice})应该低于当前价({LatestPrice})");
                }
                else if (originalOrder.Side == "SELL" && StopLossPrice <= LatestPrice)
                {
                    Console.WriteLine($"⚠️ 警告: 做空止损价({StopLossPrice})应该高于当前价({LatestPrice})");
                }
                
                // 验证数量是否匹配
                if (stopLossOrder.Quantity != originalOrder.Quantity)
                {
                    Console.WriteLine($"⚠️ 警告: 止损单数量({stopLossOrder.Quantity})与原单数量({originalOrder.Quantity})不匹配");
                }
                else
                {
                    Console.WriteLine($"✅ 止损单数量验证通过: {stopLossOrder.Quantity}");
                }
                
                Console.WriteLine("\n🚀 开始调用BinanceService下单API...");
                var success = await _binanceService.PlaceOrderAsync(stopLossOrder);
                
                Console.WriteLine($"\n📊 止损单下单结果: {(success ? "成功" : "失败")}");
                
                if (success)
                {
                    Console.WriteLine("✅ 止损单下单成功");
                    Console.WriteLine("🔄 建议等待2-3秒后刷新委托列表查看止损单");
                }
                else
                {
                    Console.WriteLine("❌ 止损单下单失败");
                    Console.WriteLine("💡 可能原因:");
                    Console.WriteLine("   • 止损价格不符合交易规则");
                    Console.WriteLine("   • 数量或价格精度不正确");
                    Console.WriteLine("   • 账户权限或余额问题");
                    Console.WriteLine("   • 网络或API问题");
                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 止损单下单异常:");
                Console.WriteLine($"   异常类型: {ex.GetType().Name}");
                Console.WriteLine($"   异常消息: {ex.Message}");
                Console.WriteLine($"   异常堆栈: {ex.StackTrace}");
                return false;
            }
            finally
            {
                Console.WriteLine("\n" + "=".PadLeft(60, '='));
                Console.WriteLine("🏁 止损单流程结束");
                Console.WriteLine("=".PadLeft(60, '=') + "\n");
            }
        }

        [RelayCommand]
        private async Task UpdateLatestPriceAsync()
        {
            if (string.IsNullOrEmpty(Symbol) || SelectedAccount == null)
                return;

            try
            {
                var price = await _binanceService.GetLatestPriceAsync(Symbol);
                if (price > 0)
                {
                    LatestPrice = price;
                    var formattedPrice = PriceFormatConverter.FormatPrice(price);
                    StatusMessage = $"{Symbol} 价格: {formattedPrice}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"获取价格失败: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ToggleTimers()
        {
            if (_priceTimer.IsEnabled)
            {
                StopTimers();
            }
            else if (SelectedAccount != null)
            {
                StartTimers();
            }
        }
        
        [RelayCommand]
        private void ToggleAutoRefresh()
        {
            AutoRefreshEnabled = !AutoRefreshEnabled;
            if (AutoRefreshEnabled)
            {
                StatusMessage = "自动刷新已启用";
            }
            else
            {
                StatusMessage = "自动刷新已暂停 - 选择状态将保持不变";
            }
        }

        [RelayCommand]
        private void SetLeverage(object parameter)
        {
            if (parameter is string leverageStr && int.TryParse(leverageStr, out int leverage))
            {
                Leverage = leverage;
                StatusMessage = $"杠杆已设置为 {leverage}x";
            }
        }

        // 在窗口关闭时调用，清理资源
        public void Cleanup()
        {
            // 保存当前交易设置
            SaveTradingSettings();
            
            StopTimers();
        }

        // 当关键参数变化时自动保存设置
        partial void OnSideChanged(string value)
        {
            if (!_isInitializing)
            {
                SaveTradingSettings();
            }
        }
        
        partial void OnLeverageChanged(int value)
        {
            if (!_isInitializing)
            {
                SaveTradingSettings();
            }
        }
        
        partial void OnMarginTypeChanged(string value)
        {
            if (!_isInitializing)
            {
                SaveTradingSettings();
            }
        }
        
        partial void OnOrderTypeChanged(string value)
        {
            // 通知IsLimitOrder属性更新
            OnPropertyChanged(nameof(IsLimitOrder));
            
            if (!_isInitializing)
            {
                SaveTradingSettings();
            }
        }
        
        partial void OnStopLossRatioChanged(decimal value)
        {
            // 验证止损比例的合理性 (范围：0.1% - 100%)
            if (value < 0.1m)
            {
                Console.WriteLine($"⚠️ 止损比例过小({value:F2}%)，最小值为0.1%，重置为5%");
                StopLossRatio = 5.0m;
                return;
            }
            
            if (value > 100m)
            {
                Console.WriteLine($"⚠️ 止损比例过大({value:F2}%)，最大值为100%，重置为5%");
                StopLossRatio = 5.0m;
                return;
            }
            
            // 数值规范化：保留最多2位小数
            var normalizedValue = Math.Round(value, 2);
            if (normalizedValue != value)
            {
                Console.WriteLine($"🔧 止损比例精度调整: {value:F4}% → {normalizedValue:F2}%");
                StopLossRatio = normalizedValue;
                return;
            }
            
            Console.WriteLine($"✅ 止损比例设置: {value:F2}%");
            
            if (!_isInitializing)
            {
                SaveTradingSettings();
            }
        }

        [RelayCommand]
        private async Task ClearAllPositionsAndOrdersAsync()
        {
            if (SelectedAccount == null)
                return;

            // 显示确认对话框
            var result = System.Windows.MessageBox.Show(
                "确定要执行一键清仓吗？\n\n此操作将：\n• 取消所有委托单\n• 平掉所有持仓（市价单）\n\n此操作不可撤销！",
                "一键清仓确认",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            IsLoading = true;
            StatusMessage = "执行一键清仓...";
            
            var resultMessages = new List<string>();
            int totalPositions = 0;
            int successfulCloses = 0;
            int failedCloses = 0;
            int totalOrders = 0;
            int successfulCancels = 0;
            int failedCancels = 0;
            
            try
            {
                // 第一步：取消所有委托单
                StatusMessage = "正在取消所有委托单...";
                Console.WriteLine("🗑️ 第一步：取消所有委托单...");
                
                var orders = await _binanceService.GetOpenOrdersAsync();
                totalOrders = orders.Count;
                Console.WriteLine($"📊 找到 {totalOrders} 个待取消的委托单");
                
                foreach (var order in orders)
                {
                    try
                    {
                        Console.WriteLine($"🗑️ 取消订单: {order.Symbol} OrderId={order.OrderId} Type={order.Type}");
                        var cancelSuccess = await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                        if (cancelSuccess)
                        {
                            successfulCancels++;
                        }
                        else
                        {
                            failedCancels++;
                            Console.WriteLine($"❌ 取消订单失败: {order.Symbol} OrderId={order.OrderId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCancels++;
                        Console.WriteLine($"❌ 取消订单异常: {order.Symbol} OrderId={order.OrderId}, 错误: {ex.Message}");
                    }
                }
                
                resultMessages.Add($"委托单处理: {successfulCancels}/{totalOrders} 成功取消");
                Console.WriteLine($"📊 委托单取消完成: 成功 {successfulCancels} 个，失败 {failedCancels} 个");

                // 第二步：获取所有持仓并逐个平仓
                StatusMessage = "正在平掉所有持仓...";
                Console.WriteLine("\n💰 第二步：平掉所有持仓...");
                
                var positions = await _binanceService.GetPositionsAsync();
                var activePositions = positions.Where(p => Math.Abs(p.PositionAmt) > 0).ToList();
                totalPositions = activePositions.Count;
                Console.WriteLine($"📊 找到 {totalPositions} 个需要平仓的持仓");
                
                foreach (var position in activePositions)
                {
                    try
                    {
                        Console.WriteLine($"\n💰 处理持仓: {position.Symbol} 数量={position.PositionAmt:F8} 方向={position.PositionSideString}");
                        
                        // 🔧 数量精度处理 - 解决0.6等小数精度问题
                        var absoluteQuantity = Math.Abs(position.PositionAmt);
                        var adjustedQuantity = await AdjustQuantityPrecisionAsync(absoluteQuantity, position.Symbol, 0.001m, 1000000m);
                        
                        if (adjustedQuantity <= 0)
                        {
                            Console.WriteLine($"⚠️ 跳过数量过小的持仓: {position.Symbol} 原始={position.PositionAmt:F8} 调整后={adjustedQuantity:F8}");
                            continue;
                        }
                        
                        // 判断平仓方向
                        string closeSide = position.PositionAmt > 0 ? "SELL" : "BUY";
                        
                        // 创建平仓订单
                        var closeOrder = new OrderRequest
                        {
                            Symbol = position.Symbol,
                            Side = closeSide,
                            Type = "MARKET",
                            Quantity = adjustedQuantity, // 使用调整后的精度
                            PositionSide = position.PositionSideString,
                            ReduceOnly = true,
                            Leverage = position.Leverage,
                            MarginType = position.MarginType ?? "ISOLATED"
                        };
                        
                        Console.WriteLine($"📋 平仓订单: {closeOrder.Side} {closeOrder.Quantity:F8} {closeOrder.Symbol} (调整精度: {position.PositionAmt:F8} → {adjustedQuantity:F8})");
                        
                        var closeSuccess = await _binanceService.PlaceOrderAsync(closeOrder);
                        
                        if (closeSuccess)
                        {
                            successfulCloses++;
                            Console.WriteLine($"✅ 持仓平仓成功: {position.Symbol}");
                        }
                        else
                        {
                            failedCloses++;
                            Console.WriteLine($"❌ 持仓平仓失败: {position.Symbol}");
                            
                            // 尝试备选方案：减少数量重试
                            if (adjustedQuantity > 1)
                            {
                                Console.WriteLine($"🔄 尝试减少数量重试平仓: {position.Symbol}");
                                var retryQuantity = Math.Floor(adjustedQuantity * 0.9m); // 减少10%重试
                                closeOrder.Quantity = retryQuantity;
                                
                                var retrySuccess = await _binanceService.PlaceOrderAsync(closeOrder);
                                if (retrySuccess)
                                {
                                    Console.WriteLine($"✅ 重试平仓成功: {position.Symbol} 数量={retryQuantity:F8}");
                                    failedCloses--; // 修正计数
                                    successfulCloses++;
                                }
                                else
                                {
                                    Console.WriteLine($"❌ 重试平仓仍失败: {position.Symbol}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCloses++;
                        Console.WriteLine($"❌ 平仓持仓异常: {position.Symbol}, 错误: {ex.Message}");
                    }
                }
                
                resultMessages.Add($"持仓平仓: {successfulCloses}/{totalPositions} 成功平仓");
                Console.WriteLine($"📊 持仓平仓完成: 成功 {successfulCloses} 个，失败 {failedCloses} 个");

                // 第三步：刷新数据验证结果
                StatusMessage = "正在验证清仓结果...";
                Console.WriteLine("\n🔄 第三步：刷新数据验证结果...");
                await RefreshDataAsync();
                
                // 检查是否还有剩余持仓或委托单
                var remainingPositions = Positions.Where(p => Math.Abs(p.PositionAmt) > 0).ToList();
                var remainingOrders = Orders.Where(o => o.Status == "NEW").ToList();
                
                if (remainingPositions.Any() || remainingOrders.Any())
                {
                    resultMessages.Add($"剩余: {remainingPositions.Count} 个持仓, {remainingOrders.Count} 个委托单");
                    Console.WriteLine($"⚠️ 发现剩余: {remainingPositions.Count} 个持仓, {remainingOrders.Count} 个委托单");
                    
                    if (remainingPositions.Any())
                    {
                        Console.WriteLine("🔍 剩余持仓详情:");
                        foreach (var pos in remainingPositions)
                        {
                            Console.WriteLine($"   {pos.Symbol}: {pos.PositionAmt:F8} ({pos.PositionSideString})");
                        }
                    }
                }
                
                // 生成最终状态消息
                string finalStatus;
                if (failedCloses == 0 && failedCancels == 0)
                {
                    finalStatus = "一键清仓完全成功！";
                }
                else if (remainingPositions.Any() || remainingOrders.Any())
                {
                    finalStatus = "一键清仓操作部分完成，可能存在以下情况：\n• 部分委托单取消失败\n• 部分持仓平仓失败\n请手动检查并处理剩余仓位";
                }
                else
                {
                    finalStatus = "一键清仓基本完成，建议验证";
                }
                
                StatusMessage = finalStatus;
                
                // 显示详细结果
                var detailMessage = string.Join("\n", resultMessages);
                Console.WriteLine($"\n🏁 一键清仓操作完成");
                Console.WriteLine($"📊 最终结果: {detailMessage}");
                
                System.Windows.MessageBox.Show(
                    $"{finalStatus}\n\n详细结果:\n{detailMessage}",
                    "一键清仓结果",
                    System.Windows.MessageBoxButton.OK,
                    (failedCloses > 0 || failedCancels > 0 || remainingPositions.Any() || remainingOrders.Any()) ? 
                        System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 一键清仓异常: {ex.Message}");
                StatusMessage = $"一键清仓失败: {ex.Message}";
                
                System.Windows.MessageBox.Show(
                    $"一键清仓操作发生异常:\n\n{ex.Message}\n\n请手动检查持仓和委托单状态。",
                    "清仓异常",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // === 订单选择相关命令 ===
        
        [RelayCommand]
        private void SelectAllOrders()
        {
            try
            {
                Console.WriteLine($"🔲 全选订单操作...");
                foreach (var order in FilteredOrders)
                {
                    order.IsSelected = true;
                }
                
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));
                OnPropertyChanged(nameof(HasSelectedStopOrders));
                OnPropertyChanged(nameof(SelectedStopOrderCount));
                
                StatusMessage = $"已全选 {FilteredOrders.Count} 个订单";
                Console.WriteLine($"✅ 全选完成: {FilteredOrders.Count} 个订单");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 全选订单异常: {ex.Message}");
                StatusMessage = $"全选订单失败: {ex.Message}";
            }
        }
        
        [RelayCommand]
        private void UnselectAllOrders()
        {
            try
            {
                Console.WriteLine($"☐ 取消全选订单操作...");
                foreach (var order in FilteredOrders)
                {
                    order.IsSelected = false;
                }
                
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));
                OnPropertyChanged(nameof(HasSelectedStopOrders));
                OnPropertyChanged(nameof(SelectedStopOrderCount));
                
                StatusMessage = $"已取消选择所有订单";
                Console.WriteLine($"✅ 取消全选完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 取消全选订单异常: {ex.Message}");
                StatusMessage = $"取消全选订单失败: {ex.Message}";
            }
        }
        
        [RelayCommand]
        private void InvertOrderSelection()
        {
            try
            {
                Console.WriteLine($"🔄 反选订单操作...");
                foreach (var order in FilteredOrders)
                {
                    order.IsSelected = !order.IsSelected;
                }
                
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));
                OnPropertyChanged(nameof(HasSelectedStopOrders));
                OnPropertyChanged(nameof(SelectedStopOrderCount));
                
                var selectedCount = FilteredOrders.Count(o => o.IsSelected);
                StatusMessage = $"反选完成，当前选中 {selectedCount} 个订单";
                Console.WriteLine($"✅ 反选完成: 当前选中 {selectedCount} 个订单");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 反选订单异常: {ex.Message}");
                StatusMessage = $"反选订单失败: {ex.Message}";
            }
        }
        
        [RelayCommand]
        private async Task CancelSelectedOrdersAsync()
        {
            try
            {
                var selectedOrders = FilteredOrders.Where(o => o.IsSelected).ToList();
                
                if (!selectedOrders.Any())
                {
                    StatusMessage = "请先选择要取消的订单";
                    System.Windows.MessageBox.Show(
                        "请先勾选要取消的订单",
                        "未选择订单",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }
                
                var result = System.Windows.MessageBox.Show(
                    $"确定要取消选中的 {selectedOrders.Count} 个订单吗？\n\n此操作不可撤销！",
                    "取消订单确认",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return;

                IsLoading = true;
                StatusMessage = $"正在取消 {selectedOrders.Count} 个选中的订单...";
                
                int successCount = 0;
                int failedCount = 0;
                
                foreach (var order in selectedOrders)
                {
                    try
                    {
                        Console.WriteLine($"🗑️ 取消订单: {order.OrderId} {order.Symbol}");
                        var success = await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                        
                        if (success)
                        {
                            successCount++;
                            Console.WriteLine($"✅ 订单取消成功: {order.OrderId}");
                        }
                        else
                        {
                            failedCount++;
                            Console.WriteLine($"❌ 订单取消失败: {order.OrderId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        Console.WriteLine($"❌ 取消订单异常: {order.OrderId}, {ex.Message}");
                    }
                }
                
                StatusMessage = $"订单取消完成: 成功 {successCount} 个，失败 {failedCount} 个";
                
                System.Windows.MessageBox.Show(
                    $"订单取消操作完成！\n\n✅ 成功取消: {successCount} 个\n❌ 取消失败: {failedCount} 个",
                    "取消结果",
                    System.Windows.MessageBoxButton.OK,
                    failedCount > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
                
                // 刷新数据
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"取消订单异常: {ex.Message}";
                Console.WriteLine($"❌ 取消选中订单异常: {ex.Message}");
                
                System.Windows.MessageBox.Show(
                    $"取消订单时发生异常：\n\n{ex.Message}",
                    "操作异常",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private async Task AddBreakEvenStopLossForSelectedOrdersAsync()
        {
            Console.WriteLine("🛡️ 开始为选中订单添加保本止损...");
            try
            {
                var selectedOrders = FilteredOrders.Where(o => o.IsSelected).ToList();
                Console.WriteLine($"选中订单数量: {selectedOrders.Count}");
                
                if (!selectedOrders.Any())
                {
                    Console.WriteLine("❌ 未选择任何订单");
                    StatusMessage = "请先选择要添加保本止损的订单";
                    System.Windows.MessageBox.Show(
                        "请先勾选要添加保本止损的订单",
                        "未选择订单",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }
                
                // 只处理限价买入/卖出单
                var validOrders = selectedOrders.Where(o => 
                    o.Type == "LIMIT" && 
                    (o.Side == "BUY" || o.Side == "SELL") && 
                    o.Price > 0).ToList();
                
                Console.WriteLine($"有效限价单数量: {validOrders.Count}");
                foreach (var order in validOrders)
                {
                    Console.WriteLine($"📋 有效订单: OrderId={order.OrderId}, Symbol={order.Symbol}, Side={order.Side}, Quantity={order.OrigQty}, Price={order.Price}");
                }
                
                if (!validOrders.Any())
                {
                    Console.WriteLine("❌ 选中的订单中没有有效的限价单");
                    StatusMessage = "选中的订单中没有有效的限价单";
                    System.Windows.MessageBox.Show(
                        "只能为限价买入/卖出单添加保本止损！\n\n当前选中的订单中没有符合条件的订单。",
                        "订单类型无效",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }
                
                var result = System.Windows.MessageBox.Show(
                    $"确定要为选中的 {validOrders.Count} 个限价单添加保本止损吗？\n\n" +
                    $"将为每个订单设置以开仓价为触发价的止损单。\n\n此操作不可撤销！",
                    "添加保本止损确认",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    Console.WriteLine("🚫 用户取消了保本止损操作");
                    return;
                }

                IsLoading = true;
                StatusMessage = $"正在为 {validOrders.Count} 个订单添加保本止损...";
                Console.WriteLine($"🚀 开始处理 {validOrders.Count} 个有效订单...");
                
                int successCount = 0;
                int failedCount = 0;
                
                foreach (var order in validOrders)
                {
                    try
                    {
                        Console.WriteLine($"\n🛡️ 处理订单: OrderId={order.OrderId}");
                        Console.WriteLine($"📊 订单详情: Symbol={order.Symbol}, Side={order.Side}, Price={order.Price}, Quantity={order.OrigQty}");
                        Console.WriteLine($"📍 PositionSide={order.PositionSide}");
                        
                        // 构建保本止损单 - 根据币安API要求
                        var stopLossOrder = new OrderRequest
                        {
                            Symbol = order.Symbol,
                            Side = order.Side == "BUY" ? "SELL" : "BUY", // 反向操作
                            PositionSide = order.PositionSide,
                            Type = "STOP_MARKET", // 市价止损单
                            StopPrice = order.Price, // 触发价=订单价格（保本价）
                            WorkingType = "CONTRACT_PRICE", // 使用合约价格触发
                            ReduceOnly = true // 只减仓
                        };

                        Console.WriteLine($"🔨 构建止损单参数:");
                        Console.WriteLine($"   Symbol: {stopLossOrder.Symbol}");
                        Console.WriteLine($"   Side: {stopLossOrder.Side} (原订单{order.Side}的反向)");
                        Console.WriteLine($"   Type: {stopLossOrder.Type}");
                        Console.WriteLine($"   原始StopPrice: {stopLossOrder.StopPrice}");
                        
                        // 根据合约调整价格精度
                        var adjustedStopPrice = AdjustPricePrecision(stopLossOrder.StopPrice, order.Symbol);
                        stopLossOrder.StopPrice = adjustedStopPrice;
                        
                        Console.WriteLine($"   调整后StopPrice: {stopLossOrder.StopPrice} (触发价=保本价)");
                        Console.WriteLine($"   PositionSide: {stopLossOrder.PositionSide}");
                        Console.WriteLine($"   WorkingType: {stopLossOrder.WorkingType}");
                        Console.WriteLine($"   ReduceOnly: {stopLossOrder.ReduceOnly}");
                        Console.WriteLine($"   注意: STOP_MARKET不需要Quantity参数");

                        Console.WriteLine($"📤 开始下单...");
                        var success = await _binanceService.PlaceOrderAsync(stopLossOrder);
                        
                        if (success)
                        {
                            successCount++;
                            Console.WriteLine($"✅ 保本止损添加成功: {order.Symbol} OrderId={order.OrderId} @ {order.Price}");
                            
                            // 清理该合约的冲突止损委托
                            try
                            {
                                var isLong = order.Side == "BUY";
                                await CleanupConflictingStopOrdersAsync(order.Symbol, order.Price, isLong);
                                Console.WriteLine($"🧹 已清理 {order.Symbol} 的冲突止损委托");
                            }
                            catch (Exception cleanupEx)
                            {
                                Console.WriteLine($"⚠️ 清理 {order.Symbol} 冲突委托时异常: {cleanupEx.Message}");
                            }
                        }
                        else
                        {
                            failedCount++;
                            Console.WriteLine($"❌ 保本止损添加失败: {order.Symbol} OrderId={order.OrderId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        Console.WriteLine($"❌ 添加保本止损异常: {order.Symbol} OrderId={order.OrderId}");
                        Console.WriteLine($"   异常信息: {ex.Message}");
                        Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
                    }
                }
                
                StatusMessage = $"保本止损添加完成: 成功 {successCount} 个，失败 {failedCount} 个";
                Console.WriteLine($"\n🏁 批量保本止损完成: 成功 {successCount} 个，失败 {failedCount} 个");
                
                System.Windows.MessageBox.Show(
                    $"保本止损添加操作完成！\n\n✅ 成功添加: {successCount} 个\n❌ 添加失败: {failedCount} 个",
                    "操作结果",
                    System.Windows.MessageBoxButton.OK,
                    failedCount > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
                
                // 刷新数据
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"批量添加保本止损异常: {ex.Message}";
                Console.WriteLine($"❌ 批量添加保本止损顶层异常: {ex.Message}");
                Console.WriteLine($"   异常堆栈: {ex.StackTrace}");
                
                System.Windows.MessageBox.Show(
                    $"批量添加保本止损时发生异常：\n\n{ex.Message}",
                    "操作异常",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                Console.WriteLine("🏁 保本止损操作完成");
            }
        }
        
        // === 持仓选择相关命令 ===
        
        [RelayCommand]
        private void SelectAllPositions()
        {
            try
            {
                Console.WriteLine($"🔲 全选持仓操作...");
                foreach (var position in Positions)
                {
                    position.IsSelected = true;
                }
                
                OnPropertyChanged(nameof(SelectedPositions));
                OnPropertyChanged(nameof(HasSelectedPositions));
                OnPropertyChanged(nameof(SelectedPositionCount));
                
                StatusMessage = $"已全选 {Positions.Count} 个持仓";
                Console.WriteLine($"✅ 持仓全选完成: {Positions.Count} 个持仓");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 全选持仓异常: {ex.Message}");
                StatusMessage = $"全选持仓失败: {ex.Message}";
            }
        }
        
        [RelayCommand]
        private void UnselectAllPositions()
        {
            try
            {
                Console.WriteLine($"☐ 取消全选持仓操作...");
                foreach (var position in Positions)
                {
                    position.IsSelected = false;
                }
                
                OnPropertyChanged(nameof(SelectedPositions));
                OnPropertyChanged(nameof(HasSelectedPositions));
                OnPropertyChanged(nameof(SelectedPositionCount));
                
                StatusMessage = $"已取消选择所有持仓";
                Console.WriteLine($"✅ 取消持仓全选完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 取消全选持仓异常: {ex.Message}");
                StatusMessage = $"取消全选持仓失败: {ex.Message}";
            }
        }
        
        [RelayCommand]
        private void InvertPositionSelection()
        {
            try
            {
                Console.WriteLine($"🔄 反选持仓操作...");
                foreach (var position in Positions)
                {
                    position.IsSelected = !position.IsSelected;
                }
                
                OnPropertyChanged(nameof(SelectedPositions));
                OnPropertyChanged(nameof(HasSelectedPositions));
                OnPropertyChanged(nameof(SelectedPositionCount));
                
                var selectedCount = Positions.Count(p => p.IsSelected);
                StatusMessage = $"持仓反选完成，当前选中 {selectedCount} 个持仓";
                Console.WriteLine($"✅ 持仓反选完成: 当前选中 {selectedCount} 个持仓");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 反选持仓异常: {ex.Message}");
                StatusMessage = $"反选持仓失败: {ex.Message}";
            }
        }
        
        [RelayCommand]
        private async Task CloseSelectedPositionsAsync()
        {
            try
            {
                var selectedPositions = Positions.Where(p => p.IsSelected).ToList();
                
                if (!selectedPositions.Any())
                {
                    StatusMessage = "请先选择要平仓的持仓";
                    System.Windows.MessageBox.Show(
                        "请先勾选要平仓的持仓",
                        "未选择持仓",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }
                
                var result = System.Windows.MessageBox.Show(
                    $"确定要平掉选中的 {selectedPositions.Count} 个持仓吗？\n\n此操作将市价平仓，不可撤销！",
                    "批量平仓确认",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return;

                IsLoading = true;
                StatusMessage = $"正在平仓 {selectedPositions.Count} 个选中的持仓...";
                
                int successCount = 0;
                int failedCount = 0;
                
                foreach (var position in selectedPositions)
                {
                    try
                    {
                        Console.WriteLine($"📤 平仓持仓: {position.Symbol} {position.PositionAmt}");
                        var success = await _binanceService.ClosePositionAsync(position.Symbol, position.PositionSideString);
                        
                        if (success)
                        {
                            successCount++;
                            Console.WriteLine($"✅ 持仓平仓成功: {position.Symbol}");
                        }
                        else
                        {
                            failedCount++;
                            Console.WriteLine($"❌ 持仓平仓失败: {position.Symbol}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        Console.WriteLine($"❌ 平仓持仓异常: {position.Symbol}, {ex.Message}");
                    }
                }
                
                StatusMessage = $"批量平仓完成: 成功 {successCount} 个，失败 {failedCount} 个";
                
                System.Windows.MessageBox.Show(
                    $"批量平仓操作完成！\n\n✅ 成功平仓: {successCount} 个\n❌ 平仓失败: {failedCount} 个",
                    "平仓结果",
                    System.Windows.MessageBoxButton.OK,
                    failedCount > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
                
                // 刷新数据
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"批量平仓异常: {ex.Message}";
                Console.WriteLine($"❌ 批量平仓异常: {ex.Message}");
                
                System.Windows.MessageBox.Show(
                    $"批量平仓时发生异常：\n\n{ex.Message}",
                    "操作异常",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private async Task AddBreakEvenStopLossForSelectedPositionsAsync()
        {
            try
            {
                var selectedPositions = Positions.Where(p => p.IsSelected && Math.Abs(p.PositionAmt) > 0).ToList();
                
                if (!selectedPositions.Any())
                {
                    StatusMessage = "请先选择要添加保本止损的持仓";
                    System.Windows.MessageBox.Show(
                        "请先勾选要添加保本止损的持仓",
                        "未选择持仓",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }
                
                var result = System.Windows.MessageBox.Show(
                    $"确定要为选中的 {selectedPositions.Count} 个持仓添加保本止损吗？\n\n" +
                    $"将为每个持仓设置以开仓价为触发价的止损单。\n\n此操作不可撤销！",
                    "批量保本止损确认",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return;

                IsLoading = true;
                StatusMessage = $"正在为 {selectedPositions.Count} 个持仓添加保本止损...";
                
                int successCount = 0;
                int failedCount = 0;
                
                foreach (var position in selectedPositions)
                {
                    try
                    {
                        Console.WriteLine($"🛡️ 为持仓添加保本止损: {position.Symbol} {position.PositionAmt}");
                        
                        // 构建保本止损单
                        var stopLossOrder = new OrderRequest
                        {
                            Symbol = position.Symbol,
                            Side = position.PositionAmt > 0 ? "SELL" : "BUY", // 反向操作
                            PositionSide = position.PositionSideString,
                            Type = "STOP_MARKET", // 市价止损单
                            Quantity = Math.Abs(position.PositionAmt), // 相同数量
                            StopPrice = position.EntryPrice, // 触发价=开仓价
                            ReduceOnly = true, // 只减仓
                            Leverage = position.Leverage,
                            MarginType = position.MarginType,
                            WorkingType = "CONTRACT_PRICE" // 使用合约价格触发
                        };

                        Console.WriteLine($"📋 止损单详情: {stopLossOrder.Side} {stopLossOrder.Quantity:F6} {stopLossOrder.Symbol} @ {PriceFormatConverter.FormatPrice(stopLossOrder.StopPrice)}");

                        var success = await _binanceService.PlaceOrderAsync(stopLossOrder);
                        
                        if (success)
                        {
                            successCount++;
                            Console.WriteLine($"✅ 保本止损添加成功: {position.Symbol} @ {position.EntryPrice}");
                            
                            // 清理该合约的冲突止损委托
                            try
                            {
                                var isLong = position.PositionAmt > 0;
                                await CleanupConflictingStopOrdersAsync(position.Symbol, position.EntryPrice, isLong);
                                Console.WriteLine($"🧹 已清理 {position.Symbol} 的冲突止损委托");
                            }
                            catch (Exception cleanupEx)
                            {
                                Console.WriteLine($"⚠️ 清理 {position.Symbol} 冲突委托时异常: {cleanupEx.Message}");
                            }
                        }
                        else
                        {
                            failedCount++;
                            Console.WriteLine($"❌ 保本止损添加失败: {position.Symbol}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        Console.WriteLine($"❌ 添加保本止损异常: {position.Symbol}, {ex.Message}");
                    }
                }
                
                StatusMessage = $"保本止损添加完成: 成功 {successCount} 个，失败 {failedCount} 个";
                
                System.Windows.MessageBox.Show(
                    $"保本止损添加操作完成！\n\n✅ 成功添加: {successCount} 个\n❌ 添加失败: {failedCount} 个",
                    "操作结果",
                    System.Windows.MessageBoxButton.OK,
                    failedCount > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
                
                // 刷新数据
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"批量添加保本止损异常: {ex.Message}";
                Console.WriteLine($"❌ 批量添加保本止损异常: {ex.Message}");
                
                System.Windows.MessageBox.Show(
                    $"批量添加保本止损时发生异常：\n\n{ex.Message}",
                    "操作异常",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private void OpenLogFile()
        {
            try
            {
                var logFilePath = LogService.GetLogFilePath();
                LogService.LogInfo($"打开日志文件: {logFilePath}");
                
                // 使用默认程序打开日志文件
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logFilePath,
                    UseShellExecute = true
                });
                
                StatusMessage = "已打开日志文件";
            }
            catch (Exception ex)
            {
                LogService.LogError($"打开日志文件失败", ex);
                StatusMessage = $"打开日志文件失败: {ex.Message}";
                
                // 显示日志文件路径让用户手动打开
                var logFilePath = LogService.GetLogFilePath();
                System.Windows.MessageBox.Show(
                    $"无法自动打开日志文件，请手动打开：\n\n{logFilePath}",
                    "打开日志文件",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
        
        [RelayCommand]
        private void ClearLogFile()
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "确定要清空日志文件吗？\n\n清空后将无法查看之前的日志记录。",
                    "清空日志确认",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    LogService.ClearLogFile();
                    StatusMessage = "日志文件已清空";
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"清空日志文件失败", ex);
                StatusMessage = $"清空日志文件失败: {ex.Message}";
            }
        }

        private decimal AdjustPricePrecision(decimal price, string symbol)
        {
            // 根据不同合约调整价格精度
            var adjustedPrice = symbol.ToUpper() switch
            {
                "BTCUSDT" => Math.Round(price, 1),    // BTC: 1位小数 (如 45000.1)
                "ETHUSDT" => Math.Round(price, 2),    // ETH: 2位小数 (如 2800.25)
                "BNBUSDT" => Math.Round(price, 3),    // BNB: 3位小数 (如 320.125)
                "ADAUSDT" => Math.Round(price, 4),    // ADA: 4位小数 (如 0.5234)
                "DOGEUSDT" => Math.Round(price, 5),   // DOGE: 5位小数 (如 0.08123)
                "SOLUSDT" => Math.Round(price, 3),    // SOL: 3位小数
                "DOTUSDT" => Math.Round(price, 3),    // DOT: 3位小数
                "LINKUSDT" => Math.Round(price, 3),   // LINK: 3位小数
                "LTCUSDT" => Math.Round(price, 2),    // LTC: 2位小数
                "BCHUSDT" => Math.Round(price, 2),    // BCH: 2位小数
                "XRPUSDT" => Math.Round(price, 4),    // XRP: 4位小数
                "MATICUSDT" => Math.Round(price, 4),  // MATIC: 4位小数
                "AVAXUSDT" => Math.Round(price, 3),   // AVAX: 3位小数
                "UNIUSDT" => Math.Round(price, 3),    // UNI: 3位小数
                "ATOMUSDT" => Math.Round(price, 3),   // ATOM: 3位小数
                _ => Math.Round(price, 4) // 默认: 4位小数
            };
            
            Console.WriteLine($"🎯 价格精度调整: {symbol} {price:F8} → {adjustedPrice:F8}");
            return adjustedPrice;
        }

        [RelayCommand]
        private async Task CheckOrderHistoryAsync()
        {
            if (SelectedAccount == null)
            {
                StatusMessage = "请先选择账户";
                return;
            }

            Console.WriteLine("\n🔍 开始检查订单历史，寻找丢失的止损单...");
            
            IsLoading = true;
            StatusMessage = "正在查询订单历史...";
            
            try
            {
                // 获取最近50条订单历史
                var allOrders = await _binanceService.GetAllOrdersAsync(Symbol, 50);
                
                Console.WriteLine($"📊 获取到 {allOrders.Count} 条历史订单");
                
                // 筛选STOP_MARKET订单
                var stopMarketOrders = allOrders.Where(o => o.Type == "STOP_MARKET").ToList();
                Console.WriteLine($"🛡️ 历史中的STOP_MARKET订单: {stopMarketOrders.Count} 个");
                
                if (stopMarketOrders.Any())
                {
                    Console.WriteLine("\n📋 止损单详细信息:");
                    foreach (var order in stopMarketOrders.OrderByDescending(o => o.UpdateTime))
                    {
                        var statusEmoji = order.Status switch
                        {
                            "FILLED" => "✅",
                            "CANCELED" => "❌", 
                            "EXPIRED" => "⏰",
                            "NEW" => "🆕",
                            _ => "❓"
                        };
                        
                        Console.WriteLine($"   {statusEmoji} OrderId: {order.OrderId}");
                        Console.WriteLine($"      合约: {order.Symbol}");
                        Console.WriteLine($"      方向: {order.Side}");
                        Console.WriteLine($"      状态: {order.Status}");
                        Console.WriteLine($"      触发价: {PriceFormatConverter.FormatPrice(order.StopPrice)}");
                        Console.WriteLine($"      数量: {order.OrigQty} (已执行: {order.ExecutedQty})");
                        Console.WriteLine($"      创建时间: {order.Time:yyyy-MM-dd HH:mm:ss}");
                        Console.WriteLine($"      更新时间: {order.UpdateTime:yyyy-MM-dd HH:mm:ss}");
                        Console.WriteLine();
                    }
                    
                    // 统计各种状态
                    var statusStats = stopMarketOrders.GroupBy(o => o.Status)
                        .ToDictionary(g => g.Key, g => g.Count());
                    
                    var statsMessage = "止损单状态统计:\n";
                    foreach (var stat in statusStats)
                    {
                        var emoji = stat.Key switch
                        {
                            "FILLED" => "✅ 已执行",
                            "CANCELED" => "❌ 已取消",
                            "EXPIRED" => "⏰ 已过期",
                            "NEW" => "🆕 未成交",
                            _ => "❓ 其他"
                        };
                        statsMessage += $"  {emoji}: {stat.Value} 个\n";
                    }
                    
                    Console.WriteLine($"📊 {statsMessage}");
                    StatusMessage = $"找到 {stopMarketOrders.Count} 个止损单历史记录";
                    
                    // 显示结果对话框
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show(
                            $"订单历史查询完成！\n\n找到 {stopMarketOrders.Count} 个止损单历史记录\n\n{statsMessage}\n请查看控制台了解详细信息。",
                            "订单历史查询结果",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    });
                }
                else
                {
                    Console.WriteLine("🤔 历史记录中没有找到STOP_MARKET订单");
                    StatusMessage = "历史记录中没有找到止损单";
                    
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show(
                            "历史记录中没有找到止损单！\n\n可能的原因:\n" +
                            "• 订单还没有被创建\n" +
                            "• 订单在更早的历史中\n" +
                            "• API配置问题\n\n" +
                            "建议:\n1. 检查API配置是否正确\n2. 尝试重新下单\n3. 查看完整的交易历史",
                            "未找到止损单",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    });
                }
                
                // 同时显示最近的几个订单作为参考
                var recentOrders = allOrders.OrderByDescending(o => o.UpdateTime).Take(10).ToList();
                Console.WriteLine($"\n📋 最近10个订单（作为参考）:");
                foreach (var order in recentOrders)
                {
                    Console.WriteLine($"   OrderId: {order.OrderId}, Type: {order.Type}, Status: {order.Status}, Symbol: {order.Symbol}, Time: {order.UpdateTime:MM-dd HH:mm:ss}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 查询订单历史异常: {ex.Message}");
                StatusMessage = $"查询订单历史失败: {ex.Message}";
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show(
                        $"查询订单历史时发生异常:\n\n{ex.Message}",
                        "查询异常",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 获取交易规则信息，使用真实API
        private async Task<(decimal minQuantity, decimal maxQuantity, int maxLeverage, decimal maxNotional, decimal estimatedPrice)> GetExchangeInfoAsync(string symbol)
        {
            Console.WriteLine($"🔍 准备获取 {symbol} 的交易规则...");
            
            try
            {
                // 获取当前价格
                var currentPrice = await _binanceService.GetLatestPriceAsync(symbol);
                
                if (currentPrice <= 0)
                {
                    Console.WriteLine($"❌ 获取 {symbol} 价格失败");
                    throw new Exception("无法获取价格");
                }
                
                // 使用动态计算的交易规则
                var (minQuantity, maxQuantity, maxLeverage, maxNotional, estimatedPrice) = GetDynamicLimits(currentPrice);
                
                Console.WriteLine($"✅ 成功计算 {symbol} 的交易规则");
                Console.WriteLine($"📦 数量范围: {minQuantity} - {maxQuantity}");
                Console.WriteLine($"🎚️ 最大杠杆: {maxLeverage}x");
                Console.WriteLine($"💵 最大名义价值: {maxNotional}");
                
                // 缓存最新价格到服务中
                _binanceService.UpdateLatestPriceCache(symbol, currentPrice);
                
                return (minQuantity, maxQuantity, maxLeverage, maxNotional, currentPrice);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取交易规则失败: {ex.Message}");
                Console.WriteLine($"⚠️ 将使用备选方案...");
                
                // 如果失败，使用备选方案
                return await GetFallbackExchangeInfoAsync(symbol);
            }
        }

        // 备选交易规则方案（仅在真实API失败时使用）
        private async Task<(decimal minQuantity, decimal maxQuantity, int maxLeverage, decimal maxNotional, decimal estimatedPrice)> GetFallbackExchangeInfoAsync(string symbol)
        {
            Console.WriteLine($"⚠️ 使用备选交易规则方案: {symbol}");
            
            // 获取当前价格
            var currentPrice = await _binanceService.GetLatestPriceAsync(symbol);
            
            decimal minQuantity, maxQuantity;
            int maxLeverage = 20;
            decimal maxNotional = 100000m;

            // 根据价格动态调整限制
            if (currentPrice >= 1000m) // 高价币（如BTC）
            {
                minQuantity = 0.001m;
                maxQuantity = 1000m;  // 更保守的最大值
                maxLeverage = 125;
                maxNotional = 2000000m;
            }
            else if (currentPrice >= 100m) // 中高价币（如ETH）
            {
                minQuantity = 0.001m;
                maxQuantity = 10000m;
                maxLeverage = 100;
                maxNotional = 1000000m;
            }
            else if (currentPrice >= 10m) // 中价币（如BNB）
            {
                minQuantity = 0.01m;
                maxQuantity = 100000m;
                maxLeverage = 75;
                maxNotional = 500000m;
            }
            else if (currentPrice >= 1m) // 一般价币（如DOT）
            {
                minQuantity = 0.1m;
                maxQuantity = 1000000m;
                maxLeverage = 75;
                maxNotional = 200000m;
            }
            else if (currentPrice >= 0.1m) // 低价币（如ADA）
            {
                minQuantity = 1m;
                maxQuantity = 10000000m;  // 使用更大的最大值以适应真实交易需求
                maxLeverage = 75;
                maxNotional = 100000m;
            }
            else if (currentPrice >= 0.01m) // 很低价币（如DOGE）
            {
                minQuantity = 10m;
                maxQuantity = 100000000m;
                maxLeverage = 50;
                maxNotional = 100000m;
            }
            else // 超低价币（如PEPE、SHIB等）
            {
                minQuantity = 1000m;
                maxQuantity = 10000000000m;  // 超低价币需要极大的数量
                maxLeverage = 25;
                maxNotional = 25000m;
            }

            Console.WriteLine($"📋 备选规则结果: 价格={currentPrice:F6}, 数量范围={minQuantity}-{maxQuantity}, 杠杆={maxLeverage}x");
            
            return (minQuantity, maxQuantity, maxLeverage, maxNotional, currentPrice);
        }

        [RelayCommand]
        private async Task QueryContractInfoAsync()
        {
            if (string.IsNullOrEmpty(Symbol) || SelectedAccount == null)
            {
                StatusMessage = "请输入合约名称并选择账户";
                return;
            }

            Console.WriteLine($"🔍 开始查询合约信息: {Symbol}");
            
            IsLoading = true;
            StatusMessage = $"正在查询 {Symbol} 的合约信息...";
            
            try
            {
                // 第一步：获取最新价格
                Console.WriteLine($"📊 步骤1: 获取 {Symbol} 的最新价格...");
                var newPrice = await _binanceService.GetLatestPriceAsync(Symbol);
                
                if (newPrice > 0)
                {
                    var oldPrice = LatestPrice;
                    LatestPrice = newPrice;
                    var formattedPrice = PriceFormatConverter.FormatPrice(newPrice);
                    Console.WriteLine($"✅ 价格更新: {Symbol} = {formattedPrice}");
                    
                    // 更新价格缓存到服务中
                    _binanceService.UpdateLatestPriceCache(Symbol, newPrice);
                }
                else
                {
                    Console.WriteLine($"❌ 获取 {Symbol} 价格失败");
                    StatusMessage = $"获取 {Symbol} 价格失败，请检查合约名称是否正确";
                    return;
                }

                // 第二步：获取交易规则信息
                Console.WriteLine($"📋 步骤2: 获取 {Symbol} 的交易规则...");
                var (minQuantity, maxQuantity, maxLeverage, maxNotional, estimatedPrice) = await GetExchangeInfoAsync(Symbol);
                
                Console.WriteLine($"✅ 交易规则获取成功:");
                Console.WriteLine($"   📦 数量范围: {minQuantity} - {maxQuantity}");
                Console.WriteLine($"   🎚️ 最大杠杆: {maxLeverage}x");
                Console.WriteLine($"   💵 最大名义价值: {maxNotional}");

                // 第三步：获取该合约的持仓信息
                Console.WriteLine($"📈 步骤3: 刷新 {Symbol} 相关的持仓和订单...");
                var positions = await _binanceService.GetPositionsAsync();
                var contractPosition = positions.FirstOrDefault(p => p.Symbol == Symbol && Math.Abs(p.PositionAmt) > 0);
                
                if (contractPosition != null)
                {
                    Console.WriteLine($"✅ 找到 {Symbol} 的持仓: {contractPosition.PositionAmt}, 开仓价: {contractPosition.EntryPrice}");
                    
                    // 自动选择该持仓
                    SelectedPosition = contractPosition;
                    
                    // 更新持仓列表
                    Positions.Clear();
                    foreach (var position in positions)
                    {
                        if (position.Symbol == Symbol && Math.Abs(position.PositionAmt) > 0)
                        {
                            position.IsSelected = true; // 自动选中该合约的持仓
                        }
                        Positions.Add(position);
                    }
                    
                    // 过滤显示该合约的订单
                    FilterOrdersForPosition(Symbol);
                }
                else
                {
                    Console.WriteLine($"ℹ️ {Symbol} 当前无持仓");
                }

                // 第四步：获取该合约的订单信息
                var orders = await _binanceService.GetOpenOrdersAsync(Symbol);
                Console.WriteLine($"📋 找到 {Symbol} 的订单: {orders.Count} 个");
                
                // 更新过滤的订单列表
                FilteredOrders.Clear();
                foreach (var order in orders)
                {
                    FilteredOrders.Add(order);
                }

                // 第五步：如果有止损比例，重新计算止损价
                if (StopLossRatio > 0)
                {
                    Console.WriteLine($"🎯 步骤4: 重新计算 {Symbol} 的止损价...");
                    CalculateStopLossPrice();
                }

                // 第六步：更新建议的杠杆设置
                if (maxLeverage > 0 && Leverage > maxLeverage)
                {
                    Console.WriteLine($"⚠️ 当前杠杆 {Leverage}x 超过 {Symbol} 最大杠杆 {maxLeverage}x，自动调整");
                    Leverage = Math.Min(maxLeverage, 20); // 设置为最大杠杆或20x，取较小值
                }

                // 强制刷新UI属性
                OnPropertyChanged(nameof(CanPlaceOrder));
                OnPropertyChanged(nameof(SelectedPositions));
                OnPropertyChanged(nameof(HasSelectedPositions));
                OnPropertyChanged(nameof(SelectedPositionCount));
                OnPropertyChanged(nameof(SelectedOrders));
                OnPropertyChanged(nameof(HasSelectedOrders));
                OnPropertyChanged(nameof(SelectedOrderCount));

                // 在状态栏显示简洁的合约信息
                var positionInfo = contractPosition != null ? $"持仓{contractPosition.PositionAmt}" : "无持仓";
                StatusMessage = $"{Symbol}: {PriceFormatConverter.FormatPrice(LatestPrice)} | {positionInfo} | 委托{orders.Count}个 | 最大杠杆{maxLeverage}x - {DateTime.Now:HH:mm:ss}";
                
                // 添加到最近合约列表
                AddToRecentContracts(Symbol);
                
                Console.WriteLine($"🎉 合约信息查询完成: {Symbol}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 查询合约信息异常: {ex.Message}");
                StatusMessage = $"查询 {Symbol} 合约信息失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CancelSelectedStopOrdersAsync()
        {
            try
            {
                // 筛选出选中的止损单（STOP_MARKET类型）
                var selectedStopOrders = FilteredOrders.Where(o => o.IsSelected && o.Type == "STOP_MARKET").ToList();
                
                if (!selectedStopOrders.Any())
                {
                    StatusMessage = "请先选择止损委托单";
                    System.Windows.MessageBox.Show(
                        "请先勾选要取消的止损委托单（类型为STOP_MARKET）",
                        "未选择止损单",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }
                
                var result = System.Windows.MessageBox.Show(
                    $"确定要取消选中的 {selectedStopOrders.Count} 个止损委托单吗？\n\n" +
                    $"这些止损单取消后将失去风险保护，请确认操作！",
                    "取消止损单确认",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return;

                IsLoading = true;
                StatusMessage = $"正在取消 {selectedStopOrders.Count} 个选中的止损单...";
                
                int successCount = 0;
                int failedCount = 0;
                
                Console.WriteLine($"🛡️ 开始取消 {selectedStopOrders.Count} 个止损委托单...");
                
                foreach (var order in selectedStopOrders)
                {
                    try
                    {
                        Console.WriteLine($"🗑️ 取消止损单: OrderId={order.OrderId}, Symbol={order.Symbol}, StopPrice={order.StopPrice}");
                        var success = await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                        
                        if (success)
                        {
                            successCount++;
                            Console.WriteLine($"✅ 止损单取消成功: OrderId={order.OrderId}");
                        }
                        else
                        {
                            failedCount++;
                            Console.WriteLine($"❌ 止损单取消失败: OrderId={order.OrderId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        Console.WriteLine($"❌ 取消止损单异常: OrderId={order.OrderId}, 错误={ex.Message}");
                    }
                }
                
                StatusMessage = $"止损单取消完成: 成功 {successCount} 个，失败 {failedCount} 个";
                Console.WriteLine($"🏁 止损单取消操作完成: 成功 {successCount} 个，失败 {failedCount} 个");
                
                System.Windows.MessageBox.Show(
                    $"止损单取消操作完成！\n\n" +
                    $"✅ 成功取消: {successCount} 个止损单\n" +
                    $"❌ 取消失败: {failedCount} 个止损单\n\n" +
                    $"注意：止损单已取消，相关持仓失去风险保护，请谨慎操作！",
                    "取消结果",
                    System.Windows.MessageBoxButton.OK,
                    failedCount > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
                
                // 刷新数据
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"取消止损单异常: {ex.Message}";
                Console.WriteLine($"❌ 取消选中止损单异常: {ex.Message}");
                
                System.Windows.MessageBox.Show(
                    $"取消止损单时发生异常：\n\n{ex.Message}",
                    "操作异常",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 添加合约到最近列表
        private void AddToRecentContracts(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return;

            try
            {
                // 移除已存在的相同合约（如果有）
                if (RecentContracts.Contains(symbol))
                {
                    RecentContracts.Remove(symbol);
                }

                // 添加到列表开头
                RecentContracts.Insert(0, symbol);

                // 保持最多10个合约
                while (RecentContracts.Count > 10)
                {
                    RecentContracts.RemoveAt(RecentContracts.Count - 1);
                }

                Console.WriteLine($"📝 最近合约已更新: {symbol} (总数: {RecentContracts.Count})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 更新最近合约列表失败: {ex.Message}");
            }
        }

        // 选择最近合约的命令
        [RelayCommand]
        private async Task SelectRecentContractAsync(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return;

            try
            {
                Console.WriteLine($"🔄 切换到最近合约: {symbol}");
                
                // 设置合约名称
                Symbol = symbol;
                
                // 查询合约信息
                await QueryContractInfoAsync();
                
                StatusMessage = $"已切换到合约: {symbol}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 切换最近合约失败: {ex.Message}");
                StatusMessage = $"切换合约失败: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CheckAccountEquityComposition()
        {
            if (AccountInfo == null)
            {
                StatusMessage = "请先选择账户";
                System.Windows.MessageBox.Show(
                    "请先选择一个交易账户",
                    "未选择账户",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            Console.WriteLine("\n" + "=".PadLeft(60, '='));
            Console.WriteLine("📊 账户权益组成分析（基于币安API官方文档）");
            Console.WriteLine("=".PadLeft(60, '='));

            // 从API获取的原始数据
            var apiTotalWallet = AccountInfo.TotalWalletBalance;
            var apiTotalMargin = AccountInfo.TotalMarginBalance;
            var apiUnrealizedProfit = AccountInfo.TotalUnrealizedProfit;
            var apiAvailableBalance = AccountInfo.AvailableBalance;
            var calculatedMarginUsed = AccountInfo.ActualMarginUsed;

            Console.WriteLine("📋 币安API字段含义（根据官方文档）:");
            Console.WriteLine($"   totalWalletBalance: {apiTotalWallet:F2} USDT  （钱包总余额，不含浮动盈亏）");
            Console.WriteLine($"   totalMarginBalance: {apiTotalMargin:F2} USDT  （⭐真正的账户权益，含浮动盈亏）");
            Console.WriteLine($"   totalUnrealizedProfit: {apiUnrealizedProfit:F2} USDT  （所有持仓浮动盈亏）");
            Console.WriteLine($"   availableBalance: {apiAvailableBalance:F2} USDT  （可用余额）");

            Console.WriteLine("\n🔧 我们的计算:");
            Console.WriteLine($"   实际保证金占用(累计持仓): {calculatedMarginUsed:F2} USDT");

            Console.WriteLine("\n🧮 验证公式（根据币安API关系）:");
            
            // 根据币安文档：totalMarginBalance = totalWalletBalance + totalUnrealizedProfit
            var verifyMarginBalance = apiTotalWallet + apiUnrealizedProfit;
            Console.WriteLine($"   验证公式: totalMarginBalance = totalWalletBalance + totalUnrealizedProfit");
            Console.WriteLine($"           {apiTotalMargin:F2} = {apiTotalWallet:F2} + {apiUnrealizedProfit:F2}");
            Console.WriteLine($"           计算结果: {verifyMarginBalance:F2}");
            Console.WriteLine($"           API实际值: {apiTotalMargin:F2}");
            
            var marginDiff = Math.Abs(verifyMarginBalance - apiTotalMargin);
            Console.WriteLine($"           差异: {marginDiff:F2} USDT");

            // 可用余额的验证
            Console.WriteLine($"\n   可用余额构成分析:");
            Console.WriteLine($"           totalMarginBalance(账户权益): {apiTotalMargin:F2}");
            Console.WriteLine($"           - 保证金占用: {calculatedMarginUsed:F2}");
            Console.WriteLine($"           理论可用余额: {apiTotalMargin - calculatedMarginUsed:F2}");
            Console.WriteLine($"           API可用余额: {apiAvailableBalance:F2}");
            
            var availableDiff = Math.Abs((apiTotalMargin - calculatedMarginUsed) - apiAvailableBalance);
            Console.WriteLine($"           差异: {availableDiff:F2} USDT");

            Console.WriteLine("\n📈 持仓汇总:");
            var activePositions = Positions.Where(p => Math.Abs(p.PositionAmt) > 0).ToList();
            Console.WriteLine($"   持仓数量: {activePositions.Count}");
            
            if (activePositions.Any())
            {
                var totalPositionUnrealized = activePositions.Sum(p => p.UnrealizedProfit);
                var totalPositionValue = activePositions.Sum(p => p.PositionValue);
                var totalIsolatedMargin = activePositions.Sum(p => p.IsolatedMargin);
                
                Console.WriteLine($"   总浮动盈亏（持仓累计）: {totalPositionUnrealized:F2} USDT");
                Console.WriteLine($"   总浮动盈亏（API返回）: {apiUnrealizedProfit:F2} USDT");
                Console.WriteLine($"   浮盈差异: {Math.Abs(totalPositionUnrealized - apiUnrealizedProfit):F2} USDT");
                Console.WriteLine($"   总货值: {totalPositionValue:F2} USDT");
                Console.WriteLine($"   IsolatedMargin累计: {totalIsolatedMargin:F2} USDT");
                Console.WriteLine($"   计算保证金累计: {calculatedMarginUsed:F2} USDT");
            }

            Console.WriteLine($"\n🎯 结论:");
            Console.WriteLine($"   🏦 币安APP显示的\"预估总资产\"应该对应:");
            Console.WriteLine($"       totalMarginBalance = {apiTotalMargin:F2} USDT");
            Console.WriteLine($"   💰 当前UI显示的\"账户权益\":");
            Console.WriteLine($"       已修正为使用 totalMarginBalance = {apiTotalMargin:F2} USDT");
            
            if (marginDiff < 0.01m)
            {
                Console.WriteLine($"   ✅ 币安API数据验证通过，公式一致");
            }
            else if (marginDiff < 1.0m)
            {
                Console.WriteLine($"   ⚠️ 有小幅差异({marginDiff:F2})，可能是时间差或精度问题");
            }
            else
            {
                Console.WriteLine($"   ❌ 较大差异({marginDiff:F2})，需要进一步检查");
            }

            Console.WriteLine("\n💡 币安API字段总结:");
            Console.WriteLine("   - totalWalletBalance: 钱包余额（不含浮盈，仅本金）");
            Console.WriteLine("   - totalMarginBalance: 账户权益（含浮盈，等于APP中的预估总资产）");
            Console.WriteLine("   - totalUnrealizedProfit: 浮动盈亏");
            Console.WriteLine("   - availableBalance: 可用余额（可开新仓的金额）");

            Console.WriteLine("=".PadLeft(60, '='));
            Console.WriteLine("📊 账户权益分析完成 - 已修正为使用totalMarginBalance");
            Console.WriteLine("=".PadLeft(60, '=') + "\n");

            // 在UI中显示结果
            var message = $"账户权益分析（已修正）：\n\n" +
                         $"🏦 币安API数据:\n" +
                         $"  💰 totalMarginBalance: {apiTotalMargin:F2} USDT\n" +
                         $"      ↑ 这个才是真正的账户权益（含浮盈）\n" +
                         $"  💵 totalWalletBalance: {apiTotalWallet:F2} USDT\n" +
                         $"      ↑ 仅为钱包余额（不含浮盈）\n" +
                         $"  📈 totalUnrealizedProfit: {apiUnrealizedProfit:F2} USDT\n" +
                         $"  🔓 availableBalance: {apiAvailableBalance:F2} USDT\n\n" +
                         $"🔧 验证结果:\n" +
                         $"  公式验证: {(marginDiff < 1.0m ? "✅ 通过" : "❌ 异常")}\n" +
                         $"  差异: {marginDiff:F2} USDT\n\n" +
                         $"📱 对比币安APP:\n" +
                         $"  界面显示账户权益: {apiTotalMargin:F2} USDT\n" +
                         $"  应该与APP预估总资产一致\n\n" +
                         $"✅ 已修正UI显示为使用totalMarginBalance";

            StatusMessage = $"权益分析完成 - 已修正为{apiTotalMargin:F2}";

            System.Windows.MessageBox.Show(
                message,
                "账户权益分析（已修正）",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task AddProfitProtectionStopLossAsync()
        {
            Console.WriteLine($"🛡️ 开始添加保盈止损...");
            
            try
            {
                // 第一步：基本参数检查
                if (SelectedAccount == null)
                {
                    Console.WriteLine($"❌ 未选择账户");
                    StatusMessage = "请选择账户";
                    System.Windows.MessageBox.Show(
                        "请先选择一个交易账户",
                        "未选择账户",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (SelectedPosition == null)
                {
                    Console.WriteLine($"❌ 未选择持仓");
                    StatusMessage = "请选择持仓";
                    System.Windows.MessageBox.Show(
                        "请先在持仓列表中选择一个持仓",
                        "未选择持仓",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // 检查持仓数据完整性
                if (Math.Abs(SelectedPosition.PositionAmt) <= 0)
                {
                    Console.WriteLine($"❌ 持仓数量无效: {SelectedPosition.PositionAmt}");
                    StatusMessage = "选中的持仓数量无效";
                    System.Windows.MessageBox.Show(
                        "选中的持仓数量为0或无效，无法设置保盈止损",
                        "持仓数量无效",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (SelectedPosition.EntryPrice <= 0)
                {
                    Console.WriteLine($"❌ 开仓价无效: {SelectedPosition.EntryPrice}");
                    StatusMessage = "持仓开仓价无效";
                    System.Windows.MessageBox.Show(
                        $"持仓 {SelectedPosition.Symbol} 的开仓价无效（{SelectedPosition.EntryPrice}），无法设置保盈止损",
                        "开仓价无效",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // 检查Symbol是否为空
                if (string.IsNullOrEmpty(SelectedPosition.Symbol))
                {
                    Console.WriteLine($"❌ 持仓合约名称为空");
                    StatusMessage = "持仓合约名称无效";
                    System.Windows.MessageBox.Show(
                        "持仓合约名称为空，无法设置保盈止损",
                        "合约名称无效",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // 检查PositionSideString是否为空
                if (string.IsNullOrEmpty(SelectedPosition.PositionSideString))
                {
                    Console.WriteLine($"❌ 持仓方向字符串为空，尝试设置默认值");
                    SelectedPosition.PositionSideString = "BOTH"; // 设置默认值
                }

                if (LatestPrice <= 0)
                {
                    Console.WriteLine($"❌ 最新价格无效: {LatestPrice}");
                    StatusMessage = "请先获取最新价格";
                    // 尝试自动获取价格
                    try
                    {
                        await UpdateLatestPriceAsync();
                        if (LatestPrice <= 0)
                        {
                            System.Windows.MessageBox.Show(
                                "无法获取合约的最新价格，请检查网络连接或手动刷新价格",
                                "价格获取失败",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                            return;
                        }
                    }
                    catch (Exception priceEx)
                    {
                        Console.WriteLine($"❌ 自动获取价格失败: {priceEx.Message}");
                        System.Windows.MessageBox.Show(
                            $"无法获取最新价格：{priceEx.Message}",
                            "价格获取失败",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                        return;
                    }
                }

                Console.WriteLine($"📊 持仓信息: {SelectedPosition.Symbol}, 数量: {SelectedPosition.PositionAmt}, 当前浮盈: {SelectedPosition.UnrealizedProfit}");

                // 第二步：弹出对话框获取保底盈利金额
                try
                {
                    // 增强参数验证
                    var symbol = SelectedPosition.Symbol ?? "未知合约";
                    var direction = SelectedPosition.PositionAmt > 0 ? "做多" : "做空";
                    var quantity = Math.Abs(SelectedPosition.PositionAmt);
                    var dialogEntryPrice = SelectedPosition.EntryPrice;
                    var unrealizedProfit = SelectedPosition.UnrealizedProfit;
                    var currentPrice = LatestPrice;
                    
                    Console.WriteLine($"🔍 对话框参数验证:");
                    Console.WriteLine($"   Symbol: '{symbol}'");
                    Console.WriteLine($"   Direction: '{direction}'");
                    Console.WriteLine($"   Quantity: {quantity}");
                    Console.WriteLine($"   EntryPrice: {dialogEntryPrice}");
                    Console.WriteLine($"   UnrealizedProfit: {unrealizedProfit}");
                    Console.WriteLine($"   CurrentPrice: {currentPrice}");
                    
                    // 验证所有参数都有效
                    if (string.IsNullOrEmpty(symbol))
                    {
                        throw new ArgumentException("合约名称为空");
                    }
                    if (quantity <= 0)
                    {
                        throw new ArgumentException($"数量无效: {quantity}");
                    }
                    if (dialogEntryPrice <= 0)
                    {
                        throw new ArgumentException($"开仓价无效: {dialogEntryPrice}");
                    }
                    if (currentPrice <= 0)
                    {
                        throw new ArgumentException($"当前价格无效: {currentPrice}");
                    }
                    
                    Console.WriteLine($"✅ 参数验证通过，创建对话框...");
                    
                    var profitProtectionDialog = new ProfitProtectionDialog(
                        symbol,
                        direction,
                        quantity,
                        dialogEntryPrice,
                        unrealizedProfit,
                        currentPrice);

                    Console.WriteLine($"✅ 对话框创建成功，显示对话框...");
                    var dialogResult = profitProtectionDialog.ShowDialog();

                    if (dialogResult != true)
                    {
                        Console.WriteLine($"🚫 用户取消操作");
                        StatusMessage = "用户取消了保盈止损操作";
                        return;
                    }

                    var userProfitProtectionAmount = profitProtectionDialog.ProfitProtectionAmount;
                    Console.WriteLine($"💰 用户输入的保底盈利: {userProfitProtectionAmount:F2} USDT");

                    // 第三步：校验当前浮盈是否足够
                    if (SelectedPosition.UnrealizedProfit <= userProfitProtectionAmount)
                    {
                        var message = $"当前浮盈不足！\n\n" +
                                     $"当前浮盈: {SelectedPosition.UnrealizedProfit:F2} USDT\n" +
                                     $"保底盈利: {userProfitProtectionAmount:F2} USDT\n\n" +
                                     $"当前浮盈必须大于保底盈利才能设置保盈止损";
                        
                        Console.WriteLine($"❌ 浮盈不足: 当前{SelectedPosition.UnrealizedProfit:F2} < 保底{userProfitProtectionAmount:F2}");
                        StatusMessage = "当前浮盈不足，无法设置保盈止损";
                        System.Windows.MessageBox.Show(message, "浮盈不足", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    // 第四步：计算保盈止损价
                    Console.WriteLine($"🧮 开始计算保盈止损价...");
                    
                    var isLong = SelectedPosition.PositionAmt > 0;
                    var positionSize = Math.Abs(SelectedPosition.PositionAmt);
                    var entryPrice = SelectedPosition.EntryPrice;
                    
                    // 计算保盈止损价
                    // 做多：止损价 = 开仓价 + (保底盈利 / 持仓数量)
                    // 做空：止损价 = 开仓价 - (保底盈利 / 持仓数量)
                    decimal profitProtectionStopPrice;
                    
                    if (isLong)
                    {
                        profitProtectionStopPrice = entryPrice + (userProfitProtectionAmount / positionSize);
                        Console.WriteLine($"📈 做多计算: {entryPrice} + ({userProfitProtectionAmount} / {positionSize}) = {profitProtectionStopPrice}");
                    }
                    else
                    {
                        profitProtectionStopPrice = entryPrice - (userProfitProtectionAmount / positionSize);
                        Console.WriteLine($"📉 做空计算: {entryPrice} - ({userProfitProtectionAmount} / {positionSize}) = {profitProtectionStopPrice}");
                    }

                    // 调整价格精度
                    profitProtectionStopPrice = AdjustPricePrecision(profitProtectionStopPrice, SelectedPosition.Symbol);
                    Console.WriteLine($"🎯 精度调整后止损价: {PriceFormatConverter.FormatPrice(profitProtectionStopPrice)}");

                    // 第五步：校验止损价与当前价的关系
                    bool priceValidation = false;
                    string validationMessage = "";
                    
                    if (isLong)
                    {
                        // 做多：止损价应该低于当前价
                        priceValidation = profitProtectionStopPrice < LatestPrice;
                        validationMessage = priceValidation ? "✅ 做多止损价低于当前价，符合预期" : "❌ 做多止损价应该低于当前价";
                    }
                    else
                    {
                        // 做空：止损价应该高于当前价
                        priceValidation = profitProtectionStopPrice > LatestPrice;
                        validationMessage = priceValidation ? "✅ 做空止损价高于当前价，符合预期" : "❌ 做空止损价应该高于当前价";
                    }

                    Console.WriteLine($"🔍 价格校验: {validationMessage}");
                    Console.WriteLine($"   当前价: {PriceFormatConverter.FormatPrice(LatestPrice)}");
                    Console.WriteLine($"   止损价: {PriceFormatConverter.FormatPrice(profitProtectionStopPrice)}");

                    if (!priceValidation)
                    {
                        var errorMessage = $"止损价格校验失败！\n\n" +
                                          $"持仓方向: {(isLong ? "做多" : "做空")}\n" +
                                          $"当前价: {PriceFormatConverter.FormatPrice(LatestPrice)}\n" +
                                          $"计算止损价: {PriceFormatConverter.FormatPrice(profitProtectionStopPrice)}\n\n" +
                                          (isLong ? "做多持仓的保盈止损价应该低于当前价" : "做空持仓的保盈止损价应该高于当前价");
                        
                        StatusMessage = "止损价格校验失败";
                        System.Windows.MessageBox.Show(errorMessage, "价格校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        return;
                    }

                    // 第六步：执行下单操作
                    IsLoading = true;
                    StatusMessage = $"正在为 {SelectedPosition.Symbol} 添加保盈止损单...";
                    Console.WriteLine($"🚀 开始执行保盈止损下单...");
                    
                    // 构建保盈止损单
                    var stopLossOrder = new OrderRequest
                    {
                        Symbol = SelectedPosition.Symbol,
                        Side = isLong ? "SELL" : "BUY", // 反向操作
                        PositionSide = SelectedPosition.PositionSideString,
                        Type = "STOP_MARKET", // 市价止损单
                        Quantity = positionSize, // 相同数量
                        StopPrice = profitProtectionStopPrice, // 保盈止损价
                        ReduceOnly = true, // 只减仓
                        Leverage = SelectedPosition.Leverage,
                        MarginType = SelectedPosition.MarginType ?? "ISOLATED",
                        WorkingType = "CONTRACT_PRICE" // 使用合约价格触发
                    };

                    Console.WriteLine($"📋 保盈止损单详情: {stopLossOrder.Side} {stopLossOrder.Quantity:F6} {stopLossOrder.Symbol} @ {PriceFormatConverter.FormatPrice(stopLossOrder.StopPrice)}");

                    var success = await _binanceService.PlaceOrderAsync(stopLossOrder);

                    if (success)
                    {
                        Console.WriteLine($"✅ 保盈止损单下单成功");
                        StatusMessage = $"保盈止损单下单成功";

                        // 第七步：清理无效委托
                        Console.WriteLine($"🧹 开始清理无效的止损委托...");
                        await CleanupConflictingStopOrdersAsync(SelectedPosition.Symbol, profitProtectionStopPrice, isLong);

                        // 刷新数据
                        await RefreshDataAsync();

                        var successMessage = $"保盈止损设置成功！\n\n" +
                                           $"✅ 保盈止损单: {(isLong ? "卖出" : "买入")} {positionSize:F6} {SelectedPosition.Symbol}\n" +
                                           $"📊 触发价: {PriceFormatConverter.FormatPrice(profitProtectionStopPrice)}\n" +
                                           $"💰 保底盈利: {userProfitProtectionAmount:F2} USDT\n\n" +
                                           $"🎯 当价格{(isLong ? "跌至" : "涨至")}止损价时将保护您的盈利";

                        System.Windows.MessageBox.Show(successMessage, "保盈止损成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    else
                    {
                        Console.WriteLine($"❌ 保盈止损单下单失败");
                        StatusMessage = $"保盈止损单下单失败";
                        System.Windows.MessageBox.Show(
                            $"保盈止损单下单失败！\n\n❌ {SelectedPosition.Symbol}\n\n请检查账户状态和网络连接",
                            "下单失败",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
                catch (Exception dialogEx)
                {
                    Console.WriteLine($"❌ 对话框创建或操作异常: {dialogEx.Message}");
                    StatusMessage = $"对话框操作失败: {dialogEx.Message}";
                    System.Windows.MessageBox.Show(
                        $"保盈止损对话框操作失败：\n\n{dialogEx.Message}",
                        "对话框异常",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"保盈止损功能异常: {ex.Message}";
                Console.WriteLine($"❌ 保盈止损功能异常: {ex.Message}");
                Console.WriteLine($"❌ 异常类型: {ex.GetType().Name}");
                Console.WriteLine($"❌ 异常堆栈: {ex.StackTrace}");
                
                System.Windows.MessageBox.Show(
                    $"保盈止损功能发生异常：\n\n{ex.Message}\n\n请查看控制台了解详细信息",
                    "系统异常",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                Console.WriteLine($"🏁 保盈止损操作完成");
            }
        }

        // 清理冲突的止损委托
        private async Task CleanupConflictingStopOrdersAsync(string symbol, decimal newStopPrice, bool isLong)
        {
            try
            {
                Console.WriteLine($"\n🧹 开始清理 {symbol} 的冲突止损委托...");
                Console.WriteLine($"📊 新止损价: {PriceFormatConverter.FormatPrice(newStopPrice)}, 持仓方向: {(isLong ? "做多" : "做空")}");
                
                // 获取该合约的所有止损委托
                var stopOrders = Orders.Where(o => 
                    o.Symbol == symbol && 
                    o.Type == "STOP_MARKET" && 
                    o.ReduceOnly == true).ToList();
                
                Console.WriteLine($"🔍 找到 {symbol} 的止损委托: {stopOrders.Count} 个");
                
                if (!stopOrders.Any())
                {
                    Console.WriteLine($"ℹ️ 没有找到需要清理的止损委托");
                    return;
                }

                var ordersToCancel = new List<OrderInfo>();
                
                foreach (var order in stopOrders)
                {
                    var formattedStopPrice = PriceFormatConverter.FormatPrice(order.StopPrice);
                    Console.WriteLine($"   📋 检查订单: OrderId={order.OrderId}, StopPrice={formattedStopPrice}, Side={order.Side}");
                    
                    bool shouldCancel = false;
                    string reason = "";
                    
                    if (isLong)
                    {
                        // 做多：如果有止损价最高的市价止损单，其他低于这个止损价的委托单就没用了
                        // 新止损单是卖出方向，检查其他卖出止损单
                        if (order.Side == "SELL" && order.StopPrice < newStopPrice)
                        {
                            shouldCancel = true;
                            reason = $"做多情况下，止损价{formattedStopPrice}低于新止损价{PriceFormatConverter.FormatPrice(newStopPrice)}，无效";
                        }
                    }
                    else
                    {
                        // 做空：如果有止损价最低的市价止损单，其他高于这个止损价的委托单就没用了
                        // 新止损单是买入方向，检查其他买入止损单
                        if (order.Side == "BUY" && order.StopPrice > newStopPrice)
                        {
                            shouldCancel = true;
                            reason = $"做空情况下，止损价{formattedStopPrice}高于新止损价{PriceFormatConverter.FormatPrice(newStopPrice)}，无效";
                        }
                    }
                    
                    if (shouldCancel)
                    {
                        Console.WriteLine($"   ❌ 标记删除: {reason}");
                        ordersToCancel.Add(order);
                    }
                    else
                    {
                        Console.WriteLine($"   ✅ 保留: 止损价{formattedStopPrice}有效");
                    }
                }
                
                if (!ordersToCancel.Any())
                {
                    Console.WriteLine($"✅ 没有需要清理的冲突委托");
                    return;
                }
                
                Console.WriteLine($"\n🗑️ 准备取消 {ordersToCancel.Count} 个冲突的止损委托:");
                foreach (var order in ordersToCancel)
                {
                    Console.WriteLine($"   🗑️ OrderId={order.OrderId}, StopPrice={PriceFormatConverter.FormatPrice(order.StopPrice)}");
                }
                
                int cancelledCount = 0;
                int failedCount = 0;
                
                foreach (var order in ordersToCancel)
                {
                    try
                    {
                        Console.WriteLine($"🗑️ 取消冲突止损单: OrderId={order.OrderId}");
                        var success = await _binanceService.CancelOrderAsync(order.Symbol, order.OrderId);
                        
                        if (success)
                        {
                            cancelledCount++;
                            Console.WriteLine($"✅ 成功取消: OrderId={order.OrderId}");
                        }
                        else
                        {
                            failedCount++;
                            Console.WriteLine($"❌ 取消失败: OrderId={order.OrderId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        Console.WriteLine($"❌ 取消异常: OrderId={order.OrderId}, 错误={ex.Message}");
                    }
                    
                    // 避免过于频繁的API调用
                    await Task.Delay(100);
                }
                
                Console.WriteLine($"\n🏁 冲突委托清理完成:");
                Console.WriteLine($"   ✅ 成功取消: {cancelledCount} 个");
                Console.WriteLine($"   ❌ 取消失败: {failedCount} 个");
                
                if (cancelledCount > 0)
                {
                    StatusMessage = $"已清理 {cancelledCount} 个冲突的止损委托";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 清理冲突委托异常: {ex.Message}");
                StatusMessage = $"清理冲突委托失败: {ex.Message}";
            }
        }

        // 🚀 移动止损功能 - 智能版本
        [RelayCommand]
        private void ToggleTrailingStop()
        {
            try
            {
                TrailingStopEnabled = !TrailingStopEnabled;
                var statusText = TrailingStopEnabled ? "启用" : "停用";
                StatusMessage = $"移动止损已{statusText}";
                Console.WriteLine($"🎯 移动止损已{statusText}");
                
                if (TrailingStopEnabled)
                {
                    Console.WriteLine("🔔 注意：移动止损将把现有的STOP_MARKET订单转换为原生移动止损单");
                    Console.WriteLine("💡 回调率将根据现有止损单的风险设置动态计算，保持原有风险水平");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"切换移动止损失败: {ex.Message}";
                Console.WriteLine($"❌ 切换移动止损异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 使用原生移动止损单的处理逻辑
        /// </summary>
        private async Task ProcessTrailingStopAsync()
        {
            try
            {
                Console.WriteLine("🎯 检查需要转换为原生移动止损单的订单...");
                
                // 获取所有普通止损订单
                var stopOrders = Orders.Where(o => o.Type == "STOP_MARKET" && o.Status == "NEW" && o.ReduceOnly).ToList();
                
                if (!stopOrders.Any())
                {
                    Console.WriteLine("🎯 没有找到需要转换的止损订单");
                    return;
                }
                
                Console.WriteLine($"🎯 找到{stopOrders.Count}个普通止损订单，准备转换为原生移动止损单");
                
                foreach (var stopOrder in stopOrders)
                {
                    await ConvertToTrailingStopAsync(stopOrder);
                }
                
                Console.WriteLine("🎯 移动止损单转换检查完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 移动止损单转换异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 将普通止损单转换为原生移动止损单
        /// </summary>
        private async Task ConvertToTrailingStopAsync(OrderInfo stopOrder)
        {
            try
            {
                // 找到对应的持仓
                var position = Positions.FirstOrDefault(p => p.Symbol == stopOrder.Symbol && Math.Abs(p.PositionAmt) > 0);
                if (position == null)
                {
                    Console.WriteLine($"🎯 {stopOrder.Symbol}: 没有找到对应持仓，跳过转换");
                    return;
                }

                bool isLongPosition = position.PositionAmt > 0;
                decimal currentPrice = await _binanceService.GetLatestPriceAsync(stopOrder.Symbol);
                
                if (currentPrice <= 0)
                {
                    Console.WriteLine($"🎯 {stopOrder.Symbol}: 无法获取当前价格，跳过转换");
                    return;
                }

                // 检查当前价格是否适合启用移动止损
                decimal entryPrice = position.EntryPrice;
                bool priceMovedFavorably = false;
                
                if (isLongPosition)
                {
                    // 多头：当前价格需要高于进场价
                    priceMovedFavorably = currentPrice > entryPrice;
                }
                else
                {
                    // 空头：当前价格需要低于进场价
                    priceMovedFavorably = currentPrice < entryPrice;
                }

                if (!priceMovedFavorably)
                {
                    Console.WriteLine($"🎯 {stopOrder.Symbol}: 价格未有利移动，暂不转换为移动止损单");
                    Console.WriteLine($"   进场价: {entryPrice:F4}, 当前价: {currentPrice:F4}, 持仓方向: {(isLongPosition ? "多头" : "空头")}");
                    return;
                }

                // 🎯 根据现有止损单动态计算回调率
                decimal stopPrice = stopOrder.StopPrice;
                decimal callbackRate;
                
                if (isLongPosition)
                {
                    // 多头：回调率 = (进场价 - 止损价) / 进场价 * 100
                    callbackRate = Math.Abs(entryPrice - stopPrice) / entryPrice * 100m;
                }
                else
                {
                    // 空头：回调率 = (止损价 - 进场价) / 进场价 * 100
                    callbackRate = Math.Abs(stopPrice - entryPrice) / entryPrice * 100m;
                }
                
                // 限制回调率在合理范围内 (0.1% - 5.0%)
                callbackRate = Math.Max(0.1m, Math.Min(5.0m, Math.Round(callbackRate, 1)));

                Console.WriteLine($"🔄 转换为原生移动止损单: {stopOrder.Symbol} {(isLongPosition ? "多头" : "空头")}");
                Console.WriteLine($"   进场价: {entryPrice:F4}, 当前价: {currentPrice:F4}");
                Console.WriteLine($"   原止损价: {stopPrice:F4}");
                Console.WriteLine($"   💡 动态计算回调率: {callbackRate:F1}% (基于现有止损设置)");

                // 创建原生移动止损单
                var trailingStopRequest = new OrderRequest
                {
                    Symbol = stopOrder.Symbol,
                    Side = stopOrder.Side,
                    Type = "TRAILING_STOP_MARKET",
                    Quantity = stopOrder.OrigQty,
                    CallbackRate = callbackRate, // 使用动态计算的回调率
                    ActivationPrice = currentPrice, // 使用当前价格作为激活价格
                    TimeInForce = "GTC",
                    WorkingType = "CONTRACT_PRICE",
                    ReduceOnly = true
                };

                // 先下移动止损单
                bool trailingOrderSuccess = await _binanceService.PlaceOrderAsync(trailingStopRequest);
                if (!trailingOrderSuccess)
                {
                    Console.WriteLine($"❌ 创建移动止损单失败: {stopOrder.Symbol}");
                    return;
                }

                Console.WriteLine($"✅ 移动止损单创建成功，准备删除原止损单");

                // 移动止损单成功后删除原止损单
                bool cancelSuccess = await _binanceService.CancelOrderAsync(stopOrder.Symbol, stopOrder.OrderId);
                if (!cancelSuccess)
                {
                    Console.WriteLine($"⚠️ 原止损单删除失败，但移动止损单已生效 {stopOrder.Symbol}");
                }
                else
                {
                    Console.WriteLine($"✅ 移动止损转换完成: {stopOrder.Symbol}");
                    Console.WriteLine($"   回调率: {callbackRate:F1}% (保持原有风险水平)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 移动止损单转换异常: {stopOrder.Symbol} - {ex.Message}");
            }
        }

    }
} 