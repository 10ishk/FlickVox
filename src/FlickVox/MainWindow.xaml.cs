using System.Text.Json;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using FlickVox.Infrastructure;
using FlickVox.Models;
using FlickVox.Services;
using NAudio.Wave;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;

namespace FlickVox;

public partial class MainWindow : Window
{
    readonly SettingsService _settings = new();
    readonly VoiceManager _voices = new();
    readonly AudioPlaybackService _audio = new();
    readonly PiperSpeechService _speech;
    readonly HotkeyService _hotkey = new();
    readonly List<string> _history = new();
    readonly List<SavedPhrase> _phrases = new();
    OverlayWindow? _overlay;
    NotifyIcon? _tray;
    bool _isBusy;
    bool _exitRequested;
    int _speechVersion;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowBackdrop.ApplyDarkTitleBar(this);
        _speech = new PiperSpeechService(_voices, _audio, _settings);
        Voice.ItemsSource = Services.VoiceManager.Voices;
        Voice.SelectedItem = Services.VoiceManager.Voices.FirstOrDefault(v => v.Id == _settings.Current.VoiceId)
                             ?? Services.VoiceManager.Voices[0];
        Speed.Value = _settings.Current.Speed;
        ApplyComposerTypography();
        HideAfter.IsChecked = _settings.Current.HideOverlayAfterSpeaking;
        Output.Items.Add("Default Windows output");
        for (var i = 0; i < WaveOut.DeviceCount; i++)
            Output.Items.Add(WaveOut.GetCapabilities(i).ProductName);
        Output.SelectedIndex = Math.Clamp(_settings.Current.OutputDevice + 1, 0, Output.Items.Count - 1);
        LoadPhrases();
        RefreshPhrases();
        UpdateComposerActions();
        SetActiveTab(true);
        ApplyResponsiveLayout();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { _hotkey.Register(this); _hotkey.Pressed += (_, _) => ShowOverlay(); }
        catch (Exception ex) { ShowNotice(ex.Message, false); }
        CreateTray();
        Input.Focus();
    }

    void CreateTray()
    {
        var trayPath = Path.Combine(AppContext.BaseDirectory, "Assets", "FlickVoxTray.ico");
        _tray = new NotifyIcon { Text = "FlickVox", Icon = new System.Drawing.Icon(trayPath), Visible = true };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open FlickVox", null, (_, _) => { Show(); Activate(); });
        menu.Items.Add("Open compact overlay", null, (_, _) => ShowOverlay());
        menu.Items.Add("Stop speech", null, (_, _) => StopSpeech());
        menu.Items.Add("Repeat last message", null, async (_, _) => await RepeatAsync());
        menu.Items.Add("Settings", null, (_, _) => OpenSettings(this, new RoutedEventArgs()));
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _exitRequested = true;
            _tray!.Visible = false;
            System.Windows.Application.Current.Shutdown();
        });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => { Show(); Activate(); };
    }

    void SetStatus(string status)
    {
        StatusText.Text = status switch
        {
            "Generating" => "Preparing",
            "Speaking" => "Speaking",
            "Ready" => "Ready",
            "Stopped" => "Ready",
            _ => "Error"
        };
        StatusDot.Fill = (Brush)FindResource(status is "Ready" or "Speaking" ? "Brush.Signal" : "Brush.AccentText");
        if (status == "Speaking") ComposerCard.BorderBrush = (Brush)FindResource("Brush.Signal");
        else ComposerCard.ClearValue(System.Windows.Controls.Border.BorderBrushProperty);
        if (status is "Ready" or "Stopped") SetBusy(false);
        else if (status is "Generating" or "Speaking") SetBusy(true);
    }

    void SetBusy(bool busy)
    {
        _isBusy = busy;
        PrimaryLabel.Text = busy ? "Stop" : "Speak";
        PrimaryIcon.Data = (Geometry)FindResource(busy ? "Icon.Stop" : "Icon.Play");
    }

    void ShowNotice(string message, bool voiceAction)
    {
        NoticeText.Text = message;
        NoticeAction.Visibility = voiceAction ? Visibility.Visible : Visibility.Collapsed;
        Notice.Visibility = Visibility.Visible;
        StatusText.Text = voiceAction ? "Voice not ready" : "Error";
        StatusDot.Fill = (Brush)FindResource("Brush.TextTertiary");
        ComposerCard.BorderBrush = (Brush)FindResource("Brush.Danger");
    }

    async void PrimaryAction(object sender, RoutedEventArgs e)
    {
        if (_isBusy) { StopSpeech(); return; }
        await SpeakAsync(Input.Text);
    }

    void StopSpeech()
    {
        _speechVersion++;
        _speech.Stop();
        SetStatus("Ready");
    }

    async Task SpeakAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var version = ++_speechVersion;
        Notice.Visibility = Visibility.Collapsed;
        AddHistory(text);
        SetBusy(true);
        SetStatus("Generating");
        try { await _speech.SpeakAsync(text, s => Dispatcher.Invoke(() => { if (version == _speechVersion) SetStatus(s); })); }
        catch (OperationCanceledException) { if (version == _speechVersion) SetStatus("Ready"); }
        catch (Exception ex) { if (version == _speechVersion) ShowNotice(ex.Message, ex.Message.Contains("voice", StringComparison.OrdinalIgnoreCase)); }
        finally { if (version == _speechVersion) { SetBusy(false); if (StatusText.Text is "Preparing" or "Speaking") SetStatus("Ready"); } }
    }

    async void Repeat(object sender, RoutedEventArgs e) => await RepeatAsync();
    async Task RepeatAsync() { if (_speech.LastText is not null) await SpeakAsync(_speech.LastText); }
    async void Preview(object sender, RoutedEventArgs e) => await SpeakAsync("Hello, this is the selected FlickVox voice.");

    void InputChanged(object sender, TextChangedEventArgs e)
    {
        if (InputPlaceholder is null) return;
        UpdateComposerActions();
    }
    void UpdateComposerActions()
    {
        var hasText = !string.IsNullOrEmpty(Input.Text);
        InputPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
        ClearButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
    }
    void ClearInput(object sender, RoutedEventArgs e) { Input.Clear(); Input.Focus(); }
    void InputKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; _ = SpeakAsync(Input.Text); }
        else if (e.Key == Key.Escape) StopSpeech();
        else if (e.Key == Key.L && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; Input.Clear(); }
        else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; SavePhrase(sender, e); }
        else if (e.Key == Key.R && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; _ = RepeatAsync(); }
    }

    void AddHistory(string text)
    {
        _history.Remove(text);
        _history.Insert(0, text);
        if (_history.Count > 30) _history.RemoveAt(30);
        History.ItemsSource = null;
        History.ItemsSource = _history;
        RecentEmpty.Visibility = Visibility.Collapsed;
        RepeatButton.IsEnabled = true;
    }
    void HistorySpeak(object sender, MouseButtonEventArgs e)
    {
        if (History.SelectedItem is string text) { Input.Text = text; _ = SpeakAsync(text); }
    }
    void PhraseSpeak(object sender, MouseButtonEventArgs e)
    {
        if (Phrases.SelectedItem is SavedPhrase phrase) { Input.Text = phrase.Text; _ = SpeakAsync(phrase.Text); }
    }

    void SpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpeedValue is null) return;
        _settings.Current.Speed = Speed.Value;
        SpeedValue.Text = Speed.Value.ToString("F2") + "×";
        if (SpeedFlyoutValue is not null) SpeedFlyoutValue.Text = SpeedValue.Text;
        _settings.Save();
    }
    void VoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Voice.SelectedItem is not VoiceDefinition voice || VoiceValue is null) return;
        _settings.Current.VoiceId = voice.Id;
        VoiceValue.Text = voice.ToString();
        _settings.Save();
        VoiceFlyout.IsOpen = false;
    }
    void OutputChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutputValue is null || Output.SelectedIndex < 0) return;
        _settings.Current.OutputDevice = Output.SelectedIndex - 1;
        OutputValue.Text = Output.SelectedIndex == 0 ? "Default output" : Output.SelectedItem?.ToString() ?? "Default output";
        OutputChip.ToolTip = OutputValue.Text;
        _settings.Save();
        OutputFlyout.IsOpen = false;
    }

    void ToggleVoiceFlyout(object sender, RoutedEventArgs e) => VoiceFlyout.IsOpen = !VoiceFlyout.IsOpen;
    void ToggleOutputFlyout(object sender, RoutedEventArgs e) => OutputFlyout.IsOpen = !OutputFlyout.IsOpen;
    void ToggleSpeedFlyout(object sender, RoutedEventArgs e) => SpeedFlyout.IsOpen = !SpeedFlyout.IsOpen;
    void FlyoutOpened(object sender, EventArgs e)
    {
        if (sender is not Popup opened) return;
        foreach (var popup in new[] { VoiceFlyout, OutputFlyout, SpeedFlyout })
            if (!ReferenceEquals(popup, opened)) popup.IsOpen = false;
        if (ReferenceEquals(opened, VoiceFlyout)) Voice.Focus();
        else if (ReferenceEquals(opened, OutputFlyout)) Output.Focus();
        else Speed.Focus();
        VoiceChip.BorderBrush = (Brush)FindResource(VoiceFlyout.IsOpen ? "Brush.AccentText" : "Brush.StrokeControl");
        OutputChip.BorderBrush = (Brush)FindResource(OutputFlyout.IsOpen ? "Brush.AccentText" : "Brush.StrokeControl");
        SpeedChip.BorderBrush = (Brush)FindResource(SpeedFlyout.IsOpen ? "Brush.AccentText" : "Brush.StrokeControl");
    }
    void WindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (!VoiceFlyout.IsOpen && !OutputFlyout.IsOpen && !SpeedFlyout.IsOpen) return;
        VoiceFlyout.IsOpen = OutputFlyout.IsOpen = SpeedFlyout.IsOpen = false;
        e.Handled = true;
    }

    void SavePhrase(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Input.Text)) return;
        var text = Input.Text.Trim();
        _phrases.Add(new SavedPhrase(Guid.NewGuid(), text.Length > 32 ? text[..32] + "…" : text, text));
        SavePhrases();
        RefreshPhrases();
        SetActiveTab(true);
    }
    string PhrasesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlickVox", "phrases.json");
    void LoadPhrases()
    {
        try { if (File.Exists(PhrasesPath)) _phrases.AddRange(JsonSerializer.Deserialize<List<SavedPhrase>>(File.ReadAllText(PhrasesPath)) ?? []); }
        catch { /* Existing file is preserved for recovery. */ }
    }
    void SavePhrases() => File.WriteAllText(PhrasesPath, JsonSerializer.Serialize(_phrases));
    void RefreshPhrases()
    {
        Phrases.ItemsSource = null;
        Phrases.ItemsSource = _phrases;
        SavedEmpty.Visibility = _phrases.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    void ShowSaved(object sender, RoutedEventArgs e) => SetActiveTab(true);
    void ShowRecent(object sender, RoutedEventArgs e) => SetActiveTab(false);
    void SetActiveTab(bool saved)
    {
        SavedPanel.Visibility = saved ? Visibility.Visible : Visibility.Collapsed;
        RecentPanel.Visibility = saved ? Visibility.Collapsed : Visibility.Visible;
        SavedTab.Background = (Brush)FindResource(saved ? "Brush.Accent" : "Brush.SurfaceRaised");
        RecentTab.Background = (Brush)FindResource(saved ? "Brush.SurfaceRaised" : "Brush.Accent");
    }

    void WindowSizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();
    void ApplyResponsiveLayout()
    {
        if (Workspace is null) return;
        var narrow = ActualWidth < 880;
        Workspace.ColumnDefinitions[2].Width = narrow ? new GridLength(0) : new GridLength(320);
        Grid.SetColumn(Rail, narrow ? 0 : 2);
        Grid.SetRow(Rail, narrow ? 1 : 0);
        Rail.Margin = narrow ? new Thickness(0, 16, 0, 0) : new Thickness();
        HotkeyHint.Visibility = ActualWidth < 720 ? Visibility.Collapsed : Visibility.Visible;
    }

    void OpenVoiceManager(object sender, RoutedEventArgs e)
    {
        VoiceFlyout.IsOpen = false;
        new VoiceManagerWindow(_voices) { Owner = this }.ShowDialog();
        _settings.Load();
        Voice.SelectedItem = Services.VoiceManager.Voices.FirstOrDefault(v => v.Id == _settings.Current.VoiceId);
    }
    void OpenSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings, _voices) { Owner = this };
        dialog.ShowDialog();
        ApplyComposerTypography();
        HideAfter.IsChecked = _settings.Current.HideOverlayAfterSpeaking;
        Voice.SelectedItem = Services.VoiceManager.Voices.FirstOrDefault(v => v.Id == _settings.Current.VoiceId);
        Speed.Value = _settings.Current.Speed;
        Output.SelectedIndex = Math.Clamp(_settings.Current.OutputDevice + 1, 0, Output.Items.Count - 1);
    }
    void ApplyComposerTypography()
    {
        Input.FontSize = _settings.Current.ComposerFontSize;
        InputPlaceholder.FontSize = Input.FontSize;
        var font = _settings.Current.ComposerFont switch
        {
            "Atkinson Hyperlegible" => (FontFamily)FindResource("Font.Accessible"),
            "System" => new FontFamily("Segoe UI Variable Text, Segoe UI"),
            _ => (FontFamily)FindResource("Font.UI")
        };
        Input.FontFamily = font;
        InputPlaceholder.FontFamily = font;
    }
    void OpenOverlay(object sender, RoutedEventArgs e) => ShowOverlay();
    void ShowOverlay() { _overlay ??= new OverlayWindow(this, _speech, _settings); _overlay.ShowAndFocus(); }
    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_exitRequested) { e.Cancel = true; Hide(); }
        _settings.Current.OutputDevice = Output.SelectedIndex - 1;
        _settings.Current.HideOverlayAfterSpeaking = HideAfter.IsChecked == true;
        _settings.Save();
    }
    protected override void OnClosed(EventArgs e) { _speech.Stop(); _tray?.Dispose(); _hotkey.Dispose(); _audio.Dispose(); base.OnClosed(e); }
}
