using System;
using System.ComponentModel;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 移动止损模式
    /// </summary>
    public enum TrailingStopMode
    {
        [Description("替换模式")]
        Replace = 0,

        [Description("并存模式")]
        Coexist = 1,

        [Description("智能分层模式")]
        SmartLayering = 2
    }

    /// <summary>
    /// 移动止损配置
    /// </summary>
    public class TrailingStopConfig
    {
        /// <summary>
        /// 移动止损模式
        /// </summary>
        public TrailingStopMode Mode { get; set; } = TrailingStopMode.Coexist;

        /// <summary>
        /// 移动止损数量分配比例（0.1-1.0）
        /// </summary>
        public decimal AllocationRatio { get; set; } = 0.3m;

        /// <summary>
        /// 是否启用智能分层
        /// </summary>
        public bool EnableSmartLayering { get; set; } = false;

        /// <summary>
        /// 分层模式：固定止损比例（0.1-1.0）
        /// </summary>
        public decimal FixedStopRatio { get; set; } = 0.7m;

        /// <summary>
        /// 分层模式：移动止损比例（0.1-1.0）
        /// </summary>
        public decimal TrailingStopRatio { get; set; } = 0.3m;

        /// <summary>
        /// 最小移动止损回调率（百分比）
        /// </summary>
        public decimal MinCallbackRate { get; set; } = 1.0m;

        /// <summary>
        /// 最大移动止损回调率（百分比）
        /// </summary>
        public decimal MaxCallbackRate { get; set; } = 10.0m;

        /// <summary>
        /// 是否只对盈利持仓启用
        /// </summary>
        public bool OnlyForProfitablePositions { get; set; } = true;
    }

    /// <summary>
    /// 移动止损状态
    /// </summary>
    public class TrailingStopStatus
    {
        /// <summary>
        /// 合约符号
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 移动止损订单ID
        /// </summary>
        public long TrailingOrderId { get; set; }

        /// <summary>
        /// 固定止损订单ID（如果有）
        /// </summary>
        public long? FixedOrderId { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 移动止损数量
        /// </summary>
        public decimal TrailingQuantity { get; set; }

        /// <summary>
        /// 固定止损数量（如果有）
        /// </summary>
        public decimal? FixedQuantity { get; set; }

        /// <summary>
        /// 回调率
        /// </summary>
        public decimal CallbackRate { get; set; }

        /// <summary>
        /// 模式
        /// </summary>
        public TrailingStopMode Mode { get; set; }

        /// <summary>
        /// 状态描述
        /// </summary>
        public string Status { get; set; } = "活跃";

        /// <summary>
        /// 是否活跃
        /// </summary>
        public bool IsActive => Status == "活跃";
    }
} 