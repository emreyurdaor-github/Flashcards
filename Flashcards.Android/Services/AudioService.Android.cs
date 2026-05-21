using System;
using System.Net.Http;
using System.Threading.Tasks;
using Android.Media;
using Avalonia.Platform;

namespace Flashcards.Services;

/// <summary>
/// Android implementation of AudioService using Android MediaPlayer
/// to stream Google Translate TTS audio.
/// </summary>
public class AudioService : IDisposable
{
    private static readonly HttpClient HttpClient = new();
    private static bool _headersInitialized = false;

    public AudioService()
    {
        if (!_headersInitialized)
        {
            HttpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Linux; Android 10) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Mobile Safari/537.36");
            HttpClient.Timeout = TimeSpan.FromSeconds(10);
            _headersInitialized = true;
        }
    }

    public Task PlayDanishPronunciation(string word) => PlayTts(word, "da");

    public Task PlayEnglishPronunciation(string word) => PlayTts(word, "en");

    /// <summary>
    /// Plays the correct answer sound from embedded assets
    /// </summary>
    public async Task PlayCorrectSoundAsync()
    {
        await PlayLocalSoundAsync("avares://Flashcards.Android/Assets/Sounds/correct.mp3");
    }

    /// <summary>
    /// Plays the wrong answer sound from embedded assets
    /// </summary>
    public async Task PlayWrongSoundAsync()
    {
        await PlayLocalSoundAsync("avares://Flashcards.Android/Assets/Sounds/wrong.mp3");
    }

    private async Task PlayLocalSoundAsync(string avaloniaResourceUri)
    {
        try
        {
            var uri = new Uri(avaloniaResourceUri);
            await using var stream = AssetLoader.Open(uri);

            var tempFile = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sound_{Guid.NewGuid()}.mp3");

            await using (var fileStream = System.IO.File.Create(tempFile))
            {
                await stream.CopyToAsync(fileStream);
            }

            await PlayFileAsync(tempFile);

            try { System.IO.File.Delete(tempFile); } catch { /* ignore cleanup errors */ }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService.Android] PlayLocalSound error: {ex.Message}");
        }
    }

    private async Task PlayTts(string word, string lang)
    {
        if (string.IsNullOrWhiteSpace(word))
            return;

        try
        {
            var encodedWord = Uri.EscapeDataString(word.Trim());
            var url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={encodedWord}&tl={lang}&client=tw-ob";

            System.Diagnostics.Debug.WriteLine($"[AudioService.Android] Fetching TTS: {url}");

            var audioBytes = await HttpClient.GetByteArrayAsync(url);

            if (audioBytes == null || audioBytes.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[AudioService.Android] Empty audio response");
                return;
            }

            var tempFile = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"tts_{Guid.NewGuid()}.mp3");

            await System.IO.File.WriteAllBytesAsync(tempFile, audioBytes);
            System.Diagnostics.Debug.WriteLine($"[AudioService.Android] Saved to: {tempFile}");

            await PlayFileAsync(tempFile);

            try { System.IO.File.Delete(tempFile); } catch { }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService.Android] Error: {ex.Message}");
        }
    }

    private static async Task PlayFileAsync(string filePath)
    {
        var tcs = new TaskCompletionSource<bool>();

        var player = new MediaPlayer();
        try
        {
            player.SetDataSource(filePath);
            player.Prepare();

            player.Completion += (_, _) =>
            {
                tcs.TrySetResult(true);
            };

            player.Error += (_, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[AudioService.Android] MediaPlayer error: {args.What}");
                tcs.TrySetResult(false);
            };

            player.Start();
            await tcs.Task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService.Android] PlayFileAsync error: {ex.Message}");
        }
        finally
        {
            player.Release();
            player.Dispose();
        }
    }

    public void Dispose() { }
}
