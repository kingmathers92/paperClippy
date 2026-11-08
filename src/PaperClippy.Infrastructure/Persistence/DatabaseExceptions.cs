namespace PaperClippy.Infrastructure.Persistence;

/// <summary>A migration failed. The failed step was rolled back; the database is unchanged.</summary>
public sealed class DatabaseMigrationException(int version, Exception inner)
    : Exception($"Database migration to version {version} failed: {inner.Message}", inner)
{
    public int Version { get; } = version;
}

/// <summary>
/// The database was written by a newer PaperClippy. We refuse to touch it rather than risk
/// damaging data we don't understand (e.g. after running an older build by mistake).
/// </summary>
public sealed class DatabaseTooNewException(int found, int supported)
    : Exception($"This database is version {found}, but this PaperClippy only understands up to version {supported}. Please update PaperClippy.")
{
    public int FoundVersion { get; } = found;
    public int SupportedVersion { get; } = supported;
}

/// <summary>SQLite's own integrity check failed.</summary>
public sealed class DatabaseCorruptException(string detail)
    : Exception($"Database integrity check failed: {detail}");
