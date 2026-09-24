using FlickVox.Services;
namespace FlickVox;
public partial class OverlayWindow : Window
{
 readonly MainWindow _main; readonly PiperSpeechService _speech; readonly SettingsService _settings; readonly List<string> _items=new(); int _cursor=-1;
 public OverlayWindow(MainWindow main,PiperSpeechService speech,SettingsService settings){InitializeComponent();_main=main;_speech=speech;_settings=settings; Width=settings.Current.OverlayWidth;Height=settings.Current.OverlayHeight;if(!double.IsNaN(settings.Current.OverlayLeft)){Left=settings.Current.OverlayLeft;Top=settings.Current.OverlayTop;} Closed+=(_,_)=>Save();}
 public void ShowAndFocus(){if(!IsVisible)Show();Activate();Input.Focus();}
 async void KeyDownHandler(object s,KeyEventArgs e){if(e.Key==Key.Enter&&!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)){e.Handled=true;var text=Input.Text;if(!string.IsNullOrWhiteSpace(text)){_items.Remove(text);_items.Insert(0,text);if(_settings.Current.HideOverlayAfterSpeaking){Hide();}else Input.Clear();await _speech.SpeakAsync(text);}}else if(e.Key==Key.Escape){_speech.Stop();Hide();}else if((e.Key is Key.Up or Key.Down)&&_items.Count>0){_cursor=Math.Clamp(_cursor+(e.Key==Key.Up?1:-1),0,_items.Count-1);Input.Text=_items[_cursor];Input.CaretIndex=Input.Text.Length;e.Handled=true;}}
 void Drag(object s,MouseButtonEventArgs e){if(e.OriginalSource is Border)DragMove();} void Save(){_settings.Current.OverlayLeft=Left;_settings.Current.OverlayTop=Top;_settings.Current.OverlayWidth=Width;_settings.Current.OverlayHeight=Height;_settings.Save();}
}
