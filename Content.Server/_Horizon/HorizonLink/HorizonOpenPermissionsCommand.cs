using Robust.Shared.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server._Horizon.HorizonLink;

public sealed class HorizonOpenPermissionsCommand
{
    public string Command => "permissions";

    public void Execute(IConsoleShell shell, string argsStr, string[] args)
    {
        shell.WriteLine("Комманда изменена инъекцией, выполнен инжект код.");
    }
}
