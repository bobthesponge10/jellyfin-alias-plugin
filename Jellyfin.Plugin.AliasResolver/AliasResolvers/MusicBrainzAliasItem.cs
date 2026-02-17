namespace Jellyfin.Plugin.AliasResolver;

/// <summary>
/// MusicBrainzAliasItem.
/// </summary>
public class MusicBrainzAliasItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MusicBrainzAliasItem"/> class.
    /// </summary>
    /// <param name="value">the text being compared.</param>
    /// <param name="script">the script of the text.</param>
    /// <param name="language">the language of the text.</param>
    public MusicBrainzAliasItem(string value, string? script = null, string? language = null)
    {
        this.Value = value;
        this.Script = script;
        this.Language = language;
    }

    /// <summary>
    /// Gets the value of the alias.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the script of the alias.
    /// </summary>
    public string? Script { get; }

    /// <summary>
    /// Gets the language of the alias.
    /// </summary>
    public string? Language { get; }

    /// <summary>
    /// Gets the percent as a double of readable characters.
    /// </summary>
    public double ValidCharPercent
    {
        get
        {
            if (Value.Length == 0)
            {
                return 0.0;
            }

            int validChars = 0;

            foreach (char c in Value)
            {
                if (c >= 0x0020 && c <= 0x007E)
                {
                    validChars += 1;
                }
            }

            return (double)validChars / Value.Length;
            }
    }
}
