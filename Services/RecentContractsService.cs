using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BinanceFuturesTrader.Services
{
    public class RecentContractsService
    {
        private readonly string _settingsPath;
        private const int MAX_RECENT_CONTRACTS = 10;
        
        public RecentContractsService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BinanceFuturesTrader");
            
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            _settingsPath = Path.Combine(appDataPath, "recent_contracts.json");
        }
        
        /// <summary>
        /// 加载最近访问的合约列表
        /// </summary>
        public List<string> LoadRecentContracts()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    Console.WriteLine("💡 最近合约文件不存在，返回空列表");
                    return new List<string>();
                }
                
                var json = File.ReadAllText(_settingsPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine("💡 最近合约文件为空，返回空列表");
                    return new List<string>();
                }
                
                var contracts = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                
                // 验证并清理无效的合约名
                var validContracts = contracts
                    .Where(c => !string.IsNullOrWhiteSpace(c) && c.Contains("USDT"))
                    .Distinct()
                    .Take(MAX_RECENT_CONTRACTS)
                    .ToList();
                
                Console.WriteLine($"📖 已加载最近合约: {validContracts.Count} 个");
                foreach (var contract in validContracts)
                {
                    Console.WriteLine($"   📝 {contract}");
                }
                
                return validContracts;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载最近合约失败: {ex.Message}");
                return new List<string>();
            }
        }
        
        /// <summary>
        /// 保存最近访问的合约列表
        /// </summary>
        public void SaveRecentContracts(IEnumerable<string> contracts)
        {
            try
            {
                if (contracts == null)
                {
                    Console.WriteLine("⚠️ 传入的合约列表为null，跳过保存");
                    return;
                }
                
                // 验证并清理合约列表
                var validContracts = contracts
                    .Where(c => !string.IsNullOrWhiteSpace(c) && c.Contains("USDT"))
                    .Distinct()
                    .Take(MAX_RECENT_CONTRACTS)
                    .ToList();
                
                if (!validContracts.Any())
                {
                    Console.WriteLine("💡 没有有效的合约需要保存");
                    return;
                }
                
                var json = JsonSerializer.Serialize(validContracts, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(_settingsPath, json);
                
                Console.WriteLine($"💾 已保存最近合约: {validContracts.Count} 个");
                foreach (var contract in validContracts)
                {
                    Console.WriteLine($"   📝 {contract}");
                }
                Console.WriteLine($"💾 保存路径: {_settingsPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存最近合约失败: {ex.Message}");
                Console.WriteLine($"   文件路径: {_settingsPath}");
                Console.WriteLine($"   异常详情: {ex}");
            }
        }
        
        /// <summary>
        /// 添加新的合约到最近列表
        /// </summary>
        public List<string> AddRecentContract(string contract, IEnumerable<string> existingContracts)
        {
            if (string.IsNullOrWhiteSpace(contract) || !contract.Contains("USDT"))
            {
                Console.WriteLine($"⚠️ 无效的合约名称: '{contract}'");
                return existingContracts?.ToList() ?? new List<string>();
            }
            
            var contracts = existingContracts?.ToList() ?? new List<string>();
            
            // 移除已存在的相同合约（如果有）
            if (contracts.Contains(contract))
            {
                contracts.Remove(contract);
                Console.WriteLine($"🔄 移除重复合约: {contract}");
            }
            
            // 添加到列表开头
            contracts.Insert(0, contract);
            
            // 保持最多10个合约
            while (contracts.Count > MAX_RECENT_CONTRACTS)
            {
                var removed = contracts[contracts.Count - 1];
                contracts.RemoveAt(contracts.Count - 1);
                Console.WriteLine($"📤 移除最旧合约: {removed}");
            }
            
            Console.WriteLine($"✅ 已添加最近合约: {contract} (总数: {contracts.Count})");
            return contracts;
        }
        
        /// <summary>
        /// 获取设置文件路径
        /// </summary>
        public string GetSettingsPath()
        {
            return _settingsPath;
        }
        
        /// <summary>
        /// 清空最近合约记录
        /// </summary>
        public void ClearRecentContracts()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    File.Delete(_settingsPath);
                    Console.WriteLine("🗑️ 已清空最近合约记录");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 清空最近合约记录失败: {ex.Message}");
            }
        }
    }
} 