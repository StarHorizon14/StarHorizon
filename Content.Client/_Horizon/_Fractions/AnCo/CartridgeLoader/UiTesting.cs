using Content.Client._Horizon._Fractions.AnCo.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Console;

namespace Content.Client._Horizon._Fractions.AnCo.CartridgeLoader;


public sealed partial class UiTesting : IConsoleCommand
{
    public string Command => "ui_companion";
    public string Description => "Открывает интерфейс настроек компаньона для теста дизайна";
    public string Help => "Использование: ui_companion";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // Создаем временное окно-контейнер
        var window = new DefaultWindow();
        window.SetSize = new(400, 300);
        var ui = new CompanionUiFragment();
        window.Contents.AddChild(ui);
        //ui.UpdateLoadState();
        window.OpenCentered();

        shell.WriteLine("Интерфейс открыт!");
    }
}
