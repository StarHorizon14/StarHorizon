using System.Linq;
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
using Content.Server.Procedural;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Server.Worldgen.Components;
using Content.Shared._Horizon.Castaway;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Access.Components;
using Content.Shared.Chat;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Content.Shared.Physics;
using Content.Shared.PDA;
using Content.Shared.Procedural;
using Content.Shared.Random;
using Content.Shared.Roles;
using Content.Shared.Salvage.Expeditions;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Collections;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
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
    [Dependency] private readonly AnchorableSystem _anchorable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DungeonSystem _dungeon = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RandomSystem _randomSys = default!;
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
        SubscribeLocalEvent<FTLStartedEvent>(OnFtlStarted);
    }

    // Mirrors salvage expeditions: once the last shuttle leaves a Castaway player's dedicated map
    // via FTL, the map is torn down behind them since it was never a valid FTL destination anyway.
    private void OnFtlStarted(ref FTLStartedEvent ev)
    {
        if (ev.FromMapUid is not { } fromMapUid || !HasComp<CastawayMapComponent>(fromMapUid))
            return;

        QueueDel(fromMapUid);
    }

    protected override void Started(EntityUid uid, CastawayRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // Salvage wrecks scattered for exploration stay on the main round map, not each player's
        // dedicated Castaway map, since they're meant to be found via the shared map, not a pocket.
        var placed = new List<Vector2>();
        SpawnMapWrecks(component, GameTicker.DefaultMap, placed);

        // Same VGRoid dungeon generation as the BluespaceErrorRule station event, but spawned
        // directly here instead of through the station-event scheduler (no "Central Command"
        // announcement tied to it, and it doesn't depend on station events being enabled).
        SpawnMapDungeons(component, GameTicker.DefaultMap, placed);
    }

    private void SpawnMapWrecks(CastawayRuleComponent castaway, MapId mapId, List<Vector2> placed)
    {
        if (castaway.MapWreckGridPaths.Count == 0)
            return;

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

    private void SpawnMapDungeons(CastawayRuleComponent castaway, MapId mapId, List<Vector2> placed)
    {
        if (castaway.MapDungeons.Count == 0 || castaway.MapDungeonFactions.Count == 0)
            return;

        for (var i = 0; i < castaway.MapDungeonCount; i++)
        {
            var point = FindMapWreckSpot(castaway, placed);
            if (point is not { } pos)
                continue;

            var dungeonProto = _proto.Index(_random.Pick(castaway.MapDungeons));
            var faction = _proto.Index(_random.Pick(castaway.MapDungeonFactions));

            var grid = _mapManager.CreateGridEntity(mapId);
            _transform.SetMapCoordinates(grid, new MapCoordinates(pos, mapId));

            GenerateDungeon(dungeonProto, faction, grid.Owner, grid.Comp, castaway.MapDungeonMobBudget);
            placed.Add(pos);
        }
    }

    private async void GenerateDungeon(DungeonConfigPrototype dungeonProto, SalvageFactionPrototype faction, EntityUid grid, MapGridComponent gridComp, int budget)
    {
        var dungeons = await _dungeon.GenerateDungeonAsync(dungeonProto, dungeonProto.ID, grid, gridComp, Vector2i.Zero, _random.Next());

        if (dungeons.Count <= 0 || dungeons[0] is not { } dungeon || dungeon.Rooms.Count <= 0)
            return;

        var budgetEntries = new List<IBudgetEntry>();
        foreach (var entry in faction.MobGroups)
            budgetEntries.Add(entry);

        float mobBudget = budget;
        var probSum = budgetEntries.Sum(x => x.Prob);
        var random = new Random(_random.Next());

        while (mobBudget > 0f)
        {
            var entry = _randomSys.GetBudgetEntry(ref mobBudget, ref probSum, budgetEntries, random);
            if (entry == null)
                break;

            SpawnDungeonMob((grid, gridComp), entry, dungeon, random);
        }
    }

    private void SpawnDungeonMob(Entity<MapGridComponent> grid, IBudgetEntry entry, Dungeon dungeon, Random random)
    {
        var availableRooms = new ValueList<DungeonRoom>(dungeon.Rooms);
        var availableTiles = new ValueList<Vector2i>();

        while (availableRooms.Count > 0)
        {
            availableTiles.Clear();
            var roomIndex = random.Next(availableRooms.Count);
            var room = availableRooms.RemoveSwap(roomIndex);
            availableTiles.AddRange(room.Tiles);

            while (availableTiles.Count > 0)
            {
                var tile = availableTiles.RemoveSwap(random.Next(availableTiles.Count));

                if (!_anchorable.TileFree(grid, tile, (int)CollisionGroup.MachineLayer, (int)CollisionGroup.MachineLayer))
                    continue;

                Spawn(entry.Proto, _mapSystem.GridTileToLocal(grid, grid, tile));
                return;
            }
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

            var newMind = _mind.CreateMind(ev.Player.UserId, ev.Profile.Name);
            _mind.SetUserId(newMind, ev.Player.UserId);

            // Players pick their scenario in the lobby via the job selector (CastawaySurvivor vs
            // CastawaySleeper). Alternate scenarios can fail to set up if their hand-placed map
            // content is missing or malformed; scenario 1 only depends on the always-present
            // WreckGridPaths, so it's the safe fallback for any of them, including an unset job.
            var mob = ev.JobId == "CastawaySleeper"
                ? SpawnPodScenario(castaway, ev)
                : null;

            var spawnedMob = mob ?? SpawnWreckScenario(castaway, ev);

            EnsureComp<WorldLoaderComponent>(spawnedMob);
            EnsureComp<BankAccountComponent>(spawnedMob);
            EnsureComp<CastawaySurvivorComponent>(spawnedMob);
            NameIdCard(spawnedMob, ev.Profile.Name);

            _mind.TransferTo(newMind, spawnedMob);

            ev.Handled = true;
            break;
        }
    }

    // Scenario 1: player wakes up floating in open space next to their own wreck and some loot.
    // Runs on its own dedicated, isolated map; it's never registered as an FTL destination, so
    // nobody (including the player themselves) can fly back to it once they've left on their wreck.
    private EntityUid SpawnWreckScenario(CastawayRuleComponent castaway, PlayerBeforeSpawnEvent ev)
    {
        var mapUid = _mapSystem.CreateMap(out var castawayMapId);
        EnsureComp<CastawayMapComponent>(mapUid);

        var spawnPos = GetRandomCoords(castaway);
        var coords = new EntityCoordinates(mapUid, spawnPos);

        var wreckGrid = SpawnWreck(castaway, castawayMapId, spawnPos);
        SpawnLoot(castaway, mapUid, spawnPos);

        var mob = _stationSpawning.SpawnPlayerMob(coords, null, ev.Profile, null, session: ev.Player);
        _outfit.SetOutfit(mob, castaway.StartingGear);

        // Turns on the emergency oxygen tank's internals automatically, same as the "Вкл подачу воздуха" verb.
        var gearEquippedEv = new StartingGearEquippedEvent(mob);
        RaiseLocalEvent(mob, ref gearEquippedEv);

        if (wreckGrid is { } grid)
            RegisterWreckOwnership(mob, grid, ev.Profile.Name);

        // IPCs and other non-breathing species have no concept of asphyxiation; skip the damage entirely for them.
        ApplyAsphyxiation(mob, 110);

        RunAtlasSequence(mob);

        return mob;
    }

    // Scenario 2: player wakes up inside a malfunctioning medical pod on a derelict station. The
    // derelict map file is a full map (not a standalone grid), and it becomes the player's own
    // dedicated map outright — same isolation/no-FTL-back guarantees as the wreck scenario.
    private EntityUid? SpawnPodScenario(CastawayRuleComponent castaway, PlayerBeforeSpawnEvent ev)
    {
        var loadOptions = new DeserializationOptions { InitializeMaps = true };
        if (!_mapLoader.TryLoadMap(castaway.PodMapPath, out var map, out _, loadOptions))
            return null;

        EnsureComp<CastawayMapComponent>(map.Value.Owner);

        var pod = FindPodSpawn(map.Value.Owner);
        if (pod is null)
        {
            QueueDel(map.Value.Owner);
            return null;
        }

        var coords = new EntityCoordinates(pod.Value, Vector2.Zero);
        var mob = _stationSpawning.SpawnPlayerMob(coords, null, ev.Profile, null, session: ev.Player);
        _outfit.SetOutfit(mob, castaway.PodStartingGear);

        _entityStorage.Insert(mob, pod.Value);

        // IPCs and other non-breathing species have no concept of asphyxiation; skip the damage entirely for them.
        ApplyAsphyxiation(mob, 110);

        RunPodSequence(mob, pod.Value);

        // The map already has a hand-placed wreck grid sitting near the station; hand it over as
        // the player's own ShuttleDeed property, same as the wreck spawned in scenario 1.
        if (FindNamedGrid(map.Value.Owner, castaway.PodWreckGridName) is { } wreckGrid)
            RegisterWreckOwnership(mob, wreckGrid, ev.Profile.Name);

        return mob;
    }

    // Gap in seconds before each line, from the previous one (or from spawn for the first line).
    // Sized to fit each line's voice clip (cryo-1.ogg ~4.2s, cryo-2.ogg ~3.3s, cryo-3.ogg ~3.5s)
    // so lines don't overlap or cut each other off.
    private static readonly float[] PodDelays = [4.5f, 3.5f, 3.5f];

    private static readonly string[] PodMessages =
    [
        "castaway-pod-malfunction",
        "castaway-pod-waking",
        "castaway-pod-ready",
    ];

    private static readonly SoundSpecifier?[] PodSounds =
    [
        new SoundPathSpecifier("/Audio/_Horizon/Cryo/cryo-1.ogg"),
        new SoundPathSpecifier("/Audio/_Horizon/Cryo/cryo-2.ogg"),
        new SoundPathSpecifier("/Audio/_Horizon/Cryo/cryo-3.ogg"),
    ];

    private void RunPodSequence(EntityUid mob, EntityUid pod)
    {
        var elapsed = 0f;
        for (var i = 0; i < PodMessages.Length; i++)
        {
            elapsed += PodDelays[i];
            var locId = PodMessages[i];
            var sound = PodSounds[i];
            var delay = TimeSpan.FromSeconds(elapsed);

            Timer.Spawn(delay, () =>
            {
                if (Deleted(pod) || Deleted(mob))
                    return;

                _chat.TrySendInGameICMessage(pod, Loc.GetString(locId), InGameICChatType.Speak, ChatTransmitRange.Normal, ignoreActionBlocker: true);
                _audio.PlayPvs(sound, pod);

                // Heal on the second line, once the "waking up" process is announced.
                if (locId == "castaway-pod-waking")
                    ApplyAsphyxiation(mob, -80);
            });
        }
    }

    // Recursively searches a map's entity tree for a hand-placed MedicalPodSpawn, since it could be
    // nested a few levels deep (grid -> pod) rather than a direct child of the map itself.
    private EntityUid? FindPodSpawn(EntityUid root)
    {
        var xform = Transform(root);
        var children = xform.ChildEnumerator;

        while (children.MoveNext(out var child))
        {
            if (Comp<MetaDataComponent>(child).EntityPrototype?.ID == "MedicalPodSpawn")
                return child;

            if (FindPodSpawn(child) is { } found)
                return found;
        }

        return null;
    }

    // Finds a direct child grid of a map by its MetaData entity name (e.g. a hand-placed wreck grid).
    private EntityUid? FindNamedGrid(EntityUid mapUid, string name)
    {
        var xform = Transform(mapUid);
        var children = xform.ChildEnumerator;

        while (children.MoveNext(out var child))
        {
            if (HasComp<MapGridComponent>(child) && Comp<MetaDataComponent>(child).EntityName == name)
                return child;
        }

        return null;
    }

    private void ApplyAsphyxiation(EntityUid mob, float amount)
    {
        if (!HasComp<RespiratorComponent>(mob))
            return;

        var damage = new DamageSpecifier(_proto.Index<DamageTypePrototype>("Asphyxiation"), FixedPoint2.New(amount));
        _damageable.TryChangeDamage(mob, damage, ignoreResistances: true);
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
                ApplyAsphyxiation(mob, -50);
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
