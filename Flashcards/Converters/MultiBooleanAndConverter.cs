using System;
using System.Collections.Generic;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Flashcards.Converters;

/// <summary>
/// Converter to combine multiple boolean values using AND logic for visibility
/// All values must be true for the result to be true
/// </summary>
public class MultiBooleanAndConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count == 0)
            return false;

        // All values must be true
        foreach (var value in values)
        {
            if (value is bool boolValue)
            {
                if (!boolValue)
                    return false;
            }
            else if (value is null)
            {
                return false;
            }
        }

        return true;
    }
}
