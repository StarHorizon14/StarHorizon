using Robust.Shared.Serialization;

namespace Content.Shared._Horizon._Fractions.AnCo.AiFax;

/// <summary>
/// AI provider used by an AI fax machine to generate responses.
/// </summary>
[Serializable, NetSerializable]
public enum AiFaxProvider
{
    Gemini,
    DeepSeek,
    Groq,
    OpenRouter,
    Grok,
}
