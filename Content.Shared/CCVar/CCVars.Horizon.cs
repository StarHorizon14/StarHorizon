using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Whether the Castaway game rule's ambient music plays for survivors adrift in space.
    ///     Meant as a kill switch if the ambience causes issues.
    /// </summary>
    public static readonly CVarDef<bool> CastawayAmbienceEnabled =
        CVarDef.Create("castaway.ambience_enabled", true, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Whether players spawning into the Castaway (LostVoid) game rule receive a bank account.
    /// </summary>
    public static readonly CVarDef<bool> CastawaySpawnBankAccountEnabled =
        CVarDef.Create("castaway.spawn_bank_account_enabled", false, CVar.SERVER | CVar.ARCHIVE);
}
