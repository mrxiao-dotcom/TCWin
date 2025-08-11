using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 🎯 简化自动监控服务 - 基于新规范的统一监控管理
    /// 专注于核心监控逻辑，避免复杂的状态管理
    /// </summary>
    public class SimplifiedAutoMonitorService : IDisposable
    {
        private readonly ILogger<SimplifiedAutoMonitorService> _logger;
        private readonly SimplifiedExecutionEngine _executionEngine;
        private readonly SimplifiedStateService _stateService;
        private readonly BinanceService _binanceService;
        private readonly GlobalModeManager _globalModeManager;
        
        // 监控状态
        private bool _isRunning = false;
        private Timer? _monitorTimer;
        private readonly object _monitorLock = new object();
        private CancellationTokenSource? _cancellationTokenSource;
        
        // 监控配置
        private int _scanIntervalSeconds = 5;
        private readonly List<string> _enabledContracts = new List<string>();
        
        // 事件
        public event EventHandler<SimplifiedMonitorStatusChangedEventArgs>? MonitorStatusChanged;
        public event EventHandler<SimplifiedExecutionResult>? ExecutionCompleted;
        public event EventHandler<string>? LogRequested;

        public SimplifiedAutoMonitorService(
            ILogger<SimplifiedAutoMonitorService> logger,
            SimplifiedExecutionEngine executionEngine,
            SimplifiedStateService stateService,
            BinanceService binanceService,
            GlobalModeManager globalModeManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
            _binanceService = binanceService ?? throw new ArgumentNullException(nameof(binanceService));
            _globalModeManager = globalModeManager ?? throw new ArgumentNullException(nameof(globalModeManager));
            
            // 订阅执行引擎的执行完成事件
            _executionEngine.ExecutionCompleted += OnExecutionEngineCompleted;
        }

        #region 监控控制

        /// <summary>
        /// 启动自动监控
        /// </summary>
        public async Task<bool> StartMonitoringAsync()
        {
            // 先执行async操作
            try
            {
                await LoadEnabledContractsAsync();
                
                if (!_enabledContracts.Any())
                {
                    AddLog("⚠️ 没有找到启用的合约，无法启动监控");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 加载合约列表失败");
                AddLog($"❌ 加载合约列表失败: {ex.Message}");
                return false;
            }

            // 然后执行同步的lock操作
            lock (_monitorLock)
            {
                if (_isRunning)
                {
                    AddLog("⚠️ 监控已在运行中");
                    return false;
                }

                try
                {
                    _logger.LogInformation("🚀 启动简化自动监控服务");
                    
                    // 初始化取消令牌
                    _cancellationTokenSource = new CancellationTokenSource();
                    
                    // 创建并启动监控定时器
                    _monitorTimer = new Timer(_scanIntervalSeconds * 1000);
                    _monitorTimer.Elapsed += OnTimerElapsed;
                    _monitorTimer.AutoReset = true;
                    _monitorTimer.Start();
                    
                    _isRunning = true;
                    AddLog($"✅ 监控已启动，扫描间隔: {_scanIntervalSeconds}秒，监控合约: {_enabledContracts.Count}个");
                    
                    // 触发状态变更事件
                    MonitorStatusChanged?.Invoke(this, new SimplifiedMonitorStatusChangedEventArgs(true, "监控启动成功"));
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 启动监控失败");
                    AddLog($"❌ 启动监控失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 停止自动监控
        /// </summary>
        public void StopMonitoring()
        {
            lock (_monitorLock)
            {
                if (!_isRunning)
                {
                    AddLog("⚠️ 监控未在运行");
                    return;
                }

                try
                {
                    _logger.LogInformation("🛑 停止简化自动监控服务");
                    
                    // 停止定时器
                    _monitorTimer?.Stop();
                    _monitorTimer?.Dispose();
                    _monitorTimer = null;
                    
                    // 取消正在进行的操作
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = null;
                    
                    _isRunning = false;
                    AddLog("✅ 监控已停止");
                    
                    // 触发状态变更事件
                    MonitorStatusChanged?.Invoke(this, new SimplifiedMonitorStatusChangedEventArgs(false, "监控停止成功"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 停止监控失败");
                    AddLog($"❌ 停止监控失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 检查监控状态
        /// </summary>
        public bool IsRunning => _isRunning;

        #endregion

        #region 核心监控逻辑

        /// <summary>
        /// 定时器触发的监控扫描
        /// </summary>
        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
                return;

            try
            {
                await ExecuteScanCycleAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 监控扫描周期执行失败");
                AddLog($"❌ 监控扫描失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行一次完整的扫描周期
        /// </summary>
        private async Task ExecuteScanCycleAsync()
        {
            var scanStartTime = DateTime.Now;
            AddLog($"🔍 开始扫描周期 [{scanStartTime:HH:mm:ss}]");

            try
            {
                // 获取当前所有持仓
                var positions = await _binanceService.GetPositionsAsync();
                if (positions == null || !positions.Any())
                {
                    AddLog("📊 当前无持仓，跳过本次扫描");
                    return;
                }

                var activePositions = positions.Where(p => Math.Abs(p.PositionAmt) > 0).ToList();
                AddLog($"📊 发现活跃持仓: {activePositions.Count} 个");

                // 只监控启用的合约
                var enabledPositions = activePositions.Where(p => 
                    _enabledContracts.Contains($"{p.Symbol}_{p.PositionSideString}")).ToList();

                if (!enabledPositions.Any())
                {
                    AddLog("📊 无启用的合约持仓，跳过本次扫描");
                    return;
                }

                AddLog($"🎯 监控合约持仓: {enabledPositions.Count} 个");

                // 并行处理每个合约
                var tasks = enabledPositions.Select(position => ProcessContractAsync(position));
                await Task.WhenAll(tasks);

                var scanDuration = (DateTime.Now - scanStartTime).TotalMilliseconds;
                AddLog($"✅ 扫描周期完成，耗时: {scanDuration:F0}ms");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 扫描周期执行异常");
                AddLog($"❌ 扫描异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理单个合约的监控逻辑
        /// </summary>
        private async Task ProcessContractAsync(PositionInfo position)
        {
            var contractKey = $"{position.Symbol}_{position.PositionSideString}";
            
            try
            {
                _logger.LogDebug($"🔍 处理合约: {contractKey}, 浮盈: {position.UnrealizedProfit:F2}");

                // 执行监控逻辑
                var results = await _executionEngine.ExecuteContractMonitoringAsync(
                    position.Symbol, 
                    position.PositionSideString, 
                    position.UnrealizedProfit);

                // 记录执行结果
                if (results.Any())
                {
                    foreach (var result in results)
                    {
                        var statusIcon = result.IsSuccess ? "✅" : "❌";
                        AddLog($"{statusIcon} {contractKey} {result.DisplayName}: {result.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 处理合约失败: {contractKey}");
                AddLog($"❌ {contractKey} 处理失败: {ex.Message}");
            }
        }

        #endregion

        #region 配置管理

        /// <summary>
        /// 加载启用的合约列表
        /// </summary>
        private async Task LoadEnabledContractsAsync()
        {
            try
            {
                _enabledContracts.Clear();
                
                var contractStates = await _stateService.GetContractStatesAsync();
                
                foreach (var state in contractStates.Values)
                {
                    var contractKey = $"{state.Symbol}_{state.PositionSide}";
                    _enabledContracts.Add(contractKey);
                }
                
                _logger.LogInformation($"📋 加载启用合约: {_enabledContracts.Count} 个");
                AddLog($"📋 加载启用合约: {_enabledContracts.Count} 个");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 加载启用合约失败");
                AddLog($"❌ 加载启用合约失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置扫描间隔
        /// </summary>
        public void SetScanInterval(int intervalSeconds)
        {
            if (intervalSeconds < 1 || intervalSeconds > 300)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "扫描间隔必须在1-300秒之间");
            }

            _scanIntervalSeconds = intervalSeconds;
            
            // 如果监控正在运行，重新配置定时器
            if (_isRunning && _monitorTimer != null)
            {
                _monitorTimer.Interval = _scanIntervalSeconds * 1000;
                AddLog($"⚙️ 扫描间隔已更新为: {_scanIntervalSeconds}秒");
            }
        }

        /// <summary>
        /// 刷新启用的合约列表
        /// </summary>
        public async Task RefreshEnabledContractsAsync()
        {
            await LoadEnabledContractsAsync();
        }

        #endregion

        #region 手动操作

        /// <summary>
        /// 手动执行一次扫描
        /// </summary>
        public async Task<bool> ExecuteManualScanAsync()
        {
            try
            {
                AddLog("🔍 开始手动扫描");
                await ExecuteScanCycleAsync();
                AddLog("✅ 手动扫描完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 手动扫描失败");
                AddLog($"❌ 手动扫描失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 手动执行特定合约的监控
        /// </summary>
        public async Task<List<SimplifiedExecutionResult>> ExecuteManualContractMonitoringAsync(string symbol, string positionSide)
        {
            try
            {
                AddLog($"🎯 手动执行合约监控: {symbol}_{positionSide}");
                
                // 获取当前持仓信息
                var positions = await _binanceService.GetPositionsAsync();
                var position = positions?.FirstOrDefault(p => 
                    p.Symbol == symbol && 
                    p.PositionSideString.Equals(positionSide, StringComparison.OrdinalIgnoreCase));

                if (position == null)
                {
                    AddLog($"⚠️ 未找到持仓: {symbol}_{positionSide}");
                    return new List<SimplifiedExecutionResult>();
                }

                var results = await _executionEngine.ExecuteContractMonitoringAsync(symbol, positionSide, position.UnrealizedProfit);
                
                AddLog($"✅ 手动监控完成: {symbol}_{positionSide}, 执行操作: {results.Count} 个");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 手动合约监控失败: {symbol}_{positionSide}");
                AddLog($"❌ 手动监控失败: {ex.Message}");
                return new List<SimplifiedExecutionResult>();
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 处理执行引擎的执行完成事件
        /// </summary>
        private void OnExecutionEngineCompleted(object? sender, SimplifiedExecutionResult e)
        {
            // 转发执行完成事件
            ExecutionCompleted?.Invoke(this, e);
            
            // 记录执行日志
            var statusIcon = e.IsSuccess ? "🎉" : "❌";
            var modeText = _globalModeManager.IsSimulationMode ? "[模拟]" : "[实盘]";
            AddLog($"{statusIcon} {modeText} {e.ContractKey} {e.DisplayName} {(e.IsSuccess ? "成功" : "失败")}: {e.Message}");
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";
            
            _logger.LogInformation(logMessage);
            LogRequested?.Invoke(this, logMessage);
        }

        #endregion

        #region 统计信息

        /// <summary>
        /// 获取监控统计信息
        /// </summary>
        public async Task<SimplifiedMonitorStats> GetMonitorStatsAsync()
        {
            try
            {
                var contractStates = await _stateService.GetContractStatesAsync();
                var stats = new SimplifiedMonitorStats
                {
                    TotalContracts = contractStates.Count,
                    EnabledContracts = _enabledContracts.Count,
                    IsRunning = _isRunning,
                    ScanIntervalSeconds = _scanIntervalSeconds
                };

                // 统计执行状态
                foreach (var state in contractStates.Values)
                {
                    // 保本统计
                    var breakEvenState = ExecutionStateExtensions.FromInt(state.BreakEvenConfig.ExecutionState);
                    if (breakEvenState == StandardExecutionState.Executed) stats.BreakEvenExecuted++;

                    // 推仓统计
                    foreach (var tier in state.AddPositionConfig.Tiers)
                    {
                        var tierState = ExecutionStateExtensions.FromInt(tier.ExecutionState);
                        if (tierState == StandardExecutionState.Executed) stats.AddPositionExecuted++;
                    }

                    // 保盈统计
                    foreach (var tier in state.ProfitProtectionConfig.Tiers)
                    {
                        var tierState = ExecutionStateExtensions.FromInt(tier.ExecutionState);
                        if (tierState == StandardExecutionState.Executed) stats.ProfitProtectionExecuted++;
                    }
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 获取监控统计失败");
                return new SimplifiedMonitorStats();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            StopMonitoring();
            
            if (_executionEngine != null)
            {
                _executionEngine.ExecutionCompleted -= OnExecutionEngineCompleted;
            }
            
            _monitorTimer?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// 监控统计信息
    /// </summary>
    public class SimplifiedMonitorStats
    {
        public int TotalContracts { get; set; }
        public int EnabledContracts { get; set; }
        public bool IsRunning { get; set; }
        public int ScanIntervalSeconds { get; set; }
        public int BreakEvenExecuted { get; set; }
        public int AddPositionExecuted { get; set; }
        public int ProfitProtectionExecuted { get; set; }
        
        public int TotalExecuted => BreakEvenExecuted + AddPositionExecuted + ProfitProtectionExecuted;
    }

    /// <summary>
    /// 简化监控状态变更事件参数
    /// </summary>
    public class SimplifiedMonitorStatusChangedEventArgs : EventArgs
    {
        public bool IsRunning { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public SimplifiedMonitorStatusChangedEventArgs(bool isRunning, string message)
        {
            IsRunning = isRunning;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }
} 