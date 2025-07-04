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
    /// </summary>
    public class AutoMonitorPersistenceService
    {
        private readonly string _dataPath;
        private readonly string _positionProfilesPath;
        private readonly string _executionHistoryPath;
        private readonly string _contractConfigsPath;
        private readonly ILogger<AutoMonitorPersistenceService>? _logger;
        
        public AutoMonitorPersistenceService(ILogger<AutoMonitorPersistenceService>? logger = null)
        {
            _logger = logger;
            
            // 创建数据目录
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BinanceFuturesTrader",
                "AutoMonitor");
            
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            _dataPath = appDataPath;
            _positionProfilesPath = Path.Combine(appDataPath, "position_profiles.json");
            _executionHistoryPath = Path.Combine(appDataPath, "execution_history.json");
            _contractConfigsPath = Path.Combine(appDataPath, "contract_configs.json");
            
            _logger?.LogDebug($"📁 自动盯盘数据目录: {_dataPath}");
        }
        
        /// <summary>
        /// 保存持仓档案状态
        /// </summary>
        public void SavePositionProfiles(Dictionary<string, PositionProfile> profiles)
        {
            try
            {
                if (profiles == null || !profiles.Any())
                {
                    _logger?.LogDebug("💡 没有持仓档案需要保存");
                    return;
                }
                
                // 只保存活跃的持仓档案
                var activeProfiles = profiles
                    .Where(kvp => kvp.Value.IsActive)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                var json = JsonSerializer.Serialize(activeProfiles, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                File.WriteAllText(_positionProfilesPath, json);
                
                _logger?.LogInformation($"💾 已保存持仓档案: {activeProfiles.Count} 个");
                foreach (var profile in activeProfiles.Values)
                {
                    _logger?.LogDebug($"   📝 {profile.Symbol}_{profile.PositionSide} - 触发记录: {profile.TriggerRecords.Count}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 保存持仓档案失败");
            }
        }
        
        /// <summary>
        /// 加载持仓档案状态
        /// </summary>
        public Dictionary<string, PositionProfile> LoadPositionProfiles()
        {
            try
            {
                if (!File.Exists(_positionProfilesPath))
                {
                    _logger?.LogDebug("💡 持仓档案文件不存在，返回空字典");
                    return new Dictionary<string, PositionProfile>();
                }
                
                var json = File.ReadAllText(_positionProfilesPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger?.LogDebug("💡 持仓档案文件为空，返回空字典");
                    return new Dictionary<string, PositionProfile>();
                }
                
                var profiles = JsonSerializer.Deserialize<Dictionary<string, PositionProfile>>(json, 
                    new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                    }) ?? new Dictionary<string, PositionProfile>();
                
                // 清理过期的档案（超过24小时的非活跃档案）
                var cutoffTime = DateTime.Now.AddHours(-24);
                var validProfiles = profiles
                    .Where(kvp => kvp.Value.IsActive || kvp.Value.LastUpdateTime > cutoffTime)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                _logger?.LogInformation($"📖 已加载持仓档案: {validProfiles.Count} 个");
                foreach (var profile in validProfiles.Values)
                {
                    _logger?.LogDebug($"   📝 {profile.Symbol}_{profile.PositionSide} - 触发记录: {profile.TriggerRecords.Count}");
                }
                
                return validProfiles;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 加载持仓档案失败");
                return new Dictionary<string, PositionProfile>();
            }
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
        /// </summary>
        public void ClearAllData()
        {
            try
            {
                if (File.Exists(_positionProfilesPath))
                {
                    File.Delete(_positionProfilesPath);
                    _logger?.LogInformation("🗑️ 已清空持仓档案数据");
                }
                
                if (File.Exists(_executionHistoryPath))
                {
                    File.Delete(_executionHistoryPath);
                    _logger?.LogInformation("🗑️ 已清空执行历史数据");
                }
                
                if (File.Exists(_contractConfigsPath))
                {
                    File.Delete(_contractConfigsPath);
                    _logger?.LogInformation("🗑️ 已清空合约配置数据");
                }
                
                _logger?.LogInformation("✅ 所有持久化数据已清理完成");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 清空数据失败");
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
        /// </summary>
        public void SaveContractConfigs(List<ContractMonitorModel> contracts)
        {
            try
            {
                if (contracts == null || !contracts.Any())
                {
                    _logger?.LogDebug("💡 没有合约配置需要保存");
                    return;
                }

                // 创建序列化友好的数据结构
                var configData = contracts.Select(contract => new
                {
                    contract.Symbol,
                    contract.PositionSide,
                    contract.IsEnabled,
                    contract.IsActive,
                    contract.CurrentPrice,
                    contract.PositionSize,
                    contract.UnrealizedPnl,
                    TriggerConditions = contract.TriggerConditions.Select(tc => new
                    {
                        tc.Id,
                        tc.Type,
                        tc.TierIndex,
                        tc.Description,
                        tc.TriggerPrice,
                        tc.KeepValue,
                        tc.Status,
                        tc.LastExecutionTime,
                        tc.StatusNote
                    }).ToList()
                }).ToList();

                var json = JsonSerializer.Serialize(configData, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(_contractConfigsPath, json);

                _logger?.LogInformation($"💾 已保存合约配置: {contracts.Count} 个合约");
                foreach (var contract in contracts)
                {
                    _logger?.LogDebug($"   📝 {contract.ContractKey} - {contract.TriggerConditions.Count} 个触发条件");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 保存合约配置失败");
            }
        }

        /// <summary>
        /// 从文件加载合约配置
        /// </summary>
        public List<ContractMonitorModel> LoadContractConfigs()
        {
            try
            {
                if (!File.Exists(_contractConfigsPath))
                {
                    _logger?.LogDebug("💡 合约配置文件不存在，返回空列表");
                    return new List<ContractMonitorModel>();
                }

                var json = File.ReadAllText(_contractConfigsPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger?.LogDebug("💡 合约配置文件为空，返回空列表");
                    return new List<ContractMonitorModel>();
                }

                // 反序列化为动态对象
                var configData = JsonSerializer.Deserialize<JsonElement[]>(json, 
                    new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                    });

                var contracts = new List<ContractMonitorModel>();

                foreach (var item in configData)
                {
                    var contract = new ContractMonitorModel
                    {
                        Symbol = item.GetProperty("symbol").GetString() ?? "",
                        PositionSide = item.GetProperty("positionSide").GetString() ?? "",
                        IsEnabled = item.GetProperty("isEnabled").GetBoolean(),
                        IsActive = item.GetProperty("isActive").GetBoolean(),
                        CurrentPrice = item.GetProperty("currentPrice").GetDecimal(),
                        PositionSize = item.GetProperty("positionSize").GetDecimal(),
                        UnrealizedPnl = item.GetProperty("unrealizedPnl").GetDecimal()
                    };

                    // 加载触发条件
                    if (item.TryGetProperty("triggerConditions", out var conditionsElement))
                    {
                        foreach (var conditionItem in conditionsElement.EnumerateArray())
                        {
                            var condition = new TriggerConditionModel
                            {
                                Id = conditionItem.GetProperty("id").GetInt32(),
                                Type = (TriggerConditionType)conditionItem.GetProperty("type").GetInt32(),
                                TierIndex = conditionItem.TryGetProperty("tierIndex", out var tierElement) && !tierElement.ValueKind.Equals(JsonValueKind.Null) 
                                    ? tierElement.GetInt32() : null,
                                Description = conditionItem.GetProperty("description").GetString() ?? "",
                                TriggerPrice = conditionItem.GetProperty("triggerPrice").GetDecimal(),
                                KeepValue = conditionItem.GetProperty("keepValue").GetDecimal(),
                                Status = (TriggerExecutionStatus)conditionItem.GetProperty("status").GetInt32(),
                                LastExecutionTime = conditionItem.TryGetProperty("lastExecutionTime", out var timeElement) && !timeElement.ValueKind.Equals(JsonValueKind.Null)
                                    ? timeElement.GetDateTime() : null,
                                StatusNote = conditionItem.GetProperty("statusNote").GetString() ?? ""
                            };

                            contract.TriggerConditions.Add(condition);
                        }
                    }

                    contracts.Add(contract);
                }

                _logger?.LogInformation($"📖 已加载合约配置: {contracts.Count} 个合约");
                foreach (var contract in contracts)
                {
                    _logger?.LogDebug($"   📝 {contract.ContractKey} - {contract.TriggerConditions.Count} 个触发条件");
                }

                return contracts;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 加载合约配置失败");
                return new List<ContractMonitorModel>();
            }
        }

        /// <summary>
        /// 清理合约配置文件
        /// </summary>
        public void ClearContractConfigs()
        {
            try
            {
                if (File.Exists(_contractConfigsPath))
                {
                    File.Delete(_contractConfigsPath);
                    _logger?.LogInformation("🗑️ 合约配置文件已清理");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 清理合约配置文件失败");
            }
        }
    }
} 