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
        public DateTime? TriggerTime { get; set; } // 触发时间
        public long OrderId { get; set; } // 订单ID
        public string Description { get; set; } = string.Empty; // 条件单描述
        public bool IsSelected { get; set; } = false; // 选择状态（用于UI）
        
        // 新增：条件单分类
        public string OrderCategory { get; set; } = "加仓型"; // 加仓型、平仓型
        
        // 新增：是否为平仓型条件单
        public bool IsClosePosition => OrderCategory == "平仓型";
        
        // 新增：是否为加仓型条件单
        public bool IsAddPosition => OrderCategory == "加仓型";
    }
} 