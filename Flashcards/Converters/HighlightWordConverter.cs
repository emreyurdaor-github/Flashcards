using System;
using System.Collections.Generic;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace Flashcards.Converters;

/// <summary>
/// Converter to highlight a specific word in text with bold and underline formatting
/// Not used directly as binding - used as a helper for TextBlock Run elements
/// </summary>
public class HighlightWordConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return null;

        var text = values[0] as string;
        var wordToHighlight = values[1] as string;

        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(wordToHighlight))
            return null;

        // This converter is a helper - the actual highlighting logic is used in the ViewModel
        // to create formatted text runs
        return text;
    }
}
