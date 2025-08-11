using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 全局模式管理器 - 统一控制模拟/实盘模式
    /// </summary>
    public class GlobalModeManager : INotifyPropertyChanged
    {
        private static GlobalModeManager? _instance;
        private static readonly object _lock = new object();
        private readonly ILogger<GlobalModeManager>? _logger;
        
        private bool _isSimulationMode = false; // 默认为实盘模式，模拟模式需要手动切换
        private string _configFilePath;
        
        // 单例模式
        public static GlobalModeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new GlobalModeManager();
                    }
                }
                return _instance;
            }
        }
        
        private GlobalModeManager()
        {
            // 配置文件路径
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BinanceFuturesTrader");
            Directory.CreateDirectory(appDataPath);
            _configFilePath = Path.Combine(appDataPath, "global_mode_config.json");
            
            // 加载配置
            LoadConfiguration();
        }
        
        /// <summary>
        /// 是否为模拟模式
        /// </summary>
        public bool IsSimulationMode
        {
            get => _isSimulationMode;
            set
            {
                if (_isSimulationMode != value)
                {
                    _isSimulationMode = value;
                    OnPropertyChanged(nameof(IsSimulationMode));
                    OnPropertyChanged(nameof(ModeDisplayText));
                    OnPropertyChanged(nameof(ModeStatusColor));
                    
                    // 保存配置
                    SaveConfiguration();
                    
                    // 触发模式变更事件
                    ModeChanged?.Invoke(this, new ModeChangedEventArgs(_isSimulationMode));
                    
                    _logger?.LogInformation($"🔄 全局模式已切换: {(_isSimulationMode ? "模拟模式" : "实盘模式")}");
                }
            }
        }
        
        /// <summary>
        /// 模式显示文本
        /// </summary>
        public string ModeDisplayText => _isSimulationMode ? "🧪 模拟模式" : "💰 实盘模式";
        
        /// <summary>
        /// 模式状态颜色
        /// </summary>
        public string ModeStatusColor => _isSimulationMode ? "#FF9800" : "#4CAF50"; // 橙色/绿色
        
        /// <summary>
        /// 模式详细说明
        /// </summary>
        public string ModeDescription => _isSimulationMode 
            ? "当前为模拟模式，所有交易操作仅做模拟，不会产生真实资金变动" 
            : "当前为实盘模式，所有交易操作将使用真实资金，请谨慎操作";
        
        /// <summary>
        /// 模式变更事件
        /// </summary>
        public event EventHandler<ModeChangedEventArgs>? ModeChanged;
        
        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        
        /// <summary>
        /// 切换模式
        /// </summary>
        public void ToggleMode()
        {
            IsSimulationMode = !IsSimulationMode;
        }
        
        /// <summary>
        /// 强制设置为模拟模式（用于安全保护）
        /// </summary>
        public void ForceSimulationMode(string reason)
        {
            _logger?.LogWarning($"⚠️ 强制切换到模拟模式: {reason}");
            IsSimulationMode = true;
        }
        
        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<GlobalModeConfig>(json);
                    if (config != null)
                    {
                        _isSimulationMode = config.IsSimulationMode;
                        _logger?.LogInformation($"📁 已加载全局模式配置: {(_isSimulationMode ? "模拟模式" : "实盘模式")}");
                    }
                }
                else
                {
                    // 首次运行，保存默认配置
                    SaveConfiguration();
                    _logger?.LogInformation($"🆕 首次运行，创建默认模式配置: {(_isSimulationMode ? "模拟模式" : "实盘模式")}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 加载全局模式配置失败，使用默认模拟模式");
                _isSimulationMode = true; // 出错时默认为安全的模拟模式
            }
        }
        
        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfiguration()
        {
            try
            {
                var config = new GlobalModeConfig
                {
                    IsSimulationMode = _isSimulationMode,
                    LastUpdated = DateTime.Now
                };
                
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
                
                _logger?.LogDebug($"💾 已保存全局模式配置: {(_isSimulationMode ? "模拟模式" : "实盘模式")}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 保存全局模式配置失败");
            }
        }
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    /// <summary>
    /// 全局模式配置
    /// </summary>
    public class GlobalModeConfig
    {
        public bool IsSimulationMode { get; set; } = true;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// 模式变更事件参数
    /// </summary>
    public class ModeChangedEventArgs : EventArgs
    {
        public bool IsSimulationMode { get; }
        
        public ModeChangedEventArgs(bool isSimulationMode)
        {
            IsSimulationMode = isSimulationMode;
        }
    }
} 