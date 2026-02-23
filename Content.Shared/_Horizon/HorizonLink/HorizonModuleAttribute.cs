using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._Horizon.HorizonLink;

/// <summary>
/// Аттрибут используется для того, чтобы отметить класс как интегрируемый
/// в жизненный цикл игры (Content.Server, Content.Client, Content.Shared).
/// При использовании аттрибута, можно использовать методы PreInit, Init, PostInit
/// и другие методы жизненного цикла игры.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class HorizonModuleAttribute : Attribute
{
    public int Priority { get; }

    public HorizonModuleAttribute(int priority = 0)
    {
        Priority = priority;
    }
}
