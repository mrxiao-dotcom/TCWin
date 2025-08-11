using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Views;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 🎯 简化配置管理器 - 基于新规范的配置管理
    /// 负责基础配置与合约状态的映射和转换
    /// </summary>
    public class SimplifiedConfigManager
    {
        private readonly ILogger<SimplifiedConfigManager> _logger;
        private readonly SimplifiedStateService _stateService;
        
        // 事件：配置变更通知
        public event EventHandler<string>? ConfigurationChanged;

        public SimplifiedConfigManager(ILogger<SimplifiedConfigManager> logger, SimplifiedStateService stateService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        }

        #region 基础配置管理

        /// <summary>
        /// 获取所有可用的基础配置名称
        /// </summary>
        public async Task<List<string>> GetAvailableConfigNamesAsync()
        {
            var configs = await _stateService.GetBaseConfigsAsync();
            return configs.Keys.ToList();
        }

        /// <summary>
        /// 获取指定的基础配置
        /// </summary>
        public async Task<SimplifiedBaseConfig?> GetBaseConfigAsync(string configName)
        {
            return await _stateService.GetBaseConfigAsync(configName);
        }

        /// <summary>
        /// 验证基础配置是否有效
        /// </summary>
        public async Task<bool> ValidateBaseConfigAsync(string configName)
        {
            var config = await GetBaseConfigAsync(configName);
            if (config == null)
            {
                _logger.LogWarning($"⚠️ 基础配置不存在: {configName}");
                return false;
            }

            // 验证配置完整性
            if (string.IsNullOrEmpty(config.Name))
            {
                _logger.LogWarning($"⚠️ 基础配置名称为空: {configName}");
                return false;
            }

            // 验证推仓配置
            if (config.AddPositionConfig.IsEnabled && !config.AddPositionConfig.Tiers.Any())
            {
                _logger.LogWarning($"⚠️ 推仓配置已启用但无阶梯设置: {configName}");
                return false;
            }

            // 验证保盈配置
            if (config.ProfitProtectionConfig.IsEnabled && !config.ProfitProtectionConfig.Tiers.Any())
            {
                _logger.LogWarning($"⚠️ 保盈配置已启用但无阶梯设置: {configName}");
                return false;
            }

            _logger.LogDebug($"✅ 基础配置验证通过: {configName}");
            return true;
        }

        #endregion

        #region 合约配置管理

        /// <summary>
        /// 为合约创建或更新配置
        /// </summary>
        public async Task<SimplifiedContractState> CreateOrUpdateContractConfigAsync(string symbol, string positionSide, string configName)
        {
            // 验证基础配置
            if (!await ValidateBaseConfigAsync(configName))
            {
                throw new ArgumentException($"基础配置无效或不存在: {configName}");
            }

            var contractKey = $"{symbol}_{positionSide}";
            
            // 检查是否已存在
            var existingState = await _stateService.GetContractStateAsync(symbol, positionSide);
            
            if (existingState != null)
            {
                _logger.LogInformation($"🔄 更新合约配置: {contractKey} -> {configName}");
                
                // 如果配置名称相同，保持现有状态
                if (existingState.ConfigName == configName)
                {
                    _logger.LogDebug($"📋 配置无变化，保持现有状态: {contractKey}");
                    return existingState;
                }
                
                // 配置变更，需要重新初始化（但可以选择保留某些状态）
                _logger.LogWarning($"⚠️ 配置变更，将重新初始化状态: {contractKey} ({existingState.ConfigName} -> {configName})");
            }

            // 初始化新的合约状态
            var newState = await _stateService.InitializeContractStateAsync(symbol, positionSide, configName);
            
            // 触发配置变更事件
            ConfigurationChanged?.Invoke(this, contractKey);
            
            _logger.LogInformation($"✅ 合约配置创建/更新成功: {contractKey} -> {configName}");
            return newState;
        }

        /// <summary>
        /// 批量创建合约配置
        /// </summary>
        public async Task<List<SimplifiedContractState>> CreateContractConfigsBatchAsync(List<(string symbol, string positionSide, string configName)> contracts)
        {
            var results = new List<SimplifiedContractState>();
            
            foreach (var (symbol, positionSide, configName) in contracts)
            {
                try
                {
                    var state = await CreateOrUpdateContractConfigAsync(symbol, positionSide, configName);
                    results.Add(state);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 批量创建合约配置失败: {symbol}_{positionSide} -> {configName}");
                }
            }
            
            _logger.LogInformation($"📦 批量创建合约配置完成: {results.Count}/{contracts.Count} 成功");
            return results;
        }

        /// <summary>
        /// 删除合约配置
        /// </summary>
        public async Task<bool> RemoveContractConfigAsync(string symbol, string positionSide)
        {
            var contractKey = $"{symbol}_{positionSide}";
            
            try
            {
                var states = await _stateService.GetContractStatesAsync();
                if (states.Remove(contractKey))
                {
                    // 这里需要 SimplifiedStateService 提供删除方法
                    // 暂时通过设置为null来标记删除
                    _logger.LogInformation($"🗑️ 删除合约配置: {contractKey}");
                    
                    // 触发配置变更事件
                    ConfigurationChanged?.Invoke(this, contractKey);
                    return true;
                }
                
                _logger.LogWarning($"⚠️ 合约配置不存在，无法删除: {contractKey}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 删除合约配置失败: {contractKey}");
                return false;
            }
        }

        #endregion

        #region 状态查询和转换

        /// <summary>
        /// 获取合约的UI显示模型
        /// </summary>
        public async Task<ContractConfigViewModel?> GetContractViewModelAsync(string symbol, string positionSide)
        {
            var contractState = await _stateService.GetContractStateAsync(symbol, positionSide);
            if (contractState == null) return null;

            // 转换为UI ViewModel
            var viewModel = new ContractConfigViewModel
            {
                Symbol = contractState.Symbol,
                Side = contractState.PositionSide,
                ContractName = $"{contractState.Symbol}_{contractState.PositionSide}",
                
                // 保本状态
                BreakEvenStatus = ExecutionStateExtensions.FromInt(contractState.BreakEvenConfig.ExecutionState).ToDisplayText(),
                BreakEvenTarget = contractState.BreakEvenConfig.TriggerProfitAmount,
                
                // 推仓金额
                PushTier1Amount = GetTierTriggerAmount(contractState.AddPositionConfig.Tiers, 1),
                PushTier2Amount = GetTierTriggerAmount(contractState.AddPositionConfig.Tiers, 2),
                PushTier3Amount = GetTierTriggerAmount(contractState.AddPositionConfig.Tiers, 3),
                PushTier4Amount = GetTierTriggerAmount(contractState.AddPositionConfig.Tiers, 4),
                
                // 保盈金额
                ProfitTier1TriggerAmount = GetProfitTierTriggerAmount(contractState.ProfitProtectionConfig.Tiers, 1),
                ProfitTier2TriggerAmount = GetProfitTierTriggerAmount(contractState.ProfitProtectionConfig.Tiers, 2),
                ProfitTier3TriggerAmount = GetProfitTierTriggerAmount(contractState.ProfitProtectionConfig.Tiers, 3),
                
                ProfitTier1ProtectionAmount = GetProfitTierProtectionAmount(contractState.ProfitProtectionConfig.Tiers, 1),
                ProfitTier2ProtectionAmount = GetProfitTierProtectionAmount(contractState.ProfitProtectionConfig.Tiers, 2),
                ProfitTier3ProtectionAmount = GetProfitTierProtectionAmount(contractState.ProfitProtectionConfig.Tiers, 3),
            };

            // 设置推仓状态 (使用动态数据设置)
            viewModel.SetDynamicData("Push1", GetTierDisplayStatus(contractState.AddPositionConfig.Tiers, 1));
            viewModel.SetDynamicData("Push2", GetTierDisplayStatus(contractState.AddPositionConfig.Tiers, 2));
            viewModel.SetDynamicData("Push3", GetTierDisplayStatus(contractState.AddPositionConfig.Tiers, 3));
            viewModel.SetDynamicData("Push4", GetTierDisplayStatus(contractState.AddPositionConfig.Tiers, 4));
            
            // 设置保盈状态 (使用动态数据设置)
            viewModel.SetDynamicData("Profit1", GetTierDisplayStatus(contractState.ProfitProtectionConfig.Tiers, 1));
            viewModel.SetDynamicData("Profit2", GetTierDisplayStatus(contractState.ProfitProtectionConfig.Tiers, 2));
            viewModel.SetDynamicData("Profit3", GetTierDisplayStatus(contractState.ProfitProtectionConfig.Tiers, 3));

            _logger.LogDebug($"📋 转换合约ViewModel: {symbol}_{positionSide}");
            return viewModel;
        }

        /// <summary>
        /// 获取所有合约的UI显示模型
        /// </summary>
        public async Task<List<ContractConfigViewModel>> GetAllContractViewModelsAsync()
        {
            var states = await _stateService.GetContractStatesAsync();
            var viewModels = new List<ContractConfigViewModel>();

            foreach (var kvp in states)
            {
                var viewModel = await GetContractViewModelAsync(kvp.Value.Symbol, kvp.Value.PositionSide);
                if (viewModel != null)
                {
                    viewModels.Add(viewModel);
                }
            }

            _logger.LogDebug($"📋 获取所有合约ViewModel: {viewModels.Count} 个");
            return viewModels;
        }

        /// <summary>
        /// 获取阶梯显示状态的辅助方法
        /// </summary>
        private string GetTierDisplayStatus<T>(List<T> tiers, int tierIndex) where T : class
        {
            var tier = tiers.FirstOrDefault(t => 
            {
                var prop = t.GetType().GetProperty("TierIndex");
                var value = prop?.GetValue(t);
                return prop != null && value != null && (int)value == tierIndex;
            });

            if (tier == null) return "-";

            var stateProp = tier.GetType().GetProperty("ExecutionState");
            if (stateProp == null) return "-";

            var stateValue = stateProp.GetValue(tier);
            if (stateValue == null) return "-";
            
            var state = (int)stateValue;
            return ExecutionStateExtensions.FromInt(state).ToDisplayText();
        }

        /// <summary>
        /// 获取推仓阶梯触发金额的辅助方法
        /// </summary>
        private decimal GetTierTriggerAmount(List<SimplifiedAddPositionTierState> tiers, int tierIndex)
        {
            return tiers.FirstOrDefault(t => t.TierIndex == tierIndex)?.TriggerProfitAmount ?? 0m;
        }

        /// <summary>
        /// 获取保盈阶梯触发金额的辅助方法
        /// </summary>
        private decimal GetProfitTierTriggerAmount(List<SimplifiedProfitProtectionTierState> tiers, int tierIndex)
        {
            return tiers.FirstOrDefault(t => t.TierIndex == tierIndex)?.TriggerProfitAmount ?? 0m;
        }

        /// <summary>
        /// 获取保盈阶梯保护金额的辅助方法
        /// </summary>
        private decimal GetProfitTierProtectionAmount(List<SimplifiedProfitProtectionTierState> tiers, int tierIndex)
        {
            return tiers.FirstOrDefault(t => t.TierIndex == tierIndex)?.ProtectionAmount ?? 0m;
        }

        #endregion

        #region 配置统计和分析

        /// <summary>
        /// 获取配置使用统计
        /// </summary>
        public async Task<Dictionary<string, int>> GetConfigUsageStatsAsync()
        {
            var states = await _stateService.GetContractStatesAsync();
            var stats = new Dictionary<string, int>();

            foreach (var state in states.Values)
            {
                var configName = state.ConfigName ?? "未知";
                if (stats.ContainsKey(configName))
                {
                    stats[configName]++;
                }
                else
                {
                    stats[configName] = 1;
                }
            }

            _logger.LogDebug($"📊 配置使用统计: {stats.Count} 种配置");
            return stats;
        }

        /// <summary>
        /// 获取执行状态统计
        /// </summary>
        public async Task<Dictionary<string, Dictionary<string, int>>> GetExecutionStatsAsync()
        {
            var states = await _stateService.GetContractStatesAsync();
            var stats = new Dictionary<string, Dictionary<string, int>>
            {
                ["保本"] = new Dictionary<string, int>(),
                ["推仓"] = new Dictionary<string, int>(),
                ["保盈"] = new Dictionary<string, int>()
            };

            foreach (var state in states.Values)
            {
                // 统计保本状态
                var breakEvenState = ExecutionStateExtensions.FromInt(state.BreakEvenConfig.ExecutionState).ToString();
                IncrementStat(stats["保本"], breakEvenState);

                // 统计推仓状态
                foreach (var tier in state.AddPositionConfig.Tiers)
                {
                    var tierState = ExecutionStateExtensions.FromInt(tier.ExecutionState).ToString();
                    IncrementStat(stats["推仓"], tierState);
                }

                // 统计保盈状态
                foreach (var tier in state.ProfitProtectionConfig.Tiers)
                {
                    var tierState = ExecutionStateExtensions.FromInt(tier.ExecutionState).ToString();
                    IncrementStat(stats["保盈"], tierState);
                }
            }

            _logger.LogDebug($"📊 执行状态统计完成");
            return stats;
        }

        private void IncrementStat(Dictionary<string, int> stats, string key)
        {
            if (stats.ContainsKey(key))
            {
                stats[key]++;
            }
            else
            {
                stats[key] = 1;
            }
        }

        #endregion
    }
} 