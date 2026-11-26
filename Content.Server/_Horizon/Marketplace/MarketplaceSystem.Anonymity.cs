using Content.Shared._Horizon.Marketplace;
using Robust.Shared.Network;

namespace Content.Server._Horizon.Marketplace;

/// <summary>
/// On anonymous networks (the black market) traders are shown as codes. Real names never leave the server;
/// admin logs still record them.
/// </summary>
public sealed partial class MarketplaceSystem
{
    /// <summary>
    /// A code that stays the same for the same trader during the round, so buyers can still tell repeat sellers apart.
    /// </summary>
    private static string GetCode(MarketplaceStorageComponent storage, string key)
    {
        var hash = (uint) HashCode.Combine(storage.Salt, key);
        var text = hash.ToString("X8");
        return $"{text[..4]}-{text[4..6]}";
    }

    private string DisplayName(bool anonymous, MarketplaceStorageComponent storage, NetUserId userId, string characterName)
    {
        return anonymous
            ? Loc.GetString("marketplace-anon-name", ("code", GetCode(storage, $"trader|{userId}|{characterName}")))
            : characterName;
    }

    private string DisplayIdName(bool anonymous, string idName)
    {
        return anonymous ? Loc.GetString("marketplace-anon-id") : idName;
    }

    private string DisplayGrid(bool anonymous, MarketplaceStorageComponent storage, string gridName)
    {
        return anonymous
            ? Loc.GetString("marketplace-anon-grid", ("code", GetCode(storage, $"grid|{gridName}")))
            : gridName;
    }
}
