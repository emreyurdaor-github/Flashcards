# Quick Reference - Running Android App in Rider

## ✅ The Issue is Fixed!

The "No AndroidManifest.xml" error has been resolved. The manifest file is now automatically copied to the build output directory during compilation.

## 🚀 To Run the App

1. **Connect Device/Emulator**
   - Plug in Android device with USB debugging enabled, OR
   - Start Android emulator

2. **In Rider IDE**
   - Select `Flashcards.Android` as the startup project (right-click → Set as Startup Project)
   - Press **Shift + F10** to Run
   - Or click the green Run button

3. **Done!**
   - The app will build, package (APK), and deploy automatically
   - App should launch on your device

## 📁 Key Locations

| File | Location | Purpose |
|------|----------|---------|
| AndroidManifest.xml (Source) | `Flashcards.Android/Platforms/Android/AndroidManifest.xml` | Original manifest |
| AndroidManifest.xml (Copy) | `Flashcards.Android/AndroidManifest.xml` | Root level copy |
| AndroidManifest.xml (Output) | `Flashcards.Android/bin/Debug/net9.0-android/AndroidManifest.xml` | ✅ **Rider uses this** |
| Unsigned APK | `Flashcards.Android/bin/Debug/net9.0-android/com.emyur.flashcards.apk` | |
| Signed APK | `Flashcards.Android/bin/Debug/net9.0-android/com.emyur.flashcards-Signed.apk` | |

## 🔧 If You Still Get the Error

```powershell
# Clean and rebuild
cd C:\Users\emyur\Projects\Flashcards
dotnet clean Flashcards.Android/Flashcards.Android.csproj
dotnet build Flashcards.Android/Flashcards.Android.csproj -c Debug

# Reload Rider
# File → Invalidate Caches and Restart
```

## 📋 What Was Fixed

✅ AndroidManifest.xml is copied to bin/Debug output automatically  
✅ Project is configured as executable (OutputType: Exe)  
✅ APK packaging is enabled  
✅ Rider run configuration is set up  
✅ Icon and resources are configured  
✅ All build errors resolved  

## 📚 For More Help

- `RIDER_ANDROID_SETUP.md` - Detailed Rider setup guide
- `ANDROID_BUILD_GUIDE.md` - Command-line build instructions
- `ANDROID_SETUP_COMPLETE.md` - Complete technical details

---

**Status: ✅ READY TO RUN**
