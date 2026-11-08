namespace PaperClippy.Infrastructure.Persistence;

/// <summary>One forward-only schema step. <see cref="Version"/> is stored in <c>PRAGMA user_version</c>.</summary>
public sealed record Migration(int Version, string Description, string Sql);

/// <summary>
/// The schema history. Rules: never edit a released migration; add a new one. Each migration runs
/// in its own transaction together with the version bump, so a failure leaves the database untouched.
/// </summary>
public static class Migrations
{
    public static IReadOnlyList<Migration> All { get; } =
    [
        new(1, "Settings key/value store",
            """
            CREATE TABLE settings (
                key         TEXT NOT NULL PRIMARY KEY,
                value       TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            ) WITHOUT ROWID;
            """),
    ];
}
