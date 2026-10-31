using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Horizon.Shipbreaking.Derelict;

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

    [DataField(required: true)]
    public ResPath ShuttlePath;

    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    [DataField]
    public int MinRank;

    [DataField]
    public ProtoId<ShuttleMinimapPrototype> Minimap = "Default";
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DerelictYardConsoleComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<DerelictVesselPrototype>> Vessels = new();
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DerelictLeaseComponent : Component
{
    public EntityUid Group;
}

[RegisterComponent]
public sealed partial class DerelictScrapOriginComponent : Component
{
    public EntityUid Group;
}

[RegisterComponent]
public sealed partial class DerelictYardRankComponent : Component
{
    public int Experience;

    public int Rank;

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

    public float Share;
}

[Serializable, NetSerializable]
public sealed class DerelictYardState : BoundUserInterfaceState
{
    public List<DerelictVesselEntry> Vessels = new();

    public int? Balance;

    public int Rank;
    public int Experience;

    public int? NextRankAt;

    public List<DerelictLeaseEntry> Leases = new();
}

[Serializable, NetSerializable]
public sealed class DerelictFinishMessage : BoundUserInterfaceMessage
{
    public NetEntity Group;

    public DerelictFinishMessage(NetEntity group)
    {
        Group = group;
    }
}

[Serializable, NetSerializable]
public sealed class DerelictPurchaseMessage : BoundUserInterfaceMessage
{
    public string Id;

    public DerelictPurchaseMessage(string id)
    {
        Id = id;
    }
}

[Serializable, NetSerializable]
public sealed class DerelictPreviewRequestMessage : BoundUserInterfaceMessage
{
    public string Id;

    public DerelictPreviewRequestMessage(string id)
    {
        Id = id;
    }
}

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
