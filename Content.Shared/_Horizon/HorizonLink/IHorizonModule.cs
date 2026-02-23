using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._Horizon.HorizonLink;

/// <summary>
/// Базовый класс реализующий весь жизненный цикл игры от и до.
/// Вызвать методы игры можно как до функционала, так и после.
/// </summary>
public interface IHorizonModule
{
    void PreInitBefore();
    void PreInitAfter();
    void InitBefore();
    void InitAfter();
    void PostInitBefore();
    void PostInitAfter();
    void UpdateBefore(float frameTime);
    void UpdateAfter(float frameTime);
    void ShutdownBefore();
    void ShutdownAfter();
}
