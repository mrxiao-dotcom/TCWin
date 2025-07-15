using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BinanceFuturesTrader.Converters;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// 分仓止盈对话框
    /// </summary>
    public partial class PartialProfitDialog : Window
    {
        private readonly string _symbol;
        private readonly string _direction;
        private readonly decimal _quantity;
        private readonly decimal _entryPrice;
        private readonly decimal _currentPrice;
        private readonly decimal _unrealizedProfit;
        
        private List<SplitOrderInfo> _splitOrders = new List<SplitOrderInfo>();
        
        public List<PartialProfitRequest> ProfitRequests { get; private set; } = new List<PartialProfitRequest>();

        public PartialProfitDialog(string symbol, string direction, decimal quantity, 
            decimal entryPrice, decimal unrealizedProfit, decimal currentPrice)
        {
            InitializeComponent();
            
            // 参数验证和调试信息
            System.Diagnostics.Debug.WriteLine($"PartialProfitDialog构造函数参数:");
            System.Diagnostics.Debug.WriteLine($"  Symbol: {symbol}");
            System.Diagnostics.Debug.WriteLine($"  Direction: {direction}");
            System.Diagnostics.Debug.WriteLine($"  Quantity: {quantity}");
            System.Diagnostics.Debug.WriteLine($"  EntryPrice: {entryPrice}");
            System.Diagnostics.Debug.WriteLine($"  UnrealizedProfit: {unrealizedProfit}");
            System.Diagnostics.Debug.WriteLine($"  CurrentPrice: {currentPrice}");
            
            // 参数验证已在MainViewModel中完成，这里只记录调试信息
            if (quantity <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 警告：传入的数量参数异常: {quantity}");
            }
            
            if (unrealizedProfit <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 警告：传入的浮盈参数异常: {unrealizedProfit}");
            }
            
            _symbol = symbol;
            _direction = direction;
            _quantity = quantity;
            _entryPrice = entryPrice;
            _currentPrice = currentPrice;
            _unrealizedProfit = unrealizedProfit;
            
            InitializeDialogData();
            
            // 延迟调用GenerateSplitOrders，确保所有控件都已初始化
            this.Loaded += (sender, e) => GenerateSplitOrders();
        }

        private void InitializeDialogData()
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
                CalculationResultText.Text = "请设置分拆数量和目标浮盈，然后点击\"预览计算\"查看止盈价";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化界面数据失败：{ex.Message}", "初始化错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SplitCountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 只允许输入数字
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void SplitCountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            GenerateSplitOrders();
        }

        private void RefreshSplitButton_Click(object sender, RoutedEventArgs e)
        {
            GenerateSplitOrders();
        }

        private void GenerateSplitOrders()
        {
            try
            {
                // 📋 检查基本条件
                System.Diagnostics.Debug.WriteLine($"GenerateSplitOrders调用 - 数量: {_quantity}, 浮盈: {_unrealizedProfit}");
                
                // ✅ 修复：移除弹窗警告，仅记录调试信息，参数验证已在MainViewModel中完成
                if (_quantity <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"数量检查失败: {_quantity}");
                    return;
                }

                if (_unrealizedProfit <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"浮盈检查失败: {_unrealizedProfit}");
                    return;
                }

                // 📋 解析分拆数量
                int splitCount = 2; // 默认值
                if (SplitCountTextBox != null && !string.IsNullOrEmpty(SplitCountTextBox.Text))
                {
                    if (!int.TryParse(SplitCountTextBox.Text, out splitCount) || splitCount < 2)
                    {
                        splitCount = 2;
                        SplitCountTextBox.Text = "2";
                    }

                    if (splitCount > 10)
                    {
                        splitCount = 10;
                        SplitCountTextBox.Text = "10";
                        MessageBox.Show("最多只能拆分成10单", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                // 📋 生成分拆订单 - 按整数分拆
                if (_splitOrders == null)
                    _splitOrders = new List<SplitOrderInfo>();

                _splitOrders.Clear();
                
                // 按整数分拆数量，余数分配给最后一单
                var totalQuantityWhole = Math.Floor(_quantity);
                var fractionalPart = _quantity - totalQuantityWhole;
                
                var baseQuantityWhole = (int)(totalQuantityWhole / splitCount);
                var remainderQuantity = (int)(totalQuantityWhole % splitCount);
                
                // 浮盈平均分配
                var avgProfit = _unrealizedProfit / splitCount;

                for (int i = 0; i < splitCount; i++)
                {
                    decimal quantity;
                    if (i == splitCount - 1)
                    {
                        // 最后一单：基础数量 + 余数 + 小数部分
                        quantity = baseQuantityWhole + remainderQuantity + fractionalPart;
                    }
                    else
                    {
                        // 前面的单：基础整数数量
                        quantity = baseQuantityWhole;
                    }
                    
                    var splitOrder = new SplitOrderInfo
                    {
                        Index = i + 1,
                        Quantity = quantity,
                        CurrentProfit = Math.Round(avgProfit, 2),
                        TargetProfit = Math.Round(avgProfit * 0.8m, 2), // 默认目标浮盈为当前浮盈的80%
                        TargetPrice = 0
                    };

                    _splitOrders.Add(splitOrder);
                }

                // 📋 确保总浮盈精确匹配
                if (_splitOrders.Count > 0)
                {
                    var totalSplitProfit = _splitOrders.Sum(o => o.CurrentProfit);
                    var profitDiff = _unrealizedProfit - totalSplitProfit;
                    if (Math.Abs(profitDiff) > 0.01m)
                    {
                        _splitOrders.Last().CurrentProfit += profitDiff;
                        _splitOrders.Last().CurrentProfit = Math.Round(_splitOrders.Last().CurrentProfit, 2);
                        
                        // 同时调整最后一单的默认目标浮盈
                        _splitOrders.Last().TargetProfit = Math.Round(_splitOrders.Last().CurrentProfit * 0.8m, 2);
                    }
                }

                // 📋 刷新UI
                RefreshSplitOrdersUI();
            }
            catch (Exception ex)
            {
                var errorMessage = $"生成分拆订单失败：{ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\n内部异常：{ex.InnerException.Message}";
                }
                
                MessageBox.Show(errorMessage, "生成分拆订单错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                
                System.Diagnostics.Debug.WriteLine($"生成分拆订单异常: {ex}");
            }
        }

        private void RefreshSplitOrdersUI()
        {
            try
            {
                if (SplitOrdersPanel == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ SplitOrdersPanel is null");
                    return;
                }

                SplitOrdersPanel.Children.Clear();

                if (_splitOrders == null || _splitOrders.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ _splitOrders is null or empty");
                    return;
                }

                for (int i = 0; i < _splitOrders.Count; i++)
                {
                    var order = _splitOrders[i];
                    if (order == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ 分拆订单 {i} 为null");
                        continue;
                    }

                    var orderPanel = CreateSplitOrderPanel(order, i);
                    if (orderPanel != null)
                    {
                        SplitOrdersPanel.Children.Add(orderPanel);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新分拆订单界面失败：{ex.Message}\n\n详细信息：{ex.StackTrace}", 
                    "界面错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Border CreateSplitOrderPanel(SplitOrderInfo order, int index)
        {
            var border = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(10),
                Background = index % 2 == 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(248, 248, 248))
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });   // 序号
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });  // 数量
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });  // 当前浮盈
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });  // 目标浮盈+快捷按钮
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });  // 目标价格
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });  // 操作按钮

            // 序号
            var indexText = new TextBlock
            {
                Text = $"#{order.Index}",
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(indexText, 0);
            grid.Children.Add(indexText);

            // 数量输入框和调整按钮
            var quantityPanel = new StackPanel { Orientation = Orientation.Vertical };
            quantityPanel.Children.Add(new TextBlock { Text = "数量", FontSize = 10, Foreground = Brushes.Gray });
            
            var quantityInputPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // 减少数量按钮
            var decreaseQuantityButton = new Button
            {
                Content = "-",
                Width = 20,
                Height = 20,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Red,
                Foreground = Brushes.White,
                Tag = index,
                Margin = new Thickness(0, 0, 2, 0)
            };
            decreaseQuantityButton.Click += DecreaseQuantityButton_Click;
            quantityInputPanel.Children.Add(decreaseQuantityButton);
            
            // 数量输入框
            var quantityTextBox = new TextBox
            {
                Text = order.Quantity.ToString("F6"),
                Width = 60,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = index
            };
            quantityTextBox.TextChanged += QuantityTextBox_TextChanged;
            quantityInputPanel.Children.Add(quantityTextBox);
            
            // 增加数量按钮
            var increaseQuantityButton = new Button
            {
                Content = "+",
                Width = 20,
                Height = 20,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Green,
                Foreground = Brushes.White,
                Tag = index,
                Margin = new Thickness(2, 0, 0, 0)
            };
            increaseQuantityButton.Click += IncreaseQuantityButton_Click;
            quantityInputPanel.Children.Add(increaseQuantityButton);
            
            quantityPanel.Children.Add(quantityInputPanel);
            Grid.SetColumn(quantityPanel, 1);
            grid.Children.Add(quantityPanel);

            // 当前浮盈
            var currentProfitPanel = new StackPanel { Orientation = Orientation.Vertical };
            currentProfitPanel.Children.Add(new TextBlock { Text = "当前浮盈", FontSize = 10, Foreground = Brushes.Gray });
            currentProfitPanel.Children.Add(new TextBlock 
            { 
                Text = $"{order.CurrentProfit:F2} U",
                FontWeight = FontWeights.Bold,
                Foreground = order.CurrentProfit >= 0 ? Brushes.Green : Brushes.Red,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(currentProfitPanel, 2);
            grid.Children.Add(currentProfitPanel);

            // 目标浮盈设置区域
            var targetProfitPanel = new StackPanel { Orientation = Orientation.Vertical };
            targetProfitPanel.Children.Add(new TextBlock { Text = "目标浮盈", FontSize = 10, Foreground = Brushes.Gray });
            
            // 目标浮盈输入框和快捷按钮在同一行
            var targetProfitRowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
            
            // 目标浮盈输入框
            var targetProfitTextBox = new TextBox
            {
                Text = order.TargetProfit.ToString("F2"),
                Width = 70,
                Height = 22,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = index,
                Margin = new Thickness(0, 0, 5, 0)
            };
            targetProfitTextBox.TextChanged += TargetProfitTextBox_TextChanged;
            targetProfitRowPanel.Children.Add(targetProfitTextBox);
            
            // 快捷比例按钮
            // 90% 按钮
            var profit90Button = new Button
            {
                Content = "90%",
                Width = 32,
                Height = 18,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Background = Brushes.DarkGreen,
                Foreground = Brushes.White,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Tag = new { Index = index, Ratio = 0.9m },
                Margin = new Thickness(0, 0, 1, 0)
            };
            profit90Button.Click += IndividualProfitRatioButton_Click;
            targetProfitRowPanel.Children.Add(profit90Button);
            
            // 80% 按钮
            var profit80Button = new Button
            {
                Content = "80%",
                Width = 32,
                Height = 18,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Green,
                Foreground = Brushes.White,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Tag = new { Index = index, Ratio = 0.8m },
                Margin = new Thickness(0, 0, 1, 0)
            };
            profit80Button.Click += IndividualProfitRatioButton_Click;
            targetProfitRowPanel.Children.Add(profit80Button);
            
            // 50% 按钮
            var profit50Button = new Button
            {
                Content = "50%",
                Width = 32,
                Height = 18,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Orange,
                Foreground = Brushes.White,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Tag = new { Index = index, Ratio = 0.5m },
                Margin = new Thickness(0, 0, 1, 0)
            };
            profit50Button.Click += IndividualProfitRatioButton_Click;
            targetProfitRowPanel.Children.Add(profit50Button);
            
            // 10% 按钮
            var profit10Button = new Button
            {
                Content = "10%",
                Width = 32,
                Height = 18,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Red,
                Foreground = Brushes.White,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Tag = new { Index = index, Ratio = 0.1m }
            };
            profit10Button.Click += IndividualProfitRatioButton_Click;
            targetProfitRowPanel.Children.Add(profit10Button);
            
            targetProfitPanel.Children.Add(targetProfitRowPanel);
            Grid.SetColumn(targetProfitPanel, 3);
            grid.Children.Add(targetProfitPanel);

            // 目标价格显示
            var targetPricePanel = new StackPanel { Orientation = Orientation.Vertical };
            targetPricePanel.Children.Add(new TextBlock { Text = "目标价格", FontSize = 10, Foreground = Brushes.Gray });
            
            string priceText = "未计算";
            try
            {
                if (order.TargetPrice > 0)
                {
                    priceText = PriceFormatConverter.FormatPrice(order.TargetPrice);
                }
            }
            catch (Exception ex)
            {
                priceText = $"{order.TargetPrice:F4}"; // 如果格式化失败，使用默认格式
                System.Diagnostics.Debug.WriteLine($"价格格式化失败: {ex.Message}");
            }
            
            var targetPriceText = new TextBlock
            {
                Text = priceText,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Blue,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = $"targetPrice_{index}"
            };
            targetPricePanel.Children.Add(targetPriceText);
            Grid.SetColumn(targetPricePanel, 4);
            grid.Children.Add(targetPricePanel);

            // 计算按钮
            var calculateButton = new Button
            {
                Content = "计算价格",
                Width = 80,
                Height = 25,
                Background = Brushes.Orange,
                Foreground = Brushes.White,
                Tag = index
            };
            calculateButton.Click += CalculateButton_Click;
            var buttonPanel = new StackPanel { Orientation = Orientation.Vertical };
            buttonPanel.Children.Add(new TextBlock { Text = "操作", FontSize = 10, Foreground = Brushes.Gray });
            buttonPanel.Children.Add(calculateButton);
            Grid.SetColumn(buttonPanel, 5);
            grid.Children.Add(buttonPanel);

            border.Child = grid;
            return border;
        }

        private void QuantityTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var textBox = sender as TextBox;
                if (textBox?.Tag is int index && index < _splitOrders.Count)
                {
                    if (decimal.TryParse(textBox.Text, out decimal newQuantity))
                    {
                        var oldQuantity = _splitOrders[index].Quantity;
                        _splitOrders[index].Quantity = newQuantity;

                        // 自动调整最后一个订单的数量以保持总量不变
                        if (index != _splitOrders.Count - 1)
                        {
                            var quantityDiff = newQuantity - oldQuantity;
                            _splitOrders.Last().Quantity -= quantityDiff;
                            _splitOrders.Last().Quantity = Math.Round(_splitOrders.Last().Quantity, 6);
                            
                            // 更新最后一个订单的UI
                            RefreshSplitOrdersUI();
                        }
                    }
                }
            }
            catch
            {
                // 静默处理数量调整错误
            }
        }

        private void TargetProfitTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var textBox = sender as TextBox;
                if (textBox?.Tag is int index && index < _splitOrders.Count)
                {
                    if (decimal.TryParse(textBox.Text, out decimal targetProfit))
                    {
                        _splitOrders[index].TargetProfit = targetProfit;
                        
                        // 自动计算目标价格
                        if (targetProfit > 0)
                        {
                            CalculateTargetPrice(_splitOrders[index], index);
                            
                            // 找到对应的目标价格显示控件并更新
                            RefreshSingleOrderTargetPrice(index);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 静默处理目标浮盈设置错误
                System.Diagnostics.Debug.WriteLine($"目标浮盈文本变化处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新单个订单的目标价格显示
        /// </summary>
        private void RefreshSingleOrderTargetPrice(int index)
        {
            try
            {
                // 由于动态创建的UI元素无法通过FindName找到，这里选择刷新整个UI
                // 更高效的做法是缓存UI控件引用，但为简化实现，直接刷新
                RefreshSplitOrdersUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新单个订单目标价格失败: {ex.Message}");
            }
        }

        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag is int index && index < _splitOrders.Count)
                {
                    var order = _splitOrders[index];
                    
                    if (order.Quantity <= 0)
                    {
                        MessageBox.Show($"第{order.Index}单的数量必须大于0", "输入错误", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (order.TargetProfit <= 0)
                    {
                        MessageBox.Show($"第{order.Index}单的目标浮盈必须大于0", "输入错误", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (order.TargetProfit >= order.CurrentProfit)
                    {
                        MessageBox.Show($"第{order.Index}单的目标浮盈({order.TargetProfit:F2})必须小于当前浮盈({order.CurrentProfit:F2})", 
                            "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 计算目标价格
                    var isLong = _direction == "做多";
                    decimal targetPrice;
                    
                    if (isLong)
                    {
                        // 做多：目标价格 = 开仓价 + (目标浮盈 / 数量)
                        targetPrice = _entryPrice + (order.TargetProfit / order.Quantity);
                    }
                    else
                    {
                        // 做空：目标价格 = 开仓价 - (目标浮盈 / 数量)
                        targetPrice = _entryPrice - (order.TargetProfit / order.Quantity);
                    }

                    order.TargetPrice = Math.Round(targetPrice, 4);

                    // 更新UI中的目标价格显示
                    var targetPriceText = this.FindName($"targetPrice_{index}") as TextBlock;
                    if (targetPriceText == null)
                    {
                        // 如果找不到控件，刷新整个UI
                        RefreshSplitOrdersUI();
                    }
                    else
                    {
                        targetPriceText.Text = PriceFormatConverter.FormatPrice(order.TargetPrice);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"计算目标价格失败：{ex.Message}", "计算错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证所有分拆订单
                var totalQuantity = _splitOrders.Sum(o => o.Quantity);
                if (Math.Abs(totalQuantity - _quantity) > 0.000001m)
                {
                    MessageBox.Show($"分拆后的总数量({totalQuantity:F6})与原始数量({_quantity:F6})不匹配", 
                        "数量不匹配", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var validOrders = _splitOrders.Where(o => o.TargetPrice > 0).ToList();
                if (validOrders.Count == 0)
                {
                    MessageBox.Show("请至少为一个分拆订单计算目标价格", "未设置目标价格", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 生成预览结果
                var result = "分仓止盈预览：\n\n";
                result += $"原始持仓：{_symbol} {_direction} {_quantity:F6}，当前浮盈：{_unrealizedProfit:F2} USDT\n\n";
                result += "将生成以下止盈订单：\n";

                foreach (var order in validOrders)
                {
                    result += $"第{order.Index}单：数量 {order.Quantity:F6}，目标浮盈 {order.TargetProfit:F2} USDT，止盈价 {PriceFormatConverter.FormatPrice(order.TargetPrice)}\n";
                }

                var totalTargetProfit = validOrders.Sum(o => o.TargetProfit);
                var totalValidQuantity = validOrders.Sum(o => o.Quantity);
                result += $"\n总计：{validOrders.Count} 个止盈订单，覆盖数量 {totalValidQuantity:F6}，目标浮盈 {totalTargetProfit:F2} USDT";

                CalculationResultText.Text = result;
            }
            catch (Exception ex)
            {
                CalculationResultText.Text = $"预览计算失败：{ex.Message}";
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 详细的调试信息
                System.Diagnostics.Debug.WriteLine($"🔍 ConfirmButton_Click: 开始生成止盈请求");
                System.Diagnostics.Debug.WriteLine($"🔍 _splitOrders总数: {_splitOrders?.Count ?? 0}");
                
                if (_splitOrders != null)
                {
                    for (int i = 0; i < _splitOrders.Count; i++)
                    {
                        var order = _splitOrders[i];
                        System.Diagnostics.Debug.WriteLine($"🔍 订单#{i + 1}: 数量={order.Quantity:F6}, 目标价格={order.TargetPrice:F4}, 目标浮盈={order.TargetProfit:F2}");
                    }
                }
                
                // 验证并生成最终的止盈请求
                var validOrders = _splitOrders.Where(o => o.TargetPrice > 0 && o.Quantity > 0).ToList();
                
                System.Diagnostics.Debug.WriteLine($"🔍 有效订单数量: {validOrders.Count}");
                
                if (validOrders.Count == 0)
                {
                    MessageBox.Show("没有有效的分拆订单可以执行", "无有效订单", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ProfitRequests.Clear();
                foreach (var order in validOrders)
                {
                    var request = new PartialProfitRequest
                    {
                        Symbol = _symbol,
                        Side = _direction == "做多" ? "SELL" : "BUY", // 平仓方向与持仓方向相反
                        Quantity = order.Quantity,
                        Price = order.TargetPrice,
                        TargetProfit = order.TargetProfit,
                        OrderIndex = order.Index
                    };
                    
                    ProfitRequests.Add(request);
                    System.Diagnostics.Debug.WriteLine($"✅ 生成请求#{request.OrderIndex}: {request.Symbol} {request.Side} {request.Quantity:F6} @{request.Price:F4}, 目标浮盈={request.TargetProfit:F2}U");
                }

                System.Diagnostics.Debug.WriteLine($"✅ 最终生成 {ProfitRequests.Count} 个止盈请求");

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成止盈请求失败：{ex.Message}", "执行错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"❌ 生成止盈请求异常: {ex}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #region 快捷按钮事件处理

        /// <summary>
        /// 单个订单的快捷比例设置
        /// </summary>
        private void IndividualProfitRatioButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag != null)
                {
                    // 使用反射获取匿名对象的属性
                    var tagType = button.Tag.GetType();
                    var indexProperty = tagType.GetProperty("Index");
                    var ratioProperty = tagType.GetProperty("Ratio");
                    
                    if (indexProperty != null && ratioProperty != null)
                    {
                        var indexValue = indexProperty.GetValue(button.Tag);
                        var ratioValue = ratioProperty.GetValue(button.Tag);
                        
                        if (indexValue != null && ratioValue != null)
                        {
                            var index = (int)indexValue;
                            var ratio = (decimal)ratioValue;
                            
                            if (index < _splitOrders.Count)
                        {
                            var order = _splitOrders[index];
                            order.TargetProfit = Math.Round(order.CurrentProfit * ratio, 2);
                            
                            // 自动计算目标价格
                            CalculateTargetPrice(order, index);
                            
                            // 刷新UI
                            RefreshSplitOrdersUI();
                        }
                    }
                }
            }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置单个订单目标浮盈比例失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 自动计算目标价格
        /// </summary>
        private void CalculateTargetPrice(SplitOrderInfo order, int index)
        {
            try
            {
                if (order.Quantity <= 0 || order.TargetProfit <= 0)
                {
                    order.TargetPrice = 0;
                    return;
                }

                // 计算目标价格
                // 做多：目标价格 = 开仓价 + (目标浮盈 / 数量)
                // 做空：目标价格 = 开仓价 - (目标浮盈 / 数量)
                if (_direction == "做多" || _direction == "LONG")
                {
                    order.TargetPrice = _entryPrice + (order.TargetProfit / order.Quantity);
                }
                else if (_direction == "做空" || _direction == "SHORT")
                {
                    order.TargetPrice = _entryPrice - (order.TargetProfit / order.Quantity);
                }

                order.TargetPrice = Math.Round(order.TargetPrice, 4);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"计算目标价格失败: {ex.Message}");
                order.TargetPrice = 0;
            }
        }

        /// <summary>
        /// 减少分拆数量
        /// </summary>
        private void DecreaseSplitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(SplitCountTextBox?.Text, out int currentCount) && currentCount > 2)
                {
                    SplitCountTextBox.Text = (currentCount - 1).ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"减少分拆数量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 增加分拆数量
        /// </summary>
        private void IncreaseSplitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(SplitCountTextBox?.Text, out int currentCount) && currentCount < 10)
                {
                    SplitCountTextBox.Text = (currentCount + 1).ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"增加分拆数量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 目标浮盈比例快捷设置
        /// </summary>
        private void ProfitRatioButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag != null && decimal.TryParse(button.Tag.ToString(), out decimal ratio))
                {
                    // 为所有分拆订单设置目标浮盈比例
                    foreach (var order in _splitOrders)
                    {
                        order.TargetProfit = Math.Round(order.CurrentProfit * ratio, 2);
                    }

                    // 刷新UI以显示新的目标浮盈
                    RefreshSplitOrdersUI();

                    // 给用户反馈
                    var percentage = (ratio * 100).ToString("F0");
                    System.Windows.MessageBox.Show($"已为所有订单设置目标浮盈为当前浮盈的{percentage}%", 
                        "快捷设置完成", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"设置目标浮盈比例失败：{ex.Message}", "设置失败", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 减少单个订单数量（减少10%，按整数分拆规则）
        /// </summary>
        private void DecreaseQuantityButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag is int index && index < _splitOrders.Count)
                {
                    var order = _splitOrders[index];
                    var originalQuantity = order.Quantity;
                    var decreaseAmount = originalQuantity * 0.1m; // 减少10%
                    
                    // 如果是前面的订单（非最后一单），减少后需要取整
                    decimal adjustedDecrease;
                    if (index < _splitOrders.Count - 1)
                    {
                        // 前面订单：减少后取整
                        var newQuantity = originalQuantity - decreaseAmount;
                        var integerQuantity = Math.Floor(newQuantity);
                        adjustedDecrease = originalQuantity - Math.Max(integerQuantity, 1); // 最少保持1个
                    }
                    else
                    {
                        // 最后一单：可以有小数
                        adjustedDecrease = Math.Min(decreaseAmount, originalQuantity - 0.000001m);
                    }

                    if (adjustedDecrease > 0)
                    {
                        // 重新应用整数分拆规则
                        ApplyIntegerSplitRule(index, -adjustedDecrease);
                        RefreshSplitOrdersUI();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"减少订单数量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 增加单个订单数量（增加10%，按整数分拆规则）
        /// </summary>
        private void IncreaseQuantityButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag is int index && index < _splitOrders.Count)
                {
                    var order = _splitOrders[index];
                    var originalQuantity = order.Quantity;
                    var increaseAmount = originalQuantity * 0.1m; // 增加10%
                    
                    // 如果是前面的订单（非最后一单），增加后需要取整
                    decimal adjustedIncrease;
                    if (index < _splitOrders.Count - 1)
                    {
                        // 前面订单：增加后取整
                        var newQuantity = originalQuantity + increaseAmount;
                        var integerQuantity = Math.Ceiling(newQuantity); // 向上取整确保增加
                        adjustedIncrease = integerQuantity - originalQuantity;
                    }
                    else
                    {
                        // 最后一单：可以有小数
                        adjustedIncrease = increaseAmount;
                    }

                    // 检查是否能从其他订单中获取足够的数量
                    var totalOtherQuantity = _splitOrders.Where((o, i) => i != index).Sum(o => o.Quantity);
                    
                    if (totalOtherQuantity >= adjustedIncrease)
                    {
                        // 重新应用整数分拆规则
                        ApplyIntegerSplitRule(index, adjustedIncrease);
                        RefreshSplitOrdersUI();
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"其他订单数量不足，无法继续增加此订单数量\n需要: {adjustedIncrease:F6}，可用: {totalOtherQuantity:F6}", "数量限制", 
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"增加订单数量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用整数分拆规则，调整指定订单的数量
        /// </summary>
        /// <param name="targetIndex">要调整的订单索引</param>
        /// <param name="quantityChange">数量变化（正数为增加，负数为减少）</param>
        private void ApplyIntegerSplitRule(int targetIndex, decimal quantityChange)
        {
            try
            {
                if (targetIndex >= _splitOrders.Count) return;

                var targetOrder = _splitOrders[targetIndex];
                var newQuantity = targetOrder.Quantity + quantityChange;
                
                // 确保新数量不为负
                if (newQuantity < 0.000001m)
                {
                    newQuantity = 0.000001m;
                }

                // 计算实际的变化量
                var actualChange = newQuantity - targetOrder.Quantity;
                targetOrder.Quantity = newQuantity;

                // 如果不是最后一单，需要将变化量传递给其他订单
                if (targetIndex != _splitOrders.Count - 1)
                {
                    // 如果调整的不是最后一单，则最后一单承担所有的平衡调整
                    var lastOrder = _splitOrders.Last();
                    lastOrder.Quantity -= actualChange;
                    
                    // 确保最后一单的数量不为负
                    if (lastOrder.Quantity < 0.000001m)
                    {
                        var shortage = 0.000001m - lastOrder.Quantity;
                        lastOrder.Quantity = 0.000001m;
                        targetOrder.Quantity -= shortage; // 从目标订单中减去不足的部分
                    }
                }
                else
                {
                    // 如果调整的是最后一单，需要从其他订单中平衡
                    var otherOrders = _splitOrders.Take(_splitOrders.Count - 1).ToList();
                    if (otherOrders.Count > 0)
                    {
                        var changePerOrder = -actualChange / otherOrders.Count;
                        
                        foreach (var order in otherOrders)
                        {
                            order.Quantity += changePerOrder;
                            if (order.Quantity < 0.000001m)
                            {
                                var shortage = 0.000001m - order.Quantity;
                                order.Quantity = 0.000001m;
                                targetOrder.Quantity -= shortage;
                            }
                        }
                    }
                }

                // 应用整数分拆规则：前面的订单保持整数
                for (int i = 0; i < _splitOrders.Count - 1; i++)
                {
                    var order = _splitOrders[i];
                    var integerPart = Math.Floor(order.Quantity);
                    var fractionalPart = order.Quantity - integerPart;
                    
                    if (fractionalPart > 0.000001m)
                    {
                        // 将小数部分转移到最后一单
                        order.Quantity = integerPart;
                        _splitOrders.Last().Quantity += fractionalPart;
                    }
                }

                // 确保所有数量都是有效的
                foreach (var order in _splitOrders)
                {
                    if (order.Quantity < 0.000001m)
                    {
                        order.Quantity = 0.000001m;
                    }
                    order.Quantity = Math.Round(order.Quantity, 6);
                }

                // 验证总数量是否匹配原始数量
                var totalQuantity = _splitOrders.Sum(o => o.Quantity);
                var quantityDiff = _quantity - totalQuantity;
                if (Math.Abs(quantityDiff) > 0.000001m)
                {
                    // 调整最后一单以确保总量匹配
                    _splitOrders.Last().Quantity += quantityDiff;
                    _splitOrders.Last().Quantity = Math.Round(_splitOrders.Last().Quantity, 6);
                }

                System.Diagnostics.Debug.WriteLine($"整数分拆调整完成：目标订单{targetIndex + 1}，变化量{quantityChange:F6}");
                System.Diagnostics.Debug.WriteLine($"调整后数量分布：{string.Join(", ", _splitOrders.Select((o, i) => $"第{i + 1}单:{o.Quantity:F6}"))}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用整数分拆规则失败: {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// 分拆订单信息
    /// </summary>
    public class SplitOrderInfo
    {
        public int Index { get; set; }
        public decimal Quantity { get; set; }
        public decimal CurrentProfit { get; set; }
        public decimal TargetProfit { get; set; }
        public decimal TargetPrice { get; set; }
    }

    /// <summary>
    /// 分仓止盈请求
    /// </summary>
    public class PartialProfitRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal TargetProfit { get; set; }
        public int OrderIndex { get; set; }
    }
} 