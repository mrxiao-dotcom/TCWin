using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BinanceFuturesTrader
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 创建和配置主机
            _host = CreateHostBuilder().Build();

            // 设置全局异常处理
            SetupGlobalExceptionHandling();
            
            Console.WriteLine("🚀 应用程序启动，已启用全局异常处理和依赖注入");
            
            // 从依赖注入容器获取主窗口
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 释放主机资源
            _host?.Dispose();
            base.OnExit(e);
        }

        /// <summary>
        /// 创建主机构建器
        /// </summary>
        private static IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    ServiceConfiguration.ConfigureServices(services);
                });
        }

        private void SetupGlobalExceptionHandling()
        {
            // 处理UI线程异常
            this.DispatcherUnhandledException += Application_DispatcherUnhandledException;
            
            // 处理非UI线程异常
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            
            // 处理Task异常
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                Console.WriteLine($"🚨 UI线程未处理异常: {e.Exception.Message}");
                Console.WriteLine($"🚨 异常类型: {e.Exception.GetType().Name}");
                Console.WriteLine($"🚨 异常堆栈: {e.Exception.StackTrace}");

                var result = MessageBox.Show(
                    $"程序遇到一个未处理的异常，但已被安全拦截：\n\n" +
                    $"异常类型：{e.Exception.GetType().Name}\n" +
                    $"异常消息：{e.Exception.Message}\n\n" +
                    $"您可以选择：\n" +
                    $"• 点击「是」继续运行程序\n" +
                    $"• 点击「否」安全退出程序\n\n" +
                    $"建议保存当前工作并重启程序。",
                    "系统异常 - 已安全拦截",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // 用户选择继续，标记异常已处理
                    e.Handled = true;
                    Console.WriteLine("✅ 用户选择继续运行程序");
                }
                else
                {
                    // 用户选择退出
                    Console.WriteLine("🚪 用户选择安全退出程序");
                    Current.Shutdown();
                }
            }
            catch (Exception handlerEx)
            {
                Console.WriteLine($"❌ 异常处理器自身异常: {handlerEx.Message}");
                // 如果异常处理器出错，强制退出
                Current.Shutdown();
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception;
                Console.WriteLine($"🚨 非UI线程未处理异常: {exception?.Message ?? "未知异常"}");
                Console.WriteLine($"🚨 异常类型: {exception?.GetType().Name ?? "未知"}");
                Console.WriteLine($"🚨 异常堆栈: {exception?.StackTrace ?? "无堆栈信息"}");
                Console.WriteLine($"🚨 程序即将终止: {e.IsTerminating}");

                if (exception != null)
                {
                    // 尝试显示错误消息
                    Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            MessageBox.Show(
                                $"程序遇到严重异常，需要退出：\n\n" +
                                $"异常类型：{exception.GetType().Name}\n" +
                                $"异常消息：{exception.Message}\n\n" +
                                $"程序将自动关闭，请重新启动。",
                                "严重系统异常",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                        catch
                        {
                            // 如果无法显示消息框，静默处理
                        }
                    }));
                }
            }
            catch (Exception handlerEx)
            {
                Console.WriteLine($"❌ 非UI异常处理器自身异常: {handlerEx.Message}");
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                Console.WriteLine($"🚨 Task未处理异常: {e.Exception.Message}");
                Console.WriteLine($"🚨 异常类型: {e.Exception.GetType().Name}");
                Console.WriteLine($"🚨 异常堆栈: {e.Exception.StackTrace}");

                // 标记异常已观察，防止程序终止
                e.SetObserved();

                // 记录异常但不显示给用户（避免干扰）
                Console.WriteLine("✅ Task异常已被观察并记录");
            }
            catch (Exception handlerEx)
            {
                Console.WriteLine($"❌ Task异常处理器自身异常: {handlerEx.Message}");
            }
        }
    }
} 