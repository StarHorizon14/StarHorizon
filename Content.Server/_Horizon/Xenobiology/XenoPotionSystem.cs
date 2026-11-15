// Maded by Gorox. Discord - smeshinka112

using Content.Server.Atmos.Components;
using Content.Server.Popups;
using Content.Shared._Horizon.XenoPotion.Components;
using Content.Shared._Horizon.XenoPotionEffected.Components;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Horizon.Xenobiology;

public sealed class XenoPotionSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    /// <summary>
    /// Barotrauma only counts protection from these slots
    /// </summary>
    private const SlotFlags PressureSlots = SlotFlags.HEAD | SlotFlags.OUTERCLOTHING;

    private const float SpaceproofLowPressureMultiplier = 1000f;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, XenoPotionComponent component, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp<ClothingComponent>(target, out var clothing))
        {
            _popup.PopupEntity(Loc.GetString("potion-not-clothing"), target, args.User);
            return;
        }

        var name = MetaData(target).EntityName;

        switch (component.Effect)
        {
            case "Speed":
                // Remove the slowdown, the same as the red potion in tg
                if (!TryComp<ClothingSpeedModifierComponent>(target, out var speed) ||
                    speed.WalkModifier >= 1f && speed.SprintModifier >= 1f)
                {
                    _popup.PopupEntity(Loc.GetString("potion-speed-already"), target, args.User);
                    return;
                }

                RemComp<ClothingSpeedModifierComponent>(target);
                _metaData.SetEntityName(target, Loc.GetString("potion-speed-name-prefix", ("target", name)));

                if (_container.TryGetContainingContainer((target, null, null), out var container))
                    _movementSpeed.RefreshMovementSpeedModifiers(container.Owner);
                break;

            case "Pressure":
                if ((clothing.Slots & PressureSlots) == 0)
                {
                    _popup.PopupEntity(Loc.GetString("potion-pressure-wrong-slot"), target, args.User);
                    return;
                }

                var pressure = EnsureComp<PressureProtectionComponent>(target);
                if (pressure.LowPressureMultiplier >= SpaceproofLowPressureMultiplier)
                {
                    _popup.PopupEntity(Loc.GetString("potion-pressure-already"), target, args.User);
                    return;
                }

                pressure.LowPressureMultiplier = SpaceproofLowPressureMultiplier;
                _metaData.SetEntityName(target, Loc.GetString("potion-pressure-name-prefix", ("target", name)));
                break;

            default:
                return;
        }

        var effected = EnsureComp<XenoPotionEffectedComponent>(target);
        effected.Color = component.Color;
        effected.Enabled = true;
        Dirty(target, effected);

        _popup.PopupEntity(Loc.GetString("potion-applied", ("target", target)), target, args.User);
        QueueDel(uid);
        args.Handled = true;
    }
}
