# Android Project Build & Deployment Guide

## Building the Android Project

The Android project is now configured and ready to build APK packages.

### Build Commands

**Debug Build:**
```powershell
cd C:\Users\emyur\Projects\Flashcards
dotnet build Flashcards.Android/Flashcards.Android.csproj -c Debug
```

**Release Build:**
```powershell
dotnet build Flashcards.Android/Flashcards.Android.csproj -c Release
```

**Publish with APK Format:**
```powershell
dotnet publish Flashcards.Android/Flashcards.Android.csproj -c Debug -f net9.0-android -p:AndroidPackageFormat=apk
```

### Generated APK Files

After building, the APK files are located at:
```
Flashcards.Android/bin/Debug/net9.0-android/
├── com.emyur.flashcards.apk          (Unsigned APK)
├── com.emyur.flashcards-Signed.apk   (Signed APK)
└── publish/                           (Published artifacts)
```

## Running on Android Device/Emulator

### Prerequisites
- Android SDK installed
- Android Device or Emulator running
- ADB (Android Debug Bridge) available in PATH

### Deployment

**Using ADB to install the APK:**
```powershell
adb install -r Flashcards.Android/bin/Debug/net9.0-android/com.emyur.flashcards.apk
```

**Or using the signed APK:**
```powershell
adb install -r Flashcards.Android/bin/Debug/net9.0-android/com.emyur.flashcards-Signed.apk
```

### IDE Integration

To run from the IDE:
1. Ensure an Android emulator or device is connected
2. Select **Flashcards.Android** as the startup project
3. Click the Run button or press F5

The project will automatically build the APK and deploy it to the connected device/emulator.

## Manifest Configuration

The Android manifest is located at:
```
Flashcards.Android/Properties/AndroidManifest.xml
```

It includes:
- Application ID: `com.emyur.flashcards`
- Target API: 34
- Required permissions: Internet, Network Access, Audio Recording
- Main Activity: `MainActivity` with launcher intent filter

## Troubleshooting

### "No AndroidManifest.xml" Error
- Run: `dotnet publish` instead of just `dotnet build`
- Ensure `Properties/AndroidManifest.xml` exists
- Check that the manifest path is correct in the `.csproj` file

### APK Not Generated
- Use `dotnet publish` command to generate APK files
- Add `-p:AndroidPackageFormat=apk` to force APK format (instead of AAB)

### Deployment Failed
- Ensure ADB can see your device: `adb devices`
- Check that the Android device/emulator is running
- Verify that USB debugging is enabled (for physical devices)
