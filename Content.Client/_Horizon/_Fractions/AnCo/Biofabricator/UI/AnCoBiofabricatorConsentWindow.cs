using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Horizon._Fractions.AnCo.Biofabricator.UI;

public sealed class AnCoBiofabricatorConsentWindow : DefaultWindow
{
    public readonly Button DenyButton;
    public readonly Button AcceptButton;

    public AnCoBiofabricatorConsentWindow()
    {
        Title = Loc.GetString("anco-biofabricator-consent-window-title");

        Contents.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children =
                    {
                        (new Label()
                        {
                            Text = Loc.GetString("anco-biofabricator-consent-window-prompt-text-part")
                        }),
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            Align = AlignMode.Center,
                            Children =
                            {
                                (AcceptButton = new Button
                                {
                                    Text = Loc.GetString("anco-biofabricator-consent-window-accept-button"),
                                }),

                                (new Control()
                                {
                                    MinSize = new Vector2(20, 0)
                                }),

                                (DenyButton = new Button
                                {
                                    Text = Loc.GetString("anco-biofabricator-consent-window-deny-button"),
                                })
                            }
                        },
                    }
                },
            }
        });
    }
}
