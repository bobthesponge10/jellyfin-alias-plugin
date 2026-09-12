using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using Jellyfin.Plugin.AliasResolver.Configuration;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MetaBrainz.MusicBrainz;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AliasResolver;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IDisposable
{
    private readonly ILogger<Plugin> _logger;
    private readonly Lock _queryLock = new();
    private readonly IPluginManager _pluginManager;
    private Query _musicBrainzQuery;
    private bool _disposed;
    private IPlugin? _musicbrainzPlugin;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="applicationHost">Instance of the <see cref="IApplicationHost"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{Plugin}"/> interface.</param>
    /// <param name="pluginManager">Instance of the <see cref="IPluginManager"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, IApplicationHost applicationHost, ILogger<Plugin> logger, IPluginManager pluginManager)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _logger = logger;
        _pluginManager = pluginManager;

        Query.DefaultUserAgent.Add(new ProductInfoHeaderValue(applicationHost.Name.Replace(' ', '-'), applicationHost.ApplicationVersionString));
        Query.DefaultUserAgent.Add(new ProductInfoHeaderValue($"({applicationHost.ApplicationUserAgentAddress})"));

        ApplyServerConfig(Configuration);
        Query.DelayBetweenRequests = Configuration.RateLimit;
        _musicBrainzQuery = new Query();

        ConfigurationChanged += OnConfigurationChanged;
    }

    /// <inheritdoc />
    public override string Name => "Music Alias Resolver";

    /// <inheritdoc />
    public override string Description => "Checks if Albums, Tracks, and Artists have english aliases.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("1f2651b1-0793-4504-b1b1-5e4de18ba892");

    /// <summary>
    /// Gets path to the dictionary values.
    /// </summary>
    public string DictPath => Path.Combine(Path.GetDirectoryName(AssemblyFilePath) ?? DataFolderPath, "IpaDic");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the current MusicBrainz query client.
    /// </summary>
    /// <remarks>
    /// Always read this property anew before each request — the underlying instance is
    /// replaced when the server URL changes. Old instances are intentionally left alive
    /// so in-flight requests can finish; their unmanaged resources leak until GC.
    /// </remarks>
    public Query MusicBrainzQuery
    {
        get
        {
            if (_musicbrainzPlugin is null)
            {
                LocalPlugin? otherPlugin = _pluginManager.Plugins.FirstOrDefault(p => p.Name == "MusicBrainz");
                if (otherPlugin != null)
                {
                    _musicbrainzPlugin = otherPlugin.Instance;
                }
            }

            if (_musicbrainzPlugin is null)
            {
                return SelfMusicBrainzQuery;
            }

            // Use musicbrainz query if available to merge rate limit usage
            var propertyInfo = _musicbrainzPlugin.GetType().GetProperty("MusicBrainzQuery");

            if (propertyInfo != null && propertyInfo.CanRead)
            {
                var value = propertyInfo.GetMethod?.Invoke(_musicbrainzPlugin, null);
                if (value != null)
                {
                    try
                    {
                        Query query = (Query)value;
                        return query;
                    }
                    catch (InvalidCastException)
                    {
                        return SelfMusicBrainzQuery;
                    }
                }
            }

            return SelfMusicBrainzQuery;
        }
    }

    private Query SelfMusicBrainzQuery
    {
        get
        {
            lock (_queryLock)
            {
                return _musicBrainzQuery;
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and managed resources.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            ConfigurationChanged -= OnConfigurationChanged;
            lock (_queryLock)
            {
                _musicBrainzQuery.Dispose();
            }
        }

        _disposed = true;
    }

    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP003:Dispose previous before re-assigning", Justification = "The previous Query may still be in use by in-flight async requests; disposing it would cause ObjectDisposedException. The orphan is intentionally left for GC.")]
    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e)
    {
        var configuration = (PluginConfiguration)e;
        ApplyServerConfig(configuration);
        Query.DelayBetweenRequests = configuration.RateLimit;

        lock (_queryLock)
        {
            _musicBrainzQuery = new Query();
        }
    }

    private void ApplyServerConfig(PluginConfiguration configuration)
    {
        if (Uri.TryCreate(configuration.Server, UriKind.Absolute, out var server))
        {
            Query.DefaultServer = server.DnsSafeHost;
            Query.DefaultPort = server.Port;
            Query.DefaultUrlScheme = server.Scheme;
        }
        else
        {
            _logger.LogWarning("Invalid MusicBrainz server specified, falling back to official server");
            var defaultServer = new Uri(PluginConfiguration.DefaultServer);
            Query.DefaultServer = defaultServer.Host;
            Query.DefaultPort = defaultServer.Port;
            Query.DefaultUrlScheme = defaultServer.Scheme;
        }
    }
}
