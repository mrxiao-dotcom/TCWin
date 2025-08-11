using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 🎯 统一状态文件数据访问服务
    /// 确保所有UI组件只从contract_monitoring_states.json读取数据
    /// </summary>
    public class UnifiedStateDataService
    {
        private readonly ILogger<UnifiedStateDataService> _logger;
        private readonly FilePathManager _filePathManager;
        private readonly string _currentAccountName;
        private ContractMonitoringStateService _stateService;

        public UnifiedStateDataService(ILogger<UnifiedStateDataService> logger, string accountName = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _filePathManager = new FilePathManager();
            _currentAccountName = accountName ?? _filePathManager.GetCurrentAccountName();
            
            // 创建状态服务实例
            var stateLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ContractMonitoringStateService>();
            _stateService = new ContractMonitoringStateService(stateLogger, BaseConfigManager.Instance, _filePathManager, _currentAccountName);
            
            _logger.LogInformation($"🎯 统一状态数据服务已初始化，账户: {_currentAccountName}");
        }

        /// <summary>
        /// 🔧 获取统一状态文件路径
        /// </summary>
        public string GetStateFilePath()
        {
            return _filePathManager.GetContractMonitoringStatesFilePath(_currentAccountName);
        }

        /// <summary>
        /// ✅ 检查统一状态文件是否存在
        /// </summary>
        public bool StateFileExists()
        {
            var filePath = GetStateFilePath();
            var exists = File.Exists(filePath);
            _logger.LogDebug($"🔍 检查状态文件: {filePath} - 存在: {exists}");
            return exists;
        }

        /// <summary>
        /// 📊 从统一状态文件获取所有合约状态（唯一数据源）
        /// </summary>
        public Dictionary<string, ContractMonitoringState> GetAllContractStates()
        {
            try
            {
                if (!StateFileExists())
                {
                    _logger.LogWarning("⚠️ 统一状态文件不存在，返回空数据");
                    return new Dictionary<string, ContractMonitoringState>();
                }

                var states = _stateService.LoadMonitoringStates();
                _logger.LogInformation($"📊 从统一状态文件加载 {states.Count} 个合约状态");
                
                // 记录详细状态
                foreach (var kvp in states.Take(3))
                {
                    var state = kvp.Value;
                    _logger.LogDebug($"   📋 {kvp.Key}: {state.Symbol}_{state.PositionSide}, 配置:{state.BaseConfigName}, 活跃:{state.IsActive}");
                }
                
                return states;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 从统一状态文件加载数据失败");
                return new Dictionary<string, ContractMonitoringState>();
            }
        }

        /// <summary>
        /// 📊 获取活跃的合约状态
        /// </summary>
        public Dictionary<string, ContractMonitoringState> GetActiveContractStates()
        {
            var allStates = GetAllContractStates();
            var activeStates = allStates.Where(kvp => kvp.Value.IsActive && kvp.Value.CurrentQuantity > 0)
                                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            _logger.LogInformation($"📊 筛选出 {activeStates.Count} 个活跃合约状态");
            return activeStates;
        }

        /// <summary>
        /// 📊 获取特定合约的状态
        /// </summary>
        public ContractMonitoringState GetContractState(string contractKey)
        {
            var allStates = GetAllContractStates();
            if (allStates.TryGetValue(contractKey, out var state))
            {
                _logger.LogDebug($"📊 获取合约状态: {contractKey} - 找到");
                return state;
            }
            
            _logger.LogDebug($"📊 获取合约状态: {contractKey} - 未找到");
            return null;
        }

        /// <summary>
        /// 📊 获取UI显示用的合约监控信息
        /// </summary>
        public List<UnifiedContractMonitorViewModel> GetContractMonitorViewModels()
        {
            try
            {
                var states = GetAllContractStates();
                var viewModels = new List<UnifiedContractMonitorViewModel>();

                foreach (var kvp in states)
                {
                    var state = kvp.Value;
                    if (!state.IsActive) continue; // 只返回活跃的合约

                    var viewModel = CreateContractMonitorViewModel(state);
                    viewModels.Add(viewModel);
                }

                _logger.LogInformation($"📊 生成 {viewModels.Count} 个UI视图模型");
                return viewModels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 生成UI视图模型失败");
                return new List<UnifiedContractMonitorViewModel>();
            }
        }

        /// <summary>
        /// 🔧 从ContractMonitoringState创建UI视图模型
        /// </summary>
        private UnifiedContractMonitorViewModel CreateContractMonitorViewModel(ContractMonitoringState state)
        {
            var viewModel = new UnifiedContractMonitorViewModel
            {
                ContractKey = $"{state.Symbol}_{state.PositionSide}",
                Symbol = state.Symbol,
                PositionSide = state.PositionSide,
                ConfigName = state.BaseConfigName ?? "未知配置",
                Quantity = state.CurrentQuantity,
                EntryPrice = state.CurrentEntryPrice,
                MarkPrice = state.CurrentMarkPrice,
                UnrealizedPnl = state.CurrentUnrealizedPnl,
                IsEnabled = state.IsEnabled,
                LastUpdateTime = state.LastUpdateTime
            };

            // 设置保本状态
            if (state.BreakEvenConfig != null)
            {
                viewModel.BreakEvenStatus = GetExecutionStatusText(state.BreakEvenConfig.ExecutionState);
                viewModel.BreakEvenTriggerAmount = state.BreakEvenConfig.TriggerProfitAmount;
            }

            // 设置推仓状态 
            if (state.AddPositionConfig?.Tiers != null)
            {
                var executedTiers = state.AddPositionConfig.Tiers.Count(t => t.ExecutionState == ExecutionState.Executed);
                var totalTiers = state.AddPositionConfig.Tiers.Count;
                viewModel.AddPositionProgress = $"{executedTiers}/{totalTiers}";
            }

            // 设置保盈状态
            if (state.ProfitProtectionConfig?.Tiers != null)
            {
                var executedTiers = state.ProfitProtectionConfig.Tiers.Count(t => t.ExecutionState == ExecutionState.Executed);
                var totalTiers = state.ProfitProtectionConfig.Tiers.Count;
                viewModel.ProfitProtectionProgress = $"{executedTiers}/{totalTiers}";
            }

            return viewModel;
        }

        /// <summary>
        /// 🔧 获取执行状态的文本描述
        /// </summary>
        private string GetExecutionStatusText(ExecutionState state)
        {
            return state switch
            {
                ExecutionState.NotTriggered => "未触发",
                ExecutionState.Executing => "执行中",
                ExecutionState.Executed => "已执行",
                _ => "未知状态"
            };
        }

        /// <summary>
        /// 📊 获取统计信息
        /// </summary>
        public UnifiedStateStatistics GetStatistics()
        {
            try
            {
                var states = GetAllContractStates();
                var activeStates = states.Values.Where(s => s.IsActive).ToList();

                return new UnifiedStateStatistics
                {
                    TotalContracts = states.Count,
                    ActiveContracts = activeStates.Count,
                    EnabledContracts = activeStates.Count(s => s.IsEnabled),
                    ExecutedBreakEvens = activeStates.Count(s => s.BreakEvenConfig?.ExecutionState == ExecutionState.Executed),
                    TotalUnrealizedPnl = activeStates.Sum(s => s.CurrentUnrealizedPnl),
                    LastUpdateTime = states.Values.Any() ? 
                                      states.Values.Max(s => s.LastUpdateTime) : 
                                      DateTime.MinValue
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 获取统计信息失败");
                return new UnifiedStateStatistics();
            }
        }

        /// <summary>
        /// 🔧 刷新状态服务（重新加载文件）
        /// </summary>
        public void RefreshStateService()
        {
            try
            {
                var stateLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ContractMonitoringStateService>();
                _stateService = new ContractMonitoringStateService(stateLogger, BaseConfigManager.Instance, _filePathManager, _currentAccountName);
                _logger.LogDebug("🔄 状态服务已刷新");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 刷新状态服务失败");
            }
        }
    }

    /// <summary>
    /// 📊 统一数据服务专用统计信息
    /// </summary>
    public class UnifiedStateStatistics
    {
        public int TotalContracts { get; set; }
        public int ActiveContracts { get; set; }
        public int EnabledContracts { get; set; }
        public int ExecutedBreakEvens { get; set; }
        public decimal TotalUnrealizedPnl { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// 📊 统一数据服务专用合约监控视图模型
    /// </summary>
    public class UnifiedContractMonitorViewModel
    {
        public string ContractKey { get; set; }
        public string Symbol { get; set; }
        public string PositionSide { get; set; }
        public string ConfigName { get; set; }
        public decimal Quantity { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal MarkPrice { get; set; }
        public decimal UnrealizedPnl { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime? LastUpdateTime { get; set; }
        
        // 状态信息
        public string BreakEvenStatus { get; set; }
        public decimal BreakEvenTriggerAmount { get; set; }
        public string AddPositionProgress { get; set; }
        public string ProfitProtectionProgress { get; set; }
    }
} 