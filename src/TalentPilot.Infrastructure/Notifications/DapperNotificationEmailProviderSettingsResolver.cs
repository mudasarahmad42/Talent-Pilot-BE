using Dapper;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Infrastructure.Persistence;

namespace TalentPilot.Infrastructure.Notifications;

public sealed class DapperNotificationEmailProviderSettingsResolver : INotificationEmailProviderSettingsResolver
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperNotificationEmailProviderSettingsResolver(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<NotificationEmailProviderSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT NotificationEmailProvider
            FROM dbo.TenantRecruitmentSettings
            WHERE TenantId = @TenantId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var provider = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        return new NotificationEmailProviderSettings(NotificationEmailProviders.NormalizeOrDefault(provider));
    }
}
