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
        public decimal TotalEquity => TotalWalletBalance + TotalUnrealizedProfit; // 权益
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
                    var marginForPosition = position.RequiredMargin;
                    totalMargin += marginForPosition;
                    
                    var formattedMarkPrice = PriceFormatConverter.FormatPrice(position.MarkPrice);
                    Console.WriteLine($"   💰 {position.Symbol}: 数量={position.PositionAmt:F4}, 标记价={formattedMarkPrice}, " +
                                    $"杠杆={position.Leverage}x, 货值={position.PositionValue:F2}, 保证金={marginForPosition:F2}");
                }
            }
            
            ActualMarginUsed = totalMargin;
            Console.WriteLine($"✅ 总保证金占用: {ActualMarginUsed:F2} USDT");
        }
    }
} 