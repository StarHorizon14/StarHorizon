namespace Content.Shared._Horizon.HorizonLink;

/// <summary>
/// Можно наследовать для того, чтобы HorizonLink нашёл класс который добавляет функционал в жизненный цикл игры.
/// <br/>
/// После добавления функционала, можно внедриться в разные менеджеры, для пересоздания функционала на свой.
/// <br/>
/// К примеру, у нас имеется класс IChatManager, мы его можем привязать к своей системе IHorizonChatManager,
/// тем самым, нам теперь не придётся менять функционал IChatManager и мы можем не только реализовать подобный
/// функционал как у игры, но ещё и добавить много новых функций от себя. Главное не забывайте про наследования
/// классов с готовыми решениями игры. Это даст возможность не изобретать велосипед и добавить наш код.
///
/// <para>
/// Не пытайтесь попытаться внедриться в функционал кода движка, мы имеем право редактировать ТОЛЬКО
/// код Content.Client, Content.Server и Content.Shared!
/// </para>
/// </summary>
public abstract class HorizonModule : IHorizonModule
{
    /// <summary>
    /// Этап Связей. Перед всем перечисленным далее.
    /// <para>
    /// Движок блокирует список регистрации и начинает отдавать зависимости.
    /// В конце этого этапа вызывается IoCManager.BuildGraph().<br/>
    /// IoCManager.BuildGraph -> "Инициализирует граф объектов, создавая каждый объект и разрешая все зависимости."
    /// <para><b>Клиент:</b></para>
    /// <list type="bullet">
    ///     <item><description>Инициализация графического движка и аудио.</description></item>
    ///     <item><description>Подключение стилей UI (UserInterfaceManager).</description></item>
    /// </list>
    /// <para><b>Сервер:</b></para>
    /// <list type="bullet">
    ///     <item><description>Инициализация карты (IMapManager) и физического движка.</description></item>
    ///     <item><description>Загрузка конфигурации из файла (IConfigurationManager).</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public virtual void InitBefore() { }
    /// <summary>
    /// Этап Связей. После всего перечисленного далее.
    /// <para>
    /// Движок блокирует список регистрации и начинает отдавать зависимости.
    /// В конце этого этапа вызывается IoCManager.BuildGraph().<br/>
    /// IoCManager.BuildGraph -> "Инициализирует граф объектов, создавая каждый объект и разрешая все зависимости."
    /// <para><b>Клиент:</b></para>
    /// <list type="bullet">
    ///     <item><description>Инициализация графического движка и аудио.</description></item>
    ///     <item><description>Подключение стилей UI (UserInterfaceManager).</description></item>
    /// </list>
    /// <para><b>Сервер:</b></para>
    /// <list type="bullet">
    ///     <item><description>Инициализация карты (IMapManager) и физического движка.</description></item>
    ///     <item><description>Загрузка конфигурации из файла (IConfigurationManager).</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public virtual void InitAfter() { }

    /// <summary>
    /// Этап Контента. Перед всем перечисленным далее.
    /// <para>
    /// Игра полностью собрана. Теперь она сканирует сборки на наличие команд, прототипов и систем.
    /// <para><b>Клиент:</b></para>
    /// <list type="bullet">
    ///     <item><description>Авторегистрация команд, игра заносит всё в RegisterCommands.</description></item>
    ///     <item><description>Загрузка прототипов, чтение yml файлов. (предметы, сущности, стены и тп.)</description></item>
    /// </list>
    /// <para><b>Сервер:</b></para>
    /// <list type="bullet">
    ///     <item><description>Регистрация серверных консольных команд.</description></item>
    ///     <item><description>Запуск сетевого слушателя (Network Heartbeat).</description></item>
    ///     <item><description>Подготовка игровых правил (Game Rules).</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public virtual void PostInitBefore() { }
    /// <summary>
    /// Этап Контента. После всего перечисленного далее.
    /// <para>
    /// Игра полностью собрана. Теперь она сканирует сборки на наличие команд, прототипов и систем.
    /// <para><b>Клиент:</b></para>
    /// <list type="bullet">
    ///     <item><description>Авторегистрация команд, игра заносит всё в RegisterCommands.</description></item>
    ///     <item><description>Загрузка прототипов, чтение yml файлов. (предметы, сущности, стены и тп.)</description></item>
    /// </list>
    /// <para><b>Сервер:</b></para>
    /// <list type="bullet">
    ///     <item><description>Регистрация серверных консольных команд.</description></item>
    ///     <item><description>Запуск сетевого слушателя (Network Heartbeat).</description></item>
    ///     <item><description>Подготовка игровых правил (Game Rules).</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public virtual void PostInitAfter() { }

    /// <summary>
    /// Этап регистрации / IoC. Перед всем перечисленным далее.
    /// <para>
    /// На данном этапе формируются списки менеджеров, которые выполняют какой либо функционал.
    /// <para><b>Клиент:</b></para>
    /// <list type="bullet">
    ///     <item><description>Поля Dependency не заполнены и равняются null.</description></item>
    ///     <item><description>Регистрация систем ввода IInputManager (он в коде движка, так что данный менеджер
    ///     мы не имеем право изменять, даже с помощью рефлексии и HorizonLink).</description></item>
    /// </list>
    /// <para><b>Сервер:</b></para>
    /// <list type="bullet">
    ///     <item><description>Регистрация базы данных IServerDbManager (EntryPoint).</description></item>
    ///     <item><description>Регистрация систем логов и администраторских прав.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public virtual void PreInitBefore() { }
    /// <summary>
    /// Этап регистрации / IoC. После всего перечисленного далее.
    /// <para>
    /// На данном этапе формируются списки менеджеров, которые выполняют какой либо функционал.
    /// <para><b>Клиент:</b></para>
    /// <list type="bullet">
    ///     <item><description>Поля Dependency не заполнены и равняются null.</description></item>
    ///     <item><description>Регистрация систем ввода IInputManager (он в коде движка, так что данный менеджер
    ///     мы не имеем право изменять, даже с помощью рефлексии и HorizonLink).</description></item>
    /// </list>
    /// <para><b>Сервер:</b></para>
    /// <list type="bullet">
    ///     <item><description>Регистрация базы данных IServerDbManager (EntryPoint).</description></item>
    ///     <item><description>Регистрация систем логов и администраторских прав.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public virtual void PreInitAfter() { }

    public virtual void ShutdownBefore() { }
    public virtual void ShutdownAfter() { }

    public virtual void UpdateBefore(float frameTime) { }
    public virtual void UpdateAfter(float frameTime) { }
}
