using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flashcards.Data;

public class WritingEntry
{
    public string DanishWriting { get; set; } = string.Empty;
    public string EnglishWriting { get; set; } = string.Empty;
}

public static class WritingDataSource
{
    private const string CsvFileName = "writing.csv";
    private const string CsvHeader = "DanishWriting,EnglishWriting";

    private static string? _overrideCsvPath;

    private static string CsvPath =>
        _overrideCsvPath ?? Path.Combine(AppContext.BaseDirectory, "Data", CsvFileName);

    /// <summary>
    /// Call before first access to override the default CSV path (e.g. on Android).
    /// </summary>
    public static void SetCsvPath(string path) => _overrideCsvPath = path;

    private static List<WritingEntry>? _writingEntries;
    private static List<WritingEntry> WritingEntries => _writingEntries ??= LoadFromCsv();

    public static IReadOnlyList<WritingEntry> GetWritingEntries() => WritingEntries;

    public static bool TryAddWritingEntry(WritingEntry entry)
    {
        var normalizedKey = NormalizeDanishKey(entry.DanishWriting);
        if (WritingEntries.Any(e => NormalizeDanishKey(e.DanishWriting) == normalizedKey))
        {
            return false;
        }

        WritingEntries.Add(entry);
        SaveToCsv();
        return true;
    }

    public static bool TryUpdateWritingEntry(string originalDanish, WritingEntry updated)
    {
        var originalKey = NormalizeDanishKey(originalDanish);
        var updatedKey = NormalizeDanishKey(updated.DanishWriting);

        // If changing the Danish key, ensure no other record has the new key
        if (originalKey != updatedKey && WritingEntries.Any(e => NormalizeDanishKey(e.DanishWriting) == updatedKey))
            return false;

        var index = WritingEntries.FindIndex(e => NormalizeDanishKey(e.DanishWriting) == originalKey);
        if (index < 0) return false;

        WritingEntries[index] = updated;
        SaveToCsv();
        return true;
    }

    // Exposed so callers can verify where data is being written.
    public static string RuntimeCsvPath => CsvPath;
    public static string SourceCsvPath => BuildInfo.CsvSourcePath;

    private static List<WritingEntry> LoadFromCsv()
    {
        var list = new List<WritingEntry>();
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

            var entry = new WritingEntry
            {
                DanishWriting = parts[0],
                EnglishWriting = parts[1],
            };

            // Keep first occurrence from CSV if duplicate Danish keys are present.
            if (!seenDanishKeys.Add(NormalizeDanishKey(entry.DanishWriting)))
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

        foreach (var entry in WritingEntries)
        {
            lines.Add($"{EscapeCsvField(entry.DanishWriting)},{EscapeCsvField(entry.EnglishWriting)}");
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
