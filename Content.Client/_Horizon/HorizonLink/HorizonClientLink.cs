using Content.Shared._Horizon.HorizonLink;
using System.Reflection;

namespace Content.Client._Horizon.HorizonLink;

/// <summary>
/// Инъектор клиента
/// </summary>
public sealed class HorizonClientLink : HorizonLinkBase
{
    public static HorizonClientLink Instance { get; } = new();

    public void Scaffold()
    {
        Log.Info("Запущено сканирование клиентских модулей");
        DiscoverModules();
    }
}
