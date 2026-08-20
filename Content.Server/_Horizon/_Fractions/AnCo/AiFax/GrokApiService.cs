using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.Server._Horizon._Fractions.AnCo.AiFax;

/// <summary>
/// Service for making HTTP requests to xAI Grok API (OpenAI-compatible chat completions).
/// </summary>
public sealed class GrokApiService
{
    [Dependency] private readonly IHttpClientHolder _http = default!;

    private readonly ISawmill _sawmill;
    private const string ChatCompletionsUrl = "https://api.x.ai/v1/chat/completions";

    public GrokApiService(ILogManager logManager)
    {
        IoCManager.InjectDependencies(this);
        _sawmill = logManager.GetSawmill("grok-api");
    }

    /// <summary>
    /// Sends a message to Grok API and returns the response.
    /// </summary>
    /// <param name="apiKey">xAI API key</param>
    /// <param name="model">Model name (e.g., grok-4, grok-3-mini)</param>
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
        var messages = new List<GrokMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        if (conversationHistory != null)
        {
            foreach (var (role, text) in conversationHistory)
            {
                // Grok/OpenAI use "assistant" instead of Gemini's "model" role.
                var mappedRole = role == "model" ? "assistant" : role;
                messages.Add(new GrokMessage { Role = mappedRole, Content = text });
            }
        }

        messages.Add(new GrokMessage { Role = "user", Content = userMessage });

        var request = new GrokRequest
        {
            Model = model,
            Messages = messages.ToArray(),
        };

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl)
            {
                Content = JsonContent.Create(request),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _http.Client.SendAsync(httpRequest, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cts.Token);
                _sawmill.Error($"Grok API error {response.StatusCode}: {error}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<GrokResponse>(cancellationToken: cts.Token);
            return result?.Choices?.FirstOrDefault()?.Message?.Content;
        }
        catch (TaskCanceledException)
        {
            _sawmill.Warning("Grok API request timed out");
            return null;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Grok API exception: {ex.Message}");
            return null;
        }
    }
}

#region Grok API JSON Models

public sealed class GrokRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public GrokMessage[] Messages { get; set; } = Array.Empty<GrokMessage>();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}

public sealed class GrokMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class GrokResponse
{
    [JsonPropertyName("choices")]
    public GrokChoice[]? Choices { get; set; }
}

public sealed class GrokChoice
{
    [JsonPropertyName("message")]
    public GrokMessage? Message { get; set; }
}

#endregion
