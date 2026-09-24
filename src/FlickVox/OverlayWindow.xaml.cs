using System.Runtime.InteropServices;
using System.Windows.Media;
using FlickVox.Models;
using FlickVox.Services;
using Screen = System.Windows.Forms.Screen;
using Brush = System.Windows.Media.Brush;

namespace FlickVox;

public partial class OverlayWindow : Window
{
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();

    readonly PiperSpeechService _speech;
    readonly SettingsService _settings;
    readonly List<string> _history = new();
    int _cursor = -1;
    bool _positionReady;

    public OverlayWindow(MainWindow main, PiperSpeechService speech, SettingsService settings)
    {
        InitializeComponent();
        _speech = speech;
        _settings = settings;
        var saved = settings.Current;
        var legacyDefaults = saved.OverlayWidth == 450 && saved.OverlayHeight == 112;
        Width = legacyDefaults ? 640 : Math.Clamp(saved.OverlayWidth, 420, 800);
        Height = legacyDefaults ? 80 : Math.Clamp(saved.OverlayHeight, 72, 168);
        Topmost = saved.AlwaysOnTop;
        LocationChanged += (_, _) => { if (_positionReady && IsVisible) SavePosition(); };
        Closed += (_, _) => SavePosition();
    }

    public void ShowAndFocus()
    {
        var foreground = GetForegroundWindow();
        var screen = Screen.FromHandle(foreground);
        Topmost = _settings.Current.AlwaysOnTop;
        Width = Math.Clamp(_settings.Current.OverlayWidth is 450 ? 640 : _settings.Current.OverlayWidth, 420, 800);
        if (!IsVisible)
        {
            PlaceOnVisibleMonitor(screen);
            Show();
        }
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
        if (StateText.Text is "Ready" or "Typing") SetState(string.IsNullOrEmpty(Input.Text) ? "Ready" : "Typing");
    }

    async void KeyDownHandler(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            var text = Input.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            _history.Remove(text);
            _history.Insert(0, text);
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
                if (hideWhenDone)
                {
                    Input.Clear();
                    SavePosition();
                    Hide();
                }
            }
            catch (OperationCanceledException) { SetState("Ready"); }
            catch { SetState("Error"); }
        }
        else if (e.Key == Key.Escape)
        {
            _speech.Stop();
            SavePosition();
            Hide();
            SetState("Ready");
        }
        else if (e.Key is Key.Up or Key.Down && _history.Count > 0)
        {
            _cursor = Math.Clamp(_cursor + (e.Key == Key.Up ? 1 : -1), 0, _history.Count - 1);
            Input.Text = _history[_cursor];
            Input.CaretIndex = Input.Text.Length;
            e.Handled = true;
        }
    }

    void SetState(string state)
    {
        StateText.Text = state;
        StateDot.Fill = (Brush)FindResource(state switch
        {
            "Ready" or "Speaking" => "Brush.Signal",
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
        _settings.Current.OverlayHeight = Height;
        _settings.Save();
    }
}
