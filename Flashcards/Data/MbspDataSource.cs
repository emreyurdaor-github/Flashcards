using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flashcards.Models;

namespace Flashcards.Data;

public static class MbspDataSource
{
    private const string CsvFileName = "medborgerskabsproeven.csv";
    // New 10-column format: Question,QuestionEnglish,ChoiceA,ChoiceAEnglish,ChoiceB,ChoiceBEnglish,ChoiceC,ChoiceCEnglish,CorrectAnswer,Period
    private const string CsvHeader = "Question,QuestionEnglish,ChoiceA,ChoiceAEnglish,ChoiceB,ChoiceBEnglish,ChoiceC,ChoiceCEnglish,CorrectAnswer,Period";

    private static string? _overrideCsvPath;

    private static string CsvPath =>
        _overrideCsvPath ?? Path.Combine(AppContext.BaseDirectory, "Data", CsvFileName);

    public static void SetCsvPath(string path) => _overrideCsvPath = path;

    private static List<MbspQuestion>? _questions;
    private static List<MbspQuestion> Questions => _questions ??= LoadFromCsv();

    public static IReadOnlyList<MbspQuestion> GetQuestions() => Questions;

    public static bool TryAddQuestion(MbspQuestion question)
    {
        var key = NormalizeKey(question.Question);
        if (Questions.Any(q => NormalizeKey(q.Question) == key))
            return false;

        Questions.Add(question);
        SaveToCsv();
        return true;
    }

    public static bool TryUpdateQuestion(string originalQuestion, MbspQuestion updated)
    {
        var originalKey = NormalizeKey(originalQuestion);
        var updatedKey = NormalizeKey(updated.Question);

        if (originalKey != updatedKey && Questions.Any(q => NormalizeKey(q.Question) == updatedKey))
            return false;

        var index = Questions.FindIndex(q => NormalizeKey(q.Question) == originalKey);
        if (index < 0) return false;

        Questions[index] = updated;
        SaveToCsv();
        return true;
    }

    public static string RuntimeCsvPath => CsvPath;
    public static string SourceCsvPath => BuildInfo.MbspCsvSourcePath;

    private static List<MbspQuestion> LoadFromCsv()
    {
        var list = new List<MbspQuestion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (!File.Exists(CsvPath))
            return list;

        var lines = File.ReadAllLines(CsvPath);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith("Question", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = SplitCsvLine(line);

            MbspQuestion entry;

            if (parts.Length >= 9)
            {
                // New 10-column format: Question,QuestionEnglish,ChoiceA,ChoiceAEnglish,ChoiceB,ChoiceBEnglish,ChoiceC,ChoiceCEnglish,CorrectAnswer,Period
                entry = new MbspQuestion
                {
                    Question = parts[0],
                    QuestionEnglish = string.IsNullOrWhiteSpace(parts[1]) ? null : parts[1],
                    ChoiceA = parts[2],
                    ChoiceAEnglish = string.IsNullOrWhiteSpace(parts[3]) ? null : parts[3],
                    ChoiceB = parts[4],
                    ChoiceBEnglish = string.IsNullOrWhiteSpace(parts[5]) ? null : parts[5],
                    ChoiceC = string.IsNullOrWhiteSpace(parts[6]) ? null : parts[6],
                    ChoiceCEnglish = string.IsNullOrWhiteSpace(parts[7]) ? null : parts[7],
                    CorrectAnswer = parts[8],
                    Period = parts.Length >= 10 && !string.IsNullOrWhiteSpace(parts[9]) ? parts[9] : null,
                };
            }
            else if (parts.Length >= 6)
            {
                // Old 6-column format: Question,QuestionEnglish,ChoiceA,ChoiceB,ChoiceC,CorrectAnswer
                var lastField = parts[5].Trim();
                bool isLegacyLetter = lastField.Length == 1 &&
                    (lastField == "A" || lastField == "B" || lastField == "C");

                string correctAnswer;
                if (isLegacyLetter)
                {
                    correctAnswer = lastField switch
                    {
                        "A" => parts[2],
                        "B" => parts[3],
                        "C" => parts[4],
                        _ => parts[2],
                    };
                }
                else
                {
                    correctAnswer = lastField;
                }

                entry = new MbspQuestion
                {
                    Question = parts[0],
                    QuestionEnglish = string.IsNullOrWhiteSpace(parts[1]) ? null : parts[1],
                    ChoiceA = parts[2],
                    ChoiceAEnglish = null,
                    ChoiceB = parts[3],
                    ChoiceBEnglish = null,
                    ChoiceC = string.IsNullOrWhiteSpace(parts[4]) ? null : parts[4],
                    ChoiceCEnglish = null,
                    CorrectAnswer = correctAnswer,
                    Period = null,
                };
            }
            else if (parts.Length >= 5)
            {
                // Old 5-column format: Question, ChoiceA, ChoiceB, ChoiceC, CorrectChoice(letter)
                var letter = parts[4].Trim().ToUpperInvariant();
                var choiceC = parts[3].Trim();
                entry = new MbspQuestion
                {
                    Question = parts[0],
                    QuestionEnglish = null,
                    ChoiceA = parts[1],
                    ChoiceAEnglish = null,
                    ChoiceB = parts[2],
                    ChoiceBEnglish = null,
                    ChoiceC = (choiceC == "x" || string.IsNullOrWhiteSpace(choiceC)) ? null : choiceC,
                    ChoiceCEnglish = null,
                    CorrectAnswer = letter switch
                    {
                        "A" => parts[1],
                        "B" => parts[2],
                        "C" => parts[3],
                        _ => parts[1],
                    },
                    Period = null,
                };
            }
            else continue;

            if (!seen.Add(NormalizeKey(entry.Question)))
                continue;

            list.Add(entry);
        }

        return list;
    }

    private static void SaveToCsv()
    {
        var lines = new List<string> { CsvHeader };

        foreach (var q in Questions)
        {
            lines.Add(string.Join(",", new[]
            {
                Escape(q.Question),
                Escape(q.QuestionEnglish ?? string.Empty),
                Escape(q.ChoiceA),
                Escape(q.ChoiceAEnglish ?? string.Empty),
                Escape(q.ChoiceB),
                Escape(q.ChoiceBEnglish ?? string.Empty),
                Escape(q.ChoiceC ?? string.Empty),
                Escape(q.ChoiceCEnglish ?? string.Empty),
                Escape(q.CorrectAnswer),
                Escape(q.Period ?? string.Empty),
            }));
        }

        var runtimeDir = Path.GetDirectoryName(CsvPath);
        if (runtimeDir is not null && !Directory.Exists(runtimeDir))
            Directory.CreateDirectory(runtimeDir);
        File.WriteAllLines(CsvPath, lines);

        try
        {
            var sourcePath = BuildInfo.MbspCsvSourcePath;
            var sourceDir = Path.GetDirectoryName(sourcePath);
            if (sourceDir is not null && !Directory.Exists(sourceDir))
                Directory.CreateDirectory(sourceDir);
            File.WriteAllLines(sourcePath, lines);
        }
        catch { }
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string NormalizeKey(string key) => key.Trim().ToUpperInvariant();

    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}