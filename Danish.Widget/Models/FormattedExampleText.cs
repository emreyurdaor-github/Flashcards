namespace Danish.Widget.Models;

/// <summary>
/// Represents text formatted for highlighting specific words
/// </summary>
public class FormattedExampleText
{
    /// <summary>
    /// The complete original text
    /// </summary>
    public string FullText { get; set; } = string.Empty;

    /// <summary>
    /// Text before the highlighted word
    /// </summary>
    public string BeforeWord { get; set; } = string.Empty;

    /// <summary>
    /// The word to be highlighted (bold and underlined)
    /// </summary>
    public string HighlightedWord { get; set; } = string.Empty;

    /// <summary>
    /// Text after the highlighted word
    /// </summary>
    public string AfterWord { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if highlighting failed and only the full text is available
    /// </summary>
    public bool IsFullTextOnly => string.IsNullOrEmpty(HighlightedWord);
}
