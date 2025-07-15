using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// 合约配置编辑对话框 - 支持修改触发金额、保盈金额和执行状态
    /// </summary>
    public partial class ContractStatusEditDialog : Window, INotifyPropertyChanged
    {
        private readonly ContractMonitorModel _contract;
        private readonly ILogger _logger;
        private readonly object _autoMonitorService;
        private bool _hasChanges = false;

        // UI控件
        private TextBlock _titleText;
        private TextBlock _contractInfoText;
        private DataGrid _allStatusGrid;
        private Button _saveButton;
        private Button _cancelButton;

        public bool HasChanges => _hasChanges;
        public ContractMonitorModel Contract => _contract;

        // 统一状态数据集合
        public ObservableCollection<StatusEditItem> AllStatusItems { get; } = new();

        public ContractStatusEditDialog(ContractMonitorModel contract, ILogger logger, object autoMonitorService = null)
        {
            _contract = contract ?? throw new ArgumentNullException(nameof(contract));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _autoMonitorService = autoMonitorService;

            // 🔒 严格检查：必须在自动盯盘停止状态下才能打开编辑窗口
            if (IsAutoMonitorRunning())
            {
                MessageBox.Show("⚠️ 安全限制：自动盯盘正在运行中，无法编辑状态！\n\n为确保数据一致性，请先停止自动盯盘，然后再编辑状态。", 
                    "编辑被阻止", MessageBoxButton.OK, MessageBoxImage.Warning);
                _logger.LogWarning("🔒 用户尝试在盯盘运行中打开状态编辑窗口，已阻止");
                DialogResult = false;
                return;
            }

            InitializeUI();
            InitializeDialog();
        }

        private void InitializeUI()
        {
            // 基本窗口设置
            Title = "合约配置编辑";
            Width = 1000;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;

            // 创建主布局
            var mainGrid = new Grid();
            mainGrid.Margin = new Thickness(16);
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 标题区域
            var titleBorder = new Border
            {
                Background = new SolidColorBrush(Colors.LightBlue),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            
            var titleStack = new StackPanel();
            
            _titleText = new TextBlock
            {
                Text = "合约配置编辑",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.DarkBlue)
            };
            
            _contractInfoText = new TextBlock
            {
                Text = "合约信息",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.DarkGreen),
                Margin = new Thickness(0, 4, 0, 0)
            };
            
            titleStack.Children.Add(_titleText);
            titleStack.Children.Add(_contractInfoText);
            titleBorder.Child = titleStack;
            Grid.SetRow(titleBorder, 0);

            // 数据表格区域
            var gridBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8)
            };

            _allStatusGrid = CreateDataGrid();
            gridBorder.Child = _allStatusGrid;
            Grid.SetRow(gridBorder, 1);

            // 底部按钮区域
            var buttonGrid = new Grid();
            buttonGrid.Margin = new Thickness(0, 12, 0, 0);
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tipText = new TextBlock
            {
                Text = "💡 可直接修改触发金额和保盈金额，修改后点击保存",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.Gray)
            };
            Grid.SetColumn(tipText, 0);

            _saveButton = new Button
            {
                Content = "💾 保存修改",
                Width = 100,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Colors.Green),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12
            };
            _saveButton.Click += SaveButton_Click;
            Grid.SetColumn(_saveButton, 1);

            _cancelButton = new Button
            {
                Content = "❌ 取消",
                Width = 80,
                Height = 32,
                Background = new SolidColorBrush(Colors.Gray),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12
            };
            _cancelButton.Click += CancelButton_Click;
            Grid.SetColumn(_cancelButton, 2);

            buttonGrid.Children.Add(tipText);
            buttonGrid.Children.Add(_saveButton);
            buttonGrid.Children.Add(_cancelButton);
            Grid.SetRow(buttonGrid, 2);

            mainGrid.Children.Add(titleBorder);
            mainGrid.Children.Add(gridBorder);
            mainGrid.Children.Add(buttonGrid);

            Content = mainGrid;
        }

        private DataGrid CreateDataGrid()
        {
            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserReorderColumns = false,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                HeadersVisibility = DataGridHeadersVisibility.All,
                FontSize = 12,
                RowHeight = 40,
                Background = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
                BorderThickness = new Thickness(0)
            };

            // 类型列
            var typeColumn = new DataGridTextColumn
            {
                Header = "类型",
                Binding = new System.Windows.Data.Binding("TypeText"),
                Width = 80,
                IsReadOnly = true
            };
            dataGrid.Columns.Add(typeColumn);

            // 阶段列
            var tierColumn = new DataGridTextColumn
            {
                Header = "阶段",
                Binding = new System.Windows.Data.Binding("TierText"),
                Width = 60,
                IsReadOnly = true
            };
            dataGrid.Columns.Add(tierColumn);

            // 触发金额列
            var triggerPriceColumn = new DataGridTemplateColumn
            {
                Header = "触发金额(USDT)",
                Width = 150
            };
            
            var triggerPriceTemplate = new DataTemplate();
            var triggerPriceTextBoxFactory = new FrameworkElementFactory(typeof(TextBox));
            triggerPriceTextBoxFactory.SetBinding(TextBox.TextProperty, 
                new System.Windows.Data.Binding("TriggerPrice") 
                { 
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
                    StringFormat = "F2"
                });
            triggerPriceTextBoxFactory.SetValue(TextBox.PaddingProperty, new Thickness(5));
            triggerPriceTextBoxFactory.SetValue(TextBox.FontSizeProperty, 11.0);
            triggerPriceTextBoxFactory.SetValue(TextBox.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            triggerPriceTextBoxFactory.SetValue(TextBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            triggerPriceTextBoxFactory.SetValue(TextBox.BorderBrushProperty, new SolidColorBrush(Colors.LightGray));
            triggerPriceTextBoxFactory.SetValue(TextBox.BorderThicknessProperty, new Thickness(1));
            triggerPriceTextBoxFactory.SetValue(TextBox.BackgroundProperty, Brushes.White);
            triggerPriceTemplate.VisualTree = triggerPriceTextBoxFactory;
            triggerPriceColumn.CellTemplate = triggerPriceTemplate;
            dataGrid.Columns.Add(triggerPriceColumn);

            // 保盈金额列
            var keepValueColumn = new DataGridTemplateColumn
            {
                Header = "保盈金额(USDT)",
                Width = 150
            };
            
            var keepValueTemplate = new DataTemplate();
            var keepValueTextBoxFactory = new FrameworkElementFactory(typeof(TextBox));
            keepValueTextBoxFactory.SetBinding(TextBox.TextProperty, 
                new System.Windows.Data.Binding("KeepValue") 
                { 
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
                    StringFormat = "F2"
                });
            keepValueTextBoxFactory.SetValue(TextBox.PaddingProperty, new Thickness(5));
            keepValueTextBoxFactory.SetValue(TextBox.FontSizeProperty, 11.0);
            keepValueTextBoxFactory.SetValue(TextBox.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            keepValueTextBoxFactory.SetValue(TextBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            keepValueTextBoxFactory.SetValue(TextBox.BorderBrushProperty, new SolidColorBrush(Colors.LightGray));
            keepValueTextBoxFactory.SetValue(TextBox.BorderThicknessProperty, new Thickness(1));
            keepValueTextBoxFactory.SetValue(TextBox.BackgroundProperty, Brushes.White);
            keepValueTextBoxFactory.SetBinding(TextBox.VisibilityProperty, 
                new System.Windows.Data.Binding("IsProfitProtection") 
                { 
                    Converter = new BooleanToVisibilityConverter() 
                });
            keepValueTemplate.VisualTree = keepValueTextBoxFactory;
            keepValueColumn.CellTemplate = keepValueTemplate;
            dataGrid.Columns.Add(keepValueColumn);

            // 描述列
            var descColumn = new DataGridTextColumn
            {
                Header = "条件描述",
                Binding = new System.Windows.Data.Binding("Description"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                IsReadOnly = true
            };
            dataGrid.Columns.Add(descColumn);

            // 状态列
            var statusColumn = new DataGridTextColumn
            {
                Header = "状态",
                Binding = new System.Windows.Data.Binding("StatusText"),
                Width = 80,
                IsReadOnly = true
            };
            dataGrid.Columns.Add(statusColumn);

            // 操作列
            var actionColumn = new DataGridTemplateColumn
            {
                Header = "操作",
                Width = 140
            };
            
            var actionTemplate = new DataTemplate();
            var actionButtonFactory = new FrameworkElementFactory(typeof(Button));
            actionButtonFactory.SetBinding(Button.ContentProperty, new System.Windows.Data.Binding("ToggleButtonText"));
            actionButtonFactory.SetBinding(Button.BackgroundProperty, new System.Windows.Data.Binding("ToggleButtonColor"));
            actionButtonFactory.SetBinding(Button.TagProperty, new System.Windows.Data.Binding());
            actionButtonFactory.SetBinding(Button.IsEnabledProperty, new System.Windows.Data.Binding("CanToggle"));
            actionButtonFactory.SetValue(Button.WidthProperty, 120.0);
            actionButtonFactory.SetValue(Button.HeightProperty, 28.0);
            actionButtonFactory.SetValue(Button.ForegroundProperty, Brushes.White);
            actionButtonFactory.SetValue(Button.FontSizeProperty, 10.0);
            actionButtonFactory.SetValue(Button.FontWeightProperty, FontWeights.Bold);
            actionButtonFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(ToggleStatusButton_Click));
            actionTemplate.VisualTree = actionButtonFactory;
            actionColumn.CellTemplate = actionTemplate;
            dataGrid.Columns.Add(actionColumn);

            return dataGrid;
        }

        /// <summary>
        /// 检查自动盯盘是否正在运行
        /// </summary>
        private bool IsAutoMonitorRunning()
        {
            try
            {
                if (_autoMonitorService == null) return false;
                
                // 使用反射检查IsRunning属性
                var isRunningProperty = _autoMonitorService.GetType().GetProperty("IsRunning");
                if (isRunningProperty != null)
                {
                    return (bool)(isRunningProperty.GetValue(_autoMonitorService) ?? false);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 检查自动盯盘状态时发生错误");
                return false;
            }
        }

        /// <summary>
        /// 检查编辑操作是否被允许
        /// </summary>
        private bool IsEditAllowed()
        {
            if (IsAutoMonitorRunning())
            {
                MessageBox.Show("⚠️ 安全限制：自动盯盘正在运行中，无法修改状态！\n\n为确保数据一致性，请先停止自动盯盘，然后再进行编辑。", 
                    "操作被阻止", MessageBoxButton.OK, MessageBoxImage.Warning);
                _logger.LogWarning("🔒 用户尝试在盯盘运行中修改状态，已阻止");
                return false;
            }
            return true;
        }

        private void InitializeDialog()
        {
            try
            {
                // 设置标题信息
                _titleText.Text = $"合约配置编辑 - {_contract.Symbol}";
                _contractInfoText.Text = $"{_contract.Symbol} {_contract.PositionSide} | 当前价: {_contract.CurrentPriceText} | 持仓: {_contract.PositionSizeText}";

                // 加载所有状态到统一表格
                LoadAllStatusItems();

                // 监听价格变化
                foreach (var item in AllStatusItems)
                {
                    item.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(StatusEditItem.TriggerPrice) || 
                            e.PropertyName == nameof(StatusEditItem.KeepValue) ||
                            e.PropertyName == nameof(StatusEditItem.Status))
                        {
                            _hasChanges = true;
                        }
                    };
                }

                _logger.LogInformation($"📝 配置编辑对话框已初始化 - {_contract.Symbol}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 配置编辑对话框初始化失败");
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAllStatusItems()
        {
            AllStatusItems.Clear();

            // 添加保本状态
            var breakEvenCondition = _contract.TriggerConditions?.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
            if (breakEvenCondition != null)
            {
                AllStatusItems.Add(new StatusEditItem
                {
                    Type = TriggerConditionType.BreakEven,
                    TypeText = "🛡️保本",
                    TierIndex = 0,
                    TierText = "-",
                    TriggerPrice = breakEvenCondition.TriggerPrice,
                    KeepValue = 0,
                    Description = breakEvenCondition.Description,
                    Status = breakEvenCondition.Status,
                    OriginalCondition = breakEvenCondition,
                    CanToggle = true,
                    IsProfitProtection = false
                });
            }

            // 添加加仓状态
            var addPositionConditions = _contract.TriggerConditions?
                .Where(c => c.Type == TriggerConditionType.AddPosition)
                .OrderBy(c => c.TierIndex ?? 0)
                .ToList();

            if (addPositionConditions?.Any() == true)
            {
                foreach (var condition in addPositionConditions)
                {
                    AllStatusItems.Add(new StatusEditItem
                    {
                        Type = TriggerConditionType.AddPosition,
                        TypeText = "📈加仓",
                        TierIndex = condition.TierIndex ?? 0,
                        TierText = $"{condition.TierIndex}",
                        TriggerPrice = condition.TriggerPrice,
                        KeepValue = 0,
                        Description = condition.Description,
                        Status = condition.Status,
                        OriginalCondition = condition,
                        CanToggle = true,
                        IsProfitProtection = false
                    });
                }
            }

            // 添加止盈状态
            var profitConditions = _contract.TriggerConditions?
                .Where(c => c.Type == TriggerConditionType.ProfitProtection)
                .OrderBy(c => c.TierIndex ?? 0)
                .ToList();

            if (profitConditions?.Any() == true)
            {
                foreach (var condition in profitConditions)
                {
                    AllStatusItems.Add(new StatusEditItem
                    {
                        Type = TriggerConditionType.ProfitProtection,
                        TypeText = "🎯止盈",
                        TierIndex = condition.TierIndex ?? 0,
                        TierText = $"{condition.TierIndex}",
                        TriggerPrice = condition.TriggerPrice,
                        KeepValue = condition.KeepValue,
                        Description = condition.Description,
                        Status = condition.Status,
                        OriginalCondition = condition,
                        CanToggle = true,
                        IsProfitProtection = true
                    });
                }
            }

            // 如果没有任何条件，添加提示项
            if (!AllStatusItems.Any())
            {
                AllStatusItems.Add(new StatusEditItem
                {
                    TypeText = "❌",
                    TierText = "-",
                    Description = "该合约尚未配置任何触发条件",
                    TriggerPrice = 0,
                    KeepValue = 0,
                    Status = TriggerExecutionStatus.NotTriggered,
                    CanToggle = false,
                    IsProfitProtection = false
                });
            }

            // 🔧 修复：在UI线程中安全绑定数据
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            {
                _allStatusGrid.ItemsSource = AllStatusItems;
            }
            else
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _allStatusGrid.ItemsSource = AllStatusItems;
                });
            }
        }

        private void ToggleStatusButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IsEditAllowed()) return;

                if (sender is Button button && button.Tag is StatusEditItem item)
                {
                    var oldStatus = item.Status;
                    item.Status = item.Status == TriggerExecutionStatus.NotTriggered 
                        ? TriggerExecutionStatus.Executed 
                        : TriggerExecutionStatus.NotTriggered;

                    _hasChanges = true;
                    _logger.LogInformation($"🔄 状态切换 - {item.TypeText} {item.TierText}: {oldStatus} → {item.Status}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 切换状态失败");
                MessageBox.Show($"切换状态失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IsEditAllowed()) return;

                if (!_hasChanges)
                {
                    MessageBox.Show("没有检测到任何更改", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 验证输入
                if (!ValidateInputs())
                {
                    return;
                }

                // 应用更改
                ApplyChanges();

                _logger.LogInformation($"✅ 配置编辑完成 - {_contract.Symbol}");
                MessageBox.Show("配置修改已保存！", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 保存配置失败");
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateInputs()
        {
            foreach (var item in AllStatusItems.Where(i => i.CanToggle))
            {
                if (item.TriggerPrice <= 0)
                {
                    MessageBox.Show($"{item.TypeText} {item.TierText} 的触发金额必须大于0", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (item.IsProfitProtection && item.KeepValue < 0)
                {
                    MessageBox.Show($"{item.TypeText} {item.TierText} 的保盈金额不能小于0", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            return true;
        }

        private void ApplyChanges()
        {
            foreach (var item in AllStatusItems.Where(i => i.CanToggle && i.OriginalCondition != null))
            {
                var condition = item.OriginalCondition;
                
                // 更新触发价格
                if (Math.Abs(condition.TriggerPrice - item.TriggerPrice) > 0.01m)
                {
                    _logger.LogInformation($"🔄 更新触发价格 - {item.TypeText} {item.TierText}: {condition.TriggerPrice:F2} → {item.TriggerPrice:F2}");
                    condition.TriggerPrice = item.TriggerPrice;
                    
                    // 🔧 关键修复：触发数值属性更新通知
                    condition.OnPropertyChanged(nameof(condition.TriggerPrice));
                    condition.OnPropertyChanged(nameof(condition.DisplayTriggerPrice));
                }

                // 更新保盈金额（仅止盈条件）
                if (item.IsProfitProtection && Math.Abs(condition.KeepValue - item.KeepValue) > 0.01m)
                {
                    _logger.LogInformation($"🔄 更新保盈金额 - {item.TypeText} {item.TierText}: {condition.KeepValue:F2} → {item.KeepValue:F2}");
                    condition.KeepValue = item.KeepValue;
                    
                    // 🔧 关键修复：触发数值属性更新通知
                    condition.OnPropertyChanged(nameof(condition.KeepValue));
                    condition.OnPropertyChanged(nameof(condition.DisplayKeepValue));
                }

                // 更新状态
                if (condition.Status != item.Status)
                {
                    _logger.LogInformation($"🔄 更新状态 - {item.TypeText} {item.TierText}: {condition.Status} → {item.Status}");
                    condition.Status = item.Status;
                    condition.LastExecutionTime = item.Status == TriggerExecutionStatus.Executed ? DateTime.Now : null;
                }
            }
            
            // 🔧 关键修复：强制触发所有状态显示属性的更新
            TriggerAllStatusPropertyChanges();
            
            // 🔧 新增：将状态变更同步到后台服务（如果服务可用）
            SyncChangesToBackendService();
        }

        /// <summary>
        /// 将状态变更同步到后台服务
        /// </summary>
        private void SyncChangesToBackendService()
        {
            try
            {
                if (_autoMonitorService == null)
                {
                    _logger.LogInformation("⚠️ 后台服务不可用，跳过同步");
                    return;
                }

                _logger.LogInformation($"🔄 开始同步状态变更到后台服务 - 合约: {_contract.Symbol}_{_contract.PositionSide}");

                // 使用反射获取AutoMonitorService的UnifiedStateManager
                var serviceType = _autoMonitorService.GetType();
                _logger.LogInformation($"🔧 服务类型: {serviceType.Name}");
                
                var unifiedStateManagerProperty = serviceType.GetProperty("UnifiedStateManager");
                
                if (unifiedStateManagerProperty == null)
                {
                    _logger.LogWarning("⚠️ 无法获取UnifiedStateManager属性");
                    return;
                }

                var unifiedStateManager = unifiedStateManagerProperty.GetValue(_autoMonitorService);
                if (unifiedStateManager == null)
                {
                    _logger.LogWarning("⚠️ UnifiedStateManager为空");
                    return;
                }

                _logger.LogInformation($"✅ 成功获取UnifiedStateManager: {unifiedStateManager.GetType().Name}");

                // 遍历所有修改的条件，同步到后台服务
                foreach (var item in AllStatusItems.Where(i => i.CanToggle && i.OriginalCondition != null))
                {
                    var condition = item.OriginalCondition;
                    
                    _logger.LogInformation($"🔍 处理条件: {item.TypeText} {item.TierText} - 状态: {condition.Status}");
                    
                    // 如果状态被修改为已执行，记录到状态管理器
                    if (condition.Status == TriggerExecutionStatus.Executed)
                    {
                        var executionType = condition.Type switch
                        {
                            TriggerConditionType.BreakEven => ExecutionType.BreakEven,
                            TriggerConditionType.AddPosition => ExecutionType.AddPosition,
                            TriggerConditionType.ProfitProtection => ExecutionType.ProfitProtection,
                            _ => ExecutionType.BreakEven
                        };

                        _logger.LogInformation($"📝 准备记录执行状态:");
                        _logger.LogInformation($"  - Symbol: {_contract.Symbol}");
                        _logger.LogInformation($"  - PositionSide: {_contract.PositionSide}");
                        _logger.LogInformation($"  - ExecutionType: {executionType}");
                        _logger.LogInformation($"  - TierIndex: {condition.TierIndex}");
                        _logger.LogInformation($"  - TriggerPrice: {condition.TriggerPrice}");

                        // 使用反射调用RecordExecution方法
                        var recordExecutionMethod = unifiedStateManager.GetType().GetMethod("RecordExecution");
                        if (recordExecutionMethod != null)
                        {
                            try
                            {
                                recordExecutionMethod.Invoke(unifiedStateManager, new object[]
                                {
                                    _contract.Symbol,
                                    _contract.PositionSide,
                                    executionType,
                                    condition.TierIndex,
                                    condition.TriggerPrice, // 使用触发价格作为触发浮盈
                                    true, // 成功
                                    "手动设置", // 消息
                                    true // autoSave
                                });

                                _logger.LogInformation($"✅ 已同步状态到后台服务: {_contract.Symbol}_{_contract.PositionSide} {item.TypeText}{item.TierText}");
                                
                                // 🔧 新增：验证状态是否正确保存
                                var isExecutedMethod = unifiedStateManager.GetType().GetMethod("IsExecuted");
                                if (isExecutedMethod != null)
                                {
                                    try
                                    {
                                        var isExecuted = (bool)isExecutedMethod.Invoke(unifiedStateManager, new object[]
                                        {
                                            _contract.Symbol,
                                            _contract.PositionSide,
                                            executionType,
                                            condition.TierIndex
                                        });
                                        
                                        _logger.LogInformation($"🔍 验证状态保存结果: {isExecuted}");
                                        
                                        if (!isExecuted)
                                        {
                                            _logger.LogError($"❌ 状态保存失败！IsExecuted返回false");
                                        }
                                    }
                                    catch (Exception verifyEx)
                                    {
                                        _logger.LogError(verifyEx, $"❌ 验证状态保存时发生异常");
                                    }
                                }
                            }
                            catch (Exception invokeEx)
                            {
                                _logger.LogError(invokeEx, $"❌ 调用RecordExecution失败: {_contract.Symbol}_{_contract.PositionSide} {item.TypeText}{item.TierText}");
                            }
                        }
                        else
                        {
                            _logger.LogError($"❌ 无法获取RecordExecution方法");
                        }
                    }
                    else if (condition.Status == TriggerExecutionStatus.NotTriggered)
                    {
                        _logger.LogInformation($"🔄 重置状态为未触发: {item.TypeText}{item.TierText}");
                        
                        // 如果状态被重置为未触发，清理状态管理器中的记录
                        var clearContractStatesMethod = _autoMonitorService.GetType().GetMethod("ClearContractStates");
                        if (clearContractStatesMethod != null)
                        {
                            try
                            {
                                clearContractStatesMethod.Invoke(_autoMonitorService, new object[]
                                {
                                    _contract.Symbol,
                                    _contract.PositionSide,
                                    "手动重置"
                                });

                                _logger.LogInformation($"✅ 已清理后台状态: {_contract.Symbol}_{_contract.PositionSide} {item.TypeText}{item.TierText}");
                            }
                            catch (Exception clearEx)
                            {
                                _logger.LogError(clearEx, $"❌ 清理后台状态失败: {_contract.Symbol}_{_contract.PositionSide} {item.TypeText}{item.TierText}");
                            }
                        }
                        else
                        {
                            _logger.LogError($"❌ 无法获取ClearContractStates方法");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 同步状态到后台服务失败");
                // 不抛出异常，只记录错误
            }
        }

        /// <summary>
        /// 触发所有状态显示属性的变化通知
        /// </summary>
        private void TriggerAllStatusPropertyChanges()
        {
            try
            {
                _logger.LogInformation("🔄 触发所有状态显示属性更新");
                
                // 🔧 在UI线程中安全执行属性更新
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 触发合约模型的核心属性更新
                    _contract.OnPropertyChanged(nameof(_contract.TriggerConditions));
                    
                    // 触发保本相关属性更新
                    _contract.OnPropertyChanged(nameof(_contract.BreakEvenDisplay));
                    _contract.OnPropertyChanged(nameof(_contract.BreakEvenStatusDisplay));
                    _contract.OnPropertyChanged(nameof(_contract.BreakEvenStatusColor));
                    _contract.OnPropertyChanged(nameof(_contract.BreakEvenTriggerDisplay));
                    _contract.OnPropertyChanged(nameof(_contract.BreakEvenStatusIcon));
                    _contract.OnPropertyChanged(nameof(_contract.BreakEvenStatusIconColor));
                    
                    // 触发推仓相关属性更新（0-9档）
                    for (int i = 0; i < 10; i++)
                    {
                        _contract.OnPropertyChanged($"AddPositionTier{i}Display");
                        _contract.OnPropertyChanged($"AddPositionTier{i}Status");
                        _contract.OnPropertyChanged($"AddPositionTier{i}StatusColor");
                    }
                    
                    // 触发止盈相关属性更新（0-9档）
                    for (int i = 0; i < 10; i++)
                    {
                        _contract.OnPropertyChanged($"ProfitProtectionTier{i}Display");
                        _contract.OnPropertyChanged($"ProfitProtectionTier{i}Status");
                        _contract.OnPropertyChanged($"ProfitProtectionTier{i}StatusColor");
                    }
                    
                    // 触发统计相关属性更新
                    _contract.OnPropertyChanged(nameof(_contract.ExecutedCount));
                    _contract.OnPropertyChanged(nameof(_contract.TotalCount));
                    _contract.OnPropertyChanged(nameof(_contract.ExecutionProgress));
                    _contract.OnPropertyChanged(nameof(_contract.ExecutedAddPositionCount));
                    _contract.OnPropertyChanged(nameof(_contract.TotalAddPositionCount));
                    _contract.OnPropertyChanged(nameof(_contract.ExecutedProfitCount));
                    _contract.OnPropertyChanged(nameof(_contract.TotalProfitCount));
                    
                    // 触发进度显示属性更新
                    _contract.OnPropertyChanged(nameof(_contract.AddPositionProgressDisplay));
                    _contract.OnPropertyChanged(nameof(_contract.AddPositionStatusIcon));
                    _contract.OnPropertyChanged(nameof(_contract.AddPositionStatusIconColor));
                    _contract.OnPropertyChanged(nameof(_contract.AddPositionProgressColor));
                    _contract.OnPropertyChanged(nameof(_contract.AddPositionProgressIcon));
                    _contract.OnPropertyChanged(nameof(_contract.AddPositionProgressText));
                    
                    _contract.OnPropertyChanged(nameof(_contract.ProfitProtectionProgressDisplay));
                    _contract.OnPropertyChanged(nameof(_contract.ProfitProtectionStatusIcon));
                    _contract.OnPropertyChanged(nameof(_contract.ProfitProtectionStatusIconColor));
                    _contract.OnPropertyChanged(nameof(_contract.ProfitProgressColor));
                    _contract.OnPropertyChanged(nameof(_contract.ProfitProgressIcon));
                    _contract.OnPropertyChanged(nameof(_contract.ProfitProgressText));
                    
                    _contract.OnPropertyChanged(nameof(_contract.BreakEvenProgressColor));
                    
                    _logger.LogInformation("✅ 所有状态显示属性更新完成");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 触发状态属性更新失败");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class StatusEditItem : INotifyPropertyChanged
    {
        private TriggerExecutionStatus _status;
        private bool _canToggle = true;
        private decimal _triggerPrice;
        private decimal _keepValue;

        public TriggerConditionType Type { get; set; }
        public string TypeText { get; set; } = "";
        public int TierIndex { get; set; }
        public string TierText { get; set; } = "";
        public string Description { get; set; } = "";
        public TriggerConditionModel? OriginalCondition { get; set; }
        public bool IsProfitProtection { get; set; }

        public decimal TriggerPrice
        {
            get => _triggerPrice;
            set
            {
                if (_triggerPrice != value)
                {
                    _triggerPrice = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal KeepValue
        {
            get => _keepValue;
            set
            {
                if (_keepValue != value)
                {
                    _keepValue = value;
                    OnPropertyChanged();
                }
            }
        }

        public TriggerExecutionStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(ToggleButtonText));
                    OnPropertyChanged(nameof(ToggleButtonColor));
                }
            }
        }

        public bool CanToggle
        {
            get => _canToggle;
            set
            {
                if (_canToggle != value)
                {
                    _canToggle = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusText => Status switch
        {
            TriggerExecutionStatus.NotTriggered => "未触发",
            TriggerExecutionStatus.Executed => "已执行",
            _ => "未知"
        };

        public Brush StatusColor => Status switch
        {
            TriggerExecutionStatus.NotTriggered => new SolidColorBrush(Colors.Green),
            TriggerExecutionStatus.Executed => new SolidColorBrush(Colors.Red),
            _ => new SolidColorBrush(Colors.Gray)
        };

        public string ToggleButtonText => Status == TriggerExecutionStatus.NotTriggered ? "设为已执行" : "设为未触发";

        public Brush ToggleButtonColor => Status == TriggerExecutionStatus.NotTriggered ? 
            new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Green);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 