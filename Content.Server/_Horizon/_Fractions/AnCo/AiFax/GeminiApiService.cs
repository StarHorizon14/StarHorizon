using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.Server._Horizon._Fractions.AnCo.AiFax;

/// <summary>
/// Service for making HTTP requests to Google Gemini API.
/// </summary>
public sealed class GeminiApiService
{
    [Dependency] private readonly IHttpClientHolder _http = default!;

    private readonly ISawmill _sawmill;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiApiService(ILogManager logManager)
    {
        IoCManager.InjectDependencies(this);
        _sawmill = logManager.GetSawmill("gemini-api");
    }

    /// <summary>
    /// Sends a message to Gemini API and returns the response.
    /// </summary>
    /// <param name="apiKey">Gemini API key</param>
    /// <param name="model">Model name (e.g., gemini-1.5-flash)</param>
    /// <param name="systemPrompt">System instruction for AI behavior</param>
    /// <param name="userMessage">User's message to process</param>
    /// <param name="conversationHistory">Previous conversation messages for context</param>
    /// <param name="timeoutSeconds">Request timeout in seconds</param>
    /// <returns>AI response text or null on error</returns>
    public async Task<string?> GenerateContentAsync(
        string apiKey,
        string model,
        string systemPrompt,
        string userMessage,
        List<(string role, string text)>? conversationHistory,
        int timeoutSeconds)
    {
        var url = $"{BaseUrl}/{model}:generateContent?key={apiKey}";

        // Build contents array with history + new message
        var contentsList = new List<GeminiContent>();

        // Add conversation history
        if (conversationHistory != null)
        {
            foreach (var (role, text) in conversationHistory)
            {
                contentsList.Add(new GeminiContent
                {
                    Role = role,
                    Parts = new[] { new GeminiPart { Text = text } }
                });
            }
        }

        // Add current user message
        contentsList.Add(new GeminiContent
        {
            Role = "user",
            Parts = new[] { new GeminiPart { Text = userMessage } }
        });

        var request = new GeminiRequest
        {
            Contents = contentsList.ToArray(),
            SystemInstruction = new GeminiContent
            {
                Parts = new[] { new GeminiPart { Text = systemPrompt } }
            }
        };

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var response = await _http.Client.PostAsJsonAsync(url, request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cts.Token);
                _sawmill.Error($"Gemini API error {response.StatusCode}: {error}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cts.Token);
            return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        }
        catch (TaskCanceledException)
        {
            _sawmill.Warning("Gemini API request timed out");
            return null;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Gemini API exception: {ex.Message}");
            return null;
        }
    }
}

#region Gemini API JSON Models

public sealed class GeminiRequest
{
    [JsonPropertyName("contents")]
    public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();

    [JsonPropertyName("systemInstruction")]
    public GeminiContent? SystemInstruction { get; set; }
}

public sealed class GeminiContent
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
}

public sealed class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public GeminiCandidate[]? Candidates { get; set; }
}

public sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }
}

#endregion
