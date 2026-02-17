using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.AliasResolver;

/// <summary>
/// MusicBrainz album external id.
/// </summary>
public class MusicBrainzAliasExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "MusicBrainz Alias";

    /// <inheritdoc />
    public string Key => ProviderKeys.MusicBrainzAlias.ToString();

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Album;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Audio || item is MusicAlbum || item is MusicArtist;
}
