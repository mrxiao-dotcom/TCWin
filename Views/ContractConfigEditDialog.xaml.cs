using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views
{
    public partial class ContractConfigEditDialog : Window
    {
        private readonly ILogger? _logger;
        private ContractConfigViewModel _originalConfig;
        private ContractConfigViewModel _editedConfig;
        private AutoMonitorConfig? _baseConfig;
        private List<ComboBox> _pushTierComboBoxes = new List<ComboBox>();
        private List<ComboBox> _profitTierComboBoxes = new List<ComboBox>();
        private List<TextBox> _pushTierAmountTextBoxes = new List<TextBox>();
        private List<TextBox> _pushTierProfitProtectionTextBoxes = new List<TextBox>();
        private List<TextBox> _profitTierAmountTextBoxes = new List<TextBox>();

        public ContractConfigViewModel EditedConfig => _editedConfig;
        public bool IsConfirmed { get; private set; } = false;

        public ContractConfigEditDialog(ContractConfigViewModel config, AutoMonitorConfig? baseConfig = null, ILogger? logger = null)
        {
            InitializeComponent();
            _logger = logger;
            _originalConfig = config;
            _editedConfig = CloneConfig(config);
            _baseConfig = baseConfig;
            
            LoadConfigData();
        }

        private void LoadConfigData()
        {
            try
            {
                // 设置窗口标题
                TitleTextBlock.Text = $"编辑合约配置 - {_originalConfig.ContractName}";
                
                // 设置基本信息
                ContractNameText.Text = _originalConfig.ContractName;
                
                // 设置浮盈显示
                var pnlText = _originalConfig.CurrentPnl >= 0 
                    ? $"+{_originalConfig.CurrentPnl:F2} USDT" 
                    : $"{_originalConfig.CurrentPnl:F2} USDT";
                CurrentPnlText.Text = pnlText;
                CurrentPnlText.Foreground = _originalConfig.CurrentPnl >= 0 
                    ? Brushes.Green 
                    : Brushes.Red;

                // 🔧 关键修复：优先从原始配置中读取最新状态（包含手动修改）
                BreakEvenTargetTextBox.Text = _editedConfig.BreakEvenTarget.ToString("F2");
                
                // 🔧 重要：先使用原始配置中的状态（可能包含手动修改）
                var initialBreakEvenStatus = _originalConfig.BreakEvenStatus;
                
                // 🔧 然后从本地文件加载已保存的配置（可能覆盖）
                LoadSavedContractConfig();
                
                // 🔧 如果原始配置中有手动修改的状态，优先使用
                if (!string.IsNullOrEmpty(initialBreakEvenStatus) && initialBreakEvenStatus != "-")
                {
                    _editedConfig.BreakEvenStatus = initialBreakEvenStatus;
                    _logger?.LogInformation($"🔧 检测到手动修改的保本状态，优先使用: {initialBreakEvenStatus}");
                }
                
                // 设置状态选择（现在使用最终确定的状态）
                SetComboBoxSelection(BreakEvenStatusComboBox, _editedConfig.BreakEvenStatus);

                // 动态生成推仓配置控件
                CreatePushTierControls();

                // 动态生成保盈配置控件
                CreateProfitTierControls();

                _logger?.LogInformation($"已加载合约配置数据: {_editedConfig.ContractName}，保本状态: {_editedConfig.BreakEvenStatus}");
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

        /// <summary>
        /// 🔧 新增：设置已保存的推仓状态到下拉框
        /// </summary>
        private void SetSavedPushTierStatuses()
        {
            try
            {
                var statuses = new[] { _editedConfig.PushTier1Status, _editedConfig.PushTier2Status, _editedConfig.PushTier3Status, _editedConfig.PushTier4Status };
                
                for (int i = 0; i < Math.Min(_pushTierComboBoxes.Count, statuses.Length); i++)
                {
                    var comboBox = _pushTierComboBoxes[i];
                    var status = statuses[i];
                    
                    if (!string.IsNullOrEmpty(status) && status != "-")
                    {
                        SetComboBoxSelection(comboBox, status);
                        _logger?.LogDebug($"🔄 设置推仓T{i+1}状态: {status}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "设置推仓状态失败");
            }
        }

        /// <summary>
        /// 🔧 新增：设置已保存的保盈状态到下拉框
        /// </summary>
        private void SetSavedProfitTierStatuses()
        {
            try
            {
                var statuses = new[] { _editedConfig.ProfitTier1Status, _editedConfig.ProfitTier2Status, _editedConfig.ProfitTier3Status };
                
                for (int i = 0; i < Math.Min(_profitTierComboBoxes.Count, statuses.Length); i++)
                {
                    var comboBox = _profitTierComboBoxes[i];
                    var status = statuses[i];
                    
                    if (!string.IsNullOrEmpty(status) && status != "-")
                    {
                        SetComboBoxSelection(comboBox, status);
                        _logger?.LogDebug($"🔄 设置保盈T{i+1}状态: {status}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "设置保盈状态失败");
            }
        }

        /// <summary>
        /// 动态创建推仓配置控件
        /// </summary>
        private void CreatePushTierControls()
        {
            PushConfigPanel.Children.Clear();
            _pushTierComboBoxes.Clear();
            _pushTierAmountTextBoxes.Clear();
            _pushTierProfitProtectionTextBoxes.Clear();

            if (_baseConfig?.AddPositionConfig?.IsEnabled == true && _baseConfig.AddPositionConfig.Tiers.Any())
            {
                foreach (var tier in _baseConfig.AddPositionConfig.Tiers.OrderBy(t => t.TierIndex))
                {
                    var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // 档位标签
                    var label = new TextBlock 
                    { 
                        Text = $"推仓{tier.TierIndex}档：", 
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 5, 0)
                    };
                    Grid.SetColumn(label, 0);
                    grid.Children.Add(label);

                    // 触发金额输入框
                    var amountTextBox = new TextBox 
                    { 
                        Text = tier.TriggerProfitAmount.ToString("F2"),
                        Width = 80,
                        Margin = new Thickness(0, 0, 5, 0),
                        Tag = tier.TierIndex
                    };
                    Grid.SetColumn(amountTextBox, 1);
                    grid.Children.Add(amountTextBox);
                    _pushTierAmountTextBoxes.Add(amountTextBox);

                    // 保盈金额输入框
                    var profitProtectionTextBox = new TextBox 
                    { 
                        Text = tier.ProfitProtectionAmount.ToString("F2"),
                        Width = 80,
                        Margin = new Thickness(0, 0, 5, 0),
                        Tag = tier.TierIndex
                    };
                    Grid.SetColumn(profitProtectionTextBox, 2);
                    grid.Children.Add(profitProtectionTextBox);
                    _pushTierProfitProtectionTextBoxes.Add(profitProtectionTextBox);

                    // 状态下拉框
                    var statusComboBox = CreateStatusComboBox();
                    statusComboBox.Tag = tier.TierIndex;
                    Grid.SetColumn(statusComboBox, 3);
                    grid.Children.Add(statusComboBox);
                    _pushTierComboBoxes.Add(statusComboBox);

                    // USDT标签
                    var usdtLabel = new TextBlock 
                    { 
                        Text = "USDT",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    Grid.SetColumn(usdtLabel, 4);
                    grid.Children.Add(usdtLabel);

                    PushConfigPanel.Children.Add(grid);
                }
                
                // 🔧 关键修复：设置已保存的推仓状态
                SetSavedPushTierStatuses();
            }
            else
            {
                var noDataLabel = new TextBlock 
                { 
                    Text = "当前基础配置未启用推仓功能",
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 20, 0, 20)
                };
                PushConfigPanel.Children.Add(noDataLabel);
            }
        }

        /// <summary>
        /// 动态创建保盈配置控件
        /// </summary>
        private void CreateProfitTierControls()
        {
            ProfitConfigPanel.Children.Clear();
            _profitTierComboBoxes.Clear();
            _profitTierAmountTextBoxes.Clear();

            if (_baseConfig?.ProfitProtectionConfig?.IsEnabled == true && _baseConfig.ProfitProtectionConfig.Tiers.Any())
            {
                foreach (var tier in _baseConfig.ProfitProtectionConfig.Tiers.OrderBy(t => t.TierIndex))
                {
                    var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // 档位标签
                    var label = new TextBlock 
                    { 
                        Text = $"保盈{tier.TierIndex}档：", 
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 5, 0)
                    };
                    Grid.SetColumn(label, 0);
                    grid.Children.Add(label);

                    // 触发金额输入框
                    var amountTextBox = new TextBox 
                    { 
                        Text = tier.TriggerProfitAmount.ToString("F2"),
                        Width = 80,
                        Margin = new Thickness(0, 0, 5, 0),
                        Tag = tier.TierIndex
                    };
                    Grid.SetColumn(amountTextBox, 1);
                    grid.Children.Add(amountTextBox);
                    _profitTierAmountTextBoxes.Add(amountTextBox);

                    // 状态下拉框
                    var statusComboBox = CreateStatusComboBox();
                    statusComboBox.Tag = tier.TierIndex;
                    Grid.SetColumn(statusComboBox, 2);
                    grid.Children.Add(statusComboBox);
                    _profitTierComboBoxes.Add(statusComboBox);

                    // USDT标签
                    var usdtLabel = new TextBlock 
                    { 
                        Text = "USDT",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    Grid.SetColumn(usdtLabel, 3);
                    grid.Children.Add(usdtLabel);

                    ProfitConfigPanel.Children.Add(grid);
                }
                
                // 🔧 关键修复：设置已保存的保盈状态
                SetSavedProfitTierStatuses();
            }
            else
            {
                var noDataLabel = new TextBlock 
                { 
                    Text = "当前基础配置未启用保盈功能",
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 20, 0, 20)
                };
                ProfitConfigPanel.Children.Add(noDataLabel);
            }
        }

        /// <summary>
        /// 创建状态下拉框（仅手工设置需要的状态）
        /// </summary>
        private ComboBox CreateStatusComboBox()
        {
            var comboBox = new ComboBox { Width = 80, Margin = new Thickness(0, 0, 5, 0) };
            
            // 🔧 修复：手工设置只需要两个状态，移除"执行中"
            var item1 = new ComboBoxItem { Content = "未触发", Tag = "-" };
            var item2 = new ComboBoxItem { Content = "已执行", Tag = "√" };
            
            comboBox.Items.Add(item1);
            comboBox.Items.Add(item2);
            
            comboBox.SelectedItem = item1; // 默认选择"未触发"
            
            return comboBox;
        }

        /// <summary>
        /// 从本地文件加载已保存的合约配置
        /// </summary>
        private void LoadSavedContractConfig()
        {
            try
            {
                var configPath = GetContractConfigFilePath();
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var savedConfigs = JsonSerializer.Deserialize<Dictionary<string, ContractConfigData>>(json);
                    
                    if (savedConfigs != null && savedConfigs.TryGetValue(_originalConfig.ContractName, out var savedConfig))
                    {
                        // 应用已保存的配置到当前编辑配置
                        ApplySavedConfig(savedConfig);
                        _logger?.LogInformation($"从本地文件加载了合约配置: {_originalConfig.ContractName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "加载本地合约配置失败，使用默认配置");
            }
        }

        /// <summary>
        /// 应用已保存的配置
        /// </summary>
        private void ApplySavedConfig(ContractConfigData savedConfig)
        {
            _editedConfig.BreakEvenTarget = savedConfig.BreakEvenTarget;
            _editedConfig.BreakEvenStatus = savedConfig.BreakEvenStatus;
            
            // 应用推仓状态
            var pushStatuses = new[] { savedConfig.PushTier1Status, savedConfig.PushTier2Status, savedConfig.PushTier3Status, savedConfig.PushTier4Status };
            for (int i = 0; i < pushStatuses.Length && i < 4; i++)
            {
                switch (i)
                {
                    case 0: _editedConfig.PushTier1Status = pushStatuses[i]; break;
                    case 1: _editedConfig.PushTier2Status = pushStatuses[i]; break;
                    case 2: _editedConfig.PushTier3Status = pushStatuses[i]; break;
                    case 3: _editedConfig.PushTier4Status = pushStatuses[i]; break;
                }
            }

            // 应用保盈状态
            var profitStatuses = new[] { savedConfig.ProfitTier1Status, savedConfig.ProfitTier2Status, savedConfig.ProfitTier3Status };
            for (int i = 0; i < profitStatuses.Length && i < 3; i++)
            {
                switch (i)
                {
                    case 0: _editedConfig.ProfitTier1Status = profitStatuses[i]; break;
                    case 1: _editedConfig.ProfitTier2Status = profitStatuses[i]; break;
                    case 2: _editedConfig.ProfitTier3Status = profitStatuses[i]; break;
                }
            }
        }

        /// <summary>
        /// 保存合约配置到本地文件
        /// </summary>
        private void SaveContractConfigToFile()
        {
            try
            {
                var configPath = GetContractConfigFilePath();
                var directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                Dictionary<string, ContractConfigData> allConfigs;
                
                // 读取现有配置
                if (File.Exists(configPath))
                {
                    var existingJson = File.ReadAllText(configPath);
                    allConfigs = JsonSerializer.Deserialize<Dictionary<string, ContractConfigData>>(existingJson) ?? new Dictionary<string, ContractConfigData>();
                }
                else
                {
                    allConfigs = new Dictionary<string, ContractConfigData>();
                }

                // 🔧 修复：创建包含完整配置数据的对象
                var configData = new ContractConfigData
                {
                    ContractName = _editedConfig.ContractName,
                    
                    // 保本配置
                    BreakEvenTarget = _editedConfig.BreakEvenTarget,
                    BreakEvenStatus = _editedConfig.BreakEvenStatus,
                    
                    // 推仓配置 - 从基础配置和用户输入获取
                    PushTier1Amount = GetPushTierAmount(1),
                    PushTier1Status = _editedConfig.PushTier1Status,
                    PushTier2Amount = GetPushTierAmount(2),
                    PushTier2Status = _editedConfig.PushTier2Status,
                    PushTier3Amount = GetPushTierAmount(3),
                    PushTier3Status = _editedConfig.PushTier3Status,
                    PushTier4Amount = GetPushTierAmount(4),
                    PushTier4Status = _editedConfig.PushTier4Status,
                    
                    // 保盈配置 - 从基础配置获取
                    ProfitTier1TriggerAmount = GetProfitTierTriggerAmount(1),
                    ProfitTier1ProtectionAmount = GetProfitTierProtectionAmount(1),
                    ProfitTier1Status = _editedConfig.ProfitTier1Status,
                    ProfitTier2TriggerAmount = GetProfitTierTriggerAmount(2),
                    ProfitTier2ProtectionAmount = GetProfitTierProtectionAmount(2),
                    ProfitTier2Status = _editedConfig.ProfitTier2Status,
                    ProfitTier3TriggerAmount = GetProfitTierTriggerAmount(3),
                    ProfitTier3ProtectionAmount = GetProfitTierProtectionAmount(3),
                    ProfitTier3Status = _editedConfig.ProfitTier3Status,
                    
                    LastModified = DateTime.Now
                };

                allConfigs[_editedConfig.ContractName] = configData;

                // 保存到文件
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(allConfigs, options);
                File.WriteAllText(configPath, json);

                _logger?.LogInformation($"✅ 已保存完整合约配置到本地文件: {_editedConfig.ContractName}");
                _logger?.LogInformation($"   保本: {configData.BreakEvenTarget}U ({configData.BreakEvenStatus})");
                _logger?.LogInformation($"   推仓: T1={configData.PushTier1Amount}U, T2={configData.PushTier2Amount}U, T3={configData.PushTier3Amount}U, T4={configData.PushTier4Amount}U");
                _logger?.LogInformation($"   保盈: T1={configData.ProfitTier1TriggerAmount}|{configData.ProfitTier1ProtectionAmount}U, T2={configData.ProfitTier2TriggerAmount}|{configData.ProfitTier2ProtectionAmount}U, T3={configData.ProfitTier3TriggerAmount}|{configData.ProfitTier3ProtectionAmount}U");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存合约配置到本地文件失败");
                throw;
            }
        }

        /// <summary>
        /// 获取推仓阶梯的触发金额
        /// </summary>
        private decimal GetPushTierAmount(int tierIndex)
        {
            try
            {
                // 优先从用户输入获取
                if (_pushTierAmountTextBoxes.Count >= tierIndex)
                {
                    var textBox = _pushTierAmountTextBoxes[tierIndex - 1];
                    if (decimal.TryParse(textBox.Text, out decimal amount))
                    {
                        return amount;
                    }
                }
                
                // 从基础配置获取
                if (_baseConfig?.AddPositionConfig?.IsEnabled == true)
                {
                    var tier = _baseConfig.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
                    if (tier != null)
                    {
                        return tier.TriggerProfitAmount;
                    }
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取保盈阶梯的触发金额
        /// </summary>
        private decimal GetProfitTierTriggerAmount(int tierIndex)
        {
            try
            {
                // 优先从用户输入获取
                if (_profitTierAmountTextBoxes.Count >= tierIndex)
                {
                    var textBox = _profitTierAmountTextBoxes[tierIndex - 1];
                    if (decimal.TryParse(textBox.Text, out decimal amount))
                    {
                        return amount;
                    }
                }
                
                // 从基础配置获取
                if (_baseConfig?.ProfitProtectionConfig?.IsEnabled == true)
                {
                    var tier = _baseConfig.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
                    if (tier != null)
                    {
                        return tier.TriggerProfitAmount;
                    }
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取保盈阶梯的保护金额
        /// </summary>
        private decimal GetProfitTierProtectionAmount(int tierIndex)
        {
            try
            {
                // 从基础配置获取（保护金额通常不由用户单独修改）
                if (_baseConfig?.ProfitProtectionConfig?.IsEnabled == true)
                {
                    var tier = _baseConfig.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
                    if (tier != null)
                    {
                        return tier.ProtectionAmount;
                    }
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取合约配置文件路径
        /// </summary>
        private string GetContractConfigFilePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "BinanceFuturesTrader", "ContractConfigs.json");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证保本目标金额输入
                if (!decimal.TryParse(BreakEvenTargetTextBox.Text, out decimal breakEvenTarget) || breakEvenTarget < 0)
                {
                    MessageBox.Show("保本目标金额必须是有效的非负数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    BreakEvenTargetTextBox.Focus();
                    return;
                }

                // 验证推仓金额输入
                foreach (var textBox in _pushTierAmountTextBoxes)
                {
                    if (!decimal.TryParse(textBox.Text, out decimal amount) || amount < 0)
                    {
                        MessageBox.Show($"推仓{textBox.Tag}档触发金额必须是有效的非负数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        textBox.Focus();
                        return;
                    }
                }

                // 验证保盈金额输入
                foreach (var textBox in _profitTierAmountTextBoxes)
                {
                    if (!decimal.TryParse(textBox.Text, out decimal amount) || amount < 0)
                    {
                        MessageBox.Show($"保盈{textBox.Tag}档触发金额必须是有效的非负数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        textBox.Focus();
                        return;
                    }
                }

                // 更新编辑后的配置
                _editedConfig.BreakEvenTarget = breakEvenTarget;
                _editedConfig.BreakEvenStatus = GetComboBoxSelection(BreakEvenStatusComboBox);
                
                // 更新推仓状态
                for (int i = 0; i < _pushTierComboBoxes.Count && i < 4; i++)
                {
                    var status = GetComboBoxSelection(_pushTierComboBoxes[i]);
                    switch (i)
                    {
                        case 0: _editedConfig.PushTier1Status = status; break;
                        case 1: _editedConfig.PushTier2Status = status; break;
                        case 2: _editedConfig.PushTier3Status = status; break;
                        case 3: _editedConfig.PushTier4Status = status; break;
                    }
                }

                // 更新保盈状态
                for (int i = 0; i < _profitTierComboBoxes.Count && i < 3; i++)
                {
                    var status = GetComboBoxSelection(_profitTierComboBoxes[i]);
                    switch (i)
                    {
                        case 0: _editedConfig.ProfitTier1Status = status; break;
                        case 1: _editedConfig.ProfitTier2Status = status; break;
                        case 2: _editedConfig.ProfitTier3Status = status; break;
                    }
                }

                _editedConfig.UpdateTime = DateTime.Now.ToString("HH:mm:ss");

                // 保存到本地文件
                SaveContractConfigToFile();

                IsConfirmed = true;
                _logger?.LogInformation($"保存合约配置: {_editedConfig.ContractName}");

                // 显示保存成功的消息
                MessageBox.Show($"合约配置已保存！\n\n✅ 合约: {_editedConfig.ContractName}\n📊 保本目标: {_editedConfig.BreakEvenTarget:F2} USDT\n⏰ 更新时间: {_editedConfig.UpdateTime}\n💾 已保存到本地文件", 
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
    }

    /// <summary>
    /// 合约配置数据存储结构
    /// </summary>
    public class ContractConfigData
    {
        public string ContractName { get; set; } = "";
        
        // 保本配置
        public decimal BreakEvenTarget { get; set; }
        public string BreakEvenStatus { get; set; } = "-";
        
        // 推仓配置 - 增加触发金额数据
        public decimal PushTier1Amount { get; set; }
        public string PushTier1Status { get; set; } = "-";
        public decimal PushTier2Amount { get; set; }
        public string PushTier2Status { get; set; } = "-";
        public decimal PushTier3Amount { get; set; }
        public string PushTier3Status { get; set; } = "-";
        public decimal PushTier4Amount { get; set; }
        public string PushTier4Status { get; set; } = "-";
        
        // 保盈配置 - 增加触发金额和保护金额数据
        public decimal ProfitTier1TriggerAmount { get; set; }
        public decimal ProfitTier1ProtectionAmount { get; set; }
        public string ProfitTier1Status { get; set; } = "-";
        public decimal ProfitTier2TriggerAmount { get; set; }
        public decimal ProfitTier2ProtectionAmount { get; set; }
        public string ProfitTier2Status { get; set; } = "-";
        public decimal ProfitTier3TriggerAmount { get; set; }
        public decimal ProfitTier3ProtectionAmount { get; set; }
        public string ProfitTier3Status { get; set; } = "-";
        
        public DateTime LastModified { get; set; }
    }
} 