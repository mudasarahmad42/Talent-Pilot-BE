using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.CandidateSources;

public sealed class AdminCandidateSourcesService : IAdminCandidateSourcesService
{
    private readonly IAdminCandidateSourcesRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminCandidateSourcesService(
        IAdminCandidateSourcesRepository repository,
        ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminCandidateSourcesResponse>> ListAsync(
        AdminCandidateSourcesQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100)
        };

        var response = await _repository.ListAsync(_currentUser.TenantId, normalized, cancellationToken);
        return Result<AdminCandidateSourcesResponse>.Success(response);
    }
}
