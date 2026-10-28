using System.Text.RegularExpressions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.Shipbreaking.Derelict;

/// <summary>
/// How a shuttle is coloured on a minimap, in the format of starhorizon-map-render: rules are laid over each other in
/// order, and the last rule that fits wins. Tiles and entities are matched by the id of their prototype, a <c>*</c> in a
/// pattern stands for any characters, and the whole id has to fit the pattern.
/// </summary>
[Prototype("shuttleMinimap")]
public sealed partial class ShuttleMinimapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The colours of tiles. A rule without <see cref="ShuttleMinimapRule.Match"/> fits every tile.
    /// </summary>
    [DataField]
    public List<ShuttleMinimapRule> Tiles = new();

    /// <summary>
    /// The colours of what stands on the tiles. The rule that comes last among everything on a tile gives its colour,
    /// and it is laid over the colour of the tile.
    /// </summary>
    [DataField]
    public List<ShuttleMinimapRule> Entities = new();
}

[DataDefinition]
public sealed partial class ShuttleMinimapRule
{
    [DataField(required: true)]
    public Color Color;

    /// <summary>
    /// The patterns an id has to fit. Empty fits everything.
    /// </summary>
    [DataField]
    public List<string> Match = new();

    /// <summary>
    /// The patterns that take an id out again.
    /// </summary>
    [DataField]
    public List<string> Exclude = new();

    /// <summary>
    /// Whether the id fits the rule.
    /// </summary>
    public bool Fits(string id)
    {
        if (Match.Count > 0 && !Match.Exists(pattern => ShuttleMinimapPattern.IsMatch(pattern, id)))
            return false;

        return !Exclude.Exists(pattern => ShuttleMinimapPattern.IsMatch(pattern, id));
    }
}

public static class ShuttleMinimapPattern
{
    private static readonly Dictionary<string, Regex> Cache = new();

    public static bool IsMatch(string pattern, string id)
    {
        Regex? regex;
        lock (Cache)
        {
            if (!Cache.TryGetValue(pattern, out regex))
            {
                var escaped = Regex.Escape(pattern).Replace("\\*", ".*");
                regex = new Regex($"^{escaped}$", RegexOptions.CultureInvariant);
                Cache[pattern] = regex;
            }
        }

        return regex.IsMatch(id);
    }
}
