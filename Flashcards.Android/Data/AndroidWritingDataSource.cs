using System.IO;
using Android.App;
using Android.Content;
using Flashcards.Data;

namespace Flashcards.Data;

/// <summary>
/// Initializes WritingDataSource for Android by copying the bundled CSV asset
/// to the app's writable files directory on first run (or when the asset has more
/// entries than the stored file), then pointing the data source at it.
/// </summary>
public static class AndroidWritingDataSource
{
    private const string CsvFileName = "writing.csv";
    private const string ExpectedHeader = "DanishWriting,EnglishWriting";

    public static void Initialize(global::Android.App.Application app)
    {
        var filesDir = app.FilesDir!.AbsolutePath;
        var destPath = Path.Combine(filesDir, CsvFileName);

        // Read the bundled asset into memory so we can compare
        byte[] assetBytes;
        using (var assetStream = app.Assets!.Open(CsvFileName))
        using (var ms = new MemoryStream())
        {
            assetStream.CopyTo(ms);
            assetBytes = ms.ToArray();
        }

        bool needsCopy = !File.Exists(destPath)
                         || !HasExpectedHeader(destPath)
                         || CountLines(assetBytes) > CountStoredLines(destPath);

        if (needsCopy)
        {
            File.WriteAllBytes(destPath, assetBytes);
        }

        WritingDataSource.SetCsvPath(destPath);
    }

    private static bool HasExpectedHeader(string filePath)
    {
        try
        {
            using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8);
            var firstLine = reader.ReadLine();
            return string.Equals(firstLine?.Trim(), ExpectedHeader, System.StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // Count non-empty lines in a byte array (used for the bundled asset)
    private static int CountLines(byte[] data)
    {
        using var reader = new StreamReader(new MemoryStream(data), System.Text.Encoding.UTF8);
        int count = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
            if (!string.IsNullOrWhiteSpace(line)) count++;
        return count;
    }

    // Count non-empty lines in the stored file on disk
    private static int CountStoredLines(string filePath)
    {
        try
        {
            int count = 0;
            foreach (var line in File.ReadLines(filePath))
                if (!string.IsNullOrWhiteSpace(line)) count++;
            return count;
        }
        catch { return 0; }
    }
}
