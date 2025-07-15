using System;
using System.IO;
using System.Threading.Tasks;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views.AutoMonitor.Services
{
    /// <summary>
    /// 自动盯盘日志服务
    /// 处理所有日志相关操作
    /// </summary>
    public class LoggingService : IDisposable
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly ILogger _logger;
        private readonly object _logLock = new object();
        private readonly object _emergencyLogLock = new object();
        
        private readonly string _logDirectory;
        private readonly string _emergencyLogPath;
        
        public LoggingService(AutoMonitorDataModel dataModel, ILogger logger)
        {
            _dataModel = dataModel ?? throw new ArgumentNullException(nameof(dataModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // 设置日志目录
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "AutoMonitor");
            _emergencyLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emergency_log.txt");
            
            // 确保日志目录存在
            Directory.CreateDirectory(_logDirectory);
            
            _logger.LogDebug("日志服务初始化完成");
        }
        
        #region 实时日志方法
        
        /// <summary>
        /// 清空实时日志（线程安全版本）
        /// </summary>
        public void ClearRealTimeLog()
        {
            try
            {
                // 🔧 修复：确保在UI线程中清空集合
                if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    _dataModel.WorkLogs.Clear();
                }
                else
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        _dataModel.WorkLogs.Clear();
                    });
                }
                _logger.LogDebug("实时日志已清空");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清空实时日志时发生异常");
            }
        }
        
        /// <summary>
        /// 在实时日志中添加条目（线程安全版本）
        /// </summary>
        private void AddToRealTimeLog(string level, string message)
        {
            try
            {
                var logEntry = new WorkLog(level, message);
                
                // 🔧 修复：确保在UI线程中操作集合
                if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    // 已在UI线程中
                    AddToRealTimeLogCore(logEntry);
                }
                else
                {
                    // 在非UI线程中，调度到UI线程执行
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        AddToRealTimeLogCore(logEntry);
                    }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加实时日志条目时发生异常");
            }
        }

        /// <summary>
        /// 核心添加实时日志逻辑（必须在UI线程中调用）
        /// </summary>
        private void AddToRealTimeLogCore(WorkLog logEntry)
        {
            if (_dataModel.WorkLogs.Count > 1000)
            {
                _dataModel.WorkLogs.RemoveAt(0);
            }
            
            _dataModel.WorkLogs.Add(logEntry);
        }
        
        #endregion
        
        #region 操作日志方法
        
        /// <summary>
        /// 记录操作日志的异步方法（线程安全版本）
        /// </summary>
        /// <param name="operation">操作描述</param>
        /// <param name="details">详细信息</param>
        public async Task LogOperationAsync(string operation, string? details = null)
        {
            try
            {
                var logEntry = new WorkLog
                {
                    Timestamp = DateTime.Now,
                    Level = "Info",
                    Message = $"{operation} {details ?? ""}",
                    Category = "操作"
                };
                
                // 🔧 修复：确保在UI线程中操作集合
                if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    _dataModel.WorkLogs.Insert(0, logEntry);
                }
                else
                {
                    await System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        _dataModel.WorkLogs.Insert(0, logEntry);
                    });
                }
                
                // 记录到文件
                await WriteToFileAsync($"[操作] {operation} {details}");
            }
            catch (Exception ex)
            {
                // 确保日志服务本身的错误不影响主程序
                Console.WriteLine($"日志记录失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 记录错误日志
        /// </summary>
        /// <param name="error">错误描述</param>
        /// <param name="exception">异常对象</param>
        public async Task LogErrorAsync(string error, Exception exception = null)
        {
            try
            {
                var message = exception != null ? $"错误: {error} - {exception.Message}" : $"错误: {error}";
                AddToRealTimeLog("Error", message);
                _logger.LogError(exception, error);
                
                // 添加到工作日志
                var workLog = new WorkLog
                {
                    Timestamp = DateTime.Now,
                    Level = "Error",
                    Message = message,
                    Category = "错误",
                    Exception = exception?.ToString()
                };
                
                // 🔧 修复：确保在UI线程中操作集合
                if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    _dataModel.WorkLogs.Insert(0, workLog);
                    
                    // 限制工作日志数量
                    while (_dataModel.WorkLogs.Count > 500)
                    {
                        _dataModel.WorkLogs.RemoveAt(_dataModel.WorkLogs.Count - 1);
                    }
                }
                else
                {
                    await System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        _dataModel.WorkLogs.Insert(0, workLog);
                        
                        // 限制工作日志数量
                        while (_dataModel.WorkLogs.Count > 500)
                        {
                            _dataModel.WorkLogs.RemoveAt(_dataModel.WorkLogs.Count - 1);
                        }
                    });
                }
                
                await WriteWorkLogAsync(workLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "记录错误日志时发生异常");
                WriteEmergencyLog($"记录错误日志失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 记录警告日志
        /// </summary>
        /// <param name="warning">警告描述</param>
        public async Task LogWarningAsync(string warning)
        {
            try
            {
                var message = $"警告: {warning}";
                AddToRealTimeLog("Warning", message);
                _logger.LogWarning(warning);
                
                // 添加到工作日志
                var workLog = new WorkLog
                {
                    Timestamp = DateTime.Now,
                    Level = "Warning",
                    Message = message,
                    Category = "警告"
                };
                
                // 🔧 修复：确保在UI线程中操作集合
                if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    _dataModel.WorkLogs.Insert(0, workLog);
                    
                    // 限制工作日志数量
                    while (_dataModel.WorkLogs.Count > 500)
                    {
                        _dataModel.WorkLogs.RemoveAt(_dataModel.WorkLogs.Count - 1);
                    }
                }
                else
                {
                    await System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        _dataModel.WorkLogs.Insert(0, workLog);
                        
                        // 限制工作日志数量
                        while (_dataModel.WorkLogs.Count > 500)
                        {
                            _dataModel.WorkLogs.RemoveAt(_dataModel.WorkLogs.Count - 1);
                        }
                    });
                }
                
                await WriteWorkLogAsync(workLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "记录警告日志时发生异常");
                WriteEmergencyLog($"记录警告日志失败: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 文件日志方法
        
        /// <summary>
        /// 写入文件日志
        /// </summary>
        private async Task WriteToFileAsync(string message)
        {
            try
            {
                var fileName = $"AutoMonitor_{DateTime.Now:yyyyMMdd}.log";
                var filePath = Path.Combine(_logDirectory, fileName);
                
                await File.AppendAllTextAsync(filePath, message + Environment.NewLine);
            }
            catch (Exception ex)
            {
                WriteEmergencyLog($"写入文件日志失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 写入工作日志文件
        /// </summary>
        private async Task WriteWorkLogAsync(WorkLog workLog)
        {
            try
            {
                var fileName = $"WorkLog_{DateTime.Now:yyyyMMdd}.log";
                var filePath = Path.Combine(_logDirectory, fileName);
                
                var logEntry = $"[{workLog.Timestamp:yyyy-MM-dd HH:mm:ss}] [{workLog.Level}] [{workLog.Category}] {workLog.Message}";
                if (!string.IsNullOrEmpty(workLog.Exception))
                {
                    logEntry += Environment.NewLine + workLog.Exception;
                }
                
                await File.AppendAllTextAsync(filePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                WriteEmergencyLog($"写入工作日志失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 写入紧急日志
        /// </summary>
        private void WriteEmergencyLog(string message)
        {
            try
            {
                lock (_emergencyLogLock)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logEntry = $"[{timestamp}] {message}";
                    File.AppendAllText(_emergencyLogPath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // 紧急日志写入失败，忽略异常
            }
        }
        
        #endregion
        
        #region 日志管理方法
        
        /// <summary>
        /// 清理过期日志文件
        /// </summary>
        public async Task CleanupOldLogsAsync(int retentionDays = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var logFiles = Directory.GetFiles(_logDirectory, "*.log");
                
                foreach (var file in logFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                        _logger.LogDebug($"删除过期日志文件: {file}");
                    }
                }
                
                await LogOperationAsync($"清理了 {retentionDays} 天前的日志文件");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期日志时发生异常");
                await LogErrorAsync("清理过期日志失败", ex);
            }
        }
        
        /// <summary>
        /// 导出日志
        /// </summary>
        /// <param name="exportPath">导出路径</param>
        public async Task ExportLogsAsync(string exportPath)
        {
            try
            {
                var exportDir = Path.Combine(exportPath, $"AutoMonitor_Logs_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(exportDir);
                
                // 复制日志文件
                var logFiles = Directory.GetFiles(_logDirectory, "*.log");
                foreach (var file in logFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(exportDir, fileName);
                    File.Copy(file, destPath);
                }
                
                // 复制紧急日志
                if (File.Exists(_emergencyLogPath))
                {
                    var emergencyDestPath = Path.Combine(exportDir, "emergency_log.txt");
                    File.Copy(_emergencyLogPath, emergencyDestPath);
                }
                
                await LogOperationAsync($"日志导出完成: {exportDir}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出日志时发生异常");
                await LogErrorAsync("导出日志失败", ex);
            }
        }
        
        #endregion
        
        #region IDisposable 实现
        
        public void Dispose()
        {
            try
            {
                // 写入服务停止日志
                WriteEmergencyLog("日志服务正在停止");
                _logger.LogDebug("日志服务已释放");
            }
            catch
            {
                // 忽略异常
            }
        }
        
        #endregion
    }
} 