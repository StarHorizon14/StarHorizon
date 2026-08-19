using Content.Shared._Horizon._Fractions.AnCo.AiFax;
using Robust.Shared.Utility;

namespace Content.Server._Horizon._Fractions.AnCo.AiFax;

/// <summary>
/// Component for AI-powered fax machine that uses Gemini API to respond to messages.
/// </summary>
[RegisterComponent]
public sealed partial class AiFaxComponent : Component
{
    /// <summary>
    /// Which AI provider to use for generating responses.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public AiFaxProvider Provider { get; set; } = AiFaxProvider.Gemini;

    /// <summary>
    /// API key for authentication with the selected provider.
    /// </summary>
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Keyword that triggers AI response. Message must contain this keyword.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string TriggerKeyword { get; set; } = "";

    /// <summary>
    /// If true, requires trigger keyword in message. If false, responds to all incoming faxes.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool RequireTriggerKeyword { get; set; } = false;

    /// <summary>
    /// System prompt that defines AI behavior and personality.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string SystemPrompt { get; set; } = "Ты помощник на космической станции.";

    /// <summary>
    /// Model to use for the selected provider (e.g. gemini-3.1-flash-lite for Gemini,
    /// deepseek-chat/deepseek-reasoner for DeepSeek).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Model { get; set; } = "gemini-3.1-flash-lite";

    /// <summary>
    /// Maximum response length (will be truncated to fit fax limits).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxResponseLength { get; set; } = 9000;

    /// <summary>
    /// Name shown as sender in response fax.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string ResponseSenderName { get; set; } = "AI Assistant";

    /// <summary>
    /// HTTP request timeout in seconds.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// List of resource files to load and include in AI context.
    /// Paths are relative to Resources folder (e.g., "/Prototypes/Recipes/cooking.yml").
    /// </summary>
    [DataField]
    public List<ResPath> ContextFiles { get; set; } = new();

    /// <summary>
    /// List of folders to scan for text files (.md, .txt, .yml, .yaml, .ftl).
    /// All text files in these folders (including subfolders) will be loaded.
    /// </summary>
    [DataField]
    public List<ResPath> ContextFolders { get; set; } = new();

    /// <summary>
    /// Cached content of loaded context files. Populated on first request.
    /// </summary>
    [ViewVariables]
    public string? LoadedContextContent { get; set; }

    /// <summary>
    /// Maximum number of conversation exchanges to remember.
    /// Each exchange = 1 user message + 1 AI response.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxHistoryLength { get; set; } = 10;

    /// <summary>
    /// If true, automatically sends AI response back to the sender.
    /// If false, only processes the request but doesn't send response (copyResponseToSelf still works).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool AutoSendResponse { get; set; } = true;

    /// <summary>
    /// If true, prints a copy of AI response on the AI fax machine itself.
    /// Useful for monitoring/reviewing AI responses.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool CopyResponseToSelf { get; set; } = false;

    /// <summary>
    /// Conversation history stored in memory. Resets on server restart.
    /// </summary>
    [ViewVariables]
    public List<ConversationMessage> ConversationHistory { get; set; } = new();
}

/// <summary>
/// Represents a single message in conversation history.
/// </summary>
public sealed class ConversationMessage
{
    public string Role { get; set; } = "user";
    public string Text { get; set; } = string.Empty;

    public ConversationMessage(string role, string text)
    {
        Role = role;
        Text = text;
    }
}
