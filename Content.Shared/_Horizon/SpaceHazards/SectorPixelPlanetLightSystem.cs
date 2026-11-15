// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Robust.Shared.GameObjects;

namespace Content.Shared._Horizon.SpaceHazards;

public sealed class SectorPixelPlanetLightSystem : EntitySystem
{
    private const float StarRadiusMul = 10f;
    private const float StarEnergy = 9f;
    private const float BlackHoleRadiusMul = 7f;
    private const float BlackHoleEnergy = 5f;

    [Dependency] private readonly SharedPointLightSystem _lights = default!;

    public void ApplyCelestial(EntityUid uid, SectorCelestialBodyComponent body)
    {
        var light = _lights.EnsureLight(uid);
        var radius = MathF.Max(body.SpriteRadius, 1f);
        _lights.SetCastShadows(uid, false, light);
        _lights.SetEnabled(uid, true, light);
        _lights.SetSoftness(uid, 1.2f, light);
        if (body.Kind == CelestialKind.Star)
        {
            _lights.SetColor(uid, PixelPlanetLightColors.StarGlow(body.Palette), light);
            _lights.SetRadius(uid, radius * StarRadiusMul, light);
            _lights.SetEnergy(uid, StarEnergy, light);
            light.Offset = Vector2.Zero;
        }
        else
        {
            _lights.SetColor(uid, PixelPlanetLightColors.BlackHoleGlow(body.Palette), light);
            _lights.SetRadius(uid, radius * BlackHoleRadiusMul, light);
            _lights.SetEnergy(uid, BlackHoleEnergy, light);
            light.Offset = Vector2.Zero;
        }

        Dirty(uid, light);
    }

    /// <summary>Horizon: planets do not emit light, so there is nothing to set up.</summary>
    public void ApplyPlanet(EntityUid uid, SectorBackgroundPlanetComponent planet)
    {
    }
}
