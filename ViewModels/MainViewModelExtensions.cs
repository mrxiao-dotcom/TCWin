using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using BinanceFuturesTrader.Services;

namespace BinanceFuturesTrader.ViewModels
{
    public partial class MainViewModel
    {
        private readonly RecentContractsService _recentContractsService = new RecentContractsService();

        /// <summary>
        /// 加载最近访问的合约列表
        /// </summary>
        private void LoadRecentContracts()
        {
            try
            {
                var contracts = _recentContractsService.LoadRecentContracts();
                RecentContracts.Clear();
                foreach (var contract in contracts)
                {
                    RecentContracts.Add(contract);
                }
                Console.WriteLine($"📖 已加载最近合约: {RecentContracts.Count} 个");
                
                // 订阅PropertyChanged事件来监听Symbol变化
                this.PropertyChanged += OnPropertyChanged;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载最近合约失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存最近访问的合约列表
        /// </summary>
        public void SaveRecentContracts()
        {
            try
            {
                _recentContractsService.SaveRecentContracts(RecentContracts);
                Console.WriteLine($"💾 已保存最近合约: {RecentContracts.Count} 个");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存最近合约失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加合约到最近列表
        /// </summary>
        private void AddToRecentContractsInternal(string contract)
        {
            if (string.IsNullOrWhiteSpace(contract) || !contract.Contains("USDT"))
                return;

            try
            {
                var updatedContracts = _recentContractsService.AddRecentContract(contract, RecentContracts);
                
                RecentContracts.Clear();
                foreach (var c in updatedContracts)
                {
                    RecentContracts.Add(c);
                }
                
                Console.WriteLine($"✅ 已添加最近合约: {contract}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 添加最近合约失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理属性变化事件
        /// </summary>
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Symbol))
            {
                if (!string.IsNullOrWhiteSpace(Symbol) && Symbol.Contains("USDT"))
                {
                    AddToRecentContractsInternal(Symbol);
                }
            }
        }
    }
} 