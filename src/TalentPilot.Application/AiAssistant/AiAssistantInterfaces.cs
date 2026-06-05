using TalentPilot.Common.Results;

namespace TalentPilot.Application.AiAssistant;

public interface IAiAssistantService
{
    Task<Result<RagChatResponse>> SendMessageAsync(RagChatRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<RagConversation>>> ListConversationsAsync(
        string? contextType,
        Guid? contextEntityId,
        Guid? focusEntityId,
        CancellationToken cancellationToken);

    Task<Result<RagConversation>> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken);

    Task<Result> SubmitFeedbackAsync(
        Guid messageId,
        RagFeedbackRequest request,
        CancellationToken cancellationToken);

    Task<Result<RagRebuildIndexResponse>> RebuildIndexAsync(
        RagRebuildIndexRequest request,
        CancellationToken cancellationToken);
}

public interface IKnowledgeIndexingService
{
    Task<IReadOnlyList<KnowledgeChunkUpsertResult>> EnsureContextIndexedAsync(
        Guid tenantId,
        Guid actorUserId,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        CancellationToken cancellationToken);
}

public interface IKnowledgeRetrievalService
{
    Task<IReadOnlyList<KnowledgeRetrievedChunk>> RetrieveAsync(
        Guid tenantId,
        Guid actorUserId,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        string question,
        CancellationToken cancellationToken);
}

public interface IRagPromptBuilder
{
    RagPrompt Build(RagPromptContext context);
}

public interface IKnowledgeRepository
{
    Task<IReadOnlySet<string>> GetActorRoleCodesAsync(Guid tenantId, Guid actorUserId, CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetActorPermissionIdsAsync(Guid tenantId, Guid actorUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<KnowledgeChunkUpsertResult>> UpsertKnowledgeChunksAsync(
        Guid tenantId,
        IReadOnlyList<KnowledgeChunkDraft> chunks,
        CancellationToken cancellationToken);

    Task MarkStaleChunksInactiveAsync(
        Guid tenantId,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        IReadOnlyList<Guid> activeChunkIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<KnowledgeRetrievedChunk>> GetChunksByVectorResultsAsync(
        Guid tenantId,
        IReadOnlyDictionary<Guid, decimal> vectorScores,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        CancellationToken cancellationToken);

    Task<Guid> CreateConversationAsync(
        Guid tenantId,
        Guid userId,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        string title,
        CancellationToken cancellationToken);

    Task<RagConversation?> GetConversationAsync(
        Guid tenantId,
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RagConversation>> ListConversationsAsync(
        Guid tenantId,
        Guid userId,
        string? contextType,
        Guid? contextEntityId,
        Guid? focusEntityId,
        CancellationToken cancellationToken);

    Task<Guid> AddMessageAsync(
        Guid tenantId,
        Guid conversationId,
        string role,
        string content,
        string? model,
        Guid? agentRunId,
        string? promptVersion,
        string? errorCode,
        string? errorMessage,
        IReadOnlyList<Guid> retrievedChunkIds,
        CancellationToken cancellationToken);

    Task SaveCitationsAsync(
        Guid tenantId,
        Guid messageId,
        IReadOnlyList<RagCitationDraft> citations,
        CancellationToken cancellationToken);

    Task SaveFeedbackAsync(
        Guid tenantId,
        Guid userId,
        Guid messageId,
        string rating,
        string? notes,
        CancellationToken cancellationToken);
}
