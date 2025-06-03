using System;
using System.IO;
using System.Threading.Tasks;

namespace BinanceFuturesTrader.Services
{
    public class LogService
    {
        private static readonly object _lockObject = new object();
        private static readonly string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "trading_log.txt");
        
        // 🎯 新增：存储最后的错误信息，用于保持弹窗与日志一致
        private static string _lastErrorMessage = string.Empty;
        
        static LogService()
        {
            // 确保日志目录存在
            var logDirectory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
        }
        
        public static void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}";
            
            // 输出到控制台
            Console.WriteLine(logEntry);
            
            // 异步写入文件，避免阻塞UI
            Task.Run(() => WriteToFile(logEntry));
        }
        
        public static void LogError(string message, Exception? ex = null)
        {
            var errorMessage = ex != null ? $"{message} - 异常: {ex.Message}\n堆栈: {ex.StackTrace}" : message;
            
            // 🎯 存储错误信息（去除图标，只保留纯文本）
            lock (_lockObject)
            {
                _lastErrorMessage = message; // 只存储原始消息，不包含前缀和异常详情
            }
            
            Log($"❌ 错误: {errorMessage}");
        }
        
        public static void LogInfo(string message)
        {
            Log($"ℹ️ 信息: {message}");
        }
        
        public static void LogSuccess(string message)
        {
            Log($"✅ 成功: {message}");
        }
        
        public static void LogWarning(string message)
        {
            Log($"⚠️ 警告: {message}");
        }
        
        public static void LogDebug(string message)
        {
            Log($"🔧 调试: {message}");
        }
        
        /// <summary>
        /// 获取最后的错误信息，用于保持弹窗与日志一致
        /// </summary>
        /// <returns>最后的错误信息，如果没有错误则返回空字符串</returns>
        public static string GetLastErrorMessage()
        {
            lock (_lockObject)
            {
                return _lastErrorMessage;
            }
        }
        
        /// <summary>
        /// 清空最后的错误信息
        /// </summary>
        public static void ClearLastErrorMessage()
        {
            lock (_lockObject)
            {
                _lastErrorMessage = string.Empty;
            }
        }
        
        private static void WriteToFile(string logEntry)
        {
            try
            {
                lock (_lockObject)
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // 文件写入失败时只输出到控制台，避免循环错误
                Console.WriteLine($"❌ 日志写入失败: {ex.Message}");
            }
        }
        
        public static void ClearLogFile()
        {
            try
            {
                lock (_lockObject)
                {
                    if (File.Exists(_logFilePath))
                    {
                        File.WriteAllText(_logFilePath, string.Empty);
                    }
                }
                Log("📄 日志文件已清空");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 清空日志文件失败: {ex.Message}");
            }
        }
        
        public static string GetLogFilePath()
        {
            return _logFilePath;
        }
    }
} 