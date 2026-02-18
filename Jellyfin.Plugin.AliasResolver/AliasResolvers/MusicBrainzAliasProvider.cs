using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AliasResolver.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Plugins;
using MetaBrainz.MusicBrainz;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AliasResolver;

/// <summary>
/// MusicBrainzAliasProvider.
/// </summary>
public class MusicBrainzAliasProvider : ICustomMetadataProvider<MusicAlbum>,
    ICustomMetadataProvider<Audio>,
    ICustomMetadataProvider<MusicArtist>,
    IHasOrder, IForcedProvider, IDisposable, IHasItemChangeMonitor
{
    private readonly ILogger<MusicBrainzAliasProvider> _logger;

    private Query _musicBrainzQuery;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicBrainzAliasProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public MusicBrainzAliasProvider(ILogger<MusicBrainzAliasProvider> logger)
    {
        _logger = logger;
        _musicBrainzQuery = new Query();
        ReloadConfig(null, Plugin.Instance!.Configuration);
        Plugin.Instance!.ConfigurationChanged += ReloadConfig;
    }

    /// <inheritdoc />
    public string Name => "MusicBrainz Alias";

    /// <inheritdoc />
    public int Order => 200;

    private void ReloadConfig(object? sender, BasePluginConfiguration e)
    {
        var configuration = (PluginConfiguration)e;
        if (Uri.TryCreate(configuration.Server, UriKind.Absolute, out var server))
        {
            Query.DefaultServer = server.DnsSafeHost;
            Query.DefaultPort = server.Port;
            Query.DefaultUrlScheme = server.Scheme;
        }
        else
        {
            // Fallback to official server
            _logger.LogWarning("Invalid MusicBrainz server specified, falling back to official server");
            var defaultServer = new Uri(PluginConfiguration.DefaultServer);
            Query.DefaultServer = defaultServer.Host;
            Query.DefaultPort = defaultServer.Port;
            Query.DefaultUrlScheme = defaultServer.Scheme;
        }

        Query.DelayBetweenRequests = configuration.RateLimit;
        _musicBrainzQuery = new Query();
    }

    /// <inheritdoc />
    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
    {
        if (item is Audio audio)
        {
            MusicBrainzAliasMediaState state = new MusicBrainzAliasMediaState(audio);

            // Update audio if audios album needs updated
            if (Plugin.Instance!.Configuration.DoAlbum && audio.Album != audio.AlbumEntity.Name)
            {
                return true;
            }

            // Update audio if artist needs updated
            if (Plugin.Instance!.Configuration.DoArtist && !audio.AlbumArtists.Contains(audio.AlbumEntity.AlbumArtist) && !string.IsNullOrEmpty(audio.AlbumEntity.AlbumArtist))
            {
                return true;
            }

            // If audio status is unknown or the set name doesn't match the current name
            if (Plugin.Instance!.Configuration.DoTrack)
            {
                return state.ShouldUpdate(audio);
            }
        }

        if (item is MusicAlbum album)
        {
            MusicBrainzAliasMediaState state = new MusicBrainzAliasMediaState(album);

            // Update album if artist needs updated
            if (Plugin.Instance!.Configuration.DoArtist && album.AlbumArtist != album.MusicArtist.Name && !string.IsNullOrEmpty(album.MusicArtist.Name))
            {
                return true;
            }

            // If album status is unknown or the set name doesn't match the current name
            if (Plugin.Instance!.Configuration.DoAlbum)
            {
                return state.ShouldUpdate(album);
            }
        }

        if (item is MusicArtist)
        {
            MusicBrainzAliasMediaState state = new MusicBrainzAliasMediaState(item);

            // If artist status is unknown or the set name doesn't match the current name
            if (Plugin.Instance!.Configuration.DoArtist)
            {
                return state.ShouldUpdate(item);
            }
        }

        return false;
    }

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(MusicAlbum item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        return ResolveAlbumAlias(item, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Audio item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        return ResolveAudioAlias(item, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(MusicArtist item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        return ResolveArtistAlias(item, options, cancellationToken);
    }

    private async Task<ItemUpdateType> ResolveAlbumAlias(MusicAlbum item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        if (options.IsAutomated && !Plugin.Instance!.Configuration.Automatic)
        {
            return ItemUpdateType.None;
        }

        var updateValue = ItemUpdateType.None;

        try
        {
            updateValue |= await UpdateAlbumName(item, options, cancellationToken).ConfigureAwait(false);
        }
        catch (MetaBrainz.Common.HttpError)
        {
        }

        updateValue |= SyncAlbumArtist(item);
        return updateValue;
    }

    private async Task<ItemUpdateType> ResolveAudioAlias(Audio item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        // If automated scan is disabled, exit
        if (options.IsAutomated && !Plugin.Instance!.Configuration.Automatic)
        {
            return ItemUpdateType.None;
        }

        var updateValue = ItemUpdateType.None;

        try
        {
            updateValue |= await UpdateAudioName(item, options, cancellationToken).ConfigureAwait(false);
        }
        catch (MetaBrainz.Common.HttpError)
        {
        }

        updateValue |= SyncAudioAlbum(item);
        updateValue |= SyncAudioArtist(item);
        return updateValue;
    }

    private async Task<ItemUpdateType> ResolveArtistAlias(MusicArtist item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        if (options.IsAutomated && !Plugin.Instance!.Configuration.Automatic)
        {
            return ItemUpdateType.None;
        }

        var updateValue = ItemUpdateType.None;

        try
        {
            updateValue |= await UpdateArtistName(item, options, cancellationToken).ConfigureAwait(false);
        }
        catch (MetaBrainz.Common.HttpError)
        {
        }

        return updateValue;
    }

    private async Task<ItemUpdateType> UpdateAlbumName(MusicAlbum item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        if (!Plugin.Instance!.Configuration.DoAlbum)
        {
            return ItemUpdateType.None;
        }

        var releaseId = item.GetProviderId(MetadataProvider.MusicBrainzAlbum);
        var releaseGroupId = item.GetProviderId(MetadataProvider.MusicBrainzReleaseGroup);
        if (string.IsNullOrEmpty(releaseId) || string.IsNullOrEmpty(releaseGroupId))
        {
            return ItemUpdateType.None;
        }

        var status = new MusicBrainzAliasMediaState(item);
        if (!status.ShouldUpdate(item) && !options.RemoveOldMetadata)
        {
            return ItemUpdateType.None;
        }

        if (status.Status != MusicBrainzAliasStatus.Unknown && !string.IsNullOrEmpty(status.Name) && item.Name != status.Name && !options.RemoveOldMetadata)
        {
            _logger.LogDebug("Restoring album name from status: {ItemName} to {StatusName}", item.Name, status.Name);
            item.Name = status.Name;
            return ItemUpdateType.ImageUpdate;
        }

        MusicBrainzAliasResolver aliasResolver = new MusicBrainzAliasResolver(item.Name, _logger);
        aliasResolver.SetRatioLimit(Plugin.Instance!.Configuration.Threshold);
        if (!aliasResolver.ShouldUpdate() && !options.RemoveOldMetadata)
        {
            return SyncStatusToItem(item, status, aliasResolver);
        }

        var currentRelease = await _musicBrainzQuery.LookupReleaseAsync(new Guid(releaseId), Include.None, cancellationToken).ConfigureAwait(false);
        if (item.Name == currentRelease.Title) // Ensure linked release matches name
        {
            aliasResolver.AddString(item.Name, currentRelease.TextRepresentation!.Script, currentRelease.TextRepresentation.Language);
        }

        if (!aliasResolver.ShouldUpdate() && !options.RemoveOldMetadata)
        {
            return SyncStatusToItem(item, status, aliasResolver);
        }

        // Check if album title is english or has an english alias
        var group = await _musicBrainzQuery.LookupReleaseGroupAsync(new Guid(releaseGroupId), Include.Releases | Include.Aliases, null, cancellationToken).ConfigureAwait(false);

        foreach (var alias in group.Aliases ?? [])
        {
            aliasResolver.AddString(alias.Name, null, currentRelease!.TextRepresentation!.Language);
        }

        foreach (var release in group.Releases ?? [])
        {
            if (!string.IsNullOrEmpty(release.Title))
            {
                aliasResolver.AddString(release.Title, release.TextRepresentation!.Script, release.TextRepresentation.Language);
            }

            foreach (var alias in release.Aliases ?? [])
            {
                aliasResolver.AddString(alias.Name, null, release.TextRepresentation!.Language);
            }
        }

        var result = await aliasResolver.GetValue().ConfigureAwait(false);
        _logger.LogDebug("Found album name with value of: {Name}", result);
        if (result != item.Name)
        {
            item.Name = result;
            SyncStatusToItem(item, status, aliasResolver);
            return ItemUpdateType.MetadataEdit;
        }

        return SyncStatusToItem(item, status, aliasResolver);
    }

    private async Task<ItemUpdateType> UpdateAudioName(Audio item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        if (!Plugin.Instance!.Configuration.DoTrack)
        {
            return ItemUpdateType.None;
        }

        var recordingId = item.GetProviderId(MetadataProvider.MusicBrainzRecording);
        var releaseId = item.GetProviderId(MetadataProvider.MusicBrainzAlbum);
        if (string.IsNullOrEmpty(recordingId) || string.IsNullOrEmpty(releaseId))
        {
            return ItemUpdateType.None;
        }

        var status = new MusicBrainzAliasMediaState(item);
        if (!status.ShouldUpdate(item) && !options.RemoveOldMetadata)
        {
            return ItemUpdateType.None;
        }

        if (status.Status != MusicBrainzAliasStatus.Unknown && !string.IsNullOrEmpty(status.Name) && item.Name != status.Name && !options.RemoveOldMetadata)
        {
            _logger.LogDebug("Restoring audio name from status: {ItemName} to {StatusName}", item.Name, status.Name);
            item.Name = status.Name;
            return ItemUpdateType.ImageUpdate;
        }

        MusicBrainzAliasResolver aliasResolver = new MusicBrainzAliasResolver(item.Name, _logger);
        aliasResolver.SetRatioLimit(Plugin.Instance!.Configuration.Threshold);
        if (!aliasResolver.ShouldUpdate() && !options.RemoveOldMetadata)
        {
            return SyncStatusToItem(item, status, aliasResolver);
        }

        var currentRelease = await _musicBrainzQuery.LookupReleaseAsync(new Guid(releaseId), Include.None, cancellationToken).ConfigureAwait(false);
        if (!aliasResolver.ShouldUpdate() && !options.RemoveOldMetadata)
        {
            return SyncStatusToItem(item, status, aliasResolver);
        }

        var search = await _musicBrainzQuery.LookupRecordingAsync(new Guid(recordingId), Include.Aliases | Include.Media | Include.Releases, null, null, cancellationToken).ConfigureAwait(false);

        foreach (var alias in search.Aliases ?? [])
        {
            aliasResolver.AddString(alias.Name, currentRelease.TextRepresentation!.Script, currentRelease.TextRepresentation.Language);
        }

        foreach (var release in search.Releases ?? [])
        {
            foreach (var media in release.Media ?? [])
            {
                foreach (var track in media.Tracks ?? [])
                {
                    if (!string.IsNullOrEmpty(track.Title))
                    {
                        aliasResolver.AddString(track.Title, release.TextRepresentation!.Script, release.TextRepresentation.Language);
                    }
                }
            }
        }

        var result = await aliasResolver.GetValue().ConfigureAwait(false);
        _logger.LogDebug("Found audio name with value of: {Name}", result);
        if (result != item.Name)
        {
            item.Name = result;
            SyncStatusToItem(item, status, aliasResolver);
            return ItemUpdateType.MetadataEdit;
        }

        return SyncStatusToItem(item, status, aliasResolver);
    }

    private async Task<ItemUpdateType> UpdateArtistName(MusicArtist item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        if (!Plugin.Instance!.Configuration.DoArtist)
        {
            return ItemUpdateType.None;
        }

        var artistId = item.GetProviderId(MetadataProvider.MusicBrainzArtist);
        if (string.IsNullOrEmpty(artistId))
        {
            return ItemUpdateType.None;
        }

        var status = new MusicBrainzAliasMediaState(item);
        if (!status.ShouldUpdate(item) && !options.RemoveOldMetadata)
        {
            return ItemUpdateType.None;
        }

        _logger.LogDebug("Looking for info about artist: {Artist}:{ArtistId}", item.Name, artistId);

        MusicBrainzAliasResolver aliasResolver = new MusicBrainzAliasResolver(item.Name, _logger);
        aliasResolver.SetRatioLimit(Plugin.Instance!.Configuration.Threshold);
        if (!aliasResolver.ShouldUpdate() && !options.RemoveOldMetadata)
        {
            return SyncStatusToItem(item, status, aliasResolver);
        }

        var artist = await _musicBrainzQuery.LookupArtistAsync(new Guid(artistId), Include.Aliases, null, null, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(artist.Disambiguation))
        {
            aliasResolver.AddString(artist.Disambiguation, null, null);
        }

        foreach (var alias in artist.Aliases ?? [])
        {
            aliasResolver.AddString(alias.Name, null, MusicBrainzAliasResolver.LocalToLang(alias.Locale));
        }

        var result = await aliasResolver.GetValue().ConfigureAwait(false);
        _logger.LogDebug("Found artist name with value of: {Name}", result);
        if (result != item.Name)
        {
            item.Name = result;
            SyncStatusToItem(item, status, aliasResolver);
            return ItemUpdateType.MetadataEdit;
        }

        return SyncStatusToItem(item, status, aliasResolver);
    }

    private ItemUpdateType SyncStatusToItem<T>(T item, MusicBrainzAliasMediaState status, MusicBrainzAliasResolver aliasResolver)
    where T : BaseItem, IHasProviderIds
    {
        bool changed = false;
        if (item.Name != status.Name)
        {
            changed = true;
            status.Name = item.Name;
        }

        if (status.Status != aliasResolver.GetStatus())
        {
            changed = true;
            status.Status = aliasResolver.GetStatus();
        }

        if (!changed)
        {
            return ItemUpdateType.None;
        }

        status.UpdatedTime = DateTime.Now;
        status.Save(item);
        return ItemUpdateType.MetadataEdit;
    }

    /// <summary>
    /// Update album artist information if needed.
    /// </summary>
    /// <param name="item">album item.</param>
    /// <returns>the item update type.</returns>
    private ItemUpdateType SyncAlbumArtist(MusicAlbum item)
    {
        if (!Plugin.Instance!.Configuration.DoArtist)
        {
            return ItemUpdateType.None;
        }

        if (item.AlbumArtist == item.MusicArtist.Name || string.IsNullOrEmpty(item.MusicArtist.Name))
        {
            return ItemUpdateType.None;
        }

        _logger.LogDebug("Album doesnt container the album artist in artists list");
        // remove old album artist from album artists and artists list, replace with musicartist name
        var oldValue = item.AlbumArtist;
        var newValue = item.MusicArtist.Name;
        item.AlbumArtists = item.AlbumArtists.Select(x => x == oldValue ? newValue : x).ToList();
        item.Artists = item.Artists.Select(x => x == oldValue ? newValue : x).ToList();
        return ItemUpdateType.MetadataEdit;
    }

    /// <summary>
    /// Update audio artist information if needed.
    /// </summary>
    /// <param name="item">audio item.</param>
    /// <returns>the item update type.</returns>
    private ItemUpdateType SyncAudioArtist(Audio item)
    {
        if (!Plugin.Instance!.Configuration.DoArtist)
        {
            return ItemUpdateType.None;
        }

        if (item.AlbumArtists.Contains(item.AlbumEntity.AlbumArtist) || string.IsNullOrEmpty(item.AlbumEntity.AlbumArtist))
        {
            return ItemUpdateType.None;
        }

        _logger.LogDebug("Track doesn't container the album artist in artists list");
        // remove old album artist from album artists and artists list, replace with musicartist name
        var oldValue = item.AlbumArtists![0];
        var newValue = item.AlbumEntity.AlbumArtist;
        item.AlbumArtists = item.AlbumArtists.Select(x => x == oldValue ? newValue : x).ToList();
        item.Artists = item.Artists.Select(x => x == oldValue ? newValue : x).ToList();
        return ItemUpdateType.MetadataEdit;
    }

    /// <summary>
    /// Update audio album information if needed.
    /// </summary>
    /// <param name="item">audio item.</param>
    /// <returns>the item update type.</returns>
    private ItemUpdateType SyncAudioAlbum(Audio item)
    {
        if (!Plugin.Instance!.Configuration.DoAlbum)
        {
            return ItemUpdateType.None;
        }

        if (item.AlbumEntity.Name == item.Album)
        {
            return ItemUpdateType.None;
        }

        item.Album = item.AlbumEntity.Name;
        return ItemUpdateType.MetadataEdit;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose all resources.
    /// </summary>
    /// <param name="disposing">Whether to dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _musicBrainzQuery.Dispose();
        }
    }
}
