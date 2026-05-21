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

    [HttpPost("job-requests")]
    public async Task<ActionResult<CreateOperationsJobRequestResult>> CreateJobRequest(
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.CreateJobRequestAsync(input, cancellationToken));
    }

    [HttpPost("workflow-assignments/{assignmentId:guid}/claim")]
    public async Task<IActionResult> ClaimAssignment(Guid assignmentId, CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ClaimAssignmentAsync(assignmentId, cancellationToken));
    }

    [HttpPatch("notifications/{notificationId:guid}/read")]
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
