using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 触发条件执行状态枚举
    /// </summary>
    public enum TriggerExecutionStatus
    {
        /// <summary>未触发</summary>
        NotTriggered,
        /// <summary>执行中</summary>
        Executing,
        /// <summary>已执行</summary>
        Executed
    }

    /// <summary>
    /// 触发条件类型枚举
    /// </summary>
    public enum TriggerConditionType
    {
        /// <summary>保本</summary>
        BreakEven,
        /// <summary>推仓</summary>
        AddPosition,
        /// <summary>止盈</summary>
        ProfitProtection
    }

    /// <summary>
    /// 单个触发条件数据模型
    /// </summary>
    public class TriggerConditionModel : INotifyPropertyChanged
    {
        private decimal _triggerPrice;
        private decimal _keepValue; // 新增：保留值（仅止盈条件使用）
        private TriggerExecutionStatus _status;
        private DateTime? _lastExecutionTime;
        private string _statusNote = "";

        public int Id { get; set; }
        public TriggerConditionType Type { get; set; }
        public int? TierIndex { get; set; } // 档位索引（保本为null）
        public string Description { get; set; } = "";
        
        public decimal TriggerPrice
        {
            get => _triggerPrice;
            set { _triggerPrice = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayTriggerPrice)); }
        }

        /// <summary>
        /// 保留值（仅止盈条件使用，触发时保留的盈利金额）
        /// </summary>
        public decimal KeepValue
        {
            get => _keepValue;
            set { _keepValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayKeepValue)); }
        }

        public TriggerExecutionStatus Status
        {
            get => _status;
            set { 
                _status = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(StatusText)); 
                OnPropertyChanged(nameof(StatusColor)); 
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusIconColor));
            }
        }

        public DateTime? LastExecutionTime
        {
            get => _lastExecutionTime;
            set { _lastExecutionTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastExecutionTimeText)); }
        }

        public string StatusNote
        {
            get => _statusNote;
            set { _statusNote = value; OnPropertyChanged(); }
        }

        // 中文显示属性
        public string TypeText => Type switch
        {
            TriggerConditionType.BreakEven => "保本条件",
            TriggerConditionType.AddPosition => "加仓条件", 
            TriggerConditionType.ProfitProtection => "止盈条件",
            _ => "未知类型"
        };

        public string StatusText => Status switch
        {
            TriggerExecutionStatus.NotTriggered => "未触发",
            TriggerExecutionStatus.Executing => "执行中",
            TriggerExecutionStatus.Executed => "已执行",
            _ => "未知状态"
        };
        
        // 🎯 新增：状态图标属性
        public string StatusIcon => Status switch
        {
            TriggerExecutionStatus.NotTriggered => "—",  // 横杠表示未触发
            TriggerExecutionStatus.Executing => "⏳",    // 旋转圆圈表示执行中
            TriggerExecutionStatus.Executed => "✓",     // 打钩表示已执行
            _ => "?"
        };

        // 🎯 新增：状态图标颜色属性
        public SolidColorBrush StatusIconColor => Status switch
        {
            TriggerExecutionStatus.NotTriggered => new SolidColorBrush(Colors.Gray),           // 灰色横杠
            TriggerExecutionStatus.Executing => new SolidColorBrush(Colors.Orange),             // 橙色旋转圆圈
            TriggerExecutionStatus.Executed => new SolidColorBrush(Colors.Green),             // 绿色打钩
            _ => new SolidColorBrush(Colors.Black)
        };

        public SolidColorBrush StatusColor => Status switch
        {
            TriggerExecutionStatus.NotTriggered => new SolidColorBrush(Color.FromRgb(34,139,34)), // 森林绿
            TriggerExecutionStatus.Executing => new SolidColorBrush(Color.FromRgb(255,165,0)),     // 橙色
            TriggerExecutionStatus.Executed => new SolidColorBrush(Color.FromRgb(255,99,99)),     // 浅红色
            _ => new SolidColorBrush(Colors.Gray)
        };

        public SolidColorBrush BackgroundColor => Status switch
        {
            TriggerExecutionStatus.NotTriggered => new SolidColorBrush(Color.FromRgb(34,139,34)), // 森林绿背景
            TriggerExecutionStatus.Executing => new SolidColorBrush(Color.FromRgb(255,165,0)),     // 橙色背景
            TriggerExecutionStatus.Executed => new SolidColorBrush(Color.FromRgb(255,99,99)),     // 浅红色背景
            _ => new SolidColorBrush(Colors.LightGray)
        };

        // 显示属性
        public string DisplayTriggerPrice => TriggerPrice > 0 ? $"{TriggerPrice:F4}" : "未设置";
        public string DisplayKeepValue => Type == TriggerConditionType.ProfitProtection && KeepValue > 0 ? $"{KeepValue:F2}" : "";
        public string LastExecutionTimeText => LastExecutionTime?.ToString("MM-dd HH:mm:ss") ?? "从未执行";

        // 是否需要显示保留值
        public bool ShowKeepValue => Type == TriggerConditionType.ProfitProtection;

        public event PropertyChangedEventHandler? PropertyChanged;
        public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 合约监控管理数据模型
    /// </summary>
    public class ContractMonitorModel : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        private bool _isActive = false;
        private decimal _currentPrice;
        private decimal _positionSize;
        private decimal _unrealizedPnl;

        public string Symbol { get; set; } = "";
        public string PositionSide { get; set; } = "";
        
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public decimal CurrentPrice
        {
            get => _currentPrice;
            set { _currentPrice = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentPriceText)); }
        }

        public decimal PositionSize
        {
            get => _positionSize;
            set { _positionSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionSizeText)); }
        }

        public decimal UnrealizedPnl
        {
            get => _unrealizedPnl;
            set { _unrealizedPnl = value; OnPropertyChanged(); OnPropertyChanged(nameof(PnlText)); OnPropertyChanged(nameof(PnlColor)); }
        }

        // 触发条件集合
        public ObservableCollection<TriggerConditionModel> TriggerConditions { get; } = new();

        public ContractMonitorModel()
        {
            // 监听触发条件集合的变化
            TriggerConditions.CollectionChanged += (s, e) =>
            {
                // 当添加新的触发条件时，订阅其状态变化事件
                if (e.NewItems != null)
                {
                    foreach (TriggerConditionModel condition in e.NewItems)
                    {
                        condition.PropertyChanged += OnTriggerConditionPropertyChanged;
                    }
                }
                // 当移除触发条件时，取消订阅
                if (e.OldItems != null)
                {
                    foreach (TriggerConditionModel condition in e.OldItems)
                    {
                        condition.PropertyChanged -= OnTriggerConditionPropertyChanged;
                    }
                }
                // 更新相关属性
                OnPropertyChanged(nameof(RowBackgroundColor));
                OnPropertyChanged(nameof(ExecutedCount));
                OnPropertyChanged(nameof(ExecutionProgress));
                OnPropertyChanged(nameof(ExecutedAddPositionCount));
                OnPropertyChanged(nameof(TotalAddPositionCount));
                OnPropertyChanged(nameof(ExecutedProfitCount));
                OnPropertyChanged(nameof(TotalProfitCount));
                OnPropertyChanged(nameof(AddPositionProgressColor));
                OnPropertyChanged(nameof(ProfitProgressColor));
                OnPropertyChanged(nameof(BreakEvenProgressColor));
                OnPropertyChanged(nameof(BreakEvenStatusText));
                // 🎯 新增：通知图标属性更新
                OnPropertyChanged(nameof(AddPositionProgressIcon));
                OnPropertyChanged(nameof(AddPositionProgressText));
                OnPropertyChanged(nameof(ProfitProgressIcon));
                OnPropertyChanged(nameof(ProfitProgressText));
                
                // 🔧 修复状态图标显示问题：集合变化时也要刷新绑定
                OnPropertyChanged(nameof(TriggerConditions));
            };
        }

        private void OnTriggerConditionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 当触发条件的状态发生变化时，更新相关颜色和统计
            if (e.PropertyName == nameof(TriggerConditionModel.Status))
            {
                OnPropertyChanged(nameof(RowBackgroundColor));
                OnPropertyChanged(nameof(ExecutedCount));
                OnPropertyChanged(nameof(ExecutionProgress));
                OnPropertyChanged(nameof(ExecutedAddPositionCount));
                OnPropertyChanged(nameof(TotalAddPositionCount));
                OnPropertyChanged(nameof(ExecutedProfitCount));
                OnPropertyChanged(nameof(TotalProfitCount));
                OnPropertyChanged(nameof(AddPositionProgressColor));
                OnPropertyChanged(nameof(ProfitProgressColor));
                OnPropertyChanged(nameof(BreakEvenProgressColor));
                OnPropertyChanged(nameof(BreakEvenStatusText));
                // 🎯 新增：通知图标属性更新
                OnPropertyChanged(nameof(AddPositionProgressIcon));
                OnPropertyChanged(nameof(AddPositionProgressText));
                OnPropertyChanged(nameof(ProfitProgressIcon));
                OnPropertyChanged(nameof(ProfitProgressText));
                
                // 🔧 修复状态图标显示问题：强制刷新所有触发条件的集合索引绑定
                OnPropertyChanged(nameof(TriggerConditions));
            }
        }

        // 显示属性
        public string ContractKey => $"{Symbol}_{PositionSide}";
        public string StatusText => !IsEnabled ? "已禁用" : (IsActive ? "活跃" : "非活跃");
        public SolidColorBrush StatusColor => !IsEnabled ? new SolidColorBrush(Colors.Gray) : 
                                            (IsActive ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Orange));
        
        public string CurrentPriceText => CurrentPrice > 0 ? $"{CurrentPrice:F4}" : "N/A";
        public string PositionSizeText => PositionSize != 0 ? $"{PositionSize:F4}" : "无持仓";
        public string PnlText => $"{UnrealizedPnl:F2}";
        public SolidColorBrush PnlColor => UnrealizedPnl >= 0 ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);

        // 根据触发条件状态确定整行背景颜色
        public SolidColorBrush RowBackgroundColor
        {
            get
            {
                if (TriggerConditions.Count == 0)
                    return new SolidColorBrush(Colors.White);

                // 如果有任何条件已执行，显示浅红色背景
                if (TriggerConditions.Any(c => c.Status == TriggerExecutionStatus.Executed))
                    return new SolidColorBrush(Color.FromRgb(255, 240, 240));

                // 如果所有条件都未触发，显示浅绿色背景
                if (TriggerConditions.All(c => c.Status == TriggerExecutionStatus.NotTriggered))
                    return new SolidColorBrush(Color.FromRgb(240, 255, 240));

                // 默认白色背景
                return new SolidColorBrush(Colors.White);
            }
        }

        // 计算执行进度的辅助属性
        public int ExecutedCount => TriggerConditions.Where(c => c.Status == TriggerExecutionStatus.Executed).Count();
        public int TotalCount => TriggerConditions.Count;
        public string ExecutionProgress => TotalCount > 0 ? $"{ExecutedCount}/{TotalCount}" : "0/0";

        // 推仓相关统计
        public int ExecutedAddPositionCount => TriggerConditions.Where(c => c.Type == TriggerConditionType.AddPosition && c.Status == TriggerExecutionStatus.Executed).Count();
        public int TotalAddPositionCount => TriggerConditions.Where(c => c.Type == TriggerConditionType.AddPosition).Count();

        // 止盈相关统计
        public int ExecutedProfitCount => TriggerConditions.Where(c => c.Type == TriggerConditionType.ProfitProtection && c.Status == TriggerExecutionStatus.Executed).Count();
        public int TotalProfitCount => TriggerConditions.Where(c => c.Type == TriggerConditionType.ProfitProtection).Count();

        // 保本状态显示文本
        public string BreakEvenStatusText
        {
            get
            {
                var breakEvenCondition = TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                return breakEvenCondition?.StatusText ?? "无保本";
            }
        }

        // 推仓进度背景颜色
        public SolidColorBrush AddPositionProgressColor
        {
            get
            {
                if (TotalAddPositionCount == 0)
                    return new SolidColorBrush(Colors.LightGray);  // 没有推仓条件
                
                if (ExecutedAddPositionCount > 0)
                    return new SolidColorBrush(Color.FromRgb(255, 99, 99));  // 有已执行 - 浅红色
                
                return new SolidColorBrush(Color.FromRgb(34, 139, 34));  // 全部未触发 - 森林绿
            }
        }

        // 🎯 新增：推仓进度图标
        public string AddPositionProgressIcon
        {
            get
            {
                if (TotalAddPositionCount == 0)
                    return "—";  // 没有推仓条件
                
                if (ExecutedAddPositionCount > 0)
                    return "✓";  // 有已执行
                
                return "—";  // 全部未触发
            }
        }

        // 🎯 新增：推仓进度文本
        public string AddPositionProgressText => TotalAddPositionCount > 0 ? $"{ExecutedAddPositionCount}/{TotalAddPositionCount}" : "";

        // 止盈进度背景颜色
        public SolidColorBrush ProfitProgressColor
        {
            get
            {
                if (TotalProfitCount == 0)
                    return new SolidColorBrush(Colors.LightGray);  // 没有止盈条件
                
                if (ExecutedProfitCount > 0)
                    return new SolidColorBrush(Color.FromRgb(255, 99, 99));  // 有已执行 - 浅红色
                
                return new SolidColorBrush(Color.FromRgb(34, 139, 34));  // 全部未触发 - 森林绿
            }
        }

        // 🎯 新增：止盈进度图标
        public string ProfitProgressIcon
        {
            get
            {
                if (TotalProfitCount == 0)
                    return "—";  // 没有止盈条件
                
                if (ExecutedProfitCount > 0)
                    return "✓";  // 有已执行
                
                return "—";  // 全部未触发
            }
        }

        // 🎯 新增：止盈进度文本
        public string ProfitProgressText => TotalProfitCount > 0 ? $"{ExecutedProfitCount}/{TotalProfitCount}" : "";

        // 保本进度背景颜色（改进版本）
        public SolidColorBrush BreakEvenProgressColor
        {
            get
            {
                var breakEvenCondition = TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                if (breakEvenCondition == null)
                    return new SolidColorBrush(Colors.LightGray);  // 没有保本条件
                
                if (breakEvenCondition.Status == TriggerExecutionStatus.Executed)
                    return new SolidColorBrush(Color.FromRgb(255, 99, 99));  // 已执行 - 浅红色
                
                return new SolidColorBrush(Color.FromRgb(34, 139, 34));  // 未触发 - 森林绿
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 触发条件编辑参数
    /// </summary>
    public class EditTriggerConditionArgs
    {
        public string ContractKey { get; set; } = "";
        public int ConditionId { get; set; }
        public decimal NewTriggerPrice { get; set; }
        public TriggerExecutionStatus NewStatus { get; set; }
        public string Reason { get; set; } = "";
    }

    /// <summary>
    /// 自动盯盘控制状态
    /// </summary>
    public enum AutoMonitorControlStatus
    {
        /// <summary>未启动</summary>
        NotStarted,
        /// <summary>运行中</summary>
        Running,
        /// <summary>已暂停</summary>
        Paused,
        /// <summary>已停止</summary>
        Stopped
    }
} 