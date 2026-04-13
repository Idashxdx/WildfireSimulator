using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WildfireSimulator.Client.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string colors)
        {
            var colorParts = colors.Split(';');
            if (colorParts.Length >= 2)
            {
                var trueColor = Color.Parse(colorParts[0].Trim());
                var falseColor = Color.Parse(colorParts[1].Trim());
                return new SolidColorBrush(boolValue ? trueColor : falseColor);
            }
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
