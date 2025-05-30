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
            // 从PasswordBox获取密码
            if (parameter is PasswordBox passwordBox)
            {
                SecretKey = passwordBox.Password;
            }

            // 验证输入
            if (string.IsNullOrWhiteSpace(AccountName) || 
                string.IsNullOrWhiteSpace(ApiKey) || 
                string.IsNullOrWhiteSpace(SecretKey))
            {
                // 这里应该显示错误消息
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
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseAction?.Invoke();
        }
    }
} 