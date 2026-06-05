namespace TalentPilot.Application.AiAssistant;

public static class RagAssistantContextTypes
{
    public const string RecruiterCandidateFit = "RecruiterCandidateFit";
    public const string PmoRequest = "PmoRequest";
    public const string HiringDecisionBrief = "HiringDecisionBrief";

    public static bool IsKnown(string contextType)
    {
        return string.Equals(contextType, RecruiterCandidateFit, StringComparison.OrdinalIgnoreCase)
            || string.Equals(contextType, PmoRequest, StringComparison.OrdinalIgnoreCase)
            || string.Equals(contextType, HiringDecisionBrief, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string contextType)
    {
        if (string.Equals(contextType, RecruiterCandidateFit, StringComparison.OrdinalIgnoreCase))
        {
            return RecruiterCandidateFit;
        }

        if (string.Equals(contextType, PmoRequest, StringComparison.OrdinalIgnoreCase))
        {
            return PmoRequest;
        }

        if (string.Equals(contextType, HiringDecisionBrief, StringComparison.OrdinalIgnoreCase))
        {
            return HiringDecisionBrief;
        }

        return contextType.Trim();
    }
}

public sealed record RagChatRequest(
    string ContextType,
    Guid ContextEntityId,
    Guid? FocusEntityId,
    Guid? ConversationId,
    string Message);

public sealed record RagChatResponse(
    Guid ConversationId,
    Guid UserMessageId,
    Guid AssistantMessageId,
    string Answer,
    IReadOnlyList<RagCitation> Citations,
    string Model,
    Guid AgentRunId,
    string PromptVersion,
    DateTimeOffset GeneratedAtUtc);

public sealed record RagCitation(
    Guid CitationId,
    Guid KnowledgeChunkId,
    string Label,
    string SourceTitle,
    string SourceType,
    Guid SourceEntityId,
    string? SourceRoute,
    decimal Score,
    string Excerpt);

public sealed record RagConversation(
    Guid ConversationId,
    string ContextType,
    Guid ContextEntityId,
    Guid? FocusEntityId,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<RagMessage> Messages);

public sealed record RagMessage(
    Guid MessageId,
    string Role,
    string Content,
    string? Model,
    Guid? AgentRunId,
    string? PromptVersion,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<RagCitation> Citations);

public sealed record RagFeedbackRequest(string Rating, string? Notes);

public sealed record RagRebuildIndexRequest(
    string? ContextType,
    Guid? ContextEntityId,
    Guid? FocusEntityId);

public sealed record RagRebuildIndexResponse(
    int ContextsIndexed,
    int ChunksUpserted,
    DateTimeOffset RebuiltAtUtc);

public sealed record KnowledgeChunkDraft(
    string ContextType,
    Guid ContextEntityId,
    Guid? FocusEntityId,
    string SourceEntityType,
    Guid SourceEntityId,
    string SourceTitle,
    string? SourceRoute,
    string PermissionScope,
    string Sensitivity,
    string ChunkType,
    int ChunkOrdinal,
    string Text,
    string MetadataJson,
    string ContentHash);

public sealed record KnowledgeChunkUpsertResult(
    Guid KnowledgeChunkId,
    string SourceTextHash,
    string ChunkType,
    bool RequiresEmbedding);

public sealed record KnowledgeRetrievedChunk(
    Guid KnowledgeChunkId,
    string ContextType,
    Guid ContextEntityId,
    Guid? FocusEntityId,
    string SourceEntityType,
    Guid SourceEntityId,
    string SourceTitle,
    string? SourceRoute,
    string PermissionScope,
    string Sensitivity,
    string ChunkType,
    int ChunkOrdinal,
    string Text,
    string ContentHash,
    decimal Score);

public sealed record RagPromptContext(
    string ContextType,
    string UserQuestion,
    IReadOnlyList<RagMessage> ConversationHistory,
    IReadOnlyList<KnowledgeRetrievedChunk> Evidence);

public sealed record RagPrompt(
    string PromptVersion,
    string Prompt,
    IReadOnlyList<RagCitationDraft> Citations);

public sealed record RagCitationDraft(
    Guid KnowledgeChunkId,
    string Label,
    string SourceTitle,
    string SourceType,
    Guid SourceEntityId,
    string? SourceRoute,
    decimal Score,
    string Excerpt);
