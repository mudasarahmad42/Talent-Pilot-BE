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
    string? AssignedToGroupId,
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
