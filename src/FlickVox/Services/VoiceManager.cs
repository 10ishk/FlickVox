using FlickVox.Models;
using System.Net.Http;
namespace FlickVox.Services;
public sealed class VoiceManager
{
    private const string Base = "https://huggingface.co/rhasspy/piper-voices/resolve/main";
    public static readonly IReadOnlyList<VoiceDefinition> Voices = new[] {
      Voice("en_US-ryan-low", "Ryan Low", 17), Voice("en_US-ryan-medium", "Ryan Medium", 63), Voice("en_US-ryan-high", "Ryan High", 63),
      Voice("en_US-lessac-low", "Lessac Low", 32), Voice("en_US-lessac-medium", "Lessac Medium", 63), Voice("en_US-lessac-high", "Lessac High", 63) };
    private readonly string _voices = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlickVox", "runtime", "voices");
    public VoiceManager() => Directory.CreateDirectory(_voices);
    private static VoiceDefinition Voice(string id, string name, int mb) { var parts=id.Split('-'); var stem=$"en/en_US/{parts[1]}/{parts[2]}/{id}"; return new(id,name,mb*1024L*1024,$"{Base}/{stem}.onnx?download=true",$"{Base}/{stem}.onnx.json?download=true"); }
    public string ModelPath(string id) => Path.Combine(_voices, id + ".onnx");
    public bool IsInstalled(string id) { var model=ModelPath(id); return File.Exists(model) && new FileInfo(model).Length > 1024 && File.Exists(model + ".json"); }
    public async Task DownloadAsync(VoiceDefinition voice, IProgress<double>? progress, CancellationToken ct)
    {
      using var client = new HttpClient(); await DownloadFile(client, voice.ModelUrl, ModelPath(voice.Id), progress, ct); await DownloadFile(client, voice.ConfigUrl, ModelPath(voice.Id)+".json", null, ct);
    }
    private static async Task DownloadFile(HttpClient client, string url, string target, IProgress<double>? progress, CancellationToken ct) { var tmp=target+".download"; try { using var r=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct); r.EnsureSuccessStatusCode(); await using var input=await r.Content.ReadAsStreamAsync(ct); await using var output=File.Create(tmp); var total=r.Content.Headers.ContentLength ?? 0L; var buffer=new byte[81920]; long done=0; int n; while((n=await input.ReadAsync(buffer,ct))>0){await output.WriteAsync(buffer.AsMemory(0,n),ct);done+=n;progress?.Report(total==0?0:(double)done/total);} if(new FileInfo(tmp).Length<1024) throw new InvalidDataException("Download is unexpectedly small."); File.Move(tmp,target,true); } finally { if(File.Exists(tmp)) File.Delete(tmp); } }
}
