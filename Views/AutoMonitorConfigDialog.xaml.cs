using System;
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

        public AutoMonitorConfigDialog()
        {
            // 初始化集合
            AddPositionStages = new ObservableCollection<AddPositionStageViewModel>();
            ProfitProtectionStages = new ObservableCollection<ProfitProtectionStageViewModel>();

            InitializeComponent();
            InitializeDefaults();
            InitializeDataGrids();
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
        /// 初始化阶梯默认值
        /// </summary>
        private void InitializeStageDefaults()
        {
            // 推仓阶梯默认配置
            AddPositionStages.Clear();
            AddPositionStages.Add(new AddPositionStageViewModel { Stage = 1, TriggerProfitAmount = 20, RiskCapitalMultiplier = 1.2m, StopLossPercentage = 80, IsEnabled = true, Description = "浮盈20U时推仓，风险金1.2倍，止损80%" });
            AddPositionStages.Add(new AddPositionStageViewModel { Stage = 2, TriggerProfitAmount = 50, RiskCapitalMultiplier = 1.5m, StopLossPercentage = 70, IsEnabled = true, Description = "浮盈50U时推仓，风险金1.5倍，止损70%" });
            AddPositionStages.Add(new AddPositionStageViewModel { Stage = 3, TriggerProfitAmount = 100, RiskCapitalMultiplier = 2.0m, StopLossPercentage = 60, IsEnabled = true, Description = "浮盈100U时推仓，风险金2.0倍，止损60%" });
            AddPositionStages.Add(new AddPositionStageViewModel { Stage = 4, TriggerProfitAmount = 200, RiskCapitalMultiplier = 2.5m, StopLossPercentage = 50, IsEnabled = false, Description = "浮盈200U时推仓，风险金2.5倍，止损50%" });

            // 保盈止损阶梯默认配置
            ProfitProtectionStages.Clear();
            ProfitProtectionStages.Add(new ProfitProtectionStageViewModel { Stage = 1, TriggerProfitAmount = 30, ProtectionAmount = 10, IsEnabled = true, Description = "浮盈30U时保护10U利润" });
            ProfitProtectionStages.Add(new ProfitProtectionStageViewModel { Stage = 2, TriggerProfitAmount = 80, ProtectionAmount = 30, IsEnabled = true, Description = "浮盈80U时保护30U利润" });
            ProfitProtectionStages.Add(new ProfitProtectionStageViewModel { Stage = 3, TriggerProfitAmount = 150, ProtectionAmount = 60, IsEnabled = true, Description = "浮盈150U时保护60U利润" });
        }

        /// <summary>
        /// 初始化DataGrid数据绑定
        /// </summary>
        private void InitializeDataGrids()
        {
            AddPositionDataGrid.ItemsSource = AddPositionStages;
            ProfitProtectionDataGrid.ItemsSource = ProfitProtectionStages;
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