using Content.Shared._Horizon.GhostTrail;
using Content.Shared.Tag;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using static Robust.Client.Animations.AnimationTrackProperty;

namespace Content.Client._Horizon.GhostTrail;

/// <summary>
/// Клиентская система для создания и управления эффектом призрачного следа.
/// </summary>
public sealed class GhostTrailSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AnimationPlayerSystem _animations = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private const string AnimationKey = "ghost-trail-fade";
    private static readonly ProtoId<TagPrototype> HideContextMenuTag = "HideContextMenu";

    /// <summary>
    /// Отслеживание последней позиции и времени создания следа для каждой сущности.
    /// </summary>
    private readonly Dictionary<EntityUid, (EntityCoordinates LastPos, TimeSpan LastTrailTime)> _trailState = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostTrailComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<GhostTrailComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<GhostTrailCloneComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    private void OnShutdown(EntityUid uid, GhostTrailComponent component, ComponentShutdown args)
    {
        _trailState.Remove(uid);
    }

    private void OnMove(EntityUid uid, GhostTrailComponent component, ref MoveEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!component.Enabled)
            return;

        if (args.ParentChanged)
        {
            _trailState.Remove(uid);
            return;
        }

        var currentTime = _timing.CurTime;
        var currentPos = args.NewPosition;

        if (!_trailState.TryGetValue(uid, out var state))
        {
            state = (args.OldPosition, currentTime);
            _trailState[uid] = state;
            return;
        }

        var timeSinceLastTrail = (currentTime - state.LastTrailTime).TotalSeconds;
        if (timeSinceLastTrail < component.TrailInterval)
            return;

        var distance = (currentPos.Position - state.LastPos.Position).Length();
        if (distance < component.MinDistance)
            return;

        var trailCount = CountActiveTrails(uid);
        if (trailCount >= component.MaxTrails)
            return;

        CreateTrail(uid, component, state.LastPos, args.OldRotation);

        _trailState[uid] = (currentPos, currentTime);
    }

    private int CountActiveTrails(EntityUid sourceUid)
    {
        var count = 0;
        var query = EntityQueryEnumerator<GhostTrailCloneComponent>();

        while (query.MoveNext(out _, out var clone))
        {
            if (clone.SourceEntity == sourceUid)
                count++;
        }

        return count;
    }

    private void CreateTrail(EntityUid sourceUid, GhostTrailComponent component,
        EntityCoordinates position, Angle rotation)
    {
        if (!position.IsValid(EntityManager))
            return;

        if (!TryComp<SpriteComponent>(sourceUid, out var sourceSprite))
            return;

        if (IsPaused(sourceUid))
            return;

        var trailEntity = Spawn("clientsideclone", position);

        var cloneComp = EnsureComp<GhostTrailCloneComponent>(trailEntity);
        cloneComp.SourceEntity = sourceUid;

        _tag.AddTag(trailEntity, HideContextMenuTag);

        if (TryComp<MetaDataComponent>(sourceUid, out var sourceMeta))
        {
            _metaData.SetEntityName(trailEntity, $"GhostTrail: {sourceMeta.EntityName}");
        }

        var trailSprite = Comp<SpriteComponent>(trailEntity);
        _sprite.CopySprite((sourceUid, sourceSprite), (trailEntity, trailSprite));
        _sprite.SetVisible((trailEntity, trailSprite), true);

        _transform.SetLocalRotationNoLerp(trailEntity, rotation);

        var startColor = component.TrailColor ?? sourceSprite.Color;
        startColor = startColor.WithAlpha(component.InitialAlpha);
        _sprite.SetColor((trailEntity, trailSprite), startColor);

        var despawn = EnsureComp<TimedDespawnComponent>(trailEntity);
        despawn.Lifetime = component.TrailLifetime;

        var animations = Comp<AnimationPlayerComponent>(trailEntity);
        var endColor = startColor.WithAlpha(0f);

        _animations.Play(new Entity<AnimationPlayerComponent>(trailEntity, animations), new Animation
        {
            Length = TimeSpan.FromSeconds(component.TrailLifetime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new KeyFrame(startColor, 0f),
                        new KeyFrame(endColor, component.TrailLifetime)
                    }
                }
            }
        }, AnimationKey);
    }

    private void OnAnimationCompleted(EntityUid uid, GhostTrailCloneComponent component,
        AnimationCompletedEvent args)
    {
        if (args.Key != AnimationKey)
            return;

        if (!Deleted(uid))
            QueueDel(uid);
    }
}
