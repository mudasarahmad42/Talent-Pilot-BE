using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Skills;

public interface IAdminSkillsService
{
    Task<Result<AdminSkillsResponse>> ListAsync(AdminSkillsQuery query, CancellationToken cancellationToken);

    Task<Result<AdminSkillListItem>> CreateAsync(CreateSkillInput input, CancellationToken cancellationToken);

    Task<Result<AdminSkillListItem>> UpdateAsync(Guid skillId, UpdateSkillInput input, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid skillId, CancellationToken cancellationToken);
}

public interface IAdminSkillsRepository
{
    Task<AdminSkillsResponse> ListAsync(Guid tenantId, AdminSkillsQuery query, CancellationToken cancellationToken);

    Task<AdminSkillListItem?> GetSkillAsync(Guid tenantId, Guid skillId, CancellationToken cancellationToken);

    Task<bool> SkillNormalizedNameExistsAsync(
        Guid tenantId,
        string normalizedName,
        CancellationToken cancellationToken);

    Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateSkillInput input,
        string normalizedName,
        string aliasesJson,
        string metadataJson,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid skillId,
        UpdateSkillInput input,
        string normalizedName,
        string aliasesJson,
        string metadataJson,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid skillId,
        string skillName,
        string metadataJson,
        CancellationToken cancellationToken);
}
