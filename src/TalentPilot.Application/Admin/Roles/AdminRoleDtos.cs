namespace TalentPilot.Application.Admin.Roles;

public sealed record AdminRolesQuery(string? Search, int Page, int PageSize, bool IncludeInactive = true);

public sealed record AdminRolesResponse(
    AdminRolesSummary Summary,
    IReadOnlyList<RoleSummary> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminRolesSummary(int ActiveRoleCount, int ProtectedRoleCount, int CustomRoleCount);

public sealed record RoleSummary(
    Guid RoleId,
    string Name,
    string Type,
    string Scope,
    int AssignedUserCount,
    string PermissionSummary,
    string LifecycleStatus,
    bool IsProtected,
    bool IsBulkAssignable);

public sealed record RoleDetails(
    Guid RoleId,
    string Name,
    string Type,
    string Scope,
    int Priority,
    string LifecycleStatus,
    bool IsProtected,
    bool IsBulkAssignable,
    IReadOnlyList<string> PermissionIds);

public sealed record SaveRoleInput(
    string Name,
    string Scope,
    int Priority,
    string Status,
    IReadOnlyList<string> PermissionIds);

public sealed record UpdateRoleStatusInput(string Status);

public sealed record PermissionCatalogItem(
    string PermissionId,
    string DisplayName,
    string GroupName,
    string Description,
    string Status);

public sealed record PermissionResolutionPolicy(
    string Mode,
    DateTimeOffset UpdatedAtUtc,
    Guid UpdatedByUserId);

public sealed record UpdatePermissionResolutionPolicyInput(string Mode);

public sealed record RoleUserAssignmentFilterInput(
    string? Search,
    IReadOnlyList<string>? AccountStatuses,
    IReadOnlyList<Guid>? DepartmentIds,
    IReadOnlyList<Guid>? CurrentRoleIds,
    IReadOnlyList<Guid>? GroupIds);

public sealed record RoleUserAssignmentPreview(
    int MatchedCount,
    int AlreadyAssignedCount,
    int AssignableCount,
    IReadOnlyList<RoleUserAssignmentPreviewItem> SampleUsers);

public sealed record RoleUserAssignmentPreviewItem(
    Guid UserId,
    string DisplayName,
    string Email,
    string? DepartmentName,
    string? CurrentHighestPriorityRoleName,
    string AccountStatus);

public sealed record BulkAssignRoleUsersInput(
    RoleUserAssignmentFilterInput Filters,
    string SelectionMode,
    IReadOnlyList<Guid>? SelectedUserIds,
    int ExpectedAssignableCount);

public sealed record BulkAssignRoleUsersResponse(
    Guid BatchId,
    int MatchedCount,
    int AssignedCount,
    int SkippedCount);
