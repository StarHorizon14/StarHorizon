namespace Content.Shared._Horizon.Shuttles.Components;

/// <summary>
/// A paper note that tells its readers which expedition planet their secret FTL tunnel goes through. The server writes
/// the planet name into the paper once the tunnel's planet has been chosen.
/// </summary>
[RegisterComponent]
public sealed partial class SecretTunnelNoteComponent : Component
{
    /// <summary>Id of the <see cref="SecretTunnelMapComponent"/> this note is about.</summary>
    [DataField(required: true)]
    public string TunnelId = string.Empty;

    /// <summary>Localization key of the note text; gets the planet name as <c>$planet</c>.</summary>
    [DataField(required: true)]
    public string Text = string.Empty;
}
