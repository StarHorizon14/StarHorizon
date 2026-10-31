using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Shuttles.Components;

/// <summary>
/// FTL concentration on a shuttle: for <see cref="Duration"/> the engines brake the shuttle like a held space bar, the
/// radius that must be clear of other ships, stations and asteroids for a regular FTL jump shrinks to
/// <see cref="FinalRangeFactor"/> of its normal size and the jump range grows to <see cref="FinalJumpRangeMultiplier"/>
/// times its normal size. The component stays,
/// with the shrunk radius, until the player cancels, changes the inertia mode or the shuttle jumps.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FtlConcentrationComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan StartTime;

    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);

    /// <summary>The required radius at the end of the concentration, as a fraction of the normal one.</summary>
    [DataField, AutoNetworkedField]
    public float FinalRangeFactor = 0.5f;

    /// <summary>The FTL jump range at the end of the concentration, as a multiple of the normal one.</summary>
    [DataField, AutoNetworkedField]
    public float FinalJumpRangeMultiplier = 3f;

    /// <summary>0 at the start, 1 once the concentration is complete.</summary>
    public float GetProgress(TimeSpan now)
    {
        if (Duration <= TimeSpan.Zero)
            return 1f;

        return Math.Clamp((float) ((now - StartTime).TotalSeconds / Duration.TotalSeconds), 0f, 1f);
    }

    /// <summary>Fraction of the normal FTL proximity radius that is required right now.</summary>
    public float GetRangeFactor(TimeSpan now)
    {
        return 1f - (1f - FinalRangeFactor) * GetProgress(now);
    }

    /// <summary>Multiple of the normal FTL jump range that applies right now.</summary>
    public float GetJumpRangeMultiplier(TimeSpan now)
    {
        return 1f + (FinalJumpRangeMultiplier - 1f) * GetProgress(now);
    }
}
