using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace CodeSnip.Helpers
{
    public class BoolToColorConverter : IValueConverter
    {
        public Color TrueColor { get; set; } = Colors.LimeGreen;
        public Color FalseColor { get; set; } = Colors.DarkRed;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool flag = value is bool b && b;
            return new SolidColorBrush(flag ? TrueColor : FalseColor);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

}
