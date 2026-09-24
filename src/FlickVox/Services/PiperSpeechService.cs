using System.Diagnostics;
using FlickVox.Models;
namespace FlickVox.Services;
public sealed class PiperSpeechService
{
    private readonly VoiceManager _voices; private readonly AudioPlaybackService _audio; private readonly SettingsService _settings;
    private CancellationTokenSource? _active; public string? LastText { get; private set; }
    public PiperSpeechService(VoiceManager voices, AudioPlaybackService audio, SettingsService settings) => (_voices,_audio,_settings)=(voices,audio,settings);
    public async Task SpeakAsync(string text, Action<string>? status = null)
    {
      if (string.IsNullOrWhiteSpace(text)) return; Stop(); _active=new(); var ct=_active.Token; LastText=text;
      var voice=_settings.Current.VoiceId; if(!_voices.IsInstalled(voice)) throw new InvalidOperationException("The selected Piper voice is not installed. Download it from Voice Manager first.");
      var piper=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlickVox","runtime","piper","piper.exe"); if(!File.Exists(piper)) throw new FileNotFoundException("Piper runtime is not installed. Run scripts/Install-FlickVox.ps1.",piper);
      var wav=Path.Combine(Path.GetTempPath(),$"FlickVox-{Guid.NewGuid():N}.wav");
      try { status?.Invoke("Generating"); var psi=new ProcessStartInfo(piper,$"--model \"{_voices.ModelPath(voice)}\" --output_file \"{wav}\" --length_scale {(1/_settings.Current.Speed).ToString(System.Globalization.CultureInfo.InvariantCulture)}") { RedirectStandardInput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true };
        using var process=Process.Start(psi) ?? throw new InvalidOperationException("Could not start Piper."); await process.StandardInput.WriteAsync(text); process.StandardInput.Close(); await process.WaitForExitAsync(ct); if(process.ExitCode != 0) throw new InvalidOperationException("Piper failed: " + await process.StandardError.ReadToEndAsync(ct)); ct.ThrowIfCancellationRequested(); status?.Invoke("Speaking"); await _audio.PlayAsync(wav,_settings.Current.Volume,_settings.Current.OutputDevice,ct); status?.Invoke("Ready");
      } finally { if(File.Exists(wav)) try { File.Delete(wav); } catch {} }
    }
    public void Stop(){ _active?.Cancel(); _audio.Stop(); }
}
