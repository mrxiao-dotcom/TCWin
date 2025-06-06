using System;
using System.Windows;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// 下单确认对话框
    /// </summary>
    public partial class OrderConfirmationDialog : Window
    {
        /// <summary>
        /// 用户是否确认下单
        /// </summary>
        public bool IsConfirmed { get; private set; } = false;

        /// <summary>
        /// 订单确认信息数据模型
        /// </summary>
        public OrderConfirmationModel ConfirmationData { get; set; }

        public OrderConfirmationDialog(OrderConfirmationModel confirmationData)
        {
            InitializeComponent();
            ConfirmationData = confirmationData;
            DataContext = confirmationData;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }
    }

    /// <summary>
    /// 订单确认信息数据模型
    /// </summary>
    public class OrderConfirmationModel
    {
        public string Symbol { get; set; } = "";
        public string Side { get; set; } = "";
        public string OrderType { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public int Leverage { get; set; }
        public string MarginType { get; set; } = "";
        public decimal StopLossRatio { get; set; }
        public decimal StopLossPrice { get; set; }

        // 计算属性
        public string SideText => Side == "BUY" ? "🟢 买入/做多" : "🔴 卖出/做空";
        public string SideColor => Side == "BUY" ? "#FF4CAF50" : "#FFFF5722";
        
        public string OrderTypeText => OrderType switch
        {
            "MARKET" => "📊 市价单",
            "LIMIT" => "📈 限价单",
            _ => OrderType
        };

        public string QuantityText => $"{Quantity:F6}";
        
        public string PriceText => OrderType == "MARKET" 
            ? "市价成交" 
            : $"{Price:F2} USDT";

        public string LeverageText => $"{Leverage}x";
        
        public string MarginTypeText => MarginType == "ISOLATED" 
            ? "🔒 逐仓模式" 
            : "🌐 全仓模式";

        public string NotionalValueText
        {
            get
            {
                var notionalValue = Quantity * Price;
                return $"{notionalValue:F2} USDT";
            }
        }

        public string RequiredMarginText
        {
            get
            {
                var notionalValue = Quantity * Price;
                var requiredMargin = notionalValue / Leverage;
                return $"{requiredMargin:F2} USDT";
            }
        }

        public bool HasStopLoss => StopLossRatio > 0;

        public string StopLossText
        {
            get
            {
                if (!HasStopLoss) return "";
                
                return $"止损比例: {StopLossRatio}%，触发价格: {StopLossPrice:F2} USDT";
            }
        }
    }
} 