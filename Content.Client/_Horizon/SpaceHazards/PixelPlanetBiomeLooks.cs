// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Robust.Client.Graphics;
using Content.Shared._Horizon.SpaceHazards;

namespace Content.Client._Horizon.SpaceHazards;

/// <summary>
/// Shader kind, palette and tuned shader parameters for a biome planet.
/// </summary>
/// <param name="Palette">Colors in the slot order of <see cref="PixelPlanetPalettes.GetPalette"/>.</param>
/// <param name="Overrides">Shader float parameters applied after <see cref="PixelPlanetShaderSetup.Configure"/>.</param>
public readonly record struct PixelPlanetLook(
    PixelPlanetKind Kind,
    Vector3[] Palette,
    IReadOnlyDictionary<string, float> Overrides);

/// <summary>
/// Biome planet presets for UI previews and the in-world expedition planets. The looks are the Pixel Planets shaders
/// that the expedition planet sprites were rendered with: palette colors come from the sprites, and the noise
/// parameters were fitted so color shares, edge density, blob count/size and light direction match them.
/// </summary>
public static class PixelPlanetBiomeLooks
{
    private static readonly Dictionary<string, PixelPlanetLook> Presets = CreatePresets();

    private static Dictionary<string, PixelPlanetLook> CreatePresets()
    {
        var presets = new Dictionary<string, PixelPlanetLook>
        {
            ["Grasslands"] = new(PixelPlanetKind.LandMasses,
            [
                H("#4fa4b8"),
                H("#4fa4b8"),
                H("#283540"),
                H("#63ab3f"),
                H("#1b832d"),
                H("#38733b"),
                H("#38733b"),
                H("#dfe0e8"),
                H("#686f99"),
                H("#404973"),
            ],
            new Dictionary<string, float>
            {
                ["cloud_cover"] = 0.7068f,
                ["cloud_curve"] = 1.9583f,
                ["cloud_octaves"] = 3.4337f,
                ["cloud_size"] = 9.7514f,
                ["dither_size"] = 2.2759f,
                ["land_cutoff"] = 0.4259f,
                ["land_light_border_1"] = 0.3387f,
                ["land_light_border_2"] = 0.481f,
                ["land_octaves"] = 4.2099f,
                ["land_size"] = 4.8653f,
                ["light_border_1"] = 0.2299f,
                ["light_border_2"] = 0.7916f,
                ["octaves"] = 2.6943f,
                ["size"] = 9.7386f,
                ["stretch"] = 2.8768f,
            }),
            ["Lava"] = new(PixelPlanetKind.LavaWorld,
            [
                H("#8f4d57"),
                H("#52333f"),
                H("#3d2936"),
                H("#52333f"),
                H("#3d2936"),
                H("#ff8933"),
                H("#e64539"),
                H("#ad2f45"),
            ],
            new Dictionary<string, float>
            {
                ["crater_light_border"] = 0.4614f,
                ["crater_size"] = 3.0396f,
                ["dither_size"] = 1.2645f,
                ["lava_border_1"] = 0.0263f,
                ["lava_border_2"] = 0.25f,
                ["lava_cutoff"] = 0.5379f,
                ["lava_detail"] = 6.537f,
                ["lava_octaves"] = 3.7587f,
                ["lava_size"] = 12.9111f,
                ["light_border_1"] = 0.4307f,
                ["light_border_2"] = 0.4674f,
                ["octaves"] = 4.7949f,
                ["size"] = 13.1539f,
            }),
            ["Snow"] = new(PixelPlanetKind.IceWorld,
            [
                H("#dfe7f2"),
                H("#d8dcd7"),
                H("#bfbfbd"),
                H("#aed1dd"),
                H("#a4d2f4"),
                H("#8dafc7"),
                H("#dfe7f2"),
                H("#bfc3e6"),
                H("#d8dcd7"),
                H("#bfc3e6"),
            ],
            new Dictionary<string, float>
            {
                ["cloud_cover"] = 0.6476f,
                ["cloud_octaves"] = 4.535f,
                ["cloud_size"] = 7.9219f,
                ["dither_size"] = 1.7386f,
                ["lake_border_1"] = 0.0701f,
                ["lake_border_2"] = 0.0293f,
                ["lake_cutoff"] = 0.5205f,
                ["lake_size"] = 7.0413f,
                ["light_border_1"] = 0.5258f,
                ["light_border_2"] = 0.6138f,
                ["octaves"] = 7f,
                ["size"] = 8.8021f,
                ["stretch"] = 2.7274f,
            }),
            ["LowDesert"] = new(PixelPlanetKind.LandMasses,
            [
                H("#91a3b9"),
                H("#6d75a1"),
                H("#6d75a1"),
                H("#ceb37d"),
                H("#947c60"),
                H("#a16c48"),
                H("#9c5541"),
                H("#dae8e7"),
                H("#b6cad0"),
                H("#908b8e"),
            ],
            new Dictionary<string, float>
            {
                ["cloud_cover"] = 0.527f,
                ["cloud_curve"] = 1.7152f,
                ["cloud_octaves"] = 4.0023f,
                ["cloud_size"] = 7.5954f,
                ["dither_size"] = 0.9568f,
                ["land_cutoff"] = 0.4f,
                ["land_light_border_1"] = 0.5108f,
                ["land_light_border_2"] = 0.5403f,
                ["land_octaves"] = 7.9095f,
                ["land_size"] = 6.2366f,
                ["light_border_1"] = 0.6373f,
                ["light_border_2"] = 0.9281f,
                ["octaves"] = 4.0945f,
                ["size"] = 8.4319f,
                ["stretch"] = 1.4276f,
            }),
            ["Shadow"] = new(PixelPlanetKind.LavaWorld,
            [
                H("#191a23"),
                H("#17101d"),
                H("#0f091b"),
                H("#27191f"),
                H("#220f25"),
                H("#3f245f"),
                H("#3e1a31"),
                H("#2b0b3b"),
            ],
            new Dictionary<string, float>
            {
                ["crater_light_border"] = 1.0802f,
                ["crater_size"] = 5.503f,
                ["dither_size"] = 1.9212f,
                ["lava_border_1"] = 0.04f,
                ["lava_border_2"] = 0.0663f,
                ["lava_cutoff"] = 0.5322f,
                ["lava_detail"] = 4.9861f,
                ["lava_octaves"] = 2.053f,
                ["lava_size"] = 7.0809f,
                ["light_border_1"] = 0.3126f,
                ["light_border_2"] = 0.4786f,
                ["octaves"] = 5f,
                ["size"] = 7.2765f,
            }),
            ["GateInferno"] = new(PixelPlanetKind.LavaWorld,
            [
                H("#6a1010"),
                H("#4a0808"),
                H("#2a0404"),
                H("#3a0606"),
                H("#1a0202"),
                H("#ffb020"),
                H("#ff6010"),
                H("#ff1808"),
            ],
            new Dictionary<string, float>
            {
                ["crater_light_border"] = 0.4251f,
                ["crater_size"] = 3.4052f,
                ["dither_size"] = 1.7244f,
                ["lava_border_1"] = 0.0885f,
                ["lava_border_2"] = 0.0865f,
                ["lava_cutoff"] = 0.7699f,
                ["lava_detail"] = 2.2117f,
                ["lava_octaves"] = 3.4094f,
                ["lava_size"] = 10.6701f,
                ["light_border_1"] = 0.3857f,
                ["light_border_2"] = 0.5324f,
                ["octaves"] = 4.184f,
                ["size"] = 7.5625f,
            }),
        };
        presets["Lavalight"] = presets["Lava"];
        presets["ArdoniumWastes"] = presets["GateInferno"];
        presets["Caves"] = presets["LowDesert"];
        return presets;
    }

    private static readonly (string Token, string Preset)[] Tokens =
    [
        ("inferno", "GateInferno"),
        ("lava", "Lava"),
        ("volcan", "Lava"),
        ("snow", "Snow"),
        ("ice", "Snow"),
        ("desert", "LowDesert"),
        ("wasteland", "LowDesert"),
        ("savanna", "LowDesert"),
        ("shadow", "Shadow"),
        ("cave", "Caves"),
        ("jungle", "Grasslands"),
        ("bioluminescent", "Grasslands"),
    ];

    /// <summary>Resolves a biome/expedition id to a look. Unknown ids fall back to Grasslands.</summary>
    public static PixelPlanetLook Resolve(string? biomeId)
    {
        if (biomeId != null)
        {
            if (Presets.TryGetValue(biomeId, out var exact))
                return exact;

            // "GateLavaCaves" etc: the first matching token decides.
            var lower = biomeId.ToLowerInvariant();
            foreach (var (token, preset) in Tokens)
            {
                if (lower.Contains(token))
                    return Presets[preset];
            }
        }

        return Presets["Grasslands"];
    }

    /// <summary>Sets all shader uniforms of a look: base setup for its kind, tuned overrides, then the palette.</summary>
    public static void Apply(ShaderInstance shader, PixelPlanetLook look, float time, float pixels, float seed)
    {
        PixelPlanetShaderSetup.Configure(shader, look.Kind, time, pixels, seed, 0f, new Vector2(0.39f, 0.39f));

        foreach (var (name, value) in look.Overrides)
            shader.SetParameter(name, value);

        PixelPlanetPalettes.ApplyPlanetColors(shader, look.Kind, look.Palette);
    }

    private static Vector3 H(string hex)
    {
        var c = Color.FromHex(hex);
        return new Vector3(c.R, c.G, c.B);
    }
}
