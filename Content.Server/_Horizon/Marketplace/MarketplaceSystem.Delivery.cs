using Content.Server.Power.Components;
using Content.Shared._Horizon.Marketplace;
using Content.Shared._Lua.StationTeleporter;
using Content.Shared._Lua.StationTeleporter.Components;
using Content.Shared.Audio;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Stacks;
using Content.Shared.Teleportation.Components;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Horizon.Marketplace;

/// <summary>
/// Teleport delivery: an offer accepted from the PDA is sent to a sector teleporter of the requester's choice,
/// paid for with a bluespace crystal from the PDA. The teleporter lights up its portal, the parcel drops out next
/// to it and the portal goes dark again.
/// </summary>
public sealed partial class MarketplaceSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private static readonly SoundSpecifier DeliveryStartSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
    private static readonly SoundSpecifier DeliveryArriveSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    /// <summary>
    /// How long the portal is open before the parcel drops out, and how long it stays open after.
    /// </summary>
    private static readonly TimeSpan DeliveryDropDelay = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan DeliveryCloseDelay = TimeSpan.FromSeconds(3);

    private sealed class PendingDelivery
    {
        public EntityUid Teleporter;
        public EntityUid Item;
        public string Network = string.Empty;
        public EntityCoordinates Fallback;
        public TimeSpan DropAt;
        public TimeSpan CloseAt;
        public bool Dropped;
    }

    private readonly List<PendingDelivery> _deliveries = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_deliveries.Count == 0)
            return;

        var now = _timing.CurTime;
        for (var i = _deliveries.Count - 1; i >= 0; i--)
        {
            var delivery = _deliveries[i];

            if (!delivery.Dropped && now >= delivery.DropAt)
            {
                delivery.Dropped = true;
                DropParcel(delivery);
            }

            if (now < delivery.CloseAt)
                continue;

            _deliveries.RemoveAt(i);
            ClosePortal(delivery.Teleporter);
        }
    }

    private void InitializeDelivery()
    {
        SubscribeLocalEvent<MarketplaceCrystalSlotComponent, EntInsertedIntoContainerMessage>(OnCrystalsInserted);
        SubscribeLocalEvent<MarketplaceCrystalSlotComponent, EntRemovedFromContainerMessage>(OnCrystalsRemoved);
    }

    private void OnCrystalsInserted(Entity<MarketplaceCrystalSlotComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == MarketplaceCrystalSlotComponent.SlotId)
            RefreshPdaProgram(ent);
    }

    private void OnCrystalsRemoved(Entity<MarketplaceCrystalSlotComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == MarketplaceCrystalSlotComponent.SlotId)
            RefreshPdaProgram(ent);
    }

    /// <summary>
    /// Updates the marketplace program if it is the one open on this PDA, e.g. to show the new crystal count.
    /// </summary>
    private void RefreshPdaProgram(EntityUid pda)
    {
        if (TryComp<Content.Shared.CartridgeLoader.CartridgeLoaderComponent>(pda, out var loader)
            && loader.ActiveProgram is { } program
            && TryComp<MarketplaceCartridgeComponent>(program, out var cartridge))
        {
            UpdateCartridge((program, cartridge), pda);
        }
    }

    private int GetCrystalCount(EntityUid pda)
    {
        if (!TryComp<MarketplaceCrystalSlotComponent>(pda, out var slot) || slot.CrystalSlot.Item is not { } crystal)
            return 0;

        return TryComp<StackComponent>(crystal, out var stack) ? _stack.GetCount(crystal, stack) : 1;
    }

    /// <summary>
    /// Sector teleporters in the same sector (map) as the player.
    /// </summary>
    private List<MarketplaceDeliveryPoint> GetDeliveryPoints(EntityUid user)
    {
        var points = new List<MarketplaceDeliveryPoint>();
        var mapId = Transform(user).MapID;

        var query = EntityQueryEnumerator<StationTeleporterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var teleporter, out var xform))
        {
            if (teleporter.Type != TeleporterType.Sector || xform.MapID != mapId)
                continue;

            points.Add(new MarketplaceDeliveryPoint
            {
                Teleporter = GetNetEntity(uid),
                Name = GetTeleporterName(uid, teleporter, xform),
                Powered = IsTeleporterPowered(uid),
            });
        }

        points.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return points;
    }

    /// <summary>
    /// Same naming as the teleporter's own console: custom name, otherwise the station (grid) it stands on.
    /// </summary>
    private string GetTeleporterName(EntityUid uid, StationTeleporterComponent teleporter, TransformComponent xform)
    {
        if (teleporter.CustomName is { } custom)
            return custom;

        return xform.GridUid is { } grid ? Name(grid) : Name(uid);
    }

    private bool IsTeleporterPowered(EntityUid uid)
    {
        return !TryComp<ApcPowerReceiverComponent>(uid, out var power) || power.Powered;
    }

    private void OnCartridgeAccept(Entity<MarketplaceCartridgeComponent> ent,
        EntityUid pda,
        EntityUid user,
        MarketplaceCartridgeAcceptOfferEvent args)
    {
        var network = ent.Comp.Network;
        var storage = GetStorage(network);

        void Fail(string message, params (string, object)[] locArgs)
        {
            _popup.PopupEntity(Loc.GetString(message, locArgs), user, user);
            UpdateCartridge(ent, pda);
        }

        if (!storage.Comp.Offers.TryGetValue(args.OfferId, out var offer)
            || offer.Status != MarketplaceOfferStatus.Pending
            || !storage.Comp.Requests.TryGetValue(offer.RequestId, out var request)
            || TerminatingOrDeleted(offer.Item))
        {
            Fail("marketplace-offer-error-missing");
            return;
        }

        if (!IsCharacter(user, request.RequesterUserId, request.RequesterCharacterName))
        {
            Fail("marketplace-request-error-not-owner");
            return;
        }

        if (!TryGetEntity(args.Teleporter, out var teleporterUid)
            || !TryComp<StationTeleporterComponent>(teleporterUid, out var teleporter)
            || teleporter.Type != TeleporterType.Sector
            || Transform(teleporterUid.Value).MapID != Transform(user).MapID)
        {
            Fail("marketplace-delivery-error-teleporter");
            return;
        }

        if (!IsTeleporterPowered(teleporterUid.Value))
        {
            Fail("marketplace-delivery-error-unpowered");
            return;
        }

        // Pay with bluespace crystals from the PDA's tray.
        if (!TryComp<MarketplaceCrystalSlotComponent>(pda, out var slot)
            || slot.CrystalSlot.Item is not { } crystal
            || GetCrystalCount(pda) < ent.Comp.DeliveryCost
            || !_stack.Use(crystal, ent.Comp.DeliveryCost))
        {
            Fail("marketplace-delivery-error-crystals", ("cost", ent.Comp.DeliveryCost));
            return;
        }

        PayForOffer(storage, offer, request, user, ent.Comp.TaxRate, ent.Comp.TaxAccount);
        StartDelivery(teleporterUid.Value, offer.Item, network);

        _popup.PopupEntity(Loc.GetString("marketplace-delivery-popup-sent",
                ("item", offer.Name),
                ("destination", GetTeleporterName(teleporterUid.Value, teleporter, Transform(teleporterUid.Value)))),
            user,
            user);

        _adminLog.Add(Content.Shared.Database.LogType.ATMUsage,
            Content.Shared.Database.LogImpact.Low,
            $"{ToPrettyString(user):actor} had {ToPrettyString(offer.Item):item} teleported to {ToPrettyString(teleporterUid.Value):entity} for marketplace request #{request.Id}");

        UpdateAllUis(network);
    }

    private void StartDelivery(EntityUid teleporter, EntityUid item, string network)
    {
        var now = _timing.CurTime;
        _deliveries.Add(new PendingDelivery
        {
            Teleporter = teleporter,
            Item = item,
            Network = network,
            Fallback = GetDropCoordinates(teleporter),
            DropAt = now + DeliveryDropDelay,
            CloseAt = now + DeliveryCloseDelay,
        });

        OpenPortal(teleporter);
        _audio.PlayPvs(DeliveryStartSound, teleporter);
    }

    /// <summary>
    /// Shows the portal on the teleporter. Only visual: an unlinked portal doesn't teleport anything.
    /// </summary>
    private void OpenPortal(EntityUid teleporter)
    {
        _appearance.SetData(teleporter, LinkedEntityVisuals.HasAnyLinks, true);
        _appearance.SetData(teleporter, TeleporterPortalVisuals.Color, Color.Cyan);

        if (TryComp<AmbientSoundComponent>(teleporter, out var ambient))
            _ambient.SetAmbience(teleporter, true, ambient);
    }

    /// <summary>
    /// Returns the teleporter to whatever state its own links call for.
    /// </summary>
    private void ClosePortal(EntityUid teleporter)
    {
        if (TerminatingOrDeleted(teleporter))
            return;

        // Another parcel is still on its way here.
        foreach (var other in _deliveries)
        {
            if (other.Teleporter == teleporter)
                return;
        }

        var linked = TryComp<LinkedEntityComponent>(teleporter, out var link) && link.LinkedEntities.Count > 0;
        _appearance.SetData(teleporter, LinkedEntityVisuals.HasAnyLinks, linked);
        _appearance.SetData(teleporter, TeleporterPortalVisuals.Color, linked ? Color.Cyan : Color.Gray);

        if (TryComp<AmbientSoundComponent>(teleporter, out var ambient))
            _ambient.SetAmbience(teleporter, linked, ambient);

        if (!linked && TryComp<StationTeleporterComponent>(teleporter, out var comp))
            _audio.PlayPvs(comp.UnlinkSound, teleporter);
    }

    private void DropParcel(PendingDelivery delivery)
    {
        if (TerminatingOrDeleted(delivery.Item))
            return;

        // If the teleporter was destroyed meanwhile, the parcel still lands where it stood.
        var coords = TerminatingOrDeleted(delivery.Teleporter)
            ? delivery.Fallback
            : GetDropCoordinates(delivery.Teleporter);

        if (_storages.TryGetValue(delivery.Network, out var storageUid))
            _container.Remove(delivery.Item, GetStorageContainer(storageUid), destination: coords);

        RemComp<MarketplaceListedItemComponent>(delivery.Item);
        _audio.PlayPvs(DeliveryArriveSound, coords);
    }

    /// <summary>
    /// A free tile next to the teleporter. Not on the pad itself: a linked teleporter would send the parcel onwards.
    /// </summary>
    private EntityCoordinates GetDropCoordinates(EntityUid teleporter)
    {
        var xform = Transform(teleporter);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return xform.Coordinates;

        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var offsets = new List<Vector2i>
        {
            new(0, -1), new(1, 0), new(-1, 0), new(0, 1),
            new(1, -1), new(-1, -1), new(1, 1), new(-1, 1),
        };

        foreach (var offset in offsets)
        {
            var indices = center + offset;
            if (!_map.TryGetTileRef(gridUid, grid, indices, out var tile) || tile.Tile.IsEmpty)
                continue;

            if (_turf.IsTileBlocked(gridUid, indices, CollisionGroup.Impassable, grid))
                continue;

            var offsetCoords = _map.GridTileToLocal(gridUid, grid, indices);
            // A little scatter so several parcels don't stack on one pixel.
            return offsetCoords.Offset(_random.NextVector2(0.2f));
        }

        return xform.Coordinates;
    }
}
