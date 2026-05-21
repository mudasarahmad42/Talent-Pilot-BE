using System.Text.Json;
using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Roles;

public sealed class AdminRolesService : IAdminRolesService
{
    private static readonly string[] ValidStatuses = ["Active", "Inactive"];
    private static readonly string[] ValidPolicyModes = ["MergeAllAssignedRoles", "HighestPriorityRoleOnly"];

    private readonly IAdminRolesRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminRolesService(IAdminRolesRepository repository, ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminRolesResponse>> ListAsync(AdminRolesQuery query, CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100)
        };

        var response = await _repository.ListAsync(_currentUser.TenantId, normalized, cancellationToken);
        return Result<AdminRolesResponse>.Success(response);
    }

    public async Task<Result<RoleDetails>> GetAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await _repository.GetAsync(_currentUser.TenantId, roleId, cancellationToken);
        return role is null
            ? Result<RoleDetails>.Failure("admin_roles.not_found", "Role was not found.")
            : Result<RoleDetails>.Success(role);
    }

    public async Task<Result<RoleDetails>> CreateAsync(SaveRoleInput input, CancellationToken cancellationToken)
    {
        var validation = await ValidateSaveInputAsync(input, null, cancellationToken);
        if (validation.Failed)
        {
            return Result<RoleDetails>.Failure(validation.Error.Code, validation.Error.Message);
        }

        var metadataJson = JsonSerializer.Serialize(new { action = "create", input.Name, input.PermissionIds });
        var roleId = await _repository.CreateAsync(_currentUser.TenantId, _currentUser.UserId, input, metadataJson, cancellationToken);
        return await GetAsync(roleId, cancellationToken);
    }

    public async Task<Result<RoleDetails>> UpdateAsync(Guid roleId, SaveRoleInput input, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetAsync(_currentUser.TenantId, roleId, cancellationToken);
        if (existing is null)
        {
            return Result<RoleDetails>.Failure("admin_roles.not_found", "Role was not found.");
        }

        if (existing.IsProtected)
        {
            return Result<RoleDetails>.Failure("admin_roles.protected", "Protected system roles cannot be changed from Admin Center.");
        }

        var validation = await ValidateSaveInputAsync(input, roleId, cancellationToken);
        if (validation.Failed)
        {
            return Result<RoleDetails>.Failure(validation.Error.Code, validation.Error.Message);
        }

        var metadataJson = JsonSerializer.Serialize(new { action = "update", input.Name, input.PermissionIds });
        await _repository.UpdateAsync(_currentUser.TenantId, _currentUser.UserId, roleId, input, metadataJson, cancellationToken);
        return await GetAsync(roleId, cancellationToken);
    }

    public async Task<Result> UpdateStatusAsync(Guid roleId, UpdateRoleStatusInput input, CancellationToken cancellationToken)
    {
        if (!ValidStatuses.Contains(input.Status, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("admin_roles.status_invalid", "Role lifecycle status must be Active or Inactive.");
        }

        var existing = await _repository.GetAsync(_currentUser.TenantId, roleId, cancellationToken);
        if (existing is null)
        {
            return Result.Failure("admin_roles.not_found", "Role was not found.");
        }

        if (existing.IsProtected)
        {
            return Result.Failure("admin_roles.protected", "Protected system roles cannot be deactivated.");
        }

        var metadataJson = JsonSerializer.Serialize(new { action = "status", input.Status });
        await _repository.UpdateStatusAsync(_currentUser.TenantId, _currentUser.UserId, roleId, input.Status, metadataJson, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<PermissionCatalogItem>>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        var permissions = await _repository.ListPermissionsAsync(cancellationToken);
        return Result<IReadOnlyList<PermissionCatalogItem>>.Success(permissions);
    }

    public async Task<Result<PermissionResolutionPolicy>> GetPermissionResolutionPolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await _repository.GetPermissionResolutionPolicyAsync(_currentUser.TenantId, cancellationToken);
        return policy is null
            ? Result<PermissionResolutionPolicy>.Failure("permission_resolution.not_found", "Permission resolution policy was not found.")
            : Result<PermissionResolutionPolicy>.Success(policy);
    }

    public async Task<Result<PermissionResolutionPolicy>> UpdatePermissionResolutionPolicyAsync(
        UpdatePermissionResolutionPolicyInput input,
        CancellationToken cancellationToken)
    {
        if (!ValidPolicyModes.Contains(input.Mode, StringComparer.OrdinalIgnoreCase))
        {
            return Result<PermissionResolutionPolicy>.Failure("permission_resolution.mode_invalid", "Mode must be MergeAllAssignedRoles or HighestPriorityRoleOnly.");
        }

        await _repository.UpdatePermissionResolutionPolicyAsync(_currentUser.TenantId, _currentUser.UserId, input.Mode, cancellationToken);
        return await GetPermissionResolutionPolicyAsync(cancellationToken);
    }

    public async Task<Result<RoleUserAssignmentPreview>> PreviewUserAssignmentsAsync(
        Guid roleId,
        RoleUserAssignmentFilterInput input,
        CancellationToken cancellationToken)
    {
        var role = await _repository.GetAsync(_currentUser.TenantId, roleId, cancellationToken);
        if (role is null)
        {
            return Result<RoleUserAssignmentPreview>.Failure("admin_roles.not_found", "Role was not found.");
        }

        if (!role.IsBulkAssignable)
        {
            return Result<RoleUserAssignmentPreview>.Failure("admin_roles.not_bulk_assignable", "This role cannot be bulk assigned.");
        }

        var preview = await _repository.PreviewUserAssignmentsAsync(_currentUser.TenantId, roleId, input, cancellationToken);
        return Result<RoleUserAssignmentPreview>.Success(preview);
    }

    public async Task<Result<BulkAssignRoleUsersResponse>> BulkAssignUsersAsync(
        Guid roleId,
        BulkAssignRoleUsersInput input,
        CancellationToken cancellationToken)
    {
        var role = await _repository.GetAsync(_currentUser.TenantId, roleId, cancellationToken);
        if (role is null)
        {
            return Result<BulkAssignRoleUsersResponse>.Failure("admin_roles.not_found", "Role was not found.");
        }

        if (!role.IsBulkAssignable)
        {
            return Result<BulkAssignRoleUsersResponse>.Failure("admin_roles.not_bulk_assignable", "This role cannot be bulk assigned.");
        }

        if (input.SelectionMode is not "AllFilteredUsers" and not "SelectedUsers")
        {
            return Result<BulkAssignRoleUsersResponse>.Failure("admin_roles.selection_mode_invalid", "Selection mode must be AllFilteredUsers or SelectedUsers.");
        }

        if (input.SelectionMode == "SelectedUsers" && (input.SelectedUserIds is null || input.SelectedUserIds.Count == 0))
        {
            return Result<BulkAssignRoleUsersResponse>.Failure("admin_roles.selected_users_required", "Selected users are required.");
        }

        var result = await _repository.BulkAssignUsersAsync(_currentUser.TenantId, _currentUser.UserId, roleId, input, cancellationToken);
        return Result<BulkAssignRoleUsersResponse>.Success(result);
    }

    private async Task<Result> ValidateSaveInputAsync(SaveRoleInput input, Guid? existingRoleId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length < 2)
        {
            return Result.Failure("admin_roles.name_invalid", "Role name must be at least 2 characters.");
        }

        if (input.Scope != "Tenant")
        {
            return Result.Failure("admin_roles.scope_invalid", "Admin Center can create tenant-scoped roles only.");
        }

        if (input.Priority < 1)
        {
            return Result.Failure("admin_roles.priority_invalid", "Role priority must be 1 or greater.");
        }

        if (!ValidStatuses.Contains(input.Status, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("admin_roles.status_invalid", "Role lifecycle status must be Active or Inactive.");
        }

        if (input.PermissionIds.Count == 0)
        {
            return Result.Failure("admin_roles.permissions_required", "At least one permission is required.");
        }

        if (await _repository.RoleNameExistsAsync(_currentUser.TenantId, input.Name, existingRoleId, cancellationToken))
        {
            return Result.Failure("admin_roles.name_duplicate", "Role name already exists in this tenant.");
        }

        if (!await _repository.PermissionIdsExistAsync(input.PermissionIds, cancellationToken))
        {
            return Result.Failure("admin_roles.permissions_invalid", "All permission ids must exist in the active permission catalog.");
        }

        return Result.Success();
    }
}
