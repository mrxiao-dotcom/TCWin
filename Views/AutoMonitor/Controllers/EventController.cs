using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views.AutoMonitor.Controllers
{
    /// <summary>
    /// 事件控制器
    /// 处理各种事件的触发和响应
    /// </summary>
    public class EventController : INotifyPropertyChanged, IDisposable
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly UIStateModel _uiStateModel;
        private readonly ILogger _logger;
        private bool _isDisposed = false;
        
        public EventController(AutoMonitorDataModel dataModel, UIStateModel uiStateModel, ILogger logger)
        {
            _dataModel = dataModel ?? throw new ArgumentNullException(nameof(dataModel));
            _uiStateModel = uiStateModel ?? throw new ArgumentNullException(nameof(uiStateModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            InitializeEventHandlers();
        }
        
        #region 事件定义
        
        /// <summary>
        /// 监控状态改变事件
        /// </summary>
        public event EventHandler<MonitorStatusChangedEventArgs>? MonitorStatusChanged;
        
        /// <summary>
        /// 数据更新事件
        /// </summary>
        public event EventHandler<DataUpdateEventArgs>? DataUpdated;
        
        /// <summary>
        /// 错误发生事件
        /// </summary>
        public event EventHandler<ErrorEventArgs>? ErrorOccurred;
        
        /// <summary>
        /// 警告发生事件
        /// </summary>
        public event EventHandler<WarningEventArgs>? WarningOccurred;
        
        /// <summary>
        /// 配置更改事件
        /// </summary>
        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
        
        #endregion
        
        #region 初始化方法
        
        /// <summary>
        /// 初始化事件处理器
        /// </summary>
        private void InitializeEventHandlers()
        {
            try
            {
                // 监听数据模型的属性变化
                _dataModel.PropertyChanged += OnDataModelPropertyChanged;
                _uiStateModel.PropertyChanged += OnUIStateModelPropertyChanged;
                
                _logger.LogDebug("事件控制器初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化事件处理器时发生异常");
                throw;
            }
        }
        
        #endregion
        
        #region 事件处理方法
        
        /// <summary>
        /// 数据模型属性变化处理
        /// </summary>
        private void OnDataModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                switch (e.PropertyName)
                {
                    case nameof(AutoMonitorDataModel.MonitorStatus):
                        OnMonitorStatusChanged(new MonitorStatusChangedEventArgs(_dataModel.MonitorStatus));
                        break;
                    
                    case nameof(AutoMonitorDataModel.ErrorCount):
                        if (_dataModel.ErrorCount > 0)
                        {
                            OnErrorOccurred(new ErrorEventArgs("系统错误计数增加"));
                        }
                        break;
                    
                    case nameof(AutoMonitorDataModel.WarningCount):
                        if (_dataModel.WarningCount > 0)
                        {
                            OnWarningOccurred(new WarningEventArgs("系统警告计数增加"));
                        }
                        break;
                    
                    case nameof(AutoMonitorDataModel.ConfigName):
                        OnConfigurationChanged(new ConfigurationChangedEventArgs(_dataModel.ConfigName));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理数据模型属性变化时发生异常");
            }
        }
        
        /// <summary>
        /// UI状态模型属性变化处理
        /// </summary>
        private void OnUIStateModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                // 处理UI状态变化
                _logger.LogDebug($"UI状态属性变化: {e.PropertyName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理UI状态模型属性变化时发生异常");
            }
        }
        
        #endregion
        
        #region 事件触发方法
        
        /// <summary>
        /// 触发监控状态改变事件
        /// </summary>
        protected virtual void OnMonitorStatusChanged(MonitorStatusChangedEventArgs e)
        {
            MonitorStatusChanged?.Invoke(this, e);
        }
        
        /// <summary>
        /// 触发数据更新事件
        /// </summary>
        protected virtual void OnDataUpdated(DataUpdateEventArgs e)
        {
            DataUpdated?.Invoke(this, e);
        }
        
        /// <summary>
        /// 触发错误事件
        /// </summary>
        protected virtual void OnErrorOccurred(ErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }
        
        /// <summary>
        /// 触发警告事件
        /// </summary>
        protected virtual void OnWarningOccurred(WarningEventArgs e)
        {
            WarningOccurred?.Invoke(this, e);
        }
        
        /// <summary>
        /// 触发配置更改事件
        /// </summary>
        protected virtual void OnConfigurationChanged(ConfigurationChangedEventArgs e)
        {
            ConfigurationChanged?.Invoke(this, e);
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 手动触发数据更新事件
        /// </summary>
        public void TriggerDataUpdate(string updateType, object data = null)
        {
            try
            {
                OnDataUpdated(new DataUpdateEventArgs(updateType, data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "触发数据更新事件时发生异常");
            }
        }
        
        /// <summary>
        /// 手动触发错误事件
        /// </summary>
        public void TriggerError(string errorMessage, Exception exception = null)
        {
            try
            {
                OnErrorOccurred(new ErrorEventArgs(errorMessage, exception));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "触发错误事件时发生异常");
            }
        }
        
        /// <summary>
        /// 手动触发警告事件
        /// </summary>
        public void TriggerWarning(string warningMessage)
        {
            try
            {
                OnWarningOccurred(new WarningEventArgs(warningMessage));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "触发警告事件时发生异常");
            }
        }
        
        #endregion
        
        #region IDisposable 实现
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        // 取消订阅事件
                        _dataModel.PropertyChanged -= OnDataModelPropertyChanged;
                        _uiStateModel.PropertyChanged -= OnUIStateModelPropertyChanged;
                        
                        _logger.LogDebug("事件控制器已释放");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "释放事件控制器时发生异常");
                    }
                }
                
                _isDisposed = true;
            }
        }
        
        #endregion
        
        #region INotifyPropertyChanged 实现
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
    
    #region 事件参数类
    
    /// <summary>
    /// 监控状态改变事件参数
    /// </summary>
    public class MonitorStatusChangedEventArgs : EventArgs
    {
        public string NewStatus { get; }
        
        public MonitorStatusChangedEventArgs(string newStatus)
        {
            NewStatus = newStatus;
        }
    }
    
    /// <summary>
    /// 数据更新事件参数
    /// </summary>
    public class DataUpdateEventArgs : EventArgs
    {
        public string UpdateType { get; }
        public object Data { get; }
        
        public DataUpdateEventArgs(string updateType, object data = null)
        {
            UpdateType = updateType;
            Data = data;
        }
    }
    
    /// <summary>
    /// 错误事件参数
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; }
        public Exception Exception { get; }
        
        public ErrorEventArgs(string errorMessage, Exception exception = null)
        {
            ErrorMessage = errorMessage;
            Exception = exception;
        }
    }
    
    /// <summary>
    /// 警告事件参数
    /// </summary>
    public class WarningEventArgs : EventArgs
    {
        public string WarningMessage { get; }
        
        public WarningEventArgs(string warningMessage)
        {
            WarningMessage = warningMessage;
        }
    }
    
    /// <summary>
    /// 配置更改事件参数
    /// </summary>
    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string ConfigName { get; }
        
        public ConfigurationChangedEventArgs(string configName)
        {
            ConfigName = configName;
        }
    }
    
    #endregion
} 