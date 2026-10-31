using System.Numerics;
using Content.Shared._Horizon.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._Horizon.Shuttles;

/// <summary>
/// Border rings around stars and black holes for the shuttle console radar and map, like the ones the sector
/// map shows: a black hole gets its pull, hazard and event horizon borders, a star gets the border of the zone
/// where solar panels work better.
/// </summary>
public static class SectorBodyRings
{
    private static readonly Color Pull = Color.FromHex("#A040FF").WithAlpha(0.45f);
    private static readonly Color Hazard = Color.FromHex("#D080FF").WithAlpha(0.55f);
    private static readonly Color Horizon = Color.FromHex("#F5F540").WithAlpha(0.7f);
    private static readonly Color SolarBoost = Color.FromHex("#7CFF8A").WithAlpha(0.5f);
    private static readonly Color ExpeditionRange = Color.FromHex("#4DD0E1").WithAlpha(0.7f);

    /// <param name="toScreen">Converts a world position on the viewed map to control pixels.</param>
    /// <param name="pixelsPerMeter">Control pixels per world meter.</param>
    public static void Draw(
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

        DrawExpeditionRings(handle, entMan, xformSys, mapId, toScreen, pixelsPerMeter, bounds);

        var query = entMan.AllEntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (query.MoveNext(out _, out var body, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var center = toScreen(xformSys.GetWorldPosition(xform));

            if (body.Kind == CelestialKind.BlackHole)
            {
                Ring(handle, center, body.PullRadius, pixelsPerMeter, bounds, Pull);
                Ring(handle, center, body.HazardRadius, pixelsPerMeter, bounds, Hazard);
            }
            else
            {
                Ring(handle, center, body.SolarBoostRadius, pixelsPerMeter, bounds, SolarBoost);
            }
        }
    }

    private static void DrawExpeditionRings(
        DrawingHandleScreen handle,
        IEntityManager entMan,
        SharedTransformSystem xformSys,
        MapId mapId,
        Func<Vector2, Vector2> toScreen,
        float pixelsPerMeter,
        UIBox2 bounds)
    {
        var planets = entMan.AllEntityQueryEnumerator<ExpeditionPlanetComponent, TransformComponent>();
        while (planets.MoveNext(out _, out var planet, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            // The radius inside which an expedition console can start an expedition.
            Ring(handle, toScreen(xformSys.GetWorldPosition(xform)), planet.Range, pixelsPerMeter, bounds, ExpeditionRange);
        }
    }

    private static void Ring(DrawingHandleScreen handle, Vector2 center, float radiusMeters, float pixelsPerMeter, UIBox2 bounds, Color color)
    {
        if (radiusMeters <= 1f)
            return;

        var radius = radiusMeters * pixelsPerMeter;

        // Only draw rings whose line passes through the visible area (huge off-screen circles are wasted work).
        var boundsCenter = bounds.Center;
        var halfDiagonal = (bounds.BottomRight - bounds.TopLeft).Length() * 0.5f;
        if (MathF.Abs(Vector2.Distance(center, boundsCenter) - radius) > halfDiagonal)
            return;

        handle.DrawCircle(center, radius, color, filled: false);
    }
}
