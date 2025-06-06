using System;
using System.Windows.Controls;
using BinanceFuturesTrader.Models;
using BinanceFuturesTrader.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BinanceFuturesTrader.ViewModels
{
    public partial class AccountConfigViewModel : ObservableObject
    {
        private readonly AccountConfigService _accountService;
        
        [ObservableProperty]
        private string _accountName = string.Empty;

        [ObservableProperty]
        private string _apiKey = string.Empty;

        [ObservableProperty]
        private string _secretKey = string.Empty;

        [ObservableProperty]
        private int _riskCapitalTimes = 10;

        [ObservableProperty]
        private bool _isTestNet = false;

        [ObservableProperty]
        private bool _isNewAccount = true;

        public Action? CloseAction { get; set; }
        public Action<bool>? CloseWithResultAction { get; set; }

        public AccountConfigViewModel(AccountConfigService accountService)
        {
            _accountService = accountService;
        }

        public AccountConfigViewModel(AccountConfigService accountService, AccountConfig account) : this(accountService)
        {
            AccountName = account.Name;
            ApiKey = account.ApiKey;
            SecretKey = account.SecretKey;
            RiskCapitalTimes = account.RiskCapitalTimes;
            IsTestNet = account.IsTestNet;
            IsNewAccount = false;
        }

        [RelayCommand]
        private void Save(object parameter)
        {
            try
            {
                // 从PasswordBox获取密码
                if (parameter is PasswordBox passwordBox)
                {
                    SecretKey = passwordBox.Password;
                }

                // 验证输入
                if (string.IsNullOrWhiteSpace(AccountName))
                {
                    System.Windows.MessageBox.Show("请输入账户名称", "验证失败", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(ApiKey))
                {
                    System.Windows.MessageBox.Show("请输入API Key", "验证失败", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(SecretKey))
                {
                    System.Windows.MessageBox.Show("请输入Secret Key", "验证失败", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (RiskCapitalTimes <= 0)
                {
                    System.Windows.MessageBox.Show("风险金次数必须大于0", "验证失败", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var account = new AccountConfig
                {
                    Name = AccountName,
                    ApiKey = ApiKey,
                    SecretKey = SecretKey,
                    RiskCapitalTimes = RiskCapitalTimes,
                    IsTestNet = IsTestNet
                };

                _accountService.SaveAccount(account);
                
                // 显示成功消息
                System.Windows.MessageBox.Show($"账户 '{AccountName}' 配置保存成功！", "保存成功", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                
                // 关闭窗口并返回true表示保存成功
                CloseWithResultAction?.Invoke(true);
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "保存失败", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            // 关闭窗口并返回false表示取消
            CloseWithResultAction?.Invoke(false);
            CloseAction?.Invoke();
        }
    }
} 