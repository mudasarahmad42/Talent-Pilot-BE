namespace TalentPilot.Application.Admin.AiSettings;

public sealed record AdminAiRuntimeResponse(
    string Provider,
    string LlmModel,
    string EmbeddingModel,
    int EmbeddingDimensions,
    string VectorStore,
    bool RuntimeEditable);

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
    string DecisionBoundary);
