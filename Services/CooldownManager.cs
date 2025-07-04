using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 冷却期管理器 - 防止短时间内重复执行相同操作
    /// </summary>
    public class CooldownManager : IDisposable
    {
        private readonly ILogger _logger;
        
        // 🔒 操作执行时间记录：操作键 -> 最后执行时间
        private readonly Dictionary<string, DateTime> _lastExecutionTimes = new();
        
        // 🔒 线程安全锁
        private readonly object _lock = new();
        
        // ⏰ 预定义的冷却期配置 - 缩短冷却期，主要依赖状态管理防重复
        private readonly Dictionary<CooldownOperationType, TimeSpan> _cooldownPeriods = new()
        {
            { CooldownOperationType.BreakEven, TimeSpan.FromSeconds(5) },       // 🔧 修改：保本止损5秒
            { CooldownOperationType.AddPosition, TimeSpan.FromSeconds(5) },     // 🔧 修改：推仓操作5秒
            { CooldownOperationType.ProfitProtection, TimeSpan.FromSeconds(5) }, // 🔧 修改：保盈止损5秒
            { CooldownOperationType.ManualOrder, TimeSpan.FromSeconds(10) },    // 手动下单：10秒
            { CooldownOperationType.StopLoss, TimeSpan.FromSeconds(10) }        // 🔧 修改：止损单创建10秒
        };
        
        // 📊 统计信息
        public CooldownStats Statistics { get; private set; } = new();

        public CooldownManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("🛡️ 冷却期管理器已初始化");
        }

        /// <summary>
        /// 检查指定操作是否可以执行（是否已过冷却期）
        /// </summary>
        /// <param name="operationKey">操作唯一标识</param>
        /// <param name="operationType">操作类型</param>
        /// <returns>是否可以执行</returns>
        public bool CanExecute(string operationKey, CooldownOperationType operationType)
        {
            if (string.IsNullOrEmpty(operationKey))
            {
                _logger.LogWarning("⚠️ 冷却期检查失败：操作键为空");
                return false;
            }

            var cooldownPeriod = _cooldownPeriods[operationType];
            return CanExecute(operationKey, cooldownPeriod);
        }

        /// <summary>
        /// 检查指定操作是否可以执行（自定义冷却期）
        /// </summary>
        /// <param name="operationKey">操作唯一标识</param>
        /// <param name="cooldownPeriod">自定义冷却期</param>
        /// <returns>是否可以执行</returns>
        public bool CanExecute(string operationKey, TimeSpan cooldownPeriod)
        {
            if (string.IsNullOrEmpty(operationKey))
            {
                return false;
            }

            lock (_lock)
            {
                if (!_lastExecutionTimes.TryGetValue(operationKey, out var lastExecutionTime))
                {
                    // 第一次执行此操作
                    return true;
                }

                var timeSinceLastExecution = DateTime.Now - lastExecutionTime;
                var canExecute = timeSinceLastExecution >= cooldownPeriod;

                if (!canExecute)
                {
                    var remainingTime = cooldownPeriod - timeSinceLastExecution;
                    _logger.LogDebug($"🔒 操作 {operationKey} 仍在冷却期，剩余: {remainingTime.TotalSeconds:F1}秒");
                    Statistics.CooldownBlocks++;
                }

                return canExecute;
            }
        }

        /// <summary>
        /// 记录操作执行，开始冷却期
        /// </summary>
        /// <param name="operationKey">操作唯一标识</param>
        public void RecordExecution(string operationKey)
        {
            if (string.IsNullOrEmpty(operationKey))
            {
                _logger.LogWarning("⚠️ 记录执行失败：操作键为空");
                return;
            }

            lock (_lock)
            {
                var now = DateTime.Now;
                var isFirstTime = !_lastExecutionTimes.ContainsKey(operationKey);
                
                _lastExecutionTimes[operationKey] = now;
                
                if (isFirstTime)
                {
                    _logger.LogDebug($"🆕 首次记录操作: {operationKey}");
                }
                else
                {
                    _logger.LogDebug($"🔄 更新操作时间: {operationKey}");
                }
                
                Statistics.TotalExecutions++;
                Statistics.LastExecutionTime = now;
            }
        }

        /// <summary>
        /// 获取操作的剩余冷却时间
        /// </summary>
        /// <param name="operationKey">操作唯一标识</param>
        /// <param name="operationType">操作类型</param>
        /// <returns>剩余冷却时间，如果可以执行则返回TimeSpan.Zero</returns>
        public TimeSpan GetRemainingCooldown(string operationKey, CooldownOperationType operationType)
        {
            var cooldownPeriod = _cooldownPeriods[operationType];
            return GetRemainingCooldown(operationKey, cooldownPeriod);
        }

        /// <summary>
        /// 获取操作的剩余冷却时间（自定义冷却期）
        /// </summary>
        /// <param name="operationKey">操作唯一标识</param>
        /// <param name="cooldownPeriod">冷却期</param>
        /// <returns>剩余冷却时间</returns>
        public TimeSpan GetRemainingCooldown(string operationKey, TimeSpan cooldownPeriod)
        {
            if (string.IsNullOrEmpty(operationKey))
            {
                return TimeSpan.Zero;
            }

            lock (_lock)
            {
                if (!_lastExecutionTimes.TryGetValue(operationKey, out var lastExecutionTime))
                {
                    return TimeSpan.Zero; // 从未执行过
                }

                var timeSinceLastExecution = DateTime.Now - lastExecutionTime;
                var remainingTime = cooldownPeriod - timeSinceLastExecution;

                return remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// 生成操作唯一标识键
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="positionSide">持仓方向</param>
        /// <param name="operationType">操作类型</param>
        /// <param name="tierIndex">阶梯索引（可选）</param>
        /// <returns>操作键</returns>
        public static string GenerateOperationKey(string symbol, string positionSide, 
            CooldownOperationType operationType, int? tierIndex = null)
        {
            var key = $"{symbol}_{positionSide}_{operationType}";
            if (tierIndex.HasValue)
            {
                key += $"_T{tierIndex.Value}";
            }
            return key;
        }

        /// <summary>
        /// 清理指定合约的所有冷却期记录
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="positionSide">持仓方向（可选，为空则清理该合约所有方向）</param>
        public void ClearContractCooldowns(string symbol, string? positionSide = null)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return;
            }

            lock (_lock)
            {
                var keysToRemove = new List<string>();
                var pattern = positionSide != null ? $"{symbol}_{positionSide}_" : $"{symbol}_";

                foreach (var key in _lastExecutionTimes.Keys)
                {
                    if (key.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _lastExecutionTimes.Remove(key);
                }

                if (keysToRemove.Any())
                {
                    _logger.LogInformation($"🧹 已清理 {keysToRemove.Count} 个冷却期记录: {symbol}" + 
                        (positionSide != null ? $"_{positionSide}" : ""));
                    Statistics.RecordsCleaned += keysToRemove.Count;
                }
            }
        }

        /// <summary>
        /// 清理过期的冷却期记录（超过24小时未使用）
        /// </summary>
        public void CleanupExpiredRecords()
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.Now.AddHours(-24);
                var keysToRemove = _lastExecutionTimes
                    .Where(kvp => kvp.Value < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _lastExecutionTimes.Remove(key);
                }

                if (keysToRemove.Any())
                {
                    _logger.LogInformation($"🧹 清理了 {keysToRemove.Count} 个过期冷却期记录");
                    Statistics.RecordsCleaned += keysToRemove.Count;
                }
            }
        }

        /// <summary>
        /// 获取所有活跃的冷却期信息
        /// </summary>
        /// <returns>活跃冷却期列表</returns>
        public List<ActiveCooldownInfo> GetActiveCooldowns()
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                var activeCooldowns = new List<ActiveCooldownInfo>();

                foreach (var kvp in _lastExecutionTimes)
                {
                    var operationKey = kvp.Key;
                    var lastExecutionTime = kvp.Value;
                    
                    // 尝试解析操作类型
                    var operationType = ParseOperationTypeFromKey(operationKey);
                    if (operationType.HasValue && _cooldownPeriods.TryGetValue(operationType.Value, out var cooldownPeriod))
                    {
                        var remainingTime = GetRemainingCooldown(operationKey, cooldownPeriod);
                        if (remainingTime > TimeSpan.Zero)
                        {
                            activeCooldowns.Add(new ActiveCooldownInfo
                            {
                                OperationKey = operationKey,
                                OperationType = operationType.Value,
                                LastExecutionTime = lastExecutionTime,
                                RemainingTime = remainingTime,
                                TotalCooldownPeriod = cooldownPeriod
                            });
                        }
                    }
                }

                return activeCooldowns.OrderBy(c => c.RemainingTime).ToList();
            }
        }

        /// <summary>
        /// 从操作键解析操作类型
        /// </summary>
        private CooldownOperationType? ParseOperationTypeFromKey(string operationKey)
        {
            if (string.IsNullOrEmpty(operationKey))
                return null;

            foreach (var operationType in Enum.GetValues<CooldownOperationType>())
            {
                if (operationKey.Contains($"_{operationType}_") || operationKey.EndsWith($"_{operationType}"))
                {
                    return operationType;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取当前记录总数
        /// </summary>
        public int GetRecordCount()
        {
            lock (_lock)
            {
                return _lastExecutionTimes.Count;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                _lastExecutionTimes.Clear();
            }
            _logger.LogInformation("🛡️ 冷却期管理器已释放资源");
        }
    }

    /// <summary>
    /// 冷却期操作类型枚举
    /// </summary>
    public enum CooldownOperationType
    {
        BreakEven,          // 保本止损
        AddPosition,        // 推仓操作
        ProfitProtection,   // 保盈止损
        ManualOrder,        // 手动下单
        StopLoss           // 止损单创建
    }

    /// <summary>
    /// 活跃冷却期信息
    /// </summary>
    public class ActiveCooldownInfo
    {
        public string OperationKey { get; set; } = string.Empty;
        public CooldownOperationType OperationType { get; set; }
        public DateTime LastExecutionTime { get; set; }
        public TimeSpan RemainingTime { get; set; }
        public TimeSpan TotalCooldownPeriod { get; set; }
        
        /// <summary>
        /// 冷却进度百分比 (0-100)
        /// </summary>
        public double ProgressPercentage => 
            ((TotalCooldownPeriod - RemainingTime).TotalSeconds / TotalCooldownPeriod.TotalSeconds) * 100;
    }

    /// <summary>
    /// 冷却期管理统计信息
    /// </summary>
    public class CooldownStats
    {
        public int TotalExecutions { get; set; }
        public int CooldownBlocks { get; set; }  // 被冷却期阻止的操作次数
        public int RecordsCleaned { get; set; }  // 清理的记录数
        public DateTime? LastExecutionTime { get; set; }
        
        /// <summary>
        /// 冷却期阻止率
        /// </summary>
        public double BlockRate => TotalExecutions > 0 ? 
            (double)CooldownBlocks / (TotalExecutions + CooldownBlocks) * 100 : 0;
    }
} 