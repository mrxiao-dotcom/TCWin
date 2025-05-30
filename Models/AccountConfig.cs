using System;

namespace BinanceFuturesTrader.Models
{
    public class AccountConfig
    {
        public string Name { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public int RiskCapitalTimes { get; set; } = 10; // 风险金次数
        public bool IsTestNet { get; set; } = false;
        public DateTime LastModified { get; set; } = DateTime.Now;
    }
} 