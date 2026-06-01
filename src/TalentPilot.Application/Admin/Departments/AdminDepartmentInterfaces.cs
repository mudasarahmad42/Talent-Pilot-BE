using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Departments;

public interface IAdminDepartmentsService
{
    Task<Result<AdminDepartmentsResponse>> ListAsync(AdminDepartmentsQuery query, CancellationToken cancellationToken);

    Task<Result<AdminDepartmentListItem>> CreateAsync(CreateDepartmentInput input, CancellationToken cancellationToken);
}

public interface IAdminDepartmentsRepository
{
    Task<AdminDepartmentsResponse> ListAsync(
        Guid tenantId,
        AdminDepartmentsQuery query,
        CancellationToken cancellationToken);

    Task<AdminDepartmentListItem?> GetDepartmentAsync(
        Guid tenantId,
        Guid departmentId,
        CancellationToken cancellationToken);

    Task<bool> DepartmentCodeOrNameExistsAsync(
        Guid tenantId,
        string code,
        string name,
        CancellationToken cancellationToken);

    Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateDepartmentInput input,
        string metadataJson,
        CancellationToken cancellationToken);
}
