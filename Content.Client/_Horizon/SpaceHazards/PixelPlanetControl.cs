// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Horizon.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.SpaceHazards;

/// <summary>
/// Small rotating pixel planet for UI (e.g. expedition mission cards), drawn with the PixelPlanets shaders
/// instead of a pre-rendered 361-frame sprite sheet. Use <see cref="SetBiome"/> to pick the look.
/// </summary>
public sealed class PixelPlanetControl : Control
{
    public const int NativePixels = 48;
    public const float DisplayScale = 1.5f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<PixelPlanetKind, ShaderInstance> _shaders = new();
    private readonly float _seed;
    private PixelPlanetLook _look = PixelPlanetBiomeLooks.Resolve(null);

    public PixelPlanetControl()
    {
        IoCManager.InjectDependencies(this);

        _seed = 0.01f + _random.NextFloat() * 9.99f;
        var size = NativePixels * DisplayScale;
        MinSize = new Vector2(size, size);
        SetSize = new Vector2(size, size);
        HorizontalAlignment = HAlignment.Center;
    }

    public void SetBiome(string? biomeId)
    {
        _look = PixelPlanetBiomeLooks.Resolve(biomeId);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (!VisibleInTree)
            return;

        if (!_shaders.TryGetValue(_look.Kind, out var shader))
        {
            shader = _protoMan.Index(PixelPlanetShaderSetup.ShaderId(_look.Kind)).InstanceUnique();
            _shaders[_look.Kind] = shader;
        }

        var time = (float) _timing.RealTime.TotalSeconds + _seed;
        PixelPlanetBiomeLooks.Apply(shader, _look, time, NativePixels, _seed);

        var size = MathF.Min(PixelSize.X, PixelSize.Y);
        var square = new Vector2(size, size);
        var origin = (PixelSize - square) / 2f;

        handle.UseShader(shader);
        handle.DrawTextureRect(Texture.White, UIBox2.FromDimensions(origin, square));
        handle.UseShader(null);
    }
}
