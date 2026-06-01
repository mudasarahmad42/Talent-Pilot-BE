using System.Text.Json;
using Dapper;
using TalentPilot.Application.Abstractions;
using TalentPilot.Infrastructure.Persistence;

namespace TalentPilot.Infrastructure.Ai;

public sealed class DapperVectorStore : IVectorStore
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperVectorStore(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task UpsertAsync(VectorRecord record, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.VectorEmbeddings
            SET IsActive = 0,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND EntityType = @EntityType
              AND EntityId = @EntityId
              AND SourceType = @SourceType
              AND IsActive = 1;

            INSERT INTO dbo.VectorEmbeddings
            (
                VectorEmbeddingId,
                TenantId,
                EntityType,
                EntityId,
                SourceType,
                SourceTextHash,
                EmbeddingModel,
                EmbeddingDimensions,
                Embedding,
                IsActive
            )
            VALUES
            (
                @VectorEmbeddingId,
                @TenantId,
                @EntityType,
                @EntityId,
                @SourceType,
                @SourceTextHash,
                @EmbeddingModel,
                @EmbeddingDimensions,
                CAST(@EmbeddingJson AS VECTOR(768)),
                1
            );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                VectorEmbeddingId = Guid.NewGuid(),
                record.TenantId,
                record.EntityType,
                record.EntityId,
                record.SourceType,
                record.SourceTextHash,
                record.EmbeddingModel,
                record.EmbeddingDimensions,
                EmbeddingJson = JsonSerializer.Serialize(record.Embedding)
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@Top)
                EntityId,
                CAST(1.0 - VECTOR_DISTANCE('cosine', Embedding, CAST(@QueryEmbeddingJson AS VECTOR(768))) AS DECIMAL(18, 6)) AS Score,
                SourceType AS Explanation
            FROM dbo.VectorEmbeddings
            WHERE TenantId = @TenantId
              AND EntityType = @EntityType
              AND IsActive = 1
            ORDER BY VECTOR_DISTANCE('cosine', Embedding, CAST(@QueryEmbeddingJson AS VECTOR(768)));
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<VectorSearchResult>(
            new CommandDefinition(
                sql,
                new
                {
                    request.TenantId,
                    request.EntityType,
                    request.Top,
                    QueryEmbeddingJson = JsonSerializer.Serialize(request.QueryEmbedding)
                },
                cancellationToken: cancellationToken));

        return results.ToArray();
    }

    public async Task<string?> GetActiveSourceTextHashAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        string sourceType,
        string embeddingModel,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) SourceTextHash
            FROM dbo.VectorEmbeddings
            WHERE TenantId = @TenantId
              AND EntityType = @EntityType
              AND EntityId = @EntityId
              AND SourceType = @SourceType
              AND EmbeddingModel = @EmbeddingModel
              AND IsActive = 1
            ORDER BY CreatedAtUtc DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                EntityType = entityType,
                EntityId = entityId,
                SourceType = sourceType,
                EmbeddingModel = embeddingModel
            },
            cancellationToken: cancellationToken));
    }
}
