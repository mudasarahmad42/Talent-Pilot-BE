using System.Data;
using Dapper;
using TalentPilot.Application.Admin.Notifications;
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
                trs.CompanyAddress,
                trs.CompanyCity,
                trs.CompanyCountry,
                trs.OfficialEmail,
                trs.OfficialPhone,
                trs.PrimaryColorHex AS PrimaryColor,
                trs.CandidateLoginRequired,
                trs.CandidateCvFormat,
                trs.PublicJobsEnabled,
                trs.InviteExpiryDays,
                trs.ReapplyCooldownDays,
                trs.NotificationEmailProvider,
                trs.LogoFileName,
                trs.LogoContentType,
                trs.LogoContent,
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
        var row = await connection.QuerySingleOrDefaultAsync<TenantProfileSettingsRow>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    ConfiguredLlmModel = configuredLlmModel,
                    ConfiguredEmbeddingModel = configuredEmbeddingModel
                },
                cancellationToken: cancellationToken));

        return row is null
            ? null
            : new TenantProfileSettings(
                row.TenantId,
                row.DisplayName,
                row.Slug,
                row.Domain,
                row.AdminContactEmail,
                row.DefaultTimezone,
                row.DefaultCurrency,
                row.Status,
                row.CareerDisplayName,
                row.CompanyAddress,
                row.CompanyCity,
                row.CompanyCountry,
                row.OfficialEmail,
                row.OfficialPhone,
                row.PrimaryColor,
                row.CandidateLoginRequired,
                row.CandidateCvFormat,
                row.PublicJobsEnabled,
                row.InviteExpiryDays,
                row.ReapplyCooldownDays,
                NotificationEmailProviders.NormalizeOrDefault(row.NotificationEmailProvider),
                row.UserCount,
                row.RoleCount,
                row.SetupComplete,
                row.ConfiguredLlmModel,
                row.ConfiguredEmbeddingModel,
                row.LogoFileName,
                row.LogoContentType,
                ToBase64(row.LogoContent),
                Utc(row.UpdatedAt));
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
                CompanyAddress = @CompanyAddress,
                CompanyCity = @CompanyCity,
                CompanyCountry = @CompanyCountry,
                OfficialEmail = @OfficialEmail,
                OfficialPhone = @OfficialPhone,
                PrimaryColorHex = @PrimaryColor,
                CandidateLoginRequired = @CandidateLoginRequired,
                CandidateCvFormat = @CandidateCvFormat,
                PublicJobsEnabled = @PublicJobsEnabled,
                InviteExpiryDays = @InviteExpiryDays,
                ReapplyCooldownDays = @ReapplyCooldownDays,
                NotificationEmailProvider = @NotificationEmailProvider,
                LogoFileName = @LogoFileName,
                LogoContentType = @LogoContentType,
                LogoContent = @LogoContent,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateRecruitmentSettingsSql,
            new
            {
                TenantId = tenantId,
                CareerDisplayName = input.CareerDisplayName.Trim(),
                CompanyAddress = NullIfWhiteSpace(input.CompanyAddress),
                CompanyCity = NullIfWhiteSpace(input.CompanyCity),
                CompanyCountry = NullIfWhiteSpace(input.CompanyCountry),
                OfficialEmail = NullIfWhiteSpace(input.OfficialEmail),
                OfficialPhone = NullIfWhiteSpace(input.OfficialPhone),
                PrimaryColor = input.PrimaryColor.Trim().ToUpperInvariant(),
                input.CandidateLoginRequired,
                CandidateCvFormat = input.CandidateCvFormat.Trim().ToUpperInvariant(),
                input.PublicJobsEnabled,
                input.InviteExpiryDays,
                input.ReapplyCooldownDays,
                NotificationEmailProvider = NotificationEmailProviders.Normalize(input.NotificationEmailProvider),
                LogoFileName = string.IsNullOrWhiteSpace(input.LogoContentBase64) ? null : input.LogoFileName?.Trim(),
                LogoContentType = string.IsNullOrWhiteSpace(input.LogoContentBase64) ? null : input.LogoContentType?.Trim(),
                LogoContent = FromBase64(input.LogoContentBase64)
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

    private static DateTimeOffset Utc(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static string? ToBase64(byte[]? value)
    {
        return value is { Length: > 0 } ? Convert.ToBase64String(value) : null;
    }

    private static byte[]? FromBase64(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Convert.FromBase64String(value);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record TenantProfileSettingsRow(
        Guid TenantId,
        string DisplayName,
        string Slug,
        string Domain,
        string AdminContactEmail,
        string DefaultTimezone,
        string DefaultCurrency,
        string Status,
        string CareerDisplayName,
        string? CompanyAddress,
        string? CompanyCity,
        string? CompanyCountry,
        string? OfficialEmail,
        string? OfficialPhone,
        string PrimaryColor,
        bool CandidateLoginRequired,
        string CandidateCvFormat,
        bool PublicJobsEnabled,
        int InviteExpiryDays,
        int ReapplyCooldownDays,
        string NotificationEmailProvider,
        string? LogoFileName,
        string? LogoContentType,
        byte[]? LogoContent,
        int UserCount,
        int RoleCount,
        bool SetupComplete,
        string ConfiguredLlmModel,
        string ConfiguredEmbeddingModel,
        DateTime UpdatedAt);
}
