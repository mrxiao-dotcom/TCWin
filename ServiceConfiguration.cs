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
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // 注册服务（单例模式）
            services.AddSingleton<IBinanceService, BinanceService>();
            services.AddSingleton<ITradingCalculationService, TradingCalculationService>();
            services.AddSingleton<AccountConfigService>();
            services.AddSingleton<TradingSettingsService>();
            services.AddSingleton<RecentContractsService>();
            services.AddSingleton<LogService>();

            // 注册ViewModel（瞬态模式，每次创建新实例）
            services.AddTransient<MainViewModel>();
            services.AddTransient<AccountConfigViewModel>();

            // 注册窗口（瞬态模式）
            services.AddTransient<MainWindow>();
            services.AddTransient<Views.AccountConfigWindow>();
        }
    }
} 