using System.Text.Json;
using PaperClippy.Core.Settings;

namespace PaperClippy.Core.Tests.Support;

/// <summary>
/// Test double that mimics the real store's contract: values round-trip through JSON, and
/// unreadable values come back as <c>default</c> rather than throwing.
/// </summary>
public sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly Dictionary<string, string> _values = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!_values.TryGetValue(key, out var json))
            return Task.FromResult<T?>(default);

        try
        {
            return Task.FromResult(JsonSerializer.Deserialize<T>(json));
        }
        catch (JsonException)
        {
            return Task.FromResult<T?>(default);
        }
    }

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        _values[key] = JsonSerializer.Serialize(value);
        return Task.CompletedTask;
    }

    public void PutRaw(string key, string json) => _values[key] = json;
}
