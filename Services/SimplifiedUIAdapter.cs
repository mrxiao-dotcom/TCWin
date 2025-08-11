using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Views;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 🎯 简化UI适配器 - 将简化数据模型与现有UI组件对接
    /// 确保UI能够使用新的简化数据结构，同时保持向后兼容性
    /// </summary>
    public class SimplifiedUIAdapter
    {
        private readonly ILogger<SimplifiedUIAdapter> _logger;
        private readonly SimplifiedConfigManager _configManager;
        private readonly SimplifiedStateService _stateService;
        private readonly SimplifiedAutoMonitorService _monitorService;

        public SimplifiedUIAdapter(
            ILogger<SimplifiedUIAdapter> logger,
            SimplifiedConfigManager configManager,
            SimplifiedStateService stateService,
            SimplifiedAutoMonitorService monitorService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
            _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
        }

        #region UI数据绑定适配

        /// <summary>
        /// 获取UI绑定的合约配置列表
        /// </summary>
        public async Task<ObservableCollection<ContractConfigViewModel>> GetUIContractConfigsAsync()
        {
            try
            {
                var viewModels = await _configManager.GetAllContractViewModelsAsync();
                var observableCollection = new ObservableCollection<ContractConfigViewModel>(viewModels);
                
                _logger.LogDebug($"📋 获取UI合约配置: {observableCollection.Count} 个");
                return observableCollection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 获取UI合约配置失败");
                return new ObservableCollection<ContractConfigViewModel>();
            }
        }

        /// <summary>
        /// 刷新UI合约配置数据
        /// </summary>
        public async Task<bool> RefreshUIContractConfigsAsync(ObservableCollection<ContractConfigViewModel> uiCollection)
        {
            try
            {
                var newViewModels = await _configManager.GetAllContractViewModelsAsync();
                
                // 使用UI线程更新集合
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 记录现有的已执行状态，防止被覆盖
                    var existingExecutedStates = new Dictionary<string, Dictionary<string, string>>();
                    foreach (var existing in uiCollection)
                    {
                        var key = $"{existing.Symbol}_{existing.Side}";
                        existingExecutedStates[key] = new Dictionary<string, string>
                        {
                            ["BreakEven"] = existing.BreakEvenStatus,
                            ["Push1"] = existing.GetDynamicData("Push1"),
                            ["Push2"] = existing.GetDynamicData("Push2"),
                            ["Push3"] = existing.GetDynamicData("Push3"),
                            ["Push4"] = existing.GetDynamicData("Push4"),
                            ["Profit1"] = existing.GetDynamicData("Profit1"),
                            ["Profit2"] = existing.GetDynamicData("Profit2"),
                            ["Profit3"] = existing.GetDynamicData("Profit3"),
                        };
                    }

                    // 清空并重新添加
                    uiCollection.Clear();
                    
                    foreach (var newViewModel in newViewModels)
                    {
                        var key = $"{newViewModel.Symbol}_{newViewModel.Side}";
                        
                        // 恢复已执行状态
                        if (existingExecutedStates.TryGetValue(key, out var states))
                        {
                            foreach (var state in states)
                            {
                                if (state.Value == "√")
                                {
                                    if (state.Key == "BreakEven")
                                        newViewModel.BreakEvenStatus = "√";
                                    else
                                        newViewModel.SetDynamicData(state.Key, "√");
                                }
                            }
                        }
                        
                        uiCollection.Add(newViewModel);
                    }
                });

                _logger.LogDebug($"✅ UI合约配置刷新成功: {newViewModels.Count} 个");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 刷新UI合约配置失败");
                return false;
            }
        }

        /// <summary>
        /// 更新单个合约的UI状态
        /// </summary>
        public async Task UpdateContractUIStateAsync(ObservableCollection<ContractConfigViewModel> uiCollection, string symbol, string positionSide, string operationType, int tierIndex, bool isExecuted)
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var contractViewModel = uiCollection.FirstOrDefault(c => 
                        c.Symbol == symbol && c.Side == positionSide);

                    if (contractViewModel == null)
                    {
                        _logger.LogWarning($"⚠️ 未找到UI中的合约: {symbol}_{positionSide}");
                        return;
                    }

                    var newStatus = isExecuted ? "√" : "-";
                    var statusText = isExecuted ? "已执行" : "未触发";

                    switch (operationType.ToUpper())
                    {
                        case "BREAKEVEN":
                            contractViewModel.BreakEvenStatus = newStatus;
                            _logger.LogDebug($"🔄 更新保本状态: {symbol}_{positionSide} -> {statusText}");
                            break;

                        case "ADDPOSITION":
                            var pushKey = $"Push{tierIndex}";
                            contractViewModel.SetDynamicData(pushKey, newStatus);
                            _logger.LogDebug($"🔄 更新推仓阶梯{tierIndex}状态: {symbol}_{positionSide} -> {statusText}");
                            break;

                        case "PROFITPROTECTION":
                            var profitKey = $"Profit{tierIndex}";
                            contractViewModel.SetDynamicData(profitKey, newStatus);
                            _logger.LogDebug($"🔄 更新保盈阶梯{tierIndex}状态: {symbol}_{positionSide} -> {statusText}");
                            break;
                    }

                    // 通知UI更新
                    contractViewModel.NotifyAllPropertiesChanged();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 更新合约UI状态失败: {symbol}_{positionSide}");
            }
        }

        #endregion

        #region 监控服务适配

        /// <summary>
        /// 启动监控服务（UI适配版本）
        /// </summary>
        public async Task<bool> StartMonitoringForUIAsync()
        {
            try
            {
                var result = await _monitorService.StartMonitoringAsync();
                
                if (result)
                {
                    _logger.LogInformation("✅ 通过UI适配器启动监控成功");
                }
                else
                {
                    _logger.LogWarning("⚠️ 通过UI适配器启动监控失败");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ UI适配器启动监控异常");
                return false;
            }
        }

        /// <summary>
        /// 停止监控服务（UI适配版本）
        /// </summary>
        public void StopMonitoringForUI()
        {
            try
            {
                _monitorService.StopMonitoring();
                _logger.LogInformation("✅ 通过UI适配器停止监控成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ UI适配器停止监控异常");
            }
        }

        /// <summary>
        /// 获取监控状态（UI适配版本）
        /// </summary>
        public bool IsMonitoringRunning()
        {
            return _monitorService.IsRunning;
        }

        /// <summary>
        /// 手动执行扫描（UI适配版本）
        /// </summary>
        public async Task<bool> ExecuteManualScanForUIAsync()
        {
            try
            {
                var result = await _monitorService.ExecuteManualScanAsync();
                
                if (result)
                {
                    _logger.LogInformation("✅ 通过UI适配器手动扫描成功");
                }
                else
                {
                    _logger.LogWarning("⚠️ 通过UI适配器手动扫描失败");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ UI适配器手动扫描异常");
                return false;
            }
        }

        #endregion

        #region 配置管理适配

        /// <summary>
        /// 为UI创建新的合约配置
        /// </summary>
        public async Task<bool> CreateContractConfigForUIAsync(string symbol, string positionSide, string configName)
        {
            try
            {
                var contractState = await _configManager.CreateOrUpdateContractConfigAsync(symbol, positionSide, configName);
                
                _logger.LogInformation($"✅ 通过UI适配器创建合约配置: {symbol}_{positionSide} -> {configName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ UI适配器创建合约配置失败: {symbol}_{positionSide}");
                return false;
            }
        }

        /// <summary>
        /// 获取可用的基础配置名称（UI适配版本）
        /// </summary>
        public async Task<List<string>> GetAvailableConfigNamesForUIAsync()
        {
            try
            {
                var configNames = await _configManager.GetAvailableConfigNamesAsync();
                _logger.LogDebug($"📋 获取可用配置: {configNames.Count} 个");
                return configNames;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 获取可用配置失败");
                return new List<string>();
            }
        }

        /// <summary>
        /// 重置合约执行状态（UI操作）
        /// </summary>
        public async Task<bool> ResetContractExecutionStatesForUIAsync(string symbol, string positionSide)
        {
            try
            {
                var contractState = await _stateService.GetContractStateAsync(symbol, positionSide);
                if (contractState == null)
                {
                    _logger.LogWarning($"⚠️ 合约状态不存在: {symbol}_{positionSide}");
                    return false;
                }

                // 重置保本状态
                await _stateService.UpdateExecutionStateAsync(symbol, positionSide, "BREAKEVEN", 0, StandardExecutionState.NotTriggered, "UI重置");

                // 重置推仓状态
                foreach (var tier in contractState.AddPositionConfig.Tiers)
                {
                    await _stateService.UpdateExecutionStateAsync(symbol, positionSide, "ADDPOSITION", tier.TierIndex, StandardExecutionState.NotTriggered, "UI重置");
                }

                // 重置保盈状态
                foreach (var tier in contractState.ProfitProtectionConfig.Tiers)
                {
                    await _stateService.UpdateExecutionStateAsync(symbol, positionSide, "PROFITPROTECTION", tier.TierIndex, StandardExecutionState.NotTriggered, "UI重置");
                }

                _logger.LogInformation($"✅ 重置合约执行状态: {symbol}_{positionSide}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 重置合约执行状态失败: {symbol}_{positionSide}");
                return false;
            }
        }

        #endregion

        #region 统计信息适配

        /// <summary>
        /// 获取UI显示的统计信息
        /// </summary>
        public async Task<SimplifiedUIStats> GetUIStatsAsync()
        {
            try
            {
                var monitorStats = await _monitorService.GetMonitorStatsAsync();
                var configStats = await _configManager.GetConfigUsageStatsAsync();
                var executionStats = await _configManager.GetExecutionStatsAsync();

                var uiStats = new SimplifiedUIStats
                {
                    MonitorStats = monitorStats,
                    ConfigUsageStats = configStats,
                    ExecutionStats = executionStats,
                    LastUpdateTime = DateTime.Now
                };

                return uiStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 获取UI统计信息失败");
                return new SimplifiedUIStats();
            }
        }

        #endregion

        #region 事件转发适配

        /// <summary>
        /// 设置事件转发，将服务层事件转发给UI
        /// </summary>
        public void SetupEventForwarding(
            Action<SimplifiedMonitorStatusChangedEventArgs>? onMonitorStatusChanged = null,
            Action<SimplifiedExecutionResult>? onExecutionCompleted = null,
            Action<string>? onLogRequested = null)
        {
            if (onMonitorStatusChanged != null)
            {
                _monitorService.MonitorStatusChanged += (sender, args) => onMonitorStatusChanged(args);
            }

            if (onExecutionCompleted != null)
            {
                _monitorService.ExecutionCompleted += (sender, result) => onExecutionCompleted(result);
            }

            if (onLogRequested != null)
            {
                _monitorService.LogRequested += (sender, log) => onLogRequested(log);
            }

            _logger.LogInformation("✅ UI事件转发设置完成");
        }

        #endregion

        #region 数据验证和修复

        /// <summary>
        /// 验证UI数据一致性
        /// </summary>
        public async Task<bool> ValidateUIDataConsistencyAsync(ObservableCollection<ContractConfigViewModel> uiCollection)
        {
            try
            {
                var contractStates = await _stateService.GetContractStatesAsync();
                var inconsistencies = new List<string>();

                foreach (var uiContract in uiCollection)
                {
                    var contractKey = $"{uiContract.Symbol}_{uiContract.Side}";
                    
                    if (!contractStates.TryGetValue(contractKey, out var state))
                    {
                        inconsistencies.Add($"UI中存在但状态文件中不存在: {contractKey}");
                        continue;
                    }

                    // 验证保本状态
                    var fileBreakEvenStatus = ExecutionStateExtensions.FromInt(state.BreakEvenConfig.ExecutionState).ToDisplayText();
                    if (uiContract.BreakEvenStatus != fileBreakEvenStatus)
                    {
                        inconsistencies.Add($"{contractKey} 保本状态不一致: UI({uiContract.BreakEvenStatus}) vs 文件({fileBreakEvenStatus})");
                    }

                    // 验证推仓状态
                    for (int i = 1; i <= 4; i++)
                    {
                        var uiStatus = uiContract.GetDynamicData($"Push{i}");
                        var fileTier = state.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == i);
                        var fileStatus = fileTier != null ? ExecutionStateExtensions.FromInt(fileTier.ExecutionState).ToDisplayText() : "-";
                        
                        if (uiStatus != fileStatus)
                        {
                            inconsistencies.Add($"{contractKey} 推仓{i}状态不一致: UI({uiStatus}) vs 文件({fileStatus})");
                        }
                    }
                }

                if (inconsistencies.Any())
                {
                    _logger.LogWarning($"⚠️ 发现UI数据不一致: {inconsistencies.Count} 个问题");
                    foreach (var issue in inconsistencies)
                    {
                        _logger.LogWarning($"   - {issue}");
                    }
                    return false;
                }

                _logger.LogDebug("✅ UI数据一致性验证通过");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ UI数据一致性验证失败");
                return false;
            }
        }

        /// <summary>
        /// 修复UI数据不一致问题
        /// </summary>
        public async Task<bool> FixUIDataInconsistencyAsync(ObservableCollection<ContractConfigViewModel> uiCollection)
        {
            try
            {
                _logger.LogInformation("🔧 开始修复UI数据不一致问题");
                
                // 使用文件数据作为准确来源，更新UI
                var result = await RefreshUIContractConfigsAsync(uiCollection);
                
                if (result)
                {
                    _logger.LogInformation("✅ UI数据不一致问题修复完成");
                }
                else
                {
                    _logger.LogWarning("⚠️ UI数据不一致问题修复失败");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 修复UI数据不一致问题失败");
                return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// UI统计信息模型
    /// </summary>
    public class SimplifiedUIStats
    {
        public SimplifiedMonitorStats? MonitorStats { get; set; }
        public Dictionary<string, int>? ConfigUsageStats { get; set; }
        public Dictionary<string, Dictionary<string, int>>? ExecutionStats { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }
} 