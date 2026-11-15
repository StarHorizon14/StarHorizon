// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Shared._Horizon.SpaceHazards;
using Robust.Shared.Prototypes;

namespace Content.Server._Horizon.SpaceHazards;

[RegisterComponent]
public sealed partial class SectorCelestialPlacerControllerComponent : Component
{
    /// <summary>Black holes to place. Two by default; they are kept very far from each other.</summary>
    [DataField]
    public int BlackHoleCount = 2;

    [DataField]
    public int StarCount = 1;

    [DataField]
    public float MinDistance = 8000f;

    [DataField]
    public float MaxDistance = 14000f;

    /// <summary>Minimum distance between two black holes.</summary>
    [DataField]
    public float BlackHoleSeparation = 18000f;

    /// <summary>Minimum distance between any other pair of sector bodies.</summary>
    [DataField]
    public float BodySeparation = 8000f;

    /// <summary>Keep this far from planets and other already placed bodies.</summary>
    [DataField]
    public float Clearance = 3000f;

    [DataField]
    public CelestialKind? ForceKind;

    public EntProtoId StarPrototype = "SectorCelestialStar";

    public EntProtoId BlackHolePrototype = "SectorCelestialBlackHole";

    [DataField]
    public bool Spawned;
}
