using System;
using System.IO;
using System.Threading.Tasks;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic; // Added for Dictionary
using System.Linq; // Added for Any

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 🎯 简化服务整合器 - 统一管理所有简化服务
    /// 负责依赖注入、服务初始化和生命周期管理
    /// </summary>
    public class SimplifiedServiceIntegrator : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SimplifiedServiceIntegrator> _logger;
        private ServiceCollection? _services;
        private bool _isInitialized = false;

        // 核心服务实例
        public SimplifiedStateService StateService { get; private set; } = null!;
        public SimplifiedConfigManager ConfigManager { get; private set; } = null!;
        public SimplifiedExecutionEngine ExecutionEngine { get; private set; } = null!;
        public SimplifiedAutoMonitorService MonitorService { get; private set; } = null!;
        public SimplifiedUIAdapter UIAdapter { get; private set; } = null!;

        // 现有服务引用
        private readonly BinanceService _binanceService;
        private readonly TradingExecutionService _tradingExecutionService;
        private readonly GlobalModeManager _globalModeManager;

        public SimplifiedServiceIntegrator(
            BinanceService binanceService,
            TradingExecutionService tradingExecutionService,
            GlobalModeManager globalModeManager,
            ILogger<SimplifiedServiceIntegrator>? logger = null)
        {
            _binanceService = binanceService ?? throw new ArgumentNullException(nameof(binanceService));
            _tradingExecutionService = tradingExecutionService ?? throw new ArgumentNullException(nameof(tradingExecutionService));
            _globalModeManager = globalModeManager ?? throw new ArgumentNullException(nameof(globalModeManager));

            // 创建临时的日志工厂和logger
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = logger ?? loggerFactory.CreateLogger<SimplifiedServiceIntegrator>();

            // 构建服务容器
            _services = new ServiceCollection();
            ConfigureServices();
            _serviceProvider = _services.BuildServiceProvider();
        }

        #region 服务配置

        /// <summary>
        /// 配置依赖注入服务
        /// </summary>
        private void ConfigureServices()
        {
            _logger.LogInformation("🔧 开始配置简化服务依赖注入");

            // 注册日志服务
            if (_services != null)
            {
                _services.AddLogging(builder => builder.AddConsole());

                // 注册现有服务实例
                _services.AddSingleton(_binanceService);
                _services.AddSingleton(_tradingExecutionService);
                _services.AddSingleton(_globalModeManager);

                // 注册简化服务
                _services.AddSingleton<SimplifiedStateService>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<SimplifiedStateService>>();
                    var dataDirectory = GetDataDirectory();
                    return new SimplifiedStateService(logger, dataDirectory);
                });

                _services.AddSingleton<SimplifiedConfigManager>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<SimplifiedConfigManager>>();
                    var stateService = provider.GetRequiredService<SimplifiedStateService>();
                    return new SimplifiedConfigManager(logger, stateService);
                });

                _services.AddSingleton<SimplifiedExecutionEngine>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<SimplifiedExecutionEngine>>();
                    var stateService = provider.GetRequiredService<SimplifiedStateService>();
                    var configManager = provider.GetRequiredService<SimplifiedConfigManager>();
                    var tradingService = provider.GetRequiredService<TradingExecutionService>();
                    var binanceService = provider.GetRequiredService<BinanceService>();
                    return new SimplifiedExecutionEngine(logger, stateService, configManager, tradingService, binanceService);
                });

                _services.AddSingleton<SimplifiedAutoMonitorService>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<SimplifiedAutoMonitorService>>();
                    var executionEngine = provider.GetRequiredService<SimplifiedExecutionEngine>();
                    var stateService = provider.GetRequiredService<SimplifiedStateService>();
                    var binanceService = provider.GetRequiredService<BinanceService>();
                    var globalModeManager = provider.GetRequiredService<GlobalModeManager>();
                    return new SimplifiedAutoMonitorService(logger, executionEngine, stateService, binanceService, globalModeManager);
                });

                _services.AddSingleton<SimplifiedUIAdapter>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<SimplifiedUIAdapter>>();
                    var configManager = provider.GetRequiredService<SimplifiedConfigManager>();
                    var stateService = provider.GetRequiredService<SimplifiedStateService>();
                    var monitorService = provider.GetRequiredService<SimplifiedAutoMonitorService>();
                    return new SimplifiedUIAdapter(logger, configManager, stateService, monitorService);
                });

                _logger.LogInformation("✅ 简化服务依赖注入配置完成");
            }
            else
            {
                _logger.LogError("❌ 服务集合为null，无法配置依赖注入");
                throw new InvalidOperationException("服务集合未正确初始化");
            }
        }

        /// <summary>
        /// 获取数据目录路径
        /// </summary>
        private string GetDataDirectory()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var dataDirectory = Path.Combine(baseDirectory, "Data");
            
            // 确保目录存在
            Directory.CreateDirectory(dataDirectory);
            
            _logger.LogInformation($"📁 数据目录: {dataDirectory}");
            return dataDirectory;
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化所有服务
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.LogWarning("⚠️ 服务已初始化，跳过重复初始化");
                return true;
            }

            try
            {
                _logger.LogInformation("🚀 开始初始化简化服务集成器");

                // 1. 获取服务实例
                StateService = _serviceProvider.GetRequiredService<SimplifiedStateService>();
                ConfigManager = _serviceProvider.GetRequiredService<SimplifiedConfigManager>();
                ExecutionEngine = _serviceProvider.GetRequiredService<SimplifiedExecutionEngine>();
                MonitorService = _serviceProvider.GetRequiredService<SimplifiedAutoMonitorService>();
                UIAdapter = _serviceProvider.GetRequiredService<SimplifiedUIAdapter>();

                _logger.LogInformation("✅ 服务实例获取完成");

                // 2. 验证基础配置文件
                await ValidateBaseConfigurationAsync();

                // 3. 清理和初始化状态文件
                await InitializeStateFilesAsync();

                // 4. 验证服务连通性
                await ValidateServiceConnectivityAsync();

                _isInitialized = true;
                _logger.LogInformation("🎉 简化服务集成器初始化完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 简化服务集成器初始化失败");
                return false;
            }
        }

        /// <summary>
        /// 验证基础配置文件
        /// </summary>
        private async Task ValidateBaseConfigurationAsync()
        {
            try
            {
                _logger.LogInformation("🔍 验证基础配置文件");

                var baseConfigs = await StateService.GetBaseConfigsAsync();
                
                if (!baseConfigs.Any())
                {
                    _logger.LogWarning("⚠️ 基础配置为空，将创建默认配置");
                    // StateService 会自动创建默认配置
                    baseConfigs = await StateService.GetBaseConfigsAsync();
                }

                _logger.LogInformation($"✅ 基础配置验证完成: {baseConfigs.Count} 个配置");
                
                foreach (var config in baseConfigs)
                {
                    var isValid = await ConfigManager.ValidateBaseConfigAsync(config.Key);
                    _logger.LogInformation($"   - {config.Key}: {(isValid ? "✅ 有效" : "❌ 无效")}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 验证基础配置失败");
                throw;
            }
        }

        /// <summary>
        /// 初始化状态文件
        /// </summary>
        private async Task InitializeStateFilesAsync()
        {
            try
            {
                _logger.LogInformation("🔧 初始化状态文件");

                var contractStates = await StateService.GetContractStatesAsync();
                _logger.LogInformation($"📊 当前状态文件包含: {contractStates.Count} 个合约状态");

                // 清理无效状态
                await StateService.CleanupInvalidStatesAsync();

                _logger.LogInformation("✅ 状态文件初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 初始化状态文件失败");
                throw;
            }
        }

        /// <summary>
        /// 验证服务连通性
        /// </summary>
        private async Task ValidateServiceConnectivityAsync()
        {
            try
            {
                _logger.LogInformation("🔗 验证服务连通性");

                // 测试配置管理器
                var configNames = await ConfigManager.GetAvailableConfigNamesAsync();
                _logger.LogInformation($"✅ 配置管理器连通性正常: {configNames.Count} 个配置");

                // 测试监控服务状态
                var monitorStats = await MonitorService.GetMonitorStatsAsync();
                _logger.LogInformation($"✅ 监控服务连通性正常: {monitorStats.TotalContracts} 个合约");

                _logger.LogInformation("✅ 服务连通性验证完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 验证服务连通性失败");
                throw;
            }
        }

        #endregion

        #region 服务操作

        /// <summary>
        /// 启动自动监控
        /// </summary>
        public async Task<bool> StartMonitoringAsync()
        {
            if (!_isInitialized)
            {
                _logger.LogError("❌ 服务未初始化，无法启动监控");
                return false;
            }

            try
            {
                var result = await MonitorService.StartMonitoringAsync();
                _logger.LogInformation($"{(result ? "✅" : "❌")} 启动自动监控{(result ? "成功" : "失败")}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 启动自动监控异常");
                return false;
            }
        }

        /// <summary>
        /// 停止自动监控
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isInitialized)
            {
                _logger.LogError("❌ 服务未初始化，无法停止监控");
                return;
            }

            try
            {
                MonitorService.StopMonitoring();
                _logger.LogInformation("✅ 停止自动监控成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 停止自动监控异常");
            }
        }

        /// <summary>
        /// 重置所有执行状态
        /// </summary>
        public async Task<bool> ResetAllExecutionStatesAsync()
        {
            if (!_isInitialized)
            {
                _logger.LogError("❌ 服务未初始化，无法重置状态");
                return false;
            }

            try
            {
                await StateService.ResetAllExecutionStatesAsync();
                _logger.LogInformation("✅ 重置所有执行状态成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 重置所有执行状态失败");
                return false;
            }
        }

        /// <summary>
        /// 创建合约配置
        /// </summary>
        public async Task<bool> CreateContractConfigAsync(string symbol, string positionSide, string configName)
        {
            if (!_isInitialized)
            {
                _logger.LogError("❌ 服务未初始化，无法创建合约配置");
                return false;
            }

            try
            {
                await ConfigManager.CreateOrUpdateContractConfigAsync(symbol, positionSide, configName);
                _logger.LogInformation($"✅ 创建合约配置成功: {symbol}_{positionSide} -> {configName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 创建合约配置失败: {symbol}_{positionSide}");
                return false;
            }
        }

        #endregion

        #region 兼容性支持

        /// <summary>
        /// 为现有UI提供兼容性支持
        /// </summary>
        public void SetupLegacyUICompatibility(
            Action<SimplifiedMonitorStatusChangedEventArgs>? onMonitorStatusChanged = null,
            Action<SimplifiedExecutionResult>? onExecutionCompleted = null,
            Action<string>? onLogRequested = null)
        {
            if (!_isInitialized)
            {
                _logger.LogError("❌ 服务未初始化，无法设置UI兼容性");
                return;
            }

            try
            {
                UIAdapter.SetupEventForwarding(onMonitorStatusChanged, onExecutionCompleted, onLogRequested);
                _logger.LogInformation("✅ UI兼容性设置完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 设置UI兼容性失败");
            }
        }

        /// <summary>
        /// 获取现有系统兼容的统计信息
        /// </summary>
        public async Task<LegacyCompatibilityStats> GetLegacyStatsAsync()
        {
            if (!_isInitialized)
            {
                return new LegacyCompatibilityStats();
            }

            try
            {
                var uiStats = await UIAdapter.GetUIStatsAsync();
                
                return new LegacyCompatibilityStats
                {
                    IsMonitoringRunning = MonitorService.IsRunning,
                    TotalContracts = uiStats.MonitorStats?.TotalContracts ?? 0,
                    EnabledContracts = uiStats.MonitorStats?.EnabledContracts ?? 0,
                    TotalExecutedOperations = uiStats.MonitorStats?.TotalExecuted ?? 0,
                    ConfigUsageStats = uiStats.ConfigUsageStats ?? new Dictionary<string, int>(),
                    LastUpdateTime = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 获取兼容性统计信息失败");
                return new LegacyCompatibilityStats();
            }
        }

        #endregion

        #region 健康检查

        /// <summary>
        /// 执行系统健康检查
        /// </summary>
        public async Task<SimplifiedHealthCheckResult> PerformHealthCheckAsync()
        {
            var result = new SimplifiedHealthCheckResult
            {
                CheckTime = DateTime.Now,
                IsHealthy = true,
                Issues = new List<string>()
            };

            try
            {
                // 检查服务初始化状态
                if (!_isInitialized)
                {
                    result.Issues.Add("服务未初始化");
                    result.IsHealthy = false;
                }

                // 检查基础配置
                try
                {
                    var baseConfigs = await StateService.GetBaseConfigsAsync();
                    if (!baseConfigs.Any())
                    {
                        result.Issues.Add("基础配置为空");
                        result.IsHealthy = false;
                    }
                }
                catch (Exception ex)
                {
                    result.Issues.Add($"基础配置检查失败: {ex.Message}");
                    result.IsHealthy = false;
                }

                // 检查状态文件
                try
                {
                    var contractStates = await StateService.GetContractStatesAsync();
                    _logger.LogDebug($"状态文件检查: {contractStates.Count} 个合约状态");
                }
                catch (Exception ex)
                {
                    result.Issues.Add($"状态文件检查失败: {ex.Message}");
                    result.IsHealthy = false;
                }

                // 检查现有服务连接
                try
                {
                    if (_binanceService == null)
                    {
                        result.Issues.Add("BinanceService 未配置");
                        result.IsHealthy = false;
                    }
                    
                    if (_tradingExecutionService == null)
                    {
                        result.Issues.Add("TradingExecutionService 未配置");
                        result.IsHealthy = false;
                    }
                }
                catch (Exception ex)
                {
                    result.Issues.Add($"现有服务检查失败: {ex.Message}");
                    result.IsHealthy = false;
                }

                _logger.LogInformation($"{(result.IsHealthy ? "✅" : "❌")} 健康检查完成，发现问题: {result.Issues.Count} 个");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 健康检查执行异常");
                result.IsHealthy = false;
                result.Issues.Add($"健康检查异常: {ex.Message}");
                return result;
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try
            {
                _logger.LogInformation("🧹 开始清理简化服务集成器");

                // 停止监控服务
                if (_isInitialized && MonitorService != null)
                {
                    MonitorService.StopMonitoring();
                    MonitorService.Dispose();
                }

                // 清理服务提供程序
                if (_serviceProvider is IDisposable disposableProvider)
                {
                    disposableProvider.Dispose();
                }

                _logger.LogInformation("✅ 简化服务集成器清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 清理简化服务集成器失败");
            }
        }

        #endregion
    }

    /// <summary>
    /// 兼容性统计信息
    /// </summary>
    public class LegacyCompatibilityStats
    {
        public bool IsMonitoringRunning { get; set; }
        public int TotalContracts { get; set; }
        public int EnabledContracts { get; set; }
        public int TotalExecutedOperations { get; set; }
        public Dictionary<string, int> ConfigUsageStats { get; set; } = new();
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// 健康检查结果
    /// </summary>
    public class SimplifiedHealthCheckResult
    {
        public DateTime CheckTime { get; set; }
        public bool IsHealthy { get; set; }
        public List<string> Issues { get; set; } = new();
    }
} 