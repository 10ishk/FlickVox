using NAudio.Wave;

namespace FlickVox.Services;

public sealed class AudioPlaybackService : IDisposable
{
    readonly object _gate = new();
    WaveOutEvent? _output;
    AudioFileReader? _reader;

    public bool IsPlaying
    {
        get { lock (_gate) return _output?.PlaybackState == PlaybackState.Playing; }
    }

    public async Task PlayAsync(string file, float volume, int device, CancellationToken ct)
    {
        Stop();
        ct.ThrowIfCancellationRequested();
        var reader = new AudioFileReader(file) { Volume = volume };
        WaveOutEvent? output = null;
        try
        {
            output = new WaveOutEvent { DeviceNumber = device };
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            output.PlaybackStopped += (_, args) =>
            {
                if (args.Exception is null) finished.TrySetResult();
                else finished.TrySetException(args.Exception);
            };
            output.Init(reader);
            lock (_gate) { _reader = reader; _output = output; }
            using var registration = ct.Register(Stop);
            ct.ThrowIfCancellationRequested();
            output.Play();
            await finished.Task.WaitAsync(ct);
        }
        finally
        {
            Stop(); // Release the WAV handle before Piper deletes its temporary file.
            output?.Dispose();
            reader.Dispose();
        }
    }

    public void Stop()
    {
        WaveOutEvent? output;
        AudioFileReader? reader;
        lock (_gate)
        {
            output = _output;
            reader = _reader;
            _output = null;
            _reader = null;
        }
        if (output is not null)
        {
            try { output.Stop(); }
            finally { output.Dispose(); }
        }
        reader?.Dispose();
    }

    public void Dispose() => Stop();
}
