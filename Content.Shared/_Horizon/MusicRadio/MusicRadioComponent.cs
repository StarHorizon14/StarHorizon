using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.MusicRadio;

/// <summary>
/// A radio that plays the tracks of a sound collection in random order while it is switched on (see ItemToggle).
/// When the time of a leased wreck is running out it plays the tension collection instead.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MusicRadioComponent : Component
{
    [DataField(required: true)]
    public ProtoId<SoundCollectionPrototype> Collection;

    [DataField(required: true)]
    public ProtoId<SoundCollectionPrototype> TensionCollection;

    /// <summary>
    /// How long before a wreck lease ends the tension tracks start.
    /// </summary>
    [DataField]
    public TimeSpan TensionTime = TimeSpan.FromMinutes(10);

    [DataField]
    public float Volume = -6f;

    [DataField]
    public float MaxDistance = 12f;

    [ViewVariables]
    public EntityUid? Stream;

    [ViewVariables]
    public string? Last;

    [ViewVariables]
    public bool Tension;

    [ViewVariables]
    public TimeSpan NextCheck;
}
