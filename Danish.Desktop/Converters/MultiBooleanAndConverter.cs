using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Danish.Desktop.Converters;

public class MultiBooleanAndConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count == 0) return false;
        foreach (var v in values)
        {
            if (v is bool b) { if (!b) return false; }
            else if (v is null) return false;
        }
        return true;
    }
}
