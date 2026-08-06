using Content.Shared.Preferences;

namespace Content.Shared._Horizon._Fractions.AnCo.Biofabricator;

/// <summary>
/// Raised on a mob when attempting to bind a memory card to them. Cancel to prevent the binding
/// (e.g. the "unrevivable"/"unclonable" traits).
/// </summary>
[ByRefEvent]
public record struct AnCoMemoryCardBindAttemptEvent(bool Cancelled = false);

/// <summary>
/// Raised when a Biofabricator is about to start restoring a body from a memory card.
/// Cancel to prevent the restoration from starting.
/// </summary>
[ByRefEvent]
public record struct AnCoBiofabricatorRestoreAttemptEvent(HumanoidCharacterProfile Profile, bool Cancelled = false);

/// <summary>
/// Raised after a Biofabricator has finished spawning and dressing a restored body,
/// before the mind is transferred to it. Extension point for systems that need to
/// add more state to the restored body (e.g. organs/limbs).
/// </summary>
[ByRefEvent]
public record struct AnCoBiofabricatorBodyRestoredEvent(EntityUid NewBody, AnCoMemoryCardComponent Card);
