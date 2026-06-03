using Dapper;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Infrastructure.Persistence;

namespace TalentPilot.Infrastructure.Notifications;

public sealed class DapperNotificationWorkerStatusStore : INotificationWorkerStatusStore
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperNotificationWorkerStatusStore(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task RecordHeartbeatAsync(NotificationWorkerHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.NotificationWorkerStatus', N'U') IS NULL
            BEGIN
                RETURN;
            END;

            MERGE dbo.NotificationWorkerStatus WITH (HOLDLOCK) AS target
            USING
            (
                SELECT
                    @WorkerName AS WorkerName,
                    @Status AS Status,
                    @HostName AS HostName,
                    @ProcessId AS ProcessId,
                    @StartedAtUtc AS StartedAtUtc,
                    @LastProcessedCount AS LastProcessedCount,
                    @LastError AS LastError
            ) AS source
            ON target.WorkerName = source.WorkerName
            WHEN MATCHED THEN
                UPDATE SET
                    Status = source.Status,
                    HostName = source.HostName,
                    ProcessId = source.ProcessId,
                    StartedAtUtc = source.StartedAtUtc,
                    LastHeartbeatUtc = SYSUTCDATETIME(),
                    LastProcessedAtUtc =
                        CASE
                            WHEN source.LastProcessedCount > 0 THEN SYSUTCDATETIME()
                            ELSE target.LastProcessedAtUtc
                        END,
                    LastProcessedCount = source.LastProcessedCount,
                    LastError = source.LastError,
                    UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT
                (
                    WorkerName,
                    Status,
                    HostName,
                    ProcessId,
                    StartedAtUtc,
                    LastHeartbeatUtc,
                    LastProcessedAtUtc,
                    LastProcessedCount,
                    LastError,
                    UpdatedAtUtc
                )
                VALUES
                (
                    source.WorkerName,
                    source.Status,
                    source.HostName,
                    source.ProcessId,
                    source.StartedAtUtc,
                    SYSUTCDATETIME(),
                    CASE WHEN source.LastProcessedCount > 0 THEN SYSUTCDATETIME() ELSE NULL END,
                    source.LastProcessedCount,
                    source.LastError,
                    SYSUTCDATETIME()
                );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                heartbeat.WorkerName,
                Status = string.IsNullOrWhiteSpace(heartbeat.LastError) ? "Running" : "Error",
                heartbeat.HostName,
                heartbeat.ProcessId,
                StartedAtUtc = heartbeat.StartedAtUtc.UtcDateTime,
                heartbeat.LastProcessedCount,
                LastError = Truncate(heartbeat.LastError, 1000)
            },
            cancellationToken: cancellationToken));
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
