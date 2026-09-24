using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace FlickVox.Services;
public sealed class HotkeyService : IDisposable
{
  const int WM_HOTKEY=0x0312, MOD_ALT=1, MOD_CONTROL=2; const int Id=7001; HwndSource? _source;
  [DllImport("user32.dll")] static extern bool RegisterHotKey(nint hWnd,int id,int modifiers,uint vk); [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint hWnd,int id);
  public event EventHandler? Pressed;
  public void Register(Window window) { _source=(HwndSource)PresentationSource.FromVisual(window)!; _source.AddHook(WndProc); if(!RegisterHotKey(_source.Handle,Id,MOD_CONTROL|MOD_ALT,(uint)KeyInterop.VirtualKeyFromKey(Key.T))) throw new InvalidOperationException("Ctrl+Alt+T is already in use by another application."); }
  nint WndProc(nint h,int m,nint w,nint l,ref bool handled){if(m==WM_HOTKEY&&w.ToInt32()==Id){Pressed?.Invoke(this,EventArgs.Empty);handled=true;}return 0;}
  public void Dispose(){if(_source is not null){UnregisterHotKey(_source.Handle,Id);_source.RemoveHook(WndProc);}}
}
