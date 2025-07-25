using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views.AutoMonitor.Controllers
{
    /// <summary>
    /// 定时器控制器
    /// 管理所有定时器相关的操作
    /// </summary>
    public class TimerController : IDisposable
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly UIStateModel _uiStateModel;
        private readonly ILogger _logger;
        
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _countdownTimer;
        private readonly DispatcherTimer _titleTimer;
        private readonly DispatcherTimer _statusTimer;
        
        private bool _disposed = false;
        
        public TimerController(
            AutoMonitorDataModel dataModel,
            UIStateModel uiStateModel,
            ILogger logger)
        {
            _dataModel = dataModel ?? throw new ArgumentNullException(nameof(dataModel));
            _uiStateModel = uiStateModel ?? throw new ArgumentNullException(nameof(uiStateModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // 初始化定时器
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _titleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            
            // 设置事件处理程序
            SetupTimerEvents();
            
            _logger.LogDebug("定时器控制器初始化完成");
        }
        
        #region 定时器事件设置
        
        private void SetupTimerEvents()
        {
            _refreshTimer.Tick += async (s, e) => await OnRefreshTimerTickAsync();
            _countdownTimer.Tick += OnCountdownTimerTick;
            _titleTimer.Tick += OnTitleTimerTick;
            _statusTimer.Tick += OnStatusTimerTick;
        }
        
        #endregion
        
        #region 定时器控制方法
        
        /// <summary>
        /// 启动所有定时器
        /// </summary>
        public void StartAllTimers()
        {
            try
            {
                _refreshTimer.Start();
                _countdownTimer.Start();
                _titleTimer.Start();
                _statusTimer.Start();
                
                _logger.LogInformation("所有定时器已启动");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动定时器时发生异常");
                throw;
            }
        }
        
        /// <summary>
        /// 停止所有定时器
        /// </summary>
        public void StopAllTimers()
        {
            try
            {
                _refreshTimer.Stop();
                _countdownTimer.Stop();
                _titleTimer.Stop();
                _statusTimer.Stop();
                
                _logger.LogInformation("所有定时器已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止定时器时发生异常");
                throw;
            }
        }
        
        /// <summary>
        /// 启动刷新定时器
        /// </summary>
        public void StartRefreshTimer()
        {
            _refreshTimer.Start();
            _logger.LogDebug("刷新定时器已启动");
        }
        
        /// <summary>
        /// 停止刷新定时器
        /// </summary>
        public void StopRefreshTimer()
        {
            _refreshTimer.Stop();
            _logger.LogDebug("刷新定时器已停止");
        }
        
        /// <summary>
        /// 设置刷新间隔
        /// </summary>
        /// <param name="intervalSeconds">间隔秒数</param>
        public void SetRefreshInterval(int intervalSeconds)
        {
            if (intervalSeconds < 1)
            {
                throw new ArgumentException("间隔时间必须大于0秒");
            }
            
            _refreshTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);
            _logger.LogDebug($"刷新间隔设置为 {intervalSeconds} 秒");
        }
        
        #endregion
        
        #region 定时器事件处理
        
        /// <summary>
        /// 刷新定时器事件
        /// </summary>
        private async Task OnRefreshTimerTickAsync()
        {
            try
            {
                // 触发数据刷新事件
                DataRefreshRequested?.Invoke(this, EventArgs.Empty);
                
                // 更新运行时间
                _dataModel.UpdateRunningTime();
                
                // 更新统计数据
                _dataModel.UpdateStatistics();
                
                _logger.LogDebug("定时刷新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "定时刷新时发生异常");
            }
        }
        
        /// <summary>
        /// 倒计时定时器事件
        /// </summary>
        private void OnCountdownTimerTick(object sender, EventArgs e)
        {
            try
            {
                // 计算下次扫描的倒计时
                var remaining = (_dataModel.NextScanDateTime - DateTime.Now).TotalSeconds;
                if (remaining < 0)
                {
                    remaining = 0;
                    // 🔧 修复：重置下次扫描时间，使用正确的扫描间隔
                    var scanInterval = _dataModel.ScanIntervalSeconds > 0 ? _dataModel.ScanIntervalSeconds : 30;
                    _dataModel.NextScanDateTime = DateTime.Now.AddSeconds(scanInterval);
                    _logger.LogDebug($"⏰ 重置倒计时，扫描间隔: {scanInterval}秒");
                }
                
                var minutes = (int)remaining / 60;
                var seconds = (int)remaining % 60;
                _dataModel.ScanCountdownDisplay = $"{minutes:00}:{seconds:00}";
                
                _logger.LogTrace($"倒计时更新: {_dataModel.ScanCountdownDisplay}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "倒计时更新时发生异常");
            }
        }
        
        /// <summary>
        /// 标题定时器事件
        /// </summary>
        private void OnTitleTimerTick(object sender, EventArgs e)
        {
            try
            {
                // 更新标题显示的时间
                var time = DateTime.Now.ToString("HH:mm:ss");
                
                // 触发标题更新事件
                TitleUpdateRequested?.Invoke(this, new TitleUpdateEventArgs(time));
                
                _logger.LogTrace($"标题时间更新: {time}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标题更新时发生异常");
            }
        }
        
        /// <summary>
        /// 状态定时器事件
        /// </summary>
        private void OnStatusTimerTick(object sender, EventArgs e)
        {
            try
            {
                // 更新状态显示
                _dataModel.LastUpdateTime = DateTime.Now;
                
                // 检查系统状态
                CheckSystemStatus();
                
                _logger.LogTrace("状态更新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "状态更新时发生异常");
            }
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 检查系统状态
        /// </summary>
        private void CheckSystemStatus()
        {
            try
            {
                // 检查是否有异常状态
                if (_dataModel.MonitorStatus == "运行中")
                {
                    // 检查是否长时间无更新
                    var timeSinceLastUpdate = DateTime.Now - _dataModel.LastUpdateTime;
                    if (timeSinceLastUpdate.TotalMinutes > 5)
                    {
                        _logger.LogWarning("系统可能出现异常，长时间无数据更新");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查系统状态时发生异常");
            }
        }
        
        #endregion
        
        #region 事件定义
        
        /// <summary>
        /// 数据刷新请求事件
        /// </summary>
        public event EventHandler DataRefreshRequested;
        
        /// <summary>
        /// 标题更新请求事件
        /// </summary>
        public event EventHandler<TitleUpdateEventArgs> TitleUpdateRequested;
        
        #endregion
        
        #region IDisposable 实现
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        _refreshTimer?.Stop();
                        _countdownTimer?.Stop();
                        _titleTimer?.Stop();
                        _statusTimer?.Stop();
                        
                        _logger.LogDebug("定时器控制器已释放");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "释放定时器控制器时发生异常");
                    }
                }
                
                _disposed = true;
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        #endregion
    }
    
    /// <summary>
    /// 标题更新事件参数
    /// </summary>
    public class TitleUpdateEventArgs : EventArgs
    {
        public string Time { get; }
        
        public TitleUpdateEventArgs(string time)
        {
            Time = time;
        }
    }
} 