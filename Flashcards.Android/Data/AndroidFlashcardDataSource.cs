using System.IO;
using Android.App;
using Flashcards.Data;

namespace Flashcards.Data;

/// <summary>
/// Initializes FlashcardDataSource for Android by copying the bundled CSV asset
/// to the app's writable files directory on first run, then pointing the data source at it.
/// Re-copies if the CSV schema (header) has changed.
/// </summary>
public static class AndroidFlashcardDataSource
{
    private const string CsvFileName = "flashcards.csv";
    private const string ExpectedHeader = "Danish,English,Type,Conjugation,ExampleDanish,ExampleEnglish,ContextualTip";

    public static void Initialize(global::Android.App.Application app)
    {
        var filesDir = app.FilesDir!.AbsolutePath;
        var destPath = Path.Combine(filesDir, CsvFileName);

        // Copy from assets if not present, or if the header/schema has changed
        bool needsCopy = !File.Exists(destPath) || !HasExpectedHeader(destPath);

        if (needsCopy)
        {
            using var assetStream = app.Assets!.Open(CsvFileName);
            using var destStream = File.Create(destPath);
            assetStream.CopyTo(destStream);
        }

        FlashcardDataSource.SetCsvPath(destPath);
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
}
