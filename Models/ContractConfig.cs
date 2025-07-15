using System;
using System.Collections.Generic;
using System.Linq;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 合约配置 - 基于基础配置为特定合约生成的具体配置
    /// </summary>
    public class ContractConfig
    {
        /// <summary>
        /// 合约标识
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 持仓方向
        /// </summary>
        public string PositionSide { get; set; } = string.Empty;

        /// <summary>
        /// 合约唯一键
        /// </summary>
        public string ContractKey { get; set; } = string.Empty;

        /// <summary>
        /// 基础配置名称
        /// </summary>
        public string BaseConfigName { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; } = DateTime.Now;

        #region 当前持仓信息

        /// <summary>
        /// 当前持仓数量
        /// </summary>
        public decimal CurrentPositionAmt { get; set; }

        /// <summary>
        /// 当前开仓价格
        /// </summary>
        public decimal CurrentEntryPrice { get; set; }

        /// <summary>
        /// 当前标记价格
        /// </summary>
        public decimal CurrentMarkPrice { get; set; }

        /// <summary>
        /// 当前未实现盈亏
        /// </summary>
        public decimal CurrentUnrealizedProfit { get; set; }

        /// <summary>
        /// 当前杠杆倍数
        /// </summary>
        public int CurrentLeverage { get; set; }

        #endregion

        #region 配置详情

        /// <summary>
        /// 保本配置
        /// </summary>
        public ContractBreakEvenConfig BreakEvenConfig { get; set; } = new ContractBreakEvenConfig();

        /// <summary>
        /// 推仓配置
        /// </summary>
        public ContractAddPositionConfig AddPositionConfig { get; set; } = new ContractAddPositionConfig();

        /// <summary>
        /// 保盈配置
        /// </summary>
        public ContractProfitProtectionConfig ProfitProtectionConfig { get; set; } = new ContractProfitProtectionConfig();

        #endregion

        /// <summary>
        /// 获取配置摘要
        /// </summary>
        public string GetConfigSummary()
        {
            var summary = $"合约: {Symbol}_{PositionSide}";
            if (!IsEnabled) summary += " (已禁用)";
            
            summary += $"\n持仓: {CurrentPositionAmt:F4} @ {CurrentEntryPrice:F4}";
            summary += $"\n浮盈: {CurrentUnrealizedProfit:F2} USDT";
            
            if (BreakEvenConfig.IsEnabled)
                summary += $"\n保本: {BreakEvenConfig.TriggerProfitAmount:F0}U触发";
            
            if (AddPositionConfig.IsEnabled)
                summary += $"\n推仓: {AddPositionConfig.Tiers.Count}个阶梯";
            
            if (ProfitProtectionConfig.IsEnabled)
                summary += $"\n保盈: {ProfitProtectionConfig.Tiers.Count}个阶梯";
            
            return summary;
        }
    }

    /// <summary>
    /// 合约保本配置
    /// </summary>
    public class ContractBreakEvenConfig
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = false;

        /// <summary>
        /// 触发盈利值
        /// </summary>
        public decimal TriggerProfitAmount { get; set; }

        /// <summary>
        /// 保本止损价格
        /// </summary>
        public decimal BreakEvenPrice { get; set; }

        /// <summary>
        /// 是否已触发
        /// </summary>
        public bool IsTriggered { get; set; } = false;

        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime? ExecutionTime { get; set; }

        /// <summary>
        /// 是否已执行
        /// </summary>
        public bool IsExecuted { get; set; } = false;

        /// <summary>
        /// 执行消息
        /// </summary>
        public string ExecutionMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// 合约推仓配置
    /// </summary>
    public class ContractAddPositionConfig
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = false;

        /// <summary>
        /// 推仓阶梯列表
        /// </summary>
        public List<ContractAddPositionTier> Tiers { get; set; } = new List<ContractAddPositionTier>();

        /// <summary>
        /// 获取已执行的阶梯数量
        /// </summary>
        public int GetExecutedTiersCount()
        {
            return Tiers.Count(t => t.IsExecuted);
        }

        /// <summary>
        /// 获取下一个可执行的阶梯
        /// </summary>
        public ContractAddPositionTier? GetNextTier()
        {
            return Tiers.Where(t => t.IsEnabled && !t.IsExecuted)
                       .OrderBy(t => t.TierIndex)
                       .FirstOrDefault();
        }
    }

    /// <summary>
    /// 合约推仓阶梯
    /// </summary>
    public class ContractAddPositionTier
    {
        /// <summary>
        /// 阶梯索引
        /// </summary>
        public int TierIndex { get; set; }

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
        public decimal RiskMultiplier { get; set; }

        /// <summary>
        /// 止损比例
        /// </summary>
        public decimal StopLossRatio { get; set; }

        /// <summary>
        /// 保盈金额（USDT）
        /// 设置范围：负数（最小负一倍风险金）到正数（最大为当前推仓阶梯触发值）
        /// 0表示保本止损，负数表示允许亏损，正数表示保护盈利
        /// </summary>
        public decimal ProfitProtectionAmount { get; set; } = 0m;

        /// <summary>
        /// 计算的加仓数量
        /// </summary>
        public decimal AddPositionQuantity { get; set; }

        /// <summary>
        /// 计算的止损价格
        /// </summary>
        public decimal StopLossPrice { get; set; }

        /// <summary>
        /// 是否已触发
        /// </summary>
        public bool IsTriggered { get; set; } = false;

        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime? ExecutionTime { get; set; }

        /// <summary>
        /// 是否已执行
        /// </summary>
        public bool IsExecuted { get; set; } = false;

        /// <summary>
        /// 执行消息
        /// </summary>
        public string ExecutionMessage { get; set; } = string.Empty;

        /// <summary>
        /// 描述
        /// </summary>
        public string Description => $"阶梯{TierIndex}: 盈利{TriggerProfitAmount:F0}U → 加仓{AddPositionQuantity:F4}, 止损{StopLossPrice:F4}, 保盈{ProfitProtectionAmount:F0}U";
    }

    /// <summary>
    /// 合约保盈配置
    /// </summary>
    public class ContractProfitProtectionConfig
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = false;

        /// <summary>
        /// 保盈阶梯列表
        /// </summary>
        public List<ContractProfitProtectionTier> Tiers { get; set; } = new List<ContractProfitProtectionTier>();

        /// <summary>
        /// 获取已执行的阶梯数量
        /// </summary>
        public int GetExecutedTiersCount()
        {
            return Tiers.Count(t => t.IsExecuted);
        }

        /// <summary>
        /// 获取下一个可执行的阶梯
        /// </summary>
        public ContractProfitProtectionTier? GetNextTier()
        {
            return Tiers.Where(t => t.IsEnabled && !t.IsExecuted)
                       .OrderBy(t => t.TierIndex)
                       .FirstOrDefault();
        }
    }

    /// <summary>
    /// 合约保盈阶梯
    /// </summary>
    public class ContractProfitProtectionTier
    {
        /// <summary>
        /// 阶梯索引
        /// </summary>
        public int TierIndex { get; set; }

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

        /// <summary>
        /// 计算的止损价格
        /// </summary>
        public decimal StopLossPrice { get; set; }

        /// <summary>
        /// 是否已触发
        /// </summary>
        public bool IsTriggered { get; set; } = false;

        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime? ExecutionTime { get; set; }

        /// <summary>
        /// 是否已执行
        /// </summary>
        public bool IsExecuted { get; set; } = false;

        /// <summary>
        /// 执行消息
        /// </summary>
        public string ExecutionMessage { get; set; } = string.Empty;

        /// <summary>
        /// 描述
        /// </summary>
        public string Description => $"阶梯{TierIndex}: 盈利{TriggerProfitAmount:F0}U → 保护{ProtectionAmount:F0}U, 止损{StopLossPrice:F4}";
    }
}