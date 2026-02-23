using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._Horizon.HorizonLink;

/// <summary>
/// Аттрибут используется для того, чтобы отметить класс как инжектируемый
/// в жизненный цикл игры (Content.Server, Content.Client или Content.Shared).
/// <br/>
/// Класс который помечень этим аттрибутом, должен наследовать <see cref="HorizonModule"/>
/// в котором уже прописаны все методы для интеграции в корень игры.
/// <br/>
/// Вам остаётся переопределить методы PreInit, Init, PostInit и тп. Дальше пишем код
/// который должен как либо интегрироваться в систему через рефлексию.
/// Пространство имён рефлексии: <c>Robust.Shared.Reflection</c>.
/// <remarks>
/// Лучше использовать методы рефлексии, которые предоставляет движок RobustToolbox.
/// Потому что если попытаться использовать рефлексию C#, то движок дальше не запустит
/// игру, потому что находясь в режиме Sandbox, он старается "предотвращать" запуск
/// небезопасного кода.
/// </remarks>
/// </summary>
///
// Как говорят источники в интернете, механизм песочницы не позволяет выполнять
// произвольный код на клиенте и сервере, чтобы обезопасить игроков от взлома.
// Но я не уверен насколько это хорошее решение, является ли правдой и
// действительно в реальности помогает?
// Тот кто хочет взломать систему, рано или поздно взломает...
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class HorizonModuleAttribute : Attribute
{
    public int Priority { get; }

    public HorizonModuleAttribute(int priority = 0)
    {
        Priority = priority;
    }
}
