namespace TalentPilot.Application.Abstractions;

public sealed record AiPromptRequest(string AgentId, string Prompt, IReadOnlyDictionary<string, string> Metadata);

public sealed record AiRuntimeSettingsSnapshot(
    Guid TenantId,
    string Provider,
    string LlmModel,
    string EmbeddingModel,
    int EmbeddingDimensions,
    string VectorStore,
    string OllamaBaseUrl);

public sealed record SemanticSimilarityHealth(
    bool IsAvailable,
    string Status,
    string Message,
    string Provider,
    string EmbeddingModel,
    int EmbeddingDimensions,
    string VectorStore,
    string OllamaBaseUrl);

public sealed record AiModelHealth(
    bool IsAvailable,
    string Status,
    string Message,
    string Provider,
    string LlmModel,
    string OllamaBaseUrl);

public sealed record AiAgentRunStart(
    Guid TenantId,
    string AgentId,
    string SourceEntityType,
    Guid SourceEntityId,
    string ModelName,
    string? EmbeddingModelName,
    string InputHash,
    IReadOnlyDictionary<string, string> Metadata);

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

public sealed record WebResearchRequest(
    Guid TenantId,
    string AgentId,
    IReadOnlyList<string> Queries,
    int MaxResultsPerQuery);

public sealed record WebResearchResult(
    string Status,
    IReadOnlyList<WebResearchSource> Sources);

public sealed record WebResearchSource(
    string Query,
    string Title,
    string Url,
    string Snippet);

public interface IWebResearchQuotaStore
{
    Task<bool> TryReserveAsync(
        string provider,
        DateOnly usageDateUtc,
        int dailyLimit,
        CancellationToken cancellationToken);
}

public interface IAiModelProvider
{
    Task<string> GenerateAsync(AiPromptRequest request, CancellationToken cancellationToken);
}

public interface IAiModelHealthChecker
{
    Task<AiModelHealth> CheckAsync(CancellationToken cancellationToken);
}

public interface IEmbeddingProvider
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken);
}

public interface ISemanticSimilarityHealthChecker
{
    Task<SemanticSimilarityHealth> CheckAsync(CancellationToken cancellationToken);
}

public interface IVectorStore
{
    Task UpsertAsync(VectorRecord record, CancellationToken cancellationToken);

    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken);

    Task<string?> GetActiveSourceTextHashAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        string sourceType,
        string embeddingModel,
        CancellationToken cancellationToken);
}

public interface IWebResearchProvider
{
    Task<WebResearchResult> ResearchAsync(WebResearchRequest request, CancellationToken cancellationToken);
}

public sealed record GitHubCandidateSearchRequest(
    Guid TenantId,
    string AgentId,
    string JobTitle,
    IReadOnlyList<string> Skills,
    string? Location,
    int Limit);

public sealed record GitHubCandidateSearchResult(
    string Status,
    IReadOnlyList<GitHubCandidateProfile> Profiles);

public sealed record GitHubCandidateProfile(
    string Login,
    string? DisplayName,
    string HtmlUrl,
    string? Location,
    string? Bio,
    string? Company,
    int PublicRepositoryCount,
    string? Email = null);

public interface IGitHubCandidateSearchProvider
{
    Task<GitHubCandidateSearchResult> SearchAsync(
        GitHubCandidateSearchRequest request,
        CancellationToken cancellationToken);
}

public interface IAiRuntimeSettingsResolver
{
    Task<AiRuntimeSettingsSnapshot> GetCurrentAsync(CancellationToken cancellationToken);
}

public interface IAiAgentRunLogger
{
    Task<Guid> StartAsync(AiAgentRunStart run, CancellationToken cancellationToken);

    Task SucceedAsync(
        Guid tenantId,
        Guid runId,
        string outputSummary,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken);

    Task FailAsync(
        Guid tenantId,
        Guid runId,
        string outputSummary,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken);
}

public sealed record JobDescriptionDraftRequest(
    string Title,
    string Client,
    string Department,
    string Location,
    IReadOnlyList<string> Skills,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    int RequiredPositions,
    string Priority,
    string HiringManager);

public sealed record JobDescriptionDraftResult(
    string Description,
    Guid AgentRunId,
    string Model,
    DateTimeOffset GeneratedAtUtc);

public interface IJobDescriptionDraftingAgent
{
    Task<JobDescriptionDraftResult> DraftAsync(
        Guid tenantId,
        JobDescriptionDraftRequest request,
        CancellationToken cancellationToken);
}

public sealed record CvParseRequest(
    string FileName,
    byte[] Content);

public sealed record CvParseResult(
    Guid AgentRunId,
    string Model,
    DateTimeOffset GeneratedAtUtc,
    string ExtractedText,
    string? DisplayName,
    string? Email,
    string? Phone,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    IReadOnlyList<string> Skills,
    string? UniversityName,
    string? DegreeName,
    int? GraduationYear,
    string Summary);

public interface ICvParserAgent
{
    Task<CvParseResult> ParseAsync(
        Guid tenantId,
        CvParseRequest request,
        CancellationToken cancellationToken);
}

public interface IRequirementParserAgent
{
    Task<string> ParseAsync(string jobRequestText, CancellationToken cancellationToken);
}

public interface IFitExplanationAgent
{
    Task<string> ExplainAsync(Guid entityId, string entityType, CancellationToken cancellationToken);
}
