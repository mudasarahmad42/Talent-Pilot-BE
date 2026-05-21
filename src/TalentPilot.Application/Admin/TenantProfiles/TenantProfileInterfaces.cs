using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.TenantProfiles;

public interface IAdminTenantProfileService
{
    Task<Result<TenantProfileSettings>> GetAsync(CancellationToken cancellationToken);

    Task<Result<TenantProfileSettings>> UpdateAsync(UpdateTenantProfileSettingsInput input, CancellationToken cancellationToken);

    Task<Result<SlugAvailabilityResponse>> CheckSlugAvailabilityAsync(string slug, CancellationToken cancellationToken);
}

public interface IAdminTenantProfileRepository
{
    Task<TenantProfileSettings?> GetAsync(
        Guid tenantId,
        string configuredLlmModel,
        string configuredEmbeddingModel,
        CancellationToken cancellationToken);

    Task<bool> IsSlugAvailableAsync(Guid tenantId, string slug, CancellationToken cancellationToken);

    Task UpdateAsync(
        Guid tenantId,
        Guid actorUserId,
        UpdateTenantProfileSettingsInput input,
        string metadataJson,
        CancellationToken cancellationToken);
}
