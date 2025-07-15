using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views.AutoMonitor.Components
{
    /// <summary>
    /// 控制面板组件
    /// </summary>
    public class ControlPanelComponent : INotifyPropertyChanged
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly ILogger _logger;
        private StackPanel _controlPanel;
        private Button _startStopButton;
        private Button _refreshButton;
        private Button _clearLogButton;
        private Button _configButton;
        
        public ControlPanelComponent(AutoMonitorDataModel dataModel, ILogger logger)
        {
            _dataModel = dataModel;
            _logger = logger;
            
            CreateControlPanel();
        }
        
        /// <summary>
        /// 创建控制面板
        /// </summary>
        private void CreateControlPanel()
        {
            _controlPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(5)
            };
            
            CreateButtons();
        }
        
        /// <summary>
        /// 创建按钮
        /// </summary>
        private void CreateButtons()
        {
            // 启动/停止按钮 - 增加宽度以避免文字被遮挡
            _startStopButton = new Button
            {
                Content = "启动监控",
                Width = 120,    // 从100增加到120
                Height = 35,    // 从30增加到35
                Margin = new Thickness(5, 0, 5, 0),
                FontSize = 12
            };
            
            // 刷新按钮 - 增加宽度
            _refreshButton = new Button
            {
                Content = "刷新数据",
                Width = 100,    // 从80增加到100
                Height = 35,    // 从30增加到35
                Margin = new Thickness(5, 0, 5, 0),
                FontSize = 12
            };
            
            // 清空日志按钮 - 增加宽度
            _clearLogButton = new Button
            {
                Content = "清空日志",
                Width = 100,    // 从80增加到100
                Height = 35,    // 从30增加到35
                Margin = new Thickness(5, 0, 5, 0),
                FontSize = 12
            };
            
            // 配置按钮 - 增加宽度
            _configButton = new Button
            {
                Content = "配置",
                Width = 80,     // 从60增加到80
                Height = 35,    // 从30增加到35
                Margin = new Thickness(5, 0, 5, 0),
                FontSize = 12
            };
            
            // 添加到面板
            _controlPanel.Children.Add(_startStopButton);
            _controlPanel.Children.Add(_refreshButton);
            _controlPanel.Children.Add(_clearLogButton);
            _controlPanel.Children.Add(_configButton);
        }
        
        /// <summary>
        /// 更新按钮状态
        /// </summary>
        public void UpdateButtonStates()
        {
            try
            {
                if (_startStopButton != null)
                {
                    var isRunning = _dataModel.MonitorStatus == "运行中";
                    _startStopButton.Content = isRunning ? "停止监控" : "启动监控";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新按钮状态时发生异常");
            }
        }
        
        /// <summary>
        /// 设置按钮点击事件
        /// </summary>
        public void SetButtonClickHandlers(
            RoutedEventHandler startStopHandler,
            RoutedEventHandler refreshHandler,
            RoutedEventHandler clearLogHandler,
            RoutedEventHandler configHandler)
        {
            try
            {
                _startStopButton.Click += startStopHandler;
                _refreshButton.Click += refreshHandler;
                _clearLogButton.Click += clearLogHandler;
                _configButton.Click += configHandler;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置按钮事件处理器时发生异常");
            }
        }
        
        /// <summary>
        /// 获取控制面板
        /// </summary>
        public StackPanel GetControlPanel() => _controlPanel;
        
        /// <summary>
        /// 获取启动/停止按钮
        /// </summary>
        public Button GetStartStopButton() => _startStopButton;
        
        /// <summary>
        /// 获取刷新按钮
        /// </summary>
        public Button GetRefreshButton() => _refreshButton;
        
        /// <summary>
        /// 获取清空日志按钮
        /// </summary>
        public Button GetClearLogButton() => _clearLogButton;
        
        /// <summary>
        /// 获取配置按钮
        /// </summary>
        public Button GetConfigButton() => _configButton;
        
        #region INotifyPropertyChanged 实现
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
} 