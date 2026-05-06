using Android.App;
using Android.Runtime;

namespace Flashcards.Android;

[Application(AllowBackup = true)]
public class FlashcardsApplication : Application
{
    public FlashcardsApplication(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }
}
