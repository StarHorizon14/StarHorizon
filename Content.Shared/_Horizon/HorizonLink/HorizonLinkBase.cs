using Robust.Shared.Reflection;

namespace Content.Shared._Horizon.HorizonLink;

/// <summary>
/// Базовый класс линкера. Реализует функционал сканера DLL всех Content файлов игры
/// </summary>
public abstract class HorizonLinkBase
{
    protected readonly List<(IHorizonModule Module, int Priority)> LoadedModules = new();
    public static readonly ISawmill Log = Logger.GetSawmill("Horizon.Link");

    // Жизненный цикл игры
    public void PreInitBefore() => LoadedModules.ForEach(m => m.Module.PreInitBefore());
    public void PreInitAfter() => LoadedModules.ForEach(m => m.Module.PreInitAfter());
    public void InitBefore() => LoadedModules.ForEach(m => m.Module.InitBefore());
    public void InitAfter() => LoadedModules.ForEach(m => m.Module.InitAfter());
    public void PostInitBefore() => LoadedModules.ForEach(m => m.Module.PostInitBefore());
    public void PostInitAfter() => LoadedModules.ForEach(m => m.Module.PostInitAfter());
    public void UpdateBefore(float frameTime) => LoadedModules.ForEach(m => m.Module.UpdateBefore(frameTime));
    public void UpdateAfter(float frameTime) => LoadedModules.ForEach(m => m.Module.UpdateAfter(frameTime));
    public void ShutdownBefore() => LoadedModules.ForEach(m => m.Module.ShutdownBefore());
    public void ShutdownAfter() => LoadedModules.ForEach(m => m.Module.ShutdownAfter());

    /// <summary>
    /// Безопасный сканер, который сканирует Content файл, в зависимости от того, откуда его вызвали.
    /// Если класс HorizonLinkBase был наследован Content.Server, то при вызове метода, он будет сканировать
    /// dll файл серверной части. Также аналогично с Client'ским и Shared кодом.
    /// </summary>
    protected void DiscoverModules()
    {
        var reflection = IoCManager.Resolve<IReflectionManager>();
        var typeFactory = IoCManager.Resolve<IDynamicTypeFactory>();

        foreach (var type in reflection.GetAllChildren<IHorizonModule>())
        {
            if (type.IsAbstract) continue;

            var attr = Attribute.GetCustomAttribute(type, typeof(HorizonModuleAttribute)) as HorizonModuleAttribute;

            if (attr == null) continue;

            try
            {
                var instance = (IHorizonModule)typeFactory.CreateInstance(type);

                IoCManager.InjectDependencies(instance);
                LoadedModules.Add((instance, attr.Priority));
                Log.Info($"Обнаружен модуль {type.Name} (Приоритет: {attr.Priority})");
            }
            catch (Exception e)
            {
                Log.Error($"Ошибка создания модуля {type.Name}: {e}");
            }
        }

        LoadedModules.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        Log.Info($"Инициализация завершена. Всего модулей: {LoadedModules.Count}");
    }
}
