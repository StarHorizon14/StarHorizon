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
/// Service for making HTTP requests to DeepSeek API (OpenAI-compatible chat completions).
/// </summary>
public sealed class DeepSeekApiService
{
    [Dependency] private readonly IHttpClientHolder _http = default!;

    private readonly ISawmill _sawmill;
    private const string ChatCompletionsUrl = "https://api.deepseek.com/chat/completions";

    public DeepSeekApiService(ILogManager logManager)
    {
        IoCManager.InjectDependencies(this);
        _sawmill = logManager.GetSawmill("deepseek-api");
    }

    /// <summary>
    /// Sends a message to DeepSeek API and returns the response.
    /// </summary>
    /// <param name="apiKey">DeepSeek API key</param>
    /// <param name="model">Model name (e.g., deepseek-chat, deepseek-reasoner)</param>
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
        var messages = new List<DeepSeekMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        if (conversationHistory != null)
        {
            foreach (var (role, text) in conversationHistory)
            {
                // DeepSeek/OpenAI use "assistant" instead of Gemini's "model" role.
                var mappedRole = role == "model" ? "assistant" : role;
                messages.Add(new DeepSeekMessage { Role = mappedRole, Content = text });
            }
        }

        messages.Add(new DeepSeekMessage { Role = "user", Content = userMessage });

        var request = new DeepSeekRequest
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
                _sawmill.Error($"DeepSeek API error {response.StatusCode}: {error}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<DeepSeekResponse>(cancellationToken: cts.Token);
            return result?.Choices?.FirstOrDefault()?.Message?.Content;
        }
        catch (TaskCanceledException)
        {
            _sawmill.Warning("DeepSeek API request timed out");
            return null;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"DeepSeek API exception: {ex.Message}");
            return null;
        }
    }
}

#region DeepSeek API JSON Models

public sealed class DeepSeekRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public DeepSeekMessage[] Messages { get; set; } = Array.Empty<DeepSeekMessage>();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}

public sealed class DeepSeekMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class DeepSeekResponse
{
    [JsonPropertyName("choices")]
    public DeepSeekChoice[]? Choices { get; set; }
}

public sealed class DeepSeekChoice
{
    [JsonPropertyName("message")]
    public DeepSeekMessage? Message { get; set; }
}

#endregion
