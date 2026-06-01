using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Infrastructure.Ai;

public sealed class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly IAiRuntimeSettingsResolver _settingsResolver;

    public OllamaEmbeddingProvider(HttpClient httpClient, IAiRuntimeSettingsResolver settingsResolver)
    {
        _httpClient = httpClient;
        _settingsResolver = settingsResolver;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.GetCurrentAsync(cancellationToken);
        var endpoint = BuildEndpoint(settings.OllamaBaseUrl, "api/embeddings");
        using var response = await _httpClient.PostAsJsonAsync(
            endpoint,
            new OllamaEmbeddingRequest(settings.EmbeddingModel, text),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Ollama embedding failed with status {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);
        if (payload?.Embedding is null || payload.Embedding.Length == 0)
        {
            throw new InvalidOperationException("Ollama embedding returned an empty vector.");
        }

        if (payload.Embedding.Length != settings.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Ollama embedding returned {payload.Embedding.Length} dimensions; Talent Pilot expects {settings.EmbeddingDimensions}.");
        }

        return payload.Embedding;
    }

    private static Uri BuildEndpoint(string baseUrl, string path)
    {
        var normalized = baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
        return new Uri(new Uri(normalized), path);
    }

    private sealed record OllamaEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt);

    private sealed record OllamaEmbeddingResponse(
        [property: JsonPropertyName("embedding")] float[]? Embedding);
}
