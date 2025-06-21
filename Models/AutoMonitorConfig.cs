using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace BinanceFuturesTrader.Models
{
    /// <summary>
    /// 自动盯盘总配置
    /// </summary>
    public class AutoMonitorConfig
    {
        /// <summary>
        /// 配置名称
        /// </summary>
        public string Name { get; set; } = "默认配置";
        
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// 扫描间隔（秒）
        /// </summary>
        public int ScanIntervalSeconds { get; set; } = 5;
        
        /// <summary>
        /// 自动保本配置
        /// </summary>
        public AutoBreakEvenConfig BreakEvenConfig { get; set; } = new AutoBreakEvenConfig();
        
        /// <summary>
        /// 自动推仓配置
        /// </summary>
        public AutoAddPositionConfig AddPositionConfig { get; set; } = new AutoAddPositionConfig();
        
        /// <summary>
        /// 自动保盈止损配置
        /// </summary>
        public AutoProfitProtectionConfig ProfitProtectionConfig { get; set; } = new AutoProfitProtectionConfig();
        
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModifiedTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 自动保本配置
    /// </summary>
    public class AutoBreakEvenConfig
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
        /// 描述
        /// </summary>
        public string Description => $"当浮盈达到 {TriggerProfitAmount:F2} USDT 时自动设置保本止损";
    }

    /// <summary>
    /// 自动推仓配置（一键保本加仓）
    /// </summary>
    public class AutoAddPositionConfig
    {
        /// <summary>
        /// 是否启用自动推仓
        /// </summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// 推仓阶梯列表（4个阶梯）
        /// </summary>
        public List<AddPositionTier> Tiers { get; set; } = new List<AddPositionTier>
        {
            new AddPositionTier { TierIndex = 1, TriggerProfitAmount = 20.0m, RiskMultiplier = 1.5m, StopLossRatio = 0.02m },
            new AddPositionTier { TierIndex = 2, TriggerProfitAmount = 50.0m, RiskMultiplier = 2.0m, StopLossRatio = 0.025m },
            new AddPositionTier { TierIndex = 3, TriggerProfitAmount = 100.0m, RiskMultiplier = 2.5m, StopLossRatio = 0.03m },
            new AddPositionTier { TierIndex = 4, TriggerProfitAmount = 200.0m, RiskMultiplier = 3.0m, StopLossRatio = 0.035m }
        };
    }

    /// <summary>
    /// 推仓阶梯
    /// </summary>
    public class AddPositionTier
    {
        /// <summary>
        /// 阶梯序号（1-4）
        /// </summary>
        public int TierIndex { get; set; }
        
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
        /// 是否已触发
        /// </summary>
        public bool IsTriggered { get; set; } = false;
        
        /// <summary>
        /// 触发时间
        /// </summary>
        public DateTime? TriggerTime { get; set; }
        
        /// <summary>
        /// 描述
        /// </summary>
        public string Description => $"阶梯{TierIndex}: 盈利{TriggerProfitAmount:F2}U → 推仓{RiskMultiplier:F1}倍风险金, 止损{StopLossRatio * 100:F1}%";
    }

    /// <summary>
    /// 自动保盈止损配置
    /// </summary>
    public class AutoProfitProtectionConfig
    {
        /// <summary>
        /// 是否启用自动保盈止损
        /// </summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>
        /// 保盈止损阶梯列表（3个阶梯）
        /// </summary>
        public List<ProfitProtectionTier> Tiers { get; set; } = new List<ProfitProtectionTier>
        {
            new ProfitProtectionTier { TierIndex = 1, TriggerProfitAmount = 30.0m, ProtectionAmount = 15.0m },
            new ProfitProtectionTier { TierIndex = 2, TriggerProfitAmount = 80.0m, ProtectionAmount = 40.0m },
            new ProfitProtectionTier { TierIndex = 3, TriggerProfitAmount = 150.0m, ProtectionAmount = 75.0m }
        };
    }

    /// <summary>
    /// 保盈止损阶梯
    /// </summary>
    public class ProfitProtectionTier
    {
        /// <summary>
        /// 阶梯序号（1-3）
        /// </summary>
        public int TierIndex { get; set; }
        
        /// <summary>
        /// 触发盈利值（USDT）
        /// </summary>
        public decimal TriggerProfitAmount { get; set; }
        
        /// <summary>
        /// 保盈金额（USDT）
        /// </summary>
        public decimal ProtectionAmount { get; set; }
        
        /// <summary>
        /// 是否已触发
        /// </summary>
        public bool IsTriggered { get; set; } = false;
        
        /// <summary>
        /// 触发时间
        /// </summary>
        public DateTime? TriggerTime { get; set; }
        
        /// <summary>
        /// 描述
        /// </summary>
        public string Description => $"阶梯{TierIndex}: 盈利{TriggerProfitAmount:F2}U → 保护{ProtectionAmount:F2}U盈利";
    }
} 