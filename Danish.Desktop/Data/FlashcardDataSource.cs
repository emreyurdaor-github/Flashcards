using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Danish.Desktop.Models;

namespace Danish.Desktop.Data;

public static class FlashcardDataSource
{
    private const string CsvFileName = "flashcards.csv";
    private const string CsvHeader   = "Danish,English,Type,Conjugation,ExampleDanish,ExampleEnglish,ContextualTip,Mnemonic";

    private static string? _overrideCsvPath;

    private static string CsvPath =>
        _overrideCsvPath ?? Path.Combine(AppContext.BaseDirectory, "Data", CsvFileName);

    // Walk up from bin/<cfg>/<tfm>/ to the Widget project's Data folder so edits persist.
    private static string SourceCsvPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Danish.Widget", "Data", CsvFileName));

    public static void SetCsvPath(string path) => _overrideCsvPath = path;

    private static List<FlashcardEntry>? _flashcards;
    private static List<FlashcardEntry> Flashcards => _flashcards ??= LoadFromCsv();

    public static IReadOnlyList<FlashcardEntry> GetFlashcards() => Flashcards;

    public static bool TryAddFlashcard(FlashcardEntry flashcard)
    {
        var key = Normalize(flashcard.Danish);
        if (Flashcards.Any(c => Normalize(c.Danish) == key)) return false;
        Flashcards.Add(flashcard);
        SaveToCsv();
        return true;
    }

    public static bool TryUpdateFlashcard(string originalDanish, FlashcardEntry updated)
    {
        var origKey    = Normalize(originalDanish);
        var updatedKey = Normalize(updated.Danish);
        if (origKey != updatedKey && Flashcards.Any(c => Normalize(c.Danish) == updatedKey)) return false;
        var index = Flashcards.FindIndex(c => Normalize(c.Danish) == origKey);
        if (index < 0) return false;
        Flashcards[index] = updated;
        SaveToCsv();
        return true;
    }

    private static string Normalize(string s) => s.Trim().ToUpperInvariant();

    // ─── CSV loading ────────────────────────────────────────────────────────────

    private static List<FlashcardEntry> LoadFromCsv()
    {
        var list = new List<FlashcardEntry>();
        if (!File.Exists(CsvPath)) return list;

        var seen    = new HashSet<string>(StringComparer.Ordinal);
        var content = File.ReadAllText(CsvPath);
        foreach (var line in SplitIntoRecords(content))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(CsvHeader, StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = SplitCsvLine(line);
            if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2])) continue;

            var entry = new FlashcardEntry
            {
                Danish       = parts[0],
                English      = parts[1],
                Type         = parts[2],
                Conjugation  = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : null,
                ExampleDanish  = parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4]) ? parts[4] : null,
                ExampleEnglish = parts.Length > 5 && !string.IsNullOrWhiteSpace(parts[5]) ? parts[5] : null,
                ContextualTip  = parts.Length > 6 && !string.IsNullOrWhiteSpace(parts[6]) ? parts[6] : null,
                Mnemonic       = parts.Length > 7 && !string.IsNullOrWhiteSpace(parts[7]) ? parts[7] : null,
            };

            if (seen.Add(Normalize(entry.Danish)))
                list.Add(entry);
        }
        return list;
    }

    private static void SaveToCsv()
    {
        var lines = new List<string> { CsvHeader };
        foreach (var c in Flashcards)
            lines.Add($"{Esc(c.Danish)},{Esc(c.English)},{Esc(c.Type)},{Esc(c.Conjugation ?? "")},{Esc(c.ExampleDanish ?? "")},{Esc(c.ExampleEnglish ?? "")},{Esc(c.ContextualTip ?? "")},{Esc(c.Mnemonic ?? "")}");

        var dir = Path.GetDirectoryName(CsvPath);
        if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllLines(CsvPath, lines);

        try
        {
            var srcDir = Path.GetDirectoryName(SourceCsvPath);
            if (srcDir is not null && !Directory.Exists(srcDir)) Directory.CreateDirectory(srcDir);
            File.WriteAllLines(SourceCsvPath, lines);
        }
        catch { /* non-fatal */ }
    }

    private static string Esc(string v) =>
        (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            ? $"\"{v.Replace("\"", "\"\"")}\"" : v;

    internal static List<string> SplitIntoRecords(string content)
    {
        var records = new List<string>();
        var cur     = new System.Text.StringBuilder();
        bool inQ    = false;
        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (inQ)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"') { cur.Append("\"\""); i++; }
                    else { inQ = false; cur.Append(c); }
                }
                else cur.Append(c);
            }
            else
            {
                if (c == '"')  { inQ = true; cur.Append(c); }
                else if (c == '\n')
                {
                    if (cur.Length > 0 && cur[cur.Length - 1] == '\r') cur.Length--;
                    var r = cur.ToString();
                    if (!string.IsNullOrWhiteSpace(r)) records.Add(r);
                    cur.Clear();
                }
                else if (c != '\r') cur.Append(c);
            }
        }
        if (cur.Length > 0) { if (cur[cur.Length - 1] == '\r') cur.Length--; var r = cur.ToString(); if (!string.IsNullOrWhiteSpace(r)) records.Add(r); }
        return records;
    }

    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var cur    = new System.Text.StringBuilder();
        bool inQ   = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQ)
            {
                if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else inQ = false; }
                else cur.Append(c);
            }
            else
            {
                if (c == '"')  inQ = true;
                else if (c == ',') { fields.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(c);
            }
        }
        fields.Add(cur.ToString());
        return [.. fields];
    }
}
