namespace Content.Shared._Horizon.HorizonLink;

/// <summary>
/// Инъектор общего кода
/// </summary>
public sealed class HorizonSharedLink : HorizonLinkBase
{
    public static HorizonSharedLink Instance { get; } = new();

    public void Scaffold()
    {
        Log.Info("Запущено сканирование общих серверных и клиентских модулей");
        DiscoverModules();
    }
}
