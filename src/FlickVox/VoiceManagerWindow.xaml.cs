using FlickVox.Models; using FlickVox.Services;
namespace FlickVox;
public partial class VoiceManagerWindow : Window
{
 readonly VoiceManager _voices; public VoiceManagerWindow(VoiceManager voices){InitializeComponent();_voices=voices;Refresh();} record Row(VoiceDefinition Voice,string Name,string Status,string Size);void Refresh()=>List.ItemsSource=VoiceManager.Voices.Select(v=>new Row(v,v.DisplayName,_voices.IsInstalled(v.Id)?"Installed":"Missing",$"~{v.ApproximateBytes/1024/1024} MB")).ToList(); async void Download(object s,RoutedEventArgs e){if(List.SelectedItem is not Row r)return;try{var p=new Progress<double>(v=>Progress.Text=$"Downloading {v:P0}");await _voices.DownloadAsync(r.Voice,p,CancellationToken.None);Refresh();Progress.Text="Installed";}catch(Exception ex){Progress.Text=ex.Message;}}void Remove(object s,RoutedEventArgs e){if(List.SelectedItem is not Row r)return;try{File.Delete(_voices.ModelPath(r.Voice.Id));File.Delete(_voices.ModelPath(r.Voice.Id)+".json");Refresh();}catch(Exception ex){Progress.Text=ex.Message;}}void Close(object s,RoutedEventArgs e)=>Close();
}
