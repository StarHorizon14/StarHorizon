using Robust.Shared.Audio;

namespace Content.Server._Horizon.ShipDrill;

[RegisterComponent]
public sealed partial class ShipDrillComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public bool Powered = false;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("soundHit")]
    public SoundSpecifier? HitSound;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float Accumulator = 0f;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float Cooldown = 0.2f;
}
