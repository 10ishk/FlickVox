using System.Collections.Concurrent;
using System.Diagnostics;
using FlickVox.Services;

var settings = new SettingsService();
settings.Current.VoiceId = "en_US-ryan-medium";
settings.Current.Speed = 1;
settings.Current.Volume = 0.35f;
settings.Current.OutputDevice = -1;
using var audio = new AudioPlaybackService();
var speech = new PiperSpeechService(new VoiceManager(), audio, settings);

if (args.Contains("invalid-device"))
{
    var sample = Path.Combine(Path.GetTempPath(), "FlickVox-audit-ryan-medium.wav");
    try
    {
        await audio.PlayAsync(sample, 0.2f, 999, CancellationToken.None);
        Console.WriteLine("Invalid device: unexpectedly accepted");
        Environment.ExitCode = 1;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Invalid device: {ex.GetType().Name}");
        using var file = new FileStream(sample, FileMode.Open, FileAccess.Read, FileShare.None);
        Console.WriteLine("Invalid device: source WAV handle released");
    }
    return;
}

HashSet<string> Wavs() => Directory.GetFiles(Path.GetTempPath(), "FlickVox-*.wav")
    .Where(p => !Path.GetFileName(p).StartsWith("FlickVox-audit-", StringComparison.Ordinal))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
HashSet<int> PiperIds() => Process.GetProcessesByName("piper").Select(p => p.Id).ToHashSet();
void CheckClean(string name, HashSet<string> wavs, HashSet<int> piperIds)
{
    var newWavs = Wavs().Except(wavs).ToArray();
    var newPiper = PiperIds().Except(piperIds).ToArray();
    Console.WriteLine($"{name}: newWavs={newWavs.Length} newPiper={newPiper.Length}");
    if (newWavs.Length > 0 || newPiper.Length > 0) Environment.ExitCode = 1;
}
async Task<bool> ExpectCancellation(Task task)
{
    try { await task.WaitAsync(TimeSpan.FromSeconds(20)); return false; }
    catch (OperationCanceledException) { return true; }
}

var baselineWavs = Wavs();
var baselinePiper = PiperIds();
var statuses = new ConcurrentQueue<string>();
await speech.SpeakAsync("FlickVox cleanup verification.", statuses.Enqueue)
    .WaitAsync(TimeSpan.FromSeconds(20));
Console.WriteLine($"Complete playback: statuses={string.Join(',', statuses)}");
CheckClean("Complete playback", baselineWavs, baselinePiper);

baselineWavs = Wavs();
baselinePiper = PiperIds();
var generating = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var longText = string.Concat(Enumerable.Repeat("This longer speech is cancelled while Piper is generating. ", 120));
var generationTask = speech.SpeakAsync(longText, status =>
{
    if (status == "Generating") generating.TrySetResult();
});
await generating.Task.WaitAsync(TimeSpan.FromSeconds(5));
var sawPiper = false;
for (var i = 0; i < 100; i++)
{
    if (PiperIds().Except(baselinePiper).Any()) { sawPiper = true; break; }
    await Task.Delay(10);
}
speech.Stop();
Console.WriteLine($"Generation cancellation: sawPiper={sawPiper} cancelled={await ExpectCancellation(generationTask)}");
CheckClean("Generation cancellation", baselineWavs, baselinePiper);
if (!sawPiper) Environment.ExitCode = 1;

baselineWavs = Wavs();
baselinePiper = PiperIds();
var speaking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var playbackTask = speech.SpeakAsync("This audio playback should be stopped before it finishes naturally. Please keep speaking for a few seconds.", status =>
{
    if (status == "Speaking") speaking.TrySetResult();
});
await speaking.Task.WaitAsync(TimeSpan.FromSeconds(15));
await Task.Delay(250);
speech.Stop();
Console.WriteLine($"Playback cancellation: cancelled={await ExpectCancellation(playbackTask)}");
CheckClean("Playback cancellation", baselineWavs, baselinePiper);

baselineWavs = Wavs();
baselinePiper = PiperIds();
var firstGenerating = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var first = speech.SpeakAsync(longText, status =>
{
    if (status == "Generating") firstGenerating.TrySetResult();
});
await firstGenerating.Task.WaitAsync(TimeSpan.FromSeconds(5));
speech.Stop();
var recoveryStates = new ConcurrentQueue<string>();
var second = speech.SpeakAsync("Speech works again after stopping.", recoveryStates.Enqueue);
var firstCancelled = await ExpectCancellation(first);
await second.WaitAsync(TimeSpan.FromSeconds(20));
Console.WriteLine($"Rapid recovery: firstCancelled={firstCancelled} secondReady={recoveryStates.Contains("Ready")}");
CheckClean("Rapid recovery", baselineWavs, baselinePiper);
if (!firstCancelled || !recoveryStates.Contains("Ready")) Environment.ExitCode = 1;
