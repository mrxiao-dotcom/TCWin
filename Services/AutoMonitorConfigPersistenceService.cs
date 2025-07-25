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
    /// 自动盯盘配置持久化服务
    /// 专门负责保存和加载基础配置(AutoMonitorConfig)到文件
    /// </summary>
    public class AutoMonitorConfigPersistenceService
    {
        private readonly string _configFilePath;
        private readonly FilePathManager _filePathManager;
        private readonly ILogger<AutoMonitorConfigPersistenceService>? _logger;
        
        public AutoMonitorConfigPersistenceService(
            ILogger<AutoMonitorConfigPersistenceService>? logger = null,
            FilePathManager? filePathManager = null)
        {
            _logger = logger;
            _filePathManager = filePathManager ?? new FilePathManager();
            
            // 🔧 修复：使用统一路径管理，配置文件放在Global目录下
            _configFilePath = _filePathManager.GetBaseConfigsFilePath();
            
            _logger?.LogDebug($"📁 自动盯盘配置文件路径 (Global): {_configFilePath}");
        }
        
        /// <summary>
        /// 保存所有账户的自动盯盘配置
        /// </summary>
        /// <param name="accountConfigs">账户配置字典</param>
        public void SaveAccountConfigs(Dictionary<string, AutoMonitorConfig> accountConfigs)
        {
            try
            {
                if (accountConfigs == null || !accountConfigs.Any())
                {
                    _logger?.LogDebug("💡 没有账户配置需要保存");
                    return;
                }
                
                // 创建序列化友好的数据结构
                var configData = new
                {
                    SaveTime = DateTime.Now,
                    Version = "1.0",
                    AccountConfigs = accountConfigs.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            kvp.Value.Name,
                            kvp.Value.IsEnabled,
                            kvp.Value.ScanIntervalSeconds,
                            kvp.Value.CooldownSeconds,
                            kvp.Value.CreateTime,
                            kvp.Value.LastModifiedTime,
                            BreakEvenConfig = new
                            {
                                kvp.Value.BreakEvenConfig.IsEnabled,
                                kvp.Value.BreakEvenConfig.TriggerProfitAmount
                            },
                            AddPositionConfig = new
                            {
                                kvp.Value.AddPositionConfig.IsEnabled,
                                Tiers = kvp.Value.AddPositionConfig.Tiers.Select(t => new
                                {
                                    t.TierIndex,
                                    t.TriggerProfitAmount,
                                    t.RiskMultiplier,
                                    t.StopLossRatio,
                                    t.IsTriggered
                                }).ToList()
                            },
                            ProfitProtectionConfig = new
                            {
                                kvp.Value.ProfitProtectionConfig.IsEnabled,
                                Tiers = kvp.Value.ProfitProtectionConfig.Tiers.Select(t => new
                                {
                                    t.TierIndex,
                                    t.TriggerProfitAmount,
                                    t.ProtectionAmount,
                                    t.IsTriggered
                                }).ToList()
                            }
                        }
                    )
                };
                
                var json = JsonSerializer.Serialize(configData, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                File.WriteAllText(_configFilePath, json);
                
                _logger?.LogInformation($"💾 已保存账户配置: {accountConfigs.Count} 个账户");
                foreach (var kvp in accountConfigs)
                {
                    _logger?.LogDebug($"   📝 账户: {kvp.Key} - 配置: {kvp.Value.Name}");
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
        /// <returns>账户配置字典</returns>
        public Dictionary<string, AutoMonitorConfig> LoadAccountConfigs()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    _logger?.LogDebug("💡 配置文件不存在，返回空配置");
                    return new Dictionary<string, AutoMonitorConfig>();
                }
                
                var json = File.ReadAllText(_configFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger?.LogDebug("💡 配置文件为空，返回空配置");
                    return new Dictionary<string, AutoMonitorConfig>();
                }
                
                // 反序列化
                var configData = JsonSerializer.Deserialize<JsonElement>(json, 
                    new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                    });
                
                var accountConfigs = new Dictionary<string, AutoMonitorConfig>();
                
                if (configData.TryGetProperty("accountConfigs", out var accountConfigsElement))
                {
                    foreach (var accountProperty in accountConfigsElement.EnumerateObject())
                    {
                        var accountName = accountProperty.Name;
                        var configElement = accountProperty.Value;
                        
                        var config = new AutoMonitorConfig
                        {
                            Name = configElement.GetProperty("name").GetString() ?? "默认配置",
                            IsEnabled = configElement.GetProperty("isEnabled").GetBoolean(),
                            ScanIntervalSeconds = configElement.GetProperty("scanIntervalSeconds").GetInt32(),
                            CooldownSeconds = configElement.GetProperty("cooldownSeconds").GetInt32(),
                            CreateTime = configElement.GetProperty("createTime").GetDateTime(),
                            LastModifiedTime = configElement.GetProperty("lastModifiedTime").GetDateTime()
                        };
                        
                        // 加载保本配置
                        if (configElement.TryGetProperty("breakEvenConfig", out var breakEvenElement))
                        {
                            config.BreakEvenConfig.IsEnabled = breakEvenElement.GetProperty("isEnabled").GetBoolean();
                            config.BreakEvenConfig.TriggerProfitAmount = breakEvenElement.GetProperty("triggerProfitAmount").GetDecimal();
                        }
                        
                        // 加载推仓配置
                        if (configElement.TryGetProperty("addPositionConfig", out var addPositionElement))
                        {
                            config.AddPositionConfig.IsEnabled = addPositionElement.GetProperty("isEnabled").GetBoolean();
                            
                            if (addPositionElement.TryGetProperty("tiers", out var tiersElement))
                            {
                                config.AddPositionConfig.Tiers.Clear();
                                foreach (var tierElement in tiersElement.EnumerateArray())
                                {
                                    var tier = new AddPositionTier
                                    {
                                        TierIndex = tierElement.GetProperty("tierIndex").GetInt32(),
                                        TriggerProfitAmount = tierElement.GetProperty("triggerProfitAmount").GetDecimal(),
                                        RiskMultiplier = tierElement.GetProperty("riskMultiplier").GetDecimal(),
                                        StopLossRatio = tierElement.GetProperty("stopLossRatio").GetDecimal(),
                                        IsTriggered = tierElement.GetProperty("isTriggered").GetBoolean()
                                    };
                                    config.AddPositionConfig.Tiers.Add(tier);
                                }
                            }
                        }
                        
                        // 加载止盈保护配置
                        if (configElement.TryGetProperty("profitProtectionConfig", out var profitProtectionElement))
                        {
                            config.ProfitProtectionConfig.IsEnabled = profitProtectionElement.GetProperty("isEnabled").GetBoolean();
                            
                            if (profitProtectionElement.TryGetProperty("tiers", out var tiersElement))
                            {
                                config.ProfitProtectionConfig.Tiers.Clear();
                                foreach (var tierElement in tiersElement.EnumerateArray())
                                {
                                    var tier = new ProfitProtectionTier
                                    {
                                        TierIndex = tierElement.GetProperty("tierIndex").GetInt32(),
                                        TriggerProfitAmount = tierElement.GetProperty("triggerProfitAmount").GetDecimal(),
                                        ProtectionAmount = tierElement.GetProperty("protectionAmount").GetDecimal(),
                                        IsTriggered = tierElement.GetProperty("isTriggered").GetBoolean()
                                    };
                                    config.ProfitProtectionConfig.Tiers.Add(tier);
                                }
                            }
                        }
                        
                        accountConfigs[accountName] = config;
                    }
                }
                
                _logger?.LogInformation($"📖 已加载账户配置: {accountConfigs.Count} 个账户");
                foreach (var kvp in accountConfigs)
                {
                    _logger?.LogDebug($"   📝 账户: {kvp.Key} - 配置: {kvp.Value.Name}");
                }
                
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
        /// <param name="accountName">账户名称</param>
        /// <param name="config">配置对象</param>
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
        /// <param name="accountName">账户名称</param>
        /// <returns>配置对象，如果不存在则返回null</returns>
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
        
        /// <summary>
        /// 清除所有配置
        /// </summary>
        public void ClearAllConfigs()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    File.Delete(_configFilePath);
                    _logger?.LogInformation("🗑️ 已清除所有配置");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 清除配置失败");
            }
        }
        
        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        /// <returns>配置文件完整路径</returns>
        public string GetConfigFilePath() => _configFilePath;
    }
} 