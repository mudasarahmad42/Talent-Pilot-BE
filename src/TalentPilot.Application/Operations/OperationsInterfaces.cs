using TalentPilot.Common.Results;

namespace TalentPilot.Application.Operations;

public interface IOperationsService
{
    Task<Result<OperationsSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<OperationsActivityEvent>>> GetActivityAsync(Guid entityId, CancellationToken cancellationToken);

    Task<Result<CreateOperationsJobRequestResult>> CreateJobRequestAsync(
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken);

    Task<Result> ClaimAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<Result> MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken);

    Task<Result> MarkAllNotificationsReadAsync(CancellationToken cancellationToken);
}

public interface IOperationsRepository
{
    Task<OperationsSnapshot> GetSnapshotAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OperationsActivityEvent>> GetActivityAsync(
        Guid tenantId,
        Guid entityId,
        CancellationToken cancellationToken);

    Task<CreateOperationsJobRequestResult> CreateJobRequestAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken);

    Task<bool> ClaimAssignmentAsync(Guid tenantId, Guid actorUserId, Guid assignmentId, CancellationToken cancellationToken);

    Task<bool> MarkNotificationReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken);

    Task MarkAllNotificationsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}
