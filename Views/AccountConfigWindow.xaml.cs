using System.Windows;
using System.Windows.Controls;
using BinanceFuturesTrader.ViewModels;

namespace BinanceFuturesTrader.Views
{
    /// <summary>
    /// AccountConfigWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AccountConfigWindow : Window
    {
        public AccountConfigWindow()
        {
            InitializeComponent();
        }

        public AccountConfigWindow(AccountConfigViewModel viewModel) : this()
        {
            DataContext = viewModel;
            if (viewModel != null)
            {
                viewModel.CloseAction = () => this.Close();
                if (!string.IsNullOrEmpty(viewModel.SecretKey))
                {
                    SecretKeyBox.Password = viewModel.SecretKey;
                }
            }
        }

        private void SecretKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is AccountConfigViewModel vm && sender is PasswordBox pb)
            {
                vm.SecretKey = pb.Password;
            }
        }
    }
} 