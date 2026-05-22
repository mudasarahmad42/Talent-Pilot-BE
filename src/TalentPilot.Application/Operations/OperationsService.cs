using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;
using TalentPilot.Domain.Access;

namespace TalentPilot.Application.Operations;

public sealed class OperationsService : IOperationsService
{
    private readonly IOperationsRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;

    public OperationsService(IOperationsRepository repository, ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<OperationsSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        if (!Can(AccessConstants.ViewJobRequests))
        {
            return Result<OperationsSnapshot>.Failure("auth.forbidden", "Current user is not allowed to view job requests.");
        }

        var snapshot = await _repository.GetSnapshotAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        return Result<OperationsSnapshot>.Success(snapshot);
    }

    public async Task<Result<IReadOnlyList<OperationsActivityEvent>>> GetActivityAsync(
        Guid entityId,
        CancellationToken cancellationToken)
    {
        if (!Can(AccessConstants.ViewJobRequests))
        {
            return Result<IReadOnlyList<OperationsActivityEvent>>.Failure("auth.forbidden", "Current user is not allowed to view job request activity.");
        }

        var activity = await _repository.GetActivityAsync(_currentUser.TenantId, entityId, cancellationToken);
        return Result<IReadOnlyList<OperationsActivityEvent>>.Success(activity);
    }

    public async Task<Result<IReadOnlyList<OperationsJobRequest>>> ListJobRequestsAsync(CancellationToken cancellationToken)
    {
        if (!Can(AccessConstants.ViewJobRequests))
        {
            return Result<IReadOnlyList<OperationsJobRequest>>.Failure("auth.forbidden", "Current user is not allowed to view job requests.");
        }

        var snapshot = await _repository.GetSnapshotAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        return Result<IReadOnlyList<OperationsJobRequest>>.Success(snapshot.JobRequests);
    }

    public async Task<Result<OperationsJobRequest>> GetJobRequestAsync(
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        if (!Can(AccessConstants.ViewJobRequests))
        {
            return Result<OperationsJobRequest>.Failure("auth.forbidden", "Current user is not allowed to view job requests.");
        }

        var jobRequest = await _repository.GetJobRequestAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            IsTenantAdmin(),
            cancellationToken);

        return jobRequest is null
            ? Result<OperationsJobRequest>.Failure("job_request.not_found", "Job request was not found.")
            : Result<OperationsJobRequest>.Success(jobRequest);
    }

    public async Task<Result<IReadOnlyList<OperationsPmoQueueItem>>> GetPmoQueueAsync(CancellationToken cancellationToken)
    {
        if (!Can(AccessConstants.ClaimWorkflowTask))
        {
            return Result<IReadOnlyList<OperationsPmoQueueItem>>.Failure("auth.forbidden", "Current user is not allowed to view the PMO queue.");
        }

        var queue = await _repository.GetPmoQueueAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            IsTenantAdmin(),
            cancellationToken);

        return Result<IReadOnlyList<OperationsPmoQueueItem>>.Success(queue);
    }

    public async Task<Result<IReadOnlyList<OperationsRecruitmentQueueItem>>> GetRecruitmentQueueAsync(CancellationToken cancellationToken)
    {
        if (!Can(AccessConstants.ManageCandidates))
        {
            return Result<IReadOnlyList<OperationsRecruitmentQueueItem>>.Failure("auth.forbidden", "Current user is not allowed to view the recruitment queue.");
        }

        var queue = await _repository.GetRecruitmentQueueAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            IsTenantAdmin(),
            cancellationToken);

        return Result<IReadOnlyList<OperationsRecruitmentQueueItem>>.Success(queue);
    }

    public async Task<Result<IReadOnlyList<OperationsBenchMatch>>> GetBenchMatchesAsync(
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        if (!Can(AccessConstants.ViewBenchMatches))
        {
            return Result<IReadOnlyList<OperationsBenchMatch>>.Failure("auth.forbidden", "Current user is not allowed to view bench matches.");
        }

        var jobRequest = await _repository.GetJobRequestAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            IsTenantAdmin(),
            cancellationToken);

        if (jobRequest is null)
        {
            return Result<IReadOnlyList<OperationsBenchMatch>>.Failure("job_request.not_found", "Job request was not found.");
        }

        var matches = await _repository.GetBenchMatchesAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            IsTenantAdmin(),
            cancellationToken);

        return Result<IReadOnlyList<OperationsBenchMatch>>.Success(matches);
    }

    public async Task<Result<IReadOnlyList<OperationsNotification>>> ListNotificationsAsync(CancellationToken cancellationToken)
    {
        var notifications = await _repository.ListNotificationsAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            cancellationToken);

        return Result<IReadOnlyList<OperationsNotification>>.Success(notifications);
    }

    public async Task<Result<CreateOperationsJobRequestResult>> CreateJobRequestAsync(
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken)
    {
        if (!Can(AccessConstants.CreateJobRequest))
        {
            return Result<CreateOperationsJobRequestResult>.Failure("auth.forbidden", "Current user is not allowed to create job requests.");
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.title_required", "Title is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Description))
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.description_required", "Description is required.");
        }

        if (input.RequiredPositions < 1)
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.positions_required", "At least one required position is needed.");
        }

        var created = await _repository.CreateJobRequestAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            input,
            cancellationToken);

        return Result<CreateOperationsJobRequestResult>.Success(created);
    }

    public async Task<Result> ClaimAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        if (!Can(AccessConstants.ClaimWorkflowTask))
        {
            return Result.Failure("auth.forbidden", "Current user is not allowed to claim workflow assignments.");
        }

        var claimed = await _repository.ClaimAssignmentAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            assignmentId,
            cancellationToken);

        return claimed
            ? Result.Success()
            : Result.Failure("workflow_assignment.not_found", "Workflow assignment was not found or cannot be claimed.");
    }

    public async Task<Result<ForwardToRecruiterResult>> ForwardToRecruiterAsync(Guid jobRequestId, CancellationToken cancellationToken)
    {
        if (!Can(AccessConstants.ClaimWorkflowTask))
        {
            return Result<ForwardToRecruiterResult>.Failure("auth.forbidden", "Current user is not allowed to forward requests to recruitment.");
        }

        var result = await _repository.ForwardToRecruiterAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            IsTenantAdmin(),
            cancellationToken);

        return result is null
            ? Result<ForwardToRecruiterResult>.Failure("job_request.not_forwardable", "Job request was not found or cannot be forwarded to recruitment.")
            : Result<ForwardToRecruiterResult>.Success(result);
    }

    public async Task<Result<CreateInternalResourceReferralResult>> CreateInternalResourceReferralAsync(
        Guid jobRequestId,
        CreateInternalResourceReferralInput input,
        CancellationToken cancellationToken)
    {
        if (!Can(AccessConstants.ClaimWorkflowTask) || !Can(AccessConstants.ViewBenchMatches))
        {
            return Result<CreateInternalResourceReferralResult>.Failure("auth.forbidden", "Current user is not allowed to refer internal employees.");
        }

        if (input.EmployeeIds is null || input.EmployeeIds.Count == 0)
        {
            return Result<CreateInternalResourceReferralResult>.Failure("employee_referral.employee_required", "At least one employee must be selected.");
        }

        if (input.Note is { Length: > 1000 })
        {
            return Result<CreateInternalResourceReferralResult>.Failure("employee_referral.note_too_long", "Referral note cannot exceed 1000 characters.");
        }

        var employeeIds = input.EmployeeIds
            .Where(employeeId => employeeId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (employeeIds.Length == 0)
        {
            return Result<CreateInternalResourceReferralResult>.Failure("employee_referral.employee_required", "At least one employee must be selected.");
        }

        var result = await _repository.CreateInternalResourceReferralAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            input with { EmployeeIds = employeeIds },
            IsTenantAdmin(),
            cancellationToken);

        return result is null
            ? Result<CreateInternalResourceReferralResult>.Failure("employee_referral.not_created", "Job request was not found, is not assigned to the current user, or includes employees that are not available on bench.")
            : Result<CreateInternalResourceReferralResult>.Success(result);
    }

    public async Task<Result> MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var updated = await _repository.MarkNotificationReadAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            notificationId,
            cancellationToken);

        return updated
            ? Result.Success()
            : Result.Failure("notification.not_found", "Notification was not found.");
    }

    public async Task<Result> MarkAllNotificationsReadAsync(CancellationToken cancellationToken)
    {
        await _repository.MarkAllNotificationsReadAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        return Result.Success();
    }

    private bool Can(string permission)
    {
        return IsTenantAdmin() || _currentUser.Permissions.Contains(permission);
    }

    private bool IsTenantAdmin()
    {
        return _currentUser.RoleCodes.Contains(AccessConstants.TenantAdminRoleCode);
    }
}
