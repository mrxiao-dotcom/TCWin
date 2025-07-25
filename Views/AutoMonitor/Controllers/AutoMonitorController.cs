using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Extensions.Logging;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using BinanceFuturesTrader.Views.AutoMonitor.Services;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.ViewModels;
using BinanceFuturesTrader.Services;

namespace BinanceFuturesTrader.Views.AutoMonitor.Controllers
{
    public class AutoMonitorController
    {
        private readonly BinanceFuturesTrader.Services.AutoMonitorService _autoMonitorService;
        private readonly AutoMonitorPersistenceService _persistenceService;
        private readonly MainViewModel _mainViewModel;
        private readonly ILogger _logger;
        private readonly AutoMonitorDataModel _dataModel;
        private readonly UIStateModel _uiStateModel;
        private readonly TimerController _timerController;
        private readonly EventController _eventController;
        private readonly ConfigurationController _configurationController;
        private readonly LoggingService _loggingService;

        public AutoMonitorController(
            BinanceFuturesTrader.Services.AutoMonitorService autoMonitorService,
            ILogger logger,
            AutoMonitorDataModel dataModel,
            UIStateModel uiStateModel,
            MainViewModel mainViewModel = null)
        {
            _autoMonitorService = autoMonitorService ?? throw new ArgumentNullException(nameof(autoMonitorService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataModel = dataModel ?? throw new ArgumentNullException(nameof(dataModel));
            _uiStateModel = uiStateModel ?? throw new ArgumentNullException(nameof(uiStateModel));
            _mainViewModel = mainViewModel;

            var persistenceLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AutoMonitorPersistenceService>();
            _persistenceService = new AutoMonitorPersistenceService(persistenceLogger);
            _timerController = new TimerController(_dataModel, _uiStateModel, _logger);
            _eventController = new EventController(_dataModel, _uiStateModel, _logger);
            _configurationController = new ConfigurationController(_dataModel, _uiStateModel, _logger);
            _loggingService = new LoggingService(_dataModel, _logger);

            _logger.LogDebug("AutoMonitorController 初始化完成");
        }

        public AutoMonitorDataModel DataModel => _dataModel;
        public UIStateModel UIStateModel => _uiStateModel;
        public TimerController TimerController => _timerController;
        public EventController EventController => _eventController;
        public ConfigurationController ConfigurationController => _configurationController;
        public LoggingService LoggingService => _loggingService;

        public async Task<bool> StartMonitoringAsync()
        {
            try
            {
                await _loggingService.LogOperationAsync("🚀 启动盯盘");
                _uiStateModel.SetLoadingState();

                if (!await _configurationController.LoadConfigurationAsync())
                {
                    await _loggingService.LogOperationAsync("❌ 配置验证失败，无法启动监控");
                    _uiStateModel.SetErrorState();
                    return false;
                }

                var loadResult = await LoadContractConfigurationsFromPositionsAsync();
                if (!loadResult)
                {
                    await _loggingService.LogOperationAsync("❌ 加载合约配置失败，无法启动监控");
                    _uiStateModel.SetErrorState();
                    return false;
                }

                await _loggingService.LogOperationAsync($"✅ 加载完毕 - {_dataModel.ContractMonitors.Count} 个合约配置");
                await RefreshDataAsync();

                var autoMonitorConfig = _mainViewModel?.CurrentAutoMonitorConfig;
                if (autoMonitorConfig != null && _autoMonitorService != null)
                {
                    await _loggingService.LogOperationAsync($"📊 加载名称: {autoMonitorConfig.Name}");
                    var serviceStarted = await _autoMonitorService.StartMonitoringAsync(autoMonitorConfig);
                    if (!serviceStarted)
                    {
                        await _loggingService.LogOperationAsync("❌ 启动底层监控服务失败");
                        _uiStateModel.SetErrorState();
                        return false;
                    }
                }

                var startTime = DateTime.Now;
                var nextScanTime = startTime.AddSeconds(_dataModel.ScanIntervalSeconds);
                _dataModel.StartTime = startTime;
                _dataModel.MonitorStatus = "运行中";
                _dataModel.NextScanDateTime = nextScanTime;
                _dataModel.ScanCountdownDisplay = $"下次扫描: {nextScanTime:HH:mm:ss}";
                _uiStateModel.SetMonitoringState();

                _timerController.StartAllTimers();
                StartMonitoringLoop();

                await _loggingService.LogOperationAsync("✅ 启动盯盘成功");
                _logger.LogInformation("✅ 自动盯盘监控启动成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动监控失败");
                await _loggingService.LogErrorAsync("启动监控失败", ex);
                _uiStateModel.SetErrorState();
                return false;
            }
        }

        public async Task<bool> StopMonitoringAsync()
        {
            try
            {
                _logger.LogInformation("开始停止自动盯盘监控");
                _uiStateModel.SetLoadingState();
                StopMonitoringLoop();
                _timerController.StopAllTimers();
                if (_autoMonitorService != null)
                {
                    await _autoMonitorService.StopMonitoringAsync();
                }
                _dataModel.MonitorStatus = "已停止";
                _uiStateModel.SetStoppedState();
                await _loggingService.LogOperationAsync("监控停止成功");
                _logger.LogInformation("自动盯盘监控停止成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止监控时发生异常");
                await _loggingService.LogErrorAsync("停止监控失败", ex);
                _uiStateModel.SetErrorState();
                return false;
            }
        }

        public async Task<bool> ToggleMonitoringAsync()
        {
            if (_dataModel.MonitorStatus == "运行中")
            {
                return await StopMonitoringAsync();
            }
            else
            {
                return await StartMonitoringAsync();
            }
        }

        private readonly object _monitoringLock = new object();
        private bool _isMonitoringActive = false;
        private Timer _monitoringTimer;

        private void StartMonitoringLoop()
        {
            lock (_monitoringLock)
            {
                if (_isMonitoringActive) return;
                _isMonitoringActive = true;
                _logger.LogInformation("🔄 启动监控循环");
                var intervalMs = _dataModel.ScanIntervalSeconds * 1000;
                _monitoringTimer = new Timer(async _ => await ExecuteMonitoringCycleAsync(), null, 0, intervalMs);
            }
        }

        private void StopMonitoringLoop()
        {
            lock (_monitoringLock)
            {
                if (!_isMonitoringActive) return;
                _isMonitoringActive = false;
                _monitoringTimer?.Dispose();
                _monitoringTimer = null;
                _logger.LogInformation("⏹ 监控循环已停止");
            }
        }

        private async Task ExecuteMonitoringCycleAsync()
        {
            if (!_isMonitoringActive) return;
            
            try
            {
                _logger.LogDebug("🔍 开始执行监控循环");
                _dataModel.ScanCount++;
                
                // 🔧 分步骤执行，确保部分失败不影响其他步骤
                
                // 步骤1：同步状态（最重要的功能）
                try
                {
                    await SyncStatusFromAutoMonitorServiceAsync();
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx, "⚠️ 状态同步失败，但监控继续运行");
                    await _loggingService.LogOperationAsync("⚠️ 状态同步失败，但监控继续运行");
                }
                
                // 步骤2：获取持仓数据
                List<BinanceFuturesTrader.Models.PositionInfo> currentPositions = null;
                try
                {
                    currentPositions = GetCurrentPositions();
                    if (currentPositions == null || !currentPositions.Any())
                    {
                        _logger.LogDebug("📊 当前没有持仓，跳过持仓数据更新");
                    }
                }
                catch (Exception posEx)
                {
                    _logger.LogWarning(posEx, "⚠️ 获取持仓数据失败");
                }
                
                // 步骤3：更新持仓数据（如果有数据的话）
                if (currentPositions != null && currentPositions.Any())
                {
                    try
                    {
                        await UpdatePositionDataAsync(currentPositions);
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogWarning(updateEx, "⚠️ 更新持仓数据失败");
                    }
                }
                
                // 步骤4：更新统计信息（在UI线程中安全执行）
                try
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _dataModel.LastScanTime = DateTime.Now;
                        _dataModel.NextScanDateTime = DateTime.Now.AddSeconds(_dataModel.ScanIntervalSeconds);
                        _dataModel.ScanCountdownDisplay = $"下次扫描: {_dataModel.NextScanDateTime:HH:mm:ss}";
                    });
                }
                catch (Exception statsEx)
                {
                    _logger.LogWarning(statsEx, "⚠️ 更新统计信息失败");
                }
                
                _logger.LogDebug("✅ 监控循环完成");
            }
            catch (Exception ex)
            {
                // 🔧 改进：即使出现未预期的异常，也要确保监控继续运行
                _dataModel.ErrorCount++;
                _logger.LogError(ex, "❌ 执行监控循环时发生严重异常，但监控将继续运行");
                
                try
                {
                    await _loggingService.LogErrorAsync("监控循环发生异常但继续运行", ex);
                }
                catch
                {
                    // 如果连日志记录都失败了，也不要让整个监控停止
                    _logger.LogError("连日志记录都失败了，但监控继续运行");
                }
                
                // 🔧 关键：不要重新抛出异常，让监控继续运行
                // 在发生异常后，等待一个较短的时间再继续，避免连续失败
                try
                {
                    await Task.Delay(1000); // 等待1秒
                }
                catch
                {
                    // 即使延迟都失败了，也继续
                }
            }
        }

        /// <summary>
        /// 从AutoMonitorService同步状态到界面（线程安全版本）
        /// </summary>
        private async Task SyncStatusFromAutoMonitorServiceAsync()
        {
            try
            {
                if (_autoMonitorService == null)
                {
                    _logger.LogDebug("AutoMonitorService 为空，跳过状态同步");
                    return;
                }

                // 🔧 简化版：从PositionProfiles获取执行状态
                var positionProfiles = _autoMonitorService.GetPositionProfiles();
                var statusUpdates = new List<(string symbol, string positionSide, string conditionType, int? tierIndex, TriggerExecutionStatus oldStatus, TriggerExecutionStatus newStatus)>();

                // 🔧 修复：在UI线程中安全地访问集合
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // 创建集合的副本以避免线程安全问题
                        var contractsCopy = _dataModel.ContractMonitors.ToList();
                        
                        // 遍历所有合约监控模型，同步状态
                        foreach (var contract in contractsCopy)
                        {
                            var symbol = contract.Symbol;
                            var positionSide = contract.PositionSide;
                            var contractKey = $"{symbol}_{positionSide}";
                            
                            // 🔧 简化版：从PositionProfile获取状态
                            var profile = positionProfiles.ContainsKey(contractKey) ? positionProfiles[contractKey] : null;
                            
                            // 检查并更新保本状态
                            var breakEvenCondition = contract.TriggerConditions.FirstOrDefault(c => c.Type == TriggerConditionType.BreakEven);
                            if (breakEvenCondition != null)
                            {
                                var isExecuted = profile?.TriggerRecords.ContainsKey("BreakEven") == true &&
                                               profile.TriggerRecords["BreakEven"].IsExecuted;
                                var newStatus = isExecuted ? TriggerExecutionStatus.Executed : TriggerExecutionStatus.NotTriggered;
                                
                                if (breakEvenCondition.Status != newStatus)
                                {
                                    var oldStatus = breakEvenCondition.Status;
                                    breakEvenCondition.Status = newStatus;
                                    breakEvenCondition.LastExecutionTime = isExecuted ? DateTime.Now : null;
                                    statusUpdates.Add((symbol, positionSide, "保本", null, oldStatus, newStatus));
                                    
                                    // 🔧 关键：触发属性变化通知
                                    breakEvenCondition.OnPropertyChanged(nameof(breakEvenCondition.Status));
                                    breakEvenCondition.OnPropertyChanged(nameof(breakEvenCondition.LastExecutionTime));
                                }
                            }
                            
                            // 检查并更新推仓状态
                            var addPositionConditions = contract.TriggerConditions.Where(c => c.Type == TriggerConditionType.AddPosition).ToList();
                            foreach (var condition in addPositionConditions)
                            {
                                var tierIndex = condition.TierIndex;
                                var triggerKey = $"AddPosition_{tierIndex}";
                                var isExecuted = profile?.TriggerRecords.ContainsKey(triggerKey) == true &&
                                               profile.TriggerRecords[triggerKey].IsExecuted;
                                var newStatus = isExecuted ? TriggerExecutionStatus.Executed : TriggerExecutionStatus.NotTriggered;
                                
                                if (condition.Status != newStatus)
                                {
                                    var oldStatus = condition.Status;
                                    condition.Status = newStatus;
                                    condition.LastExecutionTime = isExecuted ? DateTime.Now : null;
                                    statusUpdates.Add((symbol, positionSide, $"推仓{tierIndex}", tierIndex, oldStatus, newStatus));
                                    
                                    // 🔧 关键：触发属性变化通知
                                    condition.OnPropertyChanged(nameof(condition.Status));
                                    condition.OnPropertyChanged(nameof(condition.LastExecutionTime));
                                }
                            }
                            
                            // 检查并更新止盈状态
                            var profitConditions = contract.TriggerConditions.Where(c => c.Type == TriggerConditionType.ProfitProtection).ToList();
                            foreach (var condition in profitConditions)
                            {
                                var tierIndex = condition.TierIndex;
                                var triggerKey = $"ProfitProtection_{tierIndex}";
                                var isExecuted = profile?.TriggerRecords.ContainsKey(triggerKey) == true &&
                                               profile.TriggerRecords[triggerKey].IsExecuted;
                                var newStatus = isExecuted ? TriggerExecutionStatus.Executed : TriggerExecutionStatus.NotTriggered;
                                
                                if (condition.Status != newStatus)
                                {
                                    var oldStatus = condition.Status;
                                    condition.Status = newStatus;
                                    condition.LastExecutionTime = isExecuted ? DateTime.Now : null;
                                    statusUpdates.Add((symbol, positionSide, $"止盈{tierIndex}", tierIndex, oldStatus, newStatus));
                                    
                                    // 🔧 关键：触发属性变化通知
                                    condition.OnPropertyChanged(nameof(condition.Status));
                                    condition.OnPropertyChanged(nameof(condition.LastExecutionTime));
                                }
                            }

                            // 🔧 新增：触发合约模型的属性变化通知
                            contract.OnPropertyChanged(nameof(contract.TriggerConditions));
                            
                            // 🔧 新增：更新动态显示属性
                            contract.OnPropertyChanged(nameof(contract.BreakEvenDisplay));
                            contract.OnPropertyChanged(nameof(contract.BreakEvenStatusDisplay));
                            contract.OnPropertyChanged(nameof(contract.BreakEvenStatusColor));
                            
                            // 触发推仓相关属性更新
                            for (int i = 0; i < 10; i++)
                            {
                                contract.OnPropertyChanged($"AddPositionTier{i}Display");
                                contract.OnPropertyChanged($"AddPositionTier{i}Status");
                                contract.OnPropertyChanged($"AddPositionTier{i}StatusColor");
                            }
                            
                            // 触发止盈相关属性更新
                            for (int i = 0; i < 10; i++)
                            {
                                contract.OnPropertyChanged($"ProfitProtectionTier{i}Display");
                                contract.OnPropertyChanged($"ProfitProtectionTier{i}Status");
                                contract.OnPropertyChanged($"ProfitProtectionTier{i}StatusColor");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ UI线程中状态同步时发生异常");
                    }
                });

                // 🔧 异步记录状态更新日志
                if (statusUpdates.Any())
                {
                    foreach (var update in statusUpdates)
                    {
                        _logger.LogInformation($"🔄 {update.conditionType}状态更新: {update.symbol}_{update.positionSide} {update.oldStatus} → {update.newStatus}");
                        await _loggingService.LogOperationAsync($"🔄 {update.conditionType}状态更新: {update.symbol}_{update.positionSide} {update.oldStatus} → {update.newStatus}");
                    }
                    
                    _logger.LogInformation($"🔄 状态同步完成，更新了 {statusUpdates.Count} 个条件状态");
                    await _loggingService.LogOperationAsync($"🔄 状态同步完成，更新了 {statusUpdates.Count} 个条件状态");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 状态同步时发生异常");
                await _loggingService.LogErrorAsync("状态同步异常", ex);
            }
        }

        /// <summary>
        /// 更新持仓数据（线程安全版本）
        /// </summary>
        private async Task UpdatePositionDataAsync(List<BinanceFuturesTrader.Models.PositionInfo> positions)
        {
            try
            {
                // 🔧 修复：在UI线程中安全更新持仓数据
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        foreach (var position in positions)
                        {
                            var contract = _dataModel.ContractMonitors.FirstOrDefault(c => 
                                c.Symbol == position.Symbol && c.PositionSide == position.PositionSideString);
                            
                            if (contract != null)
                            {
                                // 更新持仓数据
                                contract.CurrentPrice = position.MarkPrice;
                                contract.PositionSize = Math.Abs(position.PositionAmt);
                                contract.UnrealizedPnl = position.UnrealizedProfit;
                                contract.IsActive = Math.Abs(position.PositionAmt) > 0;

                                // 🔧 关键：触发属性变化通知
                                contract.OnPropertyChanged(nameof(contract.CurrentPrice));
                                contract.OnPropertyChanged(nameof(contract.PositionSize));
                                contract.OnPropertyChanged(nameof(contract.UnrealizedPnl));
                                contract.OnPropertyChanged(nameof(contract.IsActive));
                                contract.OnPropertyChanged(nameof(contract.CurrentPriceText));
                                contract.OnPropertyChanged(nameof(contract.PositionSizeText));
                                contract.OnPropertyChanged(nameof(contract.PnlText));
                                contract.OnPropertyChanged(nameof(contract.PnlColor));
                                contract.OnPropertyChanged(nameof(contract.StatusText));
                                contract.OnPropertyChanged(nameof(contract.StatusColor));
                            }
                        }
                        
                        _logger.LogDebug($"📊 更新了 {positions.Count} 个持仓的数据");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ UI线程中更新持仓数据时发生异常");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 更新持仓数据时发生异常");
            }
        }

        public async Task RefreshDataAsync()
        {
            try
            {
                _dataModel.TotalPositions = _dataModel.ContractMonitors.Count;
                _dataModel.ActivePositions = _dataModel.ContractMonitors.Count;
                _dataModel.Uptime = DateTime.Now - _dataModel.StartTime;
                _logger.LogDebug("数据刷新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新数据时发生异常");
                await _loggingService.LogErrorAsync("数据刷新失败", ex);
            }
        }

        public async Task<bool> LoadContractConfigurationsFromPositionsAsync()
        {
            try
            {
                _logger.LogInformation("📊 开始从持仓加载合约配置");
                
                // 🔧 新增：强制刷新MainViewModel的持仓数据
                if (_mainViewModel != null)
                {
                    _logger.LogInformation("🔄 强制刷新MainViewModel持仓数据...");
                    try 
                    {
                        // 尝试触发账户数据刷新
                        var refreshMethod = _mainViewModel.GetType().GetMethod("RefreshAccountDataAsync");
                        if (refreshMethod != null)
                        {
                            var refreshTask = refreshMethod.Invoke(_mainViewModel, null) as Task;
                            if (refreshTask != null)
                            {
                                await refreshTask;
                                _logger.LogInformation("✅ MainViewModel账户数据刷新完成");
                                
                                // 等待1秒让数据同步
                                await Task.Delay(1000);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ 未找到RefreshAccountDataAsync方法");
                        }
                    }
                    catch (Exception refreshEx)
                    {
                        _logger.LogWarning(refreshEx, "⚠️ 强制刷新MainViewModel数据时出现异常，继续尝试获取持仓");
                    }
                }
                
                var currentPositions = GetCurrentPositions();
                if (currentPositions == null || !currentPositions.Any())
                {
                    _logger.LogWarning("⚠️ 当前没有持仓数据，无法加载合约配置");
                    return false;
                }
                _logger.LogInformation($"📈 检测到 {currentPositions.Count} 个持仓");
                
                // 🔧 修复：在UI线程中安全地清空和添加集合
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _dataModel.ContractMonitors.Clear();
                    foreach (var position in currentPositions)
                    {
                        var contractConfig = CreateContractConfigFromTemplate(position);
                        _dataModel.ContractMonitors.Add(contractConfig);
                        _logger.LogDebug($"✅ 创建合约配置: {contractConfig.ContractKey}");
                    }
                });
                await SaveContractConfigurationsAsync();
                _logger.LogInformation($"✅ 成功加载 {_dataModel.ContractMonitors.Count} 个合约配置");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 从持仓加载合约配置失败");
                return false;
            }
        }

        public async Task SaveContractConfigurationsAsync()
        {
            try
            {
                // 🔧 修复：在UI线程中安全获取集合副本
                List<ContractMonitorModel> contractsToSave = null;
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    contractsToSave = _dataModel.ContractMonitors.ToList();
                });
                
                _persistenceService.SaveContractConfigs(contractsToSave);
                await Task.CompletedTask;
                _logger.LogDebug("合约配置保存成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存合约配置失败");
                throw;
            }
        }

        private List<BinanceFuturesTrader.Models.PositionInfo> GetCurrentPositions()
        {
            try
            {
                _logger.LogInformation("🔍 开始获取当前持仓数据...");
                
                // 检查MainViewModel是否存在
                if (_mainViewModel == null)
                {
                    _logger.LogWarning("❌ MainViewModel 为空，无法获取持仓数据");
                    return new List<BinanceFuturesTrader.Models.PositionInfo>();
                }
                _logger.LogInformation("✅ MainViewModel 存在");
                
                // 检查Positions集合是否存在
                if (_mainViewModel.Positions == null)
                {
                    _logger.LogWarning("❌ MainViewModel.Positions 为空，无法获取持仓数据");
                    return new List<BinanceFuturesTrader.Models.PositionInfo>();
                }
                _logger.LogInformation($"✅ MainViewModel.Positions 存在，总数量: {_mainViewModel.Positions.Count}");
                
                // 获取所有持仓并分析
                var allPositions = _mainViewModel.Positions.ToList();
                _logger.LogInformation($"📊 分析持仓数据，总持仓记录: {allPositions.Count}");
                
                // 详细分析每个持仓
                foreach (var pos in allPositions)
                {
                    _logger.LogInformation($"   🔸 {pos.Symbol} {pos.PositionSideString}: PositionAmt={pos.PositionAmt}, MarkPrice={pos.MarkPrice}");
                }
                
                // 过滤有效持仓（PositionAmt != 0）
                var validPositions = allPositions.Where(p => p.PositionAmt != 0).ToList();
                _logger.LogInformation($"📈 过滤后有效持仓数量: {validPositions.Count}");
                
                if (validPositions.Any())
                {
                    _logger.LogInformation("✅ 找到有效持仓:");
                    foreach (var pos in validPositions)
                    {
                        _logger.LogInformation($"   ✓ {pos.Symbol} {pos.PositionSideString}: {pos.PositionAmt}");
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ 所有持仓的 PositionAmt 都为0，尝试使用更宽松的条件...");
                    
                    // 尝试更宽松的过滤条件
                    var anyPositions = allPositions.Where(p => !string.IsNullOrEmpty(p.Symbol)).ToList();
                    _logger.LogInformation($"🔄 使用宽松条件，找到: {anyPositions.Count} 个持仓记录");
                    
                    if (anyPositions.Any())
                    {
                        _logger.LogInformation("📋 宽松条件下的持仓列表:");
                        foreach (var pos in anyPositions)
                        {
                            _logger.LogInformation($"   📄 {pos.Symbol} {pos.PositionSideString}: Amt={pos.PositionAmt}, Entry={pos.EntryPrice}, Mark={pos.MarkPrice}");
                        }
                        return anyPositions; // 暂时返回所有有Symbol的持仓
                    }
                }
                
                return validPositions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 获取当前持仓时发生异常");
                return new List<BinanceFuturesTrader.Models.PositionInfo>();
            }
        }

        private ContractMonitorModel CreateContractConfigFromTemplate(BinanceFuturesTrader.Models.PositionInfo position)
        {
            var contract = new ContractMonitorModel
            {
                Symbol = position.Symbol,
                PositionSide = position.PositionSideString,
                CurrentPrice = position.MarkPrice,
                PositionSize = Math.Abs(position.PositionAmt),
                UnrealizedPnl = position.UnrealizedProfit,
                IsEnabled = true,
                IsActive = true
            };

            // 从MainViewModel获取当前自动监控配置
            var currentConfig = _mainViewModel?.CurrentAutoMonitorConfig;
            if (currentConfig != null)
            {
                _logger.LogInformation($"🔧 为合约 {contract.Symbol} 创建触发条件，基于当前配置: {currentConfig.Name}");
                
                int conditionId = 1;
                
                // 1. 创建保本触发条件
                if (currentConfig.BreakEvenConfig.IsEnabled)
                {
                    contract.TriggerConditions.Add(new TriggerConditionModel
                    {
                        Id = conditionId++,
                        Type = TriggerConditionType.BreakEven,
                        Description = "保本止盈",
                        TriggerPrice = currentConfig.BreakEvenConfig.TriggerProfitAmount, // 直接使用触发利润金额
                        Status = TriggerExecutionStatus.NotTriggered
                    });
                    _logger.LogInformation($"   ✅ 创建保本触发条件: 触发浮盈={currentConfig.BreakEvenConfig.TriggerProfitAmount}U");
                }

                // 2. 创建推仓触发条件
                if (currentConfig.AddPositionConfig.IsEnabled)
                {
                    var addPositionTiers = currentConfig.AddPositionConfig.Tiers.OrderBy(t => t.TriggerProfitAmount);
                    foreach (var tier in addPositionTiers)
                    {
                        contract.TriggerConditions.Add(new TriggerConditionModel
                        {
                            Id = conditionId++,
                            Type = TriggerConditionType.AddPosition,
                            Description = $"推仓{tier.TierIndex}",
                            TriggerPrice = tier.TriggerProfitAmount, // 直接使用触发利润金额
                            TierIndex = tier.TierIndex,
                            Status = TriggerExecutionStatus.NotTriggered
                        });
                        _logger.LogInformation($"   ✅ 创建推仓触发条件{tier.TierIndex}: 触发浮盈={tier.TriggerProfitAmount}U, 风险倍数={tier.RiskMultiplier}, 止损比例={tier.StopLossRatio*100}%");
                    }
                }

                // 3. 创建止盈保护触发条件  
                if (currentConfig.ProfitProtectionConfig.IsEnabled)
                {
                    var profitTiers = currentConfig.ProfitProtectionConfig.Tiers.OrderBy(t => t.TriggerProfitAmount);
                    foreach (var tier in profitTiers)
                    {
                        contract.TriggerConditions.Add(new TriggerConditionModel
                        {
                            Id = conditionId++,
                            Type = TriggerConditionType.ProfitProtection,
                            Description = $"止盈{tier.TierIndex}",
                            TriggerPrice = tier.TriggerProfitAmount, // 目标浮盈值
                            KeepValue = tier.ProtectionAmount, // 保留浮盈值
                            TierIndex = tier.TierIndex,
                            Status = TriggerExecutionStatus.NotTriggered
                        });
                        _logger.LogInformation($"   ✅ 创建止盈保护触发条件{tier.TierIndex}: 目标浮盈={tier.TriggerProfitAmount}U, 保留浮盈={tier.ProtectionAmount}U");
                    }
                }
            }
            else
            {
                _logger.LogWarning($"⚠️ 未找到当前自动监控配置，为合约 {contract.Symbol} 创建默认保本触发条件");
                
                // 默认创建保本触发条件
                contract.TriggerConditions.Add(new TriggerConditionModel
                {
                    Id = 1,
                    Type = TriggerConditionType.BreakEven,
                    Description = "保本止盈",
                    TriggerPrice = position.EntryPrice * (1 + 0.005m),
                    Status = TriggerExecutionStatus.NotTriggered
                });
            }

            return contract;
        }



        public async Task<bool> StartMonitoringWithPositionSyncAsync()
        {
            try
            {
                var result = await StartMonitoringAsync();
                if (result) StartPositionChangeMonitoring();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动集成监控失败");
                return false;
            }
        }

        public async Task<bool> StopMonitoringWithPositionSyncAsync()
        {
            try
            {
                StopPositionChangeMonitoring();
                return await StopMonitoringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止集成监控失败");
                return false;
            }
        }

        public async Task LoadConfigurationAsync()
        {
            try
            {
                await _configurationController.LoadConfigurationAsync();
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载配置时发生异常");
                await _loggingService.LogErrorAsync("配置加载失败", ex);
            }
        }

        public async Task SaveConfigurationAsync()
        {
            try
            {
                await _configurationController.SaveConfigurationAsync();
                await _loggingService.LogOperationAsync("配置保存成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存配置时发生异常");
                await _loggingService.LogErrorAsync("配置保存失败", ex);
            }
        }

        public void StartPositionChangeMonitoring()
        {
            _logger.LogInformation("启动持仓变化监听");
        }

        public void StopPositionChangeMonitoring()
        {
            _logger.LogInformation("停止持仓变化监听");
        }

        public void Dispose()
        {
            try
            {
                StopMonitoringLoop();
                _timerController?.Dispose();
                _logger.LogDebug("AutoMonitorController 资源清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理资源时发生异常");
            }
        }
    }
} 