namespace Danish.Desktop.Models;

public sealed class FlashcardEntry
{
    public required string Danish { get; init; }
    public required string English { get; init; }
    public required string Type { get; init; }
    public string? Conjugation { get; init; }
    public string? ExampleDanish { get; init; }
    public string? ExampleEnglish { get; init; }
    public string? ContextualTip { get; init; }
    public string? Mnemonic { get; init; }

    public bool HasConjugation    => !string.IsNullOrWhiteSpace(Conjugation);
    public bool HasExampleDanish  => !string.IsNullOrWhiteSpace(ExampleDanish);
    public bool HasExampleEnglish => !string.IsNullOrWhiteSpace(ExampleEnglish);
    public bool HasContextualTip  => !string.IsNullOrWhiteSpace(ContextualTip);
    public bool HasMnemonic       => !string.IsNullOrWhiteSpace(Mnemonic);
}
