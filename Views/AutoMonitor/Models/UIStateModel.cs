using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace BinanceFuturesTrader.Views.AutoMonitor.Models
{
    /// <summary>
    /// UI状态管理模型
    /// 管理所有UI相关的状态和样式
    /// </summary>
    public class UIStateModel : INotifyPropertyChanged
    {
        #region 状态卡片样式
        
        private SolidColorBrush _statusCardBackground = new(Colors.LightGray);
        private SolidColorBrush _statusIconColor = new(Colors.Gray);
        private SolidColorBrush _statusTextColor = new(Colors.Black);
        
        public SolidColorBrush StatusCardBackground
        {
            get => _statusCardBackground;
            set { _statusCardBackground = value; OnPropertyChanged(); }
        }
        
        public SolidColorBrush StatusIconColor
        {
            get => _statusIconColor;
            set { _statusIconColor = value; OnPropertyChanged(); }
        }
        
        public SolidColorBrush StatusTextColor
        {
            get => _statusTextColor;
            set { _statusTextColor = value; OnPropertyChanged(); }
        }
        
        #endregion
        
        #region 按钮状态
        
        private string _toggleButtonText = "启动盯盘";
        private SolidColorBrush _toggleButtonBackground = new(Colors.Green);
        private bool _toggleButtonEnabled = true;
        private string _toggleButtonTooltip = "开始自动盯盘监控";
        
        public string ToggleButtonText
        {
            get => _toggleButtonText;
            set { _toggleButtonText = value; OnPropertyChanged(); }
        }
        
        public SolidColorBrush ToggleButtonBackground
        {
            get => _toggleButtonBackground;
            set { _toggleButtonBackground = value; OnPropertyChanged(); }
        }
        
        public bool ToggleButtonEnabled
        {
            get => _toggleButtonEnabled;
            set { _toggleButtonEnabled = value; OnPropertyChanged(); }
        }
        
        public string ToggleButtonTooltip
        {
            get => _toggleButtonTooltip;
            set { _toggleButtonTooltip = value; OnPropertyChanged(); }
        }
        
        #endregion
        
        #region 数据表格状态
        
        private bool _isDataGridReadOnly = false;
        private bool _editButtonEnabled = true;
        
        public bool IsDataGridReadOnly
        {
            get => _isDataGridReadOnly;
            set { _isDataGridReadOnly = value; OnPropertyChanged(); }
        }
        
        public bool EditButtonEnabled
        {
            get => _editButtonEnabled;
            set { _editButtonEnabled = value; OnPropertyChanged(); }
        }
        
        #endregion
        
        #region 日志显示状态
        
        private SolidColorBrush _autoScrollButtonColor = new(Colors.Green);
        
        public SolidColorBrush AutoScrollButtonColor
        {
            get => _autoScrollButtonColor;
            set { _autoScrollButtonColor = value; OnPropertyChanged(); }
        }
        
        #endregion
        
        #region 监控状态文本
        
        private string _monitorStatusText = "未启动";
        
        public string MonitorStatusText
        {
            get => _monitorStatusText;
            set { _monitorStatusText = value; OnPropertyChanged(); }
        }
        
        #endregion
        
        #region 方法
        
        /// <summary>
        /// 设置监控启动状态的UI
        /// </summary>
        public void SetMonitoringState()
        {
            ToggleButtonText = "停止盯盘";
            ToggleButtonBackground = new SolidColorBrush(Colors.Red);
            ToggleButtonTooltip = "停止自动盯盘监控";
            IsDataGridReadOnly = true;
            EditButtonEnabled = false;
            MonitorStatusText = "运行中";
            
            // 状态卡片变为活跃状态
            StatusCardBackground = new SolidColorBrush(Colors.LightGreen);
            StatusIconColor = new SolidColorBrush(Colors.Green);
            StatusTextColor = new SolidColorBrush(Colors.DarkGreen);
        }
        
        /// <summary>
        /// 设置监控停止状态的UI
        /// </summary>
        public void SetStoppedState()
        {
            ToggleButtonText = "启动盯盘";
            ToggleButtonBackground = new SolidColorBrush(Colors.Green);
            ToggleButtonTooltip = "开始自动盯盘监控";
            IsDataGridReadOnly = false;
            EditButtonEnabled = true;
            MonitorStatusText = "未启动";
            
            // 状态卡片变为非活跃状态
            StatusCardBackground = new SolidColorBrush(Colors.LightGray);
            StatusIconColor = new SolidColorBrush(Colors.Gray);
            StatusTextColor = new SolidColorBrush(Colors.Black);
        }
        
        /// <summary>
        /// 设置错误状态的UI
        /// </summary>
        public void SetErrorState()
        {
            ToggleButtonText = "启动盯盘";
            ToggleButtonBackground = new SolidColorBrush(Colors.Orange);
            ToggleButtonTooltip = "发生错误，点击重新启动";
            IsDataGridReadOnly = false;
            EditButtonEnabled = true;
            MonitorStatusText = "错误";
            
            // 状态卡片变为错误状态
            StatusCardBackground = new SolidColorBrush(Colors.LightPink);
            StatusIconColor = new SolidColorBrush(Colors.Red);
            StatusTextColor = new SolidColorBrush(Colors.DarkRed);
        }
        
        /// <summary>
        /// 设置加载状态的UI
        /// </summary>
        public void SetLoadingState()
        {
            ToggleButtonEnabled = false;
            EditButtonEnabled = false;
            MonitorStatusText = "加载中...";
            
            // 状态卡片变为加载状态
            StatusCardBackground = new SolidColorBrush(Colors.LightBlue);
            StatusIconColor = new SolidColorBrush(Colors.Blue);
            StatusTextColor = new SolidColorBrush(Colors.DarkBlue);
        }
        
        /// <summary>
        /// 恢复正常状态的UI
        /// </summary>
        public void SetNormalState()
        {
            ToggleButtonEnabled = true;
            EditButtonEnabled = true;
        }
        
        /// <summary>
        /// 更新自动滚动按钮颜色
        /// </summary>
        /// <param name="enabled">是否启用自动滚动</param>
        public void UpdateAutoScrollButtonColor(bool enabled)
        {
            AutoScrollButtonColor = new SolidColorBrush(enabled ? Colors.Green : Colors.Gray);
        }
        
        #endregion
        
        #region INotifyPropertyChanged 实现
        
        /// <summary>
        /// 属性变化通知事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
} 