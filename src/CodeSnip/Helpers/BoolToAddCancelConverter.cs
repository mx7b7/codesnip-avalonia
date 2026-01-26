using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace CodeSnip.Helpers;

public class BoolToAddCancelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "Cancel" : "Add";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
