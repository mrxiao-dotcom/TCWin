using System.Collections.Generic;
using System.Linq;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BinanceFuturesTrader.Converters;

namespace BinanceFuturesTrader.Models
{
    public class AccountInfo : INotifyPropertyChanged
    {
        public decimal TotalWalletBalance { get; set; } // 权益
        public decimal TotalMarginBalance { get; set; } // 来自API的保证金余额
        public decimal TotalUnrealizedProfit { get; set; } // 浮盈
        public decimal AvailableBalance { get; set; } // 可用余额
        public decimal MaxWithdrawAmount { get; set; }
        
        // 计算属性
        public decimal TotalEquity => TotalMarginBalance; // 使用保证金余额，这个才是真正的账户权益（包含浮动盈亏）
        public decimal AvailableRiskCapital(int riskCapitalTimes) => TotalEquity / riskCapitalTimes;
        
        // 实际保证金占用 - 基于所有持仓计算
        public decimal ActualMarginUsed { get; set; }
        
        // 新增市值相关属性（支持属性变更通知）
        private decimal _totalMarketValue;
        public decimal TotalMarketValue 
        { 
            get => _totalMarketValue;
            set => SetProperty(ref _totalMarketValue, value);
        }
        
        private decimal _longMarketValue;
        public decimal LongMarketValue 
        { 
            get => _longMarketValue;
            set => SetProperty(ref _longMarketValue, value);
        }
        
        private decimal _shortMarketValue;
        public decimal ShortMarketValue 
        { 
            get => _shortMarketValue;
            set => SetProperty(ref _shortMarketValue, value);
        }
        
        public decimal NetMarketValue => LongMarketValue - ShortMarketValue;  // 净市值
        public decimal OverallLeverage => TotalEquity > 0 ? TotalMarketValue / TotalEquity : 0;  // 整体杠杆
        
        // INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            
            // 当市值属性变更时，同时通知计算属性
            if (propertyName == nameof(TotalMarketValue) || propertyName == nameof(LongMarketValue) || propertyName == nameof(ShortMarketValue))
            {
                OnPropertyChanged(nameof(NetMarketValue));
                OnPropertyChanged(nameof(OverallLeverage));
            }
            
            return true;
        }
        
        // 批量更新市值属性，减少UI刷新次数
        public void SetMarketValues(decimal totalMarketValue, decimal longMarketValue, decimal shortMarketValue)
        {
            // 批量更新，最后一次性通知UI
            var totalChanged = !Equals(_totalMarketValue, totalMarketValue);
            var longChanged = !Equals(_longMarketValue, longMarketValue);
            var shortChanged = !Equals(_shortMarketValue, shortMarketValue);
            
            if (totalChanged || longChanged || shortChanged)
            {
                _totalMarketValue = totalMarketValue;
                _longMarketValue = longMarketValue;
                _shortMarketValue = shortMarketValue;
                
                // 批量通知所有相关属性
                if (totalChanged) OnPropertyChanged(nameof(TotalMarketValue));
                if (longChanged) OnPropertyChanged(nameof(LongMarketValue));
                if (shortChanged) OnPropertyChanged(nameof(ShortMarketValue));
                
                // 通知计算属性
                OnPropertyChanged(nameof(NetMarketValue));
                OnPropertyChanged(nameof(OverallLeverage));
            }
        }
        
        // 计算所有持仓的保证金占用和市值（防闪烁版本）
        public void CalculateMarginUsed(IEnumerable<PositionInfo> positions)
        {
            // 使用局部变量计算，避免UI闪烁
            decimal newActualMarginUsed = 0;
            decimal newTotalMarketValue = 0;
            decimal newLongMarketValue = 0;
            decimal newShortMarketValue = 0;
            
            if (positions == null)
            {
                // 一次性更新所有属性
                ActualMarginUsed = newActualMarginUsed;
                TotalMarketValue = newTotalMarketValue;
                LongMarketValue = newLongMarketValue;
                ShortMarketValue = newShortMarketValue;
                return;
            }

            Console.WriteLine("📊 计算保证金占用和市值:");
            Console.WriteLine($"📋 总持仓数量: {positions.Count()}");
            
            foreach (var position in positions)
            {
                Console.WriteLine($"🔍 检查持仓: {position.Symbol}, 数量={position.PositionAmt:F4}, 标记价={position.MarkPrice:F4}");
                
                if (position.PositionAmt != 0) // 只计算有持仓的
                {
                    // 计算保证金
                    var apiMargin = position.IsolatedMargin;
                    var calculatedMargin = position.RequiredMargin;
                    var marginForPosition = apiMargin > 0 ? apiMargin : calculatedMargin;
                    newActualMarginUsed += marginForPosition;
                    
                    // 计算市值
                    var positionValue = position.PositionValue;
                    newTotalMarketValue += positionValue;
                    
                    Console.WriteLine($"   💵 货值计算: |{position.PositionAmt:F4}| × {position.MarkPrice:F4} = {positionValue:F2}");
                    
                    if (position.PositionAmt > 0)  // 多头持仓
                    {
                        newLongMarketValue += positionValue;
                        Console.WriteLine($"   📈 添加到多头市值: {positionValue:F2}");
                    }
                    else  // 空头持仓
                    {
                        newShortMarketValue += positionValue;
                        Console.WriteLine($"   📉 添加到空头市值: {positionValue:F2}");
                    }
                    
                    var formattedMarkPrice = PriceFormatConverter.FormatPrice(position.MarkPrice);
                    var marginSource = apiMargin > 0 ? "API值" : "计算值";
                    var direction = position.PositionAmt > 0 ? "多头" : "空头";
                    
                    Console.WriteLine($"   💰 {position.Symbol} ({direction}): 数量={position.PositionAmt:F4}, 标记价={formattedMarkPrice}, " +
                                    $"杠杆={position.Leverage}x, 货值={position.PositionValue:F2}");
                    Console.WriteLine($"      保证金={marginForPosition:F2} ({marginSource}), " +
                                    $"API值={apiMargin:F2}, 计算值={calculatedMargin:F2}");
                    
                    if (apiMargin == 0 && calculatedMargin > 0)
                    {
                        Console.WriteLine($"      ⚠️ API保证金为0，使用计算值: {calculatedMargin:F2}");
                    }
                    else if (Math.Abs(apiMargin - calculatedMargin) > 0.01m && apiMargin > 0 && calculatedMargin > 0)
                    {
                        var difference = Math.Abs(apiMargin - calculatedMargin);
                        var percentDiff = difference / Math.Max(apiMargin, calculatedMargin) * 100;
                        Console.WriteLine($"      ℹ️ API值与计算值差异: {difference:F2} ({percentDiff:F1}%)");
                    }
                }
                else
                {
                    Console.WriteLine($"   ⚪ {position.Symbol}: 无持仓，跳过计算");
                }
            }
            
            // 计算完成后一次性更新所有属性，避免UI闪烁
            ActualMarginUsed = newActualMarginUsed;
            
            // 使用批量更新方法，减少UI刷新次数
            SetMarketValues(newTotalMarketValue, newLongMarketValue, newShortMarketValue);
            
            Console.WriteLine($"✅ 总保证金占用: {ActualMarginUsed:F2} USDT");
            Console.WriteLine($"📊 市值统计: 总市值={TotalMarketValue:F2}, 多头={LongMarketValue:F2}, 空头={ShortMarketValue:F2}, 净市值={NetMarketValue:F2}");
            Console.WriteLine($"📈 整体杠杆: {OverallLeverage:F2}x (总市值/账户权益)");
        }

        // 测试方法：创建模拟数据验证计算逻辑
        public void TestMarketValueCalculation()
        {
            Console.WriteLine("🧪 开始测试市值计算逻辑...");
            
            var testPositions = new List<PositionInfo>
            {
                new PositionInfo 
                { 
                    Symbol = "BTCUSDT", 
                    PositionAmt = 0.1m, 
                    MarkPrice = 50000m, 
                    Leverage = 10 
                },
                new PositionInfo 
                { 
                    Symbol = "ETHUSDT", 
                    PositionAmt = -2m, 
                    MarkPrice = 3000m, 
                    Leverage = 5 
                },
                new PositionInfo 
                { 
                    Symbol = "ADAUSDT", 
                    PositionAmt = 0m, 
                    MarkPrice = 1m, 
                    Leverage = 20 
                }
            };
            
            CalculateMarginUsed(testPositions);
            
            Console.WriteLine("🧪 测试结果:");
            Console.WriteLine($"   预期BTC货值: |0.1| × 50000 = 5000");
            Console.WriteLine($"   预期ETH货值: |2| × 3000 = 6000");
            Console.WriteLine($"   预期总市值: 5000 + 6000 = 11000");
            Console.WriteLine($"   预期多头市值: 5000");
            Console.WriteLine($"   预期空头市值: 6000");
            Console.WriteLine($"   预期净市值: 5000 - 6000 = -1000");
            Console.WriteLine($"   实际结果: 总={TotalMarketValue}, 多头={LongMarketValue}, 空头={ShortMarketValue}, 净={NetMarketValue}");
        }
    }
} 