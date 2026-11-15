using System.Text.RegularExpressions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.Shipbreaking.Derelict;

/// How a shuttle is coloured on a minimap, in the format of starhorizon-map-render: rules are laid over each other in
[Prototype("shuttleMinimap")]
public sealed partial class ShuttleMinimapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ShuttleMinimapRule> Tiles = new();

    [DataField]
    public List<ShuttleMinimapRule> Entities = new();
}

[DataDefinition]
public sealed partial class ShuttleMinimapRule
{
    [DataField(required: true)]
    public Color Color;

    [DataField]
    public List<string> Match = new();

    [DataField]
    public List<string> Exclude = new();

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
