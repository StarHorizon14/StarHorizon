using Content.Shared._Horizon.Planets.Procedural;

namespace Content.Server._Horizon.Planets;

public sealed class ProceduralPlanetSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MapPlanetBackgroundComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            Dirty(uid, comp);
        }
    }
}
