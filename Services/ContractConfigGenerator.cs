using System;
using System.Collections.Generic;
using System.Linq;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 合约配置生成器 - 基于基础配置为每个持仓合约生成具体配置
    /// </summary>
    public class ContractConfigGenerator
    {
        private readonly ILogger<ContractConfigGenerator> _logger;
        private readonly RiskCapitalService _riskCapitalService;
        private readonly PositionCalculationService _positionCalculationService;

        public ContractConfigGenerator(
            ILogger<ContractConfigGenerator> logger,
            RiskCapitalService riskCapitalService,
            PositionCalculationService positionCalculationService)
        {
            _logger = logger;
            _riskCapitalService = riskCapitalService;
            _positionCalculationService = positionCalculationService;
        }

        /// <summary>
        /// 为持仓合约生成具体配置
        /// </summary>
        /// <param name="baseConfig">基础配置</param>
        /// <param name="position">持仓信息</param>
        /// <returns>合约配置</returns>
        public ContractConfig GenerateContractConfig(AutoMonitorConfig baseConfig, PositionInfo position)
        {
            try
            {
                if (baseConfig == null)
                    throw new ArgumentNullException(nameof(baseConfig));
                
                if (position == null)
                    throw new ArgumentNullException(nameof(position));

                var contractConfig = new ContractConfig
                {
                    Symbol = position.Symbol,
                    PositionSide = position.PositionSideString,
                    ContractKey = $"{position.Symbol}_{position.PositionSideString}",
                    BaseConfigName = baseConfig.Name,
                    IsEnabled = baseConfig.IsEnabled,
                    CreateTime = DateTime.Now,
                    LastUpdateTime = DateTime.Now,
                    
                    // 持仓信息
                    CurrentPositionAmt = position.PositionAmt,
                    CurrentEntryPrice = position.EntryPrice,
                    CurrentMarkPrice = position.MarkPrice,
                    CurrentUnrealizedProfit = position.UnrealizedProfit,
                    CurrentLeverage = position.Leverage,
                    
                    // 生成保本配置
                    BreakEvenConfig = GenerateBreakEvenConfig(baseConfig.BreakEvenConfig, position),
                    
                    // 生成推仓配置
                    AddPositionConfig = GenerateAddPositionConfig(baseConfig.AddPositionConfig, position),
                    
                    // 生成保盈配置
                    ProfitProtectionConfig = GenerateProfitProtectionConfig(baseConfig.ProfitProtectionConfig, position)
                };

                _logger.LogInformation($"为合约 {position.Symbol}_{position.PositionSideString} 生成配置成功");
                return contractConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"为合约 {position?.Symbol}_{position?.PositionSideString} 生成配置失败");
                throw;
            }
        }

        /// <summary>
        /// 批量生成合约配置
        /// </summary>
        /// <param name="baseConfig">基础配置</param>
        /// <param name="positions">持仓列表</param>
        /// <returns>合约配置列表</returns>
        public List<ContractConfig> GenerateContractConfigs(AutoMonitorConfig baseConfig, IEnumerable<PositionInfo> positions)
        {
            var configs = new List<ContractConfig>();
            
            foreach (var position in positions)
            {
                try
                {
                    // 只为有持仓的合约生成配置
                    if (Math.Abs(position.PositionAmt) > 0)
                    {
                        var config = GenerateContractConfig(baseConfig, position);
                        configs.Add(config);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"为合约 {position.Symbol}_{position.PositionSideString} 生成配置失败，跳过");
                }
            }

            _logger.LogInformation($"批量生成了 {configs.Count} 个合约配置");
            return configs;
        }

        /// <summary>
        /// 生成保本配置
        /// </summary>
        private ContractBreakEvenConfig GenerateBreakEvenConfig(AutoBreakEvenConfig baseConfig, PositionInfo position)
        {
            return new ContractBreakEvenConfig
            {
                IsEnabled = baseConfig.IsEnabled,
                TriggerProfitAmount = baseConfig.TriggerProfitAmount,
                
                // 计算保本止损价格
                BreakEvenPrice = CalculateBreakEvenPrice(position),
                
                // 保本状态
                IsTriggered = false,
                ExecutionTime = null,
                IsExecuted = false
            };
        }

        /// <summary>
        /// 生成推仓配置
        /// </summary>
        private ContractAddPositionConfig GenerateAddPositionConfig(AutoAddPositionConfig baseConfig, PositionInfo position)
        {
            var contractTiers = new List<ContractAddPositionTier>();
            var riskCapital = _riskCapitalService.CalculateRiskCapitalFromCurrentAccountInfo();

            foreach (var baseTier in baseConfig.Tiers)
            {
                var contractTier = new ContractAddPositionTier
                {
                    TierIndex = baseTier.TierIndex,
                    IsEnabled = baseTier.IsEnabled,
                    TriggerProfitAmount = baseTier.TriggerProfitAmount,
                    RiskMultiplier = baseTier.RiskMultiplier,
                    StopLossRatio = baseTier.StopLossRatio,
                    
                    // 计算具体的加仓数量
                    AddPositionQuantity = CalculateAddPositionQuantity(riskCapital, baseTier.RiskMultiplier, 
                        baseTier.StopLossRatio, position.MarkPrice),
                    
                    // 计算止损价格
                    StopLossPrice = CalculateStopLossPrice(position.EntryPrice, baseTier.StopLossRatio, 
                        position.PositionAmt > 0),
                    
                    // 执行状态
                    IsTriggered = false,
                    ExecutionTime = null,
                    IsExecuted = false
                };

                contractTiers.Add(contractTier);
            }

            return new ContractAddPositionConfig
            {
                IsEnabled = baseConfig.IsEnabled,
                Tiers = contractTiers
            };
        }

        /// <summary>
        /// 生成保盈配置
        /// </summary>
        private ContractProfitProtectionConfig GenerateProfitProtectionConfig(AutoProfitProtectionConfig baseConfig, PositionInfo position)
        {
            var contractTiers = new List<ContractProfitProtectionTier>();

            foreach (var baseTier in baseConfig.Tiers)
            {
                var contractTier = new ContractProfitProtectionTier
                {
                    TierIndex = baseTier.TierIndex,
                    IsEnabled = baseTier.IsEnabled,
                    TriggerProfitAmount = baseTier.TriggerProfitAmount,
                    ProtectionAmount = baseTier.ProtectionAmount,
                    
                    // 计算保盈止损价格
                    StopLossPrice = CalculateProfitProtectionStopLossPrice(position, baseTier.ProtectionAmount),
                    
                    // 执行状态
                    IsTriggered = false,
                    ExecutionTime = null,
                    IsExecuted = false
                };

                contractTiers.Add(contractTier);
            }

            return new ContractProfitProtectionConfig
            {
                IsEnabled = baseConfig.IsEnabled,
                Tiers = contractTiers
            };
        }

        /// <summary>
        /// 计算保本止损价格
        /// </summary>
        private decimal CalculateBreakEvenPrice(PositionInfo position)
        {
            // 保本价格就是开仓价格
            return position.EntryPrice;
        }

        /// <summary>
        /// 计算加仓数量
        /// </summary>
        private decimal CalculateAddPositionQuantity(decimal riskCapital, decimal riskMultiplier, decimal stopLossRatio, decimal currentPrice)
        {
            return _positionCalculationService.CalculateAddPositionQuantity(riskCapital, riskMultiplier, stopLossRatio, currentPrice);
        }

        /// <summary>
        /// 计算止损价格
        /// </summary>
        private decimal CalculateStopLossPrice(decimal entryPrice, decimal stopLossRatio, bool isLong)
        {
            if (isLong)
            {
                // 多头止损价格 = 开仓价格 × (1 - 止损比例)
                return entryPrice * (1 - stopLossRatio);
            }
            else
            {
                // 空头止损价格 = 开仓价格 × (1 + 止损比例)
                return entryPrice * (1 + stopLossRatio);
            }
        }

        /// <summary>
        /// 计算保盈止损价格
        /// </summary>
        private decimal CalculateProfitProtectionStopLossPrice(PositionInfo position, decimal protectionAmount)
        {
            // 计算保护盈利对应的价格
            var positionAmt = Math.Abs(position.PositionAmt);
            var isLong = position.PositionAmt > 0;
            
            if (positionAmt == 0) return position.EntryPrice;
            
            // 保盈止损价格 = 开仓价格 + (保护金额 / 持仓数量) × 方向
            if (isLong)
            {
                return position.EntryPrice + (protectionAmount / positionAmt);
            }
            else
            {
                return position.EntryPrice - (protectionAmount / positionAmt);
            }
        }

        /// <summary>
        /// 更新合约配置的市场数据
        /// </summary>
        /// <param name="contractConfig">合约配置</param>
        /// <param name="position">最新持仓信息</param>
        public void UpdateContractConfig(ContractConfig contractConfig, PositionInfo position)
        {
            try
            {
                if (contractConfig == null || position == null) return;

                // 更新持仓信息
                contractConfig.CurrentPositionAmt = position.PositionAmt;
                contractConfig.CurrentEntryPrice = position.EntryPrice;
                contractConfig.CurrentMarkPrice = position.MarkPrice;
                contractConfig.CurrentUnrealizedProfit = position.UnrealizedProfit;
                contractConfig.CurrentLeverage = position.Leverage;
                contractConfig.LastUpdateTime = DateTime.Now;

                // 重新计算价格相关配置
                contractConfig.BreakEvenConfig.BreakEvenPrice = CalculateBreakEvenPrice(position);

                // 更新推仓配置的价格
                var riskCapital = _riskCapitalService.CalculateRiskCapitalFromCurrentAccountInfo();
                foreach (var tier in contractConfig.AddPositionConfig.Tiers)
                {
                    tier.AddPositionQuantity = CalculateAddPositionQuantity(riskCapital, tier.RiskMultiplier, 
                        tier.StopLossRatio, position.MarkPrice);
                    tier.StopLossPrice = CalculateStopLossPrice(position.EntryPrice, tier.StopLossRatio, 
                        position.PositionAmt > 0);
                }

                // 更新保盈配置的价格
                foreach (var tier in contractConfig.ProfitProtectionConfig.Tiers)
                {
                    tier.StopLossPrice = CalculateProfitProtectionStopLossPrice(position, tier.ProtectionAmount);
                }

                _logger.LogDebug($"更新合约配置 {contractConfig.ContractKey} 的市场数据");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新合约配置 {contractConfig?.ContractKey} 失败");
            }
        }

        /// <summary>
        /// 验证合约配置有效性
        /// </summary>
        /// <param name="contractConfig">合约配置</param>
        /// <returns>是否有效</returns>
        public bool ValidateContractConfig(ContractConfig contractConfig)
        {
            if (contractConfig == null) return false;

            // 验证基础信息
            if (string.IsNullOrEmpty(contractConfig.Symbol) || 
                string.IsNullOrEmpty(contractConfig.PositionSide) ||
                contractConfig.CurrentPositionAmt == 0)
            {
                return false;
            }

            // 验证价格有效性
            if (contractConfig.CurrentEntryPrice <= 0 || contractConfig.CurrentMarkPrice <= 0)
            {
                return false;
            }

            // 验证推仓配置
            if (contractConfig.AddPositionConfig.IsEnabled)
            {
                foreach (var tier in contractConfig.AddPositionConfig.Tiers)
                {
                    if (tier.IsEnabled && (tier.AddPositionQuantity <= 0 || tier.StopLossPrice <= 0))
                    {
                        return false;
                    }
                }
            }

            // 验证保盈配置
            if (contractConfig.ProfitProtectionConfig.IsEnabled)
            {
                foreach (var tier in contractConfig.ProfitProtectionConfig.Tiers)
                {
                    if (tier.IsEnabled && tier.StopLossPrice <= 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}