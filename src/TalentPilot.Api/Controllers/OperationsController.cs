using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Operations;

namespace TalentPilot.Api.Controllers;

[Route("api/talent-pilot")]
public sealed class OperationsController : ApiControllerBase
{
    private readonly IOperationsService _operationsService;

    public OperationsController(IOperationsService operationsService)
    {
        _operationsService = operationsService;
    }

    [HttpGet("snapshot")]
    public async Task<ActionResult<OperationsSnapshot>> Snapshot(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetSnapshotAsync(cancellationToken));
    }

    [HttpGet("job-requests/{entityId:guid}/activity")]
    public async Task<ActionResult<IReadOnlyList<OperationsActivityEvent>>> Activity(
        Guid entityId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetActivityAsync(entityId, cancellationToken));
    }

    [HttpGet("/api/job-requests")]
    public async Task<ActionResult<IReadOnlyList<OperationsJobRequest>>> JobRequests(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ListJobRequestsAsync(cancellationToken));
    }

    [HttpGet("/api/job-requests/{jobRequestId:guid}")]
    public async Task<ActionResult<OperationsJobRequest>> GetJobRequest(
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetJobRequestAsync(jobRequestId, cancellationToken));
    }

    [HttpGet("job-requests/{jobRequestId:guid}/bench-matches")]
    [HttpGet("/api/job-requests/{jobRequestId:guid}/bench-matches")]
    public async Task<ActionResult<IReadOnlyList<OperationsBenchMatch>>> BenchMatches(
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetBenchMatchesAsync(jobRequestId, cancellationToken));
    }

    [HttpGet("/api/pmo/queue")]
    public async Task<ActionResult<IReadOnlyList<OperationsPmoQueueItem>>> PmoQueue(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetPmoQueueAsync(cancellationToken));
    }

    [HttpGet("/api/recruitment/queue")]
    public async Task<ActionResult<IReadOnlyList<OperationsRecruitmentQueueItem>>> RecruitmentQueue(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetRecruitmentQueueAsync(cancellationToken));
    }

    [HttpGet("/api/notifications")]
    public async Task<ActionResult<IReadOnlyList<OperationsNotification>>> Notifications(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ListNotificationsAsync(cancellationToken));
    }

    [HttpPost("job-requests")]
    [HttpPost("/api/job-requests")]
    public async Task<ActionResult<CreateOperationsJobRequestResult>> CreateJobRequest(
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.CreateJobRequestAsync(input, cancellationToken));
    }

    [HttpPost("workflow-assignments/{assignmentId:guid}/claim")]
    [HttpPost("/api/workflow-assignments/{assignmentId:guid}/claim")]
    public async Task<IActionResult> ClaimAssignment(Guid assignmentId, CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ClaimAssignmentAsync(assignmentId, cancellationToken));
    }

    [HttpPost("job-requests/{jobRequestId:guid}/forward-to-recruiter")]
    [HttpPost("/api/job-requests/{jobRequestId:guid}/forward-to-recruiter")]
    public async Task<ActionResult<ForwardToRecruiterResult>> ForwardToRecruiter(
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ForwardToRecruiterAsync(jobRequestId, cancellationToken));
    }

    [HttpPost("job-requests/{jobRequestId:guid}/employee-referrals")]
    [HttpPost("/api/job-requests/{jobRequestId:guid}/employee-referrals")]
    public async Task<ActionResult<CreateInternalResourceReferralResult>> CreateInternalResourceReferral(
        Guid jobRequestId,
        CreateInternalResourceReferralInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.CreateInternalResourceReferralAsync(jobRequestId, input, cancellationToken));
    }

    [HttpPatch("notifications/{notificationId:guid}/read")]
    [HttpPost("/api/notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid notificationId, CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.MarkNotificationReadAsync(notificationId, cancellationToken));
    }

    [HttpPatch("notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.MarkAllNotificationsReadAsync(cancellationToken));
    }
}
