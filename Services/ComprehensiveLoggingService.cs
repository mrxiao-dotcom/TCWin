using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Collections.Generic;

namespace BinanceFuturesTrader.Services
{
    public class ComprehensiveLoggingService
    {
        private static readonly Lazy<ComprehensiveLoggingService> _instance = new(() => new ComprehensiveLoggingService());
        public static ComprehensiveLoggingService Instance => _instance.Value;

        private readonly string _logDirectory = "Logs";
        private readonly SemaphoreSlim _logSemaphore = new(1, 1);
        private readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private readonly Timer _flushTimer;

        public ObservableCollection<LogEntry> UILogEntries { get; } = new();
        public ObservableCollection<LogEntry> OperationLogs { get; } = new();
        public ObservableCollection<LogEntry> MonitoringLogs { get; } = new();
        public ObservableCollection<LogEntry> ErrorLogs { get; } = new();

        public event EventHandler<LogEntry> NewLogEntry;

        private ComprehensiveLoggingService()
        {
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);

            _flushTimer = new Timer(FlushLogsToFile, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public void LogInfo(string message, string category = "General")
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Info,
                Message = message,
                Category = category
            };
            
            AddLogEntry(entry);
        }

        public void LogError(string message, Exception exception = null, string category = "Error")
        {
            var fullMessage = exception != null 
                ? $"{message}: {exception.Message}"
                : message;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Error,
                Message = fullMessage,
                Category = category
            };
            
            AddLogEntry(entry);
        }

        public void LogOperation(string operation, string details = "", bool success = true)
        {
            var message = success 
                ? $"✅ {operation}" + (string.IsNullOrEmpty(details) ? "" : $" - {details}")
                : $"❌ {operation}" + (string.IsNullOrEmpty(details) ? "" : $" - {details}");

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = success ? LogLevel.Info : LogLevel.Error,
                Message = message,
                Category = "Operation"
            };
            
            AddLogEntry(entry);
        }

        private void AddLogEntry(LogEntry entry)
        {
            _logQueue.Enqueue(entry);

            Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                try
                {
                    UILogEntries.Insert(0, entry);
                    
                    switch (entry.Category.ToLower())
                    {
                        case "operation":
                            OperationLogs.Insert(0, entry);
                            break;
                        case "monitoring":
                            MonitoringLogs.Insert(0, entry);
                            break;
                        case "error":
                            ErrorLogs.Insert(0, entry);
                            break;
                    }

                    TrimLogCollection(UILogEntries, 1000);
                    TrimLogCollection(OperationLogs, 500);
                    TrimLogCollection(MonitoringLogs, 500);
                    TrimLogCollection(ErrorLogs, 200);

                    NewLogEntry?.Invoke(this, entry);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"日志UI更新失败: {ex.Message}");
                }
            });
        }

        private void TrimLogCollection(ObservableCollection<LogEntry> collection, int maxSize)
        {
            while (collection.Count > maxSize)
            {
                collection.RemoveAt(collection.Count - 1);
            }
        }

        private async void FlushLogsToFile(object state)
        {
            if (_logQueue.IsEmpty) return;

            try
            {
                await _logSemaphore.WaitAsync();

                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var logFile = Path.Combine(_logDirectory, $"app-{today}.log");
                
                var logEntries = new List<LogEntry>();
                while (_logQueue.TryDequeue(out var entry))
                {
                    logEntries.Add(entry);
                }

                if (logEntries.Count > 0)
                {
                    var logLines = logEntries.Select(entry => 
                        $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] [{entry.Category}] {entry.Message}");
                    
                    await File.AppendAllLinesAsync(logFile, logLines);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"日志刷新失败: {ex.Message}");
            }
            finally
            {
                _logSemaphore.Release();
            }
        }

        public void ClearLogs()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                UILogEntries.Clear();
                OperationLogs.Clear();
                MonitoringLogs.Clear();
                ErrorLogs.Clear();
            });
        }

        public async Task LogMonitorStartAsync(string message)
        {
            LogOperation("监控启动", message, true);
            await Task.CompletedTask;
        }

        public async Task LogMonitorStartAsync(string operation, string message, bool success)
        {
            LogOperation($"监控启动 - {operation}", message, success);
            await Task.CompletedTask;
        }

        public async Task LogMonitorStopAsync(string message)
        {
            LogOperation("监控停止", message, true);
            await Task.CompletedTask;
        }

        public async Task LogButtonClickAsync(string buttonName, string details = "")
        {
            LogOperation($"按钮点击 - {buttonName}", details, true);
            await Task.CompletedTask;
        }

        public async Task LogButtonClickAsync(string buttonName, string details, bool success)
        {
            LogOperation($"按钮点击 - {buttonName}", details, success);
            await Task.CompletedTask;
        }

        public async Task LogOperationAsync(string operation, string details = "")
        {
            LogOperation(operation, details, true);
            await Task.CompletedTask;
        }

        public async Task LogWarningAsync(string message, string category = "Warning")
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Warning,
                Message = message,
                Category = category
            };
            
            AddLogEntry(entry);
            await Task.CompletedTask;
        }

        public async Task LogErrorAsync(string message, Exception exception = null, string category = "Error")
        {
            LogError(message, exception, category);
            await Task.CompletedTask;
        }

        public async Task CleanupOldLogsAsync(int daysToKeep)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(_logDirectory, "app-*.log");
                
                foreach (var file in logFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("清理旧日志失败", ex);
            }
            await Task.CompletedTask;
        }

        public async Task ExportLogsAsync(string exportPath)
        {
            try
            {
                if (!Directory.Exists(exportPath))
                    Directory.CreateDirectory(exportPath);

                var exportFile = Path.Combine(exportPath, $"exported-logs-{DateTime.Now:yyyy-MM-dd-HHmmss}.log");
                var allEntries = UILogEntries.ToList();
                
                var logLines = allEntries.Select(entry => 
                    $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level}] [{entry.Category}] {entry.Message}");
                
                await File.WriteAllLinesAsync(exportFile, logLines);
            }
            catch (Exception ex)
            {
                LogError("导出日志失败", ex);
            }
        }

        public void Dispose()
        {
            _flushTimer?.Dispose();
            _logSemaphore?.Dispose();
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public string Category { get; set; }

        public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] {Message}";
        public string LevelDisplay => Level.ToString().ToUpper();
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Debug
    }
}
