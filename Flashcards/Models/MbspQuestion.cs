namespace Flashcards.Models;

public sealed class MbspQuestion
{
    public required string Question { get; init; }
    public string? QuestionEnglish { get; init; }
    public required string ChoiceA { get; init; }
    public string? ChoiceAEnglish { get; init; }
    public required string ChoiceB { get; init; }
    public string? ChoiceBEnglish { get; init; }
    public string? ChoiceC { get; init; }
    public string? ChoiceCEnglish { get; init; }
    /// <summary>The text of the correct answer (matches one of ChoiceA/B/C)</summary>
    public required string CorrectAnswer { get; init; }

    public bool HasChoiceC => !string.IsNullOrWhiteSpace(ChoiceC);
    public bool HasQuestionEnglish => !string.IsNullOrWhiteSpace(QuestionEnglish);
    public bool HasChoiceAEnglish => !string.IsNullOrWhiteSpace(ChoiceAEnglish);
    public bool HasChoiceBEnglish => !string.IsNullOrWhiteSpace(ChoiceBEnglish);
    public bool HasChoiceCEnglish => !string.IsNullOrWhiteSpace(ChoiceCEnglish);
}