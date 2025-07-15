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
            // 🔧 修复：监听触发条件集合的变化（线程安全版本）
            TriggerConditions.CollectionChanged += (s, e) =>
            {
                // 确保属性更新在UI线程中执行
                if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    // 已在UI线程中
                    HandleCollectionChangedCore(e);
                }
                else
                {
                    // 在非UI线程中，调度到UI线程执行
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        HandleCollectionChangedCore(e);
                    }));
                }
            };
        }

        /// <summary>
        /// 核心集合变化处理逻辑（必须在UI线程中调用）
        /// </summary>
        private void HandleCollectionChangedCore(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
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
            UpdateAllDisplayProperties();
        }

        private void OnTriggerConditionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 当触发条件的状态发生变化时，更新相关颜色和统计
            if (e.PropertyName == nameof(TriggerConditionModel.Status))
            {
                UpdateAllDisplayProperties();
            }
        }

        /// <summary>
        /// 更新所有显示属性
        /// </summary>
        private void UpdateAllDisplayProperties()
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
            OnPropertyChanged(nameof(AddPositionProgressIcon));
            OnPropertyChanged(nameof(AddPositionProgressText));
            OnPropertyChanged(nameof(ProfitProgressIcon));
            OnPropertyChanged(nameof(ProfitProgressText));
            
            // 🎯 新增：更新新的显示属性
            OnPropertyChanged(nameof(BreakEvenTriggerDisplay));
            OnPropertyChanged(nameof(BreakEvenStatusIcon));
            OnPropertyChanged(nameof(BreakEvenStatusIconColor));
            OnPropertyChanged(nameof(AddPositionProgressDisplay));
            OnPropertyChanged(nameof(AddPositionStatusIcon));
            OnPropertyChanged(nameof(AddPositionStatusIconColor));
            OnPropertyChanged(nameof(ProfitProtectionProgressDisplay));
            OnPropertyChanged(nameof(ProfitProtectionStatusIcon));
            OnPropertyChanged(nameof(ProfitProtectionStatusIconColor));
            
            // 🎯 新增：更新动态列显示属性
            OnPropertyChanged(nameof(BreakEvenDisplay));
            for (int i = 0; i < 10; i++)
            {
                OnPropertyChanged($"AddPositionTier{i}Display");
                OnPropertyChanged($"ProfitProtectionTier{i}Display");
            }
            
            // 🔧 修复状态图标显示问题：强制刷新所有触发条件的集合索引绑定
            OnPropertyChanged(nameof(TriggerConditions));
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

        // 🎯 新增：动态列显示属性
        
        /// <summary>
        /// 保本显示（格式：10U）
        /// </summary>
        public string BreakEvenDisplay
        {
            get
            {
                var breakEvenCondition = TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                if (breakEvenCondition == null)
                    return "-";
                
                return breakEvenCondition.TriggerPrice > 0 ? $"{breakEvenCondition.TriggerPrice:F0}U" : "-";
            }
        }

        /// <summary>
        /// 保本状态图标显示
        /// </summary>
        public string BreakEvenStatusDisplay
        {
            get
            {
                var breakEvenCondition = TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                if (breakEvenCondition == null)
                    return "-";
                
                return breakEvenCondition.Status switch
                {
                    TriggerExecutionStatus.Executed => "✓",
                    TriggerExecutionStatus.NotTriggered => "-",
                    _ => "-"
                };
            }
        }

        /// <summary>
        /// 保本状态图标颜色
        /// </summary>
        public SolidColorBrush BreakEvenStatusColor
        {
            get
            {
                var breakEvenCondition = TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                if (breakEvenCondition == null)
                    return new SolidColorBrush(Colors.Gray);
                
                return breakEvenCondition.Status switch
                {
                    TriggerExecutionStatus.Executed => new SolidColorBrush(Colors.Green),
                    TriggerExecutionStatus.NotTriggered => new SolidColorBrush(Colors.Gray),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
        }
        
        /// <summary>
        /// 获取推仓阶梯显示（格式：20U）
        /// 注意：配置中TierIndex从1开始，显示时需要+1匹配
        /// </summary>
        private string GetAddPositionTierDisplay(int displayIndex)
        {
            // 🔧 修复：配置数据TierIndex从1开始，显示索引从0开始，需要+1匹配
            var actualTierIndex = displayIndex + 1;
            var condition = TriggerConditions.FirstOrDefault(c => 
                c.Type == TriggerConditionType.AddPosition && c.TierIndex == actualTierIndex);
            if (condition == null)
                return "-";
                
            return condition.TriggerPrice > 0 ? $"{condition.TriggerPrice:F0}U" : "-";
        }

        /// <summary>
        /// 获取推仓阶梯状态图标
        /// </summary>
        private string GetAddPositionTierStatusDisplay(int displayIndex)
        {
            var actualTierIndex = displayIndex + 1;
            var condition = TriggerConditions.FirstOrDefault(c => 
                c.Type == TriggerConditionType.AddPosition && c.TierIndex == actualTierIndex);
            if (condition == null)
                return "-";
                
            return condition.Status switch
            {
                TriggerExecutionStatus.Executed => "✓",
                TriggerExecutionStatus.NotTriggered => "-",
                _ => "-"
            };
        }

        /// <summary>
        /// 获取推仓阶梯状态颜色
        /// </summary>
        private SolidColorBrush GetAddPositionTierStatusColor(int displayIndex)
        {
            var actualTierIndex = displayIndex + 1;
            var condition = TriggerConditions.FirstOrDefault(c => 
                c.Type == TriggerConditionType.AddPosition && c.TierIndex == actualTierIndex);
            if (condition == null)
                return new SolidColorBrush(Colors.Gray);
                
            return condition.Status switch
            {
                TriggerExecutionStatus.Executed => new SolidColorBrush(Colors.Green),
                TriggerExecutionStatus.NotTriggered => new SolidColorBrush(Colors.Gray),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        
        /// <summary>
        /// 获取止盈阶梯显示（格式：100U|80U）
        /// 注意：配置中TierIndex从1开始，显示时需要+1匹配
        /// </summary>
        private string GetProfitProtectionTierDisplay(int displayIndex)
        {
            // 🔧 修复：配置数据TierIndex从1开始，显示索引从0开始，需要+1匹配
            var actualTierIndex = displayIndex + 1;
            var condition = TriggerConditions.FirstOrDefault(c => 
                c.Type == TriggerConditionType.ProfitProtection && c.TierIndex == actualTierIndex);
            if (condition == null)
                return "-";
            
            if (condition.TriggerPrice > 0 && condition.KeepValue > 0)
                return $"{condition.TriggerPrice:F0}U|{condition.KeepValue:F0}U";
            else if (condition.TriggerPrice > 0)
                return $"{condition.TriggerPrice:F0}U|-";
            else
                return "-";
        }

        /// <summary>
        /// 获取止盈阶梯状态图标
        /// </summary>
        private string GetProfitProtectionTierStatusDisplay(int displayIndex)
        {
            var actualTierIndex = displayIndex + 1;
            var condition = TriggerConditions.FirstOrDefault(c => 
                c.Type == TriggerConditionType.ProfitProtection && c.TierIndex == actualTierIndex);
            if (condition == null)
                return "-";
                
            return condition.Status switch
            {
                TriggerExecutionStatus.Executed => "✓",
                TriggerExecutionStatus.NotTriggered => "-",
                _ => "-"
            };
        }

        /// <summary>
        /// 获取止盈阶梯状态颜色
        /// </summary>
        private SolidColorBrush GetProfitProtectionTierStatusColor(int displayIndex)
        {
            var actualTierIndex = displayIndex + 1;
            var condition = TriggerConditions.FirstOrDefault(c => 
                c.Type == TriggerConditionType.ProfitProtection && c.TierIndex == actualTierIndex);
            if (condition == null)
                return new SolidColorBrush(Colors.Gray);
                
            return condition.Status switch
            {
                TriggerExecutionStatus.Executed => new SolidColorBrush(Colors.Green),
                TriggerExecutionStatus.NotTriggered => new SolidColorBrush(Colors.Gray),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        
        // 🎯 动态推仓阶梯显示属性（0-9个阶梯）
        public string AddPositionTier0Display => GetAddPositionTierDisplay(0);
        public string AddPositionTier1Display => GetAddPositionTierDisplay(1);
        public string AddPositionTier2Display => GetAddPositionTierDisplay(2);
        public string AddPositionTier3Display => GetAddPositionTierDisplay(3);
        public string AddPositionTier4Display => GetAddPositionTierDisplay(4);
        public string AddPositionTier5Display => GetAddPositionTierDisplay(5);
        public string AddPositionTier6Display => GetAddPositionTierDisplay(6);
        public string AddPositionTier7Display => GetAddPositionTierDisplay(7);
        public string AddPositionTier8Display => GetAddPositionTierDisplay(8);
        public string AddPositionTier9Display => GetAddPositionTierDisplay(9);

        // 🎯 动态推仓阶梯状态图标属性（0-9个阶梯）
        public string AddPositionTier0Status => GetAddPositionTierStatusDisplay(0);
        public string AddPositionTier1Status => GetAddPositionTierStatusDisplay(1);
        public string AddPositionTier2Status => GetAddPositionTierStatusDisplay(2);
        public string AddPositionTier3Status => GetAddPositionTierStatusDisplay(3);
        public string AddPositionTier4Status => GetAddPositionTierStatusDisplay(4);
        public string AddPositionTier5Status => GetAddPositionTierStatusDisplay(5);
        public string AddPositionTier6Status => GetAddPositionTierStatusDisplay(6);
        public string AddPositionTier7Status => GetAddPositionTierStatusDisplay(7);
        public string AddPositionTier8Status => GetAddPositionTierStatusDisplay(8);
        public string AddPositionTier9Status => GetAddPositionTierStatusDisplay(9);

        // 🎯 动态推仓阶梯状态颜色属性（0-9个阶梯）
        public SolidColorBrush AddPositionTier0StatusColor => GetAddPositionTierStatusColor(0);
        public SolidColorBrush AddPositionTier1StatusColor => GetAddPositionTierStatusColor(1);
        public SolidColorBrush AddPositionTier2StatusColor => GetAddPositionTierStatusColor(2);
        public SolidColorBrush AddPositionTier3StatusColor => GetAddPositionTierStatusColor(3);
        public SolidColorBrush AddPositionTier4StatusColor => GetAddPositionTierStatusColor(4);
        public SolidColorBrush AddPositionTier5StatusColor => GetAddPositionTierStatusColor(5);
        public SolidColorBrush AddPositionTier6StatusColor => GetAddPositionTierStatusColor(6);
        public SolidColorBrush AddPositionTier7StatusColor => GetAddPositionTierStatusColor(7);
        public SolidColorBrush AddPositionTier8StatusColor => GetAddPositionTierStatusColor(8);
        public SolidColorBrush AddPositionTier9StatusColor => GetAddPositionTierStatusColor(9);
        
        // 🎯 动态止盈阶梯显示属性（0-9个阶梯）
        public string ProfitProtectionTier0Display => GetProfitProtectionTierDisplay(0);
        public string ProfitProtectionTier1Display => GetProfitProtectionTierDisplay(1);
        public string ProfitProtectionTier2Display => GetProfitProtectionTierDisplay(2);
        public string ProfitProtectionTier3Display => GetProfitProtectionTierDisplay(3);
        public string ProfitProtectionTier4Display => GetProfitProtectionTierDisplay(4);
        public string ProfitProtectionTier5Display => GetProfitProtectionTierDisplay(5);
        public string ProfitProtectionTier6Display => GetProfitProtectionTierDisplay(6);
        public string ProfitProtectionTier7Display => GetProfitProtectionTierDisplay(7);
        public string ProfitProtectionTier8Display => GetProfitProtectionTierDisplay(8);
        public string ProfitProtectionTier9Display => GetProfitProtectionTierDisplay(9);

        // 🎯 动态止盈阶梯状态图标属性（0-9个阶梯）
        public string ProfitProtectionTier0Status => GetProfitProtectionTierStatusDisplay(0);
        public string ProfitProtectionTier1Status => GetProfitProtectionTierStatusDisplay(1);
        public string ProfitProtectionTier2Status => GetProfitProtectionTierStatusDisplay(2);
        public string ProfitProtectionTier3Status => GetProfitProtectionTierStatusDisplay(3);
        public string ProfitProtectionTier4Status => GetProfitProtectionTierStatusDisplay(4);
        public string ProfitProtectionTier5Status => GetProfitProtectionTierStatusDisplay(5);
        public string ProfitProtectionTier6Status => GetProfitProtectionTierStatusDisplay(6);
        public string ProfitProtectionTier7Status => GetProfitProtectionTierStatusDisplay(7);
        public string ProfitProtectionTier8Status => GetProfitProtectionTierStatusDisplay(8);
        public string ProfitProtectionTier9Status => GetProfitProtectionTierStatusDisplay(9);

        // 🎯 动态止盈阶梯状态颜色属性（0-9个阶梯）
        public SolidColorBrush ProfitProtectionTier0StatusColor => GetProfitProtectionTierStatusColor(0);
        public SolidColorBrush ProfitProtectionTier1StatusColor => GetProfitProtectionTierStatusColor(1);
        public SolidColorBrush ProfitProtectionTier2StatusColor => GetProfitProtectionTierStatusColor(2);
        public SolidColorBrush ProfitProtectionTier3StatusColor => GetProfitProtectionTierStatusColor(3);
        public SolidColorBrush ProfitProtectionTier4StatusColor => GetProfitProtectionTierStatusColor(4);
        public SolidColorBrush ProfitProtectionTier5StatusColor => GetProfitProtectionTierStatusColor(5);
        public SolidColorBrush ProfitProtectionTier6StatusColor => GetProfitProtectionTierStatusColor(6);
        public SolidColorBrush ProfitProtectionTier7StatusColor => GetProfitProtectionTierStatusColor(7);
        public SolidColorBrush ProfitProtectionTier8StatusColor => GetProfitProtectionTierStatusColor(8);
        public SolidColorBrush ProfitProtectionTier9StatusColor => GetProfitProtectionTierStatusColor(9);

        // 🎯 新增：保本相关显示属性
        /// <summary>
        /// 保本触发价格显示
        /// </summary>
        public string BreakEvenTriggerDisplay
        {
            get
            {
                var breakEvenCondition = TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                if (breakEvenCondition == null)
                    return "未配置";
                return breakEvenCondition.TriggerPrice > 0 ? $"{breakEvenCondition.TriggerPrice:F0}U" : "未设置";
            }
        }

        /// <summary>
        /// 保本状态图标（"-" 或 红色"√"）
        /// </summary>
        public string BreakEvenStatusIcon
        {
            get
            {
                var breakEvenCondition = TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                if (breakEvenCondition == null)
                    return "-";
                
                return breakEvenCondition.Status switch
                {
                    TriggerExecutionStatus.NotTriggered => "-",
                    TriggerExecutionStatus.Executing => "⏳",
                    TriggerExecutionStatus.Executed => "√",
                    _ => "-"
                };
            }
        }

        /// <summary>
        /// 保本状态图标颜色
        /// </summary>
        public SolidColorBrush BreakEvenStatusIconColor
        {
            get
            {
                var breakEvenCondition = TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                if (breakEvenCondition == null)
                    return new SolidColorBrush(Colors.Gray);
                
                return breakEvenCondition.Status switch
                {
                    TriggerExecutionStatus.NotTriggered => new SolidColorBrush(Colors.Gray),
                    TriggerExecutionStatus.Executing => new SolidColorBrush(Colors.Orange),
                    TriggerExecutionStatus.Executed => new SolidColorBrush(Colors.Red),  // 用户要求：已完成用红色
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
        }

        // 🎯 新增：推仓相关显示属性
        /// <summary>
        /// 推仓进度显示
        /// </summary>
        public string AddPositionProgressDisplay
        {
            get
            {
                if (TotalAddPositionCount == 0)
                    return "未配置";
                return $"{ExecutedAddPositionCount}/{TotalAddPositionCount}";
            }
        }

        /// <summary>
        /// 推仓状态图标
        /// </summary>
        public string AddPositionStatusIcon
        {
            get
            {
                if (TotalAddPositionCount == 0)
                    return "-";
                
                if (ExecutedAddPositionCount > 0)
                    return "√";  // 有已执行的
                
                return "-";  // 全部未触发
            }
        }

        /// <summary>
        /// 推仓状态图标颜色
        /// </summary>
        public SolidColorBrush AddPositionStatusIconColor
        {
            get
            {
                if (TotalAddPositionCount == 0)
                    return new SolidColorBrush(Colors.Gray);
                
                if (ExecutedAddPositionCount > 0)
                    return new SolidColorBrush(Colors.Red);  // 用户要求：已完成用红色
                
                return new SolidColorBrush(Colors.Gray);  // 全部未触发
            }
        }

        // 🎯 新增：止盈相关显示属性
        /// <summary>
        /// 止盈进度显示
        /// </summary>
        public string ProfitProtectionProgressDisplay
        {
            get
            {
                if (TotalProfitCount == 0)
                    return "未配置";
                return $"{ExecutedProfitCount}/{TotalProfitCount}";
            }
        }

        /// <summary>
        /// 止盈状态图标
        /// </summary>
        public string ProfitProtectionStatusIcon
        {
            get
            {
                if (TotalProfitCount == 0)
                    return "-";
                
                if (ExecutedProfitCount > 0)
                    return "√";  // 有已执行的
                
                return "-";  // 全部未触发
            }
        }

        /// <summary>
        /// 止盈状态图标颜色
        /// </summary>
        public SolidColorBrush ProfitProtectionStatusIconColor
        {
            get
            {
                if (TotalProfitCount == 0)
                    return new SolidColorBrush(Colors.Gray);
                
                if (ExecutedProfitCount > 0)
                    return new SolidColorBrush(Colors.Red);  // 用户要求：已完成用红色
                
                return new SolidColorBrush(Colors.Gray);  // 全部未触发
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