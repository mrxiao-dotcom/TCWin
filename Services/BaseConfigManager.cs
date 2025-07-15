using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 基础配置管理器 - 负责自动盯盘配置的CRUD操作和持久化
    /// </summary>
    public class BaseConfigManager
    {
        private readonly ILogger<BaseConfigManager> _logger;
        private readonly string _configFilePath;
        private readonly object _fileLock = new object();
        
        /// <summary>
        /// 配置列表
        /// </summary>
        public ObservableCollection<AutoMonitorConfig> Configurations { get; private set; }
        
        /// <summary>
        /// 当前选中的配置
        /// </summary>
        public AutoMonitorConfig? CurrentConfig { get; private set; }
        
        /// <summary>
        /// 配置变化事件
        /// </summary>
        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
        
        public BaseConfigManager(ILogger<BaseConfigManager> logger)
        {
            _logger = logger;
            // 🔧 修复：统一配置文件路径到用户数据目录，与SimpleConfigEditorWindow保持一致
            _configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                          "BinanceFuturesTrader", "AutoMonitorConfigs.json");
            Configurations = new ObservableCollection<AutoMonitorConfig>();
            
            // 确保目录存在
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }
            
            _logger.LogInformation($"📁 配置文件路径: {_configFilePath}");
            
            // 加载配置
            _ = LoadConfigurationsAsync();
        }
        
        #region CRUD操作
        
        /// <summary>
        /// 创建新配置
        /// </summary>
        public AutoMonitorConfig CreateConfiguration(string name, decimal accountEquity, int riskCapitalTimes)
        {
            try
            {
                // 检查名称是否重复
                if (Configurations.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException($"配置名称 '{name}' 已存在");
                }
                
                var config = new AutoMonitorConfig
                {
                    Name = name,
                    CreateTime = DateTime.Now,
                    LastModifiedTime = DateTime.Now,
                    BreakEvenConfig = new AutoBreakEvenConfig
                    {
                        IsEnabled = true,
                        TriggerProfitAmount = Math.Round((accountEquity / riskCapitalTimes) * 0.1m, 0, MidpointRounding.AwayFromZero)
                    },
                    AddPositionConfig = new AutoAddPositionConfig
                    {
                        IsEnabled = true,
                        Tiers = CreateDefaultAddPositionTiers(accountEquity, riskCapitalTimes)
                    },
                    ProfitProtectionConfig = new AutoProfitProtectionConfig
                    {
                        IsEnabled = true,
                        Tiers = CreateDefaultProfitProtectionTiers(accountEquity, riskCapitalTimes)
                    }
                };
                
                Configurations.Add(config);
                _logger.LogInformation($"创建新配置: {name}");
                
                // 保存到文件
                _ = SaveConfigurationsAsync();
                
                // 触发事件
                ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs 
                { 
                    ChangeType = ConfigChangeType.Created, 
                    Configuration = config 
                });
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建配置失败: {name}");
                throw;
            }
        }
        
        /// <summary>
        /// 更新配置
        /// </summary>
        public void UpdateConfiguration(AutoMonitorConfig config)
        {
            try
            {
                var existingConfig = Configurations.FirstOrDefault(c => c.Name == config.Name);
                if (existingConfig == null)
                {
                    throw new ArgumentException($"配置 '{config.Name}' 不存在");
                }
                
                // 更新属性
                var index = Configurations.IndexOf(existingConfig);
                config.LastModifiedTime = DateTime.Now;
                Configurations[index] = config;
                
                _logger.LogInformation($"更新配置: {config.Name}");
                
                // 保存到文件
                _ = SaveConfigurationsAsync();
                
                // 触发事件
                ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs 
                { 
                    ChangeType = ConfigChangeType.Updated, 
                    Configuration = config 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新配置失败: {config.Name}");
                throw;
            }
        }
        
        /// <summary>
        /// 删除配置
        /// </summary>
        public void DeleteConfiguration(string name)
        {
            try
            {
                var config = Configurations.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (config == null)
                {
                    throw new ArgumentException($"配置 '{name}' 不存在");
                }
                
                Configurations.Remove(config);
                _logger.LogInformation($"删除配置: {name}");
                
                // 如果删除的是当前配置，清空当前配置
                if (CurrentConfig?.Name == name)
                {
                    CurrentConfig = null;
                }
                
                // 保存到文件
                _ = SaveConfigurationsAsync();
                
                // 触发事件
                ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs 
                { 
                    ChangeType = ConfigChangeType.Deleted, 
                    Configuration = config 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除配置失败: {name}");
                throw;
            }
        }
        
        /// <summary>
        /// 获取配置
        /// </summary>
        public AutoMonitorConfig? GetConfiguration(string name)
        {
            return Configurations.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// 添加配置（如果不存在）
        /// </summary>
        public void AddConfiguration(AutoMonitorConfig config)
        {
            try
            {
                // 检查配置是否已存在
                if (Configurations.Any(c => c.Name.Equals(config.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning($"配置 '{config.Name}' 已存在，跳过添加");
                    return;
                }
                
                // 设置时间戳
                if (config.CreateTime == default)
                    config.CreateTime = DateTime.Now;
                config.LastModifiedTime = DateTime.Now;
                
                Configurations.Add(config);
                _logger.LogInformation($"添加配置: {config.Name}");
                
                // 保存到文件
                _ = SaveConfigurationsAsync();
                
                // 触发事件
                ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs 
                { 
                    ChangeType = ConfigChangeType.Created, 
                    Configuration = config 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"添加配置失败: {config.Name}");
                throw;
            }
        }
        
        /// <summary>
        /// 设置当前配置
        /// </summary>
        public void SetCurrentConfiguration(string name)
        {
            var config = GetConfiguration(name);
            if (config == null)
            {
                throw new ArgumentException($"配置 '{name}' 不存在");
            }
            
            CurrentConfig = config;
            _logger.LogInformation($"切换到配置: {name}");
            
            // 触发事件
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs 
            { 
                ChangeType = ConfigChangeType.Selected, 
                Configuration = config 
            });
        }
        
        #endregion
        
        #region 阶梯管理
        
        /// <summary>
        /// 添加推仓阶梯
        /// </summary>
        public void AddPositionTier(AutoMonitorConfig config, decimal accountEquity, int riskCapitalTimes)
        {
            var riskCapital = accountEquity / riskCapitalTimes;
            var nextTierIndex = config.AddPositionConfig.Tiers.Count + 1;
            
            var newTier = new AddPositionTier
            {
                TierIndex = nextTierIndex,
                TriggerProfitAmount = Math.Round(riskCapital * nextTierIndex, 0, MidpointRounding.AwayFromZero),
                RiskMultiplier = 1.0m,
                StopLossRatio = 0.10m,
                IsEnabled = true
            };
            
            config.AddPositionConfig.Tiers.Add(newTier);
            UpdateConfiguration(config);
        }
        
        /// <summary>
        /// 删除推仓阶梯
        /// </summary>
        public void RemovePositionTier(AutoMonitorConfig config, int tierIndex)
        {
            var tier = config.AddPositionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
            if (tier != null)
            {
                config.AddPositionConfig.Tiers.Remove(tier);
                
                // 重新排序TierIndex
                for (int i = 0; i < config.AddPositionConfig.Tiers.Count; i++)
                {
                    config.AddPositionConfig.Tiers[i].TierIndex = i + 1;
                }
                
                UpdateConfiguration(config);
            }
        }
        
        /// <summary>
        /// 添加保盈阶梯
        /// </summary>
        public void AddProfitProtectionTier(AutoMonitorConfig config, decimal accountEquity, int riskCapitalTimes)
        {
            var riskCapital = accountEquity / riskCapitalTimes;
            var nextTierIndex = config.ProfitProtectionConfig.Tiers.Count + 1;
            var triggerAmount = Math.Round(riskCapital * (nextTierIndex + 9) * 10, 0, MidpointRounding.AwayFromZero); // 10倍、20倍、30倍递增
            
            var newTier = new ProfitProtectionTier
            {
                TierIndex = nextTierIndex,
                TriggerProfitAmount = triggerAmount,
                ProtectionAmount = Math.Round(triggerAmount * 0.8m, 0, MidpointRounding.AwayFromZero),
                IsEnabled = true
            };
            
            config.ProfitProtectionConfig.Tiers.Add(newTier);
            UpdateConfiguration(config);
        }
        
        /// <summary>
        /// 删除保盈阶梯
        /// </summary>
        public void RemoveProfitProtectionTier(AutoMonitorConfig config, int tierIndex)
        {
            var tier = config.ProfitProtectionConfig.Tiers.FirstOrDefault(t => t.TierIndex == tierIndex);
            if (tier != null)
            {
                config.ProfitProtectionConfig.Tiers.Remove(tier);
                
                // 重新排序TierIndex
                for (int i = 0; i < config.ProfitProtectionConfig.Tiers.Count; i++)
                {
                    config.ProfitProtectionConfig.Tiers[i].TierIndex = i + 1;
                }
                
                UpdateConfiguration(config);
            }
        }
        
        #endregion
        
        #region 持久化
        
        /// <summary>
        /// 加载配置列表
        /// </summary>
        private Task LoadConfigurationsAsync()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    // 不创建默认配置，等待用户手动创建
                    _logger.LogInformation("配置文件不存在，等待用户创建配置");
                    return Task.CompletedTask;
                }
                
                lock (_fileLock)
                {
                    var json = File.ReadAllText(_configFilePath);
                    var configs = JsonSerializer.Deserialize<List<AutoMonitorConfig>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true
                    });
                    
                    if (configs != null)
                    {
                        Configurations.Clear();
                        foreach (var config in configs)
                        {
                            Configurations.Add(config);
                        }
                        
                        _logger.LogInformation($"加载了 {configs.Count} 个配置");
                    }
                }
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载配置文件失败");
                // 不创建默认配置，让用户手动处理
                return Task.CompletedTask;
            }
        }
        
        /// <summary>
        /// 保存配置列表
        /// </summary>
        private async Task SaveConfigurationsAsync()
        {
            try
            {
                var configs = Configurations.ToList();
                var json = JsonSerializer.Serialize(configs, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                });
                
                await Task.Run(() =>
                {
                    lock (_fileLock)
                    {
                        File.WriteAllText(_configFilePath, json);
                    }
                });
                
                _logger.LogDebug($"保存了 {configs.Count} 个配置到文件");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存配置文件失败");
            }
        }
        
        /// <summary>
        /// 创建默认配置
        /// </summary>
        private void CreateDefaultConfigurations()
        {
            try
            {
                var defaultConfig = new AutoMonitorConfig
                {
                    Name = "默认配置",
                    CreateTime = DateTime.Now,
                    LastModifiedTime = DateTime.Now,
                    BreakEvenConfig = new AutoBreakEvenConfig { IsEnabled = false, TriggerProfitAmount = 10.0m },
                    AddPositionConfig = new AutoAddPositionConfig 
                    { 
                        IsEnabled = false, 
                        Tiers = CreateDefaultAddPositionTiers(1000m, 10)
                    },
                    ProfitProtectionConfig = new AutoProfitProtectionConfig 
                    { 
                        IsEnabled = false, 
                        Tiers = CreateDefaultProfitProtectionTiers(1000m, 10)
                    }
                };
                
                Configurations.Add(defaultConfig);
                CurrentConfig = defaultConfig;
                
                _ = SaveConfigurationsAsync();
                _logger.LogInformation("创建了默认配置");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建默认配置失败");
            }
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 创建默认推仓阶梯
        /// </summary>
        private List<AddPositionTier> CreateDefaultAddPositionTiers(decimal accountEquity, int riskCapitalTimes)
        {
            var riskCapital = accountEquity / riskCapitalTimes;
            return new List<AddPositionTier>
            {
                new AddPositionTier { TierIndex = 1, TriggerProfitAmount = Math.Round(riskCapital * 1m, 0, MidpointRounding.AwayFromZero), RiskMultiplier = 1.0m, StopLossRatio = 0.10m, IsEnabled = true },
                new AddPositionTier { TierIndex = 2, TriggerProfitAmount = Math.Round(riskCapital * 2m, 0, MidpointRounding.AwayFromZero), RiskMultiplier = 1.0m, StopLossRatio = 0.10m, IsEnabled = true },
                new AddPositionTier { TierIndex = 3, TriggerProfitAmount = Math.Round(riskCapital * 3m, 0, MidpointRounding.AwayFromZero), RiskMultiplier = 1.0m, StopLossRatio = 0.10m, IsEnabled = true },
                new AddPositionTier { TierIndex = 4, TriggerProfitAmount = Math.Round(riskCapital * 4m, 0, MidpointRounding.AwayFromZero), RiskMultiplier = 1.0m, StopLossRatio = 0.10m, IsEnabled = true }
            };
        }
        
        /// <summary>
        /// 创建默认保盈阶梯
        /// </summary>
        private List<ProfitProtectionTier> CreateDefaultProfitProtectionTiers(decimal accountEquity, int riskCapitalTimes)
        {
            var riskCapital = accountEquity / riskCapitalTimes;
            return new List<ProfitProtectionTier>
            {
                new ProfitProtectionTier { TierIndex = 1, TriggerProfitAmount = Math.Round(riskCapital * 10m, 0, MidpointRounding.AwayFromZero), ProtectionAmount = Math.Round(riskCapital * 10m * 0.8m, 0, MidpointRounding.AwayFromZero), IsEnabled = true },
                new ProfitProtectionTier { TierIndex = 2, TriggerProfitAmount = Math.Round(riskCapital * 20m, 0, MidpointRounding.AwayFromZero), ProtectionAmount = Math.Round(riskCapital * 20m * 0.8m, 0, MidpointRounding.AwayFromZero), IsEnabled = true },
                new ProfitProtectionTier { TierIndex = 3, TriggerProfitAmount = Math.Round(riskCapital * 30m, 0, MidpointRounding.AwayFromZero), ProtectionAmount = Math.Round(riskCapital * 30m * 0.8m, 0, MidpointRounding.AwayFromZero), IsEnabled = true }
            };
        }
        
        #endregion
    }
    
    /// <summary>
    /// 配置变化事件参数
    /// </summary>
    public class ConfigurationChangedEventArgs : EventArgs
    {
        public ConfigChangeType ChangeType { get; set; }
        public AutoMonitorConfig Configuration { get; set; } = null!;
    }
    
    /// <summary>
    /// 配置变化类型
    /// </summary>
    public enum ConfigChangeType
    {
        Created,
        Updated,
        Deleted,
        Selected
    }
}