using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Flashcards.Data;

namespace Flashcards.Android;

[Activity(
    Label = "Flashcards",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Initialize Android-specific data source before the app starts
        AndroidFlashcardDataSource.Initialize(Application!);

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
