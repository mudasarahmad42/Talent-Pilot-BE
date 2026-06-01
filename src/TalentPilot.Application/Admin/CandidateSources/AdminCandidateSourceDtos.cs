namespace TalentPilot.Application.Admin.CandidateSources;

public sealed record AdminCandidateSourcesQuery(string? Search, int Page, int PageSize);

public sealed record AdminCandidateSourcesResponse(
    AdminCandidateSourcesSummary Summary,
    IReadOnlyList<AdminCandidateSourceListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminCandidateSourcesSummary(
    int ActiveSourceCount,
    int ReportingCategoryCount,
    int InactiveSourceCount);

public sealed record AdminCandidateSourceListItem(
    Guid CandidateSourceLabelId,
    string Code,
    string DisplayName,
    string ReportingCategory,
    string Status,
    DateTimeOffset UpdatedAtUtc);
