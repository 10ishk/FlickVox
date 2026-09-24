using System.Runtime.InteropServices;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using FlickVox.Models;
using FlickVox.Services;
using NAudio.Wave;
using Screen = System.Windows.Forms.Screen;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using FontFamily = System.Windows.Media.FontFamily;

namespace FlickVox;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();

    readonly SettingsService _settings = new();
    readonly VoiceManager _voices = new();
    readonly AudioPlaybackService _audio = new();
    readonly PiperSpeechService _speech;
    readonly HotkeyService _hotkey = new();
    readonly HistoryService _history;
    readonly PhraseService _phrases = new();
    NotifyIcon? _tray;
    bool _expanded;
    bool _busy;
    bool _exitRequested;
    bool _positionReady;
    bool _uiReady;
    int _speechVersion;
    int _historyCursor = -1;
    UiState _state = UiState.Idle;

    enum UiState { Idle, Typing, Preparing, Speaking, Error }
    sealed record VoiceOption(VoiceDefinition Voice, string Label);
    sealed record PhraseChip(SavedPhrase Phrase, string Shortcut, string Name, string Hint);

    public MainWindow()
    {
        InitializeComponent();
        MigrateWindowSettings();
        _speech = new PiperSpeechService(_voices, _audio, _settings);
        _speech.LastMessageChanged += () => Dispatcher.Invoke(() => RepeatButton.IsEnabled = true);
        _history = new HistoryService(_settings);
        Voice.ItemsSource = VoiceManager.Voices.Select(v => new VoiceOption(v, VoiceLabel(v))).ToList();
        SelectCurrentVoice();
        Speed.Value = _settings.Current.Speed;
        ApplyComposerTypography();
        Output.Items.Add("Default Windows output");
        for (var i = 0; i < WaveOut.DeviceCount; i++) Output.Items.Add(WaveOut.GetCapabilities(i).ProductName);
        Output.SelectedIndex = Math.Clamp(_settings.Current.OutputDevice + 1, 0, Output.Items.Count - 1);
        History.ItemsSource = _history.Items;
        _history.Items.CollectionChanged += (_, _) => RecentEmpty.Visibility = _history.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecentEmpty.Visibility = _history.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _phrases.Items.CollectionChanged += (_, _) => RefreshPhrases();
        RefreshPhrases();
        Topmost = _settings.Current.AlwaysOnTop;
        UpdatePin();
        SetMode(_settings.Current.UnifiedExpanded, persist: false);
        UpdatePlaceholder();
        _uiReady = true;
        Loaded += OnLoaded;
        LocationChanged += (_, _) => { if (_positionReady && IsVisible) SavePosition(); };
        Closing += OnClosing;
    }

    static string VoiceLabel(VoiceDefinition voice)
    {
        var parts = voice.Id.Split('-');
        var quality = parts[^1] switch { "low" => "Fast", "high" => "Best quality", _ => "Balanced" };
        return $"{voice.DisplayName.Split(' ')[0]} · {quality}";
    }

    void MigrateWindowSettings()
    {
        var settings = _settings.Current;
        if (settings.UnifiedWindowMigrated) return;
        settings.UnifiedLeft = settings.OverlayLeft;
        settings.UnifiedTop = settings.OverlayTop;
        if (settings.OverlayWidth is not (450 or 520 or 640))
            settings.UnifiedQuickWidth = Math.Clamp(settings.OverlayWidth, 320, 800);
        if (settings.OverlayHeight is not (72 or 80 or 112))
            settings.UnifiedQuickHeight = Math.Clamp(settings.OverlayHeight, 48, 168);
        settings.UnifiedWindowMigrated = true;
        _settings.Save();
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlaceOnVisibleMonitor(Screen.FromHandle(GetForegroundWindow()));
        try { _hotkey.Register(this, _settings.Current.Hotkey); _hotkey.Pressed += (_, _) => ShowQuick(); }
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
        menu.Items.Add("Open FlickVox", null, (_, _) => ShowWorkspace());
        menu.Items.Add("Open quick pill", null, (_, _) => ShowQuick());
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
        _tray.DoubleClick += (_, _) => ShowWorkspace();
    }

    void ShowQuick()
    {
        var foreground = Screen.FromHandle(GetForegroundWindow());
        SetMode(false);
        if (!IsVisible) { PlaceOnVisibleMonitor(foreground); Show(); }
        Activate();
        Input.Focus();
    }

    void ShowWorkspace()
    {
        var foreground = Screen.FromHandle(GetForegroundWindow());
        SetMode(true);
        if (!IsVisible) { PlaceOnVisibleMonitor(foreground); Show(); }
        Activate();
        Input.Focus();
    }

    void SetMode(bool expanded, bool persist = true)
    {
        _expanded = expanded;
        _positionReady = false;
        Header.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        SavedArea.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        Footer.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandQuickButton.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        Notice.Visibility = expanded && NoticeText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Shell.CornerRadius = new CornerRadius(expanded ? 14 : 24);
        ComposerCard.CornerRadius = new CornerRadius(expanded ? 10 : 24);
        ComposerCard.Margin = expanded ? new Thickness(12, 5, 12, 7) : new Thickness();
        var quickHeight = Math.Max(Math.Clamp(_settings.Current.UnifiedQuickHeight, 48, 168),
            Math.Min(64, Input.FontSize + 18));
        ComposerCard.Height = expanded ? 84 : quickHeight - 2;
        ComposerCard.BorderThickness = expanded ? new Thickness(1) : new Thickness(0);
        MinWidth = expanded ? 380 : 320;
        Width = expanded ? Math.Clamp(_settings.Current.UnifiedExpandedWidth, 380, 800)
                         : Math.Clamp(_settings.Current.UnifiedQuickWidth, 320, 800);
        Height = expanded ? ExpandedContentHeight() : quickHeight;
        if (IsVisible) ClampToWorkingArea();
        _positionReady = true;
        if (persist)
        {
            _settings.Current.UnifiedExpanded = expanded;
            _settings.Save();
        }
    }

    double ExpandedContentHeight()
    {
        var contentHeight = 240 + Math.Min(_phrases.Items.Count, 2) * 30
            + (Notice.Visibility == Visibility.Visible ? 35 : 0);
        return Math.Min(Math.Clamp(_settings.Current.UnifiedExpandedHeight, 260, 600), contentHeight);
    }

    void Expand(object sender, RoutedEventArgs e) => SetMode(true);
    void Collapse(object sender, RoutedEventArgs e) => SetMode(false);
    void CloseToTray(object sender, RoutedEventArgs e) { SavePosition(); Hide(); }
    void TogglePin(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _settings.Current.AlwaysOnTop = Topmost;
        _settings.Save();
        UpdatePin();
    }
    void UpdatePin()
    {
        PinButton.Background = (Brush)FindResource(Topmost ? "Brush.AccentSubtle" : "Brush.Canvas");
        PinButton.ToolTip = Topmost ? "Unpin FlickVox" : "Keep FlickVox on top";
    }
    void Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        DragMove();
        SavePosition();
        e.Handled = true;
    }

    void PlaceOnVisibleMonitor(Screen foreground)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var sx = dpi.DpiScaleX == 0 ? 1 : dpi.DpiScaleX;
        var sy = dpi.DpiScaleY == 0 ? 1 : dpi.DpiScaleY;
        var saved = _settings.Current;
        var savedScreen = double.IsFinite(saved.UnifiedLeft) && double.IsFinite(saved.UnifiedTop)
            ? Screen.AllScreens.FirstOrDefault(s =>
            {
                var area = s.WorkingArea;
                return saved.UnifiedLeft * sx >= area.Left && saved.UnifiedLeft * sx < area.Right &&
                       saved.UnifiedTop * sy >= area.Top && saved.UnifiedTop * sy < area.Bottom;
            })
            : null;
        var area = (savedScreen ?? foreground).WorkingArea;
        _positionReady = false;
        Left = savedScreen is not null
            ? Math.Clamp(saved.UnifiedLeft * sx, area.Left, Math.Max(area.Left, area.Right - Width * sx)) / sx
            : (area.Left + (area.Width - Width * sx) / 2) / sx;
        Top = savedScreen is not null
            ? Math.Clamp(saved.UnifiedTop * sy, area.Top, Math.Max(area.Top, area.Bottom - Height * sy)) / sy
            : (_expanded ? area.Top + (area.Height - Height * sy) / 2 : area.Bottom - Height * sy - area.Height * .12) / sy;
        _positionReady = true;
    }
    void ClampToWorkingArea()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var area = Screen.FromHandle(handle).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var sx = dpi.DpiScaleX == 0 ? 1 : dpi.DpiScaleX;
        var sy = dpi.DpiScaleY == 0 ? 1 : dpi.DpiScaleY;
        Left = Math.Clamp(Left * sx, area.Left, Math.Max(area.Left, area.Right - Width * sx)) / sx;
        Top = Math.Clamp(Top * sy, area.Top, Math.Max(area.Top, area.Bottom - Height * sy)) / sy;
    }
    void SavePosition()
    {
        if (!_positionReady || !double.IsFinite(Left) || !double.IsFinite(Top)) return;
        _settings.Current.UnifiedLeft = Left;
        _settings.Current.UnifiedTop = Top;
        _settings.Save();
    }

    bool DependenciesReady() =>
        File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlickVox", "runtime", "piper", "piper.exe")) && _voices.IsInstalled(_settings.Current.VoiceId);

    void CheckReadiness()
    {
        if (!DependenciesReady())
        {
            ShowNotice("Piper or the selected voice is missing.", true);
            return;
        }
        if (!_settings.Current.FirstSpeechConfirmed && !_settings.Current.FirstRunGuidanceDismissed)
        {
            NoticeText.Text = "Ready for a first audio test.";
            NoticeAction.Visibility = Visibility.Visible;
            if (_expanded) Notice.Visibility = Visibility.Visible;
        }
        else { NoticeText.Text = ""; Notice.Visibility = Visibility.Collapsed; }
        if (_expanded) Height = ExpandedContentHeight();
        SetUiState(string.IsNullOrEmpty(Input.Text) ? UiState.Idle : UiState.Typing);
    }

    void SetUiState(UiState state)
    {
        _state = state;
        _busy = state is UiState.Preparing or UiState.Speaking;
        StatusText.Text = state switch
        {
            UiState.Idle when !DependenciesReady() => "Setup",
            UiState.Idle => "Idle",
            UiState.Typing => "Typing",
            UiState.Preparing => "Preparing",
            UiState.Speaking => "Speaking",
            _ => "Error"
        };
        var dot = state switch
        {
            UiState.Speaking => "Brush.Signal",
            UiState.Error => "Brush.Danger",
            _ => "Brush.TextTertiary"
        };
        StatusDot.Fill = (Brush)FindResource(dot);
        ComposerCard.BorderBrush = (Brush)FindResource(state switch
        {
            UiState.Speaking => "Brush.SignalBorder",
            UiState.Error => "Brush.Danger",
            _ => "Brush.StrokeControl"
        });
        SendIcon.Data = (Geometry)FindResource(_busy ? "Icon.Stop" : "Icon.Send");
        SendButton.Background = (Brush)FindResource(state switch
        {
            UiState.Speaking => "Brush.Canvas",
            UiState.Preparing => "Brush.SurfaceHover",
            _ => "Brush.PrimaryAction"
        });
        SendButton.BorderBrush = (Brush)FindResource(state == UiState.Speaking ? "Brush.Signal" : "Brush.PrimaryAction");
        SendIcon.Fill = (Brush)FindResource(state switch
        {
            UiState.Speaking => "Brush.Signal",
            UiState.Preparing => "Brush.TextSecondary",
            _ => "Brush.OnPrimary"
        });
        SendButton.ToolTip = _busy ? "Stop speech · Esc" : "Speak · Enter";
        System.Windows.Automation.AutomationProperties.SetName(SendButton, _busy ? "Stop speech" : "Speak");
    }
    void ShowNotice(string message, bool setupAction)
    {
        NoticeText.Text = message;
        NoticeAction.Visibility = setupAction ? Visibility.Visible : Visibility.Collapsed;
        if (!_expanded) SetMode(true);
        Notice.Visibility = Visibility.Visible;
        Height = ExpandedContentHeight();
        SetUiState(UiState.Error);
    }
    void DismissNotice(object sender, RoutedEventArgs e)
    {
        Notice.Visibility = Visibility.Collapsed;
        NoticeText.Text = "";
        if (_expanded) Height = ExpandedContentHeight();
        if (DependenciesReady() && !_settings.Current.FirstSpeechConfirmed)
        {
            _settings.Current.FirstRunGuidanceDismissed = true;
            _settings.Save();
        }
    }

    void SendOrStop(object sender, RoutedEventArgs e)
    {
        if (_busy) StopSpeech();
        else _ = SpeakAsync(Input.Text);
    }
    void StopSpeech()
    {
        _speechVersion++;
        _speech.Stop();
        SetUiState(string.IsNullOrEmpty(Input.Text) ? UiState.Idle : UiState.Typing);
    }
    async Task SpeakAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var version = ++_speechVersion;
        Notice.Visibility = Visibility.Collapsed;
        if (_expanded) Height = ExpandedContentHeight();
        _history.Add(text);
        _historyCursor = -1;
        SetUiState(UiState.Preparing);
        try
        {
            await _speech.SpeakAsync(text, s => Dispatcher.Invoke(() =>
            {
                if (version != _speechVersion) return;
                SetUiState(s switch { "Speaking" => UiState.Speaking, "Ready" => string.IsNullOrEmpty(Input.Text) ? UiState.Idle : UiState.Typing, _ => UiState.Preparing });
            }));
            if (version == _speechVersion && !_expanded && _settings.Current.HideOverlayAfterSpeaking)
            {
                Input.Clear();
                SavePosition();
                Hide();
            }
        }
        catch (OperationCanceledException) { if (version == _speechVersion) SetUiState(UiState.Typing); }
        catch (Exception ex) { if (version == _speechVersion) ShowNotice(ex.Message, !DependenciesReady()); }
        finally
        {
            if (version == _speechVersion && _state is UiState.Preparing or UiState.Speaking)
                SetUiState(string.IsNullOrEmpty(Input.Text) ? UiState.Idle : UiState.Typing);
        }
    }
    async void Repeat(object sender, RoutedEventArgs e) => await RepeatAsync();
    async Task RepeatAsync()
    {
        if (_speech.LastText is null) return;
        var version = ++_speechVersion;
        SetUiState(UiState.Preparing);
        try
        {
            await _speech.RepeatAsync(s => Dispatcher.Invoke(() =>
            {
                if (version == _speechVersion)
                    SetUiState(s == "Speaking" ? UiState.Speaking : s == "Ready" ? UiState.Idle : UiState.Preparing);
            }));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (version == _speechVersion) ShowNotice(ex.Message, !DependenciesReady()); }
        finally { if (version == _speechVersion && _state is UiState.Preparing or UiState.Speaking) SetUiState(UiState.Idle); }
    }
    async void Preview(object sender, RoutedEventArgs e)
    {
        VoiceFlyout.IsOpen = false;
        var version = ++_speechVersion;
        SetUiState(UiState.Preparing);
        try
        {
            await _speech.PreviewAsync("Hello, this is the selected FlickVox voice.", s => Dispatcher.Invoke(() =>
            {
                if (version == _speechVersion)
                    SetUiState(s == "Speaking" ? UiState.Speaking : s == "Ready" ? UiState.Idle : UiState.Preparing);
            }));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (version == _speechVersion) ShowNotice(ex.Message, !DependenciesReady()); }
        finally { if (version == _speechVersion && _state is UiState.Preparing or UiState.Speaking) SetUiState(UiState.Idle); }
    }

    void InputChanged(object sender, TextChangedEventArgs e)
    {
        if (InputPlaceholder is null) return;
        UpdatePlaceholder();
        if (!_busy && _state != UiState.Error)
            SetUiState(string.IsNullOrEmpty(Input.Text) ? UiState.Idle : UiState.Typing);
    }
    void UpdatePlaceholder()
    {
        InputPlaceholder.Visibility = string.IsNullOrEmpty(Input.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (SaveButton is not null) SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(Input.Text);
    }
    void InputKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; _ = SpeakAsync(Input.Text); }
        else if (e.Key is Key.Up or Key.Down && _history.Items.Count > 0 && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _historyCursor = Math.Clamp(_historyCursor + (e.Key == Key.Up ? 1 : -1), 0, _history.Items.Count - 1);
            Input.Text = _history.Items[_historyCursor];
            Input.CaretIndex = Input.Text.Length;
            e.Handled = true;
        }
    }
    void WindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            var digit = key is >= Key.D1 and <= Key.D9 ? (int)key - (int)Key.D1 + 1
                : key is >= Key.NumPad1 and <= Key.NumPad9 ? (int)key - (int)Key.NumPad1 + 1 : 0;
            if (digit > 0 && digit <= _phrases.Items.Count)
            {
                e.Handled = true;
                PlayPhraseText(_phrases.Items[digit - 1]);
                return;
            }
        }
        if (key == Key.Escape)
        {
            if (VoiceFlyout.IsOpen || OutputFlyout.IsOpen || SpeedFlyout.IsOpen || HistoryPopup.IsOpen)
                VoiceFlyout.IsOpen = OutputFlyout.IsOpen = SpeedFlyout.IsOpen = HistoryPopup.IsOpen = false;
            else if (_busy) StopSpeech();
            else { SavePosition(); Hide(); }
            e.Handled = true;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && key == Key.S) { SavePhrase(sender, e); e.Handled = true; }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && key == Key.R) { _ = RepeatAsync(); e.Handled = true; }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && key == Key.L) { Input.Clear(); e.Handled = true; }
    }

    void RefreshPhrases()
    {
        PhraseChips.ItemsSource = _phrases.Items.Select((p, i) =>
            new PhraseChip(p, i < 9 ? (i + 1).ToString() : "", p.Name,
                i < 9 ? $"{p.Name}\n{p.Text}\nAlt+{i + 1} · Right-click to edit or delete" : $"{p.Name}\n{p.Text}\nRight-click to edit or delete")).ToList();
        if (_expanded) Height = ExpandedContentHeight();
    }
    void PlayPhraseText(SavedPhrase phrase)
    {
        Input.Text = phrase.Text;
        _ = SpeakAsync(phrase.Text);
    }
    void PlayPhrase(object sender, RoutedEventArgs e)
    {
        if (PhraseFromSender(sender) is { } phrase) PlayPhraseText(phrase);
    }
    static SavedPhrase? PhraseFromSender(object sender)
    {
        if (sender is FrameworkElement { Tag: SavedPhrase direct }) return direct;
        if (sender is System.Windows.Controls.MenuItem { Parent: System.Windows.Controls.ContextMenu menu } &&
            menu.PlacementTarget is FrameworkElement { Tag: SavedPhrase fromMenu }) return fromMenu;
        return null;
    }
    void SavePhrase(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Input.Text)) return;
        var text = Input.Text.Trim();
        var title = text.Length > 32 ? text[..32] + "…" : text;
        var editor = new PhraseEditorWindow(title, text) { Owner = this };
        if (editor.ShowDialog() == true) _phrases.Add(editor.PhraseTitle, editor.PhraseBody);
    }
    void EditPhrase(object sender, RoutedEventArgs e)
    {
        if (PhraseFromSender(sender) is not { } phrase) return;
        var editor = new PhraseEditorWindow(phrase.Name, phrase.Text) { Owner = this };
        if (editor.ShowDialog() == true) _phrases.Update(phrase, editor.PhraseTitle, editor.PhraseBody);
    }
    void DeletePhrase(object sender, RoutedEventArgs e)
    {
        if (PhraseFromSender(sender) is not { } phrase) return;
        if (MessageBox.Show(this, $"Delete the saved phrase \"{phrase.Name}\"?", "Delete phrase",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            _phrases.Delete(phrase);
    }
    void ToggleHistory(object sender, RoutedEventArgs e) => HistoryPopup.IsOpen = !HistoryPopup.IsOpen;
    void PlayHistory(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string text }) return;
        HistoryPopup.IsOpen = false;
        Input.Text = text;
        _ = SpeakAsync(text);
    }

    void SelectCurrentVoice()
    {
        Voice.SelectedItem = (Voice.ItemsSource as IEnumerable<VoiceOption>)?.FirstOrDefault(v => v.Voice.Id == _settings.Current.VoiceId);
    }
    void VoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Voice.SelectedItem is not VoiceOption option || VoiceValue is null) return;
        VoiceValue.Text = option.Label;
        VoiceChip.ToolTip = option.Voice.DisplayName;
        if (!_uiReady) return;
        _settings.Current.VoiceId = option.Voice.Id;
        _settings.Save();
        VoiceFlyout.IsOpen = false;
        if (IsLoaded) CheckReadiness();
    }
    void OutputChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutputValue is null || Output.SelectedIndex < 0) return;
        OutputValue.Text = Output.SelectedIndex == 0 ? "Default" : Output.SelectedItem?.ToString() ?? "Default";
        OutputChip.ToolTip = Output.SelectedItem?.ToString();
        if (!_uiReady) return;
        _settings.Current.OutputDevice = Output.SelectedIndex - 1;
        _settings.Save();
        OutputFlyout.IsOpen = false;
    }
    void SpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpeedValue is null) return;
        SpeedValue.Text = $"{Speed.Value:F2}×";
        if (SpeedFlyoutValue is not null) SpeedFlyoutValue.Text = SpeedValue.Text;
        if (!_uiReady) return;
        _settings.Current.Speed = Speed.Value;
        _settings.Save();
    }
    void ToggleVoiceFlyout(object sender, RoutedEventArgs e) => VoiceFlyout.IsOpen = !VoiceFlyout.IsOpen;
    void ToggleOutputFlyout(object sender, RoutedEventArgs e) => OutputFlyout.IsOpen = !OutputFlyout.IsOpen;
    void ToggleSpeedFlyout(object sender, RoutedEventArgs e) => SpeedFlyout.IsOpen = !SpeedFlyout.IsOpen;
    void FlyoutOpened(object sender, EventArgs e)
    {
        if (sender is not Popup opened) return;
        foreach (var popup in new[] { VoiceFlyout, OutputFlyout, SpeedFlyout })
            if (!ReferenceEquals(popup, opened)) popup.IsOpen = false;
        HistoryPopup.IsOpen = false;
        if (ReferenceEquals(opened, VoiceFlyout)) Voice.Focus();
        else if (ReferenceEquals(opened, OutputFlyout)) Output.Focus();
        else Speed.Focus();
    }

    void OpenVoiceManager(object sender, RoutedEventArgs e)
    {
        VoiceFlyout.IsOpen = false;
        new VoiceManagerWindow(_voices) { Owner = this }.ShowDialog();
        _settings.Load();
        SelectCurrentVoice();
        CheckReadiness();
    }
    void OpenSetup(object sender, RoutedEventArgs e)
    {
        new SetupWindow(_voices, _settings, _hotkey, _history, _speech) { Owner = this }.ShowDialog();
        _settings.Load();
        SelectCurrentVoice();
        CheckReadiness();
    }
    void OpenSettings(object sender, RoutedEventArgs e)
    {
        new SettingsWindow(_settings, _voices, _hotkey, _history) { Owner = this }.ShowDialog();
        ApplyComposerTypography();
        SelectCurrentVoice();
        Speed.Value = _settings.Current.Speed;
        Output.SelectedIndex = Math.Clamp(_settings.Current.OutputDevice + 1, 0, Output.Items.Count - 1);
        Topmost = _settings.Current.AlwaysOnTop;
        UpdatePin();
        SetMode(_expanded, persist: false);
        CheckReadiness();
    }
    void ApplyComposerTypography()
    {
        Input.FontSize = Math.Clamp(_settings.Current.ComposerFontSize, 14, 40);
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
    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SavePosition();
        if (!_exitRequested) { e.Cancel = true; Hide(); }
    }
    protected override void OnClosed(EventArgs e)
    {
        _speech.Stop();
        _tray?.Dispose();
        _hotkey.Dispose();
        _audio.Dispose();
        base.OnClosed(e);
    }
}
