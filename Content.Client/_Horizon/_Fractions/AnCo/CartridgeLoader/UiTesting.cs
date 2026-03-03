using Content.Client._Horizon._Fractions.AnCo.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Console;

namespace Content.Client._Horizon._Fractions.AnCo.CartridgeLoader;


public sealed partial class UiTesting : IConsoleCommand
{
    public string Command => "ui_companion";
    public string Description => "Открывает интерфейс настроек приложение компаньоны для разметки дизайна";
    public string Help => "Использование: ui_companion";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // Создаем временное окно-контейнер
        var window = new DefaultWindow();
        window.Name = "Интерфейс \"Компаньоны\"";
        window.SetSize = new(600, 400);
        var ui = new CompanionUiFragment();
        window.Contents.AddChild(ui);
        window.OpenCentered();

        // Код для проверки свойств, я его использую, чтобы понимать
        // какие свойства есть у элемента, чтобы применить в xaml.
        // В ином случае придётся гадать и 100+ раз перезапускать клиент
        // при ошибки парсинга.
        //PanelContainer test = new();

        shell.WriteLine("Интерфейс открыт!");
    }
}
