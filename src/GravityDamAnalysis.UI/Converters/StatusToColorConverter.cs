using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GravityDamAnalysis.UI
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLower() switch
                {
                    "已连接" or "就绪" or "成功" => new SolidColorBrush(Colors.Green),
                    "连接中" or "分析中" or "警告" => new SolidColorBrush(Colors.Orange),
                    "断开" or "失败" or "错误" => new SolidColorBrush(Colors.Red),
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