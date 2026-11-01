using System.Numerics;

// Mono - whole file

namespace Content.Server.Physics.Controllers;

/// <summary>
///     Shuttle input in the shuttle's local frame.
/// </summary>
public record struct ShuttleInput(Vector2 Strafe, float Rotation, float Brakes);

/// <summary>
///     Raised on non-player input sources (e.g. AI steerers) registered in <see cref="PilotedShuttleComponent"/>.
///     If GotInput is false, the source is removed from the shuttle's input sources.
/// </summary>
[ByRefEvent]
public record struct GetShuttleInputsEvent(float FrameTime, EntityUid ShuttleUid, ShuttleInput? Input = null, bool GotInput = false);

/// <summary>
///     An event raised on a piloted shuttle, relayed to each of its input sources.
/// </summary>
[ByRefEvent]
public record struct PilotedShuttleRelayedEvent<TEvent>(TEvent Args);
