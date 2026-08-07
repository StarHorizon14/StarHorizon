using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.Medical;
using Content.Shared._Horizon._Fractions.AnCo.Biofabricator;
using Robust.Shared.Prototypes;

namespace Content.Server._Horizon._Fractions.AnCo.Biofabricator;

/// <summary>
/// Destructible behavior for the Biofabricator: spawns and triggers a smoke effect, and makes the body
/// currently inside it (if any) vomit, like drinking ipecac.
/// </summary>
[DataDefinition]
public sealed partial class AnCoBiofabricatorBreakBehavior : IThresholdBehavior
{
    [DataField]
    public EntProtoId SmokeEffect = "AdminInstantEffectSmoke3";

    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
    {
        var entityManager = system.EntityManager;
        var coordinates = entityManager.GetComponent<TransformComponent>(owner).Coordinates;

        var smoke = entityManager.SpawnEntity(SmokeEffect, coordinates);
        system.TriggerSystem.Trigger(smoke, cause);

        if (entityManager.TryGetComponent<AnCoBiofabricatorComponent>(owner, out var fab) &&
            fab.BodyContainer.ContainedEntity is { Valid: true } body)
        {
            entityManager.System<VomitSystem>().Vomit(body);
        }
    }
}
