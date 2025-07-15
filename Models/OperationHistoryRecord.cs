using System;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 操作历史记录模型
    /// </summary>
    public class OperationHistoryRecord
    {
        /// <summary>
        /// 操作时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 操作类型描述
        /// </summary>
        public string Operation { get; set; } = "";

        /// <summary>
        /// 合约名称
        /// </summary>
        public string ContractName { get; set; } = "";

        /// <summary>
        /// 操作详情
        /// </summary>
        public string Details { get; set; } = "";

        /// <summary>
        /// 操作分类
        /// </summary>
        public string OperationType { get; set; } = "";

        /// <summary>
        /// 操作用户
        /// </summary>
        public string Username { get; set; } = "";

        /// <summary>
        /// 格式化显示的时间
        /// </summary>
        public string FormattedTime => Timestamp.ToString("HH:mm:ss");

        /// <summary>
        /// 格式化显示的完整信息
        /// </summary>
        public string FormattedInfo => $"[{FormattedTime}] {Operation} - {ContractName} - {Details}";
    }
} 