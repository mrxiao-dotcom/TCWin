using System;
using System.Collections.Generic;
using System.Linq;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 简化状态管理器 - 只使用PositionProfile作为唯一状态模型
    /// </summary>
    public class SimpleStateManager
    {
        private readonly ILogger<SimpleStateManager> _logger;
        private readonly UnifiedPersistenceService _persistenceService;
        
        // 🎯 简化的状态管理：只使用PositionProfile
        private readonly Dictionary<string, PositionProfile> _positionProfiles = new();
        private readonly List<ExecutionHistory> _executionHistory = new();
        
        // 简化的冷却期管理
        private readonly Dictionary<string, DateTime> _lastExecutionTimes = new();
        private readonly TimeSpan _cooldownPeriod = TimeSpan.FromMinutes(5);
        
        private readonly object _lock = new();

        public SimpleStateManager(ILogger<SimpleStateManager> logger, UnifiedPersistenceService persistenceService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        }

        /// <summary>
        /// 初始化状态管理器
        /// </summary>
        public void Initialize()
        {
            lock (_lock)
            {
                try
                {
                    // 从持久化存储加载数据
                    var profiles = _persistenceService.LoadPositionProfiles();
                    var history = _persistenceService.LoadExecutionHistory();
                    
                    _positionProfiles.Clear();
                    _executionHistory.Clear();
                    
                    foreach (var kvp in profiles)
                    {
                        _positionProfiles[kvp.Key] = kvp.Value;
                    }
                    
                    _executionHistory.AddRange(history);
                    
                    _logger.LogInformation($"✅ 状态管理器初始化完成: {profiles.Count}个档案, {history.Count}条历史");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 状态管理器初始化失败");
                }
            }
        }

        /// <summary>
        /// 获取持仓档案
        /// </summary>
        public PositionProfile? GetPositionProfile(string symbol, string positionSide)
        {
            var key = GetPositionKey(symbol, positionSide);
            lock (_lock)
            {
                return _positionProfiles.TryGetValue(key, out var profile) ? profile : null;
            }
        }

        /// <summary>
        /// 设置或更新持仓档案
        /// </summary>
        public void SetPositionProfile(string symbol, string positionSide, PositionProfile profile)
        {
            var key = GetPositionKey(symbol, positionSide);
            lock (_lock)
            {
                _positionProfiles[key] = profile;
            }
        }

        /// <summary>
        /// 检查操作是否已执行
        /// </summary>
        public bool IsOperationExecuted(string symbol, string positionSide, string operationType, int? tierIndex = null)
        {
            var profile = GetPositionProfile(symbol, positionSide);
            if (profile == null) return false;

            var triggerKey = BuildTriggerKey(symbol, positionSide, operationType, tierIndex);
            return profile.TriggerRecords.ContainsKey(triggerKey);
        }

        /// <summary>
        /// 记录操作执行
        /// </summary>
        public void RecordOperationExecution(string symbol, string positionSide, string operationType, 
            decimal triggerPnl, bool success, string message, int? tierIndex = null)
        {
            var key = GetPositionKey(symbol, positionSide);
            var triggerKey = BuildTriggerKey(symbol, positionSide, operationType, tierIndex);

            lock (_lock)
            {
                // 获取或创建档案
                if (!_positionProfiles.TryGetValue(key, out var profile))
                {
                    profile = new PositionProfile
                    {
                        Symbol = symbol,
                        PositionSide = positionSide,
                        LastUpdateTime = DateTime.Now,
                        IsActive = true,
                        TriggerRecords = new Dictionary<string, TriggerRecord>()
                    };
                    _positionProfiles[key] = profile;
                }

                // 记录触发记录
                var record = new TriggerRecord
                {
                    TriggerType = operationType,
                    TriggerTime = DateTime.Now,
                    TriggerPnl = triggerPnl,
                    IsExecuted = success
                };

                profile.TriggerRecords[triggerKey] = record;
                profile.LastUpdateTime = DateTime.Now;

                // 记录执行历史
                var history = new ExecutionHistory
                {
                    Symbol = symbol,
                    PositionSide = positionSide,
                    ExecutionType = operationType,
                    ExecutionTime = DateTime.Now
                };

                _executionHistory.Add(history);

                // 记录冷却期
                _lastExecutionTimes[triggerKey] = DateTime.Now;
            }

            _logger.LogInformation($"📝 记录操作执行: {symbol} {operationType} - {(success ? "成功" : "失败")}");
        }

        /// <summary>
        /// 检查是否在冷却期内
        /// </summary>
        public bool IsInCooldown(string symbol, string positionSide, string operationType, int? tierIndex = null)
        {
            var triggerKey = BuildTriggerKey(symbol, positionSide, operationType, tierIndex);
            
            lock (_lock)
            {
                if (!_lastExecutionTimes.TryGetValue(triggerKey, out var lastTime))
                    return false;
                
                return DateTime.Now - lastTime < _cooldownPeriod;
            }
        }

        /// <summary>
        /// 获取所有持仓档案
        /// </summary>
        public Dictionary<string, PositionProfile> GetAllPositionProfiles()
        {
            lock (_lock)
            {
                return new Dictionary<string, PositionProfile>(_positionProfiles);
            }
        }

        /// <summary>
        /// 获取执行历史
        /// </summary>
        public List<ExecutionHistory> GetExecutionHistory(int maxCount = 100)
        {
            lock (_lock)
            {
                return _executionHistory
                    .OrderByDescending(h => h.ExecutionTime)
                    .Take(maxCount)
                    .ToList();
            }
        }

        /// <summary>
        /// 清理过期数据
        /// </summary>
        public void CleanupExpiredData()
        {
            lock (_lock)
            {
                // 清理过期的冷却期记录
                var expiredKeys = _lastExecutionTimes
                    .Where(kvp => DateTime.Now - kvp.Value > _cooldownPeriod.Add(TimeSpan.FromHours(1)))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _lastExecutionTimes.Remove(key);
                }

                // 清理过期的执行历史（保留最近1000条）
                if (_executionHistory.Count > 1000)
                {
                    var toKeep = _executionHistory
                        .OrderByDescending(h => h.ExecutionTime)
                        .Take(1000)
                        .ToList();
                    
                    _executionHistory.Clear();
                    _executionHistory.AddRange(toKeep);
                }

                if (expiredKeys.Any() || _executionHistory.Count > 1000)
                {
                    _logger.LogInformation($"🧹 清理过期数据: {expiredKeys.Count}个冷却期记录");
                }
            }
        }

        /// <summary>
        /// 保存状态到持久化存储
        /// </summary>
        public void SaveToPersistence()
        {
            lock (_lock)
            {
                try
                {
                    _persistenceService.SavePositionProfiles(_positionProfiles);
                    _persistenceService.SaveExecutionHistory(_executionHistory);
                    _logger.LogDebug("💾 状态已保存到持久化存储");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 保存状态到持久化存储失败");
                }
            }
        }

        /// <summary>
        /// 移除持仓档案
        /// </summary>
        public void RemovePositionProfile(string symbol, string positionSide)
        {
            var key = GetPositionKey(symbol, positionSide);
            lock (_lock)
            {
                if (_positionProfiles.Remove(key))
                {
                    _logger.LogInformation($"🗑️ 移除持仓档案: {key}");
                }
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public UnifiedStateStats GetStatistics()
        {
            try
            {
                return new UnifiedStateStats
                {
                    TotalContracts = _positionProfiles.Count,
                    ActiveContracts = _positionProfiles.Values.Count(p => p.IsActive),
                    TotalOperations = _executionHistory.Count,
                    TotalHistoryRecords = _executionHistory.Count,
                    LastSyncTime = DateTime.Now,
                    MemoryUsageKB = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取统计信息失败");
                return new UnifiedStateStats();
            }
        }

        /// <summary>
        /// 获取执行统计信息
        /// </summary>
        public List<ContractExecutionStats> GetExecutionStats(string symbol, string positionSide)
        {
            try
            {
                var key = $"{symbol}_{positionSide}";
                var contractExecutions = _executionHistory.Where(h => 
                    h.Symbol == symbol && h.PositionSide == positionSide).ToList();
                    
                var stats = new ContractExecutionStats
                {
                    Symbol = symbol,
                    PositionSide = positionSide,
                    ContractKey = $"{symbol}_{positionSide}",
                    TotalExecutions = contractExecutions.Count,
                    BreakEvenExecuted = contractExecutions.Any(h => h.IsSuccess),
                    AddPositionTiersExecuted = 0,
                    ProfitProtectionTiersExecuted = 0
                };
                
                return new List<ContractExecutionStats> { stats };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取执行统计信息失败");
                return new List<ContractExecutionStats>();
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            // 清理资源
        }

        /// <summary>
        /// 记录执行
        /// </summary>
        public void RecordExecution(string symbol, string positionSide, ExecutionType executionType, int tierIndex, bool success = true, string remark = "", bool autoSave = true)
        {
            try
            {
                var key = $"{symbol}_{positionSide}";
                _lastExecutionTimes[key] = DateTime.Now;
                
                var history = new ExecutionHistory
                {
                    Symbol = symbol,
                    PositionSide = positionSide,
                    ExecutionType = executionType.ToString(),
                    ExecutionTime = DateTime.Now,
                    IsSuccess = success,
                    Details = $"{executionType} 第{tierIndex}阶",
                    ResultMessage = remark
                };
                
                _executionHistory.Add(history);
                _logger.LogInformation($"📝 记录执行: {symbol}_{positionSide} {executionType} 第{tierIndex}阶 - {(success ? "成功" : "失败")}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "记录执行失败");
            }
        }

        /// <summary>
        /// 记录执行 - 重载版本，接受更复杂的参数
        /// </summary>
        public void RecordExecution(string symbol, string positionSide, ExecutionType executionType, 
            int tierIndex, decimal triggerPnl, bool success, string message, bool autoSave = true)
        {
            try
            {
                var key = $"{symbol}_{positionSide}";
                _lastExecutionTimes[key] = DateTime.Now;
                
                var history = new ExecutionHistory
                {
                    Symbol = symbol,
                    PositionSide = positionSide,
                    ExecutionType = executionType.ToString(),
                    ExecutionTime = DateTime.Now,
                    IsSuccess = success,
                    Details = $"{executionType} 第{tierIndex}阶 触发价:{triggerPnl:F2}",
                    ResultMessage = message
                };
                
                _executionHistory.Add(history);
                _logger.LogInformation($"📝 记录执行: {symbol}_{positionSide} {executionType} 第{tierIndex}阶 - {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "记录执行失败");
            }
        }

        /// <summary>
        /// 检查是否已执行
        /// </summary>
        public bool IsExecuted(string symbol, string positionSide, ExecutionType executionType, int tierIndex)
        {
            try
            {
                var key = $"{symbol}_{positionSide}";
                if (_positionProfiles.TryGetValue(key, out var profile))
                {
                    var triggerKey = $"{executionType}_{tierIndex}";
                    return profile.TriggerRecords.ContainsKey(triggerKey) && 
                           profile.TriggerRecords[triggerKey].IsExecuted;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查执行状态失败");
                return false;
            }
        }

        /// <summary>
        /// 检查是否已执行 - 重载版本，不需要tierIndex
        /// </summary>
        public bool IsExecuted(string symbol, string positionSide, ExecutionType executionType)
        {
            return IsExecuted(symbol, positionSide, executionType, 0);
        }

        /// <summary>
        /// 检查是否可以执行
        /// </summary>
        public bool CanExecute(string symbol, string positionSide, ExecutionType executionType, int tierIndex)
        {
            try
            {
                var key = $"{symbol}_{positionSide}";
                if (_lastExecutionTimes.TryGetValue(key, out var lastTime))
                {
                    var elapsed = DateTime.Now - lastTime;
                    if (elapsed < _cooldownPeriod)
                    {
                        return false;
                    }
                }
                return !IsExecuted(symbol, positionSide, executionType, tierIndex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查执行条件失败");
                return false;
            }
        }

        /// <summary>
        /// 获取剩余冷却时间
        /// </summary>
        public TimeSpan GetRemainingCooldown(string symbol, string positionSide)
        {
            try
            {
                var key = $"{symbol}_{positionSide}";
                if (_lastExecutionTimes.TryGetValue(key, out var lastTime))
                {
                    var elapsed = DateTime.Now - lastTime;
                    var remaining = _cooldownPeriod - elapsed;
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }
                return TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取冷却时间失败");
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// 标记为执行中
        /// </summary>
        public void MarkAsExecuting(string symbol, string positionSide, ExecutionType executionType, int tierIndex)
        {
            try
            {
                var key = $"{symbol}_{positionSide}";
                _lastExecutionTimes[key] = DateTime.Now;
                _logger.LogInformation($"🔄 标记执行中: {symbol}_{positionSide} {executionType} 第{tierIndex}阶");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记执行状态失败");
            }
        }

        /// <summary>
        /// 标记为执行中 - 重载版本，接受更多参数
        /// </summary>
        public void MarkAsExecuting(string symbol, string positionSide, ExecutionType executionType, 
            int? tierIndex, decimal currentPnl, string message)
        {
            try
            {
                var key = $"{symbol}_{positionSide}";
                _lastExecutionTimes[key] = DateTime.Now;
                _logger.LogInformation($"🔄 标记执行中: {symbol}_{positionSide} {executionType} 第{tierIndex}阶 - {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记执行状态失败");
            }
        }

        /// <summary>
        /// 获取执行历史
        /// </summary>
        public List<ExecutionHistory> GetExecutionHistory(string symbol = "", ExecutionType? executionType = null)
        {
            try
            {
                return _executionHistory
                    .Where(h => string.IsNullOrEmpty(symbol) || h.Symbol == symbol)
                    .Where(h => !executionType.HasValue || h.ExecutionType == executionType.Value.ToString())
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取执行历史失败");
                return new List<ExecutionHistory>();
            }
        }

        /// <summary>
        /// 清除合约状态
        /// </summary>
        public void ClearContractStates(string symbol)
        {
            try
            {
                var keysToRemove = _positionProfiles.Keys
                    .Where(k => k.StartsWith($"{symbol}_"))
                    .ToList();
                    
                foreach (var key in keysToRemove)
                {
                    _positionProfiles.Remove(key);
                }
                
                _logger.LogInformation($"🗑️ 清除合约状态: {symbol} ({keysToRemove.Count}个配置)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清除合约状态失败");
            }
        }

        /// <summary>
        /// 清除合约状态 - 重载版本，接受更多参数
        /// </summary>
        public void ClearContractStates(string symbol, string positionSide, string reason)
        {
            try
            {
                var key = $"{symbol}_{positionSide}";
                if (_positionProfiles.Remove(key))
                {
                    _logger.LogInformation($"🗑️ 清除合约状态: {symbol}_{positionSide} - {reason}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清除合约状态失败");
            }
        }

        #region 私有方法

        /// <summary>
        /// 获取持仓唯一标识
        /// </summary>
        private static string GetPositionKey(string symbol, string positionSide) => $"{symbol}_{positionSide}";

        /// <summary>
        /// 构建触发器键
        /// </summary>
        private static string BuildTriggerKey(string symbol, string positionSide, string operationType, int? tierIndex)
        {
            var key = $"{symbol}_{positionSide}_{operationType}";
            if (tierIndex.HasValue)
            {
                key += $"_{tierIndex}";
            }
            return key;
        }

        #endregion
    }

    /// <summary>
    /// 状态统计信息
    /// </summary>
    public class StateStatistics
    {
        public int TotalProfiles { get; set; }
        public int TotalExecutions { get; set; }
        public int ActiveCooldowns { get; set; }
    }
} 