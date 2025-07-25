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
    /// 自动监控服务 - 简化版，移除冗余组件
    /// </summary>
    public class AutoMonitorService : IDisposable
    {
        private readonly IBinanceService _binanceService;
        private readonly MainViewModel _mainViewModel;
        private readonly ILogger<AutoMonitorService> _logger;
        private readonly AutoMonitorPersistenceService _persistenceService;
        
        // 🎯 新的双文件系统服务
        private readonly ContractMonitoringStateService? _stateService;
        private readonly BaseConfigManager? _configManager;
        
        private Timer? _scanTimer;
        private bool _isRunning;
        private AutoMonitorConfig? _config;
        private readonly object _lockObject = new();
        
        // 简化的状态管理：只使用PositionProfile
        private readonly Dictionary<string, PositionProfile> _positionProfiles = new();
        private readonly List<ExecutionHistory> _executionHistory = new();

        // 简化的冷却期管理
        private readonly Dictionary<string, DateTime> _lastExecutionTimes = new();
        private readonly TimeSpan _cooldownPeriod = TimeSpan.FromMinutes(5);
        
        // 添加缺少的字段
        private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
        private int _scanCount = 0;
        
        // 🔧 临时修复：简化复杂依赖以确保编译通过
        private readonly object _lockObject1 = new object();
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly ReaderWriterLockSlim _positionDataLock = new ReaderWriterLockSlim();
        
        // 简化的服务存根 - 避免编译错误（使用已存在的服务）
        private readonly SimpleStateManager _unifiedStateManager;
        private readonly SimpleEventBus _eventBus = new();
        private readonly SimpleCooldownManager _cooldownManager = new();
        private readonly AutoMonitorExecutionEngine _executionEngine;
        private readonly SimpleStopOrderManager _stopOrderManager = new();
        private readonly SimpleSmartOrderService _smartOrderService = new();
        private readonly SimpleConfigValidationService _configValidationService = new();
        private readonly SimpleLoggingHandler _loggingHandler = new();
        private readonly SimpleStatisticsHandler _statisticsHandler = new();

        public bool IsRunning => _isRunning;
        public AutoMonitorConfig? CurrentConfig => _config;
        public bool IsPaused { get; private set; }

        /// <summary>
        /// 暂停扫描
        /// </summary>
        public async Task PauseAsync()
        {
            if (_isRunning && !IsPaused)
            {
                IsPaused = true;
                _logger?.LogInformation("⏸️ 自动监控扫描已暂停");
                await Task.CompletedTask;
            }
        }

        /// <summary>
        /// 恢复扫描
        /// </summary>
        public async Task ResumeAsync()
        {
            if (_isRunning && IsPaused)
            {
                IsPaused = false;
                _logger?.LogInformation("▶️ 自动监控扫描已恢复");
                await Task.CompletedTask;
            }
        }

        // 事件定义
        public event EventHandler<MonitorStatusChangedEventArgs>? MonitorStatusChanged;
        public event EventHandler<ExecutionResultEventArgs>? ExecutionCompleted;
        public event EventHandler<WorkLogEventArgs>? WorkLogAdded;
        public event EventHandler<StatusUpdateEventArgs>? StatusUpdated;
        public event EventHandler<PositionChangedEventArgs>? PositionChanged;

        public AutoMonitorService(
            IBinanceService binanceService, 
            MainViewModel mainViewModel,
            ILogger<AutoMonitorService> logger)
        {
            _binanceService = binanceService ?? throw new ArgumentNullException(nameof(binanceService));
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _persistenceService = new AutoMonitorPersistenceService();
            
            // 🎯 尝试获取新的双文件系统服务（如果可用）
            try
            {
                // 从服务定位器获取新服务（如果已注册）
                var serviceProvider = Application.Current?.TryFindResource("ServiceProvider") as IServiceProvider;
                _configManager = serviceProvider?.GetService(typeof(BaseConfigManager)) as BaseConfigManager;
                _stateService = serviceProvider?.GetService(typeof(ContractMonitoringStateService)) as ContractMonitoringStateService;
                
                _logger?.LogCritical($"🔍【服务定位器诊断】serviceProvider={serviceProvider != null}");
                _logger?.LogCritical($"🔍【服务注入诊断】_configManager={_configManager != null}, _stateService={_stateService != null}");
                
                if (_configManager != null && _stateService != null)
                {
                    _logger?.LogInformation("✅ 双文件系统服务已启用");
                }
                else
                {
                    _logger?.LogCritical("⚠️ 服务定位器未找到服务，将创建新实例");
                    
                    // 🔧 关键修复：如果服务定位器没有找到，直接创建实例
                    if (_configManager == null)
                    {
                        _configManager = BaseConfigManager.Instance;
                        _logger?.LogCritical("✅ 已使用单例模式获取BaseConfigManager");
                    }
                    
                    if (_stateService == null)
                    {
                        var stateServiceLogger = new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<ContractMonitoringStateService>();
                        _stateService = new ContractMonitoringStateService(stateServiceLogger, _configManager);
                        _logger?.LogCritical("✅ 已创建新的ContractMonitoringStateService实例");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "⚠️ 获取双文件系统服务失败，将创建备用实例");
                
                // 🔧 异常恢复：创建备用实例
                _configManager = BaseConfigManager.Instance;
                var stateServiceLogger = new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<ContractMonitoringStateService>();
                _stateService = new ContractMonitoringStateService(stateServiceLogger, _configManager);
                _logger?.LogCritical("✅ 已创建备用服务实例");
            }
            
            // 初始化SimpleStateManager需要的依赖
            var stateLogger = new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<SimpleStateManager>();
            var unifiedPersistence = new UnifiedPersistenceService();
            _unifiedStateManager = new SimpleStateManager(stateLogger, unifiedPersistence);
            
            // 初始化真正的执行引擎
            var loggerFactory = new Microsoft.Extensions.Logging.LoggerFactory();
            var executionLogger = loggerFactory.CreateLogger<AutoMonitorExecutionEngine>();
            var tradingLogger = loggerFactory.CreateLogger<TradingExecutionService>();
            var profileLogger = loggerFactory.CreateLogger<ContractProfileService>();
            var configLogger = loggerFactory.CreateLogger<BaseConfigManager>();
            
            // 🔧 修复：使用BaseConfigManager单例实例，确保全局配置统一
            var configManager = BaseConfigManager.Instance;
            configManager.SetLogger(configLogger); // 设置具体的Logger
            var riskLogger = loggerFactory.CreateLogger<RiskCapitalService>();
            var riskCapitalService = new RiskCapitalService(riskLogger, mainViewModel);
            var tradingService = new TradingExecutionService(tradingLogger, binanceService);
            var profileService = new ContractProfileService(profileLogger, binanceService, configManager, riskCapitalService);
            
            _executionEngine = new AutoMonitorExecutionEngine(
                executionLogger,
                tradingService,
                profileService,
                configManager,
                _persistenceService,
                _unifiedStateManager,
                _stateService); // 🔧【关键修复】传入ContractMonitoringStateService，确保状态同步到contract_monitoring_states.json
        }

        /// <summary>
        /// 启动自动监控
        /// </summary>
        public async Task<bool> StartMonitoringAsync(AutoMonitorConfig config)
        {
            _logger.LogInformation("🚀 启动盯盘");
            AddWorkLog("INFO", "🚀 启动盯盘");
            
            if (config == null) 
            {
                throw new ArgumentNullException(nameof(config));
            }

            // 停止现有的监控
            if (_isRunning) 
            {
                await StopMonitoringAsync();
            }

            try
            {
                // 设置配置
                lock (_lockObject) 
                { 
                    _config = config;
                    _isRunning = false; // 先设为false，初始化成功后再设为true
                }

                // 初始化持仓档案
                await InitializePositionProfilesAsync();
                    
                // 🔧 【关键修复】检查并同步配置到统一状态文件
                try
                {
                    if (_stateService != null && _config != null)
                    {
                        _logger.LogInformation($"🔄 检查配置同步: {_config.Name}");
                        
                        // 检查现有状态是否使用了不同的配置
                        var existingStates = _stateService.LoadMonitoringStates();
                        var needSync = false;
                        
                        foreach (var state in existingStates.Values.Where(s => s.IsActive))
                        {
                            if (state.BaseConfigName != _config.Name)
                            {
                                needSync = true;
                                _logger.LogInformation($"🔍 发现配置不一致: {state.Symbol}_{state.PositionSide} 当前={state.BaseConfigName} vs 选择={_config.Name}");
                                break;
                            }
                        }
                        
                        if (needSync)
                        {
                            _logger.LogInformation($"🔄 同步所有合约配置到统一状态文件: {_config.Name}");
                            _stateService.SwitchAllContractsConfiguration(_config.Name);
                            _logger.LogInformation($"✅ 启动时配置同步完成");
                        }
                        else
                        {
                            _logger.LogInformation($"✅ 配置已同步，无需更新");
                        }
                    }
                }
                catch (Exception syncEx)
                {
                    _logger.LogError(syncEx, "启动时配置同步失败");
                }
                
                // 创建定时器
                var intervalMs = Math.Max(_config.ScanIntervalSeconds * 1000, 5000); // 🔧 【优化】最小5秒，提高新仓位检测响应速度
                _scanTimer = new Timer(async _ => await ScanPositionsAsync(), null, 0, intervalMs);

                // 设置运行状态
                lock (_lockObject)
                {
                    _isRunning = true;
                }
                    
                OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                    IsRunning = true,
                    Message = $"自动监控已启动 - {config.Name}"
                });

                _logger.LogInformation($"✅ 启动盯盘成功 - 配置: {config.Name}");
                AddWorkLog("INFO", $"✅ 启动盯盘成功 - 配置: {config.Name}");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动监控服务失败");
                lock (_lockObject)
                {
                    _isRunning = false;
                    _config = null;
                }
                
                OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                    IsRunning = false,
                    Message = $"启动失败: {ex.Message}"
                });
                
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
            
            try
            {
                // 原子性状态变更
                lock (_lockObject)
                {
                    wasRunning = _isRunning;
                    if (!_isRunning) 
                    {
                        _logger.LogInformation("⏹️ 自动监控已经处于停止状态");
                        return;
                    }
                    
                    _isRunning = false;
                    timerToDispose = _scanTimer;
                    _scanTimer = null;
                }
                
                _logger.LogInformation("⏹️ 开始停止自动监控服务...");
                
                // 🔧 步骤2：立即停止Timer（最高优先级）
                if (timerToDispose != null)
                {
                    try
                    {
                        timerToDispose.Dispose();
                        // 🔧 等待Timer回调完全结束，但有超时保护
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                        await Task.Delay(150, timeoutCts.Token);
                        _logger.LogInformation("⏰ 扫描定时器已完全停止");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("⏰ 扫描定时器停止（超时保护）");
                    }
                    catch (Exception timerEx)
                    {
                        _logger.LogWarning(timerEx, "⚠️ 停止扫描定时器时发生错误，继续停止流程");
                    }
                }
                
                // 🔧 步骤3：等待当前执行完成（有超时保护）
                try
                {
                    // 🔧 简化信号量处理，避免复杂的await逻辑
                    var acquired = await _executionSemaphore.WaitAsync(TimeSpan.FromSeconds(2));
                    if (acquired)
                    {
                        _logger.LogInformation("🔒 等待当前执行完成");
                        _executionSemaphore.Release();
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ 等待当前执行超时，强制继续停止流程");
                    }
                }
                catch (Exception execEx)
                {
                    _logger.LogWarning(execEx, "⚠️ 等待执行完成时发生错误，继续停止流程");
                }
                
                // 🔧 步骤4：保存状态（有超时保护）
                try
                {
                    using var saveTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await Task.Run(() =>
                    {
                        _persistenceService.SavePositionProfiles(_positionProfiles);
                        _persistenceService.SaveExecutionHistory(_executionHistory);
                    }, saveTimeoutCts.Token);
                    _logger.LogInformation("💾 已保存自动盯盘状态到持久化存储");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("⚠️ 保存状态超时，继续停止流程");
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "❌ 保存自动盯盘状态失败，继续停止流程");
                }
                
                // 🔧 步骤5：停止事件总线（有超时保护）
                try
                {
                    using var eventTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    
                    if (wasRunning)
                    {
                        var publishTask = _eventBus.PublishAsync(new MonitorStatusChangedEvent
                        {
                            Source = "AutoMonitorService",
                            IsRunning = false,
                            Message = "自动监控已停止",
                            Config = _config,
                            ActiveContractCount = 0
                        });
                        
                        var publishTimeoutTask = Task.Delay(1000, eventTimeoutCts.Token);
                        var completedPublishTask = await Task.WhenAny(publishTask, publishTimeoutTask);
                        
                        if (completedPublishTask == publishTimeoutTask)
                        {
                            _logger.LogWarning("⚠️ 发布停止事件超时");
                        }
                        else
                        {
                            await publishTask;
                            _logger.LogInformation("📢 停止事件已发布");
                        }
                    }
                    
                    var stopTask = _eventBus.StopAsync();
                    var stopTimeoutTask = Task.Delay(1000, eventTimeoutCts.Token);
                    var completedStopTask = await Task.WhenAny(stopTask, stopTimeoutTask);
                    
                    if (completedStopTask == stopTimeoutTask)
                    {
                        _logger.LogWarning("⚠️ 停止事件总线超时");
                    }
                    else
                    {
                        await stopTask;
                        _logger.LogInformation("✅ 事件总线已停止");
                    }
                }
                catch (Exception eventEx)
                {
                    _logger.LogError(eventEx, "❌ 停止事件总线时发生错误，继续停止流程");
                }
                
                _logger.LogInformation("✅ 自动监控停止完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 停止自动监控时发生严重错误");
                
                // 🔧 确保即使异常也要重置运行状态
                lock (_lockObject)
                {
                    _isRunning = false;
                }
            }
            finally
            {
                // 🔧 步骤6：确保状态变更事件能够触发（最后的保障）
                try
                {
                    OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                        IsRunning = false, 
                        Message = "自动监控已停止" 
                    });
                }
                catch (Exception eventEx)
                {
                    _logger.LogWarning(eventEx, "❌ 触发最终状态变更事件失败");
                }
            }
        }

        /// <summary>
        /// 初始化持仓档案
        /// </summary>
        private async Task InitializePositionProfilesAsync()
        {
            _logger.LogInformation("📊 开始获取持仓数据...");
            
            try
            {
                // 🔧 添加超时控制，防止API调用无限等待
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                var positions = await _binanceService.GetPositionsAsync();
                
                if (positions == null) 
                {
                    _logger.LogWarning("⚠️ API返回的持仓数据为空，自动盯盘将等待持仓数据");
                    AddWorkLog("WARN", "⚠️ API返回持仓数据为空，等待持仓出现");
                    return;
                }

                // 筛选活跃持仓（数量不为0的）
                var activePositions = positions.Where(p => Math.Abs(p.PositionAmt) > 0).ToList();
                
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

                // 🔧 关键修复：将持久化数据加载移到lock外部，提升性能
                _logger.LogInformation($"✅ 加载完毕 - {activePositions.Count} 个活跃持仓");
                
                // 🚨 启动时立即执行紧急清理
                try 
                {
                    _persistenceService.EmergencyCleanInvalidProfiles();
                }
                catch (Exception cleanEx)
                {
                    _logger.LogError(cleanEx, "❌ 启动前紧急清理失败，继续启动");
                }
                
                var persistedProfiles = await Task.Run(() => _persistenceService.LoadPositionProfiles());
                var persistedHistory = await Task.Run(() => _persistenceService.LoadExecutionHistory());
                
                // 🔧 关键修复：预处理数据，减少lock内部的复杂操作
                var newPositionProfiles = new Dictionary<string, PositionProfile>();
                var invalidProfileKeys = new List<string>();
                
                // 🔧 关键修复：只为当前真实存在的活跃持仓恢复档案
                foreach (var position in activePositions)
                {
                    // 🔧 【关键修复】使用标准化的持仓方向而不是API返回的值
                    var standardizedPositionSide = position.PositionAmt > 0 ? "LONG" : "SHORT";
                    var key = GetPositionKey(position.Symbol, standardizedPositionSide);
                    
                    // 尝试从持久化数据中恢复
                    if (persistedProfiles.TryGetValue(key, out var existingProfile))
                    {
                        // 更新持仓数据（价格、数量可能有变化）
                        existingProfile.InitialQuantity = Math.Abs(position.PositionAmt);
                        existingProfile.InitialEntryPrice = position.EntryPrice;
                        existingProfile.LastUpdateTime = DateTime.Now;
                        existingProfile.IsActive = true;
                        
                        newPositionProfiles[key] = existingProfile;
                    }
                    else
                    {
                        // 创建新档案
                        var newProfile = new PositionProfile
                        {
                            Symbol = position.Symbol,
                            PositionSide = standardizedPositionSide,
                            InitialQuantity = Math.Abs(position.PositionAmt),
                            InitialEntryPrice = position.EntryPrice,
                            CreateTime = DateTime.Now,
                            LastUpdateTime = DateTime.Now,
                            IsActive = true
                        };
                        
                        newPositionProfiles[key] = newProfile;
                    }
                }

                // 🔧 关键修复：在lock内部进行快速赋值，避免长时间锁定
                lock (_lockObject)
                {
                    _positionProfiles.Clear();
                    foreach (var kvp in newPositionProfiles)
                    {
                        _positionProfiles[kvp.Key] = kvp.Value;
                    }
                    
                    // 更新执行历史
                    _executionHistory.Clear();
                    foreach (var history in persistedHistory)
                    {
                        _executionHistory.Add(history);
                    }
                }
                
                _logger.LogInformation($"📊 档案管理: 总共 {newPositionProfiles.Count} 个，历史记录 {persistedHistory.Count} 条");
                AddWorkLog("INFO", $"📊 档案管理: 总共 {newPositionProfiles.Count} 个，历史记录 {persistedHistory.Count} 条");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 初始化持仓档案失败");
                AddWorkLog("ERROR", $"❌ 初始化持仓档案失败: {ex.Message}");
                
                // 发生异常时也要确保基本的空容器被创建
                lock (_lockObject)
                {
                    _positionProfiles.Clear();
                    _executionHistory.Clear();
                }
                
                throw; // 重新抛出异常，由上层处理
            }
        }

        /// <summary>
        /// 扫描持仓并执行相应策略
        /// </summary>
        private async Task ScanPositionsAsync()
        {
            // 检查是否已暂停
            if (IsPaused)
            {
                _logger?.LogDebug("⏸️ 扫描已暂停，跳过本次扫描");
                return;
            }
            // 🔧 新增：记录定时器触发信息，添加分割符便于查看
            _logger.LogDebug("⏰ 定时器触发扫描方法");
            AddWorkLog("INFO", "─────────────────────────────────────");
            AddWorkLog("INFO", "⏰ 定时器触发，开始扫描");

            bool isRunning;
            AutoMonitorConfig? config;
            lock (_lockObject)
            {
                isRunning = _isRunning;
                config = _config;
            }

            if (!isRunning || config == null)
            {
                return;
            }

            // 🛡️ 增加扫描计数并定期清理过期的冷却期记录（每20次扫描清理一次）
            _scanCount++;
            if (_scanCount % 20 == 0)
            {
                _cooldownManager.CleanupExpiredRecords();
            }

            var semaphoreEntered = false;
            try
            {
                semaphoreEntered = await _executionSemaphore.WaitAsync(TimeSpan.FromSeconds(2));
                if (!semaphoreEntered)
                {
                    AddWorkLog("WARN", "⚠️ 扫描繁忙，跳过本次扫描");
                    return;
                }

                // 获取持仓数据
                IEnumerable<dynamic>? positions = null;
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var positionsTask = _binanceService.GetPositionsAsync();
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15), timeoutCts.Token);
                    
                    var completedTask = await Task.WhenAny(positionsTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        AddWorkLog("ERROR", "❌ 获取持仓数据超时(15秒)，跳过本次扫描");
                        return;
                    }
                    
                    positions = await positionsTask;
                    timeoutCts.Cancel();
                }
                catch (TaskCanceledException)
                {
                    _logger.LogError($"🔍 [SCAN-ERROR] 获取持仓数据被取消");
                    AddWorkLog("ERROR", "❌ 获取持仓数据被取消，跳过本次扫描");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"🔍 [SCAN-ERROR] 获取持仓数据异常: {ex.Message}");
                    AddWorkLog("ERROR", $"❌ 获取持仓数据异常: {ex.Message}");
                    return;
                }
                
                if (positions == null || !positions.Any())
                {
                    return; // 静默跳过，避免无用日志
                }

                // 过滤活跃持仓
                var activePositions = positions.Where(p => 
                    Math.Abs(p.PositionAmt) > 0.0001m &&
                    !string.IsNullOrEmpty(p.Symbol) &&
                    p.Symbol.EndsWith("USDT") &&
                    p.MarkPrice > 0 &&
                    p.EntryPrice > 0
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
                    return; // 静默跳过，避免无用日志
                }

                // 🎯 生成统一监控状态文件（新的双文件系统）
                await GenerateUnifiedMonitoringStatesAsync(activePositions);

                // 清理已关闭的持仓
                CleanupClosedPositions(activePositions);

                // 检查档案完整性
                var missingProfiles = new List<PositionInfo>();
                var currentProfiles = _positionProfiles?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, PositionProfile>();
                foreach (var position in activePositions)
                {
                    // 🔧 【关键修复】使用标准化的持仓方向而不是API返回的值
                    var standardizedPositionSide = position.PositionAmt > 0 ? "LONG" : "SHORT";
                    var key = GetPositionKey(position.Symbol, standardizedPositionSide);
                    
                    if (!currentProfiles.ContainsKey(key))
                    {
                        missingProfiles.Add(position);
                    }
                }
                
                // 后台创建缺失的档案
                if (missingProfiles.Any())
                {
                    AddWorkLog("WARN", $"🔧 发现 {missingProfiles.Count} 个缺失档案，将在后台创建...");
                    
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(100);
                            foreach (var position in missingProfiles)
                            {
                                await CreatePositionProfileSafeAsync(position);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "后台创建档案失败");
                        }
                    });
                }
                
                const int maxConcurrency = 3; // 最多同时处理3个持仓
                var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
                
                var processingTasks = activePositions.Select(async position =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        _logger.LogCritical($"🔍 [POSITION-START] 开始处理 {position.Symbol} {position.PositionSideString}");
                        await ProcessPositionAsync(position);
                        _logger.LogCritical($"🔍 [POSITION-END] 完成处理 {position.Symbol} {position.PositionSideString}");
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
                    _logger.LogError($"🔍 [SCAN-ERROR] 持仓处理整体超时(30秒)，部分处理可能未完成");
                    AddWorkLog("ERROR", "❌ 持仓处理整体超时(30秒)，部分处理可能未完成");
                }
                else
                {
                    overallTimeoutCts.Cancel();
                    await allTasksTask; // 等待所有任务完成
                    AddWorkLog("INFO", $"✅ 扫描完成 - 已处理 {activePositions.Count} 个持仓");
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
            // 验证合约有效性
            if (string.IsNullOrEmpty(position.Symbol) || !position.Symbol.EndsWith("USDT"))
            {
                _logger.LogWarning($"⚠️ 跳过无效合约: {position.Symbol}");
                return;
            }
            
            // 🔧 【关键修复】使用标准化的持仓方向而不是API返回的值
            var standardizedPositionSide = position.PositionAmt > 0 ? "LONG" : "SHORT";
            var key = GetPositionKey(position.Symbol, standardizedPositionSide);
            
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
                        // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                        foreach (var trigger in persistedProfile.TriggerRecords.Values)
                        {
                            if (trigger.IsExecuted)
                            {
                                var executionType = trigger.TriggerType.Contains("推仓") ? ExecutionType.AddPosition :
                                                  trigger.TriggerType.Contains("保本") ? ExecutionType.BreakEven :
                                                  ExecutionType.ProfitProtection;
                                                  
                                _unifiedStateManager.RecordExecution(position.Symbol, standardizedPositionSide,
                                    executionType, trigger.TierIndex ?? 0, trigger.TriggerPnl, true, trigger.ExecutionResult ?? "成功",
                                    autoSave: false);
                                _logger.LogInformation($"   🔧 同步状态键值: {position.Symbol}_{standardizedPositionSide}_{executionType}_{trigger.TierIndex}");
                            }
                        }
                    }
                    else
                    {
                        // 确实是新持仓，清理历史状态
                        CleanupHistoryForNewPosition(position.Symbol, standardizedPositionSide);
                        
                        _positionProfiles[key] = new PositionProfile
                        {
                            Symbol = position.Symbol,
                            PositionSide = standardizedPositionSide,
                            InitialQuantity = Math.Abs(position.PositionAmt),
                            InitialEntryPrice = position.EntryPrice,
                            CreateTime = DateTime.Now,
                            LastUpdateTime = DateTime.Now
                        };
                        
                        _logger.LogInformation($"📝 新建档案: {key}");
                        
                        // 🔧 【重要新增】：检测到新持仓时发送事件通知UI立即更新
                        try
                        {
                            _logger.LogInformation($"🆕 检测到新开仓: {position.Symbol}_{standardizedPositionSide}, 通知UI更新");
                            OnPositionChanged(new PositionChangedEventArgs
                            {
                                Symbol = position.Symbol,
                                PositionSide = standardizedPositionSide,
                                ChangeType = PositionChangeType.Opened,
                                CurrentQuantity = Math.Abs(position.PositionAmt),
                                CurrentPnl = position.UnrealizedProfit,
                                Timestamp = DateTime.Now
                            });
                        }
                        catch (Exception eventEx)
                        {
                            _logger.LogError(eventEx, $"❌ 发送新开仓事件失败: {key}");
                        }
                    }
                }
                _positionProfiles[key].LastUpdateTime = DateTime.Now;
            }

            var profile = _positionProfiles[key];
            var currentPnl = position.UnrealizedProfit;

            // 【浮盈比对】关键信息
            AddWorkLog("INFO", $"🔍【浮盈比对】{key}: 当前浮盈 {currentPnl:F2}U");

            // 🔧 关键修复：移除浮盈<=0的限制，让各个操作函数自己判断条件
            // 这样可以确保所有持仓都被检查，包括浮盈为0或负数的持仓

            // 🔧 修复：移除全局冷却期，改为按操作类型独立冷却，防止跳过第一级推仓
            // 每种操作（保本、推仓、保盈）都有独立的冷却期机制，在各自的Check方法中处理

                            // 使用执行引擎处理
                try
                {
                
                // 🔧 关键参数验证
                if (position.EntryPrice <= 0)
                {
                    var errorMsg = $"持仓数据异常: {key} 开仓价格={position.EntryPrice}，跳过执行";
                    AddWorkLog("ERROR", errorMsg);
                    _logger.LogError(errorMsg);
                    return;
                }
                
                if (position.MarkPrice <= 0)
                {
                    var errorMsg = $"持仓数据异常: {key} 标记价格={position.MarkPrice}，跳过执行";
                    AddWorkLog("ERROR", errorMsg);
                    _logger.LogError(errorMsg);
                    return;
                }
                
                // 🔧 创建ContractProfile对象
                var contractProfile = new ContractProfile
                {
                    Symbol = position.Symbol,
                    Side = position.PositionAmt > 0 ? "LONG" : "SHORT", // 🚨 修复：使用真实持仓方向而不是BOTH
                    PositionSize = Math.Abs(position.PositionAmt),
                    EntryPrice = position.EntryPrice,
                    CurrentPrice = position.MarkPrice, // 🔧 添加当前价格
                    UnrealizedPnl = position.UnrealizedProfit,
                    LastUpdateTime = DateTime.Now,
                    UseIndependentConfig = true,
                    BaseConfigName = _config?.Name ?? "默认配置"
                };
                
                                    // 设置独立配置
                    if (_config != null)
                    {
                        // 设置保本配置
                        if (_config.BreakEvenConfig?.IsEnabled == true)
                        {
                            contractProfile.IndependentBreakEvenConfig = new ContractBreakEvenConfig
                            {
                                IsEnabled = true,
                                TriggerProfitAmount = _config.BreakEvenConfig.TriggerProfitAmount
                            };
                        }
                        
                        // 设置推仓配置
                        if (_config.AddPositionConfig?.IsEnabled == true)
                        {
                            contractProfile.IndependentAddPositionConfig = new ContractAddPositionConfig
                            {
                                IsEnabled = true,
                                Tiers = _config.AddPositionConfig.Tiers.Select(t => new ContractAddPositionTier
                                {
                                    TierIndex = t.TierIndex,
                                    IsEnabled = t.IsEnabled,
                                    TriggerProfitAmount = t.TriggerProfitAmount,
                                    RiskMultiplier = t.RiskMultiplier,
                                    StopLossRatio = t.StopLossRatio,
                                    AddPositionQuantity = 0,
                                    StopLossPrice = 0,
                                    IsExecuted = false
                                }).ToList()
                            };
                        }
                        
                        // 设置保盈配置
                        if (_config.ProfitProtectionConfig?.IsEnabled == true)
                        {
                            contractProfile.IndependentProfitProtectionConfig = new ContractProfitProtectionConfig
                            {
                                IsEnabled = true,
                                Tiers = _config.ProfitProtectionConfig.Tiers.Select(t => new ContractProfitProtectionTier
                                {
                                    TierIndex = t.TierIndex,
                                    IsEnabled = t.IsEnabled,
                                    TriggerProfitAmount = t.TriggerProfitAmount,
                                    ProtectionAmount = t.ProtectionAmount
                                }).ToList()
                            };
                        }
                    }
                    
                    // 🔍【执行引擎调用前诊断】
                    AddWorkLog("INFO", $"🔍【配置检查】{key}: 配置={_config?.Name ?? "NULL"}, 启用={_config?.IsEnabled ?? false}");
                    if (_config != null)
                    {
                        AddWorkLog("INFO", $"🔍【配置详情】{key}: 保本={_config.BreakEvenConfig?.IsEnabled ?? false}, 推仓={_config.AddPositionConfig?.IsEnabled ?? false}, 保盈={_config.ProfitProtectionConfig?.IsEnabled ?? false}");
                        
                        // 🔍【关键调试】显示具体的触发条件数值
                        if (_config.BreakEvenConfig?.IsEnabled == true)
                        {
                            AddWorkLog("INFO", $"🔍【保本触发条件】{key}: {_config.BreakEvenConfig.TriggerProfitAmount:F2}U");
                        }
                        
                        if (_config.AddPositionConfig?.IsEnabled == true)
                        {
                            AddWorkLog("INFO", $"🔍【推仓详情】{key}: 阶梯数={_config.AddPositionConfig.Tiers?.Count ?? 0}");
                            
                            if (_config.AddPositionConfig.Tiers?.Any() == true)
                            {
                                var firstTier = _config.AddPositionConfig.Tiers.Where(t => t.IsEnabled).OrderBy(t => t.TierIndex).FirstOrDefault();
                                if (firstTier != null)
                                {
                                    AddWorkLog("INFO", $"🔍【推仓一阶触发条件】{key}: {firstTier.TriggerProfitAmount:F2}U");
                                }
                            }
                        }
                        
                        // 🔍【关键诊断】配置数据来源分析
                        AddWorkLog("INFO", $"🔍【配置数据来源】{key}: _config对象哈希={_config?.GetHashCode()}, 配置名={_config?.Name}");
                        AddWorkLog("INFO", $"🔍【配置文件路径】{key}: 检查是否从文件重新加载");
                        
                        // 🔍【临时调试】直接从文件读取最新配置进行对比
                        try 
                        {
                            var baseConfigManager = BaseConfigManager.Instance;
                            var fileConfig = baseConfigManager.GetConfiguration(_config?.Name ?? "");
                            if (fileConfig != null)
                            {
                                AddWorkLog("INFO", $"🔍【文件配置对比】{key}: 文件保本触发={fileConfig.BreakEvenConfig?.TriggerProfitAmount:F2}U, 内存保本触发={_config?.BreakEvenConfig?.TriggerProfitAmount:F2}U");
                                
                                if (fileConfig.BreakEvenConfig?.TriggerProfitAmount != _config?.BreakEvenConfig?.TriggerProfitAmount)
                                {
                                    AddWorkLog("WARN", $"⚠️【配置不同步】{key}: 文件配置与内存配置不一致！");
                                    AddWorkLog("WARN", $"   📁 文件中保本触发值: {fileConfig.BreakEvenConfig?.TriggerProfitAmount:F2}U");
                                    AddWorkLog("WARN", $"   🧠 内存中保本触发值: {_config?.BreakEvenConfig?.TriggerProfitAmount:F2}U");
                                }
                                else
                                {
                                    AddWorkLog("INFO", $"✅【配置同步】{key}: 文件配置与内存配置一致");
                                }
                                
                                // 🔍【推仓配置对比】
                                if (fileConfig.AddPositionConfig?.IsEnabled == true && _config.AddPositionConfig?.IsEnabled == true)
                                {
                                    var fileFirstTier = fileConfig.AddPositionConfig.Tiers?.Where(t => t.IsEnabled).OrderBy(t => t.TierIndex).FirstOrDefault();
                                    var memoryFirstTier = _config.AddPositionConfig.Tiers?.Where(t => t.IsEnabled).OrderBy(t => t.TierIndex).FirstOrDefault();
                                    
                                    if (fileFirstTier != null && memoryFirstTier != null)
                                    {
                                        AddWorkLog("INFO", $"🔍【推仓配置对比】{key}: 文件推仓一阶={fileFirstTier.TriggerProfitAmount:F2}U, 内存推仓一阶={memoryFirstTier.TriggerProfitAmount:F2}U");
                                        
                                        if (fileFirstTier.TriggerProfitAmount != memoryFirstTier.TriggerProfitAmount)
                                        {
                                            AddWorkLog("WARN", $"⚠️【推仓配置不同步】{key}: 文件与内存推仓配置不一致！");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                AddWorkLog("WARN", $"⚠️【配置缺失】{key}: 无法从BaseConfigManager获取配置'{_config?.Name}'");
                            }
                        }
                        catch (Exception debugEx)
                        {
                            AddWorkLog("ERROR", $"❌【调试失败】{key}: {debugEx.Message}");
                        }
                        
                        // 🔧【修复整体配置问题】- 如果子配置启用但整体配置未启用，强制启用整体配置
                        if (!_config.IsEnabled && 
                            (_config.BreakEvenConfig?.IsEnabled == true || 
                             _config.AddPositionConfig?.IsEnabled == true || 
                             _config.ProfitProtectionConfig?.IsEnabled == true))
                        {
                            AddWorkLog("INFO", $"🔧【自动修复】{key}: 子配置已启用，自动启用整体配置");
                            _config.IsEnabled = true;
                            AddWorkLog("INFO", $"✅【修复完成】{key}: 整体配置已启用，继续执行比对");
                        }
                    }
                    
                    // 🔍【执行引擎调用】
                    AddWorkLog("INFO", $"🔍【执行引擎调用】{key}: 开始调用ExecuteContractMonitoringAsync");
                    
                    // 🔧 【关键修复】添加执行引擎超时机制，防止卡住
                    try
                    {
                        using var executionTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var executionTask = _executionEngine.ExecuteContractMonitoringAsync(contractProfile);
                        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), executionTimeoutCts.Token);
                        
                        var completedTask = await Task.WhenAny(executionTask, timeoutTask);
                        
                        if (completedTask == timeoutTask)
                        {
                            _logger.LogError($"❌【执行引擎超时】{key}: 执行超时(10秒)，跳过此次处理");
                            AddWorkLog("ERROR", $"❌【执行引擎超时】{key}: 执行超时(10秒)");
                            return;
                        }
                        
                        executionTimeoutCts.Cancel();
                        var summary = await executionTask;
                        
                        AddWorkLog("INFO", $"🔍【执行引擎返回】{key}: 成功={summary.IsSuccess}");
                        
                        // 🔍【手动补充比对日志】- 因为执行引擎的日志系统与UI分离
                        if (_config != null)
                        {
                            // 保本比对日志
                            if (_config.BreakEvenConfig?.IsEnabled == true)
                            {
                                var breakEvenTrigger = _config.BreakEvenConfig.TriggerProfitAmount;
                                if (currentPnl >= breakEvenTrigger)
                                {
                                    AddWorkLog("INFO", $"✅【保本触发】{key}: {currentPnl:F2}U >= {breakEvenTrigger:F2}U");
                                    // 🔍【交易执行检查】
                                    if (summary.BreakEvenResult?.IsSuccess == true)
                                    {
                                        AddWorkLog("SUCCESS", $"🚀【保本执行成功】{key}: 已下保本止损委托");
                                    }
                                    else if (summary.BreakEvenResult != null)
                                    {
                                        AddWorkLog("ERROR", $"❌【保本执行失败】{key}: {summary.BreakEvenResult.Message}");
                                    }
                                    else
                                    {
                                        AddWorkLog("WARN", $"⚠️【保本未执行】{key}: 触发条件满足但无执行结果，请检查执行引擎");
                                    }
                                }
                                // 🔧 【简化日志】保本未触发时静默跳过，避免冗余日志
                            }
                            else
                            {
                                AddWorkLog("INFO", $"❌【保本跳过】{key}: 配置未启用");
                            }
                            
                            // 推仓比对日志
                            if (_config.AddPositionConfig?.IsEnabled == true && _config.AddPositionConfig.Tiers?.Any() == true)
                            {
                                AddWorkLog("INFO", $"🔍【推仓检查】{key}: 当前浮盈{currentPnl:F2}U，检查{_config.AddPositionConfig.Tiers.Count(t => t.IsEnabled)}个阶梯");
                                
                                var successfulAddPositions = summary.AddPositionResults?.Count(r => r.IsSuccess) ?? 0;
                                var failedAddPositions = summary.AddPositionResults?.Count(r => !r.IsSuccess) ?? 0;
                                
                                foreach (var tier in _config.AddPositionConfig.Tiers.Where(t => t.IsEnabled).OrderBy(t => t.TierIndex))
                                {
                                    if (currentPnl >= tier.TriggerProfitAmount)
                                    {
                                        AddWorkLog("INFO", $"✅【推仓触发】{key}-阶梯{tier.TierIndex}: {currentPnl:F2}U >= {tier.TriggerProfitAmount:F2}U");
                                    }
                                    else
                                    {
                                        // 🔧 【简化日志】推仓未触发时静默跳过，避免冗余日志
                                        break; // 后续阶梯肯定也不触发
                                    }
                                }
                                
                                // 🔍【推仓执行结果检查】
                                if (successfulAddPositions > 0)
                                {
                                    AddWorkLog("SUCCESS", $"🚀【推仓执行成功】{key}: 成功执行{successfulAddPositions}个阶梯的加仓");
                                }
                                if (failedAddPositions > 0)
                                {
                                    AddWorkLog("ERROR", $"❌【推仓执行失败】{key}: {failedAddPositions}个阶梯执行失败");
                                }
                                if (successfulAddPositions == 0 && failedAddPositions == 0 && currentPnl >= _config.AddPositionConfig.Tiers.Where(t => t.IsEnabled).Min(t => t.TriggerProfitAmount))
                                {
                                    AddWorkLog("WARN", $"⚠️【推仓未执行】{key}: 触发条件满足但无执行结果，请检查执行引擎");
                                }
                            }
                            else
                            {
                                AddWorkLog("INFO", $"❌【推仓跳过】{key}: 配置未启用");
                            }
                            
                            // 保盈比对日志
                            if (_config.ProfitProtectionConfig?.IsEnabled == true && _config.ProfitProtectionConfig.Tiers?.Any() == true)
                            {
                                var enabledTiers = _config.ProfitProtectionConfig.Tiers.Where(t => t.IsEnabled).OrderBy(t => t.TierIndex);
                                AddWorkLog("INFO", $"🔍【保盈检查】{key}: 当前浮盈{currentPnl:F2}U，检查{enabledTiers.Count()}个阶梯");
                                
                                var triggeredTier = enabledTiers.Where(t => currentPnl >= t.TriggerProfitAmount)
                                                              .OrderByDescending(t => t.TriggerProfitAmount)
                                                              .FirstOrDefault();
                                
                                if (triggeredTier != null)
                                {
                                    AddWorkLog("INFO", $"✅【保盈触发】{key}-阶梯{triggeredTier.TierIndex}: {currentPnl:F2}U >= {triggeredTier.TriggerProfitAmount:F2}U");
                                }
                                // 🔧 【简化日志】保盈未触发时静默跳过，避免冗余日志
                            }
                            else
                            {
                                AddWorkLog("INFO", $"❌【保盈跳过】{key}: 配置未启用");
                            }
                        }
                    
                    // 🔧 记录执行结果并更新状态
                    // 🔍【状态更新事件诊断】详细分析执行结果
                    _logger.LogCritical($"🔍【状态更新事件诊断】{key} 执行结果分析:");
                    _logger.LogCritical($"   📊 Summary.IsSuccess: {summary.IsSuccess}");
                    _logger.LogCritical($"   📈 BreakEvenResult: IsSuccess={summary.BreakEvenResult?.IsSuccess}, Message={summary.BreakEvenResult?.Message}");
                    _logger.LogCritical($"   📈 AddPositionResults: Count={summary.AddPositionResults?.Count ?? 0}");
                    _logger.LogCritical($"   📈 ProfitProtectionResults: Count={summary.ProfitProtectionResults?.Count ?? 0}");
                    
                    if (summary.IsSuccess)
                    {
                        var breakEvenCount = summary.BreakEvenResult?.IsSuccess == true ? 1 : 0;
                        var addPositionCount = summary.AddPositionResults?.Count(r => r.IsSuccess) ?? 0;
                        var profitProtectionCount = summary.ProfitProtectionResults?.Count(r => r.IsSuccess) ?? 0;
                        
                        _logger.LogCritical($"   🎯 执行统计: 保本={breakEvenCount}, 推仓={addPositionCount}, 保盈={profitProtectionCount}");
                        _logger.LogCritical($"   🎯 是否触发状态更新: {(breakEvenCount > 0 || addPositionCount > 0 || profitProtectionCount > 0)}");
                        
                        if (breakEvenCount > 0 || addPositionCount > 0 || profitProtectionCount > 0)
                        {
                            AddWorkLog("SUCCESS", $"✅ {key}: 保本: {breakEvenCount}, 推仓: {addPositionCount}, 保盈: {profitProtectionCount}");
                            
                            // 🔍【状态更新】- 通知UI更新状态显示
                            _logger.LogCritical($"🚀【触发状态更新事件】{key} 调用NotifyStatusUpdated");
                            
                            // 🔧 修复：转换为UI层期望的持仓方向格式
                            var actualPositionSide = position.PositionAmt > 0 ? "LONG" : "SHORT";
                            NotifyStatusUpdated(position.Symbol, actualPositionSide, summary);
                            _logger.LogCritical($"✅【状态更新事件已发送】{key} - 方向: {actualPositionSide}");
                        }
                        else
                        {
                            _logger.LogCritical($"⚠️【跳过状态更新】{key} 没有成功的执行操作");
                        }
                    }
                    else
                    {
                        _logger.LogCritical($"❌【Summary执行失败】{key} Summary.IsSuccess=false，不触发状态更新");
                        AddWorkLog("WARN", $"❌ {key}: 执行失败 - {summary.Message}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"执行引擎处理持仓失败: {key}");
                    AddWorkLog("ERROR", $"❌ 执行引擎异常: {key} - {ex.Message}");
                    
                    // 🔧 关键修复：如果执行引擎失败，尝试使用旧的检查逻辑作为备用
                    _logger.LogCritical($"🔄 [FALLBACK] 执行引擎失败，尝试使用旧检查逻辑作为备用...");
                    try
                    {
                        // 检查推仓
                        if (_config?.AddPositionConfig?.IsEnabled == true)
                        {
                            await CheckAddPositionTriggersAsync(position, profile, currentPnl);
                        }
                        
                        // 检查保本
                        if (_config?.BreakEvenConfig?.IsEnabled == true)
                        {
                            await CheckBreakEvenTriggerAsync(position, profile, currentPnl);
                        }
                        
                        // 检查保盈
                        if (_config?.ProfitProtectionConfig?.IsEnabled == true)
                        {
                            await CheckProfitProtectionTriggersAsync(position, profile, currentPnl);
                        }
                        
                        _logger.LogCritical($"✅ [FALLBACK] 旧检查逻辑执行完成");
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogCritical($"❌ [FALLBACK-ERROR] 备用逻辑也失败: {fallbackEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行推仓失败: {position.Symbol}");
                return;
            }
        }

        /// <summary>
        /// 检查自动保本触发条件
        /// </summary>
        private async Task<bool> CheckBreakEvenTriggerAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            // 🔍 保本检查调试 - 关键诊断信息
            _logger.LogInformation($"🔍 【保本检查】{position.Symbol} 浮盈{currentPnl:F2}U vs 触发条件{_config!.BreakEvenConfig.TriggerProfitAmount:F2}U");
            
            // 检查1：配置是否启用
            if (!_config!.BreakEvenConfig.IsEnabled) 
            {
                _logger.LogWarning($"❌ 【保本跳过】{position.Symbol} 保本配置未启用");
                return false;
            }
            
            // 检查2：浮盈是否达到触发条件
            if (currentPnl <= _config.BreakEvenConfig.TriggerProfitAmount)
            {
                return false; // 不记录日志，避免频繁输出
            }
            
            _logger.LogInformation($"✅ 【保本触发】{position.Symbol} 浮盈{currentPnl:F2}U 达到触发条件{_config.BreakEvenConfig.TriggerProfitAmount:F2}U");

            // 检查3：是否已执行过
            // 🚨 关键修复：使用标准化的LONG/SHORT而不是BOTH，确保与执行引擎的键值一致
            var standardizedPositionSide = position.PositionAmt > 0 ? "LONG" : "SHORT";
            var isExecutedInState = _unifiedStateManager.IsExecuted(position.Symbol, standardizedPositionSide, ExecutionType.BreakEven);
            
            // 🔧 【统一状态检查】只有状态为0（未触发）时才执行
            var configState = _config.BreakEvenConfig.ExecutionState;
            
            // 🔧 【关键诊断】记录详细的状态检查信息
            _logger.LogCritical($"🔍【保本状态检查-服务层】{position.Symbol}:");
            _logger.LogCritical($"   📊 Config.ExecutionState: {(int)configState} ({configState})");
            _logger.LogCritical($"   📊 StateManager.IsExecuted: {isExecutedInState}");
            _logger.LogCritical($"   🔧 检查键值: {position.Symbol}_{standardizedPositionSide}_BreakEven");
            
            // 🎯 核心判断：只有状态为0（未触发）时才执行
            if (configState != ExecutionState.NotTriggered || isExecutedInState)
            {
                _logger.LogWarning($"🔍【保本跳过-服务层】{position.Symbol}: 状态不允许执行");
                _logger.LogWarning($"   🔧 状态详情: ConfigState={(int)configState}, StateManager={isExecutedInState}");
                _logger.LogWarning($"   ✅ 只有状态=0时才执行，当前状态={(int)configState}");
                return false;
            }

            // 检查4：冷却期检查
            // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
            var operationKey = CooldownManager.GenerateOperationKey(position.Symbol, standardizedPositionSide, CooldownOperationType.BreakEven);
            var canExecute = _cooldownManager.CanExecute(operationKey, CooldownOperationType.BreakEven);
            if (!canExecute)
            {
                var remainingTime = _cooldownManager.GetRemainingCooldown(operationKey, CooldownOperationType.BreakEven);
                _logger.LogWarning($"❌ 【保本跳过】{position.Symbol} 冷却期中，剩余{remainingTime.TotalSeconds:F1}秒");
                return false;
            }
            
            // ✅ 所有检查通过，开始执行保本
            _logger.LogInformation($"🚀 【保本执行】{position.Symbol} 开始执行保本止损");

            // 🔒 先标记为执行中状态，防止重复触发
            // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
            _unifiedStateManager.MarkAsExecuting(position.Symbol, standardizedPositionSide, 
                ExecutionType.BreakEven, null, currentPnl, "自动保本开始执行");
            _logger.LogInformation($"   🔧 标记执行键值: {position.Symbol}_{standardizedPositionSide}_BreakEven");
                
            // 🛡️ 立即记录冷却期，防止短时间内重复扫描
            _cooldownManager.RecordExecution(operationKey);

            try
            {
                var success = await ExecuteBreakEvenStopLossAsync(position);
                
                // 🔄 记录最终执行结果
                // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                _unifiedStateManager.RecordExecution(position.Symbol, standardizedPositionSide, 
                    ExecutionType.BreakEven, 0, currentPnl, success, 
                    success ? "自动保本执行成功" : "自动保本执行失败",
                    autoSave: false);
                _logger.LogInformation($"   🔧 记录执行结果键值: {position.Symbol}_{standardizedPositionSide}_BreakEven");
                
                // 🔧 【关键修复】同时更新配置层状态，防止重复执行
                if (success)
                {
                    _config.BreakEvenConfig.ExecutionState = ExecutionState.Executed;
                    _config.BreakEvenConfig.ExecutionTime = DateTime.Now;
                    _logger.LogCritical($"🔧【重要标记-服务层】{position.Symbol}: Config.BreakEven.ExecutionState设为1（已执行），防止重复执行");
                    
                    // 🔧 【关键修复】保本执行成功后，立即保存状态到文件，确保与界面同步
                    try
                    {
                        _unifiedStateManager.SaveToPersistence();
                        _logger.LogCritical($"💾【状态保存-统一管理器】{position.Symbol}: 保本状态已保存到文件，与界面同步");
                        
                        // 🔧 【双重保险】直接更新ContractMonitoringStateService，确保文件状态正确
                        var contractKey = $"{position.Symbol}_{standardizedPositionSide}";
                        if (_stateService != null)
                        {
                            _stateService.UpdateExecutionStatus(contractKey, "BreakEven", null, true, currentPnl, "保本执行成功");
                            _logger.LogCritical($"💾【状态保存-直接文件】{position.Symbol}: 直接更新监控状态文件保本状态");
                        }
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, $"❌ 保存保本状态失败: {position.Symbol}");
                    }
                }
                
                var triggerKey = $"{GetPositionKey(position.Symbol, standardizedPositionSide)}_BreakEven";
                RecordTriggerExecution(profile, position, triggerKey, "自动保本", currentPnl, success);
                
                _logger.LogInformation($"{(success ? "✅" : "❌")} 【保本结果】{position.Symbol} {(success ? "成功" : "失败")}");
                return true; // 表示执行了操作
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 【保本异常】{position.Symbol} 执行失败: {ex.Message}");
                
                // 🔄 记录异常状态为执行失败
                // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                _unifiedStateManager.RecordExecution(position.Symbol, standardizedPositionSide, 
                    ExecutionType.BreakEven, 0, currentPnl, false, ex.Message,
                    autoSave: false);
                
                // 注意：冷却期已在执行前记录，此处不需要重复记录
                
                // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                // _contractStateManager.MarkAsTriggered(position.Symbol, position.PositionSideString, 
                //     ExecutionType.BreakEven, null, currentPnl, false, ex.Message);
                
                var triggerKey = $"{GetPositionKey(position.Symbol, standardizedPositionSide)}_BreakEven";
                RecordTriggerExecution(profile, position, triggerKey, "自动保本", currentPnl, false);
                return true; // 表示执行了操作（即使失败）
            }
        }

        /// <summary>
        /// 检查自动推仓触发条件
        /// </summary>
        private async Task<bool> CheckAddPositionTriggersAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            // 🔍 推仓触发条件诊断（简化版）
            _logger.LogInformation($"🔍 {position.Symbol} 推仓检查: 浮盈{currentPnl:F2}U");
            
            if (!_config!.AddPositionConfig.IsEnabled)
            {
                _logger.LogInformation($"   ❌ 推仓功能未启用");
                return false;
            }
            
            // 🔍 显示所有阶梯的触发条件，让用户知道需要多少浮盈才能触发
            var allEnabledStages = _config.AddPositionConfig.Tiers.Where(t => t.IsEnabled).OrderBy(s => s.TriggerProfitAmount);
            if (!allEnabledStages.Any())
            {
                _logger.LogInformation($"   ❌ 没有启用的推仓阶梯");
                return false;
            }
            
            _logger.LogInformation($"   📋 推仓阶梯要求: {string.Join(", ", allEnabledStages.Select(s => $"阶梯{s.TierIndex}需要{s.TriggerProfitAmount:F0}U"))}");
            
            // 🔍 检查是否有任何阶梯可以触发
            var triggerableStages = allEnabledStages.Where(s => currentPnl > s.TriggerProfitAmount).ToList();
            if (!triggerableStages.Any())
            {
                var nextStage = allEnabledStages.FirstOrDefault(s => s.TriggerProfitAmount > currentPnl);
                if (nextStage != null)
                {
                    var needed = nextStage.TriggerProfitAmount - currentPnl;
                    _logger.LogInformation($"   💡 下一阶梯{nextStage.TierIndex}还需要{needed:F2}U浮盈才能触发（需要{nextStage.TriggerProfitAmount:F0}U）");
                }
                else
                {
                    _logger.LogInformation($"   💡 当前浮盈已超过所有阶梯要求");
                }
                return false;
            }

            // 🔄 使用新的合约状态管理器，解决多合约冲突问题
            var enabledStages = _config.AddPositionConfig.Tiers.OrderBy(s => s.TriggerProfitAmount);
            
            foreach (var stage in enabledStages)
            {
                if (!stage.IsEnabled)
                {
                    continue;
                }
                
                if (currentPnl <= stage.TriggerProfitAmount)
                {
                    _logger.LogCritical($"🚀 {position.Symbol} 推仓{stage.TierIndex}档检查: 浮盈{currentPnl:F2}U 未达到触发条件{stage.TriggerProfitAmount:F2}U");
                    continue;
                }

                // 🔄 使用统一状态管理器检查该阶梯是否已执行
                // 🚨 关键修复：使用标准化的LONG/SHORT而不是BOTH，确保与执行引擎的键值一致
                var standardizedPositionSide = position.PositionAmt > 0 ? "LONG" : "SHORT";
                var isExecutedInState = _unifiedStateManager.IsExecuted(position.Symbol, standardizedPositionSide, 
                    ExecutionType.AddPosition, stage.TierIndex);
                
                // 🔧 【统一状态检查】只有状态为0（未触发）时才执行
                var configState = stage.ExecutionState;
                
                // 🔧 【关键诊断】记录详细的状态检查信息
                _logger.LogCritical($"🔍【推仓状态检查-服务层】{position.Symbol}-阶梯{stage.TierIndex}:");
                _logger.LogCritical($"   📊 Config.ExecutionState: {(int)configState} ({configState})");
                _logger.LogCritical($"   📊 StateManager.IsExecuted: {isExecutedInState}");
                _logger.LogCritical($"   🔧 检查键值: {position.Symbol}_{standardizedPositionSide}_AddPosition_{stage.TierIndex}");
                
                // 🎯 核心判断：只有状态为0（未触发）时才执行
                if (configState != ExecutionState.NotTriggered || isExecutedInState)
                {
                    _logger.LogWarning($"🔍【推仓跳过-服务层】{position.Symbol}-阶梯{stage.TierIndex}: 状态不允许执行");
                    _logger.LogWarning($"   🔧 状态详情: ConfigState={(int)configState}, StateManager={isExecutedInState}");
                    _logger.LogWarning($"   ✅ 只有状态=0时才执行，当前状态={(int)configState}");
                    continue;
                }
                
                _logger.LogCritical($"🚀 {position.Symbol} 推仓{stage.TierIndex}档触发条件满足: {currentPnl:F2}U > {stage.TriggerProfitAmount:F2}U");

                // 🛡️ 检查冷却期：防止短时间内重复执行推仓操作  
                // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                var operationKey = CooldownManager.GenerateOperationKey(position.Symbol, standardizedPositionSide, 
                    CooldownOperationType.AddPosition, stage.TierIndex);
                
                // 🔧 冷却期超级详细调试
                var remainingTime = _cooldownManager.GetRemainingCooldown(operationKey, CooldownOperationType.AddPosition);
                _logger.LogInformation($"🔧 阶梯{stage.TierIndex}冷却期详细检查:");
                _logger.LogInformation($"🔧   操作键: {operationKey}");
                _logger.LogInformation($"🔧   冷却期配置: {CooldownOperationType.AddPosition} = 5秒");
                _logger.LogInformation($"🔧   剩余冷却时间: {remainingTime.TotalSeconds:F1}秒");
                
                // 🔧 新增：更详细的状态检查 - 查看内部执行历史
                var recentHistory = _unifiedStateManager.GetExecutionHistory(position.Symbol, ExecutionType.AddPosition);
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
                // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                _unifiedStateManager.MarkAsExecuting(position.Symbol, standardizedPositionSide, 
                    ExecutionType.AddPosition, stage.TierIndex, currentPnl, 
                    $"推仓阶梯{stage.TierIndex}开始执行");
                _logger.LogInformation($"   🔧 标记执行键值: {position.Symbol}_{standardizedPositionSide}_AddPosition_{stage.TierIndex}");
                
                // 🛡️ 立即记录冷却期，防止短时间内重复扫描
                _cooldownManager.RecordExecution(operationKey);

                try
                {
                    _logger.LogInformation($"🚀 触发推仓阶梯{stage.TierIndex}: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {stage.TriggerProfitAmount:F2}U");
                    
                    // 🎯 重要提示：检查执行模式
                    var executionMode = IsSimulationEnvironment() ? "模拟模式" : "实盘模式";
                    _logger.LogCritical($"🎯 【重要提示】当前执行模式: {executionMode}");
                    if (IsSimulationEnvironment())
                    {
                        _logger.LogWarning($"⚠️ 【模拟模式】推仓将不会进行真实下单，仅记录日志");
                        _logger.LogWarning($"💡 如需真实下单，请设置有效的API Key和Secret Key");
                    }
                    
                    var success = await ExecuteAddPositionAsync(position, stage);
                    
                    // 🔄 根据执行结果更新最终状态
                    // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                    _unifiedStateManager.RecordExecution(position.Symbol, standardizedPositionSide, 
                        ExecutionType.AddPosition, stage.TierIndex, currentPnl, success, 
                        success ? $"推仓阶梯{stage.TierIndex}执行成功" : $"推仓阶梯{stage.TierIndex}执行失败", 
                        autoSave: false);  // 统一记录最终执行结果
                    _logger.LogInformation($"   🔧 记录执行结果键值: {position.Symbol}_{standardizedPositionSide}_AddPosition_{stage.TierIndex}");
                    
                    // 🔧 【关键修复】同时更新配置层状态，防止重复执行
                    if (success)
                    {
                        stage.ExecutionState = ExecutionState.Executed;
                        stage.ExecutionTime = DateTime.Now;
                        _logger.LogCritical($"🔧【重要标记-服务层】{position.Symbol}-阶梯{stage.TierIndex}: Config.ExecutionState设为1（已执行），防止重复执行");
                        
                        // 🔧 【关键修复】推仓执行成功后，立即保存状态到文件，确保与界面同步
                        try
                        {
                            _unifiedStateManager.SaveToPersistence();
                            _logger.LogCritical($"💾【状态保存-统一管理器】{position.Symbol}-阶梯{stage.TierIndex}: 状态已保存到文件，与界面同步");
                            
                            // 🔧 【双重保险】直接更新ContractMonitoringStateService，确保文件状态正确
                            var contractKey = $"{position.Symbol}_{standardizedPositionSide}";
                            if (_stateService != null)
                            {
                                _stateService.UpdateExecutionStatus(contractKey, "AddPosition", stage.TierIndex, true, currentPnl, $"推仓阶梯{stage.TierIndex}执行成功");
                                _logger.LogCritical($"💾【状态保存-直接文件】{position.Symbol}-阶梯{stage.TierIndex}: 直接更新监控状态文件");
                            }
                        }
                        catch (Exception saveEx)
                        {
                            _logger.LogError(saveEx, $"❌ 保存推仓状态失败: {position.Symbol}-阶梯{stage.TierIndex}");
                        }
                    }
                    
                    // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                    // _contractStateManager.MarkAsTriggered(position.Symbol, position.PositionSideString, 
                    //     ExecutionType.AddPosition, stage.TierIndex, currentPnl, success, 
                    //     success ? $"推仓阶梯{stage.TierIndex}执行成功" : $"推仓阶梯{stage.TierIndex}执行失败");
                    
                    // 🔧 修复：使用统一的执行历史记录机制，避免重复记录  
                    // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                    var triggerKey = $"{GetPositionKey(position.Symbol, standardizedPositionSide)}_AddPosition_Stage{stage.TierIndex}";
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
                    // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                    _unifiedStateManager.RecordExecution(position.Symbol, standardizedPositionSide, 
                        ExecutionType.AddPosition, stage.TierIndex, currentPnl, false, ex.Message, 
                        autoSave: false);  // 记录异常信息
                    
                    // 注意：不再重复记录冷却期，因为前面已经记录了
                    
                    // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                    // _contractStateManager.MarkAsTriggered(position.Symbol, position.PositionSideString, 
                    //     ExecutionType.AddPosition, stage.TierIndex, currentPnl, false, ex.Message);
                    
                    // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                    var triggerKey = $"{GetPositionKey(position.Symbol, standardizedPositionSide)}_AddPosition_Stage{stage.TierIndex}";
                    RecordTriggerExecution(profile, position, triggerKey, $"推仓阶梯{stage.TierIndex}", currentPnl, false);
                    return true; // 表示执行了操作（即使失败）
                }
            }
            
            _logger.LogCritical($"🔍 推仓诊断结束 - 合约: {position.Symbol}, 结果: 没有触发任何阶梯");
            return false; // 没有执行任何操作
        }

        /// <summary>
        /// 检查保盈止损触发条件
        /// </summary>
        private async Task<bool> CheckProfitProtectionTriggersAsync(PositionInfo position, PositionProfile profile, decimal currentPnl)
        {
            // 🔧 新增：详细诊断日志（使用Critical级别确保显示）
            _logger.LogCritical($"🔍 {position.Symbol} 保盈诊断开始:");
            _logger.LogCritical($"   📊 当前浮盈: {currentPnl:F2}U");
            _logger.LogCritical($"   ⚙️ 保盈配置启用: {_config!.ProfitProtectionConfig.IsEnabled}");
            
            if (!_config!.ProfitProtectionConfig.IsEnabled) 
            {
                _logger.LogCritical($"   ❌ 保盈配置未启用，跳过");
                return false;
            }

            // 🔧 修复：移除全局IsTriggered检查，只依赖合约独立的TriggerRecords机制
            var enabledStages = _config.ProfitProtectionConfig.Tiers.OrderBy(s => s.TriggerProfitAmount);
            
            foreach (var stage in enabledStages)
            {
                if (currentPnl <= stage.TriggerProfitAmount)
                {
                    _logger.LogInformation($"🛡️ {position.Symbol} 保盈{stage.TierIndex}档检查: 浮盈{currentPnl:F2}U 未达到触发条件{stage.TriggerProfitAmount:F2}U");
                    continue;
                }

                // 🔄 使用统一状态管理器检查该阶梯是否已执行
                // 🚨 关键修复：使用标准化的LONG/SHORT而不是BOTH，确保与执行引擎的键值一致
                var standardizedPositionSide = position.PositionAmt > 0 ? "LONG" : "SHORT";
                if (_unifiedStateManager.IsExecuted(position.Symbol, standardizedPositionSide, 
                    ExecutionType.ProfitProtection, stage.TierIndex))
                {
                    _logger.LogInformation($"🛡️ {position.Symbol} 保盈{stage.TierIndex}档检查: 已执行过，跳过");
                    _logger.LogInformation($"   🔧 状态检查键值: {position.Symbol}_{standardizedPositionSide}_ProfitProtection_{stage.TierIndex}");
                    continue;
                }
                
                // ⚠️ 向后兼容：保留旧格式检查（后续版本将移除）
                var triggerKey = $"{GetPositionKey(position.Symbol, standardizedPositionSide)}_ProfitProtection_Stage{stage.TierIndex}";
                if (profile.TriggerRecords.ContainsKey(triggerKey))
                {
                    _logger.LogInformation($"🛡️ {position.Symbol} 保盈{stage.TierIndex}档检查: 已在旧系统执行过，跳过");
                    continue;
                }
                
                _logger.LogInformation($"🛡️ {position.Symbol} 保盈{stage.TierIndex}档触发条件满足: {currentPnl:F2}U >= {stage.TriggerProfitAmount:F2}U");

                // 🛡️ 检查冷却期：防止短时间内重复扫描
                // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                var operationKey = CooldownManager.GenerateOperationKey(position.Symbol, standardizedPositionSide, 
                    CooldownOperationType.ProfitProtection, stage.TierIndex);
                if (!_cooldownManager.CanExecute(operationKey, CooldownOperationType.ProfitProtection))
                {
                    var remainingTime = _cooldownManager.GetRemainingCooldown(operationKey, CooldownOperationType.ProfitProtection);
                    _logger.LogDebug($"🔒 保盈止损阶梯{stage.TierIndex}冷却中: {position.Symbol}, 剩余: {remainingTime.TotalSeconds:F1}秒");
                    continue;
                }

                // 🔧 关键改进：立即标记状态为"执行中"，防止重复触发
                _logger.LogInformation($"🔒 立即标记保盈止损阶梯{stage.TierIndex}为执行中状态，防止重复触发");
                // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                _unifiedStateManager.MarkAsExecuting(position.Symbol, standardizedPositionSide,
                    ExecutionType.ProfitProtection, stage.TierIndex, currentPnl,
                    $"保盈止损阶梯{stage.TierIndex}开始执行");
                _logger.LogInformation($"   🔧 标记执行键值: {position.Symbol}_{standardizedPositionSide}_ProfitProtection_{stage.TierIndex}");
                
                // 🛡️ 立即记录冷却期，防止短时间内重复扫描
                _cooldownManager.RecordExecution(operationKey);

                try
                {
                    _logger.LogInformation($"🛡️ 触发保盈止损阶梯{stage.TierIndex}: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {stage.TriggerProfitAmount:F2}U");
                    
                    var success = await ExecuteProfitProtectionAsync(position, stage);
                    
                    // 🔄 根据执行结果更新最终状态
                    // 🚨 关键修复：使用标准化的持仓方向，确保键值一致性
                    _unifiedStateManager.RecordExecution(position.Symbol, standardizedPositionSide,
                        ExecutionType.ProfitProtection, stage.TierIndex, currentPnl, success,
                        success ? $"保盈止损阶梯{stage.TierIndex}执行成功" : $"保盈止损阶梯{stage.TierIndex}执行失败",
                        autoSave: false);
                    _logger.LogInformation($"   🔧 记录执行结果键值: {position.Symbol}_{standardizedPositionSide}_ProfitProtection_{stage.TierIndex}");
                    
                    // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                    RecordTriggerExecution(profile, position, triggerKey, $"保盈止损阶梯{stage.TierIndex}", currentPnl, success);
                    
                    // 🔧 修复：不再设置全局IsTriggered状态，防止影响其他合约
                    if (success)
                    {
                        _logger.LogInformation($"✅ 保盈止损阶梯{stage.TierIndex}执行成功: {position.Symbol} (其他合约仍可独立触发此阶梯)");
                        
                        // 🔧 【关键修复】保盈执行成功后，立即保存状态到文件，确保与界面同步
                        try
                        {
                            _unifiedStateManager.SaveToPersistence();
                            _logger.LogCritical($"💾【状态保存-统一管理器】{position.Symbol}-阶梯{stage.TierIndex}: 保盈状态已保存到文件，与界面同步");
                            
                            // 🔧 【双重保险】直接更新ContractMonitoringStateService，确保文件状态正确
                            var contractKey = $"{position.Symbol}_{standardizedPositionSide}";
                            if (_stateService != null)
                            {
                                _stateService.UpdateExecutionStatus(contractKey, "ProfitProtection", stage.TierIndex, true, currentPnl, $"保盈阶梯{stage.TierIndex}执行成功");
                                _logger.LogCritical($"💾【状态保存-直接文件】{position.Symbol}-阶梯{stage.TierIndex}: 直接更新监控状态文件");
                            }
                        }
                        catch (Exception saveEx)
                        {
                            _logger.LogError(saveEx, $"❌ 保存保盈状态失败: {position.Symbol}-阶梯{stage.TierIndex}");
                        }
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

                // 🚨 修复：保本止损也需要使用正确的PositionSide
                string breakEvenPositionSide = position.PositionAmt > 0 ? "LONG" : "SHORT";

                // 🔧 关键修复：为止损单创建API添加超时控制
                var stopLossRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = side,
                    Type = "STOP_MARKET",
                    Quantity = quantity,
                    StopPrice = stopPrice,
                    ReduceOnly = true,
                    PositionSide = breakEvenPositionSide,  // 🚨 使用明确的LONG/SHORT
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

                // 6. 🎯 单向持仓模式加仓逻辑（用户确认使用单向持仓模式）
                // 在单向持仓模式下：
                // - PositionSide 固定为 "BOTH"（由BinanceService自动设置）
                // - 多空方向通过持仓数量正负号区分：+数=多头，-数=空头
                // - 加仓方向通过Side参数控制：多头加仓用BUY，空头加仓用SELL
                // - Quantity参数必须是正数（绝对值）
                
                string addPositionSide;
                string positionType;
                
                if (position.PositionAmt > 0)
                {
                    // 多头持仓：使用BUY增加多头持仓
                    addPositionSide = "BUY";
                    positionType = "多头";
                }
                else if (position.PositionAmt < 0)
                {
                    // 空头持仓：使用SELL增加空头持仓  
                    addPositionSide = "SELL";
                    positionType = "空头";
                }
                else
                {
                    _logger.LogError($"❌ 持仓数量为零，无法执行加仓: {position.Symbol}");
                    return false;
                }
                
                // 🔧 关键诊断：详细记录单向持仓模式加仓逻辑
                _logger.LogCritical($"🔍 单向持仓模式推仓诊断: {position.Symbol}");
                _logger.LogCritical($"   📊 当前持仓数量: {position.PositionAmt:F6}");
                _logger.LogCritical($"   📍 持仓类型: {positionType}（{(position.PositionAmt > 0 ? "正数" : "负数")}）");
                _logger.LogCritical($"   🎯 加仓方向(Side): {addPositionSide}");
                _logger.LogCritical($"   💰 加仓数量(Quantity): {addQuantity:F6}（必须为正数）");
                _logger.LogCritical($"   🏷️ PositionSide: BOTH（单向持仓模式固定值）");
                _logger.LogCritical($"   📈 预期效果: 增加{positionType}持仓");
                _logger.LogCritical($"   🔧 API逻辑: 持仓数量 {position.PositionAmt:F6} + {addPositionSide}({addQuantity:F6}) = 新持仓");
                
                // 7. 🔧 统一修复：与主界面下单保持一致，完全依赖BinanceService自动处理PositionSide
                // 不再手动设置PositionSide，让BinanceService根据持仓模式自动处理（单向持仓→BOTH，双向持仓→LONG/SHORT）
                
                var addOrderRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = addPositionSide,
                    Type = "MARKET",
                    Quantity = addQuantity,
                    TimeInForce = "GTC",
                    // 🔧 修复：移除手动PositionSide设置，与主界面下单保持一致，完全依赖BinanceService自动处理
                    ReduceOnly = false  // 🔧 明确设置为false，确保是加仓而不是减仓
                };
                
                // 🔍 最终下单参数确认
                _logger.LogCritical($"🚀 最终下单参数确认:");
                _logger.LogCritical($"   Symbol: {addOrderRequest.Symbol}");
                _logger.LogCritical($"   Side: {addOrderRequest.Side} (推仓方向)");
                _logger.LogCritical($"   Type: {addOrderRequest.Type}");
                _logger.LogCritical($"   Quantity: {addOrderRequest.Quantity:F6}");
                _logger.LogCritical($"   PositionSide: 由BinanceService自动处理 (无需手动设置)");
                _logger.LogCritical($"   ReduceOnly: {addOrderRequest.ReduceOnly} (必须为false)");
                _logger.LogCritical($"🎯 统一修复说明: 与主界面下单保持一致，由BinanceService根据持仓模式自动设置PositionSide");

                bool addOrderSuccess = false;
                try
                {
                    // 🚀 修复：使用智能下单服务，提高自动盯盘成功率
                    _logger.LogInformation($"🚀 推仓下单切换到智能下单服务: {position.Symbol}");
                    
                    using var orderTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 增加超时时间给智能重试
                    var smartOrderTask = _smartOrderService.PlaceSmartOrderAsync(addOrderRequest, position);
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
                
                // 🚨 关键修复：纠正止损价格计算公式
                // 正确的计算公式：
                // 多头：止损价 = 成本价 - (保盈金额 / 持仓数量) [止损价在成本价之下，确保保盈]
                // 空头：止损价 = 成本价 + (保盈金额 / 持仓数量) [止损价在成本价之上，确保保盈]
                decimal newStopPrice;
                if (updatedPosition.PositionAmt > 0) // 多头
                {
                    newStopPrice = entryPrice - (profitProtectionAmount / stopQuantity); // 多头止损价在成本价下方
                    _logger.LogCritical($"🔍 多头止损计算: {entryPrice:F4} - ({profitProtectionAmount:F2} / {stopQuantity:F6}) = {newStopPrice:F4}");
                }
                else // 空头
                {
                    newStopPrice = entryPrice + (profitProtectionAmount / stopQuantity); // 空头止损价在成本价上方
                    _logger.LogCritical($"🔍 空头止损计算: {entryPrice:F4} + ({profitProtectionAmount:F2} / {stopQuantity:F6}) = {newStopPrice:F4}");
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
                    // PositionSide由BinanceService自动处理，无需手动设置
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
                
                // 🔄 推仓成功后，通知UI更新合约配置状态
                try
                {
                    _logger.LogInformation($"🔄 推仓成功后更新合约配置状态: {position.Symbol} 阶梯{stage.TierIndex}");
                    
                    // 发送推仓执行状态变更事件
                    var executionEvent = new ExecutionStateChangedEvent
                    {
                        Timestamp = DateTime.Now,
                        Symbol = position.Symbol,
                        PositionSide = position.PositionAmt > 0 ? "LONG" : "SHORT",  // 简单记录，仅用于日志显示
                        ExecutionType = Models.ExecutionType.AddPosition,
                        TierIndex = stage.TierIndex,
                        IsSuccess = true,
                        TriggerPnl = position.UnrealizedProfit,
                        Message = $"推仓阶梯{stage.TierIndex}执行成功，数量: {addQuantity:F6}",
                        PreviousState = "NotExecuted",
                        NewState = "Executed",
                        Source = "AutoMonitorService",
                        Priority = EventPriority.High
                    };
                    
                    // 通过事件总线发送状态更新事件
                    if (_eventBus != null)
                    {
                        await _eventBus.PublishAsync(executionEvent);
                        _logger.LogInformation($"📤 推仓状态更新事件已发送: {position.Symbol} 阶梯{stage.TierIndex}");
                    }
                    
                    // 触发工作日志，用于UI显示
                    AddWorkLog("SUCCESS", $"✅ 推仓阶梯{stage.TierIndex}完成: {position.Symbol} +{addQuantity:F6}");
                    
                    // 🔧 关键修复：触发ExecutionCompleted事件，确保合约配置窗口能接收到状态更新
                    OnExecutionCompleted(new ExecutionResultEventArgs
                    {
                        Symbol = position.Symbol,
                        ExecutionType = $"推仓阶梯{stage.TierIndex}",
                        IsSuccess = true,
                        Message = $"推仓阶梯{stage.TierIndex}执行成功，数量: {addQuantity:F6}",
                        PnlAtExecution = position.UnrealizedProfit
                    });
                    
                    _logger.LogInformation($"🎯 合约配置状态已通知更新: {position.Symbol} 阶梯{stage.TierIndex}执行完成");
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, $"⚠️ 推仓状态更新失败: {position.Symbol}");
                    // 不影响推仓成功的返回值
                }
                
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
                    // 🚨 修复：多头止损价 = 开仓价 - (保护盈利 / 持仓数量) [止损价在开仓价下方，确保保盈]
                    protectionPrice = entryPrice - (protectionAmount / quantity);
                    _logger.LogCritical($"💰 多头保盈计算: {entryPrice:F4} - ({protectionAmount:F2} / {quantity:F8}) = {protectionPrice:F4}");
                }
                else
                {
                    // 🚨 修复：空头止损价 = 开仓价 + (保护盈利 / 持仓数量) [止损价在开仓价上方，确保保盈]
                    protectionPrice = entryPrice + (protectionAmount / quantity);
                    _logger.LogCritical($"💰 空头保盈计算: {entryPrice:F4} + ({protectionAmount:F2} / {quantity:F8}) = {protectionPrice:F4}");
                }

                // 🚨 修复：验证止损价合理性（基于正确的计算公式）
                bool isValidStopPrice = isLong 
                    ? (protectionPrice < entryPrice)  // 多头：止损价应低于开仓价
                    : (protectionPrice > entryPrice); // 空头：止损价应高于开仓价

                if (!isValidStopPrice)
                {
                    var validationMessage = isLong 
                        ? $"不合理(多头止损价{protectionPrice:F4}应低于开仓价{entryPrice:F4})"
                        : $"不合理(空头止损价{protectionPrice:F4}应高于开仓价{entryPrice:F4})";
                    _logger.LogError($"🚨 保盈止损价格验证失败: {validationMessage}");
                    return false;
                }

                _logger.LogInformation($"🔍 止损价验证通过: {protectionPrice:F4}");

                // 🚨 修复：保盈止损也需要使用正确的PositionSide
                string profitProtectionPositionSide = isLong ? "LONG" : "SHORT";

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
                    PositionSide = profitProtectionPositionSide,  // 🚨 使用明确的LONG/SHORT
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
        /// 安全创建持仓档案（带锁保护）
        /// </summary>
        private async Task CreatePositionProfileSafeAsync(PositionInfo position)
        {
            try
            {
                var key = GetPositionKey(position.Symbol, position.PositionSideString);
                
                // 检查持久化存储中是否有该合约的状态
                var persistedProfiles = _persistenceService.LoadPositionProfiles();
                
                bool createSuccess = false;
                lock (_lockObject)
                {
                    // 双重检查，避免重复创建
                    if (_positionProfiles.ContainsKey(key))
                    {
                        return; // 已存在，无需创建
                    }
                    
                    if (persistedProfiles.ContainsKey(key) && persistedProfiles[key].TriggerRecords.Any())
                    {
                        // 恢复持久化的档案
                        var persistedProfile = persistedProfiles[key];
                        persistedProfile.InitialQuantity = Math.Abs(position.PositionAmt);
                        persistedProfile.InitialEntryPrice = position.EntryPrice;
                        persistedProfile.LastUpdateTime = DateTime.Now;
                        persistedProfile.IsActive = true;
                        
                        _positionProfiles[key] = persistedProfile;
                        _logger.LogInformation($"🔄 恢复档案: {key} - 触发记录: {persistedProfile.TriggerRecords.Count}");
                        createSuccess = true;
                    }
                    else
                    {
                        // 创建新档案
                        var newProfile = new PositionProfile
                        {
                            Symbol = position.Symbol,
                            PositionSide = position.PositionSideString,
                            InitialQuantity = Math.Abs(position.PositionAmt),
                            InitialEntryPrice = position.EntryPrice,
                            CreateTime = DateTime.Now,
                            LastUpdateTime = DateTime.Now,
                            IsActive = true
                        };
                        
                        _positionProfiles[key] = newProfile;
                        _logger.LogInformation($"📝 创建新档案: {key}, 数量: {position.PositionAmt:F6}, 入场价: {position.EntryPrice:F4}");
                        createSuccess = true;
                        
                        // 清理该合约的历史状态，避免重复执行
                        CleanupHistoryForNewPosition(position.Symbol, position.PositionSideString);
                    }
                }
                
                if (createSuccess)
                {
                    AddWorkLog("INFO", $"✅ 后台档案创建成功: {key}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建档案失败: {position.Symbol}_{position.PositionSideString}");
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
        /// 获取真实持仓方向（避免显示BOTH）
        /// </summary>
        private static string GetRealPositionSide(PositionInfo position)
        {
            // 根据持仓数量判断真实方向，避免显示"BOTH"
            return position.PositionAmt > 0 ? "LONG" : "SHORT";
        }

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
        public List<ActiveCooldownInfo> GetActiveCooldowns() 
        {
            var cooldowns = _cooldownManager.GetActiveCooldowns();
            return cooldowns.Select(kvp => new ActiveCooldownInfo
            {
                OperationKey = kvp.Key,
                LastExecutionTime = kvp.Value,
                RemainingTime = TimeSpan.Zero,
                TotalCooldownPeriod = TimeSpan.FromMinutes(5)
            }).ToList();
        }

        /// <summary>
        /// 设置事件订阅
        /// </summary>
        private void SetupEventSubscriptions()
        {
            // 订阅执行状态变更事件
            _eventBus.Subscribe(_loggingHandler);
            _eventBus.Subscribe(_statisticsHandler);
            
            // 订阅监控状态变更事件
            _eventBus.Subscribe(_loggingHandler);
            
            // 订阅持仓变化事件
            _eventBus.Subscribe(_loggingHandler);
            
            // 订阅错误事件
            _eventBus.Subscribe(_loggingHandler);
            _eventBus.Subscribe(_statisticsHandler);
            
            // 订阅止损单事件
            _eventBus.Subscribe(_loggingHandler);
            _eventBus.Subscribe(_statisticsHandler);
            
            // 订阅冷却期事件
            _eventBus.Subscribe(_loggingHandler);
            
            // 订阅性能事件
            _eventBus.Subscribe(_loggingHandler);
            _eventBus.Subscribe(_statisticsHandler);
            
            // 订阅数据同步事件
            _eventBus.Subscribe(_loggingHandler);
            
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
                    // _contractStateManager.CleanupContractState(symbol, positionSide);
                }
                else
                {
                    // 清理该合约的所有方向（LONG和SHORT）
                    // _contractStateManager.CleanupContractState(symbol, "LONG");
                    // _contractStateManager.CleanupContractState(symbol, "SHORT");
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
        
        protected virtual void OnPositionChanged(PositionChangedEventArgs e) => PositionChanged?.Invoke(this, e);
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
                    Timestamp = DateTime.Now
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
            // 🔧 高风险修复：避免在Dispose中调用异步方法导致死锁
            bool wasRunning = false;
            Timer? timerToDispose = null;
            
            try
            {
                // 🔧 步骤1：快速获取状态并标记为停止
                lock (_lockObject)
                {
                    wasRunning = _isRunning;
                    _isRunning = false;
                    timerToDispose = _scanTimer;
                    _scanTimer = null;
                }
                
                _logger.LogInformation("🛑 AutoMonitorService 开始释放资源...");
                
                // 🔧 步骤2：同步停止Timer（最重要，避免回调继续执行）
                if (timerToDispose != null)
                {
                    timerToDispose.Dispose();
                    _logger.LogInformation("⏰ 扫描定时器已停止");
                }
                
                // 🔧 步骤3：快速停止事件总线（避免异步操作）
                if (_eventBus is IDisposable disposableEventBus)
                {
                    try
                    {
                        // 🔧 关键：使用同步方式快速停止，避免异步等待
                        if (wasRunning && _eventBus != null)
                        {
                            // 尝试发送停止事件，但不等待完成
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _eventBus.PublishAsync(new MonitorStatusChangedEvent
                                    {
                                        Source = "AutoMonitorService",
                                        IsRunning = false,
                                        Message = "服务正在关闭",
                                        Config = _config,
                                        ActiveContractCount = 0
                                    });
                                }
                                catch (Exception eventEx)
                                {
                                    _logger.LogWarning(eventEx, "发送停止事件失败，继续关闭流程");
                                }
                            });
                        }
                        
                        disposableEventBus.Dispose();
                        _logger.LogInformation("🚌 事件总线已停止");
                    }
                    catch (Exception eventBusEx)
                    {
                        _logger.LogWarning(eventBusEx, "停止事件总线时发生错误，继续关闭流程");
                    }
                }
                
                // 🔧 步骤4：释放同步资源
                _executionSemaphore?.Dispose();
                _logger.LogInformation("🔐 执行信号量已释放");
                
                _positionDataLock?.Dispose();
                _logger.LogInformation("🔐 持仓数据读写锁已释放");
                
                _cancellationTokenSource?.Dispose();
                _logger.LogInformation("🛑 取消令牌源已释放");
                
                // 🔧 步骤5：释放管理器资源
                try
                {
                    _stopOrderManager?.Dispose();
                    _logger.LogInformation("🛡️ 止损单管理器已释放");
                }
                catch (Exception stopManagerEx)
                {
                    _logger.LogWarning(stopManagerEx, "释放止损单管理器失败");
                }
                
                try
                {
                    _cooldownManager?.Dispose();
                    _logger.LogInformation("⏱️ 冷却期管理器已释放");
                }
                catch (Exception cooldownEx)
                {
                    _logger.LogWarning(cooldownEx, "释放冷却期管理器失败");
                }
                
                try
                {
                    _unifiedStateManager?.Dispose();
                    _logger.LogInformation("🔄 统一状态管理器已释放");
                }
                catch (Exception stateManagerEx)
                {
                    _logger.LogWarning(stateManagerEx, "释放统一状态管理器失败");
                }
                
                // 🔧 步骤6：保存最终状态（同步方式）
                if (wasRunning)
                {
                    try
                    {
                        _persistenceService?.SavePositionProfiles(_positionProfiles);
                        _persistenceService?.SaveExecutionHistory(_executionHistory);
                        _logger.LogInformation("💾 最终状态已保存");
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogWarning(saveEx, "保存最终状态失败");
                    }
                }
                
                _logger.LogInformation("✅ AutoMonitorService 资源释放完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 释放AutoMonitorService资源时发生错误");
            }
            finally
            {
                // 🔧 确保关键事件能够触发（即使前面有异常）
                try
                {
                    OnMonitorStatusChanged(new MonitorStatusChangedEventArgs 
                    { 
                        IsRunning = false, 
                        Message = "自动监控服务已停止" 
                    });
                }
                catch (Exception eventEx)
                {
                    _logger.LogWarning(eventEx, "触发最终状态事件失败");
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
                // 🔧 高风险修复：使用读锁提高并发性能
                _positionDataLock.EnterReadLock();
                try
                {
                    return _positionProfiles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
                finally
                {
                    _positionDataLock.ExitReadLock();
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
                // 🔧 高风险修复：使用读锁提高并发性能
                _positionDataLock.EnterReadLock();
                try
                {
                    return _positionProfiles.Values.Count(p => p.IsActive);
                }
                finally
                {
                    _positionDataLock.ExitReadLock();
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
                // 🔧 高风险修复：使用读锁提高并发性能
                _positionDataLock.EnterReadLock();
                try
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
                finally
                {
                    _positionDataLock.ExitReadLock();
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

        // 🔧 新增：获取档案状态的公共方法
        public int GetActiveProfileCount()
        {
            lock (_lockObject)
            {
                return _positionProfiles.Count(p => p.Value.IsActive);
            }
        }

        public List<string> GetActiveProfileKeys()
        {
            lock (_lockObject)
            {
                return _positionProfiles.Where(p => p.Value.IsActive)
                                      .Select(p => p.Key)
                                      .ToList();
            }
        }

        /// <summary>
        /// 通知状态更新
        /// </summary>
        private void NotifyStatusUpdated(string symbol, string positionSide, MonitorExecutionSummary summary)
        {
            try
            {
                var key = GetPositionKey(symbol, positionSide);
                
                // 构建状态更新事件参数
                var statusUpdate = new StatusUpdateEventArgs
                {
                    Symbol = symbol,
                    PositionSide = positionSide,
                    ContractKey = key,
                    BreakEvenExecuted = summary.BreakEvenResult?.IsSuccess == true,
                    AddPositionResults = new Dictionary<int, bool>(),
                    ProfitProtectionResults = new Dictionary<int, bool>()
                };
                
                // 添加推仓结果
                if (summary.AddPositionResults?.Any() == true)
                {
                    for (int i = 0; i < summary.AddPositionResults.Count; i++)
                    {
                        statusUpdate.AddPositionResults[i + 1] = summary.AddPositionResults[i].IsSuccess;
                    }
                }
                
                // 添加保盈结果
                if (summary.ProfitProtectionResults?.Any() == true)
                {
                    for (int i = 0; i < summary.ProfitProtectionResults.Count; i++)
                    {
                        statusUpdate.ProfitProtectionResults[i + 1] = summary.ProfitProtectionResults[i].IsSuccess;
                    }
                }
                
                // 触发状态更新事件
                StatusUpdated?.Invoke(this, statusUpdate);
                
                AddWorkLog("INFO", $"🔄【状态更新】{key}: 已通知UI更新状态显示");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"通知状态更新失败: {symbol}_{positionSide}");
            }
        }

        /// <summary>
        /// 🎯 生成统一监控状态文件（新的双文件系统）
        /// </summary>
        private async Task GenerateUnifiedMonitoringStatesAsync(List<PositionInfo> activePositions)
        {
            try
            {
                // 检查是否启用新的双文件系统
                if (_stateService == null || _configManager == null)
                {
                    return; // 使用传统系统
                }

                // 获取当前选择的配置名称
                string defaultConfigName = "智能默认配置";
                try
                {
                    // 尝试从当前配置获取名称
                    if (_config?.Name != null)
                    {
                        defaultConfigName = _config.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ 无法获取当前配置名称，使用默认配置");
                }

                // 从持仓数据生成统一监控状态
                var states = _stateService.GenerateMonitoringStatesFromPositions(activePositions, defaultConfigName);
                
                // 保存统一监控状态文件
                _stateService.SaveMonitoringStates(states);
                
                _logger?.LogDebug($"✅ 已生成统一监控状态: {states.Count(s => s.Value.IsActive)} 个活跃状态");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 生成统一监控状态失败");
            }
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// 检查当前是否为模拟环境
        /// </summary>
        /// <returns>是否为模拟环境</returns>
        private bool IsSimulationEnvironment()
        {
            try
            {
                // 🔧 通过检查BinanceService的API配置来判断是否为模拟环境
                if (_binanceService == null) return true;
                
                // 检查是否有有效的API配置
                var currentAccount = _binanceService.GetType().GetProperty("CurrentAccount")?.GetValue(_binanceService);
                if (currentAccount == null) return true;
                
                var apiKey = currentAccount.GetType().GetProperty("ApiKey")?.GetValue(currentAccount) as string;
                var secretKey = currentAccount.GetType().GetProperty("SecretKey")?.GetValue(currentAccount) as string;
                
                // 如果API Key或Secret Key为空，或者长度不足，认为是模拟环境
                bool isSimulation = string.IsNullOrEmpty(apiKey) || 
                                   string.IsNullOrEmpty(secretKey) ||
                                   apiKey.Length < 10 || 
                                   secretKey.Length < 10;
                
                return isSimulation;
            }
            catch (Exception)
            {
                // 出现异常时，为了安全起见，默认认为是模拟环境
                return true;
            }
        }

        /// <summary>
        /// �� 【新增】直接从状态文件检查执行状态，确保界面与文件同步
        /// </summary>
        public bool IsExecutedInStateFile(string symbol, string positionSide, string executionType, int? tierIndex = null)
        {
            try
            {
                var contractKey = $"{symbol}_{positionSide}";
                
                // 1. 优先从统一状态管理器检查
                if (_unifiedStateManager != null)
                {
                    switch (executionType.ToLower())
                    {
                        case "breakeven":
                        case "保本":
                            return _unifiedStateManager.IsExecuted(symbol, positionSide, ExecutionType.BreakEven);
                        case "addposition":
                        case "推仓":
                            return tierIndex.HasValue && _unifiedStateManager.IsExecuted(symbol, positionSide, ExecutionType.AddPosition, tierIndex.Value);
                        case "profitprotection":
                        case "保盈":
                            return tierIndex.HasValue && _unifiedStateManager.IsExecuted(symbol, positionSide, ExecutionType.ProfitProtection, tierIndex.Value);
                    }
                }
                
                // 2. 回退：直接从状态服务检查
                if (_stateService != null)
                {
                    return _stateService.IsExecuted(contractKey, executionType, tierIndex);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"检查状态文件执行状态失败: {symbol}_{positionSide} {executionType}");
                return false;
            }
        }
    }
    
    /// <summary>
    /// 持仓变化事件参数
    /// </summary>
    public class PositionChangedEventArgs : EventArgs
    {
        public string Symbol { get; set; } = "";
        public string PositionSide { get; set; } = "";
        public PositionChangeType ChangeType { get; set; }
        public decimal CurrentQuantity { get; set; }
        public decimal CurrentPnl { get; set; }
        public DateTime Timestamp { get; set; }
    }
    

    
    /// <summary>
    /// 状态更新事件参数
    /// </summary>
    public class StatusUpdateEventArgs : EventArgs
    {
        public string Symbol { get; set; } = "";
        public string PositionSide { get; set; } = "";
        public string ContractKey { get; set; } = "";
        public bool BreakEvenExecuted { get; set; }
        public Dictionary<int, bool> AddPositionResults { get; set; } = new();
        public Dictionary<int, bool> ProfitProtectionResults { get; set; } = new();
    }
} 