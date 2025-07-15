using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views.AutoMonitor.Components
{
    /// <summary>
    /// 状态显示组件
    /// </summary>
    public class StatusDisplayComponent : INotifyPropertyChanged
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly ILogger _logger;
        private TextBlock _statusTextBlock;
        private TextBlock _timeTextBlock;
        private TextBlock _statsTextBlock;
        
        public StatusDisplayComponent(AutoMonitorDataModel dataModel, ILogger logger)
        {
            _dataModel = dataModel;
            _logger = logger;
            
            CreateStatusDisplay();
        }
        
        /// <summary>
        /// 创建状态显示UI元素
        /// </summary>
        private void CreateStatusDisplay()
        {
            _statusTextBlock = new TextBlock
            {
                Text = "系统就绪",
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = Brushes.Green
            };
            
            _timeTextBlock = new TextBlock
            {
                Text = "00:00:00",
                FontSize = 12,
                Foreground = Brushes.Gray
            };
            
            _statsTextBlock = new TextBlock
            {
                Text = "准备就绪",
                FontSize = 12,
                Foreground = Brushes.Blue
            };
        }
        
        /// <summary>
        /// 更新状态显示
        /// </summary>
        public void UpdateStatus()
        {
            try
            {
                if (_statusTextBlock != null)
                {
                    _statusTextBlock.Text = $"状态: {_dataModel.MonitorStatus}";
                    _statusTextBlock.Foreground = GetStatusColor(_dataModel.MonitorStatus);
                }
                
                if (_timeTextBlock != null)
                {
                    _timeTextBlock.Text = $"运行时间: {_dataModel.UptimeText}";
                }
                
                if (_statsTextBlock != null)
                {
                    _statsTextBlock.Text = $"持仓: {_dataModel.PositionStatsText}";
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "更新状态显示时发生异常");
            }
        }
        
        /// <summary>
        /// 获取状态颜色
        /// </summary>
        private Brush GetStatusColor(string status)
        {
            return status switch
            {
                "运行中" => Brushes.Green,
                "已暂停" => Brushes.Orange,
                "已停止" => Brushes.Red,
                "未启动" => Brushes.Gray,
                _ => Brushes.Black
            };
        }
        
        /// <summary>
        /// 获取状态文本块
        /// </summary>
        public TextBlock GetStatusTextBlock() => _statusTextBlock;
        
        /// <summary>
        /// 获取时间文本块
        /// </summary>
        public TextBlock GetTimeTextBlock() => _timeTextBlock;
        
        /// <summary>
        /// 获取统计文本块
        /// </summary>
        public TextBlock GetStatsTextBlock() => _statsTextBlock;
        
        #region INotifyPropertyChanged 实现
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
} 