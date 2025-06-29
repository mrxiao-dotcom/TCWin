using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 事件处理器接口
    /// </summary>
    public interface IEventHandler<in T> where T : AutoMonitorEvent
    {
        /// <summary>
        /// 处理事件
        /// </summary>
        /// <param name="eventData">事件数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>处理任务</returns>
        Task HandleAsync(T eventData, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 事件总线接口
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// 发布事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="eventData">事件数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发布任务</returns>
        Task PublishAsync<T>(T eventData, CancellationToken cancellationToken = default) where T : AutoMonitorEvent;

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        void Subscribe<T>(IEventHandler<T> handler) where T : AutoMonitorEvent;

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        void Unsubscribe<T>(IEventHandler<T> handler) where T : AutoMonitorEvent;

        /// <summary>
        /// 启动事件总线
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 停止事件总线
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 获取统计信息
        /// </summary>
        EventBusStats GetStatistics();
    }

    /// <summary>
    /// 事件总线统计信息
    /// </summary>
    public class EventBusStats
    {
        public int TotalEventsSent { get; set; }
        public int TotalEventsProcessed { get; set; }
        public int TotalEventsFailed { get; set; }
        public int CurrentQueueSize { get; set; }
        public int ActiveHandlers { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public DateTime LastEventTime { get; set; }
        public Dictionary<string, int> EventCountByType { get; set; } = new();
    }

    /// <summary>
    /// 事件总线实现
    /// </summary>
    public class EventBus : IEventBus, IDisposable
    {
        private readonly ILogger<EventBus> _logger;
        private readonly ConcurrentDictionary<Type, ConcurrentBag<object>> _handlers = new();
        private readonly Channel<AutoMonitorEvent> _eventChannel;
        private readonly ChannelWriter<AutoMonitorEvent> _eventWriter;
        private readonly ChannelReader<AutoMonitorEvent> _eventReader;
        
        // 统计信息
        private int _totalEventsSent = 0;
        private int _totalEventsProcessed = 0;
        private int _totalEventsFailed = 0;
        private readonly ConcurrentDictionary<string, int> _eventCountByType = new();
        private readonly ConcurrentQueue<TimeSpan> _processingTimes = new();
        private DateTime _lastEventTime = DateTime.Now;
        
        // 并发控制
        private readonly SemaphoreSlim _processingSemaphore;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Task? _processingTask;
        private readonly object _lockObject = new();
        
        // 配置
        private readonly int _maxConcurrentHandlers;
        private readonly int _queueCapacity;
        private readonly TimeSpan _handlerTimeout;

        public EventBus(ILogger<EventBus> logger, int maxConcurrentHandlers = 10, int queueCapacity = 1000, TimeSpan? handlerTimeout = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxConcurrentHandlers = maxConcurrentHandlers;
            _queueCapacity = queueCapacity;
            _handlerTimeout = handlerTimeout ?? TimeSpan.FromSeconds(30);
            
            _processingSemaphore = new SemaphoreSlim(maxConcurrentHandlers, maxConcurrentHandlers);
            
            // 创建有界通道，防止内存溢出
            var channelOptions = new BoundedChannelOptions(queueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            };
            
            _eventChannel = Channel.CreateBounded<AutoMonitorEvent>(channelOptions);
            _eventWriter = _eventChannel.Writer;
            _eventReader = _eventChannel.Reader;
            
            _logger.LogInformation($"🚌 事件总线已初始化 - 最大并发处理器: {maxConcurrentHandlers}, 队列容量: {queueCapacity}, 处理器超时: {_handlerTimeout.TotalSeconds}秒");
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        public async Task PublishAsync<T>(T eventData, CancellationToken cancellationToken = default) where T : AutoMonitorEvent
        {
            if (eventData == null) throw new ArgumentNullException(nameof(eventData));

            try
            {
                // 设置事件元数据
                eventData.Source = eventData.Source ?? "EventBus";
                eventData.Timestamp = DateTime.Now;
                _lastEventTime = eventData.Timestamp;

                // 写入事件通道
                await _eventWriter.WriteAsync(eventData, cancellationToken);
                
                Interlocked.Increment(ref _totalEventsSent);
                _eventCountByType.AddOrUpdate(eventData.EventType, 1, (key, count) => count + 1);
                
                _logger.LogDebug($"📤 事件已发布: {eventData.EventType} ({eventData.EventId})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 发布事件失败: {eventData?.EventType} ({eventData?.EventId})");
                throw;
            }
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        public void Subscribe<T>(IEventHandler<T> handler) where T : AutoMonitorEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(T);
            _handlers.AddOrUpdate(eventType, 
                new ConcurrentBag<object> { handler },
                (key, existingHandlers) => 
                {
                    existingHandlers.Add(handler);
                    return existingHandlers;
                });

            _logger.LogInformation($"📝 事件处理器已订阅: {eventType.Name} -> {handler.GetType().Name}");
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        public void Unsubscribe<T>(IEventHandler<T> handler) where T : AutoMonitorEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(T);
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                // 创建一个新的集合，排除要取消订阅的处理器
                var updatedHandlers = new ConcurrentBag<object>();
                foreach (var existingHandler in handlers)
                {
                    if (!ReferenceEquals(existingHandler, handler))
                    {
                        updatedHandlers.Add(existingHandler);
                    }
                }
                
                _handlers.TryUpdate(eventType, updatedHandlers, handlers);
                _logger.LogInformation($"🗑️ 事件处理器已取消订阅: {eventType.Name} -> {handler.GetType().Name}");
            }
        }

        /// <summary>
        /// 启动事件总线
        /// </summary>
        public Task StartAsync()
        {
            lock (_lockObject)
            {
                if (_processingTask != null)
                {
                    _logger.LogWarning("⚠️ 事件总线已经在运行中");
                    return Task.CompletedTask;
                }

                _processingTask = ProcessEventsAsync(_cancellationTokenSource.Token);
                _logger.LogInformation("🚀 事件总线已启动");
                
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 停止事件总线
        /// </summary>
        public async Task StopAsync()
        {
            lock (_lockObject)
            {
                if (_processingTask == null)
                {
                    _logger.LogInformation("ℹ️ 事件总线已经停止");
                    return;
                }
            }

            try
            {
                // 停止接收新事件
                _eventWriter.Complete();
                
                // 取消处理
                _cancellationTokenSource.Cancel();
                
                // 等待处理完成
                if (_processingTask != null)
                {
                    await _processingTask;
                }
                
                _logger.LogInformation("⏹️ 事件总线已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 停止事件总线时发生错误");
            }
            finally
            {
                lock (_lockObject)
                {
                    _processingTask = null;
                }
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public EventBusStats GetStatistics()
        {
            var activeHandlers = _handlers.Values.Sum(bag => bag.Count);
            var queueSize = _eventReader.CanCount ? _eventReader.Count : 0;
            
            // 计算平均处理时间
            var avgProcessingTime = TimeSpan.Zero;
            if (_processingTimes.Count > 0)
            {
                var times = _processingTimes.ToArray();
                var totalTicks = times.Sum(t => t.Ticks);
                avgProcessingTime = new TimeSpan(totalTicks / times.Length);
            }

            return new EventBusStats
            {
                TotalEventsSent = _totalEventsSent,
                TotalEventsProcessed = _totalEventsProcessed,
                TotalEventsFailed = _totalEventsFailed,
                CurrentQueueSize = queueSize,
                ActiveHandlers = activeHandlers,
                AverageProcessingTime = avgProcessingTime,
                LastEventTime = _lastEventTime,
                EventCountByType = new Dictionary<string, int>(_eventCountByType)
            };
        }

        /// <summary>
        /// 事件处理主循环
        /// </summary>
        private async Task ProcessEventsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔄 开始事件处理循环");

            try
            {
                await foreach (var eventData in _eventReader.ReadAllAsync(cancellationToken))
                {
                    // 控制并发处理数量
                    await _processingSemaphore.WaitAsync(cancellationToken);

                    // 异步处理事件，不阻塞主循环
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessSingleEventAsync(eventData, cancellationToken);
                        }
                        finally
                        {
                            _processingSemaphore.Release();
                        }
                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🛑 事件处理循环已取消");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 事件处理循环发生异常");
            }
            finally
            {
                _logger.LogInformation("⏹️ 事件处理循环已结束");
            }
        }

        /// <summary>
        /// 处理单个事件
        /// </summary>
        private async Task ProcessSingleEventAsync(AutoMonitorEvent eventData, CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            var eventType = eventData.GetType();
            var handlerCount = 0;
            var successCount = 0;
            var failureCount = 0;

            try
            {
                _logger.LogDebug($"📥 开始处理事件: {eventData.EventType} ({eventData.EventId})");

                // 查找事件处理器
                if (_handlers.TryGetValue(eventType, out var handlers))
                {
                    handlerCount = handlers.Count;
                    
                    // 并行执行所有处理器
                    var tasks = new List<Task>();
                    
                    foreach (var handler in handlers)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        
                        tasks.Add(ExecuteHandlerAsync(handler, eventData, cancellationToken)
                            .ContinueWith(task =>
                            {
                                if (task.IsCompletedSuccessfully)
                                {
                                    Interlocked.Increment(ref successCount);
                                }
                                else
                                {
                                    Interlocked.Increment(ref failureCount);
                                }
                            }, cancellationToken));
                    }

                    // 等待所有处理器完成
                    if (tasks.Count > 0)
                    {
                        await Task.WhenAll(tasks);
                    }
                }

                Interlocked.Increment(ref _totalEventsProcessed);
                
                if (failureCount > 0)
                {
                    _logger.LogWarning($"⚠️ 事件处理部分失败: {eventData.EventType} - 成功: {successCount}, 失败: {failureCount}");
                }
                else if (handlerCount > 0)
                {
                    _logger.LogDebug($"✅ 事件处理完成: {eventData.EventType} - 处理器数量: {handlerCount}");
                }
                else
                {
                    _logger.LogDebug($"ℹ️ 没有找到事件处理器: {eventData.EventType}");
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _totalEventsFailed);
                _logger.LogError(ex, $"❌ 事件处理失败: {eventData.EventType} ({eventData.EventId})");
            }
            finally
            {
                // 记录处理时间
                var processingTime = DateTime.Now - startTime;
                RecordProcessingTime(processingTime);
            }
        }

        /// <summary>
        /// 执行单个事件处理器
        /// </summary>
        private async Task ExecuteHandlerAsync(object handler, AutoMonitorEvent eventData, CancellationToken cancellationToken)
        {
            try
            {
                // 使用反射调用泛型方法
                var handlerType = handler.GetType();
                var handleMethod = handlerType.GetMethod("HandleAsync");
                
                if (handleMethod != null)
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(_handlerTimeout);
                    
                    var task = (Task?)handleMethod.Invoke(handler, new object[] { eventData, timeoutCts.Token });
                    if (task != null)
                    {
                        await task;
                    }
                }
                else
                {
                    _logger.LogError($"❌ 处理器缺少HandleAsync方法: {handlerType.Name}");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning($"⏰ 事件处理器超时: {handler.GetType().Name} - {eventData.EventType}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 事件处理器执行失败: {handler.GetType().Name} - {eventData.EventType}");
                throw;
            }
        }

        /// <summary>
        /// 记录处理时间
        /// </summary>
        private void RecordProcessingTime(TimeSpan processingTime)
        {
            _processingTimes.Enqueue(processingTime);
            
            // 保持最近100个处理时间记录
            while (_processingTimes.Count > 100)
            {
                _processingTimes.TryDequeue(out _);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                StopAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 释放事件总线资源时发生错误");
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _processingSemaphore?.Dispose();
                _eventChannel.Writer.Complete();
                
                _logger.LogInformation("🗑️ 事件总线资源已释放");
            }
        }
    }
} 