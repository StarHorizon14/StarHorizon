using Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;
using JetBrains.Annotations;

namespace Content.Client._Horizon._Fractions.AnCo.WeaponCaseHack;

[UsedImplicitly]
public sealed class AnCoWeaponCaseHackBoundUserInterface : BoundUserInterface
{
    private AnCoWeaponCaseHackWindow? _window;

    public AnCoWeaponCaseHackBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new AnCoWeaponCaseHackWindow();
        _window.OnClose += Close;
        _window.OnChoose += (chooseB, nodeIndex) => SendMessage(new AnCoHackChooseMessage(chooseB, nodeIndex));
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not AnCoHackBoundUserInterfaceState current)
            return;

        _window?.UpdateState(current);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Dispose();
        _window = null;
    }
}
