using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flashcards.Models;

namespace Flashcards.Data;

public static class FlashcardDataSource
{
    private const string CsvFileName = "flashcards.csv";
    private const string CsvHeader = "Danish,English,Type,Conjugation,ExampleDanish,ExampleEnglish,ContextualTip,Mnemonic";

    private static string? _overrideCsvPath;

    private static string CsvPath =>
        _overrideCsvPath ?? Path.Combine(AppContext.BaseDirectory, "Data", CsvFileName);

    /// <summary>
    /// Call before first access to override the default CSV path (e.g. on Android).
    /// </summary>
    public static void SetCsvPath(string path) => _overrideCsvPath = path;

    private static List<FlashcardEntry>? _flashcards;
    private static List<FlashcardEntry> Flashcards => _flashcards ??= LoadFromCsv();

    public static IReadOnlyList<FlashcardEntry> GetFlashcards() => Flashcards;

    public static bool TryAddFlashcard(FlashcardEntry flashcard)
    {
        var normalizedKey = NormalizeDanishKey(flashcard.Danish);
        if (Flashcards.Any(card => NormalizeDanishKey(card.Danish) == normalizedKey))
        {
            return false;
        }

        Flashcards.Add(flashcard);
        SaveToCsv();
        return true;
    }

    public static bool TryUpdateFlashcard(string originalDanish, FlashcardEntry updated)
    {
        var originalKey = NormalizeDanishKey(originalDanish);
        var updatedKey = NormalizeDanishKey(updated.Danish);

        // If changing the Danish key, ensure no other record has the new key
        if (originalKey != updatedKey && Flashcards.Any(c => NormalizeDanishKey(c.Danish) == updatedKey))
            return false;

        var index = Flashcards.FindIndex(c => NormalizeDanishKey(c.Danish) == originalKey);
        if (index < 0) return false;

        Flashcards[index] = updated;
        SaveToCsv();
        return true;
    }

    // Exposed so callers can verify where data is being written.
    public static string RuntimeCsvPath => CsvPath;
    public static string SourceCsvPath => BuildInfo.CsvSourcePath;

    private static List<FlashcardEntry> LoadFromCsv()
    {
        var list = new List<FlashcardEntry>();
        var seenDanishKeys = new HashSet<string>(StringComparer.Ordinal);

        if (!File.Exists(CsvPath))
            return list;

        var lines = File.ReadAllLines(CsvPath);

        foreach (var line in lines)
        {
            // Skip header and blank lines
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith(CsvHeader, StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = SplitCsvLine(line);
            if (parts.Length < 2) continue;

            // Type (column 3) is mandatory – skip rows that are missing it.
            if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
            {
                Console.Error.WriteLine($"[FlashcardDataSource] Skipping entry '{parts[0]}': Type is required but missing.");
                continue;
            }

            var entry = new FlashcardEntry
            {
                Danish = parts[0],
                English = parts[1],
                Type = parts[2],
                Conjugation = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3])
                    ? parts[3]
                    : null,
                ExampleDanish = parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4])
                    ? parts[4]
                    : null,
                ExampleEnglish = parts.Length > 5 && !string.IsNullOrWhiteSpace(parts[5])
                    ? parts[5]
                    : null,
                ContextualTip = parts.Length > 6 && !string.IsNullOrWhiteSpace(parts[6])
                    ? parts[6]
                    : null,
                Mnemonic = parts.Length > 7 && !string.IsNullOrWhiteSpace(parts[7])
                    ? parts[7]
                    : null,
            };

            // Keep first occurrence from CSV if duplicate Danish keys are present.
            if (!seenDanishKeys.Add(NormalizeDanishKey(entry.Danish)))
            {
                continue;
            }

            list.Add(entry);
        }

        return list;
    }

    private static void SaveToCsv()
    {
        var lines = new List<string> { CsvHeader };

        foreach (var card in Flashcards)
        {
            lines.Add($"{EscapeCsvField(card.Danish)},{EscapeCsvField(card.English)},{EscapeCsvField(card.Type)},{EscapeCsvField(card.Conjugation ?? string.Empty)},{EscapeCsvField(card.ExampleDanish ?? string.Empty)},{EscapeCsvField(card.ExampleEnglish ?? string.Empty)},{EscapeCsvField(card.ContextualTip ?? string.Empty)},{EscapeCsvField(card.Mnemonic ?? string.Empty)}");
        }

        // Write to the runtime output directory (where the app reads from)
        var runtimeDir = Path.GetDirectoryName(CsvPath);
        if (runtimeDir is not null && !Directory.Exists(runtimeDir))
            Directory.CreateDirectory(runtimeDir);
        File.WriteAllLines(CsvPath, lines);

        // Also write back to the solution source file so the data persists across builds
        try
        {
            var sourceDir = Path.GetDirectoryName(BuildInfo.CsvSourcePath);
            if (sourceDir is not null && !Directory.Exists(sourceDir))
                Directory.CreateDirectory(sourceDir);
            File.WriteAllLines(BuildInfo.CsvSourcePath, lines);
        }
        catch
        {
            // Non-fatal: source write may fail in environments without solution access
        }
    }

    // Simple CSV escaping: wrap field in quotes if it contains commas, quotes or newlines.
    private static string EscapeCsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string NormalizeDanishKey(string danish)
    {
        return danish.Trim().ToUpperInvariant();
    }

    // Minimal CSV line splitter that handles quoted fields.
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
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}