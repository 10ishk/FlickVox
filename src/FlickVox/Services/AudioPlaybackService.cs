using NAudio.Wave;
namespace FlickVox.Services;
public sealed class AudioPlaybackService : IDisposable
{
    private WaveOutEvent? _output; private AudioFileReader? _reader;
    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;
    public Task PlayAsync(string file, float volume, int device, CancellationToken ct)
    {
        Stop(); _reader = new AudioFileReader(file) { Volume = volume }; _output = new WaveOutEvent { DeviceNumber = device };
        var done = new TaskCompletionSource(); _output.PlaybackStopped += (_,_) => done.TrySetResult(); _output.Init(_reader); _output.Play();
        ct.Register(Stop); return done.Task;
    }
    public void Stop() { _output?.Stop(); _output?.Dispose(); _reader?.Dispose(); _output=null; _reader=null; }
    public void Dispose() => Stop();
}
