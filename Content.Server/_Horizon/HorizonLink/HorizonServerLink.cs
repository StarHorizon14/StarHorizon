using Content.Shared._Horizon.HorizonLink;

namespace Content.Server._Horizon.HorizonLink;

/// <summary>
/// Инъектор сервера
/// </summary>
public sealed class HorizonServerLink : HorizonLinkBase
{
    public static HorizonServerLink Instance { get; } = new();

    public void Scaffold()
    {
        Log.Info("Запущено сканирование серверных модулей");
        DiscoverModules();
    }
}
