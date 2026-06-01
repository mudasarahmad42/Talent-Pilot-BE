using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Groups;

public interface IAdminGroupsService
{
    Task<Result<AdminGroupsResponse>> ListAsync(AdminGroupsQuery query, CancellationToken cancellationToken);

    Task<Result<AdminGroupListItem>> CreateAsync(CreateGroupInput input, CancellationToken cancellationToken);

    Task<Result<AdminGroupMembershipResponse>> ListMembershipAsync(
        Guid groupId,
        AdminGroupMembershipQuery query,
        CancellationToken cancellationToken);

    Task<Result<UpdateGroupMembersResult>> UpdateMembershipAsync(
        Guid groupId,
        UpdateGroupMembersInput input,
        CancellationToken cancellationToken);
}

public interface IAdminGroupsRepository
{
    Task<AdminGroupsResponse> ListAsync(Guid tenantId, AdminGroupsQuery query, CancellationToken cancellationToken);

    Task<AdminGroupListItem?> GetGroupAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken);

    Task<bool> GroupNameExistsAsync(
        Guid tenantId,
        string purpose,
        string name,
        CancellationToken cancellationToken);

    Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateGroupInput input,
        string metadataJson,
        CancellationToken cancellationToken);

    Task<AdminGroupMembershipResponse> ListMembershipAsync(
        Guid tenantId,
        Guid groupId,
        AdminGroupMembershipQuery query,
        CancellationToken cancellationToken);

    Task<bool> InternalUsersExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    Task<UpdateGroupMembersResult> UpdateMembershipAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid groupId,
        UpdateGroupMembersInput input,
        string metadataJson,
        CancellationToken cancellationToken);
}
