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
        var expectsJson = request.Metadata.TryGetValue("output", out var output) &&
            string.Equals(output, "json", StringComparison.OrdinalIgnoreCase);

        using var response = await _httpClient.PostAsJsonAsync(
            endpoint,
            new OllamaGenerateRequest(
                model,
                request.Prompt,
                false,
                expectsJson ? "json" : null,
                expectsJson ? new OllamaGenerateOptions(0.1m, 5000) : null),
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
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("format")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Format,
        [property: JsonPropertyName("options")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        OllamaGenerateOptions? Options);

    private sealed record OllamaGenerateOptions(
        [property: JsonPropertyName("temperature")] decimal Temperature,
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private sealed record OllamaGenerateResponse(
        [property: JsonPropertyName("response")] string? Response,
        [property: JsonPropertyName("error")] string? Error);
}
