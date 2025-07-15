using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BinanceFuturesTrader.Views.AutoMonitor.Models
{
    /// <summary>
    /// 工作日志模型
    /// </summary>
    public class WorkLog : INotifyPropertyChanged
    {
        private DateTime _timestamp;
        private string _level;
        private string _message;
        private string _category;
        private string _exception;
        
        public WorkLog()
        {
            _timestamp = DateTime.Now;
            _level = "Info";
            _message = "";
            _category = "General";
            _exception = "";
        }
        
        public WorkLog(string level, string message) : this()
        {
            _level = level;
            _message = message;
        }
        
        public WorkLog(string level, string message, string category) : this(level, message)
        {
            _category = category;
        }
        
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 日志级别
        /// </summary>
        public string Level
        {
            get => _level;
            set { _level = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 日志消息
        /// </summary>
        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 日志分类
        /// </summary>
        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 异常信息
        /// </summary>
        public string Exception
        {
            get => _exception;
            set { _exception = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 格式化的时间文本
        /// </summary>
        public string TimeText => Timestamp.ToString("HH:mm:ss");
        
        /// <summary>
        /// 格式化的级别文本
        /// </summary>
        public string LevelText => $"[{Level}]";
        
        /// <summary>
        /// 级别颜色
        /// </summary>
        public string LevelColor => Level.ToUpper() switch
        {
            "ERROR" => "Red",
            "WARN" => "Orange",
            "INFO" => "LightGreen",
            "DEBUG" => "LightBlue",
            _ => "White"
        };
        
        /// <summary>
        /// 消息颜色
        /// </summary>
        public string MessageColor => Level.ToUpper() switch
        {
            "ERROR" => "LightPink",
            "WARN" => "LightYellow",
            _ => "LightGreen"
        };
        
        #region INotifyPropertyChanged 实现
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
} 