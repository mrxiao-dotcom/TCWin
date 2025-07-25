using System;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 监控状态变更事件参数
    /// </summary>
    public class MonitorStatusChangedEventArgs : EventArgs
    {
        public bool IsRunning { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool DisableEditing { get; set; }
        public AutoMonitorConfig? Config { get; set; }
        public int ActiveContractCount { get; set; }
    }

    /// <summary>
    /// 执行结果事件参数
    /// </summary>
    public class ExecutionResultEventArgs : EventArgs
    {
        public ExecutionHistory History { get; set; } = new ExecutionHistory();
        public string Symbol { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string ExecutionType { get; set; } = string.Empty;    // 新增：执行类型
        public bool IsSuccess { get; set; }
        public decimal TriggerPnl { get; set; }
        public decimal PnlAtExecution { get; set; }                  // 新增：执行时盈亏
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 工作日志事件参数
    /// </summary>
    public class WorkLogEventArgs : EventArgs
    {
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Category { get; set; } = string.Empty;
        public string? Exception { get; set; }
    }
} 