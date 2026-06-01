namespace TalentPilot.Application.Admin.Workflows;

public sealed record AdminWorkflowConfigurationResponse(
    AdminWorkflowSummary Summary,
    IReadOnlyList<AdminWorkflowDefinitionItem> Definitions,
    IReadOnlyList<AdminWorkflowStageItem> Stages,
    IReadOnlyList<AdminWorkflowRoutingRuleItem> RoutingRules,
    IReadOnlyList<AdminWorkflowIntakeRoutingRuleItem> IntakeRoutingRules);

public sealed record AdminWorkflowSummary(
    int WorkflowDefinitionCount,
    int ActiveStageCount,
    int ActiveTransitionCount,
    int ActiveRoutingRuleCount,
    int ActiveIntakeRoutingRuleCount,
    int DepartmentsNeedingIntakeRoutingCount);

public sealed record AdminWorkflowDefinitionItem(
    Guid WorkflowDefinitionId,
    string Code,
    string Name,
    string EntityType,
    string Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminWorkflowStageItem(
    Guid WorkflowStageId,
    string StageKey,
    string Name,
    int StageOrder,
    bool IsTerminal,
    string Status);

public sealed record AdminWorkflowRoutingRuleItem(
    Guid WorkflowRoutingRuleId,
    Guid WorkflowTransitionId,
    string ActionKey,
    string ActionName,
    string FromStage,
    string ToStage,
    string AssignmentType,
    string AssignmentTarget,
    string ResolverKey,
    string Status);

public sealed record AdminWorkflowIntakeRoutingRuleItem(
    Guid? JobRequestIntakeRoutingRuleId,
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    string AssignmentType,
    Guid? TargetUserId,
    Guid? TargetGroupId,
    string AssignmentTarget,
    string Status,
    bool UsesTenantAdminFallback);

public sealed record UpdateAdminWorkflowIntakeRoutingInput(
    IReadOnlyList<UpdateAdminWorkflowIntakeRoutingItem> Rules);

public sealed record UpdateAdminWorkflowIntakeRoutingItem(
    Guid DepartmentId,
    string AssignmentType,
    Guid? TargetUserId,
    Guid? TargetGroupId,
    string Status);
