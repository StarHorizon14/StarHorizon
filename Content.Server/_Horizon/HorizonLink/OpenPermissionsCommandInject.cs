using Content.Shared._Horizon.HorizonLink;
using Robust.Shared.Console;
using System.Reflection;

namespace Content.Server._Horizon.HorizonLink;

[HorizonModule(priority: 1)]
public sealed class OpenPermissionsCommandInject : HorizonModule
{
    private static readonly ISawmill Sawmill = Logger.GetSawmill("Horizon.Link.Inject");

    public override void PostInitAfter()
    {
        Sawmill.Info("Переопределение команды permissions.");

        var conHost = IoCManager.Resolve<IConsoleHost>();

        var field = typeof(ConsoleHost).GetField("RegisteredCommands",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

        if (field != null)
        {
            var commands = (Dictionary<string, IConsoleCommand>)field.GetValue(conHost)!;

            commands.Remove("permissions");

            var cmd = new HorizonOpenPermissionsCommand();

            conHost.RegisterCommand(cmd.Command, "Описание команды", "Команда переопределена, функционал не задан.", cmd.Execute);

            Sawmill.Info("Переопределение команды permissions завершено, команда имеет новый функционал.");
        }
    }
}
