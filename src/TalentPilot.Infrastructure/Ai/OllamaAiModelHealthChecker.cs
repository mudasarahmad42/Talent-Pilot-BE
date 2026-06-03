using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Infrastructure.Ai;

public sealed class OllamaAiModelHealthChecker : IAiModelHealthChecker
{
    private const int HealthCheckTimeoutSeconds = 8;

    private readonly HttpClient _httpClient;
    private readonly IAiRuntimeSettingsResolver _settingsResolver;

    public OllamaAiModelHealthChecker(HttpClient httpClient, IAiRuntimeSettingsResolver settingsResolver)
    {
        _httpClient = httpClient;
        _settingsResolver = settingsResolver;
    }

    public async Task<AiModelHealth> CheckAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.GetCurrentAsync(cancellationToken);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(HealthCheckTimeoutSeconds));

            var endpoint = BuildEndpoint(settings.OllamaBaseUrl, "api/tags");
            var payload = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>(endpoint, timeout.Token);
            var models = payload?.Models ?? [];

            if (!models.Any(model => IsModelMatch(model.Name, settings.LlmModel)))
            {
                return Unavailable(
                    settings,
                    $"Unavailable: LLM model '{settings.LlmModel}' was not found in Ollama.");
            }

            return new AiModelHealth(
                true,
                "Available",
                $"LLM model '{settings.LlmModel}' is available at {settings.OllamaBaseUrl}.",
                settings.Provider,
                settings.LlmModel,
                settings.OllamaBaseUrl);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(
                settings,
                $"Unavailable: Ollama LLM service timed out at {settings.OllamaBaseUrl}.");
        }
        catch (HttpRequestException)
        {
            return Unavailable(
                settings,
                $"Unavailable: Ollama LLM service is not reachable at {settings.OllamaBaseUrl}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Unavailable(settings, $"Unavailable: {ex.Message}");
        }
    }

    private static AiModelHealth Unavailable(AiRuntimeSettingsSnapshot settings, string status)
    {
        return new AiModelHealth(
            false,
            status,
            status,
            settings.Provider,
            settings.LlmModel,
            settings.OllamaBaseUrl);
    }

    private static Uri BuildEndpoint(string baseUrl, string path)
    {
        var normalized = baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
        return new Uri(new Uri(normalized), path);
    }

    private static bool IsModelMatch(string? candidateName, string configuredModel)
    {
        if (string.IsNullOrWhiteSpace(candidateName) || string.IsNullOrWhiteSpace(configuredModel))
        {
            return false;
        }

        var candidate = candidateName.Trim();
        var requested = configuredModel.Trim();
        if (string.Equals(candidate, requested, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (requested.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(candidate, $"{requested}:latest", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith($"{requested}:", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record OllamaTagsResponse(
        [property: JsonPropertyName("models")] IReadOnlyList<OllamaModelTag>? Models);

    private sealed record OllamaModelTag(
        [property: JsonPropertyName("name")] string? Name);
}
