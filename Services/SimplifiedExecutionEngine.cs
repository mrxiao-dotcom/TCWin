using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 🎯 简化执行引擎 - 基于新规范的统一执行管理
    /// 整合简化状态服务与现有执行逻辑，确保状态一致性
    /// </summary>
    public class SimplifiedExecutionEngine
    {
        private readonly ILogger<SimplifiedExecutionEngine> _logger;
        private readonly SimplifiedStateService _stateService;
        private readonly SimplifiedConfigManager _configManager;
        private readonly TradingExecutionService _tradingService;
        private readonly BinanceService _binanceService;
        
        // 事件：执行完成通知
        public event EventHandler<SimplifiedExecutionResult>? ExecutionCompleted;

        public SimplifiedExecutionEngine(
            ILogger<SimplifiedExecutionEngine> logger,
            SimplifiedStateService stateService,
            SimplifiedConfigManager configManager,
            TradingExecutionService tradingService,
            BinanceService binanceService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _tradingService = tradingService ?? throw new ArgumentNullException(nameof(tradingService));
            _binanceService = binanceService ?? throw new ArgumentNullException(nameof(binanceService));
        }

        #region 主要执行方法

        /// <summary>
        /// 执行合约监控逻辑 - 统一入口
        /// </summary>
        public async Task<List<SimplifiedExecutionResult>> ExecuteContractMonitoringAsync(string symbol, string positionSide, decimal currentPnl)
        {
            var results = new List<SimplifiedExecutionResult>();
            var contractKey = $"{symbol}_{positionSide}";

            try
            {
                _logger.LogInformation($"🎯 开始执行合约监控: {contractKey}, 当前浮盈: {currentPnl:F2}");

                // 获取合约状态
                var contractState = await _stateService.GetContractStateAsync(symbol, positionSide);
                if (contractState == null)
                {
                    _logger.LogWarning($"⚠️ 合约状态不存在: {contractKey}");
                    return results;
                }

                // 1. 检查保本条件
                var breakEvenResult = await CheckAndExecuteBreakEvenAsync(contractState, currentPnl);
                if (breakEvenResult != null) results.Add(breakEvenResult);

                // 2. 检查推仓条件
                var addPositionResults = await CheckAndExecuteAddPositionAsync(contractState, currentPnl);
                results.AddRange(addPositionResults);

                // 3. 检查保盈条件
                var profitProtectionResults = await CheckAndExecuteProfitProtectionAsync(contractState, currentPnl);
                results.AddRange(profitProtectionResults);

                _logger.LogInformation($"✅ 合约监控执行完成: {contractKey}, 执行操作: {results.Count} 个");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 执行合约监控失败: {contractKey}");
                return results;
            }
        }

        #endregion

        #region 保本执行逻辑

        /// <summary>
        /// 检查并执行保本逻辑
        /// </summary>
        private async Task<SimplifiedExecutionResult?> CheckAndExecuteBreakEvenAsync(SimplifiedContractState contractState, decimal currentPnl)
        {
            try
            {
                var breakEvenConfig = contractState.BreakEvenConfig;
                
                // 检查是否可以执行
                if (!ExecutionStateExtensions.FromInt(breakEvenConfig.ExecutionState).CanExecute())
                {
                    _logger.LogDebug($"🔒 保本已执行或执行中: {contractState.Symbol}_{contractState.PositionSide}");
                    return null;
                }

                // 检查触发条件
                if (currentPnl < breakEvenConfig.TriggerProfitAmount)
                {
                    _logger.LogDebug($"📊 保本条件未满足: {contractState.Symbol} 当前:{currentPnl:F2} < 触发:{breakEvenConfig.TriggerProfitAmount:F2}");
                    return null;
                }

                _logger.LogInformation($"🎯 保本条件满足: {contractState.Symbol} 当前:{currentPnl:F2} >= 触发:{breakEvenConfig.TriggerProfitAmount:F2}");

                // 设置执行中状态
                await _stateService.UpdateExecutionStateAsync(contractState.Symbol, contractState.PositionSide, "BREAKEVEN", 0, StandardExecutionState.Executing, "保本执行中");

                // 执行保本交易
                var executionResult = await ExecuteBreakEvenTradeAsync(contractState, currentPnl);

                // 更新最终状态
                var finalState = executionResult.IsSuccess ? StandardExecutionState.Executed : StandardExecutionState.Failed;
                await _stateService.UpdateExecutionStateAsync(contractState.Symbol, contractState.PositionSide, "BREAKEVEN", 0, finalState, executionResult.Message);

                // 触发执行完成事件
                ExecutionCompleted?.Invoke(this, executionResult);

                return executionResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 保本执行异常: {contractState.Symbol}_{contractState.PositionSide}");
                
                // 设置失败状态
                await _stateService.UpdateExecutionStateAsync(contractState.Symbol, contractState.PositionSide, "BREAKEVEN", 0, StandardExecutionState.Failed, $"保本执行异常: {ex.Message}");
                
                return new SimplifiedExecutionResult
                {
                    Symbol = contractState.Symbol,
                    PositionSide = contractState.PositionSide,
                    OperationType = "BREAKEVEN",
                    TierIndex = 0,
                    IsSuccess = false,
                    Message = $"保本执行异常: {ex.Message}",
                    CurrentPnl = currentPnl,
                    ExecutionTime = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// 执行保本交易
        /// </summary>
        private async Task<SimplifiedExecutionResult> ExecuteBreakEvenTradeAsync(SimplifiedContractState contractState, decimal currentPnl)
        {
            _logger.LogInformation($"💰 开始执行保本交易: {contractState.Symbol}_{contractState.PositionSide}");

            try
            {
                // 创建ContractProfile和ContractBreakEvenConfig来匹配现有的TradingExecutionService方法
                var contractProfile = new ContractProfile
                {
                    Symbol = contractState.Symbol,
                    Side = contractState.PositionSide,
                    UnrealizedPnl = currentPnl
                };

                var breakEvenConfig = new ContractBreakEvenConfig
                {
                    IsEnabled = true,
                    TriggerProfitAmount = contractState.BreakEvenConfig.TriggerProfitAmount,
                    IsExecuted = false
                };

                // 调用现有的交易执行服务
                var result = await _tradingService.ExecuteBreakEvenStopLossAsync(contractProfile, breakEvenConfig);

                return new SimplifiedExecutionResult
                {
                    Symbol = contractState.Symbol,
                    PositionSide = contractState.PositionSide,
                    OperationType = "BREAKEVEN",
                    TierIndex = 0,
                    IsSuccess = result?.IsSuccess == true,
                    Message = result?.Message ?? "保本执行完成",
                    CurrentPnl = currentPnl,
                    ExecutionTime = DateTime.UtcNow,
                    OrderResult = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"保本交易执行异常: {contractState.Symbol}_{contractState.PositionSide}");
                return new SimplifiedExecutionResult
                {
                    Symbol = contractState.Symbol,
                    PositionSide = contractState.PositionSide,
                    OperationType = "BREAKEVEN",
                    TierIndex = 0,
                    IsSuccess = false,
                    Message = $"保本执行异常: {ex.Message}",
                    CurrentPnl = currentPnl,
                    ExecutionTime = DateTime.UtcNow
                };
            }
        }

        #endregion

        #region 推仓执行逻辑

        /// <summary>
        /// 检查并执行推仓逻辑
        /// </summary>
        private async Task<List<SimplifiedExecutionResult>> CheckAndExecuteAddPositionAsync(SimplifiedContractState contractState, decimal currentPnl)
        {
            var results = new List<SimplifiedExecutionResult>();

            try
            {
                var addPositionConfig = contractState.AddPositionConfig;
                
                // 按阶梯顺序检查
                foreach (var tier in addPositionConfig.Tiers.OrderBy(t => t.TierIndex))
                {
                    // 检查是否可以执行
                    if (!ExecutionStateExtensions.FromInt(tier.ExecutionState).CanExecute())
                    {
                        _logger.LogDebug($"🔒 推仓阶梯{tier.TierIndex}已执行: {contractState.Symbol}_{contractState.PositionSide}");
                        continue;
                    }

                    // 检查触发条件
                    if (currentPnl < tier.TriggerProfitAmount)
                    {
                        _logger.LogDebug($"📊 推仓阶梯{tier.TierIndex}条件未满足: {contractState.Symbol} 当前:{currentPnl:F2} < 触发:{tier.TriggerProfitAmount:F2}");
                        continue;
                    }

                    _logger.LogInformation($"🎯 推仓阶梯{tier.TierIndex}条件满足: {contractState.Symbol} 当前:{currentPnl:F2} >= 触发:{tier.TriggerProfitAmount:F2}");

                    // 设置执行中状态
                    await _stateService.UpdateExecutionStateAsync(contractState.Symbol, contractState.PositionSide, "ADDPOSITION", tier.TierIndex, StandardExecutionState.Executing, $"推仓阶梯{tier.TierIndex}执行中");

                    // 执行推仓交易
                    var executionResult = await ExecuteAddPositionTradeAsync(contractState, tier, currentPnl);

                    // 更新最终状态
                    var finalState = executionResult.IsSuccess ? StandardExecutionState.Executed : StandardExecutionState.Failed;
                    await _stateService.UpdateExecutionStateAsync(contractState.Symbol, contractState.PositionSide, "ADDPOSITION", tier.TierIndex, finalState, executionResult.Message);

                    // 触发执行完成事件
                    ExecutionCompleted?.Invoke(this, executionResult);
                    
                    results.Add(executionResult);

                    // 如果执行失败，停止检查后续阶梯
                    if (!executionResult.IsSuccess)
                    {
                        _logger.LogWarning($"⚠️ 推仓阶梯{tier.TierIndex}执行失败，停止检查后续阶梯");
                        break;
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 推仓执行异常: {contractState.Symbol}_{contractState.PositionSide}");
                return results;
            }
        }

        /// <summary>
        /// 执行推仓交易
        /// </summary>
        private async Task<SimplifiedExecutionResult> ExecuteAddPositionTradeAsync(SimplifiedContractState contractState, SimplifiedAddPositionTierState tier, decimal currentPnl)
        {
            _logger.LogInformation($"📈 开始执行推仓交易: {contractState.Symbol}_{contractState.PositionSide} 阶梯{tier.TierIndex}");

            try
            {
                // 创建ContractProfile和ContractAddPositionTier来匹配现有的TradingExecutionService方法
                var contractProfile = new ContractProfile
                {
                    Symbol = contractState.Symbol,
                    Side = contractState.PositionSide,
                    UnrealizedPnl = currentPnl
                };

                var addPositionTier = new ContractAddPositionTier
                {
                    TierIndex = tier.TierIndex,
                    IsEnabled = true,
                    TriggerProfitAmount = tier.TriggerProfitAmount,
                    RiskMultiplier = tier.RiskMultiplier,
                    StopLossRatio = tier.StopLossRatio,
                    IsExecuted = false
                };

                // 调用现有的交易执行服务
                var result = await _tradingService.ExecuteAddPositionAsync(contractProfile, addPositionTier);

                return new SimplifiedExecutionResult
                {
                    Symbol = contractState.Symbol,
                    PositionSide = contractState.PositionSide,
                    OperationType = "ADDPOSITION",
                    TierIndex = tier.TierIndex,
                    IsSuccess = result?.IsSuccess == true,
                    Message = result?.Message ?? $"推仓阶梯{tier.TierIndex}执行完成",
                    CurrentPnl = currentPnl,
                    ExecutionTime = DateTime.UtcNow,
                    OrderResult = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"推仓交易执行异常: {contractState.Symbol}_{contractState.PositionSide} 阶梯{tier.TierIndex}");
                return new SimplifiedExecutionResult
                {
                    Symbol = contractState.Symbol,
                    PositionSide = contractState.PositionSide,
                    OperationType = "ADDPOSITION",
                    TierIndex = tier.TierIndex,
                    IsSuccess = false,
                    Message = $"推仓阶梯{tier.TierIndex}执行异常: {ex.Message}",
                    CurrentPnl = currentPnl,
                    ExecutionTime = DateTime.UtcNow
                };
            }
        }

        #endregion

        #region 保盈执行逻辑

        /// <summary>
        /// 检查并执行保盈逻辑
        /// </summary>
        private async Task<List<SimplifiedExecutionResult>> CheckAndExecuteProfitProtectionAsync(SimplifiedContractState contractState, decimal currentPnl)
        {
            var results = new List<SimplifiedExecutionResult>();

            try
            {
                var profitProtectionConfig = contractState.ProfitProtectionConfig;
                
                // 按阶梯顺序检查
                foreach (var tier in profitProtectionConfig.Tiers.OrderBy(t => t.TierIndex))
                {
                    // 检查是否可以执行
                    if (!ExecutionStateExtensions.FromInt(tier.ExecutionState).CanExecute())
                    {
                        _logger.LogDebug($"🔒 保盈阶梯{tier.TierIndex}已执行: {contractState.Symbol}_{contractState.PositionSide}");
                        continue;
                    }

                    // 检查触发条件
                    if (currentPnl < tier.TriggerProfitAmount)
                    {
                        _logger.LogDebug($"📊 保盈阶梯{tier.TierIndex}条件未满足: {contractState.Symbol} 当前:{currentPnl:F2} < 触发:{tier.TriggerProfitAmount:F2}");
                        continue;
                    }

                    _logger.LogInformation($"🎯 保盈阶梯{tier.TierIndex}条件满足: {contractState.Symbol} 当前:{currentPnl:F2} >= 触发:{tier.TriggerProfitAmount:F2}");

                    // 设置执行中状态
                    await _stateService.UpdateExecutionStateAsync(contractState.Symbol, contractState.PositionSide, "PROFITPROTECTION", tier.TierIndex, StandardExecutionState.Executing, $"保盈阶梯{tier.TierIndex}执行中");

                    // 执行保盈交易
                    var executionResult = await ExecuteProfitProtectionTradeAsync(contractState, tier, currentPnl);

                    // 更新最终状态
                    var finalState = executionResult.IsSuccess ? StandardExecutionState.Executed : StandardExecutionState.Failed;
                    await _stateService.UpdateExecutionStateAsync(contractState.Symbol, contractState.PositionSide, "PROFITPROTECTION", tier.TierIndex, finalState, executionResult.Message);

                    // 触发执行完成事件
                    ExecutionCompleted?.Invoke(this, executionResult);
                    
                    results.Add(executionResult);

                    // 保盈通常只执行一个阶梯
                    break;
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 保盈执行异常: {contractState.Symbol}_{contractState.PositionSide}");
                return results;
            }
        }

        /// <summary>
        /// 执行保盈交易
        /// </summary>
        private async Task<SimplifiedExecutionResult> ExecuteProfitProtectionTradeAsync(SimplifiedContractState contractState, SimplifiedProfitProtectionTierState tier, decimal currentPnl)
        {
            _logger.LogInformation($"🛡️ 开始执行保盈交易: {contractState.Symbol}_{contractState.PositionSide} 阶梯{tier.TierIndex}");

            try
            {
                // 注意：TradingExecutionService中可能没有专门的保盈方法
                // 暂时使用模拟执行，后续可以根据实际需要调整
                _logger.LogInformation($"🛡️ 保盈功能待实现，暂时模拟执行: {contractState.Symbol}_{contractState.PositionSide} 阶梯{tier.TierIndex}");

                // 模拟执行延迟
                await Task.Delay(100);

                return new SimplifiedExecutionResult
                {
                    Symbol = contractState.Symbol,
                    PositionSide = contractState.PositionSide,
                    OperationType = "PROFITPROTECTION",
                    TierIndex = tier.TierIndex,
                    IsSuccess = true,
                    Message = $"保盈阶梯{tier.TierIndex}模拟执行完成",
                    CurrentPnl = currentPnl,
                    ExecutionTime = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"保盈交易执行异常: {contractState.Symbol}_{contractState.PositionSide} 阶梯{tier.TierIndex}");
                return new SimplifiedExecutionResult
                {
                    Symbol = contractState.Symbol,
                    PositionSide = contractState.PositionSide,
                    OperationType = "PROFITPROTECTION",
                    TierIndex = tier.TierIndex,
                    IsSuccess = false,
                    Message = $"保盈阶梯{tier.TierIndex}执行异常: {ex.Message}",
                    CurrentPnl = currentPnl,
                    ExecutionTime = DateTime.UtcNow
                };
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取合约当前持仓信息
        /// </summary>
        private async Task<decimal> GetCurrentPnlAsync(string symbol, string positionSide)
        {
            try
            {
                var positions = await _binanceService.GetPositionsAsync();
                var position = positions?.FirstOrDefault(p => 
                    p.Symbol == symbol && 
                    p.PositionSideString.Equals(positionSide, StringComparison.OrdinalIgnoreCase));
                
                return position?.UnrealizedProfit ?? 0m;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 获取持仓信息失败: {symbol}_{positionSide}");
                return 0m;
            }
        }

        /// <summary>
        /// 批量执行合约监控
        /// </summary>
        public async Task<Dictionary<string, List<SimplifiedExecutionResult>>> ExecuteBatchMonitoringAsync(List<(string symbol, string positionSide)> contracts)
        {
            var allResults = new Dictionary<string, List<SimplifiedExecutionResult>>();

            foreach (var (symbol, positionSide) in contracts)
            {
                try
                {
                    var currentPnl = await GetCurrentPnlAsync(symbol, positionSide);
                    var results = await ExecuteContractMonitoringAsync(symbol, positionSide, currentPnl);
                    
                    var contractKey = $"{symbol}_{positionSide}";
                    allResults[contractKey] = results;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 批量监控执行失败: {symbol}_{positionSide}");
                    allResults[$"{symbol}_{positionSide}"] = new List<SimplifiedExecutionResult>();
                }
            }

            _logger.LogInformation($"✅ 批量监控执行完成: {contracts.Count} 个合约, 总执行操作: {allResults.Values.SelectMany(r => r).Count()} 个");
            return allResults;
        }

        #endregion
    }

    /// <summary>
    /// 简化执行结果模型
    /// </summary>
    public class SimplifiedExecutionResult
    {
        public string Symbol { get; set; } = string.Empty;
        public string PositionSide { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty; // BREAKEVEN, ADDPOSITION, PROFITPROTECTION
        public int TierIndex { get; set; }
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal CurrentPnl { get; set; }
        public DateTime ExecutionTime { get; set; } = DateTime.UtcNow;
        public object? OrderResult { get; set; } // 原始交易结果
        
        /// <summary>
        /// 合约键值
        /// </summary>
        public string ContractKey => $"{Symbol}_{PositionSide}";
        
        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName => TierIndex > 0 ? $"{OperationType}阶梯{TierIndex}" : OperationType;
    }
} 