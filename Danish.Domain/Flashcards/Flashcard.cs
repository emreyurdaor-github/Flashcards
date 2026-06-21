using System.ComponentModel;

namespace Danish.Domain;

[Flags]
public enum WordType
{
    [Description("v.")]
    Verb = 1,
    [Description("n.")]
    Noun = 2,
    [Description("adj.")]
    Adjective = 4,
    [Description("adv.")]
    Adverb = 8,
    [Description("conj.")]
    Conjugation = 16,
}

public class Flashcard
{
    public required string DanishWord { get; set; }
    public required string EnglishWord { get; set; }
    public required WordType WordType { get; set; }
    public string? Conjugation { get; set; }
    public string? ExampleDanish { get; set; }
    public string? ExampleEnglish { get; set; }
    public string? Tip { get; set; }
    public string? Mnemonic { get; set; }
}