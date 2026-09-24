using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using FlickVox.Models;
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

baselineWavs = Wavs();
baselinePiper = PiperIds();
var repeatStates = new ConcurrentQueue<string>();
await speech.RepeatAsync(repeatStates.Enqueue).WaitAsync(TimeSpan.FromSeconds(20));
Console.WriteLine($"Cached Repeat: noGeneration={!repeatStates.Contains("Generating")} speaking={repeatStates.Contains("Speaking")} ready={repeatStates.Contains("Ready")}");
CheckClean("Cached Repeat", baselineWavs, baselinePiper);
if (repeatStates.Contains("Generating") || !repeatStates.Contains("Speaking") || !repeatStates.Contains("Ready")) Environment.ExitCode = 1;

var isolatedRoot = Path.Combine(Path.GetTempPath(), $"FlickVox-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(isolatedRoot);
try
{
    var isolatedSettings = new SettingsService();
    typeof(SettingsService).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(isolatedSettings, Path.Combine(isolatedRoot, "settings.json"));
    typeof(SettingsService).GetProperty(nameof(SettingsService.Current))!
        .SetValue(isolatedSettings, new AppSettings());
    isolatedSettings.Current.VoiceId = "en_US-lessac-medium";
    isolatedSettings.Current.Speed = 1.2;
    isolatedSettings.Current.OutputDevice = -1;
    isolatedSettings.Save();
    isolatedSettings.Current.Speed = 0.7;
    isolatedSettings.Load();
    var settingsPass = isolatedSettings.Current.VoiceId == "en_US-lessac-medium" &&
        isolatedSettings.Current.Speed == 1.2 && isolatedSettings.Current.OutputDevice == -1;
    Console.WriteLine($"Isolated settings/voice/speed/output persistence: pass={settingsPass}");
    if (!settingsPass) Environment.ExitCode = 1;

    var history = new HistoryService(isolatedSettings);
    history.Add(" first ");
    history.Add("second");
    history.Add("first");
    var historyPass = history.Items.SequenceEqual(["first", "second"]);
    Console.WriteLine($"Shared history ordering/deduplication: pass={historyPass}");
    if (!historyPass) Environment.ExitCode = 1;

    var phrases = new PhraseService();
    typeof(PhraseService).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(phrases, Path.Combine(isolatedRoot, "phrases.json"));
    phrases.Items.Clear();
    phrases.Add(" disposable ", " text one ");
    var added = phrases.Items.Single();
    phrases.Update(added, "renamed", "text two");
    var edited = phrases.Items.Single();
    var phrasePass = added.Name == "disposable" && edited.Name == "renamed" && edited.Text == "text two";
    phrases.Delete(edited);
    phrasePass &= phrases.Items.Count == 0 && File.ReadAllText(Path.Combine(isolatedRoot, "phrases.json")) == "[]";
    Console.WriteLine($"Isolated phrase create/edit/delete: pass={phrasePass}");
    if (!phrasePass) Environment.ExitCode = 1;

    var selectedStates = new ConcurrentQueue<string>();
    await new PiperSpeechService(new VoiceManager(), audio, isolatedSettings)
        .SpeakAsync("Selected voice and speed smoke test.", selectedStates.Enqueue)
        .WaitAsync(TimeSpan.FromSeconds(20));
    var selectedPass = selectedStates.Contains("Generating") && selectedStates.Contains("Speaking") && selectedStates.Contains("Ready");
    Console.WriteLine($"Selected voice/speed/default output: pass={selectedPass}");
    if (!selectedPass) Environment.ExitCode = 1;

    var sample = Path.Combine(isolatedRoot, "invalid-device.wav");
    using (var writer = new NAudio.Wave.WaveFileWriter(sample, new NAudio.Wave.WaveFormat(22050, 1)))
        writer.Write(new byte[22050 * 2], 0, 22050 * 2);
    var invalidRejected = false;
    try { await audio.PlayAsync(sample, 0.2f, 999, CancellationToken.None); }
    catch (NAudio.MmException) { invalidRejected = true; }
    using (var file = new FileStream(sample, FileMode.Open, FileAccess.Read, FileShare.None)) { }
    Console.WriteLine($"Invalid device recovery/handle release: pass={invalidRejected}");
    if (!invalidRejected) Environment.ExitCode = 1;
}
finally { Directory.Delete(isolatedRoot, recursive: true); }
