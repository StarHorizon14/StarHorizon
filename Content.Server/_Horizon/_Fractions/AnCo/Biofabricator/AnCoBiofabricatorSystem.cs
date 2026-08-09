using Content.Server.Materials;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Preferences.Managers;
using Content.Shared._Horizon._Fractions.AnCo.Biofabricator;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Implants;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Horizon._Fractions.AnCo.Biofabricator;

public sealed class AnCoBiofabricatorSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _subdermalImplant = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly MaterialStorageSystem _material = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnCoBiofabricatorComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<AnCoBiofabricatorComponent, EntInsertedIntoContainerMessage>(OnCardInserted);
    }

    private void OnComponentInit(Entity<AnCoBiofabricatorComponent> ent, ref ComponentInit args)
    {
        ent.Comp.BodyContainer = _container.EnsureContainer<ContainerSlot>(ent.Owner, "biofab-bodyContainer");
    }

    private void OnCardInserted(Entity<AnCoBiofabricatorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.CardSlotId)
            return;

        TryStartRestore(ent, null);
    }

    /// <summary>
    /// Looks up the Biofabricator a memory card is currently inserted into, if any.
    /// </summary>
    public bool TryFindFabricatorForCard(EntityUid cardUid, out Entity<AnCoBiofabricatorComponent> fab)
    {
        var parent = Transform(cardUid).ParentUid;
        if (parent.Valid && TryComp<AnCoBiofabricatorComponent>(parent, out var comp))
        {
            fab = (parent, comp);
            return true;
        }

        fab = default;
        return false;
    }

    /// <summary>
    /// Spawns and dresses the restored body immediately (like CloningPodSystem.TryCloning), transfers the mind
    /// right away since consent was already given when the owner died, and puts the body in BodyContainer until
    /// the restoration timer finishes. Called automatically when a bound, consented card ends up inserted into
    /// a Biofabricator (on card insert, or on consent if the card was already inserted) - no manual activation
    /// needed. <paramref name="user"/> is only used to target popups and may be null when triggered automatically.
    /// </summary>
    public bool TryStartRestore(Entity<AnCoBiofabricatorComponent> fab, EntityUid? user)
    {
        if (fab.Comp.Status != AnCoBiofabricatorStatus.Idle)
            return false;

        if (!this.IsPowered(fab.Owner, EntityManager))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("anco-biofabricator-error-no-power"), fab.Owner, user.Value);
            return false;
        }

        var cardUid = _itemSlots.GetItemOrNull(fab.Owner, fab.Comp.CardSlotId);
        if (cardUid == null)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("anco-biofabricator-error-no-card"), fab.Owner, user.Value);
            return false;
        }

        if (!TryComp<AnCoMemoryCardComponent>(cardUid, out var card) ||
            card.OwnerUserId == null)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("anco-biofabricator-error-card-not-bound"), fab.Owner, user.Value);
            return false;
        }

        if (!card.ConsentGranted)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("anco-biofabricator-error-no-consent"), fab.Owner, user.Value);
            return false;
        }

        if (_prefsManager.GetPreferences(card.OwnerUserId.Value).SelectedCharacter is not HumanoidCharacterProfile profile)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("anco-biofabricator-error-card-not-bound"), fab.Owner, user.Value);
            return false;
        }

        if (!_prototype.TryIndex<SpeciesPrototype>(profile.Species, out var species))
            return false;

        var biomassAmount = _material.GetMaterialAmount(fab.Owner, fab.Comp.RequiredMaterial);
        if (biomassAmount < fab.Comp.BiomassCost)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("anco-biofabricator-error-no-biomass", ("required", fab.Comp.BiomassCost)), fab.Owner, user.Value);
            return false;
        }

        var attempt = new AnCoBiofabricatorRestoreAttemptEvent(profile);
        RaiseLocalEvent(fab.Owner, ref attempt);
        if (attempt.Cancelled)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("anco-biofabricator-error-restore-cancelled"), fab.Owner, user.Value);
            return false;
        }

        _material.TryChangeMaterialAmount(fab.Owner, fab.Comp.RequiredMaterial, -fab.Comp.BiomassCost);

        var newBody = Spawn(species.Prototype, Transform(fab.Owner).Coordinates);

        _humanoid.LoadProfile(newBody, profile);
        _metaData.SetEntityName(newBody, profile.Name);

        foreach (var traitId in profile.TraitPreferences)
        {
            if (!_prototype.TryIndex<TraitPrototype>(traitId, out var trait))
                continue;

            if (_whitelist.IsWhitelistFail(trait.Whitelist, newBody) ||
                _whitelist.IsBlacklistPass(trait.Blacklist, newBody))
                continue;

            if (!trait.RequirmentsMet(profile, EntityManager))
                continue;

            if (trait.Components != null)
                EntityManager.AddComponents(newBody, trait.Components, false);
        }

        foreach (var implantId in card.StoredImplants)
            _subdermalImplant.AddImplant(newBody, implantId);

        var restoredEvent = new AnCoBiofabricatorBodyRestoredEvent(newBody, card);
        RaiseLocalEvent(fab.Owner, ref restoredEvent);

        if (_mind.TryGetMind(card.OwnerUserId.Value, out var mindId, out var mind))
            _mind.TransferTo(mindId.Value, newBody, mind: mind);

        card.ConsentGranted = false;
        card.StoredImplants.Clear();

        _container.Insert(newBody, fab.Comp.BodyContainer);

        fab.Comp.RestoreProgress = 0f;
        EnsureComp<AnCoActiveBiofabricatorComponent>(fab.Owner);
        UpdateStatus(fab.Owner, AnCoBiofabricatorStatus.Restoring, fab.Comp);

        _popup.PopupEntity(Loc.GetString("anco-biofabricator-restore-started"), fab.Owner);
        return true;
    }

    public void UpdateStatus(EntityUid uid, AnCoBiofabricatorStatus status, AnCoBiofabricatorComponent fab)
    {
        fab.Status = status;
        _appearance.SetData(uid, AnCoBiofabricatorVisuals.Status, fab.Status);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var activeQuery = EntityQueryEnumerator<AnCoActiveBiofabricatorComponent, AnCoBiofabricatorComponent>();
        while (activeQuery.MoveNext(out var uid, out _, out var fab))
        {
            if (fab.Status != AnCoBiofabricatorStatus.Restoring)
                continue;

            if (fab.BodyContainer.ContainedEntity == null)
                continue;

            if (!this.IsPowered(uid, EntityManager))
                continue;

            fab.RestoreProgress += frameTime;
            if (fab.RestoreProgress < fab.RestoreTime)
                continue;

            Eject(uid, fab);
        }

        // Idle fabricators may be holding a consented card that failed to start earlier for a fixable reason
        // (e.g. not enough biomass yet) - recheck every tick so restoration begins as soon as it becomes possible.
        var idleQuery = EntityQueryEnumerator<AnCoBiofabricatorComponent>();
        while (idleQuery.MoveNext(out var uid, out var fab))
        {
            if (fab.Status != AnCoBiofabricatorStatus.Idle)
                continue;

            var cardUid = _itemSlots.GetItemOrNull(uid, fab.CardSlotId);
            if (cardUid == null || !TryComp<AnCoMemoryCardComponent>(cardUid, out var card) || !card.ConsentGranted)
                continue;

            TryStartRestore((uid, fab), null);
        }
    }

    public void Eject(EntityUid uid, AnCoBiofabricatorComponent fab)
    {
        if (fab.BodyContainer.ContainedEntity is { Valid: true } entity)
            _container.Remove(entity, fab.BodyContainer);

        fab.RestoreProgress = 0f;
        UpdateStatus(uid, AnCoBiofabricatorStatus.Idle, fab);
        RemCompDeferred<AnCoActiveBiofabricatorComponent>(uid);
    }
}
