using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Groups;

public sealed class AdminGroupsService : IAdminGroupsService
{
    private readonly IAdminGroupsRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminGroupsService(IAdminGroupsRepository repository, ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminGroupsResponse>> ListAsync(AdminGroupsQuery query, CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 100)
        };

        var response = await _repository.ListAsync(_currentUser.TenantId, normalized, cancellationToken);
        return Result<AdminGroupsResponse>.Success(response);
    }
}
