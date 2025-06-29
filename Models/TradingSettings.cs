using System;

namespace BinanceFuturesTrader.Models
{
    public class TradingSettings
    {
        public string Symbol { get; set; } = "";
        public string Side { get; set; } = "BUY";
        public int Leverage { get; set; } = 10;
        public string MarginType { get; set; } = "CROSSED";
        public string OrderType { get; set; } = "MARKET";
        public decimal StopLossRatio { get; set; } = 10.0m;
        public string PositionSide { get; set; } = "BOTH";
        public DateTime LastSaved { get; set; } = DateTime.Now;
    }
} 