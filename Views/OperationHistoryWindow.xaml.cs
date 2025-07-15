using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using BinanceFuturesTrader.Models;
using Microsoft.Win32;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// 操作历史记录窗口
    /// </summary>
    public partial class OperationHistoryWindow : Window
    {
        private ObservableCollection<OperationHistoryRecord> _historyRecords;
        private DateTime _selectedDate;

        public OperationHistoryWindow()
        {
            InitializeComponent();
            _historyRecords = new ObservableCollection<OperationHistoryRecord>();
            _selectedDate = DateTime.Today;
            
            // 延迟初始化，确保控件已完全加载
            this.Loaded += OperationHistoryWindow_Loaded;
        }

        private void OperationHistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 确保控件已完全初始化后再设置数据
                if (HistoryDataGrid != null)
                {
                    HistoryDataGrid.ItemsSource = _historyRecords;
                }
                
                if (HistoryDatePicker != null)
                {
                    HistoryDatePicker.SelectedDate = _selectedDate;
                }
                
                LoadHistoryForDate(_selectedDate);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化历史记录窗口失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HistoryDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (HistoryDatePicker?.SelectedDate.HasValue == true)
                {
                    _selectedDate = HistoryDatePicker.SelectedDate.Value;
                    LoadHistoryForDate(_selectedDate);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择日期时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadHistoryForDate(_selectedDate);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_historyRecords.Count == 0)
                {
                    MessageBox.Show("没有记录可以导出", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "文本文件 (*.txt)|*.txt|CSV文件 (*.csv)|*.csv",
                    FileName = $"操作历史_{_selectedDate:yyyy-MM-dd}",
                    DefaultExt = ".txt"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportHistoryToFile(saveFileDialog.FileName);
                    MessageBox.Show($"历史记录已导出到：\n{saveFileDialog.FileName}", "导出成功", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadHistoryForDate(DateTime date)
        {
            try
            {
                _historyRecords.Clear();

                var historyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                              "BinanceFuturesTrader", "OperationHistory");
                var fileName = $"操作历史_{date:yyyy-MM-dd}.json";
                var filePath = Path.Combine(historyDir, fileName);

                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        try
                        {
                            var records = JsonSerializer.Deserialize<List<OperationHistoryRecord>>(json);

                            if (records != null && records.Count > 0)
                            {
                                // 按时间倒序排列
                                var sortedRecords = records.OrderByDescending(r => r.Timestamp);
                                foreach (var record in sortedRecords)
                                {
                                    if (record != null) // 确保记录不为null
                                    {
                                        _historyRecords.Add(record);
                                    }
                                }
                            }
                        }
                        catch (JsonException jsonEx)
                        {
                            MessageBox.Show($"历史文件格式错误：{jsonEx.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }

                if (RecordCountText != null)
                {
                    RecordCountText.Text = $"{date:yyyy年MM月dd日} 记录：{_historyRecords.Count} 条";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载历史记录失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportHistoryToFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            var content = new StringBuilder();

            if (extension == ".csv")
            {
                // CSV格式
                content.AppendLine("时间,操作,合约,详情,类型,用户");
                foreach (var record in _historyRecords)
                {
                    content.AppendLine($"{record.Timestamp:yyyy-MM-dd HH:mm:ss},{record.Operation},{record.ContractName},{record.Details},{record.OperationType},{record.Username}");
                }
            }
            else
            {
                // 文本格式
                content.AppendLine($"操作历史记录 - {_selectedDate:yyyy年MM月dd日}");
                content.AppendLine($"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                content.AppendLine($"记录总数：{_historyRecords.Count} 条");
                content.AppendLine(new string('=', 60));
                content.AppendLine();

                foreach (var record in _historyRecords)
                {
                    content.AppendLine($"[{record.Timestamp:HH:mm:ss}] {record.Operation}");
                    content.AppendLine($"  合约：{record.ContractName}");
                    content.AppendLine($"  详情：{record.Details}");
                    content.AppendLine($"  类型：{record.OperationType}");
                    content.AppendLine($"  用户：{record.Username}");
                    content.AppendLine();
                }
            }

            File.WriteAllText(filePath, content.ToString(), Encoding.UTF8);
        }
    }
} 