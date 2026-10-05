using Content.Shared._Horizon.StationDeployment.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.StationRecords;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Horizon.StationDeployment;

/// <summary>
/// Handles swiping an ID card on a <see cref="StationDeploymentKitComponent"/> to link/unlink it,
/// ahead of the actual deploy step (server-side, see StationDeploymentSystem).
/// </summary>
public sealed class StationDeploymentKitSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Note: subscribed on the kit's own component + AfterInteractUsingEvent (target-side, low-priority)
        // rather than on StationRecordKeyStorageComponent + AfterInteractEvent (used-side), because
        // GridAccessSystem already owns that (component, event) pair - RobustToolbox only allows one
        // directed subscriber per pair, and a second one throws at startup.
        SubscribeLocalEvent<StationDeploymentKitComponent, AfterInteractUsingEvent>(OnIdCardSwipe);
    }

    private void OnIdCardSwipe(EntityUid kitUid, StationDeploymentKitComponent kit, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var idCardUid = args.Used;

        // Is the item swiped on this kit actually an id card? If not, ignore it.
        if (!TryComp<StationRecordKeyStorageComponent>(idCardUid, out _))
            return;

        args.Handled = true;

        if (TryComp<StationDeedComponent>(idCardUid, out var deed) && deed.StationUid != null)
        {
            _popup.PopupClient(Loc.GetString("station-deployment-kit-id-already-deeded"), kitUid, args.User, PopupType.Medium);
            _audio.PlayLocal(kit.ErrorSound, kitUid, args.User);
            return;
        }

        if (kit.LinkedIdCard == idCardUid)
        {
            _popup.PopupClient(Loc.GetString("station-deployment-kit-unlinked"), kitUid, args.User, PopupType.Medium);
            _audio.PlayLocal(kit.SwipeSound, kitUid, args.User);
            kit.LinkedIdCard = null;
        }
        else
        {
            _popup.PopupClient(Loc.GetString("station-deployment-kit-linked"), kitUid, args.User, PopupType.Medium);
            _audio.PlayLocal(kit.SwipeSound, kitUid, args.User);
            kit.LinkedIdCard = idCardUid;
        }

        Dirty(kitUid, kit);
    }
}
