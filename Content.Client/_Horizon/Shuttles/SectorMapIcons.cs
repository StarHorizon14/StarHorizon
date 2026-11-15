using System.Numerics;
using Content.Client._Horizon.SpaceHazards;
using Content.Shared._Horizon.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.Shuttles;

/// <summary>
/// Icons for the shuttle console MAP tab: the planets themselves (drawn with the same pixel-planet shaders as in
/// the world) and the threats around them (black holes, stars), each with a label.
/// </summary>
public sealed class SectorMapIcons
{
    private const float MinIconPixels = 22f;
    private const float MaxIconPixels = 72f;
    private const float MapPixels = 48f;
    private const float StarOuterScale = 3f; // blobs and flares extend beyond the surface
    private const float BlackHoleIconScale = 3f; // the disk reaches far beyond the hole itself

    private static readonly Color BlackHoleColor = Color.FromHex("#A040FF");
    private static readonly Color BlackHoleHorizon = Color.FromHex("#F5F540");
    private static readonly Color StarColor = Color.FromHex("#FFB020");
    private static readonly Color LabelShadow = Color.Black.WithAlpha(0.8f);

    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();
    private readonly Dictionary<EntityUid, (ShaderInstance Blobs, ShaderInstance Star, ShaderInstance Flares)> _starShaders = new();
    private readonly Font _font;

    public SectorMapIcons()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 9);
    }

    public void Draw(
        DrawingHandleScreen handle,
        IEntityManager entMan,
        SharedTransformSystem xformSys,
        MapId mapId,
        Func<Vector2, Vector2> toScreen,
        float pixelsPerMeter,
        UIBox2 bounds)
    {
        if (mapId == MapId.Nullspace)
            return;

        var time = (float) _timing.RealTime.TotalSeconds;

        // Planets people can launch expeditions from: biome look + their own name.
        var expedition = entMan.AllEntityQueryEnumerator<ExpeditionPlanetComponent, TransformComponent, MetaDataComponent>();
        while (expedition.MoveNext(out var uid, out var planet, out var xform, out var meta))
        {
            if (xform.MapID != mapId)
                continue;

            var center = toScreen(xformSys.GetWorldPosition(xform));
            var size = IconSize(planet.SpriteRadius, pixelsPerMeter);
            if (!Visible(center, size, bounds))
                continue;

            var look = PixelPlanetBiomeLooks.Resolve(planet.BiomeId);
            var shader = GetShader(uid, look.Kind);
            PixelPlanetBiomeLooks.Apply(shader, look, time + planet.Seed, MapPixels, planet.Seed);
            DrawShaderIcon(handle, shader, center, size);
            DrawLabel(handle, meta.EntityName, center, size, Color.AliceBlue.WithAlpha(0.92f)); // same colour as other beacon names
        }

        // Threats: black holes and stars.
        var bodies = entMan.AllEntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (bodies.MoveNext(out var uid, out var body, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var center = toScreen(xformSys.GetWorldPosition(xform));
            var size = IconSize(body.SpriteRadius, pixelsPerMeter);
            if (!Visible(center, size, bounds))
                continue;

            var radius = size / 2f;
            if (body.Kind == CelestialKind.BlackHole)
            {
                // The same accretion-disk shader as in the world, big enough to see the disk around the hole.
                var holeSize = Math.Clamp(size * BlackHoleIconScale, MinIconPixels, MaxIconPixels * BlackHoleIconScale);
                DrawBlackHole(handle, uid, body, center, holeSize, time);
                DrawLabel(handle, Loc.GetString("sector-map-black-hole"), center, size, BlackHoleColor);
            }
            else
            {
                DrawStar(handle, uid, body, center, size, time);
                var label = body.SolarBoostMultiplier > 1f
                    ? Loc.GetString("sector-map-star-boost", ("multiplier", body.SolarBoostMultiplier.ToString("0.#")))
                    : Loc.GetString("sector-map-star");
                DrawLabel(handle, label, center, size, StarColor);
            }
        }
    }

    /// <summary>Expedition planets as true-scale discs for the NAV tab.</summary>
    public void DrawNavPlanets(
        DrawingHandleScreen handle,
        IEntityManager entMan,
        SharedTransformSystem xformSys,
        MapId mapId,
        Func<Vector2, Vector2> toScreen,
        float pixelsPerMeter,
        UIBox2 bounds)
    {
        if (mapId == MapId.Nullspace)
            return;

        var time = (float) _timing.RealTime.TotalSeconds;
        var query = entMan.AllEntityQueryEnumerator<ExpeditionPlanetComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var planet, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var center = toScreen(xformSys.GetWorldPosition(xform));
            var size = Math.Max(planet.SpriteRadius * 2f * pixelsPerMeter, 6f);
            if (!Visible(center, size, bounds))
                continue;

            var look = PixelPlanetBiomeLooks.Resolve(planet.BiomeId);
            var shader = GetShader(uid, look.Kind);
            PixelPlanetBiomeLooks.Apply(shader, look, time + planet.Seed, MapPixels, planet.Seed);
            DrawShaderIcon(handle, shader, center, size);
        }
    }

    /// <summary>Stars and black holes at their true size for the NAV tab.</summary>
    public void DrawNavBodies(
        DrawingHandleScreen handle,
        IEntityManager entMan,
        SharedTransformSystem xformSys,
        MapId mapId,
        Func<Vector2, Vector2> toScreen,
        float pixelsPerMeter,
        UIBox2 bounds)
    {
        if (mapId == MapId.Nullspace)
            return;

        var time = (float) _timing.RealTime.TotalSeconds;
        var bodies = entMan.AllEntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (bodies.MoveNext(out var uid, out var body, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var center = toScreen(xformSys.GetWorldPosition(xform));
            var size = Math.Max(body.SpriteRadius * 2f * pixelsPerMeter, 6f);
            var reach = body.Kind == CelestialKind.BlackHole ? size * BlackHoleIconScale : size * StarOuterScale;
            if (!Visible(center, reach, bounds))
                continue;

            if (body.Kind == CelestialKind.BlackHole)
                DrawBlackHole(handle, uid, body, center, size * BlackHoleIconScale, time + body.Seed);
            else
                DrawStar(handle, uid, body, center, size, time + body.Seed);
        }
    }

    /// <summary>Called when the control is disposed, to free the per-planet shaders.</summary>
    public void Dispose()
    {
        foreach (var shader in _shaders.Values)
            shader.Dispose();

        foreach (var (blobs, star, flares) in _starShaders.Values)
        {
            blobs.Dispose();
            star.Dispose();
            flares.Dispose();
        }

        _starShaders.Clear();
        _shaders.Clear();
    }

    /// <summary>The same three passes as the world star: outer blobs, the surface and the flares.</summary>
    private void DrawStar(DrawingHandleScreen handle, EntityUid uid, SectorCelestialBodyComponent body, Vector2 center, float size, float time)
    {
        if (!_starShaders.TryGetValue(uid, out var set))
        {
            set = (_protos.Index<ShaderPrototype>("PixelStarBlobs").InstanceUnique(),
                _protos.Index<ShaderPrototype>("PixelStar").InstanceUnique(),
                _protos.Index<ShaderPrototype>("PixelStarFlares").InstanceUnique());
            _starShaders[uid] = set;
        }

        var (blobs, star, flares) = set;
        var speed = 0.05f * PixelPlanetShaderSetup.AnimSpeedScale;
        var outerSize = size * StarOuterScale;
        var outerPixels = MapPixels * StarOuterScale;

        blobs.SetParameter("time", time);
        blobs.SetParameter("pixels", outerPixels);
        blobs.SetParameter("seed", body.Seed);
        blobs.SetParameter("rotation", 0f);
        blobs.SetParameter("time_speed", speed);
        blobs.SetParameter("size", 4.93f);
        blobs.SetParameter("circle_amount", 2f);
        blobs.SetParameter("circle_size", 1f);
        PixelPlanetPalettes.ApplyStarBlobs(blobs, body.Palette);
        DrawShaderIcon(handle, blobs, center, outerSize);

        star.SetParameter("time", time);
        star.SetParameter("pixels", MapPixels);
        star.SetParameter("seed", body.Seed);
        star.SetParameter("rotation", 0f);
        star.SetParameter("time_speed", speed);
        star.SetParameter("should_dither", 0f);
        star.SetParameter("tiles", 1f);
        PixelPlanetPalettes.ApplyStar(star, body.Palette);
        DrawShaderIcon(handle, star, center, size);

        flares.SetParameter("time", time);
        flares.SetParameter("pixels", outerPixels);
        flares.SetParameter("seed", body.Seed);
        flares.SetParameter("rotation", 0f);
        flares.SetParameter("time_speed", speed);
        flares.SetParameter("should_dither", 0f);
        flares.SetParameter("size", 1.6f);
        flares.SetParameter("octaves", 4f);
        flares.SetParameter("storm_width", 0.3f);
        flares.SetParameter("storm_dither_width", 0f);
        flares.SetParameter("flare_scale", 1f);
        flares.SetParameter("circle_amount", 2f);
        flares.SetParameter("circle_scale", 1f);
        PixelPlanetPalettes.ApplyStarFlares(flares, body.Palette);
        DrawShaderIcon(handle, flares, center, outerSize);
    }

    private void DrawBlackHole(DrawingHandleScreen handle, EntityUid uid, SectorCelestialBodyComponent body, Vector2 center, float size, float time)
    {
        var holeShader = GetBlackHoleShader(uid);
        holeShader.SetParameter("time", time * 0.2f);
        holeShader.SetParameter("pixels", MapPixels * 2f);
        holeShader.SetParameter("pixels_ring", MapPixels * 2f);
        holeShader.SetParameter("seed", body.Seed);
        holeShader.SetParameter("rotation", 0.766f);
        holeShader.SetParameter("time_speed", 0.2f);
        holeShader.SetParameter("should_dither", 0f);
        holeShader.SetParameter("disk_width", 0.065f);
        holeShader.SetParameter("ring_perspective", 14f);
        holeShader.SetParameter("disk_size", 6.598f);
        holeShader.SetParameter("octaves", 3f);
        holeShader.SetParameter("hole_radius", 0.247f);
        holeShader.SetParameter("hole_light_width", 0.028f);
        PixelPlanetPalettes.ApplyBlackHole(holeShader, body.Palette);
        DrawShaderIcon(handle, holeShader, center, size);
    }

    private ShaderInstance GetBlackHoleShader(EntityUid uid)
    {
        if (_shaders.TryGetValue(uid, out var shader))
            return shader;

        shader = _protos.Index<ShaderPrototype>("PixelBlackHole").InstanceUnique();
        _shaders[uid] = shader;
        return shader;
    }

    private ShaderInstance GetShader(EntityUid uid, PixelPlanetKind kind)
    {
        if (_shaders.TryGetValue(uid, out var shader))
            return shader;

        shader = _protos.Index(PixelPlanetShaderSetup.ShaderId(kind)).InstanceUnique();
        _shaders[uid] = shader;
        return shader;
    }

    private static float IconSize(float spriteRadiusMeters, float pixelsPerMeter)
    {
        return Math.Clamp(spriteRadiusMeters * 2f * pixelsPerMeter, MinIconPixels, MaxIconPixels);
    }

    private static bool Visible(Vector2 center, float size, UIBox2 bounds)
    {
        return center.X > bounds.Left - size && center.X < bounds.Right + size
               && center.Y > bounds.Top - size && center.Y < bounds.Bottom + size;
    }

    private static void DrawShaderIcon(DrawingHandleScreen handle, ShaderInstance shader, Vector2 center, float size)
    {
        handle.UseShader(shader);
        handle.DrawTextureRect(Texture.White, UIBox2.FromDimensions(center - new Vector2(size / 2f), new Vector2(size)));
        handle.UseShader(null);
    }

    private void DrawLabel(DrawingHandleScreen handle, string text, Vector2 center, float iconSize, Color color)
    {
        var dims = handle.GetDimensions(_font, text, 1f);
        var pos = center + new Vector2(-dims.X / 2f, iconSize / 2f + 2f);
        handle.DrawString(_font, pos + new Vector2(1f, 1f), text, LabelShadow);
        handle.DrawString(_font, pos, text, color);
    }
}
