using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Flashcards.Converters;

public class EqualityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value == null || parameter == null)
            return false;

        // Try to convert both to int for comparison
        if (int.TryParse(value.ToString(), out int valueInt) &&
            int.TryParse(parameter.ToString(), out int paramInt))
        {
            return valueInt == paramInt;
        }

        // Fallback to direct equality
        return value.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotImplementedException();
    }
}
