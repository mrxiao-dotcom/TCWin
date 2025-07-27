using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 执行状态枚举 - 统一状态管理
    /// </summary>
    public enum ExecutionState
    {
        /// <summary>未触发</summary>
        NotTriggered = 0,
        /// <summary>执行中</summary>
        Executing = 1,
        /// <summary>已执行</summary>
        Executed = 2
    }
    /// <summary>
    /// 合约监控状态 - 基于持仓数据+基础配置生成的完整监控状态
    /// 这是自动盯盘状态文件的核心结构，格式与基础配置文件一致，但每个阶梯增加了执行状态
    /// </summary>
    public class ContractMonitoringState
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
        /// 持仓方向（LONG/SHORT/BOTH）
        /// </summary>
        public string PositionSide { get; set; } = string.Empty;
        
        /// <summary>
        /// 基础配置名称
        /// </summary>
        public string BaseConfigName { get; set; } = string.Empty;
        
        /// <summary>
        /// 配置名称（继承自基础配置）
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// 是否启用监控
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 扫描间隔（秒）
        /// </summary>
        public int ScanIntervalSeconds { get; set; } = 5;
        
        /// <summary>
        /// 冷却期配置（秒）
        /// </summary>
        public int CooldownSeconds { get; set; } = 5;
        
        /// <summary>
        /// 初始持仓数量
        /// </summary>
        public decimal InitialQuantity { get; set; }
        
        /// <summary>
        /// 初始开仓价格
        /// </summary>
        public decimal InitialEntryPrice { get; set; }
        
        /// <summary>
        /// 当前持仓数量
        /// </summary>
        public decimal CurrentQuantity { get; set; }
        
        /// <summary>
        /// 当前开仓价格
        /// </summary>
        public decimal CurrentEntryPrice { get; set; }
        
        /// <summary>
        /// 当前标记价格
        /// </summary>
        public decimal CurrentMarkPrice { get; set; }
        
        /// <summary>
        /// 当前浮盈（关键字段）
        /// </summary>
        public decimal CurrentUnrealizedPnl { get; set; }
        
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
        /// 自动保本配置（含执行状态）
        /// </summary>
        public StatefulBreakEvenConfig BreakEvenConfig { get; set; } = new StatefulBreakEvenConfig();
        
        /// <summary>
        /// 自动推仓配置（含执行状态）
        /// </summary>
        public StatefulAddPositionConfig AddPositionConfig { get; set; } = new StatefulAddPositionConfig();
        
        /// <summary>
        /// 自动保盈止损配置（含执行状态）
        /// </summary>
        public StatefulProfitProtectionConfig ProfitProtectionConfig { get; set; } = new StatefulProfitProtectionConfig();
        
        /// <summary>
        /// 执行历史列表
        /// </summary>
        public List<ExecutionHistory> ExecutionHistories { get; set; } = new List<ExecutionHistory>();
    }
    
    /// <summary>
    /// 保本配置状态
    /// </summary>
    public class BreakEvenState
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// 触发盈利值（USDT）
        /// </summary>
        public decimal TriggerProfitAmount { get; set; } = 10.0m;
        
        /// <summary>
        /// 执行状态
        /// </summary>
        public ConfigNodeState State { get; set; } = new ConfigNodeState();
        
        /// <summary>
        /// 描述
        /// </summary>
        public string Description => $"当浮盈达到 {TriggerProfitAmount:F2} USDT 时自动设置保本止损";
    }
    
    /// <summary>
    /// 推仓配置状态
    /// </summary>
    public class AddPositionState
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// 推仓阶梯状态列表
        /// </summary>
        public List<AddPositionTierState> Tiers { get; set; } = new List<AddPositionTierState>();
    }
    
    /// <summary>
    /// 推仓阶梯状态
    /// </summary>
    public class AddPositionTierState
    {
        /// <summary>
        /// 阶梯序号
        /// </summary>
        public int TierIndex { get; set; }
        
        /// <summary>
        /// 是否启用此阶梯
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 触发盈利值（USDT）
        /// </summary>
        public decimal TriggerProfitAmount { get; set; }
        
        /// <summary>
        /// 推仓风险金倍数
        /// </summary>
        public decimal RiskMultiplier { get; set; }
        
        /// <summary>
        /// 推仓止损比例
        /// </summary>
        public decimal StopLossRatio { get; set; }
        
        /// <summary>
        /// 保盈金额（USDT）
        /// </summary>
        public decimal ProfitProtectionAmount { get; set; } = 0m;
        
        /// <summary>
        /// 推仓止损目标（USDT）
        /// </summary>
        public decimal ExitTargetPnl { get; set; } = 0m;
        
        /// <summary>
        /// 执行状态
        /// </summary>
        public ConfigNodeState State { get; set; } = new ConfigNodeState();
        
        /// <summary>
        /// 描述
        /// </summary>
        public string Description => $"阶梯{TierIndex}: 盈利{TriggerProfitAmount:F2}U → 推仓{RiskMultiplier:F1}倍风险金, 止损{StopLossRatio * 100:F1}%, 保盈{ProfitProtectionAmount:F0}U, 目标{ExitTargetPnl:F0}U";
    }
    
    /// <summary>
    /// 保盈配置状态
    /// </summary>
    public class ProfitProtectionState
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// 保盈阶梯状态列表
        /// </summary>
        public List<ProfitProtectionTierState> Tiers { get; set; } = new List<ProfitProtectionTierState>();
    }
    
    /// <summary>
    /// 保盈阶梯状态
    /// </summary>
    public class ProfitProtectionTierState
    {
        /// <summary>
        /// 阶梯序号
        /// </summary>
        public int TierIndex { get; set; }
        
        /// <summary>
        /// 是否启用此阶梯
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 触发盈利值（USDT）
        /// </summary>
        public decimal TriggerProfitAmount { get; set; }
        
        /// <summary>
        /// 保盈金额（USDT）
        /// </summary>
        public decimal ProtectionAmount { get; set; }
        
        /// <summary>
        /// 执行状态
        /// </summary>
        public ConfigNodeState State { get; set; } = new ConfigNodeState();
        
        /// <summary>
        /// 描述
        /// </summary>
        public string Description => $"阶梯{TierIndex}: 盈利{TriggerProfitAmount:F2}U → 保护{ProtectionAmount:F2}U盈利";
    }
    
    /// <summary>
    /// 配置节点状态
    /// </summary>
    public class ConfigNodeState
    {
        /// <summary>
        /// 是否已触发
        /// </summary>
        public bool IsTriggered { get; set; } = false;
        
        /// <summary>
        /// 是否已执行
        /// </summary>
        public bool IsExecuted { get; set; } = false;
        
        /// <summary>
        /// 触发时间
        /// </summary>
        public DateTime? TriggerTime { get; set; }
        
        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime? ExecutionTime { get; set; }
        
        /// <summary>
        /// 触发时的浮盈
        /// </summary>
        public decimal TriggerPnl { get; set; } = 0m;
        
        /// <summary>
        /// 执行结果
        /// </summary>
        public string ExecutionResult { get; set; } = string.Empty;
        
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// 订单ID（如果有下单操作）
        /// </summary>
        public long? OrderId { get; set; }
        
        /// <summary>
        /// 状态描述
        /// </summary>
        public string StatusText
        {
            get
            {
                if (!IsTriggered) return "未触发";
                if (IsTriggered && !IsExecuted) return "已触发";
                if (IsExecuted && !string.IsNullOrEmpty(ErrorMessage)) return "执行失败";
                if (IsExecuted) return "已执行";
                return "未知状态";
            }
        }
    }

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
        /// 是否为模拟执行（true=模拟，false=实盘）
        /// </summary>
        public bool IsSimulation { get; set; } = false;
        
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

    /// <summary>
    /// 带执行状态的保本配置（继承基础配置，增加执行状态）
    /// </summary>
    public class StatefulBreakEvenConfig
    {
        /// <summary>
        /// 是否启用自动保本
        /// </summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// 触发盈利值（USDT）
        /// </summary>
        public decimal TriggerProfitAmount { get; set; } = 10.0m;
        
        /// <summary>
        /// 执行状态 (0=未触发, 1=已执行)
        /// </summary>
        public ExecutionState ExecutionState { get; set; } = ExecutionState.NotTriggered;
        
        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime? ExecutionTime { get; set; }
        
        /// <summary>
        /// 执行时的浮盈
        /// </summary>
        public decimal ExecutionPnl { get; set; } = 0m;
        
        /// <summary>
        /// 是否已执行 (兼容性属性)
        /// </summary>
        public bool IsExecuted 
        { 
            get => ExecutionState == ExecutionState.Executed;
            set => ExecutionState = value ? ExecutionState.Executed : ExecutionState.NotTriggered;
        }
        
        /// <summary>
        /// 执行状态显示文本
        /// </summary>
        [JsonIgnore]
        public string ExecutionStatusDisplay => ExecutionState == ExecutionState.NotTriggered ? StatusConstants.Waiting : StatusConstants.Executed;
        
        /// <summary>
        /// UI显示符号 (0="-", 1="✓")
        /// </summary>
        [JsonIgnore]
        public string StatusSymbol => ExecutionState == ExecutionState.NotTriggered ? StatusConstants.WaitingSymbol : StatusConstants.ExecutedSymbol;
        
        /// <summary>
        /// 执行状态描述 (兼容性属性)
        /// </summary>
        [JsonIgnore]
        public string ExecutionStatus
        {
            get => ExecutionState == ExecutionState.NotTriggered ? StatusConstants.Waiting : StatusConstants.Executed;
            set => ExecutionState = value == StatusConstants.ExecutedChinese ? ExecutionState.Executed : ExecutionState.NotTriggered;
        }
        
        /// <summary>
        /// 执行结果
        /// </summary>
        public string ExecutionResult { get; set; } = "";
    }

    /// <summary>
    /// 带执行状态的推仓配置（继承基础配置，每个阶梯增加执行状态）
    /// </summary>
    public class StatefulAddPositionConfig
    {
        /// <summary>
        /// 是否启用自动推仓
        /// </summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// 带执行状态的推仓阶梯列表
        /// </summary>
        public List<StatefulAddPositionTier> Tiers { get; set; } = new List<StatefulAddPositionTier>();
    }

    /// <summary>
    /// 带执行状态的推仓阶梯（继承基础配置，增加执行状态）
    /// </summary>
    public class StatefulAddPositionTier
    {
        /// <summary>
        /// 阶梯序号（1-4）
        /// </summary>
        public int TierIndex { get; set; }
        
        /// <summary>
        /// 是否启用此阶梯
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 触发盈利值（USDT）
        /// </summary>
        public decimal TriggerProfitAmount { get; set; }
        
        /// <summary>
        /// 推仓风险金倍数
        /// </summary>
        public decimal RiskMultiplier { get; set; }
        
        /// <summary>
        /// 推仓止损比例（0.02 = 2%）
        /// </summary>
        public decimal StopLossRatio { get; set; }
        
        /// <summary>
        /// 保盈金额（USDT）
        /// </summary>
        public decimal ProfitProtectionAmount { get; set; } = 0m;
        
        /// <summary>
        /// 推仓止损目标（USDT）
        /// </summary>
        public decimal ExitTargetPnl { get; set; } = 0m;
        
        /// <summary>
        /// 执行状态 (0=未触发, 1=已执行)
        /// </summary>
        public ExecutionState ExecutionState { get; set; } = ExecutionState.NotTriggered;
        
        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime? ExecutionTime { get; set; }
        
        /// <summary>
        /// 执行时的浮盈
        /// </summary>
        public decimal ExecutionPnl { get; set; } = 0m;
        
        /// <summary>
        /// 执行结果
        /// </summary>
        public string ExecutionResult { get; set; } = "";
        
        /// <summary>
        /// 是否已执行 (兼容性属性)
        /// </summary>
        public bool IsExecuted 
        { 
            get => ExecutionState == ExecutionState.Executed;
            set => ExecutionState = value ? ExecutionState.Executed : ExecutionState.NotTriggered;
        }
        
        /// <summary>
        /// 执行状态显示文本
        /// </summary>
        [JsonIgnore]
        public string ExecutionStatusDisplay => ExecutionState == ExecutionState.NotTriggered ? StatusConstants.Waiting : StatusConstants.Executed;
        
        /// <summary>
        /// UI显示符号 (0="-", 1="✓")
        /// </summary>
        [JsonIgnore]
        public string StatusSymbol => ExecutionState == ExecutionState.NotTriggered ? StatusConstants.WaitingSymbol : StatusConstants.ExecutedSymbol;
        
        /// <summary>
        /// 执行状态描述 (兼容性属性)
        /// </summary>
        [JsonIgnore]
        public string ExecutionStatus
        {
            get => ExecutionState == ExecutionState.NotTriggered ? StatusConstants.Waiting : StatusConstants.Executed;
            set => ExecutionState = value == StatusConstants.ExecutedChinese ? ExecutionState.Executed : ExecutionState.NotTriggered;
        }
        
        /// <summary>
        /// 加仓数量
        /// </summary>
        [JsonIgnore]
        public decimal AddPositionQuantity { get; set; } = 0m;
        
        /// <summary>
        /// 止损价格
        /// </summary>
        public decimal StopLossPrice { get; set; } = 0m;
    }

    /// <summary>
    /// 带执行状态的保盈配置（继承基础配置，每个阶梯增加执行状态）
    /// </summary>
    public class StatefulProfitProtectionConfig
    {
        /// <summary>
        /// 是否启用自动保盈止损
        /// </summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// 带执行状态的保盈止损阶梯列表
        /// </summary>
        public List<StatefulProfitProtectionTier> Tiers { get; set; } = new List<StatefulProfitProtectionTier>();
    }

    /// <summary>
    /// 带执行状态的保盈止损阶梯（继承基础配置，增加执行状态）
    /// </summary>
    public class StatefulProfitProtectionTier
    {
        /// <summary>
        /// 阶梯序号（1-3）
        /// </summary>
        public int TierIndex { get; set; }
        
        /// <summary>
        /// 是否启用此阶梯
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 触发盈利值（USDT）
        /// </summary>
        public decimal TriggerProfitAmount { get; set; }
        
        /// <summary>
        /// 保护金额（USDT）
        /// </summary>
        public decimal ProtectionAmount { get; set; }
        
        /// <summary>
        /// 执行状态 (0=未触发, 1=已执行)
        /// </summary>
        public ExecutionState ExecutionState { get; set; } = ExecutionState.NotTriggered;
        
        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime? ExecutionTime { get; set; }
        
        /// <summary>
        /// 执行时的浮盈
        /// </summary>
        public decimal ExecutionPnl { get; set; } = 0m;
        
        /// <summary>
        /// 执行结果
        /// </summary>
        public string ExecutionResult { get; set; } = "";
        
        /// <summary>
        /// 是否已执行 (兼容性属性)
        /// </summary>
        public bool IsExecuted 
        { 
            get => ExecutionState == ExecutionState.Executed;
            set => ExecutionState = value ? ExecutionState.Executed : ExecutionState.NotTriggered;
        }
        
        /// <summary>
        /// 执行状态显示文本
        /// </summary>
        [JsonIgnore]
        public string ExecutionStatusDisplay => ExecutionState == ExecutionState.NotTriggered ? StatusConstants.Waiting : StatusConstants.Executed;
        
        /// <summary>
        /// UI显示符号 (0="-", 1="✓")
        /// </summary>
        [JsonIgnore]
        public string StatusSymbol => ExecutionState == ExecutionState.NotTriggered ? StatusConstants.WaitingSymbol : StatusConstants.ExecutedSymbol;
        
        /// <summary>
        /// 执行状态描述 (兼容性属性)
        /// </summary>
        [JsonIgnore]
        public string ExecutionStatus
        {
            get => ExecutionState == ExecutionState.NotTriggered ? StatusConstants.Waiting : StatusConstants.Executed;
            set => ExecutionState = value == StatusConstants.ExecutedChinese ? ExecutionState.Executed : ExecutionState.NotTriggered;
        }
        
        /// <summary>
        /// 止损价格
        /// </summary>
        [JsonIgnore]
        public decimal StopLossPrice { get; set; } = 0m;
    }
} 