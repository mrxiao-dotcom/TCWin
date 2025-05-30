using System;
using System.Globalization;
using System.Windows.Data;

namespace BinanceFuturesTrader.Converters
{
    public class PriceFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal price)
            {
                return FormatPrice(price);
            }
            
            if (value is double doublePrice)
            {
                return FormatPrice((decimal)doublePrice);
            }
            
            return value?.ToString() ?? "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && decimal.TryParse(str, out decimal result))
            {
                return result;
            }
            return 0m;
        }

        /// <summary>
        /// 智能格式化价格，根据价格大小动态调整精度
        /// </summary>
        /// <param name="price">价格</param>
        /// <returns>格式化后的价格字符串</returns>
        public static string FormatPrice(decimal price)
        {
            if (price == 0)
                return "0";

            var absPrice = Math.Abs(price);

            // 根据价格大小选择合适的小数位数
            if (absPrice >= 1000)
            {
                // ≥1000: 显示2位小数 (如 45000.00)
                return price.ToString("F2");
            }
            else if (absPrice >= 100)
            {
                // ≥100: 显示3位小数 (如 234.567)
                return price.ToString("F3");
            }
            else if (absPrice >= 10)
            {
                // ≥10: 显示4位小数 (如 45.6789)
                return price.ToString("F4");
            }
            else if (absPrice >= 1)
            {
                // ≥1: 显示5位小数 (如 2.34567)
                return price.ToString("F5");
            }
            else if (absPrice >= 0.1m)
            {
                // ≥0.1: 显示6位小数 (如 0.234567)
                return price.ToString("F6");
            }
            else if (absPrice >= 0.01m)
            {
                // ≥0.01: 显示7位小数 (如 0.0234567)
                return price.ToString("F7");
            }
            else
            {
                // <0.01: 显示8位小数 (如 0.00234567)
                return price.ToString("F8");
            }
        }
    }
} 