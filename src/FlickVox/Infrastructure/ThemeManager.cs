using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;

namespace FlickVox.Infrastructure;

internal static class ThemeManager
{
    public static readonly string[] Choices =
    ["System", "Midnight Violet", "Signal Green", "Ember", "Ocean", "Neon Rose", "Paper", "Blush Pink", "High Contrast"];

    static readonly Dictionary<string, string> Files = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Midnight Violet"] = "MidnightViolet", ["Signal Green"] = "SignalGreen", ["Ember"] = "Ember",
        ["Ocean"] = "Ocean", ["Neon Rose"] = "NeonRose", ["Paper"] = "Paper",
        ["Blush Pink"] = "BlushPink", ["High Contrast"] = "HighContrast"
    };

    public static string Preference { get; private set; } = "System";
    public static bool IsDark { get; private set; } = true;
    public static string ActiveTheme { get; private set; } = "Midnight Violet";

    public static string Normalize(string? preference) => preference switch
    {
        "Dark" => "Midnight Violet", "Light" => "Paper",
        _ when preference is not null && Files.Keys.Any(x => x.Equals(preference, StringComparison.OrdinalIgnoreCase)) =>
            Files.Keys.First(x => x.Equals(preference, StringComparison.OrdinalIgnoreCase)),
        _ => "System"
    };

    public static void Initialize(string preference)
    {
        Preference = Normalize(preference);
        Apply();
        SystemEvents.UserPreferenceChanged += (_, _) =>
            Application.Current.Dispatcher.BeginInvoke(Apply);
    }

    public static void SetPreference(string preference)
    {
        Preference = Normalize(preference);
        Apply();
    }

    public static (Color Background, Color Surface, Color Accent) Preview(string preference)
    {
        var dictionary = LoadPalette(Resolve(Normalize(preference), false));
        return ((Color)dictionary["Color.Background"], (Color)dictionary["Color.Surface"],
            (Color)dictionary["Color.Accent"]);
    }

    static string Resolve(string preference, bool respectHighContrast) =>
        respectHighContrast && SystemParameters.HighContrast ? "High Contrast" :
        preference == "System" ? (SystemUsesLightTheme() ? "Paper" : "Midnight Violet") : preference;

    static ResourceDictionary LoadPalette(string name) =>
        new() { Source = new Uri($"Themes/{Files[name]}.xaml", UriKind.Relative) };

    static void Apply()
    {
        ActiveTheme = Resolve(Preference, true);
        IsDark = ActiveTheme is not ("Paper" or "Blush Pink");
        var palette = LoadPalette(ActiveTheme);
        var semantic = BuildSemantic(palette, IsDark, ActiveTheme == "High Contrast");
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries[0] = palette;
        dictionaries[1] = semantic;
        foreach (Window window in Application.Current.Windows)
            WindowBackdrop.Apply(window, IsDark);
    }

    static ResourceDictionary BuildSemantic(ResourceDictionary palette, bool dark, bool highContrast)
    {
        var background = (Color)palette["Color.Background"];
        var surface = (Color)palette["Color.Surface"];
        var raised = (Color)palette["Color.SurfaceRaised"];
        var hover = (Color)palette["Color.SurfaceHover"];
        var accent = (Color)palette["Color.Accent"];
        var text = dark ? Color.FromRgb(0xEC, 0xEC, 0xF5) : Color.FromRgb(0x1B, 0x1B, 0x27);
        if (highContrast) text = Colors.White;
        var secondary = EnsureContrast(dark ? Color.FromRgb(0xB9, 0xBA, 0xCA) : Color.FromRgb(0x4D, 0x4B, 0x59), surface, 4.5);
        if (highContrast) secondary = Colors.White;
        var onAccent = Contrast(Colors.White, accent) >= Contrast(Colors.Black, accent) ? Colors.White : Colors.Black;
        var accentText = EnsureContrast(accent, surface, 4.5);
        var stroke = highContrast ? Colors.White : EnsureContrast(Blend(surface, text, dark ? .32 : .4), surface, 3);
        var subtle = highContrast ? Colors.White : Blend(surface, text, dark ? .16 : .14);
        var direction = onAccent == Colors.White ? Colors.Black : Colors.White;
        var accentHover = Blend(accent, direction, .1);
        var accentPressed = Blend(accent, direction, .2);
        var danger = EnsureContrast(dark ? Color.FromRgb(0xFF, 0x7A, 0x84) : Color.FromRgb(0xB1, 0x20, 0x35), surface, 4.5);
        var warning = EnsureContrast(dark ? Color.FromRgb(0xFF, 0xCB, 0x66) : Color.FromRgb(0x8A, 0x58, 0x00), surface, 4.5);
        var resources = new ResourceDictionary();
        void Add(string key, Color color) => resources[key] = new SolidColorBrush(color);
        Add("Brush.Canvas", background); Add("Brush.Surface", surface); Add("Brush.SurfaceRaised", raised);
        Add("Brush.SurfaceHover", hover); Add("Brush.Text", text); Add("Brush.TextSecondary", secondary);
        Add("Brush.TextTertiary", secondary); Add("Brush.StrokeControl", stroke); Add("Brush.StrokeSubtle", subtle);
        Add("Brush.Accent", accent); Add("Brush.AccentHover", accentHover); Add("Brush.AccentPressed", accentPressed);
        Add("Brush.AccentText", accentText); Add("Brush.PrimaryAction", accent); Add("Brush.OnPrimary", onAccent);
        Add("Brush.Signal", accent); Add("Brush.SignalBorder", accent); Add("Brush.Danger", danger); Add("Brush.Warning", warning);
        resources["Brush.AccentSubtle"] = new SolidColorBrush(Color.FromArgb(highContrast ? (byte)90 : (byte)42, accent.R, accent.G, accent.B));
        resources["Brush.SignalSubtle"] = resources["Brush.AccentSubtle"];
        resources["Brush.Selection"] = new SolidColorBrush(Color.FromArgb(105, accent.R, accent.G, accent.B));
        var logo = highContrast ? "flickvox-ui-high-contrast.png" : dark ? "flickvox-ui-light.png" : "flickvox-ui-dark.png";
        resources["Branding.Logo"] = new BitmapImage(new Uri($"pack://application:,,,/Assets/Branding/{logo}"));
        return resources;
    }

    static Color EnsureContrast(Color candidate, Color background, double minimum)
    {
        if (Contrast(candidate, background) >= minimum) return candidate;
        var target = Contrast(Colors.White, background) > Contrast(Colors.Black, background) ? Colors.White : Colors.Black;
        for (var i = 1; i <= 100; i++)
        {
            var adjusted = Blend(candidate, target, i / 100.0);
            if (Contrast(adjusted, background) >= minimum) return adjusted;
        }
        return target;
    }

    static Color Blend(Color a, Color b, double amount) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * amount),
        (byte)Math.Round(a.G + (b.G - a.G) * amount),
        (byte)Math.Round(a.B + (b.B - a.B) * amount));

    public static double Contrast(Color a, Color b)
    {
        static double Luminance(Color color)
        {
            static double Channel(byte value)
            {
                var normalized = value / 255.0;
                return normalized <= .04045 ? normalized / 12.92 : Math.Pow((normalized + .055) / 1.055, 2.4);
            }
            return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        }
        var first = Luminance(a); var second = Luminance(b);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }

    static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch { return false; }
    }
}
