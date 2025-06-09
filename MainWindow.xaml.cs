using System;
using System.Windows;
using System.Windows.Controls;
using BinanceFuturesTrader.ViewModels;
using System.Windows.Media;

namespace BinanceFuturesTrader
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            // 保存最近访问的合约
            _viewModel?.SaveRecentContracts();
            
            // 清理资源，停止定时器
            _viewModel?.Cleanup();
            base.OnClosed(e);
        }

        private void ToggleConditionalOrder_Click(object sender, RoutedEventArgs e)
        {
            // 切换条件单设置区域的可见性
            if (ConditionalOrderCard.Visibility == Visibility.Visible)
            {
                ConditionalOrderCard.Visibility = Visibility.Collapsed;
            }
            else
            {
                ConditionalOrderCard.Visibility = Visibility.Visible;
            }
        }

        // 加仓型条件单切换
        private void AddPositionConditional_Click(object sender, RoutedEventArgs e)
        {
            // 显示加仓型条件单面板，隐藏平仓型条件单面板
            AddPositionPanel.Visibility = Visibility.Visible;
            ClosePositionPanel.Visibility = Visibility.Collapsed;
            
            // 更新按钮样式
            AddPositionBtn.Background = new SolidColorBrush(Colors.Orange);
            ClosePositionBtn.Background = new SolidColorBrush(Colors.Gray);
            
            // 调用ViewModel的切换命令
            _viewModel.SwitchToAddPositionModeCommand.Execute(null);
            
            Console.WriteLine("🔄 切换到加仓型条件单模式");
        }

        // 平仓型条件单切换
        private void ClosePositionConditional_Click(object sender, RoutedEventArgs e)
        {
            // 显示平仓型条件单面板，隐藏加仓型条件单面板
            AddPositionPanel.Visibility = Visibility.Collapsed;
            ClosePositionPanel.Visibility = Visibility.Visible;
            
            // 更新按钮样式
            AddPositionBtn.Background = new SolidColorBrush(Colors.Gray);
            ClosePositionBtn.Background = new SolidColorBrush(Colors.Orange);
            
            // 调用ViewModel的切换命令
            _viewModel.SwitchToClosePositionModeCommand.Execute(null);
            
            Console.WriteLine("🔄 切换到平仓型条件单模式");
        }

        // 风险金输入框鼠标悬停事件
        private void RiskCapitalTextBox_MouseEnter(object sender, RoutedEventArgs e)
        {
            // 鼠标悬停时在状态栏显示简化的计算公式，保持单行显示
            if (!string.IsNullOrEmpty(_viewModel.RiskCapitalCalculationDetail))
            {
                // 提取核心计算信息，在单行内显示
                var lines = _viewModel.RiskCapitalCalculationDetail.Split('\n');
                var summaryLine = "";
                
                // 寻找标准风险金和浮盈风险金信息
                foreach (var line in lines)
                {
                    if (line.Contains("标准风险金:"))
                    {
                        var start = line.IndexOf("标准风险金:");
                        var standardPart = line.Substring(start).Split('=')[1].Split('U')[0] + "U";
                        summaryLine += $"标准:{standardPart} ";
                    }
                    else if (line.Contains("浮盈风险金:"))
                    {
                        var start = line.IndexOf("浮盈风险金:");
                        var profitPart = line.Substring(start + 5).Trim();
                        summaryLine += $"浮盈:{profitPart} ";
                    }
                    else if (line.Contains("最终可用风险金:"))
                    {
                        var parts = line.Split('→');
                        if (parts.Length > 1)
                        {
                            var finalAmount = parts[1].Split('(')[0].Trim();
                            summaryLine += $"→ {finalAmount}";
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(summaryLine))
                {
                    _viewModel.StatusMessage = $"💰 风险金计算: {summaryLine.Trim()}（详细信息可鼠标悬停或查看日志）";
                }
            }
        }

        private void RiskCapitalTextBox_MouseLeave(object sender, RoutedEventArgs e)
        {
            // 鼠标离开时恢复固定长度的状态消息
            if (_viewModel.AvailableRiskCapital > 0)
            {
                _viewModel.StatusMessage = $"💰 可用风险金: {_viewModel.AvailableRiskCapital:F0}U";
            }
            else
            {
                _viewModel.StatusMessage = "请先计算可用风险金";
            }
        }

        // 测试市值计算按钮点击事件
        private void TestMarketValue_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TestMarketValueCalculation();
            _viewModel.StatusMessage = "🧪 市值计算测试已执行，请查看控制台输出";
        }

    }
} 