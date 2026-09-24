using FlickVox.Infrastructure;
using FlickVox.Services;

namespace FlickVox;

public partial class SetupWindow : Window
{
    readonly PiperRuntimeManager _runtime = new();
    readonly VoiceManager _voices;
    readonly SettingsService _settings;
    readonly HotkeyService _hotkey;
    readonly HistoryService _history;
    readonly PiperSpeechService _speech;

    public SetupWindow(VoiceManager voices, SettingsService settings, HotkeyService hotkey, HistoryService history, PiperSpeechService speech)
    {
        InitializeComponent();
        _voices = voices; _settings = settings; _hotkey = hotkey; _history = history;
        _speech = speech;
        SourceInitialized += (_, _) => WindowBackdrop.ApplyDarkTitleBar(this);
        Refresh();
    }
    void Refresh()
    {
        RuntimeState.Text = _runtime.IsInstalled ? "Piper runtime: installed" : "Piper runtime: missing";
        InstallButton.IsEnabled = !_runtime.IsInstalled;
        VoiceState.Text = _voices.IsInstalled(_settings.Current.VoiceId) ? "Selected voice: installed" : "Selected voice: missing";
        TestButton.IsEnabled = _runtime.IsInstalled && _voices.IsInstalled(_settings.Current.VoiceId);
    }
    async void InstallRuntime(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        SetupStatus.Text = "Downloading Piper from the official release…";
        try { await _runtime.InstallAsync(CancellationToken.None); SetupStatus.Text = "Piper is ready. Select a voice, then try speaking."; }
        catch (Exception ex) { SetupStatus.Text = "Piper setup failed: " + ex.Message; }
        Refresh();
    }
    void ManageVoices(object sender, RoutedEventArgs e)
    {
        new VoiceManagerWindow(_voices) { Owner = this }.ShowDialog();
        _settings.Load();
        Refresh();
    }
    void AudioSettings(object sender, RoutedEventArgs e) =>
        new SettingsWindow(_settings, _voices, _hotkey, _history) { Owner = this }.ShowDialog();
    async void TestSpeech(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        HeardButton.IsEnabled = false;
        SetupStatus.Text = "Playing a short test phrase…";
        try
        {
            await _speech.PreviewAsync("Hello, this is FlickVox.");
            SetupStatus.Text = "Playback completed in software. Confirm only if you actually heard it.";
            HeardButton.IsEnabled = true;
        }
        catch (OperationCanceledException) { SetupStatus.Text = "Test stopped."; }
        catch (Exception ex) { SetupStatus.Text = "Test failed: " + ex.Message; }
        Refresh();
    }
    void ConfirmSpeech(object sender, RoutedEventArgs e)
    {
        _settings.Current.FirstSpeechConfirmed = true;
        _settings.Save();
        SetupStatus.Text = "Audible speech confirmed. FlickVox is ready to use.";
        HeardButton.IsEnabled = false;
    }
    void Skip(object sender, RoutedEventArgs e)
    {
        _settings.Current.FirstRunGuidanceDismissed = true;
        _settings.Save();
        Close();
    }
    void Close(object sender, RoutedEventArgs e) => Close();
}
