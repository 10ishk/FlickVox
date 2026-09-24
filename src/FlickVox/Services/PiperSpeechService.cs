using System.Diagnostics;
using System.Globalization;

namespace FlickVox.Services;

public sealed class PiperSpeechService
{
    readonly VoiceManager _voices;
    readonly AudioPlaybackService _audio;
    readonly SettingsService _settings;
    readonly object _gate = new();
    readonly SemaphoreSlim _singleSpeech = new(1, 1);
    CancellationTokenSource? _active;
    byte[]? _cachedAudio;
    string? _cachedVoice;
    double _cachedSpeed;
    const int MaxCachedAudioBytes = 16 * 1024 * 1024;
    public string? LastText { get; private set; }
    public event Action? LastMessageChanged;

    public PiperSpeechService(VoiceManager voices, AudioPlaybackService audio, SettingsService settings) =>
        (_voices, _audio, _settings) = (voices, audio, settings);

    public Task SpeakAsync(string text, Action<string>? status = null) => RunAsync(text, status, remember: true, repeat: false);
    public Task PreviewAsync(string text, Action<string>? status = null) => RunAsync(text, status, remember: false, repeat: false);
    public Task RepeatAsync(Action<string>? status = null) => LastText is { } text ? RunAsync(text, status, remember: true, repeat: true) : Task.CompletedTask;

    async Task RunAsync(string text, Action<string>? status, bool remember, bool repeat)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var run = new CancellationTokenSource();
        lock (_gate)
        {
            _active?.Cancel();
            _active = run;
        }
        _audio.Stop();
        try
        {
            await _singleSpeech.WaitAsync(run.Token);
            try { await GenerateAndPlayAsync(text, run.Token, status, remember, repeat); }
            finally { _singleSpeech.Release(); }
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_active, run)) _active = null;
                run.Dispose();
            }
        }
    }

    async Task GenerateAndPlayAsync(string text, CancellationToken ct, Action<string>? status, bool remember, bool repeat)
    {
        ct.ThrowIfCancellationRequested();
        var voice = _settings.Current.VoiceId;
        if (!_voices.IsInstalled(voice))
            throw new InvalidOperationException("The selected Piper voice is not installed. Download it from Voice Manager first.");
        var piper = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlickVox", "runtime", "piper", "piper.exe");
        if (!File.Exists(piper))
            throw new FileNotFoundException("Piper runtime is not installed.", piper);

        var wav = Path.Combine(Path.GetTempPath(), $"FlickVox-{Guid.NewGuid():N}.wav");
        try
        {
            ct.ThrowIfCancellationRequested();
            var speed = _settings.Current.Speed;
            var cached = repeat && _cachedAudio is not null && _cachedVoice == voice && _cachedSpeed == speed;
            if (cached)
                await File.WriteAllBytesAsync(wav, _cachedAudio!, ct);
            else
            {
                status?.Invoke("Generating");
                var args = $"--model \"{_voices.ModelPath(voice)}\" --output_file \"{wav}\" --length_scale {(1 / speed).ToString(CultureInfo.InvariantCulture)}";
                var info = new ProcessStartInfo(piper, args)
                {
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start Piper."))
                {
                    using var cancellation = ct.Register(() =>
                    {
                        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                        catch (InvalidOperationException) { }
                    });
                    try
                    {
                        await process.StandardInput.WriteAsync(text.AsMemory(), ct);
                        process.StandardInput.Close();
                        await process.WaitForExitAsync(ct);
                        ct.ThrowIfCancellationRequested();
                        if (process.ExitCode != 0)
                            throw new InvalidOperationException("Piper failed: " + await process.StandardError.ReadToEndAsync(ct));
                    }
                    finally
                    {
                        if (!process.HasExited)
                        {
                            try { process.Kill(entireProcessTree: true); }
                            catch (InvalidOperationException) { }
                        }
                        await process.WaitForExitAsync();
                    }
                }
                if (remember)
                {
                    var size = new FileInfo(wav).Length;
                    _cachedAudio = size <= MaxCachedAudioBytes ? await File.ReadAllBytesAsync(wav, ct) : null;
                    _cachedVoice = voice;
                    _cachedSpeed = speed;
                    ct.ThrowIfCancellationRequested();
                    LastText = text;
                    LastMessageChanged?.Invoke();
                }
            }

            ct.ThrowIfCancellationRequested();
            status?.Invoke("Speaking");
            await _audio.PlayAsync(wav, _settings.Current.Volume, _settings.Current.OutputDevice, ct);
            ct.ThrowIfCancellationRequested();
            status?.Invoke("Ready");
        }
        finally
        {
            if (File.Exists(wav)) File.Delete(wav);
        }
    }

    public void Stop()
    {
        lock (_gate) _active?.Cancel();
        _audio.Stop();
    }
}
