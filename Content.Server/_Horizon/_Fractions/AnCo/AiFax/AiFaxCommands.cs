using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Utility;

namespace Content.Server._Horizon._Fractions.AnCo.AiFax;

/// <summary>
/// Command to set AI fax system prompt.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class AiFaxPromptCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "aifax_prompt";
    public string Description => "Устанавливает системный промпт для AI-факса.";
    public string Help => "Usage: aifax_prompt <entityUid> <prompt text>\nExample: aifax_prompt 12345 Ты помощник на станции.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine("Использование: aifax_prompt <entityUid> <текст промпта>");
            return;
        }

        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteLine($"Неверный EntityUid: {args[0]}");
            return;
        }

        if (!_entityManager.TryGetComponent<AiFaxComponent>(uid, out var comp))
        {
            shell.WriteLine($"Entity {uid} не имеет AiFaxComponent");
            return;
        }

        var prompt = string.Join(" ", args.Skip(1));
        comp.SystemPrompt = prompt;
        shell.WriteLine($"Промпт установлен ({prompt.Length} символов)");
    }
}

/// <summary>
/// Command to add context file to AI fax.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class AiFaxAddContextCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "aifax_addcontext";
    public string Description => "Добавляет файл контекста для AI-факса.";
    public string Help => "Usage: aifax_addcontext <entityUid> <path>\nExample: aifax_addcontext 12345 /Prototypes/_Horizon/lore.md";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine("Использование: aifax_addcontext <entityUid> <путь к файлу>");
            return;
        }

        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteLine($"Неверный EntityUid: {args[0]}");
            return;
        }

        if (!_entityManager.TryGetComponent<AiFaxComponent>(uid, out var comp))
        {
            shell.WriteLine($"Entity {uid} не имеет AiFaxComponent");
            return;
        }

        var path = new ResPath(args[1]);
        comp.ContextFiles.Add(path);
        comp.LoadedContextContent = null; // Сбросить кэш для перезагрузки
        shell.WriteLine($"Добавлен файл контекста: {args[1]}");
        shell.WriteLine($"Всего файлов: {comp.ContextFiles.Count}");
    }
}

/// <summary>
/// Command to add context folder to AI fax.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class AiFaxAddFolderCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "aifax_addfolder";
    public string Description => "Добавляет папку контекста для AI-факса.";
    public string Help => "Usage: aifax_addfolder <entityUid> <path>\nExample: aifax_addfolder 12345 /Prototypes/_Horizon/_Fractions/AnCo";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine("Использование: aifax_addfolder <entityUid> <путь к папке>");
            return;
        }

        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteLine($"Неверный EntityUid: {args[0]}");
            return;
        }

        if (!_entityManager.TryGetComponent<AiFaxComponent>(uid, out var comp))
        {
            shell.WriteLine($"Entity {uid} не имеет AiFaxComponent");
            return;
        }

        var path = new ResPath(args[1]);
        comp.ContextFolders.Add(path);
        comp.LoadedContextContent = null; // Сбросить кэш для перезагрузки
        shell.WriteLine($"Добавлена папка контекста: {args[1]}");
        shell.WriteLine($"Всего папок: {comp.ContextFolders.Count}");
    }
}

/// <summary>
/// Command to reload AI fax context files.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class AiFaxReloadCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "aifax_reload";
    public string Description => "Перезагружает контекстные файлы AI-факса.";
    public string Help => "Usage: aifax_reload <entityUid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Использование: aifax_reload <entityUid>");
            return;
        }

        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteLine($"Неверный EntityUid: {args[0]}");
            return;
        }

        if (!_entityManager.TryGetComponent<AiFaxComponent>(uid, out var comp))
        {
            shell.WriteLine($"Entity {uid} не имеет AiFaxComponent");
            return;
        }

        comp.LoadedContextContent = null; // Сбросить кэш - перезагрузится при следующем запросе
        shell.WriteLine("Контекст сброшен. Файлы будут перезагружены при следующем запросе.");
        shell.WriteLine($"Файлов: {comp.ContextFiles.Count}, Папок: {comp.ContextFolders.Count}");
    }
}

/// <summary>
/// Command to clear AI fax conversation history.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class AiFaxClearHistoryCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "aifax_clearhistory";
    public string Description => "Очищает историю диалогов AI-факса.";
    public string Help => "Usage: aifax_clearhistory <entityUid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Использование: aifax_clearhistory <entityUid>");
            return;
        }

        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteLine($"Неверный EntityUid: {args[0]}");
            return;
        }

        if (!_entityManager.TryGetComponent<AiFaxComponent>(uid, out var comp))
        {
            shell.WriteLine($"Entity {uid} не имеет AiFaxComponent");
            return;
        }

        var count = comp.ConversationHistory.Count;
        comp.ConversationHistory.Clear();
        shell.WriteLine($"История очищена. Удалено {count} сообщений.");
    }
}

/// <summary>
/// Command to show AI fax status and settings.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class AiFaxStatusCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "aifax_status";
    public string Description => "Показывает статус и настройки AI-факса.";
    public string Help => "Usage: aifax_status <entityUid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Использование: aifax_status <entityUid>");
            return;
        }

        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteLine($"Неверный EntityUid: {args[0]}");
            return;
        }

        if (!_entityManager.TryGetComponent<AiFaxComponent>(uid, out var comp))
        {
            shell.WriteLine($"Entity {uid} не имеет AiFaxComponent");
            return;
        }

        shell.WriteLine($"=== AI Fax Status: {uid} ===");
        shell.WriteLine($"Модель: {comp.Model}");
        shell.WriteLine($"Ключевое слово: {comp.TriggerKeyword} (требуется: {comp.RequireTriggerKeyword})");
        shell.WriteLine($"Имя отправителя: {comp.ResponseSenderName}");
        shell.WriteLine($"Макс. длина ответа: {comp.MaxResponseLength}");
        shell.WriteLine($"Таймаут: {comp.RequestTimeoutSeconds}с");
        shell.WriteLine($"Макс. история: {comp.MaxHistoryLength} обменов");
        shell.WriteLine($"Сообщений в истории: {comp.ConversationHistory.Count}");
        shell.WriteLine($"Копировать на себя: {(comp.CopyResponseToSelf ? "да" : "нет")}");
        shell.WriteLine($"Файлов контекста: {comp.ContextFiles.Count}");
        shell.WriteLine($"Папок контекста: {comp.ContextFolders.Count}");
        shell.WriteLine($"Контекст загружен: {(comp.LoadedContextContent != null ? $"{comp.LoadedContextContent.Length} символов" : "нет")}");
    }
}

/// <summary>
/// Command to list all AI fax entities.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class AiFaxListCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "aifax_list";
    public string Description => "Показывает список всех AI-факсов на карте.";
    public string Help => "Usage: aifax_list";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var query = _entityManager.EntityQueryEnumerator<AiFaxComponent>();
        var count = 0;

        shell.WriteLine("=== AI Fax Entities ===");
        while (query.MoveNext(out var uid, out var comp))
        {
            var name = _entityManager.GetComponent<MetaDataComponent>(uid).EntityName;
            shell.WriteLine($"  {uid}: {name} (модель: {comp.Model}, история: {comp.ConversationHistory.Count} сообщений)");
            count++;
        }

        if (count == 0)
            shell.WriteLine("  AI-факсы не найдены");
        else
            shell.WriteLine($"Всего: {count}");
    }
}

/// <summary>
/// Command to set AI fax model.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class AiFaxModelCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "aifax_model";
    public string Description => "Устанавливает модель Gemini для AI-факса.";
    public string Help => "Usage: aifax_model <entityUid> <model>\nExample: aifax_model 12345 gemini-1.5-flash";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine("Использование: aifax_model <entityUid> <модель>");
            shell.WriteLine("Модели: gemini-1.5-flash, gemini-1.5-pro, gemini-3.1-flash-lite и др.");
            return;
        }

        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteLine($"Неверный EntityUid: {args[0]}");
            return;
        }

        if (!_entityManager.TryGetComponent<AiFaxComponent>(uid, out var comp))
        {
            shell.WriteLine($"Entity {uid} не имеет AiFaxComponent");
            return;
        }

        comp.Model = args[1];
        shell.WriteLine($"Модель установлена: {args[1]}");
    }
}

/// <summary>
/// Command to set AI fax API key.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class AiFaxApiKeyCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "aifax_apikey";
    public string Description => "Устанавливает API ключ Gemini для AI-факса.";
    public string Help => "Usage: aifax_apikey <entityUid> <apikey>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine("Использование: aifax_apikey <entityUid> <apikey>");
            return;
        }

        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteLine($"Неверный EntityUid: {args[0]}");
            return;
        }

        if (!_entityManager.TryGetComponent<AiFaxComponent>(uid, out var comp))
        {
            shell.WriteLine($"Entity {uid} не имеет AiFaxComponent");
            return;
        }

        comp.ApiKey = args[1];
        shell.WriteLine($"API ключ установлен ({args[1].Length} символов)");
    }
}

/// <summary>
/// Command to clear context files/folders from AI fax.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class AiFaxClearContextCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "aifax_clearcontext";
    public string Description => "Очищает список файлов и папок контекста AI-факса.";
    public string Help => "Usage: aifax_clearcontext <entityUid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Использование: aifax_clearcontext <entityUid>");
            return;
        }

        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteLine($"Неверный EntityUid: {args[0]}");
            return;
        }

        if (!_entityManager.TryGetComponent<AiFaxComponent>(uid, out var comp))
        {
            shell.WriteLine($"Entity {uid} не имеет AiFaxComponent");
            return;
        }

        var filesCount = comp.ContextFiles.Count;
        var foldersCount = comp.ContextFolders.Count;

        comp.ContextFiles.Clear();
        comp.ContextFolders.Clear();
        comp.LoadedContextContent = null;

        shell.WriteLine($"Контекст очищен. Удалено файлов: {filesCount}, папок: {foldersCount}");
    }
}
