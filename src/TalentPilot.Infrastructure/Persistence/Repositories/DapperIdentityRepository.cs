using System.Data;
using Dapper;
using TalentPilot.Application.Auth;
using TalentPilot.Domain.Access;

namespace TalentPilot.Infrastructure.Persistence.Repositories;

public sealed class DapperIdentityRepository : IIdentityRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperIdentityRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<LoginOption>> ListLoginOptionsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.UserId,
                u.DisplayName,
                u.Email,
                r.RoleId,
                r.Code,
                r.Name AS RoleName,
                r.Priority,
                g.GroupId,
                g.Name AS GroupName,
                g.Purpose AS GroupPurpose
            FROM dbo.AppUsers AS u
            INNER JOIN dbo.UserRoles AS ur ON ur.TenantId = u.TenantId AND ur.UserId = u.UserId
            INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId
            LEFT JOIN dbo.GroupMembers AS gm ON gm.TenantId = u.TenantId AND gm.UserId = u.UserId
            LEFT JOIN dbo.Groups AS g ON g.TenantId = gm.TenantId AND g.GroupId = gm.GroupId AND g.Status = N'Active'
            WHERE u.AccountStatus = N'Active'
              AND u.DeletedAtUtc IS NULL
              AND r.Status = N'Active'
            ORDER BY
                CASE WHEN r.Code = N'Candidate' THEN 1 ELSE 0 END,
                r.Priority,
                u.DisplayName;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<LoginOptionRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows
            .GroupBy(row => new { row.UserId, row.DisplayName, row.Email })
            .Select(group =>
            {
                var roles = group
                    .GroupBy(row => new { row.RoleId, row.Code, row.RoleName, row.Priority })
                    .OrderBy(row => row.Key.Priority)
                    .Select(row => new CurrentUserRole(row.Key.RoleId, row.Key.Code, row.Key.RoleName, row.Key.Priority))
                    .ToArray();

                var groups = group
                    .Where(row => row.GroupId.HasValue && !string.IsNullOrWhiteSpace(row.GroupName))
                    .GroupBy(row => new { row.GroupId, row.GroupName, row.GroupPurpose })
                    .OrderBy(row => row.Key.GroupName)
                    .Select(row => new CurrentUserGroup(row.Key.GroupId!.Value, row.Key.GroupName!, row.Key.GroupPurpose ?? string.Empty))
                    .ToArray();

                return new LoginOption(
                    group.Key.UserId,
                    group.Key.DisplayName,
                    group.Key.Email,
                    roles.FirstOrDefault()?.DisplayName ?? "No assigned role",
                    roles,
                    groups);
            })
            .OrderBy(option => option.Roles.Any(role => role.Code == "Candidate"))
            .ThenBy(option => option.Roles.Min(role => role.Priority))
            .ThenBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<AuthUserRecord?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.UserId,
                u.TenantId,
                u.DisplayName,
                u.Email,
                u.AccountStatus,
                c.PasswordHash
            FROM dbo.AppUsers AS u
            LEFT JOIN dbo.UserCredentials AS c ON c.UserId = u.UserId
            WHERE u.EmailNormalized = UPPER(@Email)
              AND u.DeletedAtUtc IS NULL;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AuthUserRecord>(
            new CommandDefinition(sql, new { Email = email.Trim() }, cancellationToken: cancellationToken));
    }

    public async Task<AuthUserRecord?> FindUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.UserId,
                u.TenantId,
                u.DisplayName,
                u.Email,
                u.AccountStatus,
                c.PasswordHash
            FROM dbo.AppUsers AS u
            LEFT JOIN dbo.UserCredentials AS c ON c.UserId = u.UserId
            WHERE u.TenantId = @TenantId
              AND u.UserId = @UserId
              AND u.DeletedAtUtc IS NULL;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AuthUserRecord>(
            new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<CurrentUserData?> GetCurrentUserDataAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            "dbo.Auth_GetCurrentUserContext",
            new { TenantId = tenantId, UserId = userId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var user = await grid.ReadSingleOrDefaultAsync<UserContextRow>();
        if (user is null)
        {
            return null;
        }

        var data = new CurrentUserData
        {
            UserId = user.UserId,
            TenantId = user.TenantId,
            TenantDisplayName = user.TenantDisplayName,
            DisplayName = user.DisplayName,
            Email = user.Email,
            PermissionResolutionMode = Enum.TryParse<PermissionResolutionMode>(
                user.PermissionResolutionMode,
                ignoreCase: true,
                out var mode)
                ? mode
                : PermissionResolutionMode.MergeAllAssignedRoles
        };

        var roles = (await grid.ReadAsync<RolePermissionRow>()).ToArray();
        foreach (var roleGroup in roles.GroupBy(role => new { role.RoleId, role.Code, role.Name, role.Priority }))
        {
            var role = new RoleWithPermissions
            {
                RoleId = roleGroup.Key.RoleId,
                Code = roleGroup.Key.Code,
                Name = roleGroup.Key.Name,
                Priority = roleGroup.Key.Priority
            };

            foreach (var permissionId in roleGroup.Select(item => item.PermissionId).Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                role.PermissionIds.Add(permissionId!);
            }

            data.Roles.Add(role);
        }

        data.Groups.AddRange(await grid.ReadAsync<CurrentUserGroup>());
        return data;
    }

    public async Task TouchLastActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.AppUsers
            SET LastActiveAtUtc = SYSUTCDATETIME(),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND UserId = @UserId
              AND DeletedAtUtc IS NULL;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task StoreRefreshTokenAsync(RefreshTokenRecord record, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.RefreshTokens
            (
                RefreshTokenId,
                TenantId,
                UserId,
                TokenHash,
                ExpiresAtUtc,
                RevokedAtUtc
            )
            VALUES
            (
                @RefreshTokenId,
                @TenantId,
                @UserId,
                @TokenHash,
                @ExpiresAtUtc,
                @RevokedAtUtc
            );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, record, cancellationToken: cancellationToken));
    }

    public async Task<RefreshTokenRecord?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                RefreshTokenId,
                TenantId,
                UserId,
                TokenHash,
                ExpiresAtUtc,
                RevokedAtUtc
            FROM dbo.RefreshTokens
            WHERE TokenHash = @TokenHash;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RefreshTokenRecord>(
            new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken));
    }

    public async Task RevokeRefreshTokenAsync(Guid refreshTokenId, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.RefreshTokens
            SET RevokedAtUtc = @RevokedAtUtc
            WHERE RefreshTokenId = @RefreshTokenId
              AND RevokedAtUtc IS NULL;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { RefreshTokenId = refreshTokenId, RevokedAtUtc = revokedAtUtc },
            cancellationToken: cancellationToken));
    }

    private sealed record UserContextRow(
        Guid UserId,
        Guid TenantId,
        string DisplayName,
        string Email,
        string AccountStatus,
        string TenantDisplayName,
        string PermissionResolutionMode);

    private sealed record RolePermissionRow(
        Guid RoleId,
        string Code,
        string Name,
        int Priority,
        string? PermissionId);

    private sealed record LoginOptionRow(
        Guid UserId,
        string DisplayName,
        string Email,
        Guid RoleId,
        string Code,
        string RoleName,
        int Priority,
        Guid? GroupId,
        string? GroupName,
        string? GroupPurpose);
}
