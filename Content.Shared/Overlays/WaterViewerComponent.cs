namespace Content.Shared.Overlays;

/// <summary>
/// Marks an entity as being submerged in water, showing the underwater screen distortion overlay.
/// </summary>
[RegisterComponent]
public sealed partial class WaterViewerComponent : Component
{
    /// <summary>
    /// When this component was added - used client-side to fade the distortion overlay in gradually
    /// instead of snapping to full strength immediately.
    /// </summary>
    [DataField]
    public TimeSpan StartTime;
}
