using System.Text.Json;
using Dapper;
using TalentPilot.Application.AiAssistant;
using TalentPilot.Domain.Access;

namespace TalentPilot.Infrastructure.Persistence.Repositories;

public sealed class DapperKnowledgeRepository : IKnowledgeRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperKnowledgeRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlySet<string>> GetActorRoleCodesAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT r.Code
            FROM dbo.UserRoles AS ur
            INNER JOIN dbo.Roles AS r
                ON r.TenantId = ur.TenantId
                AND r.RoleId = ur.RoleId
                AND r.Status = N'Active'
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = ur.TenantId
                AND u.UserId = ur.UserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            WHERE ur.TenantId = @TenantId
              AND ur.UserId = @ActorUserId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<string>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActorUserId = actorUserId },
            cancellationToken: cancellationToken));
        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlySet<string>> GetActorPermissionIdsAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT p.PermissionId
            FROM dbo.UserRoles AS ur
            INNER JOIN dbo.Roles AS r
                ON r.TenantId = ur.TenantId
                AND r.RoleId = ur.RoleId
                AND r.Status = N'Active'
            INNER JOIN dbo.RolePermissions AS rp
                ON rp.RoleId = r.RoleId
            INNER JOIN dbo.Permissions AS p
                ON p.PermissionId = rp.PermissionId
                AND p.Status = N'Active'
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = ur.TenantId
                AND u.UserId = ur.UserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            WHERE ur.TenantId = @TenantId
              AND ur.UserId = @ActorUserId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<string>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActorUserId = actorUserId },
            cancellationToken: cancellationToken));
        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<KnowledgeChunkUpsertResult>> UpsertKnowledgeChunksAsync(
        Guid tenantId,
        IReadOnlyList<KnowledgeChunkDraft> chunks,
        CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
        {
            return Array.Empty<KnowledgeChunkUpsertResult>();
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var results = new List<KnowledgeChunkUpsertResult>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var existing = await connection.QuerySingleOrDefaultAsync<KnowledgeChunkExistingRow>(new CommandDefinition(
                """
                SELECT TOP (1)
                    KnowledgeChunkId,
                    ContentHashSha256,
                    IsActive
                FROM dbo.KnowledgeChunks
                WHERE TenantId = @TenantId
                  AND ContextType = @ContextType
                  AND ContextEntityId = @ContextEntityId
                  AND ((@FocusEntityId IS NULL AND FocusEntityId IS NULL) OR FocusEntityId = @FocusEntityId)
                  AND SourceEntityType = @SourceEntityType
                  AND SourceEntityId = @SourceEntityId
                  AND ChunkType = @ChunkType
                  AND ChunkOrdinal = @ChunkOrdinal;
                """,
                new
                {
                    TenantId = tenantId,
                    chunk.ContextType,
                    chunk.ContextEntityId,
                    chunk.FocusEntityId,
                    chunk.SourceEntityType,
                    chunk.SourceEntityId,
                    chunk.ChunkType,
                    chunk.ChunkOrdinal
                },
                transaction,
                cancellationToken: cancellationToken));

            if (existing is null)
            {
                var chunkId = Guid.NewGuid();
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO dbo.KnowledgeChunks
                    (
                        KnowledgeChunkId,
                        TenantId,
                        ContextType,
                        ContextEntityId,
                        FocusEntityId,
                        SourceEntityType,
                        SourceEntityId,
                        SourceTitle,
                        SourceRoute,
                        PermissionScope,
                        Sensitivity,
                        ChunkType,
                        ChunkOrdinal,
                        ChunkText,
                        MetadataJson,
                        ContentHashSha256,
                        IsActive
                    )
                    VALUES
                    (
                        @KnowledgeChunkId,
                        @TenantId,
                        @ContextType,
                        @ContextEntityId,
                        @FocusEntityId,
                        @SourceEntityType,
                        @SourceEntityId,
                        @SourceTitle,
                        @SourceRoute,
                        @PermissionScope,
                        @Sensitivity,
                        @ChunkType,
                        @ChunkOrdinal,
                        @ChunkText,
                        @MetadataJson,
                        @ContentHashSha256,
                        1
                    );
                    """,
                    ToParameters(tenantId, chunk, chunkId),
                    transaction,
                    cancellationToken: cancellationToken));
                results.Add(new KnowledgeChunkUpsertResult(chunkId, chunk.ContentHash, chunk.ChunkType, RequiresEmbedding: true));
                continue;
            }

            var requiresEmbedding = !existing.IsActive
                || !string.Equals(existing.ContentHashSha256, chunk.ContentHash, StringComparison.OrdinalIgnoreCase);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.KnowledgeChunks
                SET SourceTitle = @SourceTitle,
                    SourceRoute = @SourceRoute,
                    PermissionScope = @PermissionScope,
                    Sensitivity = @Sensitivity,
                    ChunkText = @ChunkText,
                    MetadataJson = @MetadataJson,
                    ContentHashSha256 = @ContentHashSha256,
                    IsActive = 1,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE TenantId = @TenantId
                  AND KnowledgeChunkId = @KnowledgeChunkId;
                """,
                ToParameters(tenantId, chunk, existing.KnowledgeChunkId),
                transaction,
                cancellationToken: cancellationToken));

            results.Add(new KnowledgeChunkUpsertResult(
                existing.KnowledgeChunkId,
                chunk.ContentHash,
                chunk.ChunkType,
                requiresEmbedding));
        }

        await transaction.CommitAsync(cancellationToken);
        return results;
    }

    public async Task MarkStaleChunksInactiveAsync(
        Guid tenantId,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        IReadOnlyList<Guid> activeChunkIds,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var staleChunkIds = (await connection.QueryAsync<Guid>(new CommandDefinition(
            """
            SELECT KnowledgeChunkId
            FROM dbo.KnowledgeChunks
            WHERE TenantId = @TenantId
              AND ContextType = @ContextType
              AND ContextEntityId = @ContextEntityId
              AND ((@FocusEntityId IS NULL AND FocusEntityId IS NULL) OR FocusEntityId = @FocusEntityId)
              AND IsActive = 1
              AND KnowledgeChunkId NOT IN @ActiveChunkIds;
            """,
            new
            {
                TenantId = tenantId,
                ContextType = contextType,
                ContextEntityId = contextEntityId,
                FocusEntityId = focusEntityId,
                ActiveChunkIds = activeChunkIds
            },
            transaction,
            cancellationToken: cancellationToken))).ToArray();

        if (staleChunkIds.Length > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.KnowledgeChunks
                SET IsActive = 0,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE TenantId = @TenantId
                  AND KnowledgeChunkId IN @StaleChunkIds;

                UPDATE dbo.VectorEmbeddings
                SET IsActive = 0,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE TenantId = @TenantId
                  AND EntityType = N'KnowledgeChunk'
                  AND EntityId IN @StaleChunkIds
                  AND IsActive = 1;
                """,
                new { TenantId = tenantId, StaleChunkIds = staleChunkIds },
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeRetrievedChunk>> GetChunksByVectorResultsAsync(
        Guid tenantId,
        IReadOnlyDictionary<Guid, decimal> vectorScores,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        CancellationToken cancellationToken)
    {
        if (vectorScores.Count == 0)
        {
            return Array.Empty<KnowledgeRetrievedChunk>();
        }

        const string sql = """
            SELECT
                KnowledgeChunkId,
                ContextType,
                ContextEntityId,
                FocusEntityId,
                SourceEntityType,
                SourceEntityId,
                SourceTitle,
                SourceRoute,
                PermissionScope,
                Sensitivity,
                ChunkType,
                ChunkOrdinal,
                ChunkText AS [Text],
                ContentHashSha256 AS ContentHash
            FROM dbo.KnowledgeChunks
            WHERE TenantId = @TenantId
              AND KnowledgeChunkId IN @ChunkIds
              AND ContextType = @ContextType
              AND ContextEntityId = @ContextEntityId
              AND (@FocusEntityId IS NULL OR FocusEntityId = @FocusEntityId OR FocusEntityId IS NULL)
              AND IsActive = 1;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<KnowledgeChunkRow>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                ChunkIds = vectorScores.Keys.ToArray(),
                ContextType = contextType,
                ContextEntityId = contextEntityId,
                FocusEntityId = focusEntityId
            },
            cancellationToken: cancellationToken));

        return rows
            .Select(row => row.ToRetrieved(vectorScores[row.KnowledgeChunkId]))
            .OrderByDescending(chunk => chunk.Score)
            .ThenBy(chunk => chunk.ChunkOrdinal)
            .Take(10)
            .ToArray();
    }

    public async Task<Guid> CreateConversationAsync(
        Guid tenantId,
        Guid userId,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        string title,
        CancellationToken cancellationToken)
    {
        var conversationId = Guid.NewGuid();
        const string sql = """
            INSERT INTO dbo.AiAssistantConversations
            (
                ConversationId,
                TenantId,
                UserId,
                ContextType,
                ContextEntityId,
                FocusEntityId,
                Title,
                Status
            )
            VALUES
            (
                @ConversationId,
                @TenantId,
                @UserId,
                @ContextType,
                @ContextEntityId,
                @FocusEntityId,
                @Title,
                N'Active'
            );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                ConversationId = conversationId,
                TenantId = tenantId,
                UserId = userId,
                ContextType = contextType,
                ContextEntityId = contextEntityId,
                FocusEntityId = focusEntityId,
                Title = title
            },
            cancellationToken: cancellationToken));
        return conversationId;
    }

    public async Task<RagConversation?> GetConversationAsync(
        Guid tenantId,
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                ConversationId,
                ContextType,
                ContextEntityId,
                FocusEntityId,
                Title,
                CreatedAtUtc,
                UpdatedAtUtc
            FROM dbo.AiAssistantConversations
            WHERE TenantId = @TenantId
              AND UserId = @UserId
              AND ConversationId = @ConversationId
              AND Status = N'Active';

            SELECT
                MessageId,
                Role,
                Content,
                ModelName AS Model,
                AiAgentRunId AS AgentRunId,
                PromptVersion,
                ErrorCode,
                ErrorMessage,
                CreatedAtUtc
            FROM dbo.AiAssistantMessages
            WHERE TenantId = @TenantId
              AND ConversationId = @ConversationId
            ORDER BY CreatedAtUtc ASC;

            SELECT
                c.CitationId,
                c.MessageId,
                c.KnowledgeChunkId,
                c.Label,
                c.SourceTitle,
                c.SourceType,
                c.SourceEntityId,
                c.SourceRoute,
                c.Score,
                c.Excerpt
            FROM dbo.AiAssistantMessageCitations AS c
            INNER JOIN dbo.AiAssistantMessages AS m
                ON m.TenantId = c.TenantId
                AND m.MessageId = c.MessageId
            WHERE c.TenantId = @TenantId
              AND m.ConversationId = @ConversationId
            ORDER BY c.Label;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId, ConversationId = conversationId },
            cancellationToken: cancellationToken));

        var row = await grid.ReadSingleOrDefaultAsync<ConversationRow>();
        if (row is null)
        {
            return null;
        }

        var messages = (await grid.ReadAsync<MessageRow>()).ToArray();
        var citations = (await grid.ReadAsync<CitationRow>()).ToArray();
        return row.ToConversation(BuildMessages(messages, citations));
    }

    public async Task<IReadOnlyList<RagConversation>> ListConversationsAsync(
        Guid tenantId,
        Guid userId,
        string? contextType,
        Guid? contextEntityId,
        Guid? focusEntityId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (25)
                ConversationId,
                ContextType,
                ContextEntityId,
                FocusEntityId,
                Title,
                CreatedAtUtc,
                UpdatedAtUtc
            FROM dbo.AiAssistantConversations
            WHERE TenantId = @TenantId
              AND UserId = @UserId
              AND Status = N'Active'
              AND (@ContextType IS NULL OR ContextType = @ContextType)
              AND (@ContextEntityId IS NULL OR ContextEntityId = @ContextEntityId)
              AND (@FocusEntityId IS NULL OR FocusEntityId = @FocusEntityId OR FocusEntityId IS NULL)
            ORDER BY UpdatedAtUtc DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<ConversationRow>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                UserId = userId,
                ContextType = contextType,
                ContextEntityId = contextEntityId,
                FocusEntityId = focusEntityId
            },
            cancellationToken: cancellationToken))).ToArray();

        var conversations = new List<RagConversation>(rows.Length);
        foreach (var row in rows)
        {
            var conversation = await GetConversationAsync(tenantId, userId, row.ConversationId, cancellationToken);
            if (conversation is not null)
            {
                conversations.Add(conversation);
            }
        }

        return conversations;
    }

    public async Task<Guid> AddMessageAsync(
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
        CancellationToken cancellationToken)
    {
        var messageId = Guid.NewGuid();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.AiAssistantMessages
            (
                MessageId,
                TenantId,
                ConversationId,
                Role,
                Content,
                ModelName,
                AiAgentRunId,
                PromptVersion,
                ErrorCode,
                ErrorMessage,
                RetrievedChunkIdsJson
            )
            VALUES
            (
                @MessageId,
                @TenantId,
                @ConversationId,
                @Role,
                @Content,
                @Model,
                @AgentRunId,
                @PromptVersion,
                @ErrorCode,
                @ErrorMessage,
                @RetrievedChunkIdsJson
            );

            UPDATE dbo.AiAssistantConversations
            SET UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND ConversationId = @ConversationId;
            """,
            new
            {
                MessageId = messageId,
                TenantId = tenantId,
                ConversationId = conversationId,
                Role = role,
                Content = content,
                Model = model,
                AgentRunId = agentRunId,
                PromptVersion = promptVersion,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                RetrievedChunkIdsJson = JsonSerializer.Serialize(retrievedChunkIds)
            },
            cancellationToken: cancellationToken));
        return messageId;
    }

    public async Task SaveCitationsAsync(
        Guid tenantId,
        Guid messageId,
        IReadOnlyList<RagCitationDraft> citations,
        CancellationToken cancellationToken)
    {
        if (citations.Count == 0)
        {
            return;
        }

        const string sql = """
            INSERT INTO dbo.AiAssistantMessageCitations
            (
                CitationId,
                TenantId,
                MessageId,
                KnowledgeChunkId,
                Label,
                SourceTitle,
                SourceType,
                SourceEntityId,
                SourceRoute,
                Score,
                Excerpt
            )
            VALUES
            (
                @CitationId,
                @TenantId,
                @MessageId,
                @KnowledgeChunkId,
                @Label,
                @SourceTitle,
                @SourceType,
                @SourceEntityId,
                @SourceRoute,
                @Score,
                @Excerpt
            );
            """;

        var rows = citations.Select(citation => new
        {
            CitationId = Guid.NewGuid(),
            TenantId = tenantId,
            MessageId = messageId,
            citation.KnowledgeChunkId,
            citation.Label,
            citation.SourceTitle,
            citation.SourceType,
            citation.SourceEntityId,
            citation.SourceRoute,
            citation.Score,
            citation.Excerpt
        });

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, rows, cancellationToken: cancellationToken));
    }

    public async Task SaveFeedbackAsync(
        Guid tenantId,
        Guid userId,
        Guid messageId,
        string rating,
        string? notes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            IF EXISTS
            (
                SELECT 1
                FROM dbo.AiAssistantMessages AS m
                INNER JOIN dbo.AiAssistantConversations AS c
                    ON c.TenantId = m.TenantId
                    AND c.ConversationId = m.ConversationId
                WHERE m.TenantId = @TenantId
                  AND m.MessageId = @MessageId
                  AND c.UserId = @UserId
            )
            BEGIN
                MERGE dbo.AiAssistantFeedback AS target
                USING (SELECT @TenantId AS TenantId, @MessageId AS MessageId, @UserId AS UserId) AS source
                ON target.TenantId = source.TenantId
                   AND target.MessageId = source.MessageId
                   AND target.UserId = source.UserId
                WHEN MATCHED THEN
                    UPDATE SET
                        Rating = @Rating,
                        Notes = @Notes,
                        UpdatedAtUtc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (FeedbackId, TenantId, MessageId, UserId, Rating, Notes)
                    VALUES (NEWID(), @TenantId, @MessageId, @UserId, @Rating, @Notes);
            END;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                UserId = userId,
                MessageId = messageId,
                Rating = rating,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            },
            cancellationToken: cancellationToken));
    }

    private static object ToParameters(Guid tenantId, KnowledgeChunkDraft chunk, Guid knowledgeChunkId)
    {
        return new
        {
            KnowledgeChunkId = knowledgeChunkId,
            TenantId = tenantId,
            chunk.ContextType,
            chunk.ContextEntityId,
            chunk.FocusEntityId,
            chunk.SourceEntityType,
            chunk.SourceEntityId,
            chunk.SourceTitle,
            chunk.SourceRoute,
            chunk.PermissionScope,
            chunk.Sensitivity,
            chunk.ChunkType,
            chunk.ChunkOrdinal,
            ChunkText = chunk.Text,
            chunk.MetadataJson,
            ContentHashSha256 = chunk.ContentHash
        };
    }

    private static IReadOnlyList<RagMessage> BuildMessages(
        IReadOnlyList<MessageRow> messages,
        IReadOnlyList<CitationRow> citations)
    {
        var citationMap = citations
            .GroupBy(citation => citation.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RagCitation>)group.Select(citation => citation.ToCitation()).ToArray());

        return messages
            .Select(message => message.ToMessage(citationMap.GetValueOrDefault(message.MessageId, Array.Empty<RagCitation>())))
            .ToArray();
    }

    private sealed class KnowledgeChunkExistingRow
    {
        public Guid KnowledgeChunkId { get; init; }
        public string ContentHashSha256 { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }

    private sealed class KnowledgeChunkRow
    {
        public Guid KnowledgeChunkId { get; init; }
        public string ContextType { get; init; } = string.Empty;
        public Guid ContextEntityId { get; init; }
        public Guid? FocusEntityId { get; init; }
        public string SourceEntityType { get; init; } = string.Empty;
        public Guid SourceEntityId { get; init; }
        public string SourceTitle { get; init; } = string.Empty;
        public string? SourceRoute { get; init; }
        public string PermissionScope { get; init; } = string.Empty;
        public string Sensitivity { get; init; } = string.Empty;
        public string ChunkType { get; init; } = string.Empty;
        public int ChunkOrdinal { get; init; }
        public string Text { get; init; } = string.Empty;
        public string ContentHash { get; init; } = string.Empty;

        public KnowledgeRetrievedChunk ToRetrieved(decimal score)
        {
            return new KnowledgeRetrievedChunk(
                KnowledgeChunkId,
                ContextType,
                ContextEntityId,
                FocusEntityId,
                SourceEntityType,
                SourceEntityId,
                SourceTitle,
                SourceRoute,
                PermissionScope,
                Sensitivity,
                ChunkType,
                ChunkOrdinal,
                Text,
                ContentHash,
                score);
        }
    }

    private sealed class ConversationRow
    {
        public Guid ConversationId { get; init; }
        public string ContextType { get; init; } = string.Empty;
        public Guid ContextEntityId { get; init; }
        public Guid? FocusEntityId { get; init; }
        public string Title { get; init; } = string.Empty;
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }

        public RagConversation ToConversation(IReadOnlyList<RagMessage> messages)
        {
            return new RagConversation(
                ConversationId,
                ContextType,
                ContextEntityId,
                FocusEntityId,
                Title,
                DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(UpdatedAtUtc, DateTimeKind.Utc),
                messages);
        }
    }

    private sealed class MessageRow
    {
        public Guid MessageId { get; init; }
        public string Role { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string? Model { get; init; }
        public Guid? AgentRunId { get; init; }
        public string? PromptVersion { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime CreatedAtUtc { get; init; }

        public RagMessage ToMessage(IReadOnlyList<RagCitation> citations)
        {
            return new RagMessage(
                MessageId,
                Role,
                Content,
                Model,
                AgentRunId,
                PromptVersion,
                ErrorCode,
                ErrorMessage,
                DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc),
                citations);
        }
    }

    private sealed class CitationRow
    {
        public Guid CitationId { get; init; }
        public Guid MessageId { get; init; }
        public Guid KnowledgeChunkId { get; init; }
        public string Label { get; init; } = string.Empty;
        public string SourceTitle { get; init; } = string.Empty;
        public string SourceType { get; init; } = string.Empty;
        public Guid SourceEntityId { get; init; }
        public string? SourceRoute { get; init; }
        public decimal Score { get; init; }
        public string Excerpt { get; init; } = string.Empty;

        public RagCitation ToCitation()
        {
            return new RagCitation(
                CitationId,
                KnowledgeChunkId,
                Label,
                SourceTitle,
                SourceType,
                SourceEntityId,
                SourceRoute,
                Score,
                Excerpt);
        }
    }
}
