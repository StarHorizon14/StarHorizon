using System.Numerics;
using Content.Shared._Horizon.PlayerLasso.Components;
using Content.Shared.Alert;
using Content.Shared.CombatMode;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Horizon.PlayerLasso.Systems;

/// <summary>
/// Handles tying a rope between a player and a lasso item, letting the victim walk freely within
/// <see cref="PlayerLassoComponent.MaxDistance"/> of whoever is holding the item before the rope
/// pulls taut. The rope is anchored to the item itself (via the engine's joint-relay mechanism),
/// not to the holder directly, so dropping the lasso does not cut the rope.
/// </summary>
public sealed class PlayerLassoSystem : EntitySystem
{
    /// <summary>
    /// Minimum distance the reel-in can pull someone towards a non-mob structure, so they don't
    /// end up overlapping/inside it. One tile.
    /// </summary>
    private const float StructureReelStopDistance = 1f;

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerLassoComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PlayerLassoComponent, TieLassoDoAfterEvent>(OnTieDoAfter);
        SubscribeLocalEvent<PlayerLassoComponent, JointRemovedEvent>(OnJointRemoved);
        SubscribeLocalEvent<PlayerLassoComponent, UntieStructureDoAfterEvent>(OnUntieStructureDoAfter);
        SubscribeLocalEvent<PlayerLassoComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<LassoAnchoredComponent, GetVerbsEvent<Verb>>(AddUntieStructureVerb);

        SubscribeLocalEvent<LassoedComponent, GetVerbsEvent<Verb>>(AddUntieVerb);
        SubscribeLocalEvent<LassoedComponent, UntieLassoDoAfterEvent>(OnUntieDoAfter);
        SubscribeLocalEvent<LassoedComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<LassoedComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<LassoedComponent, ToggleLassoBraceAlertEvent>(OnToggleBraceAlert);
        SubscribeLocalEvent<LassoedComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<LassoedComponent, ComponentShutdown>(OnLassoedShutdown);

        SubscribeAllEvent<RequestLassoReelMessage>(OnReelRequest);
    }

    private void OnAfterInteract(Entity<PlayerLassoComponent> ent, ref AfterInteractEvent args)
    {
        if (!ent.Comp.TieByHand || args.Handled || args.Target is not { Valid: true } target || target == args.User)
            return;

        if (!TryComp<PhysicsComponent>(target, out var targetPhysics))
            return;

        if (!args.CanReach)
        {
            _popup.PopupClient(Loc.GetString("player-lasso-too-far-away"), args.User, args.User);
            return;
        }

        var isMob = HasComp<MobStateComponent>(target);

        if (ent.Comp.Anchor != null || (isMob && HasComp<LassoedComponent>(target)))
        {
            _popup.PopupClient(Loc.GetString("player-lasso-already-tied"), args.User, args.User);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.TieDelay, new TieLassoDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnDamage = isMob,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
            args.Handled = true;
    }

    private void OnTieDoAfter(Entity<PlayerLassoComponent> ent, ref TieLassoDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { Valid: true } target)
            return;

        var user = args.Args.User;
        var isMob = HasComp<MobStateComponent>(target);

        if (ent.Comp.Anchor != null || (isMob && HasComp<LassoedComponent>(target)))
            return;

        if (!TryComp<PhysicsComponent>(ent, out var itemPhysics) || !TryComp<PhysicsComponent>(target, out var targetPhysics))
            return;

        if (!_interaction.InRangeUnobstructed(user, target))
            return;

        if (isMob)
            TieToVictim(ent, target, itemPhysics, targetPhysics, user);
        else
            TieToStructure(ent, target, itemPhysics, targetPhysics, user);

        args.Handled = true;
    }

    private void TieToVictim(Entity<PlayerLassoComponent> lassoEnt, EntityUid target, PhysicsComponent itemPhysics, PhysicsComponent targetPhysics, EntityUid user)
    {
        var (item, lasso) = lassoEnt;
        TieVictimRope(lassoEnt, target, itemPhysics, targetPhysics);

        _audio.PlayPredicted(lasso.TieSound, item, user);

        _popup.PopupClient(Loc.GetString("player-lasso-tied-user", ("target", target)), user, user);
        _popup.PopupClient(Loc.GetString("player-lasso-tied-target", ("user", user)), target, target);
    }

    private void TieVictimRope(Entity<PlayerLassoComponent> lassoEnt, EntityUid target, PhysicsComponent itemPhysics, PhysicsComponent targetPhysics)
    {
        var (item, lasso) = lassoEnt;
        CreateLassoJoint(item, target, lasso, itemPhysics, targetPhysics, isMob: true);

        var targetLassoed = EnsureComp<LassoedComponent>(target);
        targetLassoed.LassoItem = item;
        Dirty(target, targetLassoed);

        _alerts.ShowAlert(target, targetLassoed.LassoedAlert);
    }

    /// <summary>
    /// Ties the rope to a player straight away, without a do-after or a range check, e.g. when a shot rope hits them.
    /// Not predicted: call it on the server only.
    /// </summary>
    public bool TryTieVictimRemotely(Entity<PlayerLassoComponent> lassoEnt, EntityUid target, EntityUid? user)
    {
        var (item, lasso) = lassoEnt;

        if (lasso.Anchor != null ||
            !HasComp<MobStateComponent>(target) ||
            HasComp<LassoedComponent>(target) ||
            !TryComp<PhysicsComponent>(item, out var itemPhysics) ||
            !TryComp<PhysicsComponent>(target, out var targetPhysics))
        {
            return false;
        }

        TieVictimRope(lassoEnt, target, itemPhysics, targetPhysics);

        _audio.PlayPvs(lasso.TieSound, item);

        if (user != null)
            _popup.PopupEntity(Loc.GetString("player-lasso-tied-user", ("target", target)), user.Value, user.Value);

        _popup.PopupEntity(Loc.GetString("player-lasso-tied-target", ("user", user ?? item)), target, target);
        return true;
    }

    /// <summary>
    /// Cuts the rope from whatever this lasso is tied to.
    /// </summary>
    public void Release(Entity<PlayerLassoComponent> ent)
    {
        if (ent.Comp.Anchor is not { } anchor)
            return;

        if (HasComp<MobStateComponent>(anchor))
            Untie(anchor);
        else
            UntieStructure(ent);
    }

    private void TieToStructure(Entity<PlayerLassoComponent> lassoEnt, EntityUid target, PhysicsComponent itemPhysics, PhysicsComponent targetPhysics, EntityUid user)
    {
        var (item, lasso) = lassoEnt;
        CreateLassoJoint(item, target, lasso, itemPhysics, targetPhysics, isMob: false);

        var anchored = EnsureComp<LassoAnchoredComponent>(target);
        anchored.LassoItem = item;
        Dirty(target, anchored);

        _audio.PlayPredicted(lasso.TieSound, item, user);
        _popup.PopupClient(Loc.GetString("player-lasso-tied-structure", ("target", target)), user, user);
    }

    private void CreateLassoJoint(EntityUid item, EntityUid target, PlayerLassoComponent lasso, PhysicsComponent itemPhysics, PhysicsComponent targetPhysics, bool isMob)
    {
        var jointId = $"player-lasso-{GetNetEntity(item)}-{GetNetEntity(target)}";

        lasso.Anchor = target;
        lasso.JointId = jointId;

        var joint = _joints.CreateDistanceJoint(item, target, itemPhysics.LocalCenter, targetPhysics.LocalCenter, id: jointId);
        joint.CollideConnected = true;
        // Non-mob anchors are solid structures - keep a minimum length so the holder can't
        // end up overlapping/inside them.
        joint.MinLength = isMob ? 0f : StructureReelStopDistance;
        joint.MaxLength = lasso.MaxDistance;
        joint.Stiffness = 0f;

        var visuals = EnsureComp<JointVisualsComponent>(item);
        visuals.Sprite = lasso.RopeSprite;
        visuals.Target = GetNetEntity(target);
        Dirty(item, visuals);

        Dirty(item, lasso);
    }

    private void OnExamined(Entity<PlayerLassoComponent> ent, ref ExaminedEvent args)
    {
        var lasso = ent.Comp;

        if (lasso.Anchor is not { } anchor)
        {
            args.PushMarkup(Loc.GetString("player-lasso-examine-length", ("length", lasso.MaxDistance)));
            return;
        }

        var itemPos = _transform.GetWorldPosition(ent.Owner);
        var anchorPos = _transform.GetWorldPosition(anchor);
        var used = (anchorPos - itemPos).Length();
        var remaining = MathF.Max(0f, lasso.MaxDistance - used);

        args.PushMarkup(Loc.GetString("player-lasso-examine-length-tied",
            ("length", lasso.MaxDistance),
            ("remaining", MathF.Round(remaining, 1))));
    }

    private void AddUntieStructureVerb(Entity<LassoAnchoredComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract)
            return;

        var user = args.User;
        var item = ent.Comp.LassoItem;
        Verb verb = new()
        {
            Text = Loc.GetString("player-lasso-untie-verb"),
            Act = () => StartUntieStructure(user, item),
            DoContactInteraction = true,
        };
        args.Verbs.Add(verb);
    }

    private void StartUntieStructure(EntityUid user, EntityUid item)
    {
        if (!TryComp<PlayerLassoComponent>(item, out var lasso) || lasso.Anchor == null)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, lasso.UntieDelay, new UntieStructureDoAfterEvent(), item, target: item, used: item)
        {
            BreakOnMove = true,
            NeedHand = false,
            BreakOnDamage = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnUntieStructureDoAfter(Entity<PlayerLassoComponent> ent, ref UntieStructureDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        UntieStructure(ent);
        args.Handled = true;
    }

    private void AddUntieVerb(Entity<LassoedComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract)
            return;

        var user = args.User;
        var victim = ent.Owner;
        Verb verb = new()
        {
            Text = Loc.GetString("player-lasso-untie-verb"),
            Act = () => StartUntie(user, victim),
            DoContactInteraction = true,
        };
        args.Verbs.Add(verb);
    }

    private void StartUntie(EntityUid user, EntityUid victim)
    {
        if (!TryComp<LassoedComponent>(victim, out var lassoed) || !TryComp<PlayerLassoComponent>(lassoed.LassoItem, out var lasso))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, lasso.UntieDelay, new UntieLassoDoAfterEvent(), victim, target: victim)
        {
            BreakOnMove = true,
            NeedHand = false,
            BreakOnDamage = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnUntieDoAfter(Entity<LassoedComponent> ent, ref UntieLassoDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        Untie(ent.Owner);
        args.Handled = true;
    }

    private void OnToggleBraceAlert(Entity<LassoedComponent> ent, ref ToggleLassoBraceAlertEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Braced)
            Unbrace(ent);
        else
            Brace(ent);

        args.Handled = true;
    }

    /// <summary>
    /// Anchors the victim in place, blocking their own movement input, so pulling on the
    /// rope drags the holder towards them instead.
    /// </summary>
    private void Brace(Entity<LassoedComponent> ent)
    {
        var xform = Transform(ent.Owner);

        if (xform.GridUid != xform.ParentUid)
        {
            _popup.PopupClient(Loc.GetString("player-lasso-brace-cant"), ent.Owner, ent.Owner);
            return;
        }

        _transform.AnchorEntity(ent.Owner, xform);

        if (!xform.Anchored)
        {
            _popup.PopupClient(Loc.GetString("player-lasso-brace-cant"), ent.Owner, ent.Owner);
            return;
        }

        ent.Comp.Braced = true;
        Dirty(ent);

        _alerts.ClearAlert(ent.Owner, ent.Comp.LassoedAlert);
        _alerts.ShowAlert(ent.Owner, ent.Comp.BracedAlert);
        _popup.PopupClient(Loc.GetString("player-lasso-brace-on"), ent.Owner, ent.Owner);
    }

    private void Unbrace(Entity<LassoedComponent> ent)
    {
        var xform = Transform(ent.Owner);

        if (xform.Anchored)
            _transform.Unanchor(ent.Owner, xform);

        ent.Comp.Braced = false;
        Dirty(ent);

        _alerts.ClearAlert(ent.Owner, ent.Comp.BracedAlert);
        _alerts.ShowAlert(ent.Owner, ent.Comp.LassoedAlert);
        _popup.PopupClient(Loc.GetString("player-lasso-brace-off"), ent.Owner, ent.Owner);
    }

    private void OnUpdateCanMove(Entity<LassoedComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.Braced)
            args.Cancel();
    }

    private void OnLassoedShutdown(Entity<LassoedComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.Braced)
            return;

        var xform = Transform(ent.Owner);

        if (xform.Anchored)
            _transform.Unanchor(ent.Owner, xform);
    }

    private bool TryGetHolder(EntityUid item, out EntityUid holder)
    {
        holder = default;

        if (!_container.TryGetOuterContainer(item, Transform(item), out var container))
            return false;

        if (!_hands.IsHolding(container.Owner, item, out _))
            return false;

        holder = container.Owner;
        return true;
    }

    private void OnReelRequest(RequestLassoReelMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } holder)
            return;

        if (!_hands.TryGetActiveItem(holder, out var activeItem) || !TryComp<PlayerLassoComponent>(activeItem, out var lasso) || lasso.Anchor == null)
            return;

        var state = msg.State;

        if (state != LassoPullState.None &&
            (!TryComp<CombatModeComponent>(holder, out var combat) || !combat.IsInCombatMode))
        {
            state = LassoPullState.None;
        }

        if (lasso.PullState == state)
            return;

        lasso.PullState = state;
        Dirty(activeItem.Value, lasso);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PlayerLassoComponent>();

        while (query.MoveNext(out var item, out var lasso))
        {
            if (lasso.PullState == LassoPullState.None || lasso.Anchor is not { } anchor)
                continue;

            if (!TryGetHolder(item, out var holder))
            {
                lasso.PullState = LassoPullState.None;
                continue;
            }

            if (!TryComp<JointComponent>(item, out var jointComp) ||
                !jointComp.GetJoints.TryGetValue(lasso.JointId, out var joint) ||
                joint is not DistanceJoint distance)
            {
                lasso.PullState = LassoPullState.None;
                continue;
            }

            var isMobAnchor = HasComp<MobStateComponent>(anchor);
            var minDistance = isMobAnchor ? 0f : StructureReelStopDistance;

            if (lasso.PullState == LassoPullState.Reeling)
                ReelIn(lasso, minDistance, distance, frameTime);
            else
                LetOut(lasso, minDistance, distance, frameTime);

            _physics.WakeBody(joint.BodyAUid);
            _physics.WakeBody(joint.BodyBUid);
            Dirty(item, jointComp);
        }
    }

    private void ReelIn(PlayerLassoComponent lasso, float minDistance, DistanceJoint distance, float frameTime)
    {
        // Purely shrink the joint's max length like the grappling hook does - let the physics
        // solver pull the bodies in itself (the braced/heavier side naturally stays put), instead
        // of also setting velocity by hand. Doing both at once can fight the solver's own position
        // correction near the limit and fling a body off in a random direction.
        distance.MinLength = minDistance;
        distance.MaxLength = MathF.Max(distance.MinLength, distance.MaxLength - lasso.ReelRate * frameTime);
        distance.Length = MathF.Min(distance.MaxLength, distance.Length);

        if (distance.MaxLength.Equals(distance.MinLength))
            lasso.PullState = LassoPullState.None;
    }

    private void LetOut(PlayerLassoComponent lasso, float minDistance, DistanceJoint distance, float frameTime)
    {
        // Letting the rope out doesn't push anyone - it just slackens the tether, letting the
        // holder walk further out on their own until it reaches the lasso's full length.
        distance.MinLength = minDistance;
        distance.MaxLength = MathF.Min(lasso.MaxDistance, distance.MaxLength + lasso.ReelRate * frameTime);

        if (distance.MaxLength.Equals(lasso.MaxDistance))
            lasso.PullState = LassoPullState.None;
    }

    private void OnJointRemoved(Entity<PlayerLassoComponent> ent, ref JointRemovedEvent args)
    {
        if (args.Joint.ID != ent.Comp.JointId || ent.Comp.Anchor is not { } anchor)
            return;

        // The server unties the rope itself and sends the result; a joint removed while applying its state
        // must not untie on the client. During FTL the joint is cleared by the map change and recreated later.
        if (_timing.ApplyingState || ent.Comp.InTransit)
            return;

        if (HasComp<MobStateComponent>(anchor))
            Untie(anchor);
        else
            UntieStructure(ent);
    }

    private void OnMobStateChanged(Entity<LassoedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            Untie(ent.Owner);
    }

    private void OnInsertedIntoContainer(Entity<LassoedComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        Untie(ent.Owner);
    }

    /// <summary>
    /// Cuts the rope between the victim and the lasso item, cleaning up both sides.
    /// Dropping or moving the lasso item itself does not call this - only untying, death,
    /// or the victim entering a container does.
    /// </summary>
    public void Untie(EntityUid victim)
    {
        if (!TryComp<LassoedComponent>(victim, out var lassoed))
            return;

        var item = lassoed.LassoItem;

        RemComp<LassoedComponent>(victim);
        _alerts.ClearAlert(victim, lassoed.LassoedAlert);
        _alerts.ClearAlert(victim, lassoed.BracedAlert);

        if (TryComp<PlayerLassoComponent>(item, out var lasso) && lasso.Anchor == victim)
        {
            ClearLassoJoint((item, lasso));
            _audio.PlayPredicted(lasso.UntieSound, item, victim);
        }
    }

    /// <summary>
    /// Cuts the rope between a structure and the lasso item.
    /// </summary>
    public void UntieStructure(Entity<PlayerLassoComponent> ent)
    {
        var (item, lasso) = ent;

        if (lasso.Anchor is not { } anchor || HasComp<MobStateComponent>(anchor))
            return;

        if (TryComp<LassoAnchoredComponent>(anchor, out var anchored) && anchored.LassoItem == item)
            RemComp<LassoAnchoredComponent>(anchor);

        ClearLassoJoint(ent);
        _audio.PlayPredicted(lasso.UntieSound, item, item);
    }

    /// <summary>
    /// The body that carries the item's end of the rope: the item itself when it lies on the floor,
    /// or whoever holds it, because the joint is relayed out of containers.
    /// </summary>
    public EntityUid GetItemEnd(EntityUid item)
    {
        return TryComp<JointComponent>(item, out var joint) && joint.Relay is { } relay ? relay : item;
    }

    /// <summary>
    /// Lets the braced victim move again, e.g. so a departing shuttle can drag them along.
    /// </summary>
    public void ReleaseBrace(EntityUid victim)
    {
        if (TryComp<LassoedComponent>(victim, out var lassoed) && lassoed.Braced)
            Unbrace((victim, lassoed));
    }

    /// <summary>
    /// Keeps the rope tied while both of its ends change maps together and their joint gets cleared.
    /// Must be followed by <see cref="EndTransit"/> once they arrive.
    /// </summary>
    public void BeginTransit(Entity<PlayerLassoComponent> ent)
    {
        var lasso = ent.Comp;

        if (lasso.Anchor == null || lasso.InTransit)
            return;

        lasso.TransitLength = lasso.MaxDistance;

        if (TryComp<JointComponent>(ent, out var jointComp) &&
            jointComp.GetJoints.TryGetValue(lasso.JointId, out var joint) &&
            joint is DistanceJoint distance)
        {
            lasso.TransitLength = distance.MaxLength;
        }

        lasso.InTransit = true;
        lasso.PullState = LassoPullState.None;
        Dirty(ent);
    }

    /// <summary>
    /// Recreates the rope joint after <see cref="BeginTransit"/>, or unties the rope if its ends got separated.
    /// </summary>
    public void EndTransit(Entity<PlayerLassoComponent> ent)
    {
        var (item, lasso) = ent;

        if (!lasso.InTransit)
            return;

        lasso.InTransit = false;
        Dirty(ent);

        if (lasso.Anchor is not { } anchor)
            return;

        var isMob = HasComp<MobStateComponent>(anchor);
        var itemMap = Transform(item).MapID;

        if (TerminatingOrDeleted(anchor) ||
            itemMap == MapId.Nullspace ||
            itemMap != Transform(anchor).MapID ||
            !TryComp<PhysicsComponent>(item, out var itemPhysics) ||
            !TryComp<PhysicsComponent>(anchor, out var anchorPhysics))
        {
            if (isMob)
                Untie(anchor);
            else
                UntieStructure(ent);

            return;
        }

        // The map change already cleared it, this only guards against a joint that somehow survived.
        _joints.RemoveJoint(item, lasso.JointId);

        CreateLassoJoint(item, anchor, lasso, itemPhysics, anchorPhysics, isMob);

        if (TryComp<JointComponent>(item, out var jointComp) &&
            jointComp.GetJoints.TryGetValue(lasso.JointId, out var joint) &&
            joint is DistanceJoint distance)
        {
            distance.MaxLength = Math.Clamp(lasso.TransitLength, distance.MinLength, lasso.MaxDistance);
            distance.Length = MathF.Min(distance.Length, distance.MaxLength);
            Dirty(item, jointComp);
        }
    }

    private void ClearLassoJoint(Entity<PlayerLassoComponent> ent)
    {
        var (item, lasso) = ent;

        RemComp<JointVisualsComponent>(item);

        if (!string.IsNullOrEmpty(lasso.JointId))
            _joints.RemoveJoint(item, lasso.JointId);

        lasso.Anchor = null;
        lasso.JointId = string.Empty;
        lasso.PullState = LassoPullState.None;
        lasso.InTransit = false;
        Dirty(item, lasso);
    }
}

[Serializable, NetSerializable]
public sealed class RequestLassoReelMessage : EntityEventArgs
{
    public LassoPullState State;

    public RequestLassoReelMessage(LassoPullState state)
    {
        State = state;
    }
}

[Serializable, NetSerializable]
public sealed partial class TieLassoDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class UntieLassoDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class UntieStructureDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class ToggleLassoBraceAlertEvent : BaseAlertEvent;
