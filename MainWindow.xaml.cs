using System;
using System.Windows;
using System.Windows.Controls;
using BinanceFuturesTrader.ViewModels;

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
            if (ConditionalOrderCard != null)
            {
                // 切换条件单设置区域的可见性
                ConditionalOrderCard.Visibility = ConditionalOrderCard.Visibility == Visibility.Visible 
                    ? Visibility.Collapsed 
                    : Visibility.Visible;
            }
        }
    }
} 