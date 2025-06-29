using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Services
{
    /// <summary>
    /// 自动盯盘系统专用错误处理器
    /// </summary>
    public class AutoMonitorErrorHandler
    {
        private readonly ILogger _logger;
        private readonly Dictionary<Type, string> _errorMessageMappings;
        private readonly Dictionary<string, string> _apiErrorMappings;

        public AutoMonitorErrorHandler(ILogger logger)
        {
            _logger = logger;
            _errorMessageMappings = InitializeErrorMappings();
            _apiErrorMappings = InitializeApiErrorMappings();
        }

        /// <summary>
        /// 处理并显示错误
        /// </summary>
        public void HandleError(Exception ex, string context, bool showToUser = true)
        {
            try
            {
                // 记录详细日志
                LogDetailedError(ex, context);

                // 生成用户友好的错误消息
                var userMessage = GenerateUserFriendlyMessage(ex, context);

                // 显示给用户（如果需要）
                if (showToUser)
                {
                    ShowErrorToUser(userMessage, GetErrorSeverity(ex));
                }
            }
            catch (Exception loggingEx)
            {
                // 防止日志记录本身出错
                _logger?.LogCritical(loggingEx, "错误处理器本身发生异常");
            }
        }

        /// <summary>
        /// 处理API相关错误
        /// </summary>
        public void HandleApiError(Exception ex, string apiCall, string symbol = "")
        {
            var context = $"API调用失败 - {apiCall}" + (string.IsNullOrEmpty(symbol) ? "" : $" (合约:{symbol})");
            
            _logger.LogError(ex, "🔴 {Context}", context);

            var userMessage = $"交易操作失败：{GetApiErrorMessage(ex)}\n\n";
            
            if (!string.IsNullOrEmpty(symbol))
                userMessage += $"影响合约：{symbol}\n";
                
            userMessage += "建议：\n";
            userMessage += "• 检查网络连接\n";
            userMessage += "• 确认API密钥权限\n";
            userMessage += "• 稍后重试操作\n";

            ShowErrorToUser(userMessage, ErrorSeverity.High);
        }

        /// <summary>
        /// 处理配置验证错误
        /// </summary>
        public void HandleConfigurationError(List<string> errors, bool showToUser = true)
        {
            if (!errors.Any()) return;

            var context = "配置验证失败";
            _logger.LogWarning("⚠️ {Context}: {ErrorCount}个错误", context, errors.Count);
            
            foreach (var error in errors)
            {
                _logger.LogWarning("  - {Error}", error);
            }

            if (showToUser)
            {
                var userMessage = "配置参数存在问题：\n\n";
                userMessage += string.Join("\n", errors.Select(e => $"• {e}"));
                userMessage += "\n\n请修正后重试。";

                ShowErrorToUser(userMessage, ErrorSeverity.Medium);
            }
        }

        /// <summary>
        /// 处理网络连接错误
        /// </summary>
        public void HandleNetworkError(Exception ex, string operation)
        {
            var context = $"网络操作失败 - {operation}";
            _logger.LogError(ex, "🌐 {Context}", context);

            var userMessage = $"网络连接问题导致操作失败：{operation}\n\n";
            userMessage += "可能原因：\n";
            userMessage += "• 网络连接不稳定\n";
            userMessage += "• 币安服务器维护\n";
            userMessage += "• 防火墙阻止连接\n\n";
            userMessage += "系统将自动重试，如问题持续请检查网络设置。";

            ShowErrorToUser(userMessage, ErrorSeverity.Medium);
        }

        /// <summary>
        /// 记录详细错误信息
        /// </summary>
        private void LogDetailedError(Exception ex, string context)
        {
            _logger.LogError(ex, "❌ 错误发生 - {Context}", context);
            
            // 记录异常详情
            _logger.LogDebug("异常类型: {ExceptionType}", ex.GetType().Name);
            _logger.LogDebug("错误消息: {Message}", ex.Message);
            
            if (ex.InnerException != null)
            {
                _logger.LogDebug("内部异常: {InnerException}", ex.InnerException.Message);
            }
            
            // 记录堆栈跟踪（仅在调试模式）
            _logger.LogTrace("堆栈跟踪:\n{StackTrace}", ex.StackTrace);
        }

        /// <summary>
        /// 生成用户友好的错误消息
        /// </summary>
        private string GenerateUserFriendlyMessage(Exception ex, string context)
        {
            // 尝试获取映射的错误消息
            var exceptionType = ex.GetType();
            if (_errorMessageMappings.TryGetValue(exceptionType, out var mappedMessage))
            {
                return $"{mappedMessage}\n\n操作：{context}";
            }

            // 检查是否是已知的API错误
            var apiMessage = GetApiErrorMessage(ex);
            if (!string.IsNullOrEmpty(apiMessage))
            {
                return $"交易操作遇到问题：{apiMessage}\n\n操作：{context}";
            }

            // 生成通用错误消息
            return GenerateGenericErrorMessage(ex, context);
        }

        /// <summary>
        /// 获取API错误的友好消息
        /// </summary>
        private string GetApiErrorMessage(Exception ex)
        {
            var message = ex.Message?.ToLower() ?? "";
            
            foreach (var mapping in _apiErrorMappings)
            {
                if (message.Contains(mapping.Key.ToLower()))
                {
                    return mapping.Value;
                }
            }

            // 检查常见的HTTP状态码错误
            if (message.Contains("401") || message.Contains("unauthorized"))
                return "API密钥验证失败，请检查密钥配置";
            
            if (message.Contains("403") || message.Contains("forbidden"))
                return "API权限不足，请检查密钥权限设置";
                
            if (message.Contains("429") || message.Contains("rate limit"))
                return "请求频率过高，系统将自动降低频率";
                
            if (message.Contains("500") || message.Contains("internal server"))
                return "币安服务器内部错误，请稍后重试";
                
            if (message.Contains("timeout"))
                return "请求超时，可能是网络延迟导致";

            return "未知的API错误，请查看详细日志";
        }

        /// <summary>
        /// 生成通用错误消息
        /// </summary>
        private string GenerateGenericErrorMessage(Exception ex, string context)
        {
            var message = $"操作执行失败：{context}\n\n";
            
            // 简化的错误描述
            var errorDesc = ex.Message;
            if (errorDesc.Length > 100)
            {
                errorDesc = errorDesc.Substring(0, 97) + "...";
            }
            
            message += $"错误详情：{errorDesc}\n\n";
            message += "建议的解决方案：\n";
            message += "• 检查网络连接是否正常\n";
            message += "• 确认账户配置无误\n";
            message += "• 重启程序后重试\n";
            message += "• 如问题持续，请联系技术支持";

            return message;
        }

        /// <summary>
        /// 显示错误给用户
        /// </summary>
        private void ShowErrorToUser(string message, ErrorSeverity severity)
        {
            try
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var icon = severity switch
                    {
                        ErrorSeverity.Low => MessageBoxImage.Information,
                        ErrorSeverity.Medium => MessageBoxImage.Warning,
                        ErrorSeverity.High => MessageBoxImage.Error,
                        ErrorSeverity.Critical => MessageBoxImage.Stop,
                        _ => MessageBoxImage.Warning
                    };

                    var title = severity switch
                    {
                        ErrorSeverity.Low => "提示",
                        ErrorSeverity.Medium => "警告",
                        ErrorSeverity.High => "错误",
                        ErrorSeverity.Critical => "严重错误",
                        _ => "通知"
                    };

                    MessageBox.Show(message, title, MessageBoxButton.OK, icon);
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "显示错误消息失败");
            }
        }

        /// <summary>
        /// 获取错误严重程度
        /// </summary>
        private ErrorSeverity GetErrorSeverity(Exception ex)
        {
            return ex switch
            {
                ArgumentException => ErrorSeverity.Medium,
                InvalidOperationException => ErrorSeverity.Medium,
                UnauthorizedAccessException => ErrorSeverity.High,
                TimeoutException => ErrorSeverity.Medium,
                System.Net.Http.HttpRequestException => ErrorSeverity.Medium,
                OutOfMemoryException => ErrorSeverity.Critical,
                StackOverflowException => ErrorSeverity.Critical,
                _ => ErrorSeverity.Medium
            };
        }

        /// <summary>
        /// 初始化错误映射
        /// </summary>
        private Dictionary<Type, string> InitializeErrorMappings()
        {
            return new Dictionary<Type, string>
            {
                [typeof(ArgumentException)] = "参数配置错误，请检查输入的数值是否正确",
                [typeof(ArgumentNullException)] = "缺少必要的配置信息，请完善配置后重试",
                [typeof(InvalidOperationException)] = "当前操作不被允许，请检查系统状态",
                [typeof(UnauthorizedAccessException)] = "权限不足，请检查API密钥配置",
                [typeof(TimeoutException)] = "操作超时，可能是网络延迟导致",
                [typeof(System.Net.Http.HttpRequestException)] = "网络请求失败，请检查网络连接",
                [typeof(FormatException)] = "数据格式错误，请检查输入格式",
                [typeof(OverflowException)] = "数值超出范围，请调整参数设置",
                [typeof(DivideByZeroException)] = "计算错误，检查到零除法，请检查配置参数",
                [typeof(OutOfMemoryException)] = "系统内存不足，请重启程序",
                [typeof(StackOverflowException)] = "系统堆栈溢出，请重启程序"
            };
        }

        /// <summary>
        /// 初始化API错误映射
        /// </summary>
        private Dictionary<string, string> InitializeApiErrorMappings()
        {
            return new Dictionary<string, string>
            {
                ["insufficient balance"] = "账户余额不足，无法执行操作",
                ["margin not sufficient"] = "保证金不足，请增加保证金或降低仓位",
                ["position not exists"] = "持仓不存在，可能已被手动平仓",
                ["order not exists"] = "订单不存在，可能已被取消或成交",
                ["symbol not found"] = "合约不存在，请检查合约名称",
                ["price too high"] = "价格过高，超出限制范围",
                ["price too low"] = "价格过低，超出限制范围",
                ["quantity too small"] = "数量过小，不满足最小交易要求",
                ["quantity too large"] = "数量过大，超出最大交易限制",
                ["leverage not supported"] = "不支持的杠杆倍数",
                ["market closed"] = "市场已关闭，无法交易",
                ["system maintenance"] = "系统维护中，请稍后重试",
                ["api key not found"] = "API密钥无效，请重新配置",
                ["signature not valid"] = "API签名验证失败，请检查密钥配置",
                ["timestamp outside window"] = "时间戳错误，请检查系统时间",
                ["too many requests"] = "请求过于频繁，请降低操作频率"
            };
        }
    }

    /// <summary>
    /// 错误严重程度枚举
    /// </summary>
    public enum ErrorSeverity
    {
        Low,        // 低 - 信息性错误
        Medium,     // 中 - 警告性错误  
        High,       // 高 - 严重错误
        Critical    // 极严重 - 系统级错误
    }

    /// <summary>
    /// 错误处理扩展方法
    /// </summary>
    public static class ErrorHandlerExtensions
    {
        /// <summary>
        /// 安全执行操作，自动处理异常
        /// </summary>
        public static async Task<T> SafeExecuteAsync<T>(
            this AutoMonitorErrorHandler errorHandler,
            Func<Task<T>> operation,
            string operationName,
            T defaultValue = default(T),
            bool showErrorToUser = true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                errorHandler.HandleError(ex, operationName, showErrorToUser);
                return defaultValue;
            }
        }

        /// <summary>
        /// 安全执行操作（无返回值）
        /// </summary>
        public static async Task SafeExecuteAsync(
            this AutoMonitorErrorHandler errorHandler,
            Func<Task> operation,
            string operationName,
            bool showErrorToUser = true)
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                errorHandler.HandleError(ex, operationName, showErrorToUser);
            }
        }

        /// <summary>
        /// 安全执行同步操作
        /// </summary>
        public static T SafeExecute<T>(
            this AutoMonitorErrorHandler errorHandler,
            Func<T> operation,
            string operationName,
            T defaultValue = default(T),
            bool showErrorToUser = true)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                errorHandler.HandleError(ex, operationName, showErrorToUser);
                return defaultValue;
            }
        }
    }
} 