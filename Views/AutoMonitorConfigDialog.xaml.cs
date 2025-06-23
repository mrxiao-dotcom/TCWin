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

        // 账户信息（用于生成智能默认配置）
        private decimal _accountEquity = 1000m;
        private int _riskCapitalTimes = 10;

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
            // 使用智能配置生成器
            var smartConfig = AutoMonitorConfig.CreateSmartDefault(_accountEquity, _riskCapitalTimes);
            
            // 基础设置
            ConfigNameTextBox.Text = smartConfig.Name;
            ScanIntervalTextBox.Text = smartConfig.ScanIntervalSeconds.ToString();
            
            // 自动保本设置
            BreakEvenEnabledCheckBox.IsChecked = smartConfig.BreakEvenConfig.IsEnabled;
            BreakEvenTriggerTextBox.Text = smartConfig.BreakEvenConfig.TriggerProfitAmount.ToString("F0");
            
            // 自动推仓设置
            AddPositionEnabledCheckBox.IsChecked = smartConfig.AddPositionConfig.IsEnabled;
            
            // 自动保盈止损设置
            ProfitProtectionEnabledCheckBox.IsChecked = smartConfig.ProfitProtectionConfig.IsEnabled;
            
            InitializeSmartStageDefaults(smartConfig);
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
        /// 初始化智能阶梯默认值
        /// </summary>
        private void InitializeSmartStageDefaults(AutoMonitorConfig smartConfig)
        {
            // 推仓阶梯智能配置
            AddPositionStages.Clear();
            foreach (var tier in smartConfig.AddPositionConfig.Tiers)
            {
                AddPositionStages.Add(new AddPositionStageViewModel 
                { 
                    Stage = tier.TierIndex, 
                    TriggerProfitAmount = tier.TriggerProfitAmount, 
                    RiskCapitalMultiplier = tier.RiskMultiplier, 
                    StopLossPercentage = tier.StopLossRatio * 100, // 转换为百分比显示
                    IsEnabled = true, 
                    Description = $"浮盈{tier.TriggerProfitAmount:F0}U时推仓，风险金{tier.RiskMultiplier:F1}倍，止损{tier.StopLossRatio * 100:F0}%" 
                });
            }

            // 保盈止损阶梯智能配置
            ProfitProtectionStages.Clear();
            foreach (var tier in smartConfig.ProfitProtectionConfig.Tiers)
            {
                ProfitProtectionStages.Add(new ProfitProtectionStageViewModel 
                { 
                    Stage = tier.TierIndex, 
                    TriggerProfitAmount = tier.TriggerProfitAmount, 
                    ProtectionAmount = tier.ProtectionAmount, 
                    IsEnabled = true, 
                    Description = $"浮盈{tier.TriggerProfitAmount:F0}U时保护{tier.ProtectionAmount:F0}U利润" 
                });
            }
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