using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;

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
        var snapshot = await _repository.GetSnapshotAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        return Result<OperationsSnapshot>.Success(snapshot);
    }

    public async Task<Result<IReadOnlyList<OperationsActivityEvent>>> GetActivityAsync(
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var activity = await _repository.GetActivityAsync(_currentUser.TenantId, entityId, cancellationToken);
        return Result<IReadOnlyList<OperationsActivityEvent>>.Success(activity);
    }

    public async Task<Result<CreateOperationsJobRequestResult>> CreateJobRequestAsync(
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken)
    {
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
        var claimed = await _repository.ClaimAssignmentAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            assignmentId,
            cancellationToken);

        return claimed
            ? Result.Success()
            : Result.Failure("workflow_assignment.not_found", "Workflow assignment was not found or cannot be claimed.");
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
}
