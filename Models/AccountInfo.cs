using System.Collections.Generic;
using System.Linq;
using System;
using BinanceFuturesTrader.Converters;

namespace BinanceFuturesTrader.Models
{
    public class AccountInfo
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
        
        // 计算所有持仓的保证金占用
        public void CalculateMarginUsed(IEnumerable<PositionInfo> positions)
        {
            if (positions == null)
            {
                ActualMarginUsed = 0;
                return;
            }

            decimal totalMargin = 0;
            Console.WriteLine("📊 计算保证金占用:");
            
            foreach (var position in positions)
            {
                if (position.PositionAmt != 0) // 只计算有持仓的
                {
                    // 优先使用API返回的IsolatedMargin，如果为0则使用计算值
                    var apiMargin = position.IsolatedMargin;
                    var calculatedMargin = position.RequiredMargin;
                    var marginForPosition = apiMargin > 0 ? apiMargin : calculatedMargin;
                    
                    totalMargin += marginForPosition;
                    
                    var formattedMarkPrice = PriceFormatConverter.FormatPrice(position.MarkPrice);
                    var marginSource = apiMargin > 0 ? "API值" : "计算值";
                    
                    Console.WriteLine($"   💰 {position.Symbol}: 数量={position.PositionAmt:F4}, 标记价={formattedMarkPrice}, " +
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
            }
            
            ActualMarginUsed = totalMargin;
            Console.WriteLine($"✅ 总保证金占用: {ActualMarginUsed:F2} USDT");
        }
    }
} 