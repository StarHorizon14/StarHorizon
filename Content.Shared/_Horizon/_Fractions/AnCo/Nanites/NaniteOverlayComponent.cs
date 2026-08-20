using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._Horizon._Fractions.AnCo.Nanites;

/// <summary>
/// Маркер того, что на существе отображается визуальный оверлей нанитов.
/// Навешивается через EntityEffect на каждый тик метаболизма реагента
/// и снимается системой, если реагент перестал метаболизироваться.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NaniteOverlayComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;
}
