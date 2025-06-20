using System;
using System.Windows;
using BinanceFuturesTrader.Converters;

namespace BinanceFuturesTrader.Views
{
    public partial class ProfitProtectionDialog : Window
    {
        public decimal ProfitProtectionAmount { get; private set; }
        
        private readonly string _symbol;
        private readonly string _direction;
        private readonly decimal _quantity;
        private readonly decimal _entryPrice;
        private readonly decimal _unrealizedProfit;
        private readonly decimal _currentPrice;

        public ProfitProtectionDialog(string symbol, string direction, decimal quantity, 
                                    decimal entryPrice, decimal unrealizedProfit, decimal currentPrice)
        {
            InitializeComponent();
            
            _symbol = symbol;
            _direction = direction;
            _quantity = quantity;
            _entryPrice = entryPrice;
            _unrealizedProfit = unrealizedProfit;
            _currentPrice = currentPrice;
            
            // 初始化界面数据
            InitializeData();
        }

        private void InitializeData()
        {
            try
            {
                // 设置持仓信息
                SymbolText.Text = _symbol;
                DirectionText.Text = _direction;
                QuantityText.Text = $"{_quantity:F6}";
                EntryPriceText.Text = PriceFormatConverter.FormatPrice(_entryPrice);
                CurrentPriceText.Text = PriceFormatConverter.FormatPrice(_currentPrice);
                
                // 根据浮盈设置颜色
                UnrealizedProfitText.Text = $"{_unrealizedProfit:F2} USDT";
                UnrealizedProfitText.Foreground = _unrealizedProfit >= 0 ? 
                    System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
                
                // 设置初始计算结果
                CalculationResultText.Text = "请输入保底盈利金额，然后点击\"预览计算\"查看止损价";
                
                // 聚焦到输入框
                ProfitProtectionTextBox.Focus();
                ProfitProtectionTextBox.SelectAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化界面数据失败：{ex.Message}", "初始化错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!decimal.TryParse(ProfitProtectionTextBox.Text, out decimal protectionAmount))
                {
                    MessageBox.Show("请输入有效的保底盈利金额", "输入错误", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    ProfitProtectionTextBox.Focus();
                    return;
                }

                if (protectionAmount <= 0)
                {
                    MessageBox.Show("保底盈利必须大于0", "输入错误", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    ProfitProtectionTextBox.Focus();
                    return;
                }

                if (protectionAmount >= _unrealizedProfit)
                {
                    MessageBox.Show($"保底盈利（{protectionAmount:F2}）必须小于当前浮盈（{_unrealizedProfit:F2}）", 
                        "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ProfitProtectionTextBox.Focus();
                    return;
                }

                // 计算止损价
                var isLong = _direction == "做多";
                decimal stopPrice;
                
                // 🔧 修复：考虑滑点和手续费损耗，在开仓价基础上加1U缓冲
                var bufferPerUnit = 1.0m / _quantity; // 1U分摊到每个单位的价格缓冲
                
                if (isLong)
                {
                    // 做多：止损价 = 开仓价 + (保底盈利 / 持仓数量) + 缓冲
                    stopPrice = _entryPrice + (protectionAmount / _quantity) + bufferPerUnit;
                }
                else
                {
                    // 做空：止损价 = 开仓价 - (保底盈利 / 持仓数量) - 缓冲
                    stopPrice = _entryPrice - (protectionAmount / _quantity) - bufferPerUnit;
                }

                // 调整价格精度（简化版）
                stopPrice = Math.Round(stopPrice, 4);

                // 验证止损价的合理性
                bool isValid = false;
                string validationMessage = "";
                
                if (isLong)
                {
                    isValid = stopPrice < _currentPrice;
                    validationMessage = isValid ? "✅ 合理" : "❌ 做多止损价应低于当前价";
                }
                else
                {
                    isValid = stopPrice > _currentPrice;
                    validationMessage = isValid ? "✅ 合理" : "❌ 做空止损价应高于当前价";
                }

                // 显示计算结果
                var resultText = $"📊 计算结果：\n" +
                               $"止损价：{PriceFormatConverter.FormatPrice(stopPrice)}\n" +
                               $"当前价：{PriceFormatConverter.FormatPrice(_currentPrice)}\n" +
                               $"保底盈利：{protectionAmount:F2} USDT\n" +
                               $"缓冲：{bufferPerUnit:F6} (1U分摊)\n" +
                               $"验证结果：{validationMessage}\n\n" +
                               $"💡 触发时实际收益约为：{protectionAmount:F2} USDT\n" +
                               $"🛡️ 已考虑滑点和手续费损耗";

                CalculationResultText.Text = resultText;
                CalculationResultText.Foreground = isValid ? 
                    System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Red;

                // 如果计算有效，启用确认按钮
                ConfirmButton.IsEnabled = isValid;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"计算预览失败：{ex.Message}", "计算错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!decimal.TryParse(ProfitProtectionTextBox.Text, out decimal protectionAmount))
                {
                    MessageBox.Show("请输入有效的保底盈利金额", "输入错误", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (protectionAmount <= 0)
                {
                    MessageBox.Show("保底盈利必须大于0", "输入错误", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (protectionAmount >= _unrealizedProfit)
                {
                    MessageBox.Show($"保底盈利（{protectionAmount:F2}）必须小于当前浮盈（{_unrealizedProfit:F2}）", 
                        "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 最终确认
                var result = MessageBox.Show(
                    $"确认设置保盈止损？\n\n" +
                    $"合约：{_symbol}\n" +
                    $"方向：{_direction}\n" +
                    $"保底盈利：{protectionAmount:F2} USDT\n\n" +
                    $"⚠️ 确认后将立即下单",
                    "最终确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ProfitProtectionAmount = protectionAmount;
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"确认设置失败：{ex.Message}", "确认错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SmartSuggestionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_unrealizedProfit > 0)
                {
                    var suggestedAmount = Math.Round(_unrealizedProfit * 0.5m, 2);
                    ProfitProtectionTextBox.Text = suggestedAmount.ToString("F2");
                    
                    // 自动触发预览计算
                    PreviewButton_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("当前无浮盈，无法应用智能建议", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用智能建议失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 