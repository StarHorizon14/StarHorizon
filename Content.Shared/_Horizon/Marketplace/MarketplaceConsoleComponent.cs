using Content.Shared._NF.Bank.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Contraband;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.Marketplace;

/// <summary>
/// A console connected to the sector-wide player marketplace.
/// Players insert an item into the console's slot to list it for credits and can buy listings made from any console.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MarketplaceConsoleComponent : Component
{
    public const string ItemSlotId = "marketplace-item";

    /// <summary>
    /// Consoles only see listings and requests of their own network, e.g. the legal sector market or the black market.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Network = "Sector";

    /// <summary>
    /// Hide who is trading: names, ID cards and stations are replaced by codes before being sent to clients.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Anonymous;

    /// <summary>
    /// Use the black market look for the UI.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BlackMarket;

    /// <summary>
    /// The slot an item is placed in before being listed.
    /// </summary>
    [DataField]
    public ItemSlot ItemSlot = new();

    /// <summary>
    /// Share of every sale that is taken from the seller and sent to <see cref="TaxAccount"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TaxRate = 0.1f;

    /// <summary>
    /// Where the tax goes. <see cref="SectorBankAccount.Invalid"/> takes it out of the economy, like other black market taxes.
    /// </summary>
    [DataField]
    public SectorBankAccount TaxAccount = SectorBankAccount.Frontier;

    /// <summary>
    /// Highest allowed price for a single unit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxUnitPrice = 10_000_000;

    /// <summary>
    /// How many listings a single character may have at once.
    /// </summary>
    [DataField]
    public int MaxListingsPerSeller = 30;

    /// <summary>
    /// How many open requests a single character may have at once.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxRequestsPerRequester = 10;

    /// <summary>
    /// How many pending offers a single request may receive.
    /// </summary>
    [DataField]
    public int MaxOffersPerRequest = 20;

    /// <summary>
    /// How many offers (pending or returned) a single character may have at once.
    /// </summary>
    [DataField]
    public int MaxOffersPerOfferer = 20;

    public const int MaxTitleLength = 64;
    public const int MaxDescriptionLength = 500;
    public const int MaxRequestQuantity = 10_000;

    /// <summary>
    /// Contraband severities that can't be traded (class 2 and class 3), including when inside the item.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ContrabandSeverityPrototype>> ForbiddenContraband = new()
    {
        "Class2",
        "Class2Expedition",
        "Class3General",
        "Class3Cult",
        "Class3Pirate",
        "Class3Syndicate",
        "Class3Wizard",
        "Class3Expedition",
        "Class3MobHuman",
        "Class3MobCreature",
        "Class3MobConstruct",
    };

    /// <summary>
    /// Items matching this can never be listed, nor can items containing them.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
}
