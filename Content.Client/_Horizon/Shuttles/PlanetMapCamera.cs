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

/// <summary>
/// Draws a top-down "camera" of the planet as the background of the shuttle console MAP tab, in place of the
/// parallax. It only shows what the game has really loaded around the camera (the server loads the ground the camera
/// is moved to): tiles and entities. Living creatures are left out on purpose.
/// </summary>
public sealed class PlanetMapCamera
{
    private const int MaxEntities = 1200;
    private static readonly Color Backdrop = new(0.02f, 0.02f, 0.035f);

    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private readonly SharedMapSystem _maps;
    private readonly SharedTransformSystem _xform;
    private readonly EntityLookupSystem _lookup;
    private readonly SharedRoofSystem _roof;

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
    }

    /// <summary>True when <paramref name="mapId"/> is a planet the shuttle is on and the camera was drawn.</summary>
    public bool TryDraw(
        DrawingHandleScreen handle,
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

        DrawLoadedTiles(handle, (mapUid, grid), viewBox, toScreen, pixelsPerMeter);
        DrawEntities(handle, mapId, viewBox, toScreen, pixelsPerMeter);
        return true;
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
