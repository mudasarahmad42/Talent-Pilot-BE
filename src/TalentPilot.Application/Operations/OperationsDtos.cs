namespace TalentPilot.Application.Operations;

public sealed record OperationsPeopleResponse(IReadOnlyList<OperationsPerson> Items);

public sealed record OperationsPerson(
    Guid UserId,
    string DisplayName,
    string Email,
    IReadOnlyList<string> RoleCodes,
    IReadOnlyList<string> RoleNames);

public sealed record OperationsSnapshot(
    IReadOnlyList<OperationsPerson> People,
    IReadOnlyList<OperationsJobRequest> JobRequests,
    IReadOnlyList<OperationsWorkflowAssignment> Assignments,
    IReadOnlyList<OperationsNotification> Notifications);

public sealed record OperationsPmoQueueItem(
    OperationsWorkflowAssignment Assignment,
    OperationsJobRequest JobRequest);

public sealed record OperationsRecruitmentQueueItem(
    OperationsWorkflowAssignment Assignment,
    OperationsJobRequest JobRequest,
    int CandidateCount);

public sealed record OperationsBenchMatch(
    Guid EmployeeId,
    string EmployeeCode,
    string DisplayName,
    string Email,
    string? Designation,
    string Department,
    string Location,
    IReadOnlyList<string> Skills,
    string AvailabilityStatus,
    string BenchStatus,
    int CurrentAllocationPercent,
    int MatchScore,
    string MatchExplanation);

public sealed record OperationsJobRequest(
    Guid Id,
    string Code,
    string Title,
    string Client,
    string Description,
    string Department,
    IReadOnlyList<string> Skills,
    string Experience,
    string Location,
    int RequiredPositions,
    int FulfilledPositions,
    string Priority,
    Guid HiringManagerId,
    Guid CreatedById,
    string Stage,
    Guid? OwnerId,
    string? OwnerGroupId,
    string PublishStatus,
    DateTimeOffset CreatedAt);

public sealed record OperationsWorkflowAssignment(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Stage,
    Guid? AssignedToGroupId,
    string? AssignedToGroupName,
    Guid? AssignedToUserId,
    Guid? ClaimedByUserId,
    string Status,
    DateTimeOffset AssignedAt);

public sealed record OperationsNotification(
    Guid Id,
    Guid RecipientUserId,
    string Title,
    string Message,
    string EntityType,
    Guid EntityId,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);

public sealed record OperationsActivityEvent(
    Guid Id,
    Guid EntityId,
    string ActorName,
    string Title,
    string Detail,
    DateTimeOffset CreatedAt);

public sealed record CreateOperationsJobRequestInput(
    string Title,
    string Client,
    string Description,
    string Department,
    IReadOnlyList<string> Skills,
    string Experience,
    string Location,
    int RequiredPositions,
    string Priority,
    Guid HiringManagerId);

public sealed record CreateOperationsJobRequestResult(
    OperationsJobRequest JobRequest,
    OperationsWorkflowAssignment Assignment);

public sealed record ForwardToRecruiterResult(
    OperationsJobRequest JobRequest,
    OperationsWorkflowAssignment Assignment,
    int CandidateCount);

public sealed record CreateInternalResourceReferralInput(
    IReadOnlyList<Guid> EmployeeIds,
    string? Note);

public sealed record InternalEmployeeReferral(
    Guid Id,
    Guid JobRequestId,
    Guid EmployeeId,
    string EmployeeName,
    string EmployeeEmail,
    string Status,
    int FitScore,
    string RecommendationSummary,
    Guid ReferredByUserId,
    Guid? PresalesUserId,
    DateTimeOffset CreatedAt);

public sealed record CreateInternalResourceReferralResult(
    OperationsJobRequest JobRequest,
    IReadOnlyList<InternalEmployeeReferral> Referrals);
