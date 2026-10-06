namespace Content.Shared._Horizon.Mirage;

/// <summary>
/// Marks the other side of a mirage. <see cref="MirageBorderComponent"/> with a matching
/// <see cref="MirageBorderComponent.TargetId"/> will show the area around this entity.
/// </summary>
[RegisterComponent]
public sealed partial class MirageAnchorComponent : Component
{
    [DataField(required: true)]
    public string Id = string.Empty;
}
