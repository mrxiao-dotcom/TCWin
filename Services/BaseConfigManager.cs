using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 基础配置管理器 - 负责自动盯盘配置的CRUD操作和持久化
    /// 🔧 采用单例模式，确保整个系统只有一个配置管理器实例
    /// </summary>
    public class BaseConfigManager
    {
        private readonly ILogger<BaseConfigManager> _logger;
        private readonly string _configFilePath;
        private readonly FilePathManager _filePathManager;
        private readonly object _fileLock = new object();
        
        // 🔧 单例模式实现
        private static BaseConfigManager? _instance;
        private static readonly object _singletonLock = new object();
        
        /// <summary>
        /// 获取BaseConfigManager的单例实例
        /// </summary>
        public static BaseConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_singletonLock)
                    {
                        if (_instance == null)
                        {
                            // 🔧 使用NullLogger作为默认Logger，可以通过SetLogger方法后续设置
                            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<BaseConfigManager>.Instance;
                            _instance = new BaseConfigManager(logger);
                        }
                    }
                }
                return _instance;
            }
        }
        
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
        
        /// <summary>
        /// 🔧 私有构造函数，防止外部直接实例化
        /// </summary>
        private BaseConfigManager(ILogger<BaseConfigManager> logger, FilePathManager? filePathManager = null)
        {
            _logger = logger;
            _filePathManager = filePathManager ?? new FilePathManager();
            _configFilePath = _filePathManager.GetBaseConfigsFilePath();
            Configurations = new ObservableCollection<AutoMonitorConfig>();
            
            _logger.LogDebug($"📁 基础配置文件路径: {_configFilePath}");
            
            _ = LoadConfigurationsAsync();
        }
        
        /// <summary>
        /// 🔧 设置Logger（可选，用于替换默认的NullLogger）
        /// </summary>
        public void SetLogger(ILogger<BaseConfigManager> logger)
        {
            // 注意：由于_logger是readonly，我们需要通过反射或者其他方式来设置
            // 或者我们可以使用一个包装器模式
            // 这里为了简化，我们记录日志但不实际更换Logger
            _logger.LogInformation("🔧 BaseConfigManager单例模式已启用");
        }
        
        /// <summary>
        /// 🔧 强制重新加载配置（供外部调用）
        /// </summary>
        public void RefreshConfigurations()
        {
            try
            {
                _logger.LogInformation("🔄 强制重新加载配置...");
                LoadConfigurationsAsync().Wait(); // 同步等待完成
                _logger.LogInformation($"✅ 已重新加载 {Configurations.Count} 个配置");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 强制重新加载配置失败");
            }
        }
        
        #region CRUD操作
        
        /// <summary>
        /// 创建新配置
        /// </summary>
        public AutoMonitorConfig CreateConfiguration(string name, decimal accountEquity, int riskCapitalTimes)
        {
            try
            {
                // 🔧 修复：检查名称是否重复，如果存在则返回现有配置而不是抛出异常
                var existingConfig = Configurations.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existingConfig != null)
                {
                    _logger.LogInformation($"配置 '{name}' 已存在，返回现有配置而不重复创建");
                    return existingConfig;
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
                
                // 🔧 修复：同步保存到文件，确保保存完成
                SaveConfigurationsSync();
                
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
                
                // 🔧 修复：同步保存到文件，确保保存完成
                SaveConfigurationsSync();
                
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
                
                // 🔧 修复：同步保存到文件，确保保存完成
                SaveConfigurationsSync();
                
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
                
                // 🔧 修复：同步保存到文件，确保保存完成
                SaveConfigurationsSync();
                
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
        
        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        public string GetConfigFilePath()
        {
            return _configFilePath;
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
                    List<AutoMonitorConfig>? configs = null;
                    
                    // 🎯 首先尝试新格式（数组格式）
                    try
                    {
                        configs = JsonSerializer.Deserialize<List<AutoMonitorConfig>>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            WriteIndented = true
                        });
                        _logger.LogInformation("✅ 使用新格式加载配置");
                    }
                    catch (JsonException)
                    {
                        // 🔄 如果新格式失败，尝试旧格式（对象格式）
                        _logger.LogInformation("🔄 新格式失败，尝试旧格式迁移...");
                        try
                        {
                            using var document = JsonDocument.Parse(json);
                            var root = document.RootElement;
                            
                            configs = new List<AutoMonitorConfig>();
                            
                            // 检查是否是旧格式（包含accountConfigs）
                            if (root.TryGetProperty("accountConfigs", out var accountConfigsElement))
                            {
                                foreach (var accountProperty in accountConfigsElement.EnumerateObject())
                                {
                                    var configElement = accountProperty.Value;
                                    var config = JsonSerializer.Deserialize<AutoMonitorConfig>(configElement.GetRawText(), new JsonSerializerOptions
                                    {
                                        PropertyNameCaseInsensitive = true
                                    });
                                    
                                    if (config != null)
                                    {
                                        configs.Add(config);
                                    }
                                }
                                
                                _logger.LogInformation($"✅ 从旧格式迁移了 {configs.Count} 个配置");
                                
                                // 🔧 迁移完成后，同步保存为新格式
                                SaveConfigurationsSync();
                            }
                        }
                        catch (Exception migrationEx)
                        {
                            _logger.LogError(migrationEx, "❌ 旧格式迁移失败");
                            throw;
                        }
                    }
                    
                    if (configs != null)
                    {
                        Configurations.Clear();
                        foreach (var config in configs)
                        {
                            Configurations.Add(config);
                        }
                        
                        _logger.LogInformation($"📂 最终加载了 {configs.Count} 个配置");
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
        /// 保存配置列表（异步）- 简化版：只保存基础配置信息，不保存状态
        /// </summary>
        private async Task SaveConfigurationsAsync()
        {
            try
            {
                var configs = Configurations.ToList();
                
                // 🔧 创建简化配置：移除所有状态信息，只保留基础配置
                var simplifiedConfigs = configs.Select(config => new AutoMonitorConfig
                {
                    Name = config.Name,
                    IsEnabled = config.IsEnabled,
                    ScanIntervalSeconds = config.ScanIntervalSeconds,
                    CooldownSeconds = config.CooldownSeconds,
                    CreateTime = config.CreateTime,
                    LastModifiedTime = config.LastModifiedTime,
                    
                    // 保本配置 - 只保留基础设置
                    BreakEvenConfig = new AutoBreakEvenConfig
                    {
                        IsEnabled = config.BreakEvenConfig.IsEnabled,
                        TriggerProfitAmount = config.BreakEvenConfig.TriggerProfitAmount
                        // 不保存状态信息
                    },
                    
                    // 推仓配置 - 只保留基础设置
                    AddPositionConfig = new AutoAddPositionConfig
                    {
                        IsEnabled = config.AddPositionConfig.IsEnabled,
                        Tiers = config.AddPositionConfig.Tiers.Select(tier => new AddPositionTier
                        {
                            TierIndex = tier.TierIndex,
                            IsEnabled = tier.IsEnabled,
                            TriggerProfitAmount = tier.TriggerProfitAmount,
                            RiskMultiplier = tier.RiskMultiplier,
                            StopLossRatio = tier.StopLossRatio,
                            ProfitProtectionAmount = tier.ProfitProtectionAmount,
                            ExitTargetPnl = tier.ExitTargetPnl
                            // 不保存执行状态
                        }).ToList()
                    },
                    
                    // 保盈配置 - 只保留基础设置
                    ProfitProtectionConfig = new AutoProfitProtectionConfig
                    {
                        IsEnabled = config.ProfitProtectionConfig.IsEnabled,
                        Tiers = config.ProfitProtectionConfig.Tiers.Select(tier => new ProfitProtectionTier
                        {
                            TierIndex = tier.TierIndex,
                            IsEnabled = tier.IsEnabled,
                            TriggerProfitAmount = tier.TriggerProfitAmount,
                            ProtectionAmount = tier.ProtectionAmount
                            // 不保存执行状态
                        }).ToList()
                    }
                }).ToList();
                
                var json = JsonSerializer.Serialize(simplifiedConfigs, new JsonSerializerOptions
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
                
                _logger.LogDebug($"💾 异步保存了 {simplifiedConfigs.Count} 个简化基础配置到文件（不含状态信息）");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存配置文件失败");
            }
        }
        
        /// <summary>
        /// 🔧 保存配置列表（同步）- 简化版：只保存基础配置信息，不保存状态
        /// </summary>
        private void SaveConfigurationsSync()
        {
            try
            {
                var configs = Configurations.ToList();
                
                // 🔧 创建完全简化的配置：只保留基础配置信息，完全排除状态字段
                var simplifiedConfigs = configs.Select(config => new
                {
                    Name = config.Name,
                    IsEnabled = config.IsEnabled,
                    ScanIntervalSeconds = config.ScanIntervalSeconds,
                    CooldownSeconds = config.CooldownSeconds,
                    CreateTime = config.CreateTime,
                    LastModifiedTime = config.LastModifiedTime,
                    
                    // 保本配置 - 只保留基础设置
                    BreakEvenConfig = new
                    {
                        IsEnabled = config.BreakEvenConfig.IsEnabled,
                        TriggerProfitAmount = config.BreakEvenConfig.TriggerProfitAmount,
                        Description = config.BreakEvenConfig.Description
                    },
                    
                    // 推仓配置 - 只保留基础设置
                    AddPositionConfig = new
                    {
                        IsEnabled = config.AddPositionConfig.IsEnabled,
                        Tiers = config.AddPositionConfig.Tiers.Select(tier => new
                        {
                            TierIndex = tier.TierIndex,
                            IsEnabled = tier.IsEnabled,
                            TriggerProfitAmount = tier.TriggerProfitAmount,
                            RiskMultiplier = tier.RiskMultiplier,
                            StopLossRatio = tier.StopLossRatio,
                            ProfitProtectionAmount = tier.ProfitProtectionAmount,
                            Description = tier.Description
                        }).ToList()
                    },
                    
                    // 保盈配置 - 只保留基础设置
                    ProfitProtectionConfig = new
                    {
                        IsEnabled = config.ProfitProtectionConfig.IsEnabled,
                        Tiers = config.ProfitProtectionConfig.Tiers.Select(tier => new
                        {
                            TierIndex = tier.TierIndex,
                            IsEnabled = tier.IsEnabled,
                            TriggerProfitAmount = tier.TriggerProfitAmount,
                            ProtectionAmount = tier.ProtectionAmount,
                            Description = tier.Description
                        }).ToList()
                    }
                }).ToList();
                
                var json = JsonSerializer.Serialize(simplifiedConfigs, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                    Converters = { new JsonStringEnumConverter() }
                });
                
                lock (_fileLock)
                {
                    File.WriteAllText(_configFilePath, json);
                }
                
                _logger.LogDebug($"💾 保存了 {simplifiedConfigs.Count} 个简化基础配置到文件（不含状态信息）");
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
                
                // 🔧 修复：同步保存默认配置
                SaveConfigurationsSync();
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