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
        
        // 🛡️ 扫描计数器，用于定期清理
        private int _scanCount = 0;
        
        private Timer? _scanTimer;
        private bool _isRunning;
        private AutoMonitorConfig? _config;
        private readonly object _lockObject = new();
        
        // 🔧 新增：执行操作锁，防止并发执行导致的集合访问冲突
        private readonly object _executionLock = new();
        
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
            
            // 🔌 订阅事件
            SetupEventSubscriptions();
        }



        /// <summary>
        /// 启动自动监控
        /// </summary>
        public async Task<bool> StartMonitoringAsync(AutoMonitorConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            lock (_lockObject)
            {
                if (_isRunning) throw new InvalidOperationException("自动监控已在运行中");
                _config = config;
                _isRunning = true;
            }

            try
            {
                _logger.LogInformation("🚀 启动自动监控服务...");
                
                // 🔍 新增：配置验证
                _logger.LogInformation("🔍 开始验证配置...");
                var validationResult = await _configValidationService.ValidateAsync(config, ValidationMode.Strict, true);
                
                if (!validationResult.IsValid)
                {
                    var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.Message));
                    var errorMsg = $"配置验证失败: {errorMessages}";
                    _logger.LogError(errorMsg);
                    
                    // 🔧 配置验证失败时，清空配置并重置状态
                    lock (_lockObject) 
                    { 
                        _config = null; 
                        _isRunning = false; 
                    }
                    
                    OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                        IsRunning = false, 
                        Message = errorMsg 
                    });
                    
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
                
                // 🚌 启动事件总线
                await _eventBus.StartAsync();
                
                await InitializePositionProfilesAsync();
                
                var intervalMs = _config.ScanIntervalSeconds * 1000;
                _scanTimer = new Timer(async _ => await ScanPositionsAsync(), null, 0, intervalMs);
                
                // 🚌 发布监控状态变更事件
                await _eventBus.PublishAsync(new MonitorStatusChangedEvent
                {
                    Source = "AutoMonitorService",
                    IsRunning = true,
                    Message = $"自动监控已启动 - 扫描间隔{_config.ScanIntervalSeconds}秒",
                    Config = _config,
                    ActiveContractCount = _positionProfiles.Count
                });
                
                OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                    IsRunning = true, 
                    Message = $"自动监控已启动 - 扫描间隔{_config.ScanIntervalSeconds}秒" 
                });
                
                // 🔧 新增：友好的启动成功消息
                if (_positionProfiles.Any())
                {
                    _logger.LogInformation($"✅ 自动监控启动成功 - 配置: {_config.Name}, 间隔: {_config.ScanIntervalSeconds}秒, 监控{_positionProfiles.Count}个持仓");
                }
                else
                {
                    _logger.LogInformation($"✅ 自动监控启动成功 - 配置: {_config.Name}, 间隔: {_config.ScanIntervalSeconds}秒");
                    _logger.LogInformation($"💤 当前无持仓，系统将等待新持仓并自动开始监控");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                lock (_lockObject) { _isRunning = false; }
                _logger.LogError(ex, "自动监控启动失败");
                OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                    IsRunning = false, 
                    Message = $"启动失败：{ex.Message}" 
                });
                return false;
            }
        }

        /// <summary>
        /// 停止自动监控
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            lock (_lockObject)
            {
                if (!_isRunning) return;
                _isRunning = false;
                _scanTimer?.Dispose();
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
            
            _logger.LogInformation("⏹️ 自动监控已停止");
            
            // 🚌 发布监控状态变更事件并停止事件总线
            try
            {
                await _eventBus.PublishAsync(new MonitorStatusChangedEvent
                {
                    Source = "AutoMonitorService",
                    IsRunning = false,
                    Message = "自动监控已停止",
                    Config = _config,
                    ActiveContractCount = 0
                });
                
                await _eventBus.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 停止事件总线时发生错误");
            }
            
            OnMonitorStatusChanged(new MonitorStatusChangedEventArgs { 
                IsRunning = false, 
                Message = "自动监控已停止" 
            });
        }

        /// <summary>
        /// 初始化持仓档案
        /// </summary>
        private async Task InitializePositionProfilesAsync()
        {
            var positions = await _binanceService.GetPositionsAsync();
            if (positions == null) 
            {
                _logger.LogWarning("⚠️ API返回的持仓数据为空，自动盯盘将等待持仓数据");
                return;
            }

            lock (_lockObject)
            {
                // 🔧 修复：先获取当前真实的活跃持仓
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
                    _positionProfiles.Clear();
                    _executionHistory.Clear();
                    return; // 正常返回，不报错
                }
                
                foreach (var pos in activePositions)
                {
                    _logger.LogInformation($"   📍 {pos.Symbol} {pos.PositionSideString}: {pos.PositionAmt:F6} (浮盈: {pos.UnrealizedProfit:F2}U)");
                }

                // 🔧 修复：先加载持久化的状态，但只恢复当前真实存在的持仓档案
                _positionProfiles.Clear();
                var persistedProfiles = _persistenceService.LoadPositionProfiles();
                
                _logger.LogInformation($"📖 从持久化存储加载了 {persistedProfiles.Count} 个历史档案");

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
                        
                        _positionProfiles[key] = persistedProfile;
                        _logger.LogInformation($"🔄 恢复持仓档案: {key} - 触发记录: {persistedProfile.TriggerRecords.Count}");
                    }
                    else
                    {
                        // 新建档案
                        _positionProfiles[key] = new PositionProfile
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
                var invalidProfiles = persistedProfiles.Keys.Except(_positionProfiles.Keys).ToList();
                if (invalidProfiles.Any())
                {
                    _logger.LogWarning($"🗑️ 发现{invalidProfiles.Count}个无效的历史档案（无对应活跃持仓）:");
                    foreach (var invalidKey in invalidProfiles)
                    {
                        var parts = invalidKey.Split('_');
                        if (parts.Length == 2)
                        {
                            var symbol = parts[0];
                            var positionSide = parts[1];
                            _logger.LogWarning($"   ❌ {invalidKey} - 该合约当前无活跃持仓，已跳过恢复");
                            
                            // 清理无效档案的执行历史
                            _persistenceService.CleanupContractHistory(symbol, positionSide, "无活跃持仓");
                        }
                    }
                }
                
                // 🔧 新增：加载执行历史，但只保留当前活跃持仓的记录
                var persistedHistory = _persistenceService.LoadExecutionHistory();
                var activeSymbols = activePositions.Select(p => p.Symbol).ToHashSet();
                var validHistory = persistedHistory.Where(h => activeSymbols.Contains(h.Symbol)).ToList();
                
                _executionHistory.Clear();
                _executionHistory.AddRange(validHistory);
                
                if (persistedHistory.Count != validHistory.Count)
                {
                    var removedCount = persistedHistory.Count - validHistory.Count;
                    _logger.LogInformation($"🗑️ 过滤执行历史: 移除{removedCount}条无效记录，保留{validHistory.Count}条有效记录");
                }
                
                _logger.LogInformation($"📊 初始化完成 - 持仓档案: {_positionProfiles.Count}个, 执行历史: {_executionHistory.Count}条");
                _logger.LogInformation($"✅ 所有档案均对应当前活跃持仓，无无效档案");
            }
        }

        /// <summary>
        /// 扫描持仓并执行相应策略
        /// </summary>
        private async Task ScanPositionsAsync()
        {
            if (!_isRunning || _config == null) return;

            // 🛡️ 增加扫描计数并定期清理过期的冷却期记录（每20次扫描清理一次）
            _scanCount++;
            if (_scanCount % 20 == 0)
            {
                _cooldownManager.CleanupExpiredRecords();
            }

            try
            {
                // 🔧 修复：添加执行锁，防止并发扫描导致的集合访问冲突
                if (!Monitor.TryEnter(_executionLock, TimeSpan.FromSeconds(1)))
                {
                    _logger.LogWarning("⚠️ 自动盯盘扫描繁忙，跳过本次扫描以避免并发冲突");
                    return;
                }

                try
                {
                    _logger.LogInformation("🔄 开始扫描持仓...");

                    // 🔧 修复：获取持仓数据（不能在lock中await）
                    var positions = await _binanceService.GetPositionsAsync();
                    if (positions == null || !positions.Any())
                    {
                        _logger.LogDebug("📊 当前无持仓数据，继续等待...");
                        return; // 🔧 修复：没有持仓时继续运行，不停止扫描
                    }

                    // 🔧 修复：过滤活跃持仓
                    var activePositions = positions.Where(p => 
                        Math.Abs(p.PositionAmt) > 0.001m &&
                        !string.IsNullOrEmpty(p.Symbol) &&
                        p.Symbol.EndsWith("USDT") &&
                        p.MarkPrice > 0 &&
                        p.EntryPrice > 0 &&
                        p.UnrealizedProfit != 0
                    ).ToList();

                    if (!activePositions.Any())
                    {
                        // 🔧 重要修复：没有活跃持仓时清理历史档案，但继续运行等待新持仓
                        lock (_lockObject)
                        {
                            if (_positionProfiles.Any())
                            {
                                _logger.LogInformation("🗑️ 检测到所有持仓已平仓，清理历史档案");
                                CleanupClosedPositions(new List<PositionInfo>());
                            }
                        }
                        
                        _logger.LogDebug("💤 当前无活跃持仓，继续等待新持仓...");
                        return; // 继续运行，等待持仓出现
                    }

                    // 🔧 修复：先清理已关闭的持仓，使用过滤后的活跃持仓列表
                    CleanupClosedPositions(activePositions);

                    var positionCount = activePositions.Count;
                    _logger.LogDebug($"🔄 开始处理 {positionCount} 个活跃持仓");

                    // 🔧 修复：并行处理所有活跃持仓，提高效率
                    var processingTasks = activePositions.Select(ProcessPositionAsync).ToArray();
                    await Task.WhenAll(processingTasks);

                    _logger.LogDebug($"✅ 扫描完成，已处理 {positionCount} 个持仓");
                }
                finally
                {
                    Monitor.Exit(_executionLock);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 扫描持仓时发生严重错误");
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
            
            // 确保持仓档案存在
            lock (_lockObject)
            {
                if (!_positionProfiles.ContainsKey(key))
                {
                    // 🔧 新增：检测到新持仓时，立即清理该合约的历史状态
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

            try
            {
                _logger.LogInformation($"🎯 触发自动保本: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {_config.BreakEvenConfig.TriggerProfitAmount:F2}U");
                
                var success = await ExecuteBreakEvenStopLossAsync(position);
                
                // 🔄 使用统一状态管理器记录执行状态
                _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString, 
                    ExecutionType.BreakEven, null, currentPnl, success, 
                    success ? "自动保本执行成功" : "自动保本执行失败");
                
                // 🛡️ 记录冷却期：无论成功失败都记录，防止频繁重试
                _cooldownManager.RecordExecution(operationKey);
                
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
                
                // 🔄 使用统一状态管理器记录失败状态
                _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString, 
                    ExecutionType.BreakEven, null, currentPnl, false, ex.Message);
                
                // 🛡️ 记录冷却期：异常情况下也要记录，防止频繁重试造成更多问题
                _cooldownManager.RecordExecution(operationKey);
                
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
            if (!_config!.AddPositionConfig.IsEnabled) return false;

            // 🔄 使用新的合约状态管理器，解决多合约冲突问题
            var enabledStages = _config.AddPositionConfig.Tiers.OrderBy(s => s.TriggerProfitAmount);
            
            foreach (var stage in enabledStages)
            {
                if (currentPnl <= stage.TriggerProfitAmount) continue;

                // 🔄 使用统一状态管理器检查该阶梯是否已执行
                if (_unifiedStateManager.IsExecuted(position.Symbol, position.PositionSideString, 
                    ExecutionType.AddPosition, stage.TierIndex))
                {
                    continue;
                }

                // 🛡️ 检查冷却期：防止短时间内重复执行推仓操作
                var operationKey = CooldownManager.GenerateOperationKey(position.Symbol, position.PositionSideString, 
                    CooldownOperationType.AddPosition, stage.TierIndex);
                if (!_cooldownManager.CanExecute(operationKey, CooldownOperationType.AddPosition))
                {
                    var remainingTime = _cooldownManager.GetRemainingCooldown(operationKey, CooldownOperationType.AddPosition);
                    _logger.LogDebug($"🔒 推仓阶梯{stage.TierIndex}冷却中: {position.Symbol}, 剩余: {remainingTime.TotalSeconds:F1}秒");
                    continue;
                }

                try
                {
                    _logger.LogInformation($"🚀 触发推仓阶梯{stage.TierIndex}: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {stage.TriggerProfitAmount:F2}U");
                    
                    var success = await ExecuteAddPositionAsync(position, stage);
                    
                    // 🔄 使用统一状态管理器记录执行状态
                    _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString, 
                        ExecutionType.AddPosition, stage.TierIndex, currentPnl, success, 
                        success ? $"推仓阶梯{stage.TierIndex}执行成功" : $"推仓阶梯{stage.TierIndex}执行失败");
                    
                    // 🛡️ 记录冷却期：无论成功失败都记录
                    _cooldownManager.RecordExecution(operationKey);
                    
                    // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                    _contractStateManager.MarkAsTriggered(position.Symbol, position.PositionSideString, 
                        ExecutionType.AddPosition, stage.TierIndex, currentPnl, success, 
                        success ? $"推仓阶梯{stage.TierIndex}执行成功" : $"推仓阶梯{stage.TierIndex}执行失败");
                    
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
                    
                    // 🔄 使用统一状态管理器记录失败状态
                    _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString, 
                        ExecutionType.AddPosition, stage.TierIndex, currentPnl, false, ex.Message);
                    
                    // 🛡️ 记录冷却期：异常情况下也要记录
                    _cooldownManager.RecordExecution(operationKey);
                    
                    // ⚠️ 向后兼容：同时记录到旧系统（后续版本将移除）
                    _contractStateManager.MarkAsTriggered(position.Symbol, position.PositionSideString, 
                        ExecutionType.AddPosition, stage.TierIndex, currentPnl, false, ex.Message);
                    
                    var triggerKey = $"{GetPositionKey(position.Symbol, position.PositionSideString)}_AddPosition_Stage{stage.TierIndex}";
                    RecordTriggerExecution(profile, position, triggerKey, $"推仓阶梯{stage.TierIndex}", currentPnl, false);
                    return true; // 表示执行了操作（即使失败）
                }
            }
            
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

                // 🛡️ 检查冷却期：防止短时间内重复执行保盈止损
                var operationKey = CooldownManager.GenerateOperationKey(position.Symbol, position.PositionSideString, 
                    CooldownOperationType.ProfitProtection, stage.TierIndex);
                if (!_cooldownManager.CanExecute(operationKey, CooldownOperationType.ProfitProtection))
                {
                    var remainingTime = _cooldownManager.GetRemainingCooldown(operationKey, CooldownOperationType.ProfitProtection);
                    _logger.LogDebug($"🔒 保盈止损阶梯{stage.TierIndex}冷却中: {position.Symbol}, 剩余: {remainingTime.TotalSeconds:F1}秒");
                    continue;
                }

                try
                {
                    _logger.LogInformation($"🛡️ 触发保盈止损阶梯{stage.TierIndex}: {position.Symbol}, 浮盈: {currentPnl:F2}U >= {stage.TriggerProfitAmount:F2}U");
                    
                    var success = await ExecuteProfitProtectionAsync(position, stage);
                    
                    // 🔄 使用统一状态管理器记录执行状态
                    _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString,
                        ExecutionType.ProfitProtection, stage.TierIndex, currentPnl, success,
                        success ? $"保盈止损阶梯{stage.TierIndex}执行成功" : $"保盈止损阶梯{stage.TierIndex}执行失败");
                    
                    // 🛡️ 记录冷却期：无论成功失败都记录
                    _cooldownManager.RecordExecution(operationKey);
                    
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
                    
                    // 🔄 使用统一状态管理器记录失败状态
                    _unifiedStateManager.RecordExecution(position.Symbol, position.PositionSideString,
                        ExecutionType.ProfitProtection, stage.TierIndex, currentPnl, false, ex.Message);
                    
                    // 🛡️ 记录冷却期：异常情况下也要记录
                    _cooldownManager.RecordExecution(operationKey);
                    
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

                // 🛡️ 使用止损单管理器安全创建保本止损订单
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

                var success = await _stopOrderManager.CreateStopOrderSafelyAsync(
                    position.Symbol, stopLossRequest, StopOrderType.BreakEven);
                
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

                // 🔧 集成现有的推仓逻辑（来自 MainViewModel.Trading.cs）
                // 1. 计算风险金
                var accountEquity = _mainViewModel.AccountInfo?.TotalEquity ?? 0;
                var riskTimes = _mainViewModel.SelectedAccount?.RiskCapitalTimes ?? 8;
                var singleRiskCapital = accountEquity / riskTimes;
                
                // 2. 计算加仓金额（风险金 × 风险倍数）
                var addPositionAmount = singleRiskCapital * stage.RiskMultiplier;
                
                // 3. 获取最新价格
                var latestPrice = await _binanceService.GetLatestPriceAsync(position.Symbol);
                if (latestPrice <= 0) return false;
                
                // 4. 计算加仓数量
                var addQuantity = addPositionAmount / latestPrice;
                
                _logger.LogInformation($"💰 推仓计算: 账户权益={accountEquity:F2}U, 风险次数={riskTimes}, 单笔风险金={singleRiskCapital:F2}U, 风险倍数={stage.RiskMultiplier:F1}, 推仓金额={addPositionAmount:F2}U, 合约单价={latestPrice:F4}, 推仓数量={addQuantity:F8}");

                // 5. 检查交易规则和精度
                try
                {
                    var (minQty, maxQty, stepSize, tickSize, maxLeverage) = await _binanceService.GetSymbolTradingRulesAsync(position.Symbol);
                    
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"获取交易规则失败，使用默认精度处理: {position.Symbol}");
                    addQuantity = Math.Round(addQuantity, 6);
                }

                // 6. 确定加仓方向（与当前持仓方向一致）
                var addPositionSide = position.PositionAmt > 0 ? "BUY" : "SELL";
                
                // 7. 执行加仓下单
                var addOrderRequest = new OrderRequest
                {
                    Symbol = position.Symbol,
                    Side = addPositionSide,
                    Type = "MARKET",
                    Quantity = addQuantity,
                    TimeInForce = "GTC",
                    PositionSide = position.PositionSideString
                };

                var addOrderSuccess = await _binanceService.PlaceOrderAsync(addOrderRequest);
                if (!addOrderSuccess)
                {
                    _logger.LogError($"❌ 推仓下单失败: {position.Symbol}");
                    return false;
                }

                _logger.LogInformation($"✅ 推仓下单成功: {position.Symbol} {addPositionSide} {addQuantity:F6} (金额: {addPositionAmount:F2}U) @ 市价");

                // 8. 等待订单执行
                await Task.Delay(2000);
                
                // 9. 获取更新后的持仓信息（模拟刷新）
                var updatedPositions = await _binanceService.GetPositionsAsync();
                var updatedPosition = updatedPositions?.FirstOrDefault(p => 
                    p.Symbol == position.Symbol && Math.Abs(p.PositionAmt) > 0);

                if (updatedPosition == null)
                {
                    _logger.LogWarning("⚠️ 推仓完成但无法获取更新后的持仓信息");
                    return false;
                }

                // 10. 设置保本止损
                var stopQuantity = Math.Abs(updatedPosition.PositionAmt);
                var entryPrice = updatedPosition.EntryPrice; // 这是加仓后的最新成本价
                
                // 使用很小的百分比缓冲（0.05%），确保真正保本而不会被轻易触发
                var bufferPercentage = 0.0005m; // 0.05%
                var newStopPrice = updatedPosition.PositionAmt > 0 
                    ? entryPrice * (1 + bufferPercentage)  // 多头：成本价 + 0.05%
                    : entryPrice * (1 - bufferPercentage); // 空头：成本价 - 0.05%
                newStopPrice = Math.Round(newStopPrice, 4);
                
                _logger.LogInformation($"💰 保本止损计算: 成本价={entryPrice:F4}, 缓冲={bufferPercentage * 100:F2}%, 止损价={newStopPrice:F4}");

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

                // 🛡️ 使用止损单管理器安全创建保本止损订单
                var stopSuccess = await _stopOrderManager.CreateStopOrderSafelyAsync(
                    position.Symbol, stopOrderRequest, StopOrderType.AddPosition);
                
                _logger.LogInformation($"🚀 推仓完成: {position.Symbol}, 推仓: {addQuantity:F6}@{latestPrice:F4}, 保本止损: @{newStopPrice:F4}");
                
                return stopSuccess;
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

                // 创建保盈止损订单
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

                var success = await _stopOrderManager.CreateStopOrderSafelyAsync(
                    position.Symbol, stopLossRequest, StopOrderType.ProfitProtection);
                
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
                var activeKeys = activePositions.Select(p => GetPositionKey(p.Symbol, p.PositionSideString)).ToHashSet();
                var keysToRemove = _positionProfiles.Keys.Where(k => !activeKeys.Contains(k)).ToList();
                
                // 🔧 增强：记录清理的档案信息并检测重新开仓情况
                var cleanupResults = new List<string>();
                
                foreach (var key in keysToRemove)
                {
                    var profile = _positionProfiles[key];
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
                    
                    _positionProfiles.Remove(key);
                    
                    // 🛡️ 清理该合约的所有冷却期记录
                    _cooldownManager.ClearContractCooldowns(profile.Symbol, profile.PositionSide);
                    
                    // 🔄 清理统一状态管理器中的该合约状态
                    _unifiedStateManager.ClearContractStates(profile.Symbol, profile.PositionSide, "平仓清理");
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
                            
                            cleanupResults.Add($"🔄 重新开仓清理: {newKey} (清理{historicalRecords.Count}条历史记录)");
                        }
                    }
                }
                
                // 🔧 增强：如果有清理动作，保存到持久化存储
                if (keysToRemove.Any() || newPositionKeys.Any())
                {
                    try
                    {
                        _persistenceService.SavePositionProfiles(_positionProfiles);
                        _persistenceService.SaveExecutionHistory(_executionHistory);
                        
                        if (cleanupResults.Any())
                        {
                            _logger.LogInformation($"💾 历史状态清理完成，已保存到持久化存储:");
                            foreach (var result in cleanupResults)
                            {
                                _logger.LogInformation($"   {result}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ 保存清理结果到持久化存储失败");
                    }
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
            _unifiedStateManager.ClearContractStates(symbol, positionSide, reason);
            _cooldownManager.ClearContractCooldowns(symbol, positionSide);
            
            // 清理旧的ContractStateManager状态
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
        protected virtual void OnMonitorStatusChanged(MonitorStatusChangedEventArgs e) => MonitorStatusChanged?.Invoke(this, e);
        protected virtual void OnExecutionCompleted(ExecutionResultEventArgs e) => ExecutionCompleted?.Invoke(this, e);

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
                lock (_executionLock)
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
                lock (_executionLock)
                {
                    return _executionHistory.Count;
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
} 