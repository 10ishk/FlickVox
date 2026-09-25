using FlickVox.Infrastructure;
using FlickVox.Models;
using FlickVox.Services;
using NAudio.Wave;
using System.Windows.Media;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Navigation;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;

namespace FlickVox;

public partial class SettingsWindow : Window
{
    readonly SettingsService _settings;
    readonly HotkeyService _hotkey;
    readonly HistoryService? _history;
    readonly Action? _openTools;
    bool _ready;

    public SettingsWindow(SettingsService settings, VoiceManager voices, HotkeyService hotkey, HistoryService? history = null, Action? openTools = null)
    {
        InitializeComponent();
        _settings = settings;
        _hotkey = hotkey;
        _history = history;
        _openTools = openTools;
        AboutVersion.Text = $"Version {typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? typeof(App).Assembly.GetName().Version?.ToString(3) ?? "unknown"}";
        OpenSpeechControlsButton.Visibility = openTools is null ? Visibility.Collapsed : Visibility.Visible;
        SourceInitialized += (_, _) => WindowBackdrop.ApplyDarkTitleBar(this);
        Voice.ItemsSource = VoiceManager.Voices;
        Voice.SelectedItem = VoiceManager.Voices.FirstOrDefault(v => v.Id == settings.Current.VoiceId);
        Output.Items.Add("Default Windows output");
        for (var i = 0; i < WaveOut.DeviceCount; i++) Output.Items.Add(WaveOut.GetCapabilities(i).ProductName);
        Output.SelectedIndex = Math.Clamp(settings.Current.OutputDevice + 1, 0, Output.Items.Count - 1);
        Speed.Value = settings.Current.Speed;
        Volume.Value = settings.Current.Volume;
        HideAfter.IsChecked = settings.Current.HideOverlayAfterSpeaking;
        PersistHistory.IsChecked = settings.Current.PersistHistory;
        Hotkey.Text = hotkey.Current;
        AlwaysOnTop.IsChecked = settings.Current.AlwaysOnTop;
        QuickPillWidth.Value = Math.Clamp(settings.Current.UnifiedQuickWidth, QuickPillWidth.Minimum, QuickPillWidth.Maximum);
        ComposerSize.Value = settings.Current.ComposerFontSize;
        RefreshThemeSwatches();
        ComposerFont.SelectedIndex = settings.Current.ComposerFont switch { "Atkinson Hyperlegible" => 1, "System" => 2, _ => 0 };
        Sections.SelectedIndex = 0;
        UpdateValues();
        _ready = true;
    }

    void Save(Action<AppSettings> change)
    {
        if (!_ready) return;
        change(_settings.Current);
        _settings.Save();
    }
    void SectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GeneralPanel is null) return;
        var panels = new[] { GeneralPanel, AudioPanel, CompactPanel, AppearancePanel, AccessibilityPanel, AboutPanel };
        for (var i = 0; i < panels.Length; i++) panels[i].Visibility = i == Sections.SelectedIndex ? Visibility.Visible : Visibility.Collapsed;
    }
    void UpdateValues()
    {
        SpeedValue.Text = $"{Speed.Value:F2}×";
        VolumeValue.Text = $"{Volume.Value:P0}";
        QuickPillWidthValue.Text = $"{QuickPillWidth.Value:F0} px";
        ComposerSizeValue.Text = $"{ComposerSize.Value:F0} px";
    }
    void VoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Voice.SelectedItem is VoiceDefinition voice) Save(s => s.VoiceId = voice.Id);
    }
    void OutputChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Output.SelectedIndex >= 0) Save(s => s.OutputDevice = Output.SelectedIndex - 1);
    }
    void SpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpeedValue is null) return;
        SpeedValue.Text = $"{Speed.Value:F2}×";
        Save(s => s.Speed = Speed.Value);
    }
    void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeValue is null) return;
        VolumeValue.Text = $"{Volume.Value:P0}";
        Save(s => s.Volume = (float)Volume.Value);
    }
    void HideAfterChanged(object sender, RoutedEventArgs e) => Save(s => s.HideOverlayAfterSpeaking = HideAfter.IsChecked == true);
    void PersistHistoryChanged(object sender, RoutedEventArgs e)
    {
        Save(s => s.PersistHistory = PersistHistory.IsChecked == true);
        _history?.SaveIfEnabled();
    }
    void HotkeyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && Hotkey.SelectedItem is ComboBoxItem item) ApplyHotkey(item.Content?.ToString() ?? "");
    }
    void HotkeyLostFocus(object sender, RoutedEventArgs e)
    {
        if (_ready) ApplyHotkey(Hotkey.Text);
    }
    void ApplyHotkey(string value)
    {
        if (_hotkey.TryChange(value))
        {
            Save(s => s.Hotkey = value);
            HotkeyError.Visibility = Visibility.Collapsed;
        }
        else
        {
            HotkeyError.Text = $"Cannot use {value}. It may be invalid or already registered. {_hotkey.Current} remains active.";
            HotkeyError.Visibility = Visibility.Visible;
            Hotkey.Text = _hotkey.Current;
        }
    }
    void AlwaysOnTopChanged(object sender, RoutedEventArgs e) => Save(s => s.AlwaysOnTop = AlwaysOnTop.IsChecked == true);
    void QuickPillWidthChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QuickPillWidthValue is null) return;
        QuickPillWidthValue.Text = $"{QuickPillWidth.Value:F0} px";
        Save(s => s.UnifiedQuickWidth = QuickPillWidth.Value);
    }
    void ComposerSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ComposerSizeValue is null) return;
        ComposerSizeValue.Text = $"{ComposerSize.Value:F0} px";
        Save(s => s.ComposerFontSize = ComposerSize.Value);
    }
    public sealed record ThemeSwatch(string Name, Brush Background, Brush Surface, Brush Accent,
        Brush PreviewText, Brush Outline, bool Selected, string Hint);

    void RefreshThemeSwatches()
    {
        var selected = ThemeManager.Normalize(_settings.Current.Theme);
        ThemeSwatches.ItemsSource = ThemeManager.Choices.Select(name =>
        {
            var colors = ThemeManager.Preview(name);
            var foreground = ThemeManager.Contrast(Colors.White, colors.Background) >= 4.5 ? Colors.White : Colors.Black;
            var isSelected = selected == name;
            return new ThemeSwatch(name, new SolidColorBrush(colors.Background), new SolidColorBrush(colors.Surface),
                new SolidColorBrush(colors.Accent), new SolidColorBrush(foreground),
                isSelected ? (Brush)FindResource("Brush.AccentText") : (Brush)FindResource("Brush.StrokeControl"),
                isSelected, $"{name} theme{(isSelected ? ", selected" : "")}");
        }).ToList();
    }

    void PickTheme(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not Button { Tag: string theme }) return;
        Save(s => s.Theme = theme);
        ThemeManager.SetPreference(theme);
        RefreshThemeSwatches();
    }
    void ComposerFontChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        Save(s => s.ComposerFont = ComposerFont.SelectedIndex switch { 1 => "Atkinson Hyperlegible", 2 => "System", _ => "Inter" });
    }
    void ResetWindowPosition(object sender, RoutedEventArgs e)
    {
        Save(s => { s.UnifiedLeft = double.NaN; s.UnifiedTop = double.NaN; });
    }
    void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
    void Close(object sender, RoutedEventArgs e) => Close();
    void OpenSpeechControls(object sender, RoutedEventArgs e)
    {
        Close();
        _openTools?.Invoke();
    }
    void OpenExternalLink(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, ex.Message, "Could not open link", MessageBoxButton.OK, MessageBoxImage.Warning); }
        e.Handled = true;
    }
}
