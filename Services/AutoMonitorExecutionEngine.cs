using System;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Generic; // Added for Dictionary
using System.Threading; // Added for SemaphoreSlim

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 自动监控执行引擎 - 核心监控逻辑处理
    /// </summary>
    public class AutoMonitorExecutionEngine
    {
        private readonly ILogger<AutoMonitorExecutionEngine> _logger;
        private readonly TradingExecutionService _tradingService;
        private readonly ContractProfileService _profileService;
        private readonly BaseConfigManager _configManager;
        private readonly AutoMonitorPersistenceService _persistenceService;
        private readonly SimpleStateManager? _stateManager;
        private readonly ContractMonitoringStateService? _contractStateService;
        
        // 🚨【新增】执行锁，避免并发执行
        private readonly SemaphoreSlim _executionLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, bool> _contractExecutionStatus = new Dictionary<string, bool>();
        
        // 事件系统 - 路由关键诊断日志到UI
        public event Action<string, string>? WorkLogRequested;
        
        private void AddWorkLog(string level, string message)
        {
            WorkLogRequested?.Invoke(level, message);
        }
        
        public AutoMonitorExecutionEngine(
            ILogger<AutoMonitorExecutionEngine> logger,
            TradingExecutionService tradingService,
            ContractProfileService profileService,
            BaseConfigManager configManager,
            AutoMonitorPersistenceService persistenceService,
            SimpleStateManager? stateManager = null,
            ContractMonitoringStateService? contractStateService = null)
        {
            _logger = logger;
            _tradingService = tradingService;
            _profileService = profileService;
            _configManager = configManager;
            _persistenceService = persistenceService;
            _stateManager = stateManager;
            _contractStateService = contractStateService;
            
            // 🔧 新增：诊断状态服务注入情况
            _logger.LogCritical($"🔍【执行引擎初始化】ContractMonitoringStateService注入状态: {_contractStateService != null}");
            if (_contractStateService != null)
            {
                _logger.LogCritical("✅ 执行引擎已正确接收到ContractMonitoringStateService");
            }
            else
            {
                _logger.LogCritical("❌ 执行引擎未接收到ContractMonitoringStateService，状态更新将无法工作");
            }
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
                // 🔍【执行引擎启动确认】
                _logger.LogInformation($"🔍【执行引擎启动】{profile.Symbol}: 独立配置={profile.UseIndependentConfig}, 基础配置={profile.BaseConfigName}");
                
                var (breakEvenConfig, addPositionConfig, profitProtectionConfig) = GetEffectiveConfigurations(profile);
                
                // 🔍【核心比对开始】
                _logger.LogInformation($"🔍【核心比对开始】{profile.Symbol}: 浮盈{profile.UnrealizedPnl:F2}U");
                
                // 1. 检查并执行保本逻辑
                if (breakEvenConfig?.IsEnabled == true)
                {
                    var breakEvenResult = await ProcessBreakEvenLogicAsync(profile, breakEvenConfig);
                    summary.BreakEvenResult = breakEvenResult;
                }
                else
                {
                    _logger.LogInformation($"❌【保本跳过】{profile.Symbol}: 配置未启用");
                }
                
                // 2. 检查并执行推仓逻辑
                if (addPositionConfig?.IsEnabled == true)
                {
                    var addPositionResults = await ProcessAddPositionLogicAsync(profile, addPositionConfig, profile.UnrealizedPnl, IsSimulationMode());
                    summary.AddPositionResults.AddRange(addPositionResults);
                }
                else
                {
                    _logger.LogInformation($"❌【推仓跳过】{profile.Symbol}: 配置未启用");
                }
                
                // 3. 检查并执行保盈逻辑
                if (profitProtectionConfig?.IsEnabled == true)
                {
                    var profitProtectionResults = await ProcessProfitProtectionLogicAsync(profile, profitProtectionConfig);
                    summary.ProfitProtectionResults.AddRange(profitProtectionResults);
                }
                else
                {
                    _logger.LogInformation($"❌【保盈跳过】{profile.Symbol}: 配置未启用");
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
                var currentPnl = profile.UnrealizedPnl;
                var triggerAmount = config.TriggerProfitAmount;
                
                // 🔧 检查是否为模拟环境（用于整个方法的逻辑判断）
                bool isSimulationMode = IsSimulationMode();
                
                // 🔍【浮盈比对-保本】关键信息
                _logger.LogCritical($"🔍【浮盈比对-保本】{profile.Symbol}: 浮盈{currentPnl:F2}U vs 触发条件{triggerAmount:F2}U");
                
                // 🔧 【关键修复】使用多重状态检查，确保万无一失
                var positionSide = profile.Side; // 使用标准化的LONG/SHORT
                var isExecutedInState = _stateManager?.IsOperationExecuted(profile.Symbol, positionSide, "BreakEven") ?? false;
                
                // 🔧 【新增】检查profile内部的保本状态
                var isExecutedInProfile = profile.BreakEvenState.IsTriggered && 
                                         (profile.BreakEvenState.ExecutionStatus == StatusConstants.Executed || profile.BreakEvenState.ExecutionStatus == "模拟执行");
                
                // 🔧 【关键修复】从统一状态文件检查是否已执行 - 使用标准化格式
                var contractKey = $"{profile.Symbol}_{(profile.PositionSize > 0 ? "LONG" : "SHORT")}";
                var isExecutedInUnifiedFile = _contractStateService?.IsExecuted(contractKey, "BreakEven") ?? false;
                
                _logger.LogCritical($"🔍【保本状态检查】{profile.Symbol}:");
                _logger.LogCritical($"   📊 Config.IsExecuted: {config.IsExecuted}");
                _logger.LogCritical($"   📊 StateManager.IsExecuted: {isExecutedInState}");
                _logger.LogCritical($"   📊 ProfileState.IsExecuted: {isExecutedInProfile}");
                _logger.LogCritical($"   📊 UnifiedFile.IsExecuted: {isExecutedInUnifiedFile}");
                _logger.LogCritical($"   📊 ProfileState.ExecutionStatus: {profile.BreakEvenState.ExecutionStatus}");
                _logger.LogCritical($"   🔧 检查键值: {profile.Symbol}_{positionSide}_BreakEven");
                
                if (config.IsExecuted || isExecutedInState || isExecutedInProfile || isExecutedInUnifiedFile)
                {
                    _logger.LogCritical($"🔍【保本跳过】{profile.Symbol}: 已执行过");
                    _logger.LogCritical($"   🔧 状态详情: Config={config.IsExecuted}, State={isExecutedInState}, Profile={isExecutedInProfile}, UnifiedFile={isExecutedInUnifiedFile}");
                    _logger.LogCritical($"   🔧 检查键值: {profile.Symbol}_{positionSide}_BreakEven");
                    
                    // 🔧 【关键修复】模拟模式下，即使已执行也要返回成功结果，确保UI状态同步
                    if (isSimulationMode)
                    {
                        _logger.LogInformation($"🎯【模拟模式】{profile.Symbol}: 保本已执行，返回模拟成功结果");
                        return TradingExecutionResult.Success("模拟保本已执行");
                    }
                    
                    return null; // 真实模式下跳过已执行的保本
                }
                
                // 检查2：是否达到触发条件
                if (currentPnl < triggerAmount)
                {
                    // 🔧 【简化日志】保本未触发时静默跳过，避免冗余日志
                    return null;
                }
                
                _logger.LogCritical($"✅【保本触发】{profile.Symbol}: {currentPnl:F2}U >= {triggerAmount:F2}U，开始执行");
                
                // 🔧 关键修改：先执行交易，只有成功后才更新状态
                _logger.LogInformation($"🚀 【保本执行-引擎】{profile.Symbol} 开始执行保本止损");
                
                // 🎯【模拟环境标记】在日志中标明当前环境
                if (isSimulationMode)
                {
                    _logger.LogInformation($"🎯【模拟环境】{profile.Symbol} 正在模拟保本止损执行");
                }
                
                // 执行保本止损
                var result = await _tradingService.ExecuteBreakEvenStopLossAsync(profile, config);
                
                // 🔍 【状态更新诊断】详细记录执行结果和状态更新过程
                _logger.LogCritical($"🔍【状态更新诊断】{profile.Symbol} 保本执行结果分析:");
                _logger.LogCritical($"   📈 执行结果: IsSuccess={result?.IsSuccess}, Message={result?.Message}");
                _logger.LogCritical($"   📊 当前状态: IsTriggered={profile.BreakEvenState.IsTriggered}, IsExecuted={config.IsExecuted}");
                _logger.LogCritical($"   🎯 模拟环境: {isSimulationMode}");
                
                // 🔧 只有交易真正成功后才更新状态和持久化
                if (result?.IsSuccess == true)
                {
                    _logger.LogCritical($"✅【状态更新】{profile.Symbol} 开始更新保本状态:");
                    
                    // 🔧 交易成功后才更新触发状态
                    if (!profile.BreakEvenState.IsTriggered || isSimulationMode)
                    {
                        _logger.LogCritical($"   🔄 更新BreakEvenState状态:");
                        profile.BreakEvenState.IsTriggered = true;
                        profile.BreakEvenState.TriggerTime = DateTime.Now;
                        profile.BreakEvenState.TriggerPrice = profile.CurrentPrice;
                        profile.BreakEvenState.TriggerPnl = currentPnl;
                        profile.BreakEvenState.ExecutionStatus = isSimulationMode ? "模拟执行" : StatusConstants.Executed;
                        _logger.LogCritical($"     IsTriggered: false → true");
                        _logger.LogCritical($"     TriggerTime: {profile.BreakEvenState.TriggerTime}");
                        _logger.LogCritical($"     ExecutionStatus: {profile.BreakEvenState.ExecutionStatus}");
                        
                        // 标记配置为已执行
                        config.IsExecuted = true;
                        config.ExecutionTime = DateTime.Now;
                        _logger.LogCritical($"   🔄 更新Config状态: IsExecuted设为true, ExecutionTime: {config.ExecutionTime}");
                        
                        // 🔧 【关键修复】同步状态到状态管理器并立即保存，确保键值一致性
                        _stateManager?.RecordExecution(profile.Symbol, positionSide, ExecutionType.BreakEven, 
                            0, currentPnl, true, "保本执行成功", true);
                        _stateManager?.SaveToPersistence();
                        
                        // 🔧 【新增】同时更新到新的统一状态文件
                        if (_contractStateService != null)
                        {
                            _logger.LogCritical($"🔍【关键诊断】即将调用UpdateExecutionStatus: {contractKey}");
                            _logger.LogCritical($"   📊 参数: operationType=BreakEven, tierIndex=null, isSuccess=true, triggerPnl={currentPnl}");
                            
                            try
                            {
                                _contractStateService.UpdateExecutionStatus(contractKey, "BreakEven", null, true, currentPnl, "保本执行成功");
                                _logger.LogCritical($"✅ UpdateExecutionStatus调用成功: {contractKey}");
                            }
                            catch (Exception updateEx)
                            {
                                _logger.LogCritical($"❌ UpdateExecutionStatus调用失败: {contractKey} - {updateEx.Message}");
                            }
                        }
                        else
                        {
                            _logger.LogCritical($"❌【关键问题】_contractStateService为null，无法更新统一状态文件！");
                        }
                        
                        _logger.LogInformation($"   🔧 记录状态详情: 键值={profile.Symbol}_{positionSide}_BreakEven");
                        _logger.LogCritical($"   🔄 已同步保本状态到状态管理器并保存到文件");
                        
                        var operationResult = isSimulationMode ? "模拟成功" : "成功";
                        profile.AddOperationHistory("保本执行", operationResult, $"交易{operationResult} - 触发金额: {triggerAmount:F2}U, 当前浮盈: {currentPnl:F2}U");
                        _logger.LogCritical($"   📝 添加操作历史: 保本执行{operationResult}");
                    }
                    else
                    {
                        _logger.LogCritical($"   ⚠️ BreakEvenState已经触发，跳过状态更新");
                    }
                    _logger.LogInformation($"✅ 【保本结果-引擎】{profile.Symbol} {(isSimulationMode ? "模拟" : "")}成功");
                    
                    // 🔧 【重要】立即持久化状态变更（包括模拟数据）
                    try
                    {
                        await _profileService.UpdateProfileAsync(profile);
                        _logger.LogInformation($"💾 【保本持久化】{profile.Symbol} 状态已保存到存储");
                    }
                    catch (Exception persistEx)
                    {
                        _logger.LogError(persistEx, $"❌ 【保本持久化失败】{profile.Symbol} 状态保存失败");
                    }
                }
                else
                {
                    _logger.LogCritical($"❌【状态不更新】{profile.Symbol} 保本执行失败，不更新状态:");
                    _logger.LogCritical($"   失败原因: {result?.Message ?? "执行结果为null"}");
                    _logger.LogCritical($"   状态保持: IsTriggered={profile.BreakEvenState.IsTriggered}, IsExecuted={config.IsExecuted}");
                    _logger.LogWarning($"❌ 【保本结果-引擎】{profile.Symbol} 失败: {result?.Message ?? "执行结果为null"}");
                    profile.AddOperationHistory("保本尝试", "失败", $"交易失败 - {result?.Message ?? "执行结果为null"}");
                    
                    // 🔧 【重要】失败状态也要持久化
                    try
                    {
                        await _profileService.UpdateProfileAsync(profile);
                        _logger.LogInformation($"💾 【保本持久化】{profile.Symbol} 失败状态已保存");
                    }
                    catch (Exception persistEx)
                    {
                        _logger.LogError(persistEx, $"❌ 【保本持久化失败】{profile.Symbol} 失败状态保存异常");
                    }
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
        /// 处理推仓逻辑 - 🚨【重构】实现缓存优先的新工作流程
        /// </summary>
        private async Task<List<TradingExecutionResult>> ProcessAddPositionLogicAsync(
            ContractProfile profile,
            ContractAddPositionConfig addPositionConfig,
            decimal currentPnl,
            bool isSimulationMode,
            CancellationToken cancellationToken = default)
        {
            var results = new List<TradingExecutionResult>();
            var contractKey = $"{profile.Symbol}_{profile.Side}";
            
            // 🚨【新工作流程1】检查执行锁，避免并发
            if (_contractExecutionStatus.TryGetValue(contractKey, out var isExecuting) && isExecuting)
            {
                _logger.LogCritical($"🔒【并发保护】{contractKey}: 上一轮执行未完成，等待...");
                AddWorkLog("INFO", $"🔒【并发保护】{contractKey}: 等待上一轮执行完成");
                return results;
            }

            // 🚨【新工作流程2】获取执行锁
            await _executionLock.WaitAsync(cancellationToken);
            try
            {
                _contractExecutionStatus[contractKey] = true;
                _logger.LogCritical($"🔓【获取锁】{contractKey}: 开始执行推仓检查");
                
                // 🚨【新工作流程3】从缓存读取当前状态
                var currentState = await LoadCurrentStateFromCache(contractKey);
                if (currentState == null)
                {
                    _logger.LogCritical($"❌【缓存读取失败】{contractKey}: 无法获取状态，跳过执行");
                    return results;
                }
                
                var sortedTiers = addPositionConfig.Tiers
                    .Where(t => t.IsEnabled && currentPnl >= t.TriggerProfitAmount)
                    .OrderBy(t => t.TierIndex)
                    .ToList();

                foreach (var tier in sortedTiers)
                {
                    _logger.LogCritical($"🚨【缓存状态检查】{profile.Symbol}-阶梯{tier.TierIndex}: 开始检查");
                    
                    // 🚨【新工作流程4】检查缓存中的执行状态
                    var cachedTier = currentState.AddPositionConfig?.Tiers?.FirstOrDefault(t => t.TierIndex == tier.TierIndex);
                    if (cachedTier?.IsExecuted == true || cachedTier?.ExecutionState == ExecutionState.Executed)
                    {
                        _logger.LogCritical($"🔍【缓存跳过】{profile.Symbol}-阶梯{tier.TierIndex}: 缓存显示已执行");
                        AddWorkLog("INFO", $"🔍【缓存跳过】{profile.Symbol}-阶梯{tier.TierIndex}: 已执行过");
                        continue;
                    }
                    
                    _logger.LogCritical($"✅【执行条件满足】{profile.Symbol}-阶梯{tier.TierIndex}: {currentPnl:F2}U >= {tier.TriggerProfitAmount:F2}U");
                    AddWorkLog("CRITICAL", $"✅【执行推仓】{profile.Symbol}-阶梯{tier.TierIndex}: {currentPnl:F2}U >= {tier.TriggerProfitAmount:F2}U");

                    // 🔧 执行交易
                    var result = await _tradingService.ExecuteAddPositionAsync(profile, tier);
                    results.Add(result);

                    // 🚨【新工作流程5】交易成功后立即更新缓存状态
                    if (result?.IsSuccess == true)
                    {
                        _logger.LogCritical($"🎉【交易成功】{profile.Symbol}-阶梯{tier.TierIndex}: 立即更新缓存状态");
                        
                        // 更新缓存中的状态
                        if (cachedTier != null)
                        {
                            cachedTier.IsExecuted = true;
                            cachedTier.ExecutionState = ExecutionState.Executed;
                            cachedTier.ExecutionTime = DateTime.Now;
                            cachedTier.ExecutionPnl = currentPnl;
                            cachedTier.ExecutionResult = "推仓执行成功";
                        }
                        
                        // 🚨【新工作流程6】立即保存到文件
                        await SaveStateToFile(contractKey, currentState);
                        _logger.LogCritical($"✅【文件保存】{profile.Symbol}-阶梯{tier.TierIndex}: 状态已保存到文件");
                        
                        // 🚨【新工作流程7】通知UI更新
                        NotifyUIUpdate(profile.Symbol, profile.Side, "AddPosition", tier.TierIndex, true);
                        _logger.LogCritical($"🔔【UI通知】{profile.Symbol}-阶梯{tier.TierIndex}: 已通知UI更新");
                        
                        _logger.LogInformation($"✅ 推仓阶梯{tier.TierIndex}执行成功并完成状态同步: {profile.DisplayName}");
                    }
                    else
                    {
                        _logger.LogError($"❌ 推仓阶梯{tier.TierIndex}执行失败: {profile.DisplayName} - {result?.Message}");
                    }
                }
            }
            finally
            {
                // 🚨【新工作流程8】释放锁
                _contractExecutionStatus[contractKey] = false;
                _executionLock.Release();
                _logger.LogCritical($"🔓【释放锁】{contractKey}: 执行完成");
            }

            return results;
        }
        
        /// <summary>
        /// 🚨【新方法】从缓存加载当前状态
        /// </summary>
        private async Task<ContractMonitoringState?> LoadCurrentStateFromCache(string contractKey)
        {
            try
            {
                var state = _contractStateService?.GetMonitoringState(contractKey);
                _logger.LogCritical($"📋【缓存加载】{contractKey}: {(state != null ? "成功" : "失败")}");
                return state;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌【缓存加载异常】{contractKey}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 🚨【新方法】保存状态到文件
        /// </summary>
        private async Task SaveStateToFile(string contractKey, ContractMonitoringState state)
        {
            try
            {
                if (_contractStateService != null)
                {
                    var states = _contractStateService.LoadMonitoringStates();
                    states[contractKey] = state;
                    _contractStateService.SaveMonitoringStates(states);
                    _logger.LogCritical($"💾【文件保存成功】{contractKey}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌【文件保存失败】{contractKey}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 🚨【新方法】通知UI更新
        /// </summary>
        private void NotifyUIUpdate(string symbol, string side, string operationType, int tierIndex, bool isSuccess)
        {
            try
            {
                // 通过事件系统通知UI
                AddWorkLog("SUCCESS", $"🎉【状态更新】{symbol} {side}-阶梯{tierIndex}: 已执行");
                
                // 可以在这里添加更多UI通知逻辑
                _logger.LogInformation($"🔔 UI更新通知已发送: {symbol}_{side} {operationType}_{tierIndex} = {isSuccess}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌【UI通知失败】{symbol}_{side}: {ex.Message}");
            }
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
                var enabledTiers = config.Tiers.Where(t => t.IsEnabled).ToList();
                
                // 🔧【模拟环境修复】检查是否为模拟环境
                bool isSimulationMode = IsSimulationMode();
                
                // 🔍【浮盈比对-保盈】关键信息
                _logger.LogInformation($"🔍【浮盈比对-保盈】{profile.Symbol}: 当前浮盈{currentPnl:F2}U，检查{enabledTiers.Count}个阶梯{(isSimulationMode ? "（模拟环境）" : "")}");
                
                // 找到当前应该激活的最高保盈档位
                var activeTier = enabledTiers
                    .Where(t => currentPnl >= t.TriggerProfitAmount)
                    .OrderByDescending(t => t.TriggerProfitAmount)
                    .FirstOrDefault();
                
                if (activeTier == null)
                {
                    // 🔧 【简化日志】没有触发的阶梯静默跳过，避免冗余日志
                    return results;
                }
                
                _logger.LogInformation($"✅【保盈触发】{profile.Symbol}-阶梯{activeTier.TierIndex}: {currentPnl:F2}U >= {activeTier.TriggerProfitAmount:F2}U，开始执行");
                
                // 🔧 【关键修复】使用统一状态检查，确保键值一致性
                var positionSide = profile.Side; // 使用标准化的LONG/SHORT
                var isExecutedInState = _stateManager?.IsOperationExecuted(profile.Symbol, positionSide, "ProfitProtection", activeTier.TierIndex) ?? false;
                
                // 🔧 【关键修复】从统一状态文件检查是否已执行
                var contractKey = $"{profile.Symbol}_{(profile.PositionSize > 0 ? "LONG" : "SHORT")}";
                var isExecutedInUnifiedFile = _contractStateService?.IsExecuted(contractKey, "ProfitProtection", activeTier.TierIndex) ?? false;
                
                _logger.LogCritical($"🔍【保盈状态检查】{profile.Symbol}-阶梯{activeTier.TierIndex}:");
                _logger.LogCritical($"   📊 Config.IsExecuted: {activeTier.IsExecuted}");
                _logger.LogCritical($"   📊 StateManager.IsExecuted: {isExecutedInState}");
                _logger.LogCritical($"   📊 UnifiedFile.IsExecuted: {isExecutedInUnifiedFile}");
                _logger.LogCritical($"   🔧 检查键值: {profile.Symbol}_{positionSide}_ProfitProtection_{activeTier.TierIndex}");
                
                if (activeTier.IsExecuted || isExecutedInState || isExecutedInUnifiedFile)
                {
                    _logger.LogInformation($"🔍【保盈跳过】{profile.Symbol}-阶梯{activeTier.TierIndex}: 已执行过");
                    _logger.LogInformation($"   🔧 状态详情: Config={activeTier.IsExecuted}, State={isExecutedInState}, UnifiedFile={isExecutedInUnifiedFile}");
                    _logger.LogInformation($"   🔧 状态检查详情: 键值={profile.Symbol}_{positionSide}_ProfitProtection_{activeTier.TierIndex}");
                    return results; // 跳过已执行的保盈
                }
                
                // 🎯【模拟环境标记】在日志中标明当前环境
                if (isSimulationMode)
                {
                    _logger.LogInformation($"🎯【模拟环境】{profile.Symbol}-阶梯{activeTier.TierIndex} 正在模拟保盈执行");
                }
                
                // 🔧 关键修改：先执行交易，只有成功后才更新状态
                var result = await _tradingService.ExecuteProfitProtectionAsync(profile, activeTier);
                results.Add(result);
                
                // 🔧 只有交易真正成功后才更新状态和持久化
                if (result.IsSuccess)
                {
                    _logger.LogInformation($"✅ 保盈阶梯{activeTier.TierIndex}执行成功，更新状态: {profile.DisplayName}");
                    
                    // 🔧 交易成功后才更新触发状态
                    var tierState = profile.ProfitProtectionStates.FirstOrDefault(s => s.TierIndex == activeTier.TierIndex);
                    if (tierState != null && (!tierState.IsTriggered || isSimulationMode))
                    {
                        tierState.IsTriggered = true;
                        tierState.TriggerTime = DateTime.Now;
                        tierState.TriggerPrice = profile.CurrentPrice;
                        tierState.TriggerPnl = currentPnl;
                        tierState.ExecutionStatus = isSimulationMode ? "模拟执行" : "已执行";
                        
                        // 标记配置为已执行
                        activeTier.IsExecuted = true;
                        activeTier.ExecutionTime = DateTime.Now;
                        _logger.LogInformation($"✅ 保盈阶梯{activeTier.TierIndex}状态已更新: {profile.DisplayName}");
                        
                        // 🔧 【关键修复】同步状态到状态管理器并立即保存，确保键值一致性
                        _stateManager?.RecordExecution(profile.Symbol, positionSide, ExecutionType.ProfitProtection, 
                            activeTier.TierIndex, currentPnl, true, "保盈执行成功", true);
                        _stateManager?.SaveToPersistence();
                        
                        // 🔧 【新增】同时更新到新的统一状态文件
                        if (_contractStateService != null)
                        {
                            _logger.LogCritical($"🔍【关键诊断】即将调用UpdateExecutionStatus: {contractKey}");
                            _logger.LogCritical($"   📋 参数: operationType=ProfitProtection, tierIndex={activeTier.TierIndex}, isSuccess=true, triggerPnl={currentPnl}");
                            
                            try
                            {
                                _contractStateService.UpdateExecutionStatus(contractKey, "ProfitProtection", activeTier.TierIndex, true, currentPnl, "保盈执行成功");
                                _logger.LogCritical($"✅ UpdateExecutionStatus调用成功: {contractKey}-T{activeTier.TierIndex}");
                            }
                            catch (Exception updateEx)
                            {
                                _logger.LogCritical($"❌ UpdateExecutionStatus调用失败: {contractKey}-T{activeTier.TierIndex} - {updateEx.Message}");
                            }
                        }
                        else
                        {
                            _logger.LogCritical($"❌【关键问题】_contractStateService为null，无法更新统一状态文件！");
                        }
                        
                        _logger.LogInformation($"🔄 已同步保盈阶梯{activeTier.TierIndex}状态到状态管理器并保存到文件");
                        _logger.LogInformation($"   🔧 记录状态详情: 键值={profile.Symbol}_{positionSide}_ProfitProtection_{activeTier.TierIndex}");
                        
                        var operationResult = isSimulationMode ? "模拟成功" : "成功";
                        profile.AddOperationHistory("保盈执行", operationResult, $"交易{operationResult} - 阶梯{activeTier.TierIndex}: 触发金额{activeTier.TriggerProfitAmount:F2}U");
                    }
                    
                    // 🔧 【重要】成功状态持久化
                    try
                    {
                        await _profileService.UpdateProfileAsync(profile);
                        _logger.LogInformation($"💾 【保盈持久化】{profile.Symbol}-阶梯{activeTier.TierIndex} 状态已保存");
                    }
                    catch (Exception persistEx)
                    {
                        _logger.LogError(persistEx, $"❌ 【保盈持久化失败】{profile.Symbol}-阶梯{activeTier.TierIndex}");
                    }
                }
                else
                {
                    _logger.LogWarning($"❌ 保盈阶梯{activeTier.TierIndex}执行失败: {profile.DisplayName} - {result.Message}");
                    profile.AddOperationHistory("保盈尝试", "失败", $"交易失败 - 阶梯{activeTier.TierIndex}: {result.Message}");
                    
                    // 🔧 【重要】失败状态也要持久化
                    try
                    {
                        await _profileService.UpdateProfileAsync(profile);
                        _logger.LogInformation($"💾 【保盈持久化】{profile.Symbol}-阶梯{activeTier.TierIndex} 失败状态已保存");
                    }
                    catch (Exception persistEx)
                    {
                        _logger.LogError(persistEx, $"❌ 【保盈持久化失败】{profile.Symbol}-阶梯{activeTier.TierIndex}");
                    }
                }
                
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"保盈逻辑处理失败: {profile.DisplayName}");
                results.Add(TradingExecutionResult.Failed($"保盈逻辑处理失败: {ex.Message}"));
                return results;
            }
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
                // 🔍【配置获取诊断】
                _logger.LogInformation($"🔍【配置获取】{profile.Symbol}: 使用独立配置={profile.UseIndependentConfig}");
                
                if (profile.UseIndependentConfig)
                {
                    var hasBreakEven = profile.IndependentBreakEvenConfig?.IsEnabled == true;
                    var hasAddPosition = profile.IndependentAddPositionConfig?.IsEnabled == true;
                    var hasProfitProtection = profile.IndependentProfitProtectionConfig?.IsEnabled == true;
                    
                    _logger.LogInformation($"🔍【独立配置】{profile.Symbol}: 保本={hasBreakEven}, 推仓={hasAddPosition}, 保盈={hasProfitProtection}");
                    
                    // 🔍【关键调试】显示具体的触发条件数值
                    if (hasBreakEven && profile.IndependentBreakEvenConfig != null)
                    {
                        _logger.LogInformation($"🔍【独立保本触发条件】{profile.Symbol}: {profile.IndependentBreakEvenConfig.TriggerProfitAmount:F2}U");
                    }
                    
                    if (hasAddPosition && profile.IndependentAddPositionConfig?.Tiers?.Any() == true)
                    {
                        var firstTier = profile.IndependentAddPositionConfig.Tiers.Where(t => t.IsEnabled).OrderBy(t => t.TierIndex).FirstOrDefault();
                        if (firstTier != null)
                        {
                            _logger.LogInformation($"🔍【独立推仓一阶触发条件】{profile.Symbol}: {firstTier.TriggerProfitAmount:F2}U");
                        }
                    }
                    
                    return (profile.IndependentBreakEvenConfig, profile.IndependentAddPositionConfig, profile.IndependentProfitProtectionConfig);
                }
                
                // 使用基础配置，需要转换为合约配置类型
                var baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
                if (baseConfig == null)
                {
                    _logger.LogWarning($"❌ 基础配置不存在: '{profile.BaseConfigName}'");
                    
                    // 尝试重新加载配置
                    try
                    {
                        var reloadMethod = _configManager.GetType().GetMethod("LoadConfigurationsAsync", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (reloadMethod != null)
                        {
                            var task = (System.Threading.Tasks.Task)reloadMethod.Invoke(_configManager, null);
                            task.Wait(1000);
                        }
                    }
                    catch (Exception reloadEx)
                    {
                        _logger.LogError($"重新加载配置失败: {reloadEx.Message}");
                    }
                    
                    baseConfig = _configManager.GetConfiguration(profile.BaseConfigName);
                    if (baseConfig == null)
                    {
                        return (null, null, null);
                    }
                }
                
                // 🔍【关键调试】显示基础配置的触发条件数值
                _logger.LogInformation($"🔍【基础配置】{profile.Symbol}: 配置名={profile.BaseConfigName}");
                if (baseConfig.BreakEvenConfig?.IsEnabled == true)
                {
                    _logger.LogInformation($"🔍【基础保本触发条件】{profile.Symbol}: {baseConfig.BreakEvenConfig.TriggerProfitAmount:F2}U");
                }
                if (baseConfig.AddPositionConfig?.IsEnabled == true && baseConfig.AddPositionConfig.Tiers?.Any() == true)
                {
                    var firstTier = baseConfig.AddPositionConfig.Tiers.Where(t => t.IsEnabled).OrderBy(t => t.TierIndex).FirstOrDefault();
                    if (firstTier != null)
                    {
                        _logger.LogInformation($"🔍【基础推仓一阶触发条件】{profile.Symbol}: {firstTier.TriggerProfitAmount:F2}U");
                    }
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
                    // 🚨【关键修复】保持与原始对象状态一致，而不是总是false
                    IsExecuted = t.IsExecuted,
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
        
        #region 私有方法
        
        /// <summary>
        /// 检查当前是否为模拟环境
        /// </summary>
        /// <returns>是否为模拟环境</returns>
        private bool IsSimulationMode()
        {
            try
            {
                // 🔧 【重构】直接使用全局模式管理器
                var globalMode = GlobalModeManager.Instance;
                bool isGlobalSimulation = globalMode.IsSimulationMode;
                
                _logger.LogDebug($"🎯 AutoMonitorExecutionEngine模拟环境检查: 全局模式={globalMode.ModeDisplayText}");
                
                return isGlobalSimulation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoMonitorExecutionEngine检查模拟环境失败，默认返回模拟模式");
                return true;
            }
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