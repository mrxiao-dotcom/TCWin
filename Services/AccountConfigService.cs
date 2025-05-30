using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Services
{
    public class AccountConfigService
    {
        private readonly string _configFilePath;
        private List<AccountConfig> _accounts;

        public AccountConfigService()
        {
            _configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "BinanceFuturesTrader", "accounts.json");
            _accounts = new List<AccountConfig>();
            LoadAccounts();
        }

        public List<AccountConfig> GetAllAccounts()
        {
            return _accounts.ToList();
        }

        public AccountConfig? GetAccount(string name)
        {
            return _accounts.FirstOrDefault(a => a.Name == name);
        }

        public void SaveAccount(AccountConfig account)
        {
            var existingAccount = _accounts.FirstOrDefault(a => a.Name == account.Name);
            if (existingAccount != null)
            {
                existingAccount.ApiKey = account.ApiKey;
                existingAccount.SecretKey = account.SecretKey;
                existingAccount.RiskCapitalTimes = account.RiskCapitalTimes;
                existingAccount.IsTestNet = account.IsTestNet;
                existingAccount.LastModified = DateTime.Now;
            }
            else
            {
                _accounts.Add(account);
            }
            
            SaveAccounts();
        }

        public void DeleteAccount(string name)
        {
            _accounts.RemoveAll(a => a.Name == name);
            SaveAccounts();
        }

        private void LoadAccounts()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    _accounts = JsonConvert.DeserializeObject<List<AccountConfig>>(json) ?? new List<AccountConfig>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading accounts: {ex.Message}");
                _accounts = new List<AccountConfig>();
            }
        }

        private void SaveAccounts()
        {
            try
            {
                var directory = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(_accounts, Formatting.Indented);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving accounts: {ex.Message}");
            }
        }
    }
} 