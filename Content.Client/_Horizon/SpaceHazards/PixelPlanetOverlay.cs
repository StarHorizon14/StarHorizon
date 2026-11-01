// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Client.Parallax;
using Content.Shared._Horizon.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.SpaceHazards;

public sealed class PixelPlanetOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> StarBlobsShader = "PixelStarBlobs";
    private static readonly ProtoId<ShaderPrototype> StarShader = "PixelStar";
    private static readonly ProtoId<ShaderPrototype> StarFlaresShader = "PixelStarFlares";
    private static readonly ProtoId<ShaderPrototype> BlackHoleShader = "PixelBlackHole";
    private const float BlackHoleDrawScale = 3f;
    private const float StarOuterPassScale = 3f;
    private const float AnimSpeedScale = PixelPlanetShaderSetup.AnimSpeedScale;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _prototypes;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly Dictionary<string, ShaderInstance> _shaders = new();
    private readonly Dictionary<EntityUid, ShaderInstance> _expeditionShaders = new();

    public PixelPlanetOverlay(IEntityManager entManager, IPrototypeManager prototypes)
    {
        _entManager = entManager;
        _prototypes = prototypes;
        _timing = IoCManager.Resolve<IGameTiming>();
        _transform = entManager.System<SharedTransformSystem>();
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();

        foreach (var shader in _shaders.Values)
            shader.Dispose();
        _shaders.Clear();

        foreach (var shader in _expeditionShaders.Values)
            shader.Dispose();
        _expeditionShaders.Clear();
    }

    /// <summary>How much of the camera movement expedition planets follow (0 = glued to the screen, 1 = real world position).</summary>
    private const float ExpeditionParallax = 0.1f;

    /// <summary>Expedition planets are drawn smaller than their world size to feel distant.</summary>
    private const float ExpeditionVisualScale = 0.5f;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var handle = args.WorldHandle;
        var aabb = args.WorldAABB;
        var timeBase = (float)_timing.CurTime.TotalSeconds;

        var celestialQuery = _entManager.EntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (celestialQuery.MoveNext(out _, out var body, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var pos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(body.SpriteRadius, 1f);
            var cullPad = body.Kind == CelestialKind.BlackHole
                ? radius * BlackHoleDrawScale
                : radius * StarOuterPassScale;

            if (!Intersects(aabb, pos, cullPad))
                continue;

            var time = timeBase + body.Seed;
            switch (body.Kind)
            {
                case CelestialKind.Star:
                    DrawStar(handle, pos, radius, body.Pixels, body.Seed, body.Palette, time);
                    break;
                case CelestialKind.BlackHole:
                    DrawBlackHole(handle, pos, radius, body.Pixels, body.Seed, body.Palette, time);
                    break;
            }
        }

        var planetQuery = _entManager.EntityQueryEnumerator<SectorBackgroundPlanetComponent, TransformComponent>();
        while (planetQuery.MoveNext(out _, out var planet, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var pos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(planet.SpriteRadius, 1f);
            if (!Intersects(aabb, pos, radius))
                continue;

            DrawPlanet(handle, pos, planet, timeBase + planet.Seed);
        }

        // Expedition planets: biome-looking planets with their own shader instance each (looks differ per planet).
        var expeditionQuery = _entManager.EntityQueryEnumerator<ExpeditionPlanetComponent, TransformComponent>();
        while (expeditionQuery.MoveNext(out var uid, out var expedition, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            // Parallax: expedition planets are a far-away backdrop, so they follow the camera and only slowly drift
            // against the world instead of sitting at their real world position in front of the ships.
            var cameraCenter = aabb.Center;
            var pos = cameraCenter + (_transform.GetWorldPosition(xform) - cameraCenter) * ExpeditionParallax;
            var radius = MathF.Max(expedition.SpriteRadius, 1f) * ExpeditionVisualScale;
            if (!Intersects(aabb, pos, radius))
                continue;

            var look = PixelPlanetBiomeLooks.Resolve(expedition.BiomeId);
            if (!_expeditionShaders.TryGetValue(uid, out var shader))
            {
                shader = _prototypes.Index(PixelPlanetShaderSetup.ShaderId(look.Kind)).InstanceUnique();
                _expeditionShaders[uid] = shader;
            }

            PixelPlanetBiomeLooks.Apply(shader, look, timeBase + expedition.Seed, expedition.Pixels, expedition.Seed);
            DrawQuad(handle, shader, pos, new Vector2(radius * 2f, radius * 2f));
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }


    private void DrawStar(
        DrawingHandleWorld handle,
        Vector2 pos,
        float radius,
        float pixels,
        float seed,
        byte palette,
        float time)
    {
        var surfaceSize = new Vector2(radius * 2f, radius * 2f);
        var outerSize = surfaceSize * StarOuterPassScale;
        var outerPixels = pixels * StarOuterPassScale;
        var blobs = GetShader(StarBlobsShader);
        blobs.SetParameter("time", time);
        blobs.SetParameter("pixels", outerPixels);
        blobs.SetParameter("seed", seed);
        blobs.SetParameter("rotation", 0f);
        blobs.SetParameter("time_speed", 0.05f * AnimSpeedScale);
        blobs.SetParameter("size", 4.93f);
        blobs.SetParameter("circle_amount", 2f);
        blobs.SetParameter("circle_size", 1f);
        PixelPlanetPalettes.ApplyStarBlobs(blobs, palette);
        DrawQuad(handle, blobs, pos, outerSize);
        var star = GetShader(StarShader);
        star.SetParameter("time", time);
        star.SetParameter("pixels", pixels);
        star.SetParameter("seed", seed);
        star.SetParameter("rotation", 0f);
        star.SetParameter("time_speed", 0.05f * AnimSpeedScale);
        star.SetParameter("should_dither", 0f);
        star.SetParameter("tiles", 1f);
        PixelPlanetPalettes.ApplyStar(star, palette);
        DrawQuad(handle, star, pos, surfaceSize);
        var flares = GetShader(StarFlaresShader);
        flares.SetParameter("time", time);
        flares.SetParameter("pixels", outerPixels);
        flares.SetParameter("seed", seed);
        flares.SetParameter("rotation", 0f);
        flares.SetParameter("time_speed", 0.05f * AnimSpeedScale);
        flares.SetParameter("should_dither", 0f);
        flares.SetParameter("size", 1.6f);
        flares.SetParameter("octaves", 4f);
        flares.SetParameter("storm_width", 0.3f);
        flares.SetParameter("storm_dither_width", 0f);
        flares.SetParameter("flare_scale", 1f);
        flares.SetParameter("circle_amount", 2f);
        flares.SetParameter("circle_scale", 1f);
        PixelPlanetPalettes.ApplyStarFlares(flares, palette);
        DrawQuad(handle, flares, pos, outerSize);
    }

    private void DrawBlackHole(
        DrawingHandleWorld handle,
        Vector2 pos,
        float radius,
        float pixels,
        float seed,
        byte palette,
        float time)
    {
        var drawSize = new Vector2(radius * 2f * BlackHoleDrawScale, radius * 2f * BlackHoleDrawScale);
        var shader = GetShader(BlackHoleShader);
        shader.SetParameter("time", time);
        shader.SetParameter("pixels", pixels);
        shader.SetParameter("pixels_ring", pixels);
        shader.SetParameter("seed", seed);
        shader.SetParameter("rotation", 0.766f);
        shader.SetParameter("time_speed", 0.2f * AnimSpeedScale);
        shader.SetParameter("should_dither", 0f);
        shader.SetParameter("disk_width", 0.065f);
        shader.SetParameter("ring_perspective", 14f);
        shader.SetParameter("disk_size", 6.598f);
        shader.SetParameter("octaves", 3f);
        shader.SetParameter("hole_radius", 0.247f);
        shader.SetParameter("hole_light_width", 0.028f);
        PixelPlanetPalettes.ApplyBlackHole(shader, palette);
        DrawQuad(handle, shader, pos, drawSize);
    }

    private void DrawPlanet(
        DrawingHandleWorld handle,
        Vector2 pos,
        SectorBackgroundPlanetComponent planet,
        float time)
    {
        var radius = MathF.Max(planet.SpriteRadius, 1f);
        var drawSize = new Vector2(radius * 2f, radius * 2f);
        var light = new Vector2(planet.LightOriginX, planet.LightOriginY);
        var pixels = planet.Pixels;
        var seed = planet.Seed;
        var rotation = planet.Rotation;

        var s = GetShader(PixelPlanetShaderSetup.ShaderId(planet.PlanetKind));
        PixelPlanetShaderSetup.Configure(s, planet.PlanetKind, time, pixels, seed, rotation, light);
        PixelPlanetPalettes.ApplyPlanet(s, planet.PlanetKind, planet.PaletteIndex);
        DrawQuad(handle, s, pos, drawSize);
    }

    private static void DrawQuad(DrawingHandleWorld handle, ShaderInstance shader, Vector2 pos, Vector2 size)
    {
        handle.UseShader(shader);
        handle.SetTransform(Matrix3Helpers.CreateTranslation(pos));
        handle.DrawTextureRect(Texture.White, Box2.CenteredAround(Vector2.Zero, size));
    }

    private static bool Intersects(Box2 aabb, Vector2 pos, float radius)
    {
        var bounds = Box2.CenteredAround(pos, new Vector2(radius * 2f, radius * 2f));
        return aabb.Intersects(bounds);
    }
    private ShaderInstance GetShader(ProtoId<ShaderPrototype> id)
    {
        var key = id.Id;
        if (_shaders.TryGetValue(key, out var existing))
            return existing;

        var instance = _prototypes.Index(id).InstanceUnique();
        _shaders[key] = instance;
        return instance;
    }
}
