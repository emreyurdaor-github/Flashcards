namespace Flashcards.Models;

/// <summary>
/// Represents a segment of writing text, optionally highlighted in green.
/// </summary>
public class WritingSegment
{
    public string Text { get; set; } = string.Empty;
    public bool IsHighlighted { get; set; }
    /// <summary>True when the word matches a vocabulary dictionary entry (rendered bold + italic).</summary>
    public bool IsBoldItalic { get; set; }
    /// <summary>
    /// Optional tooltip text shown on hover (set for Danish key matches to display the English translation).
    /// </summary>
    public string? Tooltip { get; set; }
}
