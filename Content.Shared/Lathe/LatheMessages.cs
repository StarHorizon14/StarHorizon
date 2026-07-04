using Content.Shared.Research.Prototypes;
using NetSerializer;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Lathe;

[Serializable, NetSerializable]
public sealed class LatheUpdateState : BoundUserInterfaceState
{
    public List<ProtoId<LatheRecipePrototype>> Recipes;

    public List<LatheRecipeBatch> Queue; // Frontier: ProtoId<LatheRecipePrototype>[] < List<LatheRecipeBatch>

    public ProtoId<LatheRecipePrototype>? CurrentlyProducing;

    public bool InfiniteProduction; // Horizon

    public LatheUpdateState(List<ProtoId<LatheRecipePrototype>> recipes, List<LatheRecipeBatch> queue, ProtoId<LatheRecipePrototype>? currentlyProducing = null, bool infiniteProduction = false) // Frontier: change queue type, Horizon: add infiniteProduction
    {
        Recipes = recipes;
        Queue = queue;
        CurrentlyProducing = currentlyProducing;
        InfiniteProduction = infiniteProduction; // Horizon
    }
}

/// <summary>
///     Sent to the server to sync material storage and the recipe queue.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheSyncRequestMessage : BoundUserInterfaceMessage
{

}

/// <summary>
///     Sent to the server when a client queues a new recipe.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheQueueRecipeMessage : BoundUserInterfaceMessage
{
    public readonly string ID;
    public readonly int Quantity;
    public LatheQueueRecipeMessage(string id, int quantity)
    {
        ID = id;
        Quantity = quantity;
    }
}

// Horizon: сообщение для переключения бесконечного производства
[Serializable, NetSerializable]
public sealed class LatheToggleInfiniteProductionMessage : BoundUserInterfaceMessage
{
    public readonly bool Enabled;

    public LatheToggleInfiniteProductionMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

[NetSerializable, Serializable]
public enum LatheUiKey
{
    Key,
}
