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
using System.Threading.Tasks; // Added for Task.CompletedTask

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
        
        // 🔧 【紧急修复】动态状态存储，支持推仓5-7和更多保盈阶梯
        private Dictionary<int, string> _extendedPushTierStatuses = new Dictionary<int, string>();
        private Dictionary<int, string> _extendedProfitTierStatuses = new Dictionary<int, string>();
        
        // 🔧 新增：增强版数据管理器支持
        private bool _useEnhancedManager = false;
        private AutoMonitorDataManager? _dataManager = null;

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
        
        // 🔧 【紧急修复】动态状态访问方法
        
        /// <summary>
        /// 获取推仓状态（支持动态阶梯）
        /// </summary>
        private string GetPushTierStatus(int tierIndex)
        {
            // 先从扩展字典获取
            if (_extendedPushTierStatuses.TryGetValue(tierIndex, out var extendedStatus))
                return extendedStatus;
                
            // 向后兼容：从固定属性获取
            return tierIndex switch
            {
                1 => _editedConfig.PushTier1Status,
                2 => _editedConfig.PushTier2Status,
                3 => _editedConfig.PushTier3Status,
                4 => _editedConfig.PushTier4Status,
                _ => "-"
            };
        }
        
        /// <summary>
        /// 设置推仓状态（支持动态阶梯）
        /// </summary>
        private void SetPushTierStatus(int tierIndex, string status)
        {
            // 同时更新扩展字典和固定属性
            _extendedPushTierStatuses[tierIndex] = status;
            
            // 向后兼容：同步到固定属性
            switch (tierIndex)
            {
                case 1: _editedConfig.PushTier1Status = status; break;
                case 2: _editedConfig.PushTier2Status = status; break;
                case 3: _editedConfig.PushTier3Status = status; break;
                case 4: _editedConfig.PushTier4Status = status; break;
            }
        }
        
        /// <summary>
        /// 获取保盈状态（支持动态阶梯）
        /// </summary>
        private string GetProfitTierStatus(int tierIndex)
        {
            if (_extendedProfitTierStatuses.TryGetValue(tierIndex, out var extendedStatus))
                return extendedStatus;
                
            return tierIndex switch
            {
                1 => _editedConfig.ProfitTier1Status,
                2 => _editedConfig.ProfitTier2Status,
                3 => _editedConfig.ProfitTier3Status,
                _ => "-"
            };
        }
        
        /// <summary>
        /// 设置保盈状态（支持动态阶梯）
        /// </summary>
        private void SetProfitTierStatus(int tierIndex, string status)
        {
            _extendedProfitTierStatuses[tierIndex] = status;
            
            switch (tierIndex)
            {
                case 1: _editedConfig.ProfitTier1Status = status; break;
                case 2: _editedConfig.ProfitTier2Status = status; break;
                case 3: _editedConfig.ProfitTier3Status = status; break;
            }
        }

        /// <summary>
        /// 启用增强版数据管理器
        /// </summary>
        public void EnableEnhancedDataManager(AutoMonitorDataManager dataManager)
        {
            try
            {
                _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
                _useEnhancedManager = true;
                
                _logger?.LogInformation("🚀 ContractConfigEditDialog 已启用增强版数据管理器");
                
                // 重新加载配置数据
                LoadConfigData();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 启用增强版数据管理器失败");
                throw;
            }
        }

        /// <summary>
        /// 检查是否使用增强版管理器
        /// </summary>
        private bool IsUsingEnhancedManager => _useEnhancedManager && _dataManager != null;

        /// <summary>
        /// 从增强版数据管理器的ContractState应用状态到编辑配置
        /// </summary>
        private void ApplySavedStateFromEnhancedManager(ContractState contractState)
        {
            try
            {
                // 应用基本信息
                _editedConfig.BreakEvenTarget = contractState.ExecutionStates.Breakeven.TriggerAmount;
                _editedConfig.BreakEvenStatus = ConvertExecutionStateToStatus(contractState.ExecutionStates.Breakeven.State);

                // 应用推仓状态
                var pushTiers = contractState.ExecutionStates.AddPositionTiers.OrderBy(t => t.TierIndex).Take(4).ToList();
                for (int i = 0; i < 4; i++)
                {
                    var status = i < pushTiers.Count ? ConvertExecutionStateToStatus(pushTiers[i].State) : "-";
                    switch (i)
                    {
                        case 0: _editedConfig.PushTier1Status = status; break;
                        case 1: _editedConfig.PushTier2Status = status; break;
                        case 2: _editedConfig.PushTier3Status = status; break;
                        case 3: _editedConfig.PushTier4Status = status; break;
                    }
                }

                // 应用保盈状态
                var profitTiers = contractState.ExecutionStates.ProfitProtectionTiers.OrderBy(t => t.TierIndex).Take(3).ToList();
                for (int i = 0; i < 3; i++)
                {
                    var status = i < profitTiers.Count ? ConvertExecutionStateToStatus(profitTiers[i].State) : "-";
                    switch (i)
                    {
                        case 0: _editedConfig.ProfitTier1Status = status; break;
                        case 1: _editedConfig.ProfitTier2Status = status; break;
                        case 2: _editedConfig.ProfitTier3Status = status; break;
                    }
                }

                _logger?.LogInformation($"📋 应用增强版状态: 保本={_editedConfig.BreakEvenStatus}, 推仓=T1:{_editedConfig.PushTier1Status}|T2:{_editedConfig.PushTier2Status}|T3:{_editedConfig.PushTier3Status}|T4:{_editedConfig.PushTier4Status}, 保盈=T1:{_editedConfig.ProfitTier1Status}|T2:{_editedConfig.ProfitTier2Status}|T3:{_editedConfig.ProfitTier3Status}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "应用增强版状态失败");
            }
        }

        /// <summary>
        /// 将新的执行状态转换为旧的状态字符串
        /// </summary>
        private string ConvertExecutionStateToStatus(string executionState)
        {
            return executionState switch
            {
                ExecutionStateTypes.NotTriggered => "-",
                ExecutionStateTypes.Executing => "执行中",
                ExecutionStateTypes.Executed => "√",
                _ => "-"
            };
        }

        /// <summary>
        /// 将旧的状态字符串转换为新的执行状态
        /// </summary>
        private string ConvertStatusToExecutionState(string status)
        {
            return status switch
            {
                "-" => ExecutionStateTypes.NotTriggered,
                "执行中" => ExecutionStateTypes.Executing,
                "√" => ExecutionStateTypes.Executed,
                _ => ExecutionStateTypes.NotTriggered
            };
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
        /// 🔧 【动态修复】设置已保存的推仓状态到下拉框，支持推仓5-7及更多
        /// </summary>
        private void SetSavedPushTierStatuses()
        {
            try
            {
                _logger?.LogCritical($"🔧【UI加载】开始设置推仓状态到UI，控件数量: {_pushTierComboBoxes.Count}");
                
                // 🔧 【动态设置】使用动态状态获取方法
                for (int i = 0; i < _pushTierComboBoxes.Count; i++)
                {
                    var comboBox = _pushTierComboBoxes[i];
                    var tierIndex = i + 1; // 阶梯从1开始
                    var status = GetPushTierStatus(tierIndex); // 使用动态获取方法
                    
                    SetComboBoxSelection(comboBox, status);
                    _logger?.LogCritical($"🔧【UI加载】推仓T{tierIndex}状态设置为: {status}");
                }
                
                _logger?.LogCritical($"🔧【UI加载】推仓状态设置完成，共设置 {_pushTierComboBoxes.Count} 个阶梯");
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
            
            // 🔧 【状态统一修复】只保留两个状态：未触发和已执行
            var item1 = new ComboBoxItem { Content = "未触发", Tag = "-" };
            var item2 = new ComboBoxItem { Content = "已执行", Tag = "√" };
            
            comboBox.Items.Add(item1);
            comboBox.Items.Add(item2);
            
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
                var contractKey = _originalConfig.ContractName.Replace(" ", "_"); // 将 "BTCUSDT LONG" 转换为 "BTCUSDT_LONG"
                
                if (IsUsingEnhancedManager)
                {
                    // 🔧 使用增强版数据管理器加载配置
                    _logger?.LogInformation($"🔄 从增强版数据管理器加载合约配置: {contractKey}");
                    
                    var contractState = _dataManager!.GetContractState(contractKey);
                    if (contractState != null)
                    {
                        // 应用增强版数据管理器的状态到当前编辑配置
                        ApplySavedStateFromEnhancedManager(contractState);
                        _logger?.LogInformation($"✅ 从增强版数据管理器加载了合约配置: {contractKey}");
                    }
                    else
                    {
                        _logger?.LogInformation($"📋 增强版数据管理器中未找到合约状态记录，使用默认配置: {contractKey}");
                    }
                }
                else
                {
                    // 🔧 修复：使用统一状态管理服务加载配置
                    var filePathManager = new FilePathManager();
                    var currentAccount = filePathManager.GetCurrentAccountName();
                    var configManager = BaseConfigManager.Instance;
                    var typedLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ContractMonitoringStateService>.Instance;
                    var stateService = new ContractMonitoringStateService(typedLogger, configManager, filePathManager, currentAccount);

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
<<<<<<< HEAD
            // 🔧 【状态统一修复】应用保本状态和触发金额，使用统一符号
            _editedConfig.BreakEvenStatus = state.BreakEvenConfig.IsExecuted ? "√" : "-";
            _editedConfig.BreakEvenTarget = state.BreakEvenConfig.TriggerProfitAmount;
            _logger?.LogInformation($"   📋 加载保本触发金额: {state.BreakEvenConfig.TriggerProfitAmount}");
            
            // 🔧 【动态修复】应用所有推仓状态，支持推仓5-7及更多
            var pushTiers = state.AddPositionConfig.Tiers.OrderBy(t => t.TierIndex).ToArray();
            _logger?.LogCritical($"🔧【动态加载】文件中推仓阶梯总数: {pushTiers.Length}");
            
            // 🔧 【关键】清空动态状态字典，重新加载
            _extendedPushTierStatuses.Clear();
=======
            // 应用保本状态
            switch (state.BreakEvenConfig.ExecutionState)
            {
                case ExecutionState.NotTriggered:
                    _editedConfig.BreakEvenStatus = "-";
                    break;
                case ExecutionState.Executing:
                    _editedConfig.BreakEvenStatus = "⚡";
                    break;
                case ExecutionState.Executed:
                    _editedConfig.BreakEvenStatus = "√";
                    break;
                default:
                    _editedConfig.BreakEvenStatus = "-";
                    break;
            }
>>>>>>> df3e9d4bd657da2e1fc523952fc0a2a313f8ef6b
            
            for (int i = 0; i < pushTiers.Length; i++)
            {
<<<<<<< HEAD
                var tier = pushTiers[i];
                var tierIndex = tier.TierIndex;
                var status = tier.IsExecuted ? "√" : "-";
                var tierAmount = tier.TriggerProfitAmount;

                // 🔧 【动态设置】使用动态方法设置状态
                SetPushTierStatus(tierIndex, status);
                
                _logger?.LogCritical($"🔧【动态加载】推仓T{tierIndex}: 状态={status}, 金额={tierAmount}");
=======
                string status;
                switch (pushTiers[i].ExecutionState)
                {
                    case ExecutionState.NotTriggered:
                        status = "-";
                        break;
                    case ExecutionState.Executing:
                        status = "⚡";
                        break;
                    case ExecutionState.Executed:
                        status = "√";
                        break;
                    default:
                        status = "-";
                        break;
                }
                switch (i)
                {
                    case 0: _editedConfig.PushTier1Status = status; break;
                    case 1: _editedConfig.PushTier2Status = status; break;
                    case 2: _editedConfig.PushTier3Status = status; break;
                    case 3: _editedConfig.PushTier4Status = status; break;
                }
>>>>>>> df3e9d4bd657da2e1fc523952fc0a2a313f8ef6b
            }

            // 🔧 【状态统一修复】应用保盈状态和触发金额，使用统一符号
            var profitTiers = state.ProfitProtectionConfig.Tiers.OrderBy(t => t.TierIndex).Take(3).ToArray();
            for (int i = 0; i < profitTiers.Length; i++)
            {
<<<<<<< HEAD
                var status = profitTiers[i].IsExecuted ? "√" : "-";
                var triggerAmount = profitTiers[i].TriggerProfitAmount;
                var protectionAmount = profitTiers[i].ProtectionAmount;
=======
                string status;
                switch (profitTiers[i].ExecutionState)
                {
                    case ExecutionState.NotTriggered:
                        status = "-";
                        break;
                    case ExecutionState.Executing:
                        status = "⚡";
                        break;
                    case ExecutionState.Executed:
                        status = "√";
                        break;
                    default:
                        status = "-";
                        break;
                }
>>>>>>> df3e9d4bd657da2e1fc523952fc0a2a313f8ef6b
                switch (i)
                {
                    case 0: 
                        _editedConfig.ProfitTier1Status = status; 
                        _editedConfig.ProfitTier1TriggerAmount = triggerAmount;
                        _editedConfig.ProfitTier1ProtectionAmount = protectionAmount;
                        break;
                    case 1: 
                        _editedConfig.ProfitTier2Status = status; 
                        _editedConfig.ProfitTier2TriggerAmount = triggerAmount;
                        _editedConfig.ProfitTier2ProtectionAmount = protectionAmount;
                        break;
                    case 2: 
                        _editedConfig.ProfitTier3Status = status; 
                        _editedConfig.ProfitTier3TriggerAmount = triggerAmount;
                        _editedConfig.ProfitTier3ProtectionAmount = protectionAmount;
                        break;
                }
                _logger?.LogInformation($"   📋 加载保盈T{i+1}: 状态={status}, 触发={triggerAmount}, 保护={protectionAmount}");
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
                _logger?.LogCritical($"🔥【SaveContractConfigToFile】方法被调用，合约: {_editedConfig?.ContractName}");
                _logger?.LogCritical($"🔥【SaveContractConfigToFile】保本状态: '{_editedConfig?.BreakEvenStatus}'");
                _logger?.LogCritical($"🔥【变化跟踪】准备进行精确一对一修改，避免状态串改");
                _logger?.LogCritical($"🔥【变化跟踪】当前编辑状态:");
                _logger?.LogCritical($"🔥【变化跟踪】  保本: 状态={_editedConfig?.BreakEvenStatus}, 金额={_editedConfig?.BreakEvenTarget}");
                _logger?.LogCritical($"🔥【变化跟踪】  推仓: T1={_editedConfig?.PushTier1Status}, T2={_editedConfig?.PushTier2Status}, T3={_editedConfig?.PushTier3Status}, T4={_editedConfig?.PushTier4Status}");
                _logger?.LogCritical($"🔥【变化跟踪】  保盈: T1={_editedConfig?.ProfitTier1Status}, T2={_editedConfig?.ProfitTier2Status}, T3={_editedConfig?.ProfitTier3Status}");
                
                var contractKey = _editedConfig.ContractName.Replace(" ", "_");
                
<<<<<<< HEAD
                if (IsUsingEnhancedManager)
=======
                // 更新保本状态（处理三种状态：waiting, executing, executed）
                if (_editedConfig.BreakEvenStatus == "√" || _editedConfig.BreakEvenStatus == "✓")
>>>>>>> df3e9d4bd657da2e1fc523952fc0a2a313f8ef6b
                {
                    // 🔧 使用增强版数据管理器保存配置
                    _logger?.LogInformation($"🔄 使用增强版数据管理器保存合约配置: {contractKey}");
                    SaveToEnhancedDataManager(contractKey);
                }
                else
                {
                    // 🔧 修复：使用正确的统一状态管理服务
                    var filePathManager = new FilePathManager();
                    var currentAccount = filePathManager.GetCurrentAccountName();
                    var configManager = BaseConfigManager.Instance;
                    
                    // 🔧 【关键修复】使用真正的Logger而不是NullLogger，确保能看到保存过程的详细日志
                    var typedLogger = _logger != null ? 
                        Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ContractMonitoringStateService>() :
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<ContractMonitoringStateService>.Instance;
                        
                    var stateService = new ContractMonitoringStateService(typedLogger, configManager, filePathManager, currentAccount);
                    
                    _logger?.LogCritical($"🔥【保存路径】使用传统状态服务，账户: {currentAccount}");
                    SaveToLegacyStateService(stateService, contractKey);
                }
<<<<<<< HEAD
=======
                else if (_editedConfig.BreakEvenStatus == "-")
                {
                    stateService.UpdateExecutionStatus(contractKey, "BreakEven", null, false, 0, "手动重置为waiting");
                    _logger?.LogInformation($"   🔄 保本状态重置为waiting");
                }
                
                // 更新推仓状态（处理三种状态：waiting, executing, executed）
                var pushStatuses = new[] { _editedConfig.PushTier1Status, _editedConfig.PushTier2Status, _editedConfig.PushTier3Status, _editedConfig.PushTier4Status };
                for (int i = 0; i < pushStatuses.Length; i++)
                {
                    if (pushStatuses[i] == "√" || pushStatuses[i] == "✓")
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
                    if (profitStatuses[i] == "√" || profitStatuses[i] == "✓")
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
>>>>>>> df3e9d4bd657da2e1fc523952fc0a2a313f8ef6b
            }
            catch (Exception ex)
            {
                _logger?.LogCritical($"🔥【SaveContractConfigToFile】保存配置发生异常: {ex.Message}");
                _logger?.LogError(ex, "保存配置失败");
                throw;
            }
        }

        /// <summary>
        /// 保存到增强版数据管理器 - 精确一对一修改，避免串改
        /// </summary>
        private async void SaveToEnhancedDataManager(string contractKey)
        {
            try
            {
                _logger?.LogCritical($"🔧【精确保存】开始精确保存合约配置: {contractKey}");
                
                // 🎯 第一步：获取现有的合约状态（作为基础数据）
                var contractState = _dataManager!.GetContractState(contractKey);
                if (contractState == null)
                {
                    _logger?.LogWarning($"⚠️ 合约状态不存在，无法保存: {contractKey}");
                    return;
                }

                // 🎯 第二步：创建状态快照用于比较
                var originalBreakevenState = contractState.ExecutionStates.Breakeven.State;
                var originalBreakevenAmount = contractState.ExecutionStates.Breakeven.TriggerAmount;
                
                _logger?.LogCritical($"🔧【精确保存】原始保本状态: {originalBreakevenState}, 金额: {originalBreakevenAmount}");
                _logger?.LogCritical($"🔧【精确保存】编辑后保本状态: {_editedConfig.BreakEvenStatus}, 金额: {_editedConfig.BreakEvenTarget}");

                // 🎯 第三步：只有当保本字段确实被修改时才更新
                var newBreakevenState = ConvertStatusToExecutionState(_editedConfig.BreakEvenStatus);
                if (originalBreakevenState != newBreakevenState || Math.Abs(originalBreakevenAmount - _editedConfig.BreakEvenTarget) > 0.01m)
                {
                    _logger?.LogCritical($"🔧【精确保存】检测到保本字段变化，开始更新...");
                    contractState.ExecutionStates.Breakeven.TriggerAmount = _editedConfig.BreakEvenTarget;
                    contractState.ExecutionStates.Breakeven.State = newBreakevenState;
                    
                    // 根据状态设置执行时间
                    if (newBreakevenState == ExecutionStateTypes.Executed)
                    {
                        contractState.ExecutionStates.Breakeven.ExecutedAt = DateTime.UtcNow;
                    }
                    else if (newBreakevenState == ExecutionStateTypes.NotTriggered)
                    {
                        contractState.ExecutionStates.Breakeven.ExecutedAt = null;
                    }
                    
                    _logger?.LogCritical($"✅【精确保存】保本字段已更新: 状态={newBreakevenState}, 金额={_editedConfig.BreakEvenTarget}");
                }
                else
                {
                    _logger?.LogCritical($"✅【精确保存】保本字段无变化，跳过更新");
                }

                // 🎯 第四步：精确更新推仓状态（只更新有变化的）
                var pushStatuses = new[] 
                { 
                    new { Index = 1, Status = _editedConfig.PushTier1Status },
                    new { Index = 2, Status = _editedConfig.PushTier2Status },
                    new { Index = 3, Status = _editedConfig.PushTier3Status },
                    new { Index = 4, Status = _editedConfig.PushTier4Status }
                };

                foreach (var pushStatus in pushStatuses)
                {
                    var tier = contractState.ExecutionStates.AddPositionTiers.FirstOrDefault(t => t.TierIndex == pushStatus.Index);
                    if (tier != null)
                    {
                        var originalState = tier.State;
                        var newState = ConvertStatusToExecutionState(pushStatus.Status);
                        
                        if (originalState != newState)
                        {
                            _logger?.LogCritical($"🔧【精确保存】推仓阶梯{pushStatus.Index}状态变化: {originalState} -> {newState}");
                            tier.State = newState;
                            
                            // 根据状态设置执行时间
                            if (newState == ExecutionStateTypes.Executed)
                            {
                                tier.ExecutedAt = DateTime.UtcNow;
                                _logger?.LogCritical($"✅【精确保存】推仓阶梯{pushStatus.Index}设置为已执行，执行时间: {tier.ExecutedAt}");
                            }
                            else if (newState == ExecutionStateTypes.NotTriggered)
                            {
                                tier.ExecutedAt = null;
                                _logger?.LogCritical($"✅【精确保存】推仓阶梯{pushStatus.Index}重置为未触发");
                            }
                        }
                        else
                        {
                            _logger?.LogCritical($"✅【精确保存】推仓阶梯{pushStatus.Index}状态无变化，跳过更新");
                        }
                    }
                }

                // 🎯 第五步：精确更新保盈状态（只更新有变化的）
                var profitStatuses = new[] 
                { 
                    new { Index = 1, Status = _editedConfig.ProfitTier1Status },
                    new { Index = 2, Status = _editedConfig.ProfitTier2Status },
                    new { Index = 3, Status = _editedConfig.ProfitTier3Status }
                };

                foreach (var profitStatus in profitStatuses)
                {
                    var tier = contractState.ExecutionStates.ProfitProtectionTiers.FirstOrDefault(t => t.TierIndex == profitStatus.Index);
                    if (tier != null)
                    {
                        var originalState = tier.State;
                        var newState = ConvertStatusToExecutionState(profitStatus.Status);
                        
                        if (originalState != newState)
                        {
                            _logger?.LogCritical($"🔧【精确保存】保盈阶梯{profitStatus.Index}状态变化: {originalState} -> {newState}");
                            tier.State = newState;
                            
                            // 根据状态设置执行时间
                            if (newState == ExecutionStateTypes.Executed)
                            {
                                tier.ExecutedAt = DateTime.UtcNow;
                                _logger?.LogCritical($"✅【精确保存】保盈阶梯{profitStatus.Index}设置为已执行，执行时间: {tier.ExecutedAt}");
                            }
                            else if (newState == ExecutionStateTypes.NotTriggered)
                            {
                                tier.ExecutedAt = null;
                                _logger?.LogCritical($"✅【精确保存】保盈阶梯{profitStatus.Index}重置为未触发");
                            }
                        }
                        else
                        {
                            _logger?.LogCritical($"✅【精确保存】保盈阶梯{profitStatus.Index}状态无变化，跳过更新");
                        }
                    }
                }

                // 🎯 第六步：更新合约状态的全局信息
                contractState.Meta.UpdatedAt = DateTime.UtcNow;

                // 🎯 第七步：保存到数据管理器并立即同步到文件
                await _dataManager.SaveContractStateAsync(contractState);
                
                // 🎯 第八步：保存后验证，确保数据正确且无串改
                await VerifyContractStateAfterSave(contractKey);
                
                _logger?.LogCritical($"✅【精确保存】合约配置已精确保存到缓存和文件: {contractKey}");
                _logger?.LogCritical($"✅【精确保存】一对一修改完成，避免了状态串改问题");
                
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ 精确保存到增强版数据管理器失败: {contractKey}");
                throw;
            }
        }

        /// <summary>
        /// 保存后验证合约状态，确保数据正确且无串改
        /// </summary>
        private async Task VerifyContractStateAfterSave(string contractKey)
        {
            try
            {
                _logger?.LogCritical($"🔍【保存验证】开始验证合约状态: {contractKey}");
                
                // 重新获取已保存的状态
                var savedState = _dataManager!.GetContractState(contractKey);
                if (savedState == null)
                {
                    _logger?.LogError($"❌【保存验证】无法获取已保存的合约状态: {contractKey}");
                    return;
                }
                
                // 验证保本状态
                var expectedBreakevenState = ConvertStatusToExecutionState(_editedConfig.BreakEvenStatus);
                if (savedState.ExecutionStates.Breakeven.State != expectedBreakevenState)
                {
                    _logger?.LogError($"❌【保存验证】保本状态验证失败: 期望={expectedBreakevenState}, 实际={savedState.ExecutionStates.Breakeven.State}");
                }
                else
                {
                    _logger?.LogCritical($"✅【保存验证】保本状态验证通过: {expectedBreakevenState}");
                }
                
                // 验证保本金额
                if (Math.Abs(savedState.ExecutionStates.Breakeven.TriggerAmount - _editedConfig.BreakEvenTarget) > 0.01m)
                {
                    _logger?.LogError($"❌【保存验证】保本金额验证失败: 期望={_editedConfig.BreakEvenTarget}, 实际={savedState.ExecutionStates.Breakeven.TriggerAmount}");
                }
                else
                {
                    _logger?.LogCritical($"✅【保存验证】保本金额验证通过: {_editedConfig.BreakEvenTarget}");
                }
                
                // 验证推仓状态
                var pushExpected = new[] 
                { 
                    new { Index = 1, Status = _editedConfig.PushTier1Status },
                    new { Index = 2, Status = _editedConfig.PushTier2Status },
                    new { Index = 3, Status = _editedConfig.PushTier3Status },
                    new { Index = 4, Status = _editedConfig.PushTier4Status }
                };
                
                foreach (var expected in pushExpected)
                {
                    var tier = savedState.ExecutionStates.AddPositionTiers.FirstOrDefault(t => t.TierIndex == expected.Index);
                    if (tier != null)
                    {
                        var expectedState = ConvertStatusToExecutionState(expected.Status);
                        if (tier.State != expectedState)
                        {
                            _logger?.LogError($"❌【保存验证】推仓阶梯{expected.Index}状态验证失败: 期望={expectedState}, 实际={tier.State}");
                        }
                        else
                        {
                            _logger?.LogCritical($"✅【保存验证】推仓阶梯{expected.Index}状态验证通过: {expectedState}");
                        }
                    }
                }
                
                // 验证保盈状态
                var profitExpected = new[] 
                { 
                    new { Index = 1, Status = _editedConfig.ProfitTier1Status },
                    new { Index = 2, Status = _editedConfig.ProfitTier2Status },
                    new { Index = 3, Status = _editedConfig.ProfitTier3Status }
                };
                
                foreach (var expected in profitExpected)
                {
                    var tier = savedState.ExecutionStates.ProfitProtectionTiers.FirstOrDefault(t => t.TierIndex == expected.Index);
                    if (tier != null)
                    {
                        var expectedState = ConvertStatusToExecutionState(expected.Status);
                        if (tier.State != expectedState)
                        {
                            _logger?.LogError($"❌【保存验证】保盈阶梯{expected.Index}状态验证失败: 期望={expectedState}, 实际={tier.State}");
                        }
                        else
                        {
                            _logger?.LogCritical($"✅【保存验证】保盈阶梯{expected.Index}状态验证通过: {expectedState}");
                        }
                    }
                }
                
                _logger?.LogCritical($"🎉【保存验证】合约状态验证完成: {contractKey}");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌【保存验证】验证过程发生异常: {contractKey}");
            }
        }

        /// <summary>
        /// 保存到旧版状态服务
        /// </summary>
        private void SaveToLegacyStateService(ContractMonitoringStateService stateService, string contractKey)
        {
            try
            {
                // 获取当前账户
                var filePathManager = new FilePathManager();
                var currentAccount = filePathManager.GetCurrentAccountName();
                
                // 🔧 【统一键名】使用传入的contractKey参数
                _logger?.LogCritical($"🔍【键名调试】开始更新合约状态: {contractKey}");
                _logger?.LogCritical($"🔍【键名调试】原始合约名: '{_editedConfig.ContractName}'");
                _logger?.LogCritical($"🔍【键名调试】标准化合约键: '{contractKey}'");
                _logger?.LogCritical($"🔍【键名调试】当前账号: {currentAccount}");
                _logger?.LogInformation($"🔧 待更新状态: 保本={_editedConfig.BreakEvenStatus}, 推仓=T1:{_editedConfig.PushTier1Status}|T2:{_editedConfig.PushTier2Status}|T3:{_editedConfig.PushTier3Status}|T4:{_editedConfig.PushTier4Status}, 保盈=T1:{_editedConfig.ProfitTier1Status}|T2:{_editedConfig.ProfitTier2Status}|T3:{_editedConfig.ProfitTier3Status}");
                
                // 🔧 【关键修复】更新保本状态和金额（确保未触发状态显示最新金额）
                var allStates = stateService.LoadMonitoringStates();
                
                // 🔍【关键调试】详细显示所有可用的键名和格式
                _logger?.LogCritical($"🔍【键名调试】状态文件中共有 {allStates.Count} 个合约状态");
                
                if (allStates.Count == 0)
                {
                    _logger?.LogCritical($"🚨【致命问题】状态文件为空！没有任何合约状态！");
                    
                    // 检查文件路径
                    var stateFilePath = stateService.GetType().GetMethod("GetFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (stateFilePath != null)
                    {
                        try
                        {
                            var filePath = stateFilePath.Invoke(stateService, null) as string;
                            _logger?.LogCritical($"🔍【文件路径】状态文件路径: {filePath}");
                            _logger?.LogCritical($"🔍【文件存在】状态文件是否存在: {(filePath != null && File.Exists(filePath))}");
                            if (filePath != null && File.Exists(filePath))
                            {
                                var fileContent = File.ReadAllText(filePath);
                                _logger?.LogCritical($"🔍【文件内容】状态文件内容长度: {fileContent.Length}");
                                _logger?.LogCritical($"🔍【文件内容】前500字符: {fileContent.Substring(0, Math.Min(500, fileContent.Length))}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogCritical($"🔍【文件检查】检查文件时出错: {ex.Message}");
                        }
                    }
                }
                else
                {
                    foreach (var kvp in allStates)
                    {
                        _logger?.LogCritical($"🔍【键名调试】存在的键: '{kvp.Key}' -> Symbol:{kvp.Value.Symbol}, Side:{kvp.Value.PositionSide}");
                    }
                }
                
                // 尝试多种可能的键名格式
                var possibleKeys = new[]
                {
                    contractKey,                                           // "BTCUSDT_SHORT"
                    _editedConfig.ContractName,                           // "BTCUSDT SHORT" (原格式)
                    $"{_editedConfig.ContractName.Split(' ')[0]}_{_editedConfig.ContractName.Split(' ')[1]}", // 重新组合
                    _editedConfig.ContractName.Replace(" ", "")           // "BTCUSDTSHORT" (无空格无下划线)
                };
                
                ContractMonitoringState? foundState = null;
                string? foundKey = null;
                
                foreach (var key in possibleKeys)
                {
                    if (allStates.TryGetValue(key, out foundState))
                    {
                        foundKey = key;
                        _logger?.LogCritical($"🎯【键名调试】找到匹配！使用键: '{key}'");
                        break;
                    }
                    else
                    {
                        _logger?.LogCritical($"🔍【键名调试】尝试键 '{key}' - 未找到");
                    }
                }
                
                if (foundState == null || foundKey == null)
                {
                    _logger?.LogError($"❌ 所有可能的键名都无法找到合约状态");
                    _logger?.LogError($"❌ 尝试的键名: {string.Join(", ", possibleKeys)}");
                    _logger?.LogError($"❌ 可用的合约键: {string.Join(", ", allStates.Keys)}");
                    throw new InvalidOperationException($"无法找到合约状态，尝试的键名: {string.Join(", ", possibleKeys)}");
                }
                
                var contractState = foundState;
                contractKey = foundKey; // 使用找到的实际键名
                
                // 🔧 【金额保护修复】只在触发金额确实有变化且大于0时才更新，防止被错误清零
                if (_editedConfig.BreakEvenTarget > 0 && Math.Abs(_editedConfig.BreakEvenTarget - contractState.BreakEvenConfig.TriggerProfitAmount) > 0.01m)
                {
                    contractState.BreakEvenConfig.TriggerProfitAmount = _editedConfig.BreakEvenTarget;
                    _logger?.LogInformation($"   💰 保本触发金额更新: {contractState.BreakEvenConfig.TriggerProfitAmount} → {_editedConfig.BreakEvenTarget}");
                }
                else
                {
                    _logger?.LogInformation($"   🔒 保本触发金额保护: 保持原值 {contractState.BreakEvenConfig.TriggerProfitAmount}，跳过更新（编辑值: {_editedConfig.BreakEvenTarget}）");
                }
                
                // 🔧 【关键修复】直接更新保本状态，不调用UpdateExecutionStatus避免覆盖
                _logger?.LogCritical($"🔥【保存调试】准备更新保本状态: '{_editedConfig.BreakEvenStatus}'");
                _logger?.LogCritical($"🔥【保存调试】当前文件中保本状态: {contractState.BreakEvenConfig.ExecutionState}");
                
                if (_editedConfig.BreakEvenStatus == "√")
                {
                    contractState.BreakEvenConfig.ExecutionState = ExecutionState.Executed;
                    contractState.BreakEvenConfig.ExecutionTime = DateTime.Now;
                    _logger?.LogCritical($"🔥【保存调试】✅ 保本状态已更新为executed");
                }
                else if (_editedConfig.BreakEvenStatus == "-")
                {
                    contractState.BreakEvenConfig.ExecutionState = ExecutionState.NotTriggered;
                    contractState.BreakEvenConfig.ExecutionTime = null;
                    _logger?.LogCritical($"🔥【保存调试】🔄 保本状态已重置为waiting");
                }
                else
                {
                    _logger?.LogCritical($"🔥【保存调试】⚠️ 未知的保本状态: '{_editedConfig.BreakEvenStatus}'");
                }
                
                _logger?.LogCritical($"🔥【保存调试】更新后文件中保本状态: {contractState.BreakEvenConfig.ExecutionState}");
                
                // 🔧 【动态更新】根据文件中的实际推仓阶梯数量进行更新
                if (allStates.TryGetValue(contractKey, out var state))
                {
                    // 🔧 【动态处理】遍历文件中所有推仓阶梯
                    var pushTiers = state.AddPositionConfig.Tiers;
                    _logger?.LogCritical($"🔧【动态保存】文件中推仓阶梯数量: {pushTiers.Count}");

                    foreach (var tier in pushTiers)
                    {
                        var tierIndex = tier.TierIndex;
                        var newStatus = GetPushTierStatus(tierIndex); // 使用动态获取方法
                        var currentStatus = tier.ExecutionState == ExecutionState.Executed ? "√" : "-";
                        
                        if (newStatus != currentStatus)
                        {
                            // 🔧 【关键修复】只更新执行状态，不更新触发金额（保护文件中的正确金额）
                            _logger?.LogCritical($"🔥【动态保存】推仓阶梯{tierIndex}状态变化: {currentStatus} → {newStatus}，金额保持: {tier.TriggerProfitAmount}");
                            
                            // 🔧 【精确更新】基于动态状态更新
                            if (newStatus == "√")
                            {
                                tier.ExecutionState = ExecutionState.Executed;
                                tier.ExecutionTime = DateTime.Now;
                                _logger?.LogCritical($"🔥【状态更新】推仓阶梯{tierIndex}状态更新为executed，金额保持: {tier.TriggerProfitAmount}");
                            }
                            else if (newStatus == "-")
                            {
                                tier.ExecutionState = ExecutionState.NotTriggered;
                                tier.ExecutionTime = null;
                                _logger?.LogCritical($"🔥【状态更新】推仓阶梯{tierIndex}状态重置为waiting，金额保持: {tier.TriggerProfitAmount}");
                            }
                        }
                        else
                        {
                            _logger?.LogInformation($"   ✓ 推仓阶梯{tierIndex}状态无变化，跳过更新");
                        }
                    }
                }
                
                // 🔧 【动态更新】根据文件中的实际保盈阶梯数量进行更新
                if (allStates.TryGetValue(contractKey, out state))
                {
                    // 🔧 【动态处理】遍历文件中所有保盈阶梯
                    var profitTiers = state.ProfitProtectionConfig.Tiers;
                    _logger?.LogCritical($"🔧【动态保存】文件中保盈阶梯数量: {profitTiers.Count}");

                    foreach (var tier in profitTiers)
                    {
                        var tierIndex = tier.TierIndex;
                        var newStatus = GetProfitTierStatus(tierIndex); // 使用动态获取方法
                        var currentStatus = tier.ExecutionState == ExecutionState.Executed ? "√" : "-";
                        
                        if (newStatus != currentStatus)
                        {
                            // 🔧 【关键修复】只更新执行状态，不更新触发金额和保护金额（保护文件中的正确金额）
                            _logger?.LogCritical($"🔥【动态保存】保盈阶梯{tierIndex}状态变化: {currentStatus} → {newStatus}，金额保持: 触发{tier.TriggerProfitAmount}|保护{tier.ProtectionAmount}");
                            
                            // 🔧 【精确更新】基于动态状态更新
                            if (newStatus == "√")
                            {
                                tier.ExecutionState = ExecutionState.Executed;
                                tier.ExecutionTime = DateTime.Now;
                                _logger?.LogCritical($"🔥【状态更新】保盈阶梯{tierIndex}状态更新为executed，金额保持: 触发{tier.TriggerProfitAmount}|保护{tier.ProtectionAmount}");
                            }
                            else if (newStatus == "-")
                            {
                                tier.ExecutionState = ExecutionState.NotTriggered;
                                tier.ExecutionTime = null;
                                _logger?.LogCritical($"🔥【状态更新】保盈阶梯{tierIndex}状态重置为waiting，金额保持: 触发{tier.TriggerProfitAmount}|保护{tier.ProtectionAmount}");
                            }
                        }
                        else
                        {
                            _logger?.LogInformation($"   ✓ 保盈阶梯{tierIndex}状态无变化，跳过更新");
                        }
                    }
                }
                
                // 🔧 【最终保存】一次性保存所有更新（保本、推仓、保盈）
                stateService.SaveMonitoringStates(allStates);
                _logger?.LogCritical($"🔥【保存调试】💾 所有配置金额和状态更新已保存到文件");
                
                // 🔧 【最终确认】重新读取文件确认保存结果
                var savedStates = stateService.LoadMonitoringStates();
                if (savedStates.TryGetValue(contractKey, out var savedState))
                {
                    _logger?.LogCritical($"🔥【保存确认】文件中最终保本状态: {savedState.BreakEvenConfig.ExecutionState}");
                    _logger?.LogCritical($"🔥【保存确认】文件中最终保本时间: {savedState.BreakEvenConfig.ExecutionTime}");
                    
                    // 🔧 【关键修复】增加推仓状态的保存验证
                    for (int i = 1; i <= 4; i++)
                    {
                        var pushTier = savedState.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == i);
                        if (pushTier != null)
                        {
                            _logger?.LogCritical($"🔥【保存确认】推仓阶梯{i}状态: {pushTier.ExecutionState}");
                            _logger?.LogCritical($"🔥【保存确认】推仓阶梯{i}时间: {pushTier.ExecutionTime}");
                        }
                        else
                        {
                            _logger?.LogCritical($"🔥【保存确认】⚠️ 未找到推仓阶梯{i}");
                        }
                    }
                    
                    // 🔧 【关键修复】增加保盈状态的保存验证
                    for (int i = 1; i <= 3; i++)
                    {
                        var profitTier = savedState.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == i);
                        if (profitTier != null)
                        {
                            _logger?.LogCritical($"🔥【保存确认】保盈阶梯{i}状态: {profitTier.ExecutionState}");
                            _logger?.LogCritical($"🔥【保存确认】保盈阶梯{i}时间: {profitTier.ExecutionTime}");
                        }
                        else
                        {
                            _logger?.LogCritical($"🔥【保存确认】⚠️ 未找到保盈阶梯{i}");
                        }
                    }
                }
                else
                {
                    _logger?.LogCritical($"🔥【保存确认】警告：重新读取时未找到合约: {contractKey}");
                }

                _logger?.LogCritical($"🔥【保存调试】✅ 合约状态更新完成: {contractKey}");
            }
            catch (Exception ex)
            {
                _logger?.LogCritical($"🔥【SaveContractConfigToFile】发生异常: {ex.Message}");
                _logger?.LogCritical($"🔥【SaveContractConfigToFile】异常堆栈: {ex.StackTrace}");
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
                // 🔥 强制输出，确保能看到保存按钮被点击
                Console.WriteLine($"🔥🔥🔥 SAVE BUTTON CLICKED: {_editedConfig?.ContractName ?? "未知合约"}");
                System.Diagnostics.Debug.WriteLine($"🔥🔥🔥 SAVE BUTTON CLICKED: {_editedConfig?.ContractName ?? "未知合约"}");
                
                _logger?.LogCritical($"🔥【保存按钮点击】开始处理保存请求: {_editedConfig?.ContractName ?? "未知合约"}");
                
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

                _logger?.LogCritical($"🔥【保存按钮点击】输入验证通过，开始更新配置");
                
                // 获取当前的状态值（调试）
                var currentBreakEvenStatus = GetComboBoxSelection(BreakEvenStatusComboBox);
                _logger?.LogCritical($"🔥【保存按钮点击】当前保本状态选择: '{currentBreakEvenStatus}'");
                
                // 🔧 【重要调试】详细检查ComboBox状态
                _logger?.LogCritical($"🔥【ComboBox调试】BreakEvenStatusComboBox.SelectedItem: {BreakEvenStatusComboBox.SelectedItem}");
                _logger?.LogCritical($"🔥【ComboBox调试】BreakEvenStatusComboBox.SelectedIndex: {BreakEvenStatusComboBox.SelectedIndex}");
                if (BreakEvenStatusComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    _logger?.LogCritical($"🔥【ComboBox调试】SelectedItem.Content: '{selectedItem.Content}'");
                    _logger?.LogCritical($"🔥【ComboBox调试】SelectedItem.Tag: '{selectedItem.Tag}'");
                }
                
                // 🔧 【强制确认】显示弹窗确认用户选择
                var confirmMessage = $"确认保存配置？\n\n" +
                                   $"合约: {_editedConfig.ContractName}\n" +
                                   $"保本目标: {breakEvenTarget:F2} USDT\n" +
                                   $"保本状态: {currentBreakEvenStatus}\n\n" +
                                   $"点击确定继续保存";
                
                var confirmResult = MessageBox.Show(confirmMessage, "确认保存", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (confirmResult != MessageBoxResult.OK)
                {
                    _logger?.LogCritical($"🔥【保存取消】用户取消了保存操作");
                    return;
                }
                
                // 更新编辑后的配置
                _editedConfig.BreakEvenTarget = breakEvenTarget;
                _editedConfig.BreakEvenStatus = currentBreakEvenStatus;
                
                // 🔧 【关键修复】保存前强制同步所有UI控件状态到_editedConfig
                _logger?.LogCritical($"🔥【状态同步】开始强制同步UI控件状态到配置...");
                
                // 🔧 【推仓状态同步】强制从UI控件读取推仓状态
                SyncPushTierStatusFromUI();
                
                // 🔧 【保盈状态同步】强制从UI控件读取保盈状态  
                SyncProfitTierStatusFromUI();
                
                _logger?.LogCritical($"🔥【状态同步】UI状态同步完成，当前_editedConfig状态:");
                
                // 🔧 【动态日志】显示所有推仓阶梯状态
                var pushStatusLog = "";
                var maxPushTier = Math.Max(4, _pushTierComboBoxes?.Count ?? 0);
                for (int i = 1; i <= maxPushTier; i++)
                {
                    var status = GetPushTierStatus(i);
                    pushStatusLog += $"T{i}={status}, ";
                }
                _logger?.LogCritical($"🔥【状态同步】  推仓: {pushStatusLog.TrimEnd(' ', ',')}");
                
                // 🔧 【动态日志】显示所有保盈阶梯状态  
                var profitStatusLog = "";
                var maxProfitTier = Math.Max(3, _profitTierComboBoxes?.Count ?? 0);
                for (int i = 1; i <= maxProfitTier; i++)
                {
                    var status = GetProfitTierStatus(i);
                    profitStatusLog += $"T{i}={status}, ";
                }
                _logger?.LogCritical($"🔥【状态同步】  保盈: {profitStatusLog.TrimEnd(' ', ',')}");

                _editedConfig.UpdateTime = DateTime.Now.ToString("HH:mm:ss");

                _logger?.LogCritical($"🔥【保存按钮点击】配置更新完成，准备调用SaveContractConfigToFile");
                _logger?.LogCritical($"🔥【保存按钮点击】最终保本状态: '{_editedConfig.BreakEvenStatus}'");
                
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
                _logger?.LogCritical($"🔥【保存按钮点击】保存配置发生异常: {ex.Message}");
                _logger?.LogCritical($"🔥【保存按钮点击】异常堆栈: {ex.StackTrace}");
                _logger?.LogError(ex, "保存配置失败");
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 🔧 【动态修复】根据UI控件数量动态同步推仓状态，支持推仓5-7及更多
        /// </summary>
        private void SyncPushTierStatusFromUI()
        {
            try
            {
                var comboBoxCount = _pushTierComboBoxes?.Count ?? 0;
                _logger?.LogCritical($"🔧【推仓同步】开始动态同步推仓状态，UI控件数量: {comboBoxCount}");
                
                // 🔧 【关键】清空现有的动态状态
                _extendedPushTierStatuses.Clear();
                
                // 🔧 【动态同步】根据实际UI控件数量进行同步
                for (int i = 0; i < comboBoxCount; i++)
                {
                    if (_pushTierComboBoxes[i]?.SelectedItem is ComboBoxItem item)
                    {
                        var tierIndex = i + 1; // 阶梯从1开始
                        var newStatus = item.Tag?.ToString() ?? "-";
                        var oldStatus = GetPushTierStatus(tierIndex);
                        
                        // 🔧 【动态设置】使用便捷方法设置状态
                        SetPushTierStatus(tierIndex, newStatus);
                        
                        _logger?.LogCritical($"🔧【推仓同步】T{tierIndex}: {oldStatus} → {newStatus}");
                    }
                }
                
                _logger?.LogCritical($"🔧【推仓同步】推仓状态同步完成，共同步 {comboBoxCount} 个阶梯");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 动态同步推仓状态失败");
            }
        }
        
        /// <summary>
        /// 🔧 【动态修复】根据UI控件数量动态同步保盈状态，支持保盈4-10及更多
        /// </summary>
        private void SyncProfitTierStatusFromUI()
        {
            try
            {
                var comboBoxCount = _profitTierComboBoxes?.Count ?? 0;
                _logger?.LogCritical($"🔧【保盈同步】开始动态同步保盈状态，UI控件数量: {comboBoxCount}");
                
                // 🔧 【关键】清空现有的动态状态
                _extendedProfitTierStatuses.Clear();
                
                // 🔧 【动态同步】根据实际UI控件数量进行同步
                for (int i = 0; i < comboBoxCount; i++)
                {
                    if (_profitTierComboBoxes[i]?.SelectedItem is ComboBoxItem item)
                    {
                        var tierIndex = i + 1; // 阶梯从1开始
                        var newStatus = item.Tag?.ToString() ?? "-";
                        var oldStatus = GetProfitTierStatus(tierIndex);
                        
                        // 🔧 【动态设置】使用便捷方法设置状态
                        SetProfitTierStatus(tierIndex, newStatus);
                        
                        _logger?.LogCritical($"🔧【保盈同步】T{tierIndex}: {oldStatus} → {newStatus}");
                    }
                }
                
                _logger?.LogCritical($"🔧【保盈同步】保盈状态同步完成，共同步 {comboBoxCount} 个阶梯");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 动态同步保盈状态失败");
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