using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Goobstation.SlotMachine;

/// <summary>
/// Prototype for the slotmachine and claw machine prizes and losses
/// </summary>
[Prototype]
public sealed partial class PrizePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The chance to win this prize
    /// </summary>
    [DataField(required: true)]
    public float Weight;

    /// <summary>
    /// The entity table to spawn when the prize is won
    /// </summary>
    [DataField]
    public EntityTableSelector? PrizeTable;

    [DataField]
    public LocId? WinMessage;

    [DataField]
    public AnnounceType AnnounceType = AnnounceType.Speak;

    [DataField]
    public SoundSpecifier WinSound = new SoundPathSpecifier("/Audio/Effects/Arcade/win.ogg");
}

public enum AnnounceType : byte
{
    Speak,
    Popup,
}
