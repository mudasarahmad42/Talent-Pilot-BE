namespace TalentPilot.Application.Admin.Departments;

public sealed record AdminDepartmentsQuery(string? Search, int Page, int PageSize);

public sealed record AdminDepartmentsResponse(
    AdminDepartmentsSummary Summary,
    IReadOnlyList<AdminDepartmentListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminDepartmentsSummary(
    int ActiveDepartmentCount,
    int TotalEmployeeCount,
    int OpenJobRequestCount,
    int InactiveDepartmentCount);

public sealed record AdminDepartmentListItem(
    Guid DepartmentId,
    string Code,
    string Name,
    string LeadName,
    int EmployeeCount,
    int OpenJobRequestCount,
    string Status);

public sealed record CreateDepartmentInput(
    string Code,
    string Name,
    string Status);
