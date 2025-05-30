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
        public DateTime Time { get; set; }
        public DateTime UpdateTime { get; set; }
        
        // 选择状态属性
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        
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