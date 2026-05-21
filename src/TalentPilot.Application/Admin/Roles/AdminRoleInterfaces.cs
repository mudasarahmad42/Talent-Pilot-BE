using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Roles;

public interface IAdminRolesService
{
    Task<Result<AdminRolesResponse>> ListAsync(AdminRolesQuery query, CancellationToken cancellationToken);

    Task<Result<RoleDetails>> GetAsync(Guid roleId, CancellationToken cancellationToken);

    Task<Result<RoleDetails>> CreateAsync(SaveRoleInput input, CancellationToken cancellationToken);

    Task<Result<RoleDetails>> UpdateAsync(Guid roleId, SaveRoleInput input, CancellationToken cancellationToken);

    Task<Result> UpdateStatusAsync(Guid roleId, UpdateRoleStatusInput input, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PermissionCatalogItem>>> ListPermissionsAsync(CancellationToken cancellationToken);

    Task<Result<PermissionResolutionPolicy>> GetPermissionResolutionPolicyAsync(CancellationToken cancellationToken);

    Task<Result<PermissionResolutionPolicy>> UpdatePermissionResolutionPolicyAsync(UpdatePermissionResolutionPolicyInput input, CancellationToken cancellationToken);

    Task<Result<RoleUserAssignmentPreview>> PreviewUserAssignmentsAsync(Guid roleId, RoleUserAssignmentFilterInput input, CancellationToken cancellationToken);

    Task<Result<BulkAssignRoleUsersResponse>> BulkAssignUsersAsync(Guid roleId, BulkAssignRoleUsersInput input, CancellationToken cancellationToken);
}

public interface IAdminRolesRepository
{
    Task<AdminRolesResponse> ListAsync(Guid tenantId, AdminRolesQuery query, CancellationToken cancellationToken);

    Task<RoleDetails?> GetAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PermissionCatalogItem>> ListPermissionsAsync(CancellationToken cancellationToken);

    Task<bool> PermissionIdsExistAsync(IReadOnlyCollection<string> permissionIds, CancellationToken cancellationToken);

    Task<bool> RoleNameExistsAsync(Guid tenantId, string name, Guid? exceptRoleId, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(Guid tenantId, Guid actorUserId, SaveRoleInput input, string metadataJson, CancellationToken cancellationToken);

    Task UpdateAsync(Guid tenantId, Guid actorUserId, Guid roleId, SaveRoleInput input, string metadataJson, CancellationToken cancellationToken);

    Task UpdateStatusAsync(Guid tenantId, Guid actorUserId, Guid roleId, string status, string metadataJson, CancellationToken cancellationToken);

    Task<PermissionResolutionPolicy?> GetPermissionResolutionPolicyAsync(Guid tenantId, CancellationToken cancellationToken);

    Task UpdatePermissionResolutionPolicyAsync(Guid tenantId, Guid actorUserId, string mode, CancellationToken cancellationToken);

    Task<RoleUserAssignmentPreview> PreviewUserAssignmentsAsync(Guid tenantId, Guid roleId, RoleUserAssignmentFilterInput input, CancellationToken cancellationToken);

    Task<BulkAssignRoleUsersResponse> BulkAssignUsersAsync(Guid tenantId, Guid actorUserId, Guid roleId, BulkAssignRoleUsersInput input, CancellationToken cancellationToken);
}
