// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared._Horizon.SpaceHazards;
using Robust.Server.GameStates;
using Robust.Shared.Map.Components;

namespace Content.Server._Horizon.SpaceHazards;

/// <summary>
/// Keeps sector celestial bodies and background planets parented to the map (never a grid)
/// and sends them to every client, since they sit far outside normal PVS range.
/// </summary>
public sealed class SectorLandmarkAnchorSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SectorCelestialBodyComponent, EntParentChangedMessage>(OnCelestialParent);
        SubscribeLocalEvent<SectorBackgroundPlanetComponent, EntParentChangedMessage>(OnPlanetParent);
        SubscribeLocalEvent<SectorCelestialBodyComponent, ComponentStartup>(OnCelestialStartup);
        SubscribeLocalEvent<SectorBackgroundPlanetComponent, ComponentStartup>(OnPlanetStartup);
        SubscribeLocalEvent<ExpeditionPlanetComponent, EntParentChangedMessage>(OnExpeditionPlanetParent);
        SubscribeLocalEvent<ExpeditionPlanetComponent, ComponentStartup>(OnExpeditionPlanetStartup);
    }

    private void OnCelestialParent(EntityUid uid, SectorCelestialBodyComponent _, ref EntParentChangedMessage args)
        => LockToMap(uid);

    private void OnPlanetParent(EntityUid uid, SectorBackgroundPlanetComponent _, ref EntParentChangedMessage args)
        => LockToMap(uid);

    private void OnExpeditionPlanetParent(EntityUid uid, ExpeditionPlanetComponent _, ref EntParentChangedMessage args)
        => LockToMap(uid);

    private void OnExpeditionPlanetStartup(EntityUid uid, ExpeditionPlanetComponent _, ComponentStartup args)
        => _pvs.AddGlobalOverride(uid);

    private void OnCelestialStartup(EntityUid uid, SectorCelestialBodyComponent _, ComponentStartup args)
        => _pvs.AddGlobalOverride(uid);

    private void OnPlanetStartup(EntityUid uid, SectorBackgroundPlanetComponent _, ComponentStartup args)
        => _pvs.AddGlobalOverride(uid);

    public void LockToMap(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } mapUid || xform.ParentUid == mapUid)
            return;

        if (HasComp<MapGridComponent>(xform.ParentUid) || xform.GridUid != null)
            _transform.SetParent(uid, xform, mapUid);
    }
}
