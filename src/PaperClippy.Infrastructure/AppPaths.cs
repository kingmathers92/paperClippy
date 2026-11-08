namespace PaperClippy.Infrastructure;

/// <summary>
/// Where PaperClippy keeps its files. Everything lives under one per-user folder so backup,
/// uninstall and "portable mode" are all trivial. Set <c>PAPERCLIPPY_DATA_DIR</c> to redirect
/// it (useful for manual testing without touching real data).
/// </summary>
public sealed class AppPaths(string rootDirectory)
{
    public const string DataDirectoryEnvironmentVariable = "PAPERCLIPPY_DATA_DIR";

    public string Root { get; } = rootDirectory;
    public string DatabaseFile => Path.Combine(Root, "data", "paperclippy.db");
    public string BackupsDirectory => Path.Combine(Root, "backups");
    public string LogFile => Path.Combine(Root, "logs", "paperclippy.log");

    public static AppPaths ForCurrentUser()
    {
        var overridden = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
            return new AppPaths(Path.GetFullPath(overridden));

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPaths(Path.Combine(local, "PaperClippy"));
    }
}
