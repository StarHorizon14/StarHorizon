using System.Numerics;
using Content.Server._Horizon.GameRule.Components;
using Content.Server.Access.Systems;
using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Content.Server.Clothing.Systems;
using Content.Server.Maps.NameGenerators;
using Content.Server._NF.Shipyard.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Server.Worldgen.Components;
using Content.Shared._Horizon.Castaway;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Access.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.GameRule;

/// <summary>
/// Spawns players scattered across open space around the round's map instead of on a station,
/// wearing an emergency EVA suit with an oxygen tank.
/// </summary>
public sealed class CastawayRuleSystem : GameRuleSystem<CastawayRuleComponent>
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly OutfitSystem _outfit = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
    }

    protected override void Started(EntityUid uid, CastawayRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        SpawnMapWrecks(component, GameTicker.DefaultMap);
    }

    private void SpawnMapWrecks(CastawayRuleComponent castaway, MapId mapId)
    {
        if (castaway.MapWreckGridPaths.Count == 0)
            return;

        var placed = new List<Vector2>();

        for (var i = 0; i < castaway.MapWreckCount; i++)
        {
            var point = FindMapWreckSpot(castaway, placed);
            if (point is not { } pos)
                continue;

            var path = _random.Pick(castaway.MapWreckGridPaths);
            if (_mapLoader.TryLoadGrid(mapId, path, out _, offset: pos, rot: _random.NextAngle()))
                placed.Add(pos);
        }
    }

    private Vector2? FindMapWreckSpot(CastawayRuleComponent castaway, List<Vector2> placed)
    {
        for (var attempt = 0; attempt < castaway.MapWreckPlacementRetries; attempt++)
        {
            var distance = _random.NextFloat(castaway.MapWreckMinDistance, castaway.MapWreckMaxDistance);
            var candidate = _random.NextAngle().ToVec() * distance;

            var clear = true;
            foreach (var other in placed)
            {
                if (Vector2.Distance(candidate, other) < castaway.MapWreckClearance)
                {
                    clear = false;
                    break;
                }
            }

            if (clear)
                return candidate;
        }

        return null;
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        var query = EntityQueryEnumerator<CastawayRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var castaway, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            if (!_mapSystem.TryGetMap(GameTicker.DefaultMap, out var mapUid))
                continue;

            var spawnPos = GetRandomCoords(castaway);
            var coords = new EntityCoordinates(mapUid.Value, spawnPos);

            var wreckGrid = SpawnWreck(castaway, GameTicker.DefaultMap, spawnPos);
            SpawnLoot(castaway, mapUid.Value, spawnPos);

            var newMind = _mind.CreateMind(ev.Player.UserId, ev.Profile.Name);
            _mind.SetUserId(newMind, ev.Player.UserId);

            var mob = _stationSpawning.SpawnPlayerMob(coords, null, ev.Profile, null, session: ev.Player);
            _outfit.SetOutfit(mob, castaway.StartingGear);

            // Turns on the emergency oxygen tank's internals automatically, same as the "Вкл подачу воздуха" verb.
            var gearEquippedEv = new StartingGearEquippedEvent(mob);
            RaiseLocalEvent(mob, ref gearEquippedEv);

            EnsureComp<WorldLoaderComponent>(mob);
            EnsureComp<BankAccountComponent>(mob);
            EnsureComp<CastawaySurvivorComponent>(mob);
            NameIdCard(mob, ev.Profile.Name);

            if (wreckGrid is { } grid)
                RegisterWreckOwnership(mob, grid, ev.Profile.Name);

            // IPCs and other non-breathing species have no concept of asphyxiation; skip the damage entirely for them.
            if (HasComp<RespiratorComponent>(mob))
            {
                var damage = new DamageSpecifier(_proto.Index<DamageTypePrototype>("Asphyxiation"), FixedPoint2.New(110));
                _damageable.TryChangeDamage(mob, damage, ignoreResistances: true);
            }

            RunAtlasSequence(mob);

            _mind.TransferTo(newMind, mob);

            ev.Handled = true;
            break;
        }
    }

    private EntityUid? SpawnWreck(CastawayRuleComponent castaway, MapId mapId, Vector2 playerPos)
    {
        if (castaway.WreckGridPaths.Count == 0)
            return null;

        var path = _random.Pick(castaway.WreckGridPaths);

        var wreckDistance = _random.NextFloat(castaway.WreckMinDistance, castaway.WreckMaxDistance);
        var wreckOffset = playerPos + _random.NextAngle().ToVec() * wreckDistance;

        if (_mapLoader.TryLoadGrid(mapId, path, out var grid, offset: wreckOffset, rot: _random.NextAngle()))
            return grid.Value.Owner;

        return null;
    }

    private static readonly NanotrasenNameGenerator WreckNameGenerator = new() { PrefixCreator = "14" };

    private void RegisterWreckOwnership(EntityUid mob, EntityUid wreckGrid, string ownerName)
    {
        if (!_inventory.TryGetSlotEntity(mob, "id", out var pdaUid))
            return;

        var targetId = pdaUid.Value;
        if (TryComp<PdaComponent>(pdaUid, out var pda) && pda.ContainedId != null)
            targetId = pda.ContainedId.Value;

        var wreckName = WreckNameGenerator.FormatName("{1}");
        _metaData.SetEntityName(wreckGrid, wreckName);

        _shuttle.SetPlayerShuttleIFF(wreckGrid, Color.White);

        _shipyard.RegisterShuttleDeed(targetId, wreckGrid, wreckName, ownerName);
    }

    private void SpawnLoot(CastawayRuleComponent castaway, EntityUid mapUid, Vector2 playerPos)
    {
        foreach (var proto in castaway.SurvivalLoot)
        {
            var lootDistance = _random.NextFloat(castaway.LootMinDistance, castaway.LootMaxDistance);
            var lootOffset = playerPos + _random.NextAngle().ToVec() * lootDistance;

            var loot = Spawn(proto, new EntityCoordinates(mapUid, lootOffset));
            _transform.SetLocalRotation(loot, _random.NextAngle());
        }
    }

    private static readonly string[] AtlasMessages =
    [
        "atlas-pai-boot",
        "atlas-pai-oxygen",
        "atlas-pai-critical",
        "atlas-pai-stabilized",
        "atlas-pai-jetpack",
        "atlas-pai-ready",
    ];

    private static readonly SoundSpecifier?[] AtlasSounds =
    [
        new SoundPathSpecifier("/Audio/_Horizon/Suit/Hev-suit1.ogg"),
        new SoundPathSpecifier("/Audio/_Horizon/Suit/Hev-suit2.ogg"),
        new SoundPathSpecifier("/Audio/_Horizon/Suit/Hev-suit3.ogg"),
        new SoundPathSpecifier("/Audio/_Horizon/Suit/Hev-suit4.ogg"),
        new SoundPathSpecifier("/Audio/_Horizon/Suit/Hev-suit5.ogg"),
        new SoundPathSpecifier("/Audio/_Horizon/Suit/Hev-suit6.ogg"),
    ];

    // Gap in seconds before each line, from the previous one (or from spawn for the first line).
    // Lines 3 ("critical", 5s VO) and 4 ("stabilized", 6s VO) need extra room after them so their voice lines finish playing.
    // The gap after "critical" also has to fit the defib sequence (safety_on/charge/zap), which starts once its VO ends.
    private static readonly float[] AtlasDelays = [5, 5, 6, 12, 7, 5];

    private void RunAtlasSequence(EntityUid mob)
    {
        if (!_inventory.TryGetSlotEntity(mob, "id", out var pdaUid))
            return;

        if (!TryComp<PdaComponent>(pdaUid, out var pdaComponent) || pdaComponent.PaiSlot.Item is not { } atlas)
            return;

        var elapsed = 0f;
        for (var i = 0; i < AtlasMessages.Length; i++)
        {
            elapsed += AtlasDelays[i];
            var locId = AtlasMessages[i];
            var sound = AtlasSounds[i];
            var delay = TimeSpan.FromSeconds(elapsed);

            Timer.Spawn(delay, () =>
            {
                if (Deleted(atlas) || Deleted(mob))
                    return;

                AtlasSpeak(atlas, Loc.GetString(locId));
                _audio.PlayPvs(sound, atlas);

                // Kick off the defib sequence right after the second line ("critical state" warning).
                // The asphyxiation damage is healed once the zap itself lands, not before.
                if (locId == "atlas-pai-critical")
                    RunDefibSequence(mob);
            });
        }
    }

    // Timings roughly mirror the real defibrillator: a safety-off beep, a charge-up whine, then the zap itself.
    // The 5s base offset lets the "critical" voice line finish before the defib sequence starts.
    private static readonly (string Path, float Delay, bool IsZap)[] DefibSteps =
    [
        ("/Audio/Items/Defib/defib_safety_on.ogg", 5f, false),
        ("/Audio/Items/Defib/defib_charge.ogg", 6f, false),
        ("/Audio/Items/Defib/defib_zap.ogg", 7f, true),
    ];

    private void RunDefibSequence(EntityUid mob)
    {
        foreach (var (path, delay, isZap) in DefibSteps)
        {
            Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
            {
                if (Deleted(mob))
                    return;

                _audio.PlayPvs(new SoundPathSpecifier(path), mob);

                if (!isZap)
                    return;

                _jitter.DoJitter(mob, TimeSpan.FromSeconds(1), refresh: true, amplitude: 40f, frequency: 8f);

                // Heal some of the asphyxiation damage once the zap actually lands.
                if (HasComp<RespiratorComponent>(mob))
                {
                    var heal = new DamageSpecifier(_proto.Index<DamageTypePrototype>("Asphyxiation"), FixedPoint2.New(-50));
                    _damageable.TryChangeDamage(mob, heal, ignoreResistances: true);
                }
            });
        }
    }

    private void AtlasSpeak(EntityUid atlas, string message)
    {
        _chat.TrySendInGameICMessage(atlas, message, InGameICChatType.Speak, ChatTransmitRange.Normal, ignoreActionBlocker: true);
    }

    private void NameIdCard(EntityUid mob, string characterName)
    {
        if (!_inventory.TryGetSlotEntity(mob, "id", out var idUid))
            return;

        var cardId = idUid.Value;
        if (TryComp<PdaComponent>(idUid, out var pda) && pda.ContainedId != null)
            cardId = pda.ContainedId.Value;

        if (!TryComp<IdCardComponent>(cardId, out var card))
            return;

        _idCard.TryChangeFullName(cardId, characterName, card);
    }

    private Vector2 GetRandomCoords(CastawayRuleComponent component)
    {
        var distance = _random.NextFloat(component.MinDistance, component.MaxDistance);
        var angle = _random.NextAngle();
        return angle.ToVec() * distance;
    }
}
