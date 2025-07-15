using System;
using System.Threading;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 日志记录事件处理器 - 处理所有事件的日志记录
    /// </summary>
    public class LoggingEventHandler : 
        IEventHandler<ExecutionStateChangedEvent>,
        IEventHandler<MonitorStatusChangedEvent>,
        IEventHandler<PositionChangedEvent>,
        IEventHandler<ErrorEvent>,
        IEventHandler<StopOrderEvent>,
        IEventHandler<CooldownEvent>,
        IEventHandler<PerformanceEvent>,
        IEventHandler<DataSyncEvent>
    {
        private readonly ILogger<LoggingEventHandler> _logger;

        public LoggingEventHandler(ILogger<LoggingEventHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task HandleAsync(ExecutionStateChangedEvent eventData, CancellationToken cancellationToken = default)
        {
            var tierText = eventData.TierIndex.HasValue ? $"阶梯{eventData.TierIndex}" : "";
            var statusText = eventData.IsSuccess ? "✅ 成功" : "❌ 失败";
            
            _logger.LogInformation($"🔄 执行状态变更: {eventData.Symbol}_{eventData.PositionSide} {eventData.ExecutionType}{tierText} - {statusText} (浮盈: {eventData.TriggerPnl:F2}U)");
            
            if (!eventData.IsSuccess && !string.IsNullOrEmpty(eventData.Message))
            {
                _logger.LogWarning($"   ⚠️ 失败原因: {eventData.Message}");
            }
            
            return Task.CompletedTask;
        }

        public Task HandleAsync(MonitorStatusChangedEvent eventData, CancellationToken cancellationToken = default)
        {
            var statusText = eventData.IsRunning ? "🚀 启动" : "⏹️ 停止";
            _logger.LogInformation($"📊 监控状态: {statusText} - {eventData.Message} (活跃合约: {eventData.ActiveContractCount})");
            return Task.CompletedTask;
        }

        public Task HandleAsync(PositionChangedEvent eventData, CancellationToken cancellationToken = default)
        {
            var changeIcon = eventData.ChangeType switch
            {
                PositionChangeType.Opened => "🆕",
                PositionChangeType.Increased => "📈",
                PositionChangeType.Decreased => "📉",
                PositionChangeType.Closed => "❌",
                _ => "🔄"
            };
            
            _logger.LogInformation($"{changeIcon} 持仓变化: {eventData.Symbol}_{eventData.PositionSide} {eventData.ChangeType} " +
                $"(数量变化: {eventData.QuantityChange:F6}, 当前: {eventData.CurrentQuantity:F6}, 浮盈: {eventData.CurrentPnl:F2}U) - {eventData.Reason}");
            
            return Task.CompletedTask;
        }

        public Task HandleAsync(ErrorEvent eventData, CancellationToken cancellationToken = default)
        {
            var priority = eventData.Priority switch
            {
                EventPriority.Critical => "🚨",
                EventPriority.High => "❗",
                EventPriority.Normal => "⚠️",
                _ => "ℹ️"
            };
            
            var logLevel = eventData.Priority >= EventPriority.High ? Microsoft.Extensions.Logging.LogLevel.Error : Microsoft.Extensions.Logging.LogLevel.Warning;
            
            _logger.Log(logLevel, new EventId(), $"{priority} 错误事件: {eventData.ErrorType} - {eventData.ErrorMessage}", null, (state, ex) => state.ToString());
            
            if (!string.IsNullOrEmpty(eventData.Symbol))
            {
                _logger.Log(logLevel, new EventId(), $"   📍 相关合约: {eventData.Symbol}", null, (state, ex) => state.ToString());
            }
            
            if (!string.IsNullOrEmpty(eventData.Operation))
            {
                _logger.Log(logLevel, new EventId(), $"   🔧 相关操作: {eventData.Operation}", null, (state, ex) => state.ToString());
            }
            
            if (eventData.RequiresIntervention)
            {
                _logger.LogCritical($"   🚨 需要人工干预: {eventData.ErrorMessage}");
            }
            
            return Task.CompletedTask;
        }

        public Task HandleAsync(StopOrderEvent eventData, CancellationToken cancellationToken = default)
        {
            var actionIcon = eventData.Action switch
            {
                StopOrderAction.Creating => "🔄",
                StopOrderAction.Created => "✅",
                StopOrderAction.Failed => "❌",
                StopOrderAction.Replaced => "🔄",
                StopOrderAction.Cancelled => "🚫",
                StopOrderAction.Triggered => "⚡",
                _ => "ℹ️"
            };
            
            _logger.LogInformation($"{actionIcon} 止损单: {eventData.Symbol} {eventData.Action} " +
                $"(类型: {eventData.StopOrderType}, 价格: {eventData.StopPrice:F4}, 数量: {eventData.Quantity:F6})");
            
            if (!eventData.IsSuccess && !string.IsNullOrEmpty(eventData.ErrorMessage))
            {
                _logger.LogWarning($"   ⚠️ 错误信息: {eventData.ErrorMessage}");
            }
            
            return Task.CompletedTask;
        }

        public Task HandleAsync(CooldownEvent eventData, CancellationToken cancellationToken = default)
        {
            var actionIcon = eventData.ActionType switch
            {
                CooldownActionType.Started => "🔒",
                CooldownActionType.Blocked => "⏰",
                CooldownActionType.Expired => "🔓",
                CooldownActionType.Cleared => "🧹",
                _ => "ℹ️"
            };
            
            var tierText = eventData.TierIndex.HasValue ? $"阶梯{eventData.TierIndex}" : "";
            
            _logger.LogDebug($"{actionIcon} 冷却期: {eventData.Symbol}_{eventData.PositionSide} {eventData.OperationType}{tierText} " +
                $"{eventData.ActionType} (时长: {eventData.CooldownSeconds}s, 剩余: {eventData.RemainingSeconds:F1}s)");
            
            return Task.CompletedTask;
        }

        public Task HandleAsync(PerformanceEvent eventData, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug($"📊 性能指标: {eventData.MetricName} = {eventData.Value:F2} {eventData.Unit} " +
                $"(类型: {eventData.MetricType}, 时间窗口: {eventData.TimeWindowSeconds}s)");
            
            return Task.CompletedTask;
        }

        public Task HandleAsync(DataSyncEvent eventData, CancellationToken cancellationToken = default)
        {
            var statusText = eventData.IsSuccess ? "✅" : "❌";
            
            _logger.LogInformation($"💾 数据同步: {eventData.SyncType} {eventData.DataType} {statusText} " +
                $"(记录数: {eventData.RecordCount}, 耗时: {eventData.DurationMs}ms)");
            
            if (!eventData.IsSuccess && !string.IsNullOrEmpty(eventData.ErrorMessage))
            {
                _logger.LogWarning($"   ⚠️ 同步失败: {eventData.ErrorMessage}");
            }
            
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// UI更新事件处理器 - 负责更新用户界面
    /// </summary>
    public class UIUpdateEventHandler : 
        IEventHandler<ExecutionStateChangedEvent>,
        IEventHandler<MonitorStatusChangedEvent>,
        IEventHandler<PositionChangedEvent>,
        IEventHandler<ErrorEvent>
    {
        private readonly ILogger<UIUpdateEventHandler> _logger;
        
        // 事件用于通知UI更新
        public event EventHandler<ExecutionStateChangedEvent>? ExecutionStateChanged;
        public event EventHandler<MonitorStatusChangedEvent>? MonitorStatusChanged;
        public event EventHandler<PositionChangedEvent>? PositionChanged;
        public event EventHandler<ErrorEvent>? ErrorOccurred;

        public UIUpdateEventHandler(ILogger<UIUpdateEventHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task HandleAsync(ExecutionStateChangedEvent eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                // 在UI线程上触发事件
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    ExecutionStateChanged?.Invoke(this, eventData);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ UI更新失败: ExecutionStateChanged");
            }
            
            return Task.CompletedTask;
        }

        public Task HandleAsync(MonitorStatusChangedEvent eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    MonitorStatusChanged?.Invoke(this, eventData);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ UI更新失败: MonitorStatusChanged");
            }
            
            return Task.CompletedTask;
        }

        public Task HandleAsync(PositionChangedEvent eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    PositionChanged?.Invoke(this, eventData);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ UI更新失败: PositionChanged");
            }
            
            return Task.CompletedTask;
        }

        public Task HandleAsync(ErrorEvent eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    ErrorOccurred?.Invoke(this, eventData);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ UI更新失败: ErrorEvent");
            }
            
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 统计收集事件处理器 - 收集和更新各种统计信息
    /// </summary>
    public class StatisticsEventHandler : 
        IEventHandler<ExecutionStateChangedEvent>,
        IEventHandler<PerformanceEvent>,
        IEventHandler<ErrorEvent>,
        IEventHandler<StopOrderEvent>
    {
        private readonly ILogger<StatisticsEventHandler> _logger;
        
        // 统计计数器（线程安全）
        private int _totalExecutions = 0;
        private int _successfulExecutions = 0;
        private int _failedExecutions = 0;
        private int _totalErrors = 0;
        private int _stopOrdersCreated = 0;
        private int _stopOrdersFailed = 0;
        
        // 按合约的统计
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ContractStatistics> _contractStats = new();
        
        // 按执行类型的统计
        private readonly System.Collections.Concurrent.ConcurrentDictionary<ExecutionType, ExecutionTypeStatistics> _executionTypeStats = new();

        public StatisticsEventHandler(ILogger<StatisticsEventHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task HandleAsync(ExecutionStateChangedEvent eventData, CancellationToken cancellationToken = default)
        {
            // 更新全局统计
            Interlocked.Increment(ref _totalExecutions);
            if (eventData.IsSuccess)
            {
                Interlocked.Increment(ref _successfulExecutions);
            }
            else
            {
                Interlocked.Increment(ref _failedExecutions);
            }

            // 更新合约统计
            var contractStats = _contractStats.GetOrAdd(eventData.ContractKey, _ => new ContractStatistics());
            contractStats.UpdateExecution(eventData.IsSuccess, eventData.TriggerPnl);

            // 更新执行类型统计
            var typeStats = _executionTypeStats.GetOrAdd(eventData.ExecutionType, _ => new ExecutionTypeStatistics());
            typeStats.UpdateExecution(eventData.IsSuccess, eventData.TierIndex);

            return Task.CompletedTask;
        }

        public Task HandleAsync(PerformanceEvent eventData, CancellationToken cancellationToken = default)
        {
            // 这里可以收集性能指标，比如存储到时序数据库
            _logger.LogTrace($"📊 性能指标收集: {eventData.MetricName} = {eventData.Value}");
            return Task.CompletedTask;
        }

        public Task HandleAsync(ErrorEvent eventData, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _totalErrors);
            
            // 如果有相关合约，更新合约的错误统计
            if (!string.IsNullOrEmpty(eventData.Symbol))
            {
                var positionSide = eventData.Data.TryGetValue("PositionSide", out var value) ? value?.ToString() ?? "UNKNOWN" : "UNKNOWN";
                var contractKey = $"{eventData.Symbol}_{positionSide}";
                var contractStats = _contractStats.GetOrAdd(contractKey, _ => new ContractStatistics());
                contractStats.UpdateError(eventData.ErrorType);
            }

            return Task.CompletedTask;
        }

        public Task HandleAsync(StopOrderEvent eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Action == StopOrderAction.Created)
            {
                Interlocked.Increment(ref _stopOrdersCreated);
            }
            else if (eventData.Action == StopOrderAction.Failed)
            {
                Interlocked.Increment(ref _stopOrdersFailed);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取全局统计信息
        /// </summary>
        public GlobalStatistics GetGlobalStatistics()
        {
            return new GlobalStatistics
            {
                TotalExecutions = _totalExecutions,
                SuccessfulExecutions = _successfulExecutions,
                FailedExecutions = _failedExecutions,
                TotalErrors = _totalErrors,
                StopOrdersCreated = _stopOrdersCreated,
                StopOrdersFailed = _stopOrdersFailed,
                SuccessRate = _totalExecutions > 0 ? (double)_successfulExecutions / _totalExecutions * 100 : 0
            };
        }

        /// <summary>
        /// 获取合约统计信息
        /// </summary>
        public System.Collections.Concurrent.ConcurrentDictionary<string, ContractStatistics> GetContractStatistics() => _contractStats;

        /// <summary>
        /// 获取执行类型统计信息
        /// </summary>
        public System.Collections.Concurrent.ConcurrentDictionary<ExecutionType, ExecutionTypeStatistics> GetExecutionTypeStatistics() => _executionTypeStats;
    }

    /// <summary>
    /// 合约统计信息
    /// </summary>
    public class ContractStatistics
    {
        private int _executions = 0;
        private int _successes = 0;
        private int _failures = 0;
        private int _errors = 0;
        private decimal _totalPnl = 0;
        private readonly object _lock = new();

        public void UpdateExecution(bool isSuccess, decimal pnl)
        {
            lock (_lock)
            {
                _executions++;
                _totalPnl += pnl;
                
                if (isSuccess) _successes++;
                else _failures++;
            }
        }

        public void UpdateError(ErrorType errorType)
        {
            Interlocked.Increment(ref _errors);
        }

        public (int executions, int successes, int failures, int errors, decimal totalPnl, double successRate) GetStats()
        {
            lock (_lock)
            {
                var successRate = _executions > 0 ? (double)_successes / _executions * 100 : 0;
                return (_executions, _successes, _failures, _errors, _totalPnl, successRate);
            }
        }
    }

    /// <summary>
    /// 执行类型统计信息
    /// </summary>
    public class ExecutionTypeStatistics
    {
        private int _executions = 0;
        private int _successes = 0;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> _tierExecutions = new();
        private readonly object _lock = new();

        public void UpdateExecution(bool isSuccess, int? tierIndex)
        {
            lock (_lock)
            {
                _executions++;
                if (isSuccess) _successes++;
                
                if (tierIndex.HasValue)
                {
                    _tierExecutions.AddOrUpdate(tierIndex.Value, 1, (key, count) => count + 1);
                }
            }
        }

        public (int executions, int successes, double successRate, System.Collections.Concurrent.ConcurrentDictionary<int, int> tierStats) GetStats()
        {
            lock (_lock)
            {
                var successRate = _executions > 0 ? (double)_successes / _executions * 100 : 0;
                return (_executions, _successes, successRate, _tierExecutions);
            }
        }
    }

    /// <summary>
    /// 全局统计信息
    /// </summary>
    public class GlobalStatistics
    {
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public int TotalErrors { get; set; }
        public int StopOrdersCreated { get; set; }
        public int StopOrdersFailed { get; set; }
        public double SuccessRate { get; set; }
    }
} 