using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BinanceFuturesTrader.Converters
{
    /// <summary>
    /// 字符串到可见性转换器
    /// 空字符串或null -> Collapsed
    /// 非空字符串 -> Visible
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return string.IsNullOrEmpty(str) ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 