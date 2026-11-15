using System.Linq; // Horizon
using Robust.Shared.Prototypes;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Audio.Jukebox;

public abstract partial class SharedJukeboxSystem : EntitySystem
{
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected IPrototypeManager _protoManager = default!; // wizden#42210

    // wizden#42210
    public IEnumerable<JukeboxPrototype> GetAvailableTracks(Entity<JukeboxComponent> entity)
    {
        return _protoManager.EnumeratePrototypes<JukeboxPrototype>().ToList();
    }
    // End wizden#42210
}
