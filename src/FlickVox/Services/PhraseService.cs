using System.Collections.ObjectModel;
using System.Text.Json;
using FlickVox.Models;
using FlickVox.Infrastructure;

namespace FlickVox.Services;

public sealed class PhraseService
{
    readonly string _path = Path.Combine(AppDataPaths.Root, "phrases.json");
    bool _loadFailed;
    public ObservableCollection<SavedPhrase> Items { get; } = [];

    public PhraseService()
    {
        try
        {
            if (File.Exists(_path))
                foreach (var phrase in JsonSerializer.Deserialize<List<SavedPhrase>>(File.ReadAllText(_path)) ?? []) Items.Add(phrase);
        }
        catch { _loadFailed = true; /* Never overwrite an unreadable personal phrase file on startup. */ }
    }

    public void Add(string name, string text) { EnsureWritable(); Items.Add(new SavedPhrase(Guid.NewGuid(), name.Trim(), text.Trim())); Save(); }
    public void Update(SavedPhrase original, string name, string text)
    {
        EnsureWritable();
        var index = Items.IndexOf(original);
        if (index < 0) return;
        Items[index] = original with { Name = name.Trim(), Text = text.Trim() };
        Save();
    }
    public void Delete(SavedPhrase phrase) { EnsureWritable(); if (Items.Remove(phrase)) Save(); }
    void EnsureWritable()
    {
        if (_loadFailed) throw new InvalidDataException("Saved phrases could not be read. The original file has not been changed.");
    }
    void Save()
    {
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Items));
        File.Move(temp, _path, true);
    }
}
