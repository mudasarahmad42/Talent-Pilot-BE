using Dapper;
using TalentPilot.Infrastructure.Persistence;

namespace TalentPilot.Api.Startup;

internal static class AdminCenterAccessPolicySchemaGuard
{
    public static async Task EnsureAdminCenterAccessPolicySchemaAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!UsesSqlServerIdentity(configuration))
        {
            return;
        }

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("AdminCenterAccessPolicySchemaGuard");

        try
        {
            var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
            await using var connection = connectionFactory.CreateConnection();
            await connection.ExecuteAsync(
                """
                IF OBJECT_ID(N'dbo.TenantAccessPolicies', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.TenantAccessPolicies', N'AdminCenterAccessMode') IS NULL
                BEGIN
                    ALTER TABLE dbo.TenantAccessPolicies
                    ADD AdminCenterAccessMode NVARCHAR(20) NOT NULL
                        CONSTRAINT DF_TenantAccessPolicies_AdminCenterAccessMode DEFAULT N'FullAccess';
                END;

                IF OBJECT_ID(N'dbo.TenantAccessPolicies', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.TenantAccessPolicies', N'AdminCenterAccessMode') IS NOT NULL
                   AND NOT EXISTS
                   (
                       SELECT 1
                       FROM sys.check_constraints
                       WHERE name = N'CK_TenantAccessPolicies_AdminCenterAccessMode'
                         AND parent_object_id = OBJECT_ID(N'dbo.TenantAccessPolicies')
                   )
                BEGIN
                    EXEC(N'ALTER TABLE dbo.TenantAccessPolicies
                        ADD CONSTRAINT CK_TenantAccessPolicies_AdminCenterAccessMode
                        CHECK (AdminCenterAccessMode IN (N''FullAccess'', N''ReadOnly''));');
                END;
                """);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin Center access policy schema guard did not complete.");
        }
    }

    private static bool UsesSqlServerIdentity(IConfiguration configuration)
    {
        var provider = configuration["DataAccess:IdentityProvider"]
            ?? configuration["DataAccess:Provider"]
            ?? "InMemory";

        return string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase);
    }
}
