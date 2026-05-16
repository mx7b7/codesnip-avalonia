using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace CodeSnip.Helpers;

public class UtcStringToLocalConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            // SQLite format: "2024-01-15 18:22:10"
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            {
                var local = dt.ToLocalTime();
                return local.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        return value; // fallback
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}

