using System.IO;
using Danish.Widget.Data;

namespace Flashcards.Data;

/// <summary>
/// Initializes SpeakingDataSource for Android by copying the bundled CSV asset
/// to the app's writable files directory on every launch.
/// </summary>
public static class AndroidSpeakingDataSource
{
    private const string CsvFileName = "speaking.csv";

    public static void Initialize(global::Android.App.Application app)
    {
        var filesDir = app.FilesDir!.AbsolutePath;
        var destPath = Path.Combine(filesDir, CsvFileName);

        using var assetStream = app.Assets!.Open(CsvFileName);
        using var destStream = File.Create(destPath);
        assetStream.CopyTo(destStream);

        SpeakingDataSource.SetCsvPath(destPath);
    }
}
