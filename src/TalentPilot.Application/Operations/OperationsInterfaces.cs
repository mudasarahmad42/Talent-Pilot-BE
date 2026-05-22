using TalentPilot.Common.Results;

namespace TalentPilot.Application.Operations;

public interface IOperationsService
{
    Task<Result<OperationsSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<OperationsActivityEvent>>> GetActivityAsync(Guid entityId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<OperationsJobRequest>>> ListJobRequestsAsync(CancellationToken cancellationToken);

    Task<Result<OperationsJobRequest>> GetJobRequestAsync(Guid jobRequestId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<OperationsPmoQueueItem>>> GetPmoQueueAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<OperationsRecruitmentQueueItem>>> GetRecruitmentQueueAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<OperationsBenchMatch>>> GetBenchMatchesAsync(
        Guid jobRequestId,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<OperationsNotification>>> ListNotificationsAsync(CancellationToken cancellationToken);

    Task<Result<CreateOperationsJobRequestResult>> CreateJobRequestAsync(
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken);

    Task<Result> ClaimAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<Result<ForwardToRecruiterResult>> ForwardToRecruiterAsync(Guid jobRequestId, CancellationToken cancellationToken);

    Task<Result<CreateInternalResourceReferralResult>> CreateInternalResourceReferralAsync(
        Guid jobRequestId,
        CreateInternalResourceReferralInput input,
        CancellationToken cancellationToken);

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

    Task<OperationsJobRequest?> GetJobRequestAsync(
        Guid tenantId,
        Guid userId,
        Guid jobRequestId,
        bool canViewAll,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OperationsPmoQueueItem>> GetPmoQueueAsync(
        Guid tenantId,
        Guid userId,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OperationsRecruitmentQueueItem>> GetRecruitmentQueueAsync(
        Guid tenantId,
        Guid userId,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OperationsBenchMatch>> GetBenchMatchesAsync(
        Guid tenantId,
        Guid userId,
        Guid jobRequestId,
        bool canViewAll,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OperationsNotification>> ListNotificationsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<CreateOperationsJobRequestResult> CreateJobRequestAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken);

    Task<bool> ClaimAssignmentAsync(Guid tenantId, Guid actorUserId, Guid assignmentId, CancellationToken cancellationToken);

    Task<ForwardToRecruiterResult?> ForwardToRecruiterAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken);

    Task<CreateInternalResourceReferralResult?> CreateInternalResourceReferralAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CreateInternalResourceReferralInput input,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken);

    Task<bool> MarkNotificationReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken);

    Task MarkAllNotificationsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}
