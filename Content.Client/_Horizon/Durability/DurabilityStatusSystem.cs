using Content.Client.Items;
using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._Horizon.ArmorPlates;
using Content.Shared._Horizon.Durability;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.Durability;

/// <summary>
/// Показывает прочность оружия и бронепластин в статусе предмета в руке.
/// </summary>
public sealed class DurabilityStatusSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<WeaponWearComponent>(ent => new DurabilityStatusControl(() => PollWeapon(ent)));
        Subs.ItemStatus<ArmorPlateComponent>(ent => new DurabilityStatusControl(() => PollPlate(ent)));
    }

    private DurabilityStatus PollWeapon(EntityUid uid)
    {
        if (!TryComp<WeaponWearComponent>(uid, out var wear))
            return default;

        var stalled = wear.StalledUntil > _timing.CurTime
            ? (int) MathF.Ceiling((float) (wear.StalledUntil - _timing.CurTime).TotalSeconds)
            : 0;

        return new DurabilityStatus(
            DurabilityFormat.Percent(DurabilityFormat.Fraction(wear.Durability, wear.MaxDurability)),
            wear.Broken,
            stalled,
            true);
    }

    private DurabilityStatus PollPlate(EntityUid uid)
    {
        if (!TryComp<ArmorPlateComponent>(uid, out var plate))
            return default;

        return new DurabilityStatus(
            DurabilityFormat.Percent(DurabilityFormat.Fraction(plate.Durability, plate.MaxDurability)),
            plate.Broken,
            0,
            true);
    }
}

public record struct DurabilityStatus(int Percent, bool Broken, int StalledSeconds, bool Valid);

public sealed class DurabilityStatusControl : PollingItemStatusControl<DurabilityStatus>
{
    private readonly Func<DurabilityStatus> _poll;
    private readonly RichTextLabel _label;

    public DurabilityStatusControl(Func<DurabilityStatus> poll)
    {
        _poll = poll;
        _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
        AddChild(_label);
    }

    protected override DurabilityStatus PollData()
    {
        return _poll();
    }

    protected override void Update(in DurabilityStatus data)
    {
        if (!data.Valid)
        {
            _label.SetMarkup(string.Empty);
            return;
        }

        if (data.Broken)
        {
            _label.SetMarkup(Loc.GetString("durability-status-broken"));
            return;
        }

        var color = DurabilityFormat.Color(data.Percent / 100f);

        _label.SetMarkup(data.StalledSeconds > 0
            ? Loc.GetString("durability-status-stalled", ("color", color), ("percent", data.Percent), ("seconds", data.StalledSeconds))
            : Loc.GetString("durability-status", ("color", color), ("percent", data.Percent)));
    }
}
