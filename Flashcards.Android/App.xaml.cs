using Android.App;
using Android.OS;

namespace Flashcards.Android;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class App : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.main);
    }
}
