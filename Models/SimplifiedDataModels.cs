using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BinanceFuturesTrader.Models
{
    // ================================
    // 🎯 简化的基础配置模型（只存参数）
    // ================================

    /// <summary>
    /// 简化的基础配置文件根对象
    /// </summary>
    public class SimplifiedBaseConfigFile
    {
        [JsonPropertyName("configs")]
        public Dictionary<string, SimplifiedBaseConfig> Configs { get; set; } = new();
    }

    /// <summary>
    /// 简化的基础配置模型 - 只存储策略参数，不包含任何状态
    /// </summary>
    public class SimplifiedBaseConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("breakEvenConfig")]
        public SimplifiedBreakEvenConfig BreakEvenConfig { get; set; } = new();

        [JsonPropertyName("addPositionConfig")]
        public SimplifiedAddPositionConfig AddPositionConfig { get; set; } = new();

        [JsonPropertyName("profitProtectionConfig")]
        public SimplifiedProfitProtectionConfig ProfitProtectionConfig { get; set; } = new();
    }

    /// <summary>
    /// 简化的保本配置
    /// </summary>
    public class SimplifiedBreakEvenConfig
    {
        [JsonPropertyName("isEnabled")]
        public bool IsEnabled { get; set; } = true;

        [JsonPropertyName("triggerProfitAmount")]
        public decimal TriggerProfitAmount { get; set; } = 0m;
    }

    /// <summary>
    /// 简化的推仓配置
    /// </summary>
    public class SimplifiedAddPositionConfig
    {
        [JsonPropertyName("isEnabled")]
        public bool IsEnabled { get; set; } = true;

        [JsonPropertyName("tiers")]
        public List<SimplifiedAddPositionTier> Tiers { get; set; } = new();
    }

    /// <summary>
    /// 简化的推仓阶梯配置
    /// </summary>
    public class SimplifiedAddPositionTier
    {
        [JsonPropertyName("tierIndex")]
        public int TierIndex { get; set; }

        [JsonPropertyName("triggerProfitAmount")]
        public decimal TriggerProfitAmount { get; set; } = 0m;

        [JsonPropertyName("riskMultiplier")]
        public decimal RiskMultiplier { get; set; } = 1.0m;

        [JsonPropertyName("stopLossRatio")]
        public decimal StopLossRatio { get; set; } = 0.1m;
    }

    /// <summary>
    /// 简化的保盈配置
    /// </summary>
    public class SimplifiedProfitProtectionConfig
    {
        [JsonPropertyName("isEnabled")]
        public bool IsEnabled { get; set; } = true;

        [JsonPropertyName("tiers")]
        public List<SimplifiedProfitProtectionTier> Tiers { get; set; } = new();
    }

    /// <summary>
    /// 简化的保盈阶梯配置
    /// </summary>
    public class SimplifiedProfitProtectionTier
    {
        [JsonPropertyName("tierIndex")]
        public int TierIndex { get; set; }

        [JsonPropertyName("triggerProfitAmount")]
        public decimal TriggerProfitAmount { get; set; } = 0m;

        [JsonPropertyName("protectionAmount")]
        public decimal ProtectionAmount { get; set; } = 0m;
    }

    // ================================
    // 🎯 简化的统一状态模型（只存状态）
    // ================================

    /// <summary>
    /// 简化的统一状态文件根对象
    /// </summary>
    public class SimplifiedContractStatesFile
    {
        [JsonPropertyName("states")]
        public Dictionary<string, SimplifiedContractState> States { get; set; } = new();
    }

    /// <summary>
    /// 简化的合约状态模型 - 键值为 symbol_positionSide
    /// </summary>
    public class SimplifiedContractState
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [JsonPropertyName("positionSide")]
        public string PositionSide { get; set; } = string.Empty;

        [JsonPropertyName("configName")]
        public string ConfigName { get; set; } = string.Empty;

        [JsonPropertyName("lastUpdateTime")]
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("breakEvenConfig")]
        public SimplifiedBreakEvenState BreakEvenConfig { get; set; } = new();

        [JsonPropertyName("addPositionConfig")]
        public SimplifiedAddPositionState AddPositionConfig { get; set; } = new();

        [JsonPropertyName("profitProtectionConfig")]
        public SimplifiedProfitProtectionState ProfitProtectionConfig { get; set; } = new();
    }

    /// <summary>
    /// 简化的保本状态
    /// </summary>
    public class SimplifiedBreakEvenState
    {
        [JsonPropertyName("triggerProfitAmount")]
        public decimal TriggerProfitAmount { get; set; } = 0m;

        [JsonPropertyName("executionState")]
        public int ExecutionState { get; set; } = 0; // 0=未触发, 1=执行中, 2=已执行, 3=失败

        [JsonPropertyName("executionTime")]
        public DateTime? ExecutionTime { get; set; } = null;

        [JsonPropertyName("executionResult")]
        public string ExecutionResult { get; set; } = string.Empty;
    }

    /// <summary>
    /// 简化的推仓状态
    /// </summary>
    public class SimplifiedAddPositionState
    {
        [JsonPropertyName("tiers")]
        public List<SimplifiedAddPositionTierState> Tiers { get; set; } = new();
    }

    /// <summary>
    /// 简化的推仓阶梯状态
    /// </summary>
    public class SimplifiedAddPositionTierState
    {
        [JsonPropertyName("tierIndex")]
        public int TierIndex { get; set; }

        [JsonPropertyName("triggerProfitAmount")]
        public decimal TriggerProfitAmount { get; set; } = 0m;

        [JsonPropertyName("riskMultiplier")]
        public decimal RiskMultiplier { get; set; } = 1.0m;

        [JsonPropertyName("stopLossRatio")]
        public decimal StopLossRatio { get; set; } = 0.1m;

        [JsonPropertyName("executionState")]
        public int ExecutionState { get; set; } = 0; // 0=未触发, 1=执行中, 2=已执行, 3=失败

        [JsonPropertyName("executionTime")]
        public DateTime? ExecutionTime { get; set; } = null;

        [JsonPropertyName("executionResult")]
        public string ExecutionResult { get; set; } = string.Empty;
    }

    /// <summary>
    /// 简化的保盈状态
    /// </summary>
    public class SimplifiedProfitProtectionState
    {
        [JsonPropertyName("tiers")]
        public List<SimplifiedProfitProtectionTierState> Tiers { get; set; } = new();
    }

    /// <summary>
    /// 简化的保盈阶梯状态
    /// </summary>
    public class SimplifiedProfitProtectionTierState
    {
        [JsonPropertyName("tierIndex")]
        public int TierIndex { get; set; }

        [JsonPropertyName("triggerProfitAmount")]
        public decimal TriggerProfitAmount { get; set; } = 0m;

        [JsonPropertyName("protectionAmount")]
        public decimal ProtectionAmount { get; set; } = 0m;

        [JsonPropertyName("executionState")]
        public int ExecutionState { get; set; } = 0; // 0=未触发, 1=执行中, 2=已执行, 3=失败

        [JsonPropertyName("executionTime")]
        public DateTime? ExecutionTime { get; set; } = null;

        [JsonPropertyName("executionResult")]
        public string ExecutionResult { get; set; } = string.Empty;
    }

    // ================================
    // 🎯 ExecutionState 枚举（标准化）
    // ================================

    /// <summary>
    /// 标准化的执行状态枚举
    /// </summary>
    public enum StandardExecutionState
    {
        /// <summary>未触发</summary>
        NotTriggered = 0,
        
        /// <summary>执行中</summary>
        Executing = 1,
        
        /// <summary>已执行</summary>
        Executed = 2,
        
        /// <summary>执行失败</summary>
        Failed = 3
    }

    // ================================
    // 🎯 扩展方法（状态转换）
    // ================================

    /// <summary>
    /// 执行状态扩展方法
    /// </summary>
    public static class ExecutionStateExtensions
    {
        /// <summary>
        /// 转换为UI显示文本
        /// </summary>
        public static string ToDisplayText(this StandardExecutionState state)
        {
            return state switch
            {
                StandardExecutionState.NotTriggered => "-",
                StandardExecutionState.Executing => "执行中",
                StandardExecutionState.Executed => "√",
                StandardExecutionState.Failed => "❌",
                _ => "-"
            };
        }

        /// <summary>
        /// 从整数值转换为枚举
        /// </summary>
        public static StandardExecutionState FromInt(int value)
        {
            return value switch
            {
                0 => StandardExecutionState.NotTriggered,
                1 => StandardExecutionState.Executing,
                2 => StandardExecutionState.Executed,
                3 => StandardExecutionState.Failed,
                _ => StandardExecutionState.NotTriggered
            };
        }

        /// <summary>
        /// 检查是否可以执行（只有未触发状态才能执行）
        /// </summary>
        public static bool CanExecute(this StandardExecutionState state)
        {
            return state == StandardExecutionState.NotTriggered;
        }

        /// <summary>
        /// 检查是否已完成（已执行或失败）
        /// </summary>
        public static bool IsCompleted(this StandardExecutionState state)
        {
            return state == StandardExecutionState.Executed || state == StandardExecutionState.Failed;
        }
    }
} 