using System.Net.Http;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Application.Ai;

public static class SemanticSimilarityDiagnostics
{
    public const string Available = "Available";

    public static string Unavailable(Exception exception, AiRuntimeSettingsSnapshot settings)
    {
        var message = Normalize(exception.Message);
        var lower = message.ToLowerInvariant();

        if (exception is HttpRequestException ||
            lower.Contains("actively refused", StringComparison.Ordinal) ||
            lower.Contains("connection refused", StringComparison.Ordinal) ||
            lower.Contains("no connection could be made", StringComparison.Ordinal))
        {
            return $"Unavailable: Ollama embedding service is not reachable at {settings.OllamaBaseUrl}. Start Ollama and pull '{settings.EmbeddingModel}'.";
        }

        if (exception is OperationCanceledException ||
            exception is TaskCanceledException ||
            lower.Contains("timed out", StringComparison.Ordinal) ||
            lower.Contains("timeout", StringComparison.Ordinal))
        {
            return $"Unavailable: Ollama embedding service timed out at {settings.OllamaBaseUrl}.";
        }

        if (lower.Contains("status 404", StringComparison.Ordinal))
        {
            return $"Unavailable: embedding model '{settings.EmbeddingModel}' was not found in Ollama.";
        }

        if (lower.Contains("dimensions", StringComparison.Ordinal))
        {
            return $"Unavailable: embedding dimension mismatch for '{settings.EmbeddingModel}'. Expected {settings.EmbeddingDimensions} dimensions.";
        }

        return $"Unavailable: {message}";
    }

    public static string Unavailable(string reason)
    {
        return $"Unavailable: {Normalize(reason)}";
    }

    public static bool IsUnavailable(string? status)
    {
        return (status ?? string.Empty).Trim().StartsWith("Unavailable", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        var normalized = string.Join(' ', value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim();

        return normalized.Length <= 240 ? normalized : $"{normalized[..240]}...";
    }
}
