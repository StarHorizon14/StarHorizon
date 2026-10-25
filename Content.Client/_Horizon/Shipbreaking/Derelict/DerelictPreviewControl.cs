using System.Numerics;
using Content.Shared._Horizon.Shipbreaking.Derelict;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Horizon.Shipbreaking.Derelict;

/// <summary>
/// A view from above on a wreck, one coloured cell per tile.
/// </summary>
public sealed class DerelictPreviewControl : Control
{
    private static readonly Color Background = Color.FromHex("#0b0b10");

    private Vector2i[] _tiles = Array.Empty<Vector2i>();
    private DerelictPreviewKind[] _kinds = Array.Empty<DerelictPreviewKind>();

    public DerelictPreviewControl()
    {
        RectClipContent = true;
    }

    public void SetPreview(Vector2i[] tiles, DerelictPreviewKind[] kinds)
    {
        _tiles = tiles;
        _kinds = kinds;
    }

    public void Clear()
    {
        _tiles = Array.Empty<Vector2i>();
        _kinds = Array.Empty<DerelictPreviewKind>();
    }

    private static Color ColorOf(DerelictPreviewKind kind)
    {
        return kind switch
        {
            DerelictPreviewKind.Wall => Color.FromHex("#8a8f99"),
            DerelictPreviewKind.Door => Color.FromHex("#d9a441"),
            DerelictPreviewKind.Window => Color.FromHex("#5fb4d9"),
            DerelictPreviewKind.Machine => Color.FromHex("#58a36a"),
            _ => Color.FromHex("#2d3340"),
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        handle.DrawRect(PixelSizeBox, Background);
        if (_tiles.Length == 0)
            return;

        var min = _tiles[0];
        var max = _tiles[0];
        foreach (var tile in _tiles)
        {
            min = Vector2i.ComponentMin(min, tile);
            max = Vector2i.ComponentMax(max, tile);
        }

        var width = max.X - min.X + 1;
        var height = max.Y - min.Y + 1;
        var margin = 8f;
        var cell = MathF.Floor(MathF.Min((PixelWidth - margin * 2) / width, (PixelHeight - margin * 2) / height));
        cell = MathF.Max(cell, 1f);

        var origin = new Vector2((PixelWidth - width * cell) / 2f, (PixelHeight - height * cell) / 2f);

        for (var i = 0; i < _tiles.Length; i++)
        {
            // The grid goes up with y, the screen goes down.
            var x = _tiles[i].X - min.X;
            var y = max.Y - _tiles[i].Y;
            var box = UIBox2.FromDimensions(origin + new Vector2(x, y) * cell, new Vector2(cell, cell));
            handle.DrawRect(box, ColorOf(_kinds[i]));
        }
    }
}
