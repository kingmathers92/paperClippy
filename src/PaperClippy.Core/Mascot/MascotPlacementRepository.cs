using PaperClippy.Core.Settings;

namespace PaperClippy.Core.Mascot;

/// <summary>Loads and saves <see cref="MascotPlacement"/> through the settings store.</summary>
public sealed class MascotPlacementRepository(ISettingsStore store)
{
    public const string Key = "mascot.placement";

    public async Task<MascotPlacement> LoadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await store.GetAsync<MascotPlacement>(Key, cancellationToken).ConfigureAwait(false);
        return (stored ?? new MascotPlacement()).Sanitized();
    }

    public Task SaveAsync(MascotPlacement placement, CancellationToken cancellationToken = default)
        => store.SetAsync(Key, placement.Sanitized(), cancellationToken);
}
