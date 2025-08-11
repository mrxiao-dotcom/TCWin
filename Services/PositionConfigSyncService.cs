using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 🔄 持仓与统一配置文件同步服务
    /// 核心功能：确保统一配置文件与实际持仓保持同步
    /// </summary>
    public class PositionConfigSyncService
    {
        private readonly ILogger<PositionConfigSyncService>? _logger;
        private readonly string _accountName;
        private readonly ContractMonitoringStateService _stateService;
        private readonly BaseConfigManager _configManager;

        public PositionConfigSyncService(ILogger<PositionConfigSyncService>? logger = null, string? accountName = null)
        {
            _logger = logger;
            _accountName = accountName ?? "Test";
            
            // 创建必要的依赖服务
            var stateLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<ContractMonitoringStateService>();
            _configManager = BaseConfigManager.Instance;
            _stateService = new ContractMonitoringStateService(stateLogger, _configManager, null, _accountName);
            
            _logger?.LogInformation($"🔄 持仓配置同步服务已初始化，账户: {_accountName}");
        }

        /// <summary>
        /// 🎯 核心同步方法：将统一配置文件与当前持仓同步
        /// </summary>
        /// <param name="currentPositions">当前实际持仓列表</param>
        /// <returns>是否进行了同步操作</returns>
        public Task<bool> SyncConfigWithPositionsAsync(List<PositionInfo> currentPositions)
        {
            try
            {
                _logger?.LogInformation("🔄 开始执行持仓与配置文件同步");
                
                // 1. 获取当前基础配置
                var baseConfig = GetCurrentBaseConfig();
                if (baseConfig == null)
                {
                    _logger?.LogWarning("⚠️ 未找到基础配置，跳过同步");
                    return Task.FromResult(false);
                }

                // 2. 加载现有的统一配置状态
                var existingStates = _stateService.LoadMonitoringStates();
                _logger?.LogInformation($"📂 加载现有配置状态：{existingStates.Count} 个");

                // 3. 生成当前持仓的合约键列表 - 🔧【修复】使用Symbol_PositionSide格式
                var currentContractKeys = currentPositions
                    .Where(p => Math.Abs(p.PositionAmt) > 0) // 只包含有持仓的合约
                    .Select(p => $"{p.Symbol}_{p.PositionSideString}") // 使用Symbol_PositionSide格式
                    .ToHashSet();
                
                _logger?.LogInformation($"📊 当前实际持仓：{currentContractKeys.Count} 个");
                foreach (var key in currentContractKeys)
                {
                    _logger?.LogDebug($"   📋 {key}");
                }

                // 4. 分析需要同步的操作
                var toAdd = new List<PositionInfo>();
                var toRemove = new List<string>();

                // 4.1 找出需要添加的新持仓配置
                foreach (var position in currentPositions.Where(p => Math.Abs(p.PositionAmt) > 0))
                {
                    var contractKey = $"{position.Symbol}_{position.PositionSideString}"; // 🔧【修复】使用Symbol_PositionSide格式
                    if (!existingStates.ContainsKey(contractKey))
                    {
                        toAdd.Add(position);
                        _logger?.LogInformation($"➕ 需要添加配置：{contractKey}");
                    }
                }

                // 4.2 找出需要删除的多余配置（已平仓）
                foreach (var existingKey in existingStates.Keys)
                {
                    if (!currentContractKeys.Contains(existingKey))
                    {
                        toRemove.Add(existingKey);
                        _logger?.LogInformation($"➖ 需要删除配置：{existingKey} (已平仓)");
                    }
                }

                // 5. 执行同步操作
                bool hasChanges = false;

                // 5.1 添加新持仓的配置
                foreach (var position in toAdd)
                {
                    var newState = CreateConfigFromPosition(position, baseConfig);
                    var contractKey = $"{position.Symbol}_{position.PositionSideString}"; // 🔧【修复】使用Symbol_PositionSide格式
                    existingStates[contractKey] = newState;
                    hasChanges = true;
                    
                    _logger?.LogInformation($"✅ 已添加配置：{contractKey}");
                }

                // 5.2 删除多余的配置
                foreach (var keyToRemove in toRemove)
                {
                    existingStates.Remove(keyToRemove);
                    hasChanges = true;
                    
                    _logger?.LogInformation($"✅ 已删除配置：{keyToRemove}");
                }

                // 6. 保存更新后的配置文件
                if (hasChanges)
                {
                    _stateService.SaveMonitoringStates(existingStates);
                    _logger?.LogInformation($"💾 配置文件已更新保存，当前配置数：{existingStates.Count}");
                }
                else
                {
                    _logger?.LogInformation("✅ 配置文件与持仓已同步，无需更新");
                }

                return Task.FromResult(hasChanges);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 持仓配置同步失败");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 🔄 基于持仓创建新的配置状态
        /// </summary>
        private ContractMonitoringState CreateConfigFromPosition(PositionInfo position, AutoMonitorConfig baseConfig)
        {
            var state = new ContractMonitoringState
            {
                Symbol = position.Symbol,
                PositionSide = position.PositionSideString,
                IsActive = true,
                IsEnabled = true,
                LastUpdateTime = DateTime.Now,
                BaseConfigName = baseConfig.Name,
                Name = $"{baseConfig.Name}_{position.Symbol}",
                ScanIntervalSeconds = baseConfig.ScanIntervalSeconds,
                CooldownSeconds = baseConfig.CooldownSeconds
            };

            // 复制保本配置
            if (baseConfig.BreakEvenConfig != null)
            {
                state.BreakEvenConfig = new StatefulBreakEvenConfig
                {
                    IsEnabled = baseConfig.BreakEvenConfig.IsEnabled,
                    TriggerProfitAmount = baseConfig.BreakEvenConfig.TriggerProfitAmount,
                    ExecutionState = ExecutionState.NotTriggered,
                    ExecutionTime = null,
                    ExecutionPnl = 0,
                    ExecutionResult = ""
                };
            }

            // 复制推仓配置
            if (baseConfig.AddPositionConfig?.Tiers != null)
            {
                state.AddPositionConfig = new StatefulAddPositionConfig
                {
                    IsEnabled = baseConfig.AddPositionConfig.IsEnabled,
                    Tiers = baseConfig.AddPositionConfig.Tiers.Select(tier => new StatefulAddPositionTier
                    {
                        TierIndex = tier.TierIndex,
                        IsEnabled = tier.IsEnabled,
                        TriggerProfitAmount = tier.TriggerProfitAmount,
                        RiskMultiplier = tier.RiskMultiplier,
                        StopLossRatio = tier.StopLossRatio,
                        ProfitProtectionAmount = tier.ProfitProtectionAmount,
                        ExitTargetPnl = tier.ExitTargetPnl,
                        ExecutionState = ExecutionState.NotTriggered,
                        ExecutionTime = null,
                        ExecutionPnl = 0,
                        ExecutionResult = ""
                    }).ToList()
                };
            }

            // 复制保盈配置
            if (baseConfig.ProfitProtectionConfig?.Tiers != null)
            {
                state.ProfitProtectionConfig = new StatefulProfitProtectionConfig
                {
                    IsEnabled = baseConfig.ProfitProtectionConfig.IsEnabled,
                    Tiers = baseConfig.ProfitProtectionConfig.Tiers.Select(tier => new StatefulProfitProtectionTier
                    {
                        TierIndex = tier.TierIndex,
                        IsEnabled = tier.IsEnabled,
                        TriggerProfitAmount = tier.TriggerProfitAmount,
                        ProtectionAmount = tier.ProtectionAmount,
                        ExecutionState = ExecutionState.NotTriggered,
                        ExecutionTime = null,
                        ExecutionPnl = 0,
                        ExecutionResult = ""
                    }).ToList()
                };
            }

            _logger?.LogDebug($"🔧 为持仓 {position.Symbol}_{position.PositionSideString} 创建配置基于 {baseConfig.Name}");
            
            return state;
        }

        /// <summary>
        /// 📋 获取当前基础配置
        /// </summary>
        private AutoMonitorConfig? GetCurrentBaseConfig()
        {
            try
            {
                // 优先使用当前配置
                var currentConfig = _configManager.CurrentConfig;
                if (currentConfig != null)
                {
                    return currentConfig;
                }

                // 如果没有当前配置，查找智能默认配置
                var intelligentConfig = _configManager.Configurations
                    .FirstOrDefault(c => c.Name == "智能默认配置");
                if (intelligentConfig != null)
                {
                    return intelligentConfig;
                }

                // 最后使用任意可用配置
                var availableConfigs = _configManager.Configurations.ToList();
                if (availableConfigs.Any())
                {
                    return availableConfigs.First();
                }

                _logger?.LogWarning("⚠️ 未找到任何可用的基础配置");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 获取基础配置失败");
                return null;
            }
        }

        /// <summary>
        /// 🔄 手动触发同步（用于UI调用）
        /// </summary>
        public async Task<bool> TriggerSyncFromMainViewModelAsync()
        {
            try
            {
                _logger?.LogInformation("🎯 手动触发持仓配置同步");
                
                // 从MainViewModel获取当前持仓
                var positions = await GetCurrentPositionsFromMainViewModelAsync();
                if (positions == null || !positions.Any())
                {
                    _logger?.LogWarning("⚠️ 未获取到当前持仓数据");
                    return false;
                }

                return await SyncConfigWithPositionsAsync(positions);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 手动触发同步失败");
                return false;
            }
        }

        /// <summary>
        /// 📊 从MainViewModel获取当前持仓
        /// </summary>
        private async Task<List<PositionInfo>> GetCurrentPositionsFromMainViewModelAsync()
        {
            try
            {
                // 这里需要访问MainViewModel的持仓数据
                // 由于架构限制，这里返回空列表，实际使用时需要传入持仓数据
                _logger?.LogInformation("📊 尝试获取当前持仓数据...");
                
                // TODO: 实际实现中需要从外部传入持仓数据
                return new List<PositionInfo>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 获取持仓数据失败");
                return new List<PositionInfo>();
            }
        }

        /// <summary>
        /// 📈 统计同步结果
        /// </summary>
        public class SyncResult
        {
            public bool HasChanges { get; set; }
            public int AddedCount { get; set; }
            public int RemovedCount { get; set; }
            public int TotalConfigCount { get; set; }
            public List<string> AddedContracts { get; set; } = new();
            public List<string> RemovedContracts { get; set; } = new();
        }

        /// <summary>
        /// 🔄 执行同步并返回详细结果
        /// </summary>
        public async Task<SyncResult> SyncWithDetailedResultAsync(List<PositionInfo> currentPositions)
        {
            var result = new SyncResult();
            
            try
            {
                var hasChanges = await SyncConfigWithPositionsAsync(currentPositions);
                result.HasChanges = hasChanges;
                
                // 重新加载状态获取最新计数
                var states = _stateService.LoadMonitoringStates();
                result.TotalConfigCount = states.Count();
                
                _logger?.LogInformation($"📈 同步结果：变更={hasChanges}, 总配置数={result.TotalConfigCount}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 同步详细结果获取失败");
            }

            return result;
        }
    }
} 