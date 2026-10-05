using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Cinematic;

/// <summary>
/// Sent from the server to one specific client, telling it to switch its local UI/game State to
/// the named class. Resolved client-side the same way the engine's local "scene" console command
/// resolves state classes: by matching the end of the type's full name.
/// </summary>
[Serializable, NetSerializable]
public sealed class ForceSceneEvent : EntityEventArgs
{
    public string SceneName = string.Empty;
}
