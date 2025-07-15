using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.ViewModels;

namespace BinanceFuturesTrader
{
    /// <summary>
    /// 服务配置类
    /// </summary>
    public static class ServiceConfiguration
    {
        /// <summary>
        /// 配置依赖注入服务
        /// </summary>
        public static void ConfigureServices(IServiceCollection services)
        {
            // 添加日志服务
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                                    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            });

            // 注册服务（单例模式）
            services.AddSingleton<IBinanceService, BinanceService>();
            services.AddSingleton<ITradingCalculationService, TradingCalculationService>();
            services.AddSingleton<AccountConfigService>();
            services.AddSingleton<TradingSettingsService>();
            services.AddSingleton<RecentContractsService>();
            services.AddSingleton<LogService>();
            
            // 注册新架构服务
            services.AddSingleton<IEventBus, EventBus>();
            services.AddSingleton<CooldownManager>();
            services.AddSingleton<StopOrderManager>();
            services.AddSingleton<UnifiedStateManager>();
            services.AddSingleton<IConfigValidationService, ConfigValidationService>();
            services.AddSingleton<AutoMonitorService>();
            services.AddSingleton<AutoMonitorPersistenceService>();
            services.AddSingleton<AutoMonitorConfigPersistenceService>();
            
            // 注册事件处理器
            services.AddSingleton<LoggingEventHandler>();
            services.AddSingleton<UIUpdateEventHandler>();
            services.AddSingleton<StatisticsEventHandler>();
            
            // 🔧 Phase 9: 注册增强错误处理服务
            services.AddSingleton<AutoMonitorErrorHandler>();
            services.AddSingleton<EnhancedErrorHandler>();

            // 注册ViewModel（瞬态模式，每次创建新实例）
            services.AddTransient<MainViewModel>();
            services.AddTransient<AccountConfigViewModel>();

            // 注册窗口（瞬态模式）
            services.AddTransient<MainWindow>();
            services.AddTransient<Views.AccountConfigWindow>();
        }
    }
} 