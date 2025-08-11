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
        
        // 日志记录器
        private readonly ILogger<SimpleConfigEditorWindow> _logger;
        
        // 🔧 移除手动获取方法，改用RiskCapitalService
        
        // 🔧 新增：增强版数据管理器支持
        private bool _useEnhancedManager = false;
        private EnhancedBaseConfigManager? _enhancedConfigManager = null;

        /// <summary>
        /// 启用新的增强版数据管理器
        /// </summary>
        public void EnableEnhancedDataManager()
        {
            try
            {
                _logger?.LogInformation("🚀 切换到增强版数据管理器");
                
                _useEnhancedManager = true;
                _enhancedConfigManager = EnhancedBaseConfigManager.Instance;
                
                // 重新加载配置
                LoadConfigs();
                
                _logger?.LogInformation("✅ 增强版数据管理器已启用");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 启用增强版数据管理器失败");
                MessageBox.Show($"启用增强版数据管理器失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 检查是否使用增强版管理器
        /// </summary>
        private bool IsUsingEnhancedManager => _useEnhancedManager && _enhancedConfigManager != null;

        public SimpleConfigEditorWindow(MainViewModel? mainViewModel = null, BaseConfigManager? configManager = null)
        {
            InitializeComponent();
            
            // 🔧 修复：使用BaseConfigManager单例实例，确保全局配置统一
            _configManager = BaseConfigManager.Instance;
            _configs = new ObservableCollection<AutoMonitorConfig>();
            
            // 初始化日志记录器
            _logger = NullLogger<SimpleConfigEditorWindow>.Instance;
            
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
                // 🔧【自动诊断】窗口激活时检查配置文件状态
                if (_configManager.Configurations.Count == 0)
                {
                    _logger?.LogWarning("⚠️ 检测到内存中没有配置，自动运行诊断...");
                    
                    // 延时运行诊断，确保窗口完全加载
                    this.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() =>
                        {
                            // 只记录日志，不弹出对话框（避免干扰用户）
                            LogConfigFileStatus();
                        })
                    );
                }
                
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
                if (IsUsingEnhancedManager)
                {
                    // 使用新的增强版管理器
                    _enhancedConfigManager.RefreshConfigurations();
                    
                    foreach (var config in _enhancedConfigManager.Configurations)
                    {
                        _configs.Add(config);
                    }
                    
                    _logger?.LogInformation($"✅ 从增强版管理器加载了 {_configs.Count} 个配置");
                }
                else
                {
                    // 使用原有的管理器（保持兼容性）
                    _configManager.RefreshConfigurations();
                    
                    foreach (var config in _configManager.Configurations)
                    {
                        _configs.Add(config);
                    }
                    
                    _logger?.LogInformation($"✅ 从原管理器加载了 {_configs.Count} 个配置");
                }
                
                // 🔧 显示配置文件路径和加载状态
                var configPath = IsUsingEnhancedManager 
                    ? _enhancedConfigManager.GetConfigFilePath() 
                    : _configManager.GetConfigFilePath();
                    
                System.Diagnostics.Debug.WriteLine($"📁 基础配置文档路径: {configPath}");
                
                if (_configs.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("📝 当前没有基础配置，请创建新配置");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✅ 已加载 {_configs.Count} 个配置:");
                    foreach (var config in _configs)
                    {
                        System.Diagnostics.Debug.WriteLine($"   📋 配置: {config.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 加载配置失败");
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
                
                // 🔧【调试信息】显示配置文件路径和保存过程
                var configFilePath = _configManager.GetType()
                    .GetField("_configFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_configManager)?.ToString() ?? "未知路径";
                
                _logger?.LogInformation($"🔍 准备保存配置: {configName}");
                _logger?.LogInformation($"🔍 配置文件路径: {configFilePath}");
                _logger?.LogInformation($"🔍 当前配置数量: {_configManager.Configurations.Count}");
                
                // 检查目录是否存在
                var configDir = System.IO.Path.GetDirectoryName(configFilePath);
                if (!string.IsNullOrEmpty(configDir))
                {
                    if (!System.IO.Directory.Exists(configDir))
                    {
                        System.IO.Directory.CreateDirectory(configDir);
                        _logger?.LogInformation($"🔧 创建配置目录: {configDir}");
                    }
                    else
                    {
                        _logger?.LogInformation($"✅ 配置目录已存在: {configDir}");
                    }
                }
                
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
                
                // 🔧【关键修复】分别处理新增和修改模式
                try
                {
                    if (_selectedConfig == null) // 新增模式
                    {
                        // 🔧【新增模式】检查配置是否已存在于BaseConfigManager中
                        var existingConfig = _configManager.GetConfiguration(_editingConfig.Name);
                        if (existingConfig == null)
                        {
                            // 配置不存在，需要添加 - 统一使用BaseConfigManager
                            _configManager.AddConfiguration(_editingConfig);
                            _logger?.LogInformation($"💾 新配置已保存: {_editingConfig.Name}");
                                
                            // 🔧【调试】保存后立即检查文件是否存在
                            if (System.IO.File.Exists(configFilePath))
                            {
                                var fileContent = System.IO.File.ReadAllText(configFilePath);
                                _logger?.LogInformation($"✅ 配置文件已创建，大小: {fileContent.Length} 字符");
                                
                                // 显示文件位置给用户
                                MessageBox.Show($"✅ 配置保存成功！\n\n配置文件位置：\n{configFilePath}\n\n文件大小：{fileContent.Length} 字符", 
                                    "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                _logger?.LogWarning($"⚠️ 配置文件未找到: {configFilePath}");
                                MessageBox.Show($"⚠️ 配置可能未正确保存\n\n预期文件位置：\n{configFilePath}\n\n请检查该位置是否存在文件", 
                                    "保存警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                            
                            // 将新配置添加到本地列表
                            _configs.Add(_editingConfig);
                        }
                        else
                        {
                            // 配置已存在，更新它 - 统一使用BaseConfigManager
                            _configManager.UpdateConfiguration(_editingConfig);
                            _logger?.LogInformation($"💾 配置已更新: {_editingConfig.Name}");
                        }
                        
                        _selectedConfig = _editingConfig;
                    }
                    else // 修改模式
                    {
                        // 将修改后的配置复制到选中的配置
                        _selectedConfig.Name = _editingConfig.Name;
                        _selectedConfig.LastModifiedTime = _editingConfig.LastModifiedTime;
                        _selectedConfig.BreakEvenConfig = _editingConfig.BreakEvenConfig;
                        _selectedConfig.AddPositionConfig = _editingConfig.AddPositionConfig;
                        _selectedConfig.ProfitProtectionConfig = _editingConfig.ProfitProtectionConfig;
                        
                        // 🔧 统一使用BaseConfigManager保存配置
                        _configManager.UpdateConfiguration(_selectedConfig);
                        _logger?.LogInformation($"💾 配置已保存: {_selectedConfig.Name}");
                    }
                    
                    // 重新加载配置列表，确保数据同步
                    LoadConfigs();
                    
                    // 重新选中更新后的配置
                    var updatedConfig = _configs.FirstOrDefault(c => c.Name == configName);
                    if (updatedConfig != null)
                    {
                        ConfigListBox.SelectedItem = updatedConfig;
                        _selectedConfig = updatedConfig;
                    }
                    
                    _logger?.LogInformation($"✅ 配置保存操作完成: {configName}");
                }
                catch (Exception saveEx)
                {
                    _logger?.LogError(saveEx, $"❌ 保存配置失败: {configName}");
                    MessageBox.Show($"保存配置失败：{saveEx.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SaveCurrentConfig失败");
                MessageBox.Show($"保存配置时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            
            // 🔧【修复】新建配置时不立即保存，让用户编辑后再保存
            try
            {
                // 🔧【关键修复】设置为新增模式，配置将在用户点击保存时才真正保存
                _selectedConfig = null; // 明确设置为新增模式
                _isNewConfig = true; // 标记为新建状态
                
                _logger?.LogInformation($"🆕 准备创建新配置: {configName}");
                
                // 🔧 进入编辑模式
                SetEditMode();
                
                // 手动加载新配置的详情到界面
                LoadConfigDetails(_editingConfig);
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
            
            // 🔧【修复】提示用户新配置已准备好，需要保存
            MessageBox.Show($"✅ 新配置已准备就绪：{configName}\n\n" +
                          $"已自动读取账户信息：\n" +
                          $"• 账户权益：{accountEquity:F2} USDT\n" +
                          $"• 风险次数：{riskTimes}\n" +
                          $"• 单倍风险金：{_currentRiskCapital:F2} USDT\n\n" +
                          $"请编辑配置参数，然后点击【保存配置】按钮保存到文件。",
                          "新建配置", MessageBoxButton.OK, MessageBoxImage.Information);
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
        /// 🔧 调试配置文件状态
        /// </summary>
        private void DebugConfigFileStatus()
        {
            try
            {
                var configFilePath = _configManager.GetType()
                    .GetField("_configFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_configManager)?.ToString() ?? "未知路径";
                
                var debugInfo = new System.Text.StringBuilder();
                debugInfo.AppendLine("🔍 基础配置文件诊断报告");
                debugInfo.AppendLine($"📁 配置文件路径: {configFilePath}");
                
                // 检查文件是否存在
                if (System.IO.File.Exists(configFilePath))
                {
                    var fileInfo = new System.IO.FileInfo(configFilePath);
                    debugInfo.AppendLine($"✅ 文件存在");
                    debugInfo.AppendLine($"📊 文件大小: {fileInfo.Length} 字节");
                    debugInfo.AppendLine($"🕒 最后修改: {fileInfo.LastWriteTime}");
                    
                    try
                    {
                        var content = System.IO.File.ReadAllText(configFilePath);
                        debugInfo.AppendLine($"📖 文件内容长度: {content.Length} 字符");
                        
                        if (string.IsNullOrWhiteSpace(content))
                        {
                            debugInfo.AppendLine("⚠️ 文件内容为空");
                        }
                        else
                        {
                            // 显示前500个字符的内容
                            var preview = content.Length > 500 ? content.Substring(0, 500) + "\n..." : content;
                            debugInfo.AppendLine($"📝 文件内容预览:\n{preview}");
                            
                            // 尝试解析JSON结构
                            try
                            {
                                using var document = System.Text.Json.JsonDocument.Parse(content);
                                var root = document.RootElement;
                                debugInfo.AppendLine($"🔍 JSON根元素类型: {root.ValueKind}");
                                
                                if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    debugInfo.AppendLine($"📋 数组格式，包含 {root.GetArrayLength()} 个元素");
                                }
                                else if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    debugInfo.AppendLine("📋 对象格式，属性列表:");
                                    foreach (var property in root.EnumerateObject())
                                    {
                                        debugInfo.AppendLine($"   - {property.Name}: {property.Value.ValueKind}");
                                    }
                                }
                            }
                            catch (System.Text.Json.JsonException jsonEx)
                            {
                                debugInfo.AppendLine($"❌ JSON解析失败: {jsonEx.Message}");
                            }
                        }
                    }
                    catch (Exception readEx)
                    {
                        debugInfo.AppendLine($"❌ 读取文件失败: {readEx.Message}");
                    }
                }
                else
                {
                    debugInfo.AppendLine("❌ 文件不存在");
                    
                    // 检查目录是否存在
                    var directory = System.IO.Path.GetDirectoryName(configFilePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        if (System.IO.Directory.Exists(directory))
                        {
                            debugInfo.AppendLine($"✅ 目录存在: {directory}");
                            var files = System.IO.Directory.GetFiles(directory, "*.json");
                            debugInfo.AppendLine($"📁 目录中的JSON文件: {files.Length} 个");
                            foreach (var file in files)
                            {
                                debugInfo.AppendLine($"   - {System.IO.Path.GetFileName(file)}");
                            }
                        }
                        else
                        {
                            debugInfo.AppendLine($"❌ 目录不存在: {directory}");
                        }
                    }
                }
                
                // 检查内存中的配置
                debugInfo.AppendLine($"💾 内存中的配置数量: {_configManager.Configurations.Count}");
                foreach (var config in _configManager.Configurations)
                {
                    debugInfo.AppendLine($"   - {config.Name} (创建时间: {config.CreateTime})");
                }
                
                // 显示诊断报告
                MessageBox.Show(debugInfo.ToString(), "配置文件诊断报告", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"调试配置文件状态时发生错误：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 调试按钮点击事件
        /// </summary>
        private void DebugConfigButton_Click(object sender, RoutedEventArgs e)
        {
            DebugConfigFileStatus();
        }
        
        /// <summary>
        /// 只记录日志的配置文件状态检查（不弹出对话框）
        /// </summary>
        private void LogConfigFileStatus()
        {
            try
            {
                var configFilePath = _configManager.GetType()
                    .GetField("_configFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_configManager)?.ToString() ?? "未知路径";
                
                _logger?.LogInformation($"🔍 自动诊断配置文件: {configFilePath}");
                
                if (System.IO.File.Exists(configFilePath))
                {
                    var fileInfo = new System.IO.FileInfo(configFilePath);
                    _logger?.LogInformation($"📄 文件存在，大小: {fileInfo.Length} 字节");
                    
                    var content = System.IO.File.ReadAllText(configFilePath);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        _logger?.LogWarning("⚠️ 配置文件为空");
                    }
                    else
                    {
                        _logger?.LogInformation($"📖 文件内容长度: {content.Length} 字符");
                        
                        // 尝试手动重新加载配置
                        _logger?.LogInformation("🔄 尝试手动重新加载配置...");
                        _configManager.RefreshConfigurations();
                        
                        // 重新加载UI
                        LoadConfigs();
                        
                        _logger?.LogInformation($"✅ 重新加载后配置数量: {_configManager.Configurations.Count}");
                    }
                }
                else
                {
                    _logger?.LogWarning($"❌ 配置文件不存在: {configFilePath}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "自动诊断配置文件失败");
            }
        }

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
                
                // 取消事件订阅
                this.Activated -= SimpleConfigEditorWindow_Activated;
            }
            catch (Exception ex)
            {
                // 记录错误但不阻止窗口关闭
                _logger?.LogError(ex, "处理窗口关闭事件时发生错误");
            }
            finally
            {
                // 调用基类方法
                base.OnClosed(e);
            }
        }
    }
} 