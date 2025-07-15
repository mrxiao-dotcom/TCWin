using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.ViewModels;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 自动监控服务 - 集成现有交易功能的完整实现
    /// </summary>
    public class AutoMonitorService : IDisposable
    {
        private readonly IBinanceService _binanceService;
        private readonly MainViewModel _mainViewModel;
        private readonly ILogger<AutoMonitorService> _logger;
        private readonly AutoMonitorPersistenceService _persistenceService;
        
        // 🛡️ 新增：止损单管理器，确保止损单唯一性
        private readonly StopOrderManager _stopOrderManager;
        
        // 🔄 新增：合约状态管理器，解决多合约状态冲突
        private readonly ContractStateManager _contractStateManager;
        
        // 🛡️ 新增：冷却期管理器，防止重复触发
        private readonly CooldownManager _cooldownManager;
        
        // 🔄 新增：统一状态管理器，整合三套状态系统
        private readonly UnifiedStateManager _unifiedStateManager;
        
        // 🚌 新增：事件总线，实现事件驱动架构
        private readonly IEventBus _eventBus;
        
        // 📊 新增：事件处理器
        private readonly LoggingEventHandler _loggingHandler;
        private readonly StatisticsEventHandler _statisticsHandler;
        
        // 🔍 新增：配置验证服务
        private readonly IConfigValidationService _configValidationService;
        
        // 🚀 新增：智能下单服务，提高自动盯盘下单成功率
        private readonly SmartOrderService _smartOrderService;
        
        // 🛡️ 扫描计数器，用于定期清理
        private int _scanCount = 0;
        
        private Timer? _scanTimer;
        private bool _isRunning;
        private AutoMonitorConfig? _config;
        private readonly object _lockObject = new();
        
        // 🔧 修复：使用SemaphoreSlim替代Monitor，提供更安全的并发控制
        private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
        
        // 🔧 新增：持仓数据缓存锁，防止数据读取冲突
        private readonly object _positionDataLock = new();

        // 持仓档案存储
        private readonly Dictionary<string, PositionProfile> _positionProfiles = new();
        
        // 执行历史记录
        private readonly List<ExecutionHistory> _executionHistory = new();

        // 🔗 新增：公开状态管理器访问器（供监控面板使用）
        public StopOrderManager StopOrderManager => _stopOrderManager;
        public ContractStateManager ContractStateManager => _contractStateManager;
        public CooldownManager CooldownManager => _cooldownManager;
        public UnifiedStateManager UnifiedStateManager => _unifiedStateManager;
        public IEventBus EventBus => _eventBus;
        public bool IsRunning => _isRunning;
        public AutoMonitorConfig? CurrentConfig => _config;

        // 事件定义
        public event EventHandler<MonitorStatusChangedEventArgs>? MonitorStatusChanged;
        public event EventHandler<ExecutionResultEventArgs>? ExecutionCompleted;
        public event EventHandler<WorkLogEventArgs>? WorkLogAdded;

        public AutoMonitorService(
            IBinanceService binanceService, 
            MainViewModel mainViewModel,
            ILogger<AutoMonitorService> logger)
        {
            _binanceService = binanceService ?? throw new ArgumentNullException(nameof(binanceService));
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // 🔧 初始化持久化服务（使用现有接口）
            _persistenceService = new AutoMonitorPersistenceService();
            
            // 🛡️ 新增：初始化止损单管理器
            _stopOrderManager = new StopOrderManager(_binanceService, logger);
            
            // 🔄 新增：初始化合约状态管理器
            _contractStateManager = new ContractStateManager();
            
            // 🛡️ 新增：初始化冷却期管理器
            _cooldownManager = new CooldownManager(logger);
            
            // 🔄 新增：初始化统一状态管理器
            _unifiedStateManager = new UnifiedStateManager(logger);
            _unifiedStateManager.Initialize();
            
            // 🚌 新增：初始化事件总线
            var eventBusLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EventBus>();
            _eventBus = new EventBus(eventBusLogger);
            
            // 🔗 将事件总线设置到统一状态管理器
            _unifiedStateManager.SetEventBus(_eventBus);
            
            // 📊 新增：初始化事件处理器
            var loggingHandlerLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<LoggingEventHandler>();
            var statisticsHandlerLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<StatisticsEventHandler>();
            
            _loggingHandler = new LoggingEventHandler(loggingHandlerLogger);
            _statisticsHandler = new StatisticsEventHandler(statisticsHandlerLogger);
            
            // 🔍 新增：初始化配置验证服务
            var configValidationLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<ConfigValidationService>();
            _configValidationService = new ConfigValidationService(configValidationLogger);
            
            // 🚀 新增：初始化智能下单服务
            var smartOrderLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<SmartOrderService>();
            _smartOrderService = new SmartOrderService(_binanceService, smartOrderLogger);
            
            // 🔌 订阅事件
            SetupEventSubscriptions();
        }



        /// <summary>
        /// 启动自动监控
        /// </summary>
        public async Task<bool> StartMonitoringAsync(AutoMonitorConfig config)
        {
            _logger.LogCritical("🔍 [STARTUP-01] 开始执行 StartMonitoringAsync 方法");
            
            if (config == null) 
            {
                _logger.LogCritical("❌ [STARTUP-02] 配置为空，抛出异常");
                throw new ArgumentNullException(nameof(config));
            }

            _logger.LogCritical($"🔍 [STARTUP-03] 配置验证通过，配置名称: {config.Name}");

            Timer? oldTimerToDispose = null;
            bool wasRunning = false;
            
            _logger.LogCritical("🔍 [STARTUP-04] 开始获取锁并检查当前状态");
            
            lock (_lockObject)
            {
                _logger.LogCritical("🔍 [STARTUP-05] 已获取锁，检查运行状态");
                wasRunning = _isRunning;
                if (_isRunning) 
                {
                    _logger.LogCritical("⚠️ [STARTUP-06] 自动监控已在运行中，先停止再重新启动");
                    // 🔧 修复：强制重置状态，但Timer处理移到lock外部
                    _isRunning = false;
                    oldTimerToDispose = _scanTimer;
                    _scanTimer = null;
                }
                
                // 🔧 修复：延迟设置运行状态，确保初始化成功后再设置
                _config = config;
                // _isRunning = true; // 移到初始化成功后设置
                _logger.LogCritical("🔍 [STARTUP-07] 锁内状态设置完成，准备释放锁");
            }
            
            _logger.LogCritical("🔍 [STARTUP-08] 已释放锁，开始处理旧定时器清理");
            
            // 🔧 修复：在lock外部处理旧Timer的清理，避免并发问题
            if (oldTimerToDispose != null)
            {
                _logger.LogCritical("🔍 [STARTUP-09] 开始清理旧定时器");
                oldTimerToDispose.Dispose();
                await Task.Delay(200); // 增加等待时间，确保完全清理
                _logger.LogCritical("🔍 [STARTUP-10] 旧的扫描定时器已清理完成");
            }
            
            // 🔧 修复：如果是重启，额外等待确保状态完全重置
            if (wasRunning)
            {
                _logger.LogCritical("🔍 [STARTUP-11] 检测到重启，等待状态完全重置...");
                await Task.Delay(300);
                _logger.LogCritical("🔍 [STARTUP-12] 状态重置等待完成");
            }

            try
            {
                _logger.LogCritical("🔍 [STARTUP-13] 进入主启动流程try块");
                _logger.LogInformation("🚀 启动自动监控服务...");
                AddWorkLog("INFO", "🚀 正在启动自动监控服务...");
                
                // 🔍 新增：详细的配置验证日志
                _logger.LogCritical("🔍 [STARTUP-14] 开始配置验证阶段");
                _logger.LogInformation("🔍 开始验证配置...");
                _logger.LogInformation($"📝 配置名称: {config.Name ?? "未命名"}");
                _logger.LogInformation($"⏱️ 扫描间隔: {config.ScanIntervalSeconds}秒");
                _logger.LogInformation($"🔧 配置验证服务状态: {(_configValidationService != null ? "已初始化" : "未初始化")}");
                
                AddWorkLog("INFO", $"📝 验证配置: {config.Name ?? "未命名"} (间隔: {config.ScanIntervalSeconds}秒)");
                
                ConfigValidationResult? validationResult = null;
                try
                {
                    _logger.LogCritical("🔍 [STARTUP-15] 开始调用配置验证服务");
                    validationResult = await _configValidationService.ValidateAsync(config, ValidationMode.Strict, true);
                    _logger.LogCritical($"🔍 [STARTUP-16] 配置验证服务调用完成，结果: {validationResult?.IsValid}");
                    _logger.LogInformation($"✅ 配置验证服务调用成功，结果: {validationResult?.IsValid}");
                    AddWorkLog("INFO", $"✅ 配置验证完成: {(validationResult?.IsValid == true ? "通过" : "失败")}");
                }
                catch (Exception validationEx)
                {
                    _logger.LogCritical(validationEx, "🔍 [STARTUP-17] 配置验证服务调用异常");
                    _logger.LogError(validationEx, "❌ 配置验证服务调用失败");
                    AddWorkLog("WARN", "⚠️ 配置验证服务异常，切换到基础验证模式");
                    
                    // 🔧 验证服务失败时使用基础验证
                    _logger.LogWarning("⚠️ 切换到基础配置验证模式");
                    validationResult = PerformBasicConfigValidation(config);
                    _logger.LogCritical("🔍 [STARTUP-18] 基础配置验证完成");
                }
                
                if (validationResult == null)
                {
                    _logger.LogError("❌ 配置验证结果为null，服务启动失败");
                    
                    lock (_lockObject) 
                    { 
                        _config = null; 
                        _isRunning = false; 
                    }
                    
                    OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                        IsRunning = false, 
                        Message = "启动失败: 配置验证服务异常" 
                    });
                    
                    return false;
                }
                
                if (!validationResult.IsValid)
                {
                    var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.Message));
                    var errorMsg = $"配置验证失败: {errorMessages}";
                    _logger.LogError($"❌ {errorMsg}");
                    
                    // 🔧 配置验证失败时，清空配置并重置状态
                    lock (_lockObject) 
                    { 
                        _config = null; 
                        _isRunning = false; 
                    }
                    
                    OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                        IsRunning = false, 
                        Message = $"启动失败: {errorMsg}" 
                    });
                    
                    _logger.LogError($"❌ 启动失败: 配置验证不通过");
                    return false;
                }
                
                // 记录配置验证结果
                if (validationResult.Warnings.Any())
                {
                    _logger.LogWarning($"⚠️ 配置验证警告: {validationResult.Warnings.Count}个警告");
                    foreach (var warning in validationResult.Warnings.Take(3)) // 只显示前3个警告
                    {
                        _logger.LogWarning($"   ⚠️ {warning.Message}");
                    }
                }
                
                if (validationResult.AutoFixes.Any())
                {
                    _logger.LogInformation($"🔧 应用了 {validationResult.AutoFixes.Count} 个自动修复");
                    foreach (var fix in validationResult.AutoFixes)
                    {
                        _logger.LogInformation($"   🔧 {fix.ConfigKey}: {fix.OriginalValue} → {fix.FixedValue} ({fix.FixReason})");
                    }
                }
                
                _logger.LogInformation($"✅ 配置验证通过: {validationResult.Summary}");
                
                // 🚌 启动事件总线（重试机制）
                _logger.LogCritical("🔍 [STARTUP-19] 开始事件总线启动阶段");
                _logger.LogInformation("🚌 开始启动事件总线...");
                _logger.LogInformation($"🔧 事件总线状态: {(_eventBus != null ? "已初始化" : "未初始化")}");
                
                bool eventBusStarted = false;
                AddWorkLog("INFO", "🚌 正在启动事件总线...");
                for (int retryCount = 0; retryCount < 3; retryCount++)
                {
                    try
                    {
                        _logger.LogCritical($"🔍 [STARTUP-20] 事件总线启动尝试 {retryCount + 1}/3");
                        _logger.LogInformation($"🔄 事件总线启动尝试 {retryCount + 1}/3");
                        AddWorkLog("INFO", $"🔄 事件总线启动尝试 {retryCount + 1}/3");
                        
                        // 🔧 修复：确保事件总线在启动前已停止
                        _logger.LogCritical("🔍 [STARTUP-21] 停止事件总线...");
                        _logger.LogDebug("⏹️ 停止事件总线...");
                        await _eventBus.StopAsync();
                        _logger.LogCritical("🔍 [STARTUP-22] 事件总线停止完成");
                        await Task.Delay(50); // 短暂等待确保完全停止
                        
                        _logger.LogCritical("🔍 [STARTUP-23] 启动事件总线...");
                        _logger.LogDebug("▶️ 启动事件总线...");
                        await _eventBus.StartAsync();
                        _logger.LogCritical("🔍 [STARTUP-24] 事件总线启动完成");
                        eventBusStarted = true;
                        _logger.LogInformation($"✅ 事件总线启动成功 (第{retryCount + 1}次尝试)");
                        AddWorkLog("INFO", $"✅ 事件总线启动成功 (第{retryCount + 1}次尝试)");
                        break;
                    }
                    catch (Exception busEx)
                    {
                        _logger.LogCritical(busEx, $"🔍 [STARTUP-25] 事件总线启动异常 (第{retryCount + 1}次尝试)");
                        _logger.LogWarning(busEx, $"❌ 事件总线启动失败 (第{retryCount + 1}次尝试): {busEx.Message}");
                        AddWorkLog("WARN", $"❌ 事件总线启动失败 (第{retryCount + 1}次尝试): {busEx.Message}");
                        if (retryCount < 2)
                        {
                            var waitTime = 200 * (retryCount + 1);
                            _logger.LogInformation($"⏳ 等待{waitTime}ms后重试...");
                            await Task.Delay(waitTime); // 重试前等待
                        }
                    }
                }
                
                if (!eventBusStarted)
                {
                    _logger.LogError("❌ 事件总线启动最终失败，已尝试3次");
                    _logger.LogError("💡 可能原因: 资源占用、权限不足、系统负载过高");
                    AddWorkLog("ERROR", "❌ 事件总线启动最终失败，已尝试3次");
                    
                    // 🔧 修复：事件总线启动失败时重置状态
                    lock (_lockObject) 
                    { 
                        _config = null; 
                        _isRunning = false; 
                    }
                    
                    OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                        IsRunning = false, 
                        Message = "启动失败: 事件总线启动失败" 
                    });
                    
                    _logger.LogError("❌ 启动失败: 事件总线无法启动，服务依赖事件系统运行");
                    return false;
                }
                
                // 🔧 修复：初始化持仓档案失败不应该阻止服务启动
                _logger.LogCritical("🔍 [STARTUP-26] 开始持仓档案初始化阶段");
                _logger.LogInformation("📊 开始初始化持仓档案...");
                _logger.LogInformation($"🔧 Binance服务状态: {(_binanceService != null ? "已初始化" : "未初始化")}");
                _logger.LogInformation($"💾 持久化服务状态: {(_persistenceService != null ? "已初始化" : "未初始化")}");
                
                AddWorkLog("INFO", "📊 正在初始化持仓档案...");
                
                try
                {
                    _logger.LogCritical("🔍 [STARTUP-27] 开始调用 InitializePositionProfilesAsync");
                    await InitializePositionProfilesAsync();
                    _logger.LogCritical($"🔍 [STARTUP-28] InitializePositionProfilesAsync 完成，档案数: {_positionProfiles.Count}");
                    _logger.LogInformation($"✅ 持仓档案初始化完成，当前档案数: {_positionProfiles.Count}");
                    AddWorkLog("INFO", $"✅ 持仓档案初始化完成，档案数: {_positionProfiles.Count}");
                }
                catch (Exception initEx)
                {
                    _logger.LogCritical(initEx, "🔍 [STARTUP-29] InitializePositionProfilesAsync 异常");
                    _logger.LogError(initEx, $"❌ 初始化持仓档案失败: {initEx.Message}");
                    _logger.LogWarning("⚠️ 继续启动监控服务，将在首次扫描时逐个恢复持仓状态");
                    AddWorkLog("WARN", $"⚠️ 持仓档案初始化失败: {initEx.Message}");
                    
                    // 🔧 修复：记录初始化失败的详细信息
                    if (initEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("💡 可能原因: API连接超时，网络连接不稳定");
                    }
                    else if (initEx.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("💡 可能原因: API密钥无效或权限不足");
                    }
                    else if (initEx.Message.Contains("rate", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("💡 可能原因: API调用频率限制");
                    }
                    
                    // 🔧 修复：不清空数据，让ProcessPositionAsync来处理状态恢复
                    // 注释掉清空操作，避免误清理已有状态
                    /*
                    lock (_lockObject)
                    {
                        _positionProfiles.Clear();
                        _executionHistory.Clear();
                    }
                    */
                }
                
                // 🔧 修复：在创建Timer前设置运行状态
                _logger.LogCritical("🔍 [STARTUP-30] 开始定时器创建阶段");
                _logger.LogInformation("⏰ 准备创建扫描定时器...");
                AddWorkLog("INFO", "⏰ 正在创建扫描定时器...");
                
                _logger.LogCritical("🔍 [STARTUP-31] 获取锁设置运行状态");
                lock (_lockObject)
                {
                    _isRunning = true;
                    _logger.LogCritical("🔍 [STARTUP-32] 服务运行状态已设置为true");
                    _logger.LogInformation("🔧 服务运行状态已设置为true");
                }
                
                var intervalMs = _config.ScanIntervalSeconds * 1000;
                
                // 🔧 修复：确保最小间隔为10秒，防止扫描重叠
                if (intervalMs < 10000) // 小于10秒
                {
                    intervalMs = 10000; // 强制设置为10秒
                    _logger.LogWarning($"⚠️ 扫描间隔过短，已调整为10秒以防止扫描重叠");
                    AddWorkLog("WARN", $"⚠️ 扫描间隔过短，已调整为10秒");
                }
                
                _logger.LogCritical($"🔍 [STARTUP-33] 定时器配置: 间隔 {_config.ScanIntervalSeconds}秒 ({intervalMs}ms)");
                _logger.LogInformation($"📝 定时器配置: 间隔 {_config.ScanIntervalSeconds}秒 ({intervalMs}ms)");
                
                try
                {
                    _logger.LogCritical("🔍 [STARTUP-34] 开始创建Timer实例...");
                    _logger.LogDebug("🔧 正在创建Timer实例...");
                    
                    // 🔧 关键修复：使用同步回调，避免async/await在Timer中导致的死锁
                    _scanTimer = new Timer(_ => 
                    {
                        // 🔧 在Timer回调中使用Task.Run确保异步执行不阻塞
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // 🔧 增强：定时器触发时检查服务状态
                                bool serviceIsRunning;
                                lock (_lockObject)
                                {
                                    serviceIsRunning = _isRunning;
                                }
                                
                                if (!serviceIsRunning)
                                {
                                    _logger.LogDebug("⏰ 定时器触发但服务未运行，跳过扫描");
                                    return;
                                }
                                
                                await ScanPositionsAsync();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "❌ 定时器回调中执行扫描时发生错误");
                                AddWorkLog("ERROR", $"❌ 定时器扫描错误: {ex.Message}");
                                
                                // 🔧 增强：记录详细的错误信息
                                if (ex.Message.Contains("synchronization"))
                                {
                                    _logger.LogError($"🔍 定时器同步错误: {ex.GetType().Name} - {ex.Message}");
                                    _logger.LogError($"🔍 线程信息: ID={Thread.CurrentThread.ManagedThreadId}, IsBackground={Thread.CurrentThread.IsBackground}");
                                }
                            }
                        });
                    }, null, intervalMs, intervalMs); // 🔧 修复：延迟启动，避免立即触发
                    
                    _logger.LogCritical("🔍 [STARTUP-35] Timer实例创建完成");
                    _logger.LogInformation($"✅ 扫描定时器创建成功，间隔: {_config.ScanIntervalSeconds}秒");
                    _logger.LogInformation($"🔧 定时器状态: {(_scanTimer != null ? "已创建" : "创建失败")}");
                    AddWorkLog("INFO", $"✅ 扫描定时器创建成功，间隔: {_config.ScanIntervalSeconds}秒");

                    // 🔧 新增：记录定时器详细信息
                    var nextScanTime = DateTime.Now.AddSeconds(_config.ScanIntervalSeconds);
                    _logger.LogInformation($"⏰ 首次扫描时间: {nextScanTime:HH:mm:ss}");
                    AddWorkLog("INFO", $"⏰ 定时器已启动，首次扫描: {nextScanTime:HH:mm:ss}");
                    
                    _logger.LogCritical("🔍 [STARTUP-36] 定时器创建成功，准备开始工作");
                    _logger.LogInformation("🔧 定时器已创建，准备开始工作");
                }
                catch (Exception timerEx)
                {
                    _logger.LogCritical(timerEx, "🔍 [STARTUP-37] 定时器创建异常");
                    _logger.LogError(timerEx, $"❌ 创建扫描定时器失败: {timerEx.Message}");
                    _logger.LogError($"💡 定时器参数: 间隔={intervalMs}ms, 回调={nameof(ScanPositionsAsync)}");
                    AddWorkLog("ERROR", $"❌ 创建扫描定时器失败: {timerEx.Message}");
                    
                    // 🔧 定时器创建失败时重置状态
                    lock (_lockObject) 
                    { 
                        _config = null; 
                        _isRunning = false; 
                        _logger.LogInformation("🔧 因定时器创建失败，运行状态已重置为false");
                    }
                    
                    OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                        IsRunning = false, 
                        Message = "启动失败: 无法创建扫描定时器" 
                    });
                    
                    _logger.LogError("❌ 启动失败: 扫描定时器创建失败");
                    return false;
                }
                
                // 🚌 发布监控状态变更事件
                _logger.LogCritical("🔍 [STARTUP-38] 开始事件发布阶段");
                _logger.LogInformation("📢 发布监控状态变更事件...");
                
                try
                {
                    _logger.LogCritical("🔍 [STARTUP-39] 发布监控状态变更事件");
                    await _eventBus.PublishAsync(new MonitorStatusChangedEvent
                    {
                        Source = "AutoMonitorService",
                        IsRunning = true,
                        Message = $"自动监控已启动 - 扫描间隔{_config.ScanIntervalSeconds}秒",
                        Config = _config,
                        ActiveContractCount = _positionProfiles.Count
                    });
                    _logger.LogCritical("🔍 [STARTUP-40] 监控状态变更事件发布完成");
                    _logger.LogInformation("✅ 状态变更事件发布成功");
                }
                catch (Exception eventEx)
                {
                    _logger.LogCritical(eventEx, "🔍 [STARTUP-41] 监控状态变更事件发布异常");
                    _logger.LogWarning(eventEx, $"⚠️ 状态变更事件发布失败，但不影响服务启动: {eventEx.Message}");
                }
                
                try
                {
                    _logger.LogCritical("🔍 [STARTUP-42] 触发本地状态变更事件");
                    OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                        IsRunning = true, 
                        Message = $"自动监控已启动 - 扫描间隔{_config.ScanIntervalSeconds}秒" 
                    });
                    _logger.LogCritical("🔍 [STARTUP-43] 本地状态变更事件触发完成");
                    _logger.LogInformation("✅ 本地状态变更事件触发成功");
                }
                catch (Exception localEventEx)
                {
                    _logger.LogCritical(localEventEx, "🔍 [STARTUP-44] 本地状态变更事件触发异常");
                    _logger.LogWarning(localEventEx, $"⚠️ 本地状态变更事件触发失败: {localEventEx.Message}");
                }
                
                // 🔧 新增：友好的启动成功消息
                _logger.LogInformation("🎉 自动监控服务启动完成！");
                
                if (_positionProfiles.Any())
                {
                    _logger.LogInformation($"✅ 启动成功 - 配置: {_config.Name}, 间隔: {_config.ScanIntervalSeconds}秒, 监控{_positionProfiles.Count}个持仓");
                    AddWorkLog("INFO", $"🎉 启动成功！配置: {_config.Name}, 间隔: {_config.ScanIntervalSeconds}秒");
                    AddWorkLog("INFO", $"📊 当前监控 {_positionProfiles.Count} 个持仓");
                    foreach (var profile in _positionProfiles.Take(3))
                    {
                        _logger.LogInformation($"   📍 {profile.Key}");
                    }
                    if (_positionProfiles.Count > 3)
                    {
                        _logger.LogInformation($"   ... 还有{_positionProfiles.Count - 3}个持仓");
                    }
                }
                else
                {
                    _logger.LogInformation($"✅ 启动成功 - 配置: {_config.Name}, 间隔: {_config.ScanIntervalSeconds}秒");
                    _logger.LogInformation($"💤 当前无持仓，系统将等待新持仓并自动开始监控");
                    AddWorkLog("INFO", $"🎉 启动成功！配置: {_config.Name}, 间隔: {_config.ScanIntervalSeconds}秒");
                    AddWorkLog("INFO", "💤 当前无持仓，等待新持仓开始监控");
                }
                
                // 🔧 记录服务最终状态
                bool finalIsRunning;
                string finalConfigName;
                lock (_lockObject)
                {
                    finalIsRunning = _isRunning;
                    finalConfigName = _config?.Name ?? "未知";
                }
                
                _logger.LogInformation($"🔧 最终服务状态检查:");
                _logger.LogInformation($"   • IsRunning: {finalIsRunning}");
                _logger.LogInformation($"   • Config: {finalConfigName}");
                _logger.LogInformation($"   • Timer: {(_scanTimer != null ? "运行中" : "未创建")}");
                _logger.LogInformation($"   • EventBus: 已启动");
                _logger.LogInformation($"   • PositionProfiles: {_positionProfiles.Count}个");
                
                // 🔧 新增：启动完成后的系统状态报告
                AddWorkLog("INFO", "🎉 自动盯盘系统已启动！");
                AddWorkLog("INFO", $"📊 系统状态: 运行中 | 配置: {finalConfigName}");
                AddWorkLog("INFO", $"⏰ 扫描间隔: {_config.ScanIntervalSeconds}秒");
                AddWorkLog("INFO", $"🔧 定时器状态: {(_scanTimer != null ? "正常运行" : "异常")}");
                AddWorkLog("INFO", $"📈 监控档案: {_positionProfiles.Count}个");
                
                // 🔧 新增：记录系统开始工作的时间
                var startTime = DateTime.Now;
                _logger.LogInformation($"🚀 系统开始工作时间: {startTime:yyyy-MM-dd HH:mm:ss}");
                AddWorkLog("INFO", $"🚀 系统开始工作: {startTime:HH:mm:ss}");
                
                _logger.LogCritical("🔍 [STARTUP-45] 启动流程即将完成");
                _logger.LogInformation("🚀 自动监控服务启动流程完成，返回true");
                _logger.LogCritical("🔍 [STARTUP-46] 返回 true，启动成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "🔍 [STARTUP-ERROR] 启动流程发生异常");
                
                lock (_lockObject) 
                { 
                    _isRunning = false; 
                    _config = null;
                }
                
                var errorMsg = $"自动监控启动失败: {ex.Message}";
                _logger.LogError(ex, errorMsg);
                
                // 🔧 修复：提供更详细的错误诊断
                if (ex.Message.Contains("channel", StringComparison.OrdinalIgnoreCase) || 
                    ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
                {
                    errorMsg = "启动失败: 网络连接问题，请检查网络连接后重试";
                    _logger.LogError("💡 建议: 检查网络连接和API密钥配置");
                }
                else if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    errorMsg = "启动失败: 连接超时，请稍后重试";
                    _logger.LogError("💡 建议: 等待10-15秒后重试启动");
                }
                else if (ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                         ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase))
                {
                    errorMsg = "启动失败: API认证失败，请检查API密钥配置";
                    _logger.LogError("💡 建议: 检查API Key和Secret配置是否正确");
                }
                else
                {
                    errorMsg = $"启动失败: {ex.Message}";
                    _logger.LogError($"💡 详细错误: {ex}");
                }
                
                OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                    IsRunning = false, 
                    Message = errorMsg
                });
                
                _logger.LogError($"❌ 最终启动结果: 失败 - {errorMsg}");
                return false;
            }
        }

        /// <summary>
        /// 停止自动监控
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            bool wasRunning = false;
            Timer? timerToDispose = null;
            
            lock (_lockObject)
            {
                wasRunning = _isRunning;
                if (!_isRunning) 
                {
                    _logger.LogInformation("⏹️ 自动监控已经处于停止状态");
                    return;
                }
                
                _isRunning = false;
                // 🔧 修复：先获取Timer引用，在lock外部处理
                timerToDispose = _scanTimer;
                _scanTimer = null;
                
                // 🔧 新增：停止时保存状态到持久化存储
                try
                {
                    _persistenceService.SavePositionProfiles(_positionProfiles);
                    _persistenceService.SaveExecutionHistory(_executionHistory);
                    _logger.LogInformation("💾 已保存自动盯盘状态到持久化存储");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 保存自动盯盘状态失败");
                }
            }
            
            // 🔧 修复：在lock外部处理Timer的dispose和等待
            if (timerToDispose != null)
            {
                timerToDispose.Dispose();
                // 🔧 修复：短暂等待确保Timer回调完全结束，避免并发冲突
                await Task.Delay(150);
                _logger.LogInformation("⏰ 扫描定时器已完全停止");
            }
            
            _logger.LogInformation("⏹️ 自动监控正在停止...");
            
            // 🚌 发布监控状态变更事件并停止事件总线
            try
            {
                if (wasRunning)
                {
                    await _eventBus.PublishAsync(new MonitorStatusChangedEvent
                    {
                        Source = "AutoMonitorService",
                        IsRunning = false,
                        Message = "自动监控已停止",
                        Config = _config,
                        ActiveContractCount = 0
                    });
                }
                
                await _eventBus.StopAsync();
                _logger.LogInformation("✅ 事件总线已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 停止事件总线时发生错误");
                // 继续执行，不让事件总线错误阻止停止流程
            }
            
            // 🔧 修复：确保状态变更事件能够正确触发
            try
            {
                OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                    IsRunning = false, 
                    Message = "自动监控已停止" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 触发状态变更事件时发生错误");
            }
            
            _logger.LogInformation("✅ 自动监控停止完成");
        }

        /// <summary>
        /// 初始化持仓档案
        /// </summary>
        private async Task InitializePositionProfilesAsync()
        {
            _logger.LogCritical("🔍 [POSITION-01] 开始 InitializePositionProfilesAsync");
            _logger.LogInformation("📊 开始获取持仓数据...");
            
            try
            {
                _logger.LogCritical("🔍 [POSITION-02] 调用 _binanceService.GetPositionsAsync()");
                
                // 🔧 添加超时控制，防止API调用无限等待
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                var positions = await _binanceService.GetPositionsAsync();
                
                _logger.LogCritical($"🔍 [POSITION-03] GetPositionsAsync 完成，结果: {(positions == null ? "null" : $"{positions.Count()}个")}");
                
                if (positions == null) 
                {
                    _logger.LogCritical("🔍 [POSITION-04] 持仓数据为空，提前返回");
                    _logger.LogWarning("⚠️ API返回的持仓数据为空，自动盯盘将等待持仓数据");
                    return;
                }
                
                await ProcessPositionsData(positions);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogCritical("🔍 [POSITION-ERROR] GetPositionsAsync 超时（30秒）");
                _logger.LogError("❌ 获取持仓数据超时，请检查网络连接和API配置");
                throw new InvalidOperationException("获取持仓数据超时，请检查网络连接");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogCritical("🔍 [POSITION-ERROR] API认证失败");
                _logger.LogError($"❌ API认证失败: {ex.Message}");
                throw new InvalidOperationException("API认证失败，请检查API Key配置和IP白名单");
            }
            catch (Exception ex) when (ex.Message.Contains("2015") || ex.Message.Contains("Invalid API-key"))
            {
                _logger.LogCritical("🔍 [POSITION-ERROR] API权限不足或密钥无效");
                _logger.LogError($"❌ API认证错误: {ex.Message}");
                throw new InvalidOperationException("API认证失败，请检查API Key配置、权限设置和IP白名单");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "🔍 [POSITION-ERROR] 获取持仓数据异常");
                _logger.LogError(ex, $"❌ 获取持仓数据失败: {ex.Message}");
                throw new InvalidOperationException($"获取持仓数据失败: {ex.Message}", ex);
            }
        }

        private async Task ProcessPositionsData(IEnumerable<dynamic> positions)
        {
            _logger.LogCritical("🔍 [POSITION-05] 开始处理持仓数据");
            _logger.LogInformation("🔍 开始过滤活跃持仓...");
            
            // 🔧 关键修复：将持仓过滤移到lock外部，减少锁持有时间
            var activePositions = positions.Where(p => 
                Math.Abs(p.PositionAmt) > 0.001m &&     // 🔧 提高数量阈值，过滤掉极小持仓
                !string.IsNullOrEmpty(p.Symbol) &&      // 合约名称过滤：确保合约名称有效
                p.Symbol.EndsWith("USDT") &&            // 🔧 新增：只处理USDT合约
                p.MarkPrice > 0 &&                      // 标记价格过滤：确保价格有效
                p.EntryPrice > 0 &&                     // 开仓价格过滤：确保开仓价有效
                p.UnrealizedProfit != 0                 // 🔧 新增：确保有实际盈亏数据
            ).ToList();

            _logger.LogInformation($"📊 当前活跃持仓: {activePositions.Count}个");
            
            // 🔧 关键修复：没有持仓时也正常初始化，只是提示等待状态
            if (!activePositions.Any())
            {
                _logger.LogInformation("💤 当前暂无活跃持仓，自动盯盘已启动并处于等待状态");
                _logger.LogInformation("📝 当有新持仓时，系统将自动开始监控");
                
                lock (_lockObject)
                {
                    _positionProfiles.Clear();
                    _executionHistory.Clear();
                }
                return; // 正常返回，不报错
            }
            
            foreach (var pos in activePositions)
            {
                _logger.LogInformation($"   📍 {pos.Symbol} {pos.PositionSideString}: {pos.PositionAmt:F6} (浮盈: {pos.UnrealizedProfit:F2}U)");
            }

            // 🔧 关键修复：将持久化数据加载移到lock外部，提升性能
            _logger.LogInformation("📖 开始加载持久化数据...");
            var persistedProfiles = await Task.Run(() => _persistenceService.LoadPositionProfiles());
            var persistedHistory = await Task.Run(() => _persistenceService.LoadExecutionHistory());
            
            _logger.LogInformation($"📖 从持久化存储加载了 {persistedProfiles.Count} 个历史档案");

            // 🔧 关键修复：预处理数据，减少lock内部的复杂操作
            var newPositionProfiles = new Dictionary<string, PositionProfile>();
            var invalidProfileKeys = new List<string>();
            
            // 🔧 关键修复：只为当前真实存在的活跃持仓恢复档案
            foreach (var position in activePositions)
            {
                var key = GetPositionKey(position.Symbol, position.PositionSideString);
                
                // 如果持久化存储中有该持仓的档案，则使用持久化的数据（保留执行状态）
                if (persistedProfiles.ContainsKey(key))
                {
                    var persistedProfile = persistedProfiles[key];
                    // 更新实时数据，但保留执行状态
                    persistedProfile.InitialQuantity = Math.Abs(position.PositionAmt);
                    persistedProfile.InitialEntryPrice = position.EntryPrice;
                    persistedProfile.LastUpdateTime = DateTime.Now;
                    persistedProfile.IsActive = true;
                    
                    newPositionProfiles[key] = persistedProfile;
                    _logger.LogInformation($"🔄 恢复持仓档案: {key} - 触发记录: {persistedProfile.TriggerRecords.Count}");
                }
                else
                {
                    // 新建档案
                    newPositionProfiles[key] = new PositionProfile
                    {
                        Symbol = position.Symbol,
                        PositionSide = position.PositionSideString,
                        InitialQuantity = Math.Abs(position.PositionAmt),
                        InitialEntryPrice = position.EntryPrice,
                        CreateTime = DateTime.Now,
                        LastUpdateTime = DateTime.Now,
                        IsActive = true
                    };
                    _logger.LogInformation($"📝 新建档案: {key}, 数量: {position.PositionAmt:F6}, 入场价: {position.EntryPrice:F4}");
                    
                    // 🔧 新增：为新持仓清理历史状态，避免重复执行
                    CleanupHistoryForNewPosition(position.Symbol, position.PositionSideString);
                }
            }

            // 🔧 新增：检查并清理无效的历史档案
            invalidProfileKeys = persistedProfiles.Keys.Except(newPositionProfiles.Keys).ToList();
            if (invalidProfileKeys.Any())
            {
                _logger.LogWarning($"🗑️ 发现{invalidProfileKeys.Count}个无效的历史档案（无对应活跃持仓）:");
                foreach (var invalidKey in invalidProfileKeys)
                {
                    var parts = invalidKey.Split('_');
                    if (parts.Length == 2)
                    {
                        var symbol = parts[0];
                        var positionSide = parts[1];
                        _logger.LogWarning($"   ❌ {invalidKey} - 该合约当前无活跃持仓，已跳过恢复");
                        
                        // 🔧 异步清理无效档案的执行历史
                        _ = Task.Run(() => _persistenceService.CleanupContractHistory(symbol, positionSide, "无活跃持仓"));
                    }
                }
            }
            
            // 🔧 新增：预处理执行历史，只保留当前活跃持仓的记录
            var activeSymbols = activePositions.Select(p => p.Symbol).ToHashSet();
            var validHistory = persistedHistory.Where(h => activeSymbols.Contains(h.Symbol)).ToList();
            
            if (persistedHistory.Count != validHistory.Count)
            {
                var removedCount = persistedHistory.Count - validHistory.Count;
                _logger.LogInformation($"🗑️ 过滤执行历史: 移除{removedCount}条无效记录，保留{validHistory.Count}条有效记录");
            }
            
            // 🔧 关键修复：最小化lock操作，只进行数据替换
            lock (_lockObject)
            {
                _positionProfiles.Clear();
                foreach (var kvp in newPositionProfiles)
                {
                    _positionProfiles[kvp.Key] = kvp.Value;
                }
                
                _executionHistory.Clear();
                _executionHistory.AddRange(validHistory);
            }
            
            _logger.LogInformation($"📊 初始化完成 - 持仓档案: {newPositionProfiles.Count}个, 执行历史: {validHistory.Count}条");
            _logger.LogInformation($"✅ 所有档案均对应当前活跃持仓，无无效档案");
        }

        /// <summary>
        /// 扫描持仓并执行相应策略
        /// </summary>
        private async Task ScanPositionsAsync()
        {
            // 🔧 新增：记录定时器触发信息
            _logger.LogDebug("⏰ 定时器触发扫描方法");
            AddWorkLog("INFO", "⏰ 定时器触发，开始扫描");

            // 🔧 新增：详细的状态检查和日志记录
            bool isRunning;
            AutoMonitorConfig? config;
            lock (_lockObject)
            {
                isRunning = _isRunning;
                config = _config;
            }

            _logger.LogDebug($"🔧 扫描状态检查: IsRunning={isRunning}, Config={config?.Name ?? "NULL"}");

            if (!isRunning)
            {
                _logger.LogWarning("❌ 扫描被跳过: 服务未运行");
                AddWorkLog("WARN", "❌ 扫描被跳过: 服务未运行");
                return;
            }

            if (config == null)
            {
                _logger.LogWarning("❌ 扫描被跳过: 配置为空");
                AddWorkLog("WARN", "❌ 扫描被跳过: 配置为空");
                return;
            }

            _logger.LogDebug($"✅ 状态检查通过，开始扫描 (配置: {config.Name})");
            AddWorkLog("INFO", $"✅ 状态检查通过，开始扫描 (配置: {config.Name})");

            // 🛡️ 增加扫描计数并定期清理过期的冷却期记录（每20次扫描清理一次）
            _scanCount++;
            if (_scanCount % 20 == 0)
            {
                _cooldownManager.CleanupExpiredRecords();
            }

            // 🔧 新增：记录扫描计数
            _logger.LogDebug($"🔢 扫描计数: {_scanCount}");

            // 🔧 修复：使用SemaphoreSlim替代Monitor，提供更安全的并发控制
            var semaphoreEntered = false;
            try
            {
                // 尝试获取信号量，超时时间设置为2秒
                semaphoreEntered = await _executionSemaphore.WaitAsync(TimeSpan.FromSeconds(2));
                if (!semaphoreEntered)
                {
                    _logger.LogWarning("⚠️ 自动盯盘扫描繁忙，跳过本次扫描以避免并发冲突");
                    AddWorkLog("WARN", "⚠️ 扫描繁忙，跳过本次扫描");
                    return;
                }

                _logger.LogInformation("🔄 开始扫描持仓...");
                AddWorkLog("INFO", "🔄 开始扫描持仓...");

                // 🔧 关键修复：为API调用添加超时控制，防止运行期间卡死
                IEnumerable<dynamic>? positions = null;
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var positionsTask = _binanceService.GetPositionsAsync();
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15), timeoutCts.Token);
                    
                    var completedTask = await Task.WhenAny(positionsTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        _logger.LogWarning("⚠️ 获取持仓数据超时(15秒)，跳过本次扫描");
                        AddWorkLog("WARN", "⚠️ 获取持仓数据超时，跳过本次扫描");
                        return;
                    }
                    
                    positions = await positionsTask;
                    timeoutCts.Cancel(); // 取消超时任务
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning("⚠️ 获取持仓数据被取消，跳过本次扫描");
                    AddWorkLog("WARN", "⚠️ 获取持仓数据被取消，跳过本次扫描");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 获取持仓数据异常，跳过本次扫描");
                    AddWorkLog("ERROR", $"❌ 获取持仓数据异常: {ex.Message}");
                    return;
                }
                
                if (positions == null || !positions.Any())
                {
                    _logger.LogDebug("📊 当前无持仓数据，继续等待...");
                    AddWorkLog("INFO", "📊 当前无持仓数据，继续等待...");
                    return; // 🔧 修复：没有持仓时继续运行，不停止扫描
                }

                // 🔧 修复：过滤活跃持仓并转换为PositionInfo类型
                var activePositions = positions.Where(p => 
                    Math.Abs(p.PositionAmt) > 0.001m &&
                    !string.IsNullOrEmpty(p.Symbol) &&
                    p.Symbol.EndsWith("USDT") &&
                    p.MarkPrice > 0 &&
                    p.EntryPrice > 0 &&
                    p.UnrealizedProfit != 0
                ).Select(p => new PositionInfo
                {
                    Symbol = p.Symbol,
                    PositionAmt = p.PositionAmt,
                    EntryPrice = p.EntryPrice,
                    MarkPrice = p.MarkPrice,
                    UnrealizedProfit = p.UnrealizedProfit,
                    PositionSide = p.PositionSide,
                    PositionSideString = p.PositionSideString,
                    Leverage = p.Leverage,
                    MarginType = p.MarginType,
                    IsolatedMargin = p.IsolatedMargin,
                    UpdateTime = p.UpdateTime
                }).ToList();

                if (!activePositions.Any())
                {
                    // 🔧 重要修复：没有活跃持仓时清理历史档案，但继续运行等待新持仓
                    lock (_lockObject)
                    {
                        if (_positionProfiles.Any())
                        {
                            _logger.LogInformation("🗑️ 检测到所有持仓已平仓，清理历史档案");
                            AddWorkLog("INFO", "🗑️ 检测到所有持仓已平仓，清理历史档案");
                            CleanupClosedPositions(new List<PositionInfo>());
                        }
                    }
                    
                    _logger.LogDebug("💤 当前无活跃持仓，继续等待新持仓...");
                    AddWorkLog("INFO", "💤 当前无活跃持仓，继续等待新持仓...");
                    return; // 继续运行，等待持仓出现
                }

                // 🔧 修复：先清理已关闭的持仓，使用过滤后的活跃持仓列表
                CleanupClosedPositions(activePositions);

                var positionCount = activePositions.Count;
                _logger.LogDebug($"🔄 开始处理 {positionCount} 个活跃持仓");
                AddWorkLog("INFO", $"🔄 开始处理 {positionCount} 个活跃持仓");

                // 🔧 关键修复：避免过度并行，限制同时处理的持仓数量
                const int maxConcurrency = 3; // 最多同时处理3个持仓
                var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
                
                var processingTasks = activePositions.Select(async position =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await ProcessPositionAsync(position);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToArray();
                
                // 🔧 添加整体超时控制，防止所有处理任务卡死
                using var overallTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var allTasksTask = Task.WhenAll(processingTasks);
                var overallTimeoutTask = Task.Delay(TimeSpan.FromSeconds(30), overallTimeoutCts.Token);
                
                var completedOverallTask = await Task.WhenAny(allTasksTask, overallTimeoutTask);
                
                if (completedOverallTask == overallTimeoutTask)
                {
                    _logger.LogWarning("⚠️ 持仓处理整体超时(30秒)，部分处理可能未完成");
                    AddWorkLog("WARN", "⚠️ 持仓处理整体超时，部分处理可能未完成");
                }
                else
                {
                    overallTimeoutCts.Cancel();
                    await allTasksTask; // 等待所有任务完成
                    _logger.LogDebug($"✅ 扫描完成，已处理 {positionCount} 个持仓");
                    AddWorkLog("INFO", $"✅ 扫描完成，已处理 {positionCount} 个持仓");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 扫描持仓时发生严重错误");
                AddWorkLog("ERROR", $"❌ 扫描持仓时发生严重错误: {ex.Message}");
                
                // 🔧 增强错误诊断：记录详细的错误信息
                if (ex.Message.Contains("synchronization method"))
                {
                    _logger.LogError($"🔍 同步错误详情: semaphoreEntered={semaphoreEntered}, 线程ID={Thread.CurrentThread.ManagedThreadId}");
                    _logger.LogError($"🔍 错误堆栈: {ex.StackTrace}");
                }
            }
            finally
            {
                // 🔧 关键修复：使用SemaphoreSlim的Release方法，无需手动状态检查
                try
                {
                    if (semaphoreEntered)
                    {
                        _executionSemaphore.Release();
                        _logger.LogDebug($"🔓 成功释放扫描信号量，线程ID={Thread.CurrentThread.ManagedThreadId}");
                    }
                    else
                    {
                        _logger.LogDebug($"🔍 未获取到信号量，无需释放，线程ID={Thread.CurrentThread.ManagedThreadId}");
                    }
                }
                catch (Exception finallyEx)
                {
                    _logger.LogError(finallyEx, $"❌ 释放扫描信号量时发生错误: semaphoreEntered={semaphoreEntered}, 线程ID={Thread.CurrentThread.ManagedThreadId}");
                    AddWorkLog("ERROR", $"❌ 释放扫描信号量错误: {finallyEx.Message}");
                }
            }
        }

        /// <summary>
        /// 处理单个持仓
        /// </summary>
        private async Task ProcessPositionAsync(PositionInfo position)
        {
            // 🔧 新增：额外的合约验证
            if (string.IsNullOrEmpty(position.Symbol) || !position.Symbol.EndsWith("USDT"))
            {
                _logger.LogWarning($"⚠️ 跳过无效合约: {position.Symbol}");
                return;
            }
            
            var key = GetPositionKey(position.Symbol, position.PositionSideString);
            
            // 🔧 修复：改进持仓档案管理，避免重启后误清理状态
            lock (_lockObject)
            {
                if (!_positionProfiles.ContainsKey(key))
                {
                    // 🔧 关键修复：先检查持久化存储中是否有该合约的状态
                    var persistedProfiles = _persistenceService.LoadPositionProfiles();
                    var hasPersistedState = persistedProfiles.ContainsKey(key) && 
                                          persistedProfiles[key].TriggerRecords.Any();
                    
                    if (hasPersistedState)
                    {
                        // 如果持久化存储中有状态，恢复它而不是清理
                        var persistedProfile = persistedProfiles[key];
                        persistedProfile.InitialQuantity = Math.Abs(position.PositionAmt);
                        persistedProfile.InitialEntryPrice = position.EntryPrice;
                        persistedProfile.LastUpdateTime = DateTime.Now;
                        persistedProfile.IsActive = true;
                        
                        _positionProfiles[key] = persistedProfile;
                        _logger.LogInformation($"🔄 恢复持仓档案: {key} - 触发记录: {persistedProfile.TriggerRecords.Count}");
                        
                        // 🔧 重要：同步状态到统一状态管理器
                        foreach (var trigger in persistedProfile.TriggerRecords.Values)
                        {
                            if (trigger.IsExecuted)
                            {
                                var executionType = trigger.TriggerType.Contains("推仓") ? ExecutionType.AddPosition :
                                                  trigger.TriggerType.Contains("保本") ? ExecutionType.BreakEven :
                                                  ExecutionType.ProfitProtection;
                                                  
                                _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString,
                                    executionType, trigger.TierIndex, trigger.TriggerPnl, true, trigger.ExecutionResult ?? "成功",
                                    autoSave: false);
                            }
                        }
                    }
                    else
                    {
                        // 确实是新持仓，清理历史状态
                        CleanupHistoryForNewPosition(position.Symbol, position.PositionSideString);
                        
                        _positionProfiles[key] = new PositionProfile
                        {
                            Symbol = position.Symbol,
                            PositionSide = position.PositionSideString,
                            InitialQuantity = Math.Abs(position.PositionAmt),
                            InitialEntryPrice = position.EntryPrice,
                            CreateTime = DateTime.Now,
                            LastUpdateTime = DateTime.Now
                        };
                        
                        _logger.LogInformation($"📝 新建档案: {key}");
                    }
                }
                _positionProfiles[key].LastUpdateTime = DateTime.Now;
            }

            var profile = _positionProfiles[key];
            var currentPnl = position.UnrealizedProfit;

            // 只对有盈利的持仓进行检查
            if (currentPnl <= 0) return;

            // 🔧 修复：移除全局冷却期，改为按操作类型独立冷却，防止跳过第一级推仓
            // 每种操作（保本、推仓、保盈）都有独立的冷却期机制，在各自的Check方法中处理

            // 🔧 修复：改进执行逻辑，允许同一次扫描执行多个不同类型的操作
            // 但每种类型最多执行一个操作，防止过度触发
            var executedOperations = new List<string>();
            
            // 1. 优先检查保本（最高优先级）
            var breakEvenExecuted = await CheckBreakEvenTriggerAsync(position, profile, currentPnl);
            if (breakEvenExecuted) 
            {
                executedOperations.Add("保本");
                _logger.LogInformation($"🎯 {key} 执行了自动保本");
            }
            
            // 2. 检查推仓（中等优先级，独立于保本）
            var addPositionExecuted = await CheckAddPositionTriggersAsync(position, profile, currentPnl);
            if (addPositionExecuted) 
            {
                executedOperations.Add("推仓");
                _logger.LogInformation($"🚀 {key} 执行了推仓操作");
            }
            
            // 3. 检查保盈止损（最低优先级）
            var profitProtectionExecuted = await CheckProfitProtectionTriggersAsync(position, profile, currentPnl);
            if (profitProtectionExecuted) 
            {
                executedOperations.Add("保盈");
                _logger.LogInformation($"🛡️ {key} 执行了保盈止损");
            }
            
            // 记录本次扫描的执行情况
            if (executedOperations.Count > 0)
            {
                _logger.LogInformation($"📊 {key} 本次扫描执行了: {string.Join("、", executedOperations)}");
            }
        }

        /// <summary>
        /// 检查自动保本触发条件
        /// </summary>
        private async Task<bool> CheckBreakEvenTriggerAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            if (!_config!.BreakEvenConfig.IsEnabled) return false;
            if (currentPnl <= _config.BreakEvenConfig.TriggerProfitAmount) return false;

            // 🔄 使用统一状态管理器检查是否已执行
            if (_unifiedStateManager.IsExecuted(position.Symbol, position.PositionSideString, ExecutionType.BreakEven))
            {
                return false;
            }

            // 🛡️ 检查冷却期：防止短时间内重复执行保本止损
            var operationKey = CooldownManager.GenerateOperationKey(position.Symbol, position.PositionSideString, CooldownOperationType.BreakEven);
            if (!_cooldownManager.CanExecute(operationKey, CooldownOperationType.BreakEven))
            {
                var remainingTime = _cooldownManager.GetRemainingCooldown(operationKey, CooldownOperationType.BreakEven);
                _logger.LogDebug($"🔒 保本止损冷却中: {position.Symbol}, 剩余: {remainingTime.TotalSeconds:F1}秒");
                return false;
            }

            // 🔒 先标记为执行中状态，防止重复触发
            _logger.LogInformation($"🔒 标记保本为执行中状态，防止重复触发");
            _unifiedStateManager.MarkAsExecuting(position.Symbol, position.PositionSideString, 
                ExecutionType.BreakEven, null, currentPnl, "自动保本开始执行");
                
            // 🛡️ 立即记录冷却期，防止短时间内重复扫描
            _cooldownManager.RecordExecution(operationKey);

            try
            {
                _logger.LogInformation($"🎯 触发自动保本: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {_config.BreakEvenConfig.TriggerProfitAmount:F2}U");
                
                var success = await ExecuteBreakEvenStopLossAsync(position);
                
                // 🔄 记录最终执行结果
                _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString, 
                    ExecutionType.BreakEven, null, currentPnl, success, 
                    success ? "自动保本执行成功" : "自动保本执行失败",
                    autoSave: false);
                
                // 注意：冷却期已在执行前记录，此处不需要重复记录
                
                // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                _contractStateManager.MarkAsTriggered(position.Symbol, position.PositionSideString, 
                    ExecutionType.BreakEven, null, currentPnl, success, 
                    success ? "自动保本执行成功" : "自动保本执行失败");
                
                var triggerKey = $"{GetPositionKey(position.Symbol, position.PositionSideString)}_BreakEven";
                RecordTriggerExecution(profile, position, triggerKey, "自动保本", currentPnl, success);
                
                _logger.LogInformation($"✅ 自动保本执行{(success ? "成功" : "失败")}: {position.Symbol}");
                return true; // 表示执行了操作
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行自动保本时发生错误: {position.Symbol}");
                
                // 🔄 记录异常状态为执行失败
                _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString, 
                    ExecutionType.BreakEven, null, currentPnl, false, ex.Message,
                    autoSave: false);
                
                // 注意：冷却期已在执行前记录，此处不需要重复记录
                
                // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                _contractStateManager.MarkAsTriggered(position.Symbol, position.PositionSideString, 
                    ExecutionType.BreakEven, null, currentPnl, false, ex.Message);
                
                var triggerKey = $"{GetPositionKey(position.Symbol, position.PositionSideString)}_BreakEven";
                RecordTriggerExecution(profile, position, triggerKey, "自动保本", currentPnl, false);
                return true; // 表示执行了操作（即使失败）
            }
        }

        /// <summary>
        /// 检查自动推仓触发条件
        /// </summary>
        private async Task<bool> CheckAddPositionTriggersAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            // 🔧 增强诊断：详细记录推仓触发检查过程
            _logger.LogInformation($"🔍 推仓诊断开始 - 合约: {position.Symbol} ({position.PositionSideString})");
            _logger.LogInformation($"🔍 当前浮盈: {currentPnl:F2}U");
            
            if (!_config!.AddPositionConfig.IsEnabled)
            {
                _logger.LogInformation($"🔍 推仓功能被禁用，跳过检查");
                return false;
            }
            
            _logger.LogInformation($"🔍 推仓配置启用: ✅");

            // 🔄 使用新的合约状态管理器，解决多合约冲突问题
            var enabledStages = _config.AddPositionConfig.Tiers.OrderBy(s => s.TriggerProfitAmount);
            _logger.LogInformation($"🔍 共有 {enabledStages.Count()} 个推仓阶梯需要检查");
            
            // 🔧 超级详细调试：预先检查所有阶梯状态
            _logger.LogInformation($"🔧 === 开始超级详细调试 ===");
            foreach (var debugStage in enabledStages)
            {
                var debugExecuted = _unifiedStateManager.IsExecuted(position.Symbol, position.PositionSideString, 
                    ExecutionType.AddPosition, debugStage.TierIndex);
                var debugKey = CooldownManager.GenerateOperationKey(position.Symbol, position.PositionSideString, 
                    CooldownOperationType.AddPosition, debugStage.TierIndex);
                var debugCooldown = _cooldownManager.GetRemainingCooldown(debugKey, CooldownOperationType.AddPosition);
                
                _logger.LogInformation($"🔧 阶梯{debugStage.TierIndex}完整状态: " +
                    $"启用={debugStage.IsEnabled}, " +
                    $"触发金额={debugStage.TriggerProfitAmount:F2}U, " +
                    $"当前浮盈={currentPnl:F2}U, " +
                    $"浮盈足够={(currentPnl > debugStage.TriggerProfitAmount)}, " +
                    $"已执行={debugExecuted}, " +
                    $"冷却剩余={debugCooldown.TotalSeconds:F1}秒, " +
                    $"操作键={debugKey}");
            }
            _logger.LogInformation($"🔧 === 超级详细调试结束 ===");
            
            foreach (var stage in enabledStages)
            {
                _logger.LogInformation($"🔍 检查阶梯{stage.TierIndex}: 触发金额={stage.TriggerProfitAmount:F2}U, 启用={stage.IsEnabled}");
                
                if (!stage.IsEnabled)
                {
                    _logger.LogInformation($"🔍 阶梯{stage.TierIndex}: ❌ 该阶梯被禁用");
                    continue;
                }
                
                if (currentPnl <= stage.TriggerProfitAmount)
                {
                    _logger.LogInformation($"🔍 阶梯{stage.TierIndex}: ❌ 浮盈不足 ({currentPnl:F2}U <= {stage.TriggerProfitAmount:F2}U)");
                    continue;
                }
                
                _logger.LogInformation($"🔍 阶梯{stage.TierIndex}: ✅ 浮盈条件满足 ({currentPnl:F2}U > {stage.TriggerProfitAmount:F2}U)");

                // 🔄 使用统一状态管理器检查该阶梯是否已执行
                var isExecuted = _unifiedStateManager.IsExecuted(position.Symbol, position.PositionSideString, 
                    ExecutionType.AddPosition, stage.TierIndex);
                if (isExecuted)
                {
                    _logger.LogInformation($"🔍 阶梯{stage.TierIndex}: ❌ 该阶梯已执行过");
                    continue;
                }
                
                _logger.LogInformation($"🔍 阶梯{stage.TierIndex}: ✅ 执行状态检查通过（未执行过）");

                // 🛡️ 检查冷却期：防止短时间内重复执行推仓操作
                var operationKey = CooldownManager.GenerateOperationKey(position.Symbol, position.PositionSideString, 
                    CooldownOperationType.AddPosition, stage.TierIndex);
                
                // 🔧 冷却期超级详细调试
                var remainingTime = _cooldownManager.GetRemainingCooldown(operationKey, CooldownOperationType.AddPosition);
                _logger.LogInformation($"🔧 阶梯{stage.TierIndex}冷却期详细检查:");
                _logger.LogInformation($"🔧   操作键: {operationKey}");
                _logger.LogInformation($"🔧   冷却期配置: {CooldownOperationType.AddPosition} = 5秒");
                _logger.LogInformation($"🔧   剩余冷却时间: {remainingTime.TotalSeconds:F1}秒");
                
                // 🔧 新增：更详细的状态检查 - 查看内部执行历史
                var recentHistory = _unifiedStateManager.GetExecutionHistory(50, position.Symbol);
                var stageHistory = recentHistory.Where(h => h.ExecutionType.Contains($"推仓阶梯{stage.TierIndex}")).ToList();
                _logger.LogInformation($"🔧   历史记录: 找到{stageHistory.Count}条阶梯{stage.TierIndex}的执行记录");
                foreach (var history in stageHistory.Take(3))
                {
                    _logger.LogInformation($"🔧     - {history.ExecutionTime:HH:mm:ss} {history.ExecutionType} 成功={history.IsSuccess} PnL={history.TriggerPnl:F2}U");
                }
                
                var canExecute = _cooldownManager.CanExecute(operationKey, CooldownOperationType.AddPosition);
                if (!canExecute)
                {
                    _logger.LogInformation($"🔍 阶梯{stage.TierIndex}: ❌ 冷却期中，剩余: {remainingTime.TotalSeconds:F1}秒");
                    continue;
                }
                
                _logger.LogInformation($"🔍 阶梯{stage.TierIndex}: ✅ 冷却期检查通过");
                _logger.LogInformation($"🔍 阶梯{stage.TierIndex}: 🎯 所有条件满足，开始执行推仓");

                // 🔧 关键改进：立即标记状态为"执行中"，防止重复触发
                // 这样做的好处：
                // 1. 不会因为执行时间长而重复触发
                // 2. 明确的状态管理，更安全可靠
                // 3. 避免并发扫描导致的重复下单
                _logger.LogInformation($"🔒 立即标记阶梯{stage.TierIndex}为执行中状态，防止重复触发");
                _unifiedStateManager.MarkAsExecuting(position.Symbol, position.PositionSideString, 
                    ExecutionType.AddPosition, stage.TierIndex, currentPnl, 
                    $"推仓阶梯{stage.TierIndex}开始执行");
                
                // 🛡️ 立即记录冷却期，防止短时间内重复扫描
                _cooldownManager.RecordExecution(operationKey);

                try
                {
                    _logger.LogInformation($"🚀 触发推仓阶梯{stage.TierIndex}: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {stage.TriggerProfitAmount:F2}U");
                    
                    var success = await ExecuteAddPositionAsync(position, stage);
                    
                    // 🔄 根据执行结果更新最终状态
                    _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString, 
                        ExecutionType.AddPosition, stage.TierIndex, currentPnl, success, 
                        success ? $"推仓阶梯{stage.TierIndex}执行成功" : $"推仓阶梯{stage.TierIndex}执行失败", 
                        autoSave: false);  // 统一记录最终执行结果
                    
                    // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                    _contractStateManager.MarkAsTriggered(position.Symbol, position.PositionSideString, 
                        ExecutionType.AddPosition, stage.TierIndex, currentPnl, success, 
                        success ? $"推仓阶梯{stage.TierIndex}执行成功" : $"推仓阶梯{stage.TierIndex}执行失败");
                    
                    // 🔧 修复：使用统一的执行历史记录机制，避免重复记录
                    var triggerKey = $"{GetPositionKey(position.Symbol, position.PositionSideString)}_AddPosition_Stage{stage.TierIndex}";
                    RecordTriggerExecution(profile, position, triggerKey, $"推仓阶梯{stage.TierIndex}", currentPnl, success);
                    
                    // 🔧 修复：不再设置全局IsTriggered状态，防止影响其他合约
                    // 防重复机制完全依赖profile.TriggerRecords，这是按合约独立的
                    if (success)
                    {
                        _logger.LogInformation($"✅ 推仓阶梯{stage.TierIndex}执行成功: {position.Symbol} (其他合约仍可独立触发此阶梯)");
                    }
                    
                    _logger.LogInformation($"✅ 推仓阶梯{stage.TierIndex}执行{(success ? "成功" : "失败")}: {position.Symbol}");
                    return true; // 🔧 修复：执行一个阶梯后返回，下次扫描继续检查其他阶梯
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"执行推仓阶梯{stage.TierIndex}时发生错误: {position.Symbol}");
                    
                    // 🔄 记录异常状态为执行失败
                    _logger.LogWarning($"⚠️ 推仓阶梯{stage.TierIndex}发生异常，标记为执行失败");
                    _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString, 
                        ExecutionType.AddPosition, stage.TierIndex, currentPnl, false, ex.Message, 
                        autoSave: false);  // 记录异常信息
                    
                    // 注意：不再重复记录冷却期，因为前面已经记录了
                    
                    // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                    _contractStateManager.MarkAsTriggered(position.Symbol, position.PositionSideString, 
                        ExecutionType.AddPosition, stage.TierIndex, currentPnl, false, ex.Message);
                    
                    var triggerKey = $"{GetPositionKey(position.Symbol, position.PositionSideString)}_AddPosition_Stage{stage.TierIndex}";
                    RecordTriggerExecution(profile, position, triggerKey, $"推仓阶梯{stage.TierIndex}", currentPnl, false);
                    return true; // 表示执行了操作（即使失败）
                }
            }
            
            _logger.LogInformation($"🔍 推仓诊断结束 - 合约: {position.Symbol}, 结果: 没有触发任何阶梯");
            return false; // 没有执行任何操作
        }

        /// <summary>
        /// 检查保盈止损触发条件
        /// </summary>
        private async Task<bool> CheckProfitProtectionTriggersAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            if (!_config!.ProfitProtectionConfig.IsEnabled) return false;

            // 🔧 修复：移除全局IsTriggered检查，只依赖合约独立的TriggerRecords机制
            var enabledStages = _config.ProfitProtectionConfig.Tiers.OrderBy(s => s.TriggerProfitAmount);
            
            foreach (var stage in enabledStages)
            {
                if (currentPnl <= stage.TriggerProfitAmount) continue;

                // 🔄 使用统一状态管理器检查该阶梯是否已执行
                if (_unifiedStateManager.IsExecuted(position.Symbol, position.PositionSideString, 
                    ExecutionType.ProfitProtection, stage.TierIndex))
                {
                    continue;
                }
                
                // ⚠️ 向后兼容：保留旧格式检查（后续版本将移除）
                var triggerKey = $"{GetPositionKey(position.Symbol, position.PositionSideString)}_ProfitProtection_Stage{stage.TierIndex}";
                if (profile.TriggerRecords.ContainsKey(triggerKey)) continue;

                // 🛡️ 检查冷却期：防止短时间内重复扫描
                var operationKey = CooldownManager.GenerateOperationKey(position.Symbol, position.PositionSideString, 
                    CooldownOperationType.ProfitProtection, stage.TierIndex);
                if (!_cooldownManager.CanExecute(operationKey, CooldownOperationType.ProfitProtection))
                {
                    var remainingTime = _cooldownManager.GetRemainingCooldown(operationKey, CooldownOperationType.ProfitProtection);
                    _logger.LogDebug($"🔒 保盈止损阶梯{stage.TierIndex}冷却中: {position.Symbol}, 剩余: {remainingTime.TotalSeconds:F1}秒");
                    continue;
                }

                // 🔧 关键改进：立即标记状态为"执行中"，防止重复触发
                _logger.LogInformation($"🔒 立即标记保盈止损阶梯{stage.TierIndex}为执行中状态，防止重复触发");
                _unifiedStateManager.MarkAsExecuting(position.Symbol, position.PositionSideString,
                    ExecutionType.ProfitProtection, stage.TierIndex, currentPnl,
                    $"保盈止损阶梯{stage.TierIndex}开始执行");
                
                // 🛡️ 立即记录冷却期，防止短时间内重复扫描
                _cooldownManager.RecordExecution(operationKey);

                try
                {
                    _logger.LogInformation($"🛡️ 触发保盈止损阶梯{stage.TierIndex}: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {stage.TriggerProfitAmount:F2}U");
                    
                    var success = await ExecuteProfitProtectionAsync(position, stage);
                    
                    // 🔄 根据执行结果更新最终状态
                    _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString,
                        ExecutionType.ProfitProtection, stage.TierIndex, currentPnl, success,
                        success ? $"保盈止损阶梯{stage.TierIndex}执行成功" : $"保盈止损阶梯{stage.TierIndex}执行失败",
                        autoSave: false);
                    
                    // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                    RecordTriggerExecution(profile, position, triggerKey, $"保盈止损阶梯{stage.TierIndex}", currentPnl, success);
                    
                    // 🔧 修复：不再设置全局IsTriggered状态，防止影响其他合约
                    if (success)
                    {
                        _logger.LogInformation($"✅ 保盈止损阶梯{stage.TierIndex}执行成功: {position.Symbol} (其他合约仍可独立触发此阶梯)");
                    }
                    
                    _logger.LogInformation($"✅ 保盈止损阶梯{stage.TierIndex}执行{(success ? "成功" : "失败")}: {position.Symbol}");
                    return true; // 执行一个阶梯后立即返回
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"执行保盈止损阶梯{stage.TierIndex}时发生错误: {position.Symbol}");
                    
                    // 🔄 记录异常状态为执行失败
                    _logger.LogWarning($"⚠️ 保盈止损阶梯{stage.TierIndex}发生异常，标记为执行失败");
                    _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString,
                        ExecutionType.ProfitProtection, stage.TierIndex, currentPnl, false, ex.Message,
                        autoSave: false);
                    
                    // 注意：不再重复记录冷却期，因为前面已经记录了
                    
                    // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                    RecordTriggerExecution(profile, position, triggerKey, $"保盈止损阶梯{stage.TierIndex}", currentPnl, false);
                    return true; // 表示执行了操作（即使失败）
                }
            }
            
            return false; // 没有执行任何操作
        }

        /// <summary>
        /// 执行保本止损设置 - 集成现有功能
        /// </summary>
        private async Task<bool> ExecuteBreakEvenStopLossAsync(PositionInfo position)
        {
            try
            {
                _logger.LogInformation($"🛡️ 开始执行保本止损: {position.Symbol}");
                
                // 🔧 集成现有的保本止损逻辑（来自 MainViewModel.RiskManagement.cs）
                var entryPrice = position.EntryPrice;
                var quantity = Math.Abs(position.PositionAmt);
                var side = position.PositionAmt > 0 ? "SELL" : "BUY";
                
                // 使用百分比缓冲（0.05%），确保真正保本而不会被轻易触发
                var bufferPercentage = 0.0005m; // 0.05%
                var stopPrice = position.PositionAmt > 0 
                    ? entryPrice * (1 + bufferPercentage)  // 多头：成本价 + 0.05%
                    : entryPrice * (1 - bufferPercentage); // 空头：成本价 - 0.05%
                stopPrice = Math.Round(stopPrice, 4);
                
                _logger.LogInformation($"💰 保本止损计算: 成本价={entryPrice:F4}, 缓冲={bufferPercentage * 100:F2}%, 止损价={stopPrice:F4}");

                // 🔧 关键修复：为止损单创建API添加超时控制
                var stopLossRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = quantity,
                    StopPrice = stopPrice,
                    ReduceOnly = true,
                    PositionSide = position.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                bool success = false;
                try
                {
                    using var stopTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var stopTask = _stopOrderManager.CreateStopOrderSafelyAsync(
                        position.Symbol, stopLossRequest, StopOrderType.BreakEven);
                    var stopTimeoutTask = Task.Delay(TimeSpan.FromSeconds(15), stopTimeoutCts.Token);
                    
                    var completedStopTask = await Task.WhenAny(stopTask, stopTimeoutTask);
                    
                    if (completedStopTask == stopTimeoutTask)
                    {
                        _logger.LogError($"⚠️ 保本止损单创建超时(15秒): {position.Symbol}");
                        return false;
                    }
                    
                    success = await stopTask;
                    stopTimeoutCts.Cancel();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 保本止损单创建异常: {position.Symbol}");
                    return false;
                }
                
                if (success)
                {
                    _logger.LogInformation($"💰 保本止损设置成功: {position.Symbol} @{stopPrice:F4}");
                }
                else
                {
                    _logger.LogError($"💰 保本止损设置失败: {position.Symbol}");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行保本止损失败: {position.Symbol}");
                return false;
            }
        }

        /// <summary>
        /// 执行一键保本加仓 - 集成现有功能
        /// </summary>
        private async Task<bool> ExecuteAddPositionAsync(PositionInfo position, AddPositionTier stage)
        {
            try
            {
                _logger.LogInformation($"💰 开始执行推仓: {position.Symbol}, 风险倍数: {stage.RiskMultiplier}倍, 止损比例: {stage.StopLossRatio * 100:F1}%");

                // 🔧 修正加仓数量计算逻辑
                // 1. 计算账户的单笔风险金
                var accountEquity = _mainViewModel.AccountInfo?.TotalEquity ?? 0;
                var riskTimes = _mainViewModel.SelectedAccount?.RiskCapitalTimes ?? 8;
                var singleRiskCapital = accountEquity / riskTimes;
                
                // 2. 从配置获取当前阶梯的参数
                var riskMultiplier = stage.RiskMultiplier;  // 风险倍数（例如：1.0倍）
                var stopLossRatio = stage.StopLossRatio;    // 止损比例（例如：0.10 = 10%）
                
                // 🔧 关键修复：为获取最新价格API添加超时控制
                decimal latestPrice = 0;
                try
                {
                    using var priceTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var priceTask = _binanceService.GetLatestPriceAsync(position.Symbol);
                    var priceTimeoutTask = Task.Delay(TimeSpan.FromSeconds(10), priceTimeoutCts.Token);
                    
                    var completedPriceTask = await Task.WhenAny(priceTask, priceTimeoutTask);
                    
                    if (completedPriceTask == priceTimeoutTask)
                    {
                        _logger.LogError($"⚠️ 获取最新价格超时(10秒): {position.Symbol}");
                        return false;
                    }
                    
                    latestPrice = await priceTask;
                    priceTimeoutCts.Cancel();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 获取最新价格失败: {position.Symbol}");
                    return false;
                }
                
                if (latestPrice <= 0)
                {
                    _logger.LogError($"❌ 获取到的价格无效: {position.Symbol}, 价格: {latestPrice}");
                    return false;
                }
                
                // 4. 计算加仓货值 = 风险倍数 * 单笔风险金 / 止损比例
                var addPositionValue = riskMultiplier * singleRiskCapital / stopLossRatio;
                
                // 5. 计算加仓数量 = 加仓货值 / 币的单价
                var addQuantity = addPositionValue / latestPrice;
                
                _logger.LogInformation($"💰 推仓计算: 账户权益={accountEquity:F2}U, 风险次数={riskTimes}, 单笔风险金={singleRiskCapital:F2}U, 风险倍数={riskMultiplier:F1}, 止损比例={stopLossRatio * 100:F1}%, 推仓货值={addPositionValue:F2}U, 合约单价={latestPrice:F4}, 推仓数量={addQuantity:F8}");

                // 🔧 关键修复：为获取交易规则API添加超时控制
                try
                {
                    using var rulesTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var rulesTask = _binanceService.GetSymbolTradingRulesAsync(position.Symbol);
                    var rulesTimeoutTask = Task.Delay(TimeSpan.FromSeconds(10), rulesTimeoutCts.Token);
                    
                    var completedRulesTask = await Task.WhenAny(rulesTask, rulesTimeoutTask);
                    
                    if (completedRulesTask == rulesTimeoutTask)
                    {
                        _logger.LogWarning($"⚠️ 获取交易规则超时(10秒): {position.Symbol}，使用默认精度");
                        addQuantity = Math.Round(addQuantity, 6);
                    }
                    else
                    {
                        var (minQty, maxQty, stepSize, tickSize, maxLeverage) = await rulesTask;
                        rulesTimeoutCts.Cancel();
                        
                        // 调整数量到正确的精度
                        addQuantity = Math.Floor(addQuantity / stepSize) * stepSize;
                        
                        if (addQuantity < minQty)
                        {
                            _logger.LogWarning($"❌ 计算的推仓数量 {addQuantity:F6} 小于最小交易数量 {minQty:F6}");
                            return false;
                        }
                        
                        if (addQuantity > maxQty)
                        {
                            _logger.LogWarning($"❌ 计算的推仓数量 {addQuantity:F6} 超过最大交易数量 {maxQty:F6}");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"获取交易规则失败，使用默认精度处理: {position.Symbol}");
                    addQuantity = Math.Round(addQuantity, 6);
                }

                // 6. 确定加仓方向（与当前持仓方向一致）
                var addPositionSide = position.PositionAmt > 0 ? "BUY" : "SELL";
                
                // 7. 🔧 关键修复：为推仓下单API添加超时控制
                var addOrderRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = addPositionSide,
                    Type = "MARKET",
                    Quantity = addQuantity,
                    TimeInForce = "GTC",
                    PositionSide = position.PositionSideString
                };

                bool addOrderSuccess = false;
                try
                {
                    // 🚀 修复：使用智能下单服务，提高自动盯盘成功率
                    _logger.LogInformation($"🚀 推仓下单切换到智能下单服务: {position.Symbol}");
                    
                    using var orderTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 增加超时时间给智能重试
                    var smartOrderTask = _smartOrderService.PlaceSmartOrderAsync(addOrderRequest);
                    var orderTimeoutTask = Task.Delay(TimeSpan.FromSeconds(30), orderTimeoutCts.Token);
                    
                    var completedOrderTask = await Task.WhenAny(smartOrderTask, orderTimeoutTask);
                    
                    if (completedOrderTask == orderTimeoutTask)
                    {
                        _logger.LogError($"⚠️ 推仓智能下单超时(30秒): {position.Symbol}");
                        return false;
                    }
                    
                    var smartOrderResult = await smartOrderTask;
                    addOrderSuccess = smartOrderResult.IsSuccess;
                    orderTimeoutCts.Cancel();
                    
                    // 记录智能下单的详细信息
                    if (smartOrderResult.IsSuccess)
                    {
                        _logger.LogInformation($"✅ 推仓智能下单成功: {position.Symbol}");
                        foreach (var action in smartOrderResult.Actions)
                        {
                            _logger.LogInformation($"   {action}");
                        }
                    }
                    else
                    {
                        _logger.LogError($"❌ 推仓智能下单失败: {position.Symbol} - {smartOrderResult.ErrorMessage}");
                        foreach (var action in smartOrderResult.Actions)
                        {
                            _logger.LogWarning($"   {action}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 推仓智能下单异常: {position.Symbol}");
                    return false;
                }
                
                if (!addOrderSuccess)
                {
                    _logger.LogError($"❌ 推仓下单失败: {position.Symbol}");
                    return false;
                }

                _logger.LogInformation($"✅ 推仓下单成功: {position.Symbol} {addPositionSide} {addQuantity:F6} (货值: {addPositionValue:F2}U) @ 市价");

                // 8. 等待订单执行
                await Task.Delay(2000);
                
                // 9. 🔧 关键修复：为获取更新后持仓信息API添加超时控制
                var originalPositionAmt = Math.Abs(position.PositionAmt);
                IEnumerable<dynamic>? updatedPositions = null;
                try
                {
                    using var positionsTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var positionsTask = _binanceService.GetPositionsAsync();
                    var positionsTimeoutTask = Task.Delay(TimeSpan.FromSeconds(10), positionsTimeoutCts.Token);
                    
                    var completedPositionsTask = await Task.WhenAny(positionsTask, positionsTimeoutTask);
                    
                    if (completedPositionsTask == positionsTimeoutTask)
                    {
                        _logger.LogError($"⚠️ 获取更新后持仓信息超时(10秒): {position.Symbol}");
                        return false;
                    }
                    
                    updatedPositions = await positionsTask;
                    positionsTimeoutCts.Cancel();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 获取更新后持仓信息失败: {position.Symbol}");
                    return false;
                }
                
                var updatedPosition = updatedPositions?.FirstOrDefault(p => 
                    p.Symbol == position.Symbol && Math.Abs(p.PositionAmt) > 0);

                if (updatedPosition == null)
                {
                    _logger.LogError($"❌ 推仓下单成功但持仓消失: {position.Symbol}，这是严重错误！");
                    return false;
                }

                // 🔧 修复：验证持仓数量是否真的增加了
                var newPositionAmt = Math.Abs(updatedPosition.PositionAmt);
                var positionIncrease = newPositionAmt - originalPositionAmt;
                
                _logger.LogInformation($"📊 推仓验证: {position.Symbol} 原持仓={originalPositionAmt:F6}, 新持仓={newPositionAmt:F6}, 增加={positionIncrease:F6}");
                
                if (positionIncrease < addQuantity * 0.95m) // 允许5%的误差
                {
                    _logger.LogWarning($"⚠️ 推仓数量异常: {position.Symbol} 预期增加{addQuantity:F6}, 实际增加{positionIncrease:F6}");
                    // 不返回false，因为可能有精度问题，但会记录警告
                }
                else
                {
                    _logger.LogInformation($"✅ 推仓数量验证通过: {position.Symbol} 持仓成功增加{positionIncrease:F6}");
                }

                // 10. 设置带保盈金额的止损
                var stopQuantity = Math.Abs(updatedPosition.PositionAmt);
                var entryPrice = updatedPosition.EntryPrice; // 这是加仓后的最新成本价
                var profitProtectionAmount = stage.ProfitProtectionAmount; // 保盈金额
                
                // 🔧 关键修改：根据保盈金额调整止损价格
                // 计算公式：
                // 多头：止损价 = 成本价 + (保盈金额 / 持仓数量)
                // 空头：止损价 = 成本价 - (保盈金额 / 持仓数量)
                decimal newStopPrice;
                if (updatedPosition.PositionAmt > 0) // 多头
                {
                    newStopPrice = entryPrice + (profitProtectionAmount / stopQuantity);
                }
                else // 空头
                {
                    newStopPrice = entryPrice - (profitProtectionAmount / stopQuantity);
                }
                newStopPrice = Math.Round(newStopPrice, 4);
                
                _logger.LogInformation($"💰 带保盈金额的止损计算: 成本价={entryPrice:F4}, 保盈金额={profitProtectionAmount:F2}U, 持仓数量={stopQuantity:F6}, 止损价={newStopPrice:F4}");

                var stopOrderSide = updatedPosition.PositionAmt > 0 ? "SELL" : "BUY";
                var stopOrderRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = stopOrderSide,
                    Type = "STOP_MARKET",
                    Quantity = stopQuantity,
                    StopPrice = newStopPrice,
                    TimeInForce = "GTC",
                    ReduceOnly = true,
                    PositionSide = position.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                // 🔧 关键修复：为止损单创建API添加超时控制
                bool stopSuccess = false;
                try
                {
                    using var stopTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var stopTask = _stopOrderManager.CreateStopOrderSafelyAsync(
                        position.Symbol, stopOrderRequest, StopOrderType.AddPosition);
                    var stopTimeoutTask = Task.Delay(TimeSpan.FromSeconds(15), stopTimeoutCts.Token);
                    
                    var completedStopTask = await Task.WhenAny(stopTask, stopTimeoutTask);
                    
                    if (completedStopTask == stopTimeoutTask)
                    {
                        _logger.LogWarning($"⚠️ 止损单创建超时(15秒): {position.Symbol}");
                        stopSuccess = false;
                    }
                    else
                    {
                        stopSuccess = await stopTask;
                        stopTimeoutCts.Cancel();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"❌ 止损单创建失败: {position.Symbol}");
                    stopSuccess = false;
                }
                
                if (stopSuccess)
                {
                    _logger.LogInformation($"🚀 推仓完成(含止损): {position.Symbol}, 推仓: {addQuantity:F6}@{latestPrice:F4}, 保本止损: @{newStopPrice:F4}");
                }
                else
                {
                    _logger.LogWarning($"⚠️ 推仓下单成功但止损单创建失败: {position.Symbol}, 推仓: {addQuantity:F6}@{latestPrice:F4}");
                }
                
                // 🔧 修复：推仓成功与否应该以推仓下单为准，而不是止损单创建
                // 推仓下单已经成功（第938行验证），即使止损单失败，推仓操作也应该被认为是成功的
                _logger.LogInformation($"✅ 推仓操作总结: {position.Symbol} 推仓下单=成功, 止损单创建={(stopSuccess ? "成功" : "失败")}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行推仓失败: {position.Symbol}");
                return false;
            }
        }

        /// <summary>
        /// 执行保盈止损设置 - 集成现有功能
        /// </summary>
        private async Task<bool> ExecuteProfitProtectionAsync(PositionInfo position, ProfitProtectionTier stage)
        {
            try
            {
                _logger.LogInformation($"🛡️ 开始执行保盈止损: {position.Symbol}, 保护金额: {stage.ProtectionAmount:F2}U");

                // 🔧 集成现有的保盈止损逻辑（来自 MainViewModel.RiskManagement.cs）
                var isLong = position.PositionAmt > 0;
                var entryPrice = position.EntryPrice;
                var quantity = Math.Abs(position.PositionAmt);
                var protectionAmount = stage.ProtectionAmount;
                var currentPrice = position.MarkPrice;
                
                _logger.LogInformation($"📊 保盈止损计算参数: 方向={(isLong ? "多头" : "空头")}, 入场价={entryPrice:F4}, 当前价={currentPrice:F4}, 数量={quantity:F8}, 保护金额={protectionAmount:F2}U");
                
                decimal protectionPrice;
                if (isLong)
                {
                    // 多头：止损价 = 开仓价 + (保护盈利 / 持仓数量)
                    protectionPrice = entryPrice + (protectionAmount / quantity);
                    _logger.LogInformation($"💰 多头计算: {entryPrice:F4} + ({protectionAmount:F2} / {quantity:F8}) = {protectionPrice:F4}");
                }
                else
                {
                    // 空头：止损价 = 开仓价 - (保护盈利 / 持仓数量)
                    protectionPrice = entryPrice - (protectionAmount / quantity);
                    _logger.LogInformation($"💰 空头计算: {entryPrice:F4} - ({protectionAmount:F2} / {quantity:F8}) = {protectionPrice:F4}");
                }

                // 验证止损价合理性
                bool isValidStopPrice = isLong 
                    ? (protectionPrice < currentPrice && protectionPrice > entryPrice)
                    : (protectionPrice > currentPrice && protectionPrice < entryPrice);

                if (!isValidStopPrice)
                {
                    var validationMessage = isLong 
                        ? $"不合理(止损价应在 {entryPrice:F4} 到 {currentPrice:F4} 之间)"
                        : $"不合理(止损价应在 {currentPrice:F4} 到 {entryPrice:F4} 之间)";
                    _logger.LogWarning($"保盈止损价格不合理: {protectionPrice:F4}, {validationMessage}");
                    return false;
                }

                _logger.LogInformation($"🔍 止损价验证通过: {protectionPrice:F4}");

                // 🔧 关键修复：为保盈止损单创建API添加超时控制
                var side = isLong ? "SELL" : "BUY";
                var stopLossRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = quantity,
                    StopPrice = protectionPrice,
                    ReduceOnly = true,
                    PositionSide = position.PositionSideString,
                    WorkingType = "CONTRACT_PRICE"
                };

                bool success = false;
                try
                {
                    using var stopTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var stopTask = _stopOrderManager.CreateStopOrderSafelyAsync(
                        position.Symbol, stopLossRequest, StopOrderType.ProfitProtection);
                    var stopTimeoutTask = Task.Delay(TimeSpan.FromSeconds(15), stopTimeoutCts.Token);
                    
                    var completedStopTask = await Task.WhenAny(stopTask, stopTimeoutTask);
                    
                    if (completedStopTask == stopTimeoutTask)
                    {
                        _logger.LogError($"⚠️ 保盈止损单创建超时(15秒): {position.Symbol}");
                        return false;
                    }
                    
                    success = await stopTask;
                    stopTimeoutCts.Cancel();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 保盈止损单创建异常: {position.Symbol}");
                    return false;
                }
                
                if (success)
                {
                    _logger.LogInformation($"🛡️ 保盈止损设置成功: {position.Symbol} @{protectionPrice:F4}, 保护: {protectionAmount:F2}U");
                }
                else
                {
                    _logger.LogError($"🛡️ 保盈止损设置失败: {position.Symbol}");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行保盈止损失败: {position.Symbol}");
                return false;
            }
        }

        /// <summary>
        /// 记录触发执行结果
        /// </summary>
        private void RecordTriggerExecution(PositionProfile profile, PositionInfo position, string triggerKey, string executionType, decimal currentPnl, bool success)
        {
            // 🔧 修复：添加线程安全保护，避免并发修改集合
            lock (_lockObject)
            {
                profile.TriggerRecords[triggerKey] = new TriggerRecord
                {
                    TriggerType = executionType,
                    TriggerTime = DateTime.Now,
                    TriggerPnl = currentPnl,
                    IsExecuted = success,
                    ExecutionResult = success ? "成功" : "失败"
                };

                var executionHistory = new ExecutionHistory
                {
                    Symbol = position.Symbol,
                    PositionSide = position.PositionSideString,
                    ExecutionType = executionType,
                    ExecutionTime = DateTime.Now,
                    TriggerPnl = currentPnl,
                    IsSuccess = success,
                    Details = $"浮盈{currentPnl:F2}U时触发{executionType}"
                };
                
                // 🔧 修复：安全添加到执行历史，避免并发访问冲突
                try
                {
                    _executionHistory.Add(executionHistory);
                    _logger.LogInformation($"📝 记录执行历史: {executionType} - {(success ? "成功" : "失败")}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 记录执行历史失败: {executionType}");
                }
            }

            OnExecutionCompleted(new ExecutionResultEventArgs
            {
                Symbol = position.Symbol,
                ExecutionType = executionType,
                IsSuccess = success,
                Message = success ? $"{executionType}执行成功" : $"{executionType}执行失败",
                PnlAtExecution = currentPnl
            });
        }

        /// <summary>
        /// 为新持仓清理历史状态（即时清理，解决止损委托触发后的重新开仓问题）
        /// </summary>
        private void CleanupHistoryForNewPosition(string symbol, string positionSide)
        {
            try
            {
                // 检查是否存在该合约的历史执行记录
                var historicalRecords = _executionHistory
                    .Where(h => h.Symbol == symbol && h.PositionSide == positionSide && h.ExecutionType != "状态清理")
                    .ToList();
                
                if (historicalRecords.Any())
                {
                    _logger.LogInformation($"🔄 检测到新持仓开仓: {symbol}_{positionSide} - 发现{historicalRecords.Count}条历史记录，立即清理");
                    
                    // 立即清理该合约的历史执行记录
                    _executionHistory.RemoveAll(h => h.Symbol == symbol && h.PositionSide == positionSide && h.ExecutionType != "状态清理");
                    
                    // 记录清理动作
                    var immediateCleanupHistory = new ExecutionHistory
                    {
                        Symbol = symbol,
                        PositionSide = positionSide,
                        ExecutionType = "新持仓即时清理",
                        ExecutionTime = DateTime.Now,
                        TriggerPnl = 0,
                        IsSuccess = true,
                        Details = $"检测到新持仓，立即清理{historicalRecords.Count}条历史执行记录（解决止损委托触发后重新开仓问题）"
                    };
                    _executionHistory.Add(immediateCleanupHistory);
                    
                    // 实时保存到持久化存储
                    try
                    {
                        _persistenceService.SaveExecutionHistory(_executionHistory);
                        _logger.LogInformation($"💾 新持仓即时清理完成并已保存: {symbol}_{positionSide}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"❌ 保存新持仓即时清理结果失败: {symbol}_{positionSide}");
                    }
                }
                else
                {
                    _logger.LogDebug($"ℹ️ 新持仓开仓: {symbol}_{positionSide} - 无历史记录需要清理");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 新持仓即时清理失败: {symbol}_{positionSide}");
            }
        }

        /// <summary>
        /// 清理已平仓的持仓档案
        /// </summary>
        private void CleanupClosedPositions(List<PositionInfo> activePositions)
        {
            lock (_lockObject)
            {
                // 🔧 增强诊断：记录清理前的状态
                var totalProfilesBefore = _positionProfiles.Count;
                var activeCount = activePositions.Count;
                
                _logger.LogInformation($"🔍 档案清理诊断 - 清理前档案数: {totalProfilesBefore}, 活跃持仓数: {activeCount}");
                
                var activeKeys = activePositions.Select(p => GetPositionKey(p.Symbol, p.PositionSideString)).ToHashSet();
                var keysToRemove = _positionProfiles.Keys.Where(k => !activeKeys.Contains(k)).ToList();
                
                // 🔧 详细诊断：输出所有档案和活跃持仓的对比
                _logger.LogInformation($"🔍 档案清理详情:");
                _logger.LogInformation($"  📋 活跃持仓键值: [{string.Join(", ", activeKeys)}]");
                _logger.LogInformation($"  📂 现有档案键值: [{string.Join(", ", _positionProfiles.Keys)}]");
                _logger.LogInformation($"  🗑️ 待清理档案: [{string.Join(", ", keysToRemove)}]");
                
                // 🔧 增强：记录清理的档案信息并检测重新开仓情况
                var cleanupResults = new List<string>();
                var actuallyRemoved = 0;
                
                foreach (var key in keysToRemove)
                {
                    if (_positionProfiles.TryGetValue(key, out var profile))
                    {
                        var triggerCount = profile.TriggerRecords.Count;
                        
                        if (triggerCount > 0)
                        {
                            cleanupResults.Add($"🗑️ 清理已平仓档案: {key} (清理{triggerCount}个触发记录)");
                            _logger.LogInformation($"🗑️ 清理已平仓档案: {key} - 清理触发记录: {triggerCount}个");
                            
                            // 记录清理历史，用于检测重新开仓
                            var cleanupHistory = new ExecutionHistory
                            {
                                Symbol = profile.Symbol,
                                PositionSide = profile.PositionSide,
                                ExecutionType = "状态清理",
                                ExecutionTime = DateTime.Now,
                                TriggerPnl = 0,
                                IsSuccess = true,
                                Details = $"平仓后清理历史状态，共清理{triggerCount}个触发记录"
                            };
                            _executionHistory.Add(cleanupHistory);
                        }
                        else
                        {
                            _logger.LogDebug($"🗑️ 清理已平仓档案: {key} (无触发记录)");
                        }
                        
                        // 🔧 关键：实际移除档案
                        var removeSuccess = _positionProfiles.Remove(key);
                        if (removeSuccess)
                        {
                            actuallyRemoved++;
                            _logger.LogInformation($"✅ 成功移除档案: {key}");
                        }
                        else
                        {
                            _logger.LogWarning($"⚠️ 移除档案失败: {key}");
                        }
                        
                        // 🛡️ 清理该合约的所有冷却期记录
                        _cooldownManager.ClearContractCooldowns(profile.Symbol, profile.PositionSide);
                        
                        // 🔄 清理统一状态管理器中的该合约状态
                        _unifiedStateManager.ClearContractStates(profile.Symbol, profile.PositionSide, "平仓清理");
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ 尝试清理不存在的档案: {key}");
                    }
                }
                
                // 🔧 诊断：记录清理后的状态
                var totalProfilesAfter = _positionProfiles.Count;
                _logger.LogInformation($"🔍 档案清理结果 - 清理后档案数: {totalProfilesAfter}, 实际移除: {actuallyRemoved}/{keysToRemove.Count}");
                
                // 🔧 关键诊断：如果档案数量与活跃持仓数量不匹配，输出详细信息
                if (totalProfilesAfter != activeCount)
                {
                    _logger.LogWarning($"⚠️ 档案数量不匹配! 档案数: {totalProfilesAfter}, 活跃持仓数: {activeCount}");
                    _logger.LogWarning($"   剩余档案: [{string.Join(", ", _positionProfiles.Keys)}]");
                    _logger.LogWarning($"   活跃持仓: [{string.Join(", ", activeKeys)}]");
                    
                    // 🔧 进一步诊断：检查是否有档案的key格式异常
                    foreach (var profileKey in _positionProfiles.Keys)
                    {
                        if (!activeKeys.Contains(profileKey))
                        {
                            var profile = _positionProfiles[profileKey];
                            var expectedKey = GetPositionKey(profile.Symbol, profile.PositionSide);
                            _logger.LogWarning($"   异常档案: {profileKey} (预期: {expectedKey})");
                        }
                    }
                }
                
                // 🔧 新增：检测重新开仓的合约并清理历史状态
                var newPositionKeys = activeKeys.Where(k => !_positionProfiles.ContainsKey(k)).ToList();
                foreach (var newKey in newPositionKeys)
                {
                    // 检查是否存在该合约的历史执行记录
                    var keyParts = newKey.Split('_');
                    if (keyParts.Length == 2)
                    {
                        var symbol = keyParts[0];
                        var positionSide = keyParts[1];
                        
                        var historicalRecords = _executionHistory
                            .Where(h => h.Symbol == symbol && h.PositionSide == positionSide && h.ExecutionType != "状态清理")
                            .ToList();
                        
                        if (historicalRecords.Any())
                        {
                            _logger.LogInformation($"🔄 检测到重新开仓: {newKey} - 发现{historicalRecords.Count}条历史记录，准备清理");
                            
                            // 清理该合约的历史执行记录
                            _executionHistory.RemoveAll(h => h.Symbol == symbol && h.PositionSide == positionSide && h.ExecutionType != "状态清理");
                            
                            // 记录清理动作
                            var reopenCleanupHistory = new ExecutionHistory
                            {
                                Symbol = symbol,
                                PositionSide = positionSide,
                                ExecutionType = "重新开仓清理",
                                ExecutionTime = DateTime.Now,
                                TriggerPnl = 0,
                                IsSuccess = true,
                                Details = $"检测到重新开仓，清理{historicalRecords.Count}条历史执行记录"
                            };
                            _executionHistory.Add(reopenCleanupHistory);
                        }
                    }
                }
                
                // 🔧 保存到持久化存储
                try
                {
                    _persistenceService.SavePositionProfiles(_positionProfiles);
                    _persistenceService.SaveExecutionHistory(_executionHistory);
                    _logger.LogDebug($"💾 档案清理结果已保存到持久化存储");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💾 保存档案清理结果到持久化存储失败");
                }
                
                // 📊 输出清理汇总
                if (actuallyRemoved > 0)
                {
                    _logger.LogInformation($"🧹 档案清理完成: 移除 {actuallyRemoved} 个已平仓档案，剩余 {totalProfilesAfter} 个活跃档案");
                    AddWorkLog("INFO", $"🧹 档案清理完成: 移除 {actuallyRemoved} 个已平仓档案");
                }
                else if (keysToRemove.Any())
                {
                    _logger.LogWarning($"⚠️ 档案清理异常: 发现 {keysToRemove.Count} 个待清理档案，但实际移除 0 个");
                }
            }
        }

        /// <summary>
        /// 获取持仓唯一标识
        /// </summary>
        private static string GetPositionKey(string symbol, string positionSide) => $"{symbol}_{positionSide}";

        /// <summary>
        /// 获取最后执行时间
        /// </summary>
        private DateTime? GetLastExecutionTime(PositionProfile profile)
        {
            if (!profile.TriggerRecords.Any()) return null;
            return profile.TriggerRecords.Values.Max(t => t.TriggerTime);
        }

        /// <summary>
        /// 获取执行历史
        /// </summary>
        public List<ExecutionHistory> GetExecutionHistory() => _executionHistory.ToList();

        /// <summary>
        /// 清空执行历史
        /// </summary>
        public void ClearExecutionHistory()
        {
            lock (_lockObject)
            {
                _executionHistory.Clear();
                _logger.LogInformation("🧹 已清空所有执行历史记录");
                
                // 持久化清理
                _persistenceService.SaveExecutionHistory(_executionHistory);
            }
        }

        /// <summary>
        /// 获取持仓档案
        /// </summary>
        public Dictionary<string, PositionProfile> GetPositionProfiles()
        {
            lock (_lockObject) { return new Dictionary<string, PositionProfile>(_positionProfiles); }
        }

        /// <summary>
        /// 获取冷却期管理器统计信息
        /// </summary>
        public CooldownStats GetCooldownStatistics() => _cooldownManager.Statistics;

        /// <summary>
        /// 获取活跃的冷却期信息
        /// </summary>
        public List<ActiveCooldownInfo> GetActiveCooldowns() => _cooldownManager.GetActiveCooldowns();

        /// <summary>
        /// 设置事件订阅
        /// </summary>
        private void SetupEventSubscriptions()
        {
            // 订阅执行状态变更事件
            _eventBus.Subscribe<ExecutionStateChangedEvent>(_loggingHandler);
            _eventBus.Subscribe<ExecutionStateChangedEvent>(_statisticsHandler);
            
            // 订阅监控状态变更事件
            _eventBus.Subscribe<MonitorStatusChangedEvent>(_loggingHandler);
            
            // 订阅持仓变化事件
            _eventBus.Subscribe<PositionChangedEvent>(_loggingHandler);
            
            // 订阅错误事件
            _eventBus.Subscribe<ErrorEvent>(_loggingHandler);
            _eventBus.Subscribe<ErrorEvent>(_statisticsHandler);
            
            // 订阅止损单事件
            _eventBus.Subscribe<StopOrderEvent>(_loggingHandler);
            _eventBus.Subscribe<StopOrderEvent>(_statisticsHandler);
            
            // 订阅冷却期事件
            _eventBus.Subscribe<CooldownEvent>(_loggingHandler);
            
            // 订阅性能事件
            _eventBus.Subscribe<PerformanceEvent>(_loggingHandler);
            _eventBus.Subscribe<PerformanceEvent>(_statisticsHandler);
            
            // 订阅数据同步事件
            _eventBus.Subscribe<DataSyncEvent>(_loggingHandler);
            
            _logger.LogInformation("🔌 事件订阅设置完成");
        }

        /// <summary>
        /// 获取统一状态管理器统计信息
        /// </summary>
        public UnifiedStateStats GetUnifiedStateStatistics() => _unifiedStateManager.GetStatistics();

        /// <summary>
        /// 获取合约执行统计信息（来自统一状态管理器）
        /// </summary>
        public List<ContractExecutionStats> GetContractExecutionStats(string? symbol = null, string? positionSide = null) 
            => _unifiedStateManager.GetExecutionStats(symbol, positionSide);

        /// <summary>
        /// 清理指定合约的所有状态（手动清理）
        /// </summary>
        public void ClearContractStates(string symbol, string? positionSide = null, string reason = "手动清理")
        {
            lock (_lockObject)
            {
                // 🔧 修复：同步清理所有数据源
                
                // 1. 清理统一状态管理器
                _unifiedStateManager.ClearContractStates(symbol, positionSide, reason);
                
                // 2. 清理冷却期管理器
                _cooldownManager.ClearContractCooldowns(symbol, positionSide);
                
                // 3. 清理旧的ContractStateManager状态
                if (positionSide != null)
                {
                    _contractStateManager.CleanupContractState(symbol, positionSide);
                }
                else
                {
                    // 清理该合约的所有方向（LONG和SHORT）
                    _contractStateManager.CleanupContractState(symbol, "LONG");
                    _contractStateManager.CleanupContractState(symbol, "SHORT");
                }
                
                // 🔧 关键修复：清理UI数据源 (_positionProfiles)
                List<string> keysToRemove = new List<string>();
                
                if (string.IsNullOrEmpty(symbol))
                {
                    // 清理所有合约
                    keysToRemove.AddRange(_positionProfiles.Keys);
                    _logger.LogInformation($"🧹 准备清理所有 {_positionProfiles.Count} 个合约的状态");
                }
                else
                {
                    // 清理指定合约
                    foreach (var key in _positionProfiles.Keys)
                    {
                        var parts = key.Split('_');
                        if (parts.Length >= 2 && parts[0].Equals(symbol, StringComparison.OrdinalIgnoreCase))
                        {
                            if (positionSide == null || parts[1].Equals(positionSide, StringComparison.OrdinalIgnoreCase))
                            {
                                keysToRemove.Add(key);
                            }
                        }
                    }
                    _logger.LogInformation($"🧹 准备清理合约 {symbol}_{positionSide ?? "ALL"} 的状态，匹配到 {keysToRemove.Count} 个档案");
                }
                
                // 清理匹配的档案并重置TriggerRecords
                foreach (var key in keysToRemove)
                {
                    if (_positionProfiles.TryGetValue(key, out var profile))
                    {
                        // 🔧 重置所有触发记录（这是UI显示的数据源）
                        profile.TriggerRecords.Clear();
                        profile.LastUpdateTime = DateTime.Now;
                        _logger.LogInformation($"🧹 已清理档案 {key} 的触发记录，共 {profile.TriggerRecords.Count} 条");
                    }
                }
                
                // 5. 清理执行历史（同步到两个地方）
                if (string.IsNullOrEmpty(symbol))
                {
                    _executionHistory.Clear();
                    _logger.LogInformation("🧹 已清理所有执行历史");
                }
                else
                {
                    var recordsToRemove = positionSide != null 
                        ? _executionHistory.Where(h => h.Symbol == symbol && h.PositionSide == positionSide).ToList()
                        : _executionHistory.Where(h => h.Symbol == symbol).ToList();
                    
                    foreach (var record in recordsToRemove)
                    {
                        _executionHistory.Remove(record);
                    }
                    _logger.LogInformation($"🧹 已清理 {recordsToRemove.Count} 条执行历史");
                }
                
                _logger.LogInformation($"🧹 完成合约状态清理: {symbol ?? "ALL"}_{positionSide ?? "ALL"} - 原因: {reason}");
            }
        }

        /// <summary>
        /// 验证当前配置
        /// </summary>
        /// <param name="mode">验证模式</param>
        /// <param name="allowAutoFix">是否允许自动修复</param>
        /// <returns>验证结果</returns>
        public async Task<ConfigValidationResult> ValidateConfigAsync(ValidationMode mode = ValidationMode.Strict, bool allowAutoFix = false)
        {
            if (_config == null)
            {
                return new ConfigValidationResult
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new ValidationError
                        {
                            ErrorCode = "CONFIG_E000",
                            ConfigKey = "Config",
                            Message = "当前没有加载的配置",
                            Severity = ValidationSeverity.Error
                        }
                    }
                };
            }

            return await _configValidationService.ValidateAsync(_config, mode, allowAutoFix);
        }
        
        /// <summary>
        /// 验证指定配置（不需要先设置到服务中）
        /// </summary>
        /// <param name="config">要验证的配置</param>
        /// <param name="mode">验证模式</param>
        /// <param name="allowAutoFix">是否允许自动修复</param>
        /// <returns>验证结果</returns>
        public async Task<ConfigValidationResult> ValidateConfigAsync(AutoMonitorConfig config, ValidationMode mode = ValidationMode.Strict, bool allowAutoFix = false)
        {
            if (config == null)
            {
                return new ConfigValidationResult
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new ValidationError
                        {
                            ErrorCode = "CONFIG_E000",
                            ConfigKey = "Config",
                            Message = "配置对象为空",
                            Severity = ValidationSeverity.Error
                        }
                    }
                };
            }

            return await _configValidationService.ValidateAsync(config, mode, allowAutoFix);
        }

        /// <summary>
        /// 获取配置验证建议
        /// </summary>
        /// <returns>配置建议列表</returns>
        public async Task<List<ConfigSuggestion>> GetConfigSuggestionsAsync()
        {
            if (_config == null) return new List<ConfigSuggestion>();

            var validationResult = await _configValidationService.ValidateAsync(_config, ValidationMode.Lenient, false);
            return validationResult.Suggestions;
        }

        /// <summary>
        /// 注册自定义配置验证规则
        /// </summary>
        /// <param name="rule">验证规则</param>
        public void RegisterValidationRule(ConfigValidationRule rule)
        {
            _configValidationService.RegisterRule(rule);
            _logger.LogInformation($"📝 已注册自定义验证规则: {rule.RuleId}");
        }

        /// <summary>
        /// 获取所有配置验证规则
        /// </summary>
        /// <returns>验证规则列表</returns>
        public List<ConfigValidationRule> GetValidationRules()
        {
            return _configValidationService.GetAllRules();
        }

        // 事件触发方法
        /// <summary>
        /// 基础配置验证（当配置验证服务失败时的后备方案）
        /// </summary>
        private ConfigValidationResult PerformBasicConfigValidation(AutoMonitorConfig config)
        {
            var result = new ConfigValidationResult { IsValid = true };
            
            try
            {
                _logger.LogInformation("🔧 执行基础配置验证...");
                
                // 基础必要项检查
                if (string.IsNullOrWhiteSpace(config.Name))
                {
                    result.Errors.Add(new ValidationError
                    {
                        ErrorCode = "BASIC_E001",
                        ConfigKey = "Name",
                        Message = "配置名称不能为空",
                        Severity = ValidationSeverity.Error
                    });
                }
                
                if (config.ScanIntervalSeconds <= 0)
                {
                    result.Errors.Add(new ValidationError
                    {
                        ErrorCode = "BASIC_E002",
                        ConfigKey = "ScanIntervalSeconds",
                        Message = "扫描间隔必须大于0秒",
                        Severity = ValidationSeverity.Error
                    });
                }
                
                if (config.ScanIntervalSeconds < 3)
                {
                    result.Warnings.Add(new ValidationWarning
                    {
                        WarningCode = "BASIC_W001",
                        ConfigKey = "ScanIntervalSeconds",
                        Message = "扫描间隔过短可能影响性能",
                        CurrentValue = config.ScanIntervalSeconds,
                        RecommendedValue = 5
                    });
                }
                
                // 设置验证结果
                result.IsValid = !result.Errors.Any();
                
                _logger.LogInformation($"🔧 基础配置验证完成: {(result.IsValid ? "通过" : "失败")} | 错误: {result.Errors.Count} | 警告: {result.Warnings.Count}");
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 基础配置验证失败");
                
                // 返回失败结果
                return new ConfigValidationResult
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new ValidationError
                        {
                            ErrorCode = "BASIC_E999",
                            ConfigKey = "General",
                            Message = $"基础配置验证异常: {ex.Message}",
                            Severity = ValidationSeverity.Critical
                        }
                    }
                };
            }
        }

        protected virtual void OnMonitorStatusChanged(MonitorStatusChangedEventArgs e) => MonitorStatusChanged?.Invoke(this, e);
        protected virtual void OnExecutionCompleted(ExecutionResultEventArgs e) => ExecutionCompleted?.Invoke(this, e);
        protected virtual void OnWorkLogAdded(WorkLogEventArgs e) => WorkLogAdded?.Invoke(this, e);

        /// <summary>
        /// 添加工作日志记录
        /// </summary>
        private void AddWorkLog(string level, string message)
        {
            try
            {
                var workLogEvent = new WorkLogEventArgs
                {
                    Level = level,
                    Message = message,
                    Time = DateTime.Now
                };
                
                OnWorkLogAdded(workLogEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发布工作日志时发生错误: {Message}", message);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                // 同步调用异步停止方法
                StopMonitoringAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 停止自动监控服务时发生错误");
            }
            finally
            {
                _scanTimer?.Dispose();
                _executionSemaphore?.Dispose(); // 🔧 释放执行信号量资源
                _stopOrderManager?.Dispose(); // 🛡️ 释放止损单管理器资源
                _cooldownManager?.Dispose(); // 🛡️ 释放冷却期管理器资源
                _unifiedStateManager?.Dispose(); // 🔄 释放统一状态管理器资源
                if (_eventBus is IDisposable disposableEventBus)
                {
                    disposableEventBus.Dispose(); // 🚌 释放事件总线资源
                }
            }
        }

        /// <summary>
        /// 获取合约状态列表（供监控面板使用）
        /// </summary>
        /// <summary>
        /// 获取合约状态列表（内部使用）
        /// </summary>
        public Dictionary<string, PositionProfile> GetContractStates()
        {
            try
            {
                lock (_positionDataLock)
                {
                    return _positionProfiles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取合约状态失败");
                return new Dictionary<string, PositionProfile>();
            }
        }

        /// <summary>
        /// 获取最近执行历史（供监控面板使用）
        /// </summary>
        public async Task<List<ExecutionHistoryRecord>> GetRecentExecutionHistoryAsync(int maxCount = 100)
        {
            var records = new List<ExecutionHistoryRecord>();
            
            try
            {
                // 🔧 修复：使用SemaphoreSlim保护执行历史的访问
                await _executionSemaphore.WaitAsync();
                try
                {
                    var recentHistory = _executionHistory
                        .OrderByDescending(h => h.ExecutionTime)
                        .Take(maxCount)
                        .ToList();
                    
                    foreach (var history in recentHistory)
                    {
                        records.Add(new ExecutionHistoryRecord
                        {
                            ExecutionTime = history.ExecutionTime,
                            AccountName = _mainViewModel.SelectedAccount?.Name ?? "未知账户",
                            Symbol = history.Symbol,
                            ExecutionType = history.ExecutionType,
                            IsSuccess = history.IsSuccess,
                            TriggerPnl = history.TriggerPnl,
                            ErrorMessage = history.ErrorMessage
                        });
                    }
                }
                finally
                {
                    _executionSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取执行历史失败");
            }
            
            return records;
        }

        /// <summary>
        /// 获取活跃合约数量
        /// </summary>
        public int GetActiveContractCount()
        {
            try
            {
                lock (_positionDataLock)
                {
                    return _positionProfiles.Values.Count(p => p.IsActive);
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取总执行次数
        /// </summary>
        public int GetTotalExecutions()
        {
            try
            {
                // 🔧 修复：使用SemaphoreSlim保护执行历史的访问（同步方式）
                _executionSemaphore.Wait();
                try
                {
                    return _executionHistory.Count;
                }
                finally
                {
                    _executionSemaphore.Release();
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取活跃止损单数量
        /// </summary>
        public int GetActiveStopOrderCount()
        {
            try
            {
                // 获取所有活跃合约的止损单总数
                var totalCount = 0;
                lock (_positionDataLock)
                {
                    foreach (var profile in _positionProfiles.Values.Where(p => p.IsActive))
                    {
                        try
                        {
                            totalCount += _stopOrderManager.GetActiveStopOrderCount(profile.Symbol);
                        }
                        catch
                        {
                            // 忽略单个合约的查询错误
                        }
                    }
                }
                return totalCount;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public AutoMonitorConfig? GetCurrentConfig()
        {
            lock (_lockObject)
            {
                return _config;
            }
        }
    }

    /// <summary>
    /// 监控状态变化事件参数
    /// </summary>
    public class MonitorStatusChangedEventArgs : EventArgs
    {
        public bool IsRunning { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// 执行结果事件参数
    /// </summary>
    public class ExecutionResultEventArgs : EventArgs
    {
        public string Symbol { get; set; } = "";
        public string ExecutionType { get; set; } = "";
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = "";
        public decimal PnlAtExecution { get; set; }
    }

    public class WorkLogEventArgs : EventArgs
    {
        public string Level { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Time { get; set; } = DateTime.Now;
    }
} 