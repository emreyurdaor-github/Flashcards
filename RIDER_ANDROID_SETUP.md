# Flashcards App - Rider IDE Setup Guide

## Overview
This solution contains two projects:
1. **Flashcards** - Desktop application using Avalonia
2. **Flashcards.Android** - Android mobile application using Xamarin.Android

## Running the Android Project in Rider

### Prerequisites
1. Android SDK installed and configured
2. Android device or emulator connected
3. Rider IDE with Android support enabled

### Steps to Run

#### Option 1: Run from Rider IDE (Recommended)

1. **Select the Android Project**
   - In the project tree, right-click on `Flashcards.Android`
   - Select "Set as Startup Project" (if not already set)

2. **Select Run Configuration**
   - Ensure you have an Android device or emulator running
   - Click on the Run Configuration dropdown (top-right of Rider)
   - Select your Android target device

3. **Build and Deploy**
   - Press `Shift + F10` (Windows/Linux) or `Ctrl + R` (Mac) to run
   - Or click the green Run button
   - Rider will build, package, and deploy the APK to your device

#### Option 2: Build and Install Manually

If you prefer manual control, use these PowerShell commands:

**Build the project:**
```powershell
cd C:\Users\emyur\Projects\Flashcards
dotnet build Flashcards.Android/Flashcards.Android.csproj -c Debug
```

**Publish APK:**
```powershell
dotnet publish Flashcards.Android/Flashcards.Android.csproj -c Debug -f net9.0-android -p:AndroidPackageFormat=apk
```

**Install on device using ADB:**
```powershell
adb install -r Flashcards.Android/bin/Debug/net9.0-android/com.emyur.flashcards.apk
```

### Project Files Location

Generated APK files are located at:
```
Flashcards.Android/bin/Debug/net9.0-android/
├── AndroidManifest.xml              ← Required for Rider
├── com.emyur.flashcards.apk         ← Unsigned APK
├── com.emyur.flashcards-Signed.apk  ← Signed APK
└── publish/                         ← Published artifacts
```

### Troubleshooting

#### "There is no AndroidManifest.xml" Error

This error occurs when Rider cannot find the manifest file. The manifest has been automatically configured to copy to the build output during compilation.

**If you still see this error:**

1. **Clean and Rebuild:**
   ```powershell
   cd C:\Users\emyur\Projects\Flashcards
   dotnet clean Flashcards.Android/Flashcards.Android.csproj
   dotnet build Flashcards.Android/Flashcards.Android.csproj -c Debug
   ```

2. **Verify the manifest exists in bin/Debug:**
   ```powershell
   Get-ChildItem -Path "Flashcards.Android\bin\Debug\net9.0-android\AndroidManifest.xml"
   ```

3. **Reload Rider Project:**
   - Go to File → Invalidate Caches and Restart
   - Or close and reopen the project

#### APK Not Generated

1. Use `dotnet publish` instead of just `dotnet build`:
   ```powershell
   dotnet publish Flashcards.Android/Flashcards.Android.csproj -c Debug -f net9.0-android -p:AndroidPackageFormat=apk
   ```

2. Ensure `.csproj` file has `<AndroidPackageFormat>apk</AndroidPackageFormat>`

#### Device Not Connected

Check ADB connection:
```powershell
adb devices
```

If your device is not listed:
- Ensure USB debugging is enabled (on physical devices)
- Restart ADB server:
  ```powershell
  adb kill-server
  adb start-server
  ```

### Project Configuration

**Application Details:**
- Package ID: `com.emyur.flashcards`
- Target API: 34 (Android 14)
- Main Activity: `MainActivity`
- Permissions: Internet, Network Access, Audio Recording

**Key Files:**
- `AndroidManifest.xml` - Application manifest
- `Platforms/Android/AndroidManifest.xml` - Source manifest
- `Flashcards.Android.csproj` - Project configuration
- `MainActivity.cs` - Main activity entry point

### Next Steps

1. Build and test the Android app
2. Customize the MainActivity and UI layouts
3. Share code between desktop and Android projects using shared libraries
4. Debug using Rider's Android debugger (breakpoints, inspection, etc.)

---

**For more help:**
- Android Developer Docs: https://developer.android.com
- Xamarin.Android Docs: https://learn.microsoft.com/en-us/xamarin/android/
- Rider Documentation: https://www.jetbrains.com/help/rider/
