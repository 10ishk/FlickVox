namespace FlickVox.Infrastructure;

internal static class AppDataPaths
{
    // An explicit per-process root permits isolated release checks without touching a user's profile.
    internal static string Root => Environment.GetEnvironmentVariable("FLICKVOX_DATA_ROOT") is { Length: > 0 } overrideRoot
        ? Path.GetFullPath(overrideRoot)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlickVox");

    internal static string Runtime => Path.Combine(Root, "runtime");
}
