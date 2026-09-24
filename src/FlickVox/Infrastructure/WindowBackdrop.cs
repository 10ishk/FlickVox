using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace FlickVox.Infrastructure;
internal static class WindowBackdrop
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    public static void ApplyDarkTitleBar(Window window) => Apply(window, ThemeManager.IsDark);
    public static void Apply(Window window, bool useDark)
    {
        try { var hwnd = new WindowInteropHelper(window).EnsureHandle(); var dark = useDark ? 1 : 0; DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)); }
        catch { /* Solid dark client area remains the fallback. */ }
    }
}
