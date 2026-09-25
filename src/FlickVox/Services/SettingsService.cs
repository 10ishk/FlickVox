using System.Text.Json;
using System.Text.Json.Serialization;
using FlickVox.Models;
namespace FlickVox.Services;
public sealed class SettingsService
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    private readonly string _path;
    public AppSettings Current { get; private set; } = new();
    public SettingsService()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlickVox");
        Directory.CreateDirectory(root); _path = Path.Combine(root, "settings.json"); Load();
    }
    public void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new();
            using var document = JsonDocument.Parse(json);
            // Every pre-existing settings file predates the completed onboarding rollout,
            // including files written by development builds with a provisional unlock flag.
            if (!document.RootElement.TryGetProperty(nameof(AppSettings.OnboardingMigrationComplete), out _))
            {
                Current.OverflowUnlocked = true;
                Current.OnboardingMigrationComplete = true;
                Save();
            }
        }
        catch { Current = new(); }
    }
    public void Save() { var temp = _path + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(Current, JsonOptions)); File.Move(temp, _path, true); }
}
