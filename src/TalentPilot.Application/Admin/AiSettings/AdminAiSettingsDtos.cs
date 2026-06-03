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
