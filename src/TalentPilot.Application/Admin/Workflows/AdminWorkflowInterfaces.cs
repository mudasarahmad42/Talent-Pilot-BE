using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Workflows;

public interface IAdminWorkflowsService
{
    Task<Result<AdminWorkflowConfigurationResponse>> GetConfigurationAsync(CancellationToken cancellationToken);

    Task<Result<AdminWorkflowConfigurationResponse>> UpdateIntakeRoutingAsync(
        UpdateAdminWorkflowIntakeRoutingInput input,
        CancellationToken cancellationToken);
}

public interface IAdminWorkflowsRepository
{
    Task<AdminWorkflowConfigurationResponse> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken);

    Task UpdateIntakeRoutingAsync(
        Guid tenantId,
        Guid actorUserId,
        UpdateAdminWorkflowIntakeRoutingInput input,
        string metadataJson,
        CancellationToken cancellationToken);

    Task<bool> ActiveDepartmentIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> departmentIds,
        CancellationToken cancellationToken);

    Task<bool> ActiveUserIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    Task<bool> ActiveGroupIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken);
}
