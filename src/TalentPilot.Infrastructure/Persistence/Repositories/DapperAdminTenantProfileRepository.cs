using System.Data;
using Dapper;
using TalentPilot.Application.Admin.TenantProfiles;

namespace TalentPilot.Infrastructure.Persistence.Repositories;

public sealed class DapperAdminTenantProfileRepository : IAdminTenantProfileRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperAdminTenantProfileRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<TenantProfileSettings?> GetAsync(
        Guid tenantId,
        string configuredLlmModel,
        string configuredEmbeddingModel,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                t.TenantId,
                t.DisplayName,
                t.Slug,
                t.Domain,
                t.AdminContactEmail,
                t.DefaultTimezoneId AS DefaultTimezone,
                t.DefaultCurrencyCode AS DefaultCurrency,
                t.Status,
                trs.CareerDisplayName,
                trs.PrimaryColorHex AS PrimaryColor,
                trs.CandidateLoginRequired,
                trs.CandidateCvFormat,
                trs.PublicJobsEnabled,
                trs.InviteExpiryDays,
                trs.ReapplyCooldownDays,
                (SELECT COUNT(1)
                 FROM dbo.AppUsers AS u
                 WHERE u.TenantId = t.TenantId
                   AND u.DeletedAtUtc IS NULL) AS UserCount,
                (SELECT COUNT(1)
                 FROM dbo.Roles AS r
                 WHERE (r.TenantId = t.TenantId OR r.TenantId IS NULL)
                   AND r.Status = N'Active') AS RoleCount,
                t.SetupComplete,
                COALESCE(tais.LlmModel, @ConfiguredLlmModel) AS ConfiguredLlmModel,
                COALESCE(tais.EmbeddingModel, @ConfiguredEmbeddingModel) AS ConfiguredEmbeddingModel,
                CASE
                    WHEN t.UpdatedAtUtc >= trs.UpdatedAtUtc THEN t.UpdatedAtUtc
                    ELSE trs.UpdatedAtUtc
                END AS UpdatedAt
            FROM dbo.Tenants AS t
            INNER JOIN dbo.TenantRecruitmentSettings AS trs ON trs.TenantId = t.TenantId
            LEFT JOIN dbo.TenantAiSettings AS tais ON tais.TenantId = t.TenantId
            WHERE t.TenantId = @TenantId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantProfileSettings>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    ConfiguredLlmModel = configuredLlmModel,
                    ConfiguredEmbeddingModel = configuredEmbeddingModel
                },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> IsSlugAvailableAsync(Guid tenantId, string slug, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Tenants
            WHERE TenantId <> @TenantId
              AND Slug = @Slug;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, Slug = slug.Trim().ToLowerInvariant() },
                cancellationToken: cancellationToken));

        return count == 0;
    }

    public async Task UpdateAsync(
        Guid tenantId,
        Guid actorUserId,
        UpdateTenantProfileSettingsInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateTenantSql = """
            UPDATE dbo.Tenants
            SET DisplayName = @DisplayName,
                Slug = @Slug,
                Domain = @Domain,
                AdminContactEmail = @AdminContactEmail,
                DefaultTimezoneId = @DefaultTimezone,
                DefaultCurrencyCode = @DefaultCurrency,
                Status = @Status,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateTenantSql,
            new
            {
                TenantId = tenantId,
                DisplayName = input.DisplayName.Trim(),
                Slug = input.Slug.Trim().ToLowerInvariant(),
                Domain = input.Domain.Trim().ToLowerInvariant(),
                AdminContactEmail = input.AdminContactEmail.Trim(),
                input.DefaultTimezone,
                input.DefaultCurrency,
                input.Status
            },
            transaction,
            cancellationToken: cancellationToken));

        const string updateRecruitmentSettingsSql = """
            UPDATE dbo.TenantRecruitmentSettings
            SET CareerDisplayName = @CareerDisplayName,
                PrimaryColorHex = @PrimaryColor,
                CandidateLoginRequired = @CandidateLoginRequired,
                CandidateCvFormat = @CandidateCvFormat,
                PublicJobsEnabled = @PublicJobsEnabled,
                InviteExpiryDays = @InviteExpiryDays,
                ReapplyCooldownDays = @ReapplyCooldownDays,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateRecruitmentSettingsSql,
            new
            {
                TenantId = tenantId,
                CareerDisplayName = input.CareerDisplayName.Trim(),
                PrimaryColor = input.PrimaryColor.Trim().ToUpperInvariant(),
                input.CandidateLoginRequired,
                CandidateCvFormat = input.CandidateCvFormat.Trim().ToUpperInvariant(),
                input.PublicJobsEnabled,
                input.InviteExpiryDays,
                input.ReapplyCooldownDays
            },
            transaction,
            cancellationToken: cancellationToken));

        const string insertAuditSql = """
            INSERT INTO dbo.AuditLogs
            (
                AuditLogId,
                TenantId,
                ActorUserId,
                ActorDisplayName,
                EventType,
                EntityType,
                EntityId,
                RecordLabel,
                EventSummary,
                Area,
                MetadataJson
            )
            SELECT
                NEWID(),
                @TenantId,
                @ActorUserId,
                u.DisplayName,
                N'tenant_profile.updated',
                N'Tenant',
                @TenantId,
                @DisplayName,
                N'Tenant profile settings were updated.',
                N'Admin Center',
                @MetadataJson
            FROM dbo.AppUsers AS u
            WHERE u.TenantId = @TenantId
              AND u.UserId = @ActorUserId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertAuditSql,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                DisplayName = input.DisplayName.Trim(),
                MetadataJson = metadataJson
            },
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }
}
