using System.Globalization;
using System.Text;

namespace PaperClippy.Infrastructure.Diagnostics;

/// <summary>
/// A deliberately tiny, dependency-free rolling log file. Logging must never be the reason the
/// app fails, so every I/O problem is swallowed. One rollover file (<c>.1</c>) caps disk use.
/// </summary>
public sealed class FileLog(string path, long maxBytes = 1_000_000)
{
    private readonly object _gate = new();

    public string Path { get; } = path;

    public void Info(string message) => Write("INFO ", message, null);

    public void Warn(string message, Exception? exception = null) => Write("WARN ", message, exception);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
                .Append(' ').Append(level).Append(' ').Append(message);
            if (exception is not null)
                line.AppendLine().Append(exception);
            line.AppendLine();

            lock (_gate)
            {
                var directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(Path) && new FileInfo(Path).Length > maxBytes)
                    File.Move(Path, Path + ".1", overwrite: true);

                File.AppendAllText(Path, line.ToString());
            }
        }
        catch
        {
            // Never let diagnostics take the app down.
        }
    }
}
