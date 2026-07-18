namespace Content.Client._Horizon.GhostTrail;

/// <summary>
/// Маркерный компонент для клонов-следов GhostTrail.
/// Используется для отслеживания количества активных следов.
/// </summary>
[RegisterComponent]
[Access(typeof(GhostTrailSystem))]
public sealed partial class GhostTrailCloneComponent : Component
{
    /// <summary>
    /// EntityUid источника, от которого создан этот след.
    /// </summary>
    public EntityUid SourceEntity;
}
