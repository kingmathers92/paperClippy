using PaperClippy.Core.Settings;

namespace PaperClippy.Core.Mascot;

/// <summary>Loads and saves <see cref="MascotSettings"/> through the settings store.</summary>
public sealed class MascotSettingsRepository(ISettingsStore store)
{
    public const string Key = "mascot.settings";

    public async Task<MascotSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await store.GetAsync<MascotSettings>(Key, cancellationToken).ConfigureAwait(false);
        return (stored ?? new MascotSettings()).Sanitized();
    }

    public Task SaveAsync(MascotSettings settings, CancellationToken cancellationToken = default)
        => store.SetAsync(Key, settings.Sanitized(), cancellationToken);
}
