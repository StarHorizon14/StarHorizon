using Robust.Shared.Utility;

namespace Content.Client._Horizon.Marketplace;

/// <summary>
/// Connects item icons (deep inside cards) with the open marketplace UI that can ask the server
/// for full examine text, and caches the answers.
/// </summary>
public static class MarketplaceExamineHub
{
    private static readonly Dictionary<NetEntity, FormattedMessage> Cache = new();

    /// <summary>
    /// Set by the open terminal UI; sends the request to the server.
    /// </summary>
    public static Action<NetEntity>? Requester;

    /// <summary>
    /// Raised when the server sends examine text for an item.
    /// </summary>
    public static event Action<NetEntity, FormattedMessage>? Received;

    public static bool TryGetCached(NetEntity item, out FormattedMessage text)
    {
        return Cache.TryGetValue(item, out text!);
    }

    public static void Request(NetEntity item)
    {
        if (!Cache.ContainsKey(item))
            Requester?.Invoke(item);
    }

    public static void OnResponse(NetEntity item, FormattedMessage text)
    {
        Cache[item] = text;
        Received?.Invoke(item, text);
    }

    /// <summary>
    /// Items may change (e.g. stack size), so forget old answers whenever the market updates.
    /// </summary>
    public static void Clear()
    {
        Cache.Clear();
    }
}
