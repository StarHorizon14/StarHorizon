using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Horizon._Fractions.AnCo.WeaponCaseHack;

/// <summary>
/// Прозрачный оверлей со scanlines консоли сайта: тонкая тёмная линия каждые 3 px.
/// Кладётся последним ребёнком поверх содержимого и не ловит клики.
/// </summary>
public sealed class AnCoConsoleScanlines : Control
{
    private static readonly Color Line = new(0f, 0f, 0f, 0.2f);

    public AnCoConsoleScanlines()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var thickness = MathF.Max(1f, UIScale);
        var period = 3f * UIScale;
        for (var y = 0f; y < PixelHeight; y += period)
        {
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(0, y), new Vector2(PixelWidth, thickness)), Line);
        }
    }
}
