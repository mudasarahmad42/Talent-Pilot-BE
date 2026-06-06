namespace TalentPilot.Application.Admin.AiSettings;

public sealed record AdminAiRuntimeResponse(
    string Provider,
    string LlmModel,
    string EmbeddingModel,
    int EmbeddingDimensions,
    string VectorStore,
    bool RuntimeEditable);

public sealed record AdminLlmHealthResponse(
    bool Available,
    string Status,
    string Message,
    string Provider,
    string LlmModel,
    string OllamaBaseUrl);

public sealed record AiHealthStatusResponse(
    bool Available,
    string Status,
    string Message);

public sealed record AdminSemanticSimilarityHealthResponse(
    bool Available,
    string Status,
    string Message,
    string Provider,
    string EmbeddingModel,
    int EmbeddingDimensions,
    string VectorStore,
    string OllamaBaseUrl);

public sealed record AdminAiAgentListResponse(
    int ActiveAgentCount,
    IReadOnlyList<AdminAiAgentDefinition> Items);

public sealed record AdminAiAgentDefinition(
    string Id,
    string DisplayName,
    string Responsibility,
    string InputSummary,
    string OutputSummary,
    string MvpBoundary,
    bool Enabled);

public sealed record AdminAiGuardrailsResponse(
    bool HumanReviewRequired,
    bool AutoRejectEnabled,
    string DecisionBoundary,
    IReadOnlyList<AdminAiGuardrailItem> Items);

public sealed record AdminAiGuardrailItem(
    string Name,
    string Value,
    string Reason);

public sealed record AdminAiGuardrailSettings(
    bool HumanReviewRequired,
    bool AutoRejectEnabled);

public sealed record AdminAiAgentRunListResponse(
    int TotalCount,
    IReadOnlyList<AdminAiAgentRunListItem> Items);

public sealed record AdminAiAgentRunListItem(
    Guid AiAgentRunId,
    string AgentId,
    string AgentName,
    string SourceEntityType,
    Guid SourceEntityId,
    string ModelName,
    string? EmbeddingModelName,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int? DurationMs,
    string? OutputSummary,
    string InputHash,
    string? PromptVersion,
    string? SemanticSimilarityStatus,
    bool HumanDecisionRequired,
    string? FailureType);

public sealed record AdminAiEvaluationResponse(
    string OverallStatus,
    int ScorePercent,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<AdminAiEvaluationItem> Items);

public sealed record AdminAiEvaluationItem(
    string Name,
    string Status,
    string RubricArea,
    string Evidence,
    string NextStep);
