using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.CandidateSources;

public interface IAdminCandidateSourcesService
{
    Task<Result<AdminCandidateSourcesResponse>> ListAsync(
        AdminCandidateSourcesQuery query,
        CancellationToken cancellationToken);
}

public interface IAdminCandidateSourcesRepository
{
    Task<AdminCandidateSourcesResponse> ListAsync(
        Guid tenantId,
        AdminCandidateSourcesQuery query,
        CancellationToken cancellationToken);
}
