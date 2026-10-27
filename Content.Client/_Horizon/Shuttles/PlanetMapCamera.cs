using System.Numerics;
using Content.Client.Parallax;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Light.Components;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Client.Graphics;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Color = Robust.Shared.Maths.Color;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Horizon.Shuttles;

/// <summary>How the shuttle console MAP tab shows a planet the shuttle is on.</summary>
public enum PlanetViewMode : byte
{
    /// <summary>A rough outline of the terrain: ground, rocks and plants from the biome, and where the complex is.</summary>
    Relief,

    /// <summary>A real top-down camera that loads the ground where it looks. Not available yet.</summary>
    Camera,
}

/// <summary>
/// Draws the planet the shuttle is on as the background of the shuttle console MAP tab, in place of the parallax.
/// <see cref="PlanetViewMode.Relief"/> is a rough outline computed from the biome (ground, rocks, plants) with the
/// complex marked where it is loaded; <see cref="PlanetViewMode.Camera"/> is a real top-down view of the loaded
/// tiles and entities. Living creatures are left out on purpose.
/// </summary>
public sealed class PlanetMapCamera
{
    private const int MaxEntities = 1200;
    private const float TargetCellPixels = 5f;
    private const int MaxCells = 12000;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(5);
    private static readonly Color ComplexColor = new(0.62f, 0.72f, 0.9f);
    private static readonly Color Backdrop = new(0.02f, 0.02f, 0.035f);

    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SharedMapSystem _maps;
    private readonly SharedTransformSystem _xform;
    private readonly EntityLookupSystem _lookup;
    private readonly SharedRoofSystem _roof;
    private readonly BiomeSystem _biome;

    // Relief mode: per sampled cell, the biome's ground, its relief entity and whether a different real tile is there.
    private readonly Dictionary<Vector2i, (int? Tile, string? Relief, bool Complex)> _sampled = new();
    private readonly Dictionary<string, Color> _reliefColors = new();
    private TimeSpan _cacheExpires;
    private MapId _cachedMap = MapId.Nullspace;

    private readonly Dictionary<int, Color> _tileColors = new();
    private readonly Dictionary<int, Texture?> _tileTextures = new();
    private readonly HashSet<Entity<SpriteComponent>> _entityScratch = new();

    public PlanetMapCamera()
    {
        IoCManager.InjectDependencies(this);
        var sys = _entMan.EntitySysManager;
        _maps = sys.GetEntitySystem<SharedMapSystem>();
        _xform = sys.GetEntitySystem<SharedTransformSystem>();
        _lookup = sys.GetEntitySystem<EntityLookupSystem>();
        _roof = sys.GetEntitySystem<SharedRoofSystem>();
        _biome = sys.GetEntitySystem<BiomeSystem>();
    }

    /// <summary>True when <paramref name="mapId"/> is a planet the shuttle is on and the camera was drawn.</summary>
    public bool TryDraw(
        DrawingHandleScreen handle,
        PlanetViewMode mode,
        MapId mapId,
        EntityUid? shuttle,
        Box2 viewBox,
        Func<Vector2, Vector2> toScreen,
        float pixelsPerMeter,
        UIBox2 bounds)
    {
        if (shuttle == null
            || mapId == MapId.Nullspace
            || !_entMan.TryGetComponent(shuttle.Value, out TransformComponent? shuttleXform)
            || shuttleXform.MapID != mapId)
        {
            return false;
        }

        var mapUid = _maps.GetMapOrInvalid(mapId);
        if (!_entMan.TryGetComponent(mapUid, out BiomeComponent? _)
            || !_entMan.TryGetComponent(mapUid, out MapGridComponent? grid))
        {
            return false;
        }

        handle.DrawRect(bounds, Backdrop);

        if (mode == PlanetViewMode.Camera)
        {
            DrawLoadedTiles(handle, (mapUid, grid), viewBox, toScreen, pixelsPerMeter);
            DrawEntities(handle, mapId, viewBox, toScreen, pixelsPerMeter);
            return true;
        }

        if (mapId != _cachedMap || _timing.RealTime > _cacheExpires)
        {
            _sampled.Clear();
            _cachedMap = mapId;
            _cacheExpires = _timing.RealTime + CacheLifetime;
        }

        if (_entMan.TryGetComponent(mapUid, out BiomeComponent? biome) && biome.Layers.Count > 0)
            DrawRelief(handle, (mapUid, grid), biome, viewBox, toScreen, pixelsPerMeter);
        return true;
    }

    private void DrawRelief(
        DrawingHandleScreen handle,
        Entity<MapGridComponent> grid,
        BiomeComponent biome,
        Box2 viewBox,
        Func<Vector2, Vector2> toScreen,
        float pixelsPerMeter)
    {
        // One cell is a block of tiles sampled at its centre; blocks sit on a fixed lattice so panning reuses samples.
        var cell = Math.Max(1, (int) MathF.Ceiling(TargetCellPixels / MathF.Max(pixelsPerMeter, 0.0001f)));

        int minX, maxX, minY, maxY;
        while (true)
        {
            minX = (int) MathF.Floor(viewBox.Left / cell) * cell;
            maxX = (int) MathF.Ceiling(viewBox.Right / cell) * cell;
            minY = (int) MathF.Floor(viewBox.Bottom / cell) * cell;
            maxY = (int) MathF.Ceiling(viewBox.Top / cell) * cell;

            if (((maxX - minX) / cell + 1) * ((maxY - minY) / cell + 1) <= MaxCells)
                break;

            cell *= 2;
        }

        for (var x = minX; x < maxX; x += cell)
        {
            for (var y = minY; y < maxY; y += cell)
            {
                var sample = new Vector2i(x + cell / 2, y + cell / 2);
                var (tileType, relief, complex) = Sample(sample, grid, biome);
                if (tileType == null)
                    continue;

                var a = toScreen(new Vector2(x, y));
                var b = toScreen(new Vector2(x + cell, y + cell));
                var box = new UIBox2(
                    MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y),
                    MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));

                // Outline only: flat colours. The complex stands out from the natural ground.
                var color = complex
                    ? ComplexColor
                    : relief != null ? GetReliefColor(relief) : GetTileColor(tileType.Value);
                handle.DrawRect(box, color);
            }
        }
    }

    private (int? Tile, string? Relief, bool Complex) Sample(Vector2i indices, Entity<MapGridComponent> grid, BiomeComponent biome)
    {
        if (_sampled.TryGetValue(indices, out var cached))
            return cached;

        Entity<MapGridComponent>? gridArg = grid;
        int? tileType = null;
        string? reliefId = null;
        var complex = false;

        // The ground the biome would generate here, whatever is really placed on it.
        if (_biome.TryGetTile(indices, biome.Layers, biome.Seed, gridArg, out var biomeTile) && !biomeTile.Value.IsEmpty)
        {
            tileType = biomeTile.Value.TypeId;

            // A loaded tile that differs from the biome's ground is part of the complex.
            if (_maps.TryGetTileRef(grid.Owner, grid.Comp, indices, out var realTile)
                && !realTile.Tile.IsEmpty
                && realTile.Tile.TypeId != biomeTile.Value.TypeId)
            {
                complex = true;
            }
            else if (_biome.TryGetEntity(indices, biome.Layers, biomeTile.Value, biome.Seed, gridArg, out var entityId))
            {
                reliefId = entityId;
            }
        }

        cached = (tileType, reliefId, complex);
        _sampled[indices] = cached;
        return cached;
    }

    private Color GetReliefColor(string entityId)
    {
        if (_reliefColors.TryGetValue(entityId, out var color))
            return color;

        var id = entityId.ToLowerInvariant();
        // Liquids are entities too (water, lava, plasma pools).
        if (id.Contains("water"))
            color = new Color(0.22f, 0.45f, 0.78f);
        else if (id.Contains("lava"))
            color = new Color(0.9f, 0.33f, 0.08f);
        else if (id.Contains("plasma"))
            color = new Color(0.62f, 0.25f, 0.78f);
        else if (id.Contains("crystal") || id.Contains("gem"))
            color = new Color(0.45f, 0.78f, 0.85f);
        else if (id.Contains("flora") || id.Contains("tree") || id.Contains("grass") || id.Contains("bush") || id.Contains("plant"))
            color = new Color(0.18f, 0.42f, 0.2f);
        else if (id.Contains("snow") || id.Contains("ice"))
            color = new Color(0.85f, 0.9f, 0.95f);
        else if (id.StartsWith("ore") || id.Contains("depositore") || id.Contains("kazie"))
            color = new Color(0.78f, 0.62f, 0.25f);
        else if (id.Contains("wall") || id.Contains("rock") || id.Contains("basalt") || id.Contains("stalag") || id.Contains("asteroid"))
            color = new Color(0.22f, 0.2f, 0.19f);
        else
            color = Color.FromHsv(new Vector4((entityId.GetHashCode() & 0xFF) / 255f, 0.25f, 0.4f, 1f));

        _reliefColors[entityId] = color;
        return color;
    }

    /// <summary>The tiles the game has really loaded around the camera, one by one at full detail.</summary>
    private void DrawLoadedTiles(
        DrawingHandleScreen handle,
        Entity<MapGridComponent> grid,
        Box2 viewBox,
        Func<Vector2, Vector2> toScreen,
        float pixelsPerMeter)
    {
        var visible = viewBox;
        if (pixelsPerMeter < 0.5f)
            return;

        var minX = (int) MathF.Floor(visible.Left);
        var maxX = (int) MathF.Ceiling(visible.Right);
        var minY = (int) MathF.Floor(visible.Bottom);
        var maxY = (int) MathF.Ceiling(visible.Top);

        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                if (!_maps.TryGetTileRef(grid.Owner, grid.Comp, new Vector2i(x, y), out var tileRef) || tileRef.Tile.IsEmpty)
                    continue;

                var a = toScreen(new Vector2(x, y));
                var b = toScreen(new Vector2(x + 1, y + 1));
                var box = new UIBox2(
                    MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y),
                    MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));

                if (GetTileTexture(tileRef.Tile.TypeId) is { } texture)
                    handle.DrawTextureRect(texture, box);
                else
                    handle.DrawRect(box, GetTileColor(tileRef.Tile.TypeId));
            }
        }
    }

    private void DrawEntities(
        DrawingHandleScreen handle,
        MapId mapId,
        Box2 viewBox,
        Func<Vector2, Vector2> toScreen,
        float pixelsPerMeter)
    {
        // Sprites are authored at 32 px per meter; scale them so that one meter is `pixelsPerMeter` map pixels.
        var scale = new Vector2(pixelsPerMeter / 32f);

        _entityScratch.Clear();
        _lookup.GetEntitiesIntersecting(mapId, viewBox, _entityScratch, LookupFlags.Static | LookupFlags.Dynamic | LookupFlags.Sundries);

        var drawn = 0;
        foreach (var (uid, sprite) in _entityScratch)
        {
            if (drawn >= MaxEntities)
                break;

            if (!sprite.Visible
                || _entMan.HasComponent<MobStateComponent>(uid)
                || _entMan.HasComponent<GhostComponent>(uid)
                || !_entMan.TryGetComponent(uid, out TransformComponent? xform)
                || xform.ParentUid != xform.GridUid && xform.ParentUid != xform.MapUid)
            {
                continue;
            }

            // Anything under a roof (a tile that weather cannot reach, or a roofed one) is hidden: only the tile shows.
            if (IsUnderRoof(xform))
                continue;

            var pos = toScreen(_xform.GetWorldPosition(xform));
            handle.DrawEntity(uid, pos, scale, -_xform.GetWorldRotation(xform), Angle.Zero, Direction.South, sprite);
            drawn++;
        }
    }

    private bool IsUnderRoof(TransformComponent xform)
    {
        // Ships (grids with an IFF transponder) are always shown, roof or not.
        if (xform.GridUid is { } shuttleGrid && _entMan.HasComponent<IFFComponent>(shuttleGrid))
            return false;

        if ((xform.GridUid ?? xform.MapUid) is not { } gridUid
            || !_entMan.TryGetComponent(gridUid, out MapGridComponent? grid)
            || !_maps.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef))
        {
            return false;
        }

        // Only the tile and the roof marker count, not entities that happen to block weather (walls, rocks).
        if (_entMan.TryGetComponent(gridUid, out RoofComponent? roof) && _roof.IsRooved((gridUid, grid, roof), tileRef.GridIndices))
            return true;

        return !tileRef.Tile.IsEmpty && _tileDefs[tileRef.Tile.TypeId] is ContentTileDefinition { Weather: false };
    }

    private Texture? GetTileTexture(int tileId)
    {
        if (_tileTextures.TryGetValue(tileId, out var cached))
            return cached;

        Texture? texture = null;
        try
        {
            if (_tileDefs[tileId] is ContentTileDefinition { Sprite: { } sprite })
            {
                texture = _cache.GetResource<TextureResource>(sprite).Texture;

                // Tiles with variants keep them side by side in one strip: use the first one.
                if (texture.Width > texture.Height)
                    texture = new AtlasTexture(texture, UIBox2.FromDimensions(0f, 0f, texture.Height, texture.Height));
            }
        }
        catch (Exception)
        {
            texture = null;
        }

        _tileTextures[tileId] = texture;
        return texture;
    }

    private Color GetTileColor(int tileId)
    {
        if (_tileColors.TryGetValue(tileId, out var color))
            return color;

        color = AverageTileColor(tileId);
        _tileColors[tileId] = color;
        return color;
    }

    private Color AverageTileColor(int tileId)
    {
        var fallback = Color.FromHsv(new Vector4((tileId * 0.137f) % 1f, 0.25f, 0.45f, 1f));

        try
        {
            if (_tileDefs[tileId] is not ContentTileDefinition { Sprite: { } sprite })
                return fallback;

            using var stream = _resources.ContentFileRead(sprite);
            using var image = Image.Load<Rgba32>(stream);

            double r = 0, g = 0, b = 0;
            var count = 0;
            foreach (var p in image.GetPixelSpan())
            {
                if (p.A < 128)
                    continue;

                r += p.R;
                g += p.G;
                b += p.B;
                count++;
            }

            return count == 0
                ? fallback
                : new Color((float) (r / count / 255.0), (float) (g / count / 255.0), (float) (b / count / 255.0));
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
