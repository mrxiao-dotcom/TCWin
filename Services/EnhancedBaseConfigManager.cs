using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 增强版基础配置管理器
    /// 内部使用新的AutoMonitorDataManager和FilePersistenceManager
    /// 但保持与现有代码兼容的API接口
    /// </summary>
    public class EnhancedBaseConfigManager : IDisposable
    {
        private readonly ILogger<EnhancedBaseConfigManager> _logger;
        private readonly AutoMonitorDataManager _dataManager;
        private readonly FilePersistenceManager _persistenceManager;
        private readonly ObservableCollection<AutoMonitorConfig> _configurations;
        
        // 单例模式实现
        private static EnhancedBaseConfigManager? _instance;
        private static readonly object _singletonLock = new object();
        
        /// <summary>
        /// 私有构造函数
        /// </summary>
        private EnhancedBaseConfigManager(ILogger<EnhancedBaseConfigManager> logger)
        {
            _logger = logger;
            
            // 初始化新的数据管理组件
            var persistenceLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<FilePersistenceManager>();
            _persistenceManager = new FilePersistenceManager(persistenceLogger);
            
            var dataManagerLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<AutoMonitorDataManager>();
            _dataManager = new AutoMonitorDataManager(dataManagerLogger, _persistenceManager);
            
            _configurations = new ObservableCollection<AutoMonitorConfig>();
            
            // 异步初始化（但在构造函数中启动）
            _ = InitializeAsync();
        }
        
        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static EnhancedBaseConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_singletonLock)
                    {
                        if (_instance == null)
                        {
                            var logger = LoggerFactory.Create(builder => builder.AddConsole())
                                .CreateLogger<EnhancedBaseConfigManager>();
                            _instance = new EnhancedBaseConfigManager(logger);
                        }
                    }
                }
                return _instance;
            }
        }

        #region 兼容性API - 与现有代码保持一致

        /// <summary>
        /// 配置集合（只读）
        /// </summary>
        public ObservableCollection<AutoMonitorConfig> Configurations => _configurations;

        /// <summary>
        /// 刷新配置（重新从文件加载）
        /// </summary>
        public void RefreshConfigurations()
        {
            try
            {
                _logger?.LogInformation("🔄 刷新增强版配置...");
                
                // 使用适配器将新格式转换为旧格式以保持兼容性
                var baseConfigs = _dataManager.GetAllBaseConfigs();
                var autoConfigs = baseConfigs.Select(AutoMonitorConfigAdapter.ToAutoMonitorConfig).ToList();
                
                _configurations.Clear();
                foreach (var config in autoConfigs)
                {
                    _configurations.Add(config);
                }
                
                _logger?.LogInformation($"✅ 增强版配置刷新完成: {_configurations.Count} 个配置");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 刷新增强版配置失败");
            }
        }
        
        /// <summary>
        /// 检查是否有任何基础配置
        /// </summary>
        public bool HasAnyBaseConfigs()
        {
            try
            {
                // 首先检查内存中的配置
                if (_configurations.Count > 0)
                {
                    return true;
                }
                
                // 检查数据管理器中的配置
                var baseConfigs = _dataManager.GetAllBaseConfigs();
                return baseConfigs.Count > 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "检查基础配置时发生错误");
                return false;
            }
        }

        /// <summary>
        /// 保存单个配置
        /// </summary>
        public void SaveConfiguration(AutoMonitorConfig config)
        {
            try
            {
                if (config == null)
                {
                    _logger.LogWarning("⚠️ 尝试保存空配置");
                    return;
                }

                _logger.LogInformation($"💾 保存配置: {config.Name}");

                // 转换为新模型并保存
                var baseConfig = AutoMonitorConfigAdapter.ToBaseConfig(config);
                _ = _dataManager.SaveBaseConfigAsync(baseConfig);

                // 更新本地集合
                var existingConfig = _configurations.FirstOrDefault(c => c.Name == config.Name);
                if (existingConfig != null)
                {
                    var index = _configurations.IndexOf(existingConfig);
                    _configurations[index] = config;
                }
                else
                {
                    _configurations.Add(config);
                }

                _logger.LogInformation($"✅ 配置保存成功: {config.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 保存配置失败: {config?.Name}");
                throw;
            }
        }

        /// <summary>
        /// 删除配置
        /// </summary>
        public void DeleteConfiguration(string configName)
        {
            try
            {
                _logger.LogInformation($"🗑️ 删除配置: {configName}");

                // 在新数据管理器中查找并删除对应配置
                var baseConfigs = _dataManager.GetAllBaseConfigs();
                var configToDelete = baseConfigs.FirstOrDefault(c => c.Name == configName);
                
                if (configToDelete != null)
                {
                    _ = _dataManager.DeleteBaseConfigAsync(configToDelete.Id);
                }

                // 更新本地集合
                var existingConfig = _configurations.FirstOrDefault(c => c.Name == configName);
                if (existingConfig != null)
                {
                    _configurations.Remove(existingConfig);
                }

                _logger.LogInformation($"✅ 配置删除成功: {configName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 删除配置失败: {configName}");
                throw;
            }
        }

        /// <summary>
        /// 获取配置文件路径（兼容性）
        /// </summary>
        public string GetConfigFilePath()
        {
            return _persistenceManager.GetFilePath("base_configs.json");
        }

        /// <summary>
        /// 根据名称获取配置
        /// </summary>
        public AutoMonitorConfig? GetConfigurationByName(string configName)
        {
            return _configurations.FirstOrDefault(c => c.Name == configName);
        }

        /// <summary>
        /// 检查配置是否存在
        /// </summary>
        public bool ConfigurationExists(string configName)
        {
            return _configurations.Any(c => c.Name == configName);
        }

        #endregion

        #region 新增API - 增强功能

        /// <summary>
        /// 设置当前活跃配置
        /// </summary>
        public async Task SetCurrentConfigurationAsync(string configName)
        {
            try
            {
                var baseConfigs = _dataManager.GetAllBaseConfigs();
                var targetConfig = baseConfigs.FirstOrDefault(c => c.Name == configName);
                
                if (targetConfig == null)
                {
                    throw new ArgumentException($"配置不存在: {configName}");
                }

                await _dataManager.SwitchConfigurationAsync(targetConfig.Id);
                _logger.LogInformation($"✅ 当前配置已切换为: {configName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 切换配置失败: {configName}");
                throw;
            }
        }

        /// <summary>
        /// 获取当前活跃配置
        /// </summary>
        public AutoMonitorConfig? GetCurrentConfiguration()
        {
            var currentBaseConfig = _dataManager.GetCurrentConfig();
            return currentBaseConfig != null 
                ? AutoMonitorConfigAdapter.ToAutoMonitorConfig(currentBaseConfig) 
                : null;
        }

        /// <summary>
        /// 获取配置切换历史
        /// </summary>
        public List<ContractExecutionHistory> GetConfigurationHistory()
        {
            return _dataManager.GetRecentExecutionHistory()
                .Where(h => h.ExecutionType == ExecutionTypes.ConfigurationSwitched)
                .ToList();
        }

        /// <summary>
        /// 批量导入配置
        /// </summary>
        public async Task ImportConfigurationsAsync(List<AutoMonitorConfig> configs)
        {
            try
            {
                _logger.LogInformation($"📥 开始批量导入 {configs.Count} 个配置");

                foreach (var config in configs)
                {
                    if (AutoMonitorConfigAdapter.IsValidConfig(config))
                    {
                        var baseConfig = AutoMonitorConfigAdapter.ToBaseConfig(config);
                        await _dataManager.SaveBaseConfigAsync(baseConfig);
                        
                        // 更新本地集合
                        if (!_configurations.Any(c => c.Name == config.Name))
                        {
                            _configurations.Add(config);
                        }
                    }
                }

                _logger.LogInformation($"✅ 配置批量导入完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 批量导入配置失败");
                throw;
            }
        }

        /// <summary>
        /// 导出所有配置
        /// </summary>
        public List<AutoMonitorConfig> ExportAllConfigurations()
        {
            return _configurations.ToList();
        }

        #endregion

        #region 数据迁移支持

        /// <summary>
        /// 从旧格式迁移配置
        /// </summary>
        public async Task MigrateFromLegacyAsync(List<AutoMonitorConfig> legacyConfigs)
        {
            try
            {
                _logger.LogInformation($"🔄 开始迁移 {legacyConfigs.Count} 个旧配置");

                foreach (var legacyConfig in legacyConfigs)
                {
                    // 转换并保存到新系统
                    var baseConfig = AutoMonitorConfigAdapter.ToBaseConfig(legacyConfig);
                    await _dataManager.SaveBaseConfigAsync(baseConfig);
                }

                // 刷新本地配置列表
                RefreshConfigurations();

                _logger.LogInformation($"✅ 配置迁移完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 配置迁移失败");
                throw;
            }
        }

        /// <summary>
        /// 迁移合约状态数据
        /// </summary>
        public async Task MigrateContractStatesAsync(Dictionary<string, ContractMonitoringState> legacyStates)
        {
            try
            {
                _logger.LogInformation($"🔄 开始迁移 {legacyStates.Count} 个合约状态");

                foreach (var kvp in legacyStates)
                {
                    var contractState = AutoMonitorConfigAdapter.ToContractState(kvp.Value);
                    await _dataManager.SaveContractStateAsync(contractState);
                }

                _logger.LogInformation($"✅ 合约状态迁移完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 合约状态迁移失败");
                throw;
            }
        }

        /// <summary>
        /// 数据迁移工具集成
        /// </summary>
        public async Task<bool> MigrateFromLegacyAsync()
        {
            try
            {
                _logger?.LogInformation("🔄 开始数据迁移检查...");
                
                var migrationLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DataMigrationTool>();
                var migrationTool = new DataMigrationTool(migrationLogger);
                
                var dataDirectory = _persistenceManager.GetFilePath("").Replace("\\", "/").TrimEnd('/');
                var needsMigration = await migrationTool.IsMigrationNeededAsync(dataDirectory);
                
                if (!needsMigration)
                {
                    _logger?.LogInformation("✅ 无需数据迁移");
                    return false;
                }
                
                _logger?.LogInformation("🚀 开始数据迁移...");
                var result = await migrationTool.MigrateAllDataAsync(dataDirectory);
                
                if (result.Success)
                {
                    _logger?.LogInformation($"✅ 数据迁移成功: {result.Message}");
                    
                    // 迁移完成后重新初始化数据管理器
                    await _dataManager.InitializeAsync();
                    
                    // 刷新配置显示
                    RefreshConfigurations();
                    
                    return true;
                }
                else
                {
                    _logger?.LogError($"❌ 数据迁移失败: {result.Message}");
                    
                    foreach (var warning in result.Warnings)
                    {
                        _logger?.LogWarning($"⚠️ {warning}");
                    }
                    
                    if (result.Exception != null)
                    {
                        _logger?.LogError(result.Exception, "迁移异常详情");
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 数据迁移过程中发生异常");
                return false;
            }
        }

        /// <summary>
        /// 获取迁移状态信息
        /// </summary>
        public async Task<string> GetMigrationStatusAsync()
        {
            try
            {
                var migrationLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DataMigrationTool>();
                var migrationTool = new DataMigrationTool(migrationLogger);
                
                var dataDirectory = _persistenceManager.GetFilePath("").Replace("\\", "/").TrimEnd('/');
                var needsMigration = await migrationTool.IsMigrationNeededAsync(dataDirectory);
                
                if (needsMigration)
                {
                    return "📋 发现旧格式数据，建议执行迁移";
                }
                else
                {
                    return "✅ 数据格式为最新版本";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "检查迁移状态时发生错误");
                return "❌ 无法检查迁移状态";
            }
        }

        #endregion

        #region 事件订阅

        /// <summary>
        /// 订阅配置切换事件
        /// </summary>
        public void SubscribeToConfigurationChanges(EventHandler<ConfigurationSwitchedEventArgs> handler)
        {
            _dataManager.ConfigurationSwitched += handler;
        }

        /// <summary>
        /// 取消订阅配置切换事件
        /// </summary>
        public void UnsubscribeFromConfigurationChanges(EventHandler<ConfigurationSwitchedEventArgs> handler)
        {
            _dataManager.ConfigurationSwitched -= handler;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 异步初始化
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("🚀 开始初始化增强版配置管理器...");

                // 初始化数据管理器
                await _dataManager.InitializeAsync();

                // 加载现有配置
                RefreshConfigurations();

                _logger.LogInformation("✅ 增强版配置管理器初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 初始化失败");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try
            {
                _logger.LogInformation("🔄 开始关闭增强版配置管理器...");
                
                // 关闭数据管理器
                _dataManager?.ShutdownAsync().Wait(TimeSpan.FromSeconds(5));
                _dataManager?.Dispose();
                
                // 释放持久化管理器
                _persistenceManager?.Dispose();
                
                _logger.LogInformation("✅ 增强版配置管理器已关闭");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 关闭过程中出现错误");
            }
        }

        #endregion

        #region 静态方法

        /// <summary>
        /// 替换现有的BaseConfigManager实例
        /// 用于平滑过渡到新的数据管理器
        /// </summary>
        public static void ReplaceExistingInstance()
        {
            lock (_singletonLock)
            {
                // 强制重新创建实例
                _instance?.Dispose();
                _instance = null;
                
                // 下次访问时会创建新实例
            }
        }

        #endregion
    }
} 