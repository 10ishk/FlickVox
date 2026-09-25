using System.IO.Compression;
using System.Net.Http;
using FlickVox.Infrastructure;

namespace FlickVox.Services;

public sealed class PiperRuntimeManager
{
    const string RuntimeUrl = "https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip";
    readonly string _root = AppDataPaths.Runtime;
    public string ExecutablePath => Path.Combine(_root, "piper", "piper.exe");
    public bool IsInstalled => File.Exists(ExecutablePath);

    public async Task InstallAsync(CancellationToken ct)
    {
        if (IsInstalled) return;
        Directory.CreateDirectory(_root);
        var staging = Path.Combine(_root, "piper-staging-" + Guid.NewGuid().ToString("N"));
        var archive = staging + ".zip";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = await client.GetAsync(RuntimeUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            await using (var output = File.Create(archive))
                await response.Content.CopyToAsync(output, ct);
            if (new FileInfo(archive).Length < 1024 * 1024) throw new InvalidDataException("Piper download was unexpectedly small.");
            ZipFile.ExtractToDirectory(archive, staging);
            var exe = Directory.GetFiles(staging, "piper.exe", SearchOption.AllDirectories).FirstOrDefault()
                      ?? throw new InvalidDataException("The official Piper archive did not contain piper.exe.");
            var source = Path.GetDirectoryName(exe)!;
            var target = Path.GetDirectoryName(ExecutablePath)!;
            if (!Directory.Exists(target))
                Directory.Move(source, target);
            if (!IsInstalled) throw new InvalidDataException("Piper installation is incomplete.");
        }
        finally
        {
            if (File.Exists(archive)) File.Delete(archive);
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }
}
