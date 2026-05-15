using System.IO;
using Android.App;
using Flashcards.Data;

namespace Flashcards.Data;

/// <summary>
/// Initializes WritingDataSource for Android by always copying the bundled CSV asset
/// to the app's writable files directory on every launch, then pointing the data source at it.
/// This ensures the device always reflects the latest data bundled in the APK.
/// </summary>
public static class AndroidWritingDataSource
{
    private const string CsvFileName = "writing.csv";

    public static void Initialize(global::Android.App.Application app)
    {
        var filesDir = app.FilesDir!.AbsolutePath;
        var destPath = Path.Combine(filesDir, CsvFileName);

        // Always overwrite from bundled assets so Android always has the latest data from the APK
        using var assetStream = app.Assets!.Open(CsvFileName);
        using var destStream = File.Create(destPath);
        assetStream.CopyTo(destStream);

        WritingDataSource.SetCsvPath(destPath);
    }
}
