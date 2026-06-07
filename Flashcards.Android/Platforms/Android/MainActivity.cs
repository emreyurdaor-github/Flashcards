using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Flashcards.Data;

namespace Flashcards.Android;

[Activity(
    Label = "Flashcards",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_launcher_round",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Keep the screen on while this app is in the foreground
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Initialize Android-specific data source before the app starts
        AndroidFlashcardDataSource.Initialize(Application!);
        AndroidWritingDataSource.Initialize(Application!);
        AndroidSpeakingDataSource.Initialize(Application!);
        AndroidMbspDataSource.Initialize(Application!);

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
