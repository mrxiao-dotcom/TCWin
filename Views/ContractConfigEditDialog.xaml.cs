using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
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
            
            // 🔧 修复：添加三个状态选项，包括执行中
            var item1 = new ComboBoxItem { Content = StatusConstants.Waiting, Tag = StatusConstants.WaitingSymbol };
            var item2 = new ComboBoxItem { Content = StatusConstants.Executing, Tag = "⚡" }; // 执行中用闪电符号
            var item3 = new ComboBoxItem { Content = StatusConstants.Executed, Tag = StatusConstants.ExecutedSymbol };
            
            comboBox.Items.Add(item1);
            comboBox.Items.Add(item2);
            comboBox.Items.Add(item3);
            
            comboBox.SelectedItem = item1; // 默认选择"未触发"
            
            return comboBox;
        }

        /// <summary>
        /// 从统一状态文件加载已保存的合约配置
        /// </summary>
        private void LoadSavedContractConfig()
        {
            try
            {
                // 🔧 修复：使用统一状态管理服务加载配置
                var filePathManager = new FilePathManager();
                var currentAccount = filePathManager.GetCurrentAccountName();
                var configManager = BaseConfigManager.Instance;
                var typedLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ContractMonitoringStateService>.Instance;
                var stateService = new ContractMonitoringStateService(typedLogger, configManager, filePathManager, currentAccount);

                var contractKey = _originalConfig.ContractName.Replace(" ", "_"); // 将 "BTCUSDT LONG" 转换为 "BTCUSDT_LONG"
                var state = stateService.GetMonitoringState(contractKey);
                
                _logger?.LogInformation($"🔍 尝试加载状态: 原始={_originalConfig.ContractName}, 标准化={contractKey}");
                
                if (state != null)
                {
                    // 应用已保存的状态到当前编辑配置
                    ApplySavedStateFromUnifiedFile(state);
                    _logger?.LogInformation($"✅ 从统一状态文件加载了合约配置: {contractKey}");
                }
                else
                {
                    _logger?.LogInformation($"📋 未找到合约状态记录，使用默认配置: {contractKey}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "加载合约状态失败，使用默认配置");
            }
        }

        /// <summary>
        /// 应用已保存的配置（旧格式兼容）
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
        /// 从统一状态文件应用已保存的状态
        /// </summary>
        private void ApplySavedStateFromUnifiedFile(ContractMonitoringState state)
        {
            // 应用保本状态
            _editedConfig.BreakEvenStatus = state.BreakEvenConfig.IsExecuted ? "✓" : "-";
            
            // 应用推仓状态
            var pushTiers = state.AddPositionConfig.Tiers.OrderBy(t => t.TierIndex).Take(4).ToArray();
            for (int i = 0; i < pushTiers.Length; i++)
            {
                var status = pushTiers[i].IsExecuted ? "✓" : "-";
                switch (i)
                {
                    case 0: _editedConfig.PushTier1Status = status; break;
                    case 1: _editedConfig.PushTier2Status = status; break;
                    case 2: _editedConfig.PushTier3Status = status; break;
                    case 3: _editedConfig.PushTier4Status = status; break;
                }
            }

            // 应用保盈状态
            var profitTiers = state.ProfitProtectionConfig.Tiers.OrderBy(t => t.TierIndex).Take(3).ToArray();
            for (int i = 0; i < profitTiers.Length; i++)
            {
                var status = profitTiers[i].IsExecuted ? "✓" : "-";
                switch (i)
                {
                    case 0: _editedConfig.ProfitTier1Status = status; break;
                    case 1: _editedConfig.ProfitTier2Status = status; break;
                    case 2: _editedConfig.ProfitTier3Status = status; break;
                }
            }
            
            _logger?.LogInformation($"📋 应用统一状态: 保本={_editedConfig.BreakEvenStatus}, 推仓=T1:{_editedConfig.PushTier1Status}|T2:{_editedConfig.PushTier2Status}|T3:{_editedConfig.PushTier3Status}|T4:{_editedConfig.PushTier4Status}, 保盈=T1:{_editedConfig.ProfitTier1Status}|T2:{_editedConfig.ProfitTier2Status}|T3:{_editedConfig.ProfitTier3Status}");
        }

        /// <summary>
        /// 保存合约配置到本地文件 - 使用统一状态管理正确更新状态
        /// </summary>
        private void SaveContractConfigToFile()
        {
            try
            {
                // 🔧 修复：使用正确的统一状态管理服务
                var filePathManager = new FilePathManager();
                var currentAccount = filePathManager.GetCurrentAccountName();
                var configManager = BaseConfigManager.Instance;
                var typedLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ContractMonitoringStateService>.Instance;
                var stateService = new ContractMonitoringStateService(typedLogger, configManager, filePathManager, currentAccount);

                // 🔧 修复：解析合约名称获取正确的合约键格式（确保使用下划线格式）
                var contractKey = _editedConfig.ContractName.Replace(" ", "_"); // 将 "BTCUSDT LONG" 转换为 "BTCUSDT_LONG"
                
                _logger?.LogInformation($"🔧 开始更新合约状态: {contractKey}");
                _logger?.LogInformation($"🔧 原始合约名: {_editedConfig.ContractName}");
                _logger?.LogInformation($"🔧 标准化合约键: {contractKey}");
                _logger?.LogInformation($"🔧 当前账号: {currentAccount}");
                _logger?.LogInformation($"🔧 待更新状态: 保本={_editedConfig.BreakEvenStatus}, 推仓=T1:{_editedConfig.PushTier1Status}|T2:{_editedConfig.PushTier2Status}|T3:{_editedConfig.PushTier3Status}|T4:{_editedConfig.PushTier4Status}, 保盈=T1:{_editedConfig.ProfitTier1Status}|T2:{_editedConfig.ProfitTier2Status}|T3:{_editedConfig.ProfitTier3Status}");
                
                // 更新保本状态（处理三种状态：waiting, executing, executed）
                if (_editedConfig.BreakEvenStatus == "✓")
                {
                    stateService.UpdateExecutionStatus(contractKey, "BreakEven", null, true, 0, "手动设置为executed");
                    _logger?.LogInformation($"   ✅ 保本状态更新为executed");
                }
                else if (_editedConfig.BreakEvenStatus == "⚡")
                {
                    // 需要特殊处理executing状态 - 暂时使用executing标记
                    stateService.UpdateExecutionStatusToExecuting(contractKey, "BreakEven", null, 0, "手动设置为executing");
                    _logger?.LogInformation($"   ⚡ 保本状态更新为executing");
                }
                else if (_editedConfig.BreakEvenStatus == "-")
                {
                    stateService.UpdateExecutionStatus(contractKey, "BreakEven", null, false, 0, "手动重置为waiting");
                    _logger?.LogInformation($"   🔄 保本状态重置为waiting");
                }
                
                // 更新推仓状态（处理三种状态：waiting, executing, executed）
                var pushStatuses = new[] { _editedConfig.PushTier1Status, _editedConfig.PushTier2Status, _editedConfig.PushTier3Status, _editedConfig.PushTier4Status };
                for (int i = 0; i < pushStatuses.Length; i++)
                {
                    if (pushStatuses[i] == "✓")
                    {
                        stateService.UpdateExecutionStatus(contractKey, "AddPosition", i + 1, true, 0, "手动设置为executed");
                        _logger?.LogInformation($"   ✅ 推仓阶梯{i + 1}状态更新为executed");
                    }
                    else if (pushStatuses[i] == "⚡")
                    {
                        stateService.UpdateExecutionStatusToExecuting(contractKey, "AddPosition", i + 1, 0, "手动设置为executing");
                        _logger?.LogInformation($"   ⚡ 推仓阶梯{i + 1}状态更新为executing");
                    }
                    else if (pushStatuses[i] == "-")
                    {
                        stateService.UpdateExecutionStatus(contractKey, "AddPosition", i + 1, false, 0, "手动重置为waiting");
                        _logger?.LogInformation($"   🔄 推仓阶梯{i + 1}状态重置为waiting");
                    }
                }
                
                // 更新保盈状态（处理三种状态：waiting, executing, executed）
                var profitStatuses = new[] { _editedConfig.ProfitTier1Status, _editedConfig.ProfitTier2Status, _editedConfig.ProfitTier3Status };
                for (int i = 0; i < profitStatuses.Length; i++)
                {
                    if (profitStatuses[i] == "✓")
                    {
                        stateService.UpdateExecutionStatus(contractKey, "ProfitProtection", i + 1, true, 0, "手动设置为executed");
                        _logger?.LogInformation($"   ✅ 保盈阶梯{i + 1}状态更新为executed");
                    }
                    else if (profitStatuses[i] == "⚡")
                    {
                        stateService.UpdateExecutionStatusToExecuting(contractKey, "ProfitProtection", i + 1, 0, "手动设置为executing");
                        _logger?.LogInformation($"   ⚡ 保盈阶梯{i + 1}状态更新为executing");
                    }
                    else if (profitStatuses[i] == "-")
                    {
                        stateService.UpdateExecutionStatus(contractKey, "ProfitProtection", i + 1, false, 0, "手动重置为waiting");
                        _logger?.LogInformation($"   🔄 保盈阶梯{i + 1}状态重置为waiting");
                    }
                }

                _logger?.LogInformation($"✅ 合约状态更新完成: {contractKey}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "更新合约状态失败");
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
        /// 获取合约配置文件路径 - 已废弃：现在使用统一状态管理
        /// </summary>
        [Obsolete("已废弃：不再使用ContractConfigs.json文件，现在使用ContractMonitoringStateService进行统一状态管理")]
        private string GetContractConfigFilePath()
        {
            // 已废弃：返回空路径
            return string.Empty;
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