using Dapper;
using TalentPilot.Application.Feedback;
using TalentPilot.Infrastructure.Persistence;

namespace TalentPilot.Infrastructure.Notifications;

public sealed class DapperPublicFeedbackTenantResolver : IPublicFeedbackTenantResolver
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperPublicFeedbackTenantResolver(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PublicFeedbackTenant?> ResolveAsync(
        PublicFeedbackTenantQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        if (query.JobPostId.HasValue)
        {
            return await connection.QuerySingleOrDefaultAsync<PublicFeedbackTenant>(new CommandDefinition(
                """
                SELECT TOP (1)
                    tenant.TenantId,
                    tenant.DisplayName,
                    tenant.Slug
                FROM dbo.JobPosts AS post
                INNER JOIN dbo.Tenants AS tenant
                    ON tenant.TenantId = post.TenantId
                WHERE post.JobPostId = @JobPostId
                  AND tenant.Status = N'Active';
                """,
                new { JobPostId = query.JobPostId.Value },
                cancellationToken: cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(query.TenantSlug))
        {
            return await connection.QuerySingleOrDefaultAsync<PublicFeedbackTenant>(new CommandDefinition(
                """
                SELECT TOP (1)
                    tenant.TenantId,
                    tenant.DisplayName,
                    tenant.Slug
                FROM dbo.Tenants AS tenant
                WHERE tenant.Slug = @TenantSlug
                  AND tenant.Status = N'Active';
                """,
                new { TenantSlug = query.TenantSlug.Trim().ToLowerInvariant() },
                cancellationToken: cancellationToken));
        }

        var tenants = (await connection.QueryAsync<PublicFeedbackTenant>(new CommandDefinition(
            """
            SELECT
                tenant.TenantId,
                tenant.DisplayName,
                tenant.Slug
            FROM dbo.Tenants AS tenant
            LEFT JOIN dbo.TenantRecruitmentSettings AS settings
                ON settings.TenantId = tenant.TenantId
            WHERE tenant.Status = N'Active'
              AND COALESCE(settings.PublicJobsEnabled, CAST(1 AS BIT)) = CAST(1 AS BIT);
            """,
            cancellationToken: cancellationToken))).ToArray();

        return tenants.Length == 1 ? tenants[0] : null;
    }
}
