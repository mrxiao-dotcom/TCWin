using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BinanceFuturesTrader.Models
{
    public class OrderInfo : INotifyPropertyChanged
    {
        public long OrderId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ClientOrderId { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal OrigQty { get; set; }
        public decimal ExecutedQty { get; set; }
        public decimal CumQty { get; set; }
        public decimal CumQuote { get; set; }
        public string TimeInForce { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool ReduceOnly { get; set; }
        public bool ClosePosition { get; set; }
        public string Side { get; set; } = string.Empty;
        public string PositionSide { get; set; } = string.Empty;
        public decimal StopPrice { get; set; }
        public string WorkingType { get; set; } = string.Empty;
        public decimal? CallbackRate { get; set; } // 移动止损回调率
        public DateTime Time { get; set; }
        public DateTime UpdateTime { get; set; }
        
        // 选择状态属性
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set 
            {
                if (SetProperty(ref _isSelected, value))
                {
                    // 🔧 修复：当选择状态改变时，触发外部通知事件
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        
        // 🔧 新增：选择状态变化事件
        public event EventHandler? SelectionChanged;
        
        // 计算属性
        public decimal RemainingQty => OrigQty - ExecutedQty;
        public decimal AvgPrice => ExecutedQty > 0 ? CumQuote / ExecutedQty : 0;
        
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