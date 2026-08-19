using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Fax;
using Robust.Shared.Utility;
using Content.Shared._Horizon._Fractions.AnCo.AiFax;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Fax.Components;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;
using Robust.Shared.Asynchronous;

namespace Content.Server._Horizon._Fractions.AnCo.AiFax;

/// <summary>
/// System that handles AI-powered fax machines.
/// Listens for incoming fax messages, sends them to Gemini API, and returns responses.
/// </summary>
public sealed class AiFaxSystem : EntitySystem
{
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly FaxSystem _faxSystem = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;

    private GeminiApiService _geminiApi = default!;
    private DeepSeekApiService _deepSeekApi = default!;
    private GroqApiService _groqApi = default!;
    private OpenRouterApiService _openRouterApi = default!;
    private GrokApiService _grokApi = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("ai-fax");
        _geminiApi = new GeminiApiService(_logManager);
        _deepSeekApi = new DeepSeekApiService(_logManager);
        _groqApi = new GroqApiService(_logManager);
        _openRouterApi = new OpenRouterApiService(_logManager);
        _grokApi = new GrokApiService(_logManager);

        SubscribeLocalEvent<AiFaxComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
    }

    private void OnPacketReceived(EntityUid uid, AiFaxComponent aiComp, DeviceNetworkPacketEvent args)
    {
        // Check if this is a fax print command
        if (!args.Data.TryGetValue(DeviceNetworkConstants.Command, out string? command) ||
            command != FaxConstants.FaxPrintCommand)
            return;

        // Get fax content
        if (!args.Data.TryGetValue(FaxConstants.FaxPaperContentData, out string? content) ||
            string.IsNullOrWhiteSpace(content))
            return;

        // Check for trigger keyword if required
        var hasTriggerKeyword = !string.IsNullOrEmpty(aiComp.TriggerKeyword) &&
                                content.Contains(aiComp.TriggerKeyword, StringComparison.OrdinalIgnoreCase);
        if (aiComp.RequireTriggerKeyword && !hasTriggerKeyword)
            return;

        // Get sender address for response
        var senderAddress = args.SenderAddress;
        if (string.IsNullOrEmpty(senderAddress))
            return;

        _sawmill.Info($"AI Fax received message from {senderAddress}: {content[..Math.Min(50, content.Length)]}...");

        // Clean message from trigger keyword if present
        var cleanedMessage = hasTriggerKeyword && !string.IsNullOrEmpty(aiComp.TriggerKeyword)
            ? content.Replace(aiComp.TriggerKeyword, "", StringComparison.OrdinalIgnoreCase).Trim()
            : content.Trim();

        if (string.IsNullOrWhiteSpace(cleanedMessage))
        {
            cleanedMessage = "Привет!";
        }

        // Load context files if not already loaded
        if (aiComp.LoadedContextContent == null && (aiComp.ContextFiles.Count > 0 || aiComp.ContextFolders.Count > 0))
        {
            LoadContextFiles(aiComp);
        }

        // Process AI request asynchronously
        ProcessAiRequestAsync(uid, aiComp, senderAddress, cleanedMessage, aiComp.SystemPrompt);
    }

    private static readonly string[] TextExtensions = { ".md", ".txt", ".yml", ".yaml", ".ftl" };

    private void LoadContextFiles(AiFaxComponent aiComp)
    {
        var sb = new StringBuilder();
        var totalFiles = 0;

        // Load individual files
        foreach (var path in aiComp.ContextFiles)
        {
            if (LoadSingleFile(path, sb))
                totalFiles++;
        }

        // Load folders recursively
        foreach (var folderPath in aiComp.ContextFolders)
        {
            totalFiles += LoadFolder(folderPath, sb);
        }

        aiComp.LoadedContextContent = sb.ToString();
        _sawmill.Info($"AI Fax loaded {totalFiles} files, total context: {aiComp.LoadedContextContent.Length} chars");
    }

    private bool LoadSingleFile(ResPath path, StringBuilder sb)
    {
        try
        {
            if (_resourceManager.TryContentFileRead(path, out var stream))
            {
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                sb.AppendLine($"=== File: {path} ===");
                sb.AppendLine(content);
                sb.AppendLine();
                _sawmill.Debug($"AI Fax loaded: {path} ({content.Length} chars)");
                return true;
            }

            _sawmill.Warning($"AI Fax could not find: {path}");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"AI Fax error loading {path}: {ex.Message}");
        }
        return false;
    }

    private int LoadFolder(ResPath folderPath, StringBuilder sb)
    {
        var count = 0;
        try
        {
            var files = _resourceManager.ContentFindFiles(folderPath);
            foreach (var file in files)
            {
                var ext = file.Extension.ToLowerInvariant();
                if (TextExtensions.Contains(ext))
                {
                    if (LoadSingleFile(file, sb))
                        count++;
                }
            }
            _sawmill.Info($"AI Fax loaded {count} files from folder: {folderPath}");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"AI Fax error scanning folder {folderPath}: {ex.Message}");
        }
        return count;
    }

    private async void ProcessAiRequestAsync(
        EntityUid uid,
        AiFaxComponent aiComp,
        string senderAddress,
        string message,
        string systemPrompt)
    {
        try
        {
            // Build full system prompt with context files
            var fullSystemPrompt = systemPrompt;
            if (!string.IsNullOrEmpty(aiComp.LoadedContextContent))
            {
                fullSystemPrompt = $"{systemPrompt}\n\n=== КОНТЕКСТ (информация из файлов сборки) ===\n{aiComp.LoadedContextContent}";
                _sawmill.Debug($"AI Fax system prompt length: {fullSystemPrompt.Length} chars");
            }

            _sawmill.Info($"AI Fax sending request to {aiComp.Provider} model: {aiComp.Model}");

            // Build conversation history for API
            List<(string role, string text)>? history = null;
            if (aiComp.ConversationHistory.Count > 0)
            {
                history = aiComp.ConversationHistory
                    .Select(m => (m.Role, m.Text))
                    .ToList();
                _sawmill.Debug($"AI Fax including {history.Count} history messages");
            }

            var response = await (aiComp.Provider switch
            {
                AiFaxProvider.DeepSeek => _deepSeekApi.GenerateContentAsync(
                    aiComp.ApiKey,
                    aiComp.Model,
                    fullSystemPrompt,
                    message,
                    history,
                    aiComp.RequestTimeoutSeconds),
                AiFaxProvider.Groq => _groqApi.GenerateContentAsync(
                    aiComp.ApiKey,
                    aiComp.Model,
                    fullSystemPrompt,
                    message,
                    history,
                    aiComp.RequestTimeoutSeconds),
                AiFaxProvider.OpenRouter => _openRouterApi.GenerateContentAsync(
                    aiComp.ApiKey,
                    aiComp.Model,
                    fullSystemPrompt,
                    message,
                    history,
                    aiComp.RequestTimeoutSeconds),
                AiFaxProvider.Grok => _grokApi.GenerateContentAsync(
                    aiComp.ApiKey,
                    aiComp.Model,
                    fullSystemPrompt,
                    message,
                    history,
                    aiComp.RequestTimeoutSeconds),
                _ => _geminiApi.GenerateContentAsync(
                    aiComp.ApiKey,
                    aiComp.Model,
                    fullSystemPrompt,
                    message,
                    history,
                    aiComp.RequestTimeoutSeconds),
            });

            if (string.IsNullOrEmpty(response))
            {
                _sawmill.Warning("AI Fax got empty response, not replying");
                return;
            }

            // Truncate response to fit fax limits
            if (response.Length > aiComp.MaxResponseLength)
            {
                response = response[..aiComp.MaxResponseLength] + "...";
            }

            // Store conversation in history
            var finalResponse = response;
            _taskManager.RunOnMainThread(() =>
            {
                // Add user message to history
                aiComp.ConversationHistory.Add(new ConversationMessage("user", message));
                // Add AI response to history
                aiComp.ConversationHistory.Add(new ConversationMessage("model", finalResponse));

                // Trim history if exceeds max length (each exchange = 2 messages)
                var maxMessages = aiComp.MaxHistoryLength * 2;
                while (aiComp.ConversationHistory.Count > maxMessages)
                {
                    aiComp.ConversationHistory.RemoveAt(0);
                }

                _sawmill.Debug($"AI Fax history now has {aiComp.ConversationHistory.Count} messages");
            });

            // Send response back on main thread
            var responseSenderName = aiComp.ResponseSenderName;
            var autoSend = aiComp.AutoSendResponse;
            var copyToSelf = aiComp.CopyResponseToSelf;
            _taskManager.RunOnMainThread(() =>
            {
                // Send to original sender if enabled
                if (autoSend)
                {
                    SendResponseFax(uid, responseSenderName, senderAddress, finalResponse);
                }

                // Print copy on AI fax if enabled
                if (copyToSelf)
                {
                    PrintLocalCopy(uid, aiComp, finalResponse);
                }
            });
        }
        catch (Exception ex)
        {
            _sawmill.Error($"AI Fax processing error: {ex.Message}");
        }
    }

    private void SendResponseFax(EntityUid uid, string senderName, string destinationAddress, string content)
    {
        // Check if entity still exists
        if (!TryComp<FaxMachineComponent>(uid, out _))
            return;

        if (!TryComp<DeviceNetworkComponent>(uid, out _))
            return;

        // Create payload for sending fax
        var payload = new NetworkPayload
        {
            { DeviceNetworkConstants.Command, FaxConstants.FaxPrintCommand },
            { FaxConstants.FaxPaperNameData, senderName },
            { FaxConstants.FaxPaperContentData, content },
            { FaxConstants.FaxPaperLockedData, false }
        };

        // Send fax response
        _deviceNetwork.QueuePacket(uid, destinationAddress, payload);

        _sawmill.Info($"AI Fax sent response to {destinationAddress}");
    }

    private void PrintLocalCopy(EntityUid uid, AiFaxComponent aiComp, string aiResponse)
    {
        if (!TryComp<FaxMachineComponent>(uid, out var faxComp))
            return;

        // Use FaxSystem to print locally via Receive (adds to queue and prints)
        var printout = new FaxPrintout(aiResponse, aiComp.ResponseSenderName);
        _faxSystem.Receive(uid, printout, null, faxComp);

        _sawmill.Debug("AI Fax printed local copy");
    }
}
