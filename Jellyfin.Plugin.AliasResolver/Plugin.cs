using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using Jellyfin.Plugin.AliasResolver.Configuration;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MetaBrainz.MusicBrainz;

namespace Jellyfin.Plugin.AliasResolver;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="applicationHost">Instance of the <see cref="IApplicationHost"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, IApplicationHost applicationHost)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        Query.DefaultUserAgent.Add(new ProductInfoHeaderValue(applicationHost.Name.Replace(' ', '-'), applicationHost.ApplicationVersionString));
        Query.DefaultUserAgent.Add(new ProductInfoHeaderValue($"({applicationHost.ApplicationUserAgentAddress})"));
        Query.DelayBetweenRequests = Instance.Configuration.RateLimit;
        Query.DefaultServer = Instance.Configuration.Server;
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

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
        };
    }
}
