namespace FlickVox.Models;
public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string VoiceId { get; set; } = "en_US-ryan-medium";
    public double Speed { get; set; } = 1;
    public float Volume { get; set; } = 0.85f;
    public int OutputDevice { get; set; } = -1;
    public string Hotkey { get; set; } = "Ctrl+Alt+T";
    public double OverlayLeft { get; set; } = double.NaN;
    public double OverlayTop { get; set; } = double.NaN;
    public double OverlayWidth { get; set; } = 640;
    public double OverlayHeight { get; set; } = 80;
    public bool HideOverlayAfterSpeaking { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool PersistHistory { get; set; }
    public double ComposerFontSize { get; set; } = 22;
    public string ComposerFont { get; set; } = "Inter";
}
public sealed record VoiceDefinition(string Id, string DisplayName, long ApproximateBytes, string ModelUrl, string ConfigUrl)
{
    public override string ToString() => DisplayName.Replace(" ", " · ");
}
public sealed record SavedPhrase(Guid Id, string Name, string Text, string? Shortcut = null);
