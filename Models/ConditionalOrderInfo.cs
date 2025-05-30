using System;

namespace BinanceFuturesTrader.Models
{
    public class ConditionalOrderInfo
    {
        public string Symbol { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // STOP, TAKE_PROFIT, STOP_MARKET, TAKE_PROFIT_MARKET
        public string Side { get; set; } = string.Empty; // BUY, SELL
        public decimal StopPrice { get; set; } // 触发价格
        public decimal? Price { get; set; } // 限价单价格(可选)
        public decimal Quantity { get; set; } // 数量
        public string Status { get; set; } = "待触发"; // 状态
        public string WorkingType { get; set; } = "CONTRACT_PRICE"; // 触发价格类型
        public DateTime CreateTime { get; set; } = DateTime.Now;
        public long OrderId { get; set; } // 订单ID
    }
} 