namespace FlickVox.Models;
public sealed class AppSettings
{
    public string Theme { get; set; } = "System";
    public string VoiceId { get; set; } = "en_US-ryan-medium";
    public double Speed { get; set; } = 1;
    public float Volume { get; set; } = 0.85f;
    public int OutputDevice { get; set; } = -1;
    public string Hotkey { get; set; } = "Ctrl+Alt+T";
    public double OverlayLeft { get; set; } = double.NaN;
    public double OverlayTop { get; set; } = double.NaN;
    public double OverlayWidth { get; set; } = 520;
    public double OverlayHeight { get; set; } = 72;
    public bool PhrasePanelExpanded { get; set; } = true;
    public bool ShowSavedPhrases { get; set; } = true;
    public bool OverlayExpanded { get; set; }
    public bool UnifiedWindowMigrated { get; set; }
    public bool UnifiedExpanded { get; set; } = true;
    public double UnifiedLeft { get; set; } = double.NaN;
    public double UnifiedTop { get; set; } = double.NaN;
    public double UnifiedQuickWidth { get; set; } = 360;
    public double UnifiedQuickHeight { get; set; } = 48;
    public double UnifiedExpandedWidth { get; set; } = 410;
    public double UnifiedExpandedHeight { get; set; } = 300;
    public bool FirstSpeechConfirmed { get; set; }
    public bool FirstRunGuidanceDismissed { get; set; }
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
