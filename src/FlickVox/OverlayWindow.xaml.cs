using System.Runtime.InteropServices;
using System.Windows.Media;
using FlickVox.Models;
using FlickVox.Services;
using NAudio.Wave;
using Screen = System.Windows.Forms.Screen;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace FlickVox;

public partial class OverlayWindow : Window
{
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();

    readonly PiperSpeechService _speech;
    readonly SettingsService _settings;
    readonly HistoryService _history;
    readonly PhraseService _phrases;
    readonly VoiceManager _voices = new();
    readonly MainWindow _main;
    int _cursor = -1;
    bool _positionReady;
    bool _expanded;
    bool _busy;
    double _quickHeight;

    public OverlayWindow(MainWindow main, PiperSpeechService speech, SettingsService settings, HistoryService history, PhraseService phrases)
    {
        InitializeComponent();
        _main = main;
        _speech = speech;
        _settings = settings;
        _history = history;
        _phrases = phrases;
        var saved = settings.Current;
        var legacyDefaults = saved.OverlayWidth == 450 && saved.OverlayHeight == 112;
        Width = legacyDefaults ? 520 : Math.Clamp(saved.OverlayWidth, 420, 800);
        _quickHeight = legacyDefaults ? 72 : Math.Clamp(saved.OverlayHeight, 72, 168);
        Height = _quickHeight;
        Topmost = saved.AlwaysOnTop;
        Voice.ItemsSource = VoiceManager.Voices;
        Voice.SelectedItem = VoiceManager.Voices.FirstOrDefault(v => v.Id == saved.VoiceId);
        Output.Items.Add("Default Windows output");
        for (var i = 0; i < WaveOut.DeviceCount; i++) Output.Items.Add(WaveOut.GetCapabilities(i).ProductName);
        Output.SelectedIndex = Math.Clamp(saved.OutputDevice + 1, 0, Output.Items.Count - 1);
        Phrases.ItemsSource = phrases.Items;
        History.ItemsSource = history.Items;
        phrases.Items.CollectionChanged += (_, _) => UpdateEmptyStates();
        history.Items.CollectionChanged += (_, _) => UpdateEmptyStates();
        UpdateEmptyStates();
        LocationChanged += (_, _) => { if (_positionReady && IsVisible) SavePosition(); };
        Closed += (_, _) => SavePosition();
    }

    public void ShowAndFocus()
    {
        var foreground = GetForegroundWindow();
        var screen = Screen.FromHandle(foreground);
        Topmost = _settings.Current.AlwaysOnTop;
        if (_expanded) SetExpanded(false);
        Width = Math.Clamp(_settings.Current.OverlayWidth is 450 ? 520 : _settings.Current.OverlayWidth, 420, 800);
        _quickHeight = Math.Clamp(_settings.Current.OverlayHeight is 112 ? 72 : _settings.Current.OverlayHeight, 72, 168);
        Height = _quickHeight;
        Voice.SelectedItem = VoiceManager.Voices.FirstOrDefault(v => v.Id == _settings.Current.VoiceId);
        Output.SelectedIndex = Math.Clamp(_settings.Current.OutputDevice + 1, 0, Output.Items.Count - 1);
        if (!IsVisible)
        {
            PlaceOnVisibleMonitor(screen);
            Show();
        }
        SetState(string.IsNullOrWhiteSpace(Input.Text) ? "Ready" : "Typing");
        Activate();
        Input.Focus();
    }

    void PlaceOnVisibleMonitor(Screen foreground)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var scaleX = dpi.DpiScaleX == 0 ? 1 : dpi.DpiScaleX;
        var scaleY = dpi.DpiScaleY == 0 ? 1 : dpi.DpiScaleY;
        var saved = _settings.Current;
        var savedScreen = double.IsFinite(saved.OverlayLeft) && double.IsFinite(saved.OverlayTop)
            ? Screen.AllScreens.FirstOrDefault(s =>
            {
                var a = s.WorkingArea;
                var x = saved.OverlayLeft * scaleX;
                var y = saved.OverlayTop * scaleY;
                return x >= a.Left && x < a.Right && y >= a.Top && y < a.Bottom;
            })
            : null;
        var area = foreground.WorkingArea;
        _positionReady = false;
        if (savedScreen is not null)
        {
            var visible = savedScreen.WorkingArea;
            Left = Math.Clamp(saved.OverlayLeft * scaleX, visible.Left,
                Math.Max(visible.Left, visible.Right - Width * scaleX)) / scaleX;
            Top = Math.Clamp(saved.OverlayTop * scaleY, visible.Top,
                Math.Max(visible.Top, visible.Bottom - Height * scaleY)) / scaleY;
        }
        else
        {
            Left = (area.Left + (area.Width - Width * scaleX) / 2) / scaleX;
            Top = (area.Bottom - Height * scaleY - area.Height * .12) / scaleY;
        }
        _positionReady = true;
    }

    void InputChanged(object sender, TextChangedEventArgs e)
    {
        if (Placeholder is null) return;
        Placeholder.Visibility = string.IsNullOrEmpty(Input.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (StateText.Text is "Ready" or "Typing" or "Setup") SetState(string.IsNullOrEmpty(Input.Text) ? "Ready" : "Typing");
    }

    async void KeyDownHandler(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            await SubmitAsync(Input.Text);
        }
        else if (e.Key == Key.Escape)
        {
            _speech.Stop();
            SavePosition();
            Hide();
            SetState("Ready");
        }
        else if (e.Key is Key.Up or Key.Down && _history.Items.Count > 0 && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _cursor = Math.Clamp(_cursor + (e.Key == Key.Up ? 1 : -1), 0, _history.Items.Count - 1);
            Input.Text = _history.Items[_cursor];
            Input.CaretIndex = Input.Text.Length;
            e.Handled = true;
        }
    }

    async Task SubmitAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _history.Add(text);
        _cursor = -1;
        var hideWhenDone = _settings.Current.HideOverlayAfterSpeaking;
        if (!hideWhenDone) Input.Clear();
        SetState("Preparing");
        try
        {
            await _speech.SpeakAsync(text, s => Dispatcher.Invoke(() => SetState(s switch
            {
                "Generating" => "Preparing",
                "Speaking" => "Speaking",
                _ => "Ready"
            })));
            SetState("Ready");
            if (hideWhenDone) { Input.Clear(); SavePosition(); Hide(); }
        }
        catch (OperationCanceledException) { SetState("Ready"); }
        catch { SetState("Error"); }
    }

    void SetState(string state)
    {
        _busy = state is "Preparing" or "Speaking";
        SpeakButton.Content = _busy ? "Stop" : "Speak";
        var ready = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlickVox", "runtime", "piper", "piper.exe")) && _voices.IsInstalled(_settings.Current.VoiceId);
        StateText.Text = state == "Ready" && !ready ? "Setup" : state;
        StateDot.Fill = (Brush)FindResource(state switch
        {
            "Ready" when ready => "Brush.Signal",
            "Speaking" => "Brush.Signal",
            "Error" => "Brush.Danger",
            _ => "Brush.AccentText"
        });
        Shell.BorderBrush = (Brush)FindResource(state switch
        {
            "Speaking" => "Brush.Signal",
            "Error" => "Brush.Danger",
            _ => "Brush.StrokeControl"
        });
    }

    void Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        DragMove();
        SavePosition();
        e.Handled = true;
    }

    void SavePosition()
    {
        if (!_positionReady) return;
        _settings.Current.OverlayLeft = Left;
        _settings.Current.OverlayTop = Top;
        _settings.Current.OverlayWidth = Width;
        _settings.Current.OverlayHeight = _quickHeight;
        _settings.Save();
    }

    void ToggleExpanded(object sender, RoutedEventArgs e) => SetExpanded(!_expanded);
    void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        ExpandedContent.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandButton.Content = expanded ? "▴" : "▾";
        ExpandButton.ToolTip = expanded ? "Collapse overlay" : "Expand overlay";
        ExpandButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, expanded ? "Collapse overlay" : "Expand overlay");
        InputRow.Height = new GridLength(expanded ? 105 : 55);
        Height = expanded ? 420 : _quickHeight;
        if (IsVisible)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var area = Screen.FromHandle(GetForegroundWindow()).WorkingArea;
            Top = Math.Clamp(Top, area.Top / dpi.DpiScaleY, Math.Max(area.Top / dpi.DpiScaleY, (area.Bottom - Height * dpi.DpiScaleY) / dpi.DpiScaleY));
        }
        _settings.Current.OverlayExpanded = expanded;
        _settings.Save();
        Input.Focus();
    }
    void VoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Voice.SelectedItem is not VoiceDefinition voice) return;
        _settings.Current.VoiceId = voice.Id;
        _settings.Save();
    }
    void OutputChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Output.SelectedIndex < 0) return;
        _settings.Current.OutputDevice = Output.SelectedIndex - 1;
        _settings.Save();
    }
    void SpeakOrStop(object sender, RoutedEventArgs e)
    {
        if (_busy) { _speech.Stop(); SetState("Ready"); return; }
        _ = SubmitAsync(Input.Text);
    }
    void ShowSaved(object sender, RoutedEventArgs e) { SavedPanel.Visibility = Visibility.Visible; RecentPanel.Visibility = Visibility.Collapsed; }
    void ShowRecent(object sender, RoutedEventArgs e) { SavedPanel.Visibility = Visibility.Collapsed; RecentPanel.Visibility = Visibility.Visible; }
    void UpdateEmptyStates()
    {
        SavedEmpty.Visibility = _phrases.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecentEmpty.Visibility = _history.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    void PlayPhrase(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SavedPhrase phrase }) return;
        Input.Text = phrase.Text;
        _ = SpeakFromRow(phrase.Text);
    }
    void PlayHistory(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string text }) return;
        Input.Text = text;
        _ = SpeakFromRow(text);
    }
    async Task SpeakFromRow(string text)
    {
        _history.Add(text);
        SetState("Preparing");
        try
        {
            await _speech.SpeakAsync(text, s => Dispatcher.Invoke(() => SetState(s switch { "Generating" => "Preparing", "Speaking" => "Speaking", _ => "Ready" })));
            SetState("Ready");
            if (_settings.Current.HideOverlayAfterSpeaking) { Input.Clear(); SavePosition(); Hide(); }
        }
        catch (OperationCanceledException) { SetState("Ready"); }
        catch { SetState("Error"); }
    }
    void EditPhrase(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SavedPhrase phrase }) return;
        var editor = new PhraseEditorWindow(phrase.Name, phrase.Text) { Owner = _main };
        if (editor.ShowDialog() == true) _phrases.Update(phrase, editor.PhraseTitle, editor.PhraseBody);
    }
    void SavePhrase(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Input.Text)) return;
        var text = Input.Text.Trim();
        var name = text.Length > 32 ? text[..32] + "…" : text;
        var editor = new PhraseEditorWindow(name, text) { Owner = _main };
        if (editor.ShowDialog() == true) _phrases.Add(editor.PhraseTitle, editor.PhraseBody);
    }
    void DeletePhrase(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SavedPhrase phrase }) return;
        if (MessageBox.Show(_main, $"Delete the saved phrase \"{phrase.Name}\"?", "Delete phrase",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) _phrases.Delete(phrase);
    }
}
