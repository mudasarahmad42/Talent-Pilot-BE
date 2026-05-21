namespace TalentPilot.Application.Admin.Groups;

public sealed record AdminGroupsQuery(string? Purpose, int Page, int PageSize);

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
