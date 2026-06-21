using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Android.App;
using Danish.Widget.Data;

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
            // (rows whose Danish key does not appear in the new asset).
            // Use record-level splitting so multi-line quoted fields (e.g. mnemonic)
            // are treated as a single record and not corrupted.
            var assetContent = System.Text.Encoding.UTF8.GetString(assetBytes);
            var assetRecords = FlashcardDataSource.SplitIntoRecords(assetContent);
            var existingContent = File.ReadAllText(destPath);
            var existingRecords = FlashcardDataSource.SplitIntoRecords(existingContent);
            var merged = MergeUserEntries(assetRecords, existingRecords);
            // Write records separated by newlines; embedded newlines inside quoted
            // fields are preserved correctly as part of each record string.
            File.WriteAllText(destPath, string.Join("\n", merged) + "\n", System.Text.Encoding.UTF8);
        }

        // Persist the asset hash so we can detect future updates
        if (assetChanged || destMissing)
            File.WriteAllText(hashPath, assetHash);

        FlashcardDataSource.SetCsvPath(destPath);
    }

    /// <summary>
    /// Returns the asset records plus any records from the existing stored CSV whose
    /// Danish key (first CSV field) does not already appear in the asset.
    /// Each element in the lists is a complete logical CSV record (may contain embedded
    /// newlines for multi-line quoted fields such as mnemonic).
    /// </summary>
    private static List<string> MergeUserEntries(List<string> assetRecords, List<string> existingRecords)
    {
        // Collect the set of Danish keys already present in the new asset
        var assetKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var record in assetRecords)
        {
            if (string.IsNullOrWhiteSpace(record) ||
                record.StartsWith(ExpectedHeader, System.StringComparison.OrdinalIgnoreCase))
                continue;
            var key = ExtractFirstField(record);
            if (!string.IsNullOrWhiteSpace(key))
                assetKeys.Add(key.Trim());
        }

        var result = new List<string>(assetRecords);

        // Append user-added records that are not in the new asset
        foreach (var record in existingRecords)
        {
            if (string.IsNullOrWhiteSpace(record) ||
                record.StartsWith(ExpectedHeader, System.StringComparison.OrdinalIgnoreCase))
                continue;
            var key = ExtractFirstField(record);
            if (!string.IsNullOrWhiteSpace(key) && !assetKeys.Contains(key.Trim()))
                result.Add(record);
        }

        return result;
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
