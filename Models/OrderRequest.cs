namespace BinanceFuturesTrader.Models
{
    public class OrderRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = "BUY"; // BUY, SELL
        public string PositionSide { get; set; } = "BOTH"; // BOTH, LONG, SHORT
        public string Type { get; set; } = "MARKET"; // MARKET, LIMIT, STOP, TAKE_PROFIT, STOP_MARKET, TAKE_PROFIT_MARKET
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal StopPrice { get; set; }
        public int Leverage { get; set; } = 10;
        public string MarginType { get; set; } = "CROSSED"; // ISOLATED, CROSSED
        public string TimeInForce { get; set; } = "GTC"; // GTC, IOC, FOK, GTX
        public bool ReduceOnly { get; set; } = false;
        public bool ClosePosition { get; set; } = false;
        public string WorkingType { get; set; } = "CONTRACT_PRICE"; // CONTRACT_PRICE, MARK_PRICE
        
        // 止损设置
        public decimal StopLossRatio { get; set; } // 止损比例
        public decimal StopLossPrice { get; set; } // 止损价
        public decimal StopLossAmount { get; set; } // 止损金额
        
        // 🚀 移动止损单设置
        public decimal CallbackRate { get; set; } // 回调率（百分比，如0.5表示0.5%）
        public decimal ActivationPrice { get; set; } // 激活价格（可选）
        
        // 条件单判断
        public bool IsConditionalOrder => 
            Type == "STOP" || Type == "TAKE_PROFIT" || 
            Type == "STOP_MARKET" || Type == "TAKE_PROFIT_MARKET" ||
            Type == "TRAILING_STOP_MARKET";
            
        public bool IsLimitConditionalOrder => 
            Type == "STOP" || Type == "TAKE_PROFIT";
            
        // 移动止损单判断
        public bool IsTrailingStopOrder => Type == "TRAILING_STOP_MARKET";
    }
} 