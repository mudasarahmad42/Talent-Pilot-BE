namespace TalentPilot.Application.Admin.HiringPipelines;

public sealed record AdminHiringPipelineTemplatesQuery(string? Search, int Page, int PageSize);

public sealed record AdminHiringPipelineTemplatesResponse(
    AdminHiringPipelineSummary Summary,
    IReadOnlyList<AdminHiringPipelineTemplateItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminHiringPipelineSummary(
    int ActiveTemplateCount,
    int DepartmentSpecificTemplateCount,
    int ActiveRoundCount,
    int MissingInterviewerRoundCount);

public sealed record AdminHiringPipelineTemplateItem(
    Guid InterviewTemplateId,
    string Name,
    string DepartmentName,
    string Description,
    string StageFlow,
    string DefaultInterviewers,
    int RoundCount,
    string Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminHiringPipelineTemplateDetails(
    Guid InterviewTemplateId,
    Guid? DepartmentId,
    string Name,
    string DepartmentName,
    string Description,
    string Status,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<AdminHiringPipelineTemplateRoundItem> Rounds);

public sealed record AdminHiringPipelineTemplateRoundItem(
    Guid InterviewTemplateRoundId,
    int RoundOrder,
    string Name,
    Guid? OwnerRoleId,
    string OwnerRoleName,
    Guid? OwnerUserId,
    string OwnerUserName,
    int DurationMinutes,
    bool IsRequired,
    string Status);

public sealed record UpdateAdminHiringPipelineTemplateInput(
    string Name,
    Guid? DepartmentId,
    string? Description,
    string Status,
    IReadOnlyList<UpdateAdminHiringPipelineTemplateRoundInput> Rounds);

public sealed record CreateAdminHiringPipelineTemplateInput(
    string Name,
    Guid? DepartmentId,
    string? Description,
    string Status,
    IReadOnlyList<CreateAdminHiringPipelineTemplateRoundInput> Rounds);

public sealed record UpdateAdminHiringPipelineTemplateRoundInput(
    Guid? InterviewTemplateRoundId,
    int RoundOrder,
    string Name,
    Guid? OwnerRoleId,
    Guid? OwnerUserId,
    int DurationMinutes,
    bool IsRequired,
    string Status);

public sealed record CreateAdminHiringPipelineTemplateRoundInput(
    int RoundOrder,
    string Name,
    Guid? OwnerRoleId,
    Guid? OwnerUserId,
    int DurationMinutes,
    string Status);
