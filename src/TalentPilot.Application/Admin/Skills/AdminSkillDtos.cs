namespace TalentPilot.Application.Admin.Skills;

public sealed record AdminSkillsQuery(string? Category, string? Search, int Page, int PageSize);

public sealed record AdminSkillsResponse(
    AdminSkillsSummary Summary,
    IReadOnlyList<AdminSkillListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminSkillsSummary(
    int ActiveSkillCount,
    int CategoryCount,
    int AliasCount);

public sealed record AdminSkillListItem(
    Guid SkillId,
    string Name,
    string NormalizedName,
    string Category,
    IReadOnlyList<string> Aliases,
    string Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateSkillInput(
    string Name,
    string Category,
    IReadOnlyList<string> Aliases,
    string Status);

public sealed record UpdateSkillInput(
    string Name,
    string Category,
    IReadOnlyList<string> Aliases,
    string Status);
