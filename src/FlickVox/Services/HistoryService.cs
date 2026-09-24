using System.Collections.ObjectModel;
using System.Text.Json;

namespace FlickVox.Services;

public sealed class HistoryService
{
    readonly SettingsService _settings;
    readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlickVox", "history.json");
    public ObservableCollection<string> Items { get; } = [];

    public HistoryService(SettingsService settings)
    {
        _settings = settings;
        if (!settings.Current.PersistHistory) return;
        try
        {
            if (File.Exists(_path))
                foreach (var item in JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_path)) ?? [])
                    if (!string.IsNullOrWhiteSpace(item) && Items.Count < 30 && !Items.Contains(item)) Items.Add(item);
        }
        catch { /* Preserve a damaged file for manual recovery. */ }
    }

    public void Add(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        Items.Remove(text);
        Items.Insert(0, text);
        while (Items.Count > 30) Items.RemoveAt(Items.Count - 1);
        SaveIfEnabled();
    }

    public void SaveIfEnabled()
    {
        if (!_settings.Current.PersistHistory) return;
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Items));
        File.Move(temp, _path, true);
    }
}
