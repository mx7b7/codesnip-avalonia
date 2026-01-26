using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace CodeSnip.Helpers;

public class LanguageToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == AvaloniaProperty.UnsetValue) return false;

        if (value is not string currentLanguageCode || parameter is not string targetLanguageCodes)
            return false;

        var supportedLanguages = targetLanguageCodes.Split(',');

        return supportedLanguages.Contains(currentLanguageCode, StringComparer.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
