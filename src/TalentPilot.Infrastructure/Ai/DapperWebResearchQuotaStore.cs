using Dapper;
using TalentPilot.Application.Abstractions;
using TalentPilot.Infrastructure.Persistence;

namespace TalentPilot.Infrastructure.Ai;

public sealed class DapperWebResearchQuotaStore : IWebResearchQuotaStore
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperWebResearchQuotaStore(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> TryReserveAsync(
        string provider,
        DateOnly usageDateUtc,
        int dailyLimit,
        CancellationToken cancellationToken)
    {
        if (dailyLimit <= 0)
        {
            return false;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var usageDate = usageDateUtc.ToDateTime(TimeOnly.MinValue);
        var providerName = NormalizeProvider(provider);

        const string readSql = """
            SELECT RequestCount
            FROM dbo.ExternalToolDailyUsage WITH (UPDLOCK, HOLDLOCK)
            WHERE Provider = @Provider
              AND UsageDateUtc = @UsageDateUtc;
            """;

        var existingCount = await connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(
                readSql,
                new { Provider = providerName, UsageDateUtc = usageDate },
                transaction,
                cancellationToken: cancellationToken));

        if (existingCount.GetValueOrDefault() >= dailyLimit)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (existingCount.HasValue)
        {
            const string updateSql = """
                UPDATE dbo.ExternalToolDailyUsage
                SET RequestCount = RequestCount + 1,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE Provider = @Provider
                  AND UsageDateUtc = @UsageDateUtc;
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                updateSql,
                new { Provider = providerName, UsageDateUtc = usageDate },
                transaction,
                cancellationToken: cancellationToken));
        }
        else
        {
            const string insertSql = """
                INSERT INTO dbo.ExternalToolDailyUsage
                (
                    ExternalToolDailyUsageId,
                    Provider,
                    UsageDateUtc,
                    RequestCount
                )
                VALUES
                (
                    @ExternalToolDailyUsageId,
                    @Provider,
                    @UsageDateUtc,
                    1
                );
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new
                {
                    ExternalToolDailyUsageId = Guid.NewGuid(),
                    Provider = providerName,
                    UsageDateUtc = usageDate
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static string NormalizeProvider(string provider)
    {
        return string.IsNullOrWhiteSpace(provider) ? "Unknown" : provider.Trim();
    }
}
