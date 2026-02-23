using Content.Shared._Horizon.HorizonLink;

namespace Content.Client._Horizon.HorizonLink;

[HorizonModule(priority: 1000)]
public sealed class TestIntegration : HorizonModule
{
    private readonly ISawmill Log = Logger.GetSawmill("Horizon.Link.TestIntegration");

    public override void PostInitBefore()
    {
        Log.Info($"Интеграция после Init клиента в самом начале метода.");
    }
}
