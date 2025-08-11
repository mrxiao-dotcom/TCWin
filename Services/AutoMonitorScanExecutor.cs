using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 自动盯盘扫描执行器
    /// 🔒 使用FileBasedStateManager实现文件驱动的扫描流程
    /// </summary>
    public class AutoMonitorScanExecutor
    {
        private readonly FileBasedStateManager _fileStateManager;
        private readonly ILogger<AutoMonitorScanExecutor>? _logger;
        
        // 依赖的服务
        private readonly Func<Task<List<PositionInfo>>> _getPositionsFunc;
        private readonly Func<string, Task<AutoMonitorConfig?>> _getConfigFunc;
        private readonly Func<string, PositionInfo, AutoMonitorConfig, ContractMonitoringState, Task<bool>> _executeMonitoringFunc;
        
        // 🔧 新增：状态生成器，用于正确创建新状态
        private readonly ContractMonitoringStateGenerator _stateGenerator;

        public AutoMonitorScanExecutor(
            FileBasedStateManager fileStateManager,
            Func<Task<List<PositionInfo>>> getPositionsFunc,
            Func<string, Task<AutoMonitorConfig?>> getConfigFunc,
            Func<string, PositionInfo, AutoMonitorConfig, ContractMonitoringState, Task<bool>> executeMonitoringFunc,
            ILogger<AutoMonitorScanExecutor>? logger = null)
        {
            _fileStateManager = fileStateManager;
            _getPositionsFunc = getPositionsFunc;
            _getConfigFunc = getConfigFunc;
            _executeMonitoringFunc = executeMonitoringFunc;
            _logger = logger;
            
            // 🔧 新增：初始化状态生成器
            _stateGenerator = new ContractMonitoringStateGenerator(
                logger as ILogger<ContractMonitoringStateGenerator> ?? 
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ContractMonitoringStateGenerator>.Instance);
        }

        /// <summary>
        /// 🔒 执行一次完整的扫描周期
        /// </summary>
        public async Task<ScanResult> ExecuteScanCycleAsync()
        {
            return await _fileStateManager.ExecuteWithFileLockAsync(async (contractStates) =>
            {
                _logger?.LogInformation("⏰ [SCAN-START] 开始扫描周期");
                
                try
                {
                    // 步骤1: 获取当前持仓
                    var currentPositions = await _getPositionsFunc();
                    var activeContractKeys = currentPositions.Select(p => $"{p.Symbol}_{p.PositionSide}").ToHashSet();
                    
                    _logger?.LogInformation($"📊 [SCAN-POSITIONS] 当前活跃持仓: {currentPositions.Count}个");

                    // 步骤2: 清理已平仓的合约状态
                    CleanupClosedPositions(contractStates, activeContractKeys);

                    // 步骤3: 处理每个活跃持仓
                    int processedCount = 0;
                    int executedCount = 0;
                    
                    foreach (var position in currentPositions)
                    {
                        var contractKey = $"{position.Symbol}_{position.PositionSide}";
                        
                        try
                        {
                            // 获取或创建合约状态
                            var contractState = GetOrCreateContractState(contractStates, contractKey, position);
                            
                            // 获取合约配置
                            var config = await _getConfigFunc(contractKey);
                            if (config == null)
                            {
                                _logger?.LogDebug($"⚠️ [SCAN-CONFIG] 合约 {contractKey} 无配置，跳过");
                                continue;
                            }

                            // 🔧 关键修复：使用配置补全状态，但保持现有执行状态
                            CompleteContractState(contractState, config);

                            // 执行监控逻辑
                            var executed = await _executeMonitoringFunc(contractKey, position, config, contractState);
                            if (executed)
                            {
                                executedCount++;
                                _logger?.LogInformation($"✅ [SCAN-EXECUTED] 合约 {contractKey} 执行了操作");
                            }

                            processedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, $"❌ [SCAN-ERROR] 处理合约 {contractKey} 时发生异常");
                        }
                    }

                    var result = new ScanResult
                    {
                        TotalPositions = currentPositions.Count,
                        ProcessedCount = processedCount,
                        ExecutedCount = executedCount,
                        StateFileContractCount = contractStates.Count,
                        ScanTime = DateTime.Now
                    };

                    _logger?.LogInformation($"✅ [SCAN-COMPLETE] 扫描完成 - 已处理 {processedCount}/{currentPositions.Count} 个持仓，执行 {executedCount} 次操作");
                    return result;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "❌ [SCAN-FATAL] 扫描周期发生严重异常");
                    return new ScanResult
                    {
                        ScanTime = DateTime.Now,
                        HasError = true,
                        ErrorMessage = ex.Message
                    };
                }
            });
        }

        /// <summary>
        /// 清理已平仓的合约状态（在锁定上下文中执行）
        /// </summary>
        private void CleanupClosedPositions(Dictionary<string, ContractMonitoringState> contractStates, HashSet<string> activeContractKeys)
        {
            var toRemove = new List<string>();
            
            foreach (var contractKey in contractStates.Keys)
            {
                if (!activeContractKeys.Contains(contractKey))
                {
                    toRemove.Add(contractKey);
                }
            }
            
            foreach (var key in toRemove)
            {
                contractStates.Remove(key);
                _logger?.LogInformation($"🗑️ [CLEANUP] 移除已平仓合约状态: {key}");
            }
            
            if (toRemove.Count > 0)
            {
                _logger?.LogInformation($"🗑️ [CLEANUP] 清理完成，移除 {toRemove.Count} 个已平仓合约");
            }
        }

        /// <summary>
        /// 获取或创建合约状态（在锁定上下文中执行）
        /// </summary>
        private ContractMonitoringState GetOrCreateContractState(Dictionary<string, ContractMonitoringState> contractStates, string contractKey, PositionInfo position)
        {
            if (contractStates.TryGetValue(contractKey, out var existingState))
            {
                // 🔧 关键修复：更新现有状态的实时数据，但保持执行状态不变
                existingState.CurrentQuantity = Math.Abs(position.PositionAmt);
                existingState.CurrentEntryPrice = position.EntryPrice;
                existingState.CurrentMarkPrice = position.MarkPrice;
                existingState.CurrentUnrealizedPnl = position.UnrealizedProfit;
                existingState.LastUpdateTime = DateTime.Now;
                existingState.IsActive = Math.Abs(position.PositionAmt) > 0;
                
                _logger?.LogDebug($"🔄 [UPDATE-STATE] 更新现有状态: {contractKey}, 浮盈: {position.UnrealizedProfit:F2}U");
                return existingState;
            }
            else
            {
                // 🔧 关键修复：创建带有基本结构的新状态，避免null引用
                var newState = new ContractMonitoringState
                {
                    ArchiveId = Guid.NewGuid().ToString(),
                    Symbol = position.Symbol,
                    PositionSide = position.PositionSide.ToString(),
                    BaseConfigName = "智能默认配置", // 临时设置，后续会更新
                    
                    // 持仓基本信息
                    InitialQuantity = Math.Abs(position.PositionAmt),
                    InitialEntryPrice = position.EntryPrice,
                    CurrentQuantity = Math.Abs(position.PositionAmt),
                    CurrentEntryPrice = position.EntryPrice,
                    CurrentMarkPrice = position.MarkPrice,
                    CurrentUnrealizedPnl = position.UnrealizedProfit,
                    
                    // 状态信息
                    CreateTime = DateTime.Now,
                    LastUpdateTime = DateTime.Now,
                    IsActive = true,
                    IsEnabled = true,
                    
                    // 🔧 关键修复：初始化基本配置结构，避免null引用
                    BreakEvenConfig = new StatefulBreakEvenConfig
                    {
                        IsEnabled = false,
                        TriggerProfitAmount = 0,
                        ExecutionState = ExecutionState.NotTriggered
                    },
                    AddPositionConfig = new StatefulAddPositionConfig 
                    { 
                        IsEnabled = false,
                        Tiers = new List<StatefulAddPositionTier>()
                    },
                    ProfitProtectionConfig = new StatefulProfitProtectionConfig 
                    { 
                        IsEnabled = false,
                        Tiers = new List<StatefulProfitProtectionTier>()
                    }
                };
                
                contractStates[contractKey] = newState;
                _logger?.LogInformation($"🆕 [NEW-CONTRACT] 创建新合约状态: {contractKey}, 浮盈: {position.UnrealizedProfit:F2}U");
                
                return newState;
            }
        }
        
        /// <summary>
        /// 🔧 新增：使用配置补全合约状态
        /// </summary>
        private void CompleteContractState(ContractMonitoringState state, AutoMonitorConfig config)
        {
            try
            {
                if (state == null || config == null)
                {
                    _logger?.LogWarning($"⚠️ [COMPLETE-STATE] 状态或配置为null，跳过补全");
                    return;
                }

                // 🔧 修复：改进判断逻辑，总是尝试补全配置
                bool needsUpdate = string.IsNullOrEmpty(state.BaseConfigName) || 
                                   state.BaseConfigName == "智能默认配置" ||
                                   state.BaseConfigName != config.Name;

                if (needsUpdate)
                {
                    _logger?.LogDebug($"🔧 [COMPLETE-STATE] 开始配置补全: {state.Symbol}_{state.PositionSide}");
                    
                    // 更新基本配置信息
                    state.BaseConfigName = config.Name;
                    state.Name = $"{config.Name}_{state.Symbol}";
                    state.ScanIntervalSeconds = config.ScanIntervalSeconds;
                    state.CooldownSeconds = config.CooldownSeconds;
                    
                    // 🔧 修复：安全地更新保本配置
                    if (state.BreakEvenConfig != null && config.BreakEvenConfig != null)
                    {
                        // 保持现有执行状态，只更新配置参数
                        var currentExecutionState = state.BreakEvenConfig.ExecutionState;
                        var currentExecutionTime = state.BreakEvenConfig.ExecutionTime;
                        var currentExecutionPnl = state.BreakEvenConfig.ExecutionPnl;
                        var currentExecutionResult = state.BreakEvenConfig.ExecutionResult;
                        
                        state.BreakEvenConfig.IsEnabled = config.BreakEvenConfig.IsEnabled;
                        state.BreakEvenConfig.TriggerProfitAmount = config.BreakEvenConfig.TriggerProfitAmount;
                        
                        // 🔧 关键：恢复执行状态，不重置！
                        state.BreakEvenConfig.ExecutionState = currentExecutionState;
                        state.BreakEvenConfig.ExecutionTime = currentExecutionTime;
                        state.BreakEvenConfig.ExecutionPnl = currentExecutionPnl;
                        state.BreakEvenConfig.ExecutionResult = currentExecutionResult;
                    }
                    
                    // 🔧 修复：安全地更新推仓配置
                    if (state.AddPositionConfig != null && config.AddPositionConfig != null)
                    {
                        state.AddPositionConfig.IsEnabled = config.AddPositionConfig.IsEnabled;
                        
                        if (config.AddPositionConfig.Tiers != null)
                        {
                            // 保存现有的执行状态
                            var existingTiers = state.AddPositionConfig.Tiers ?? new List<StatefulAddPositionTier>();
                            
                            state.AddPositionConfig.Tiers = config.AddPositionConfig.Tiers.Select(tier => 
                            {
                                // 查找现有阶梯状态
                                var existingTier = existingTiers.FirstOrDefault(t => t.TierIndex == tier.TierIndex);
                                return new StatefulAddPositionTier
                                {
                                    TierIndex = tier.TierIndex,
                                    IsEnabled = tier.IsEnabled,
                                    TriggerProfitAmount = tier.TriggerProfitAmount,
                                    RiskMultiplier = tier.RiskMultiplier,
                                    StopLossRatio = tier.StopLossRatio,
                                    ProfitProtectionAmount = tier.ProfitProtectionAmount,
                                    ExitTargetPnl = tier.ExitTargetPnl,
                                    // 🔧 关键：保持现有执行状态，不重置！
                                    ExecutionState = existingTier?.ExecutionState ?? ExecutionState.NotTriggered,
                                    ExecutionTime = existingTier?.ExecutionTime,
                                    ExecutionPnl = existingTier?.ExecutionPnl ?? 0m,
                                    ExecutionResult = existingTier?.ExecutionResult ?? ""
                                };
                            }).ToList();
                        }
                    }
                    
                    // 🔧 修复：安全地更新保盈配置
                    if (state.ProfitProtectionConfig != null && config.ProfitProtectionConfig != null)
                    {
                        state.ProfitProtectionConfig.IsEnabled = config.ProfitProtectionConfig.IsEnabled;
                        
                        if (config.ProfitProtectionConfig.Tiers != null)
                        {
                            // 保存现有的执行状态
                            var existingTiers = state.ProfitProtectionConfig.Tiers ?? new List<StatefulProfitProtectionTier>();
                            
                            state.ProfitProtectionConfig.Tiers = config.ProfitProtectionConfig.Tiers.Select(tier => 
                            {
                                var existingTier = existingTiers.FirstOrDefault(t => t.TierIndex == tier.TierIndex);
                                return new StatefulProfitProtectionTier
                                {
                                    TierIndex = tier.TierIndex,
                                    IsEnabled = tier.IsEnabled,
                                    TriggerProfitAmount = tier.TriggerProfitAmount,
                                    ProtectionAmount = tier.ProtectionAmount,
                                    // 🔧 关键：保持现有执行状态，不重置！
                                    ExecutionState = existingTier?.ExecutionState ?? ExecutionState.NotTriggered,
                                    ExecutionTime = existingTier?.ExecutionTime,
                                    ExecutionPnl = existingTier?.ExecutionPnl ?? 0m,
                                    ExecutionResult = existingTier?.ExecutionResult ?? ""
                                };
                            }).ToList();
                        }
                    }
                    
                    _logger?.LogDebug($"✅ [COMPLETE-STATE] 配置补全完成: {state.Symbol}_{state.PositionSide} -> 配置: {config.Name}");
                }
                else
                {
                    _logger?.LogDebug($"ℹ️ [COMPLETE-STATE] 配置已是最新，无需补全: {state.Symbol}_{state.PositionSide}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ [COMPLETE-STATE-ERROR] 配置补全失败: {state?.Symbol}_{state?.PositionSide}");
                // 补全失败不应该阻止后续处理，继续执行
            }
        }

        /// <summary>
        /// 获取当前状态快照（用于UI显示）
        /// </summary>
        public async Task<Dictionary<string, ContractMonitoringState>> GetCurrentStatesAsync()
        {
            return await _fileStateManager.GetStatesSnapshotAsync();
        }

        /// <summary>
        /// 记录执行历史
        /// </summary>
        public async Task RecordExecutionAsync(ExecutionHistoryRecord record)
        {
            await _fileStateManager.AppendExecutionHistoryAsync(record);
        }

        /// <summary>
        /// 获取执行历史
        /// </summary>
        public async Task<List<ExecutionHistoryRecord>> GetExecutionHistoryAsync()
        {
            return await _fileStateManager.GetExecutionHistoryAsync();
        }
    }

    /// <summary>
    /// 扫描结果
    /// </summary>
    public class ScanResult
    {
        public int TotalPositions { get; set; }
        public int ProcessedCount { get; set; }
        public int ExecutedCount { get; set; }
        public int StateFileContractCount { get; set; }
        public DateTime ScanTime { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }

        public override string ToString()
        {
            if (HasError)
            {
                return $"扫描失败: {ErrorMessage}";
            }
            
            return $"扫描完成 - 持仓:{TotalPositions}, 处理:{ProcessedCount}, 执行:{ExecutedCount}, 状态文件合约:{StateFileContractCount}";
        }
    }
} 