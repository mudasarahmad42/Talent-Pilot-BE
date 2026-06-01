using System.Net;
using System.Text.Json;
using Dapper;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Infrastructure.Persistence;

namespace TalentPilot.Infrastructure.Notifications;

public sealed class DapperNotificationOutboxProcessor : INotificationOutboxProcessor
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly INotificationEmailSender _emailSender;

    public DapperNotificationOutboxProcessor(
        ISqlConnectionFactory connectionFactory,
        INotificationEmailSender emailSender)
    {
        _connectionFactory = connectionFactory;
        _emailSender = emailSender;
    }

    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        var rows = await ClaimPendingRowsAsync(Math.Max(1, batchSize), cancellationToken);
        foreach (var row in rows)
        {
            await ProcessRowAsync(row, cancellationToken);
        }

        return rows.Count;
    }

    private async Task<IReadOnlyList<OutboxRow>> ClaimPendingRowsAsync(int batchSize, CancellationToken cancellationToken)
    {
        const string sql = """
            ;WITH pending AS
            (
                SELECT TOP (@BatchSize)
                    NotificationOutboxId,
                    RecipientEmail,
                    Channel,
                    PayloadJson,
                    Status,
                    AttemptCount,
                    UpdatedAtUtc
                FROM dbo.NotificationOutbox WITH (READPAST, UPDLOCK, ROWLOCK)
                WHERE Status = N'Pending'
                  AND AvailableAtUtc <= SYSUTCDATETIME()
                ORDER BY CreatedAtUtc
            )
            UPDATE pending
            SET Status = N'Processing',
                AttemptCount = AttemptCount + 1,
                UpdatedAtUtc = SYSUTCDATETIME()
            OUTPUT
                inserted.NotificationOutboxId,
                inserted.RecipientEmail,
                inserted.Channel,
                inserted.PayloadJson;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var rows = (await connection.QueryAsync<OutboxRow>(new CommandDefinition(
            sql,
            new { BatchSize = batchSize },
            transaction,
            cancellationToken: cancellationToken))).ToArray();

        await transaction.CommitAsync(cancellationToken);
        return rows;
    }

    private async Task ProcessRowAsync(OutboxRow row, CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(row.Channel, "Email", StringComparison.OrdinalIgnoreCase))
            {
                await MarkFailedAsync(row.NotificationOutboxId, $"Unsupported outbox channel '{row.Channel}'.", cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(row.RecipientEmail))
            {
                await MarkFailedAsync(row.NotificationOutboxId, "Recipient email is missing.", cancellationToken);
                return;
            }

            var payload = NotificationOutboxEmailPayload.Parse(row.PayloadJson);
            if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Body))
            {
                await MarkFailedAsync(row.NotificationOutboxId, "Email outbox payload is missing subject or body.", cancellationToken);
                return;
            }

            var sendResult = await _emailSender.SendAsync(
                new NotificationEmailMessage(
                    row.RecipientEmail,
                    payload.Subject,
                    payload.Body,
                    payload.HtmlBody ?? ToHtml(payload.Body)),
                cancellationToken);

            if (sendResult.Succeeded)
            {
                await MarkSentAsync(row.NotificationOutboxId, cancellationToken);
                return;
            }

            await MarkFailedAsync(row.NotificationOutboxId, sendResult.Error.Message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(row.NotificationOutboxId, exception.Message, cancellationToken);
        }
    }

    private async Task MarkSentAsync(Guid notificationOutboxId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.NotificationOutbox
            SET Status = N'Sent',
                ProcessedAtUtc = SYSUTCDATETIME(),
                LastError = NULL,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE NotificationOutboxId = @NotificationOutboxId;
            """;

        await ExecuteStatusUpdateAsync(sql, notificationOutboxId, null, cancellationToken);
    }

    private async Task MarkFailedAsync(Guid notificationOutboxId, string error, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.NotificationOutbox
            SET Status = N'Failed',
                ProcessedAtUtc = SYSUTCDATETIME(),
                LastError = @LastError,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE NotificationOutboxId = @NotificationOutboxId;
            """;

        await ExecuteStatusUpdateAsync(sql, notificationOutboxId, Truncate(error, 1000), cancellationToken);
    }

    private async Task ExecuteStatusUpdateAsync(
        string sql,
        Guid notificationOutboxId,
        string? lastError,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                NotificationOutboxId = notificationOutboxId,
                LastError = lastError
            },
            cancellationToken: cancellationToken));
    }

    private static string ToHtml(string text)
    {
        return WebUtility.HtmlEncode(text).Replace("\n", "<br>", StringComparison.Ordinal);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record OutboxRow(
        Guid NotificationOutboxId,
        string? RecipientEmail,
        string Channel,
        string PayloadJson);

    private sealed record NotificationOutboxEmailPayload(
        string? Subject,
        string? Body,
        string? HtmlBody)
    {
        public static NotificationOutboxEmailPayload Parse(string payloadJson)
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;

            return new NotificationOutboxEmailPayload(
                ReadString(root, "subject"),
                ReadString(root, "body"),
                ReadString(root, "htmlBody"));
        }

        private static string? ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}
