using System;
using System.IO;
using System.Text.Json;
using BinanceFuturesTrader.Models;

namespace BinanceFuturesTrader.Services
{
    public class TradingSettingsService
    {
        private const string SettingsFileName = "TradingSettings.json";
        private readonly string _settingsFilePath;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public TradingSettingsService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "BinanceFuturesTrader");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            
            _settingsFilePath = Path.Combine(appFolder, SettingsFileName);
        }

        public TradingSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    Console.WriteLine("📁 交易设置文件不存在，使用默认设置");
                    return GetDefaultSettings();
                }

                var jsonContent = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<TradingSettings>(jsonContent, _jsonOptions);
                
                if (settings == null)
                {
                    Console.WriteLine("⚠️ 交易设置文件解析失败，使用默认设置");
                    return GetDefaultSettings();
                }

                Console.WriteLine("✅ 交易设置已加载");
                Console.WriteLine($"   📊 交易方向: {settings.Side}");
                Console.WriteLine($"   📊 杠杆倍数: {settings.Leverage}x");
                Console.WriteLine($"   📊 仓位模式: {settings.MarginType}");
                Console.WriteLine($"   📊 下单方式: {settings.OrderType}");
                Console.WriteLine($"   📊 止损比例: {settings.StopLossRatio}%");
                
                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载交易设置失败: {ex.Message}");
                return GetDefaultSettings();
            }
        }

        public void SaveSettings(TradingSettings settings)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText(_settingsFilePath, jsonContent);
                
                Console.WriteLine("✅ 交易设置已保存");
                Console.WriteLine($"   📊 交易方向: {settings.Side}");
                Console.WriteLine($"   📊 杠杆倍数: {settings.Leverage}x");
                Console.WriteLine($"   📊 仓位模式: {settings.MarginType}");
                Console.WriteLine($"   📊 下单方式: {settings.OrderType}");
                Console.WriteLine($"   📊 止损比例: {settings.StopLossRatio}%");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存交易设置失败: {ex.Message}");
            }
        }

        private TradingSettings GetDefaultSettings()
        {
            return new TradingSettings
            {
                Side = "BUY",
                Leverage = 10,
                MarginType = "ISOLATED", 
                OrderType = "MARKET",
                StopLossRatio = 10.0m,
                Symbol = "",
                PositionSide = "BOTH"
            };
        }
    }
} 