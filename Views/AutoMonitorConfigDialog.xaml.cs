using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// 推仓阶梯配置ViewModel
    /// </summary>
    public class AddPositionStageViewModel : INotifyPropertyChanged
    {
        private int _stage;
        private decimal _triggerProfitAmount;
        private decimal _riskCapitalMultiplier;
        private decimal _stopLossPercentage;
        private bool _isEnabled;
        private string _description = "";

        public int Stage
        {
            get => _stage;
            set { _stage = value; OnPropertyChanged(); }
        }

        public decimal TriggerProfitAmount
        {
            get => _triggerProfitAmount;
            set { _triggerProfitAmount = value; OnPropertyChanged(); }
        }

        public decimal RiskCapitalMultiplier
        {
            get => _riskCapitalMultiplier;
            set { _riskCapitalMultiplier = value; OnPropertyChanged(); }
        }

        public decimal StopLossPercentage
        {
            get => _stopLossPercentage;
            set { _stopLossPercentage = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 保盈止损阶梯配置ViewModel
    /// </summary>
    public class ProfitProtectionStageViewModel : INotifyPropertyChanged
    {
        private int _stage;
        private decimal _triggerProfitAmount;
        private decimal _protectionAmount;
        private bool _isEnabled;
        private string _description = "";

        public int Stage
        {
            get => _stage;
            set { _stage = value; OnPropertyChanged(); }
        }

        public decimal TriggerProfitAmount
        {
            get => _triggerProfitAmount;
            set { _triggerProfitAmount = value; OnPropertyChanged(); }
        }

        public decimal ProtectionAmount
        {
            get => _protectionAmount;
            set { _protectionAmount = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 自动盯盘配置对话框
    /// </summary>
    public partial class AutoMonitorConfigDialog : Window
    {
        /// <summary>
        /// 配置结果
        /// </summary>
        public AutoMonitorConfig? ConfigResult { get; private set; }

        /// <summary>
        /// 推仓阶梯数据
        /// </summary>
        public ObservableCollection<AddPositionStageViewModel> AddPositionStages { get; set; }

        /// <summary>
        /// 保盈止损阶梯数据
        /// </summary>
        public ObservableCollection<ProfitProtectionStageViewModel> ProfitProtectionStages { get; set; }

        // 账户信息（用于生成智能默认配置）
        private decimal _accountEquity = 1000m;
        private int _riskCapitalTimes = 10;

        public AutoMonitorConfigDialog()
        {
            // 初始化集合
            AddPositionStages = new ObservableCollection<AddPositionStageViewModel>();
            ProfitProtectionStages = new ObservableCollection<ProfitProtectionStageViewModel>();

            try
            {
                InitializeComponent();
                InitializeDefaults();
                InitializeDataGrids();
            }
            catch (Exception ex)
            {
                // 🔧 临时处理XAML编译问题
                System.Diagnostics.Debug.WriteLine($"AutoMonitorConfigDialog初始化失败: {ex.Message}");
                
                // 设置基本的对话框结果为取消
                this.DialogResult = false;
            }
        }

        /// <summary>
        /// 使用账户信息的构造函数（推荐使用）
        /// </summary>
        /// <param name="accountEquity">账户权益</param>
        /// <param name="riskCapitalTimes">风险金倍数</param>
        public AutoMonitorConfigDialog(decimal accountEquity, int riskCapitalTimes) : this()
        {
            _accountEquity = accountEquity > 0 ? accountEquity : 1000m; // 默认1000U
            _riskCapitalTimes = riskCapitalTimes > 0 ? riskCapitalTimes : 10; // 默认10倍
            
            // 重新初始化为智能默认配置
            InitializeSmartDefaults();
        }

        /// <summary>
        /// 初始化默认值
        /// </summary>
        private void InitializeDefaults()
        {
            // 基础设置默认值
            ConfigNameTextBox.Text = "默认配置";
            ScanIntervalTextBox.Text = "5";
            
            // 自动保本设置默认值
            BreakEvenEnabledCheckBox.IsChecked = false;
            BreakEvenTriggerTextBox.Text = "10";
            
            // 自动推仓设置默认值
            AddPositionEnabledCheckBox.IsChecked = false;
            
            // 自动保盈止损设置默认值
            ProfitProtectionEnabledCheckBox.IsChecked = false;
            
            InitializeStageDefaults();
        }

        /// <summary>
        /// 初始化智能默认配置
        /// </summary>
        private void InitializeSmartDefaults()
        {
            // 使用平衡型配置作为默认配置
            var smartConfig = CreateBalancedTemplate(_accountEquity, _riskCapitalTimes);
            ApplyConfigToUI(smartConfig);
        }

        /// <summary>
        /// 模板选择变化事件
        /// </summary>
        private void TemplateComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TemplateComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                string templateType = selectedItem.Tag?.ToString() ?? "Balanced";
                UpdateTemplateDescription(templateType);
            }
        }

        /// <summary>
        /// 应用模板按钮点击事件
        /// </summary>
        private void LoadTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TemplateComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
                {
                    string templateType = selectedItem.Tag?.ToString() ?? "Balanced";
                    
                    if (templateType == "Custom")
                    {
                        MessageBox.Show("自定义模式下，请手动调整各项参数。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    AutoMonitorConfig templateConfig = templateType switch
                    {
                        "Conservative" => CreateConservativeTemplate(_accountEquity, _riskCapitalTimes),
                        "Balanced" => CreateBalancedTemplate(_accountEquity, _riskCapitalTimes),
                        "Aggressive" => CreateAggressiveTemplate(_accountEquity, _riskCapitalTimes),
                        _ => CreateBalancedTemplate(_accountEquity, _riskCapitalTimes)
                    };

                    // 应用模板到UI
                    ApplyConfigToUI(templateConfig);
                    
                    MessageBox.Show($"已应用{selectedItem.Content}模板！\n请检查参数是否符合您的风险偏好。", 
                        "模板应用成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用模板时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新模板描述文字
        /// </summary>
        private void UpdateTemplateDescription(string templateType)
        {
            try
            {
                string description = templateType switch
                {
                    "Conservative" => "🛡️ 保守型：低风险配置，适合新手用户。保本门槛低，推仓倍数小，注重资金安全。",
                    "Balanced" => "⚖️ 平衡型：风险收益平衡，适合有经验用户。合理的保本和推仓参数，追求稳健收益。",
                    "Aggressive" => "🚀 激进型：高风险高收益，适合专业用户。更高的推仓倍数和更激进的参数设置。",
                    "Custom" => "🔧 自定义：完全由您自行配置所有参数，适合有丰富经验的高级用户。",
                    _ => "请选择合适的配置模板。"
                };
                
                // 🔧 修复空引用异常：检查控件是否已初始化
                if (TemplateDescriptionText != null)
                {
                    TemplateDescriptionText.Text = description;
                }
            }
            catch (Exception ex)
            {
                // 🔧 修复空引用异常：在异常处理中也要检查控件
                try
                {
                    if (TemplateDescriptionText != null)
                    {
                        TemplateDescriptionText.Text = "模板描述更新失败。";
                    }
                }
                catch
                {
                    // 静默处理，避免级联异常
                }
                System.Diagnostics.Debug.WriteLine($"UpdateTemplateDescription异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建保守型配置模板
        /// </summary>
        private AutoMonitorConfig CreateConservativeTemplate(decimal accountEquity, int riskCapitalTimes)
        {
            // 计算单倍风险金
            var singleRiskCapital = accountEquity / riskCapitalTimes;
            
            var config = new AutoMonitorConfig
            {
                Name = "保守型配置",
                ScanIntervalSeconds = 10, // 10秒扫描间隔
                
                // 保本配置：账户风险金的30%
                BreakEvenConfig = new AutoBreakEvenConfig
                {
                    IsEnabled = true,
                    TriggerProfitAmount = Math.Max(5m, singleRiskCapital * 0.3m) // 30%风险金
                },
                
                // 推仓配置：1-3档，每次推仓止盈金额为2倍、3倍、4倍风险金
                AddPositionConfig = new AutoAddPositionConfig
                {
                    IsEnabled = true,
                    Tiers = new List<AddPositionTier>
                    {
                        new AddPositionTier
                        {
                            TierIndex = 1,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(10m, singleRiskCapital * 2m), // 2倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier
                        {
                            TierIndex = 2,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(15m, singleRiskCapital * 3m), // 3倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier
                        {
                            TierIndex = 3,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(20m, singleRiskCapital * 4m), // 4倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier { TierIndex = 4, IsEnabled = false }
                    }
                },
                
                // 保盈配置：第一档8倍，第二档16倍，第三档30倍，回撤90%
                ProfitProtectionConfig = new AutoProfitProtectionConfig
                {
                    IsEnabled = true,
                    Tiers = new List<ProfitProtectionTier>
                    {
                        new ProfitProtectionTier
                        {
                            TierIndex = 1,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(40m, singleRiskCapital * 8m), // 8倍风险金
                            ProtectionAmount = Math.Max(36m, singleRiskCapital * 8m * 0.9m) // 90%回撤保护
                        },
                        new ProfitProtectionTier
                        {
                            TierIndex = 2,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(80m, singleRiskCapital * 16m), // 16倍风险金
                            ProtectionAmount = Math.Max(72m, singleRiskCapital * 16m * 0.9m) // 90%回撤保护
                        },
                        new ProfitProtectionTier
                        {
                            TierIndex = 3,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(150m, singleRiskCapital * 30m), // 30倍风险金
                            ProtectionAmount = Math.Max(135m, singleRiskCapital * 30m * 0.9m) // 90%回撤保护
                        }
                    }
                }
            };
            
            return config;
        }

        /// <summary>
        /// 创建平衡型配置模板
        /// </summary>
        private AutoMonitorConfig CreateBalancedTemplate(decimal accountEquity, int riskCapitalTimes)
        {
            // 计算单倍风险金
            var singleRiskCapital = accountEquity / riskCapitalTimes;
            
            var config = new AutoMonitorConfig
            {
                Name = "平衡型配置",
                ScanIntervalSeconds = 10, // 10秒扫描间隔
                
                // 保本配置：账户风险金的50%
                BreakEvenConfig = new AutoBreakEvenConfig
                {
                    IsEnabled = true,
                    TriggerProfitAmount = Math.Max(8m, singleRiskCapital * 0.5m) // 50%风险金
                },
                
                // 推仓配置：1-4档，每次推仓止盈金额为1倍到4倍风险金
                AddPositionConfig = new AutoAddPositionConfig
                {
                    IsEnabled = true,
                    Tiers = new List<AddPositionTier>
                    {
                        new AddPositionTier
                        {
                            TierIndex = 1,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(10m, singleRiskCapital * 1m), // 1倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier
                        {
                            TierIndex = 2,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(20m, singleRiskCapital * 2m), // 2倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier
                        {
                            TierIndex = 3,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(30m, singleRiskCapital * 3m), // 3倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier
                        {
                            TierIndex = 4,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(40m, singleRiskCapital * 4m), // 4倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        }
                    }
                },
                
                // 保盈配置：第一档10倍，第二档20倍，第三档30倍，回撤80%
                ProfitProtectionConfig = new AutoProfitProtectionConfig
                {
                    IsEnabled = true,
                    Tiers = new List<ProfitProtectionTier>
                    {
                        new ProfitProtectionTier
                        {
                            TierIndex = 1,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(50m, singleRiskCapital * 10m), // 10倍风险金
                            ProtectionAmount = Math.Max(40m, singleRiskCapital * 10m * 0.8m) // 80%回撤保护
                        },
                        new ProfitProtectionTier
                        {
                            TierIndex = 2,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(100m, singleRiskCapital * 20m), // 20倍风险金
                            ProtectionAmount = Math.Max(80m, singleRiskCapital * 20m * 0.8m) // 80%回撤保护
                        },
                        new ProfitProtectionTier
                        {
                            TierIndex = 3,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(150m, singleRiskCapital * 30m), // 30倍风险金
                            ProtectionAmount = Math.Max(120m, singleRiskCapital * 30m * 0.8m) // 80%回撤保护
                        }
                    }
                }
            };
            
            return config;
        }

        /// <summary>
        /// 创建激进型配置模板
        /// </summary>
        private AutoMonitorConfig CreateAggressiveTemplate(decimal accountEquity, int riskCapitalTimes)
        {
            // 计算单倍风险金
            var singleRiskCapital = accountEquity / riskCapitalTimes;
            
            var config = new AutoMonitorConfig
            {
                Name = "激进型配置",
                ScanIntervalSeconds = 10, // 10秒扫描间隔
                
                // 保本配置：账户风险金的80%
                BreakEvenConfig = new AutoBreakEvenConfig
                {
                    IsEnabled = true,
                    TriggerProfitAmount = Math.Max(12m, singleRiskCapital * 0.8m) // 80%风险金
                },
                
                // 推仓配置：1-8档，每次推仓止盈金额为1倍、2倍...8倍风险金
                AddPositionConfig = new AutoAddPositionConfig
                {
                    IsEnabled = true,
                    Tiers = new List<AddPositionTier>
                    {
                        new AddPositionTier
                        {
                            TierIndex = 1,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(10m, singleRiskCapital * 1m), // 1倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier
                        {
                            TierIndex = 2,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(20m, singleRiskCapital * 2m), // 2倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier
                        {
                            TierIndex = 3,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(30m, singleRiskCapital * 3m), // 3倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier
                        {
                            TierIndex = 4,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(40m, singleRiskCapital * 4m), // 4倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        // 激进型特有：扩展到8档推仓
                        new AddPositionTier
                        {
                            TierIndex = 5,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(50m, singleRiskCapital * 5m), // 5倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier
                        {
                            TierIndex = 6,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(60m, singleRiskCapital * 6m), // 6倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier
                        {
                            TierIndex = 7,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(70m, singleRiskCapital * 7m), // 7倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        },
                        new AddPositionTier
                        {
                            TierIndex = 8,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(80m, singleRiskCapital * 8m), // 8倍风险金
                            RiskMultiplier = 1.0m, // 加仓倍数为1倍风险金
                            StopLossRatio = 0.10m // 止损金额为10%
                        }
                    }
                },
                
                // 保盈配置：4档保盈，第一、二档60%回撤，第三档80%回撤，第四档90%回撤
                ProfitProtectionConfig = new AutoProfitProtectionConfig
                {
                    IsEnabled = true,
                    Tiers = new List<ProfitProtectionTier>
                    {
                        new ProfitProtectionTier
                        {
                            TierIndex = 1,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(50m, singleRiskCapital * 10m), // 10倍风险金  
                            ProtectionAmount = Math.Max(30m, singleRiskCapital * 10m * 0.6m) // 60%回撤保护
                        },
                        new ProfitProtectionTier
                        {
                            TierIndex = 2,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(100m, singleRiskCapital * 20m), // 20倍风险金
                            ProtectionAmount = Math.Max(60m, singleRiskCapital * 20m * 0.6m) // 60%回撤保护
                        },
                        new ProfitProtectionTier
                        {
                            TierIndex = 3,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(150m, singleRiskCapital * 30m), // 30倍风险金
                            ProtectionAmount = Math.Max(120m, singleRiskCapital * 30m * 0.8m) // 80%回撤保护
                        },
                        new ProfitProtectionTier
                        {
                            TierIndex = 4,
                            IsEnabled = true,
                            TriggerProfitAmount = Math.Max(200m, singleRiskCapital * 40m), // 40倍风险金
                            ProtectionAmount = Math.Max(180m, singleRiskCapital * 40m * 0.9m) // 90%回撤保护
                        }
                    }
                }
            };
            
            return config;
        }

        /// <summary>
        /// 将配置应用到UI界面
        /// </summary>
        private void ApplyConfigToUI(AutoMonitorConfig config)
        {
            try
            {
                // 基础设置
                ConfigNameTextBox.Text = config.Name;
                ScanIntervalTextBox.Text = config.ScanIntervalSeconds.ToString();
                
                // 保本设置
                BreakEvenEnabledCheckBox.IsChecked = config.BreakEvenConfig.IsEnabled;
                BreakEvenTriggerTextBox.Text = config.BreakEvenConfig.TriggerProfitAmount.ToString("F1");
                
                // 推仓设置
                AddPositionEnabledCheckBox.IsChecked = config.AddPositionConfig.IsEnabled;
                
                // 保盈设置
                ProfitProtectionEnabledCheckBox.IsChecked = config.ProfitProtectionConfig.IsEnabled;
                
                // 更新阶梯数据
                UpdateStageDataFromConfig(config);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用配置到界面时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从配置更新阶梯数据
        /// </summary>
        private void UpdateStageDataFromConfig(AutoMonitorConfig config)
        {
            try
            {
                // 更新推仓阶梯数据
                AddPositionStages.Clear();
                foreach (var tier in config.AddPositionConfig.Tiers.Take(4))
                {
                    AddPositionStages.Add(new AddPositionStageViewModel
                    {
                        Stage = tier.TierIndex,
                        TriggerProfitAmount = tier.TriggerProfitAmount,
                        RiskCapitalMultiplier = tier.RiskMultiplier,
                        StopLossPercentage = tier.StopLossRatio * 100, // 转换为百分比显示
                        IsEnabled = tier.IsEnabled,
                        Description = $"阶梯{tier.TierIndex} - {(tier.IsEnabled ? "启用" : "禁用")}"
                    });
                }
                
                // 更新保盈阶梯数据
                ProfitProtectionStages.Clear();
                foreach (var tier in config.ProfitProtectionConfig.Tiers.Take(3))
                {
                    ProfitProtectionStages.Add(new ProfitProtectionStageViewModel
                    {
                        Stage = tier.TierIndex,
                        TriggerProfitAmount = tier.TriggerProfitAmount,
                        ProtectionAmount = tier.ProtectionAmount,
                        IsEnabled = tier.IsEnabled,
                        Description = $"保盈{tier.TierIndex} - 盈利{tier.TriggerProfitAmount:F0}U时保护{tier.ProtectionAmount:F0}U"
                    });
                }
                
                UpdateStageCountDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新阶梯数据时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 初始化阶梯默认值（旧版本兼容）
        /// </summary>
        private void InitializeStageDefaults()
        {
            // 🔧 修改：使用风险金计算默认阶梯值
            var singleRiskCapital = _accountEquity / _riskCapitalTimes;
            var riskCapitalIncrement = Math.Round(singleRiskCapital, 0);
            
            // 推仓阶梯默认配置（基于风险金计算）
            AddPositionStages.Clear();
            AddPositionStages.Add(new AddPositionStageViewModel { 
                Stage = 1, 
                TriggerProfitAmount = riskCapitalIncrement, 
                RiskCapitalMultiplier = 1.0m, 
                StopLossPercentage = 10, 
                IsEnabled = true, 
                Description = $"浮盈{riskCapitalIncrement:F0}U时推仓，风险金1.0倍，止损10%" 
            });
            AddPositionStages.Add(new AddPositionStageViewModel { 
                Stage = 2, 
                TriggerProfitAmount = riskCapitalIncrement * 2, 
                RiskCapitalMultiplier = 1.0m, 
                StopLossPercentage = 10, 
                IsEnabled = true, 
                Description = $"浮盈{riskCapitalIncrement * 2:F0}U时推仓，风险金1.0倍，止损10%" 
            });
            AddPositionStages.Add(new AddPositionStageViewModel { 
                Stage = 3, 
                TriggerProfitAmount = riskCapitalIncrement * 3, 
                RiskCapitalMultiplier = 1.0m, 
                StopLossPercentage = 10, 
                IsEnabled = true, 
                Description = $"浮盈{riskCapitalIncrement * 3:F0}U时推仓，风险金1.0倍，止损10%" 
            });
            AddPositionStages.Add(new AddPositionStageViewModel { 
                Stage = 4, 
                TriggerProfitAmount = riskCapitalIncrement * 4, 
                RiskCapitalMultiplier = 1.0m, 
                StopLossPercentage = 10, 
                IsEnabled = false, 
                Description = $"浮盈{riskCapitalIncrement * 4:F0}U时推仓，风险金1.0倍，止损10%" 
            });

            // 保盈止损阶梯默认配置（基于风险金的10倍作为基础）
            var profitProtectionBase = riskCapitalIncrement * 10;
            ProfitProtectionStages.Clear();
            ProfitProtectionStages.Add(new ProfitProtectionStageViewModel { 
                Stage = 1, 
                TriggerProfitAmount = profitProtectionBase, 
                ProtectionAmount = profitProtectionBase * 0.8m, 
                IsEnabled = true, 
                Description = $"浮盈{profitProtectionBase:F0}U时保护{profitProtectionBase * 0.8m:F0}U利润" 
            });
            ProfitProtectionStages.Add(new ProfitProtectionStageViewModel { 
                Stage = 2, 
                TriggerProfitAmount = profitProtectionBase * 2, 
                ProtectionAmount = profitProtectionBase * 2 * 0.8m, 
                IsEnabled = true, 
                Description = $"浮盈{profitProtectionBase * 2:F0}U时保护{profitProtectionBase * 2 * 0.8m:F0}U利润" 
            });
            ProfitProtectionStages.Add(new ProfitProtectionStageViewModel { 
                Stage = 3, 
                TriggerProfitAmount = profitProtectionBase * 3, 
                ProtectionAmount = profitProtectionBase * 3 * 0.8m, 
                IsEnabled = true, 
                Description = $"浮盈{profitProtectionBase * 3:F0}U时保护{profitProtectionBase * 3 * 0.8m:F0}U利润" 
            });
        }

        /// <summary>
        /// 初始化DataGrid数据绑定
        /// </summary>
        private void InitializeDataGrids()
        {
            AddPositionDataGrid.ItemsSource = AddPositionStages;
            ProfitProtectionDataGrid.ItemsSource = ProfitProtectionStages;
            
            // 更新阶梯数量显示
            UpdateStageCountDisplay();
        }

        /// <summary>
        /// 更新阶梯数量显示
        /// </summary>
        private void UpdateStageCountDisplay()
        {
            AddPositionStageCountText.Text = $"（{AddPositionStages.Count}个阶梯）";
            ProfitProtectionStageCountText.Text = $"（{ProfitProtectionStages.Count}个阶梯）";
            
            // 更新删除按钮状态（至少保留1个阶梯）
            RemovePositionStageButton.IsEnabled = AddPositionStages.Count > 1;
            RemoveProfitProtectionStageButton.IsEnabled = ProfitProtectionStages.Count > 1;
        }

        /// <summary>
        /// 确认按钮点击事件
        /// </summary>
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证输入
                if (!ValidateInputs())
                {
                    return;
                }

                // 创建配置对象
                ConfigResult = CreateConfigFromInputs();

                // 设置对话框结果并关闭
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"配置创建失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 增加推仓阶梯按钮点击事件
        /// </summary>
        private void AddPositionStageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newStageIndex = AddPositionStages.Count + 1;
                var lastStage = AddPositionStages.LastOrDefault();
                
                // 🔧 修改：使用一份风险金作为默认间距，而不是固定100U
                var singleRiskCapital = _accountEquity / _riskCapitalTimes; // 计算一份风险金
                var riskCapitalIncrement = Math.Round(singleRiskCapital, 0); // 四舍五入到整数
                
                // 智能计算新阶梯的默认值
                var newTriggerAmount = lastStage != null 
                    ? lastStage.TriggerProfitAmount + riskCapitalIncrement 
                    : riskCapitalIncrement; // 第一个阶梯就是一份风险金
                    
                var newRiskMultiplier = lastStage != null ? lastStage.RiskCapitalMultiplier : 1.0m;
                var newStopLossPercentage = lastStage != null ? lastStage.StopLossPercentage : 10m;
                
                var newStage = new AddPositionStageViewModel
                {
                    Stage = newStageIndex,
                    TriggerProfitAmount = newTriggerAmount,
                    RiskCapitalMultiplier = newRiskMultiplier,
                    StopLossPercentage = newStopLossPercentage,
                    IsEnabled = true,
                    Description = $"浮盈{newTriggerAmount:F0}U时推仓，风险金{newRiskMultiplier:F1}倍，止损{newStopLossPercentage:F0}%"
                };
                
                AddPositionStages.Add(newStage);
                UpdateStageCountDisplay();
                
                // 显示智能计算的提示信息
                var message = $"已添加推仓阶梯{newStageIndex}：\n" +
                             $"触发值：{newTriggerAmount:F0}U\n" +
                             $"(基于一份风险金 {riskCapitalIncrement:F0}U = 账户权益{_accountEquity:F0}U ÷ 风险次数{_riskCapitalTimes})";
                MessageBox.Show(message, "阶梯添加成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加推仓阶梯失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除推仓阶梯按钮点击事件
        /// </summary>
        private void RemovePositionStageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AddPositionStages.Count > 1)
                {
                    var result = MessageBox.Show("确定要删除最后一个推仓阶梯吗？", "确认删除", 
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        AddPositionStages.RemoveAt(AddPositionStages.Count - 1);
                        UpdateStageCountDisplay();
                    }
                }
                else
                {
                    MessageBox.Show("至少需要保留一个推仓阶梯", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除推仓阶梯失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 增加保盈止损阶梯按钮点击事件
        /// </summary>
        private void ProfitProtectionStageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newStageIndex = ProfitProtectionStages.Count + 1;
                var lastStage = ProfitProtectionStages.LastOrDefault();
                
                // 智能计算新阶梯的默认值
                var newTriggerAmount = lastStage != null ? lastStage.TriggerProfitAmount + 1000 : 1000;
                var newProtectionAmount = lastStage != null ? newTriggerAmount * 0.8m : 800; // 保护80%利润
                
                var newStage = new ProfitProtectionStageViewModel
                {
                    Stage = newStageIndex,
                    TriggerProfitAmount = newTriggerAmount,
                    ProtectionAmount = newProtectionAmount,
                    IsEnabled = true,
                    Description = $"浮盈{newTriggerAmount:F0}U时保护{newProtectionAmount:F0}U利润"
                };
                
                ProfitProtectionStages.Add(newStage);
                UpdateStageCountDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加保盈止损阶梯失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除保盈止损阶梯按钮点击事件
        /// </summary>
        private void RemoveProfitProtectionStageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ProfitProtectionStages.Count > 1)
                {
                    var result = MessageBox.Show("确定要删除最后一个保盈止损阶梯吗？", "确认删除", 
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        ProfitProtectionStages.RemoveAt(ProfitProtectionStages.Count - 1);
                        UpdateStageCountDisplay();
                    }
                }
                else
                {
                    MessageBox.Show("至少需要保留一个保盈止损阶梯", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除保盈止损阶梯失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 验证输入
        /// </summary>
        private bool ValidateInputs()
        {
            // 验证配置名称
            if (string.IsNullOrWhiteSpace(ConfigNameTextBox.Text))
            {
                MessageBox.Show("请输入配置名称", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                ConfigNameTextBox.Focus();
                return false;
            }

            // 验证扫描间隔
            if (!int.TryParse(ScanIntervalTextBox.Text, out int scanInterval) || scanInterval < 1 || scanInterval > 60)
            {
                MessageBox.Show("扫描间隔必须是1-60之间的整数", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                ScanIntervalTextBox.Focus();
                return false;
            }

            // 验证自动保本触发值
            if (BreakEvenEnabledCheckBox.IsChecked == true)
            {
                if (!decimal.TryParse(BreakEvenTriggerTextBox.Text, out decimal trigger) || trigger <= 0)
                {
                    MessageBox.Show("自动保本触发盈利值必须大于0", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                    BreakEvenTriggerTextBox.Focus();
                    return false;
                }
            }

            // 验证推仓配置
            if (AddPositionEnabledCheckBox.IsChecked == true)
            {
                var enabledStages = AddPositionStages.Where(s => s.IsEnabled).ToList();
                if (!enabledStages.Any())
                {
                    MessageBox.Show("启用推仓功能时，必须至少启用一个推仓阶梯", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                foreach (var stage in enabledStages)
                {
                    if (stage.TriggerProfitAmount <= 0 || stage.RiskCapitalMultiplier <= 0 || stage.StopLossPercentage <= 0 || stage.StopLossPercentage > 100)
                    {
                        MessageBox.Show($"推仓阶梯{stage.Stage}的参数无效，请检查触发盈利值、风险金倍数和止损比例", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
            }

            // 验证保盈止损配置
            if (ProfitProtectionEnabledCheckBox.IsChecked == true)
            {
                var enabledStages = ProfitProtectionStages.Where(s => s.IsEnabled).ToList();
                if (!enabledStages.Any())
                {
                    MessageBox.Show("启用保盈止损功能时，必须至少启用一个保盈止损阶梯", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                foreach (var stage in enabledStages)
                {
                    if (stage.TriggerProfitAmount <= 0 || stage.ProtectionAmount <= 0 || stage.ProtectionAmount >= stage.TriggerProfitAmount)
                    {
                        MessageBox.Show($"保盈止损阶梯{stage.Stage}的参数无效，保护金额必须大于0且小于触发盈利值", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
            }

            // 至少启用一个功能
            if (BreakEvenEnabledCheckBox.IsChecked != true && 
                AddPositionEnabledCheckBox.IsChecked != true &&
                ProfitProtectionEnabledCheckBox.IsChecked != true)
            {
                var result = MessageBox.Show("您没有启用任何自动功能，是否继续？", "确认", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 从输入创建配置对象
        /// </summary>
        private AutoMonitorConfig CreateConfigFromInputs()
        {
            var config = new AutoMonitorConfig
            {
                Name = ConfigNameTextBox.Text.Trim(),
                IsEnabled = true,
                ScanIntervalSeconds = int.Parse(ScanIntervalTextBox.Text),
                CreateTime = DateTime.Now,
                LastModifiedTime = DateTime.Now
            };

            // 自动保本配置
            config.BreakEvenConfig.IsEnabled = BreakEvenEnabledCheckBox.IsChecked == true;
            if (config.BreakEvenConfig.IsEnabled)
            {
                config.BreakEvenConfig.TriggerProfitAmount = decimal.Parse(BreakEvenTriggerTextBox.Text);
            }

            // 自动推仓配置
            config.AddPositionConfig.IsEnabled = AddPositionEnabledCheckBox.IsChecked == true;
            if (config.AddPositionConfig.IsEnabled)
            {
                config.AddPositionConfig.Tiers.Clear();
                foreach (var viewModel in AddPositionStages.Where(s => s.IsEnabled))
                {
                    config.AddPositionConfig.Tiers.Add(new AddPositionTier
                    {
                        TierIndex = viewModel.Stage,
                        TriggerProfitAmount = viewModel.TriggerProfitAmount,
                        RiskMultiplier = viewModel.RiskCapitalMultiplier,
                        StopLossRatio = viewModel.StopLossPercentage / 100,
                        IsTriggered = false
                    });
                }
            }

            // 自动保盈止损配置
            config.ProfitProtectionConfig.IsEnabled = ProfitProtectionEnabledCheckBox.IsChecked == true;
            if (config.ProfitProtectionConfig.IsEnabled)
            {
                config.ProfitProtectionConfig.Tiers.Clear();
                foreach (var viewModel in ProfitProtectionStages.Where(s => s.IsEnabled))
                {
                    config.ProfitProtectionConfig.Tiers.Add(new ProfitProtectionTier
                    {
                        TierIndex = viewModel.Stage,
                        TriggerProfitAmount = viewModel.TriggerProfitAmount,
                        ProtectionAmount = viewModel.ProtectionAmount,
                        IsTriggered = false
                    });
                }
            }

            return config;
        }

        /// <summary>
        /// 设置现有配置（用于编辑现有配置）
        /// </summary>
        public void SetConfig(AutoMonitorConfig config)
        {
            if (config == null) return;

            // 基础设置
            ConfigNameTextBox.Text = config.Name;
            ScanIntervalTextBox.Text = config.ScanIntervalSeconds.ToString();

            // 自动保本设置
            BreakEvenEnabledCheckBox.IsChecked = config.BreakEvenConfig.IsEnabled;
            BreakEvenTriggerTextBox.Text = config.BreakEvenConfig.TriggerProfitAmount.ToString();

            // 自动推仓设置
            AddPositionEnabledCheckBox.IsChecked = config.AddPositionConfig.IsEnabled;
            if (config.AddPositionConfig.Tiers.Any())
            {
                AddPositionStages.Clear();
                foreach (var stage in config.AddPositionConfig.Tiers)
                {
                    AddPositionStages.Add(new AddPositionStageViewModel
                    {
                        Stage = stage.TierIndex,
                        TriggerProfitAmount = stage.TriggerProfitAmount,
                        RiskCapitalMultiplier = stage.RiskMultiplier,
                        StopLossPercentage = stage.StopLossRatio * 100,
                        IsEnabled = !stage.IsTriggered,
                        Description = $"浮盈{stage.TriggerProfitAmount}U时推仓，风险金{stage.RiskMultiplier}倍，止损{stage.StopLossRatio * 100}%"
                    });
                }
            }

            // 自动保盈止损设置
            ProfitProtectionEnabledCheckBox.IsChecked = config.ProfitProtectionConfig.IsEnabled;
            if (config.ProfitProtectionConfig.Tiers.Any())
            {
                ProfitProtectionStages.Clear();
                foreach (var stage in config.ProfitProtectionConfig.Tiers)
                {
                    ProfitProtectionStages.Add(new ProfitProtectionStageViewModel
                    {
                        Stage = stage.TierIndex,
                        TriggerProfitAmount = stage.TriggerProfitAmount,
                        ProtectionAmount = stage.ProtectionAmount,
                        IsEnabled = !stage.IsTriggered,
                        Description = $"浮盈{stage.TriggerProfitAmount}U时保护{stage.ProtectionAmount}U利润"
                    });
                }
            }
        }
    }
} 