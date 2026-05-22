using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Integrations;

public interface IAdminIntegrationsService
{
    Task<Result<AdminIntegrationStatusResponse>> GetStatusAsync(CancellationToken cancellationToken);
}
