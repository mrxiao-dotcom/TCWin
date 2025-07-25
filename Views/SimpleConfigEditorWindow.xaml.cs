using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.ViewModels;

namespace BinanceFuturesTrader.Views
{
    public partial class SimpleConfigEditorWindow : Window
    {
        private BaseConfigManager _configManager;
        private ObservableCollection<AutoMonitorConfig> _configs;
        private AutoMonitorConfig? _selectedConfig;
        private bool _isEditMode = false;
        private AutoMonitorConfig? _editingConfig;
        private bool _isNewConfig = false; // 🔧 新增：标识是否为新建配置
        
        // 风险金计算相关
        private decimal _currentRiskCapital = 0m;
        private readonly RiskCapitalService _riskCapitalService;
        
        // 🔧 移除手动获取方法，改用RiskCapitalService
        
        public SimpleConfigEditorWindow(MainViewModel? mainViewModel = null, BaseConfigManager? configManager = null)
        {
            InitializeComponent();
            
            // 🔧 修复：使用BaseConfigManager单例实例，确保全局配置统一
            _configManager = BaseConfigManager.Instance;
            _configs = new ObservableCollection<AutoMonitorConfig>();
            
            // 🔧 初始化RiskCapitalService
            try
            {
                if (mainViewModel != null)
                {
                    var logger = NullLogger<RiskCapitalService>.Instance;
                    _riskCapitalService = new RiskCapitalService(logger, mainViewModel);
                }
                else
                {
                    // 从Application.Current.MainWindow获取MainViewModel
                    if (Application.Current.MainWindow is MainWindow mainWindow && mainWindow.DataContext is MainViewModel vm)
                    {
                        var logger = NullLogger<RiskCapitalService>.Instance;
                        _riskCapitalService = new RiskCapitalService(logger, vm);
                    }
                    else
                    {
                        throw new InvalidOperationException("无法获取MainViewModel，风险金服务初始化失败");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化风险金服务失败：{ex.Message}\n将使用默认值。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                _riskCapitalService = null!;
            }
            
            LoadConfigs();
            SetupUI();
            
            // 🔧 自动加载账户权益和风险次数
            LoadSystemAccountInfo();
            
            // 🔧 添加窗口激活事件，确保每次显示时都刷新配置
            this.Activated += SimpleConfigEditorWindow_Activated;
        }

        /// <summary>
        /// 🔧 从系统配置加载账户权益和风险次数
        /// </summary>
        private void LoadSystemAccountInfo()
        {
            try
            {
                if (_riskCapitalService != null)
                {
                    // 从系统配置获取账户权益和风险次数
                    var accountEquity = _riskCapitalService.GetCurrentAccountEquity();
                    var riskTimes = _riskCapitalService.GetCurrentRiskCapitalTimes();
                    
                    // 🔧 自动填充到界面并设为只读
                    AccountEquityTextBox.Text = accountEquity.ToString("F2");
                    RiskTimesTextBox.Text = riskTimes.ToString();
                    
                    // 🔧 设为只读，不让用户修改
                    AccountEquityTextBox.IsReadOnly = true;
                    RiskTimesTextBox.IsReadOnly = true;
                    AccountEquityTextBox.Background = System.Windows.Media.Brushes.LightGray;
                    RiskTimesTextBox.Background = System.Windows.Media.Brushes.LightGray;
                    
                    // 自动计算风险金
                    _currentRiskCapital = _riskCapitalService.CalculateRiskCapital(accountEquity, riskTimes);
                    SingleRiskCapitalTextBox.Text = _currentRiskCapital.ToString("F2");
                    
                    // 更新计算按钮为刷新按钮
                    CalculateRiskCapitalButton.Content = "🔄 刷新系统数据";
                }
                else
                {
                    // 如果服务不可用，显示提示并保持可编辑
                    AccountEquityTextBox.Text = "1000.00";
                    RiskTimesTextBox.Text = "10";
                    CalculateRiskCapitalButton.Content = "🔄 计算风险金";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载系统账户信息失败：{ex.Message}\n请手动输入账户权益和风险次数。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                
                // 出错时保持可编辑状态
                AccountEquityTextBox.Text = "1000.00";
                RiskTimesTextBox.Text = "10";
                AccountEquityTextBox.IsReadOnly = false;
                RiskTimesTextBox.IsReadOnly = false;
                CalculateRiskCapitalButton.Content = "🔄 计算风险金";
            }
        }
        
        /// <summary>
        /// 🔧 窗口激活时重新加载配置，确保显示最新的基础配置
        /// </summary>
        private void SimpleConfigEditorWindow_Activated(object? sender, EventArgs e)
        {
            try
            {
                // 强制重新加载配置，确保从基础配置文档导入最新数据
                System.Diagnostics.Debug.WriteLine("🔄 窗口激活，重新加载配置...");
                LoadConfigs();
                
                // 🔧 只有在确实没有选中配置且不在编辑模式时才自动选择第一个
                if (ConfigListBox.SelectedItem == null && _configs.Count > 0 && !_isEditMode && _selectedConfig == null)
                {
                    ConfigListBox.SelectedIndex = 0;
                    System.Diagnostics.Debug.WriteLine("🎯 自动选中第一个配置");
                }
                
                System.Diagnostics.Debug.WriteLine($"✅ 配置重新加载完成，当前配置数量: {_configs.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 窗口激活时重新加载配置失败: {ex.Message}");
            }
        }
        
        private void LoadConfigs()
        {
            // 🔧 保存当前选中的配置名称
            string currentSelectedConfigName = _selectedConfig?.Name;
            
            _configs.Clear();
            
            try
            {
                // 🔧 强制重新加载最新配置，确保从基础配置文档导入之前保存的数据
                _configManager.RefreshConfigurations();
                
                // 🎯 完全从BaseConfigManager加载配置，之前保存的数据直接载入
                foreach (var config in _configManager.Configurations)
                {
                    _configs.Add(config);
                }
                
                // 🔧 显示配置文件路径和加载状态 - 使用统一路径管理
                var configPath = _configManager.GetConfigFilePath();
                System.Diagnostics.Debug.WriteLine($"📁 基础配置文档路径: {configPath}");
                
                // 🎯 修复：直接从基础配置文档导入，不进行旧文件迁移
                if (_configs.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("📝 当前没有基础配置，请创建新配置");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✅ 已从基础配置文档加载 {_configs.Count} 个配置:");
                    foreach (var config in _configs)
                    {
                        System.Diagnostics.Debug.WriteLine($"   📋 配置: {config.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            ConfigListBox.ItemsSource = _configs;
            
            // 🔧 重新选中之前选中的配置，保持选择状态
            if (!string.IsNullOrEmpty(currentSelectedConfigName))
            {
                var configToSelect = _configs.FirstOrDefault(c => c.Name == currentSelectedConfigName);
                if (configToSelect != null)
                {
                    ConfigListBox.SelectedItem = configToSelect;
                    _selectedConfig = configToSelect;
                    System.Diagnostics.Debug.WriteLine($"🎯 重新选中配置: {configToSelect.Name}");
                }
            }
        }
        
        private void LoadConfigsFromFile()
        {
            string configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var configs = System.Text.Json.JsonSerializer.Deserialize<List<AutoMonitorConfig>>(json);
                if (configs != null)
                {
                    foreach (var config in configs)
                    {
                        _configs.Add(config);
                    }
                }
            }
        }
        
        private void SaveConfigsToFile()
        {
            try
            {
                string configPath = GetConfigFilePath();
                var json = System.Text.Json.JsonSerializer.Serialize(_configs.ToList(), new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置文件失败：{ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private string GetConfigFilePath()
        {
            return _configManager.GetConfigFilePath();
        }
        
        private void SetupUI()
        {
            SetReadOnlyMode();
            
            // 🔧 只有在没有选中配置时才自动选择第一个
            if (_configs.Count > 0 && _selectedConfig == null)
            {
                ConfigListBox.SelectedIndex = 0;
                System.Diagnostics.Debug.WriteLine("🎯 SetupUI: 自动选中第一个配置");
            }
            else if (_configs.Count == 0)
            {
                // 没有配置时清空详细信息显示
                ClearConfigDetails();
            }
        }
        
        private void SetReadOnlyMode()
        {
            _isEditMode = false;
            _editingConfig = null;
            
            ConfigNameTextBox.IsReadOnly = true;
            ConfigNameTextBox.Background = System.Windows.Media.Brushes.LightGray;
            
            BreakEvenEnabledCheckBox.IsEnabled = false;
            BreakEvenAmountTextBox.IsReadOnly = true;
            BreakEvenAmountTextBox.Background = System.Windows.Media.Brushes.LightGray;
            
            AddPositionEnabledCheckBox.IsEnabled = false;
            ProfitProtectionEnabledCheckBox.IsEnabled = false;
            
            // 设置DataGrid为只读
            AddPositionTiersDataGrid.IsReadOnly = true;
            ProfitProtectionTiersDataGrid.IsReadOnly = true;
            
            // 隐藏编辑按钮
            SaveConfigButton.Visibility = Visibility.Collapsed;
            AddTierButton.Visibility = Visibility.Collapsed;
            RemoveTierButton.Visibility = Visibility.Collapsed;
            AddProfitTierButton.Visibility = Visibility.Collapsed;
            RemoveProfitTierButton.Visibility = Visibility.Collapsed;
            
            // 风险金计算区域在只读模式下也可以使用
            AccountEquityTextBox.IsReadOnly = false;
            RiskTimesTextBox.IsReadOnly = false;
            CalculateRiskCapitalButton.IsEnabled = true;
            AutoFillButton.IsEnabled = false; // 只读模式下不能自动填写
        }
        
        private void SetEditMode()
        {
            _isEditMode = true;
            
            ConfigNameTextBox.IsReadOnly = false;
            ConfigNameTextBox.Background = System.Windows.Media.Brushes.White;
            
            BreakEvenEnabledCheckBox.IsEnabled = true;
            BreakEvenAmountTextBox.IsReadOnly = false;
            BreakEvenAmountTextBox.Background = System.Windows.Media.Brushes.White;
            
            AddPositionEnabledCheckBox.IsEnabled = true;
            ProfitProtectionEnabledCheckBox.IsEnabled = true;
            
            // 设置DataGrid为可编辑
            AddPositionTiersDataGrid.IsReadOnly = false;
            ProfitProtectionTiersDataGrid.IsReadOnly = false;
            
            // 显示编辑按钮
            SaveConfigButton.Visibility = Visibility.Visible;
            AddTierButton.Visibility = Visibility.Visible;
            RemoveTierButton.Visibility = Visibility.Visible;
            AddProfitTierButton.Visibility = Visibility.Visible;
            RemoveProfitTierButton.Visibility = Visibility.Visible;
            
            // 编辑模式下可以使用自动填写功能
            AutoFillButton.IsEnabled = true;
        }
        
        private void LoadConfigDetails(AutoMonitorConfig? config)
        {
            if (config == null)
            {
                ClearConfigDetails();
                return;
            }
            
            ConfigNameTextBox.Text = config.Name;
            
            // 保本配置
            BreakEvenEnabledCheckBox.IsChecked = config.BreakEvenConfig.IsEnabled;
            BreakEvenAmountTextBox.Text = config.BreakEvenConfig.TriggerProfitAmount.ToString("F2");
            
            // 推仓配置
            AddPositionEnabledCheckBox.IsChecked = config.AddPositionConfig.IsEnabled;
            AddPositionTiersDataGrid.ItemsSource = config.AddPositionConfig.Tiers;
            
            // 保盈配置
            ProfitProtectionEnabledCheckBox.IsChecked = config.ProfitProtectionConfig.IsEnabled;
            ProfitProtectionTiersDataGrid.ItemsSource = config.ProfitProtectionConfig.Tiers;
        }
        
        private void ClearConfigDetails()
        {
            ConfigNameTextBox.Text = string.Empty;
            BreakEvenEnabledCheckBox.IsChecked = false;
            BreakEvenAmountTextBox.Text = "0.00";
            AddPositionEnabledCheckBox.IsChecked = false;
            ProfitProtectionEnabledCheckBox.IsChecked = false;
            AddPositionTiersDataGrid.ItemsSource = null;
            ProfitProtectionTiersDataGrid.ItemsSource = null;
        }
        
        private AutoMonitorConfig CreateNewConfig()
        {
            // 🔧 自动读取账户权益和风险次数
            var accountEquity = _riskCapitalService.GetCurrentAccountEquity();
            var riskTimes = _riskCapitalService.GetCurrentRiskCapitalTimes();
            
            return AutoMonitorConfig.CreateSmartDefault(accountEquity, riskTimes);
        }
        
        private void SaveCurrentConfig()
        {
            if (_editingConfig == null) return;
            
            try
            {
                // 更新编辑中的配置信息
                var configName = ConfigNameTextBox.Text?.Trim() ?? "";
                
                // 检查配置名称是否重复（排除当前正在编辑的配置）
                if (_selectedConfig == null) // 新增模式
                {
                    if (_configs.Any(c => c.Name.Equals(configName, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show($"配置名称 '{configName}' 已存在，请使用其他名称", "名称冲突", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else // 修改模式
                {
                    if (_configs.Any(c => c != _selectedConfig && c.Name.Equals(configName, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show($"配置名称 '{configName}' 已存在，请使用其他名称", "名称冲突", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                
                _editingConfig.Name = configName;
                _editingConfig.LastModifiedTime = DateTime.Now;
                
                _editingConfig.BreakEvenConfig.IsEnabled = BreakEvenEnabledCheckBox.IsChecked ?? false;
                if (decimal.TryParse(BreakEvenAmountTextBox.Text, out decimal breakEvenAmount))
                {
                    _editingConfig.BreakEvenConfig.TriggerProfitAmount = breakEvenAmount;
                }
                
                _editingConfig.AddPositionConfig.IsEnabled = AddPositionEnabledCheckBox.IsChecked ?? false;
                _editingConfig.ProfitProtectionConfig.IsEnabled = ProfitProtectionEnabledCheckBox.IsChecked ?? false;
                
                // 🎯 使用BaseConfigManager保存配置（统一处理新增和修改）
                    try
                    {
                        // 将修改后的配置复制到选中的配置
                        _selectedConfig.Name = _editingConfig.Name;
                        _selectedConfig.LastModifiedTime = _editingConfig.LastModifiedTime;
                        _selectedConfig.BreakEvenConfig = _editingConfig.BreakEvenConfig;
                        _selectedConfig.AddPositionConfig = _editingConfig.AddPositionConfig;
                        _selectedConfig.ProfitProtectionConfig = _editingConfig.ProfitProtectionConfig;
                        
                        // 使用BaseConfigManager更新配置（会自动保存到文件）
                        _configManager.UpdateConfiguration(_selectedConfig);
                        
                    // 重新加载配置列表，确保数据同步
                    LoadConfigs();
                    
                    // 重新选中更新后的配置
                    var updatedConfig = _configs.FirstOrDefault(c => c.Name == configName);
                    if (updatedConfig != null)
                    {
                        ConfigListBox.SelectedItem = updatedConfig;
                        _selectedConfig = updatedConfig;
                    }
                        
                    MessageBox.Show($"配置 '{configName}' 保存成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // 🔧 重置新建标志
                    _isNewConfig = false;
                    }
                    catch (Exception ex)
                    {
                    MessageBox.Show($"保存配置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                }
                
                SetReadOnlyMode();
                LoadConfigDetails(_selectedConfig);
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        #region 事件处理
        
        private void ConfigListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigListBox.SelectedItem is AutoMonitorConfig selectedConfig)
            {
                // 🔧 防止在编辑模式下意外退出编辑状态
                // 如果选择的是当前正在编辑的配置，不要退出编辑模式
                bool isSameConfig = _selectedConfig != null && selectedConfig.Name == _selectedConfig.Name;
                
                // 🔧 如果是同一个配置，直接返回，避免重复处理
                if (isSameConfig)
                {
                    return;
                }
                
                _selectedConfig = selectedConfig;
                _isNewConfig = false; // 🔧 选择已有配置时重置新建标志
                
                if (_isEditMode)
                {
                    // 如果当前在编辑模式下选择了不同配置，退出编辑模式
                    SetReadOnlyMode();
                }
                
                // 加载新选择的配置详情
                LoadConfigDetails(selectedConfig);
            }
        }
        
        private void NewConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
        {
            // 🔧 新建配置时自动读取和填充账户权益
                var accountEquity = _riskCapitalService?.GetCurrentAccountEquity() ?? 1000m;
                var riskTimes = _riskCapitalService?.GetCurrentRiskCapitalTimes() ?? 10;
            
            var newConfig = CreateNewConfig();
                
                // 🔧 生成唯一的配置名称
                string baseName = "新配置";
                string configName = baseName;
                int counter = 1;
                while (_configs.Any(c => c.Name == configName))
                {
                    configName = $"{baseName}{counter}";
                    counter++;
                }
                newConfig.Name = configName;
                
            _editingConfig = newConfig;
            
                // 🔧 关键修复：先将配置保存到BaseConfigManager，确保持久化
                try
                {
                    // 使用BaseConfigManager创建临时配置
                    var tempConfig = _configManager.CreateConfiguration(configName, accountEquity, riskTimes);
                    
                    // 更新配置内容
                    tempConfig.BreakEvenConfig = newConfig.BreakEvenConfig;
                    tempConfig.AddPositionConfig = newConfig.AddPositionConfig;
                    tempConfig.ProfitProtectionConfig = newConfig.ProfitProtectionConfig;
                    tempConfig.LastModifiedTime = DateTime.Now;
                    
                    // 保存到BaseConfigManager
                    _configManager.UpdateConfiguration(tempConfig);
                    
                    // 使用持久化后的配置
                    _editingConfig = tempConfig;
                    
                    // 重新加载配置列表，确保包含新配置
                    LoadConfigs();
                    
                    // 选中新创建的配置
                    var createdConfig = _configs.FirstOrDefault(c => c.Name == configName);
                    if (createdConfig != null)
                    {
                        // 🔧 先设置编辑模式标志，防止选择变化事件干扰
                        _selectedConfig = createdConfig;
                        _isNewConfig = false; // 已经持久化，不再是临时新建状态
                        
                        // 🔧 进入编辑模式
            SetEditMode();
                        
                        // 🔧 设置选中项（此时_isEditMode已经为true，不会被选择变化事件干扰）
                        ConfigListBox.SelectedItem = createdConfig;
                        
                        // 手动加载配置详情，确保显示正确
                        LoadConfigDetails(tempConfig);
                    }
                }
                catch (Exception createEx)
                {
                    MessageBox.Show($"创建配置失败：{createEx.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            
            // 🔧 自动填充账户权益到风险金计算区域
            AccountEquityTextBox.Text = accountEquity.ToString("F2");
            RiskTimesTextBox.Text = riskTimes.ToString();
            
            // 自动计算并显示风险金
            _currentRiskCapital = accountEquity / riskTimes;
            SingleRiskCapitalTextBox.Text = _currentRiskCapital.ToString("F2");
            
                // 🔧 自动聚焦到配置名称输入框，方便用户修改
                ConfigNameTextBox.Focus();
                ConfigNameTextBox.SelectAll();
                
                MessageBox.Show($"✅ 新配置已创建并保存：{configName}\n\n" +
                              $"已自动读取账户信息：\n" +
                              $"• 账户权益：{accountEquity:F2} USDT\n" +
                              $"• 风险次数：{riskTimes}\n" +
                              $"• 单倍风险金：{_currentRiskCapital:F2} USDT\n\n" +
                              $"配置已保存到文件，您可以继续编辑参数。",
                              "新建配置成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建新配置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void EditConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedConfig == null)
            {
                MessageBox.Show("请先选择一个配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // 克隆配置用于编辑
            _editingConfig = new AutoMonitorConfig
            {
                Name = _selectedConfig.Name,
                IsEnabled = _selectedConfig.IsEnabled,
                ScanIntervalSeconds = _selectedConfig.ScanIntervalSeconds,
                BreakEvenConfig = new AutoBreakEvenConfig
                {
                    IsEnabled = _selectedConfig.BreakEvenConfig.IsEnabled,
                    TriggerProfitAmount = _selectedConfig.BreakEvenConfig.TriggerProfitAmount
                },
                AddPositionConfig = new AutoAddPositionConfig
                {
                    IsEnabled = _selectedConfig.AddPositionConfig.IsEnabled,
                    Tiers = _selectedConfig.AddPositionConfig.Tiers.ToList()
                },
                ProfitProtectionConfig = new AutoProfitProtectionConfig
                {
                    IsEnabled = _selectedConfig.ProfitProtectionConfig.IsEnabled,
                    Tiers = _selectedConfig.ProfitProtectionConfig.Tiers.ToList()
                }
            };
            
            SetEditMode();
            LoadConfigDetails(_editingConfig);
        }
        
        private void DeleteConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedConfig == null)
            {
                MessageBox.Show("请先选择一个配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var result = MessageBox.Show($"确定要删除配置 '{_selectedConfig.Name}' 吗？", "确认删除", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 🎯 使用BaseConfigManager删除配置
                    _configManager.DeleteConfiguration(_selectedConfig.Name);
                    
                    // 从本地列表中移除
                    _configs.Remove(_selectedConfig);
                    _selectedConfig = null;
                    ClearConfigDetails();
                    
                    MessageBox.Show("配置删除成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除配置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ConfigNameTextBox.Text))
            {
                MessageBox.Show("配置名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            SaveCurrentConfig();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditMode)
            {
                var result = MessageBox.Show("确定要取消编辑吗？未保存的更改将丢失。", "确认取消", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    SetReadOnlyMode();
                    LoadConfigDetails(_selectedConfig);
                }
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditMode)
            {
                var result = MessageBox.Show("有未保存的更改，确定要关闭吗？", "确认关闭", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }
            
            Close();
        }
        
        private void AddTierButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editingConfig?.AddPositionConfig?.Tiers != null)
            {
                var nextTierIndex = _editingConfig.AddPositionConfig.Tiers.Count + 1;
                
                // 🔧 修复：根据当前风险金计算合适的默认值
                var triggerAmount = _currentRiskCapital > 0 ? Math.Round(_currentRiskCapital * nextTierIndex, 2) : nextTierIndex * 100m;
                var protectionAmount = (nextTierIndex == 1 && _currentRiskCapital > 0) ? Math.Round(-_currentRiskCapital / 2m, 2) : 0m;
                
                var newTier = new AddPositionTier
                {
                    TierIndex = nextTierIndex,
                    TriggerProfitAmount = triggerAmount,
                    RiskMultiplier = 1.0m,
                    StopLossRatio = 0.1m,
                    ProfitProtectionAmount = protectionAmount,
                    IsEnabled = true
                };
                _editingConfig.AddPositionConfig.Tiers.Add(newTier);
                
                // 刷新DataGrid
                AddPositionTiersDataGrid.ItemsSource = null;
                AddPositionTiersDataGrid.ItemsSource = _editingConfig.AddPositionConfig.Tiers;
            }
        }
        
        private void RemoveTierButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editingConfig?.AddPositionConfig?.Tiers != null && _editingConfig.AddPositionConfig.Tiers.Count > 1)
            {
                _editingConfig.AddPositionConfig.Tiers.RemoveAt(_editingConfig.AddPositionConfig.Tiers.Count - 1);
                
                // 刷新DataGrid
                AddPositionTiersDataGrid.ItemsSource = null;
                AddPositionTiersDataGrid.ItemsSource = _editingConfig.AddPositionConfig.Tiers;
            }
        }
        
        private void AddProfitTierButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editingConfig?.ProfitProtectionConfig?.Tiers != null)
            {
                var nextTierIndex = _editingConfig.ProfitProtectionConfig.Tiers.Count + 1;
                
                // 🔧 修复：根据当前风险金计算合适的默认值
                var triggerAmount = _currentRiskCapital > 0 ? Math.Round(_currentRiskCapital * (nextTierIndex * 10), 2) : nextTierIndex * 1000m;
                var protectionAmount = _currentRiskCapital > 0 ? Math.Round(_currentRiskCapital * (nextTierIndex * 10) * 0.8m, 2) : nextTierIndex * 1000m * 0.8m;
                
                var newTier = new ProfitProtectionTier
                {
                    TierIndex = nextTierIndex,
                    TriggerProfitAmount = triggerAmount,
                    ProtectionAmount = protectionAmount,
                    IsEnabled = true
                };
                _editingConfig.ProfitProtectionConfig.Tiers.Add(newTier);
                
                // 刷新DataGrid
                ProfitProtectionTiersDataGrid.ItemsSource = null;
                ProfitProtectionTiersDataGrid.ItemsSource = _editingConfig.ProfitProtectionConfig.Tiers;
            }
        }
        
        private void RemoveProfitTierButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editingConfig?.ProfitProtectionConfig?.Tiers != null && _editingConfig.ProfitProtectionConfig.Tiers.Count > 1)
            {
                _editingConfig.ProfitProtectionConfig.Tiers.RemoveAt(_editingConfig.ProfitProtectionConfig.Tiers.Count - 1);
                
                // 刷新DataGrid
                ProfitProtectionTiersDataGrid.ItemsSource = null;
                ProfitProtectionTiersDataGrid.ItemsSource = _editingConfig.ProfitProtectionConfig.Tiers;
            }
        }
        
        #endregion
        
        #region 风险金计算相关事件处理
        
        /// <summary>
        /// 计算风险金按钮点击事件
        /// </summary>
        private void CalculateRiskCapitalButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 🔧 从系统配置重新获取最新的账户权益和风险次数
                if (_riskCapitalService != null)
                {
                    var accountEquity = _riskCapitalService.GetCurrentAccountEquity();
                    var riskTimes = _riskCapitalService.GetCurrentRiskCapitalTimes();
                    
                    // 更新界面显示
                    AccountEquityTextBox.Text = accountEquity.ToString("F2");
                    RiskTimesTextBox.Text = riskTimes.ToString();
                    
                    // 计算单倍风险金
                    _currentRiskCapital = _riskCapitalService.CalculateRiskCapital(accountEquity, riskTimes);
                    SingleRiskCapitalTextBox.Text = _currentRiskCapital.ToString("F2");
                    
                    MessageBox.Show($"系统数据刷新完成！\n\n账户权益：{accountEquity:F2} USDT\n风险次数：{riskTimes}\n单倍风险金：{_currentRiskCapital:F2} USDT\n\n这些数据已从系统配置自动获取。\n点击\"自动填写配置\"按钮可一键填写所有配置项。", 
                        "刷新完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // 🔧 如果服务不可用，提示用户手动输入
                    MessageBox.Show("风险金服务不可用，无法从系统配置获取数据。\n请检查系统连接状态。", "服务不可用", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新系统数据时发生错误：{ex.Message}\n\n请检查账户连接状态或联系系统管理员。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 自动填写配置按钮点击事件
        /// </summary>
        private void AutoFillButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查是否已计算风险金
                if (_currentRiskCapital <= 0)
                {
                    MessageBox.Show("请先计算风险金再进行自动填写", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 检查是否在编辑模式
                if (!_isEditMode || _editingConfig == null)
                {
                    MessageBox.Show("请先进入编辑模式再进行自动填写", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 确认对话框
                var existingAddPositionCount = _editingConfig.AddPositionConfig.Tiers.Count;
                var existingProfitProtectionCount = _editingConfig.ProfitProtectionConfig.Tiers.Count;
                
                var message = $"将根据计算的风险金({_currentRiskCapital:F2} USDT)自动填写配置：\n\n" +
                    $"• 保本配置：{_currentRiskCapital:F2} USDT (1倍风险金)\n";
                
                if (existingAddPositionCount > 0)
                {
                    message += $"• 推仓配置：保留现有{existingAddPositionCount}个阶梯，更新触发金额为按风险金倍数计算\n";
                }
                else
                {
                    message += $"• 推仓配置：添加默认4个阶梯，{_currentRiskCapital:F2} USDT (1倍) 到 {_currentRiskCapital * 4:F2} USDT (4倍)\n";
                }
                
                if (existingProfitProtectionCount > 0)
                {
                    message += $"• 保盈配置：保留现有{existingProfitProtectionCount}个阶梯，更新触发金额为按风险金倍数计算\n";
                }
                else
                {
                    message += $"• 保盈配置：添加默认3个阶梯，{_currentRiskCapital * 10:F2} USDT (10倍) 到 {_currentRiskCapital * 30:F2} USDT (30倍)\n";
                }
                
                message += "\n💡 现有手工添加的阶梯会被保留，只更新触发金额。\n确定要自动填写吗？";
                
                var result = MessageBox.Show(message, "确认自动填写", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                
                // 自动填写配置
                AutoFillConfiguration();
                
                MessageBox.Show("配置自动填写完成！\n\n请检查各项配置是否符合您的需求，如有需要可手动调整。", 
                    "填写完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"自动填写配置时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 执行自动填写配置的核心逻辑
        /// </summary>
        private void AutoFillConfiguration()
        {
            if (_editingConfig == null) return;
            
            // 🔧 保存当前选中的配置名称，防止在DataGrid更新时丢失选择
            string currentSelectedConfigName = _selectedConfig?.Name;
            
            // 1. 自动填写保本配置（1倍风险金）
            _editingConfig.BreakEvenConfig.IsEnabled = true;
            _editingConfig.BreakEvenConfig.TriggerProfitAmount = Math.Round(_currentRiskCapital * 1.0m, 2);
            
            // 更新界面显示
            BreakEvenEnabledCheckBox.IsChecked = true;
            BreakEvenAmountTextBox.Text = _editingConfig.BreakEvenConfig.TriggerProfitAmount.ToString("F2");
            
            // 2. 自动填写推仓配置（保留现有手工添加的阶梯）
            _editingConfig.AddPositionConfig.IsEnabled = true;
            
            // 🔧 修复：不清除现有阶梯，而是添加标准阶梯（如果没有的话）
            if (_editingConfig.AddPositionConfig.Tiers.Count == 0)
            {
                // 如果没有现有阶梯，则添加标准的4个阶梯
                for (int i = 1; i <= 4; i++)
                {
                    var tier = new AddPositionTier
                    {
                        TierIndex = i,
                        TriggerProfitAmount = Math.Round(_currentRiskCapital * i, 2),
                        RiskMultiplier = 1.0m,
                        StopLossRatio = 0.10m,
                        ProfitProtectionAmount = i == 1 ? Math.Round(-_currentRiskCapital / 2m, 2) : 0m, // 第一阶梯：负二分之一倍风险金，其他为0
                        IsEnabled = true
                    };
                    _editingConfig.AddPositionConfig.Tiers.Add(tier);
                }
            }
            else
            {
                // 如果有现有阶梯，则更新它们的触发金额（按照风险金倍数）
                for (int i = 0; i < _editingConfig.AddPositionConfig.Tiers.Count; i++)
                {
                    var tier = _editingConfig.AddPositionConfig.Tiers[i];
                    tier.TriggerProfitAmount = Math.Round(_currentRiskCapital * (i + 1), 2);
                    // 只更新第一阶梯的保盈金额，其他阶梯保持原值
                    if (i == 0)
                    {
                        tier.ProfitProtectionAmount = Math.Round(-_currentRiskCapital / 2m, 2);
                    }
                }
            }
            
            // 更新界面显示
            AddPositionEnabledCheckBox.IsChecked = true;
            
            // 🔧 安全地更新DataGrid数据源，避免触发意外事件
            if (AddPositionTiersDataGrid.ItemsSource != _editingConfig.AddPositionConfig.Tiers)
            {
            AddPositionTiersDataGrid.ItemsSource = _editingConfig.AddPositionConfig.Tiers;
            }
            else
            {
                AddPositionTiersDataGrid.Items.Refresh();
            }
            
            // 3. 自动填写保盈配置（保留现有手工添加的阶梯）
            _editingConfig.ProfitProtectionConfig.IsEnabled = true;
            
            // 🔧 修复：不清除现有阶梯，而是添加标准阶梯（如果没有的话）
            if (_editingConfig.ProfitProtectionConfig.Tiers.Count == 0)
            {
                // 如果没有现有阶梯，则添加标准的3个阶梯
                for (int i = 1; i <= 3; i++)
                {
                    var tier = new ProfitProtectionTier
                    {
                        TierIndex = i,
                        TriggerProfitAmount = Math.Round(_currentRiskCapital * (i * 10), 2),
                        ProtectionAmount = Math.Round(_currentRiskCapital * (i * 10) * 0.8m, 2),
                        IsEnabled = true
                    };
                    _editingConfig.ProfitProtectionConfig.Tiers.Add(tier);
                }
            }
            else
            {
                // 如果有现有阶梯，则更新它们的触发金额（按照风险金倍数）
                for (int i = 0; i < _editingConfig.ProfitProtectionConfig.Tiers.Count; i++)
                {
                    var tier = _editingConfig.ProfitProtectionConfig.Tiers[i];
                    tier.TriggerProfitAmount = Math.Round(_currentRiskCapital * ((i + 1) * 10), 2);
                    tier.ProtectionAmount = Math.Round(_currentRiskCapital * ((i + 1) * 10) * 0.8m, 2);
                }
            }
            
            // 更新界面显示
            ProfitProtectionEnabledCheckBox.IsChecked = true;
            
            // 🔧 安全地更新DataGrid数据源，避免触发意外事件
            if (ProfitProtectionTiersDataGrid.ItemsSource != _editingConfig.ProfitProtectionConfig.Tiers)
            {
            ProfitProtectionTiersDataGrid.ItemsSource = _editingConfig.ProfitProtectionConfig.Tiers;
            }
            else
            {
                ProfitProtectionTiersDataGrid.Items.Refresh();
            }
            
            // 🔧 确保配置选择没有被意外改变
            if (!string.IsNullOrEmpty(currentSelectedConfigName) && 
                (_selectedConfig == null || _selectedConfig.Name != currentSelectedConfigName))
            {
                var configToReselect = _configs.FirstOrDefault(c => c.Name == currentSelectedConfigName);
                if (configToReselect != null)
                {
                    ConfigListBox.SelectedItem = configToReselect;
                    _selectedConfig = configToReselect;
                    System.Diagnostics.Debug.WriteLine($"🔧 自动填写后重新选中配置: {configToReselect.Name}");
                }
            }
        }
        
        #endregion

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 🔧 新增：通知BaseConfigManager配置可能发生了变化
                _configManager.RefreshConfigurations();
                
                // 🔧 新增：触发配置变更事件，通知其他窗口刷新
                if (_selectedConfig != null)
                {
                    _configManager.SetCurrentConfiguration(_selectedConfig.Name);
                }
                
                base.OnClosed(e);
            }
            catch (Exception ex)
            {
                // 记录错误但不阻止窗口关闭
                System.Diagnostics.Debug.WriteLine($"窗口关闭处理异常：{ex.Message}");
            }
        }
    }
} 