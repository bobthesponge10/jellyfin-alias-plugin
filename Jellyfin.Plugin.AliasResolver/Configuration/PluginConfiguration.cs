using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AliasResolver.Configuration;

/// <summary>
/// MusicBrainz plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// The default server URL.
    /// </summary>
    public const string DefaultServer = "https://musicbrainz.org";

    /// <summary>
    /// The default rate limit.
    /// </summary>
    public const double DefaultRateLimit = 1.0;

    private string _server = DefaultServer;

    private double _rateLimit = DefaultRateLimit;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        DoArtist = false;
        DoTrack = false;
        DoAlbum = false;
        Threshold = 0.80;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to run on automated scans.
    /// </summary>
    public bool Automatic { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether album artists should be processed.
    /// </summary>
    public bool DoArtist { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether tracks should be processed.
    /// </summary>
    public bool DoTrack { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether albums should be processed.
    /// </summary>
    public bool DoAlbum { get; set; }

    /// <summary>
    /// Gets or sets a value indicating the threshold for processing.
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// Gets or sets the server URL.
    /// </summary>
    public string Server
    {
        get => _server;

        set => _server = value.TrimEnd('/');
    }

    /// <summary>
    /// Gets or sets the rate limit.
    /// </summary>
    public double RateLimit
    {
        get => _rateLimit;
        set
        {
            if (value < DefaultRateLimit && _server == DefaultServer)
            {
                _rateLimit = DefaultRateLimit;
            }
            else
            {
                _rateLimit = value;
            }
        }
    }
}
