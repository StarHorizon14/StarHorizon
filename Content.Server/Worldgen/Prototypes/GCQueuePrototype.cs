using Robust.Shared.Prototypes;

namespace Content.Server.Worldgen.Prototypes;

/// <summary>
///     This is a prototype for a GC queue.
/// </summary>
[Prototype("gcQueue")]
public sealed class GCQueuePrototype : IPrototype
{
    /// <inheritdoc />
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    ///     How deep the GC queue is at most. If this value is ever exceeded entities get processed automatically regardless of
    ///     tick-time cap.
    /// </summary>
    [DataField("depth", required: true)]
    public int Depth { get; }

    /// <summary>
    ///     The maximum amount of time that can be spent processing this queue.
    /// </summary>
    [DataField("maximumTickTime")]
    public TimeSpan MaximumTickTime { get; } = TimeSpan.FromMilliseconds(1);

    // Horizon-Start
    /// <summary>
    ///     Сколько миллисекунд за тик добавлять на каждый объект очереди сверх
    ///     <see cref="MinDepthToProcess"/>. Длинная очередь разбирается быстрее и не копится в памяти.
    /// </summary>
    [DataField]
    public double TimeDeletePerObject { get; } = 0.1;
    // Horizon-End

    /// <summary>
    ///     The minimum depth before entities in the queue actually get processed for deletion.
    /// </summary>
    [DataField("minDepthToProcess", required: true)]
    public int MinDepthToProcess { get; }

    /// <summary>
    ///     Whether or not the GC should fire an event on the entity to see if it's eligible to skip the queue.
    ///     Useful for making it so only objects a player has actually interacted with get put in the collection queue.
    /// </summary>
    [DataField("trySkipQueue")]
    public bool TrySkipQueue { get; }
}

