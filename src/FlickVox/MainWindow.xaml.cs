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
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace FlickVox;

public partial class MainWindow : Window
{
    readonly SettingsService _settings = new();
    readonly VoiceManager _voices = new();
    readonly AudioPlaybackService _audio = new();
    readonly PiperSpeechService _speech;
    readonly HotkeyService _hotkey = new();
    readonly HistoryService _history;
    readonly PhraseService _phrases = new();
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
        _speech.LastMessageChanged += () => Dispatcher.Invoke(() => RepeatButton.IsEnabled = true);
        _history = new HistoryService(_settings);
        Voice.ItemsSource = Services.VoiceManager.Voices;
        Voice.SelectedItem = Services.VoiceManager.Voices.FirstOrDefault(v => v.Id == _settings.Current.VoiceId)
                             ?? Services.VoiceManager.Voices[0];
        Speed.Value = _settings.Current.Speed;
        ApplyComposerTypography();
        Output.Items.Add("Default Windows output");
        for (var i = 0; i < WaveOut.DeviceCount; i++)
            Output.Items.Add(WaveOut.GetCapabilities(i).ProductName);
        Output.SelectedIndex = Math.Clamp(_settings.Current.OutputDevice + 1, 0, Output.Items.Count - 1);
        Phrases.ItemsSource = _phrases.Items;
        History.ItemsSource = _history.Items;
        _phrases.Items.CollectionChanged += (_, _) => RefreshEmptyStates();
        _history.Items.CollectionChanged += (_, _) => RefreshEmptyStates();
        RefreshEmptyStates();
        UpdateComposerActions();
        SetActiveTab(_settings.Current.ShowSavedPhrases);
        UpdatePhrasePanel();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { _hotkey.Register(this, _settings.Current.Hotkey); _hotkey.Pressed += (_, _) => ShowOverlay(); }
        catch (Exception ex) { ShowNotice(ex.Message, false); }
        CreateTray();
        CheckReadiness();
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
            "Ready" => DependenciesReady() ? "Ready" : "Voice not ready",
            "Stopped" => DependenciesReady() ? "Ready" : "Voice not ready",
            _ => "Error"
        };
        StatusDot.Fill = (Brush)FindResource(status == "Speaking" || ((status is "Ready" or "Stopped") && DependenciesReady())
            ? "Brush.Signal" : "Brush.AccentText");
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
    void DismissNotice(object sender, RoutedEventArgs e)
    {
        Notice.Visibility = Visibility.Collapsed;
        if (DependenciesReady() && !_settings.Current.FirstSpeechConfirmed)
        {
            _settings.Current.FirstRunGuidanceDismissed = true;
            _settings.Save();
        }
    }

    bool DependenciesReady() =>
        File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlickVox", "runtime", "piper", "piper.exe"))
        && _voices.IsInstalled(_settings.Current.VoiceId);

    void CheckReadiness()
    {
        if (DependenciesReady())
        {
            SetStatus("Ready");
            if (!_settings.Current.FirstSpeechConfirmed && !_settings.Current.FirstRunGuidanceDismissed)
            {
                NoticeText.Text = "Ready for a first audio test.";
                NoticeAction.Visibility = Visibility.Visible;
                Notice.Visibility = Visibility.Visible;
            }
            else Notice.Visibility = Visibility.Collapsed;
            return;
        }
        ShowNotice("Piper or the selected voice is missing.", true);
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
        _history.Add(text);
        SetBusy(true);
        SetStatus("Generating");
        try { await _speech.SpeakAsync(text, s => Dispatcher.Invoke(() => { if (version == _speechVersion) SetStatus(s); })); }
        catch (OperationCanceledException) { if (version == _speechVersion) SetStatus("Ready"); }
        catch (Exception ex) { if (version == _speechVersion) ShowNotice(ex.Message, ex.Message.Contains("voice", StringComparison.OrdinalIgnoreCase)); }
        finally { if (version == _speechVersion) { SetBusy(false); RepeatButton.IsEnabled = _speech.LastText is not null; if (StatusText.Text is "Preparing" or "Speaking") SetStatus("Ready"); } }
    }

    async void Repeat(object sender, RoutedEventArgs e) => await RepeatAsync();
    async Task RepeatAsync()
    {
        if (_speech.LastText is null) return;
        var version = ++_speechVersion;
        SetBusy(true);
        try { await _speech.RepeatAsync(s => Dispatcher.Invoke(() => { if (version == _speechVersion) SetStatus(s); })); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowNotice(ex.Message, false); }
        finally { if (version == _speechVersion) { SetBusy(false); if (StatusText.Text is "Preparing" or "Speaking") SetStatus("Ready"); } }
    }
    async void Preview(object sender, RoutedEventArgs e)
    {
        var version = ++_speechVersion;
        VoiceFlyout.IsOpen = false;
        SetBusy(true);
        try { await _speech.PreviewAsync("Hello, this is the selected FlickVox voice.", s => Dispatcher.Invoke(() => { if (version == _speechVersion) SetStatus(s); })); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowNotice(ex.Message, false); }
        finally { if (version == _speechVersion) { SetBusy(false); if (StatusText.Text is "Preparing" or "Speaking") SetStatus("Ready"); } }
    }

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

    void PlayHistory(object sender, RoutedEventArgs e)
    { if (sender is Button { Tag: string text }) { Input.Text = text; _ = SpeakAsync(text); } }
    void PlayPhrase(object sender, RoutedEventArgs e)
    { if (sender is Button { Tag: SavedPhrase phrase }) { Input.Text = phrase.Text; _ = SpeakAsync(phrase.Text); } }

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
        VoiceChip.ToolTip = voice.DisplayName;
        _settings.Save();
        VoiceFlyout.IsOpen = false;
        if (IsLoaded) CheckReadiness();
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
        var title = text.Length > 32 ? text[..32] + "…" : text;
        var editor = new PhraseEditorWindow(title, text) { Owner = this };
        if (editor.ShowDialog() != true) return;
        _phrases.Add(editor.PhraseTitle, editor.PhraseBody);
        SetActiveTab(true);
        _settings.Current.PhrasePanelExpanded = true;
        UpdatePhrasePanel();
    }
    void EditPhrase(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SavedPhrase phrase }) return;
        var editor = new PhraseEditorWindow(phrase.Name, phrase.Text) { Owner = this };
        if (editor.ShowDialog() == true) _phrases.Update(phrase, editor.PhraseTitle, editor.PhraseBody);
    }
    void DeletePhrase(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SavedPhrase phrase }) return;
        if (MessageBox.Show(this, $"Delete the saved phrase \"{phrase.Name}\"?", "Delete phrase",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) _phrases.Delete(phrase);
    }
    void RefreshEmptyStates()
    {
        SavedEmpty.Visibility = _phrases.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecentEmpty.Visibility = _history.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    void ShowSaved(object sender, RoutedEventArgs e) => SetActiveTab(true);
    void ShowRecent(object sender, RoutedEventArgs e) => SetActiveTab(false);
    void SetActiveTab(bool saved)
    {
        SavedPanel.Visibility = saved ? Visibility.Visible : Visibility.Collapsed;
        RecentPanel.Visibility = saved ? Visibility.Collapsed : Visibility.Visible;
        SavedTab.Background = (Brush)FindResource(saved ? "Brush.Accent" : "Brush.SurfaceRaised");
        RecentTab.Background = (Brush)FindResource(saved ? "Brush.SurfaceRaised" : "Brush.Accent");
        _settings.Current.ShowSavedPhrases = saved;
        _settings.Save();
    }
    void TogglePhrasePanel(object sender, RoutedEventArgs e)
    {
        _settings.Current.PhrasePanelExpanded = !_settings.Current.PhrasePanelExpanded;
        _settings.Save();
        UpdatePhrasePanel();
    }
    void UpdatePhrasePanel()
    {
        PhraseContent.Visibility = _settings.Current.PhrasePanelExpanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandButton.Content = _settings.Current.PhrasePanelExpanded ? "Collapse ▴" : "Expand ▾";
    }

    void OpenVoiceManager(object sender, RoutedEventArgs e)
    {
        VoiceFlyout.IsOpen = false;
        new VoiceManagerWindow(_voices) { Owner = this }.ShowDialog();
        _settings.Load();
        Voice.SelectedItem = Services.VoiceManager.Voices.FirstOrDefault(v => v.Id == _settings.Current.VoiceId);
        CheckReadiness();
    }
    void OpenSetup(object sender, RoutedEventArgs e)
    {
        new SetupWindow(_voices, _settings, _hotkey, _history, _speech) { Owner = this }.ShowDialog();
        _settings.Load();
        Voice.SelectedItem = Services.VoiceManager.Voices.FirstOrDefault(v => v.Id == _settings.Current.VoiceId);
        CheckReadiness();
    }
    void OpenSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings, _voices, _hotkey, _history) { Owner = this };
        dialog.ShowDialog();
        ApplyComposerTypography();
        Voice.SelectedItem = Services.VoiceManager.Voices.FirstOrDefault(v => v.Id == _settings.Current.VoiceId);
        Speed.Value = _settings.Current.Speed;
        Output.SelectedIndex = Math.Clamp(_settings.Current.OutputDevice + 1, 0, Output.Items.Count - 1);
        CheckReadiness();
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
    void ShowOverlay() { _overlay ??= new OverlayWindow(this, _speech, _settings, _history, _phrases); _overlay.ShowAndFocus(); }
    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_exitRequested) { e.Cancel = true; Hide(); }
        _settings.Current.OutputDevice = Output.SelectedIndex - 1;
        _settings.Save();
    }
    protected override void OnClosed(EventArgs e) { _speech.Stop(); _tray?.Dispose(); _hotkey.Dispose(); _audio.Dispose(); base.OnClosed(e); }
}
