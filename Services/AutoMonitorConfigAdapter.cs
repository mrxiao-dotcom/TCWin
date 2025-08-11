using System;
using System.Collections.Generic;
using System.Linq;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 自动监控配置适配器
    /// 负责在旧版 AutoMonitorConfig 和新版 BaseConfig 之间进行转换
    /// </summary>
    public static class AutoMonitorConfigAdapter
    {
        /// <summary>
        /// 将旧版AutoMonitorConfig转换为新版BaseConfig
        /// </summary>
        public static BaseConfig ToBaseConfig(AutoMonitorConfig oldConfig)
        {
            return new BaseConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = oldConfig.Name,
                Description = $"从旧配置迁移: {oldConfig.Name}",
                CreatedAt = oldConfig.CreateTime,
                UpdatedAt = oldConfig.LastModifiedTime,
                GlobalSettings = new GlobalSettings
                {
                    ScanIntervalSeconds = oldConfig.ScanIntervalSeconds,
                    CooldownSeconds = oldConfig.CooldownSeconds
                },
                BreakevenConfig = new BreakevenConfig
                {
                    Enabled = oldConfig.BreakEvenConfig.IsEnabled,
                    TriggerProfitAmount = oldConfig.BreakEvenConfig.TriggerProfitAmount,
                    Description = "保本配置"
                },
                AddPositionConfig = ToNewAddPositionConfig(oldConfig.AddPositionConfig),
                ProfitProtectionConfig = ToNewProfitProtectionConfig(oldConfig.ProfitProtectionConfig)
            };
        }

        /// <summary>
        /// 将新版BaseConfig转换为旧版AutoMonitorConfig
        /// </summary>
        public static AutoMonitorConfig ToAutoMonitorConfig(BaseConfig newConfig)
        {
            return new AutoMonitorConfig
            {
                Name = newConfig.Name,
                IsEnabled = true, // 默认启用
                ScanIntervalSeconds = newConfig.GlobalSettings.ScanIntervalSeconds,
                CooldownSeconds = newConfig.GlobalSettings.CooldownSeconds,
                CreateTime = newConfig.CreatedAt,
                LastModifiedTime = newConfig.UpdatedAt,
                BreakEvenConfig = new AutoBreakEvenConfig
                {
                    IsEnabled = newConfig.BreakevenConfig.Enabled,
                    TriggerProfitAmount = newConfig.BreakevenConfig.TriggerProfitAmount
                },
                AddPositionConfig = ToOldAddPositionConfig(newConfig.AddPositionConfig),
                ProfitProtectionConfig = ToOldProfitProtectionConfig(newConfig.ProfitProtectionConfig)
            };
        }

        /// <summary>
        /// 将旧版推仓配置转换为新版
        /// </summary>
        private static AddPositionConfig ToNewAddPositionConfig(AutoAddPositionConfig oldConfig)
        {
            var newTiers = new List<BaseAddPositionTier>();

            // 转换推仓阶梯列表
            for (int i = 0; i < oldConfig.Tiers.Count && i < 4; i++)
            {
                var oldTier = oldConfig.Tiers[i];
                var newTier = new BaseAddPositionTier
                {
                    TierIndex = oldTier.TierIndex,
                    Enabled = oldTier.IsEnabled,
                    TriggerProfitAmount = oldTier.TriggerProfitAmount,
                    RiskMultiplier = oldTier.RiskMultiplier,
                    StopLossRatio = oldTier.StopLossRatio,
                    Description = $"推仓阶梯{oldTier.TierIndex}"
                };
                newTiers.Add(newTier);
            }

            return new AddPositionConfig
            {
                Enabled = oldConfig.IsEnabled,
                Tiers = newTiers
            };
        }

        /// <summary>
        /// 将新版推仓配置转换为旧版
        /// </summary>
        private static AutoAddPositionConfig ToOldAddPositionConfig(AddPositionConfig newConfig)
        {
            var oldTiers = new List<AddPositionTier>();

            // 转换推仓阶梯列表
            for (int i = 0; i < newConfig.Tiers.Count && i < 4; i++)
            {
                var newTier = newConfig.Tiers[i];
                var oldTier = new AddPositionTier
                {
                    TierIndex = newTier.TierIndex,
                    IsEnabled = newTier.Enabled,
                    TriggerProfitAmount = newTier.TriggerProfitAmount,
                    RiskMultiplier = newTier.RiskMultiplier,
                    StopLossRatio = newTier.StopLossRatio
                };
                oldTiers.Add(oldTier);
            }

            // 如果阶梯不足4个，补充默认阶梯
            for (int i = oldTiers.Count; i < 4; i++)
            {
                oldTiers.Add(new AddPositionTier
                {
                    TierIndex = i + 1,
                    IsEnabled = false,
                    TriggerProfitAmount = (i + 1) * 100.0m,
                    RiskMultiplier = 1.0m,
                    StopLossRatio = 0.10m
                });
            }

            return new AutoAddPositionConfig
            {
                IsEnabled = newConfig.Enabled,
                Tiers = oldTiers
            };
        }

        /// <summary>
        /// 将旧版保盈配置转换为新版
        /// </summary>
        private static ProfitProtectionConfig ToNewProfitProtectionConfig(AutoProfitProtectionConfig oldConfig)
        {
            var newTiers = new List<BaseProfitProtectionTier>();

            // 转换保盈阶梯列表
            for (int i = 0; i < oldConfig.Tiers.Count && i < 3; i++)
            {
                var oldTier = oldConfig.Tiers[i];
                var newTier = new BaseProfitProtectionTier
                {
                    TierIndex = oldTier.TierIndex,
                    Enabled = oldTier.IsEnabled,
                    TriggerProfitAmount = oldTier.TriggerProfitAmount,
                    ProtectionAmount = oldTier.ProtectionAmount,
                    Description = $"保盈阶梯{oldTier.TierIndex}"
                };
                newTiers.Add(newTier);
            }

            return new ProfitProtectionConfig
            {
                Enabled = oldConfig.IsEnabled,
                Tiers = newTiers
            };
        }

        /// <summary>
        /// 将新版保盈配置转换为旧版
        /// </summary>
        private static AutoProfitProtectionConfig ToOldProfitProtectionConfig(ProfitProtectionConfig newConfig)
        {
            var oldTiers = new List<ProfitProtectionTier>();

            // 转换保盈阶梯列表
            for (int i = 0; i < newConfig.Tiers.Count && i < 3; i++)
            {
                var newTier = newConfig.Tiers[i];
                var oldTier = new ProfitProtectionTier
                {
                    TierIndex = newTier.TierIndex,
                    IsEnabled = newTier.Enabled,
                    TriggerProfitAmount = newTier.TriggerProfitAmount,
                    ProtectionAmount = newTier.ProtectionAmount
                };
                oldTiers.Add(oldTier);
            }

            // 如果阶梯不足3个，补充默认阶梯
            for (int i = oldTiers.Count; i < 3; i++)
            {
                oldTiers.Add(new ProfitProtectionTier
                {
                    TierIndex = i + 1,
                    IsEnabled = false,
                    TriggerProfitAmount = (i + 1) * 1000.0m,
                    ProtectionAmount = (i + 1) * 800.0m
                });
            }

            return new AutoProfitProtectionConfig
            {
                IsEnabled = newConfig.Enabled,
                Tiers = oldTiers
            };
        }

        /// <summary>
        /// 将旧版合约监控状态转换为新版合约状态
        /// </summary>
        public static ContractState ToContractState(ContractMonitoringState oldState)
        {
            return new ContractState
            {
                Symbol = oldState.Symbol,
                PositionSide = oldState.PositionSide,
                BaseConfigId = oldState.BaseConfigName ?? "default",
                PositionInfo = new ContractPositionInfo
                {
                    // 使用默认值，因为属性可能不匹配
                },
                ExecutionStates = new ExecutionStates
                {
                    Breakeven = new BreakevenExecutionState
                    {
                        State = ExecutionStateTypes.NotTriggered,
                        TriggerAmount = 0,
                        ExecutedAt = null
                    },
                    AddPositionTiers = new List<AddPositionExecutionState>(),
                    ProfitProtectionTiers = new List<ProfitProtectionExecutionState>()
                },
                Meta = new ContractStateMeta
                {
                    IsActive = oldState.IsActive,
                    CreatedAt = oldState.CreateTime,
                    UpdatedAt = oldState.LastUpdateTime
                }
            };
        }

        /// <summary>
        /// 将新版合约状态转换为旧版合约监控状态
        /// </summary>
        public static ContractMonitoringState ToContractMonitoringState(ContractState newState)
        {
            return new ContractMonitoringState
            {
                Symbol = newState.Symbol,
                PositionSide = newState.PositionSide,
                BaseConfigName = newState.BaseConfigId,
                IsEnabled = true,
                IsActive = newState.Meta.IsActive,
                CreateTime = newState.Meta.CreatedAt,
                LastUpdateTime = newState.Meta.UpdatedAt,
                // 其他属性使用默认值
                ScanIntervalSeconds = 5,
                CooldownSeconds = 5
            };
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public static bool IsValidConfig(AutoMonitorConfig config)
        {
            if (config == null) return false;
            if (string.IsNullOrWhiteSpace(config.Name)) return false;
            if (config.ScanIntervalSeconds <= 0) return false;
            
            return true;
        }

        /// <summary>
        /// 验证新版配置是否有效
        /// </summary>
        public static bool IsValidConfig(BaseConfig config)
        {
            if (config == null) return false;
            if (string.IsNullOrWhiteSpace(config.Name)) return false;
            if (config.GlobalSettings.ScanIntervalSeconds <= 0) return false;
            
            return true;
        }
    }
} 