using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BinanceFuturesTrader.Models
{
    public class PositionInfo : INotifyPropertyChanged
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal PositionAmt { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal MarkPrice { get; set; }
        public decimal UnrealizedProfit { get; set; }
        public decimal PositionSide { get; set; }
        public string PositionSideString { get; set; } = string.Empty;
        public int Leverage { get; set; }
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