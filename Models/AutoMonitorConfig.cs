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
        /// 冷却期配置（秒）- 防止短时间内重复扫描触发
        /// 默认5秒，主要依赖状态管理而不是冷却期来确保不重复执行
        /// </summary>
        public int CooldownSeconds { get; set; } = 5;
        
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

        /// <summary>
        /// 根据账户信息创建智能默认配置
        /// </summary>
        /// <param name="accountEquity">账户权益（USDT）</param>
        /// <param name="riskCapitalTimes">风险金倍数（默认10）</param>
        /// <returns>配置好的自动盯盘配置</returns>
        /// <summary>
        /// 🔧 修复：对盈利目标金额进行四舍五入取整处理
        /// </summary>
        public static AutoMonitorConfig CreateSmartDefault(decimal accountEquity, int riskCapitalTimes = 10)
        {
            var riskCapital = accountEquity / riskCapitalTimes;
            
            var config = new AutoMonitorConfig
            {
                Name = $"智能配置（权益{accountEquity:F0}U）",
                BreakEvenConfig = new AutoBreakEvenConfig
                {
                    TriggerProfitAmount = Math.Round(riskCapital * 0.1m, 0, MidpointRounding.AwayFromZero) // 四舍五入取整
                },
                AddPositionConfig = new AutoAddPositionConfig
                {
                    Tiers = new List<AddPositionTier>
                    {
                        new AddPositionTier { TierIndex = 1, TriggerProfitAmount = Math.Round(riskCapital * 1m, 0, MidpointRounding.AwayFromZero), RiskMultiplier = 1.0m, StopLossRatio = 0.10m },
                        new AddPositionTier { TierIndex = 2, TriggerProfitAmount = Math.Round(riskCapital * 2m, 0, MidpointRounding.AwayFromZero), RiskMultiplier = 1.0m, StopLossRatio = 0.10m },
                        new AddPositionTier { TierIndex = 3, TriggerProfitAmount = Math.Round(riskCapital * 3m, 0, MidpointRounding.AwayFromZero), RiskMultiplier = 1.0m, StopLossRatio = 0.10m },
                        new AddPositionTier { TierIndex = 4, TriggerProfitAmount = Math.Round(riskCapital * 4m, 0, MidpointRounding.AwayFromZero), RiskMultiplier = 1.0m, StopLossRatio = 0.10m }
                    }
                },
                ProfitProtectionConfig = new AutoProfitProtectionConfig
                {
                    Tiers = new List<ProfitProtectionTier>
                    {
                        new ProfitProtectionTier { TierIndex = 1, TriggerProfitAmount = Math.Round(riskCapital * 10m, 0, MidpointRounding.AwayFromZero), ProtectionAmount = Math.Round(riskCapital * 10m * 0.8m, 0, MidpointRounding.AwayFromZero) },
                        new ProfitProtectionTier { TierIndex = 2, TriggerProfitAmount = Math.Round(riskCapital * 20m, 0, MidpointRounding.AwayFromZero), ProtectionAmount = Math.Round(riskCapital * 20m * 0.8m, 0, MidpointRounding.AwayFromZero) },
                        new ProfitProtectionTier { TierIndex = 3, TriggerProfitAmount = Math.Round(riskCapital * 30m, 0, MidpointRounding.AwayFromZero), ProtectionAmount = Math.Round(riskCapital * 30m * 0.8m, 0, MidpointRounding.AwayFromZero) }
                    }
                }
            };
            
            return config;
        }
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
        /// 默认配置：触发金额为风险金的1/2/3/4倍，风险金倍数为1倍，止损比例为10%
        /// 假设风险金倍数为10，账户权益为1000U，则风险金为100U
        /// 第1档：浮盈100U时触发，第2档：浮盈200U时触发，以此类推
        /// </summary>
        public List<AddPositionTier> Tiers { get; set; } = new List<AddPositionTier>
        {
            new AddPositionTier { TierIndex = 1, TriggerProfitAmount = 100.0m, RiskMultiplier = 1.0m, StopLossRatio = 0.10m },
            new AddPositionTier { TierIndex = 2, TriggerProfitAmount = 200.0m, RiskMultiplier = 1.0m, StopLossRatio = 0.10m },
            new AddPositionTier { TierIndex = 3, TriggerProfitAmount = 300.0m, RiskMultiplier = 1.0m, StopLossRatio = 0.10m },
            new AddPositionTier { TierIndex = 4, TriggerProfitAmount = 400.0m, RiskMultiplier = 1.0m, StopLossRatio = 0.10m }
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
        /// 默认配置：触发金额为风险金的10倍，保护金额为触发金额的80%
        /// 假设风险金倍数为10，账户权益为1000U，则风险金为100U
        /// 第1档：浮盈1000U时触发，保护800U盈利
        /// </summary>
        public List<ProfitProtectionTier> Tiers { get; set; } = new List<ProfitProtectionTier>
        {
            new ProfitProtectionTier { TierIndex = 1, TriggerProfitAmount = 1000.0m, ProtectionAmount = 800.0m },
            new ProfitProtectionTier { TierIndex = 2, TriggerProfitAmount = 2000.0m, ProtectionAmount = 1600.0m },
            new ProfitProtectionTier { TierIndex = 3, TriggerProfitAmount = 3000.0m, ProtectionAmount = 2400.0m }
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