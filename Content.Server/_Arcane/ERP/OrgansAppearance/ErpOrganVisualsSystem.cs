using Content.Server._Arcane.ERP.Preferences;
using Content.Server.Preferences.Managers;
using Content.Shared._Arcane.ERP;
using Content.Shared._Arcane.ERP.Organs;
using Content.Shared._Arcane.ERP.OrgansAppearance;
using Content.Shared._Arcane.ERP.Preferences;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Arcane.ERP.OrgansAppearance;

public sealed class ErpOrganVisualsSystem : EntitySystem
{
    [Dependency] private readonly ErpOrganPreferencesManager _erpPrefs = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    // By the time OrganRemovedFromBodyEvent fires, the organ's container entry (and thus its slot id) is already
    // gone, so we have to remember it from when the organ was added.
    private readonly Dictionary<EntityUid, string> _organSlotIds = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<EroticOrgansComponent, EroticOrgansSpawnedEvent>(OnOrgansSpawned);
        SubscribeLocalEvent<EroticOrganComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<EroticOrganComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);
        SubscribeLocalEvent<EroticOrganComponent, ComponentShutdown>(OnOrganShutdown);
    }

    private void OnOrganShutdown(Entity<EroticOrganComponent> ent, ref ComponentShutdown args)
    {
        _organSlotIds.Remove(ent.Owner);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!HasComp<HumanoidAppearanceComponent>(args.Entity) || !HasComp<EroticOrgansComponent>(args.Entity))
            return;

        var userId = args.Player.UserId;
        var slot = _prefs.GetPreferences(userId).SelectedCharacterIndex;
        RebuildOrganVisuals(args.Entity, userId, slot);
    }

    private void OnOrgansSpawned(Entity<EroticOrgansComponent> ent, ref EroticOrgansSpawnedEvent args)
    {
        if (!HasComp<HumanoidAppearanceComponent>(ent))
            return;

        // Only rebuild if a player is already controlling this entity.
        // If not, PlayerAttached will handle it later.
        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var userId = actor.PlayerSession.UserId;
        var slot = _prefs.GetPreferences(userId).SelectedCharacterIndex;
        RebuildOrganVisuals(ent, userId, slot);
    }

    private void OnOrganAdded(Entity<EroticOrganComponent> ent, ref OrganAddedToBodyEvent args)
    {
        var slotId = GetOrganSlotId(ent);
        if (string.IsNullOrEmpty(slotId))
            return;

        _organSlotIds[ent.Owner] = slotId;

        var visuals = EnsureComp<ErpOrganVisualsComponent>(args.Body);
        var eroticComp = CompOrNull<EroticOrgansComponent>(args.Body);
        visuals.HideWhenFlaccid = GetHideWhenFlaccid(eroticComp);

        ErpOrganConfig cfg;
        if (TryComp<ActorComponent>(args.Body, out var actor))
        {
            var userId = actor.PlayerSession.UserId;
            var slot = _prefs.GetPreferences(userId).SelectedCharacterIndex;
            var organPrefs = _erpPrefs.GetCached(userId, slot) ?? ErpOrganPreferences.Default();
            cfg = organPrefs.Organs.TryGetValue(slotId, out var saved)
                ? saved
                : new ErpOrganConfig { Variant = GetDefaultVariant(eroticComp, slotId, ent.Comp) };
        }
        else
        {
            cfg = new ErpOrganConfig { Variant = GetDefaultVariant(eroticComp, slotId, ent.Comp) };
        }

        visuals.Organs[slotId] = cfg;
        Dirty(args.Body, visuals);
    }

    private void OnOrganRemoved(Entity<EroticOrganComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (!_organSlotIds.Remove(ent.Owner, out var slotId))
            return;

        if (!TryComp<ErpOrganVisualsComponent>(args.OldBody, out var visuals))
            return;

        visuals.Organs.Remove(slotId);
        Dirty(args.OldBody, visuals);
    }

    private string? GetOrganSlotId(EntityUid organEnt)
    {
        if (!_containers.TryGetContainingContainer(organEnt, out var container))
            return null;

        var containerId = container.ID;
        if (!containerId.StartsWith(SharedBodySystem.OrganSlotContainerIdPrefix))
            return null;

        return containerId[SharedBodySystem.OrganSlotContainerIdPrefix.Length..];
    }

    private void RebuildOrganVisuals(EntityUid uid, NetUserId userId, int slot)
    {
        var organPrefs = _erpPrefs.GetCached(userId, slot) ?? ErpOrganPreferences.Default();
        var eroticComp = CompOrNull<EroticOrgansComponent>(uid);

        var organs = new Dictionary<string, ErpOrganConfig>();
        foreach (var organ in _body.GetBodyOrganEntityComps<EroticOrganComponent>((uid, null)))
        {
            var slotId = GetOrganSlotId(organ.Owner);
            if (string.IsNullOrEmpty(slotId))
                continue;

            _organSlotIds[organ.Owner] = slotId;

            if (organPrefs.Organs.TryGetValue(slotId, out var cfg))
            {
                organs[slotId] = cfg;
            }
            else
            {
                // No saved preference: use a valid species override if defined, otherwise the organ default.
                var defaultVariant = GetDefaultVariant(eroticComp, slotId, organ.Comp1);
                organs[slotId] = new ErpOrganConfig { Variant = defaultVariant };
            }
        }

        var visuals = EnsureComp<ErpOrganVisualsComponent>(uid);
        visuals.Organs = organs;
        visuals.HideWhenFlaccid = GetHideWhenFlaccid(eroticComp);
        Dirty(uid, visuals);
    }

    private static string GetDefaultVariant(EroticOrgansComponent? organs, string slotId, EroticOrganComponent organ)
        => ErpOrganEditorDefinitions.GetDefaultVariant(organs, slotId, organ);

    private static HashSet<string> GetHideWhenFlaccid(EroticOrgansComponent? organs)
        => organs == null ? [] : new HashSet<string>(organs.HideWhenFlaccid);
}
