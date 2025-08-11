using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BinanceFuturesTrader.Models
{
    // ================================
    // 基础配置相关模型
    // ================================

    /// <summary>
    /// 基础配置文件根对象
    /// </summary>
    public class BaseConfigFile
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0";

        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("current_config_id")]
        public string CurrentConfigId { get; set; } = string.Empty;

        [JsonPropertyName("configs")]
        public List<BaseConfig> Configs { get; set; } = new();
    }

    /// <summary>
    /// 基础配置模型
    /// </summary>
    public class BaseConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("global_settings")]
        public GlobalSettings GlobalSettings { get; set; } = new();

        [JsonPropertyName("breakeven_config")]
        public BreakevenConfig BreakevenConfig { get; set; } = new();

        [JsonPropertyName("add_position_config")]
        public AddPositionConfig AddPositionConfig { get; set; } = new();

        [JsonPropertyName("profit_protection_config")]
        public ProfitProtectionConfig ProfitProtectionConfig { get; set; } = new();
    }

    /// <summary>
    /// 全局设置
    /// </summary>
    public class GlobalSettings
    {
        [JsonPropertyName("scan_interval_seconds")]
        public int ScanIntervalSeconds { get; set; } = 5;

        [JsonPropertyName("cooldown_seconds")]
        public int CooldownSeconds { get; set; } = 5;
    }

    /// <summary>
    /// 保本配置
    /// </summary>
    public class BreakevenConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("trigger_profit_amount")]
        public decimal TriggerProfitAmount { get; set; } = 0m;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// 推仓配置
    /// </summary>
    public class AddPositionConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("tiers")]
        public List<BaseAddPositionTier> Tiers { get; set; } = new();
    }

    /// <summary>
    /// 基础配置中的推仓阶梯
    /// </summary>
    public class BaseAddPositionTier
    {
        [JsonPropertyName("tier_index")]
        public int TierIndex { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("trigger_profit_amount")]
        public decimal TriggerProfitAmount { get; set; } = 0m;

        [JsonPropertyName("risk_multiplier")]
        public decimal RiskMultiplier { get; set; } = 1.0m;

        [JsonPropertyName("stop_loss_ratio")]
        public decimal StopLossRatio { get; set; } = 0.02m;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// 保盈配置
    /// </summary>
    public class ProfitProtectionConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("tiers")]
        public List<BaseProfitProtectionTier> Tiers { get; set; } = new();
    }

    /// <summary>
    /// 基础配置中的保盈阶梯
    /// </summary>
    public class BaseProfitProtectionTier
    {
        [JsonPropertyName("tier_index")]
        public int TierIndex { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("trigger_profit_amount")]
        public decimal TriggerProfitAmount { get; set; } = 0m;

        [JsonPropertyName("protection_amount")]
        public decimal ProtectionAmount { get; set; } = 0m;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    // ================================
    // 合约状态相关模型
    // ================================

    /// <summary>
    /// 合约状态文件根对象
    /// </summary>
    public class ContractStateFile
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0";

        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("current_config_id")]
        public string CurrentConfigId { get; set; } = string.Empty;

        [JsonPropertyName("account_name")]
        public string AccountName { get; set; } = string.Empty;

        [JsonPropertyName("states")]
        public Dictionary<string, ContractState> States { get; set; } = new();
    }

    /// <summary>
    /// 合约状态模型
    /// </summary>
    public class ContractState
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [JsonPropertyName("position_side")]
        public string PositionSide { get; set; } = string.Empty;

        [JsonPropertyName("base_config_id")]
        public string BaseConfigId { get; set; } = string.Empty;

        [JsonPropertyName("position_info")]
        public ContractPositionInfo PositionInfo { get; set; } = new();

        [JsonPropertyName("execution_states")]
        public ExecutionStates ExecutionStates { get; set; } = new();

        [JsonPropertyName("meta")]
        public ContractStateMeta Meta { get; set; } = new();

        /// <summary>
        /// 获取合约键名
        /// </summary>
        public string GetKey() => $"{Symbol}_{PositionSide}";
    }

    /// <summary>
    /// 合约状态中的持仓信息
    /// </summary>
    public class ContractPositionInfo
    {
        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; } = 0m;

        [JsonPropertyName("entry_price")]
        public decimal EntryPrice { get; set; } = 0m;

        [JsonPropertyName("current_price")]
        public decimal CurrentPrice { get; set; } = 0m;

        [JsonPropertyName("unrealized_pnl")]
        public decimal UnrealizedPnl { get; set; } = 0m;

        [JsonPropertyName("last_price_update")]
        public DateTime LastPriceUpdate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 执行状态集合
    /// </summary>
    public class ExecutionStates
    {
        [JsonPropertyName("breakeven")]
        public BreakevenExecutionState Breakeven { get; set; } = new();

        [JsonPropertyName("add_position_tiers")]
        public List<AddPositionExecutionState> AddPositionTiers { get; set; } = new();

        [JsonPropertyName("profit_protection_tiers")]
        public List<ProfitProtectionExecutionState> ProfitProtectionTiers { get; set; } = new();
    }

    /// <summary>
    /// 保本执行状态
    /// </summary>
    public class BreakevenExecutionState
    {
        [JsonPropertyName("state")]
        public string State { get; set; } = "NOT_TRIGGERED";

        [JsonPropertyName("trigger_amount")]
        public decimal TriggerAmount { get; set; } = 0m;

        [JsonPropertyName("executed_at")]
        public DateTime? ExecutedAt { get; set; }

        [JsonPropertyName("execution_pnl")]
        public decimal ExecutionPnl { get; set; } = 0m;
    }

    /// <summary>
    /// 推仓执行状态
    /// </summary>
    public class AddPositionExecutionState
    {
        [JsonPropertyName("tier_index")]
        public int TierIndex { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; } = "NOT_TRIGGERED";

        [JsonPropertyName("trigger_amount")]
        public decimal TriggerAmount { get; set; } = 0m;

        [JsonPropertyName("executed_at")]
        public DateTime? ExecutedAt { get; set; }

        [JsonPropertyName("execution_pnl")]
        public decimal ExecutionPnl { get; set; } = 0m;
    }

    /// <summary>
    /// 保盈执行状态
    /// </summary>
    public class ProfitProtectionExecutionState
    {
        [JsonPropertyName("tier_index")]
        public int TierIndex { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; } = "NOT_TRIGGERED";

        [JsonPropertyName("trigger_amount")]
        public decimal TriggerAmount { get; set; } = 0m;

        [JsonPropertyName("protection_amount")]
        public decimal ProtectionAmount { get; set; } = 0m;

        [JsonPropertyName("executed_at")]
        public DateTime? ExecutedAt { get; set; }

        [JsonPropertyName("execution_pnl")]
        public decimal ExecutionPnl { get; set; } = 0m;
    }

    /// <summary>
    /// 合约状态元数据
    /// </summary>
    public class ContractStateMeta
    {
        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; } = true;

        [JsonPropertyName("last_scan_at")]
        public DateTime? LastScanAt { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("scan_count")]
        public long ScanCount { get; set; } = 0;

        [JsonPropertyName("config_reset_at")]
        public DateTime? ConfigResetAt { get; set; }
    }

    // ================================
    // 执行历史相关模型
    // ================================

    /// <summary>
    /// 执行历史文件根对象
    /// </summary>
    public class ExecutionHistoryFile
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0";

        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("history")]
        public List<ContractExecutionHistory> History { get; set; } = new();
    }

    /// <summary>
    /// 合约执行历史记录
    /// </summary>
    public class ContractExecutionHistory
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("contract_key")]
        public string ContractKey { get; set; } = string.Empty;

        [JsonPropertyName("execution_type")]
        public string ExecutionType { get; set; } = string.Empty; // BREAKEVEN, ADD_POSITION, PROFIT_PROTECTION, POSITION_CLOSED

        [JsonPropertyName("tier_index")]
        public int? TierIndex { get; set; }

        [JsonPropertyName("trigger_pnl")]
        public decimal TriggerPnl { get; set; } = 0m;

        [JsonPropertyName("execution_result")]
        public string ExecutionResult { get; set; } = string.Empty; // SUCCESS, FAILED

        [JsonPropertyName("execution_details")]
        public string ExecutionDetails { get; set; } = string.Empty;

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; } = string.Empty;
    }

    // ================================
    // 保存触发器枚举
    // ================================

    /// <summary>
    /// 保存触发类型
    /// </summary>
    public enum SaveTrigger
    {
        ExecutionCompleted,    // 执行操作后
        ConfigurationChanged,  // 配置修改后
        ConfigurationSwitched, // 配置切换后  
        PositionClosed,        // 持仓平仓后
        PeriodicSave          // 定期保存
    }

    // ================================
    // 执行状态常量
    // ================================

    /// <summary>
    /// 执行状态常量
    /// </summary>
    public static class ExecutionStateTypes
    {
        public const string NotTriggered = "NOT_TRIGGERED";
        public const string Executing = "EXECUTING";
        public const string Executed = "EXECUTED";
    }

    /// <summary>
    /// 执行类型常量
    /// </summary>
    public static class ExecutionTypes
    {
        public const string Breakeven = "BREAKEVEN";
        public const string AddPosition = "ADD_POSITION";
        public const string ProfitProtection = "PROFIT_PROTECTION";
        public const string PositionClosed = "POSITION_CLOSED";
        public const string ConfigurationSwitched = "CONFIGURATION_SWITCHED";
    }

    /// <summary>
    /// 执行结果常量
    /// </summary>
    public static class ExecutionResults
    {
        public const string Success = "SUCCESS";
        public const string Failed = "FAILED";
    }
} 