using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Flashcards.Services;

public class AudioService : IDisposable
{
    private static readonly HttpClient HttpClient = new();
    private IWavePlayer? _wavePlayer;
    private static bool _headersInitialized = false;

    public AudioService()
    {
        // Set up HttpClient with proper headers (only once)
        if (!_headersInitialized)
        {
            HttpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            HttpClient.Timeout = TimeSpan.FromSeconds(10);
            _headersInitialized = true;
        }
    }

    /// <summary>
    /// Plays Danish pronunciation for a given word using Google Translate TTS
    /// </summary>
    public async Task PlayDanishPronunciation(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService] Playing pronunciation for: {word}");

            // Google Translate TTS URL for Danish
            var encodedWord = Uri.EscapeDataString(word.Trim());
            var url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={encodedWord}&tl=da&client=tw-ob";
            
            System.Diagnostics.Debug.WriteLine($"[AudioService] Requesting URL: {url}");
            
            var response = await HttpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioService] HTTP Error: {response.StatusCode}");
                return;
            }

            var audioBytes = await response.Content.ReadAsByteArrayAsync();
            System.Diagnostics.Debug.WriteLine($"[AudioService] Downloaded {audioBytes.Length} bytes of audio");
            
            if (audioBytes.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[AudioService] Audio bytes are empty");
                return;
            }

            // Save to temporary file
            var tempDir = System.IO.Path.GetTempPath();
            var tempFile = System.IO.Path.Combine(tempDir, $"flashcard_{Guid.NewGuid()}.mp3");
            
            await System.IO.File.WriteAllBytesAsync(tempFile, audioBytes);
            System.Diagnostics.Debug.WriteLine($"[AudioService] Saved audio to: {tempFile}");

            // Play the audio asynchronously and wait for it to complete
            await PlayAudioFileAsync(tempFile);

            // Clean up temp file after playback completes
            try
            {
                if (System.IO.File.Exists(tempFile))
                {
                    System.IO.File.Delete(tempFile);
                    System.Diagnostics.Debug.WriteLine($"[AudioService] Cleaned up temp file: {tempFile}");
                }
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"[AudioService] Cleanup error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService] Error playing pronunciation: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[AudioService] Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Plays English pronunciation for a given word using Google Translate TTS
    /// </summary>
    public async Task PlayEnglishPronunciation(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService] Playing English pronunciation for: {word}");

            // Google Translate TTS URL for English
            var encodedWord = Uri.EscapeDataString(word.Trim());
            var url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={encodedWord}&tl=en&client=tw-ob";
            
            System.Diagnostics.Debug.WriteLine($"[AudioService] Requesting URL: {url}");
            
            var response = await HttpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioService] HTTP Error: {response.StatusCode}");
                return;
            }

            var audioBytes = await response.Content.ReadAsByteArrayAsync();
            System.Diagnostics.Debug.WriteLine($"[AudioService] Downloaded {audioBytes.Length} bytes of audio");
            
            if (audioBytes.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[AudioService] Audio bytes are empty");
                return;
            }

            // Save to temporary file
            var tempDir = System.IO.Path.GetTempPath();
            var tempFile = System.IO.Path.Combine(tempDir, $"flashcard_{Guid.NewGuid()}.mp3");
            
            await System.IO.File.WriteAllBytesAsync(tempFile, audioBytes);
            System.Diagnostics.Debug.WriteLine($"[AudioService] Saved audio to: {tempFile}");

            // Play the audio asynchronously and wait for it to complete
            await PlayAudioFileAsync(tempFile);

            // Clean up temp file after playback completes
            try
            {
                if (System.IO.File.Exists(tempFile))
                {
                    System.IO.File.Delete(tempFile);
                    System.Diagnostics.Debug.WriteLine($"[AudioService] Cleaned up temp file: {tempFile}");
                }
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"[AudioService] Cleanup error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService] Error playing English pronunciation: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[AudioService] Stack trace: {ex.StackTrace}");
        }
    }

    private async Task PlayAudioFileAsync(string filePath)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService] Playing audio file: {filePath}");
            
            // Verify file exists and has content
            if (!System.IO.File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"[AudioService] File does not exist: {filePath}");
                return;
            }

            var fileInfo = new System.IO.FileInfo(filePath);
            System.Diagnostics.Debug.WriteLine($"[AudioService] File size: {fileInfo.Length} bytes");

            if (fileInfo.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[AudioService] File is empty");
                return;
            }

            // Dispose previous player if any
            _wavePlayer?.Dispose();
            
            // Create new player and play MP3 file
            _wavePlayer = new WaveOutEvent();
            var reader = new AudioFileReader(filePath);
            
            _wavePlayer.Init(reader);
            System.Diagnostics.Debug.WriteLine("[AudioService] Starting playback...");
            _wavePlayer.Play();
            
            // Wait for playback to complete asynchronously
            await Task.Run(() =>
            {
                while (_wavePlayer.PlaybackState == PlaybackState.Playing)
                {
                    System.Threading.Thread.Sleep(100);
                }
            });
            
            System.Diagnostics.Debug.WriteLine("[AudioService] Audio playback completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService] PlayAudioFile error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[AudioService] Stack trace: {ex.StackTrace}");
        }
    }

    public void Dispose()
    {
        _wavePlayer?.Dispose();
    }
}


