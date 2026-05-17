namespace Flashcards.Models;

public sealed class MbspQuestion
{
    public required string Question { get; init; }
    public required string ChoiceA { get; init; }
    public required string ChoiceB { get; init; }
    public required string ChoiceC { get; init; }
    /// <summary>Which choice is correct: A, B, or C</summary>
    public required string CorrectChoice { get; init; }
}
