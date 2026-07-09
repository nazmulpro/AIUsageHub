using System.Globalization;
using System.Windows.Data;

namespace AIUsageHub.Converters;

/// <summary>
/// Multi-value converter for the progress-bar fill width.
/// Values: [0] = track ActualWidth (double), [1] = Used (double), [2] = Limit (double).
/// Returns the fill width as a fraction of the track width.
/// </summary>
public class ProgressFillWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3) return 0.0;
        var trackWidth = values[0] as double? ?? 0;
        var used = values[1] as double? ?? 0;
        var limit = values[2] as double? ?? 0;
        if (limit <= 0 || trackWidth <= 0) return 0.0;
        var ratio = used / limit;
        if (ratio < 0) ratio = 0;
        if (ratio > 1) ratio = 1;
        return ratio * trackWidth;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
