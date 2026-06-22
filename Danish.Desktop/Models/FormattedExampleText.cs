namespace Danish.Desktop.Models;

public class FormattedExampleText
{
    public string FullText        { get; set; } = string.Empty;
    public string BeforeWord      { get; set; } = string.Empty;
    public string HighlightedWord { get; set; } = string.Empty;
    public string AfterWord       { get; set; } = string.Empty;

    public bool IsFullTextOnly => string.IsNullOrEmpty(HighlightedWord);
}
