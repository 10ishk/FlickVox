using Microsoft.Win32;
using System.Windows;

namespace FlickVox.Infrastructure;

internal static class ThemeManager
{
    public static string Preference { get; private set; } = "Dark";
    public static bool IsDark { get; private set; } = true;

    public static void Initialize(string preference)
    {
        Preference = preference;
        Apply();
        SystemEvents.UserPreferenceChanged += (_, _) =>
            System.Windows.Application.Current.Dispatcher.BeginInvoke(Apply);
    }

    public static void SetPreference(string preference)
    {
        Preference = preference;
        Apply();
    }

    static void Apply()
    {
        var highContrast = SystemParameters.HighContrast;
        var light = Preference == "Light" || (Preference == "System" && SystemUsesLightTheme());
        IsDark = highContrast || !light;
        var name = highContrast ? "HighContrast" : light ? "Light" : "Dark";
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        dictionaries[0] = new ResourceDictionary { Source = new Uri($"Themes/{name}.xaml", UriKind.Relative) };
        foreach (Window window in System.Windows.Application.Current.Windows)
            WindowBackdrop.Apply(window, IsDark);
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
