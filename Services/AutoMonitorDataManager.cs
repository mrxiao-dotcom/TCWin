using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 自动盯盘数据管理器
    /// 核心职责：内存缓存管理、业务逻辑协调、数据持久化
    /// 架构原则：内存为主，文件为辅，定期和立即持久化相结合
    /// </summary>
    public class AutoMonitorDataManager : IDisposable
    {
        private readonly ILogger<AutoMonitorDataManager> _logger;
        private readonly FilePersistenceManager _persistenceManager;
        
        // 内存数据缓存（唯一数据源）
        private readonly ConcurrentDictionary<string, BaseConfig> _baseConfigs = new();
        private readonly ConcurrentDictionary<string, ContractState> _contractStates = new();
        private readonly List<ContractExecutionHistory> _executionHistory = new();
        
        // 当前状态
        private string _currentConfigId = string.Empty;
        private string _currentAccountName = string.Empty;
        
        // 定期保存
        private readonly Timer _periodicSaveTimer;
        private readonly object _historyLock = new();
        
        // 事件
        public event EventHandler<ConfigurationSwitchedEventArgs>? ConfigurationSwitched;
        public event EventHandler<ExecutionCompletedEventArgs>? ExecutionCompleted;
        public event EventHandler<PositionClosedEventArgs>? PositionClosed;

        public AutoMonitorDataManager(ILogger<AutoMonitorDataManager> logger, FilePersistenceManager persistenceManager)
        {
            _logger = logger;
            _persistenceManager = persistenceManager;
            
            // 启动60秒定期保存
            _periodicSaveTimer = new Timer(PeriodicSaveCallback, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
            
            _logger.LogInformation("🏗️ 自动盯盘数据管理器初始化完成");
        }

        #region 初始化和关闭

        /// <summary>
        /// 从文件初始化内存缓存
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("🔄 开始从文件加载数据到内存缓存...");

                // 加载基础配置
                var baseConfigFile = await _persistenceManager.LoadBaseConfigsAsync();
                _baseConfigs.Clear();
                foreach (var config in baseConfigFile.Configs)
                {
                    _baseConfigs[config.Id] = config;
                }
                _currentConfigId = baseConfigFile.CurrentConfigId;

                // 加载合约状态
                var contractStateFile = await _persistenceManager.LoadContractStatesAsync();
                _contractStates.Clear();
                foreach (var kvp in contractStateFile.States)
                {
                    _contractStates[kvp.Key] = kvp.Value;
                }
                _currentAccountName = contractStateFile.AccountName;

                // 加载执行历史
                var executionHistoryFile = await _persistenceManager.LoadExecutionHistoryAsync();
                lock (_historyLock)
                {
                    _executionHistory.Clear();
                    _executionHistory.AddRange(executionHistoryFile.History);
                }

                _logger.LogInformation($"✅ 数据加载完成: {_baseConfigs.Count} 个配置, {_contractStates.Count} 个合约状态, {_executionHistory.Count} 条历史记录");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 数据初始化失败");
                throw;
            }
        }

        /// <summary>
        /// 关闭和清理资源
        /// </summary>
        public async Task ShutdownAsync()
        {
            try
            {
                _logger.LogInformation("🔄 开始关闭数据管理器...");

                // 停止定期保存
                _periodicSaveTimer?.Dispose();

                // 最后一次强制保存
                await FlushAllDataAsync();

                _logger.LogInformation("✅ 数据管理器关闭完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 数据管理器关闭失败");
            }
        }

        #endregion

        #region 基础配置管理

        /// <summary>
        /// 获取所有基础配置
        /// </summary>
        public List<BaseConfig> GetAllBaseConfigs()
        {
            return _baseConfigs.Values.ToList();
        }

        /// <summary>
        /// 获取当前使用的配置
        /// </summary>
        public BaseConfig? GetCurrentConfig()
        {
            if (string.IsNullOrEmpty(_currentConfigId))
                return null;
                
            _baseConfigs.TryGetValue(_currentConfigId, out var config);
            return config;
        }

        /// <summary>
        /// 根据ID获取配置
        /// </summary>
        public BaseConfig? GetBaseConfig(string configId)
        {
            _baseConfigs.TryGetValue(configId, out var config);
            return config;
        }

        /// <summary>
        /// 添加或更新基础配置
        /// </summary>
        public async Task SaveBaseConfigAsync(BaseConfig config)
        {
            try
            {
                config.UpdatedAt = DateTime.UtcNow;
                _baseConfigs[config.Id] = config;

                // 立即保存到文件
                var baseConfigFile = CreateBaseConfigFile();
                await _persistenceManager.SaveImmediately(SaveTrigger.ConfigurationChanged, baseConfigFile);

                _logger.LogInformation($"💾 基础配置已保存: {config.Name} (ID: {config.Id})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 保存基础配置失败: {config.Name}");
                throw;
            }
        }

        /// <summary>
        /// 删除基础配置
        /// </summary>
        public async Task DeleteBaseConfigAsync(string configId)
        {
            try
            {
                if (_baseConfigs.TryRemove(configId, out var removedConfig))
                {
                    // 如果删除的是当前配置，清空当前配置ID
                    if (_currentConfigId == configId)
                    {
                        _currentConfigId = string.Empty;
                    }

                    // 立即保存到文件
                    var baseConfigFile = CreateBaseConfigFile();
                    await _persistenceManager.SaveImmediately(SaveTrigger.ConfigurationChanged, baseConfigFile);

                    _logger.LogInformation($"🗑️ 基础配置已删除: {removedConfig.Name} (ID: {configId})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 删除基础配置失败: {configId}");
                throw;
            }
        }

        /// <summary>
        /// 切换基础配置（重置所有合约状态）
        /// </summary>
        public async Task SwitchConfigurationAsync(string newConfigId)
        {
            try
            {
                if (!_baseConfigs.ContainsKey(newConfigId))
                {
                    throw new ArgumentException($"配置不存在: {newConfigId}");
                }

                var oldConfigId = _currentConfigId;
                _currentConfigId = newConfigId;

                _logger.LogInformation($"🔄 开始切换配置: {oldConfigId} → {newConfigId}");

                // 重置所有合约的执行状态
                var resetCount = 0;
                foreach (var contractState in _contractStates.Values)
                {
                    ResetContractExecutionStates(contractState, newConfigId);
                    resetCount++;
                }

                // 记录配置切换历史
                lock (_historyLock)
                {
                    _executionHistory.Add(new ContractExecutionHistory
                    {
                        Timestamp = DateTime.UtcNow,
                        ContractKey = "SYSTEM",
                        ExecutionType = ExecutionTypes.ConfigurationSwitched,
                        ExecutionResult = ExecutionResults.Success,
                        ExecutionDetails = $"配置切换: {oldConfigId} → {newConfigId}, 重置了 {resetCount} 个合约状态"
                    });
                }

                // 立即保存配置和状态变更
                var baseConfigFile = CreateBaseConfigFile();
                var contractStateFile = CreateContractStateFile();
                await _persistenceManager.SaveImmediately(SaveTrigger.ConfigurationSwitched, baseConfigFile, contractStateFile);

                // 触发事件
                ConfigurationSwitched?.Invoke(this, new ConfigurationSwitchedEventArgs
                {
                    OldConfigId = oldConfigId,
                    NewConfigId = newConfigId,
                    ResetContractCount = resetCount
                });

                _logger.LogInformation($"✅ 配置切换完成: {newConfigId}, 重置了 {resetCount} 个合约状态");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 配置切换失败: {newConfigId}");
                throw;
            }
        }

        #endregion

        #region 合约状态管理

        /// <summary>
        /// 获取所有活跃合约状态
        /// </summary>
        public List<ContractState> GetActiveContractStates()
        {
            return _contractStates.Values.Where(s => s.Meta.IsActive).ToList();
        }

        /// <summary>
        /// 获取所有合约状态
        /// </summary>
        public List<ContractState> GetAllContractStates()
        {
            return _contractStates.Values.ToList();
        }

        /// <summary>
        /// 获取指定合约状态
        /// </summary>
        public ContractState? GetContractState(string contractKey)
        {
            _contractStates.TryGetValue(contractKey, out var state);
            return state;
        }

        /// <summary>
        /// 添加或更新合约状态
        /// </summary>
        public async Task SaveContractStateAsync(ContractState contractState)
        {
            try
            {
                contractState.Meta.UpdatedAt = DateTime.UtcNow;
                _contractStates[contractState.GetKey()] = contractState;

                _logger.LogDebug($"💾 合约状态已更新: {contractState.GetKey()}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 保存合约状态失败: {contractState.GetKey()}");
                throw;
            }
        }

        /// <summary>
        /// 批量更新合约实时盈亏
        /// </summary>
        public async Task UpdateContractPnLBatchAsync(Dictionary<string, decimal> pnlUpdates)
        {
            try
            {
                var updateCount = 0;
                foreach (var kvp in pnlUpdates)
                {
                    if (_contractStates.TryGetValue(kvp.Key, out var contractState))
                    {
                        contractState.PositionInfo.UnrealizedPnl = kvp.Value;
                        contractState.PositionInfo.LastPriceUpdate = DateTime.UtcNow;
                        contractState.Meta.UpdatedAt = DateTime.UtcNow;
                        updateCount++;
                    }
                }

                if (updateCount > 0)
                {
                    _logger.LogDebug($"📊 批量更新盈亏数据: {updateCount} 个合约");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 批量更新盈亏失败");
                throw;
            }
        }

        /// <summary>
        /// 处理持仓平仓（删除合约状态）
        /// </summary>
        public async Task HandlePositionClosedAsync(string symbol, string positionSide)
        {
            try
            {
                var contractKey = $"{symbol}_{positionSide}";

                if (_contractStates.TryRemove(contractKey, out var removedState))
                {
                    // 记录到执行历史
                    lock (_historyLock)
                    {
                        _executionHistory.Add(new ContractExecutionHistory
                        {
                            Timestamp = DateTime.UtcNow,
                            ContractKey = contractKey,
                            ExecutionType = ExecutionTypes.PositionClosed,
                            ExecutionResult = ExecutionResults.Success,
                            ExecutionDetails = "持仓平仓，监控状态已删除"
                        });
                    }

                    // 立即保存状态变更
                    var contractStateFile = CreateContractStateFile();
                    await _persistenceManager.SaveImmediately(SaveTrigger.PositionClosed, contractStateFile);

                    // 触发事件
                    PositionClosed?.Invoke(this, new PositionClosedEventArgs
                    {
                        ContractKey = contractKey,
                        Symbol = symbol,
                        PositionSide = positionSide
                    });

                    _logger.LogInformation($"🗑️ 持仓平仓处理完成: {contractKey}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 处理持仓平仓失败: {symbol}_{positionSide}");
                throw;
            }
        }

        #endregion

        #region 执行历史查询

        /// <summary>
        /// 获取指定合约的执行历史
        /// </summary>
        public List<ContractExecutionHistory> GetContractExecutionHistory(string contractKey, int maxRecords = 100)
        {
            lock (_historyLock)
            {
                return _executionHistory
                    .Where(h => h.ContractKey == contractKey)
                    .OrderByDescending(h => h.Timestamp)
                    .Take(maxRecords)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取最近的执行历史
        /// </summary>
        public List<ContractExecutionHistory> GetRecentExecutionHistory(int maxRecords = 100)
        {
            lock (_historyLock)
            {
                return _executionHistory
                    .OrderByDescending(h => h.Timestamp)
                    .Take(maxRecords)
                    .ToList();
            }
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 重置合约执行状态
        /// </summary>
        private void ResetContractExecutionStates(ContractState contractState, string newConfigId)
        {
            contractState.BaseConfigId = newConfigId;

            // 重置保本状态
            contractState.ExecutionStates.Breakeven.State = ExecutionStateTypes.NotTriggered;
            contractState.ExecutionStates.Breakeven.ExecutedAt = null;
            contractState.ExecutionStates.Breakeven.ExecutionPnl = 0;

            // 重置推仓状态
            foreach (var tier in contractState.ExecutionStates.AddPositionTiers)
            {
                tier.State = ExecutionStateTypes.NotTriggered;
                tier.ExecutedAt = null;
                tier.ExecutionPnl = 0;
            }

            // 重置保盈状态
            foreach (var tier in contractState.ExecutionStates.ProfitProtectionTiers)
            {
                tier.State = ExecutionStateTypes.NotTriggered;
                tier.ExecutedAt = null;
                tier.ExecutionPnl = 0;
            }

            contractState.Meta.ConfigResetAt = DateTime.UtcNow;
            contractState.Meta.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 创建基础配置文件对象
        /// </summary>
        private BaseConfigFile CreateBaseConfigFile()
        {
            return new BaseConfigFile
            {
                Version = "2.0",
                LastUpdated = DateTime.UtcNow,
                CurrentConfigId = _currentConfigId,
                Configs = _baseConfigs.Values.ToList()
            };
        }

        /// <summary>
        /// 创建合约状态文件对象
        /// </summary>
        private ContractStateFile CreateContractStateFile()
        {
            return new ContractStateFile
            {
                Version = "2.0",
                LastUpdated = DateTime.UtcNow,
                CurrentConfigId = _currentConfigId,
                AccountName = _currentAccountName,
                States = new Dictionary<string, ContractState>(_contractStates)
            };
        }

        /// <summary>
        /// 创建执行历史文件对象
        /// </summary>
        private ExecutionHistoryFile CreateExecutionHistoryFile()
        {
            lock (_historyLock)
            {
                return new ExecutionHistoryFile
                {
                    Version = "2.0",
                    LastUpdated = DateTime.UtcNow,
                    History = new List<ContractExecutionHistory>(_executionHistory)
                };
            }
        }

        /// <summary>
        /// 强制保存所有数据
        /// </summary>
        private async Task FlushAllDataAsync()
        {
            try
            {
                var baseConfigFile = CreateBaseConfigFile();
                var contractStateFile = CreateContractStateFile();
                var executionHistoryFile = CreateExecutionHistoryFile();

                await _persistenceManager.SaveImmediately(SaveTrigger.PeriodicSave, baseConfigFile, contractStateFile, executionHistoryFile);

                _logger.LogDebug("💾 所有数据强制保存完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 强制保存失败");
            }
        }

        /// <summary>
        /// 定期保存回调
        /// </summary>
        private async void PeriodicSaveCallback(object? state)
        {
            try
            {
                await FlushAllDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 定期保存失败");
            }
        }

        #endregion

        #region 属性

        /// <summary>
        /// 当前配置ID
        /// </summary>
        public string CurrentConfigId => _currentConfigId;

        /// <summary>
        /// 当前账户名
        /// </summary>
        public string CurrentAccountName => _currentAccountName;

        /// <summary>
        /// 设置当前账户名
        /// </summary>
        public void SetCurrentAccountName(string accountName)
        {
            _currentAccountName = accountName;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _periodicSaveTimer?.Dispose();
        }

        #endregion
    }

    #region 事件参数类

    public class ConfigurationSwitchedEventArgs : EventArgs
    {
        public string OldConfigId { get; set; } = string.Empty;
        public string NewConfigId { get; set; } = string.Empty;
        public int ResetContractCount { get; set; }
    }

    public class ExecutionCompletedEventArgs : EventArgs
    {
        public string ContractKey { get; set; } = string.Empty;
        public string ExecutionType { get; set; } = string.Empty;
        public int? TierIndex { get; set; }
        public string Result { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }

    public class PositionClosedEventArgs : EventArgs
    {
        public string ContractKey { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string PositionSide { get; set; } = string.Empty;
    }

    #endregion
} 