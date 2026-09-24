using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace FlickVox.Infrastructure;
internal static class WindowBackdrop
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    public static void ApplyDarkTitleBar(Window window)
    {
        try { var hwnd = new WindowInteropHelper(window).EnsureHandle(); var dark = 1; DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)); }
        catch { /* Solid dark client area remains the fallback. */ }
    }
}
