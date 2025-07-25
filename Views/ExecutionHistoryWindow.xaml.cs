using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.Logging;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using BinanceFuturesTrader.Views.AutoMonitor.Models;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// ExecutionHistoryWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ExecutionHistoryWindow : Window
    {
        private readonly AutoMonitorService _autoMonitorService;
        private readonly ILogger _logger;
        private readonly ObservableCollection<ExecutionHistoryDisplayModel> _executionHistory;
        private readonly ObservableCollection<ExecutionHistoryDisplayModel> _filteredHistory;
        private CollectionViewSource _collectionViewSource;

        public ExecutionHistoryWindow(AutoMonitorService autoMonitorService, ILogger logger)
        {
            try
            {
                InitializeComponent();
                
                _autoMonitorService = autoMonitorService ?? throw new ArgumentNullException(nameof(autoMonitorService));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                
                _executionHistory = new ObservableCollection<ExecutionHistoryDisplayModel>();
                _filteredHistory = new ObservableCollection<ExecutionHistoryDisplayModel>();
                
                InitializeWindow();
                InitializeFilters();
                LoadExecutionHistory();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 执行历史窗口初始化失败");
                MessageBox.Show($"执行历史窗口初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeWindow()
        {
            // 设置窗口标题
            Title = "自动盯盘执行历史";
            TitleTextBlock.Text = "🕒 自动盯盘执行历史";
            
            // 设置数据源
            _collectionViewSource = new CollectionViewSource { Source = _filteredHistory };
            ExecutionHistoryDataGrid.ItemsSource = _collectionViewSource.View;
            
            // 设置默认排序
            _collectionViewSource.SortDescriptions.Add(new SortDescription("ExecutionTime", ListSortDirection.Descending));
            
            _logger.LogInformation("🔍 执行历史窗口已初始化");
        }

        private void InitializeFilters()
        {
            try
            {
                // 初始化过滤条件下拉框
                SymbolFilterComboBox.Items.Add("全部合约");
                SymbolFilterComboBox.SelectedIndex = 0;
                
                ExecutionTypeFilterComboBox.Items.Add("全部类型");
                ExecutionTypeFilterComboBox.Items.Add("保本");
                ExecutionTypeFilterComboBox.Items.Add("推仓");
                ExecutionTypeFilterComboBox.Items.Add("保盈止损");
                ExecutionTypeFilterComboBox.SelectedIndex = 0;
                
                ResultFilterComboBox.Items.Add("全部结果");
                ResultFilterComboBox.Items.Add("成功");
                ResultFilterComboBox.Items.Add("失败");
                ResultFilterComboBox.SelectedIndex = 0;
                
                TimeRangeFilterComboBox.Items.Add("全部时间");
                TimeRangeFilterComboBox.Items.Add("最近1小时");
                TimeRangeFilterComboBox.Items.Add("最近24小时");
                TimeRangeFilterComboBox.Items.Add("最近7天");
                TimeRangeFilterComboBox.Items.Add("最近30天");
                TimeRangeFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 初始化过滤条件失败");
            }
        }

        private void LoadExecutionHistory()
        {
            try
            {
                _logger.LogInformation("📊 开始加载执行历史数据");
                
                // 获取执行历史数据
                var historyData = _autoMonitorService.GetExecutionHistory();
                
                // 转换为显示模型
                _executionHistory.Clear();
                foreach (var history in historyData)
                {
                    _executionHistory.Add(new ExecutionHistoryDisplayModel
                    {
                        ExecutionTime = history.ExecutionTime,
                        Symbol = history.Symbol,
                        PositionSide = history.PositionSide,
                        ExecutionType = history.ExecutionType,
                        IsSuccess = history.IsSuccess,
                        ResultText = history.IsSuccess ? "成功" : "失败",
                        TriggerPnl = history.TriggerPnl,
                        OrderId = history.OrderId?.ToString() ?? "",
                        ResultMessage = history.ResultMessage,
                        Details = history.Details,
                        ErrorMessage = history.ErrorMessage
                    });
                }
                
                // 更新合约过滤选项
                UpdateSymbolFilter();
                
                // 应用过滤
                ApplyFilters();
                
                _logger.LogInformation($"✅ 执行历史加载完成，共 {_executionHistory.Count} 条记录");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 加载执行历史数据失败");
                MessageBox.Show($"加载执行历史数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSymbolFilter()
        {
            try
            {
                // 🔧 修复空对象错误：检查控件是否已初始化
                if (SymbolFilterComboBox == null)
                {
                    _logger?.LogWarning("⚠️ SymbolFilterComboBox 控件未初始化，跳过更新");
                    return;
                }

                var currentSelection = SymbolFilterComboBox.SelectedItem?.ToString();
                
                SymbolFilterComboBox.Items.Clear();
                SymbolFilterComboBox.Items.Add("全部合约");
                
                var symbols = _executionHistory.Select(h => h.Symbol).Distinct().OrderBy(s => s).ToList();
                foreach (var symbol in symbols)
                {
                    SymbolFilterComboBox.Items.Add(symbol);
                }
                
                // 恢复选择
                if (!string.IsNullOrEmpty(currentSelection) && SymbolFilterComboBox.Items.Contains(currentSelection))
                {
                    SymbolFilterComboBox.SelectedItem = currentSelection;
                }
                else
                {
                    SymbolFilterComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 更新合约过滤选项失败");
            }
        }

        private void ApplyFilters()
        {
            try
            {
                var filteredData = _executionHistory.AsEnumerable();
                
                // 合约过滤
                var symbolFilter = SymbolFilterComboBox.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(symbolFilter) && symbolFilter != "全部合约")
                {
                    filteredData = filteredData.Where(h => h.Symbol == symbolFilter);
                }
                
                // 执行类型过滤
                var executionTypeFilter = ExecutionTypeFilterComboBox.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(executionTypeFilter) && executionTypeFilter != "全部类型")
                {
                    filteredData = filteredData.Where(h => h.ExecutionType == executionTypeFilter);
                }
                
                // 执行结果过滤
                var resultFilter = ResultFilterComboBox.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(resultFilter) && resultFilter != "全部结果")
                {
                    var isSuccess = resultFilter == "成功";
                    filteredData = filteredData.Where(h => h.IsSuccess == isSuccess);
                }
                
                // 时间范围过滤
                var timeRangeFilter = TimeRangeFilterComboBox.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(timeRangeFilter) && timeRangeFilter != "全部时间")
                {
                    var cutoffTime = GetCutoffTime(timeRangeFilter);
                    if (cutoffTime.HasValue)
                    {
                        filteredData = filteredData.Where(h => h.ExecutionTime >= cutoffTime.Value);
                    }
                }
                
                // 更新过滤后的数据
                _filteredHistory.Clear();
                foreach (var item in filteredData)
                {
                    _filteredHistory.Add(item);
                }
                
                // 更新统计信息
                UpdateStatistics();
                
                _logger.LogDebug($"📊 过滤完成，显示 {_filteredHistory.Count} 条记录");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 应用过滤条件失败");
            }
        }

        private DateTime? GetCutoffTime(string timeRange)
        {
            var now = DateTime.Now;
            return timeRange switch
            {
                "最近1小时" => now.AddHours(-1),
                "最近24小时" => now.AddDays(-1),
                "最近7天" => now.AddDays(-7),
                "最近30天" => now.AddDays(-30),
                _ => null
            };
        }

        private void UpdateStatistics()
        {
            try
            {
                if (!_filteredHistory.Any())
                {
                    CountTextBlock.Text = "共 0 条记录";
                    StatisticsTextBlock.Text = "暂无数据";
                    return;
                }
                
                var totalCount = _filteredHistory.Count;
                var successCount = _filteredHistory.Count(h => h.IsSuccess);
                var failedCount = totalCount - successCount;
                var successRate = totalCount > 0 ? (double)successCount / totalCount * 100 : 0;
                
                // 按执行类型统计
                var breakEvenCount = _filteredHistory.Count(h => h.ExecutionType == "保本");
                var addPositionCount = _filteredHistory.Count(h => h.ExecutionType == "推仓");
                var profitProtectionCount = _filteredHistory.Count(h => h.ExecutionType == "保盈止损");
                
                // 按合约统计
                var symbolCount = _filteredHistory.GroupBy(h => h.Symbol).Count();
                
                // 时间范围
                var earliestTime = _filteredHistory.Min(h => h.ExecutionTime);
                var latestTime = _filteredHistory.Max(h => h.ExecutionTime);
                
                // 总盈亏
                var totalPnl = _filteredHistory.Sum(h => h.TriggerPnl);
                
                CountTextBlock.Text = $"共 {totalCount} 条记录";
                
                StatisticsTextBlock.Text = $"成功: {successCount} | 失败: {failedCount} | 成功率: {successRate:F1}% | " +
                                          $"保本: {breakEvenCount} | 推仓: {addPositionCount} | 保盈: {profitProtectionCount} | " +
                                          $"涉及合约: {symbolCount} | 累计触发盈亏: {totalPnl:F2}U | " +
                                          $"时间范围: {earliestTime:MM-dd HH:mm} ~ {latestTime:MM-dd HH:mm}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 更新统计信息失败");
                StatisticsTextBlock.Text = $"统计计算失败: {ex.Message}";
            }
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                ApplyFilters();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("🔄 用户点击刷新按钮");
                LoadExecutionHistory();
                MessageBox.Show("数据已刷新", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 刷新失败");
                MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("确定要清空所有执行历史记录吗？此操作不可恢复。", 
                    "确认清空", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    _logger.LogInformation("🗑️ 用户确认清空执行历史");
                    
                    // 清空服务中的历史数据
                    _autoMonitorService.ClearExecutionHistory();
                    
                    // 清空界面显示
                    _executionHistory.Clear();
                    _filteredHistory.Clear();
                    
                    // 更新统计信息
                    UpdateStatistics();
                    
                    MessageBox.Show("执行历史已清空", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 清空执行历史失败");
                MessageBox.Show($"清空失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecutionHistoryDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedItem = ExecutionHistoryDataGrid.SelectedItem as ExecutionHistoryDisplayModel;
                if (selectedItem != null)
                {
                    UpdateDetailInfo(selectedItem);
                    ViewDetailsButton.IsEnabled = true;
                }
                else
                {
                    DetailInfoTextBlock.Text = "请选择一条执行记录查看详细信息";
                    ViewDetailsButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 处理选择变化失败");
            }
        }

        private void UpdateDetailInfo(ExecutionHistoryDisplayModel item)
        {
            try
            {
                var detailInfo = $"📊 执行详情\n" +
                               $"━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                               $"🕒 执行时间: {item.ExecutionTime:yyyy-MM-dd HH:mm:ss}\n" +
                               $"📈 合约代码: {item.Symbol}\n" +
                               $"📊 持仓方向: {item.PositionSide}\n" +
                               $"🎯 执行类型: {item.ExecutionType}\n" +
                               $"✅ 执行结果: {item.ResultText}\n" +
                               $"💰 触发浮盈: {item.TriggerPnl:F2} USDT\n" +
                               $"🏷️ 订单ID: {(string.IsNullOrEmpty(item.OrderId) ? "无" : item.OrderId)}\n" +
                               $"📝 结果消息: {item.ResultMessage}\n" +
                               $"📋 详细信息: {item.Details}";
                
                if (!string.IsNullOrEmpty(item.ErrorMessage))
                {
                    detailInfo += $"\n❌ 错误信息: {item.ErrorMessage}";
                }
                
                DetailInfoTextBlock.Text = detailInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 更新详细信息失败");
                DetailInfoTextBlock.Text = $"更新详细信息失败: {ex.Message}";
            }
        }

        private void ViewDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ExecutionHistoryDataGrid.SelectedItem as ExecutionHistoryDisplayModel;
                if (selectedItem != null)
                {
                    // 简单的详情对话框
                    var detailInfo = $"📊 执行详情\n" +
                                   $"━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                                   $"🕒 执行时间: {selectedItem.ExecutionTime:yyyy-MM-dd HH:mm:ss}\n" +
                                   $"📈 合约代码: {selectedItem.Symbol}\n" +
                                   $"📊 持仓方向: {selectedItem.PositionSide}\n" +
                                   $"🎯 执行类型: {selectedItem.ExecutionType}\n" +
                                   $"✅ 执行结果: {selectedItem.ResultText}\n" +
                                   $"💰 触发浮盈: {selectedItem.TriggerPnl:F2} USDT\n" +
                                   $"🏷️ 订单ID: {(string.IsNullOrEmpty(selectedItem.OrderId) ? "无" : selectedItem.OrderId)}\n" +
                                   $"📝 结果消息: {selectedItem.ResultMessage}\n" +
                                   $"📋 详细信息: {selectedItem.Details}";
                    
                    if (!string.IsNullOrEmpty(selectedItem.ErrorMessage))
                    {
                        detailInfo += $"\n❌ 错误信息: {selectedItem.ErrorMessage}";
                    }
                    
                    MessageBox.Show(detailInfo, "执行详情", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 查看详情失败");
                MessageBox.Show($"查看详情失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("📊 用户点击导出Excel按钮");
                
                // 这里可以实现Excel导出功能
                MessageBox.Show("Excel导出功能正在开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 导出失败");
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }


} 