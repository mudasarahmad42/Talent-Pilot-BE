namespace TalentPilot.Application.Abstractions;

public sealed record AiPromptRequest(string AgentId, string Prompt, IReadOnlyDictionary<string, string> Metadata);

public sealed record VectorRecord(
    Guid TenantId,
    string EntityType,
    Guid EntityId,
    string SourceType,
    string SourceTextHash,
    string EmbeddingModel,
    int EmbeddingDimensions,
    IReadOnlyList<float> Embedding);

public sealed record VectorSearchRequest(
    Guid TenantId,
    string EntityType,
    IReadOnlyList<float> QueryEmbedding,
    int Top);

public sealed record VectorSearchResult(Guid EntityId, decimal Score, string Explanation);

public interface IAiModelProvider
{
    Task<string> GenerateAsync(AiPromptRequest request, CancellationToken cancellationToken);
}

public interface IEmbeddingProvider
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken);
}

public interface IVectorStore
{
    Task UpsertAsync(VectorRecord record, CancellationToken cancellationToken);

    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken);
}

public interface IRequirementParserAgent
{
    Task<string> ParseAsync(string jobRequestText, CancellationToken cancellationToken);
}

public interface IBenchMatchingAgent
{
    Task<string> RankAsync(Guid jobRequestId, CancellationToken cancellationToken);
}

public interface IFitExplanationAgent
{
    Task<string> ExplainAsync(Guid entityId, string entityType, CancellationToken cancellationToken);
}
