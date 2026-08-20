using Content.Client.Eui;
using Content.Shared._Horizon._Fractions.AnCo.Biofabricator;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Horizon._Fractions.AnCo.Biofabricator.UI;

[UsedImplicitly]
public sealed class AnCoBiofabricatorConsentEui : BaseEui
{
    private readonly AnCoBiofabricatorConsentWindow _window;

    public AnCoBiofabricatorConsentEui()
    {
        _window = new AnCoBiofabricatorConsentWindow();

        _window.DenyButton.OnPressed += _ =>
        {
            SendMessage(new AnCoBiofabricatorConsentChoiceMessage(AnCoBiofabricatorConsentButton.Deny));
            _window.Close();
        };

        _window.OnClose += () => SendMessage(new AnCoBiofabricatorConsentChoiceMessage(AnCoBiofabricatorConsentButton.Deny));

        _window.AcceptButton.OnPressed += _ =>
        {
            SendMessage(new AnCoBiofabricatorConsentChoiceMessage(AnCoBiofabricatorConsentButton.Accept));
            _window.Close();
        };
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }
}
