using System.Text.Json;
using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;
using TalentPilot.Domain.Access;

namespace TalentPilot.Application.Admin.Users;

public sealed class AdminUsersService : IAdminUsersService, IAdminAccessPoliciesService
{
    private static readonly string[] ValidStatuses = ["Active", "Disabled", "Invited"];

    private readonly IAdminUsersRepository _usersRepository;
    private readonly IAdminAccessPoliciesRepository _accessPoliciesRepository;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminUsersService(
        IAdminUsersRepository usersRepository,
        IAdminAccessPoliciesRepository accessPoliciesRepository,
        ICurrentUserAccessor currentUser)
    {
        _usersRepository = usersRepository;
        _accessPoliciesRepository = accessPoliciesRepository;
        _currentUser = currentUser;
    }

    public Task<Result<AdminUsersResponse>> ListAsync(AdminUsersQuery query, CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100)
        };

        return WrapAsync(() => _usersRepository.ListAsync(_currentUser.TenantId, normalized, cancellationToken));
    }

    public async Task<Result<AdminUserDetails>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _usersRepository.GetAsync(_currentUser.TenantId, userId, cancellationToken);
        return user is null
            ? Result<AdminUserDetails>.Failure("admin_users.not_found", "User was not found.")
            : Result<AdminUserDetails>.Success(user);
    }

    public async Task<Result<AdminUserDetails>> CreateAsync(SaveAdminUserInput input, CancellationToken cancellationToken)
    {
        var validation = await ValidateSaveInputAsync(input, null, cancellationToken);
        if (validation.Failed)
        {
            return Result<AdminUserDetails>.Failure(validation.Error.Code, validation.Error.Message);
        }

        var metadataJson = JsonSerializer.Serialize(new { action = "create", input.Email, input.RoleIds, input.GroupIds });
        var userId = await _usersRepository.CreateAsync(_currentUser.TenantId, _currentUser.UserId, input, metadataJson, cancellationToken);

        if (string.Equals(input.AccountStatus, "Invited", StringComparison.OrdinalIgnoreCase))
        {
            await _usersRepository.InsertInviteNotificationAsync(_currentUser.TenantId, _currentUser.UserId, userId, cancellationToken);
        }

        return await GetAsync(userId, cancellationToken);
    }

    public async Task<Result<AdminUserDetails>> UpdateAsync(
        Guid userId,
        SaveAdminUserInput input,
        CancellationToken cancellationToken)
    {
        var existing = await _usersRepository.GetAsync(_currentUser.TenantId, userId, cancellationToken);
        if (existing is null)
        {
            return Result<AdminUserDetails>.Failure("admin_users.not_found", "User was not found.");
        }

        var validation = await ValidateSaveInputAsync(input, userId, cancellationToken);
        if (validation.Failed)
        {
            return Result<AdminUserDetails>.Failure(validation.Error.Code, validation.Error.Message);
        }

        if (await WouldRemoveFinalTenantAdminAsync(userId, input.RoleIds, input.AccountStatus, cancellationToken))
        {
            return Result<AdminUserDetails>.Failure("admin_users.final_admin", "Tenant Admin cannot remove or disable the final active Tenant Admin.");
        }

        var metadataJson = JsonSerializer.Serialize(new { action = "update", input.Email, input.RoleIds, input.GroupIds });
        await _usersRepository.UpdateAsync(_currentUser.TenantId, _currentUser.UserId, userId, input, metadataJson, cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }

    public async Task<Result> UpdateStatusAsync(Guid userId, UpdateAdminUserStatusInput input, CancellationToken cancellationToken)
    {
        if (!ValidStatuses.Contains(input.AccountStatus, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("admin_users.status_invalid", "Account status must be Active, Disabled, or Invited.");
        }

        var existing = await _usersRepository.GetAsync(_currentUser.TenantId, userId, cancellationToken);
        if (existing is null)
        {
            return Result.Failure("admin_users.not_found", "User was not found.");
        }

        if (string.Equals(input.AccountStatus, "Disabled", StringComparison.OrdinalIgnoreCase) &&
            await _usersRepository.CountActiveTenantAdminsAsync(_currentUser.TenantId, userId, cancellationToken) == 0)
        {
            return Result.Failure("admin_users.final_admin", "Tenant Admin cannot disable the final active Tenant Admin.");
        }

        var metadataJson = JsonSerializer.Serialize(new { action = "status", input.AccountStatus, input.Reason });
        await _usersRepository.UpdateStatusAsync(_currentUser.TenantId, _currentUser.UserId, userId, input, metadataJson, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ResendInviteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await _usersRepository.GetAsync(_currentUser.TenantId, userId, cancellationToken);
        if (existing is null)
        {
            return Result.Failure("admin_users.not_found", "User was not found.");
        }

        await _usersRepository.InsertInviteNotificationAsync(_currentUser.TenantId, _currentUser.UserId, userId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<BenchVisibilityPolicy>> GetBenchVisibilityPolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await _accessPoliciesRepository.GetBenchVisibilityPolicyAsync(_currentUser.TenantId, cancellationToken);
        return policy is null
            ? Result<BenchVisibilityPolicy>.Failure("access_policy.not_found", "Bench visibility policy was not found.")
            : Result<BenchVisibilityPolicy>.Success(policy);
    }

    public async Task<Result<BenchVisibilityPolicy>> UpdateBenchVisibilityPolicyAsync(
        UpdateBenchVisibilityPolicyInput input,
        CancellationToken cancellationToken)
    {
        if (!await _accessPoliciesRepository.RoleIsActiveAsync(_currentUser.TenantId, input.RoleId, cancellationToken))
        {
            return Result<BenchVisibilityPolicy>.Failure("access_policy.role_invalid", "Bench visibility role must be an active tenant role.");
        }

        await _accessPoliciesRepository.UpdateBenchVisibilityPolicyAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            input.RoleId,
            cancellationToken);

        return await GetBenchVisibilityPolicyAsync(cancellationToken);
    }

    private async Task<Result> ValidateSaveInputAsync(
        SaveAdminUserInput input,
        Guid? existingUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Trim().Length < 2)
        {
            return Result.Failure("admin_users.display_name_invalid", "Display name must be at least 2 characters.");
        }

        if (string.IsNullOrWhiteSpace(input.Email) || !input.Email.Contains('@', StringComparison.Ordinal))
        {
            return Result.Failure("admin_users.email_invalid", "Email must be valid.");
        }

        if (input.RoleIds.Count == 0)
        {
            return Result.Failure("admin_users.roles_required", "At least one active role is required.");
        }

        if (!ValidStatuses.Contains(input.AccountStatus, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("admin_users.status_invalid", "Account status must be Active, Disabled, or Invited.");
        }

        if (await _usersRepository.EmailExistsAsync(_currentUser.TenantId, input.Email, existingUserId, cancellationToken))
        {
            return Result.Failure("admin_users.email_duplicate", "Email is already used in this tenant.");
        }

        if (!await _usersRepository.ActiveRolesExistAsync(_currentUser.TenantId, input.RoleIds, cancellationToken))
        {
            return Result.Failure("admin_users.roles_invalid", "All assigned roles must be active tenant roles.");
        }

        if (input.GroupIds.Count > 0 &&
            !await _usersRepository.ActiveGroupsExistAsync(_currentUser.TenantId, input.GroupIds, cancellationToken))
        {
            return Result.Failure("admin_users.groups_invalid", "All assigned groups must be active routing groups for the tenant.");
        }

        return Result.Success();
    }

    private async Task<bool> WouldRemoveFinalTenantAdminAsync(
        Guid userId,
        IReadOnlyCollection<Guid> newRoleIds,
        string accountStatus,
        CancellationToken cancellationToken)
    {
        if (string.Equals(accountStatus, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return await _usersRepository.CountActiveTenantAdminsAsync(_currentUser.TenantId, userId, cancellationToken) == 0;
        }

        var tenantAdminRoleId = await _usersRepository.FindRoleIdByCodeAsync(
            _currentUser.TenantId,
            AccessConstants.TenantAdminRoleCode,
            cancellationToken);

        return tenantAdminRoleId.HasValue &&
            !newRoleIds.Contains(tenantAdminRoleId.Value) &&
            await _usersRepository.CountActiveTenantAdminsAsync(_currentUser.TenantId, userId, cancellationToken) == 0;
    }

    private static async Task<Result<T>> WrapAsync<T>(Func<Task<T>> action)
    {
        return Result<T>.Success(await action());
    }
}
