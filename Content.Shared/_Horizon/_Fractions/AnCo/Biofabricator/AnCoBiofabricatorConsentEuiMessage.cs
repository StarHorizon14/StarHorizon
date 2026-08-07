using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon._Fractions.AnCo.Biofabricator;

[Serializable, NetSerializable]
public enum AnCoBiofabricatorConsentButton
{
    Deny,
    Accept,
}

[Serializable, NetSerializable]
public sealed class AnCoBiofabricatorConsentChoiceMessage : EuiMessageBase
{
    public readonly AnCoBiofabricatorConsentButton Button;

    public AnCoBiofabricatorConsentChoiceMessage(AnCoBiofabricatorConsentButton button)
    {
        Button = button;
    }
}
