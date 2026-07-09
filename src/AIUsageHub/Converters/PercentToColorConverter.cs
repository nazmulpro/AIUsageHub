using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AIUsageHub.Converters;

public class PercentToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            "blue" => new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),   // #3B82F6
            "yellow" => new SolidColorBrush(Color.FromArgb(255, 245, 158, 11)),  // #F59E0B
            "red" => new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)),     // #EF4444
            _ => new SolidColorBrush(Color.FromArgb(255, 59, 130, 246))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}