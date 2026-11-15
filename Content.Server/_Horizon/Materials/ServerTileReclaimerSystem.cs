using Content.Server._Horizon.Shipbreaking.Derelict;
using Content.Server._NF.Bank;
using Content.Shared._Horizon.Materials;
using Content.Shared._NF.Bank.BUI;
using Content.Shared.Maps;
using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Server._Horizon.Materials;

public sealed class ServerTileReclaimerSystem : TileReclaimerSystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly DerelictYardSystem _derelict = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    protected override void SpawnMaterialsFromComposition(Entity<MaterialStorageComponent?, TransformComponent?> ent,
        ContentTileDefinition tileDefinition,
        float efficiency,
        EntityUid sourceGrid)
    {
        if (!TryComp<TileReclaimerComponent>(ent, out var reclaimer) || !reclaimer.Pay)
            return;

        var value = 0.0;
        foreach (var (material, amount) in tileDefinition.MaterialComposition)
        {
            if (_prototype.TryIndex<MaterialPrototype>(material, out var proto))
                value += proto.Price * amount;
        }

        var payout = (int) (value * efficiency);
        if (payout > 0 && _bank.TrySectorDeposit(reclaimer.Account, payout, LedgerEntryType.StationDepositAssetsSold))
            _derelict.CreditScrap(sourceGrid, payout);
    }
}
