// Auto-generated — do not edit manually.
using System;
using System.IO;

namespace Flashcards;

internal static class BuildInfo
{
    // Walk up from bin/<Configuration>/<tfm>/ to the project root, then into Data/.
    // This works on any machine regardless of where the repository is located.
    private static string DataSourcePath(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", fileName));

    public static string CsvSourcePath => DataSourcePath("flashcards.csv");
    public static string WritingCsvSourcePath => DataSourcePath("writing.csv");
    public static string MbspCsvSourcePath => DataSourcePath("medborgerskabsproeven.csv");
    public static string SpeakingCsvSourcePath => DataSourcePath("speaking.csv");
}