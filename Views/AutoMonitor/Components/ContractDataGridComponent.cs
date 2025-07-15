using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows;
using System.Linq;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using Microsoft.Extensions.Logging;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Views.AutoMonitor.Components
{
    /// <summary>
    /// 合约数据网格组件
    /// </summary>
    public class ContractDataGridComponent : INotifyPropertyChanged
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly ILogger _logger;
        private DataGrid _dataGrid;
        private int _lastAddPositionTierCount = 4;  // 记录上次推仓阶梯数
        private int _lastProfitProtectionTierCount = 3;  // 记录上次止盈阶梯数
        
        public ContractDataGridComponent(AutoMonitorDataModel dataModel, ILogger logger)
        {
            _dataModel = dataModel;
            _logger = logger;
            
            // 🎯 修复：从基础配置初始化阶梯数
            InitializeFromBaseConfiguration();
            
            CreateDataGrid();
            SetupConfigurationMonitoring();
        }

        /// <summary>
        /// 🎯 新增：从基础配置初始化阶梯数
        /// </summary>
        private void InitializeFromBaseConfiguration()
        {
            try
            {
                // 尝试从主视图模型获取当前配置
                var config = GetCurrentAutoMonitorConfig();
                if (config != null)
                {
                    var addPositionTiers = config.AddPositionConfig.IsEnabled ? 
                        config.AddPositionConfig.Tiers.Count : 0;
                    var profitProtectionTiers = config.ProfitProtectionConfig.IsEnabled ? 
                        config.ProfitProtectionConfig.Tiers.Count : 0;
                    
                    _lastAddPositionTierCount = Math.Max(addPositionTiers, 4);
                    _lastProfitProtectionTierCount = Math.Max(profitProtectionTiers, 3);
                    
                    _logger.LogInformation($"从基础配置初始化：推仓{_lastAddPositionTierCount}阶梯，止盈{_lastProfitProtectionTierCount}阶梯");
                }
                else
                {
                    _logger.LogWarning("无法获取基础配置，使用默认阶梯数");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从基础配置初始化时发生异常");
            }
        }

        /// <summary>
        /// 🎯 新增：获取当前自动监控配置
        /// </summary>
        private AutoMonitorConfig GetCurrentAutoMonitorConfig()
        {
            try
            {
                // 通过 Application.Current.MainWindow 获取主视图模型
                if (Application.Current?.MainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
                {
                    return mainViewModel.CurrentAutoMonitorConfig;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取当前自动监控配置时发生异常");
                return null;
            }
        }
        
        /// <summary>
        /// 设置配置监听
        /// </summary>
        private void SetupConfigurationMonitoring()
        {
            try
            {
                // 监听合约集合变化
                _dataModel.ContractMonitors.CollectionChanged += (s, e) =>
                {
                    // 🔧 修复：确保在UI线程中执行刷新操作
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CheckAndRefreshColumnsIfNeeded();
                    }));
                };
                
                // 监听每个合约的触发条件变化
                foreach (var contract in _dataModel.ContractMonitors)
                {
                    SubscribeToContractChanges(contract);
                }
                
                _logger.LogDebug("配置监听设置完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置配置监听时发生异常");
            }
        }
        
        /// <summary>
        /// 订阅合约变化事件
        /// </summary>
        private void SubscribeToContractChanges(ContractMonitorModel contract)
        {
            contract.TriggerConditions.CollectionChanged += (s, e) =>
            {
                // 🔧 修复：确保在UI线程中执行刷新操作
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    CheckAndRefreshColumnsIfNeeded();
                }));
            };
        }
        
        /// <summary>
        /// 检查并在需要时刷新列结构
        /// </summary>
        private void CheckAndRefreshColumnsIfNeeded()
        {
            try
            {
                var currentAddPositionTierCount = GetMaxAddPositionTiers();
                var currentProfitProtectionTierCount = GetMaxProfitProtectionTiers();
                
                // 检查是否需要刷新列结构
                if (currentAddPositionTierCount != _lastAddPositionTierCount ||
                    currentProfitProtectionTierCount != _lastProfitProtectionTierCount)
                {
                    _logger.LogInformation($"检测到配置变化：推仓阶梯 {_lastAddPositionTierCount}→{currentAddPositionTierCount}，止盈阶梯 {_lastProfitProtectionTierCount}→{currentProfitProtectionTierCount}");
                    
                    // 更新记录的数量
                    _lastAddPositionTierCount = currentAddPositionTierCount;
                    _lastProfitProtectionTierCount = currentProfitProtectionTierCount;
                    
                    // 刷新列结构
                    RefreshColumns();
                    
                    // 通知配置变化事件
                    OnConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
                    {
                        AddPositionTierCount = currentAddPositionTierCount,
                        ProfitProtectionTierCount = currentProfitProtectionTierCount
                    });
                    
                    _logger.LogInformation("列结构已动态调整");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查列结构变化时发生异常");
            }
        }
        
        /// <summary>
        /// 创建数据网格（美化版）
        /// </summary>
        private void CreateDataGrid()
        {
            _dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeColumns = true,
                CanUserSortColumns = true,
                GridLinesVisibility = DataGridGridLinesVisibility.None,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                RowHeight = 35,  // 增加行高
                FontSize = 12,   // 字体大小
                FontFamily = new FontFamily("Microsoft YaHei UI"),  // 使用微软雅黑字体
                Margin = new Thickness(0),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 226, 226)),
                Background = new SolidColorBrush(Colors.White),
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                VerticalGridLinesBrush = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(250, 250, 250))
            };

            // 设置表格样式
            SetDataGridStyle();
            
            CreateColumns();
            BindData();
            SetupEventHandlers();
        }

        /// <summary>
        /// 设置数据网格样式
        /// </summary>
        private void SetDataGridStyle()
        {
            // 表头样式
            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, 
                new SolidColorBrush(Color.FromRgb(64, 128, 196))));  // 深蓝色背景
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, 
                new SolidColorBrush(Colors.White)));  // 白色文字
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, 
                FontWeights.Bold));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, 13.0));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, 
                new Thickness(10, 8, 10, 8)));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, 
                new SolidColorBrush(Color.FromRgb(48, 96, 144))));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, 
                new Thickness(0, 0, 1, 0)));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, 
                HorizontalAlignment.Center));

            // 行样式
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(DataGridRow.BorderBrushProperty, 
                new SolidColorBrush(Color.FromRgb(226, 226, 226))));
            rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, 
                new Thickness(0, 0, 0, 1)));

            // 设置鼠标悬停样式
            var mouseOverTrigger = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
            mouseOverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, 
                new SolidColorBrush(Color.FromRgb(233, 244, 255))));
            rowStyle.Triggers.Add(mouseOverTrigger);

            // 设置选中样式
            var selectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, 
                new SolidColorBrush(Color.FromRgb(185, 216, 255))));
            rowStyle.Triggers.Add(selectedTrigger);

            // 单元格样式
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, 
                new Thickness(0)));
            cellStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty, 
                new Thickness(8, 5, 8, 5)));
            cellStyle.Setters.Add(new Setter(DataGridCell.VerticalContentAlignmentProperty, 
                VerticalAlignment.Center));
            
            // 单元格获取焦点时的样式
            var cellFocusTrigger = new Trigger { Property = DataGridCell.IsFocusedProperty, Value = true };
            cellFocusTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, 
                new Thickness(0)));
            cellStyle.Triggers.Add(cellFocusTrigger);

            // 应用样式
            _dataGrid.ColumnHeaderStyle = headerStyle;
            _dataGrid.RowStyle = rowStyle;
            _dataGrid.CellStyle = cellStyle;
        }
        
        /// <summary>
        /// 设置事件处理器
        /// </summary>
        private void SetupEventHandlers()
        {
            _dataGrid.MouseDoubleClick += OnDataGridDoubleClick;
        }
        
        /// <summary>
        /// 数据网格双击事件处理
        /// </summary>
        private void OnDataGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (_dataGrid.SelectedItem is ContractMonitorModel selectedContract)
                {
                    ShowContractConfigurationDetails(selectedContract);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "显示合约配置详情时发生异常");
            }
        }
        
        /// <summary>
        /// 显示合约配置编辑对话框
        /// </summary>
        /// <param name="contract">合约信息</param>
        private void ShowContractConfigurationDetails(ContractMonitorModel contract)
        {
            var editWindow = new Window
            {
                Title = $"合约配置编辑 - {contract.Symbol} {contract.PositionSide}",
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow
            };
            
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10)
            };
            
            var stackPanel = new StackPanel();
            
            // 基本信息编辑
            AddConfigurationSection(stackPanel, "基本信息", CreateBasicInfoEditPanel(contract));
            
            // 保本配置编辑
            AddConfigurationSection(stackPanel, "保本配置", CreateBreakEvenConfigEditPanel(contract));
            
            // 推仓配置编辑
            AddConfigurationSection(stackPanel, "推仓配置", CreateAddPositionConfigEditPanel(contract));
            
            // 止盈配置编辑
            AddConfigurationSection(stackPanel, "止盈配置", CreateProfitProtectionConfigEditPanel(contract));
            
            scrollViewer.Content = stackPanel;
            Grid.SetRow(scrollViewer, 0);
            mainGrid.Children.Add(scrollViewer);
            
            // 按钮面板
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            
            var saveButton = new Button
            {
                Content = "保存",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsDefault = true
            };
            
            var cancelButton = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsCancel = true
            };
            
            saveButton.Click += (s, e) => {
                try
                {
                    SaveContractConfiguration(contract, stackPanel);
                    editWindow.DialogResult = true;
                    editWindow.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "保存合约配置时发生异常");
                    MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            
            cancelButton.Click += (s, e) => {
                editWindow.DialogResult = false;
                editWindow.Close();
            };
            
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            
            Grid.SetRow(buttonPanel, 1);
            mainGrid.Children.Add(buttonPanel);
            
            editWindow.Content = mainGrid;
            editWindow.ShowDialog();
        }
        
        /// <summary>
        /// 添加配置节到面板
        /// </summary>
        private void AddConfigurationSection(StackPanel parent, string title, FrameworkElement content)
        {
            var titleLabel = new Label
            {
                Content = title,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 10, 0, 5)
            };
            
            var border = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10)
            };
            
            border.Child = content;
            
            parent.Children.Add(titleLabel);
            parent.Children.Add(border);
        }
        
        /// <summary>
        /// 创建基本信息面板
        /// </summary>
        private FrameworkElement CreateBasicInfoPanel(ContractMonitorModel contract)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            var row = 0;
            
            AddInfoRow(grid, row++, "合约符号:", contract.Symbol);
            AddInfoRow(grid, row++, "持仓方向:", contract.PositionSide);
            AddInfoRow(grid, row++, "当前价格:", contract.CurrentPriceText);
            AddInfoRow(grid, row++, "持仓大小:", contract.PositionSizeText);
            AddInfoRow(grid, row++, "未实现盈亏:", contract.PnlText);
            AddInfoRow(grid, row++, "启用状态:", contract.IsEnabled ? "启用" : "禁用");
            AddInfoRow(grid, row++, "活跃状态:", contract.IsActive ? "活跃" : "非活跃");
            
            return grid;
        }
        
        /// <summary>
        /// 创建基本信息编辑面板
        /// </summary>
        private FrameworkElement CreateBasicInfoEditPanel(ContractMonitorModel contract)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            var row = 0;
            
            // 只读信息
            AddInfoRow(grid, row++, "合约符号:", contract.Symbol);
            AddInfoRow(grid, row++, "持仓方向:", contract.PositionSide);
            AddInfoRow(grid, row++, "当前价格:", contract.CurrentPriceText);
            AddInfoRow(grid, row++, "持仓大小:", contract.PositionSizeText);
            AddInfoRow(grid, row++, "未实现盈亏:", contract.PnlText);
            AddInfoRow(grid, row++, "启用状态:", contract.IsEnabled ? "启用" : "禁用");
            AddInfoRow(grid, row++, "活跃状态:", contract.IsActive ? "活跃" : "非活跃");
            
            return grid;
        }
        
        /// <summary>
        /// 创建保本配置面板
        /// </summary>
        private FrameworkElement CreateBreakEvenConfigPanel(ContractMonitorModel contract)
        {
            var breakEvenCondition = contract.TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
            
            if (breakEvenCondition == null)
            {
                return new TextBlock
                {
                    Text = "未配置保本条件",
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray
                };
            }
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            var row = 0;
            AddInfoRow(grid, row++, "触发价格:", breakEvenCondition.DisplayTriggerPrice + " USDT");
            AddInfoRow(grid, row++, "当前状态:", breakEvenCondition.StatusText);
            AddInfoRow(grid, row++, "最后执行时间:", breakEvenCondition.LastExecutionTimeText);
            AddInfoRow(grid, row++, "条件描述:", breakEvenCondition.Description);
            if (!string.IsNullOrEmpty(breakEvenCondition.StatusNote))
            {
                AddInfoRow(grid, row++, "状态说明:", breakEvenCondition.StatusNote);
            }
            
            return grid;
        }
        
        /// <summary>
        /// 创建推仓配置面板
        /// </summary>
        private FrameworkElement CreateAddPositionConfigPanel(ContractMonitorModel contract)
        {
            var addPositionConditions = contract.TriggerConditions.Where(c => c.Type == TriggerConditionType.AddPosition).OrderBy(c => c.TierIndex).ToList();
            
            if (!addPositionConditions.Any())
            {
                return new TextBlock
                {
                    Text = "未配置推仓条件",
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray
                };
            }
            
            var stackPanel = new StackPanel();
            
            foreach (var condition in addPositionConditions)
            {
                var tierTitle = new TextBlock
                {
                    Text = $"推仓阶梯 {condition.TierIndex}",  // 🔧 修复：TierIndex已经是从1开始的，不需要+1
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                var row = 0;
                AddInfoRow(grid, row++, "触发价格:", condition.DisplayTriggerPrice + " USDT");
                AddInfoRow(grid, row++, "当前状态:", condition.StatusText);
                AddInfoRow(grid, row++, "最后执行时间:", condition.LastExecutionTimeText);
                AddInfoRow(grid, row++, "条件描述:", condition.Description);
                if (!string.IsNullOrEmpty(condition.StatusNote))
                {
                    AddInfoRow(grid, row++, "状态说明:", condition.StatusNote);
                }
                
                stackPanel.Children.Add(tierTitle);
                stackPanel.Children.Add(grid);
            }
            
            return stackPanel;
        }
        
        /// <summary>
        /// 创建止盈配置面板
        /// </summary>
        private FrameworkElement CreateProfitProtectionConfigPanel(ContractMonitorModel contract)
        {
            var profitConditions = contract.TriggerConditions.Where(c => c.Type == TriggerConditionType.ProfitProtection).OrderBy(c => c.TierIndex).ToList();
            
            if (!profitConditions.Any())
            {
                return new TextBlock
                {
                    Text = "未配置止盈条件",
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray
                };
            }
            
            var stackPanel = new StackPanel();
            
            foreach (var condition in profitConditions)
            {
                var tierTitle = new TextBlock
                {
                    Text = $"止盈阶梯 {condition.TierIndex}",  // 🔧 修复：TierIndex已经是从1开始的，不需要+1
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                var row = 0;
                AddInfoRow(grid, row++, "触发价格:", condition.DisplayTriggerPrice + " USDT");
                AddInfoRow(grid, row++, "保护金额:", condition.DisplayKeepValue + " USDT");
                AddInfoRow(grid, row++, "当前状态:", condition.StatusText);
                AddInfoRow(grid, row++, "最后执行时间:", condition.LastExecutionTimeText);
                AddInfoRow(grid, row++, "条件描述:", condition.Description);
                if (!string.IsNullOrEmpty(condition.StatusNote))
                {
                    AddInfoRow(grid, row++, "状态说明:", condition.StatusNote);
                }
                
                stackPanel.Children.Add(tierTitle);
                stackPanel.Children.Add(grid);
            }
            
            return stackPanel;
        }
        
        /// <summary>
        /// 创建保本配置编辑面板
        /// </summary>
        private FrameworkElement CreateBreakEvenConfigEditPanel(ContractMonitorModel contract)
        {
            var breakEvenCondition = contract.TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
            
            if (breakEvenCondition == null)
            {
                return new TextBlock
                {
                    Text = "未配置保本条件",
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray
                };
            }
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            var row = 0;
            AddEditRow(grid, row++, "触发浮盈:", CreateTriggerValueTextBox(breakEvenCondition, "TriggerPrice"));
            AddEditRow(grid, row++, "执行状态:", CreateStatusComboBox(breakEvenCondition));
            AddInfoRow(grid, row++, "最后执行时间:", breakEvenCondition.LastExecutionTimeText);
            AddInfoRow(grid, row++, "条件描述:", breakEvenCondition.Description);
            
            return grid;
        }
        
        /// <summary>
        /// 创建推仓配置编辑面板
        /// </summary>
        private FrameworkElement CreateAddPositionConfigEditPanel(ContractMonitorModel contract)
        {
            var addPositionConditions = contract.TriggerConditions.Where(c => c.Type == TriggerConditionType.AddPosition).OrderBy(c => c.TierIndex).ToList();
            
            if (!addPositionConditions.Any())
            {
                return new TextBlock
                {
                    Text = "未配置推仓条件",
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray
                };
            }
            
            var stackPanel = new StackPanel();
            
            foreach (var condition in addPositionConditions)
            {
                var tierTitle = new TextBlock
                {
                    Text = $"推仓阶梯 {condition.TierIndex}",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                var row = 0;
                AddEditRow(grid, row++, "触发浮盈:", CreateTriggerValueTextBox(condition, "TriggerPrice"));
                AddEditRow(grid, row++, "执行状态:", CreateStatusComboBox(condition));
                AddInfoRow(grid, row++, "最后执行时间:", condition.LastExecutionTimeText);
                AddInfoRow(grid, row++, "条件描述:", condition.Description);
                
                stackPanel.Children.Add(tierTitle);
                stackPanel.Children.Add(grid);
            }
            
            return stackPanel;
        }
        
        /// <summary>
        /// 创建止盈配置编辑面板
        /// </summary>
        private FrameworkElement CreateProfitProtectionConfigEditPanel(ContractMonitorModel contract)
        {
            var profitConditions = contract.TriggerConditions.Where(c => c.Type == TriggerConditionType.ProfitProtection).OrderBy(c => c.TierIndex).ToList();
            
            if (!profitConditions.Any())
            {
                return new TextBlock
                {
                    Text = "未配置止盈条件",
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray
                };
            }
            
            var stackPanel = new StackPanel();
            
            foreach (var condition in profitConditions)
            {
                var tierTitle = new TextBlock
                {
                    Text = $"止盈阶梯 {condition.TierIndex}",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                var row = 0;
                AddEditRow(grid, row++, "目标浮盈:", CreateTriggerValueTextBox(condition, "TriggerPrice"));
                AddEditRow(grid, row++, "保留浮盈:", CreateKeepValueTextBox(condition, "KeepValue"));
                AddEditRow(grid, row++, "执行状态:", CreateStatusComboBox(condition));
                AddInfoRow(grid, row++, "最后执行时间:", condition.LastExecutionTimeText);
                AddInfoRow(grid, row++, "条件描述:", condition.Description);
                
                stackPanel.Children.Add(tierTitle);
                stackPanel.Children.Add(grid);
            }
            
            return stackPanel;
        }
        
        /// <summary>
        /// 创建触发值文本框
        /// </summary>
        private TextBox CreateTriggerValueTextBox(TriggerConditionModel condition, string propertyName)
        {
            var value = propertyName == "TriggerPrice" ? condition.TriggerPrice : condition.KeepValue;
            var textBox = new TextBox
            {
                Text = value.ToString("F0"),
                Width = 100,
                Tag = new { Condition = condition, Property = propertyName }
            };
            return textBox;
        }
        
        /// <summary>
        /// 创建保留值文本框
        /// </summary>
        private TextBox CreateKeepValueTextBox(TriggerConditionModel condition, string propertyName)
        {
            var textBox = new TextBox
            {
                Text = condition.KeepValue.ToString("F0"),
                Width = 100,
                Tag = new { Condition = condition, Property = propertyName }
            };
            return textBox;
        }
        
        /// <summary>
        /// 保存合约配置
        /// </summary>
        private void SaveContractConfiguration(ContractMonitorModel contract, StackPanel stackPanel)
        {
            try
            {
                _logger.LogInformation($"开始保存合约 {contract.Symbol} 的配置");
                
                // 遍历所有控件，保存编辑的值
                SaveControlValues(stackPanel, contract);
                
                // 触发属性更新
                contract.OnPropertyChanged(string.Empty);
                
                // 刷新数据显示
                RefreshData();
                
                _logger.LogInformation($"合约 {contract.Symbol} 配置保存成功");
                MessageBox.Show("配置保存成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"保存合约 {contract.Symbol} 配置时发生异常");
                throw;
            }
        }
        
        /// <summary>
        /// 递归保存控件值
        /// </summary>
        private void SaveControlValues(Panel panel, ContractMonitorModel contract)
        {
            foreach (var child in panel.Children)
            {
                if (child is CheckBox checkBox && checkBox.Tag != null)
                {
                    var tag = checkBox.Tag.ToString();
                    if (tag == "IsEnabled")
                    {
                        contract.IsEnabled = checkBox.IsChecked ?? false;
                    }
                    else if (tag == "IsActive")
                    {
                        contract.IsActive = checkBox.IsChecked ?? false;
                    }
                }
                else if (child is TextBox textBox && textBox.Tag != null)
                {
                    // 使用反射获取Tag中的信息
                    var tagType = textBox.Tag.GetType();
                    var conditionProperty = tagType.GetProperty("Condition");
                    var propertyProperty = tagType.GetProperty("Property");
                    
                    if (conditionProperty != null && propertyProperty != null &&
                        decimal.TryParse(textBox.Text, out decimal parsedValue))
                    {
                        var condition = conditionProperty.GetValue(textBox.Tag) as TriggerConditionModel;
                        var property = propertyProperty.GetValue(textBox.Tag) as string;
                        
                        if (condition != null && property == "TriggerPrice")
                        {
                            condition.TriggerPrice = parsedValue;
                            _logger.LogInformation($"更新触发条件 {condition.Description} 的触发浮盈: {parsedValue}U");
                        }
                        else if (condition != null && property == "KeepValue")
                        {
                            condition.KeepValue = parsedValue;
                            _logger.LogInformation($"更新触发条件 {condition.Description} 的保留浮盈: {parsedValue}U");
                        }
                    }
                }
                else if (child is ComboBox comboBox && comboBox.Tag != null && comboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    // 处理状态选择
                    var tagType = comboBox.Tag.GetType();
                    var conditionProperty = tagType.GetProperty("Condition");
                    var propertyProperty = tagType.GetProperty("Property");
                    
                    if (conditionProperty != null && propertyProperty != null)
                    {
                        var condition = conditionProperty.GetValue(comboBox.Tag) as TriggerConditionModel;
                        var property = propertyProperty.GetValue(comboBox.Tag) as string;
                        
                        if (condition != null && property == "Status" && selectedItem.Tag is TriggerExecutionStatus newStatus)
                        {
                            var oldStatus = condition.Status;
                            condition.Status = newStatus;
                            _logger.LogInformation($"更新触发条件 {condition.Description} 的状态: {oldStatus} → {newStatus}");
                            
                            // 如果状态从已执行改为未触发，清除执行时间
                            if (oldStatus == TriggerExecutionStatus.Executed && newStatus == TriggerExecutionStatus.NotTriggered)
                            {
                                condition.LastExecutionTime = null;
                                _logger.LogInformation($"清除触发条件 {condition.Description} 的执行时间");
                            }
                            // 如果状态从未触发改为已执行，设置执行时间
                            else if (oldStatus == TriggerExecutionStatus.NotTriggered && newStatus == TriggerExecutionStatus.Executed)
                            {
                                condition.LastExecutionTime = DateTime.Now;
                                _logger.LogInformation($"设置触发条件 {condition.Description} 的执行时间: {condition.LastExecutionTime}");
                            }
                        }
                    }
                }
                else if (child is Panel childPanel)
                {
                    SaveControlValues(childPanel, contract);
                }
            }
        }
        
        /// <summary>
        /// 向网格添加信息行
        /// </summary>
        private void AddInfoRow(Grid grid, int row, string label, string value)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var labelBlock = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 10, 2)
            };
            
            var valueBlock = new TextBlock
            {
                Text = value,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            
            grid.Children.Add(labelBlock);
            grid.Children.Add(valueBlock);
        }
        
        /// <summary>
        /// 向网格添加编辑行
        /// </summary>
        private void AddEditRow(Grid grid, int row, string label, FrameworkElement control)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var labelBlock = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 10, 2)
            };
            
            control.VerticalAlignment = VerticalAlignment.Center;
            control.Margin = new Thickness(0, 2, 0, 2);
            
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            
            grid.Children.Add(labelBlock);
            grid.Children.Add(control);
        }
        
        /// <summary>
        /// 创建启用状态复选框
        /// </summary>
        private CheckBox CreateEnabledCheckBox(ContractMonitorModel contract)
        {
            var checkBox = new CheckBox
            {
                IsChecked = contract.IsEnabled,
                Content = "启用合约监控",
                Tag = "IsEnabled"
            };
            return checkBox;
        }
        
        /// <summary>
        /// 创建活跃状态复选框
        /// </summary>
        private CheckBox CreateActiveCheckBox(ContractMonitorModel contract)
        {
            var checkBox = new CheckBox
            {
                IsChecked = contract.IsActive,
                Content = "设为活跃状态",
                Tag = "IsActive"
            };
            return checkBox;
        }
        
        /// <summary>
        /// 创建触发条件状态选择下拉框
        /// </summary>
        private ComboBox CreateStatusComboBox(TriggerConditionModel condition)
        {
            var comboBox = new ComboBox
            {
                Width = 120,
                Tag = new { Condition = condition, Property = "Status" }
            };
            
            // 添加可选状态（排除执行中，执行中是内部状态）
            comboBox.Items.Add(new ComboBoxItem
            {
                Content = "未触发",
                Tag = TriggerExecutionStatus.NotTriggered
            });
            
            comboBox.Items.Add(new ComboBoxItem
            {
                Content = "已执行",
                Tag = TriggerExecutionStatus.Executed
            });
            
            // 设置当前选中项
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if ((TriggerExecutionStatus)item.Tag == condition.Status)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
            
            // 如果当前状态是执行中，默认选择未触发
            if (comboBox.SelectedItem == null)
            {
                comboBox.SelectedIndex = 0; // 默认选择"未触发"
            }
            
            return comboBox;
        }
        
        /// <summary>
        /// 创建保本单元格模板
        /// </summary>
        private DataTemplate CreateBreakEvenCellTemplate()
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            factory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            
            // 价格文本
            var priceText = new FrameworkElementFactory(typeof(TextBlock));
            priceText.SetBinding(TextBlock.TextProperty, new Binding("BreakEvenDisplay"));
            priceText.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 5, 0));
            factory.AppendChild(priceText);
            
            // 状态图标
            var statusIcon = new FrameworkElementFactory(typeof(TextBlock));
            statusIcon.SetBinding(TextBlock.TextProperty, new Binding("BreakEvenStatusDisplay"));
            statusIcon.SetBinding(TextBlock.ForegroundProperty, new Binding("BreakEvenStatusColor"));
            statusIcon.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            statusIcon.SetValue(TextBlock.FontSizeProperty, 14.0);
            factory.AppendChild(statusIcon);
            
            template.VisualTree = factory;
            return template;
        }

        /// <summary>
        /// 创建推仓单元格模板
        /// </summary>
        private DataTemplate CreateAddPositionCellTemplate(int tierIndex)
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            factory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            
            // 价格文本
            var priceText = new FrameworkElementFactory(typeof(TextBlock));
            priceText.SetBinding(TextBlock.TextProperty, new Binding($"AddPositionTier{tierIndex}Display"));
            priceText.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 5, 0));
            factory.AppendChild(priceText);
            
            // 状态图标
            var statusIcon = new FrameworkElementFactory(typeof(TextBlock));
            statusIcon.SetBinding(TextBlock.TextProperty, new Binding($"AddPositionTier{tierIndex}Status"));
            statusIcon.SetBinding(TextBlock.ForegroundProperty, new Binding($"AddPositionTier{tierIndex}StatusColor"));
            statusIcon.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            statusIcon.SetValue(TextBlock.FontSizeProperty, 14.0);
            factory.AppendChild(statusIcon);
            
            template.VisualTree = factory;
            return template;
        }

        /// <summary>
        /// 创建止盈单元格模板
        /// </summary>
        private DataTemplate CreateProfitProtectionCellTemplate(int tierIndex)
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            factory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            
            // 价格文本
            var priceText = new FrameworkElementFactory(typeof(TextBlock));
            priceText.SetBinding(TextBlock.TextProperty, new Binding($"ProfitProtectionTier{tierIndex}Display"));
            priceText.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 5, 0));
            factory.AppendChild(priceText);
            
            // 状态图标
            var statusIcon = new FrameworkElementFactory(typeof(TextBlock));
            statusIcon.SetBinding(TextBlock.TextProperty, new Binding($"ProfitProtectionTier{tierIndex}Status"));
            statusIcon.SetBinding(TextBlock.ForegroundProperty, new Binding($"ProfitProtectionTier{tierIndex}StatusColor"));
            statusIcon.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            statusIcon.SetValue(TextBlock.FontSizeProperty, 14.0);
            factory.AppendChild(statusIcon);
            
            template.VisualTree = factory;
            return template;
        }

        /// <summary>
        /// 创建列定义
        /// </summary>
        private void CreateColumns()
        {
            try
            {
                _dataGrid.Columns.Clear();
                
                // 固定列：合约符号
                var symbolColumn = new DataGridTextColumn
                {
                    Header = "合约",
                    Binding = new Binding("Symbol"),
                    Width = new DataGridLength(100)
                };
                _dataGrid.Columns.Add(symbolColumn);
                
                // 固定列：未实现盈亏
                var pnlColumn = new DataGridTextColumn
                {
                    Header = "盈亏",
                    Binding = new Binding("PnlText"),
                    Width = new DataGridLength(80)
                };
                _dataGrid.Columns.Add(pnlColumn);
                
                // 固定列：保本（带状态图标）
                var breakEvenColumn = new DataGridTemplateColumn
                {
                    Header = "保本",
                    Width = new DataGridLength(100),
                    CellTemplate = CreateBreakEvenCellTemplate()
                };
                _dataGrid.Columns.Add(breakEvenColumn);
                
                // 动态列：推仓阶梯（根据数据决定列数，带状态图标）
                var maxAddPositionTiers = GetMaxAddPositionTiers();
                for (int i = 0; i < maxAddPositionTiers; i++)
                {
                    var addPositionColumn = new DataGridTemplateColumn
                    {
                        Header = $"推仓{i + 1}",
                        Width = new DataGridLength(100),
                        CellTemplate = CreateAddPositionCellTemplate(i)
                    };
                    _dataGrid.Columns.Add(addPositionColumn);
                }
                
                // 动态列：止盈阶梯（根据数据决定列数，带状态图标）
                var maxProfitProtectionTiers = GetMaxProfitProtectionTiers();
                for (int i = 0; i < maxProfitProtectionTiers; i++)
                {
                    var profitColumn = new DataGridTemplateColumn
                    {
                        Header = $"止盈{i + 1}",
                        Width = new DataGridLength(120),
                        CellTemplate = CreateProfitProtectionCellTemplate(i)
                    };
                    _dataGrid.Columns.Add(profitColumn);
                }
                
                _logger.LogDebug($"列结构创建完成：推仓 {maxAddPositionTiers} 列，止盈 {maxProfitProtectionTiers} 列");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建列定义时发生异常");
            }
        }
        
        /// <summary>
        /// 获取最大推仓阶梯数
        /// </summary>
        private int GetMaxAddPositionTiers()
        {
            // 🎯 修复：优先使用记录的阶梯数，确保与基础配置保持一致
            if (_lastAddPositionTierCount > 0)
            {
                return _lastAddPositionTierCount;
            }
            
            // 如果没有记录，从合约数据中计算
            if (_dataModel.ContractMonitors.Count == 0) return 4; // 默认4个阶梯
            
            var maxTiers = _dataModel.ContractMonitors
                .SelectMany(c => c.TriggerConditions.Where(tc => tc.Type == TriggerConditionType.AddPosition))
                .Select(tc => tc.TierIndex ?? 0)
                .DefaultIfEmpty(0)
                .Max();
                
            return Math.Max(maxTiers, 4); // 至少4个阶梯
        }
        
        /// <summary>
        /// 获取最大止盈阶梯数
        /// </summary>
        private int GetMaxProfitProtectionTiers()
        {
            // 🎯 修复：优先使用记录的阶梯数，确保与基础配置保持一致
            if (_lastProfitProtectionTierCount > 0)
            {
                return _lastProfitProtectionTierCount;
            }
            
            // 如果没有记录，从合约数据中计算
            if (_dataModel.ContractMonitors.Count == 0) return 3; // 默认3个阶梯
            
            var maxTiers = _dataModel.ContractMonitors
                .SelectMany(c => c.TriggerConditions.Where(tc => tc.Type == TriggerConditionType.ProfitProtection))
                .Select(tc => tc.TierIndex ?? 0)
                .DefaultIfEmpty(0)
                .Max();
                
            return Math.Max(maxTiers, 3); // 至少3个阶梯
        }
        
        /// <summary>
        /// 刷新列结构（当数据变化时调用）
        /// </summary>
        public void RefreshColumns()
        {
            try
            {
                // 保存当前选中项
                var selectedItem = _dataGrid.SelectedItem;
                
                // 重新创建列结构
                CreateColumns();
                
                // 恢复选中项
                if (selectedItem != null)
                {
                    _dataGrid.SelectedItem = selectedItem;
                }
                
                _logger.LogInformation("列结构已刷新");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新列结构时发生异常");
            }
        }
        
        /// <summary>
        /// 强制刷新列结构（外部调用）
        /// </summary>
        public void ForceRefreshColumns()
        {
            try
            {
                // 重新计算当前应有的列数
                CheckAndRefreshColumnsIfNeeded();
                
                _logger.LogInformation("强制刷新列结构完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "强制刷新列结构时发生异常");
            }
        }

        /// <summary>
        /// 🎯 新增：更新基础配置阶梯数（外部调用）
        /// </summary>
        /// <param name="addPositionTiers">推仓阶梯数</param>
        /// <param name="profitProtectionTiers">止盈阶梯数</param>
        public void UpdateBaseConfigurationTiers(int addPositionTiers, int profitProtectionTiers)
        {
            try
            {
                _logger.LogInformation($"更新基础配置阶梯数：推仓{addPositionTiers}，止盈{profitProtectionTiers}");
                
                // 更新记录的阶梯数
                _lastAddPositionTierCount = Math.Max(addPositionTiers, 1);
                _lastProfitProtectionTierCount = Math.Max(profitProtectionTiers, 1);
                
                // 立即刷新列结构
                RefreshColumns();
                
                _logger.LogInformation("基础配置阶梯数更新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新基础配置阶梯数时发生异常");
            }
        }
        
        /// <summary>
        /// 同步合约配置到基础配置
        /// </summary>
        /// <param name="baseAddPositionTiers">基础配置推仓阶梯数</param>
        /// <param name="baseProfitProtectionTiers">基础配置止盈阶梯数</param>
        public void SyncContractConfigsToBaseConfig(int baseAddPositionTiers, int baseProfitProtectionTiers)
        {
            try
            {
                _logger.LogInformation($"开始同步合约配置：推仓阶梯 {baseAddPositionTiers}，止盈阶梯 {baseProfitProtectionTiers}");
                
                foreach (var contract in _dataModel.ContractMonitors)
                {
                    SyncSingleContractConfig(contract, baseAddPositionTiers, baseProfitProtectionTiers);
                }
                
                // 更新记录的数量
                _lastAddPositionTierCount = baseAddPositionTiers;
                _lastProfitProtectionTierCount = baseProfitProtectionTiers;
                
                // 刷新列结构
                RefreshColumns();
                
                _logger.LogInformation("合约配置同步完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步合约配置时发生异常");
            }
        }
        
        /// <summary>
        /// 同步单个合约配置
        /// </summary>
        private void SyncSingleContractConfig(ContractMonitorModel contract, int targetAddPositionTiers, int targetProfitProtectionTiers)
        {
            try
            {
                // 同步推仓阶梯
                var currentAddPositionTiers = contract.TriggerConditions.Where(c => c.Type == TriggerConditionType.AddPosition).Count();
                if (currentAddPositionTiers < targetAddPositionTiers)
                {
                    // 需要增加推仓阶梯
                    for (int i = currentAddPositionTiers; i < targetAddPositionTiers; i++)
                    {
                        // 🔧 修复：TierIndex从1开始，需要+1
                        var tierIndex = i + 1;
                        var newCondition = new TriggerConditionModel
                        {
                            Id = GenerateConditionId(contract),
                            Type = TriggerConditionType.AddPosition,
                            TierIndex = tierIndex,
                            TriggerPrice = 0, // 默认值，用户需要手动设置
                            Status = TriggerExecutionStatus.NotTriggered,
                            Description = $"推仓阶梯 {tierIndex}"
                        };
                        contract.TriggerConditions.Add(newCondition);
                    }
                    _logger.LogDebug($"为 {contract.Symbol} 增加了 {targetAddPositionTiers - currentAddPositionTiers} 个推仓阶梯");
                }
                else if (currentAddPositionTiers > targetAddPositionTiers)
                {
                    // 需要减少推仓阶梯
                    // 🔧 修复：TierIndex从1开始，移除时需要考虑+1
                    var toRemove = contract.TriggerConditions
                        .Where(c => c.Type == TriggerConditionType.AddPosition && c.TierIndex > targetAddPositionTiers)
                        .ToList();
                    foreach (var condition in toRemove)
                    {
                        contract.TriggerConditions.Remove(condition);
                    }
                    _logger.LogDebug($"为 {contract.Symbol} 移除了 {currentAddPositionTiers - targetAddPositionTiers} 个推仓阶梯");
                }
                
                // 同步止盈阶梯
                var currentProfitProtectionTiers = contract.TriggerConditions.Where(c => c.Type == TriggerConditionType.ProfitProtection).Count();
                if (currentProfitProtectionTiers < targetProfitProtectionTiers)
                {
                    // 需要增加止盈阶梯
                    for (int i = currentProfitProtectionTiers; i < targetProfitProtectionTiers; i++)
                    {
                        // 🔧 修复：TierIndex从1开始，需要+1
                        var tierIndex = i + 1;
                        var newCondition = new TriggerConditionModel
                        {
                            Id = GenerateConditionId(contract),
                            Type = TriggerConditionType.ProfitProtection,
                            TierIndex = tierIndex,
                            TriggerPrice = 0, // 默认值，用户需要手动设置
                            KeepValue = 0, // 默认值，用户需要手动设置
                            Status = TriggerExecutionStatus.NotTriggered,
                            Description = $"止盈阶梯 {tierIndex}"
                        };
                        contract.TriggerConditions.Add(newCondition);
                    }
                    _logger.LogDebug($"为 {contract.Symbol} 增加了 {targetProfitProtectionTiers - currentProfitProtectionTiers} 个止盈阶梯");
                }
                else if (currentProfitProtectionTiers > targetProfitProtectionTiers)
                {
                    // 需要减少止盈阶梯
                    // 🔧 修复：TierIndex从1开始，移除时需要考虑+1
                    var toRemove = contract.TriggerConditions
                        .Where(c => c.Type == TriggerConditionType.ProfitProtection && c.TierIndex > targetProfitProtectionTiers)
                        .ToList();
                    foreach (var condition in toRemove)
                    {
                        contract.TriggerConditions.Remove(condition);
                    }
                    _logger.LogDebug($"为 {contract.Symbol} 移除了 {currentProfitProtectionTiers - targetProfitProtectionTiers} 个止盈阶梯");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"同步合约 {contract.Symbol} 配置时发生异常");
            }
        }
        
        /// <summary>
        /// 生成触发条件ID
        /// </summary>
        private int GenerateConditionId(ContractMonitorModel contract)
        {
            if (contract.TriggerConditions.Count == 0)
                return 1;
            return contract.TriggerConditions.Max(c => c.Id) + 1;
        }
        
        /// <summary>
        /// 添加新合约时自动配置阶梯
        /// </summary>
        public void AddNewContractWithCurrentConfig(ContractMonitorModel newContract)
        {
            try
            {
                // 根据当前的阶梯数量配置新合约
                SyncSingleContractConfig(newContract, _lastAddPositionTierCount, _lastProfitProtectionTierCount);
                
                // 订阅新合约的变化事件
                SubscribeToContractChanges(newContract);
                
                _logger.LogInformation($"新合约 {newContract.Symbol} 已配置完成，推仓阶梯: {_lastAddPositionTierCount}，止盈阶梯: {_lastProfitProtectionTierCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"配置新合约 {newContract.Symbol} 时发生异常");
            }
        }
        
        /// <summary>
        /// 绑定数据（线程安全版本）
        /// </summary>
        private void BindData()
        {
            // 🔧 修复：确保ItemsSource绑定在UI线程中执行
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            {
                _dataGrid.ItemsSource = _dataModel.ContractMonitors;
            }
            else
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _dataGrid.ItemsSource = _dataModel.ContractMonitors;
                });
            }
        }
        
        /// <summary>
        /// 刷新数据
        /// </summary>
        public void RefreshData()
        {
            try
            {
                _dataGrid.Items.Refresh();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "刷新合约数据网格时发生异常");
            }
        }
        
        /// <summary>
        /// 获取数据网格控件
        /// </summary>
        public DataGrid GetDataGrid() => _dataGrid;
        
        /// <summary>
        /// 配置变化事件
        /// </summary>
        public event EventHandler<ConfigurationChangedEventArgs> OnConfigurationChanged;
        
        #region INotifyPropertyChanged 实现
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
    
    /// <summary>
    /// 配置变化事件参数
    /// </summary>
    public class ConfigurationChangedEventArgs : EventArgs
    {
        public int AddPositionTierCount { get; set; }
        public int ProfitProtectionTierCount { get; set; }
    }
} 