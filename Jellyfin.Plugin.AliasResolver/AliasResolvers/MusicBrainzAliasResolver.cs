using System;
using System.Globalization;
using System.Threading.Tasks;
using Kawazu;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AliasResolver;

/// <summary>
/// MusicBrainzAliasResolver.
/// </summary>
public class MusicBrainzAliasResolver
{
    private readonly ILogger _logger;

    private readonly string goalScript = "Latn";
    private readonly string goalLanguage = "eng";

    private double ratioLimit = 0.8;

    private MusicBrainzAliasItem currentValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicBrainzAliasResolver"/> class.
    /// </summary>
    /// <param name="startingValue">The initial value.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="script">The initial value script.</param>
    /// <param name="language">The initial value language.</param>
    public MusicBrainzAliasResolver(string startingValue, ILogger logger, string? script = null, string? language = null)
    {
        currentValue = new MusicBrainzAliasItem(startingValue, script, language);
        _logger = logger;
    }

    /// <summary>
    /// Sets the ratio limit.
    /// </summary>
    /// <param name="ratioLimit">The ratio limit.</param>
    public void SetRatioLimit(double? ratioLimit)
    {
        if (ratioLimit != null)
        {
            this.ratioLimit = (double)ratioLimit;
        }
    }

    /// <summary>
    /// Updated the current value if new value is a better match.
    /// </summary>
    /// <param name="newAlias">The new string to try.</param>
    /// <param name="script">The alias script.</param>
    /// <param name="language">The alias language.</param>
    /// <returns>If the new value is considered good.</returns>
    public bool AddString(string newAlias, string? script = null, string? language = null)
    {
        var newValue = new MusicBrainzAliasItem(newAlias, script, language);

        if (newValue.ValidCharPercent < currentValue.ValidCharPercent)
        {
            return false;
        }

        if (currentValue.Script == goalScript && newValue.Script != goalScript)
        {
            return false;
        }

        if (currentValue.Language == goalLanguage && newValue.Language != goalLanguage)
        {
            return false;
        }

        currentValue = newValue;

        return true;
    }

    /// <summary>
    /// Returns if the plugin considers the text readable.
    /// </summary>
    /// <returns>If the value is considered good.</returns>
    public bool GoodRatio()
    {
        return currentValue.ValidCharPercent > ratioLimit;
    }

    /// <summary>
    /// Returns if the plugin should try to update the text.
    /// </summary>
    /// <returns>If the value is considered good.</returns>
    public bool ShouldUpdate()
    {
        return !GoodRatio() && currentValue.Script != goalScript && currentValue.Language != goalLanguage;
    }

    /// <summary>
    /// Gets the current value of the alias resolver.
    /// </summary>
    /// <returns>Current Value.</returns>
    public async Task<string> GetValue()
    {
        if (!GoodRatio() && (currentValue.Script == "Jpan" || currentValue.Language == "jpn"))
        {
            using var converter = new KawazuConverter(Plugin.Instance!.DictPath);
            var a = await converter.Convert(currentValue.Value, To.Romaji, Mode.Spaced).ConfigureAwait(false);
            a = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(a).TrimEnd();
            _logger.LogDebug("Romanized {Start} to {End}", currentValue.Value, a);
            return a;
        }

        return currentValue.Value;
    }

    /// <summary>
    /// Gets the status from the current alias.
    /// </summary>
    /// <returns>The status.</returns>
    public MusicBrainzAliasStatus GetStatus()
    {
        if (GoodRatio() && (currentValue.Script == goalScript || currentValue.Language == goalLanguage))
        {
            return MusicBrainzAliasStatus.Good;
        }

        if (GoodRatio() || currentValue.Script == "Jpan" || currentValue.Language == "jpn")
        {
            return MusicBrainzAliasStatus.Medium;
        }

        return MusicBrainzAliasStatus.Bad;
    }

    /// <summary>
    /// Gets the current value of the alias resolver.
    /// </summary>
    /// <param name="local"> The input local string.</param>
    /// <returns>The language value.</returns>
    public static string? LocalToLang(string? local)
    {
        if (local == "en")
        {
            return "eng";
        }

        if (local == "jp")
        {
            return "jpn";
        }

        return null;
    }

    /// <summary>
    /// Gets the status from a given item.
    /// </summary>
    /// <param name="item">The item to get the status from.</param>
    /// <returns>The status.</returns>
    public static MusicBrainzAliasStatus GetItemStatus(IHasProviderIds item)
    {
        if (Enum.TryParse(item.GetProviderId(ProviderKeys.MusicBrainzAlias.ToString()), out MusicBrainzAliasStatus aliasStatus))
        {
            return aliasStatus;
        }

        return MusicBrainzAliasStatus.Unknown;
    }
}
