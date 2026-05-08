using System.IO;
using Android.App;
using Android.Content;
using Flashcards.Data;

namespace Flashcards.Data;

/// <summary>
/// Initializes FlashcardDataSource for Android by copying the bundled CSV asset
/// to the app's writable files directory on first run, then pointing the data source at it.
/// </summary>
public static class AndroidFlashcardDataSource
{
    private const string CsvFileName = "flashcards.csv";

    public static void Initialize(global::Android.App.Application app)
    {
        var filesDir = app.FilesDir!.AbsolutePath;
        var destPath = Path.Combine(filesDir, CsvFileName);

        // Copy from assets to writable storage if not already present
        if (!File.Exists(destPath))
        {
            using var assetStream = app.Assets!.Open(CsvFileName);
            using var destStream = File.Create(destPath);
            assetStream.CopyTo(destStream);
        }

        FlashcardDataSource.SetCsvPath(destPath);
    }
}
