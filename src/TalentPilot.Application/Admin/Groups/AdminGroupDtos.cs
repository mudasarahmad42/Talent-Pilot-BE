namespace TalentPilot.Application.Admin.Groups;

public sealed record AdminGroupsQuery(string? Purpose, string? Search, int Page, int PageSize);

public sealed record AdminGroupMembershipQuery(string? Search, string? Membership, int Page, int PageSize);

public sealed record AdminGroupsResponse(
    IReadOnlyList<AdminGroupListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminGroupListItem(
    Guid GroupId,
    string Name,
    string Purpose,
    string Status,
    int MemberCount);

public sealed record CreateGroupInput(
    string Name,
    string Purpose,
    string Status);

public sealed record AdminGroupMembershipResponse(
    AdminGroupListItem Group,
    AdminGroupMembershipSummary Summary,
    IReadOnlyList<AdminGroupMembershipUser> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminGroupMembershipSummary(
    int MemberCount,
    int AvailableUserCount,
    int FilteredMemberCount,
    int FilteredAvailableUserCount);

public sealed record AdminGroupMembershipUser(
    Guid UserId,
    string DisplayName,
    string Email,
    string Initials,
    IReadOnlyList<string> RoleNames,
    string AccountStatus,
    bool IsMember,
    bool IsDefaultAssignee);

public sealed record UpdateGroupMembersInput(
    IReadOnlyList<Guid> UserIdsToAdd,
    IReadOnlyList<Guid> UserIdsToRemove,
    BulkGroupMembershipSelection? BulkSelection);

public sealed record BulkGroupMembershipSelection(
    string Mode,
    string? Search,
    string? Membership);

public sealed record UpdateGroupMembersResult(
    int AddedCount,
    int RemovedCount,
    int MemberCount);
