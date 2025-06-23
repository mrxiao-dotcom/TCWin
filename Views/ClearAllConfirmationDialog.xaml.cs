using System.Windows;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// 一键清仓确认对话框
    /// </summary>
    public partial class ClearAllConfirmationDialog : Window
    {
        /// <summary>
        /// 用户是否确认清仓
        /// </summary>
        public bool IsConfirmed { get; private set; } = false;

        public ClearAllConfirmationDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 确认按钮点击事件
        /// </summary>
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }
    }
} 