using System;
using System.Windows;
using System.Windows.Controls;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Views
{
    public partial class TrailingStopConfigWindow : Window
    {
        public TrailingStopConfig Config { get; private set; }
        public bool IsConfirmed { get; private set; } = false;
        
        private bool _isUpdatingFromCode = false;

        public TrailingStopConfigWindow(TrailingStopConfig currentConfig)
        {
            InitializeComponent();
            
            // 防止空引用
            if (currentConfig == null)
            {
                currentConfig = new TrailingStopConfig();
            }
            
            Config = new TrailingStopConfig
            {
                Mode = currentConfig.Mode,
                AllocationRatio = currentConfig.AllocationRatio,
                OnlyForProfitablePositions = currentConfig.OnlyForProfitablePositions,
                MinCallbackRate = currentConfig.MinCallbackRate,
                MaxCallbackRate = currentConfig.MaxCallbackRate,
                FixedStopRatio = currentConfig.FixedStopRatio,
                TrailingStopRatio = currentConfig.TrailingStopRatio
            };
            
            // 先设置事件处理程序，再加载UI
            SetupEventHandlers();
            LoadConfigToUI();
            UpdatePreview();
        }

        private void LoadConfigToUI()
        {
            _isUpdatingFromCode = true;
            
            // 设置模式
            switch (Config.Mode)
            {
                case TrailingStopMode.Replace:
                    RbReplace.IsChecked = true;
                    break;
                case TrailingStopMode.Coexist:
                    RbCoexist.IsChecked = true;
                    break;
                case TrailingStopMode.SmartLayering:
                    RbSmartLayering.IsChecked = true;
                    break;
            }
            
            // 设置分配比例
            AllocationSlider.Value = (double)(Config.AllocationRatio * 100);
            AllocationTextBox.Text = (Config.AllocationRatio * 100).ToString("F1");
            
            // 设置处理范围
            if (Config.OnlyForProfitablePositions)
                RbProfitOnly.IsChecked = true;
            else
                RbAllPositions.IsChecked = true;
            
            // 设置回调率
            MinCallbackTextBox.Text = Config.MinCallbackRate.ToString("F1");
            MaxCallbackTextBox.Text = Config.MaxCallbackRate.ToString("F1");
            
            // 设置分层比例
            FixedStopTextBox.Text = (Config.FixedStopRatio * 100).ToString("F0");
            TrailingStopTextBox.Text = (Config.TrailingStopRatio * 100).ToString("F0");
            
            _isUpdatingFromCode = false;
            UpdateGroupVisibility();
        }

        private void SetupEventHandlers()
        {
            // 模式选择事件
            RbReplace.Checked += ModeRadioButton_Checked;
            RbCoexist.Checked += ModeRadioButton_Checked;
            RbSmartLayering.Checked += ModeRadioButton_Checked;
            
            // 处理范围事件
            RbProfitOnly.Checked += ScopeRadioButton_Checked;
            RbAllPositions.Checked += ScopeRadioButton_Checked;
            
            // 回调率事件
            MinCallbackTextBox.TextChanged += CallbackTextBox_TextChanged;
            MaxCallbackTextBox.TextChanged += CallbackTextBox_TextChanged;
        }

        private void ModeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingFromCode) return;
            
            if (RbReplace.IsChecked == true)
                Config.Mode = TrailingStopMode.Replace;
            else if (RbCoexist.IsChecked == true)
                Config.Mode = TrailingStopMode.Coexist;
            else if (RbSmartLayering.IsChecked == true)
                Config.Mode = TrailingStopMode.SmartLayering;
            
            UpdateGroupVisibility();
            UpdatePreview();
        }

        private void ScopeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingFromCode) return;
            
            Config.OnlyForProfitablePositions = RbProfitOnly.IsChecked == true;
            UpdatePreview();
        }

        private void AllocationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingFromCode) return;
            
            // 添加空引用检查
            if (AllocationTextBox == null || Config == null || e == null) return;
            
            _isUpdatingFromCode = true;
            AllocationTextBox.Text = e.NewValue.ToString("F1");
            Config.AllocationRatio = (decimal)(e.NewValue / 100);
            _isUpdatingFromCode = false;
            
            UpdatePreview();
        }

        private void AllocationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFromCode) return;
            
            if (decimal.TryParse(AllocationTextBox.Text, out decimal value) && value >= 1 && value <= 100)
            {
                _isUpdatingFromCode = true;
                AllocationSlider.Value = (double)value;
                Config.AllocationRatio = value / 100;
                _isUpdatingFromCode = false;
                
                AllocationTextBox.Background = System.Windows.Media.Brushes.White;
                UpdatePreview();
            }
            else if (!string.IsNullOrEmpty(AllocationTextBox.Text))
            {
                AllocationTextBox.Background = System.Windows.Media.Brushes.LightPink;
            }
        }

        private void CallbackTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFromCode) return;
            
            ValidateAndUpdateCallback();
        }

        private void LayeringTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFromCode) return;
            
            ValidateAndUpdateLayering();
        }

        private void UpdateGroupVisibility()
        {
            // 分配比例组仅在并存模式时显示
            AllocationGroup.Visibility = Config.Mode == TrailingStopMode.Coexist ? Visibility.Visible : Visibility.Collapsed;
            
            // 分层比例组仅在智能分层模式时显示
            LayeringGroup.Visibility = Config.Mode == TrailingStopMode.SmartLayering ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ValidateAndUpdateCallback()
        {
            bool isMinValid = decimal.TryParse(MinCallbackTextBox.Text, out decimal minValue) && minValue >= 1 && minValue <= 50;
            bool isMaxValid = decimal.TryParse(MaxCallbackTextBox.Text, out decimal maxValue) && maxValue >= 1 && maxValue <= 50;
            bool isRangeValid = isMinValid && isMaxValid && minValue < maxValue;
            
            MinCallbackTextBox.Background = isMinValid ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.LightPink;
            MaxCallbackTextBox.Background = isMaxValid ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.LightPink;
            
            if (isRangeValid)
            {
                Config.MinCallbackRate = minValue;
                Config.MaxCallbackRate = maxValue;
                UpdatePreview();
            }
        }

        private void ValidateAndUpdateLayering()
        {
            bool isFixedValid = decimal.TryParse(FixedStopTextBox.Text, out decimal fixedValue) && fixedValue >= 0 && fixedValue <= 100;
            bool isTrailingValid = decimal.TryParse(TrailingStopTextBox.Text, out decimal trailingValue) && trailingValue >= 0 && trailingValue <= 100;
            bool isSumValid = isFixedValid && isTrailingValid && Math.Abs(fixedValue + trailingValue - 100) < 0.1m;
            
            FixedStopTextBox.Background = isFixedValid ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.LightPink;
            TrailingStopTextBox.Background = isTrailingValid ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.LightPink;
            
            if (isSumValid)
            {
                Config.FixedStopRatio = fixedValue / 100;
                Config.TrailingStopRatio = trailingValue / 100;
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            // 添加空引用检查
            if (Config == null || PreviewTextBlock == null) return;
            
            var modeDescription = Config.Mode switch
            {
                TrailingStopMode.Replace => "替换模式",
                TrailingStopMode.Coexist => "并存模式",
                TrailingStopMode.SmartLayering => "智能分层模式",
                _ => "未知模式"
            };

            var preview = $"【配置预览】\n\n";
            preview += $"🎯 模式: {modeDescription}\n";
            preview += $"📊 处理范围: {(Config.OnlyForProfitablePositions ? "仅盈利持仓" : "所有持仓")}\n";
            preview += $"📈 回调率: {Config.MinCallbackRate:F1}% - {Config.MaxCallbackRate:F1}%\n";
            
            if (Config.Mode == TrailingStopMode.Coexist)
            {
                preview += $"⚖️ 分配比例: {Config.AllocationRatio:P1}\n";
            }
            else if (Config.Mode == TrailingStopMode.SmartLayering)
            {
                preview += $"🏗️ 分层比例: 固定{Config.FixedStopRatio:P0} + 移动{Config.TrailingStopRatio:P0}\n";
            }
            
            preview += $"\n💡 说明:\n";
            preview += Config.Mode switch
            {
                TrailingStopMode.Replace => "将现有止损单替换为移动止损单",
                TrailingStopMode.Coexist => "保留现有止损，另外添加移动止损",
                TrailingStopMode.SmartLayering => "智能分配固定止损和移动止损",
                _ => "未知模式"
            };

            PreviewTextBlock.Text = preview;
        }

        private bool ValidateAllInputs()
        {
            // 验证回调率
            bool isMinValid = decimal.TryParse(MinCallbackTextBox.Text, out decimal minValue) && minValue >= 1 && minValue <= 50;
            bool isMaxValid = decimal.TryParse(MaxCallbackTextBox.Text, out decimal maxValue) && maxValue >= 1 && maxValue <= 50;
            bool isCallbackRangeValid = isMinValid && isMaxValid && minValue < maxValue;
            
            if (!isCallbackRangeValid)
            {
                MessageBox.Show("回调率设置无效。最小值和最大值都必须在1-50%范围内，且最小值小于最大值。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            // 验证分配比例（仅并存模式）
            if (Config.Mode == TrailingStopMode.Coexist)
            {
                bool isAllocationValid = decimal.TryParse(AllocationTextBox.Text, out decimal allocation) && allocation >= 1 && allocation <= 100;
                if (!isAllocationValid)
                {
                    MessageBox.Show("分配比例必须在1-100%范围内。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            
            // 验证分层比例（仅智能分层模式）
            if (Config.Mode == TrailingStopMode.SmartLayering)
            {
                bool isFixedValid = decimal.TryParse(FixedStopTextBox.Text, out decimal fixedValue) && fixedValue >= 0 && fixedValue <= 100;
                bool isTrailingValid = decimal.TryParse(TrailingStopTextBox.Text, out decimal trailingValue) && trailingValue >= 0 && trailingValue <= 100;
                bool isSumValid = isFixedValid && isTrailingValid && Math.Abs(fixedValue + trailingValue - 100) < 0.1m;
                
                if (!isSumValid)
                {
                    MessageBox.Show("分层比例设置无效。固定止损和移动止损的比例之和必须等于100%。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            
            return true;
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateAllInputs())
            {
                IsConfirmed = true;
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            Config = new TrailingStopConfig(); // 重置为默认值
            LoadConfigToUI();
            UpdatePreview();
        }
    }
}