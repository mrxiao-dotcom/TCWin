using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BinanceFuturesTrader.Views.AutoMonitor.Controllers
{
    /// <summary>
    /// 配置控制器
    /// 处理配置的加载、保存和管理
    /// </summary>
    public class ConfigurationController : INotifyPropertyChanged, IDisposable
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly UIStateModel _uiStateModel;
        private readonly ILogger _logger;
        private bool _isDisposed = false;
        private readonly string _configFilePath;
        
        public ConfigurationController(AutoMonitorDataModel dataModel, UIStateModel uiStateModel, ILogger logger)
        {
            _dataModel = dataModel ?? throw new ArgumentNullException(nameof(dataModel));
            _uiStateModel = uiStateModel ?? throw new ArgumentNullException(nameof(uiStateModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "AutoMonitorConfig.json");
            
            EnsureConfigDirectory();
        }
        
        #region 配置属性
        
        /// <summary>
        /// 当前配置
        /// </summary>
        public AutoMonitorConfiguration CurrentConfiguration { get; private set; } = new AutoMonitorConfiguration();
        
        /// <summary>
        /// 配置是否已加载
        /// </summary>
        public bool IsConfigurationLoaded { get; private set; } = false;
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 加载配置
        /// </summary>
        public async Task<bool> LoadConfigurationAsync()
        {
            try
            {
                _logger.LogInformation("开始加载配置文件");
                
                if (!File.Exists(_configFilePath))
                {
                    _logger.LogWarning("配置文件不存在，创建默认配置");
                    await CreateDefaultConfigurationAsync();
                    return true;
                }
                
                var jsonContent = await File.ReadAllTextAsync(_configFilePath);
                var config = JsonConvert.DeserializeObject<AutoMonitorConfiguration>(jsonContent);
                
                if (config != null)
                {
                    CurrentConfiguration = config;
                    ApplyConfigurationToDataModel();
                    IsConfigurationLoaded = true;
                    
                    _logger.LogInformation($"配置加载成功: {CurrentConfiguration.Name}");
                    return true;
                }
                else
                {
                    _logger.LogError("配置文件格式错误");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载配置时发生异常");
                return false;
            }
        }
        
        /// <summary>
        /// 保存配置
        /// </summary>
        public async Task<bool> SaveConfigurationAsync()
        {
            try
            {
                _logger.LogInformation("开始保存配置文件");
                
                // 从数据模型更新配置
                UpdateConfigurationFromDataModel();
                
                var jsonContent = JsonConvert.SerializeObject(CurrentConfiguration, Formatting.Indented);
                await File.WriteAllTextAsync(_configFilePath, jsonContent);
                
                _logger.LogInformation("配置保存成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存配置时发生异常");
                return false;
            }
        }
        
        /// <summary>
        /// 创建默认配置
        /// </summary>
        public async Task CreateDefaultConfigurationAsync()
        {
            try
            {
                CurrentConfiguration = new AutoMonitorConfiguration
                {
                    Name = "默认配置",
                    ScanIntervalSeconds = 5,
                    EnableSoundNotification = true,
                    LogRetentionDays = 7,
                    MaxLogFileSize = 100,
                    LogLevel = "INFO",
                    EnableDetailedLogging = false,
                    AutoScroll = true,
                    ShowOnlyActiveContracts = false,
                    Theme = "默认主题"
                };
                
                ApplyConfigurationToDataModel();
                await SaveConfigurationAsync();
                
                IsConfigurationLoaded = true;
                _logger.LogInformation("默认配置创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建默认配置时发生异常");
            }
        }
        
        /// <summary>
        /// 重置配置
        /// </summary>
        public async Task ResetConfigurationAsync()
        {
            try
            {
                _logger.LogInformation("重置配置");
                await CreateDefaultConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重置配置时发生异常");
            }
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 确保配置目录存在
        /// </summary>
        private void EnsureConfigDirectory()
        {
            try
            {
                var configDir = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建配置目录时发生异常");
            }
        }
        
        /// <summary>
        /// 应用配置到数据模型
        /// </summary>
        private void ApplyConfigurationToDataModel()
        {
            try
            {
                _dataModel.ConfigName = CurrentConfiguration.Name;
                _dataModel.ScanIntervalSeconds = CurrentConfiguration.ScanIntervalSeconds;
                _dataModel.EnableSoundNotification = CurrentConfiguration.EnableSoundNotification;
                _dataModel.LogRetentionDays = CurrentConfiguration.LogRetentionDays;
                _dataModel.MaxLogFileSize = CurrentConfiguration.MaxLogFileSize;
                _dataModel.LogLevel = CurrentConfiguration.LogLevel;
                _dataModel.EnableDetailedLogging = CurrentConfiguration.EnableDetailedLogging;
                _dataModel.AutoScroll = CurrentConfiguration.AutoScroll;
                _dataModel.ShowOnlyActiveContracts = CurrentConfiguration.ShowOnlyActiveContracts;
                _dataModel.CurrentTheme = CurrentConfiguration.Theme;
                _dataModel.IsConfigLoaded = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "应用配置到数据模型时发生异常");
            }
        }
        
        /// <summary>
        /// 从数据模型更新配置
        /// </summary>
        private void UpdateConfigurationFromDataModel()
        {
            try
            {
                CurrentConfiguration.Name = _dataModel.ConfigName;
                CurrentConfiguration.ScanIntervalSeconds = _dataModel.ScanIntervalSeconds;
                CurrentConfiguration.EnableSoundNotification = _dataModel.EnableSoundNotification;
                CurrentConfiguration.LogRetentionDays = _dataModel.LogRetentionDays;
                CurrentConfiguration.MaxLogFileSize = _dataModel.MaxLogFileSize;
                CurrentConfiguration.LogLevel = _dataModel.LogLevel;
                CurrentConfiguration.EnableDetailedLogging = _dataModel.EnableDetailedLogging;
                CurrentConfiguration.AutoScroll = _dataModel.AutoScroll;
                CurrentConfiguration.ShowOnlyActiveContracts = _dataModel.ShowOnlyActiveContracts;
                CurrentConfiguration.Theme = _dataModel.CurrentTheme;
                CurrentConfiguration.LastModified = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据模型更新配置时发生异常");
            }
        }
        
        #endregion
        
        #region IDisposable 实现
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        // 保存配置
                        _ = SaveConfigurationAsync();
                        
                        _logger.LogDebug("配置控制器已释放");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "释放配置控制器时发生异常");
                    }
                }
                
                _isDisposed = true;
            }
        }
        
        #endregion
        
        #region INotifyPropertyChanged 实现
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
    
    /// <summary>
    /// 自动监控配置数据模型
    /// </summary>
    public class AutoMonitorConfiguration
    {
        public string Name { get; set; } = "默认配置";
        public int ScanIntervalSeconds { get; set; } = 5;
        public bool EnableSoundNotification { get; set; } = true;
        public int LogRetentionDays { get; set; } = 7;
        public int MaxLogFileSize { get; set; } = 100;
        public string LogLevel { get; set; } = "INFO";
        public bool EnableDetailedLogging { get; set; } = false;
        public bool AutoScroll { get; set; } = true;
        public bool ShowOnlyActiveContracts { get; set; } = false;
        public string Theme { get; set; } = "默认主题";
        public DateTime LastModified { get; set; } = DateTime.Now;
        public string Version { get; set; } = "2.0.0";
    }
} 