using Content.Shared._Horizon.Durability;
using Robust.Client.GameObjects;

namespace Content.Client._Horizon.Durability;

/// <summary>
/// Затемняет спрайт сломанного оружия, чтобы его было видно без осмотра.
/// Исходный цвет спрайта запоминается и возвращается после ремонта.
/// </summary>
public sealed class WeaponWearVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    /// <summary>
    /// Оттенок сломанного оружия: темнее и с лёгкой ржавчиной.
    /// </summary>
    private static readonly Color BrokenTint = new(0.55f, 0.45f, 0.42f);

    private readonly Dictionary<EntityUid, Color> _originalColors = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeaponWearComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WeaponWearComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<WeaponWearComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<WeaponWearComponent> ent, ref ComponentStartup args)
    {
        UpdateTint(ent);
    }

    private void OnHandleState(Entity<WeaponWearComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateTint(ent);
    }

    private void OnShutdown(Entity<WeaponWearComponent> ent, ref ComponentShutdown args)
    {
        _originalColors.Remove(ent);
    }

    private void UpdateTint(Entity<WeaponWearComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var tinted = _originalColors.TryGetValue(ent, out var original);

        if (ent.Comp.Broken && !tinted)
        {
            _originalColors[ent] = sprite.Color;
            _sprite.SetColor((ent, sprite), sprite.Color * BrokenTint);
        }
        else if (!ent.Comp.Broken && tinted)
        {
            _originalColors.Remove(ent);
            _sprite.SetColor((ent, sprite), original);
        }
    }
}
