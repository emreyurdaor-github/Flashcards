using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Platform;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

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
    public async Task PlayDanishPronunciation(string word, bool slow = false)
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
            await PlayAudioFileAsync(tempFile, slow);

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

    private async Task PlayAudioFileAsync(string filePath, bool slow = false)
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

            if (slow)
            {
                // Read source at 75% frame rate via linear interpolation.
                // Output WaveFormat stays at the original rate (standard, unaffected by OS mixer).
                var slowProvider = new VariSpeedSampleProvider(reader, speed: 0.82);
                _wavePlayer.Init(slowProvider);
                System.Diagnostics.Debug.WriteLine("[AudioService] Slow mode: using VariSpeedSampleProvider at 0.75x");
            }
            else
            {
                _wavePlayer.Init(reader);
            }

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

    /// <summary>
    /// Plays the correct answer sound from embedded assets
    /// </summary>
    public async Task PlayCorrectSoundAsync()
    {
        await PlayLocalSoundAsync("avares://Flashcards/Assets/Sounds/correct.mp3");
    }

    /// <summary>
    /// Plays the wrong answer sound from embedded assets
    /// </summary>
    public async Task PlayWrongSoundAsync()
    {
        await PlayLocalSoundAsync("avares://Flashcards/Assets/Sounds/wrong.mp3");
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

            await PlayAudioFileAsync(tempFile);

            try { if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile); } catch { /* ignore cleanup errors */ }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService] PlayLocalSound error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _wavePlayer?.Dispose();
    }

    /// <summary>
    /// Reads a source ISampleProvider at a fractional rate to change playback speed
    /// (pitch changes proportionally, like slowing a tape).
    /// The output WaveFormat is identical to the source so no OS-level resampling interferes.
    /// </summary>
    private sealed class VariSpeedSampleProvider : ISampleProvider
    {
        private readonly float[] _allSamples;
        private readonly int _channels;
        private double _readPos;
        private readonly double _speed;

        public WaveFormat WaveFormat { get; }

        /// <param name="source">Audio source to wrap.</param>
        /// <param name="speed">Playback speed factor (e.g. 0.75 = 75% speed, lower pitch).</param>
        public VariSpeedSampleProvider(ISampleProvider source, double speed)
        {
            _speed = speed;
            WaveFormat = source.WaveFormat;
            _channels = source.WaveFormat.Channels;

            // Buffer the entire source (TTS clips are short, typically < 30 s)
            var buf = new float[8192];
            var allSamples = new System.Collections.Generic.List<float>(
                source.WaveFormat.SampleRate * _channels * 30);
            int read;
            while ((read = source.Read(buf, 0, buf.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                    allSamples.Add(buf[i]);
            }
            _allSamples = allSamples.ToArray();
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int outputFrames = count / _channels;
            int totalSourceFrames = _allSamples.Length / _channels;
            int written = 0;

            for (int i = 0; i < outputFrames; i++)
            {
                int frame0 = (int)_readPos;
                if (frame0 >= totalSourceFrames - 1) break;

                double frac = _readPos - frame0;
                int idx0 = frame0 * _channels;
                int idx1 = (frame0 + 1) * _channels;

                // Linear interpolation between adjacent source frames
                for (int c = 0; c < _channels; c++)
                {
                    buffer[offset + i * _channels + c] =
                        (float)(_allSamples[idx0 + c] + frac * (_allSamples[idx1 + c] - _allSamples[idx0 + c]));
                }

                written++;
                _readPos += _speed; // advance at fractional rate (0.75 → slower)
            }

            return written * _channels;
        }
    }
}