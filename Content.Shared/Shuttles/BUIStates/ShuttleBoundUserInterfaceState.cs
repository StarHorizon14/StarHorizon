using Content.Shared._Mono.FireControl; // Lua
using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class ShuttleBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState NavState;
    public ShuttleMapInterfaceState MapState;
    public DockingInterfaceState DockState;
    public bool Broken; // Horizon tweak
    public bool FireControlConnected; // Lua
    public FireControllableEntry[]? FireControllables; // Lua

    public ShuttleBoundUserInterfaceState(NavInterfaceState navState, ShuttleMapInterfaceState mapState, DockingInterfaceState dockState, bool broken, bool fireControlConnected = false, FireControllableEntry[]? fireControllables = null)   // Horizon - broken bool
    {
        NavState = navState;
        MapState = mapState;
        DockState = dockState;
        Broken = broken;    // Horizon
        FireControlConnected = fireControlConnected; // Lua
        FireControllables = fireControllables; // Lua
    }
}
