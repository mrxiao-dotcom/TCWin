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
        private MainViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
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

        // 标准条件单切换
        private void StandardConditional_Click(object sender, RoutedEventArgs e)
        {
            // 显示标准条件单面板，隐藏浮盈条件单面板
            StandardConditionalPanel.Visibility = Visibility.Visible;
            ProfitConditionalPanel.Visibility = Visibility.Collapsed;
            
            // 更新按钮样式
            StandardConditionalBtn.Background = new SolidColorBrush(Colors.Orange);
            ProfitConditionalBtn.Background = new SolidColorBrush(Colors.Gray);
            
            Console.WriteLine("🔄 切换到标准条件单模式");
        }

        // 浮盈条件单切换
        private void ProfitConditional_Click(object sender, RoutedEventArgs e)
        {
            // 显示浮盈条件单面板，隐藏标准条件单面板
            StandardConditionalPanel.Visibility = Visibility.Collapsed;
            ProfitConditionalPanel.Visibility = Visibility.Visible;
            
            // 更新按钮样式
            StandardConditionalBtn.Background = new SolidColorBrush(Colors.Gray);
            ProfitConditionalBtn.Background = new SolidColorBrush(Colors.Orange);
            
            Console.WriteLine("🔄 切换到浮盈条件单模式");
        }
    }
} 