using Content.Client.Popups;
using Content.Client.UserInterface.Controls;
using Content.Shared.AdvancedRCD;
using Content.Shared.AdvancedRCD.Components;
using Content.Shared.Materials;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.AdvancedRCD;

[UsedImplicitly]
public sealed class AdvancedRCDMenuBoundUserInterface : BoundUserInterface
{
    private const string TopLevelActionCategory = "Main";
    private const string BoardsCategory = "Boards";
    private const int MaxBoardsPerCategory = 10;

    private static readonly Dictionary<string, (string Tooltip, SpriteSpecifier Sprite)> CategoryGroupingInfo
        = new Dictionary<string, (string Tooltip, SpriteSpecifier Sprite)>
        {
            ["Walls"] = ("advanced-rcd-category-walls", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/walls_and_flooring.png"))),
            ["Windows"] = ("advanced-rcd-category-windows", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/windows_and_grilles.png"))),
            ["Airlocks"] = ("advanced-rcd-category-airlocks", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/airlocks.png"))),
            ["Electrical"] = ("advanced-rcd-category-electrical", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/multicoil.png"))),
            ["Lighting"] = ("advanced-rcd-category-lighting", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/lighting.png"))),
            ["Boards"] = ("advanced-rcd-category-boards", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/machine_frame.png"))),
        };

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    private SimpleRadialMenu? _menu;

    public AdvancedRCDMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<AdvancedRCDComponent>(Owner, out var advRcd))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);

        var models = BuildMenuOptions(advRcd);
        _menu.SetButtons(models);

        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOption> BuildMenuOptions(AdvancedRCDComponent advRcd)
    {
        Dictionary<string, List<RadialMenuActionOption>> buttonsByCategory = new();
        List<RadialMenuActionOption> topLevelActions = new();

        // Add base RCD prototypes
        foreach (var protoId in advRcd.AvailablePrototypes)
        {
            if (!_prototypeManager.TryIndex<AdvancedRCDPrototype>(protoId, out var prototype))
                continue;

            if (prototype.Category == TopLevelActionCategory)
            {
                var topLevelActionOption = new RadialMenuActionOption<AdvancedRCDPrototype>(HandleRcdProtoClick, prototype)
                {
                    Sprite = prototype.Sprite,
                    ToolTip = GetRcdTooltip(prototype)
                };
                topLevelActions.Add(topLevelActionOption);
                continue;
            }

            if (!CategoryGroupingInfo.TryGetValue(prototype.Category, out _))
                continue;

            if (!buttonsByCategory.TryGetValue(prototype.Category, out var list))
            {
                list = new List<RadialMenuActionOption>();
                buttonsByCategory.Add(prototype.Category, list);
            }

            var actionOption = new RadialMenuActionOption<AdvancedRCDPrototype>(HandleRcdProtoClick, prototype)
            {
                Sprite = prototype.Sprite,
                ToolTip = GetRcdTooltip(prototype)
            };
            list.Add(actionOption);
        }

        // Add inserted entity prototypes from machine boards
        if (advRcd.InsertedEntities.Count > 0)
        {
            var allBoardButtons = new List<RadialMenuActionOption>();

            foreach (var (entityProtoId, boardData) in advRcd.InsertedEntities)
            {
                if (!boardData.Enabled)
                    continue;

                if (!_prototypeManager.TryIndex<EntityPrototype>(entityProtoId, out var entProto))
                    continue;

                var actionOption = new RadialMenuActionOption<string>(HandleEntityProtoClick, entityProtoId.Id)
                {
                    Sprite = new SpriteSpecifier.EntityPrototype(entityProtoId.Id),
                    ToolTip = GetEntityTooltip(entProto, boardData.MaterialCost)
                };
                allBoardButtons.Add(actionOption);
            }

            // Split boards into multiple categories if more than MaxBoardsPerCategory
            if (allBoardButtons.Count > 0)
            {
                if (allBoardButtons.Count <= MaxBoardsPerCategory)
                {
                    buttonsByCategory[BoardsCategory] = allBoardButtons;
                }
                else
                {
                    var pageNum = 1;
                    for (var j = 0; j < allBoardButtons.Count; j += MaxBoardsPerCategory)
                    {
                        var pageButtons = allBoardButtons.GetRange(j, Math.Min(MaxBoardsPerCategory, allBoardButtons.Count - j));
                        var categoryName = pageNum == 1 ? BoardsCategory : $"{BoardsCategory}{pageNum}";
                        buttonsByCategory[categoryName] = pageButtons;
                        pageNum++;
                    }
                }
            }
        }

        var models = new RadialMenuOption[buttonsByCategory.Count + topLevelActions.Count];
        var i = 0;
        foreach (var (key, list) in buttonsByCategory)
        {
            if (CategoryGroupingInfo.TryGetValue(key, out var groupInfo))
            {
                models[i] = new RadialMenuNestedLayerOption(list)
                {
                    Sprite = groupInfo.Sprite,
                    ToolTip = Loc.GetString(groupInfo.Tooltip)
                };
            }
            // Handle paginated board categories (Boards2, Boards3, etc.)
            else if (key.StartsWith(BoardsCategory) && CategoryGroupingInfo.TryGetValue(BoardsCategory, out var boardsInfo))
            {
                var pageNum = key.Substring(BoardsCategory.Length);
                models[i] = new RadialMenuNestedLayerOption(list)
                {
                    Sprite = boardsInfo.Sprite,
                    ToolTip = $"{Loc.GetString(boardsInfo.Tooltip)} {pageNum}"
                };
            }
            else
            {
                models[i] = new RadialMenuNestedLayerOption(list)
                {
                    ToolTip = key
                };
            }
            i++;
        }

        foreach (var action in topLevelActions)
        {
            models[i] = action;
            i++;
        }

        return models;
    }

    private void HandleRcdProtoClick(AdvancedRCDPrototype proto)
    {
        SendMessage(new AdvancedRCDSelectMessage(proto.ID));

        if (_playerManager.LocalSession?.AttachedEntity == null)
            return;

        string name;
        if (proto.Prototype != null &&
            _prototypeManager.TryIndex<EntityPrototype>(proto.Prototype, out var entProto))
        {
            name = entProto.Name;
        }
        else
        {
            name = Loc.GetString(proto.Name);
        }

        var msg = Loc.GetString("advanced-rcd-selected", ("name", name));
        var popup = EntMan.System<PopupSystem>();
        popup.PopupClient(msg, Owner, _playerManager.LocalSession.AttachedEntity);
    }

    private void HandleEntityProtoClick(string entityProtoId)
    {
        SendMessage(new AdvancedRCDSelectEntityMessage(entityProtoId));

        if (_playerManager.LocalSession?.AttachedEntity == null)
            return;

        if (!_prototypeManager.TryIndex<EntityPrototype>(entityProtoId, out var entProto))
            return;

        var msg = Loc.GetString("advanced-rcd-selected", ("name", entProto.Name));
        var popup = EntMan.System<PopupSystem>();
        popup.PopupClient(msg, Owner, _playerManager.LocalSession.AttachedEntity);
    }

    private string GetRcdTooltip(AdvancedRCDPrototype rcdProto)
    {
        string name;
        if (rcdProto.Prototype != null &&
            _prototypeManager.TryIndex<EntityPrototype>(rcdProto.Prototype, out var entProto))
        {
            name = entProto.Name;
        }
        else
        {
            name = Loc.GetString(rcdProto.Name);
        }

        var costText = "";
        if (rcdProto.MaterialCost.Count > 0)
        {
            var costParts = new List<string>();
            foreach (var (mat, amount) in rcdProto.MaterialCost)
            {
                if (_prototypeManager.TryIndex<MaterialPrototype>(mat, out var matProto))
                {
                    costParts.Add($"{Loc.GetString(matProto.Name)}: {amount}");
                }
            }

            if (costParts.Count > 0)
                costText = $"\n{string.Join(", ", costParts)}";
        }

        var tooltip = name;
        if (tooltip.Length > 0)
            tooltip = OopsConcat(char.ToUpper(tooltip[0]).ToString(), tooltip.Remove(0, 1));

        return $"{tooltip}{costText}";
    }

    private string GetEntityTooltip(EntityPrototype entProto, Dictionary<string, int> materialCost)
    {
        var name = entProto.Name;

        // Use stored material cost from board data
        var costText = "";
        if (materialCost.Count > 0)
        {
            var costParts = new List<string>();
            foreach (var (mat, amount) in materialCost)
            {
                if (_prototypeManager.TryIndex<MaterialPrototype>(mat, out var matProto))
                {
                    costParts.Add($"{Loc.GetString(matProto.Name)}: {amount}");
                }
            }

            if (costParts.Count > 0)
                costText = $"\n{string.Join(", ", costParts)}";
        }

        var tooltip = name;
        if (tooltip.Length > 0)
            tooltip = OopsConcat(char.ToUpper(tooltip[0]).ToString(), tooltip.Remove(0, 1));

        return $"{tooltip}{costText}";
    }

    private static string OopsConcat(string a, string b)
    {
        // This exists to prevent Roslyn being clever and compiling something that fails sandbox checks.
        return a + b;
    }
}
