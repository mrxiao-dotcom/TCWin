using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// OrderHistoryWindow.xaml 的交互逻辑
    /// </summary>
    public partial class OrderHistoryWindow : Window
    {
        private readonly List<OrderInfo> _orders;
        private readonly string _symbol;

        public OrderHistoryWindow(List<OrderInfo> orders, string symbol)
        {
            InitializeComponent();
            _orders = orders ?? new List<OrderInfo>();
            _symbol = symbol;
            
            InitializeWindow();
            LoadOrderData();
        }

        private void InitializeWindow()
        {
            // 设置窗口标题
            TitleTextBlock.Text = $"{_symbol} - 最近成交记录";
            Title = $"{_symbol} - 最近成交记录";
        }

        private void LoadOrderData()
        {
            try
            {
                // 绑定数据到DataGrid
                OrderHistoryDataGrid.ItemsSource = _orders;
                
                // 更新记录数量
                CountTextBlock.Text = $"共 {_orders.Count} 条记录";
                
                // 计算和显示统计信息
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载订单数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                if (!_orders.Any())
                {
                    StatisticsTextBlock.Text = "暂无数据";
                    return;
                }

                // 统计各种状态的订单数量
                var filledCount = _orders.Count(o => o.Status == "FILLED");
                var partiallyFilledCount = _orders.Count(o => o.Status == "PARTIALLY_FILLED");
                var canceledCount = _orders.Count(o => o.Status == "CANCELED");
                var rejectedCount = _orders.Count(o => o.Status == "REJECTED");
                
                // 统计买卖方向
                var buyCount = _orders.Count(o => o.Side == "BUY");
                var sellCount = _orders.Count(o => o.Side == "SELL");
                
                // 统计订单类型
                var marketCount = _orders.Count(o => o.Type == "MARKET");
                var limitCount = _orders.Count(o => o.Type == "LIMIT");
                var stopCount = _orders.Count(o => o.Type == "STOP_MARKET" || o.Type == "TAKE_PROFIT_MARKET");
                
                // 计算总成交数量
                var totalExecutedQty = _orders.Sum(o => o.ExecutedQty);
                
                // 时间范围
                var earliestTime = _orders.Min(o => o.Time);
                var latestTime = _orders.Max(o => o.UpdateTime);

                var statisticsText = $"成交: {filledCount} | 部分成交: {partiallyFilledCount} | 已撤销: {canceledCount} | " +
                                   $"买入: {buyCount} | 卖出: {sellCount} | " +
                                   $"市价单: {marketCount} | 限价单: {limitCount} | 止损单: {stopCount} | " +
                                   $"总成交量: {totalExecutedQty:F4} | " +
                                   $"时间范围: {earliestTime:MM-dd HH:mm} ~ {latestTime:MM-dd HH:mm}";

                StatisticsTextBlock.Text = statisticsText;
            }
            catch (Exception ex)
            {
                StatisticsTextBlock.Text = $"统计计算失败: {ex.Message}";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 刷新数据显示
                LoadOrderData();
                
                // 可以在这里添加重新从API获取数据的逻辑
                MessageBox.Show("数据已刷新", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 获取选中的订单信息（如果需要的话）
        /// </summary>
        public OrderInfo? GetSelectedOrder()
        {
            return OrderHistoryDataGrid.SelectedItem as OrderInfo;
        }

        /// <summary>
        /// 设置窗口关闭时的回调（如果需要的话）
        /// </summary>
        public event EventHandler? WindowClosed;

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            WindowClosed?.Invoke(this, e);
        }
    }
} 