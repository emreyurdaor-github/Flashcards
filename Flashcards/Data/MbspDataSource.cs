using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flashcards.Models;

namespace Flashcards.Data;

public static class MbspDataSource
{
    private const string CsvFileName = "medborgerskabsproeven.csv";
    private const string CsvHeader = "Question,ChoiceA,ChoiceB,ChoiceC,CorrectChoice";

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
            if (parts.Length < 5) continue;

            var entry = new MbspQuestion
            {
                Question = parts[0],
                ChoiceA = parts[1],
                ChoiceB = parts[2],
                ChoiceC = parts[3],
                CorrectChoice = parts[4].Trim().ToUpperInvariant(),
            };

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
            lines.Add($"{Escape(q.Question)},{Escape(q.ChoiceA)},{Escape(q.ChoiceB)},{Escape(q.ChoiceC)},{Escape(q.CorrectChoice)}");
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
