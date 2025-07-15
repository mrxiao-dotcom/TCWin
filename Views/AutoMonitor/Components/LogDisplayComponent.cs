using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views.AutoMonitor.Components
{
    /// <summary>
    /// 日志显示组件
    /// </summary>
    public class LogDisplayComponent : INotifyPropertyChanged
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly ILogger _logger;
        private ListBox _logListBox;
        private ScrollViewer _scrollViewer;
        
        public LogDisplayComponent(AutoMonitorDataModel dataModel, ILogger logger)
        {
            _dataModel = dataModel;
            _logger = logger;
            
            CreateLogDisplay();
        }
        
        /// <summary>
        /// 创建日志显示控件
        /// </summary>
        private void CreateLogDisplay()
        {
            _logListBox = new ListBox
            {
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                Background = System.Windows.Media.Brushes.Black,
                Foreground = System.Windows.Media.Brushes.LightGreen,
                BorderThickness = new Thickness(1),
                BorderBrush = System.Windows.Media.Brushes.Gray
            };
            
            // 创建数据模板
            var dataTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            
            // 时间
            var timeFactory = new FrameworkElementFactory(typeof(TextBlock));
            timeFactory.SetBinding(TextBlock.TextProperty, new Binding("TimeText"));
            timeFactory.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray);
            timeFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 5, 0));
            timeFactory.SetValue(TextBlock.WidthProperty, 60.0);
            factory.AppendChild(timeFactory);
            
            // 级别
            var levelFactory = new FrameworkElementFactory(typeof(TextBlock));
            levelFactory.SetBinding(TextBlock.TextProperty, new Binding("LevelText"));
            levelFactory.SetBinding(TextBlock.ForegroundProperty, new Binding("LevelColor"));
            levelFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 5, 0));
            levelFactory.SetValue(TextBlock.WidthProperty, 60.0);
            factory.AppendChild(levelFactory);
            
            // 消息
            var messageFactory = new FrameworkElementFactory(typeof(TextBlock));
            messageFactory.SetBinding(TextBlock.TextProperty, new Binding("Message"));
            messageFactory.SetBinding(TextBlock.ForegroundProperty, new Binding("MessageColor"));
            messageFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            factory.AppendChild(messageFactory);
            
            dataTemplate.VisualTree = factory;
            _logListBox.ItemTemplate = dataTemplate;
            
            // 🔧 修复：在UI线程中安全绑定数据
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            {
                _logListBox.ItemsSource = _dataModel.WorkLogs;
            }
            else
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _logListBox.ItemsSource = _dataModel.WorkLogs;
                });
            }
            
            // 🔧 修复：监听集合变化自动滚动（线程安全版本）
            _dataModel.WorkLogs.CollectionChanged += (s, e) =>
            {
                // 确保UI操作在UI线程中执行
                if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    // 已在UI线程中
                    if (_dataModel.AutoScroll && _logListBox.Items.Count > 0)
                    {
                        _logListBox.ScrollIntoView(_logListBox.Items[_logListBox.Items.Count - 1]);
                    }
                }
                else
                {
                    // 在非UI线程中，调度到UI线程执行
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        if (_dataModel.AutoScroll && _logListBox.Items.Count > 0)
                        {
                            _logListBox.ScrollIntoView(_logListBox.Items[_logListBox.Items.Count - 1]);
                        }
                    }));
                }
            };
        }
        
        /// <summary>
        /// 滚动到底部
        /// </summary>
        public void ScrollToBottom()
        {
            try
            {
                if (_logListBox.Items.Count > 0)
                {
                    _logListBox.ScrollIntoView(_logListBox.Items[_logListBox.Items.Count - 1]);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "滚动到底部时发生异常");
            }
        }
        
        /// <summary>
        /// 清空日志显示
        /// </summary>
        public void ClearDisplay()
        {
            try
            {
                _dataModel.WorkLogs.Clear();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "清空日志显示时发生异常");
            }
        }
        
        /// <summary>
        /// 获取日志列表框
        /// </summary>
        public ListBox GetLogListBox() => _logListBox;
        
        /// <summary>
        /// 刷新显示
        /// </summary>
        public void RefreshDisplay()
        {
            try
            {
                _logListBox.Items.Refresh();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "刷新日志显示时发生异常");
            }
        }
        
        #region INotifyPropertyChanged 实现
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
} 