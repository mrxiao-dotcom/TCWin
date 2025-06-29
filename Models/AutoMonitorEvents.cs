using System;
using System.Collections.Generic;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 自动盯盘事件基类
    /// </summary>
    public abstract class AutoMonitorEvent
    {
        /// <summary>
        /// 事件ID
        /// </summary>
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// 事件时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 事件类型
        /// </summary>
        public abstract string EventType { get; }
        
        /// <summary>
        /// 事件源（触发事件的组件）
        /// </summary>
        public string Source { get; set; } = string.Empty;
        
        /// <summary>
        /// 事件数据（JSON格式的额外信息）
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
        
        /// <summary>
        /// 事件优先级
        /// </summary>
        public EventPriority Priority { get; set; } = EventPriority.Normal;
    }

    /// <summary>
    /// 事件优先级
    /// </summary>
    public enum EventPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// 执行状态变更事件
    /// </summary>
    public class ExecutionStateChangedEvent : AutoMonitorEvent
    {
        public override string EventType => "ExecutionStateChanged";
        
        /// <summary>
        /// 合约标识
        /// </summary>
        public string ContractKey { get; set; } = string.Empty;
        
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 持仓方向
        /// </summary>
        public string PositionSide { get; set; } = string.Empty;
        
        /// <summary>
        /// 执行类型
        /// </summary>
        public ExecutionType ExecutionType { get; set; }
        
        /// <summary>
        /// 阶梯索引（如果适用）
        /// </summary>
        public int? TierIndex { get; set; }
        
        /// <summary>
        /// 触发时的浮盈
        /// </summary>
        public decimal TriggerPnl { get; set; }
        
        /// <summary>
        /// 是否执行成功
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// 执行消息
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// 执行前状态
        /// </summary>
        public string? PreviousState { get; set; }
        
        /// <summary>
        /// 执行后状态
        /// </summary>
        public string? NewState { get; set; }
    }

    /// <summary>
    /// 监控状态变更事件
    /// </summary>
    public class MonitorStatusChangedEvent : AutoMonitorEvent
    {
        public override string EventType => "MonitorStatusChanged";
        
        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning { get; set; }
        
        /// <summary>
        /// 状态变更消息
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// 配置信息
        /// </summary>
        public AutoMonitorConfig? Config { get; set; }
        
        /// <summary>
        /// 活跃合约数量
        /// </summary>
        public int ActiveContractCount { get; set; }
    }

    /// <summary>
    /// 持仓变化事件
    /// </summary>
    public class PositionChangedEvent : AutoMonitorEvent
    {
        public override string EventType => "PositionChanged";
        
        /// <summary>
        /// 变化类型
        /// </summary>
        public PositionChangeType ChangeType { get; set; }
        
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 持仓方向
        /// </summary>
        public string PositionSide { get; set; } = string.Empty;
        
        /// <summary>
        /// 持仓数量变化
        /// </summary>
        public decimal QuantityChange { get; set; }
        
        /// <summary>
        /// 当前持仓数量
        /// </summary>
        public decimal CurrentQuantity { get; set; }
        
        /// <summary>
        /// 当前浮盈
        /// </summary>
        public decimal CurrentPnl { get; set; }
        
        /// <summary>
        /// 变化原因
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// 持仓变化类型
    /// </summary>
    public enum PositionChangeType
    {
        Opened,      // 新开仓
        Increased,   // 加仓
        Decreased,   // 减仓
        Closed,      // 平仓
        Updated      // 其他更新
    }

    /// <summary>
    /// 冷却期事件
    /// </summary>
    public class CooldownEvent : AutoMonitorEvent
    {
        public override string EventType => "Cooldown";
        
        /// <summary>
        /// 冷却期动作类型
        /// </summary>
        public CooldownActionType ActionType { get; set; }
        
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 持仓方向
        /// </summary>
        public string PositionSide { get; set; } = string.Empty;
        
        /// <summary>
        /// 操作类型
        /// </summary>
        public CooldownOperationType OperationType { get; set; }
        
        /// <summary>
        /// 阶梯索引（如果适用）
        /// </summary>
        public int? TierIndex { get; set; }
        
        /// <summary>
        /// 冷却期时长（秒）
        /// </summary>
        public int CooldownSeconds { get; set; }
        
        /// <summary>
        /// 剩余冷却时间（秒）
        /// </summary>
        public double RemainingSeconds { get; set; }
    }

    /// <summary>
    /// 冷却期动作类型
    /// </summary>
    public enum CooldownActionType
    {
        Started,    // 开始冷却
        Blocked,    // 被冷却期阻止
        Expired,    // 冷却期到期
        Cleared     // 手动清理
    }

    /// <summary>
    /// 错误事件
    /// </summary>
    public class ErrorEvent : AutoMonitorEvent
    {
        public override string EventType => "Error";
        
        /// <summary>
        /// 错误类型
        /// </summary>
        public ErrorType ErrorType { get; set; }
        
        /// <summary>
        /// 错误代码
        /// </summary>
        public string? ErrorCode { get; set; }
        
        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// 异常详情
        /// </summary>
        public string? ExceptionDetails { get; set; }
        
        /// <summary>
        /// 相关合约（如果有）
        /// </summary>
        public string? Symbol { get; set; }
        
        /// <summary>
        /// 相关操作（如果有）
        /// </summary>
        public string? Operation { get; set; }
        
        /// <summary>
        /// 是否需要人工干预
        /// </summary>
        public bool RequiresIntervention { get; set; }
        
        /// <summary>
        /// 重试次数
        /// </summary>
        public int RetryCount { get; set; }
        
        public ErrorEvent()
        {
            Priority = EventPriority.High;
        }
    }

    /// <summary>
    /// 错误类型
    /// </summary>
    public enum ErrorType
    {
        ApiError,           // API调用错误
        NetworkError,       // 网络错误
        ValidationError,    // 数据验证错误
        CalculationError,   // 计算错误
        ConfigurationError, // 配置错误
        StateError,         // 状态错误
        UnknownError        // 未知错误
    }

    /// <summary>
    /// 止损单事件
    /// </summary>
    public class StopOrderEvent : AutoMonitorEvent
    {
        public override string EventType => "StopOrder";
        
        /// <summary>
        /// 止损单动作
        /// </summary>
        public StopOrderAction Action { get; set; }
        
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 止损单类型
        /// </summary>
        public StopOrderType StopOrderType { get; set; }
        
        /// <summary>
        /// 订单ID
        /// </summary>
        public string? OrderId { get; set; }
        
        /// <summary>
        /// 止损价格
        /// </summary>
        public decimal StopPrice { get; set; }
        
        /// <summary>
        /// 数量
        /// </summary>
        public decimal Quantity { get; set; }
        
        /// <summary>
        /// 执行结果
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// 错误消息（如果失败）
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 止损单动作
    /// </summary>
    public enum StopOrderAction
    {
        Creating,    // 正在创建
        Created,     // 创建成功
        Failed,      // 创建失败
        Replaced,    // 替换现有订单
        Cancelled,   // 取消订单
        Triggered    // 订单被触发
    }

    /// <summary>
    /// 性能统计事件
    /// </summary>
    public class PerformanceEvent : AutoMonitorEvent
    {
        public override string EventType => "Performance";
        
        /// <summary>
        /// 统计类型
        /// </summary>
        public PerformanceMetricType MetricType { get; set; }
        
        /// <summary>
        /// 指标名称
        /// </summary>
        public string MetricName { get; set; } = string.Empty;
        
        /// <summary>
        /// 指标值
        /// </summary>
        public double Value { get; set; }
        
        /// <summary>
        /// 指标单位
        /// </summary>
        public string Unit { get; set; } = string.Empty;
        
        /// <summary>
        /// 时间窗口（秒）
        /// </summary>
        public int TimeWindowSeconds { get; set; }
        
        /// <summary>
        /// 附加信息
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    /// <summary>
    /// 性能指标类型
    /// </summary>
    public enum PerformanceMetricType
    {
        ExecutionTime,      // 执行时间
        MemoryUsage,        // 内存使用
        ApiCallCount,       // API调用次数
        ErrorRate,          // 错误率
        ThroughputRate,     // 吞吐率
        SuccessRate         // 成功率
    }

    /// <summary>
    /// 数据同步事件
    /// </summary>
    public class DataSyncEvent : AutoMonitorEvent
    {
        public override string EventType => "DataSync";
        
        /// <summary>
        /// 同步类型
        /// </summary>
        public DataSyncType SyncType { get; set; }
        
        /// <summary>
        /// 同步的数据类型
        /// </summary>
        public string DataType { get; set; } = string.Empty;
        
        /// <summary>
        /// 同步的记录数
        /// </summary>
        public int RecordCount { get; set; }
        
        /// <summary>
        /// 同步是否成功
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// 同步耗时（毫秒）
        /// </summary>
        public long DurationMs { get; set; }
        
        /// <summary>
        /// 错误消息（如果失败）
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 数据同步类型
    /// </summary>
    public enum DataSyncType
    {
        Load,           // 加载数据
        Save,           // 保存数据
        Backup,         // 备份数据
        Restore,        // 恢复数据
        Cleanup         // 清理数据
    }

    /// <summary>
    /// 冷却期操作类型枚举（复制自CooldownManager）
    /// </summary>
    public enum CooldownOperationType
    {
        BreakEven,          // 保本止损
        AddPosition,        // 推仓操作
        ProfitProtection,   // 保盈止损
        ManualOrder,        // 手动下单
        StopLoss           // 止损单创建
    }

    /// <summary>
    /// 止损单类型枚举（复制自StopOrderManager）
    /// </summary>
    public enum StopOrderType
    {
        BreakEven,           // 保本止损
        AddPosition,         // 推仓止损
        ProfitProtection,    // 保盈止损
        Manual,              // 手动止损
        TrailingStop         // 移动止损
    }
} 