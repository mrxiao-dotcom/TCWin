using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// 合约配置编辑对话框 - 支持修改触发金额、保盈金额和执行状态
    /// </summary>
    public partial class ContractEditDialog : Window
    {
        private readonly ContractMonitorModel _contract;
        private readonly ILogger _logger;
        private readonly Dictionary<int, TriggerConditionEditData> _editData;
        private bool _hasChanges = false;
        
        // 主要UI控件
        private StackPanel _triggerConditionsPanel;
        private CheckBox _isEnabledCheckBox;
        private TextBlock _contractInfoTextBlock;
        private TextBlock _symbolTextBlock;
        private TextBlock _positionSideTextBlock;
        private TextBlock _currentPriceTextBlock;
        private TextBlock _positionSizeTextBlock;
        private TextBlock _unrealizedPnlTextBlock;

        public bool HasChanges => _hasChanges;
        public Dictionary<int, TriggerConditionEditData> EditData => _editData;

        public ContractEditDialog(ContractMonitorModel contract, ILogger logger)
        {
            _contract = contract ?? throw new ArgumentNullException(nameof(contract));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _editData = new Dictionary<int, TriggerConditionEditData>();

            CreateUserInterface();
            InitializeDialog();
            LoadContractData();
            CreateTriggerConditionEditors();
        }

        private void CreateUserInterface()
        {
            // 窗口基本设置
            Title = "合约配置编辑";
            Width = 800;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(Color.FromRgb(248, 249, 250));

            // 主容器
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 合约信息
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 编辑区域
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 按钮栏

            // 标题栏
            var titlePanel = CreateTitlePanel();
            Grid.SetRow(titlePanel, 0);
            mainGrid.Children.Add(titlePanel);

            // 合约信息面板
            var infoPanel = CreateContractInfoPanel();
            Grid.SetRow(infoPanel, 1);
            mainGrid.Children.Add(infoPanel);

            // 编辑区域
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(10, 10, 10, 10)
            };

            _triggerConditionsPanel = new StackPanel();
            scrollViewer.Content = _triggerConditionsPanel;
            
            Grid.SetRow(scrollViewer, 2);
            mainGrid.Children.Add(scrollViewer);

            // 按钮栏
            var buttonPanel = CreateButtonPanel();
            Grid.SetRow(buttonPanel, 3);
            mainGrid.Children.Add(buttonPanel);

            Content = mainGrid;
        }

        private StackPanel CreateTitlePanel()
        {
            var panel = new StackPanel
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Margin = new Thickness(0, 0, 0, 0)
            };

            _contractInfoTextBlock = new TextBlock
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(15, 10, 15, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            panel.Children.Add(_contractInfoTextBlock);
            return panel;
        }

        private Border CreateContractInfoPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(236, 240, 245)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                BorderThickness = new Thickness(1, 1, 1, 1),
                Margin = new Thickness(10, 5, 10, 5),
                Padding = new Thickness(15, 15, 15, 15)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 左列
            var leftPanel = new StackPanel();
            leftPanel.Children.Add(CreateInfoRow("合约:", out _symbolTextBlock));
            leftPanel.Children.Add(CreateInfoRow("方向:", out _positionSideTextBlock));
            Grid.SetColumn(leftPanel, 0);
            grid.Children.Add(leftPanel);

            // 中列
            var centerPanel = new StackPanel();
            centerPanel.Children.Add(CreateInfoRow("当前价格:", out _currentPriceTextBlock));
            centerPanel.Children.Add(CreateInfoRow("持仓数量:", out _positionSizeTextBlock));
            Grid.SetColumn(centerPanel, 1);
            grid.Children.Add(centerPanel);

            // 右列
            var rightPanel = new StackPanel();
            rightPanel.Children.Add(CreateInfoRow("浮盈:", out _unrealizedPnlTextBlock));
            
            // 启用状态
            var enablePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var enableLabel = new TextBlock
            {
                Text = "启用监控:",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            _isEnabledCheckBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            enablePanel.Children.Add(enableLabel);
            enablePanel.Children.Add(_isEnabledCheckBox);
            rightPanel.Children.Add(enablePanel);
            
            Grid.SetColumn(rightPanel, 2);
            grid.Children.Add(rightPanel);

            border.Child = grid;
            return border;
        }

        private StackPanel CreateInfoRow(string label, out TextBlock valueTextBlock)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2)
            };

            var labelTextBlock = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };

            valueTextBlock = new TextBlock
            {
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Medium
            };

            panel.Children.Add(labelTextBlock);
            panel.Children.Add(valueTextBlock);

            return panel;
        }

        private StackPanel CreateButtonPanel()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10, 5, 10, 10)
            };

            var saveButton = new Button
            {
                Content = "💾 保存修改",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0, 0, 0, 0),
                FontSize = 12
            };
            saveButton.Click += SaveChangesButton_Click;

            var cancelButton = new Button
            {
                Content = "❌ 取消",
                Width = 80,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0, 0, 0, 0),
                FontSize = 12
            };
            cancelButton.Click += CancelButton_Click;

            panel.Children.Add(saveButton);
            panel.Children.Add(cancelButton);

            return panel;
        }

        private void InitializeDialog()
        {
            _contractInfoTextBlock.Text = $"📊 {_contract.Symbol}_{_contract.PositionSide} 配置编辑";
            _logger.LogInformation($"🔧 打开合约编辑对话框: {_contract.ContractKey}");
        }

        private void LoadContractData()
        {
            try
            {
                // 加载基本信息
                _symbolTextBlock.Text = _contract.Symbol;
                _positionSideTextBlock.Text = _contract.PositionSide;
                _currentPriceTextBlock.Text = _contract.CurrentPriceText;
                _positionSizeTextBlock.Text = _contract.PositionSizeText;
                _unrealizedPnlTextBlock.Text = _contract.PnlText;
                _unrealizedPnlTextBlock.Foreground = _contract.PnlColor;
                _isEnabledCheckBox.IsChecked = _contract.IsEnabled;
                
                // 监听启用状态变化
                _isEnabledCheckBox.Checked += (s, e) => _hasChanges = true;
                _isEnabledCheckBox.Unchecked += (s, e) => _hasChanges = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 加载合约数据失败");
            }
        }

        private void CreateTriggerConditionEditors()
        {
            try
            {
                _triggerConditionsPanel.Children.Clear();
                
                if (!_contract.TriggerConditions.Any())
                {
                    var noDataTextBlock = new TextBlock
                    {
                        Text = "暂无触发条件配置",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 0)
                    };
                    _triggerConditionsPanel.Children.Add(noDataTextBlock);
                    return;
                }

                // 按类型分组显示
                var groupedConditions = _contract.TriggerConditions.GroupBy(c => c.Type).OrderBy(g => (int)g.Key);
                
                foreach (var group in groupedConditions)
                {
                    // 创建类型标题
                    var typeHeader = CreateTypeHeader(group.Key);
                    _triggerConditionsPanel.Children.Add(typeHeader);
                    
                    // 为每个触发条件创建编辑器
                    foreach (var condition in group.OrderBy(c => c.TierIndex ?? 0))
                    {
                        var editor = CreateConditionEditor(condition);
                        _triggerConditionsPanel.Children.Add(editor);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 创建触发条件编辑器失败");
                MessageBox.Show($"创建编辑器失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FrameworkElement CreateTypeHeader(TriggerConditionType type)
        {
            var typeInfo = GetTypeInfo(type);
            
            var header = new TextBlock
            {
                Text = $"{typeInfo.Icon} {typeInfo.Name}",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = typeInfo.Color,
                Margin = new Thickness(0, 15, 0, 5)
            };
            
            return header;
        }

        private FrameworkElement CreateConditionEditor(TriggerConditionModel condition)
        {
            // 创建编辑数据
            var editData = new TriggerConditionEditData
            {
                Id = condition.Id,
                OriginalTriggerPrice = condition.TriggerPrice,
                OriginalKeepValue = condition.KeepValue,
                OriginalStatus = condition.Status,
                NewTriggerPrice = condition.TriggerPrice,
                NewKeepValue = condition.KeepValue,
                NewStatus = condition.Status
            };
            _editData[condition.Id] = editData;

            // 创建主容器
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(0, 5, 0, 0)
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // 条件信息
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // 触发金额
            if (condition.Type == TriggerConditionType.ProfitProtection)
            {
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // 保盈金额
            }
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 状态
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 操作

            // 1. 条件信息
            var infoPanel = CreateConditionInfoPanel(condition);
            Grid.SetColumn(infoPanel, 0);
            mainGrid.Children.Add(infoPanel);

            // 2. 触发金额编辑（可编辑输入框）
            var triggerPanel = CreateEditableAmountPanel("触发金额", condition.TriggerPrice, (newValue) => {
                editData.NewTriggerPrice = newValue;
                _hasChanges = true;
            });
            Grid.SetColumn(triggerPanel, 1);
            mainGrid.Children.Add(triggerPanel);

            // 3. 保盈金额编辑（仅止盈条件）
            if (condition.Type == TriggerConditionType.ProfitProtection)
            {
                var keepValuePanel = CreateEditableAmountPanel("保盈金额", condition.KeepValue, (newValue) => {
                    editData.NewKeepValue = newValue;
                    _hasChanges = true;
                });
                Grid.SetColumn(keepValuePanel, 2);
                mainGrid.Children.Add(keepValuePanel);
            }

            // 4. 状态显示
            var statusPanel = CreateStatusDisplayPanel(condition);
            Grid.SetColumn(statusPanel, condition.Type == TriggerConditionType.ProfitProtection ? 3 : 2);
            mainGrid.Children.Add(statusPanel);

            // 5. 操作按钮（仅保留有用的）
            var actionPanel = CreateActionPanel(condition, editData);
            Grid.SetColumn(actionPanel, condition.Type == TriggerConditionType.ProfitProtection ? 4 : 3);
            mainGrid.Children.Add(actionPanel);

            border.Child = mainGrid;
            return border;
        }

        private StackPanel CreateConditionInfoPanel(TriggerConditionModel condition)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };

            var titleText = condition.TierIndex.HasValue ? 
                $"{condition.TypeText} 第{condition.TierIndex}档" : 
                condition.TypeText;

            var title = new TextBlock
            {
                Text = titleText,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 2)
            };

            var desc = new TextBlock
            {
                Text = condition.Description,
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            };

            panel.Children.Add(title);
            panel.Children.Add(desc);

            return panel;
        }

        private StackPanel CreateEditableAmountPanel(string label, decimal currentValue, Action<decimal> onValueChanged)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };

            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 3)
            };

            var textBox = new TextBox
            {
                Text = currentValue.ToString("F2"),
                Padding = new Thickness(5),
                FontSize = 11,
                BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Colors.White)
            };

            // 监听文本变化
            textBox.TextChanged += (s, e) =>
            {
                if (decimal.TryParse(textBox.Text, out var newValue) && newValue >= 0)
                {
                    onValueChanged(newValue);
                    textBox.Background = new SolidColorBrush(Colors.White);
                    textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                }
                else
                {
                    textBox.Background = new SolidColorBrush(Color.FromRgb(255, 235, 235));
                    textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                }
            };

            // 获得焦点时高亮
            textBox.GotFocus += (s, e) =>
            {
                textBox.SelectAll();
                textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219));
            };

            textBox.LostFocus += (s, e) =>
            {
                textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199));
            };

            panel.Children.Add(labelText);
            panel.Children.Add(textBox);

            return panel;
        }

        private StackPanel CreateStatusDisplayPanel(TriggerConditionModel condition)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };

            var label = new TextBlock
            {
                Text = "状态",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 3)
            };

            var statusBorder = new Border
            {
                Background = condition.Status == TriggerExecutionStatus.Executed ? 
                    new SolidColorBrush(Color.FromRgb(231, 76, 60)) :
                    new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 2, 8, 2)
            };

            var statusText = new TextBlock
            {
                                                Text = condition.Status == TriggerExecutionStatus.Executed ? StatusConstants.Executed : StatusConstants.Waiting,
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            statusBorder.Child = statusText;
            panel.Children.Add(label);
            panel.Children.Add(statusBorder);

            return panel;
        }

        private StackPanel CreateActionPanel(TriggerConditionModel condition, TriggerConditionEditData editData)
        {
            var panel = new StackPanel();

            var label = new TextBlock
            {
                Text = "操作",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 3)
            };

            // 只保留状态重置按钮，只有在已执行时才显示
            if (condition.Status == TriggerExecutionStatus.Executed)
            {
                var resetButton = new Button
                {
                    Content = "重置",
                    Padding = new Thickness(8, 2, 8, 2),
                    FontSize = 10,
                    Background = new SolidColorBrush(Color.FromRgb(243, 156, 18)),
                    Foreground = new SolidColorBrush(Colors.White),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                resetButton.Click += (s, e) =>
                {
                    editData.NewStatus = TriggerExecutionStatus.NotTriggered;
                    _hasChanges = true;
                    
                    // 隐藏重置按钮
                    resetButton.Visibility = Visibility.Collapsed;
                    
                                                MessageBox.Show($"状态已重置为\"{StatusConstants.Waiting}\"，保存后生效", "状态重置", MessageBoxButton.OK, MessageBoxImage.Information);
                };

                panel.Children.Add(label);
                panel.Children.Add(resetButton);
            }
            else
            {
                var placeholderText = new TextBlock
                {
                    Text = "-",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 15, 0, 0)
                };

                panel.Children.Add(label);
                panel.Children.Add(placeholderText);
            }

            return panel;
        }

        private (string Icon, string Name, SolidColorBrush Color) GetTypeInfo(TriggerConditionType type)
        {
            return type switch
            {
                TriggerConditionType.BreakEven => ("🛡️", "保本条件", new SolidColorBrush(Color.FromRgb(52, 152, 219))),
                TriggerConditionType.AddPosition => ("📈", "推仓条件", new SolidColorBrush(Color.FromRgb(243, 156, 18))),
                TriggerConditionType.ProfitProtection => ("💰", "止盈条件", new SolidColorBrush(Color.FromRgb(155, 89, 182))),
                _ => ("❓", "未知条件", new SolidColorBrush(Colors.Gray))
            };
        }

        private void ResetAllStatusButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("确定要重置所有触发条件的执行状态吗？", 
                    "确认重置", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    int resetCount = 0;
                    foreach (var editData in _editData.Values)
                    {
                        if (editData.OriginalStatus == TriggerExecutionStatus.Executed)
                        {
                            editData.NewStatus = TriggerExecutionStatus.NotTriggered;
                            resetCount++;
                        }
                    }
                    
                    if (resetCount > 0)
                    {
                        _hasChanges = true;
                        MessageBox.Show($"已重置 {resetCount} 个触发条件的状态", "重置完成", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // 重新创建编辑器以反映状态变化
                        CreateTriggerConditionEditors();
                    }
                    else
                    {
                        MessageBox.Show("没有需要重置的触发条件", "无需操作", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 重置所有状态失败");
                MessageBox.Show($"重置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_hasChanges && _isEnabledCheckBox.IsChecked == _contract.IsEnabled)
                {
                    MessageBox.Show("没有检测到任何修改", "无需保存", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 验证输入
                if (!ValidateInputs())
                {
                    return;
                }

                // 应用修改
                ApplyChanges();
                
                DialogResult = true;
                _logger.LogInformation($"✅ 合约配置修改已保存: {_contract.ContractKey}");
                
                MessageBox.Show("修改已保存成功！", "保存完成", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 保存修改失败");
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasChanges)
            {
                var result = MessageBox.Show("有未保存的修改，确定要取消吗？", 
                    "确认取消", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            
            DialogResult = false;
            Close();
        }

        private bool ValidateInputs()
        {
            foreach (var editData in _editData.Values)
            {
                if (editData.NewTriggerPrice <= 0)
                {
                    MessageBox.Show($"触发金额必须大于0", "输入验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (editData.NewKeepValue < 0)
                {
                    MessageBox.Show($"保盈金额不能小于0", "输入验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            return true;
        }

        private void ApplyChanges()
        {
            // 应用合约启用状态
            _contract.IsEnabled = _isEnabledCheckBox.IsChecked ?? false;
            
            // 应用触发条件修改
            foreach (var condition in _contract.TriggerConditions)
            {
                if (_editData.TryGetValue(condition.Id, out var editData))
                {
                    condition.TriggerPrice = editData.NewTriggerPrice;
                    condition.KeepValue = editData.NewKeepValue;
                    condition.Status = editData.NewStatus;
                    
                    if (editData.NewStatus != editData.OriginalStatus && editData.NewStatus == TriggerExecutionStatus.NotTriggered)
                    {
                        condition.LastExecutionTime = null;
                        condition.StatusNote = $"手动重置 {DateTime.Now:HH:mm:ss}";
                    }
                }
            }
        }
    }

    /// <summary>
    /// 触发条件编辑数据
    /// </summary>
    public class TriggerConditionEditData
    {
        public int Id { get; set; }
        public decimal OriginalTriggerPrice { get; set; }
        public decimal OriginalKeepValue { get; set; }
        public TriggerExecutionStatus OriginalStatus { get; set; }
        
        public decimal NewTriggerPrice { get; set; }
        public decimal NewKeepValue { get; set; }
        public TriggerExecutionStatus NewStatus { get; set; }
        
        public bool HasTriggerPriceChanged => NewTriggerPrice != OriginalTriggerPrice;
        public bool HasKeepValueChanged => NewKeepValue != OriginalKeepValue;
        public bool HasStatusChanged => NewStatus != OriginalStatus;
        public bool HasAnyChanges => HasTriggerPriceChanged || HasKeepValueChanged || HasStatusChanged;
    }
} 