using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views
{
    public partial class ContractConfigEditWindow : Window
    {
        private readonly ILogger? _logger;
        private ContractConfigViewModel _originalConfig;
        private ContractConfigViewModel _editedConfig;

        public ContractConfigViewModel EditedConfig => _editedConfig;
        public bool IsConfirmed { get; private set; } = false;

        public ContractConfigEditWindow(ContractConfigViewModel config, ILogger? logger = null)
        {
            InitializeComponent();
            _logger = logger;
            _originalConfig = config;
            _editedConfig = CloneConfig(config);
            
            InitializeUI();
            LoadConfigData();
        }

        private void InitializeUI()
        {
            // 设置窗口标题
            TitleTextBlock.Text = $"编辑合约配置 - {_originalConfig.ContractName}";
            
            // 设置浮盈显示
            var pnlText = _originalConfig.CurrentPnl >= 0 
                ? $"+{_originalConfig.CurrentPnl:F2} USDT" 
                : $"{_originalConfig.CurrentPnl:F2} USDT";
            var pnlColor = _originalConfig.CurrentPnl >= 0 ? "Green" : "Red";
            
            SubtitleTextBlock.Text = $"当前浮盈: {pnlText}";
            SubtitleTextBlock.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(pnlColor);
        }

        private void LoadConfigData()
        {
            try
            {
                // 加载保本配置
                BreakEvenTargetTextBox.Text = _editedConfig.BreakEvenTarget.ToString("F2");
                SetComboBoxSelection(BreakEvenStatusComboBox, _editedConfig.BreakEvenStatus);

                // 加载推仓配置（这里需要从实际的配置数据获取触发金额）
                // 暂时使用默认值，实际项目中应该从配置文件或数据库读取
                PushTier1AmountTextBox.Text = "50.00";
                PushTier2AmountTextBox.Text = "100.00";
                PushTier3AmountTextBox.Text = "150.00";
                PushTier4AmountTextBox.Text = "200.00";

                SetComboBoxSelection(PushTier1StatusComboBox, _editedConfig.PushTier1Status);
                SetComboBoxSelection(PushTier2StatusComboBox, _editedConfig.PushTier2Status);
                SetComboBoxSelection(PushTier3StatusComboBox, _editedConfig.PushTier3Status);
                SetComboBoxSelection(PushTier4StatusComboBox, _editedConfig.PushTier4Status);

                // 加载保盈配置
                ProfitTier1AmountTextBox.Text = "500.00";
                ProfitTier2AmountTextBox.Text = "1000.00";
                ProfitTier3AmountTextBox.Text = "1500.00";

                SetComboBoxSelection(ProfitTier1StatusComboBox, _editedConfig.ProfitTier1Status);
                SetComboBoxSelection(ProfitTier2StatusComboBox, _editedConfig.ProfitTier2Status);
                SetComboBoxSelection(ProfitTier3StatusComboBox, _editedConfig.ProfitTier3Status);

                _logger?.LogInformation($"已加载合约配置数据: {_editedConfig.ContractName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载配置数据失败");
                MessageBox.Show($"加载配置数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetComboBoxSelection(ComboBox comboBox, string status)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString() == status)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private string GetComboBoxSelection(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "-";
        }

        private ContractConfigViewModel CloneConfig(ContractConfigViewModel original)
        {
            return new ContractConfigViewModel
            {
                ContractName = original.ContractName,
                CurrentPnl = original.CurrentPnl,
                BreakEvenTarget = original.BreakEvenTarget,
                BreakEvenStatus = original.BreakEvenStatus,
                PushTier1Status = original.PushTier1Status,
                PushTier2Status = original.PushTier2Status,
                PushTier3Status = original.PushTier3Status,
                PushTier4Status = original.PushTier4Status,
                ProfitTier1Status = original.ProfitTier1Status,
                ProfitTier2Status = original.ProfitTier2Status,
                ProfitTier3Status = original.ProfitTier3Status,
                UpdateTime = DateTime.Now.ToString("HH:mm:ss")
            };
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证并保存配置
                if (!ValidateInputs())
                {
                    return;
                }

                // 更新编辑后的配置
                if (decimal.TryParse(BreakEvenTargetTextBox.Text, out decimal breakEvenTarget))
                {
                    _editedConfig.BreakEvenTarget = breakEvenTarget;
                }

                _editedConfig.BreakEvenStatus = GetComboBoxSelection(BreakEvenStatusComboBox);
                _editedConfig.PushTier1Status = GetComboBoxSelection(PushTier1StatusComboBox);
                _editedConfig.PushTier2Status = GetComboBoxSelection(PushTier2StatusComboBox);
                _editedConfig.PushTier3Status = GetComboBoxSelection(PushTier3StatusComboBox);
                _editedConfig.PushTier4Status = GetComboBoxSelection(PushTier4StatusComboBox);
                _editedConfig.ProfitTier1Status = GetComboBoxSelection(ProfitTier1StatusComboBox);
                _editedConfig.ProfitTier2Status = GetComboBoxSelection(ProfitTier2StatusComboBox);
                _editedConfig.ProfitTier3Status = GetComboBoxSelection(ProfitTier3StatusComboBox);
                _editedConfig.UpdateTime = DateTime.Now.ToString("HH:mm:ss");

                IsConfirmed = true;
                _logger?.LogInformation($"保存合约配置: {_editedConfig.ContractName}");

                // 显示保存成功的消息
                MessageBox.Show($"合约配置已保存！\n\n合约: {_editedConfig.ContractName}\n保本目标: {_editedConfig.BreakEvenTarget:F2} USDT\n更新时间: {_editedConfig.UpdateTime}", 
                    "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存配置失败");
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("确定要重置为默认配置吗？\n\n这将清除所有修改的内容。", 
                    "确认重置", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 重置为原始配置
                    _editedConfig = CloneConfig(_originalConfig);
                    LoadConfigData();
                    
                    _logger?.LogInformation($"重置合约配置: {_editedConfig.ContractName}");
                    MessageBox.Show("配置已重置为默认值", "重置完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "重置配置失败");
                MessageBox.Show($"重置配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要取消编辑吗？\n\n所有修改的内容将丢失。", 
                "确认取消", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                IsConfirmed = false;
                _logger?.LogInformation($"取消编辑合约配置: {_originalConfig.ContractName}");
                DialogResult = false;
                Close();
            }
        }

        private bool ValidateInputs()
        {
            // 验证保本目标金额
            if (!decimal.TryParse(BreakEvenTargetTextBox.Text, out decimal breakEvenTarget) || breakEvenTarget < 0)
            {
                MessageBox.Show("保本目标金额必须是有效的非负数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                BreakEvenTargetTextBox.Focus();
                return false;
            }

            // 验证推仓触发金额
            if (!decimal.TryParse(PushTier1AmountTextBox.Text, out decimal pushTier1) || pushTier1 < 0)
            {
                MessageBox.Show("推仓1档触发金额必须是有效的非负数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                PushTier1AmountTextBox.Focus();
                return false;
            }

            if (!decimal.TryParse(PushTier2AmountTextBox.Text, out decimal pushTier2) || pushTier2 < 0)
            {
                MessageBox.Show("推仓2档触发金额必须是有效的非负数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                PushTier2AmountTextBox.Focus();
                return false;
            }

            if (!decimal.TryParse(PushTier3AmountTextBox.Text, out decimal pushTier3) || pushTier3 < 0)
            {
                MessageBox.Show("推仓3档触发金额必须是有效的非负数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                PushTier3AmountTextBox.Focus();
                return false;
            }

            if (!decimal.TryParse(PushTier4AmountTextBox.Text, out decimal pushTier4) || pushTier4 < 0)
            {
                MessageBox.Show("推仓4档触发金额必须是有效的非负数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                PushTier4AmountTextBox.Focus();
                return false;
            }

            // 验证保盈触发金额
            if (!decimal.TryParse(ProfitTier1AmountTextBox.Text, out decimal profitTier1) || profitTier1 < 0)
            {
                MessageBox.Show("保盈1档触发金额必须是有效的非负数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProfitTier1AmountTextBox.Focus();
                return false;
            }

            if (!decimal.TryParse(ProfitTier2AmountTextBox.Text, out decimal profitTier2) || profitTier2 < 0)
            {
                MessageBox.Show("保盈2档触发金额必须是有效的非负数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProfitTier2AmountTextBox.Focus();
                return false;
            }

            if (!decimal.TryParse(ProfitTier3AmountTextBox.Text, out decimal profitTier3) || profitTier3 < 0)
            {
                MessageBox.Show("保盈3档触发金额必须是有效的非负数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProfitTier3AmountTextBox.Focus();
                return false;
            }

            // 验证阶梯递增关系
            if (pushTier1 >= pushTier2 || pushTier2 >= pushTier3 || pushTier3 >= pushTier4)
            {
                MessageBox.Show("推仓阶梯触发金额必须递增：1档 < 2档 < 3档 < 4档", "逻辑错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (profitTier1 >= profitTier2 || profitTier2 >= profitTier3)
            {
                MessageBox.Show("保盈阶梯触发金额必须递增：1档 < 2档 < 3档", "逻辑错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
} 