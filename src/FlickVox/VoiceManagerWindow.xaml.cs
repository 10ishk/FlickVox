using FlickVox.Infrastructure;
using FlickVox.Models;
using FlickVox.Services;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace FlickVox;

public partial class VoiceManagerWindow : Window
{
    readonly VoiceManager _voices;
    readonly SettingsService _settings = new();
    CancellationTokenSource? _download;

    public VoiceManagerWindow(VoiceManager voices)
    {
        InitializeComponent();
        _voices = voices;
        SourceInitialized += (_, _) => WindowBackdrop.ApplyDarkTitleBar(this);
        Refresh();
    }

    public sealed record VoiceRow(VoiceDefinition Voice, string Quality, string Detail,
                                  string Size, string Status, bool Installed, bool Selected)
    {
        public bool Missing => !Installed;
        public bool CanRemove => Installed && !Selected;
    }

    void Refresh()
    {
        var rows = VoiceManager.Voices.Select(v =>
        {
            var quality = v.Id.Split('-')[2];
            var installed = _voices.IsInstalled(v.Id);
            return new VoiceRow(v,
                char.ToUpperInvariant(quality[0]) + quality[1..],
                quality switch { "low" => "Fastest, smallest", "medium" => "Balanced", _ => "Most natural" },
                $"~{v.ApproximateBytes / 1024 / 1024} MB",
                installed ? "Installed" : "Not installed",
                installed, _settings.Current.VoiceId == v.Id);
        }).ToList();
        RyanRows.ItemsSource = rows.Where(r => r.Voice.Id.Contains("-ryan-")).ToList();
        LessacRows.ItemsSource = rows.Where(r => r.Voice.Id.Contains("-lessac-")).ToList();
    }

    async void Download(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: VoiceDefinition voice } || _download is not null) return;
        _download = new CancellationTokenSource();
        CancelButton.Visibility = Visibility.Visible;
        DownloadProgress.Visibility = Visibility.Visible;
        DownloadProgress.Value = 0;
        ProgressText.Text = $"Downloading {voice.DisplayName}…";
        try
        {
            var progress = new Progress<double>(value =>
            {
                DownloadProgress.Value = value * 100;
                ProgressText.Text = $"Downloading {voice.DisplayName} · {value:P0}";
            });
            await _voices.DownloadAsync(voice, progress, _download.Token);
            ProgressText.Text = $"{voice.DisplayName} installed.";
            Refresh();
        }
        catch (OperationCanceledException) { ProgressText.Text = "Download cancelled."; }
        catch (Exception ex) { ProgressText.Text = $"Download failed: {ex.Message}"; }
        finally
        {
            _download.Dispose();
            _download = null;
            CancelButton.Visibility = Visibility.Collapsed;
            DownloadProgress.Visibility = Visibility.Collapsed;
        }
    }

    void CancelDownload(object sender, RoutedEventArgs e) => _download?.Cancel();

    void SelectVoice(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: VoiceDefinition voice } || !_voices.IsInstalled(voice.Id)) return;
        _settings.Current.VoiceId = voice.Id;
        _settings.Save();
        ProgressText.Text = $"{voice.DisplayName} selected.";
        Refresh();
    }

    void Remove(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: VoiceDefinition voice } || voice.Id == _settings.Current.VoiceId) return;
        var answer = MessageBox.Show(this,
            $"Remove {voice.DisplayName}? You will need to download it again to use it.",
            "Remove voice", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            File.Delete(_voices.ModelPath(voice.Id));
            File.Delete(_voices.ModelPath(voice.Id) + ".json");
            ProgressText.Text = $"{voice.DisplayName} removed.";
            Refresh();
        }
        catch (Exception ex) { ProgressText.Text = $"Could not remove voice: {ex.Message}"; }
    }

    void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
    void Close(object sender, RoutedEventArgs e) => Close();
}
