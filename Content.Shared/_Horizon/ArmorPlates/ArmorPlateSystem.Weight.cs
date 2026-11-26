using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Horizon.ArmorPlates;

/// <summary>
/// Замедление от бронепластин: пластина замедляет того, кто её несёт —
/// в руках, карманах, одежде, рюкзаке и любых вложенных контейнерах.
/// </summary>
public sealed partial class ArmorPlateSystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Как часто перепроверять, у кого пластины (на случай, если переместили рюкзак с пластинами внутри).
    /// </summary>
    private static readonly TimeSpan CarrierCheckInterval = TimeSpan.FromSeconds(0.5);

    private TimeSpan _nextCarrierCheck;

    private void InitializeWeight()
    {
        SubscribeLocalEvent<ArmorPlateComponent, EntGotInsertedIntoContainerMessage>(OnPlateInserted);
        SubscribeLocalEvent<ArmorPlateComponent, EntGotRemovedFromContainerMessage>(OnPlateRemoved);
        SubscribeLocalEvent<ArmorPlateComponent, ComponentShutdown>(OnPlateShutdown);
        SubscribeLocalEvent<ArmorPlateCarrierComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCarrierCheck)
            return;

        _nextCarrierCheck = _timing.CurTime + CarrierCheckInterval;

        var query = EntityQueryEnumerator<ArmorPlateComponent>();
        while (query.MoveNext(out var uid, out var plate))
        {
            UpdateCarrier((uid, plate));
        }
    }

    private void OnPlateInserted(Entity<ArmorPlateComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        UpdateCarrier(ent);
    }

    private void OnPlateRemoved(Entity<ArmorPlateComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        UpdateCarrier(ent);
    }

    private void OnPlateShutdown(Entity<ArmorPlateComponent> ent, ref ComponentShutdown args)
    {
        SetCarrier(ent, null);
    }

    private void OnRefreshSpeed(Entity<ArmorPlateCarrierComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var walk = 1f;
        var sprint = 1f;

        foreach (var plateUid in ent.Comp.Plates)
        {
            if (!TryComp<ArmorPlateComponent>(plateUid, out var plate))
                continue;

            walk *= plate.WalkSpeedModifier;
            sprint *= plate.SprintSpeedModifier;
        }

        args.ModifySpeed(walk, sprint);
    }

    /// <summary>
    /// Находит, кто несёт пластину: первый владелец по цепочке контейнеров, у которого есть скорость передвижения.
    /// </summary>
    private EntityUid? FindCarrier(EntityUid plate)
    {
        var current = plate;

        // Ограничение глубины на всякий случай.
        for (var i = 0; i < 16; i++)
        {
            if (!_container.TryGetContainingContainer(current, out var container))
                return null;

            current = container.Owner;
            if (HasComp<MovementSpeedModifierComponent>(current))
                return current;
        }

        return null;
    }

    private void UpdateCarrier(Entity<ArmorPlateComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        SetCarrier(ent, FindCarrier(ent));
    }

    private void SetCarrier(Entity<ArmorPlateComponent> ent, EntityUid? carrier)
    {
        if (ent.Comp.Carrier == carrier)
            return;

        var old = ent.Comp.Carrier;
        ent.Comp.Carrier = carrier;

        if (old != null && TryComp<ArmorPlateCarrierComponent>(old, out var oldComp))
        {
            oldComp.Plates.Remove(ent);

            if (oldComp.Plates.Count == 0)
                RemComp<ArmorPlateCarrierComponent>(old.Value);

            if (!TerminatingOrDeleted(old.Value))
                _movementSpeed.RefreshMovementSpeedModifiers(old.Value);
        }

        if (carrier != null && !TerminatingOrDeleted(carrier.Value))
        {
            var newComp = EnsureComp<ArmorPlateCarrierComponent>(carrier.Value);
            newComp.Plates.Add(ent);
            _movementSpeed.RefreshMovementSpeedModifiers(carrier.Value);
        }
    }
}
