using Content.Client.UserInterface.Fragments;
using Content.Shared._Horizon._Fractions.AnCo.Companions;
using Robust.Client.UserInterface;

namespace Content.Client._Horizon._Fractions.AnCo.CartridgeLoader.Cartridges;

/// <summary>
/// Клиентская логика для управления фрагментом интерфейса "Компаньоны".<br/>
/// Отвечает за инициализацию визуального представления и синхронизацию состояния с сервером.
/// </summary>
public sealed partial class CompanionUi : UIFragment
{
    private CompanionUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new CompanionUiFragment();
        _fragment.Initialize();
        _fragment.UpdateLoadState();
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not CompanionPDABoundUserInterfaceState uiState)
            return;

        _fragment?.UpdateState(uiState.Companions);
    }
}
