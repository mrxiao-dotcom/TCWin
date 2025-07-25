using System;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 合约状态信息
    /// </summary>
    public class ContractStatus
    {
        public string Symbol { get; set; } = string.Empty;
        public string PositionSide { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public int TriggerCount { get; set; }
        public int ExecutedCount { get; set; }
    }
} 