using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace FlickVox.Services;

public sealed class HotkeyService : IDisposable
{
    const int WM_HOTKEY = 0x0312, MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, Id = 7001;
    HwndSource? _source;
    string _current = "Ctrl+Alt+T";
    [DllImport("user32.dll")] static extern bool RegisterHotKey(nint handle, int id, int modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint handle, int id);
    public event EventHandler? Pressed;

    public void Register(Window window, string shortcut)
    {
        _source = (HwndSource)PresentationSource.FromVisual(window)!;
        _source.AddHook(WndProc);
        if (TryChange(shortcut)) return;
        if (shortcut != "Ctrl+Alt+T" && TryChange("Ctrl+Alt+T")) return;
        throw new InvalidOperationException($"{shortcut} and the Ctrl+Alt+T fallback are in use by another application.");
    }
    public bool TryChange(string shortcut)
    {
        if (_source is null || !TryParse(shortcut, out var modifiers, out var key)) return false;
        if (shortcut.Equals(_current, StringComparison.OrdinalIgnoreCase) && _registered) return true;
        if (_registered) UnregisterHotKey(_source.Handle, Id);
        if (RegisterHotKey(_source.Handle, Id, modifiers, key))
        {
            _current = shortcut;
            _registered = true;
            return true;
        }
        if (TryParse(_current, out var oldModifiers, out var oldKey))
            _registered = RegisterHotKey(_source.Handle, Id, oldModifiers, oldKey);
        return false;
    }
    bool _registered;
    public string Current => _current;
    static bool TryParse(string shortcut, out int modifiers, out uint key)
    {
        modifiers = 0; key = 0;
        var parts = shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        foreach (var part in parts[..^1])
            modifiers |= part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => MOD_CONTROL,
                "ALT" => MOD_ALT,
                "SHIFT" => MOD_SHIFT,
                _ => 0
            };
        var last = parts[^1].ToUpperInvariant();
        if (last.Length == 1 && last[0] is >= 'A' and <= 'Z') key = last[0];
        else if (last.StartsWith('F') && int.TryParse(last[1..], out var fn) && fn is >= 1 and <= 12) key = (uint)(0x70 + fn - 1);
        return key != 0 && (modifiers & (MOD_CONTROL | MOD_ALT)) != 0;
    }
    nint WndProc(nint handle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WM_HOTKEY && wParam.ToInt32() == Id) { Pressed?.Invoke(this, EventArgs.Empty); handled = true; }
        return 0;
    }
    public void Dispose()
    {
        if (_source is null) return;
        if (_registered) UnregisterHotKey(_source.Handle, Id);
        _source.RemoveHook(WndProc);
    }
}
