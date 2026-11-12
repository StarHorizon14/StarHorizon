// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Horizon.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Horizon.SpaceHazards;

/// <summary>
/// Per-kind shader id and parameter setup shared by the world overlay and UI controls.
/// Palette colors are applied separately via <see cref="PixelPlanetPalettes"/>.
/// </summary>
public static class PixelPlanetShaderSetup
{
    public const float AnimSpeedScale = 0.15f;

    public static ProtoId<ShaderPrototype> ShaderId(PixelPlanetKind kind)
    {
        return kind switch
        {
            PixelPlanetKind.LandRivers => "PixelLandRivers",
            PixelPlanetKind.TerranDry => "PixelTerranDry",
            PixelPlanetKind.LandMasses => "PixelLandMasses",
            PixelPlanetKind.NoAtmosphere => "PixelNoAtmosphere",
            PixelPlanetKind.GasPlanet => "PixelGasPlanet",
            PixelPlanetKind.GasPlanetLayers => "PixelGasPlanetLayers",
            PixelPlanetKind.IceWorld => "PixelIceWorld",
            PixelPlanetKind.LavaWorld => "PixelLavaWorld",
            _ => "PixelLandRivers",
        };
    }

    public static void Configure(
        ShaderInstance s,
        PixelPlanetKind kind,
        float time,
        float pixels,
        float seed,
        float rotation,
        Vector2 light)
    {
        switch (kind)
        {
            case PixelPlanetKind.LandRivers:
            {
                SetCommonPlanet(s, time, pixels, seed, rotation, 0.2f * AnimSpeedScale, light);
                s.SetParameter("size", 4.6f);
                s.SetParameter("octaves", 6f);
                s.SetParameter("river_cutoff", 0.368f);
                s.SetParameter("dither_size", 0f);
                s.SetParameter("light_border_1", 0.4f);
                s.SetParameter("light_border_2", 0.5f);
                s.SetParameter("cloud_cover", 0.47f);
                s.SetParameter("stretch", 2f);
                s.SetParameter("cloud_curve", 1.3f);
                s.SetParameter("cloud_size", 7.315f);
                s.SetParameter("cloud_octaves", 2f);
                s.SetParameter("cloud_seed", seed + 1.5f);
                s.SetParameter("time_speed_clouds", 0.1f * AnimSpeedScale);
                break;
            }
            case PixelPlanetKind.TerranDry:
            {
                SetCommonPlanet(s, time, pixels, seed, rotation, 0.1f * AnimSpeedScale, light);
                s.SetParameter("size", 8f);
                s.SetParameter("octaves", 3f);
                s.SetParameter("dither_size", 0f);
                s.SetParameter("light_distance1", 0.362f);
                s.SetParameter("light_distance2", 0.525f);
                break;
            }
            case PixelPlanetKind.LandMasses:
            {
                SetCommonPlanet(s, time, pixels, seed, rotation, 0.1f * AnimSpeedScale, light);
                s.SetParameter("size", 5.228f);
                s.SetParameter("octaves", 3f);
                s.SetParameter("dither_size", 0f);
                s.SetParameter("light_border_1", 0.4f);
                s.SetParameter("light_border_2", 0.6f);
                s.SetParameter("land_cutoff", 0.633f);
                s.SetParameter("land_size", 4.292f);
                s.SetParameter("land_octaves", 6f);
                s.SetParameter("land_seed", seed + 2f);
                s.SetParameter("land_time_speed", 0.2f * AnimSpeedScale);
                s.SetParameter("land_light_border_1", 0.32f);
                s.SetParameter("land_light_border_2", 0.534f);
                s.SetParameter("cloud_cover", 0.515f);
                s.SetParameter("stretch", 2f);
                s.SetParameter("cloud_curve", 1.3f);
                s.SetParameter("cloud_size", 7.745f);
                s.SetParameter("cloud_octaves", 2f);
                s.SetParameter("cloud_seed", seed + 1.5f);
                s.SetParameter("time_speed_clouds", 0.1f * AnimSpeedScale);
                break;
            }
            case PixelPlanetKind.NoAtmosphere:
            {
                SetCommonPlanet(s, time, pixels, seed, rotation, 0.4f * AnimSpeedScale, light);
                s.SetParameter("size", 8f);
                s.SetParameter("octaves", 4f);
                s.SetParameter("dither_size", 0f);
                s.SetParameter("light_border_1", 0.615f);
                s.SetParameter("light_border_2", 0.729f);
                s.SetParameter("crater_size", 5f);
                s.SetParameter("crater_seed", seed + 3f);
                s.SetParameter("crater_light_border", 0.465f);
                break;
            }
            case PixelPlanetKind.GasPlanet:
            {
                SetCommonPlanet(s, time, pixels, seed, rotation, 0.7f * AnimSpeedScale, light);
                s.SetParameter("size", 9f);
                s.SetParameter("octaves", 5f);
                s.SetParameter("cloud_cover", 0.538f);
                s.SetParameter("stretch", 1f);
                s.SetParameter("cloud_curve", 1.3f);
                s.SetParameter("light_border_1", 0.439f);
                s.SetParameter("light_border_2", 0.746f);
                s.SetParameter("inner_cover", 0f);
                s.SetParameter("inner_time_speed", 0.7f * AnimSpeedScale);
                s.SetParameter("inner_stretch", 1f);
                s.SetParameter("inner_curve", 1.3f);
                s.SetParameter("inner_border_1", 0.52f);
                s.SetParameter("inner_border_2", 0.62f);
                s.SetParameter("outer_time_speed", 0.7f * AnimSpeedScale);
                break;
            }
            case PixelPlanetKind.GasPlanetLayers:
            {
                SetCommonPlanet(s, time, pixels, seed, rotation, 0.05f * AnimSpeedScale, light);
                s.SetParameter("size", 8f);
                s.SetParameter("octaves", 8f);
                s.SetParameter("dither_size", 0f);
                s.SetParameter("bands", 1f);
                s.SetParameter("ring_width", 0.127f);
                s.SetParameter("ring_perspective", 6f);
                s.SetParameter("ring_scale", 6f);
                s.SetParameter("ring_rotation", -0.5f);
                s.SetParameter("ring_time_speed", 0.2f * AnimSpeedScale);
                s.SetParameter("ring_size", 5f);
                s.SetParameter("ring_octaves", 4f);
                s.SetParameter("ring_seed", seed + 4f);
                s.SetParameter("full_scale", 1f);
                break;
            }
            case PixelPlanetKind.IceWorld:
            {
                SetCommonPlanet(s, time, pixels, seed, rotation, 0.2f * AnimSpeedScale, light);
                s.SetParameter("size", 4.6f);
                s.SetParameter("octaves", 6f);
                s.SetParameter("dither_size", 0f);
                s.SetParameter("light_border_1", 0.4f);
                s.SetParameter("light_border_2", 0.5f);
                s.SetParameter("lake_cutoff", 0.55f);
                s.SetParameter("lake_size", 10f);
                s.SetParameter("lake_seed", seed + 2f);
                s.SetParameter("lake_border_1", 0.024f);
                s.SetParameter("lake_border_2", 0.047f);
                s.SetParameter("cloud_cover", 0.546f);
                s.SetParameter("stretch", 2.5f);
                s.SetParameter("cloud_curve", 1.3f);
                s.SetParameter("cloud_size", 4f);
                s.SetParameter("cloud_octaves", 4f);
                s.SetParameter("cloud_seed", seed + 1.5f);
                s.SetParameter("time_speed_clouds", 0.1f * AnimSpeedScale);
                break;
            }
            case PixelPlanetKind.LavaWorld:
            {
                SetCommonPlanet(s, time, pixels, seed, rotation, 0.2f * AnimSpeedScale, light);
                s.SetParameter("size", 10f);
                s.SetParameter("octaves", 3f);
                s.SetParameter("dither_size", 0f);
                s.SetParameter("light_border_1", 0.4f);
                s.SetParameter("light_border_2", 0.6f);
                s.SetParameter("crater_size", 3.5f);
                s.SetParameter("crater_seed", seed + 3f);
                s.SetParameter("crater_time_speed", 0.09f * AnimSpeedScale);
                s.SetParameter("crater_light_border", 0.4f);
                s.SetParameter("lava_cutoff", 0.579f);
                s.SetParameter("lava_size", 10f);
                s.SetParameter("lava_octaves", 4f);
                s.SetParameter("lava_seed", seed + 5f);
                s.SetParameter("lava_time_speed", 0.2f * AnimSpeedScale);
                s.SetParameter("lava_border_1", 0.019f);
                s.SetParameter("lava_border_2", 0.036f);
                s.SetParameter("lava_detail", 1f);
                break;
            }
        }
    }

    private static void SetCommonPlanet(
        ShaderInstance shader,
        float time,
        float pixels,
        float seed,
        float rotation,
        float timeSpeed,
        Vector2 lightOrigin)
    {
        shader.SetParameter("time", time);
        shader.SetParameter("pixels", pixels);
        shader.SetParameter("seed", seed);
        shader.SetParameter("rotation", rotation);
        shader.SetParameter("time_speed", timeSpeed);
        shader.SetParameter("light_origin", lightOrigin);
    }
}
