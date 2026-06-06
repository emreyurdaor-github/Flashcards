using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flashcards.Data;

public class SpeakingEntry
{
    public string TopicTitle { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string NotesTitle { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public static class SpeakingDataSource
{
    private const string CsvFileName = "speaking.csv";
    private const string CsvHeader = "TopicTitle,Topic,NotesTitle,Notes";

    private static string? _overrideCsvPath;

    private static string CsvPath =>
        _overrideCsvPath ?? Path.Combine(AppContext.BaseDirectory, "Data", CsvFileName);

    public static void SetCsvPath(string path) => _overrideCsvPath = path;

    private static List<SpeakingEntry>? _entries;
    private static List<SpeakingEntry> Entries => _entries ??= LoadFromCsv();

    public static IReadOnlyList<SpeakingEntry> GetEntries() => Entries;

    public static bool TryAddEntry(SpeakingEntry entry)
    {
        var normalizedKey = NormalizeKey(entry.Topic);
        if (Entries.Any(e => NormalizeKey(e.Topic) == normalizedKey))
            return false;

        Entries.Add(entry);
        SaveToCsv();
        return true;
    }

    public static bool TryUpdateEntry(string originalTopic, SpeakingEntry updated)
    {
        var originalKey = NormalizeKey(originalTopic);
        var updatedKey = NormalizeKey(updated.Topic);

        if (originalKey != updatedKey && Entries.Any(e => NormalizeKey(e.Topic) == updatedKey))
            return false;

        var index = Entries.FindIndex(e => NormalizeKey(e.Topic) == originalKey);
        if (index < 0) return false;

        Entries[index] = updated;
        SaveToCsv();
        return true;
    }

    public static string RuntimeCsvPath => CsvPath;
    public static string SourceCsvPath => BuildInfo.SpeakingCsvSourcePath;

    private static List<SpeakingEntry> LoadFromCsv()
    {
        var list = new List<SpeakingEntry>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        if (!File.Exists(CsvPath))
            return list;

        var content = File.ReadAllText(CsvPath);
        var records = ParseCsvRecords(content);

        foreach (var parts in records)
        {
            if (parts.Count < 2) continue;

            if (parts[0].Equals("Topic", StringComparison.OrdinalIgnoreCase) ||
                parts[0].Equals("TopicTitle", StringComparison.OrdinalIgnoreCase))
                continue;

            var entry = parts.Count >= 4
                ? new SpeakingEntry
                {
                    TopicTitle = parts[0],
                    Topic = parts[1],
                    NotesTitle = parts[2],
                    Notes = parts[3],
                }
                : new SpeakingEntry
                {
                    TopicTitle = string.Empty,
                    Topic = parts[0],
                    NotesTitle = string.Empty,
                    Notes = parts.Count > 1 ? parts[1] : string.Empty,
                };

            if (string.IsNullOrWhiteSpace(entry.Topic))
                continue;

            if (!seenKeys.Add(NormalizeKey(entry.Topic)))
                continue;

            list.Add(entry);
        }

        return list;
    }

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
                    fields.Add(current.ToString());
                    current.Clear();

                    if (fields.Count > 0 && !(fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0])))
                        records.Add(new List<string>(fields));
                    fields.Clear();

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

        fields.Add(current.ToString());
        if (fields.Count > 0 && !(fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0])))
            records.Add(new List<string>(fields));

        return records;
    }

    private static void SaveToCsv()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(CsvHeader);

        foreach (var entry in Entries)
        {
            sb.AppendLine($"{EscapeCsvField(entry.TopicTitle)},{EscapeCsvField(entry.Topic)},{EscapeCsvField(entry.NotesTitle)},{EscapeCsvField(entry.Notes)}");
        }

        var content = sb.ToString();

        var runtimeDir = Path.GetDirectoryName(CsvPath);
        if (runtimeDir is not null && !Directory.Exists(runtimeDir))
            Directory.CreateDirectory(runtimeDir);
        File.WriteAllText(CsvPath, content);

        var sourcePath = BuildInfo.SpeakingCsvSourcePath;
        if (!string.IsNullOrEmpty(sourcePath) &&
            !string.Equals(Path.GetFullPath(CsvPath), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var sourceDir = Path.GetDirectoryName(sourcePath);
                if (sourceDir is not null && !Directory.Exists(sourceDir))
                    Directory.CreateDirectory(sourceDir);
                File.WriteAllText(sourcePath, content, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpeakingDataSource] Failed to sync source CSV: {ex.Message}");
            }
        }
    }

    private static string EscapeCsvField(string value)
    {
        var normalized = value.Replace("\r\n", "\n").Replace("\r", "\n");
        return $"\"{normalized.Replace("\"", "\"\"")}\"";
    }

    private static string NormalizeKey(string topic) => topic.Trim().ToUpperInvariant();
}
