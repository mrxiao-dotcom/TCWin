using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using BinanceFuturesTrader.Views.AutoMonitor.Models;
using BinanceFuturesTrader.Models;
using Microsoft.Extensions.Logging;

namespace BinanceFuturesTrader.Views.AutoMonitor.Components
{
    /// <summary>
    /// 合约配置概览组件
    /// 显示保本、推仓、保盈配置的概览信息
    /// </summary>
    public class ContractConfigurationOverviewComponent : INotifyPropertyChanged
    {
        private readonly AutoMonitorDataModel _dataModel;
        private readonly ILogger _logger;
        private Grid _overviewGrid;
        private bool _isInitialized = false;

        public ContractConfigurationOverviewComponent(AutoMonitorDataModel dataModel, ILogger logger)
        {
            _dataModel = dataModel ?? throw new ArgumentNullException(nameof(dataModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            CreateOverviewGrid();
        }

        /// <summary>
        /// 创建概览网格
        /// </summary>
        private void CreateOverviewGrid()
        {
            try
            {
                _overviewGrid = new Grid
                {
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush(Color.FromRgb(248, 249, 250))
                };

                // 创建行定义
                _overviewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                _overviewGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 创建标题
                var titlePanel = CreateTitlePanel();
                Grid.SetRow(titlePanel, 0);
                _overviewGrid.Children.Add(titlePanel);

                // 创建内容区域
                var contentPanel = CreateContentPanel();
                Grid.SetRow(contentPanel, 1);
                _overviewGrid.Children.Add(contentPanel);

                _isInitialized = true;
                _logger.LogDebug("合约配置概览组件创建完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建合约配置概览组件时发生异常");
                throw;
            }
        }

        /// <summary>
        /// 创建标题面板
        /// </summary>
        private Panel CreateTitlePanel()
        {
            var titlePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 5, 10, 5)
            };

            var titleIcon = new TextBlock
            {
                Text = "📊",
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var titleText = new TextBlock
            {
                Text = "合约配置概览",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            titlePanel.Children.Add(titleIcon);
            titlePanel.Children.Add(titleText);

            return titlePanel;
        }

        /// <summary>
        /// 创建内容面板
        /// </summary>
        private Panel CreateContentPanel()
        {
            var contentPanel = new StackPanel
            {
                Margin = new Thickness(10)
            };

            // 统计信息卡片
            var statsCard = CreateStatsCard();
            contentPanel.Children.Add(statsCard);

            // 配置状态卡片
            var configCard = CreateConfigurationStatusCard();
            contentPanel.Children.Add(configCard);

            return contentPanel;
        }

        /// <summary>
        /// 创建统计信息卡片
        /// </summary>
        private Border CreateStatsCard()
        {
            var card = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10),
                Background = new SolidColorBrush(Colors.White)
            };

            var statsGrid = new Grid();
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 总合约数
            var totalContractsPanel = CreateStatPanel("📈", "总合约数", "0", Colors.Blue);
            Grid.SetColumn(totalContractsPanel, 0);
            statsGrid.Children.Add(totalContractsPanel);

            // 保本配置数
            var breakEvenPanel = CreateStatPanel("🛡️", "保本配置", "0", Colors.Green);
            Grid.SetColumn(breakEvenPanel, 1);
            statsGrid.Children.Add(breakEvenPanel);

            // 推仓配置数
            var addPositionPanel = CreateStatPanel("🚀", "推仓配置", "0", Colors.Orange);
            Grid.SetColumn(addPositionPanel, 2);
            statsGrid.Children.Add(addPositionPanel);

            // 止盈配置数
            var profitPanel = CreateStatPanel("💰", "止盈配置", "0", Colors.Purple);
            Grid.SetColumn(profitPanel, 3);
            statsGrid.Children.Add(profitPanel);

            card.Child = statsGrid;
            return card;
        }

        /// <summary>
        /// 创建统计面板
        /// </summary>
        private Panel CreateStatPanel(string icon, string title, string value, Color color)
        {
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5)
            };

            var iconBlock = new TextBlock
            {
                Text = icon,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(color)
            };

            var valueBlock = new TextBlock
            {
                Text = value,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(color)
            };

            panel.Children.Add(iconBlock);
            panel.Children.Add(titleBlock);
            panel.Children.Add(valueBlock);

            return panel;
        }

        /// <summary>
        /// 创建配置状态卡片
        /// </summary>
        private Border CreateConfigurationStatusCard()
        {
            var card = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Colors.White)
            };

            var statusPanel = new StackPanel();

            // 状态标题
            var statusTitle = new TextBlock
            {
                Text = "📋 配置状态",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };

            statusPanel.Children.Add(statusTitle);

            // 状态列表
            var statusList = new ItemsControl
            {
                MaxHeight = 200,
                Template = new ControlTemplate(typeof(ItemsControl))
                {
                    VisualTree = new FrameworkElementFactory(typeof(ScrollViewer))
                    {
                        Name = "ScrollViewer"
                    }
                }
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 200
            };

            var statusStackPanel = new StackPanel();
            scrollViewer.Content = statusStackPanel;

            statusPanel.Children.Add(scrollViewer);

            card.Child = statusPanel;
            return card;
        }

        /// <summary>
        /// 更新概览数据
        /// </summary>
        public void UpdateOverview()
        {
            try
            {
                if (!_isInitialized) return;

                // 更新统计信息
                UpdateStatistics();

                // 更新配置状态
                UpdateConfigurationStatus();

                _logger.LogTrace("合约配置概览更新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新合约配置概览时发生异常");
            }
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics()
        {
            try
            {
                if (_overviewGrid.Children.Count < 2) return;

                var contentPanel = _overviewGrid.Children[1] as StackPanel;
                if (contentPanel?.Children.Count == 0) return;

                var statsCard = contentPanel.Children[0] as Border;
                var statsGrid = statsCard?.Child as Grid;
                if (statsGrid == null) return;

                var contracts = _dataModel.ContractMonitors;
                var totalContracts = contracts.Count;
                var breakEvenCount = contracts.Count(c => c.TriggerConditions.Any(tc => tc.Type == TriggerConditionType.BreakEven));
                var addPositionCount = contracts.Count(c => c.TriggerConditions.Any(tc => tc.Type == TriggerConditionType.AddPosition));
                var profitCount = contracts.Count(c => c.TriggerConditions.Any(tc => tc.Type == TriggerConditionType.ProfitProtection));

                // 更新统计数值
                UpdateStatValue(statsGrid, 0, totalContracts.ToString());
                UpdateStatValue(statsGrid, 1, breakEvenCount.ToString());
                UpdateStatValue(statsGrid, 2, addPositionCount.ToString());
                UpdateStatValue(statsGrid, 3, profitCount.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新统计信息时发生异常");
            }
        }

        /// <summary>
        /// 更新统计值
        /// </summary>
        private void UpdateStatValue(Grid statsGrid, int columnIndex, string value)
        {
            try
            {
                var statPanel = statsGrid.Children[columnIndex] as StackPanel;
                if (statPanel?.Children.Count >= 3)
                {
                    var valueBlock = statPanel.Children[2] as TextBlock;
                    if (valueBlock != null)
                    {
                        valueBlock.Text = value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新统计值时发生异常 (列索引: {columnIndex})");
            }
        }

        /// <summary>
        /// 更新配置状态
        /// </summary>
        private void UpdateConfigurationStatus()
        {
            try
            {
                // 暂时简化实现
                _logger.LogTrace("配置状态更新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新配置状态时发生异常");
            }
        }

        /// <summary>
        /// 获取概览控件
        /// </summary>
        public Grid GetOverviewGrid() => _overviewGrid;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        #region INotifyPropertyChanged 实现

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
} 