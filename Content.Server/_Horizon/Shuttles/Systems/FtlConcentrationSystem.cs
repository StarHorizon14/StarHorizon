using Content.Server.Shuttles.Components;
using Content.Shared._Horizon.Shuttles.Components;
using Content.Shared._Horizon.Shuttles.Events;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.Shuttles.Systems;

/// <summary>
/// FTL concentration: a button on the MAP tab that, over 30 seconds, halves the radius that must be clear of other
/// grids for a regular FTL jump and triples the jump range. The shuttle is braked actively the whole time (see
/// MoverController, same as holding space); it ends when the player cancels, picks an inertia mode or the shuttle
/// starts an FTL jump.
/// </summary>
public sealed class FtlConcentrationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShuttleConsoleComponent, FtlConcentrationRequest>(OnRequest);
        SubscribeLocalEvent<FTLComponent, ComponentInit>(OnFtlInit);
    }

    private void OnRequest(EntityUid console, ShuttleConsoleComponent comp, FtlConcentrationRequest args)
    {
        if (Transform(console).GridUid is not { } shuttle || !HasComp<ShuttleComponent>(shuttle))
            return;

        if (!args.Enable)
        {
            Cancel(shuttle);
            return;
        }

        if (HasComp<FTLComponent>(shuttle) || HasComp<FtlConcentrationComponent>(shuttle))
            return;

        var concentration = EnsureComp<FtlConcentrationComponent>(shuttle);
        concentration.StartTime = _timing.CurTime;
        Dirty(shuttle, concentration);
    }

    /// <summary>A jump (even just the start-up of one) ends the concentration.</summary>
    private void OnFtlInit(Entity<FTLComponent> ent, ref ComponentInit args)
    {
        RemCompDeferred<FtlConcentrationComponent>(ent);
    }

    /// <summary>Called when the player picks an inertia mode: any change ends the concentration.</summary>
    public void OnInertiaModeChosen(EntityUid shuttle, InertiaDampeningMode mode)
    {
        if (mode == InertiaDampeningMode.Query)
            return;

        Cancel(shuttle);
    }

    public void Cancel(EntityUid shuttle)
    {
        RemCompDeferred<FtlConcentrationComponent>(shuttle);
    }

    /// <summary>Fraction of the normal FTL proximity radius that currently applies to the shuttle.</summary>
    public float GetRangeFactor(EntityUid shuttle)
    {
        return TryComp<FtlConcentrationComponent>(shuttle, out var concentration)
            ? concentration.GetRangeFactor(_timing.CurTime)
            : 1f;
    }
}
