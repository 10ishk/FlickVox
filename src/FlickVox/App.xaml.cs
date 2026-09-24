namespace FlickVox;
public partial class App : System.Windows.Application
{
    private Mutex? _instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        _instance = new Mutex(true, "FlickVox.SingleInstance", out var created);
        if (!created) { Current.Shutdown(); return; }
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e) { _instance?.Dispose(); base.OnExit(e); }
}
