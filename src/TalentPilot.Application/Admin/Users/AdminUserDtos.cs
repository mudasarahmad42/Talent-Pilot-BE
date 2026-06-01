namespace TalentPilot.Application.Admin.Users;

public sealed record AdminUsersQuery(
    string? Search,
    Guid? RoleId,
    Guid? GroupId,
    string? AccountStatus,
    int Page,
    int PageSize);

public sealed record AdminUsersResponse(
    AdminUsersSummary Summary,
    IReadOnlyList<AdminUserListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminUsersSummary(
    int InternalUserCount,
    int RoutingGroupCount,
    BenchVisibilityPolicySummary BenchVisibilityPolicy);

public sealed record BenchVisibilityPolicySummary(
    Guid RoleId,
    string RoleName,
    string ConfiguredIn);

public sealed record AdminUserListItem(
    Guid Id,
    string DisplayName,
    string Email,
    string Initials,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<string> RoleNames,
    Guid HighestPriorityRoleId,
    string HighestPriorityRoleName,
    int HighestPriorityRolePriority,
    IReadOnlyList<Guid> GroupIds,
    IReadOnlyList<string> GroupNames,
    Guid? DepartmentId,
    string? DepartmentName,
    decimal? ExperienceYears,
    DateOnly? JoiningDate,
    int CompletedInterviewCount,
    string AccountStatus,
    DateTimeOffset? LastActiveAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminUserDetails(
    Guid Id,
    string DisplayName,
    string Email,
    string Initials,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<Guid> GroupIds,
    string AccountStatus,
    DateTimeOffset? LastActiveAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SaveAdminUserInput(
    string DisplayName,
    string Email,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<Guid> GroupIds,
    string AccountStatus);

public sealed record UpdateAdminUserStatusInput(string AccountStatus, string? Reason);

public sealed record BenchVisibilityPolicy(
    Guid RoleId,
    string RoleName,
    DateTimeOffset UpdatedAt,
    Guid UpdatedByUserId);

public sealed record UpdateBenchVisibilityPolicyInput(Guid RoleId);
