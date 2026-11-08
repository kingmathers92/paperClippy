namespace PaperClippy.Core.Settings;

/// <summary>
/// Durable key/value storage for application settings. Values are stored as JSON.
/// Implementations must never throw for a missing or unreadable value: they return
/// <c>default</c> so callers can fall back to sensible defaults.
/// </summary>
public interface ISettingsStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}

public static class SettingsStoreExtensions
{
    public static async Task<T> GetOrDefaultAsync<T>(this ISettingsStore store, string key, T fallback, CancellationToken cancellationToken = default)
    {
        var value = await store.GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        return value is null ? fallback : value;
    }
}
