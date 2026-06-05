using TalentPilot.Application.Abstractions;

namespace TalentPilot.Application.AiAssistant;

public sealed class KnowledgeRetrievalService : IKnowledgeRetrievalService
{
    private const string KnowledgeChunkEntityType = "KnowledgeChunk";
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly IKnowledgeRepository _repository;

    public KnowledgeRetrievalService(
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        IKnowledgeRepository repository)
    {
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _repository = repository;
    }

    public async Task<IReadOnlyList<KnowledgeRetrievedChunk>> RetrieveAsync(
        Guid tenantId,
        Guid actorUserId,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        string question,
        CancellationToken cancellationToken)
    {
        var embedding = await _embeddingProvider.GenerateEmbeddingAsync(question, cancellationToken);
        var vectorResults = await _vectorStore.SearchAsync(
            new VectorSearchRequest(tenantId, KnowledgeChunkEntityType, embedding, Top: 24),
            cancellationToken);

        var scores = vectorResults
            .GroupBy(result => result.EntityId)
            .ToDictionary(group => group.Key, group => group.Max(item => item.Score));

        if (scores.Count == 0)
        {
            return Array.Empty<KnowledgeRetrievedChunk>();
        }

        return await _repository.GetChunksByVectorResultsAsync(
            tenantId,
            scores,
            RagAssistantContextTypes.Normalize(contextType),
            contextEntityId,
            focusEntityId,
            cancellationToken);
    }
}
