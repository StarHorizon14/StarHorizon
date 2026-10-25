using Content.Client.Stylesheets;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Content.Client.Message;

namespace Content.Client._Horizon.Shipbreaking;

/// <summary>
/// The line under the hand that says which mode the ship tool is in, like the one of the omnitool.
/// </summary>
public sealed class ShipToolStatusControl : Control
{
    private readonly ShipCutterComponent _tool;
    private readonly RichTextLabel _label;
    private ShipToolMode? _shown;

    public ShipToolStatusControl(ShipCutterComponent tool)
    {
        _tool = tool;
        _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
        AddChild(_label);
        Update();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_shown != _tool.Mode)
            Update();
    }

    private void Update()
    {
        _shown = _tool.Mode;
        _label.SetMarkup(Loc.GetString(_tool.Mode == ShipToolMode.Cut ? "ship-tool-status-cut" : "ship-tool-status-weld"));
    }
}
