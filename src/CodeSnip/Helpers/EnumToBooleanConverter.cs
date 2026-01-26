using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace CodeSnip.Helpers;

public class EnumToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? parameterString = parameter as string;
        if (parameterString == null || value == null)
            return false;

        return value.ToString() == parameterString;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? parameterString = parameter as string;
        if (parameterString == null)
            return null; // Avalonia: null = skip binding

        if (value is bool boolValue && boolValue)
        {
            return Enum.Parse(targetType, parameterString);
        }

        return null; // do not change the source value
    }
}
