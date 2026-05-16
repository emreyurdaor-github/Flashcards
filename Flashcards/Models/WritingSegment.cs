namespace Flashcards.Models;

/// <summary>
/// Represents a segment of writing text, optionally highlighted in green.
/// </summary>
public class WritingSegment
{
    public string Text { get; set; } = string.Empty;
    public bool IsHighlighted { get; set; }
}
