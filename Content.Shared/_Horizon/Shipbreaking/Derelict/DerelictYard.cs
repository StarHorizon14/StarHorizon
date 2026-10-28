using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Horizon.Shipbreaking.Derelict;

/// <summary>
/// A derelict shuttle sold by the derelict yard. The yard takes <see cref="Price"/> from the station account;
/// what is made of the wreck by the shipbreaking machines pays back into the same account.
/// </summary>
[Prototype("derelictVessel")]
public sealed partial class DerelictVesselPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    [DataField(required: true)]
    public int Price;

    /// <summary>
    /// A grid file with a single shuttle grid.
    /// </summary>
    [DataField(required: true)]
    public ResPath ShuttlePath;

    /// <summary>
    /// How long the shuttle lasts after it was bought.
    /// </summary>
    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The rank of the yard needed to buy this wreck.
    /// </summary>
    [DataField]
    public int MinRank;

    /// <summary>
    /// How the preview of the wreck is coloured.
    /// </summary>
    [DataField]
    public ProtoId<ShuttleMinimapPrototype> Minimap = "Default";
}

/// <summary>
/// The console of the derelict yard. Opens <see cref="DerelictYardUiKey.Key"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DerelictYardConsoleComponent : Component
{
    /// <summary>
    /// The wrecks the console offers.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<DerelictVesselPrototype>> Vessels = new();
}

/// <summary>
/// On the grid of a bought derelict and on every piece cut off it, pointing at the grid that was bought. The job of
/// the wreck (time, appraisal, revenue) is kept by the derelict yard system under that grid.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DerelictLeaseComponent : Component
{
    public EntityUid Group;
}

/// <summary>
/// On everything that was on a leased wreck when it was bought, wherever it ends up. What the scrap machines pay for
/// it counts towards the job of that wreck.
/// </summary>
[RegisterComponent]
public sealed partial class DerelictScrapOriginComponent : Component
{
    public EntityUid Group;
}

/// <summary>
/// The rank of the derelict yard of a station, for the round. It grows with the revenue of finished jobs
/// and opens the bigger wrecks.
/// </summary>
[RegisterComponent]
public sealed partial class DerelictYardRankComponent : Component
{
    public int Experience;

    public int Rank;

    /// <summary>
    /// Experience needed for each rank, the first one is always 0.
    /// </summary>
    public static readonly int[] Thresholds = { 0, 15000, 50000, 120000 };
}

[Serializable, NetSerializable]
public enum DerelictYardUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DerelictVesselEntry
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Price;
    public TimeSpan Lifetime;
    public int MinRank;
    public bool Locked;
}

[Serializable, NetSerializable]
public sealed class DerelictLeaseEntry
{
    public NetEntity Group;
    public string Name = string.Empty;
    public TimeSpan Left;

    /// <summary>
    /// Revenue against the appraisal, 0 to 1 and more.
    /// </summary>
    public float Share;

    public bool CanExtend;
    public int ExtendPrice;
}

[Serializable, NetSerializable]
public sealed class DerelictYardState : BoundUserInterfaceState
{
    public List<DerelictVesselEntry> Vessels = new();

    /// <summary>
    /// The balance of the station account, null if the console has no station.
    /// </summary>
    public int? Balance;

    public int Rank;
    public int Experience;

    /// <summary>
    /// The experience needed for the next rank, null at the top.
    /// </summary>
    public int? NextRankAt;

    public List<DerelictLeaseEntry> Leases = new();
}

/// <summary>
/// The client wants to extend a lease.
/// </summary>
[Serializable, NetSerializable]
public sealed class DerelictExtendMessage : BoundUserInterfaceMessage
{
    public NetEntity Group;

    public DerelictExtendMessage(NetEntity group)
    {
        Group = group;
    }
}

/// <summary>
/// The client wants to buy a wreck.
/// </summary>
[Serializable, NetSerializable]
public sealed class DerelictPurchaseMessage : BoundUserInterfaceMessage
{
    public string Id;

    public DerelictPurchaseMessage(string id)
    {
        Id = id;
    }
}

/// <summary>
/// The client wants the preview of a wreck.
/// </summary>
[Serializable, NetSerializable]
public sealed class DerelictPreviewRequestMessage : BoundUserInterfaceMessage
{
    public string Id;

    public DerelictPreviewRequestMessage(string id)
    {
        Id = id;
    }
}

/// <summary>
/// A view from above on the shuttle: one coloured cell per tile.
/// </summary>
[Serializable, NetSerializable]
public sealed class DerelictPreviewMessage : BoundUserInterfaceMessage
{
    public string Id;
    public Vector2i[] Tiles;
    public Color[] Colors;

    public DerelictPreviewMessage(string id, Vector2i[] tiles, Color[] colors)
    {
        Id = id;
        Tiles = tiles;
        Colors = colors;
    }
}
