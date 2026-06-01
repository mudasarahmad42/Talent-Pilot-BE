using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.HiringPipelines;

public interface IAdminHiringPipelinesService
{
    Task<Result<AdminHiringPipelineTemplatesResponse>> ListTemplatesAsync(
        AdminHiringPipelineTemplatesQuery query,
        CancellationToken cancellationToken);

    Task<Result<AdminHiringPipelineTemplateDetails>> GetTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken);

    Task<Result<AdminHiringPipelineTemplateDetails>> CreateTemplateAsync(
        CreateAdminHiringPipelineTemplateInput input,
        CancellationToken cancellationToken);

    Task<Result<AdminHiringPipelineTemplateDetails>> UpdateTemplateAsync(
        Guid templateId,
        UpdateAdminHiringPipelineTemplateInput input,
        CancellationToken cancellationToken);
}

public interface IAdminHiringPipelinesRepository
{
    Task<AdminHiringPipelineTemplatesResponse> ListTemplatesAsync(
        Guid tenantId,
        AdminHiringPipelineTemplatesQuery query,
        CancellationToken cancellationToken);

    Task<AdminHiringPipelineTemplateDetails?> GetHiringPipelineTemplateAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken cancellationToken);

    Task CreateTemplateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid templateId,
        UpdateAdminHiringPipelineTemplateInput input,
        string metadataJson,
        CancellationToken cancellationToken);

    Task UpdateTemplateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid templateId,
        UpdateAdminHiringPipelineTemplateInput input,
        string metadataJson,
        CancellationToken cancellationToken);

    Task<bool> TemplateNameExistsAsync(
        Guid tenantId,
        string name,
        Guid exceptTemplateId,
        CancellationToken cancellationToken);

    Task<bool> DepartmentExistsAsync(
        Guid tenantId,
        Guid departmentId,
        CancellationToken cancellationToken);

    Task<bool> RoleIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken);

    Task<bool> ActiveUserIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}
