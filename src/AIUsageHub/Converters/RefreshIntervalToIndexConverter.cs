using System.Globalization;
using System.Windows.Data;

namespace AIUsageHub.Converters;

public class RefreshIntervalToIndexConverter : IValueConverter
{
    private static readonly int[] Minutes = { 1, 5, 10, 15 };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int minutes)
        {
            int idx = Array.IndexOf(Minutes, minutes);
            return idx < 0 ? 1 : idx;
        }
        return 1;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && index >= 0 && index < Minutes.Length)
            return Minutes[index];
        return 5;
    }
}
