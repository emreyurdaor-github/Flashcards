using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flashcards.Data;

public class WritingEntry
{
    public string DanishWritingTitle { get; set; } = string.Empty;
    public string DanishWriting { get; set; } = string.Empty;
    public string EnglishWritingTitle { get; set; } = string.Empty;
    public string EnglishWriting { get; set; } = string.Empty;
}

public static class WritingDataSource
{
    private const string CsvFileName = "writing.csv";
    private const string CsvHeader = "DanishWritingTitle,DanishWriting,EnglishWritingTitle,EnglishWriting";

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
    public static string SourceCsvPath => BuildInfo.WritingCsvSourcePath;

    private static List<WritingEntry> LoadFromCsv()
    {
        var list = new List<WritingEntry>();
        var seenDanishKeys = new HashSet<string>(StringComparer.Ordinal);

        if (!File.Exists(CsvPath))
            return list;

        var content = File.ReadAllText(CsvPath);
        var records = ParseCsvRecords(content);

        foreach (var parts in records)
        {
            if (parts.Count < 2) continue;

            // Skip header row
            if (parts[0].Equals("DanishWriting", StringComparison.OrdinalIgnoreCase) ||
                parts[0].Equals("DanishWritingTitle", StringComparison.OrdinalIgnoreCase))
                continue;

            // Support both old 2-column format and new 4-column format
            var entry = parts.Count >= 4
                ? new WritingEntry
                {
                    DanishWritingTitle = parts[0],
                    DanishWriting = parts[1],
                    EnglishWritingTitle = parts[2],
                    EnglishWriting = parts[3],
                }
                : new WritingEntry
                {
                    DanishWritingTitle = string.Empty,
                    DanishWriting = parts[0],
                    EnglishWritingTitle = string.Empty,
                    EnglishWriting = parts[1],
                };

            if (string.IsNullOrWhiteSpace(entry.DanishWriting))
                continue;

            // Keep first occurrence from CSV if duplicate Danish keys are present.
            if (!seenDanishKeys.Add(NormalizeDanishKey(entry.DanishWriting)))
                continue;

            list.Add(entry);
        }

        return list;
    }

    // Parses a full CSV text into records (list of field lists), handling quoted multi-line fields.
    private static List<List<string>> ParseCsvRecords(string content)
    {
        var records = new List<List<string>>();
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < content.Length)
        {
            char c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        // Escaped quote
                        current.Append('"');
                        i += 2;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    current.Append(c);
                    i++;
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                    i++;
                }
                else if (c == '\r' || c == '\n')
                {
                    // End of record
                    fields.Add(current.ToString());
                    current.Clear();

                    if (fields.Count > 0 && !(fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0])))
                        records.Add(new List<string>(fields));
                    fields.Clear();

                    // Skip \r\n pair
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                        i++;
                    i++;
                }
                else
                {
                    current.Append(c);
                    i++;
                }
            }
        }

        // Handle last field/record without trailing newline
        fields.Add(current.ToString());
        if (fields.Count > 0 && !(fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0])))
            records.Add(new List<string>(fields));

        return records;
    }

    private static void SaveToCsv()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(CsvHeader);

        foreach (var entry in WritingEntries)
        {
            sb.AppendLine($"{EscapeCsvField(entry.DanishWritingTitle)},{EscapeCsvField(entry.DanishWriting)},{EscapeCsvField(entry.EnglishWritingTitle)},{EscapeCsvField(entry.EnglishWriting)}");
        }

        var content = sb.ToString();

        // Write to the runtime output directory (where the app reads from)
        var runtimeDir = Path.GetDirectoryName(CsvPath);
        if (runtimeDir is not null && !Directory.Exists(runtimeDir))
            Directory.CreateDirectory(runtimeDir);
        File.WriteAllText(CsvPath, content);

        // Also write back to the solution source file so the data persists across builds
        var sourcePath = BuildInfo.WritingCsvSourcePath;
        if (!string.IsNullOrEmpty(sourcePath) &&
            !string.Equals(Path.GetFullPath(CsvPath), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var sourceDir = Path.GetDirectoryName(sourcePath);
                if (sourceDir is not null && !Directory.Exists(sourceDir))
                    Directory.CreateDirectory(sourceDir);
                // Write the same content string directly — avoids File.Copy lock issues when app is running
                File.WriteAllText(sourcePath, content, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WritingDataSource] Failed to sync source CSV: {ex.Message}");
            }
        }
    }

    // Always wrap every field in quotes, escaping any embedded quotes by doubling them.
    // Normalize line endings inside the field to \n for clean CSV.
    private static string EscapeCsvField(string value)
    {
        var normalized = value.Replace("\r\n", "\n").Replace("\r", "\n");
        return $"\"{normalized.Replace("\"", "\"\"")}\"";
    }

    private static string NormalizeDanishKey(string danish)
    {
        return danish.Trim().ToUpperInvariant();
    }
}
