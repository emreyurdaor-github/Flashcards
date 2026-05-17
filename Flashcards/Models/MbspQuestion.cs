namespace Flashcards.Models;

public sealed class MbspQuestion
{
    public required string Question { get; init; }
    public string? QuestionEnglish { get; init; }
    public required string ChoiceA { get; init; }
    public required string ChoiceB { get; init; }
    public string? ChoiceC { get; init; }
    /// <summary>The text of the correct answer (matches one of ChoiceA/B/C)</summary>
    public required string CorrectAnswer { get; init; }

    public bool HasChoiceC => !string.IsNullOrWhiteSpace(ChoiceC);
    public bool HasQuestionEnglish => !string.IsNullOrWhiteSpace(QuestionEnglish);
}