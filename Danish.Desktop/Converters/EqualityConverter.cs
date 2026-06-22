using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Danish.Desktop.Converters;

public class EqualityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value == null || parameter == null) return false;
        if (int.TryParse(value.ToString(), out int vi) && int.TryParse(parameter.ToString(), out int pi))
            return vi == pi;
        return value.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
        => throw new NotImplementedException();
}
