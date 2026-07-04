using Content.Server.Popups;
using Content.Server.Research.Systems;
using Content.Shared._Horizon._Fractions.AnCo.Cryptominer;
using Content.Shared.Interaction;
using Content.Shared.Research.Components;

namespace Content.Server._Horizon._Fractions.AnCo.Cryptominer;

public sealed class CryptominerDiskResearchSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ResearchSystem _research = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CryptominerDiskComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, CryptominerDiskComponent disk, AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        if (!args.CanReach)
            return;

        if (args.Target == null)
            return;

        if (!TryComp<ResearchServerComponent>(args.Target, out var server))
            return;

        var credits = disk.StoredCredits;

        if (credits <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cryptominer-disk-empty"), args.Target.Value, args.User);
            args.Handled = true;
            return;
        }

        _research.ModifyServerPoints(args.Target.Value, credits, server);
        _popup.PopupEntity(Loc.GetString("cryptominer-disk-research-converted", ("points", credits)), args.Target.Value, args.User);

        QueueDel(uid);
        args.Handled = true;
    }
}
