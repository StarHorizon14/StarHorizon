using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Horizon.Marketplace;

/// <summary>
/// Colours of the marketplace window: blue for the sector market, red for the black market.
/// </summary>
public sealed class MarketplaceTheme
{
    public static readonly MarketplaceTheme Sector = new()
    {
        HeaderTitleKey = "marketplace-header-title",
        WindowTitleKey = "marketplace-window-title",
    };

    public static readonly MarketplaceTheme BlackMarket = new()
    {
        HeaderTitleKey = "marketplace-black-header-title",
        WindowTitleKey = "marketplace-black-window-title",
        CardBackground = Color.FromHex("#1A0F12"),
        CardBorder = Color.FromHex("#4A1C22"),
        RequestBorder = Color.FromHex("#7A2A2A"),
        Inset = Color.FromHex("#110809"),
        RowBackground = Color.FromHex("#150B0D"),
        HeaderBackground = Color.FromHex("#1C0B0E"),
        HeaderBorder = Color.FromHex("#B3262E"),
        ChipBackground = Color.FromHex("#2A1015"),
        ChipBorder = Color.FromHex("#7D2028"),
        IconBorder = Color.FromHex("#6E1F27"),
        IconBorderHover = Color.FromHex("#FF4D57"),
        Muted = "#A38A8E",
        Accent = "#FF5A62",
    };

    public string HeaderTitleKey = string.Empty;
    public string WindowTitleKey = string.Empty;

    public Color CardBackground = Color.FromHex("#121823");
    public Color CardBorder = Color.FromHex("#23344A");
    public Color OwnBorder = Color.FromHex("#B8892B");
    public Color RequestBorder = Color.FromHex("#3A5E3F");
    public Color Inset = Color.FromHex("#0B0F16");
    public Color RowBackground = Color.FromHex("#0E131C");
    public Color HeaderBackground = Color.FromHex("#0D1520");
    public Color HeaderBorder = Color.FromHex("#2E5D86");
    public Color ChipBackground = Color.FromHex("#142031");
    public Color ChipBorder = Color.FromHex("#2E5D86");
    public Color IconBorder = Color.FromHex("#2E5D86");
    public Color IconBorderHover = Color.FromHex("#5FB4FF");

    public string Muted = "#8391A5";
    public string Price = "#6EE7A0";
    public string Accent = "#7CC4FF";
    public string Danger = "#FF6B6B";

    public PanelContainer MakeDivider()
    {
        return new PanelContainer
        {
            MinSize = new Vector2(0, 1),
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CardBorder },
        };
    }

    public StyleBoxFlat MakeInset()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Inset,
            BorderColor = CardBorder,
            BorderThickness = new Thickness(1),
        };
    }
}
