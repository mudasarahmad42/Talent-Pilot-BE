using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;
using System.Text.Json;

namespace TalentPilot.Application.Admin.Workflows;

public sealed class AdminWorkflowsService : IAdminWorkflowsService
{
    private const int MaxIntakeRoutingRules = 250;

    private readonly IAdminWorkflowsRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminWorkflowsService(
        IAdminWorkflowsRepository repository,
        ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminWorkflowConfigurationResponse>> GetConfigurationAsync(
        CancellationToken cancellationToken)
    {
        var response = await _repository.GetConfigurationAsync(_currentUser.TenantId, cancellationToken);
        return Result<AdminWorkflowConfigurationResponse>.Success(response);
    }

    public async Task<Result<AdminWorkflowConfigurationResponse>> UpdateIntakeRoutingAsync(
        UpdateAdminWorkflowIntakeRoutingInput input,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateIntakeRoutingInputAsync(input, cancellationToken);
        if (validation.Failed)
        {
            return Result<AdminWorkflowConfigurationResponse>.Failure(
                validation.Error.Code,
                validation.Error.Message);
        }

        var normalizedRules = input.Rules
            .DistinctBy(rule => rule.DepartmentId)
            .OrderBy(rule => rule.DepartmentId)
            .Select(rule =>
            {
                var assignmentType = rule.AssignmentType.Trim();
                var status = rule.Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";
                return assignmentType.Equals("User", StringComparison.OrdinalIgnoreCase)
                    ? new UpdateAdminWorkflowIntakeRoutingItem(rule.DepartmentId, "User", rule.TargetUserId, null, status)
                    : new UpdateAdminWorkflowIntakeRoutingItem(rule.DepartmentId, "Group", null, rule.TargetGroupId, status);
            })
            .ToArray();
        var normalizedInput = new UpdateAdminWorkflowIntakeRoutingInput(normalizedRules);
        var metadataJson = JsonSerializer.Serialize(new
        {
            action = "update",
            ruleCount = normalizedRules.Length,
            userTargets = normalizedRules.Count(rule => rule.AssignmentType == "User"),
            groupTargets = normalizedRules.Count(rule => rule.AssignmentType == "Group")
        });

        await _repository.UpdateIntakeRoutingAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            normalizedInput,
            metadataJson,
            cancellationToken);

        var response = await _repository.GetConfigurationAsync(_currentUser.TenantId, cancellationToken);
        return Result<AdminWorkflowConfigurationResponse>.Success(response);
    }

    private async Task<Result> ValidateIntakeRoutingInputAsync(
        UpdateAdminWorkflowIntakeRoutingInput input,
        CancellationToken cancellationToken)
    {
        if (input.Rules is not { Count: > 0 })
        {
            return Result.Failure(
                "admin_workflows.intake_routing_required",
                "At least one department intake routing rule is required.");
        }

        if (input.Rules.Count > MaxIntakeRoutingRules)
        {
            return Result.Failure(
                "admin_workflows.intake_routing_too_many",
                $"Department intake routing cannot exceed {MaxIntakeRoutingRules} rules.");
        }

        var duplicate = input.Rules
            .GroupBy(rule => rule.DepartmentId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            return Result.Failure(
                "admin_workflows.intake_routing_duplicate_department",
                "Each department can only have one intake routing rule.");
        }

        foreach (var rule in input.Rules)
        {
            if (rule.DepartmentId == Guid.Empty)
            {
                return Result.Failure(
                    "admin_workflows.department_invalid",
                    "A valid department is required for each intake routing rule.");
            }

            if (!IsValidIntakeStatus(rule.Status))
            {
                return Result.Failure(
                    "admin_workflows.intake_status_invalid",
                    "Department intake routing status must be Active or Inactive.");
            }

            if (rule.AssignmentType.Equals("User", StringComparison.OrdinalIgnoreCase))
            {
                if (rule.TargetUserId is null || rule.TargetUserId == Guid.Empty || rule.TargetGroupId is not null)
                {
                    return Result.Failure(
                        "admin_workflows.intake_user_target_required",
                        "User intake routing must target exactly one active user.");
                }

                continue;
            }

            if (rule.AssignmentType.Equals("Group", StringComparison.OrdinalIgnoreCase))
            {
                if (rule.TargetGroupId is null || rule.TargetGroupId == Guid.Empty || rule.TargetUserId is not null)
                {
                    return Result.Failure(
                        "admin_workflows.intake_group_target_required",
                        "Group intake routing must target exactly one active group.");
                }

                continue;
            }

            return Result.Failure(
                "admin_workflows.intake_assignment_type_invalid",
                "Department intake routing must target a user or group.");
        }

        var departmentIds = input.Rules.Select(rule => rule.DepartmentId).Distinct().ToArray();
        if (!await _repository.ActiveDepartmentIdsExistAsync(_currentUser.TenantId, departmentIds, cancellationToken))
        {
            return Result.Failure(
                "admin_workflows.departments_invalid",
                "All intake routing departments must be active tenant departments.");
        }

        var userIds = input.Rules
            .Where(rule => rule.AssignmentType.Equals("User", StringComparison.OrdinalIgnoreCase))
            .Select(rule => rule.TargetUserId!.Value)
            .Distinct()
            .ToArray();
        if (!await _repository.ActiveUserIdsExistAsync(_currentUser.TenantId, userIds, cancellationToken))
        {
            return Result.Failure(
                "admin_workflows.users_invalid",
                "All intake routing user targets must be active tenant users.");
        }

        var groupIds = input.Rules
            .Where(rule => rule.AssignmentType.Equals("Group", StringComparison.OrdinalIgnoreCase))
            .Select(rule => rule.TargetGroupId!.Value)
            .Distinct()
            .ToArray();
        if (!await _repository.ActiveGroupIdsExistAsync(_currentUser.TenantId, groupIds, cancellationToken))
        {
            return Result.Failure(
                "admin_workflows.groups_invalid",
                "All intake routing group targets must be active tenant groups.");
        }

        return Result.Success();
    }

    private static bool IsValidIntakeStatus(string status)
    {
        return status.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Inactive", StringComparison.OrdinalIgnoreCase);
    }
}
