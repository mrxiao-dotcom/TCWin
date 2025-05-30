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
        public int Leverage { get; set; } = 1;
        public string MarginType { get; set; } = "CROSSED"; // ISOLATED, CROSSED
        public string TimeInForce { get; set; } = "GTC"; // GTC, IOC, FOK, GTX
        public bool ReduceOnly { get; set; } = false;
        public bool ClosePosition { get; set; } = false;
        public string WorkingType { get; set; } = "CONTRACT_PRICE"; // CONTRACT_PRICE, MARK_PRICE
        
        // 止损设置
        public decimal StopLossRatio { get; set; } // 止损比例
        public decimal StopLossPrice { get; set; } // 止损价
        public decimal StopLossAmount { get; set; } // 止损金额
        
        // 条件单判断
        public bool IsConditionalOrder => 
            Type == "STOP" || Type == "TAKE_PROFIT" || 
            Type == "STOP_MARKET" || Type == "TAKE_PROFIT_MARKET";
            
        public bool IsLimitConditionalOrder => 
            Type == "STOP" || Type == "TAKE_PROFIT";
    }
} 