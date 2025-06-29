using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Generic;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
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
        private readonly DispatcherTimer _refreshTimer;

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

        /// <summary>
        /// 集合属性
        /// </summary>
        public ObservableCollection<ContractStateDisplayModel> ContractStates { get; } = new();
        public ObservableCollection<ExecutionHistoryDisplayModel> ExecutionHistory { get; } = new();
        public ObservableCollection<AddPositionTierDisplayModel> AddPositionTiers { get; } = new();
        public ObservableCollection<ProfitProtectionTierDisplayModel> ProfitProtectionTiers { get; } = new();

        /// <summary>
        /// 命令
        /// </summary>
        public ICommand RefreshCommand { get; }

        private DateTime _monitorStartTime;

        public AutoMonitorDashboard(AutoMonitorService autoMonitorService, ILogger logger)
        {
            _autoMonitorService = autoMonitorService ?? throw new ArgumentNullException(nameof(autoMonitorService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // 初始化命令
            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());

            // 订阅自动盯盘事件
            _autoMonitorService.MonitorStatusChanged += OnMonitorStatusChanged;
            _autoMonitorService.ExecutionCompleted += OnExecutionCompleted;

            // 初始化定时器（每30秒刷新一次）
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _refreshTimer.Tick += async (s, e) => await RefreshDataAsync();

            // 🔧 简化的窗口初始化，避免XAML编译问题
            InitializeSimpleWindow();
            
            // 启动定时器和初始化数据
            _refreshTimer.Start();
            _ = Task.Run(async () => await RefreshDataAsync());
            
            _logger.LogInformation("🖥️ 自动盯盘监控面板已初始化");
        }

        /// <summary>
        /// 简化的窗口初始化
        /// </summary>
        private void InitializeSimpleWindow()
        {
            try
            {
                // 🔧 修复：直接使用代码创建界面（避免XAML编译问题）
                _logger.LogInformation("⚠️ 使用代码创建监控面板界面");
                CreateCodeBasedUI();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 监控面板初始化失败，创建最基本界面");
                CreateFallbackUI();
            }
        }

        /// <summary>
        /// 创建代码化的UI界面（当XAML加载失败时使用）
        /// </summary>
        private void CreateCodeBasedUI()
        {
            try
            {
                Title = "🔍 自动盯盘监控面板";
                Width = 1200;
                Height = 800;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                DataContext = this;

                // 创建基本的UI结构
                var mainGrid = new System.Windows.Controls.Grid();
                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

                // 标题栏
                var titlePanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Margin = new Thickness(10)
                };

                var titleText = new System.Windows.Controls.TextBlock
                {
                    Text = "🔍 自动盯盘监控面板",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var refreshButton = new System.Windows.Controls.Button
                {
                    Content = "🔄 刷新",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(20, 0, 10, 0)
                };
                refreshButton.Click += RefreshButton_Click;

                var closeButton = new System.Windows.Controls.Button
                {
                    Content = "❌ 关闭", 
                    Width = 80,
                    Height = 30
                };
                closeButton.Click += CloseButton_Click;

                titlePanel.Children.Add(titleText);
                titlePanel.Children.Add(refreshButton);
                titlePanel.Children.Add(closeButton);

                System.Windows.Controls.Grid.SetRow(titlePanel, 0);
                mainGrid.Children.Add(titlePanel);

                // 内容区域 - 创建完整的监控界面
                var mainContentGrid = new System.Windows.Controls.Grid();
                mainContentGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
                mainContentGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

                // 顶部状态卡片区域
                var statusCardsPanel = CreateStatusCardsPanel();
                System.Windows.Controls.Grid.SetRow(statusCardsPanel, 0);
                mainContentGrid.Children.Add(statusCardsPanel);

                // 🔧 优化主要内容区域布局，确保列表铺满可用空间
                var mainContentArea = new System.Windows.Controls.Grid();
                mainContentArea.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(3, System.Windows.GridUnitType.Star) }); // 增加左侧比例
                mainContentArea.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(2, System.Windows.GridUnitType.Star) }); // 增加右侧比例

                // 左侧：配置详情和合约状态
                var leftPanel = CreateLeftPanel();
                System.Windows.Controls.Grid.SetColumn(leftPanel, 0);
                mainContentArea.Children.Add(leftPanel);

                // 右侧：执行历史
                var rightPanel = CreateRightPanel();
                System.Windows.Controls.Grid.SetColumn(rightPanel, 1);
                mainContentArea.Children.Add(rightPanel);

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
                CreateFallbackUI();
            }
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
                Text = "📊 监控状态",
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
        /// 窗口关闭事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 取消订阅自动盯盘事件
                _autoMonitorService.MonitorStatusChanged -= OnMonitorStatusChanged;
                _autoMonitorService.ExecutionCompleted -= OnExecutionCompleted;
                
                // 停止定时器
                _refreshTimer?.Stop();
                
                _logger.LogInformation("🖥️ 监控界面资源清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 清理监控界面资源时发生错误");
            }
            
            Close();
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
        /// 刷新数据
        /// </summary>
        private async Task RefreshDataAsync()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateBasicStats();
                    UpdateConfiguration();
                    UpdateContractStates();
                    UpdateExecutionHistory();
                    
                    LastUpdateTime = DateTime.Now;
                });
                
                _logger.LogDebug("✅ 自动盯盘监控面板数据刷新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 刷新监控面板数据时发生异常");
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
                        
                        // 🔧 优化：计算执行进度，使用更精确的触发记录检查
                        var breakEvenExecuted = profile.TriggerRecords.Values.Any(r => 
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

        private void OnMonitorStatusChanged(object? sender, MonitorStatusChangedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _ = Task.Run(async () => await RefreshDataAsync());
            });
        }

        private void OnExecutionCompleted(object? sender, ExecutionResultEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _ = Task.Run(async () => await RefreshDataAsync());
            });
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
                Margin = new Thickness(2),
                Padding = new Thickness(8)
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
                Foreground = new SolidColorBrush(Colors.Gray),
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
                Foreground = new SolidColorBrush(Colors.Gray),
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
        /// 创建左侧面板（合约状态）
        /// </summary>
        private System.Windows.Controls.Border CreateLeftPanel()
        {
            var card = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var panel = new System.Windows.Controls.DockPanel();

            var title = new System.Windows.Controls.TextBlock
            {
                Text = "📊 合约执行状态详情",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Colors.DarkBlue),
                Margin = new Thickness(0, 0, 0, 8)
            };
            System.Windows.Controls.DockPanel.SetDock(title, System.Windows.Controls.Dock.Top);
            panel.Children.Add(title);

            // 创建合约状态数据表格
            var dataGrid = CreateContractStateDataGrid();
            panel.Children.Add(dataGrid);

            card.Child = panel;
            return card;
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
                Padding = new Thickness(8),
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

        /// <summary>
        /// 创建合约状态数据表格
        /// </summary>
        private System.Windows.Controls.DataGrid CreateContractStateDataGrid()
        {
            var dataGrid = new System.Windows.Controls.DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.Horizontal,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(249, 249, 249)),
                FontSize = 13,
                RowHeight = 38, // 🔧 增加行高，确保内容完整显示
                ItemsSource = ContractStates
            };

            // 合约列  
            var symbolColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "合约",
                Binding = new System.Windows.Data.Binding("Symbol"),
                Width = 110,
                FontWeight = FontWeights.Bold
            };
            dataGrid.Columns.Add(symbolColumn);

            // 保本状态列
            var breakEvenColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "保本状态",
                Binding = new System.Windows.Data.Binding("BreakEvenStatus"),
                Width = 100
            };
            dataGrid.Columns.Add(breakEvenColumn);

            // 推仓进度列
            var addPositionColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "推仓进度",
                Binding = new System.Windows.Data.Binding("AddPositionProgress") { StringFormat = "{0}/4" },
                Width = 80
            };
            dataGrid.Columns.Add(addPositionColumn);

            // 保盈进度列
            var profitProtectionColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "保盈进度",
                Binding = new System.Windows.Data.Binding("ProfitProtectionProgress") { StringFormat = "{0}/3" },
                Width = 80
            };
            dataGrid.Columns.Add(profitProtectionColumn);

            // 总执行次数列
            var totalExecutionsColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "总执行次数",
                Binding = new System.Windows.Data.Binding("TotalExecutions"),
                Width = 80
            };
            dataGrid.Columns.Add(totalExecutionsColumn);

            // 最后执行列
            var lastExecutionColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "最后执行",
                Binding = new System.Windows.Data.Binding("LastExecutionTime") { StringFormat = "{0:MM-dd HH:mm}" },
                Width = 90
            };
            dataGrid.Columns.Add(lastExecutionColumn);

            return dataGrid;
        }

        /// <summary>
        /// 创建执行历史数据表格
        /// </summary>
        private System.Windows.Controls.DataGrid CreateExecutionHistoryDataGrid()
        {
            var dataGrid = new System.Windows.Controls.DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.Horizontal,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(240, 248, 240)),
                FontSize = 12,
                RowHeight = 35, // 🔧 增加行高，确保内容完整显示
                ItemsSource = ExecutionHistory
            };

            // 🔧 优化列宽设置，确保内容完整显示并铺满可用空间
            // 时间列
            var timeColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "时间",
                Binding = new System.Windows.Data.Binding("ExecutionTime") { StringFormat = "{0:HH:mm:ss}" },
                Width = new System.Windows.Controls.DataGridLength(70) // 增加宽度确保时间完整显示
            };
            var timeStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            timeStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            timeStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            timeStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontSizeProperty, 11.0));
            timeStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, new SolidColorBrush(Colors.Gray)));
            timeColumn.ElementStyle = timeStyle;
            dataGrid.Columns.Add(timeColumn);

            // 合约列
            var symbolColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "合约",
                Binding = new System.Windows.Data.Binding("Symbol"),
                Width = new System.Windows.Controls.DataGridLength(80) // 增加宽度确保合约名完整显示
            };
            var symbolStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            symbolStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            symbolStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            symbolStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            symbolColumn.ElementStyle = symbolStyle;
            dataGrid.Columns.Add(symbolColumn);

            // 类型列
            var typeColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "类型",
                Binding = new System.Windows.Data.Binding("ExecutionType"),
                Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star) // 使用Star宽度自适应
            };
            var typeStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            typeStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            typeStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            typeStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            typeStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontSizeProperty, 11.0));
            typeColumn.ElementStyle = typeStyle;
            dataGrid.Columns.Add(typeColumn);

            // 结果列
            var resultColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "结果",
                Binding = new System.Windows.Data.Binding("ResultText"),
                Width = new System.Windows.Controls.DataGridLength(60) // 增加宽度确保结果文字完整显示
            };
            var resultStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            resultStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            resultStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            resultStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            resultStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontSizeProperty, 10.0));
            resultColumn.ElementStyle = resultStyle;
            dataGrid.Columns.Add(resultColumn);

            // 浮盈列
            var pnlColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "浮盈",
                Binding = new System.Windows.Data.Binding("TriggerPnl") { StringFormat = "{0:F1}U" },
                Width = new System.Windows.Controls.DataGridLength(60) // 增加宽度确保浮盈数值完整显示
            };
            var pnlStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            pnlStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            pnlStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            pnlStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            pnlStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontSizeProperty, 11.0));
            pnlStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, new SolidColorBrush(Colors.DarkBlue)));
            pnlColumn.ElementStyle = pnlStyle;
            dataGrid.Columns.Add(pnlColumn);

            return dataGrid;
        }

        /// <summary>
        /// 创建推仓配置卡片
        /// </summary>
        private System.Windows.Controls.Border CreateAddPositionConfigCard()
        {
            var card = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var panel = new System.Windows.Controls.StackPanel();

            var title = new System.Windows.Controls.TextBlock
            {
                Text = "⚡ 推仓配置详情",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(73, 80, 87)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            panel.Children.Add(title);

            // 🔧 创建推仓配置数据表格 - 支持动态数量档位
            var dataGrid = new System.Windows.Controls.DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.Horizontal,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                FontSize = 12, // 增加字体大小提升可读性
                RowHeight = 32, // 🔧 增加行高，确保文字完整显示
                HeadersVisibility = System.Windows.Controls.DataGridHeadersVisibility.Column,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(241, 243, 244)),
                ItemsSource = AddPositionTiers,
                MaxHeight = 220, // 相应增加最大高度
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled
            };

            // 🔧 优化列宽设置，确保内容完整显示
            // 档位列
            var tierColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "档位",
                Binding = new System.Windows.Data.Binding("TierIndex"),
                Width = new System.Windows.Controls.DataGridLength(50) // 固定宽度，确保数字完整显示
            };
            tierColumn.ElementStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            tierColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            tierColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            tierColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            tierColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(73, 80, 87))));
            dataGrid.Columns.Add(tierColumn);

            // 触发浮盈列
            var triggerColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "触发浮盈",
                Binding = new System.Windows.Data.Binding("TriggerProfitAmount") { StringFormat = "{0:F0}U" },
                Width = new System.Windows.Controls.DataGridLength(85) // 固定宽度，确保数值和单位完整显示
            };
            triggerColumn.ElementStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            triggerColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            triggerColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            triggerColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            triggerColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(40, 167, 69))));
            triggerColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)));
            dataGrid.Columns.Add(triggerColumn);

            // 风险倍数列
            var riskColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "风险倍数",
                Binding = new System.Windows.Data.Binding("RiskMultiplier") { StringFormat = "{0:F1}倍" },
                Width = new System.Windows.Controls.DataGridLength(80) // 固定宽度，确保倍数完整显示
            };
            riskColumn.ElementStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            riskColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            riskColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            riskColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            riskColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0, 123, 255))));
            riskColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)));
            dataGrid.Columns.Add(riskColumn);

            // 止损比例列
            var stopLossColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "止损比例",
                Binding = new System.Windows.Data.Binding("StopLossRatio") { StringFormat = "{0:P1}" },
                Width = new System.Windows.Controls.DataGridLength(80) // 固定宽度，确保百分比完整显示
            };
            stopLossColumn.ElementStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            stopLossColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            stopLossColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            stopLossColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            stopLossColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(220, 53, 69))));
            stopLossColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)));
            dataGrid.Columns.Add(stopLossColumn);

            panel.Children.Add(dataGrid);
            card.Child = panel;
            return card;
        }

        /// <summary>
        /// 创建保盈配置卡片
        /// </summary>
        private System.Windows.Controls.Border CreateProfitProtectionConfigCard()
        {
            var card = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(4, 0, 0, 0)
            };

            var panel = new System.Windows.Controls.StackPanel();

            var title = new System.Windows.Controls.TextBlock
            {
                Text = "💰 保盈配置详情",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(73, 80, 87)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            panel.Children.Add(title);

            // 🔧 创建保盈配置数据表格 - 支持动态数量档位
            var dataGrid = new System.Windows.Controls.DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.Horizontal,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                FontSize = 12, // 增加字体大小提升可读性
                RowHeight = 32, // 🔧 增加行高，确保文字完整显示
                HeadersVisibility = System.Windows.Controls.DataGridHeadersVisibility.Column,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(241, 243, 244)),
                ItemsSource = ProfitProtectionTiers,
                MaxHeight = 220, // 相应增加最大高度
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled
            };

            // 🔧 优化列宽设置，确保内容完整显示
            // 档位列
            var tierColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "档位",
                Binding = new System.Windows.Data.Binding("TierIndex"),
                Width = new System.Windows.Controls.DataGridLength(50) // 固定宽度，确保数字完整显示
            };
            tierColumn.ElementStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            tierColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            tierColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            tierColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            tierColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(73, 80, 87))));
            dataGrid.Columns.Add(tierColumn);

            // 触发浮盈列
            var triggerColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "触发浮盈",
                Binding = new System.Windows.Data.Binding("TriggerProfitAmount") { StringFormat = "{0:F0}U" },
                Width = new System.Windows.Controls.DataGridLength(85) // 固定宽度，确保数值和单位完整显示
            };
            triggerColumn.ElementStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            triggerColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            triggerColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            triggerColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            triggerColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(40, 167, 69))));
            triggerColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)));
            dataGrid.Columns.Add(triggerColumn);

            // 保护金额列
            var protectionColumn = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "保护金额",
                Binding = new System.Windows.Data.Binding("ProtectionAmount") { StringFormat = "{0:F0}U" },
                Width = new System.Windows.Controls.DataGridLength(85) // 固定宽度，确保数值和单位完整显示
            };
            protectionColumn.ElementStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            protectionColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            protectionColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            protectionColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            protectionColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(111, 66, 193))));
            protectionColumn.ElementStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)));
            dataGrid.Columns.Add(protectionColumn);

            panel.Children.Add(dataGrid);
            card.Child = panel;
            return card;
        }

        #endregion
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
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
        
        // 🔧 新增：动态进度显示支持（支持多次推仓多次止盈）
        public int AddPositionTotalTiers { get; set; }
        public int ProfitProtectionTotalTiers { get; set; }
        public string AddPositionProgressDisplay => $"{AddPositionProgress}/{AddPositionTotalTiers}";
        public string ProfitProtectionProgressDisplay => $"{ProfitProtectionProgress}/{ProfitProtectionTotalTiers}";
    }

    public class ExecutionHistoryDisplayModel
    {
        public DateTime ExecutionTime { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string ExecutionType { get; set; } = string.Empty;
        public string ResultText { get; set; } = string.Empty;
        public SolidColorBrush ResultColor { get; set; } = new(Colors.Gray);
        public decimal TriggerPnl { get; set; }
        public string Details { get; set; } = string.Empty;
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
}