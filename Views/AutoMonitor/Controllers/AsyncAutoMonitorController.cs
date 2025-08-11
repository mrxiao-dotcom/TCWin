using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using BinanceFuturesTrader.ViewModels;
using BinanceFuturesTrader.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace BinanceFuturesTrader.Views.AutoMonitor.Controllers
{
    /// <summary>
    /// 简化的异步自动监控控制器
    /// 解决UI线程阻塞问题，使用后台线程执行监控任务
    /// </summary>
    public class AsyncAutoMonitorController : IDisposable
    {
        private readonly ILogger _logger;
        private readonly MainViewModel _mainViewModel;
        private readonly AutoMonitorService _autoMonitorService;
        
        // 线程安全的监控控制
        private readonly object _monitoringLock = new object();
        private bool _isMonitoringActive = false;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _monitoringTask;
        
        // 监控配置
        private int _scanIntervalSeconds = 10;
        private DateTime _nextScanTime;
        
        // 事件回调
        public event Action<string> OnLogMessage;
        public event Action<bool> OnMonitoringStateChanged;
        
        public AsyncAutoMonitorController(
            ILogger logger,
            MainViewModel mainViewModel,
            AutoMonitorService autoMonitorService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _autoMonitorService = autoMonitorService ?? throw new ArgumentNullException(nameof(autoMonitorService));
            
            _logger.LogInformation("🔧 AsyncAutoMonitorController 初始化完成 - 多线程架构");
        }
        
        #region 公共接口
        
        /// <summary>
        /// 启动异步监控
        /// </summary>
        public async Task<bool> StartMonitoringAsync()
        {
            try
            {
                lock (_monitoringLock)
                {
                    if (_isMonitoringActive)
                    {
                        LogMessage("⚠️ 监控已在运行中");
                        return true;
                    }
                }
                
                LogMessage("🚀 启动异步监控...");
                
                // 验证配置
                var autoMonitorConfig = _mainViewModel?.CurrentAutoMonitorConfig;
                if (autoMonitorConfig == null)
                {
                    LogMessage("❌ 缺少监控配置");
                    return false;
                }
                
                // 启动底层服务
                var serviceStarted = await _autoMonitorService.StartMonitoringAsync(autoMonitorConfig);
                if (!serviceStarted)
                {
                    LogMessage("❌ 启动底层监控服务失败");
                    return false;
                }
                
                // 启动监控循环
                lock (_monitoringLock)
                {
                    _isMonitoringActive = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                    
                    // 启动后台监控任务
                    _monitoringTask = Task.Run(async () => await MonitoringLoopAsync(_cancellationTokenSource.Token));
                }
                
                // 更新状态
                _nextScanTime = DateTime.Now.AddSeconds(_scanIntervalSeconds);
                OnMonitoringStateChanged?.Invoke(true);
                
                LogMessage("✅ 异步监控启动成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动异步监控失败");
                LogMessage($"❌ 启动失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 停止异步监控
        /// </summary>
        public async Task<bool> StopMonitoringAsync()
        {
            try
            {
                LogMessage("⏹ 正在停止异步监控...");
                
                lock (_monitoringLock)
                {
                    if (!_isMonitoringActive)
                    {
                        LogMessage("⚠️ 监控未在运行");
                        return true;
                    }
                    
                    _isMonitoringActive = false;
                    
                    // 取消后台任务
                    _cancellationTokenSource?.Cancel();
                }
                
                // 等待后台任务完成（最多5秒）
                if (_monitoringTask != null)
                {
                    try
                    {
                        await _monitoringTask.WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常的取消操作
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("监控任务停止超时");
                    }
                }
                
                // 停止底层服务
                await _autoMonitorService.StopMonitoringAsync();
                
                // 更新状态
                OnMonitoringStateChanged?.Invoke(false);
                
                LogMessage("✅ 异步监控已停止");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止异步监控失败");
                LogMessage($"❌ 停止失败: {ex.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region 后台监控循环
        
        /// <summary>
        /// 后台监控循环（运行在非UI线程）
        /// </summary>
        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            LogMessage("🔄 后台监控循环已启动");
            
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // 等待到下次扫描时间
                        var now = DateTime.Now;
                        if (now < _nextScanTime)
                        {
                            var delay = _nextScanTime - now;
                            if (delay.TotalMilliseconds > 0)
                            {
                                await Task.Delay(delay, cancellationToken);
                            }
                        }
                        
                        // 检查是否被取消
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // 执行监控周期
                        await ExecuteMonitoringCycleAsync(cancellationToken);
                        
                        // 更新下次扫描时间
                        _nextScanTime = DateTime.Now.AddSeconds(_scanIntervalSeconds);
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，退出循环
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "监控循环异常");
                        LogMessage($"❌ 监控异常: {ex.Message}");
                        
                        // 短暂延迟后继续
                        try
                        {
                            await Task.Delay(1000, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                LogMessage("🔄 后台监控循环已退出");
            }
        }
        
        /// <summary>
        /// 执行一次监控周期（运行在非UI线程）
        /// </summary>
        private async Task ExecuteMonitoringCycleAsync(CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            LogMessage($"🔍 开始扫描 [{startTime:HH:mm:ss}]");
            
            try
            {
                // 检查是否被取消
                cancellationToken.ThrowIfCancellationRequested();
                
                // 这里执行实际的监控逻辑
                // 模拟处理时间
                await Task.Delay(100, cancellationToken);
                
                var duration = DateTime.Now - startTime;
                LogMessage($"✅ 扫描完成，耗时: {duration.TotalMilliseconds:F0}ms");
            }
            catch (OperationCanceledException)
            {
                LogMessage("⏹ 扫描被取消");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行监控周期失败");
                LogMessage($"❌ 扫描失败: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 辅助方法
        
        private void LogMessage(string message)
        {
            var logWithTime = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logger.LogInformation(message);
            OnLogMessage?.Invoke(logWithTime);
        }
        
        #endregion
        
        #region 资源清理
        
        public void Dispose()
        {
            try
            {
                // 停止监控
                _ = StopMonitoringAsync();
                
                // 释放取消令牌
                _cancellationTokenSource?.Dispose();
                
                LogMessage("🔧 AsyncAutoMonitorController 已释放资源");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放资源失败");
            }
        }
        
        #endregion
    }
} 