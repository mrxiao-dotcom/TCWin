using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json;
using System.Diagnostics;

namespace BinanceFuturesTrader.Services
{
    public class EnhancedErrorHandler
    {
        private static readonly Lazy<EnhancedErrorHandler> _instance = new(() => new EnhancedErrorHandler());
        public static EnhancedErrorHandler Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, DateTime> _errorFrequency = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly object _lockObject = new();

        private EnhancedErrorHandler() { }

        public async Task<T> SafeExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationName,
            int maxRetries = 3,
            int timeoutMs = 10000,
            T defaultValue = default(T))
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var retryCount = 0;

            while (retryCount <= maxRetries)
            {
                try
                {
                    await _semaphore.WaitAsync(1000, cts.Token);
                    try
                    {
                        return await operation(cts.Token);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    LogError($"操作超时: {operationName}, 尝试次数: {retryCount}");
                    break;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    LogError($"操作失败: {operationName}, 尝试 {retryCount}/{maxRetries + 1}", ex);

                    if (retryCount <= maxRetries)
                    {
                        var delay = Math.Min(1000 * (int)Math.Pow(2, retryCount - 1), 5000);
                        await Task.Delay(delay, CancellationToken.None);
                    }
                }
            }

            LogError($"操作最终失败: {operationName}, 返回默认值");
            return defaultValue;
        }

        public void SafeUIOperation(Action uiAction, string operationName)
        {
            try
            {
                if (Application.Current?.Dispatcher?.CheckAccess() == true)
                {
                    uiAction();
                }
                else
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        try
                        {
                            uiAction();
                        }
                        catch (Exception ex)
                        {
                            LogError($"UI操作失败: {operationName}", ex);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogError($"UI线程调用失败: {operationName}", ex);
            }
        }

        public void SafeDisposeResources(params IDisposable[] resources)
        {
            foreach (var resource in resources)
            {
                try
                {
                    resource?.Dispose();
                }
                catch (Exception ex)
                {
                    LogError("资源释放失败", ex);
                }
            }
        }

        public string GetUserFriendlyError(Exception ex)
        {
            return ex switch
            {
                TimeoutException => "操作超时，请检查网络连接后重试",
                UnauthorizedAccessException => "访问权限不足，请检查文件权限",
                FileNotFoundException => "配置文件不存在，系统将创建默认配置",
                JsonException => "配置文件格式错误，将重置为默认配置",
                TaskCanceledException => "操作已取消",
                HttpRequestException => "网络连接失败，请检查网络设置",
                _ => $"系统错误: {ex.Message}"
            };
        }

        private void LogError(string message, Exception ex = null)
        {
            var logMessage = ex != null ? $"{message}: {ex.Message}" : message;
            Debug.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} {logMessage}");
        }

        private void LogInfo(string message)
        {
            Debug.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} {message}");
        }
    }
}
