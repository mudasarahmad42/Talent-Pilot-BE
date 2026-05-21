using System.Text.Json;
using Dapper;
using TalentPilot.Application.Admin.AuditLogs;
using TalentPilot.Application.Admin.Groups;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Application.Admin.Roles;
using TalentPilot.Application.Admin.Users;
using TalentPilot.Domain.Access;

namespace TalentPilot.Infrastructure.Persistence.Repositories;

public sealed class DapperAdminCenterRepository :
    IAdminUsersRepository,
    IAdminAccessPoliciesRepository,
    IAdminGroupsRepository,
    IAdminRolesRepository,
    IAdminNotificationsRepository,
    IAdminAuditLogRepository,
    INotificationOutboxProcessor
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperAdminCenterRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AdminUsersResponse> ListAsync(Guid tenantId, AdminUsersQuery query, CancellationToken cancellationToken)
    {
        var users = (await LoadAdminUsersAsync(tenantId, cancellationToken))
            .Where(user => user.IsInternalUser)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            users = users
                .Where(user =>
                    user.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    user.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    user.RoleNames.Any(role => role.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    user.GroupNames.Any(group => group.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        if (query.RoleId.HasValue)
        {
            users = users.Where(user => user.RoleIds.Contains(query.RoleId.Value)).ToArray();
        }

        if (query.GroupId.HasValue)
        {
            users = users.Where(user => user.GroupIds.Contains(query.GroupId.Value)).ToArray();
        }

        if (!string.IsNullOrWhiteSpace(query.AccountStatus))
        {
            users = users
                .Where(user => user.AccountStatus.Equals(query.AccountStatus, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var ordered = users.OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToAdminUserListItem)
            .ToArray();

        var policy = await GetBenchVisibilityPolicySummaryAsync(tenantId, cancellationToken);
        var summary = new AdminUsersSummary(
            users.Length,
            await CountRoutingGroupsAsync(tenantId, cancellationToken),
            policy);

        return new AdminUsersResponse(summary, items, query.Page, query.PageSize, ordered.Length);
    }

    async Task<AdminUserDetails?> IAdminUsersRepository.GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        => await GetUserAsync(tenantId, userId, cancellationToken);

    private async Task<AdminUserDetails?> GetUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = (await LoadAdminUsersAsync(tenantId, cancellationToken))
            .FirstOrDefault(item => item.UserId == userId);

        return user is null ? null : ToAdminUserDetails(user);
    }

    public async Task<Guid?> FindRoleIdByCodeAsync(Guid tenantId, string roleCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT RoleId
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND Code = @RoleCode;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { TenantId = tenantId, RoleCode = roleCode }, cancellationToken: cancellationToken));
    }

    public async Task<bool> EmailExistsAsync(Guid tenantId, string email, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND EmailNormalized = UPPER(@Email)
              AND DeletedAtUtc IS NULL
              AND (@ExceptUserId IS NULL OR UserId <> @ExceptUserId);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, Email = email.Trim(), ExceptUserId = exceptUserId },
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> ActiveRolesExistAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var distinctRoleIds = roleIds.Distinct().ToArray();
        if (distinctRoleIds.Length == 0)
        {
            return false;
        }

        const string sql = """
            SELECT COUNT(DISTINCT RoleId)
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND RoleId IN @RoleIds;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, RoleIds = distinctRoleIds }, cancellationToken: cancellationToken));

        return count == distinctRoleIds.Length;
    }

    public async Task<bool> ActiveGroupsExistAsync(Guid tenantId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken)
    {
        var distinctGroupIds = groupIds.Distinct().ToArray();
        if (distinctGroupIds.Length == 0)
        {
            return true;
        }

        const string sql = """
            SELECT COUNT(DISTINCT GroupId)
            FROM dbo.Groups
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND GroupId IN @GroupIds;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, GroupIds = distinctGroupIds }, cancellationToken: cancellationToken));

        return count == distinctGroupIds.Length;
    }

    public async Task<int> CountActiveTenantAdminsAsync(Guid tenantId, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(DISTINCT u.UserId)
            FROM dbo.AppUsers AS u
            INNER JOIN dbo.UserRoles AS ur ON ur.TenantId = u.TenantId AND ur.UserId = u.UserId
            INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId
            WHERE u.TenantId = @TenantId
              AND u.AccountStatus = N'Active'
              AND u.DeletedAtUtc IS NULL
              AND r.Code = @TenantAdminRoleCode
              AND r.Status = N'Active'
              AND (@ExceptUserId IS NULL OR u.UserId <> @ExceptUserId);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    ExceptUserId = exceptUserId,
                    TenantAdminRoleCode = AccessConstants.TenantAdminRoleCode
                },
                cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        SaveAdminUserInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        var userId = Guid.NewGuid();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertUserSql = """
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
                UPPER(@Email),
                @Initials,
                @AccountStatus,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );

            INSERT INTO dbo.UserCredentials
            (
                UserCredentialId,
                TenantId,
                UserId,
                PasswordHash,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                NEWID(),
                @TenantId,
                @UserId,
                NULL,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        var trimmedName = input.DisplayName.Trim();
        await connection.ExecuteAsync(new CommandDefinition(
            insertUserSql,
            new
            {
                UserId = userId,
                TenantId = tenantId,
                DisplayName = trimmedName,
                Email = input.Email.Trim().ToLowerInvariant(),
                Initials = BuildInitials(trimmedName),
                input.AccountStatus
            },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceUserAssignmentsAsync(connection, transaction, tenantId, actorUserId, userId, input.RoleIds, input.GroupIds, cancellationToken);
        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "UserCreated", "User", userId, trimmedName, "Created internal user.", "Admin Center", metadataJson, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return userId;
    }

    public async Task UpdateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid userId,
        SaveAdminUserInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE dbo.AppUsers
            SET DisplayName = @DisplayName,
                Email = @Email,
                EmailNormalized = UPPER(@Email),
                Initials = @Initials,
                AccountStatus = @AccountStatus,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND UserId = @UserId
              AND DeletedAtUtc IS NULL;
            """;

        var trimmedName = input.DisplayName.Trim();
        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new
            {
                TenantId = tenantId,
                UserId = userId,
                DisplayName = trimmedName,
                Email = input.Email.Trim().ToLowerInvariant(),
                Initials = BuildInitials(trimmedName),
                input.AccountStatus
            },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceUserAssignmentsAsync(connection, transaction, tenantId, actorUserId, userId, input.RoleIds, input.GroupIds, cancellationToken);
        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "UserUpdated", "User", userId, trimmedName, "Updated internal user.", "Admin Center", metadataJson, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid userId,
        UpdateAdminUserStatusInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.AppUsers
            SET AccountStatus = @AccountStatus,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND UserId = @UserId
              AND DeletedAtUtc IS NULL;

            SELECT DisplayName
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId = @UserId;
            """;

        var displayName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, UserId = userId, input.AccountStatus },
                transaction,
                cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "UserStatusUpdated",
            "User",
            userId,
            displayName ?? "User",
            $"Changed account status to {input.AccountStatus}.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task InsertInviteNotificationAsync(Guid tenantId, Guid actorUserId, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string displayNameSql = """
            SELECT DisplayName
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId = @UserId;
            """;

        var displayName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(displayNameSql, new { TenantId = tenantId, UserId = userId }, transaction, cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "UserInviteQueued",
            "User",
            userId,
            displayName ?? "User",
            "Queued user invitation email.",
            "Admin Center",
            "{}",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<BenchVisibilityPolicy?> GetBenchVisibilityPolicyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.BenchVisibilityRoleId AS RoleId,
                r.Name AS RoleName,
                p.UpdatedAtUtc,
                p.UpdatedByUserId
            FROM dbo.TenantAccessPolicies AS p
            INNER JOIN dbo.Roles AS r ON r.RoleId = p.BenchVisibilityRoleId
            WHERE p.TenantId = @TenantId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<BenchPolicyRow>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new BenchVisibilityPolicy(row.RoleId, row.RoleName, Utc(row.UpdatedAtUtc), row.UpdatedByUserId ?? Guid.Empty);
    }

    public async Task<bool> RoleIsActiveAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND RoleId = @RoleId
              AND Status = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, RoleId = roleId }, cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task UpdateBenchVisibilityPolicyAsync(Guid tenantId, Guid actorUserId, Guid roleId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE dbo.TenantAccessPolicies
            SET BenchVisibilityRoleId = @RoleId,
                UpdatedByUserId = @ActorUserId,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new { TenantId = tenantId, RoleId = roleId, ActorUserId = actorUserId },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "BenchVisibilityPolicyUpdated", "AccessPolicy", roleId, "Bench visibility", "Updated bench visibility role.", "Admin Center", "{}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AdminGroupsResponse> ListAsync(Guid tenantId, AdminGroupsQuery query, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                g.GroupId,
                g.Name,
                g.Purpose,
                g.Status,
                COUNT(gm.UserId) AS MemberCount
            FROM dbo.Groups AS g
            LEFT JOIN dbo.GroupMembers AS gm ON gm.TenantId = g.TenantId AND gm.GroupId = g.GroupId
            WHERE g.TenantId = @TenantId
              AND (@Purpose IS NULL OR g.Purpose = @Purpose)
            GROUP BY g.GroupId, g.Name, g.Purpose, g.Status
            ORDER BY g.Name;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<AdminGroupListItem>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, Purpose = EmptyToNull(query.Purpose) },
                cancellationToken: cancellationToken))).ToArray();

        var items = rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        return new AdminGroupsResponse(items, query.Page, query.PageSize, rows.Length);
    }

    public async Task<AdminRolesResponse> ListAsync(Guid tenantId, AdminRolesQuery query, CancellationToken cancellationToken)
    {
        var roles = await LoadRolesAsync(tenantId, cancellationToken);

        if (!query.IncludeInactive)
        {
            roles = roles.Where(role => role.Status == "Active").ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            roles = roles
                .Where(role => role.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var ordered = roles.OrderBy(role => role.Priority).ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToRoleSummary)
            .ToArray();

        var summary = new AdminRolesSummary(
            roles.Count(role => role.Status == "Active"),
            roles.Count(role => role.IsProtected),
            roles.Count(role => role.Type == "Custom"));

        return new AdminRolesResponse(summary, items, query.Page, query.PageSize, ordered.Length);
    }

    async Task<RoleDetails?> IAdminRolesRepository.GetAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
        => await GetRoleAsync(tenantId, roleId, cancellationToken);

    private async Task<RoleDetails?> GetRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        var role = (await LoadRolesAsync(tenantId, cancellationToken))
            .FirstOrDefault(item => item.RoleId == roleId);

        return role is null ? null : ToRoleDetails(role);
    }

    public async Task<IReadOnlyList<PermissionCatalogItem>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT PermissionId, DisplayName, GroupName, Description, Status
            FROM dbo.Permissions
            ORDER BY GroupName, DisplayName;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return (await connection.QueryAsync<PermissionCatalogItem>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))).ToArray();
    }

    public async Task<bool> PermissionIdsExistAsync(IReadOnlyCollection<string> permissionIds, CancellationToken cancellationToken)
    {
        var distinctIds = permissionIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (distinctIds.Length == 0)
        {
            return false;
        }

        const string sql = """
            SELECT COUNT(DISTINCT PermissionId)
            FROM dbo.Permissions
            WHERE Status = N'Active'
              AND PermissionId IN @PermissionIds;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { PermissionIds = distinctIds }, cancellationToken: cancellationToken));

        return count == distinctIds.Length;
    }

    public async Task<bool> RoleNameExistsAsync(Guid tenantId, string name, Guid? exceptRoleId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND LOWER(Name) = LOWER(@Name)
              AND (@ExceptRoleId IS NULL OR RoleId <> @ExceptRoleId);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, Name = name.Trim(), ExceptRoleId = exceptRoleId },
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        SaveRoleInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        var roleId = Guid.NewGuid();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertRoleSql = """
            INSERT INTO dbo.Roles
            (
                RoleId,
                TenantId,
                Code,
                Name,
                Type,
                Scope,
                Priority,
                IsProtected,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @RoleId,
                @TenantId,
                @Code,
                @Name,
                N'Custom',
                @Scope,
                @Priority,
                0,
                @Status,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        var roleName = input.Name.Trim();
        await connection.ExecuteAsync(new CommandDefinition(
            insertRoleSql,
            new
            {
                RoleId = roleId,
                TenantId = tenantId,
                Code = BuildRoleCode(roleName),
                Name = roleName,
                input.Scope,
                input.Priority,
                input.Status
            },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceRolePermissionsAsync(connection, transaction, roleId, input.PermissionIds, cancellationToken);
        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "RoleCreated", "Role", roleId, roleName, "Created role.", "Admin Center", metadataJson, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return roleId;
    }

    public async Task UpdateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid roleId,
        SaveRoleInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateRoleSql = """
            UPDATE dbo.Roles
            SET Name = @Name,
                Scope = @Scope,
                Priority = @Priority,
                Status = @Status,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND RoleId = @RoleId
              AND IsProtected = 0;
            """;

        var roleName = input.Name.Trim();
        await connection.ExecuteAsync(new CommandDefinition(
            updateRoleSql,
            new { TenantId = tenantId, RoleId = roleId, Name = roleName, input.Scope, input.Priority, input.Status },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceRolePermissionsAsync(connection, transaction, roleId, input.PermissionIds, cancellationToken);
        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "RoleUpdated", "Role", roleId, roleName, "Updated role.", "Admin Center", metadataJson, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid roleId,
        string status,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.Roles
            SET Status = @Status,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND RoleId = @RoleId
              AND IsProtected = 0;

            SELECT Name
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND RoleId = @RoleId;
            """;

        var roleName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, RoleId = roleId, Status = status },
                transaction,
                cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "RoleStatusUpdated", "Role", roleId, roleName ?? "Role", $"Changed role status to {status}.", "Admin Center", metadataJson, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PermissionResolutionPolicy?> GetPermissionResolutionPolicyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                PermissionResolutionMode AS Mode,
                UpdatedAtUtc,
                UpdatedByUserId
            FROM dbo.TenantAccessPolicies
            WHERE TenantId = @TenantId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PermissionPolicyRow>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new PermissionResolutionPolicy(row.Mode, Utc(row.UpdatedAtUtc), row.UpdatedByUserId ?? Guid.Empty);
    }

    public async Task UpdatePermissionResolutionPolicyAsync(Guid tenantId, Guid actorUserId, string mode, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE dbo.TenantAccessPolicies
            SET PermissionResolutionMode = @Mode,
                UpdatedByUserId = @ActorUserId,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new { TenantId = tenantId, ActorUserId = actorUserId, Mode = mode },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "PermissionResolutionPolicyUpdated", "AccessPolicy", tenantId, "Permission resolution", $"Updated permission resolution to {mode}.", "Admin Center", "{}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<RoleUserAssignmentPreview> PreviewUserAssignmentsAsync(
        Guid tenantId,
        Guid roleId,
        RoleUserAssignmentFilterInput input,
        CancellationToken cancellationToken)
    {
        var matching = await FindUsersForRoleAssignmentAsync(tenantId, input, cancellationToken);
        var assignable = matching.Where(user => !user.RoleIds.Contains(roleId)).ToArray();
        var sample = assignable.Take(25).Select(ToRoleUserAssignmentPreviewItem).ToArray();

        return new RoleUserAssignmentPreview(
            matching.Length,
            matching.Length - assignable.Length,
            assignable.Length,
            sample);
    }

    public async Task<BulkAssignRoleUsersResponse> BulkAssignUsersAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid roleId,
        BulkAssignRoleUsersInput input,
        CancellationToken cancellationToken)
    {
        var matching = await FindUsersForRoleAssignmentAsync(tenantId, input.Filters, cancellationToken);
        var targetUsers = input.SelectionMode == "SelectedUsers"
            ? matching.Where(user => input.SelectedUserIds?.Contains(user.UserId) == true).ToArray()
            : matching;

        var assignable = targetUsers.Where(user => !user.RoleIds.Contains(roleId)).ToArray();
        var batchId = Guid.NewGuid();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertAssignmentSql = """
            IF NOT EXISTS (
                SELECT 1
                FROM dbo.UserRoles
                WHERE TenantId = @TenantId
                  AND UserId = @UserId
                  AND RoleId = @RoleId
            )
            BEGIN
                INSERT INTO dbo.UserRoles (TenantId, UserId, RoleId, AssignedByUserId, CreatedAtUtc)
                VALUES (@TenantId, @UserId, @RoleId, @ActorUserId, SYSUTCDATETIME());
            END;
            """;

        foreach (var user in assignable)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertAssignmentSql,
                new { TenantId = tenantId, UserId = user.UserId, RoleId = roleId, ActorUserId = actorUserId },
                transaction,
                cancellationToken: cancellationToken));
        }

        const string insertBatchSql = """
            INSERT INTO dbo.RoleAssignmentBatches
            (
                RoleAssignmentBatchId,
                TenantId,
                RoleId,
                FilterJson,
                SelectionMode,
                SelectedUserIdsJson,
                MatchedCount,
                AssignedCount,
                SkippedCount,
                CreatedByUserId,
                CreatedAtUtc
            )
            VALUES
            (
                @BatchId,
                @TenantId,
                @RoleId,
                @FilterJson,
                @SelectionMode,
                @SelectedUserIdsJson,
                @MatchedCount,
                @AssignedCount,
                @SkippedCount,
                @ActorUserId,
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertBatchSql,
            new
            {
                BatchId = batchId,
                TenantId = tenantId,
                RoleId = roleId,
                FilterJson = JsonSerializer.Serialize(input.Filters),
                input.SelectionMode,
                SelectedUserIdsJson = input.SelectedUserIds is null ? null : JsonSerializer.Serialize(input.SelectedUserIds),
                MatchedCount = matching.Length,
                AssignedCount = assignable.Length,
                SkippedCount = targetUsers.Length - assignable.Length,
                ActorUserId = actorUserId
            },
            transaction,
            cancellationToken: cancellationToken));

        var roleName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Name FROM dbo.Roles WHERE TenantId = @TenantId AND RoleId = @RoleId;",
                new { TenantId = tenantId, RoleId = roleId },
                transaction,
                cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "RoleBulkAssigned", "Role", roleId, roleName ?? "Role", $"Bulk assigned {roleName ?? "role"} to {assignable.Length} users.", "Admin Center", "{}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BulkAssignRoleUsersResponse(batchId, matching.Length, assignable.Length, targetUsers.Length - assignable.Length);
    }

    public async Task<AdminNotificationEventsResponse> ListEventsAsync(
        Guid tenantId,
        AdminNotificationEventsQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                e.NotificationEventId AS EventId,
                e.EventCode,
                e.Name,
                e.DefaultRecipientType AS Recipient,
                COALESCE(t.Name, N'No email template') AS TemplateName,
                e.Status AS LifecycleStatus,
                CASE
                    WHEN t.UpdatedAtUtc IS NOT NULL AND t.UpdatedAtUtc > e.UpdatedAtUtc THEN t.UpdatedAtUtc
                    ELSE e.UpdatedAtUtc
                END AS UpdatedAtUtc
            FROM dbo.NotificationEvents AS e
            OUTER APPLY
            (
                SELECT TOP (1) template.Name, template.UpdatedAtUtc
                FROM dbo.NotificationTemplates AS template
                WHERE template.TenantId = e.TenantId
                  AND template.NotificationEventId = e.NotificationEventId
                ORDER BY template.UpdatedAtUtc DESC
            ) AS t
            WHERE e.TenantId = @TenantId
              AND (
                    @Search IS NULL
                    OR e.EventCode LIKE @SearchLike
                    OR e.Name LIKE @SearchLike
                  )
            ORDER BY e.EventCode;

            SELECT
                SUM(CASE WHEN Status = N'Active' THEN 1 ELSE 0 END) AS ActiveEventCount
            FROM dbo.NotificationEvents
            WHERE TenantId = @TenantId;

            SELECT COUNT(1)
            FROM dbo.NotificationTemplates
            WHERE TenantId = @TenantId
              AND Status = N'Active';

            SELECT COUNT(1)
            FROM dbo.NotificationOutbox
            WHERE TenantId = @TenantId
              AND Status = N'Pending';

            SELECT COUNT(1)
            FROM dbo.NotificationOutbox
            WHERE TenantId = @TenantId
              AND Status = N'Failed';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            SearchParameters(tenantId, query.Search),
            cancellationToken: cancellationToken));

        var rows = (await grid.ReadAsync<NotificationEventRow>()).ToArray();
        var activeEventCount = await grid.ReadSingleAsync<int>();
        var editableTemplateCount = await grid.ReadSingleAsync<int>();
        var pendingOutboxCount = await grid.ReadSingleAsync<int>();
        var failedOutboxCount = await grid.ReadSingleAsync<int>();

        var items = rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToNotificationEventListItem)
            .ToArray();

        return new AdminNotificationEventsResponse(
            new AdminNotificationEventsSummary(activeEventCount, editableTemplateCount, pendingOutboxCount, failedOutboxCount),
            items,
            query.Page,
            query.PageSize,
            rows.Length);
    }

    public async Task<AdminNotificationEventDetails?> GetEventAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                NotificationEventId AS EventId,
                EventCode,
                Name,
                DefaultRecipientType AS Recipient,
                Status AS LifecycleStatus
            FROM dbo.NotificationEvents
            WHERE TenantId = @TenantId
              AND NotificationEventId = @EventId;

            SELECT
                t.NotificationTemplateId AS TemplateId,
                e.EventCode,
                t.Name,
                t.Recipient,
                t.Subject,
                t.Body,
                t.AllowedVariablesJson,
                t.Status AS LifecycleStatus,
                t.UpdatedAtUtc,
                t.UpdatedByUserId
            FROM dbo.NotificationTemplates AS t
            INNER JOIN dbo.NotificationEvents AS e ON e.NotificationEventId = t.NotificationEventId
            WHERE t.TenantId = @TenantId
              AND t.NotificationEventId = @EventId
            ORDER BY t.Name;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, EventId = eventId },
            cancellationToken: cancellationToken));

        var row = await grid.ReadSingleOrDefaultAsync<NotificationEventDetailsRow>();
        if (row is null)
        {
            return null;
        }

        var templates = (await grid.ReadAsync<NotificationTemplateRow>())
            .Select(ToNotificationTemplateSummary)
            .ToArray();

        return new AdminNotificationEventDetails(row.EventId, row.EventCode, row.Name, row.Recipient, row.LifecycleStatus, templates);
    }

    public async Task<IReadOnlyList<NotificationTemplateSummary>> ListTemplatesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                t.NotificationTemplateId AS TemplateId,
                e.EventCode,
                t.Name,
                t.Recipient,
                t.Subject,
                t.Body,
                t.AllowedVariablesJson,
                t.Status AS LifecycleStatus,
                t.UpdatedAtUtc,
                t.UpdatedByUserId
            FROM dbo.NotificationTemplates AS t
            INNER JOIN dbo.NotificationEvents AS e ON e.NotificationEventId = t.NotificationEventId
            WHERE t.TenantId = @TenantId
            ORDER BY t.Name;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return (await connection.QueryAsync<NotificationTemplateRow>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)))
            .Select(ToNotificationTemplateSummary)
            .ToArray();
    }

    public async Task<NotificationTemplateSummary?> GetTemplateAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                t.NotificationTemplateId AS TemplateId,
                e.EventCode,
                t.Name,
                t.Recipient,
                t.Subject,
                t.Body,
                t.AllowedVariablesJson,
                t.Status AS LifecycleStatus,
                t.UpdatedAtUtc,
                t.UpdatedByUserId
            FROM dbo.NotificationTemplates AS t
            INNER JOIN dbo.NotificationEvents AS e ON e.NotificationEventId = t.NotificationEventId
            WHERE t.TenantId = @TenantId
              AND t.NotificationTemplateId = @TemplateId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<NotificationTemplateRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, TemplateId = templateId }, cancellationToken: cancellationToken));

        return row is null ? null : ToNotificationTemplateSummary(row);
    }

    public async Task UpdateTemplateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid templateId,
        UpdateNotificationTemplateInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE dbo.NotificationTemplates
            SET Subject = @Subject,
                Body = @Body,
                UpdatedByUserId = @ActorUserId,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND NotificationTemplateId = @TemplateId;

            SELECT Name
            FROM dbo.NotificationTemplates
            WHERE TenantId = @TenantId
              AND NotificationTemplateId = @TemplateId;
            """;

        var templateName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                updateSql,
                new { TenantId = tenantId, ActorUserId = actorUserId, TemplateId = templateId, input.Subject, input.Body },
                transaction,
                cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "NotificationTemplateUpdated", "NotificationTemplate", templateId, templateName ?? "Notification template", "Updated notification template.", "Admin Center", metadataJson, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateEventStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid eventId,
        string status,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE dbo.NotificationEvents
            SET Status = @Status,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND NotificationEventId = @EventId;

            SELECT Name
            FROM dbo.NotificationEvents
            WHERE TenantId = @TenantId
              AND NotificationEventId = @EventId;
            """;

        var eventName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                updateSql,
                new { TenantId = tenantId, EventId = eventId, Status = status },
                transaction,
                cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "NotificationEventStatusUpdated", "NotificationEvent", eventId, eventName ?? "Notification event", $"Changed notification event status to {status}.", "Admin Center", metadataJson, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        const string sql = """
            ;WITH pending AS
            (
                SELECT TOP (@BatchSize) *
                FROM dbo.NotificationOutbox
                WHERE Status = N'Pending'
                  AND AvailableAtUtc <= SYSUTCDATETIME()
                ORDER BY CreatedAtUtc
            )
            UPDATE pending
            SET Status = N'Sent',
                ProcessedAtUtc = SYSUTCDATETIME(),
                UpdatedAtUtc = SYSUTCDATETIME();

            SELECT @@ROWCOUNT;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { BatchSize = Math.Max(1, batchSize) }, cancellationToken: cancellationToken));
    }

    public async Task<AdminAuditLogListResponse> ListAsync(Guid tenantId, AdminAuditLogQuery query, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                AuditLogId AS Id,
                OccurredAtUtc,
                ActorDisplayName,
                EventSummary,
                RecordLabel,
                Area
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND (@Area IS NULL OR Area = @Area)
              AND (@ActorId IS NULL OR ActorUserId = @ActorId)
              AND (@EntityType IS NULL OR EntityType = @EntityType)
              AND (@EntityId IS NULL OR EntityId = @EntityId)
              AND (
                    @Search IS NULL
                    OR EventSummary LIKE @SearchLike
                    OR RecordLabel LIKE @SearchLike
                    OR ActorDisplayName LIKE @SearchLike
                  )
            ORDER BY OccurredAtUtc DESC;

            SELECT COUNT(1)
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND CONVERT(date, OccurredAtUtc) = CONVERT(date, SYSUTCDATETIME());

            SELECT COUNT(1)
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND Area = N'Admin Center';

            SELECT COUNT(1)
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND Area = N'Workflow';

            SELECT COUNT(1)
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND Area = N'AI';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                Area = EmptyToNull(query.Area),
                query.ActorId,
                EntityType = EmptyToNull(query.EntityType),
                query.EntityId,
                Search = EmptyToNull(query.Search),
                SearchLike = Like(query.Search)
            },
            cancellationToken: cancellationToken));

        var rows = (await grid.ReadAsync<AuditLogListRow>()).ToArray();
        var eventsToday = await grid.ReadSingleAsync<int>();
        var configChanges = await grid.ReadSingleAsync<int>();
        var workflowDecisions = await grid.ReadSingleAsync<int>();
        var aiEvents = await grid.ReadSingleAsync<int>();

        var items = rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new AdminAuditLogListItem(row.Id, Utc(row.OccurredAtUtc), row.ActorDisplayName, row.EventSummary, row.RecordLabel, row.Area))
            .ToArray();

        return new AdminAuditLogListResponse(
            new AdminAuditLogSummary(eventsToday, configChanges, workflowDecisions, aiEvents),
            items,
            query.Page,
            query.PageSize,
            rows.Length);
    }

    async Task<AdminAuditLogDetails?> IAdminAuditLogRepository.GetAsync(Guid tenantId, Guid auditLogId, CancellationToken cancellationToken)
        => await GetAuditLogAsync(tenantId, auditLogId, cancellationToken);

    private async Task<AdminAuditLogDetails?> GetAuditLogAsync(Guid tenantId, Guid auditLogId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                AuditLogId AS Id,
                OccurredAtUtc,
                ActorUserId,
                ActorDisplayName,
                EventType,
                EntityType,
                EntityId,
                RecordLabel,
                EventSummary,
                Area,
                MetadataJson
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND AuditLogId = @AuditLogId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<AuditLogDetailsRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, AuditLogId = auditLogId }, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new AdminAuditLogDetails(
                row.Id,
                Utc(row.OccurredAtUtc),
                row.ActorUserId,
                row.ActorDisplayName,
                row.EventType,
                row.EntityType,
                row.EntityId,
                row.RecordLabel,
                row.EventSummary,
                row.Area,
                row.MetadataJson);
    }

    private async Task<IReadOnlyList<AdminUserMaterialized>> LoadAdminUsersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.UserId,
                u.DisplayName,
                u.Email,
                u.Initials,
                u.AccountStatus,
                u.LastActiveAtUtc,
                u.CreatedAtUtc,
                u.UpdatedAtUtc,
                e.DepartmentId,
                d.Name AS DepartmentName
            FROM dbo.AppUsers AS u
            LEFT JOIN dbo.Employees AS e ON e.TenantId = u.TenantId AND e.AppUserId = u.UserId
            LEFT JOIN dbo.Departments AS d ON d.DepartmentId = e.DepartmentId
            WHERE u.TenantId = @TenantId
              AND u.DeletedAtUtc IS NULL;

            SELECT
                ur.UserId,
                r.RoleId,
                r.Code,
                r.Name,
                r.Priority,
                r.Scope
            FROM dbo.UserRoles AS ur
            INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId
            WHERE ur.TenantId = @TenantId
              AND r.Status = N'Active';

            SELECT
                gm.UserId,
                g.GroupId,
                g.Name
            FROM dbo.GroupMembers AS gm
            INNER JOIN dbo.Groups AS g ON g.GroupId = gm.GroupId
            WHERE gm.TenantId = @TenantId
              AND g.Status = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        var userRows = (await grid.ReadAsync<UserRow>()).ToArray();
        var roleRows = (await grid.ReadAsync<UserRoleRow>()).ToArray();
        var groupRows = (await grid.ReadAsync<UserGroupRow>()).ToArray();

        var users = userRows.ToDictionary(
            user => user.UserId,
            user => new AdminUserMaterialized
            {
                UserId = user.UserId,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Initials = user.Initials,
                AccountStatus = user.AccountStatus,
                LastActiveAtUtc = user.LastActiveAtUtc,
                CreatedAtUtc = user.CreatedAtUtc,
                UpdatedAtUtc = user.UpdatedAtUtc,
                DepartmentId = user.DepartmentId,
                DepartmentName = user.DepartmentName
            });

        foreach (var role in roleRows)
        {
            if (users.TryGetValue(role.UserId, out var user))
            {
                user.Roles.Add(new UserRoleMaterialized(role.RoleId, role.Code, role.Name, role.Priority, role.Scope));
            }
        }

        foreach (var group in groupRows)
        {
            if (users.TryGetValue(group.UserId, out var user))
            {
                user.Groups.Add(new UserGroupMaterialized(group.GroupId, group.Name));
            }
        }

        return users.Values.ToArray();
    }

    private async Task<List<RoleMaterialized>> LoadRolesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                RoleId,
                Name,
                Type,
                Scope,
                Priority,
                IsProtected,
                Status
            FROM dbo.Roles
            WHERE TenantId = @TenantId;

            SELECT
                rp.RoleId,
                p.PermissionId,
                p.DisplayName
            FROM dbo.RolePermissions AS rp
            INNER JOIN dbo.Permissions AS p ON p.PermissionId = rp.PermissionId
            INNER JOIN dbo.Roles AS r ON r.RoleId = rp.RoleId
            WHERE r.TenantId = @TenantId
            ORDER BY p.GroupName, p.DisplayName;

            SELECT RoleId, COUNT(1) AS AssignedUserCount
            FROM dbo.UserRoles
            WHERE TenantId = @TenantId
            GROUP BY RoleId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        var roles = (await grid.ReadAsync<RoleRow>())
            .Select(row => new RoleMaterialized
            {
                RoleId = row.RoleId,
                Name = row.Name,
                Type = row.Type,
                Scope = row.Scope,
                Priority = row.Priority,
                IsProtected = row.IsProtected,
                Status = row.Status
            })
            .ToDictionary(role => role.RoleId);

        foreach (var permission in await grid.ReadAsync<RolePermissionRow>())
        {
            if (roles.TryGetValue(permission.RoleId, out var role))
            {
                role.Permissions.Add(new PermissionMaterialized(permission.PermissionId, permission.DisplayName));
            }
        }

        foreach (var count in await grid.ReadAsync<RoleAssignmentCountRow>())
        {
            if (roles.TryGetValue(count.RoleId, out var role))
            {
                role.AssignedUserCount = count.AssignedUserCount;
            }
        }

        return roles.Values.ToList();
    }

    private async Task<AdminUserMaterialized[]> FindUsersForRoleAssignmentAsync(
        Guid tenantId,
        RoleUserAssignmentFilterInput input,
        CancellationToken cancellationToken)
    {
        var users = (await LoadAdminUsersAsync(tenantId, cancellationToken))
            .Where(user => user.IsInternalUser);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.Trim();
            users = users.Where(user =>
                user.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                user.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (user.DepartmentName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (input.AccountStatuses is { Count: > 0 })
        {
            users = users.Where(user => input.AccountStatuses.Contains(user.AccountStatus, StringComparer.OrdinalIgnoreCase));
        }

        if (input.DepartmentIds is { Count: > 0 })
        {
            users = users.Where(user => user.DepartmentId.HasValue && input.DepartmentIds.Contains(user.DepartmentId.Value));
        }

        if (input.CurrentRoleIds is { Count: > 0 })
        {
            users = users.Where(user => user.RoleIds.Any(input.CurrentRoleIds.Contains));
        }

        if (input.GroupIds is { Count: > 0 })
        {
            users = users.Where(user => user.GroupIds.Any(input.GroupIds.Contains));
        }

        return users.OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<BenchVisibilityPolicySummary> GetBenchVisibilityPolicySummaryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var policy = await GetBenchVisibilityPolicyAsync(tenantId, cancellationToken);
        return policy is null
            ? new BenchVisibilityPolicySummary(Guid.Empty, "Not configured", "Roles & Permissions")
            : new BenchVisibilityPolicySummary(policy.RoleId, policy.RoleName, "Roles & Permissions");
    }

    private async Task<int> CountRoutingGroupsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Groups
            WHERE TenantId = @TenantId
              AND Status = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private static async Task ReplaceUserAssignmentsAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        Guid userId,
        IReadOnlyList<Guid> roleIds,
        IReadOnlyList<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        const string deleteSql = """
            DELETE FROM dbo.UserRoles
            WHERE TenantId = @TenantId
              AND UserId = @UserId;

            DELETE FROM dbo.GroupMembers
            WHERE TenantId = @TenantId
              AND UserId = @UserId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql,
            new { TenantId = tenantId, UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        const string insertRoleSql = """
            INSERT INTO dbo.UserRoles (TenantId, UserId, RoleId, AssignedByUserId, CreatedAtUtc)
            VALUES (@TenantId, @UserId, @RoleId, @ActorUserId, SYSUTCDATETIME());
            """;

        foreach (var roleId in roleIds.Distinct())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertRoleSql,
                new { TenantId = tenantId, UserId = userId, RoleId = roleId, ActorUserId = actorUserId },
                transaction,
                cancellationToken: cancellationToken));
        }

        const string insertGroupSql = """
            INSERT INTO dbo.GroupMembers (TenantId, GroupId, UserId, IsDefaultAssignee, CreatedAtUtc)
            VALUES (@TenantId, @GroupId, @UserId, 0, SYSUTCDATETIME());
            """;

        foreach (var groupId in groupIds.Distinct())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertGroupSql,
                new { TenantId = tenantId, GroupId = groupId, UserId = userId },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static async Task ReplaceRolePermissionsAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid roleId,
        IReadOnlyList<string> permissionIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.RolePermissions WHERE RoleId = @RoleId;",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        const string insertSql = """
            INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedAtUtc)
            VALUES (@RoleId, @PermissionId, SYSUTCDATETIME());
            """;

        foreach (var permissionId in permissionIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new { RoleId = roleId, PermissionId = permissionId },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static async Task InsertAuditAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        string eventType,
        string entityType,
        Guid? entityId,
        string recordLabel,
        string eventSummary,
        string area,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        const string sql = """
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
                @EventType,
                @EntityType,
                @EntityId,
                @RecordLabel,
                @EventSummary,
                @Area,
                @MetadataJson
            FROM dbo.AppUsers AS u
            WHERE u.TenantId = @TenantId
              AND u.UserId = @ActorUserId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                EventType = eventType,
                EntityType = entityType,
                EntityId = entityId,
                RecordLabel = recordLabel,
                EventSummary = eventSummary,
                Area = area,
                MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static AdminUserListItem ToAdminUserListItem(AdminUserMaterialized user)
    {
        var highestRole = user.HighestPriorityRole;
        return new AdminUserListItem(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.Initials,
            user.RoleIds,
            user.RoleNames,
            highestRole?.RoleId ?? Guid.Empty,
            highestRole?.Name ?? "Unassigned",
            highestRole?.Priority ?? int.MaxValue,
            user.GroupIds,
            user.GroupNames,
            user.AccountStatus,
            ToUtc(user.LastActiveAtUtc),
            Utc(user.CreatedAtUtc),
            Utc(user.UpdatedAtUtc));
    }

    private static AdminUserDetails ToAdminUserDetails(AdminUserMaterialized user)
    {
        return new AdminUserDetails(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.Initials,
            user.RoleIds,
            user.GroupIds,
            user.AccountStatus,
            ToUtc(user.LastActiveAtUtc),
            Utc(user.CreatedAtUtc),
            Utc(user.UpdatedAtUtc));
    }

    private static RoleSummary ToRoleSummary(RoleMaterialized role)
    {
        return new RoleSummary(
            role.RoleId,
            role.Name,
            role.Type,
            role.Scope,
            role.AssignedUserCount,
            BuildPermissionSummary(role.Permissions),
            role.IsProtected ? "Protected" : role.Status,
            role.IsProtected,
            !role.IsProtected && role.Scope == "Tenant");
    }

    private static RoleDetails ToRoleDetails(RoleMaterialized role)
    {
        return new RoleDetails(
            role.RoleId,
            role.Name,
            role.Type,
            role.Scope,
            role.Priority,
            role.IsProtected ? "Protected" : role.Status,
            role.IsProtected,
            !role.IsProtected && role.Scope == "Tenant",
            role.Permissions.Select(permission => permission.PermissionId).ToArray());
    }

    private static RoleUserAssignmentPreviewItem ToRoleUserAssignmentPreviewItem(AdminUserMaterialized user)
    {
        return new RoleUserAssignmentPreviewItem(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.DepartmentName,
            user.HighestPriorityRole?.Name,
            user.AccountStatus);
    }

    private static AdminNotificationEventListItem ToNotificationEventListItem(NotificationEventRow row)
    {
        return new AdminNotificationEventListItem(
            row.EventId,
            row.EventCode,
            row.Name,
            row.Recipient,
            row.TemplateName,
            row.LifecycleStatus,
            Utc(row.UpdatedAtUtc));
    }

    private static NotificationTemplateSummary ToNotificationTemplateSummary(NotificationTemplateRow row)
    {
        return new NotificationTemplateSummary(
            row.TemplateId,
            row.EventCode,
            row.Name,
            row.Recipient,
            row.Subject,
            row.Body,
            ParseStringArray(row.AllowedVariablesJson),
            row.LifecycleStatus,
            Utc(row.UpdatedAtUtc),
            row.UpdatedByUserId ?? Guid.Empty);
    }

    private static string BuildPermissionSummary(IReadOnlyCollection<PermissionMaterialized> permissions)
    {
        if (permissions.Count == 0)
        {
            return "No permissions";
        }

        var selected = permissions.Select(permission => permission.DisplayName).Take(3).ToArray();
        var suffix = permissions.Count > selected.Length ? $" +{permissions.Count - selected.Length} more" : string.Empty;
        return string.Join(", ", selected) + suffix;
    }

    private static IReadOnlyList<string> ParseStringArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static object SearchParameters(Guid tenantId, string? search)
    {
        return new
        {
            TenantId = tenantId,
            Search = EmptyToNull(search),
            SearchLike = Like(search)
        };
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Like(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : $"%{value.Trim()}%";
    }

    private static DateTimeOffset Utc(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static DateTimeOffset? ToUtc(DateTime? value)
    {
        return value.HasValue ? Utc(value.Value) : null;
    }

    private static string BuildInitials(string displayName)
    {
        var initials = displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]))
            .ToArray();

        return initials.Length == 0 ? "U" : new string(initials);
    }

    private static string BuildRoleCode(string roleName)
    {
        var code = new string(roleName.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(code) ? $"Role{Guid.NewGuid():N}"[..12] : code;
    }

    private sealed class AdminUserMaterialized
    {
        public Guid UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Initials { get; init; } = string.Empty;
        public string AccountStatus { get; init; } = string.Empty;
        public DateTime? LastActiveAtUtc { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public Guid? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public List<UserRoleMaterialized> Roles { get; } = [];
        public List<UserGroupMaterialized> Groups { get; } = [];
        public IReadOnlyList<Guid> RoleIds => Roles.Select(role => role.RoleId).ToArray();
        public IReadOnlyList<string> RoleNames => Roles.Select(role => role.Name).ToArray();
        public IReadOnlyList<Guid> GroupIds => Groups.Select(group => group.GroupId).ToArray();
        public IReadOnlyList<string> GroupNames => Groups.Select(group => group.Name).ToArray();
        public UserRoleMaterialized? HighestPriorityRole => Roles.OrderBy(role => role.Priority).FirstOrDefault();
        public bool IsInternalUser => Roles.All(role => role.Code != "Candidate" && role.Scope != "Portal");
    }

    private sealed class RoleMaterialized
    {
        public Guid RoleId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Scope { get; init; } = string.Empty;
        public int Priority { get; init; }
        public bool IsProtected { get; init; }
        public string Status { get; init; } = string.Empty;
        public int AssignedUserCount { get; set; }
        public List<PermissionMaterialized> Permissions { get; } = [];
    }

    private sealed record UserRoleMaterialized(Guid RoleId, string Code, string Name, int Priority, string Scope);
    private sealed record UserGroupMaterialized(Guid GroupId, string Name);
    private sealed record PermissionMaterialized(string PermissionId, string DisplayName);
    private sealed record UserRow(Guid UserId, string DisplayName, string Email, string Initials, string AccountStatus, DateTime? LastActiveAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, Guid? DepartmentId, string? DepartmentName);
    private sealed record UserRoleRow(Guid UserId, Guid RoleId, string Code, string Name, int Priority, string Scope);
    private sealed record UserGroupRow(Guid UserId, Guid GroupId, string Name);
    private sealed record BenchPolicyRow(Guid RoleId, string RoleName, DateTime UpdatedAtUtc, Guid? UpdatedByUserId);
    private sealed record PermissionPolicyRow(string Mode, DateTime UpdatedAtUtc, Guid? UpdatedByUserId);
    private sealed record RoleRow(Guid RoleId, string Name, string Type, string Scope, int Priority, bool IsProtected, string Status);
    private sealed record RolePermissionRow(Guid RoleId, string PermissionId, string DisplayName);
    private sealed record RoleAssignmentCountRow(Guid RoleId, int AssignedUserCount);
    private sealed record NotificationEventRow(Guid EventId, string EventCode, string Name, string Recipient, string TemplateName, string LifecycleStatus, DateTime UpdatedAtUtc);
    private sealed record NotificationEventDetailsRow(Guid EventId, string EventCode, string Name, string Recipient, string LifecycleStatus);
    private sealed record NotificationTemplateRow(Guid TemplateId, string EventCode, string Name, string Recipient, string Subject, string Body, string AllowedVariablesJson, string LifecycleStatus, DateTime UpdatedAtUtc, Guid? UpdatedByUserId);
    private sealed record AuditLogListRow(Guid Id, DateTime OccurredAtUtc, string ActorDisplayName, string EventSummary, string RecordLabel, string Area);
    private sealed record AuditLogDetailsRow(Guid Id, DateTime OccurredAtUtc, Guid? ActorUserId, string ActorDisplayName, string EventType, string EntityType, Guid? EntityId, string RecordLabel, string EventSummary, string Area, string MetadataJson);
}
