using System;
using System.Collections.Generic;
using System.Linq;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 合约监控状态生成器 - 负责从基础配置和持仓数据生成统一监控状态
    /// </summary>
    public class ContractMonitoringStateGenerator
    {
        private readonly ILogger<ContractMonitoringStateGenerator> _logger;

        public ContractMonitoringStateGenerator(ILogger<ContractMonitoringStateGenerator> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 从基础配置和持仓数据生成合约监控状态
        /// </summary>
        /// <param name="baseConfig">基础配置</param>
        /// <param name="position">持仓信息</param>
        /// <param name="existingState">现有状态（如果有）</param>
        /// <returns>生成的监控状态</returns>
        public ContractMonitoringState GenerateMonitoringState(
            AutoMonitorConfig baseConfig, 
            PositionInfo position, 
            ContractMonitoringState? existingState = null)
        {
            _logger.LogDebug($"🔄 生成监控状态: {position.Symbol}_{position.PositionSideString}");

            var state = existingState ?? new ContractMonitoringState();
            var now = DateTime.Now;

            // 基本信息
            state.Symbol = position.Symbol;
            state.PositionSide = position.PositionAmt > 0 ? "LONG" : "SHORT"; // 标准化持仓方向
            state.BaseConfigName = baseConfig.Name;
            state.Name = $"{baseConfig.Name}_{position.Symbol}";
            state.IsEnabled = baseConfig.IsEnabled;
            state.ScanIntervalSeconds = baseConfig.ScanIntervalSeconds;
            state.CooldownSeconds = baseConfig.CooldownSeconds;

            // 持仓信息
            if (existingState == null)
            {
                state.InitialQuantity = Math.Abs(position.PositionAmt);
                state.InitialEntryPrice = position.EntryPrice;
                state.CreateTime = now;
            }
            
            state.CurrentQuantity = Math.Abs(position.PositionAmt);
            state.CurrentEntryPrice = position.EntryPrice;
            state.CurrentMarkPrice = position.MarkPrice;
            state.CurrentUnrealizedPnl = position.UnrealizedProfit;
            state.LastUpdateTime = now;
            state.IsActive = Math.Abs(position.PositionAmt) > 0;

            // 生成保本配置状态
            state.BreakEvenConfig = GenerateBreakEvenState(baseConfig.BreakEvenConfig, state.BreakEvenConfig);

            // 生成推仓配置状态
            state.AddPositionConfig = GenerateAddPositionState(baseConfig.AddPositionConfig, state.AddPositionConfig);

            // 生成保盈配置状态
            state.ProfitProtectionConfig = GenerateProfitProtectionState(baseConfig.ProfitProtectionConfig, state.ProfitProtectionConfig);

            _logger.LogDebug($"✅ 监控状态生成完成: {state.Symbol}_{state.PositionSide}");
            return state;
        }

        /// <summary>
        /// 生成保本配置状态
        /// </summary>
        private StatefulBreakEvenConfig GenerateBreakEvenState(
            AutoBreakEvenConfig baseConfig, 
            StatefulBreakEvenConfig? existingState)
        {
            var state = existingState ?? new StatefulBreakEvenConfig();
            
            // 从基础配置复制设置
            state.IsEnabled = baseConfig.IsEnabled;
            state.TriggerProfitAmount = baseConfig.TriggerProfitAmount;
            
            // 保留现有的执行状态
            // state.IsExecuted, ExecutionTime, ExecutionPnl, ExecutionStatus, ExecutionResult 保持不变
            
            return state;
        }

        /// <summary>
        /// 生成推仓配置状态
        /// </summary>
        private StatefulAddPositionConfig GenerateAddPositionState(
            AutoAddPositionConfig baseConfig, 
            StatefulAddPositionConfig? existingState)
        {
            var state = existingState ?? new StatefulAddPositionConfig();
            
            // 从基础配置复制设置
            state.IsEnabled = baseConfig.IsEnabled;
            
            // 更新阶梯配置
            var existingTiers = state.Tiers.ToDictionary(t => t.TierIndex, t => t);
            state.Tiers.Clear();

            foreach (var baseTier in baseConfig.Tiers)
            {
                var tier = existingTiers.TryGetValue(baseTier.TierIndex, out var existing) 
                    ? existing 
                    : new StatefulAddPositionTier();

                // 从基础配置复制设置
                tier.TierIndex = baseTier.TierIndex;
                tier.IsEnabled = baseTier.IsEnabled;
                tier.TriggerProfitAmount = baseTier.TriggerProfitAmount;
                tier.RiskMultiplier = baseTier.RiskMultiplier;
                tier.StopLossRatio = baseTier.StopLossRatio;
                tier.ProfitProtectionAmount = baseTier.ProfitProtectionAmount;
                tier.ExitTargetPnl = baseTier.ExitTargetPnl;
                
                // 保留现有的执行状态
                // tier.IsExecuted, ExecutionTime, ExecutionPnl, ExecutionStatus, ExecutionResult 等保持不变

                state.Tiers.Add(tier);
            }

            return state;
        }

        /// <summary>
        /// 生成保盈配置状态
        /// </summary>
        private StatefulProfitProtectionConfig GenerateProfitProtectionState(
            AutoProfitProtectionConfig baseConfig, 
            StatefulProfitProtectionConfig? existingState)
        {
            var state = existingState ?? new StatefulProfitProtectionConfig();
            
            // 从基础配置复制设置
            state.IsEnabled = baseConfig.IsEnabled;
            
            // 更新阶梯配置
            var existingTiers = state.Tiers.ToDictionary(t => t.TierIndex, t => t);
            state.Tiers.Clear();

            foreach (var baseTier in baseConfig.Tiers)
            {
                var tier = existingTiers.TryGetValue(baseTier.TierIndex, out var existing) 
                    ? existing 
                    : new StatefulProfitProtectionTier();

                // 从基础配置复制设置
                tier.TierIndex = baseTier.TierIndex;
                tier.IsEnabled = baseTier.IsEnabled;
                tier.TriggerProfitAmount = baseTier.TriggerProfitAmount;
                tier.ProtectionAmount = baseTier.ProtectionAmount;
                
                // 保留现有的执行状态
                // tier.IsExecuted, ExecutionTime, ExecutionPnl, ExecutionStatus, ExecutionResult 等保持不变

                state.Tiers.Add(tier);
            }

            return state;
        }

        /// <summary>
        /// 更新执行状态
        /// </summary>
        public void UpdateExecutionStatus(
            ContractMonitoringState state, 
            string operationType, 
            int? tierIndex, 
            bool isSuccess, 
            decimal triggerPnl, 
            string result)
        {
            var now = DateTime.Now;
            var status = isSuccess ? StatusConstants.Executed : StatusConstants.Failed;
            
            _logger.LogCritical($"🔧【StateGenerator】更新执行状态: {state.Symbol}_{state.PositionSide} {operationType}_{tierIndex}");
            _logger.LogCritical($"   🎯 操作类型: {operationType}, 阶梯: {tierIndex}, 成功: {isSuccess}");

            switch (operationType.ToLower())
            {
                case "breakeven":
                case "保本":
                    _logger.LogCritical($"   📊 保本状态更新: 更新前={state.BreakEvenConfig.IsExecuted}");
                    state.BreakEvenConfig.ExecutionState = isSuccess ? ExecutionState.Executed : ExecutionState.NotTriggered;
                    state.BreakEvenConfig.ExecutionTime = now;
                    state.BreakEvenConfig.ExecutionPnl = triggerPnl;
                    state.BreakEvenConfig.ExecutionResult = result;
                    _logger.LogCritical($"   📊 保本状态更新: 更新后={state.BreakEvenConfig.IsExecuted}");
                    break;

                case "addposition":
                case "推仓":
                    if (tierIndex.HasValue)
                    {
                        var tier = state.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                        if (tier != null)
                        {
                            _logger.LogCritical($"   📊 推仓阶梯{tierIndex}状态更新: 更新前={tier.IsExecuted}");
                            tier.ExecutionState = isSuccess ? ExecutionState.Executed : ExecutionState.NotTriggered;
                            tier.ExecutionTime = now;
                            tier.ExecutionPnl = triggerPnl;
                            tier.ExecutionResult = result;
                            _logger.LogCritical($"   📊 推仓阶梯{tierIndex}状态更新: 更新后={tier.IsExecuted}");
                        }
                        else
                        {
                            _logger.LogCritical($"   ❌ 未找到推仓阶梯{tierIndex}, 可用阶梯: {string.Join(",", state.AddPositionConfig.Tiers.Select(t => t.TierIndex))}");
                        }
                    }
                    else
                    {
                        _logger.LogCritical($"   ❌ 推仓操作缺少阶梯索引");
                    }
                    break;

                case "profitprotection":
                case "保盈":
                    if (tierIndex.HasValue)
                    {
                        var tier = state.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                        if (tier != null)
                        {
                            _logger.LogCritical($"   📊 保盈阶梯{tierIndex}状态更新: 更新前={tier.IsExecuted}");
                            tier.ExecutionState = isSuccess ? ExecutionState.Executed : ExecutionState.NotTriggered;
                            tier.ExecutionTime = now;
                            tier.ExecutionPnl = triggerPnl;
                            tier.ExecutionResult = result;
                            _logger.LogCritical($"   📊 保盈阶梯{tierIndex}状态更新: 更新后={tier.IsExecuted}");
                        }
                        else
                        {
                            _logger.LogCritical($"   ❌ 未找到保盈阶梯{tierIndex}, 可用阶梯: {string.Join(",", state.ProfitProtectionConfig.Tiers.Select(t => t.TierIndex))}");
                        }
                    }
                    else
                    {
                        _logger.LogCritical($"   ❌ 保盈操作缺少阶梯索引");
                    }
                    break;
                
                default:
                    _logger.LogCritical($"   ❌ 未知操作类型: {operationType}");
                    break;
            }

            state.LastUpdateTime = now;
            _logger.LogDebug($"✅ 更新执行状态: {state.Symbol}_{state.PositionSide} {operationType}_{tierIndex} = {status}");
        }

        /// <summary>
        /// 检查执行状态
        /// </summary>
        public bool IsExecuted(ContractMonitoringState state, string operationType, int? tierIndex = null)
        {
            switch (operationType.ToLower())
            {
                case "breakeven":
                case "保本":
                    return state.BreakEvenConfig.IsExecuted;

                case "addposition":
                case "推仓":
                    if (tierIndex.HasValue)
                    {
                        var tier = state.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                        return tier?.IsExecuted ?? false;
                    }
                    return false;

                case "profitprotection":
                case "保盈":
                    if (tierIndex.HasValue)
                    {
                        var tier = state.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex.Value);
                        return tier?.IsExecuted ?? false;
                    }
                    return false;

                default:
                    return false;
            }
        }
    }
} 