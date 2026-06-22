using System;
using System.Net.Http;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Danish.Desktop.Services;

public class AudioService : IDisposable
{
    private static readonly HttpClient HttpClient = new();
    private IWavePlayer? _wavePlayer;
    private static bool _headersInitialized;

    public AudioService()
    {
        if (!_headersInitialized)
        {
            HttpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            HttpClient.Timeout = TimeSpan.FromSeconds(10);
            _headersInitialized = true;
        }
    }

    public async Task PlayDanishPronunciation(string word, bool slow = false)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        try
        {
            var url  = $"https://translate.google.com/translate_tts?ie=UTF-8&q={Uri.EscapeDataString(word.Trim())}&tl=da&client=tw-ob";
            var resp = await HttpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return;
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0) return;
            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fc_{Guid.NewGuid()}.mp3");
            await System.IO.File.WriteAllBytesAsync(tmp, bytes);
            await PlayFileAsync(tmp, slow);
            try { if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp); } catch { }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Audio] Danish error: {ex.Message}"); }
    }

    public async Task PlayEnglishPronunciation(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        try
        {
            var url  = $"https://translate.google.com/translate_tts?ie=UTF-8&q={Uri.EscapeDataString(word.Trim())}&tl=en&client=tw-ob";
            var resp = await HttpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return;
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0) return;
            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fc_{Guid.NewGuid()}.mp3");
            await System.IO.File.WriteAllBytesAsync(tmp, bytes);
            await PlayFileAsync(tmp);
            try { if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp); } catch { }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Audio] English error: {ex.Message}"); }
    }

    private async Task PlayFileAsync(string path, bool slow = false)
    {
        try
        {
            if (!System.IO.File.Exists(path) || new System.IO.FileInfo(path).Length == 0) return;
            _wavePlayer?.Dispose();
            _wavePlayer = new WaveOutEvent();
            var reader = new AudioFileReader(path);
            if (slow)
                _wavePlayer.Init(new VariSpeedSampleProvider(reader, speed: 0.82));
            else
                _wavePlayer.Init(reader);
            _wavePlayer.Play();
            await Task.Run(() => { while (_wavePlayer.PlaybackState == PlaybackState.Playing) System.Threading.Thread.Sleep(100); });
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Audio] PlayFile error: {ex.Message}"); }
    }

    public void Dispose() => _wavePlayer?.Dispose();

    private sealed class VariSpeedSampleProvider : ISampleProvider
    {
        private readonly float[] _all;
        private readonly int _channels;
        private double _pos;
        private readonly double _speed;
        public WaveFormat WaveFormat { get; }

        public VariSpeedSampleProvider(ISampleProvider src, double speed)
        {
            _speed   = speed;
            WaveFormat = src.WaveFormat;
            _channels  = src.WaveFormat.Channels;
            var buf  = new float[8192];
            var list = new System.Collections.Generic.List<float>(src.WaveFormat.SampleRate * _channels * 30);
            int read;
            while ((read = src.Read(buf, 0, buf.Length)) > 0)
                for (int i = 0; i < read; i++) list.Add(buf[i]);
            _all = list.ToArray();
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int outFrames = count / _channels;
            int srcFrames = _all.Length / _channels;
            int written   = 0;
            for (int i = 0; i < outFrames; i++)
            {
                int f0 = (int)_pos;
                if (f0 >= srcFrames - 1) break;
                double frac = _pos - f0;
                int i0 = f0 * _channels, i1 = (f0 + 1) * _channels;
                for (int c = 0; c < _channels; c++)
                    buffer[offset + i * _channels + c] = (float)(_all[i0 + c] + frac * (_all[i1 + c] - _all[i0 + c]));
                written++;
                _pos += _speed;
            }
            return written * _channels;
        }
    }
}
