using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Android.App;
using Flashcards.Data;

namespace Flashcards.Data;

/// <summary>
/// Initializes MbspDataSource for Android by copying the bundled CSV asset
/// to the app's writable files directory on first run (or when the asset content changes),
/// then pointing the data source at it. User-added questions are preserved during updates.
/// </summary>
public static class AndroidMbspDataSource
{
    private const string CsvFileName = "medborgerskabsproeven.csv";
    private const string ExpectedHeader = "Question,QuestionEnglish,ChoiceA,ChoiceAEnglish,ChoiceB,ChoiceBEnglish,ChoiceC,ChoiceCEnglish,CorrectAnswer,Period";

    public static void Initialize(global::Android.App.Application app)
    {
        var filesDir = app.FilesDir!.AbsolutePath;
        var destPath = Path.Combine(filesDir, CsvFileName);

        // Read the bundled asset lines
        string[] assetLines;
        using (var assetStream = app.Assets!.Open(CsvFileName))
        using (var reader = new StreamReader(assetStream, System.Text.Encoding.UTF8))
        {
            assetLines = reader.ReadToEnd()
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();
        }

        if (!File.Exists(destPath) || !HasExpectedHeader(destPath))
        {
            // First run or schema change: just copy the asset directly
            File.WriteAllLines(destPath, assetLines, System.Text.Encoding.UTF8);
        }
        else
        {
            // Check if the asset has changed by comparing content
            var cachedLines = File.ReadAllLines(destPath, System.Text.Encoding.UTF8)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            var assetQuestionKeys = GetQuestionKeys(assetLines);
            var cachedQuestionKeys = GetQuestionKeys(cachedLines);

            bool assetChanged = !assetQuestionKeys.SetEquals(cachedQuestionKeys);

            if (assetChanged)
            {
                // Merge: start with the latest asset, then append any user-added questions
                // (lines in cache whose question key is not present in the asset)
                var userAddedLines = cachedLines
                    .Skip(1) // skip header
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Where(line =>
                    {
                        var key = ExtractQuestionKey(line);
                        return key != null && !assetQuestionKeys.Contains(key);
                    })
                    .ToArray();

                var merged = assetLines.Concat(userAddedLines).ToArray();
                File.WriteAllLines(destPath, merged, System.Text.Encoding.UTF8);
            }
            // else: cached file is already up-to-date, leave it as-is
        }

        MbspDataSource.SetCsvPath(destPath);
    }

    private static bool HasExpectedHeader(string filePath)
    {
        try
        {
            using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8);
            var firstLine = reader.ReadLine();
            return string.Equals(firstLine?.Trim(), ExpectedHeader, System.StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Returns the set of normalized question keys (first CSV field) from the given lines, skipping the header.</summary>
    private static HashSet<string> GetQuestionKeys(IEnumerable<string> lines)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            if (line.StartsWith("Question", StringComparison.OrdinalIgnoreCase))
                continue;
            var key = ExtractQuestionKey(line);
            if (key != null)
                keys.Add(key);
        }
        return keys;
    }

    /// <summary>Extracts and normalizes the first CSV field (the question text) from a line.</summary>
    private static string? ExtractQuestionKey(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try
        {
            // Handle quoted fields
            if (line.StartsWith("\""))
            {
                int endQuote = line.IndexOf('"', 1);
                while (endQuote >= 0 && endQuote + 1 < line.Length && line[endQuote + 1] == '"')
                    endQuote = line.IndexOf('"', endQuote + 2);
                if (endQuote > 0)
                    return line.Substring(1, endQuote - 1).Replace("\"\"", "\"").Trim().ToUpperInvariant();
            }
            var comma = line.IndexOf(',');
            return (comma > 0 ? line.Substring(0, comma) : line).Trim().ToUpperInvariant();
        }
        catch
        {
            return null;
        }
    }
}
