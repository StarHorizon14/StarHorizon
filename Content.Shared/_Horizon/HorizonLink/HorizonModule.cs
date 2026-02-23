namespace Content.Shared._Horizon.HorizonLink;

public abstract class HorizonModule : IHorizonModule
{
    /// <summary>
    /// Вызывается до Init.
    /// </summary>
    public virtual void InitBefore() { }
    /// <summary>
    /// Вызывается после Init
    /// </summary>
    public virtual void InitAfter() { }

    /// <summary>
    /// Вызывается до PostInit.
    /// </summary>
    public virtual void PostInitBefore() { }
    /// <summary>
    /// Вызывается после PostInit.
    /// </summary>
    public virtual void PostInitAfter() { }

    /// <summary>
    /// Вызывается до PreInit.
    /// </summary>
    public virtual void PreInitBefore() { }
    /// <summary>
    /// Вызывается после PreInit.
    /// </summary>
    public virtual void PreInitAfter() { }

    public virtual void ShutdownBefore() { }
    public virtual void ShutdownAfter() { }

    public virtual void UpdateBefore(float frameTime) { }
    public virtual void UpdateAfter(float frameTime) { }
}
