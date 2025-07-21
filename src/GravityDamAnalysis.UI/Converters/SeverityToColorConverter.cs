using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GravityDamAnalysis.UI
{
    public class SeverityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string severity)
            {
                return severity.ToLower() switch
                {
                    "info" or "信息" => new SolidColorBrush(Colors.Blue),
                    "warning" or "警告" => new SolidColorBrush(Colors.Orange),
                    "error" or "错误" => new SolidColorBrush(Colors.Red),
                    "critical" or "严重" => new SolidColorBrush(Colors.DarkRed),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 