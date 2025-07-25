using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 统一持久化服务 - 支持多账号隔离的配置和状态持久化功能
    /// </summary>
    public class UnifiedPersistenceService
    {
        private readonly FilePathManager _filePathManager;
        private readonly string _currentAccountName;
        private readonly ILogger<UnifiedPersistenceService>? _logger;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        public UnifiedPersistenceService(
            ILogger<UnifiedPersistenceService>? logger = null, 
            FilePathManager? filePathManager = null,
            string? accountName = null)
        {
            _logger = logger;
            _filePathManager = filePathManager ?? new FilePathManager();
            _currentAccountName = accountName ?? _filePathManager.GetCurrentAccountName();
            
            _logger?.LogDebug($"📁 统一持久化服务初始化 - 账号: {_currentAccountName}");
            _logger?.LogDebug($"📁 账号目录: {_filePathManager.GetAccountDirectory(_currentAccountName)}");
        }

        #region 路径获取辅助方法

        private string GetExecutionHistoryPath() => _filePathManager.GetExecutionHistoryFilePath(_currentAccountName);
        private string GetConfigsPath() => _filePathManager.GetBaseConfigsFilePath(); // 基础配置是全局的
        private string GetContractMonitoringStatePath() => _filePathManager.GetContractMonitoringStatesFilePath(_currentAccountName);

        #endregion
        
        #region 状态持久化
        
        /// <summary>
        /// 保存持仓档案状态
        /// ⚠️ 已废弃：请使用 ContractMonitoringStateService 替代
        /// </summary>
        [Obsolete("已废弃：请使用 ContractMonitoringStateService 替代，数据现在统一保存在 contract_monitoring_states.json")]
        public void SavePositionProfiles(Dictionary<string, PositionProfile> profiles)
        {
            _logger?.LogWarning("⚠️ SavePositionProfiles 已废弃：请使用 ContractMonitoringStateService 替代");
            // 不再执行任何操作，数据现在通过 contract_monitoring_states.json 管理
        }
        
        /// <summary>
        /// 加载持仓档案状态
        /// ⚠️ 已废弃：请使用 ContractMonitoringStateService 替代
        /// </summary>
        [Obsolete("已废弃：请使用 ContractMonitoringStateService 替代，数据现在统一从 contract_monitoring_states.json 读取")]
        public Dictionary<string, PositionProfile> LoadPositionProfiles()
        {
            _logger?.LogWarning("⚠️ LoadPositionProfiles 已废弃：请使用 ContractMonitoringStateService 替代");
            return new Dictionary<string, PositionProfile>();
        }
        
        /// <summary>
        /// 保存执行历史
        /// </summary>
        public void SaveExecutionHistory(List<ExecutionHistory> history)
        {
            try
            {
                if (history == null || !history.Any())
                {
                    _logger?.LogDebug("💡 没有执行历史需要保存");
                    return;
                }
                
                // 只保存最近7天的执行历史
                var cutoffTime = DateTime.Now.AddDays(-7);
                var recentHistory = history
                    .Where(h => h.ExecutionTime > cutoffTime)
                    .OrderByDescending(h => h.ExecutionTime)
                    .Take(1000) // 最多保存1000条记录
                    .ToList();
                
                var json = JsonSerializer.Serialize(recentHistory, _jsonOptions);
                File.WriteAllText(GetExecutionHistoryPath(), json);
                
                _logger?.LogInformation($"💾 已保存执行历史: {recentHistory.Count} 条");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 保存执行历史失败");
            }
        }
        
        /// <summary>
        /// 加载执行历史
        /// </summary>
        public List<ExecutionHistory> LoadExecutionHistory()
        {
            try
            {
                var executionHistoryPath = GetExecutionHistoryPath();
                if (!File.Exists(executionHistoryPath))
                {
                    _logger?.LogDebug($"💡 执行历史文件不存在，返回空列表 (账号: {_currentAccountName})");
                    return new List<ExecutionHistory>();
                }
                
                var json = File.ReadAllText(executionHistoryPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger?.LogDebug("💡 执行历史文件为空，返回空列表");
                    return new List<ExecutionHistory>();
                }
                
                var history = JsonSerializer.Deserialize<List<ExecutionHistory>>(json, _jsonOptions) 
                    ?? new List<ExecutionHistory>();
                
                _logger?.LogInformation($"📖 已加载执行历史: {history.Count} 条");
                
                return history;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 加载执行历史失败");
                return new List<ExecutionHistory>();
            }
        }
        
        #endregion
        
        #region 新的统一监控状态持久化
        
        /// <summary>
        /// 保存合约监控状态
        /// </summary>
        public void SaveContractMonitoringStates(Dictionary<string, ContractMonitoringState> states)
        {
            try
            {
                if (states == null || !states.Any())
                {
                    _logger?.LogDebug("💡 没有监控状态需要保存");
                    return;
                }
                
                // 只保存活跃的监控状态
                var activeStates = states
                    .Where(kvp => kvp.Value.IsActive)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                var json = JsonSerializer.Serialize(activeStates, _jsonOptions);
                File.WriteAllText(GetContractMonitoringStatePath(), json);
                
                _logger?.LogInformation($"💾 已保存监控状态: {activeStates.Count} 个合约");
                foreach (var state in activeStates.Values)
                {
                    _logger?.LogDebug($"   📝 {state.Symbol}_{state.PositionSide} - 配置: {state.BaseConfigName}, 浮盈: {state.CurrentUnrealizedPnl:F2}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 保存监控状态失败");
            }
        }
        
        /// <summary>
        /// 加载合约监控状态
        /// </summary>
        public Dictionary<string, ContractMonitoringState> LoadContractMonitoringStates()
        {
            try
            {
                var contractMonitoringStatePath = GetContractMonitoringStatePath();
                if (!File.Exists(contractMonitoringStatePath))
                {
                    _logger?.LogDebug($"💡 监控状态文件不存在，返回空字典 (账号: {_currentAccountName})");
                    return new Dictionary<string, ContractMonitoringState>();
                }
                
                var json = File.ReadAllText(contractMonitoringStatePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger?.LogDebug("💡 监控状态文件为空，返回空字典");
                    return new Dictionary<string, ContractMonitoringState>();
                }
                
                var states = JsonSerializer.Deserialize<Dictionary<string, ContractMonitoringState>>(json, _jsonOptions) 
                    ?? new Dictionary<string, ContractMonitoringState>();
                
                // 清理过期的状态（超过24小时的非活跃状态）
                var cutoffTime = DateTime.Now.AddHours(-24);
                var validStates = states
                    .Where(kvp => kvp.Value.IsActive || kvp.Value.LastUpdateTime > cutoffTime)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                _logger?.LogInformation($"📖 已加载监控状态: {validStates.Count} 个合约");
                foreach (var state in validStates.Values)
                {
                    _logger?.LogDebug($"   📝 {state.Symbol}_{state.PositionSide} - 配置: {state.BaseConfigName}, 浮盈: {state.CurrentUnrealizedPnl:F2}");
                }
                
                return validStates;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 加载监控状态失败");
                return new Dictionary<string, ContractMonitoringState>();
            }
        }
        
        /// <summary>
        /// 从基础配置和持仓数据生成监控状态
        /// </summary>
        public ContractMonitoringState CreateMonitoringStateFromBaseConfig(
            string symbol, 
            string positionSide, 
            decimal currentQuantity, 
            decimal currentEntryPrice, 
            decimal currentMarkPrice, 
            decimal currentPnl,
            AutoMonitorConfig baseConfig)
        {
            var state = new ContractMonitoringState
            {
                Symbol = symbol,
                PositionSide = positionSide,
                BaseConfigName = baseConfig.Name,
                InitialQuantity = currentQuantity,
                InitialEntryPrice = currentEntryPrice,
                CurrentQuantity = currentQuantity,
                CurrentEntryPrice = currentEntryPrice,
                CurrentMarkPrice = currentMarkPrice,
                CurrentUnrealizedPnl = currentPnl,
                CreateTime = DateTime.Now,
                LastUpdateTime = DateTime.Now,
                IsActive = true
            };
            
            // 从基础配置复制保本配置
            state.BreakEvenConfig = new StatefulBreakEvenConfig
            {
                IsEnabled = baseConfig.BreakEvenConfig.IsEnabled,
                TriggerProfitAmount = baseConfig.BreakEvenConfig.TriggerProfitAmount,
                ExecutionState = ExecutionState.NotTriggered,
                ExecutionResult = ""
            };
            
            // 从基础配置复制推仓配置
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
                    ExecutionResult = ""
                }).ToList()
            };
            
            // 从基础配置复制保盈配置
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
                    ExecutionResult = ""
                }).ToList()
            };
            
            _logger?.LogDebug($"🆕 创建监控状态: {symbol}_{positionSide}, 配置: {baseConfig.Name}");
            
            return state;
        }
        
        /// <summary>
        /// 更新监控状态的市场数据
        /// </summary>
        public void UpdateMonitoringStateMarketData(
            ContractMonitoringState state,
            decimal currentQuantity,
            decimal currentEntryPrice,
            decimal currentMarkPrice,
            decimal currentPnl)
        {
            state.CurrentQuantity = currentQuantity;
            state.CurrentEntryPrice = currentEntryPrice;
            state.CurrentMarkPrice = currentMarkPrice;
            state.CurrentUnrealizedPnl = currentPnl;
            state.LastUpdateTime = DateTime.Now;
            
            _logger?.LogDebug($"🔄 更新市场数据: {state.Symbol}_{state.PositionSide}, 浮盈: {currentPnl:F2}");
        }
        
        /// <summary>
        /// 数据迁移：从旧格式转换为新的监控状态格式
        /// </summary>
        public Dictionary<string, ContractMonitoringState> MigrateFromOldFormat(AutoMonitorConfig defaultConfig)
        {
            var result = new Dictionary<string, ContractMonitoringState>();
            
            try
            {
                            // 🔧 已移除：旧文件迁移逻辑 (position_profiles.json 已废弃)
            var oldProfiles = new Dictionary<string, PositionProfile>();
                _logger?.LogInformation($"🔄 开始数据迁移，发现 {oldProfiles.Count} 个旧档案");
                
                foreach (var kvp in oldProfiles)
                {
                    var oldProfile = kvp.Value;
                    var key = $"{oldProfile.Symbol}_{oldProfile.PositionSide}";
                    
                    // 创建新的监控状态
                    var newState = new ContractMonitoringState
                    {
                        ArchiveId = oldProfile.ArchiveId,
                        Symbol = oldProfile.Symbol,
                        PositionSide = oldProfile.PositionSide,
                        BaseConfigName = defaultConfig.Name,
                        InitialQuantity = oldProfile.InitialQuantity,
                        InitialEntryPrice = oldProfile.InitialEntryPrice,
                        CurrentQuantity = oldProfile.InitialQuantity,
                        CurrentEntryPrice = oldProfile.InitialEntryPrice,
                        CurrentMarkPrice = 0, // 需要从市场数据更新
                        CurrentUnrealizedPnl = 0, // 需要从市场数据更新
                        CreateTime = oldProfile.CreateTime,
                        LastUpdateTime = oldProfile.LastUpdateTime,
                        IsActive = oldProfile.IsActive,
                        ExecutionHistories = oldProfile.ExecutionHistories ?? new List<ExecutionHistory>()
                    };
                    
                    // 从默认配置复制配置结构
                    var tempState = CreateMonitoringStateFromBaseConfig(
                        oldProfile.Symbol, 
                        oldProfile.PositionSide, 
                        oldProfile.InitialQuantity, 
                        oldProfile.InitialEntryPrice, 
                        0, 0, 
                        defaultConfig);
                    
                    newState.BreakEvenConfig = tempState.BreakEvenConfig;
                    newState.AddPositionConfig = tempState.AddPositionConfig;
                    newState.ProfitProtectionConfig = tempState.ProfitProtectionConfig;
                    
                    // 迁移旧的触发记录到新状态
                    if (oldProfile.TriggerRecords != null)
                    {
                        foreach (var triggerRecord in oldProfile.TriggerRecords)
                        {
                            var record = triggerRecord.Value;
                            
                            if (record.TriggerType.Contains("保本") && newState.BreakEvenConfig != null)
                            {
                                newState.BreakEvenConfig.IsExecuted = record.IsExecuted;
                                newState.BreakEvenConfig.ExecutionTime = record.TriggerTime;
                                newState.BreakEvenConfig.ExecutionPnl = record.TriggerPnl;
                                newState.BreakEvenConfig.ExecutionResult = record.ExecutionResult;
                                newState.BreakEvenConfig.ExecutionState = record.IsExecuted ? ExecutionState.Executed : ExecutionState.NotTriggered;
                            }
                            else if (record.TriggerType.Contains("推仓") && record.TierIndex.HasValue)
                            {
                                var tier = newState.AddPositionConfig?.Tiers?.FirstOrDefault(t => t.TierIndex == record.TierIndex.Value);
                                if (tier != null)
                                {
                                    tier.ExecutionState = record.IsExecuted ? ExecutionState.Executed : ExecutionState.NotTriggered;
                                    tier.ExecutionTime = record.TriggerTime;
                                    tier.ExecutionPnl = record.TriggerPnl;
                                    tier.ExecutionResult = record.ExecutionResult;
                                }
                            }
                            else if (record.TriggerType.Contains("保盈") && record.TierIndex.HasValue)
                            {
                                var tier = newState.ProfitProtectionConfig?.Tiers?.FirstOrDefault(t => t.TierIndex == record.TierIndex.Value);
                                if (tier != null)
                                {
                                    tier.ExecutionState = record.IsExecuted ? ExecutionState.Executed : ExecutionState.NotTriggered;
                                    tier.ExecutionTime = record.TriggerTime;
                                    tier.ExecutionPnl = record.TriggerPnl;
                                    tier.ExecutionResult = record.ExecutionResult;
                                }
                            }
                        }
                    }
                    
                    result[key] = newState;
                    _logger?.LogDebug($"   ✅ 迁移档案: {key}");
                }
                
                // 2. 保存迁移后的数据到新格式
                if (result.Any())
                {
                    SaveContractMonitoringStates(result);
                    _logger?.LogInformation($"✅ 数据迁移完成，已转换 {result.Count} 个监控状态");
                    
                    // 🔧 已移除：旧文件备份逻辑 (position_profiles.json 已废弃)
                    _logger?.LogInformation("🔧 跳过旧文件备份，使用新的统一文件系统");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 数据迁移失败");
                return result;
            }
        }
        
        #endregion
        
        #region 配置持久化
        
        /// <summary>
        /// 保存所有账户的自动盯盘配置
        /// </summary>
        public void SaveAccountConfigs(Dictionary<string, AutoMonitorConfig> accountConfigs)
        {
            try
            {
                if (accountConfigs == null || !accountConfigs.Any())
                {
                    _logger?.LogDebug("💡 没有配置需要保存");
                    return;
                }
                
                var configData = new
                {
                    SaveTime = DateTime.Now,
                    AccountConfigs = accountConfigs
                };
                
                var json = JsonSerializer.Serialize(configData, _jsonOptions);
                File.WriteAllText(GetConfigsPath(), json);
                
                _logger?.LogInformation($"💾 已保存账户配置: {accountConfigs.Count} 个账户");
                
                foreach (var kvp in accountConfigs)
                {
                    _logger?.LogDebug($"   📝 账户: {kvp.Key}, 配置: {kvp.Value.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 保存账户配置失败");
            }
        }
        
        /// <summary>
        /// 加载所有账户的自动盯盘配置
        /// </summary>
        public Dictionary<string, AutoMonitorConfig> LoadAccountConfigs()
        {
            try
            {
                var configsPath = GetConfigsPath();
                if (!File.Exists(configsPath))
                {
                    _logger?.LogDebug("💡 基础配置文件不存在，返回空配置 (全局配置)");
                    return new Dictionary<string, AutoMonitorConfig>();
                }
                
                var json = File.ReadAllText(configsPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger?.LogDebug("💡 配置文件为空，返回空配置");
                    return new Dictionary<string, AutoMonitorConfig>();
                }
                
                var configData = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
                var accountConfigs = new Dictionary<string, AutoMonitorConfig>();
                
                if (configData.TryGetProperty("accountConfigs", out var accountConfigsElement))
                {
                    foreach (var property in accountConfigsElement.EnumerateObject())
                    {
                        try
                        {
                            var config = JsonSerializer.Deserialize<AutoMonitorConfig>(property.Value.GetRawText(), _jsonOptions);
                            if (config != null)
                            {
                                accountConfigs[property.Name] = config;
                            }
                        }
                        catch (Exception deserializeEx)
                        {
                            _logger?.LogWarning(deserializeEx, $"⚠️ 反序列化账户 '{property.Name}' 配置失败，跳过该配置");
                        }
                    }
                }
                
                _logger?.LogInformation($"📖 已加载账户配置: {accountConfigs.Count} 个账户");
                
                return accountConfigs;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 加载账户配置失败");
                return new Dictionary<string, AutoMonitorConfig>();
            }
        }
        
        /// <summary>
        /// 保存单个账户的配置
        /// </summary>
        public void SaveSingleAccountConfig(string accountName, AutoMonitorConfig config)
        {
            try
            {
                // 加载现有配置
                var allConfigs = LoadAccountConfigs();
                
                // 更新或添加配置
                allConfigs[accountName] = config;
                
                // 保存所有配置
                SaveAccountConfigs(allConfigs);
                
                _logger?.LogInformation($"💾 已保存账户 '{accountName}' 的配置: {config.Name}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ 保存账户 '{accountName}' 配置失败");
            }
        }
        
        /// <summary>
        /// 获取特定账户的配置
        /// </summary>
        public AutoMonitorConfig? GetAccountConfig(string accountName)
        {
            try
            {
                var allConfigs = LoadAccountConfigs();
                return allConfigs.TryGetValue(accountName, out var config) ? config : null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ 获取账户 '{accountName}' 配置失败");
                return null;
            }
        }
        
        #endregion
        
        #region 数据清理
        
        /// <summary>
        /// 清理过期数据
        /// </summary>
        public void CleanupExpiredData()
        {
            try
            {
                // 清理过期的持仓档案（超过24小时的非活跃档案）
                var profiles = LoadPositionProfiles();
                var cutoffTime = DateTime.Now.AddHours(-24);
                var activeProfiles = profiles
                    .Where(kvp => kvp.Value.IsActive || kvp.Value.LastUpdateTime > cutoffTime)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                if (activeProfiles.Count != profiles.Count)
                {
                    SavePositionProfiles(activeProfiles);
                    _logger?.LogInformation($"🧹 清理过期持仓档案: {profiles.Count - activeProfiles.Count} 个");
                }
                
                // 清理过期的执行历史（超过7天）
                var history = LoadExecutionHistory();
                var historyCutoffTime = DateTime.Now.AddDays(-7);
                var recentHistory = history
                    .Where(h => h.ExecutionTime > historyCutoffTime)
                    .ToList();
                
                if (recentHistory.Count != history.Count)
                {
                    SaveExecutionHistory(recentHistory);
                    _logger?.LogInformation($"🧹 清理过期执行历史: {history.Count - recentHistory.Count} 条");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 清理过期数据失败");
            }
        }
        
        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void ClearAllData()
        {
            try
            {
                var filesToDelete = new[] { 
                    GetExecutionHistoryPath(), 
                    GetConfigsPath(), 
                    GetContractMonitoringStatePath() 
                    // 🔧 已移除：_positionProfilesPath (已废弃)
                };
                
                foreach (var file in filesToDelete)
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        _logger?.LogInformation($"🗑️ 已删除文件: {Path.GetFileName(file)}");
                    }
                }
                
                _logger?.LogInformation("🗑️ 所有持久化数据已清空");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 清空数据失败");
            }
        }
        
        /// <summary>
        /// 获取数据目录路径 (当前账号)
        /// </summary>
        public string GetDataDirectoryPath() => _filePathManager.GetAccountDirectory(_currentAccountName);
        
        /// <summary>
        /// 获取数据统计信息
        /// </summary>
        public DataStatistics GetDataStatistics()
        {
            try
            {
                var stats = new DataStatistics();
                
                // 🔧 已移除：position_profiles.json 统计信息 (文件已废弃)
                stats.PositionProfilesFileSize = 0;
                stats.PositionProfilesLastModified = DateTime.MinValue;
                stats.PositionProfilesCount = 0;
                
                var executionHistoryPath = GetExecutionHistoryPath();
                if (File.Exists(executionHistoryPath))
                {
                    var fileInfo = new FileInfo(executionHistoryPath);
                    stats.ExecutionHistoryFileSize = fileInfo.Length;
                    stats.ExecutionHistoryLastModified = fileInfo.LastWriteTime;
                    
                    var history = LoadExecutionHistory();
                    stats.ExecutionHistoryCount = history.Count;
                }
                
                var configsPath = GetConfigsPath();
                if (File.Exists(configsPath))
                {
                    var fileInfo = new FileInfo(configsPath);
                    stats.ConfigsFileSize = fileInfo.Length;
                    stats.ConfigsLastModified = fileInfo.LastWriteTime;
                    
                    var configs = LoadAccountConfigs();
                    stats.AccountConfigsCount = configs.Count;
                }
                
                return stats;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 获取数据统计失败");
                return new DataStatistics();
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 数据统计信息
    /// </summary>
    public class DataStatistics
    {
        public int PositionProfilesCount { get; set; }
        public long PositionProfilesFileSize { get; set; }
        public DateTime PositionProfilesLastModified { get; set; }
        
        public int ExecutionHistoryCount { get; set; }
        public long ExecutionHistoryFileSize { get; set; }
        public DateTime ExecutionHistoryLastModified { get; set; }
        
        public int AccountConfigsCount { get; set; }
        public long ConfigsFileSize { get; set; }
        public DateTime ConfigsLastModified { get; set; }
        
        public override string ToString()
        {
            return $"持仓档案: {PositionProfilesCount}个 ({PositionProfilesFileSize}字节), " +
                   $"执行历史: {ExecutionHistoryCount}条 ({ExecutionHistoryFileSize}字节), " +
                   $"账户配置: {AccountConfigsCount}个 ({ConfigsFileSize}字节)";
        }
    }
} 