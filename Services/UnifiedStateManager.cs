using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 统一状态管理器 - 整合三套状态系统
    /// 解决内存状态、历史状态、持久化状态的重复和不一致问题
    /// </summary>
    public class UnifiedStateManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly AutoMonitorPersistenceService _persistenceService;
        private IEventBus? _eventBus;
        
        // 🎯 核心数据：使用ContractExecutionState作为唯一数据模型
        private readonly Dictionary<string, ContractExecutionState> _contractStates = new();
        private readonly List<ExecutionHistory> _executionHistory = new();
        
        // 🔒 线程安全锁
        private readonly object _lock = new();
        
        // 📊 统计信息
        private int _totalOperations = 0;
        private DateTime _lastSyncTime = DateTime.Now;
        
        // 🔄 向后兼容：维护旧格式的状态映射
        private readonly Dictionary<string, PositionProfile> _legacyProfiles = new();

        /// <summary>
        /// 状态变更事件
        /// </summary>
        public event EventHandler<StateChangedEventArgs>? StateChanged;

        public UnifiedStateManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // 为持久化服务创建专用的logger
            var persistenceLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<AutoMonitorPersistenceService>();
            _persistenceService = new AutoMonitorPersistenceService(persistenceLogger);
            
            _logger.LogInformation("🔄 统一状态管理器已初始化");
        }

        /// <summary>
        /// 设置事件总线
        /// </summary>
        /// <param name="eventBus">事件总线实例</param>
        public void SetEventBus(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _logger.LogInformation("🚌 事件总线已设置到统一状态管理器");
        }

        /// <summary>
        /// 初始化：从持久化存储加载状态
        /// </summary>
        public void Initialize()
        {
            lock (_lock)
            {
                try
                {
                    // 1. 加载持久化的PositionProfile数据
                    var persistedProfiles = _persistenceService.LoadPositionProfiles();
                    var persistedHistory = _persistenceService.LoadExecutionHistory();
                    
                    _logger.LogInformation($"📖 从持久化存储加载: {persistedProfiles.Count}个档案, {persistedHistory.Count}条历史");
                    
                    // 2. 转换为统一的ContractExecutionState格式
                    foreach (var kvp in persistedProfiles)
                    {
                        var profile = kvp.Value;
                        var contractState = ConvertFromPositionProfile(profile);
                        _contractStates[kvp.Key] = contractState;
                        
                        // 向后兼容：保留旧格式
                        _legacyProfiles[kvp.Key] = profile;
                    }
                    
                    // 3. 加载执行历史
                    _executionHistory.AddRange(persistedHistory);
                    
                    _lastSyncTime = DateTime.Now;
                    _logger.LogInformation($"✅ 状态初始化完成: {_contractStates.Count}个合约状态已加载");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 状态初始化失败，将从空状态开始");
                }
            }
        }

        /// <summary>
        /// 检查指定操作是否已执行
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="positionSide">持仓方向</param>
        /// <param name="executionType">执行类型</param>
        /// <param name="tierIndex">阶梯索引（可选）</param>
        /// <returns>是否已执行</returns>
        public bool IsExecuted(string symbol, string positionSide, ExecutionType executionType, int? tierIndex = null)
        {
            lock (_lock)
            {
                var contractKey = GetContractKey(symbol, positionSide);
                
                if (!_contractStates.TryGetValue(contractKey, out var state))
                {
                    return false; // 没有状态记录，表示未执行
                }
                
                var result = state.IsTriggered(executionType, tierIndex);
                
                _logger.LogDebug($"🔍 状态检查: {contractKey} {executionType}" + 
                    (tierIndex.HasValue ? $"_T{tierIndex}" : "") + $" = {result}");
                
                return result;
            }
        }

        /// <summary>
        /// 记录操作执行状态
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="positionSide">持仓方向</param>
        /// <param name="executionType">执行类型</param>
        /// <param name="tierIndex">阶梯索引（可选）</param>
        /// <param name="triggerPnl">触发时浮盈</param>
        /// <param name="isSuccess">是否成功</param>
        /// <param name="message">执行消息</param>
        /// <param name="autoSave">是否自动保存到持久化存储</param>
        public void RecordExecution(string symbol, string positionSide, ExecutionType executionType, 
            int? tierIndex, decimal triggerPnl, bool isSuccess, string message = "", bool autoSave = true)
        {
            lock (_lock)
            {
                try
                {
                    var contractKey = GetContractKey(symbol, positionSide);
                    
                    // 1. 获取或创建合约状态
                    if (!_contractStates.TryGetValue(contractKey, out var state))
                    {
                        state = new ContractExecutionState
                        {
                            ContractKey = contractKey,
                            Symbol = symbol,
                            PositionSide = positionSide
                        };
                        _contractStates[contractKey] = state;
                    }
                    
                    // 2. 记录执行状态
                    state.MarkAsTriggered(executionType, tierIndex, triggerPnl, isSuccess, message);
                    
                    // 3. 更新向后兼容的PositionProfile格式
                    UpdateLegacyProfile(contractKey, symbol, positionSide, executionType, tierIndex, triggerPnl, isSuccess);
                    
                    // 4. 记录执行历史
                    var executionHistory = new ExecutionHistory
                    {
                        Symbol = symbol,
                        PositionSide = positionSide,
                        ExecutionType = GetExecutionTypeName(executionType, tierIndex),
                        ExecutionTime = DateTime.Now,
                        TriggerPnl = triggerPnl,
                        IsSuccess = isSuccess,
                        Details = message
                    };
                    _executionHistory.Add(executionHistory);
                    
                    // 5. 触发状态变更事件
                    StateChanged?.Invoke(this, new StateChangedEventArgs 
                    { 
                        ContractKey = contractKey,
                        ExecutionType = executionType,
                        TierIndex = tierIndex,
                        IsSuccess = isSuccess
                    });
                    
                    // 🚌 6. 发布执行状态变更事件到事件总线
                    if (_eventBus != null)
                    {
                        var executionEvent = new ExecutionStateChangedEvent
                        {
                            Source = "UnifiedStateManager",
                            ContractKey = contractKey,
                            Symbol = symbol,
                            PositionSide = positionSide,
                            ExecutionType = executionType,
                            TierIndex = tierIndex,
                            TriggerPnl = triggerPnl,
                            IsSuccess = isSuccess,
                            Message = message,
                            Priority = isSuccess ? EventPriority.Normal : EventPriority.High
                        };
                        
                        // 异步发布事件，不阻塞主线程
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _eventBus.PublishAsync(executionEvent);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"❌ 发布执行状态变更事件失败: {contractKey}");
                            }
                        });
                    }
                    
                    _totalOperations++;
                    
                    var tierText = tierIndex.HasValue ? $"阶梯{tierIndex}" : "";
                    _logger.LogInformation($"📝 记录执行状态: {contractKey} {executionType}{tierText} - {(isSuccess ? "成功" : "失败")}");
                    
                    // 7. 自动保存到持久化存储
                    if (autoSave)
                    {
                        SaveToPersistence();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 记录执行状态失败: {symbol}_{positionSide} {executionType}");
                }
            }
        }

        /// <summary>
        /// 清理指定合约的所有状态
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="positionSide">持仓方向（可选，为空则清理该合约所有方向）</param>
        /// <param name="reason">清理原因</param>
        public void ClearContractStates(string symbol, string? positionSide = null, string reason = "手动清理")
        {
            lock (_lock)
            {
                try
                {
                    var keysToRemove = new List<string>();
                    var pattern = positionSide != null ? $"{symbol}_{positionSide}" : $"{symbol}_";
                    
                    // 1. 找出需要清理的合约键
                    foreach (var key in _contractStates.Keys)
                    {
                        if (key.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            keysToRemove.Add(key);
                        }
                    }
                    
                    // 2. 清理内存状态
                    var totalCleared = 0;
                    foreach (var key in keysToRemove)
                    {
                        if (_contractStates.TryGetValue(key, out var state))
                        {
                            var stats = state.GetExecutionStats();
                            totalCleared += stats.TotalExecutions;
                        }
                        
                        _contractStates.Remove(key);
                        _legacyProfiles.Remove(key);
                    }
                    
                    // 3. 清理执行历史
                    var historicalRecords = positionSide != null 
                        ? _executionHistory.Where(h => h.Symbol == symbol && h.PositionSide == positionSide).ToList()
                        : _executionHistory.Where(h => h.Symbol == symbol).ToList();
                    
                    foreach (var record in historicalRecords)
                    {
                        _executionHistory.Remove(record);
                    }
                    
                    // 4. 记录清理历史
                    var cleanupHistory = new ExecutionHistory
                    {
                        Symbol = symbol,
                        PositionSide = positionSide ?? "ALL",
                        ExecutionType = "状态清理",
                        ExecutionTime = DateTime.Now,
                        TriggerPnl = 0,
                        IsSuccess = true,
                        Details = $"清理原因: {reason}, 清理{totalCleared}个执行记录, {historicalRecords.Count}条历史记录"
                    };
                    _executionHistory.Add(cleanupHistory);
                    
                    // 5. 同步到持久化存储
                    SaveToPersistence();
                    
                    var targetText = positionSide != null ? $"{symbol}_{positionSide}" : $"{symbol}_*";
                    _logger.LogInformation($"🧹 状态清理完成: {targetText} - 清理{keysToRemove.Count}个合约状态, {historicalRecords.Count}条历史");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 清理合约状态失败: {symbol}_{positionSide}");
                }
            }
        }

        /// <summary>
        /// 获取合约执行统计信息
        /// </summary>
        /// <param name="symbol">合约名称（可选）</param>
        /// <param name="positionSide">持仓方向（可选）</param>
        /// <returns>统计信息列表</returns>
        public List<ContractExecutionStats> GetExecutionStats(string? symbol = null, string? positionSide = null)
        {
            lock (_lock)
            {
                var stats = new List<ContractExecutionStats>();
                
                foreach (var state in _contractStates.Values)
                {
                    // 过滤条件
                    if (symbol != null && !state.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (positionSide != null && !state.PositionSide.Equals(positionSide, StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    stats.Add(state.GetExecutionStats());
                }
                
                return stats.OrderBy(s => s.Symbol).ThenBy(s => s.PositionSide).ToList();
            }
        }

        /// <summary>
        /// 获取执行历史
        /// </summary>
        /// <param name="maxCount">最大返回数量</param>
        /// <param name="symbol">合约名称过滤（可选）</param>
        /// <returns>执行历史列表</returns>
        public List<ExecutionHistory> GetExecutionHistory(int maxCount = 100, string? symbol = null)
        {
            lock (_lock)
            {
                var query = _executionHistory.AsEnumerable();
                
                if (!string.IsNullOrEmpty(symbol))
                {
                    query = query.Where(h => h.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
                }
                
                return query
                    .OrderByDescending(h => h.ExecutionTime)
                    .Take(maxCount)
                    .ToList();
            }
        }

        /// <summary>
        /// 向后兼容：获取PositionProfile格式的数据
        /// </summary>
        /// <returns>PositionProfile字典</returns>
        public Dictionary<string, PositionProfile> GetLegacyProfiles()
        {
            lock (_lock)
            {
                return new Dictionary<string, PositionProfile>(_legacyProfiles);
            }
        }

        /// <summary>
        /// 强制同步到持久化存储
        /// </summary>
        public void SaveToPersistence()
        {
            try
            {
                _persistenceService.SavePositionProfiles(_legacyProfiles);
                _persistenceService.SaveExecutionHistory(_executionHistory);
                _lastSyncTime = DateTime.Now;
                
                _logger.LogDebug($"💾 状态已同步到持久化存储: {_contractStates.Count}个合约, {_executionHistory.Count}条历史");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 同步到持久化存储失败");
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        /// <returns>状态管理器统计</returns>
        public UnifiedStateStats GetStatistics()
        {
            lock (_lock)
            {
                return new UnifiedStateStats
                {
                    TotalContracts = _contractStates.Count,
                    TotalOperations = _totalOperations,
                    TotalHistoryRecords = _executionHistory.Count,
                    LastSyncTime = _lastSyncTime,
                    ActiveContracts = _contractStates.Values.Count(s => s.IsActive),
                    MemoryUsageKB = GC.GetTotalMemory(false) / 1024
                };
            }
        }

        #region 私有辅助方法

        /// <summary>
        /// 获取合约唯一标识
        /// </summary>
        private static string GetContractKey(string symbol, string positionSide) => $"{symbol}_{positionSide}";

        /// <summary>
        /// 从PositionProfile转换为ContractExecutionState
        /// </summary>
        private ContractExecutionState ConvertFromPositionProfile(PositionProfile profile)
        {
            var state = new ContractExecutionState
            {
                ContractKey = $"{profile.Symbol}_{profile.PositionSide}",
                Symbol = profile.Symbol,
                PositionSide = profile.PositionSide,
                CreateTime = profile.CreateTime,
                LastUpdateTime = profile.LastUpdateTime,
                IsActive = profile.IsActive
            };

            // 转换触发记录
            foreach (var kvp in profile.TriggerRecords)
            {
                var triggerRecord = kvp.Value;
                var executionType = ParseExecutionType(triggerRecord.TriggerType);
                
                if (executionType.HasValue)
                {
                    state.MarkAsTriggered(executionType.Value, triggerRecord.TierIndex, 
                        triggerRecord.TriggerPnl, triggerRecord.IsExecuted, triggerRecord.ExecutionResult);
                }
            }

            return state;
        }

        /// <summary>
        /// 更新向后兼容的PositionProfile
        /// </summary>
        private void UpdateLegacyProfile(string contractKey, string symbol, string positionSide, 
            ExecutionType executionType, int? tierIndex, decimal triggerPnl, bool isSuccess)
        {
            if (!_legacyProfiles.TryGetValue(contractKey, out var profile))
            {
                profile = new PositionProfile
                {
                    Symbol = symbol,
                    PositionSide = positionSide,
                    CreateTime = DateTime.Now,
                    IsActive = true
                };
                _legacyProfiles[contractKey] = profile;
            }

            profile.LastUpdateTime = DateTime.Now;
            
            var triggerKey = GetLegacyTriggerKey(executionType, tierIndex);
            profile.TriggerRecords[triggerKey] = new TriggerRecord
            {
                ArchiveId = profile.ArchiveId,
                TriggerType = GetExecutionTypeName(executionType, tierIndex),
                TierIndex = tierIndex,
                TriggerPnl = triggerPnl,
                TriggerTime = DateTime.Now,
                IsExecuted = true,
                ExecutionResult = isSuccess ? "成功" : "失败"
            };
        }

        /// <summary>
        /// 解析执行类型
        /// </summary>
        private ExecutionType? ParseExecutionType(string triggerType)
        {
            if (triggerType.Contains("保本") || triggerType.Contains("BreakEven"))
                return ExecutionType.BreakEven;
            if (triggerType.Contains("推仓") || triggerType.Contains("AddPosition"))
                return ExecutionType.AddPosition;
            if (triggerType.Contains("保盈") || triggerType.Contains("ProfitProtection"))
                return ExecutionType.ProfitProtection;
            
            return null;
        }

        /// <summary>
        /// 获取执行类型名称
        /// </summary>
        private string GetExecutionTypeName(ExecutionType executionType, int? tierIndex)
        {
            var baseName = executionType switch
            {
                ExecutionType.BreakEven => "自动保本",
                ExecutionType.AddPosition => "推仓",
                ExecutionType.ProfitProtection => "保盈止损",
                _ => executionType.ToString()
            };

            return tierIndex.HasValue ? $"{baseName}阶梯{tierIndex}" : baseName;
        }

        /// <summary>
        /// 获取旧格式的触发键
        /// </summary>
        private string GetLegacyTriggerKey(ExecutionType executionType, int? tierIndex)
        {
            var baseKey = executionType switch
            {
                ExecutionType.BreakEven => "BreakEven",
                ExecutionType.AddPosition => "AddPosition",
                ExecutionType.ProfitProtection => "ProfitProtection",
                _ => executionType.ToString()
            };

            return tierIndex.HasValue ? $"{baseKey}_Stage{tierIndex}" : baseKey;
        }

        #endregion

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                // 最后一次保存
                SaveToPersistence();
                
                _contractStates.Clear();
                _executionHistory.Clear();
                _legacyProfiles.Clear();
            }
            
            _logger.LogInformation("🔄 统一状态管理器已释放资源");
        }
    }

    /// <summary>
    /// 状态变更事件参数
    /// </summary>
    public class StateChangedEventArgs : EventArgs
    {
        public string ContractKey { get; set; } = string.Empty;
        public ExecutionType ExecutionType { get; set; }
        public int? TierIndex { get; set; }
        public bool IsSuccess { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 统一状态管理器统计信息
    /// </summary>
    public class UnifiedStateStats
    {
        public int TotalContracts { get; set; }
        public int ActiveContracts { get; set; }
        public int TotalOperations { get; set; }
        public int TotalHistoryRecords { get; set; }
        public DateTime LastSyncTime { get; set; }
        public long MemoryUsageKB { get; set; }
        
        /// <summary>
        /// 活跃合约比例
        /// </summary>
        public double ActiveContractRatio => TotalContracts > 0 ? (double)ActiveContracts / TotalContracts * 100 : 0;
    }
} 