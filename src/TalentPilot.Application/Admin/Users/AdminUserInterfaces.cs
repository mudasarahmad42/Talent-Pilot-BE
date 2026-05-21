using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Users;

public interface IAdminUsersService
{
    Task<Result<AdminUsersResponse>> ListAsync(AdminUsersQuery query, CancellationToken cancellationToken);

    Task<Result<AdminUserDetails>> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<AdminUserDetails>> CreateAsync(SaveAdminUserInput input, CancellationToken cancellationToken);

    Task<Result<AdminUserDetails>> UpdateAsync(Guid userId, SaveAdminUserInput input, CancellationToken cancellationToken);

    Task<Result> UpdateStatusAsync(Guid userId, UpdateAdminUserStatusInput input, CancellationToken cancellationToken);

    Task<Result> ResendInviteAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IAdminAccessPoliciesService
{
    Task<Result<BenchVisibilityPolicy>> GetBenchVisibilityPolicyAsync(CancellationToken cancellationToken);

    Task<Result<BenchVisibilityPolicy>> UpdateBenchVisibilityPolicyAsync(UpdateBenchVisibilityPolicyInput input, CancellationToken cancellationToken);
}

public interface IAdminUsersRepository
{
    Task<AdminUsersResponse> ListAsync(Guid tenantId, AdminUsersQuery query, CancellationToken cancellationToken);

    Task<AdminUserDetails?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<Guid?> FindRoleIdByCodeAsync(Guid tenantId, string roleCode, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(Guid tenantId, string email, Guid? exceptUserId, CancellationToken cancellationToken);

    Task<bool> ActiveRolesExistAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);

    Task<bool> ActiveGroupsExistAsync(Guid tenantId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken);

    Task<int> CountActiveTenantAdminsAsync(Guid tenantId, Guid? exceptUserId, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(Guid tenantId, Guid actorUserId, SaveAdminUserInput input, string metadataJson, CancellationToken cancellationToken);

    Task UpdateAsync(Guid tenantId, Guid actorUserId, Guid userId, SaveAdminUserInput input, string metadataJson, CancellationToken cancellationToken);

    Task UpdateStatusAsync(Guid tenantId, Guid actorUserId, Guid userId, UpdateAdminUserStatusInput input, string metadataJson, CancellationToken cancellationToken);

    Task InsertInviteNotificationAsync(Guid tenantId, Guid actorUserId, Guid userId, CancellationToken cancellationToken);
}

public interface IAdminAccessPoliciesRepository
{
    Task<BenchVisibilityPolicy?> GetBenchVisibilityPolicyAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<bool> RoleIsActiveAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);

    Task UpdateBenchVisibilityPolicyAsync(Guid tenantId, Guid actorUserId, Guid roleId, CancellationToken cancellationToken);
}
