using System;
using System.Threading.Tasks;

namespace Flashcards.Services;

/// <summary>
/// Android stub for AudioService. NAudio is Windows-only; audio playback on Android
/// can be implemented here using Android MediaPlayer or similar in the future.
/// </summary>
public class AudioService : IDisposable
{
    public Task PlayDanishPronunciation(string word) => Task.CompletedTask;
    public Task PlayEnglishPronunciation(string word) => Task.CompletedTask;
    public void Dispose() { }
}
