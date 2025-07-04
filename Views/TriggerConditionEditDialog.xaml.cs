using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// 触发条件编辑对话框
    /// </summary>
    public partial class TriggerConditionEditDialog : Window, INotifyPropertyChanged
    {
        private readonly ILogger? _logger;
        private readonly ContractMonitorModel _originalContract;
        private bool _hasChanges = false;

        // 数据绑定属性
        private string _windowTitle = "🔧 编辑触发条件";
        private string _contractSymbol = "";
        private string _positionSide = "";
        private string _modificationReason = "";

        public string WindowTitle
        {
            get => _windowTitle;
            set { _windowTitle = value; OnPropertyChanged(); }
        }

        public string ContractSymbol
        {
            get => _contractSymbol;
            set { _contractSymbol = value; OnPropertyChanged(); }
        }

        public string PositionSide
        {
            get => _positionSide;
            set { _positionSide = value; OnPropertyChanged(); }
        }

        public string ModificationReason
        {
            get => _modificationReason;
            set { _modificationReason = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TriggerConditionEditModel> TriggerConditions { get; } = new();

        /// <summary>
        /// 编辑结果
        /// </summary>
        public TriggerConditionEditResult? EditResult { get; private set; }

        public TriggerConditionEditDialog(ContractMonitorModel contract, ILogger? logger = null)
        {
            InitializeComponent();
            DataContext = this;
            
            _logger = logger;
            _originalContract = contract;
            
            InitializeData();
        }

        /// <summary>
        /// 初始化数据
        /// </summary>
        private void InitializeData()
        {
            try
            {
                // 设置基本信息
                ContractSymbol = _originalContract.Symbol;
                PositionSide = _originalContract.PositionSide;
                WindowTitle = $"🔧 编辑触发条件 - {ContractSymbol} {PositionSide}";

                // 加载触发条件
                TriggerConditions.Clear();
                foreach (var condition in _originalContract.TriggerConditions.OrderBy(c => c.Type).ThenBy(c => c.TierIndex))
                {
                    var editModel = new TriggerConditionEditModel
                    {
                        Id = condition.Id,
                        OriginalCondition = condition,
                        Type = condition.Type,
                        TierIndex = condition.TierIndex,
                        Description = condition.Description,
                        TriggerPrice = condition.TriggerPrice,
                        Status = condition.Status,
                        LastExecutionTime = condition.LastExecutionTime
                    };

                    // 监听变化
                    editModel.PropertyChanged += EditModel_PropertyChanged;
                    TriggerConditions.Add(editModel);
                }

                _logger?.LogInformation($"✅ 编辑对话框初始化完成 - {ContractSymbol}_{PositionSide}, 条件数: {TriggerConditions.Count}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 初始化编辑对话框数据时发生错误");
                MessageBox.Show($"初始化数据失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 监听编辑模型的变化
        /// </summary>
        private void EditModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TriggerConditionEditModel.TriggerPrice) || 
                e.PropertyName == nameof(TriggerConditionEditModel.Status))
            {
                _hasChanges = true;
                _logger?.LogDebug($"🔄 检测到触发条件变化: {e.PropertyName}");
            }
        }

        // 已移除保存按钮和取消按钮的事件处理方法

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 触发条件编辑模型
    /// </summary>
    public class TriggerConditionEditModel : INotifyPropertyChanged
    {
        private decimal _triggerPrice;
        private TriggerExecutionStatus _status;

        public int Id { get; set; }
        public TriggerConditionModel OriginalCondition { get; set; } = null!;
        public TriggerConditionType Type { get; set; }
        public int? TierIndex { get; set; }
        public string Description { get; set; } = "";
        public DateTime? LastExecutionTime { get; set; }

        public decimal TriggerPrice
        {
            get => _triggerPrice;
            set { _triggerPrice = value; OnPropertyChanged(); }
        }

        public TriggerExecutionStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        // 显示属性
        public string StatusText => Status switch
        {
            TriggerExecutionStatus.NotTriggered => "未触发",
            TriggerExecutionStatus.Executed => "已执行",
            _ => "未知"
        };

        public System.Windows.Media.SolidColorBrush StatusColor => Status switch
        {
            TriggerExecutionStatus.NotTriggered => new(System.Windows.Media.Colors.SteelBlue),
            TriggerExecutionStatus.Executed => new(System.Windows.Media.Colors.Green),
            _ => new(System.Windows.Media.Colors.Gray)
        };

        public string LastExecutionTimeText => LastExecutionTime?.ToString("MM-dd HH:mm:ss") ?? "从未执行";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 编辑结果
    /// </summary>
    public class TriggerConditionEditResult
    {
        public string ContractKey { get; set; } = "";
        public string ModificationReason { get; set; } = "";
        public DateTime ModificationTime { get; set; }
        public List<TriggerConditionModification> ModifiedConditions { get; set; } = new();
    }

    /// <summary>
    /// 单个条件的修改信息
    /// </summary>
    public class TriggerConditionModification
    {
        public int ConditionId { get; set; }
        public TriggerConditionType Type { get; set; }
        public int? TierIndex { get; set; }
        public decimal OriginalTriggerPrice { get; set; }
        public decimal NewTriggerPrice { get; set; }
        public TriggerExecutionStatus OriginalStatus { get; set; }
        public TriggerExecutionStatus NewStatus { get; set; }
        public bool PriceChanged { get; set; }
        public bool StatusChanged { get; set; }
    }
} 