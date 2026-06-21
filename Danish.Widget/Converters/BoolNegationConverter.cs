using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Danish.Widget.Converters;

/// <summary>
/// Converter that negates a boolean value
/// </summary>
public class BoolNegationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return value;
    }
}
