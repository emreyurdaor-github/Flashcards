using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Android.App;
using Flashcards.Data;

namespace Flashcards.Data;

/// <summary>
/// Initializes FlashcardDataSource for Android by copying the bundled CSV asset
/// to the app's writable files directory on first run, then pointing the data source at it.
/// Re-copies (and merges user-added entries) whenever the bundled asset content changes.
/// </summary>
public static class AndroidFlashcardDataSource
{
    private const string CsvFileName = "flashcards.csv";
    private const string HashFileName = "flashcards.csv.assetHash";
    private const string ExpectedHeader = "Danish,English,Type,Conjugation,ExampleDanish,ExampleEnglish,ContextualTip,Mnemonic";

    public static void Initialize(global::Android.App.Application app)
    {
        var filesDir = app.FilesDir!.AbsolutePath;
        var destPath = Path.Combine(filesDir, CsvFileName);
        var hashPath = Path.Combine(filesDir, HashFileName);

        // Read the bundled asset into memory so we can hash it and copy it if needed
        byte[] assetBytes;
        using (var assetStream = app.Assets!.Open(CsvFileName))
        using (var ms = new MemoryStream())
        {
            assetStream.CopyTo(ms);
            assetBytes = ms.ToArray();
        }

        var assetHash = ComputeHash(assetBytes);
        var storedHash = File.Exists(hashPath) ? File.ReadAllText(hashPath).Trim() : string.Empty;

        bool assetChanged = assetHash != storedHash;
        bool destMissing = !File.Exists(destPath);

        if (destMissing)
        {
            // First install: just copy the asset directly
            File.WriteAllBytes(destPath, assetBytes);
        }
        else if (assetChanged)
        {
            // Bundled asset was updated: re-copy it, but preserve any user-added rows
            // (rows whose Danish key does not appear in the new asset)
            var assetLines = ReadLines(assetBytes);
            var existingLines = File.ReadAllLines(destPath);
            var merged = MergeUserEntries(assetLines, existingLines);
            File.WriteAllLines(destPath, merged, System.Text.Encoding.UTF8);
        }

        // Persist the asset hash so we can detect future updates
        if (assetChanged || destMissing)
            File.WriteAllText(hashPath, assetHash);

        FlashcardDataSource.SetCsvPath(destPath);
    }

    /// <summary>
    /// Returns the asset lines plus any rows from the existing stored CSV whose
    /// Danish key (first CSV field) does not already appear in the asset.
    /// </summary>
    private static List<string> MergeUserEntries(string[] assetLines, string[] existingLines)
    {
        // Collect the set of Danish keys already present in the new asset
        var assetKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var line in assetLines)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith(ExpectedHeader, System.StringComparison.OrdinalIgnoreCase))
                continue;
            var key = ExtractFirstField(line);
            if (!string.IsNullOrWhiteSpace(key))
                assetKeys.Add(key.Trim());
        }

        var result = new List<string>(assetLines);

        // Append user-added rows that are not in the new asset
        foreach (var line in existingLines)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith(ExpectedHeader, System.StringComparison.OrdinalIgnoreCase))
                continue;
            var key = ExtractFirstField(line);
            if (!string.IsNullOrWhiteSpace(key) && !assetKeys.Contains(key.Trim()))
                result.Add(line);
        }

        return result;
    }

    private static string[] ReadLines(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var reader = new StreamReader(ms, System.Text.Encoding.UTF8);
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
            lines.Add(line);
        return [.. lines];
    }

    /// <summary>Extracts the first CSV field (handles quoted fields).</summary>
    private static string ExtractFirstField(string line)
    {
        if (line.Length == 0) return string.Empty;
        if (line[0] == '"')
        {
            int end = line.IndexOf('"', 1);
            return end > 0 ? line.Substring(1, end - 1) : line;
        }
        int comma = line.IndexOf(',');
        return comma >= 0 ? line.Substring(0, comma) : line;
    }

    private static string ComputeHash(byte[] data)
    {
        var hashBytes = MD5.HashData(data);
        return System.Convert.ToHexString(hashBytes);
    }
}
