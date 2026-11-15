using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;

namespace Content.Shared._Horizon.GravityGun;

[RegisterComponent, NetworkedComponent]
public sealed partial class GridDragGunComponent : Component
{
    [DataField]
    public float MaxDistance = 15f;

    [DataField]
    public int MaxTiles = 9;

    [DataField]
    public bool RequireUtilization;

    [DataField]
    public float Gain = 4f;

    [DataField]
    public float Blend = 0.08f;

    [DataField]
    public float MaxSpeed = 20f;

    [DataField]
    public Color LineColor = Color.Orange;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GridDraggedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Gun;

    public TimeSpan LastRequest;

    public EntityUid? Stream;
}

public abstract class SharedGridDragGunSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;

    public bool CanGrab(EntityUid user, GridDragGunComponent gun, EntityUid grid)
    {
        if (TerminatingOrDeleted(grid) || !TryComp<MapGridComponent>(grid, out var comp) || Transform(user).GridUid == grid ||
            (gun.RequireUtilization && !HasComp<Shipbreaking.Derelict.ShuttleUtilizationComponent>(grid)))
            return false;

        var tiles = 0;
        foreach (var _ in _maps.GetAllTiles(grid, comp))
        {
            if (++tiles > gun.MaxTiles)
                return false;
        }

        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var uid, out var mob, out var xform))
        {
            if (xform.GridUid == grid && _mobState.IsAlive(uid, mob))
                return false;
        }

        return true;
    }
}
