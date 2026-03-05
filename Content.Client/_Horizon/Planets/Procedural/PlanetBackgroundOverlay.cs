using Content.Shared._Horizon.Planets.Procedural;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client._Horizon.Planets.Procedural;

public sealed class PlanetBackgroundOverlay : Overlay
{
    private readonly IGameTiming _timing = default!;
    private readonly IEntityManager _entity = default!;

    private SharedMapSystem _map = default!;

    private readonly Dictionary<string, ShaderInstance> _cachedShaders = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public PlanetBackgroundOverlay()
    {
        _timing = IoCManager.Resolve<IGameTiming>();
        _entity = IoCManager.Resolve<IEntityManager>();

        _map = _entity.System<SharedMapSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;
        var eye = args.Viewport.Eye;
        if (eye == null) return;

        if (!_map.TryGetMap(eye.Position.MapId, out var mapUid)) return;
        if (!_entity.TryGetComponent<MapPlanetBackgroundComponent>(mapUid.Value, out var background)) return;

        // Важно: Resolve здесь, чтобы не забыть IoC
        var proto = IoCManager.Resolve<IPrototypeManager>();
        var eyePos = eye.Position.Position;

        foreach (var planet in background.Planets)
        {
            if (!_cachedShaders.TryGetValue(planet.ShaderName, out var shader))
            {
                if (!proto.TryIndex<ShaderPrototype>(planet.ShaderName, out var shaderProto))
                    continue;
                shader = shaderProto.Instance().Duplicate();

                _cachedShaders[planet.ShaderName] = shader;
            }

            float speed = planet.TimeSpeed <= 0 ? 1.0f : planet.TimeSpeed; // Защита от нуля
            float time = (float)_timing.RealTime.TotalSeconds * speed;

            var slowness = 0.998f;
            var finalPos = eyePos * slowness;

            var rect = Box2.CenteredAround(finalPos, new Vector2(planet.Size, planet.Size));

            // Передаем параметры С ПРОВЕРКОЙ (защита от "пустых" данных в команде)
            shader.SetParameter("pixels", planet.Pixels <= 0 ? 100f : planet.Pixels);
            shader.SetParameter("zoom", planet.Zoom <= 0 ? 1.0f : planet.Zoom);
            shader.SetParameter("size", planet.Size <= 0 ? 1.0f : planet.Size);
            shader.SetParameter("n_layers", planet.NLayers <= 0 ? 4.0f : planet.NLayers);
            shader.SetParameter("layer_height", planet.LayerHeight <= 0 ? 0.2f : planet.LayerHeight);
            shader.SetParameter("tilt", planet.Tilt <= 0 ? 1.0f : planet.Tilt);
            shader.SetParameter("s_time", time);
            shader.SetParameter("rotation", planet.Rotation);
            shader.SetParameter("time_speed", 1.0f);
            shader.SetParameter("seed", planet.Seed);
            shader.SetParameter("swirl", planet.Swirl);
            shader.SetParameter("should_dither", planet.ShouldDither);
            shader.SetParameter("n_colors", planet.NColors);

            // Цвета
            shader.SetParameter("color0", planet.Color0);
            shader.SetParameter("color1", planet.Color1);
            shader.SetParameter("color2", planet.Color2);
            shader.SetParameter("color3", planet.Color3);
            shader.SetParameter("color4", planet.Color4);
            shader.SetParameter("color5", planet.Color5);
            shader.SetParameter("color6", planet.Color6);

            worldHandle.UseShader(shader);
            // Рисуем на весь экран — шейдер сам нарисует круг в центре UV
            worldHandle.DrawRect(args.WorldBounds, Color.White);
            worldHandle.UseShader(null);
        }
    }
}
