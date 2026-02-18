using System;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.AliasResolver;

/// <summary>
/// MusicBrainzAliasMediaState.
/// </summary>
public class MusicBrainzAliasMediaState
{
    private string delimiter = "|";

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicBrainzAliasMediaState"/> class.
    /// </summary>
    /// <param name="item">The item to get the status of.</param>
    public MusicBrainzAliasMediaState(IHasProviderIds item)
    {
        string raw_status = item.GetProviderId(ProviderKeys.MusicBrainzAlias.ToString()) ?? string.Empty;
        string[] split_status = raw_status.Split(delimiter, 3);

        if (Enum.TryParse(split_status[0], out MusicBrainzAliasStatus status_result))
        {
            Status = status_result;
        }
        else
        {
            Status = MusicBrainzAliasStatus.Unknown;
        }

        if (split_status.Length > 1 && DateTime.TryParse(split_status[1], out DateTime datetime_result))
        {
            UpdatedTime = datetime_result;
        }
        else
        {
            UpdatedTime = null;
        }

        if (split_status.Length > 2 && split_status[2].Trim().Length > 0)
        {
            Name = split_status[2];
        }
        else
        {
            Name = null;
        }
    }

    /// <summary>
    /// Gets or Sets the current status.
    /// </summary>
    public MusicBrainzAliasStatus Status { get; set; }

    /// <summary>
    /// Gets or Sets the current update time.
    /// </summary>
    public DateTime? UpdatedTime { get; set; }

    /// <summary>
    /// Gets or Sets the current name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets the state string.
    /// </summary>
    /// <returns>The state string.</returns>
    public string AsString()
    {
        return Status.ToString() + delimiter + (UpdatedTime!.ToString() ?? string.Empty) + delimiter + (Name ?? string.Empty);
    }

    /// <summary>
    /// Gets if the item is missing data or has incorrect data.
    /// </summary>
    /// <param name="item"> the item to check.</param>
    /// <returns> boolean if item should be updated or not. </returns>
    public bool ShouldUpdate(BaseItem item)
    {
        if (Status == MusicBrainzAliasStatus.Unknown)
        {
            return true;
        }

        if (Name != item.Name)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the state string.
    /// </summary>
    /// <param name="item"> the item to save to.</param>
    public void Save(IHasProviderIds item)
    {
        item.TrySetProviderId(ProviderKeys.MusicBrainzAlias.ToString(), AsString());
    }
}
