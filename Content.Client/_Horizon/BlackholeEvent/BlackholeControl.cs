using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.BlackholeEvent;

/// <summary>
/// Fullscreen pixel-art blackhole, drawn with the ported PixelBlackHole shader.
/// </summary>
public sealed class BlackholeControl : Control
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly ShaderInstance _shader;
    private readonly float _seed;

    public BlackholeControl()
    {
        IoCManager.InjectDependencies(this);

        _shader = _protoMan.Index<ShaderPrototype>("PixelBlackHole").InstanceUnique();
        _seed = _random.NextFloat(0f, 1000f);

        _shader.SetParameter("pixels", 1024f);
        _shader.SetParameter("pixels_ring", 1024f);
        _shader.SetParameter("seed", _seed);
        _shader.SetParameter("rotation", 0.766f);
        _shader.SetParameter("time_speed", 0.2f);
        _shader.SetParameter("should_dither", 0f);
        _shader.SetParameter("disk_width", 0.065f);
        _shader.SetParameter("ring_perspective", 14f);
        _shader.SetParameter("disk_size", 6.598f);
        _shader.SetParameter("octaves", 3f);
        _shader.SetParameter("hole_radius", 0.247f);
        _shader.SetParameter("hole_light_width", 0.028f);
        _shader.SetParameter("hole_c0", Vector3.Zero);
        _shader.SetParameter("hole_c1", new Vector3(0.02f, 0.02f, 0.025f));
        _shader.SetParameter("hole_c2", new Vector3(0.06f, 0.06f, 0.07f));
        _shader.SetParameter("ring_c0", new Vector3(1.000f, 1.000f, 0.922f));
        _shader.SetParameter("ring_c1", new Vector3(1.000f, 0.961f, 0.251f));
        _shader.SetParameter("ring_c2", new Vector3(1.000f, 0.722f, 0.290f));
        _shader.SetParameter("ring_c3", new Vector3(0.929f, 0.482f, 0.224f));
        _shader.SetParameter("ring_c4", new Vector3(0.741f, 0.251f, 0.208f));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var time = (float) _timing.RealTime.TotalSeconds + _seed;
        _shader.SetParameter("time", time);

        var size = MathF.Max(PixelSize.X, PixelSize.Y);
        var square = new Vector2(size, size);
        var origin = (PixelSize - square) / 2f;

        handle.DrawRect(new UIBox2(0, 0, PixelSize.X, PixelSize.Y), Color.Black);
        handle.UseShader(_shader);
        handle.DrawTextureRect(Texture.White, UIBox2.FromDimensions(origin, square));
        handle.UseShader(null);
    }
}
