using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 🎯 简化状态管理服务 - 基于新规范的统一状态管理
    /// 职责清晰：基础配置只存参数，状态文件只存状态
    /// </summary>
    public class SimplifiedStateService
    {
        private readonly ILogger<SimplifiedStateService> _logger;
        private readonly string _baseConfigPath;
        private readonly string _contractStatesPath;
        private readonly object _fileLock = new object();

        // 内存缓存
        private SimplifiedBaseConfigFile? _baseConfigFile;
        private SimplifiedContractStatesFile? _contractStatesFile;
        private DateTime _lastConfigLoad = DateTime.MinValue;
        private DateTime _lastStateLoad = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(1);

        public SimplifiedStateService(ILogger<SimplifiedStateService> logger, string dataDirectory = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            var dataDir = dataDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(dataDir);
            
            _baseConfigPath = Path.Combine(dataDir, "BaseConfig.json");
            _contractStatesPath = Path.Combine(dataDir, "ContractStates.json");
            
            _logger.LogInformation($"🎯 简化状态服务已初始化");
            _logger.LogInformation($"📁 基础配置路径: {_baseConfigPath}");
            _logger.LogInformation($"📊 状态文件路径: {_contractStatesPath}");
        }

        #region 基础配置管理（只读）

        /// <summary>
        /// 获取所有基础配置
        /// </summary>
        public async Task<Dictionary<string, SimplifiedBaseConfig>> GetBaseConfigsAsync()
        {
            try
            {
                // 检查缓存
                if (_baseConfigFile != null && (DateTime.Now - _lastConfigLoad) < _cacheTimeout)
                {
                    return _baseConfigFile.Configs;
                }

                if (!File.Exists(_baseConfigPath))
                {
                    _logger.LogWarning($"📁 基础配置文件不存在，创建默认配置: {_baseConfigPath}");
                    await CreateDefaultBaseConfigAsync();
                }

                var json = await File.ReadAllTextAsync(_baseConfigPath);
                _baseConfigFile = JsonSerializer.Deserialize<SimplifiedBaseConfigFile>(json);
                _lastConfigLoad = DateTime.Now;

                _logger.LogDebug($"✅ 加载基础配置成功: {_baseConfigFile?.Configs.Count ?? 0}个配置");
                return _baseConfigFile?.Configs ?? new Dictionary<string, SimplifiedBaseConfig>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 加载基础配置失败: {_baseConfigPath}");
                return new Dictionary<string, SimplifiedBaseConfig>();
            }
        }

        /// <summary>
        /// 获取指定名称的基础配置
        /// </summary>
        public async Task<SimplifiedBaseConfig?> GetBaseConfigAsync(string configName)
        {
            var configs = await GetBaseConfigsAsync();
            return configs.TryGetValue(configName, out var config) ? config : null;
        }

        /// <summary>
        /// 创建默认的基础配置文件
        /// </summary>
        private async Task CreateDefaultBaseConfigAsync()
        {
            var defaultConfig = new SimplifiedBaseConfigFile
            {
                Configs = new Dictionary<string, SimplifiedBaseConfig>
                {
                    ["基础"] = new SimplifiedBaseConfig
                    {
                        Name = "基础",
                        Description = "基础交易策略配置",
                        BreakEvenConfig = new SimplifiedBreakEvenConfig
                        {
                            IsEnabled = true,
                            TriggerProfitAmount = 42.0m
                        },
                        AddPositionConfig = new SimplifiedAddPositionConfig
                        {
                            IsEnabled = true,
                            Tiers = new List<SimplifiedAddPositionTier>
                            {
                                new() { TierIndex = 1, TriggerProfitAmount = 1.00m, RiskMultiplier = 1.0m, StopLossRatio = 0.10m },
                                new() { TierIndex = 2, TriggerProfitAmount = 9.03m, RiskMultiplier = 1.0m, StopLossRatio = 0.10m },
                                new() { TierIndex = 3, TriggerProfitAmount = 13.55m, RiskMultiplier = 1.0m, StopLossRatio = 0.10m }
                            }
                        },
                        ProfitProtectionConfig = new SimplifiedProfitProtectionConfig
                        {
                            IsEnabled = true,
                            Tiers = new List<SimplifiedProfitProtectionTier>
                            {
                                new() { TierIndex = 1, TriggerProfitAmount = 45.16m, ProtectionAmount = 36.13m },
                                new() { TierIndex = 2, TriggerProfitAmount = 90.32m, ProtectionAmount = 72.26m },
                                new() { TierIndex = 3, TriggerProfitAmount = 135.48m, ProtectionAmount = 108.39m }
                            }
                        }
                    },
                    ["保守"] = new SimplifiedBaseConfig
                    {
                        Name = "保守",
                        Description = "保守交易策略配置",
                        BreakEvenConfig = new SimplifiedBreakEvenConfig
                        {
                            IsEnabled = true,
                            TriggerProfitAmount = 50.0m
                        },
                        AddPositionConfig = new SimplifiedAddPositionConfig
                        {
                            IsEnabled = true,
                            Tiers = new List<SimplifiedAddPositionTier>
                            {
                                new() { TierIndex = 1, TriggerProfitAmount = 2.00m, RiskMultiplier = 0.5m, StopLossRatio = 0.08m },
                                new() { TierIndex = 2, TriggerProfitAmount = 10.00m, RiskMultiplier = 0.5m, StopLossRatio = 0.08m }
                            }
                        },
                        ProfitProtectionConfig = new SimplifiedProfitProtectionConfig
                        {
                            IsEnabled = true,
                            Tiers = new List<SimplifiedProfitProtectionTier>
                            {
                                new() { TierIndex = 1, TriggerProfitAmount = 60.00m, ProtectionAmount = 50.00m }
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_baseConfigPath, json);
            _logger.LogInformation($"✅ 创建默认基础配置文件: {_baseConfigPath}");
        }

        #endregion

        #region 合约状态管理

        /// <summary>
        /// 获取所有合约状态
        /// </summary>
        public async Task<Dictionary<string, SimplifiedContractState>> GetContractStatesAsync()
        {
            try
            {
                // 检查缓存
                if (_contractStatesFile != null && (DateTime.Now - _lastStateLoad) < _cacheTimeout)
                {
                    return _contractStatesFile.States;
                }

                if (!File.Exists(_contractStatesPath))
                {
                    _logger.LogInformation($"📊 状态文件不存在，创建空状态文件: {_contractStatesPath}");
                    _contractStatesFile = new SimplifiedContractStatesFile();
                    await SaveContractStatesAsync();
                    return _contractStatesFile.States;
                }

                var json = await File.ReadAllTextAsync(_contractStatesPath);
                _contractStatesFile = JsonSerializer.Deserialize<SimplifiedContractStatesFile>(json) ?? new SimplifiedContractStatesFile();
                _lastStateLoad = DateTime.Now;

                _logger.LogDebug($"✅ 加载合约状态成功: {_contractStatesFile.States.Count}个合约");
                return _contractStatesFile.States;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 加载合约状态失败: {_contractStatesPath}");
                return new Dictionary<string, SimplifiedContractState>();
            }
        }

        /// <summary>
        /// 获取指定合约的状态
        /// </summary>
        public async Task<SimplifiedContractState?> GetContractStateAsync(string symbol, string positionSide)
        {
            var contractKey = $"{symbol}_{positionSide}";
            var states = await GetContractStatesAsync();
            return states.TryGetValue(contractKey, out var state) ? state : null;
        }

        /// <summary>
        /// 初始化合约状态（从基础配置创建）
        /// </summary>
        public async Task<SimplifiedContractState> InitializeContractStateAsync(string symbol, string positionSide, string configName)
        {
            var baseConfig = await GetBaseConfigAsync(configName);
            if (baseConfig == null)
            {
                throw new ArgumentException($"基础配置 '{configName}' 不存在");
            }

            var contractKey = $"{symbol}_{positionSide}";
            var contractState = new SimplifiedContractState
            {
                Symbol = symbol,
                PositionSide = positionSide,
                ConfigName = configName,
                LastUpdateTime = DateTime.UtcNow,
                BreakEvenConfig = new SimplifiedBreakEvenState
                {
                    TriggerProfitAmount = baseConfig.BreakEvenConfig.TriggerProfitAmount,
                    ExecutionState = 0
                },
                AddPositionConfig = new SimplifiedAddPositionState
                {
                    Tiers = baseConfig.AddPositionConfig.Tiers.Select(t => new SimplifiedAddPositionTierState
                    {
                        TierIndex = t.TierIndex,
                        TriggerProfitAmount = t.TriggerProfitAmount,
                        RiskMultiplier = t.RiskMultiplier,
                        StopLossRatio = t.StopLossRatio,
                        ExecutionState = 0
                    }).ToList()
                },
                ProfitProtectionConfig = new SimplifiedProfitProtectionState
                {
                    Tiers = baseConfig.ProfitProtectionConfig.Tiers.Select(t => new SimplifiedProfitProtectionTierState
                    {
                        TierIndex = t.TierIndex,
                        TriggerProfitAmount = t.TriggerProfitAmount,
                        ProtectionAmount = t.ProtectionAmount,
                        ExecutionState = 0
                    }).ToList()
                }
            };

            // 保存到状态文件
            await SetContractStateAsync(contractKey, contractState);
            
            _logger.LogInformation($"🎯 初始化合约状态: {contractKey} -> 配置: {configName}");
            return contractState;
        }

        /// <summary>
        /// 设置合约状态
        /// </summary>
        public async Task SetContractStateAsync(string contractKey, SimplifiedContractState contractState)
        {
            lock (_fileLock)
            {
                try
                {
                    var states = GetContractStatesAsync().Result;
                    contractState.LastUpdateTime = DateTime.UtcNow;
                    states[contractKey] = contractState;
                    
                    _contractStatesFile = new SimplifiedContractStatesFile { States = states };
                    SaveContractStatesAsync().Wait();
                    
                    // 清除缓存，下次读取时重新加载
                    _lastStateLoad = DateTime.MinValue;
                    
                    _logger.LogDebug($"✅ 更新合约状态: {contractKey}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 设置合约状态失败: {contractKey}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 更新执行状态 - 核心方法
        /// </summary>
        public async Task UpdateExecutionStateAsync(string symbol, string positionSide, string operationType, int tierIndex, StandardExecutionState executionState, string executionResult = "")
        {
            var contractKey = $"{symbol}_{positionSide}";
            var contractState = await GetContractStateAsync(symbol, positionSide);
            
            if (contractState == null)
            {
                _logger.LogWarning($"⚠️ 合约状态不存在，无法更新执行状态: {contractKey}");
                return;
            }

            lock (_fileLock)
            {
                try
                {
                    switch (operationType.ToUpper())
                    {
                        case "BREAKEVEN":
                            contractState.BreakEvenConfig.ExecutionState = (int)executionState;
                            contractState.BreakEvenConfig.ExecutionTime = executionState != StandardExecutionState.NotTriggered ? DateTime.UtcNow : null;
                            contractState.BreakEvenConfig.ExecutionResult = executionResult;
                            break;

                        case "ADDPOSITION":
                            var addTier = contractState.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
                            if (addTier != null)
                            {
                                addTier.ExecutionState = (int)executionState;
                                addTier.ExecutionTime = executionState != StandardExecutionState.NotTriggered ? DateTime.UtcNow : null;
                                addTier.ExecutionResult = executionResult;
                            }
                            break;

                        case "PROFITPROTECTION":
                            var profitTier = contractState.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
                            if (profitTier != null)
                            {
                                profitTier.ExecutionState = (int)executionState;
                                profitTier.ExecutionTime = executionState != StandardExecutionState.NotTriggered ? DateTime.UtcNow : null;
                                profitTier.ExecutionResult = executionResult;
                            }
                            break;
                    }

                    SetContractStateAsync(contractKey, contractState).Wait();
                    
                    _logger.LogInformation($"🚨 状态更新成功: {contractKey} -> {operationType}");
                    if (tierIndex > 0) _logger.LogInformation($"   阶梯: {tierIndex}, 状态: {executionState}, 结果: {executionResult}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ 更新执行状态失败: {contractKey} -> {operationType}-{tierIndex}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 保存状态文件
        /// </summary>
        private async Task SaveContractStatesAsync()
        {
            if (_contractStatesFile == null) return;

            try
            {
                var json = JsonSerializer.Serialize(_contractStatesFile, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_contractStatesPath, json);
                _logger.LogDebug($"✅ 保存状态文件成功: {_contractStatesPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 保存状态文件失败: {_contractStatesPath}");
                throw;
            }
        }

        #endregion

        #region 状态查询辅助方法

        /// <summary>
        /// 检查操作是否可以执行（状态为未触发）
        /// </summary>
        public async Task<bool> CanExecuteAsync(string symbol, string positionSide, string operationType, int tierIndex = 0)
        {
            var contractState = await GetContractStateAsync(symbol, positionSide);
            if (contractState == null) return false;

            return operationType.ToUpper() switch
            {
                "BREAKEVEN" => ExecutionStateExtensions.FromInt(contractState.BreakEvenConfig.ExecutionState).CanExecute(),
                "ADDPOSITION" => contractState.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex)?.ExecutionState == 0,
                "PROFITPROTECTION" => contractState.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex)?.ExecutionState == 0,
                _ => false
            };
        }

        /// <summary>
        /// 获取操作的执行状态
        /// </summary>
        public async Task<StandardExecutionState> GetExecutionStateAsync(string symbol, string positionSide, string operationType, int tierIndex = 0)
        {
            var contractState = await GetContractStateAsync(symbol, positionSide);
            if (contractState == null) return StandardExecutionState.NotTriggered;

            var state = operationType.ToUpper() switch
            {
                "BREAKEVEN" => contractState.BreakEvenConfig.ExecutionState,
                "ADDPOSITION" => contractState.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex)?.ExecutionState ?? 0,
                "PROFITPROTECTION" => contractState.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex)?.ExecutionState ?? 0,
                _ => 0
            };

            return ExecutionStateExtensions.FromInt(state);
        }

        #endregion

        #region 清理和维护

        /// <summary>
        /// 清理无效的合约状态
        /// </summary>
        public async Task CleanupInvalidStatesAsync()
        {
            lock (_fileLock)
            {
                try
                {
                    var states = GetContractStatesAsync().Result;
                    var invalidKeys = new List<string>();

                    foreach (var kvp in states)
                    {
                        if (string.IsNullOrEmpty(kvp.Value.Symbol) || string.IsNullOrEmpty(kvp.Value.PositionSide))
                        {
                            invalidKeys.Add(kvp.Key);
                        }
                    }

                    foreach (var key in invalidKeys)
                    {
                        states.Remove(key);
                        _logger.LogWarning($"🗑️ 清理无效状态: {key}");
                    }

                    if (invalidKeys.Any())
                    {
                        _contractStatesFile = new SimplifiedContractStatesFile { States = states };
                        SaveContractStatesAsync().Wait();
                        _logger.LogInformation($"✅ 清理完成，移除 {invalidKeys.Count} 个无效状态");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 清理无效状态失败");
                }
            }
        }

        /// <summary>
        /// 重置所有执行状态为未触发
        /// </summary>
        public async Task ResetAllExecutionStatesAsync()
        {
            lock (_fileLock)
            {
                try
                {
                    var states = GetContractStatesAsync().Result;
                    
                    foreach (var state in states.Values)
                    {
                        // 重置保本状态
                        state.BreakEvenConfig.ExecutionState = 0;
                        state.BreakEvenConfig.ExecutionTime = null;
                        state.BreakEvenConfig.ExecutionResult = string.Empty;

                        // 重置推仓状态
                        foreach (var tier in state.AddPositionConfig.Tiers)
                        {
                            tier.ExecutionState = 0;
                            tier.ExecutionTime = null;
                            tier.ExecutionResult = string.Empty;
                        }

                        // 重置保盈状态
                        foreach (var tier in state.ProfitProtectionConfig.Tiers)
                        {
                            tier.ExecutionState = 0;
                            tier.ExecutionTime = null;
                            tier.ExecutionResult = string.Empty;
                        }

                        state.LastUpdateTime = DateTime.UtcNow;
                    }

                    _contractStatesFile = new SimplifiedContractStatesFile { States = states };
                    SaveContractStatesAsync().Wait();
                    
                    _logger.LogInformation($"🔄 重置所有执行状态完成: {states.Count} 个合约");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 重置执行状态失败");
                    throw;
                }
            }
        }

        #endregion
    }
} 