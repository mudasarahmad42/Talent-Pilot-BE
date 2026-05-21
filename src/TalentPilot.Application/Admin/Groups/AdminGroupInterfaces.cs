using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Groups;

public interface IAdminGroupsService
{
    Task<Result<AdminGroupsResponse>> ListAsync(AdminGroupsQuery query, CancellationToken cancellationToken);
}

public interface IAdminGroupsRepository
{
    Task<AdminGroupsResponse> ListAsync(Guid tenantId, AdminGroupsQuery query, CancellationToken cancellationToken);
}
