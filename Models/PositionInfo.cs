using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BinanceFuturesTrader.Models
{
    public class PositionInfo : INotifyPropertyChanged
    {
        public string Symbol { get; set; } = string.Empty;
        
        // 🔧 修复：将关键数值属性改为支持属性变更通知，确保UI能实时更新
        private decimal _positionAmt;
        public decimal PositionAmt
        {
            get => _positionAmt;
            set
            {
                if (SetProperty(ref _positionAmt, value))
                {
                    // 当持仓数量变化时，通知所有相关的计算属性
                    OnPropertyChanged(nameof(NotionalValue));
                    OnPropertyChanged(nameof(PositionValue));
                    OnPropertyChanged(nameof(RequiredMargin));
                    OnPropertyChanged(nameof(ProfitRate));
                    OnPropertyChanged(nameof(Direction));
                    OnPropertyChanged(nameof(DirectionColor));
                    OnPropertyChanged(nameof(PnlPercent));
                }
            }
        }
        
        private decimal _entryPrice;
        public decimal EntryPrice
        {
            get => _entryPrice;
            set
            {
                if (SetProperty(ref _entryPrice, value))
                {
                    // 当开仓价格变化时，通知相关的计算属性
                    OnPropertyChanged(nameof(PnlPercent));
                    OnPropertyChanged(nameof(ProfitRate));
                }
            }
        }
        
        private decimal _markPrice;
        public decimal MarkPrice
        {
            get => _markPrice;
            set
            {
                if (SetProperty(ref _markPrice, value))
                {
                    // 当标记价格变化时，通知所有价格相关的计算属性
                    OnPropertyChanged(nameof(NotionalValue));
                    OnPropertyChanged(nameof(PositionValue));
                    OnPropertyChanged(nameof(RequiredMargin));
                    OnPropertyChanged(nameof(ProfitRate));
                }
            }
        }
        
        private decimal _unrealizedProfit;
        public decimal UnrealizedProfit
        {
            get => _unrealizedProfit;
            set
            {
                if (SetProperty(ref _unrealizedProfit, value))
                {
                    // 🔧 关键修复：当浮盈变化时，通知UI更新颜色和百分比
                    OnPropertyChanged(nameof(ProfitColor));
                    OnPropertyChanged(nameof(PnlPercent));
                    OnPropertyChanged(nameof(ProfitRate));
                }
            }
        }
        
        public decimal PositionSide { get; set; }
        public string PositionSideString { get; set; } = string.Empty;
        
        private int _leverage;
        public int Leverage
        {
            get => _leverage;
            set
            {
                if (SetProperty(ref _leverage, value))
                {
                    // 当杠杆变化时，通知保证金相关的计算属性
                    OnPropertyChanged(nameof(RequiredMargin));
                    OnPropertyChanged(nameof(ProfitRate));
                }
            }
        }
        
        public string MarginType { get; set; } = string.Empty;
        public decimal IsolatedMargin { get; set; }
        public DateTime UpdateTime { get; set; }
        
        // 选择状态属性
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        
        // 计算属性
        public decimal NotionalValue => Math.Abs(PositionAmt) * MarkPrice;
        public decimal PnlPercent => EntryPrice > 0 ? (UnrealizedProfit / (Math.Abs(PositionAmt) * EntryPrice)) * 100 : 0;

        // 计算属性：持仓方向（买入/卖出）
        public string Direction
        {
            get
            {
                if (PositionAmt > 0)
                    return "买入";
                else if (PositionAmt < 0)
                    return "卖出";
                else
                    return "无持仓";
            }
        }

        // 计算属性：持仓货值（数量 × 标记价格）
        public decimal PositionValue
        {
            get
            {
                return Math.Abs(PositionAmt) * MarkPrice;
            }
        }

        // 计算属性：所需保证金
        public decimal RequiredMargin
        {
            get
            {
                if (Leverage <= 0) return 0;
                return PositionValue / Leverage;
            }
        }

        // 计算属性：收益率
        public decimal ProfitRate
        {
            get
            {
                if (RequiredMargin <= 0) return 0;
                return (UnrealizedProfit / RequiredMargin) * 100;
            }
        }

        // 计算属性：方向颜色（用于UI绑定）
        public string DirectionColor
        {
            get
            {
                if (PositionAmt > 0)
                    return "Green";
                else if (PositionAmt < 0)
                    return "Red";
                else
                    return "Gray";
            }
        }

        // 计算属性：盈亏颜色
        public string ProfitColor
        {
            get
            {
                if (UnrealizedProfit > 0)
                    return "Green";
                else if (UnrealizedProfit < 0)
                    return "Red";
                else
                    return "Gray";
            }
        }
        
        // INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
} 