using Content.Server.Preferences.Managers;
using Content.Shared._Arcane.ERP;
using Content.Shared._Horizon.FlavorText;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.Player;

namespace Content.Server._Arcane.ERP;

public sealed class ErpStatusSystem : EntitySystem
{
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidAppearanceComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(Entity<HumanoidAppearanceComponent> ent, ref PlayerAttachedEvent args)
    {
        var profile = _prefs.GetPreferencesOrNull(args.Player.UserId)?.SelectedCharacter as HumanoidCharacterProfile;
        // Arcane-edit: map the existing Horizon ErpStat consent flag onto the Arcane ERP preference.
        // No = refuse ERP entirely, Consentual/NonCon both allow it (NonCon just skips the per-action ask).
        var preference = profile?.ErpStat is ErpStatus.Consentual or ErpStatus.NonCon
            ? ErpPreference.Yes
            : ErpPreference.No;

        EnsureComp<ArousalComponent>(ent);

        var comp = EnsureComp<ErpPreferenceComponent>(ent);
        var oldPreference = comp.Preference;
        comp.Preference = preference;
        Dirty(ent, comp);

        if (oldPreference != preference)
            RaiseLocalEvent(ent, new ErpPreferenceChangedEvent(oldPreference, preference));
    }
}
