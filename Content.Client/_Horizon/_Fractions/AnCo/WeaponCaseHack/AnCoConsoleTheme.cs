using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._Horizon._Fractions.AnCo.WeaponCaseHack;

/// <summary>
/// Палитра и стили консоли АнКо-сайта (themes/_anco.scss и base/_console-theme.scss сайта):
/// чёрный фон, красный текст, квадратные рамки, моноширинный шрифт.
/// </summary>
public static class AnCoConsoleTheme
{
    public static readonly Color Background = Color.FromHex("#000000");
    public static readonly Color Panel = Color.FromHex("#0d0d0d");
    public static readonly Color MapBackground = Color.FromHex("#050000");
    public static readonly Color Border = Color.FromHex("#620505");
    public static readonly Color Accent = Color.FromHex("#f05a5a");
    public static readonly Color Heading = Color.FromHex("#d21e1e");
    public static readonly Color Text = Color.FromHex("#9e0000");
    public static readonly Color Highlight = Color.FromHex("#ffffff");
    public static readonly Color Muted = Color.FromHex("#2b2f35");
    public static readonly Color Disabled = Color.FromHex("#42464b");
    public static readonly Color Ok = Color.FromHex("#5dff95");
    public static readonly Color Warning = Color.FromHex("#ffb000");
    public static readonly Color Danger = Color.FromHex("#ff3b3b");

    private const string MonoFont = "/EngineFonts/NotoSans/NotoSansMono-Regular.ttf";

    /// <summary>Плоская рамка без скруглений, как у <c>.card</c> на сайте.</summary>
    public static StyleBoxFlat Box(Color background, Color border, float padding = 0)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = padding,
            ContentMarginRightOverride = padding,
            ContentMarginTopOverride = padding,
            ContentMarginBottomOverride = padding,
        };
    }

    public static Font Mono(int size)
    {
        return IoCManager.Resolve<IResourceCache>().GetFont(MonoFont, size);
    }
}
