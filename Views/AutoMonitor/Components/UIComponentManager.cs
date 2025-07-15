using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views.AutoMonitor.Components
{
    /// <summary>
    /// UI组件管理器
    /// 负责创建和管理各种UI组件
    /// </summary>
    public class UIComponentManager
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly UIStateModel _uiStateModel;
        private readonly ILogger _logger;
        
        private StatusDisplayComponent _statusDisplay;
        private ContractDataGridComponent _contractDataGrid;
        private ContractConfigurationOverviewComponent _configurationOverview;
        private ControlPanelComponent _controlPanel;
        private LogDisplayComponent _logDisplay;
        
        private bool _isInitialized = false;
        
        public UIComponentManager(
            AutoMonitorDataModel dataModel,
            UIStateModel uiStateModel,
            ILogger logger)
        {
            _dataModel = dataModel ?? throw new ArgumentNullException(nameof(dataModel));
            _uiStateModel = uiStateModel ?? throw new ArgumentNullException(nameof(uiStateModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        #region 公共属性
        
        public StatusDisplayComponent StatusDisplay => _statusDisplay;
        public ContractDataGridComponent ContractDataGrid => _contractDataGrid;
        public ContractConfigurationOverviewComponent ConfigurationOverview => _configurationOverview;
        public ControlPanelComponent ControlPanel => _controlPanel;
        public LogDisplayComponent LogDisplay => _logDisplay;
        
        public bool IsInitialized => _isInitialized;
        
        #endregion
        
        #region 初始化方法
        
        /// <summary>
        /// 初始化所有UI组件
        /// </summary>
        /// <param name="parentWindow">父窗口</param>
        public void InitializeComponents(Window parentWindow)
        {
            try
            {
                _logger.LogInformation("开始初始化UI组件");
                
                // 创建各个组件 - 使用正确的构造函数参数
                _statusDisplay = new StatusDisplayComponent(_dataModel, _logger);
                _contractDataGrid = new ContractDataGridComponent(_dataModel, _logger);
                _configurationOverview = new ContractConfigurationOverviewComponent(_dataModel, _logger);
                _controlPanel = new ControlPanelComponent(_dataModel, _logger);
                _logDisplay = new LogDisplayComponent(_dataModel, _logger);
                
                // 设置组件间的关联
                SetupComponentRelationships();
                
                // 🎯 新增：设置配置变化监听
                SetupConfigurationChangeHandling();
                
                _isInitialized = true;
                _logger.LogInformation("UI组件初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化UI组件时发生异常");
                throw;
            }
        }
        
        /// <summary>
        /// 设置组件间的关联关系
        /// </summary>
        private void SetupComponentRelationships()
        {
            try
            {
                // 设置控制面板的事件处理
                _controlPanel.SetButtonClickHandlers(
                    (s, e) => OnToggleMonitoringRequested(s, EventArgs.Empty),
                    (s, e) => OnRefreshDataRequested(s, EventArgs.Empty),
                    (s, e) => OnClearLogRequested(s, EventArgs.Empty),
                    (s, e) => OnConfigRequested(s, EventArgs.Empty)
                );
                
                _logger.LogDebug("组件关联关系设置完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置组件关联关系时发生异常");
                throw;
            }
        }
        
        /// <summary>
        /// 🎯 新增：设置配置变化处理
        /// </summary>
        private void SetupConfigurationChangeHandling()
        {
            try
            {
                // 监听合约数据网格的配置变化
                _contractDataGrid.OnConfigurationChanged += OnContractDataGridConfigurationChanged;
                
                _logger.LogDebug("配置变化处理设置完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置配置变化处理时发生异常");
                throw;
            }
        }
        
        /// <summary>
        /// 处理合约数据网格配置变化
        /// </summary>
        private void OnContractDataGridConfigurationChanged(object sender, ConfigurationChangedEventArgs e)
        {
            try
            {
                _logger.LogInformation($"检测到配置变化：推仓阶梯 {e.AddPositionTierCount}，止盈阶梯 {e.ProfitProtectionTierCount}");
                
                // 更新配置概览组件
                _configurationOverview?.UpdateOverview();
                
                // 触发配置变化事件
                OnConfigurationStructureChanged?.Invoke(this, e);
                
                _logger.LogDebug("配置变化处理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理配置变化时发生异常");
            }
        }
        
        #endregion
        
        #region 布局管理方法
        
        /// <summary>
        /// 创建主布局
        /// </summary>
        /// <returns>主布局控件</returns>
        public Grid CreateMainLayout()
        {
            try
            {
                if (!_isInitialized)
                {
                    throw new InvalidOperationException("组件未初始化");
                }
                
                var mainGrid = new Grid
                {
                    Margin = new Thickness(10)
                };
                
                // 定义行和列
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) }); // 状态显示
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) }); // 控制面板
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) }); // 配置概览
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) }); // 数据表格
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 日志显示
                
                // 添加组件到网格
                var statusPanel = CreateStatusPanel();
                Grid.SetRow(statusPanel, 0);
                mainGrid.Children.Add(statusPanel);
                
                var controlPanel = _controlPanel.GetControlPanel();
                Grid.SetRow(controlPanel, 1);
                mainGrid.Children.Add(controlPanel);
                
                // 🎯 新增：配置概览组件
                var configurationOverview = _configurationOverview.GetOverviewGrid();
                configurationOverview.Margin = new Thickness(0, 5, 0, 5);
                Grid.SetRow(configurationOverview, 2);
                mainGrid.Children.Add(configurationOverview);
                
                var dataGrid = _contractDataGrid.GetDataGrid();
                dataGrid.Margin = new Thickness(0, 5, 0, 5);
                Grid.SetRow(dataGrid, 3);
                mainGrid.Children.Add(dataGrid);
                
                var logPanel = CreateLogPanel();
                Grid.SetRow(logPanel, 4);
                mainGrid.Children.Add(logPanel);
                
                _logger.LogDebug("主布局创建完成");
                return mainGrid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建主布局时发生异常");
                throw;
            }
        }
        
        /// <summary>
        /// 创建状态面板
        /// </summary>
        private Panel CreateStatusPanel()
        {
            var statusPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            statusPanel.Children.Add(_statusDisplay.GetStatusTextBlock());
            statusPanel.Children.Add(new Separator { Width = 20, Visibility = Visibility.Hidden });
            statusPanel.Children.Add(_statusDisplay.GetTimeTextBlock());
            statusPanel.Children.Add(new Separator { Width = 20, Visibility = Visibility.Hidden });
            statusPanel.Children.Add(_statusDisplay.GetStatsTextBlock());
            
            return statusPanel;
        }
        
        /// <summary>
        /// 创建日志面板
        /// </summary>
        private Panel CreateLogPanel()
        {
            var logPanel = new DockPanel
            {
                Margin = new Thickness(0, 5, 0, 0)
            };
            
            // 日志标题
            var logTitle = new Label
            {
                Content = "运行日志",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            DockPanel.SetDock(logTitle, Dock.Top);
            logPanel.Children.Add(logTitle);
            
            // 日志显示
            var logListBox = _logDisplay.GetLogListBox();
            logListBox.Margin = new Thickness(0);
            logPanel.Children.Add(logListBox);
            
            return logPanel;
        }
        
        #endregion
        
        #region 组件事件处理
        
        private void OnToggleMonitoringRequested(object sender, EventArgs e)
        {
            ToggleMonitoringRequested?.Invoke(this, EventArgs.Empty);
        }
        
        private void OnRefreshDataRequested(object sender, EventArgs e)
        {
            RefreshDataRequested?.Invoke(this, EventArgs.Empty);
        }
        
        private void OnClearLogRequested(object sender, EventArgs e)
        {
            ClearLogRequested?.Invoke(this, EventArgs.Empty);
        }
        
        private void OnConfigRequested(object sender, EventArgs e)
        {
            ConfigRequested?.Invoke(this, EventArgs.Empty);
        }
        
        private void OnEditRequested(object sender, ContractEditEventArgs e)
        {
            EditRequested?.Invoke(this, e);
        }
        
        private void OnDeleteRequested(object sender, ContractDeleteEventArgs e)
        {
            DeleteRequested?.Invoke(this, e);
        }
        
        private void OnAutoScrollToggled(object sender, AutoScrollEventArgs e)
        {
            AutoScrollToggled?.Invoke(this, e);
        }
        
        private void OnExportLogRequested(object sender, EventArgs e)
        {
            ExportLogRequested?.Invoke(this, EventArgs.Empty);
        }
        
        #endregion
        
        #region 更新方法
        
        /// <summary>
        /// 更新所有组件
        /// </summary>
        public void UpdateAllComponents()
        {
            try
            {
                if (!_isInitialized) return;
                
                _statusDisplay?.UpdateStatus();
                _contractDataGrid?.RefreshData();
                _configurationOverview?.UpdateOverview();
                _controlPanel?.UpdateButtonStates();
                _logDisplay?.RefreshDisplay();
                
                _logger.LogTrace("所有组件更新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新组件时发生异常");
            }
        }
        
        /// <summary>
        /// 🎯 新增：处理基础配置变化
        /// </summary>
        /// <param name="newAddPositionTiers">新的推仓阶梯数</param>
        /// <param name="newProfitProtectionTiers">新的止盈阶梯数</param>
        public void HandleBaseConfigurationChange(int newAddPositionTiers, int newProfitProtectionTiers)
        {
            try
            {
                _logger.LogInformation($"🔄 处理基础配置变化：推仓阶梯 {newAddPositionTiers}，止盈阶梯 {newProfitProtectionTiers}");
                
                // 🎯 关键修复：先更新基础配置阶梯数，确保列结构正确
                _contractDataGrid?.UpdateBaseConfigurationTiers(newAddPositionTiers, newProfitProtectionTiers);
                
                // 同步合约配置到新的基础配置
                _contractDataGrid?.SyncContractConfigsToBaseConfig(newAddPositionTiers, newProfitProtectionTiers);
                
                // 更新配置概览
                _configurationOverview?.UpdateOverview();
                
                // 触发基础配置变化事件
                OnBaseConfigurationChanged?.Invoke(this, new BaseConfigurationChangedEventArgs
                {
                    AddPositionTierCount = newAddPositionTiers,
                    ProfitProtectionTierCount = newProfitProtectionTiers
                });
                
                _logger.LogInformation("✅ 基础配置变化处理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 处理基础配置变化时发生异常");
                throw;
            }
        }
        
        /// <summary>
        /// 🎯 新增：强制刷新列结构
        /// </summary>
        public void ForceRefreshColumnStructure()
        {
            try
            {
                _logger.LogInformation("🔄 强制刷新列结构");
                
                _contractDataGrid?.ForceRefreshColumns();
                _configurationOverview?.UpdateOverview();
                
                _logger.LogInformation("✅ 列结构强制刷新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 强制刷新列结构时发生异常");
            }
        }
        
        /// <summary>
        /// 🎯 新增：添加新合约时的配置处理
        /// </summary>
        /// <param name="newContract">新合约</param>
        public void HandleNewContractAdded(ContractMonitorModel newContract)
        {
            try
            {
                _logger.LogInformation($"🆕 处理新合约添加：{newContract.Symbol}");
                
                // 为新合约配置当前的阶梯结构
                _contractDataGrid?.AddNewContractWithCurrentConfig(newContract);
                
                // 更新配置概览
                _configurationOverview?.UpdateOverview();
                
                _logger.LogInformation($"✅ 新合约 {newContract.Symbol} 配置完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 处理新合约 {newContract.Symbol} 时发生异常");
            }
        }
        
        /// <summary>
        /// 🎯 新增：批量同步所有合约配置
        /// </summary>
        /// <param name="baseAddPositionTiers">基础推仓阶梯数</param>
        /// <param name="baseProfitProtectionTiers">基础止盈阶梯数</param>
        public void BatchSyncAllContractConfigs(int baseAddPositionTiers, int baseProfitProtectionTiers)
        {
            try
            {
                _logger.LogInformation($"🔄 批量同步所有合约配置：推仓阶梯 {baseAddPositionTiers}，止盈阶梯 {baseProfitProtectionTiers}");
                
                var contractCount = _dataModel.ContractMonitors.Count;
                if (contractCount == 0)
                {
                    _logger.LogInformation("⚠️ 没有合约需要同步");
                    return;
                }
                
                // 执行同步
                HandleBaseConfigurationChange(baseAddPositionTiers, baseProfitProtectionTiers);
                
                _logger.LogInformation($"✅ 批量同步完成，共处理 {contractCount} 个合约");
                
                // 显示用户提示
                MessageBox.Show(
                    $"✅ 配置同步完成！\n\n📊 同步结果：\n• 处理合约数量：{contractCount} 个\n• 推仓阶梯：{baseAddPositionTiers} 个\n• 止盈阶梯：{baseProfitProtectionTiers} 个\n\n💡 表格列结构已自动调整",
                    "配置同步",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 批量同步合约配置时发生异常");
                MessageBox.Show($"❌ 配置同步失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        #endregion
        
        #region 样式方法
        
        /// <summary>
        /// 应用主题
        /// </summary>
        /// <param name="theme">主题名称</param>
        public void ApplyTheme(string theme)
        {
            try
            {
                if (!_isInitialized) return;
                
                _logger.LogDebug($"主题 '{theme}' 应用完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"应用主题 '{theme}' 时发生异常");
            }
        }
        
        #endregion
        
        #region 清理方法
        
        /// <summary>
        /// 清理所有组件
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // 取消事件订阅
                if (_contractDataGrid != null)
                {
                    _contractDataGrid.OnConfigurationChanged -= OnContractDataGridConfigurationChanged;
                }
                
                _isInitialized = false;
                _logger.LogDebug("UI组件清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理UI组件时发生异常");
            }
        }
        
        #endregion
        
        #region 事件定义
        
        public event EventHandler ToggleMonitoringRequested;
        public event EventHandler RefreshDataRequested;
        public event EventHandler ClearLogRequested;
        public event EventHandler ConfigRequested;
        public event EventHandler<ContractEditEventArgs> EditRequested;
        public event EventHandler<ContractDeleteEventArgs> DeleteRequested;
        public event EventHandler<AutoScrollEventArgs> AutoScrollToggled;
        public event EventHandler ExportLogRequested;
        public event EventHandler<ContractConfigurationChangedEventArgs> ContractConfigurationChanged;
        
        /// <summary>
        /// 🎯 新增：配置结构变化事件
        /// </summary>
        public event EventHandler<ConfigurationChangedEventArgs> OnConfigurationStructureChanged;
        
        /// <summary>
        /// 🎯 新增：基础配置变化事件
        /// </summary>
        public event EventHandler<BaseConfigurationChangedEventArgs> OnBaseConfigurationChanged;
        
        #endregion
        
        #region 编辑功能
        
        /// <summary>
        /// 编辑合约配置
        /// 只允许在停止监控状态时编辑
        /// </summary>
        /// <param name="contract">要编辑的合约</param>
        /// <returns>是否成功编辑</returns>
        public bool EditContractConfiguration(ContractMonitorModel contract)
        {
            try
            {
                // 检查是否可以编辑
                if (!CanEditConfiguration())
                {
                    var message = "无法编辑配置：只能在停止盯盘时修改合约配置";
                    _logger.LogWarning(message);
                    MessageBox.Show(message, "编辑限制", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                
                _logger.LogInformation($"🔧 开始编辑合约配置: {contract.ContractKey}");
                
                // 显示编辑对话框
                var editDialog = CreateContractEditDialog(contract);
                if (editDialog.ShowDialog() == true)
                {
                    _logger.LogInformation($"✅ 合约配置已更新: {contract.ContractKey}");
                    
                    // 触发配置已更改事件
                    OnContractConfigurationChanged(contract);
                    
                    // 更新UI显示
                    UpdateAllComponents();
                    
                    return true;
                }
                
                _logger.LogDebug($"❌ 用户取消了编辑: {contract.ContractKey}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 编辑合约配置失败: {contract?.ContractKey}");
                MessageBox.Show($"编辑配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        
        /// <summary>
        /// 检查是否可以编辑配置
        /// </summary>
        /// <returns>true如果可以编辑</returns>
        public bool CanEditConfiguration()
        {
            // 检查监控状态 - 只有在停止状态时才能编辑
            var isRunning = _dataModel.MonitorStatus == "运行中";
            var reason = isRunning ? "监控运行中" : "允许编辑";
            
            _logger.LogDebug($"📋 编辑状态检查: {reason} (监控状态: {_dataModel.MonitorStatus})");
            
            return !isRunning;
        }
        
        /// <summary>
        /// 批量编辑多个合约配置
        /// </summary>
        /// <param name="contracts">要编辑的合约列表</param>
        /// <returns>成功编辑的数量</returns>
        public int BatchEditContractConfigurations(System.Collections.Generic.List<ContractMonitorModel> contracts)
        {
            try
            {
                if (!CanEditConfiguration())
                {
                    var editMessage = "无法批量编辑：只能在停止盯盘时修改合约配置";
                    _logger.LogWarning(editMessage);
                    MessageBox.Show(editMessage, "编辑限制", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return 0;
                }
                
                if (contracts == null || contracts.Count == 0)
                {
                    _logger.LogWarning("⚠️ 没有选中要编辑的合约");
                    MessageBox.Show("请先选择要编辑的合约", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return 0;
                }
                
                _logger.LogInformation($"🔧 开始批量编辑 {contracts.Count} 个合约配置");
                
                var successCount = 0;
                var failedContracts = new System.Collections.Generic.List<string>();
                
                foreach (var contract in contracts)
                {
                    try
                    {
                        if (EditContractConfiguration(contract))
                        {
                            successCount++;
                        }
                        else
                        {
                            failedContracts.Add(contract.ContractKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"❌ 批量编辑失败: {contract.ContractKey}");
                        failedContracts.Add(contract.ContractKey);
                    }
                }
                
                var message = $"批量编辑完成 - 成功: {successCount}个";
                if (failedContracts.Count > 0)
                {
                    message += $", 失败: {failedContracts.Count}个";
                }
                
                _logger.LogInformation($"✅ {message}");
                MessageBox.Show(message, "批量编辑结果", MessageBoxButton.OK, MessageBoxImage.Information);
                
                return successCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 批量编辑合约配置失败");
                MessageBox.Show($"批量编辑失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return 0;
            }
        }
        
        /// <summary>
        /// 创建合约编辑对话框
        /// </summary>
        /// <param name="contract">要编辑的合约</param>
        /// <returns>编辑对话框</returns>
        private Window CreateContractEditDialog(ContractMonitorModel contract)
        {
            // 创建简单的编辑对话框
            var dialog = new Window
            {
                Title = $"编辑合约配置 - {contract.ContractKey}",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize,
                ShowInTaskbar = false
            };
            
            var mainGrid = new Grid { Margin = new Thickness(10) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // 标题
            var title = new Label
            {
                Content = $"合约: {contract.Symbol} {contract.PositionSide}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(title, 0);
            mainGrid.Children.Add(title);
            
            // 编辑内容
            var editContent = CreateContractEditPanel(contract);
            Grid.SetRow(editContent, 1);
            mainGrid.Children.Add(editContent);
            
            // 按钮
            var buttonPanel = CreateEditDialogButtons(dialog, contract);
            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);
            
            dialog.Content = mainGrid;
            return dialog;
        }
        
        /// <summary>
        /// 创建合约编辑面板
        /// </summary>
        /// <param name="contract">合约模型</param>
        /// <returns>编辑面板</returns>
        private FrameworkElement CreateContractEditPanel(ContractMonitorModel contract)
        {
            var panel = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            
            var stackPanel = new StackPanel { Margin = new Thickness(5) };
            
            // 基本信息
            stackPanel.Children.Add(CreateSectionTitle("基本信息"));
            stackPanel.Children.Add(CreateCheckBox("启用监控", contract.IsEnabled, 
                (isChecked) => contract.IsEnabled = isChecked));
            stackPanel.Children.Add(CreateTextBox("当前价格", contract.CurrentPrice.ToString("F4"), true));
            stackPanel.Children.Add(CreateTextBox("持仓大小", contract.PositionSize.ToString("F4"), true));
            stackPanel.Children.Add(CreateTextBox("浮盈浮亏", contract.UnrealizedPnl.ToString("F2"), true));
            
            // 触发条件
            stackPanel.Children.Add(CreateSectionTitle("触发条件"));
            var conditionsPanel = CreateTriggerConditionsEditPanel(contract);
            stackPanel.Children.Add(conditionsPanel);
            
            panel.Content = stackPanel;
            return panel;
        }
        
        /// <summary>
        /// 创建触发条件编辑面板
        /// </summary>
        /// <param name="contract">合约模型</param>
        /// <returns>触发条件编辑面板</returns>
        private Panel CreateTriggerConditionsEditPanel(ContractMonitorModel contract)
        {
            var panel = new StackPanel();
            
            foreach (var condition in contract.TriggerConditions)
            {
                var conditionPanel = CreateSingleConditionEditPanel(condition);
                panel.Children.Add(conditionPanel);
            }
            
            return panel;
        }
        
        /// <summary>
        /// 创建单个条件编辑面板
        /// </summary>
        /// <param name="condition">触发条件</param>
        /// <returns>条件编辑面板</returns>
        private FrameworkElement CreateSingleConditionEditPanel(TriggerConditionModel condition)
        {
            var border = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 5, 0, 5)
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            var row = 0;
            
            // 条件类型
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var typeLabel = new Label { Content = "条件类型:", FontWeight = FontWeights.Bold };
            var typeValue = new TextBlock { Text = condition.TypeText, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(typeLabel, row);
            Grid.SetColumn(typeLabel, 0);
            Grid.SetRow(typeValue, row);
            Grid.SetColumn(typeValue, 1);
            grid.Children.Add(typeLabel);
            grid.Children.Add(typeValue);
            row++;
            
            // 触发价格
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var priceLabel = new Label { Content = "触发价格:", FontWeight = FontWeights.Bold };
            var priceBox = new TextBox
            {
                Text = condition.TriggerPrice.ToString("F4"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            priceBox.TextChanged += (s, e) =>
            {
                if (decimal.TryParse(priceBox.Text, out var price))
                {
                    condition.TriggerPrice = price;
                }
            };
            Grid.SetRow(priceLabel, row);
            Grid.SetColumn(priceLabel, 0);
            Grid.SetRow(priceBox, row);
            Grid.SetColumn(priceBox, 1);
            grid.Children.Add(priceLabel);
            grid.Children.Add(priceBox);
            row++;
            
            // 如果是止盈条件，显示保护金额
            if (condition.Type == TriggerConditionType.ProfitProtection)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var keepLabel = new Label { Content = "保护金额:", FontWeight = FontWeights.Bold };
                var keepBox = new TextBox
                {
                    Text = condition.KeepValue.ToString("F2"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                keepBox.TextChanged += (s, e) =>
                {
                    if (decimal.TryParse(keepBox.Text, out var keep))
                    {
                        condition.KeepValue = keep;
                    }
                };
                Grid.SetRow(keepLabel, row);
                Grid.SetColumn(keepLabel, 0);
                Grid.SetRow(keepBox, row);
                Grid.SetColumn(keepBox, 1);
                grid.Children.Add(keepLabel);
                grid.Children.Add(keepBox);
                row++;
            }
            
            // 状态
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var statusLabel = new Label { Content = "状态:", FontWeight = FontWeights.Bold };
            var statusValue = new TextBlock { Text = condition.StatusText, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(statusLabel, row);
            Grid.SetColumn(statusLabel, 0);
            Grid.SetRow(statusValue, row);
            Grid.SetColumn(statusValue, 1);
            grid.Children.Add(statusLabel);
            grid.Children.Add(statusValue);
            
            border.Child = grid;
            return border;
        }
        
        /// <summary>
        /// 创建编辑对话框按钮
        /// </summary>
        /// <param name="dialog">对话框</param>
        /// <param name="contract">合约模型</param>
        /// <returns>按钮面板</returns>
        private Panel CreateEditDialogButtons(Window dialog, ContractMonitorModel contract)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };
            
            var saveButton = new Button
            {
                Content = "保存",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            saveButton.Click += (s, e) =>
            {
                dialog.DialogResult = true;
                dialog.Close();
            };
            
            var cancelButton = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30
            };
            cancelButton.Click += (s, e) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };
            
            panel.Children.Add(saveButton);
            panel.Children.Add(cancelButton);
            
            return panel;
        }
        
        /// <summary>
        /// 创建节标题
        /// </summary>
        /// <param name="title">标题</param>
        /// <returns>标题控件</returns>
        private Label CreateSectionTitle(string title)
        {
            return new Label
            {
                Content = title,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 5)
            };
        }
        
        /// <summary>
        /// 创建复选框
        /// </summary>
        /// <param name="label">标签</param>
        /// <param name="isChecked">是否选中</param>
        /// <param name="onChanged">变化回调</param>
        /// <returns>复选框</returns>
        private CheckBox CreateCheckBox(string label, bool isChecked, Action<bool> onChanged)
        {
            var checkBox = new CheckBox
            {
                Content = label,
                IsChecked = isChecked,
                Margin = new Thickness(0, 2, 0, 2)
            };
            checkBox.Checked += (s, e) => onChanged(true);
            checkBox.Unchecked += (s, e) => onChanged(false);
            return checkBox;
        }
        
        /// <summary>
        /// 创建文本框
        /// </summary>
        /// <param name="label">标签</param>
        /// <param name="text">文本</param>
        /// <param name="isReadOnly">是否只读</param>
        /// <returns>文本框面板</returns>
        private DockPanel CreateTextBox(string label, string text, bool isReadOnly = false)
        {
            var panel = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            
            var labelControl = new Label
            {
                Content = label,
                Width = 100,
                FontWeight = FontWeights.Bold
            };
            DockPanel.SetDock(labelControl, Dock.Left);
            
            var textBox = new TextBox
            {
                Text = text,
                IsReadOnly = isReadOnly,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            panel.Children.Add(labelControl);
            panel.Children.Add(textBox);
            
            return panel;
        }
        
        /// <summary>
        /// 配置已更改处理
        /// </summary>
        /// <param name="contract">合约模型</param>
        private void OnContractConfigurationChanged(ContractMonitorModel contract)
        {
            try
            {
                var args = new ContractConfigurationChangedEventArgs(contract);
                ContractConfigurationChanged?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理配置更改事件时发生异常");
            }
        }
        
        #endregion
    }
    
    #region 事件参数类
    
    public class ContractEditEventArgs : EventArgs
    {
        public ContractMonitorModel Contract { get; }
        
        public ContractEditEventArgs(ContractMonitorModel contract)
        {
            Contract = contract;
        }
    }
    
    public class ContractDeleteEventArgs : EventArgs
    {
        public ContractMonitorModel Contract { get; }
        
        public ContractDeleteEventArgs(ContractMonitorModel contract)
        {
            Contract = contract;
        }
    }
    
    public class AutoScrollEventArgs : EventArgs
    {
        public bool IsEnabled { get; }
        
        public AutoScrollEventArgs(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }
    }
    
    public class ContractConfigurationChangedEventArgs : EventArgs
    {
        public ContractMonitorModel Contract { get; }
        
        public ContractConfigurationChangedEventArgs(ContractMonitorModel contract)
        {
            Contract = contract;
        }
    }
    
    /// <summary>
    /// 🎯 新增：基础配置变化事件参数
    /// </summary>
    public class BaseConfigurationChangedEventArgs : EventArgs
    {
        public int AddPositionTierCount { get; set; }
        public int ProfitProtectionTierCount { get; set; }
    }
    
    #endregion
} 