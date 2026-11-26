using Content.Shared._Horizon.PlayerLasso.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Horizon.PlayerLasso;

/// <summary>
/// Swaps the coil sprite of a lasso item depending on how much slack is left in the rope.
/// A rope that is not tied to anything shows a full coil; the closer the rope is to pulling taut, the smaller the coil.
/// </summary>
public sealed class PlayerLassoVisualsSystem : EntitySystem
{
    private const string StatePrefix = "rope-";

    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerLassoComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<PlayerLassoComponent> ent, ref ComponentStartup args)
    {
        UpdateSprite(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<PlayerLassoComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var lasso, out _))
        {
            UpdateSprite((uid, lasso));
        }
    }

    private void UpdateSprite(Entity<PlayerLassoComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        var fraction = 1f;
        if (ent.Comp.Anchor is { } anchor && ent.Comp.MaxDistance > 0f)
        {
            var used = (_transform.GetWorldPosition(anchor) - _transform.GetWorldPosition(ent.Owner)).Length();
            fraction = Math.Clamp((ent.Comp.MaxDistance - used) / ent.Comp.MaxDistance, 0f, 1f);
        }

        var maxStep = ent.Comp.SpriteSteps;
        var step = Math.Clamp((int) MathF.Ceiling(fraction * maxStep), 0, maxStep);
        var state = StatePrefix + step;

        if (!_sprite.TryGetLayer((ent.Owner, sprite), 0, out var layer, false) || layer.State.Name == state)
            return;

        _sprite.LayerSetRsiState((ent.Owner, sprite), 0, state);
    }
}
