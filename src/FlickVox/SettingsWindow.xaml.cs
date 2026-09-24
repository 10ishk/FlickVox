using FlickVox.Infrastructure;
using FlickVox.Models;
using FlickVox.Services;
using NAudio.Wave;

namespace FlickVox;

public partial class SettingsWindow : Window
{
    readonly SettingsService _settings;
    readonly HotkeyService _hotkey;
    readonly HistoryService? _history;
    bool _ready;

    public SettingsWindow(SettingsService settings, VoiceManager voices, HotkeyService hotkey, HistoryService? history = null)
    {
        InitializeComponent();
        _settings = settings;
        _hotkey = hotkey;
        _history = history;
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
        OverlayWidth.Value = Math.Clamp(settings.Current.UnifiedQuickWidth, OverlayWidth.Minimum, OverlayWidth.Maximum);
        ComposerSize.Value = settings.Current.ComposerFontSize;
        Theme.SelectedIndex = settings.Current.Theme switch { "Light" => 1, "System" => 2, _ => 0 };
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
        var panels = new[] { GeneralPanel, AudioPanel, OverlayPanel, AppearancePanel, AccessibilityPanel, AboutPanel };
        for (var i = 0; i < panels.Length; i++) panels[i].Visibility = i == Sections.SelectedIndex ? Visibility.Visible : Visibility.Collapsed;
    }
    void UpdateValues()
    {
        SpeedValue.Text = $"{Speed.Value:F2}×";
        VolumeValue.Text = $"{Volume.Value:P0}";
        OverlayWidthValue.Text = $"{OverlayWidth.Value:F0} px";
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
    void OverlayWidthChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OverlayWidthValue is null) return;
        OverlayWidthValue.Text = $"{OverlayWidth.Value:F0} px";
        Save(s => s.UnifiedQuickWidth = OverlayWidth.Value);
    }
    void ComposerSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ComposerSizeValue is null) return;
        ComposerSizeValue.Text = $"{ComposerSize.Value:F0} px";
        Save(s => s.ComposerFontSize = ComposerSize.Value);
    }
    void ThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        var theme = Theme.SelectedIndex switch { 1 => "Light", 2 => "System", _ => "Dark" };
        Save(s => s.Theme = theme);
        ThemeManager.SetPreference(theme);
    }
    void ComposerFontChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        Save(s => s.ComposerFont = ComposerFont.SelectedIndex switch { 1 => "Atkinson Hyperlegible", 2 => "System", _ => "Inter" });
    }
    void ResetOverlayPosition(object sender, RoutedEventArgs e)
    {
        Save(s => { s.UnifiedLeft = double.NaN; s.UnifiedTop = double.NaN; });
    }
    void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
    void Close(object sender, RoutedEventArgs e) => Close();
}
