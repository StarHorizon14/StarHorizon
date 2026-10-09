using System.Numerics;

namespace Content.Shared._Horizon.Mirage;

public abstract class SharedMirageBorderSystem : EntitySystem
{
    /// <summary>
    /// Biggest mirage size, in tiles, on either axis. Keeps the render target sizes sane.
    /// </summary>
    public const float MaxSize = 64f;

    /// <summary>
    /// Biggest <see cref="MirageBorderComponent.ViewRange"/> the server will look for borders in.
    /// </summary>
    public const float MaxViewRange = 64f;

    public void SetTarget(Entity<MirageBorderComponent> ent, EntityUid? target)
    {
        if (ent.Comp.Target == target)
            return;

        ent.Comp.Target = target;
        Dirty(ent);
    }

    public void SetArea(Entity<MirageBorderComponent> ent, Vector2 offset, Vector2 size)
    {
        ent.Comp.Offset = offset;
        ent.Comp.Size = Vector2.Clamp(size, Vector2.One, new Vector2(MaxSize));
        Dirty(ent);
    }
}
