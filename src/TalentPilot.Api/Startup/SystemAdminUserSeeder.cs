using Dapper;
using TalentPilot.Application.Auth;
using TalentPilot.Domain.Access;
using TalentPilot.Infrastructure.Persistence;

namespace TalentPilot.Api.Startup;

internal static class SystemAdminUserSeeder
{
    private const string DefaultEmail = "SysAdmin@talentpilot.com";
    private const string DefaultDisplayName = "System Administrator";
    private const string DefaultInitials = "SA";
    private const string DefaultPasswordHash = "$2a$11$1LxhIBEkjgy27QdeQcGRpOIyxnheI5kHFIclG/eCQyUS1xiAej18u";

    public static async Task SeedSystemAdminUserAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!UsesSqlServerIdentity(configuration) || !IsEnabled(configuration))
        {
            return;
        }

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SystemAdminUserSeeder");

        try
        {
            var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
            var passwordHash = ResolvePasswordHash(scope.ServiceProvider, configuration);
            var email = FirstConfigured(
                configuration["SystemAdminSeed:Email"],
                Environment.GetEnvironmentVariable("SYSTEM_ADMIN_EMAIL"),
                DefaultEmail);

            await SeedAsync(connectionFactory, logger, email, passwordHash);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "System admin startup seed did not complete.");
        }
    }

    private static async Task SeedAsync(
        ISqlConnectionFactory connectionFactory,
        ILogger logger,
        string email,
        string passwordHash)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var tenantId = await connection.ExecuteScalarAsync<Guid?>(
            """
            SELECT TOP (1) TenantId
            FROM dbo.Tenants
            WHERE Status = N'Active'
            ORDER BY CreatedAtUtc, DisplayName;
            """,
            transaction: transaction);

        if (!tenantId.HasValue)
        {
            logger.LogWarning("System admin startup seed skipped because no active tenant exists.");
            await transaction.RollbackAsync();
            return;
        }

        var systemAdminRoleId = await connection.ExecuteScalarAsync<Guid?>(
            """
            SELECT TOP (1) RoleId
            FROM dbo.Roles
            WHERE Code = @SystemAdminRoleCode
              AND Type = N'System'
              AND Status = N'Active'
            ORDER BY Priority, Name;
            """,
            new { SystemAdminRoleCode = AccessConstants.SystemAdminRoleCode },
            transaction);

        if (!systemAdminRoleId.HasValue)
        {
            logger.LogWarning("System admin startup seed skipped because the SystemAdmin role does not exist.");
            await transaction.RollbackAsync();
            return;
        }

        var user = await connection.QuerySingleOrDefaultAsync<SystemAdminSeedUserRow>(
            """
            SELECT TOP (1)
                UserId,
                TenantId,
                AccountStatus
            FROM dbo.AppUsers
            WHERE EmailNormalized = @NormalizedEmail
              AND DeletedAtUtc IS NULL
            ORDER BY CreatedAtUtc;
            """,
            new { NormalizedEmail = normalizedEmail },
            transaction);

        var userId = user?.UserId ?? Guid.NewGuid();
        var userTenantId = user?.TenantId ?? tenantId.Value;
        if (user is null)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO dbo.AppUsers
                (
                    UserId,
                    TenantId,
                    DisplayName,
                    Email,
                    EmailNormalized,
                    Initials,
                    AccountStatus,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    @UserId,
                    @TenantId,
                    @DisplayName,
                    @Email,
                    @NormalizedEmail,
                    @Initials,
                    N'Active',
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
                """,
                new
                {
                    UserId = userId,
                    TenantId = userTenantId,
                    DisplayName = DefaultDisplayName,
                    Email = email.Trim(),
                    NormalizedEmail = normalizedEmail,
                    Initials = DefaultInitials
                },
                transaction);

            logger.LogInformation("Seeded startup system admin user {Email}.", email);
        }
        else if (!string.Equals(user.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "System admin user {Email} already exists with status {AccountStatus}; status was not changed.",
                email,
                user.AccountStatus);
        }

        await EnsureCredentialAsync(connection, transaction, userTenantId, userId, passwordHash);
        await EnsureRoleAssignmentAsync(connection, transaction, userTenantId, userId, systemAdminRoleId.Value);

        var tenantAdminRoleId = await connection.ExecuteScalarAsync<Guid?>(
            """
            SELECT TOP (1) RoleId
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND Code = @TenantAdminRoleCode
              AND Status = N'Active'
            ORDER BY Priority, Name;
            """,
            new { TenantId = userTenantId, TenantAdminRoleCode = AccessConstants.TenantAdminRoleCode },
            transaction);

        if (tenantAdminRoleId.HasValue)
        {
            await EnsureRoleAssignmentAsync(connection, transaction, userTenantId, userId, tenantAdminRoleId.Value);
        }
        else
        {
            logger.LogWarning(
                "System admin user {Email} was seeded without TenantAdmin compatibility because the tenant role is missing.",
                email);
        }

        await transaction.CommitAsync();
    }

    private static async Task EnsureCredentialAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid tenantId,
        Guid userId,
        string passwordHash)
    {
        await connection.ExecuteAsync(
            """
            IF EXISTS
            (
                SELECT 1
                FROM dbo.UserCredentials
                WHERE UserId = @UserId
            )
            BEGIN
                UPDATE dbo.UserCredentials
                SET PasswordHash = CASE
                        WHEN NULLIF(LTRIM(RTRIM(PasswordHash)), N'') IS NULL THEN @PasswordHash
                        ELSE PasswordHash
                    END,
                    PasswordUpdatedAtUtc = CASE
                        WHEN NULLIF(LTRIM(RTRIM(PasswordHash)), N'') IS NULL THEN SYSUTCDATETIME()
                        ELSE PasswordUpdatedAtUtc
                    END,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE UserId = @UserId;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.UserCredentials
                (
                    UserCredentialId,
                    TenantId,
                    UserId,
                    PasswordHash,
                    PasswordUpdatedAtUtc,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    NEWID(),
                    @TenantId,
                    @UserId,
                    @PasswordHash,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            END;
            """,
            new { TenantId = tenantId, UserId = userId, PasswordHash = passwordHash },
            transaction);
    }

    private static async Task EnsureRoleAssignmentAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid tenantId,
        Guid userId,
        Guid roleId)
    {
        await connection.ExecuteAsync(
            """
            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.UserRoles
                WHERE TenantId = @TenantId
                  AND UserId = @UserId
                  AND RoleId = @RoleId
            )
            BEGIN
                INSERT INTO dbo.UserRoles
                (
                    TenantId,
                    UserId,
                    RoleId,
                    AssignedByUserId,
                    CreatedAtUtc
                )
                VALUES
                (
                    @TenantId,
                    @UserId,
                    @RoleId,
                    NULL,
                    SYSUTCDATETIME()
                );
            END;
            """,
            new { TenantId = tenantId, UserId = userId, RoleId = roleId },
            transaction);
    }

    private static string ResolvePasswordHash(IServiceProvider services, IConfiguration configuration)
    {
        var configuredHash = FirstConfigured(
            configuration["SystemAdminSeed:PasswordHash"],
            Environment.GetEnvironmentVariable("SYSTEM_ADMIN_PASSWORD_HASH"));
        if (!string.IsNullOrWhiteSpace(configuredHash))
        {
            return configuredHash;
        }

        var configuredPassword = FirstConfigured(
            configuration["SystemAdminSeed:Password"],
            Environment.GetEnvironmentVariable("SYSTEM_ADMIN_PASSWORD"));
        if (!string.IsNullOrWhiteSpace(configuredPassword))
        {
            return services.GetRequiredService<IPasswordHasher>().Hash(configuredPassword);
        }

        return DefaultPasswordHash;
    }

    private static bool IsEnabled(IConfiguration configuration)
    {
        return !bool.TryParse(configuration["SystemAdminSeed:Enabled"], out var enabled) || enabled;
    }

    private static bool UsesSqlServerIdentity(IConfiguration configuration)
    {
        var provider = configuration["DataAccess:IdentityProvider"]
            ?? configuration["DataAccess:Provider"]
            ?? "InMemory";

        return string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstConfigured(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private sealed record SystemAdminSeedUserRow(
        Guid UserId,
        Guid TenantId,
        string AccountStatus);
}
