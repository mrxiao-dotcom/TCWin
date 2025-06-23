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
    /// 自动盯盘状态持久化服务
    /// 负责保存和恢复自动盯盘的执行状态，避免重复执行
    /// </summary>
    public class AutoMonitorPersistenceService
    {
        private readonly string _dataPath;
        private readonly string _positionProfilesPath;
        private readonly string _executionHistoryPath;
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
    }
} 