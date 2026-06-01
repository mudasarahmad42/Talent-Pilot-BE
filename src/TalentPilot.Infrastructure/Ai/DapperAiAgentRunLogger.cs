using System.Text.Json;
using Dapper;
using TalentPilot.Application.Abstractions;
using TalentPilot.Infrastructure.Persistence;

namespace TalentPilot.Infrastructure.Ai;

public sealed class DapperAiAgentRunLogger : IAiAgentRunLogger
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperAiAgentRunLogger(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> StartAsync(AiAgentRunStart run, CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid();
        const string sql = """
            INSERT INTO dbo.AiAgentRuns
            (
                AiAgentRunId,
                TenantId,
                AiAgentDefinitionId,
                SourceEntityType,
                SourceEntityId,
                ModelName,
                EmbeddingModelName,
                InputHash,
                Status,
                MetadataJson
            )
            VALUES
            (
                @RunId,
                @TenantId,
                @AgentId,
                @SourceEntityType,
                @SourceEntityId,
                @ModelName,
                @EmbeddingModelName,
                @InputHash,
                N'Running',
                @MetadataJson
            );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                RunId = runId,
                TenantId = run.TenantId,
                AgentId = run.AgentId,
                run.SourceEntityType,
                run.SourceEntityId,
                run.ModelName,
                run.EmbeddingModelName,
                run.InputHash,
                MetadataJson = SerializeMetadata(run.Metadata)
            },
            cancellationToken: cancellationToken));

        return runId;
    }

    public Task SucceedAsync(
        Guid tenantId,
        Guid runId,
        string outputSummary,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        return CompleteAsync(tenantId, runId, "Succeeded", outputSummary, metadata, cancellationToken);
    }

    public Task FailAsync(
        Guid tenantId,
        Guid runId,
        string outputSummary,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        return CompleteAsync(tenantId, runId, "Failed", outputSummary, metadata, cancellationToken);
    }

    private async Task CompleteAsync(
        Guid tenantId,
        Guid runId,
        string status,
        string outputSummary,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.AiAgentRuns
            SET Status = @Status,
                CompletedAtUtc = SYSUTCDATETIME(),
                OutputSummary = @OutputSummary,
                MetadataJson = @MetadataJson
            WHERE TenantId = @TenantId
              AND AiAgentRunId = @RunId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                RunId = runId,
                Status = status,
                OutputSummary = outputSummary,
                MetadataJson = SerializeMetadata(metadata)
            },
            cancellationToken: cancellationToken));
    }

    private static string SerializeMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        return JsonSerializer.Serialize(metadata.Count == 0 ? new Dictionary<string, string>() : metadata);
    }
}
