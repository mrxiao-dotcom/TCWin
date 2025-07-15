using System;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 自动盯盘执行引擎 - 整合保本、推仓、保盈逻辑的核心执行器
    /// </summary>
    public class AutoMonitorExecutionEngine
    {
        private readonly ILogger<AutoMonitorExecutionEngine> _logger;
        private readonly TradingExecutionService _tradingService;
        private readonly ContractProfileService _profileService;
        private readonly BaseConfigManager _configManager;
        
        public AutoMonitorExecutionEngine(
            ILogger<AutoMonitorExecutionEngine> logger,
            TradingExecutionService tradingService,
            ContractProfileService profileService,
            BaseConfigManager configManager)
        {
            _logger = logger;
            _tradingService = tradingService;
            _profileService = profileService;
            _configManager = configManager;
        }
        
        /// <summary>
        /// 执行单个合约的完整监控逻辑
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <returns>执行结果摘要</returns>
        public async Task<MonitorExecutionSummary> ExecuteContractMonitoringAsync(ContractProfile profile)
        {
            var summary = new MonitorExecutionSummary { Profile = profile };
            
            try
            {
                _logger.LogDebug($"开始执行合约监控: {profile.DisplayName}, 当前浮盈: {profile.UnrealizedPnl:F2}U");
                
                // 获取有效配置
                var (breakEvenConfig, addPositionConfig, profitProtectionConfig) = GetEffectiveConfigurations(profile);
                
                // 1. 检查并执行保本逻辑
                if (breakEvenConfig?.IsEnabled == true)
                {
                    var breakEvenResult = await ProcessBreakEvenLogicAsync(profile, breakEvenConfig);
                    summary.BreakEvenResult = breakEvenResult;
                }
                
                // 2. 检查并执行推仓逻辑
                if (addPositionConfig?.IsEnabled == true)
                {
                    var addPositionResults = await ProcessAddPositionLogicAsync(profile, addPositionConfig);
                    summary.AddPositionResults.AddRange(addPositionResults);
                }
                
                // 3. 检查并执行保盈逻辑
                if (profitProtectionConfig?.IsEnabled == true)
                {
                    var profitProtectionResults = await ProcessProfitProtectionLogicAsync(profile, profitProtectionConfig);
                    summary.ProfitProtectionResults.AddRange(profitProtectionResults);
                }
                
                // 更新档案状态
                await _profileService.UpdateProfileStatesAsync(profile);
                
                summary.IsSuccess = true;
                summary.Message = $"监控执行完成: 保本({summary.BreakEvenResult?.IsSuccess ?? false}), " +
                                 $"推仓({summary.AddPositionResults.Count(r => r.IsSuccess)}个), " +
                                 $"保盈({summary.ProfitProtectionResults.Count(r => r.IsSuccess)}个)";
                
                _logger.LogDebug(summary.Message);
            }
            catch (Exception ex)
            {
                summary.IsSuccess = false;
                summary.Message = $"监控执行失败: {ex.Message}";
                _logger.LogError(ex, $"合约监控执行失败: {profile.DisplayName}");
            }
            
            return summary;
        }
        
        #region 保本逻辑处理
        
        /// <summary>
        /// 处理保本逻辑
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="config">保本配置</param>
        /// <returns>执行结果</returns>
        private async Task<TradingExecutionResult?> ProcessBreakEvenLogicAsync(ContractProfile profile, ContractBreakEvenConfig config)
        {
            try
            {
                // 检查是否已经执行过
                if (config.IsExecuted)
                {
                    _logger.LogDebug($"保本已执行，跳过: {profile.DisplayName}");
                    return null;
                }
                
                // 检查是否达到触发条件
                var currentPnl = profile.UnrealizedPnl;
                var triggerAmount = config.TriggerProfitAmount;
                
                if (currentPnl < triggerAmount)
                {
                    _logger.LogDebug($"保本未达到触发条件: {profile.DisplayName}, 当前浮盈: {currentPnl:F2}U, 触发金额: {triggerAmount:F2}U");
                    return null;
                }
                
                // 更新触发状态
                if (!profile.BreakEvenState.IsTriggered)
                {
                    profile.BreakEvenState.IsTriggered = true;
                    profile.BreakEvenState.TriggerTime = DateTime.Now;
                    profile.BreakEvenState.TriggerPrice = profile.CurrentPrice;
                    profile.BreakEvenState.TriggerPnl = currentPnl;
                    profile.BreakEvenState.ExecutionStatus = "触发中";
                    
                    profile.AddOperationHistory("保本触发", "成功", $"触发金额: {triggerAmount:F2}U, 当前浮盈: {currentPnl:F2}U");
                    _logger.LogInformation($"保本条件触发: {profile.DisplayName}, 浮盈: {currentPnl:F2}U");
                }
                
                // 执行保本止损
                var result = await _tradingService.ExecuteBreakEvenStopLossAsync(profile, config);
                
                if (result.IsSuccess)
                {
                    _logger.LogInformation($"✅ 保本执行成功: {profile.DisplayName}");
                }
                else
                {
                    _logger.LogWarning($"❌ 保本执行失败: {profile.DisplayName}, 原因: {result.Message}");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"保本逻辑处理失败: {profile.DisplayName}");
                return TradingExecutionResult.Failed($"保本逻辑处理失败: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 推仓逻辑处理
        
        /// <summary>
        /// 处理推仓逻辑
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="config">推仓配置</param>
        /// <returns>执行结果列表</returns>
        private async Task<System.Collections.Generic.List<TradingExecutionResult>> ProcessAddPositionLogicAsync(ContractProfile profile, ContractAddPositionConfig config)
        {
            var results = new System.Collections.Generic.List<TradingExecutionResult>();
            
            try
            {
                var currentPnl = profile.UnrealizedPnl;
                
                // 按阶梯顺序检查每个推仓档位
                var sortedTiers = config.Tiers.Where(t => t.IsEnabled).OrderBy(t => t.TierIndex).ToList();
                
                foreach (var tier in sortedTiers)
                {
                    // 检查是否已经执行过
                    if (tier.IsExecuted)
                    {
                        continue;
                    }
                    
                    // 检查是否达到触发条件
                    if (currentPnl < tier.TriggerProfitAmount)
                    {
                        break; // 后续档位肯定也不会触发
                    }
                    
                    // 更新触发状态
                    var tierState = profile.AddPositionStates.FirstOrDefault(s => s.TierIndex == tier.TierIndex);
                    if (tierState != null && !tierState.IsTriggered)
                    {
                        tierState.IsTriggered = true;
                        tierState.TriggerTime = DateTime.Now;
                        tierState.TriggerPrice = profile.CurrentPrice;
                        tierState.TriggerPnl = currentPnl;
                        tierState.ExecutionStatus = "触发中";
                        
                        profile.AddOperationHistory("推仓触发", "成功", $"阶梯{tier.TierIndex}: 触发金额{tier.TriggerProfitAmount:F2}U");
                        _logger.LogInformation($"推仓阶梯{tier.TierIndex}触发: {profile.DisplayName}, 浮盈: {currentPnl:F2}U");
                    }
                    
                    // 执行推仓加仓
                    var result = await _tradingService.ExecuteAddPositionAsync(profile, tier);
                    results.Add(result);
                    
                    if (result.IsSuccess)
                    {
                        _logger.LogInformation($"✅ 推仓阶梯{tier.TierIndex}执行成功: {profile.DisplayName}");
                    }
                    else
                    {
                        _logger.LogWarning($"❌ 推仓阶梯{tier.TierIndex}执行失败: {profile.DisplayName}, 原因: {result.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"推仓逻辑处理失败: {profile.DisplayName}");
                results.Add(TradingExecutionResult.Failed($"推仓逻辑处理失败: {ex.Message}"));
            }
            
            return results;
        }
        
        #endregion
        
        #region 保盈逻辑处理
        
        /// <summary>
        /// 处理保盈逻辑
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <param name="config">保盈配置</param>
        /// <returns>执行结果列表</returns>
        private async Task<System.Collections.Generic.List<TradingExecutionResult>> ProcessProfitProtectionLogicAsync(ContractProfile profile, ContractProfitProtectionConfig config)
        {
            var results = new System.Collections.Generic.List<TradingExecutionResult>();
            
            try
            {
                var currentPnl = profile.UnrealizedPnl;
                
                // 找到当前应该激活的最高保盈档位
                var activeTier = config.Tiers
                    .Where(t => t.IsEnabled && currentPnl >= t.TriggerProfitAmount)
                    .OrderByDescending(t => t.TriggerProfitAmount)
                    .FirstOrDefault();
                
                if (activeTier == null)
                {
                    _logger.LogDebug($"保盈未达到触发条件: {profile.DisplayName}, 当前浮盈: {currentPnl:F2}U");
                    return results;
                }
                
                // 检查该档位是否已经执行过
                if (activeTier.IsExecuted)
                {
                    _logger.LogDebug($"保盈阶梯{activeTier.TierIndex}已执行，跳过: {profile.DisplayName}");
                    return results;
                }
                
                // 更新触发状态
                var tierState = profile.ProfitProtectionStates.FirstOrDefault(s => s.TierIndex == activeTier.TierIndex);
                if (tierState != null && !tierState.IsTriggered)
                {
                    tierState.IsTriggered = true;
                    tierState.TriggerTime = DateTime.Now;
                    tierState.TriggerPrice = profile.CurrentPrice;
                    tierState.TriggerPnl = currentPnl;
                    tierState.ExecutionStatus = "触发中";
                    
                    profile.AddOperationHistory("保盈触发", "成功", $"阶梯{activeTier.TierIndex}: 触发金额{activeTier.TriggerProfitAmount:F2}U");
                    _logger.LogInformation($"保盈阶梯{activeTier.TierIndex}触发: {profile.DisplayName}, 浮盈: {currentPnl:F2}U");
                }
                
                // 执行保盈止损
                var result = await _tradingService.ExecuteProfitProtectionAsync(profile, activeTier);
                results.Add(result);
                
                if (result.IsSuccess)
                {
                    _logger.LogInformation($"✅ 保盈阶梯{activeTier.TierIndex}执行成功: {profile.DisplayName}");
                }
                else
                {
                    _logger.LogWarning($"❌ 保盈阶梯{activeTier.TierIndex}执行失败: {profile.DisplayName}, 原因: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"保盈逻辑处理失败: {profile.DisplayName}");
                results.Add(TradingExecutionResult.Failed($"保盈逻辑处理失败: {ex.Message}"));
            }
            
            return results;
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 获取有效配置（优先使用独立配置，否则使用基础配置）
        /// </summary>
        /// <param name="profile">合约档案</param>
        /// <returns>有效配置</returns>
        private (ContractBreakEvenConfig?, ContractAddPositionConfig?, ContractProfitProtectionConfig?) GetEffectiveConfigurations(ContractProfile profile)
        {
            if (profile.UseIndependentConfig)
            {
                return (profile.IndependentBreakEvenConfig, profile.IndependentAddPositionConfig, profile.IndependentProfitProtectionConfig);
            }
            
            // 使用基础配置，需要转换为合约配置类型
            var baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
            if (baseConfig == null)
            {
                _logger.LogWarning($"基础配置不存在: {profile.BaseConfigName}");
                return (null, null, null);
            }
            
            // 转换基础配置为合约配置格式
            var contractBreakEvenConfig = ConvertToContractBreakEvenConfig(baseConfig.BreakEvenConfig);
            var contractAddPositionConfig = ConvertToContractAddPositionConfig(baseConfig.AddPositionConfig);
            var contractProfitProtectionConfig = ConvertToContractProfitProtectionConfig(baseConfig.ProfitProtectionConfig);
            
            return (contractBreakEvenConfig, contractAddPositionConfig, contractProfitProtectionConfig);
        }
        
        /// <summary>
        /// 转换基础保本配置为合约保本配置
        /// </summary>
        /// <param name="baseConfig">基础保本配置</param>
        /// <returns>合约保本配置</returns>
        private ContractBreakEvenConfig? ConvertToContractBreakEvenConfig(AutoBreakEvenConfig baseConfig)
        {
            if (!baseConfig.IsEnabled)
                return null;
                
            return new ContractBreakEvenConfig
            {
                IsEnabled = baseConfig.IsEnabled,
                TriggerProfitAmount = baseConfig.TriggerProfitAmount,
                BreakEvenPrice = 0, // 将在执行时计算
                IsTriggered = false,
                IsExecuted = false,
                ExecutionMessage = ""
            };
        }
        
        /// <summary>
        /// 转换基础推仓配置为合约推仓配置
        /// </summary>
        /// <param name="baseConfig">基础推仓配置</param>
        /// <returns>合约推仓配置</returns>
        private ContractAddPositionConfig? ConvertToContractAddPositionConfig(AutoAddPositionConfig baseConfig)
        {
            if (!baseConfig.IsEnabled)
                return null;
                
            return new ContractAddPositionConfig
            {
                IsEnabled = baseConfig.IsEnabled,
                Tiers = baseConfig.Tiers.Select(t => new ContractAddPositionTier
                {
                    TierIndex = t.TierIndex,
                    IsEnabled = t.IsEnabled,
                    TriggerProfitAmount = t.TriggerProfitAmount,
                    RiskMultiplier = t.RiskMultiplier,
                    StopLossRatio = t.StopLossRatio,
                    AddPositionQuantity = 0, // 将在执行时计算
                    StopLossPrice = 0, // 将在执行时计算
                    IsTriggered = false,
                    IsExecuted = false,
                    ExecutionMessage = ""
                }).ToList()
            };
        }
        
        /// <summary>
        /// 转换基础保盈配置为合约保盈配置
        /// </summary>
        /// <param name="baseConfig">基础保盈配置</param>
        /// <returns>合约保盈配置</returns>
        private ContractProfitProtectionConfig? ConvertToContractProfitProtectionConfig(AutoProfitProtectionConfig baseConfig)
        {
            if (!baseConfig.IsEnabled)
                return null;
                
            return new ContractProfitProtectionConfig
            {
                IsEnabled = baseConfig.IsEnabled,
                Tiers = baseConfig.Tiers.Select(t => new ContractProfitProtectionTier
                {
                    TierIndex = t.TierIndex,
                    IsEnabled = t.IsEnabled,
                    TriggerProfitAmount = t.TriggerProfitAmount,
                    ProtectionAmount = t.ProtectionAmount,
                    StopLossPrice = 0, // 将在执行时计算
                    IsTriggered = false,
                    IsExecuted = false,
                    ExecutionMessage = ""
                }).ToList()
            };
        }
        
        #endregion
    }
    
    /// <summary>
    /// 监控执行摘要
    /// </summary>
    public class MonitorExecutionSummary
    {
        /// <summary>
        /// 关联的合约档案
        /// </summary>
        public ContractProfile Profile { get; set; } = null!;
        
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// 摘要消息
        /// </summary>
        public string Message { get; set; } = "";
        
        /// <summary>
        /// 保本执行结果
        /// </summary>
        public TradingExecutionResult? BreakEvenResult { get; set; }
        
        /// <summary>
        /// 推仓执行结果列表
        /// </summary>
        public System.Collections.Generic.List<TradingExecutionResult> AddPositionResults { get; set; } = new();
        
        /// <summary>
        /// 保盈执行结果列表
        /// </summary>
        public System.Collections.Generic.List<TradingExecutionResult> ProfitProtectionResults { get; set; } = new();
        
        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime ExecutionTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 获取执行统计
        /// </summary>
        public string GetExecutionStats()
        {
            var breakEvenCount = BreakEvenResult?.IsSuccess == true ? 1 : 0;
            var addPositionCount = AddPositionResults.Count(r => r.IsSuccess);
            var profitProtectionCount = ProfitProtectionResults.Count(r => r.IsSuccess);
            
            return $"执行统计 - 保本: {breakEvenCount}, 推仓: {addPositionCount}, 保盈: {profitProtectionCount}";
        }
    }
} 