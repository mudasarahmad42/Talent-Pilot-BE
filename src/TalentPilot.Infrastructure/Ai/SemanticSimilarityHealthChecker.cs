using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Ai;

namespace TalentPilot.Infrastructure.Ai;

public sealed class SemanticSimilarityHealthChecker : ISemanticSimilarityHealthChecker
{
    private const int HealthCheckTimeoutSeconds = 8;

    private readonly IAiRuntimeSettingsResolver _settingsResolver;
    private readonly IEmbeddingProvider _embeddingProvider;

    public SemanticSimilarityHealthChecker(
        IAiRuntimeSettingsResolver settingsResolver,
        IEmbeddingProvider embeddingProvider)
    {
        _settingsResolver = settingsResolver;
        _embeddingProvider = embeddingProvider;
    }

    public async Task<SemanticSimilarityHealth> CheckAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.GetCurrentAsync(cancellationToken);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(HealthCheckTimeoutSeconds));

            var embedding = await _embeddingProvider.GenerateEmbeddingAsync(
                "Talent Pilot semantic similarity health check.",
                timeout.Token);

            if (embedding.Length != settings.EmbeddingDimensions)
            {
                return Unavailable(
                    settings,
                    SemanticSimilarityDiagnostics.Unavailable(
                        $"embedding dimension mismatch for '{settings.EmbeddingModel}'. Expected {settings.EmbeddingDimensions}, received {embedding.Length}."));
            }

            return new SemanticSimilarityHealth(
                true,
                SemanticSimilarityDiagnostics.Available,
                $"Embedding model '{settings.EmbeddingModel}' returned {embedding.Length} dimensions from {settings.OllamaBaseUrl}.",
                settings.Provider,
                settings.EmbeddingModel,
                settings.EmbeddingDimensions,
                settings.VectorStore,
                settings.OllamaBaseUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Unavailable(settings, SemanticSimilarityDiagnostics.Unavailable(ex, settings));
        }
    }

    private static SemanticSimilarityHealth Unavailable(AiRuntimeSettingsSnapshot settings, string status)
    {
        return new SemanticSimilarityHealth(
            false,
            status,
            status,
            settings.Provider,
            settings.EmbeddingModel,
            settings.EmbeddingDimensions,
            settings.VectorStore,
            settings.OllamaBaseUrl);
    }
}
