using Content.Server.EUI;
using Content.Shared._Horizon._Fractions.AnCo.Biofabricator;
using Content.Shared.Eui;
using Content.Shared.Mind;

namespace Content.Server._Horizon._Fractions.AnCo.Biofabricator;

public sealed class AnCoBiofabricatorConsentEui : BaseEui
{
    private readonly EntityUid _mindId;
    private readonly MindComponent _mind;
    private readonly EntityUid _deadBody;
    private readonly AnCoCloneConsentSystem _cloneConsentSystem;

    public AnCoBiofabricatorConsentEui(EntityUid mindId, MindComponent mind, EntityUid deadBody, AnCoCloneConsentSystem cloneConsentSystem)
    {
        _mindId = mindId;
        _mind = mind;
        _deadBody = deadBody;
        _cloneConsentSystem = cloneConsentSystem;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not AnCoBiofabricatorConsentChoiceMessage choice ||
            choice.Button == AnCoBiofabricatorConsentButton.Deny)
        {
            Close();
            return;
        }

        _cloneConsentSystem.HandleConsent(_mindId, _mind, _deadBody, true);
        Close();
    }
}
