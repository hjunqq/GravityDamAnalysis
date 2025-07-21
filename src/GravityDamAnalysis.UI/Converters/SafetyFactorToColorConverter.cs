using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GravityDamAnalysis.UI
{
    public class SafetyFactorToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double safetyFactor)
            {
                if (safetyFactor >= 1.5) return new SolidColorBrush(Colors.Green);
                if (safetyFactor >= 1.3) return new SolidColorBrush(Colors.Orange);
                return new SolidColorBrush(Colors.Red);
            }
            return new SolidColorBrush(Colors.Gray);
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 