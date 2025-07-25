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
    /// 自动盯盘状态持久化服务
    /// 负责保存和恢复自动盯盘的执行状态，避免重复执行
    /// 🔧 已更新：使用新的统一文件系统，移除对旧文件的引用
    /// </summary>
    public class AutoMonitorPersistenceService
    {
        private readonly string _dataPath;
        private readonly string _executionHistoryPath;
        private readonly string _contractConfigsPath; // 🔧 保留用于兼容性，但相关方法已废弃
        private readonly FilePathManager _filePathManager;
        private readonly ILogger<AutoMonitorPersistenceService>? _logger;
        
        public AutoMonitorPersistenceService(
            ILogger<AutoMonitorPersistenceService>? logger = null,
            FilePathManager? filePathManager = null,
            string? accountName = null)
        {
            _logger = logger;
            _filePathManager = filePathManager ?? new FilePathManager();
            
            // 🔧 修复：使用统一路径管理，指向默认账号目录
            var currentAccount = accountName ?? _filePathManager.GetCurrentAccountName();
            _dataPath = _filePathManager.GetAccountDirectory(currentAccount);
            _executionHistoryPath = _filePathManager.GetExecutionHistoryFilePath(currentAccount);
            _contractConfigsPath = Path.Combine(_dataPath, "contract_configs.json"); // 🔧 保留用于兼容性
            
            // 🔧 已移除：_positionProfilesPath (已废弃，使用contract_monitoring_states.json)
            
            _logger?.LogDebug($"📁 自动盯盘数据目录 (账号: {currentAccount}): {_dataPath}");
        }
        
        /// <summary>
        /// 保存持仓档案状态
        /// ⚠️ 已废弃：此方法已不再使用，数据现在保存在 contract_monitoring_states.json 中
        /// 请使用 ContractMonitoringStateService 或 UnifiedPersistenceService
        /// </summary>
        [Obsolete("已废弃：请使用 ContractMonitoringStateService 替代，数据现在统一保存在 contract_monitoring_states.json")]
        public void SavePositionProfiles(Dictionary<string, PositionProfile> profiles)
        {
            _logger?.LogWarning("⚠️ SavePositionProfiles 已废弃：请使用 ContractMonitoringStateService 替代");
            // 不再执行任何操作，数据现在通过 contract_monitoring_states.json 管理
        }
        
        /// <summary>
        /// 加载持仓档案状态
        /// ⚠️ 已废弃：此方法已不再使用，数据现在从 contract_monitoring_states.json 中读取
        /// 请使用 ContractMonitoringStateService 或 UnifiedPersistenceService
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
                
                var json = JsonSerializer.Serialize(recentHistory, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                File.WriteAllText(_executionHistoryPath, json);
                
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
                if (!File.Exists(_executionHistoryPath))
                {
                    _logger?.LogDebug("💡 执行历史文件不存在，返回空列表");
                    return new List<ExecutionHistory>();
                }
                
                var json = File.ReadAllText(_executionHistoryPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger?.LogDebug("💡 执行历史文件为空，返回空列表");
                    return new List<ExecutionHistory>();
                }
                
                var history = JsonSerializer.Deserialize<List<ExecutionHistory>>(json, 
                    new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                    }) ?? new List<ExecutionHistory>();
                
                _logger?.LogInformation($"📖 已加载执行历史: {history.Count} 条");
                
                return history.OrderByDescending(h => h.ExecutionTime).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 加载执行历史失败");
                return new List<ExecutionHistory>();
            }
        }
        
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
        /// ⚠️ 已部分废弃：现在只清理执行历史，其他数据请使用 ContractMonitoringStateService 管理
        /// </summary>
        [Obsolete("部分废弃：持仓档案和合约配置现在统一在 contract_monitoring_states.json 中管理")]
        public void ClearAllData()
        {
            try
            {
                // 🔧 已移除：_positionProfilesPath 清理 (文件已废弃)
                _logger?.LogInformation("🔧 跳过持仓档案数据清理 (已迁移到新系统)");
                
                if (File.Exists(_executionHistoryPath))
                {
                    File.Delete(_executionHistoryPath);
                    _logger?.LogInformation("🗑️ 已清空执行历史数据");
                }
                
                // 🔧 已移除：_contractConfigsPath 清理 (文件已废弃)
                _logger?.LogInformation("🔧 跳过合约配置数据清理 (已迁移到新系统)");
                
                _logger?.LogInformation("✅ 兼容清理完成，其他数据请使用 ContractMonitoringStateService 管理");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 清空数据失败");
            }
        }

        /// <summary>
        /// 🚨 紧急清理无效档案（如BTC等误添加的合约）
        /// </summary>
        public void EmergencyCleanInvalidProfiles()
        {
            try
            {
                var profiles = LoadPositionProfiles();
                var originalCount = profiles.Count;
                
                // 定义可疑的合约列表
                var suspiciousSymbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "ADAUSDT", "DOTUSDT", "SOLUSDT" };
                
                var invalidKeys = profiles.Where(kvp => 
                    suspiciousSymbols.Contains(kvp.Value.Symbol) ||
                    !kvp.Value.Symbol.EndsWith("USDT") ||
                    !kvp.Value.IsActive ||
                    kvp.Value.LastUpdateTime < DateTime.Now.AddHours(-24)
                ).Select(kvp => kvp.Key).ToList();
                
                if (invalidKeys.Any())
                {
                    _logger?.LogWarning($"🚨 紧急清理: 发现 {invalidKeys.Count} 个可疑档案");
                    
                    foreach (var key in invalidKeys)
                    {
                        if (profiles.TryGetValue(key, out var profile))
                        {
                            _logger?.LogWarning($"   ❌ 清理: {key} (合约: {profile.Symbol}, 最后更新: {profile.LastUpdateTime})");
                            profiles.Remove(key);
                        }
                    }
                    
                    // 保存清理后的档案
                    SavePositionProfiles(profiles);
                    
                    // 清理执行历史
                    var history = LoadExecutionHistory();
                    var validHistory = history.Where(h => 
                        !suspiciousSymbols.Contains(h.Symbol) &&
                        h.Symbol.EndsWith("USDT")
                    ).ToList();
                    
                    if (validHistory.Count != history.Count)
                    {
                        SaveExecutionHistory(validHistory);
                        _logger?.LogInformation($"🧹 同时清理了 {history.Count - validHistory.Count} 条无效执行历史");
                    }
                    
                    _logger?.LogInformation($"✅ 紧急清理完成: 移除 {originalCount - profiles.Count} 个档案，剩余 {profiles.Count} 个有效档案");
                }
                else
                {
                    _logger?.LogInformation("✅ 没有发现可疑档案，持久化数据正常");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 紧急清理失败");
            }
        }
        
        /// <summary>
        /// 获取数据目录路径
        /// </summary>
        public string GetDataPath() => _dataPath;
        
        /// <summary>
        /// 检查特定持仓是否已执行过某个阶梯
        /// </summary>
        public bool HasExecutedStage(string symbol, string positionSide, string triggerType, int stageIndex)
        {
            try
            {
                var profiles = LoadPositionProfiles();
                var key = $"{symbol}_{positionSide}";
                
                if (!profiles.ContainsKey(key))
                {
                    return false;
                }
                
                var profile = profiles[key];
                var triggerKey = $"{triggerType}_Stage{stageIndex}";
                
                return profile.TriggerRecords.ContainsKey(triggerKey) && 
                       profile.TriggerRecords[triggerKey].IsExecuted;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ 检查执行状态失败: {symbol}_{positionSide} {triggerType} Stage{stageIndex}");
                return false;
            }
        }
        
        /// <summary>
        /// 记录阶梯执行状态
        /// </summary>
        public void RecordStageExecution(string symbol, string positionSide, string triggerType, int stageIndex, decimal triggerPnl, bool success)
        {
            try
            {
                var profiles = LoadPositionProfiles();
                var key = $"{symbol}_{positionSide}";
                
                if (!profiles.ContainsKey(key))
                {
                    profiles[key] = new PositionProfile
                    {
                        Symbol = symbol,
                        PositionSide = positionSide,
                        CreateTime = DateTime.Now,
                        LastUpdateTime = DateTime.Now,
                        IsActive = true
                    };
                }
                
                var profile = profiles[key];
                var triggerKey = $"{triggerType}_Stage{stageIndex}";
                
                profile.TriggerRecords[triggerKey] = new TriggerRecord
                {
                    ArchiveId = profile.ArchiveId,
                    TriggerType = triggerType,
                    TierIndex = stageIndex,
                    TriggerPnl = triggerPnl,
                    TriggerTime = DateTime.Now,
                    IsExecuted = true,
                    ExecutionResult = success ? "成功" : "失败"
                };
                
                profile.LastUpdateTime = DateTime.Now;
                
                SavePositionProfiles(profiles);
                
                _logger?.LogInformation($"📝 记录执行状态: {key} {triggerType} Stage{stageIndex} - {(success ? "成功" : "失败")}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ 记录执行状态失败: {symbol}_{positionSide} {triggerType} Stage{stageIndex}");
            }
        }
        
        /// <summary>
        /// 清理特定合约的历史状态
        /// </summary>
        public void CleanupContractHistory(string symbol, string positionSide, string reason = "重新开仓")
        {
            try
            {
                var contractKey = $"{symbol}_{positionSide}";
                
                // 清理持仓档案
                var profiles = LoadPositionProfiles();
                var profileRemoved = false;
                
                if (profiles.ContainsKey(contractKey))
                {
                    var triggerCount = profiles[contractKey].TriggerRecords.Count;
                    profiles.Remove(contractKey);
                    profileRemoved = true;
                    
                    SavePositionProfiles(profiles);
                    _logger?.LogInformation($"🗑️ 清理合约档案: {contractKey} - 清理{triggerCount}个触发记录 (原因: {reason})");
                }
                
                // 清理执行历史
                var history = LoadExecutionHistory();
                var initialCount = history.Count;
                
                // 保留状态清理相关的记录，移除交易执行记录
                var filteredHistory = history.Where(h => !(h.Symbol == symbol && h.PositionSide == positionSide && 
                    !h.ExecutionType.Contains("清理"))).ToList();
                
                if (filteredHistory.Count != initialCount)
                {
                    var removedCount = initialCount - filteredHistory.Count;
                    
                    // 添加清理记录
                    filteredHistory.Add(new ExecutionHistory
                    {
                        Symbol = symbol,
                        PositionSide = positionSide,
                        ExecutionType = $"手动清理-{reason}",
                        ExecutionTime = DateTime.Now,
                        TriggerPnl = 0,
                        IsSuccess = true,
                        Details = $"手动清理历史状态，移除{removedCount}条执行记录"
                    });
                    
                    SaveExecutionHistory(filteredHistory);
                    _logger?.LogInformation($"🗑️ 清理执行历史: {contractKey} - 移除{removedCount}条记录 (原因: {reason})");
                }
                
                if (profileRemoved || filteredHistory.Count != initialCount)
                {
                    _logger?.LogInformation($"✅ 合约 {contractKey} 历史状态清理完成 (原因: {reason})");
                }
                else
                {
                    _logger?.LogInformation($"ℹ️ 合约 {contractKey} 无需清理，未发现历史状态");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ 清理合约历史状态失败: {symbol}_{positionSide}");
            }
        }
        
        /// <summary>
        /// 批量清理多个合约的历史状态
        /// </summary>
        public void BatchCleanupContractHistory(List<(string symbol, string positionSide)> contracts, string reason = "批量清理")
        {
            try
            {
                if (!contracts.Any())
                {
                    _logger?.LogInformation("ℹ️ 批量清理: 没有需要清理的合约");
                    return;
                }
                
                _logger?.LogInformation($"🧹 开始批量清理 {contracts.Count} 个合约的历史状态 (原因: {reason})");
                
                var profiles = LoadPositionProfiles();
                var history = LoadExecutionHistory();
                
                var profilesModified = false;
                var historyModified = false;
                var cleanupResults = new List<string>();
                
                foreach (var (symbol, positionSide) in contracts)
                {
                    var contractKey = $"{symbol}_{positionSide}";
                    
                    // 清理持仓档案
                    if (profiles.ContainsKey(contractKey))
                    {
                        var triggerCount = profiles[contractKey].TriggerRecords.Count;
                        profiles.Remove(contractKey);
                        profilesModified = true;
                        cleanupResults.Add($"  📝 {contractKey}: 清理{triggerCount}个触发记录");
                    }
                    
                    // 计算需要清理的执行历史数量
                    var recordsToRemove = history.Count(h => h.Symbol == symbol && h.PositionSide == positionSide && 
                        !h.ExecutionType.Contains("清理"));
                    
                    if (recordsToRemove > 0)
                    {
                        cleanupResults.Add($"  📊 {contractKey}: 清理{recordsToRemove}条执行记录");
                        historyModified = true;
                    }
                }
                
                // 批量移除执行历史
                if (historyModified)
                {
                    var contractsSet = contracts.ToHashSet();
                    var filteredHistory = history.Where(h => !contractsSet.Contains((h.Symbol, h.PositionSide)) ||
                        h.ExecutionType.Contains("清理")).ToList();
                    
                    // 添加批量清理记录
                    filteredHistory.Add(new ExecutionHistory
                    {
                        Symbol = "BATCH",
                        PositionSide = "ALL",
                        ExecutionType = $"批量清理-{reason}",
                        ExecutionTime = DateTime.Now,
                        TriggerPnl = 0,
                        IsSuccess = true,
                        Details = $"批量清理{contracts.Count}个合约的历史状态"
                    });
                    
                    SaveExecutionHistory(filteredHistory);
                }
                
                // 保存修改后的档案
                if (profilesModified)
                {
                    SavePositionProfiles(profiles);
                }
                
                // 输出清理结果
                if (cleanupResults.Any())
                {
                    _logger?.LogInformation($"✅ 批量清理完成:");
                    foreach (var result in cleanupResults)
                    {
                        _logger?.LogInformation(result);
                    }
                }
                else
                {
                    _logger?.LogInformation("ℹ️ 批量清理: 所有合约都没有历史状态需要清理");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ 批量清理合约历史状态失败");
            }
        }

        /// <summary>
        /// 保存合约配置到文件
        /// ⚠️ 已废弃：此方法已不再使用，数据现在保存在 contract_monitoring_states.json 中
        /// 请使用 ContractMonitoringStateService 或 UnifiedPersistenceService
        /// </summary>
        [Obsolete("已废弃：请使用 ContractMonitoringStateService 替代，数据现在统一保存在 contract_monitoring_states.json")]
        public void SaveContractConfigs(List<ContractMonitorModel> contracts)
        {
            _logger?.LogWarning("⚠️ SaveContractConfigs 已废弃：请使用 ContractMonitoringStateService 替代");
            // 不再执行任何操作，数据现在通过 contract_monitoring_states.json 管理
        }

        /// <summary>
        /// 从文件加载合约配置
        /// ⚠️ 已废弃：此方法已不再使用，数据现在从 contract_monitoring_states.json 中读取
        /// 请使用 ContractMonitoringStateService 或 UnifiedPersistenceService
        /// </summary>
        [Obsolete("已废弃：请使用 ContractMonitoringStateService 替代，数据现在统一从 contract_monitoring_states.json 读取")]
        public List<ContractMonitorModel> LoadContractConfigs()
        {
            _logger?.LogWarning("⚠️ LoadContractConfigs 已废弃：请使用 ContractMonitoringStateService 替代");
            return new List<ContractMonitorModel>();
        }

        /// <summary>
        /// 清理合约配置文件
        /// ⚠️ 已废弃：请使用 ContractMonitoringStateService 替代
        /// </summary>
        [Obsolete("已废弃：请使用 ContractMonitoringStateService 替代，数据现在统一在 contract_monitoring_states.json 中管理")]
        public void ClearContractConfigs()
        {
            _logger?.LogWarning("⚠️ ClearContractConfigs 已废弃：请使用 ContractMonitoringStateService 替代");
            // 不再执行任何操作，数据现在通过 contract_monitoring_states.json 管理
        }

        /// <summary>
        /// 从文件中移除特定合约的配置
        /// ⚠️ 已废弃：请使用 ContractMonitoringStateService 替代
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="positionSide">持仓方向</param>
        [Obsolete("已废弃：请使用 ContractMonitoringStateService 替代，数据现在统一在 contract_monitoring_states.json 中管理")]
        public void RemoveContractConfig(string symbol, string positionSide)
        {
            _logger?.LogWarning($"⚠️ RemoveContractConfig 已废弃：请使用 ContractMonitoringStateService 替代 (合约: {symbol}_{positionSide})");
            // 不再执行任何操作，数据现在通过 contract_monitoring_states.json 管理
        }
        
        /// <summary>
        /// 批量移除已平仓合约的配置
        /// ⚠️ 已废弃：请使用 ContractMonitoringStateService 替代
        /// </summary>
        /// <param name="activePositionKeys">当前活跃持仓的键值列表</param>
        [Obsolete("已废弃：请使用 ContractMonitoringStateService 替代，数据现在统一在 contract_monitoring_states.json 中管理")]
        public void RemoveClosedPositionConfigs(HashSet<string> activePositionKeys)
        {
            _logger?.LogWarning("⚠️ RemoveClosedPositionConfigs 已废弃：请使用 ContractMonitoringStateService 替代");
            // 不再执行任何操作，数据现在通过 contract_monitoring_states.json 管理
        }
    }
} 