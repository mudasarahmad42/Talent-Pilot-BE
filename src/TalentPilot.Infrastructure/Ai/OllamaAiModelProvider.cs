using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Infrastructure.Ai;

public sealed class OllamaAiModelProvider : IAiModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly IAiRuntimeSettingsResolver _settingsResolver;

    public OllamaAiModelProvider(HttpClient httpClient, IAiRuntimeSettingsResolver settingsResolver)
    {
        _httpClient = httpClient;
        _settingsResolver = settingsResolver;
    }

    public async Task<string> GenerateAsync(AiPromptRequest request, CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.GetCurrentAsync(cancellationToken);
        var model = request.Metadata.TryGetValue("model", out var requestedModel) && !string.IsNullOrWhiteSpace(requestedModel)
            ? requestedModel
            : settings.LlmModel;

        var endpoint = BuildEndpoint(settings.OllamaBaseUrl, "api/generate");
        using var response = await _httpClient.PostAsJsonAsync(
            endpoint,
            new OllamaGenerateRequest(model, request.Prompt, false),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Ollama generation failed with status {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
        if (payload is null || !string.IsNullOrWhiteSpace(payload.Error))
        {
            throw new InvalidOperationException(payload?.Error ?? "Ollama generation returned an empty response.");
        }

        return payload.Response?.Trim() ?? string.Empty;
    }

    private static Uri BuildEndpoint(string baseUrl, string path)
    {
        var normalized = baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
        return new Uri(new Uri(normalized), path);
    }

    private sealed record OllamaGenerateRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record OllamaGenerateResponse(
        [property: JsonPropertyName("response")] string? Response,
        [property: JsonPropertyName("error")] string? Error);
}
