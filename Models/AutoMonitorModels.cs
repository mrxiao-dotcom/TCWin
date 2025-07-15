using System;
using System.Collections.Generic;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 持仓档案信息
    /// </summary>
    public class PositionProfile
    {
        /// <summary>
        /// 档案ID
        /// </summary>
        public string ArchiveId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 持仓方向（LONG/SHORT）
        /// </summary>
        public string PositionSide { get; set; } = string.Empty;
        
        /// <summary>
        /// 初始持仓数量
        /// </summary>
        public decimal InitialQuantity { get; set; }
        
        /// <summary>
        /// 初始开仓价格
        /// </summary>
        public decimal InitialEntryPrice { get; set; }
        
        /// <summary>
        /// 建档时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 是否还活跃（持仓是否还存在）
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// 触发记录字典
        /// </summary>
        public Dictionary<string, TriggerRecord> TriggerRecords { get; set; } = new Dictionary<string, TriggerRecord>();
        
        /// <summary>
        /// 执行历史列表
        /// </summary>
        public List<ExecutionHistory> ExecutionHistories { get; set; } = new List<ExecutionHistory>();
    }

    /// <summary>
    /// 触发记录
    /// </summary>
    public class TriggerRecord
    {
        /// <summary>
        /// 记录ID
        /// </summary>
        public string RecordId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// 档案ID
        /// </summary>
        public string ArchiveId { get; set; } = string.Empty;
        
        /// <summary>
        /// 触发类型名称
        /// </summary>
        public string TriggerType { get; set; } = string.Empty;
        
        /// <summary>
        /// 阶梯索引（推仓和保盈止损有阶梯）
        /// </summary>
        public int? TierIndex { get; set; }
        
        /// <summary>
        /// 触发时的浮盈值
        /// </summary>
        public decimal TriggerPnl { get; set; }
        
        /// <summary>
        /// 触发时间
        /// </summary>
        public DateTime TriggerTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 是否已执行
        /// </summary>
        public bool IsExecuted { get; set; } = false;
        
        /// <summary>
        /// 执行结果描述
        /// </summary>
        public string ExecutionResult { get; set; } = string.Empty;
    }

    /// <summary>
    /// 执行历史
    /// </summary>
    public class ExecutionHistory
    {
        /// <summary>
        /// 历史ID
        /// </summary>
        public string HistoryId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// 档案ID
        /// </summary>
        public string ArchiveId { get; set; } = string.Empty;
        
        /// <summary>
        /// 触发记录ID
        /// </summary>
        public string TriggerRecordId { get; set; } = string.Empty;
        
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
        public string ExecutionType { get; set; } = string.Empty;
        
        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime ExecutionTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 执行结果
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// 触发时的浮盈
        /// </summary>
        public decimal TriggerPnl { get; set; }
        
        /// <summary>
        /// 详细信息
        /// </summary>
        public string Details { get; set; } = string.Empty;
        
        /// <summary>
        /// 订单ID（如果有下单操作）
        /// </summary>
        public long? OrderId { get; set; }
        
        /// <summary>
        /// 执行参数JSON
        /// </summary>
        public string ExecutionParams { get; set; } = string.Empty;
        
        /// <summary>
        /// 执行结果描述
        /// </summary>
        public string ResultMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// 错误信息（如果执行失败）
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// 自动盯盘运行状态
    /// </summary>
    public class AutoMonitorStatus
    {
        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning { get; set; } = false;
        
        /// <summary>
        /// 启动时间
        /// </summary>
        public DateTime? StartTime { get; set; }
        
        /// <summary>
        /// 最后扫描时间
        /// </summary>
        public DateTime? LastScanTime { get; set; }
        
        /// <summary>
        /// 扫描次数
        /// </summary>
        public int ScanCount { get; set; } = 0;
        
        /// <summary>
        /// 活跃档案数量
        /// </summary>
        public int ActiveArchiveCount { get; set; } = 0;
        
        /// <summary>
        /// 总触发次数
        /// </summary>
        public int TotalTriggerCount { get; set; } = 0;
        
        /// <summary>
        /// 成功执行次数
        /// </summary>
        public int SuccessExecutionCount { get; set; } = 0;
        
        /// <summary>
        /// 失败执行次数
        /// </summary>
        public int FailedExecutionCount { get; set; } = 0;
        
        /// <summary>
        /// 状态描述
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// 最后错误信息
        /// </summary>
        public string LastError { get; set; } = string.Empty;
    }

    /// <summary>
    /// 推仓阶梯配置
    /// </summary>
    public class AddPositionStageConfig
    {
        /// <summary>
        /// 阶梯索引
        /// </summary>
        public int Stage { get; set; }
        
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 触发盈利值
        /// </summary>
        public decimal TriggerProfitAmount { get; set; }
        
        /// <summary>
        /// 风险金倍数
        /// </summary>
        public decimal RiskCapitalMultiplier { get; set; }
        
        /// <summary>
        /// 止损比例
        /// </summary>
        public decimal StopLossPercentage { get; set; }
        
        /// <summary>
        /// 保盈金额（USDT）
        /// 设置范围：负数（最小负一倍风险金）到正数（最大为当前推仓阶梯触发值）
        /// 0表示保本止损，负数表示允许亏损，正数表示保护盈利
        /// </summary>
        public decimal ProfitProtectionAmount { get; set; } = 0m;
    }

    /// <summary>
    /// 保盈止损阶梯配置
    /// </summary>
    public class ProfitProtectionStageConfig
    {
        /// <summary>
        /// 阶梯索引
        /// </summary>
        public int Stage { get; set; }
        
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 触发盈利值
        /// </summary>
        public decimal TriggerProfitAmount { get; set; }
        
        /// <summary>
        /// 保护金额
        /// </summary>
        public decimal ProtectionAmount { get; set; }
    }

    /// <summary>
    /// 合约状态显示模型（供监控面板使用）
    /// </summary>
    public class ContractStateDisplay
    {
        public string Symbol { get; set; } = "";
        public string PositionSide { get; set; } = "";
        public bool BreakEvenExecuted { get; set; }
        public int AddPositionProgress { get; set; }
        public int ProfitProtectionProgress { get; set; }
        public int TotalExecutions { get; set; }
        public DateTime LastExecutionTime { get; set; }
    }

    /// <summary>
    /// 执行历史记录（增强版）
    /// </summary>
    public class ExecutionHistoryRecord
    {
        public DateTime ExecutionTime { get; set; }
        public string AccountName { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string ExecutionType { get; set; } = "";
        public bool IsSuccess { get; set; }
        public decimal TriggerPnl { get; set; }
        public string ErrorMessage { get; set; } = "";
        public string Description { get; set; } = "";
    }
} 