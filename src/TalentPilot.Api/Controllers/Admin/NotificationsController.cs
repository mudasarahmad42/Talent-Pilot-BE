using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.Notifications;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/notifications")]
public sealed class NotificationsController : AdminApiControllerBase
{
    private readonly IAdminNotificationsService _service;

    public NotificationsController(IAdminNotificationsService service)
    {
        _service = service;
    }

    [HttpGet("events")]
    public async Task<ActionResult<AdminNotificationEventsResponse>> ListEvents(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.ListEventsAsync(new AdminNotificationEventsQuery(search, page, pageSize), cancellationToken));
    }

    [HttpGet("events/{eventId:guid}")]
    public async Task<ActionResult<AdminNotificationEventDetails>> GetEvent(Guid eventId, CancellationToken cancellationToken)
    {
        return FromResult(await _service.GetEventAsync(eventId, cancellationToken));
    }

    [HttpPatch("events/{eventId:guid}/status")]
    public async Task<IActionResult> UpdateEventStatus(
        Guid eventId,
        UpdateNotificationEventStatusInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.UpdateEventStatusAsync(eventId, input, cancellationToken));
    }

    [HttpGet("templates")]
    public async Task<ActionResult<AdminNotificationTemplatesResponse>> ListTemplates(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.ListTemplatesAsync(new AdminNotificationTemplatesQuery(search, page, pageSize), cancellationToken));
    }

    [HttpPut("templates/{templateId:guid}")]
    public async Task<ActionResult<NotificationTemplateSummary>> UpdateTemplate(
        Guid templateId,
        UpdateNotificationTemplateInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.UpdateTemplateAsync(templateId, input, cancellationToken));
    }

    [HttpPost("test-email")]
    public async Task<ActionResult<SendTestNotificationEmailResponse>> SendTestEmail(
        SendTestNotificationEmailInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.SendTestEmailAsync(input, cancellationToken));
    }

    [HttpGet("realtime/status")]
    public async Task<ActionResult<RealtimeNotificationConnectionStatusResponse>> GetRealtimeConnectionStatus(
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.GetRealtimeConnectionStatusAsync(cancellationToken));
    }

    [HttpPost("test-realtime")]
    public async Task<ActionResult<SendTestRealtimeNotificationResponse>> SendTestRealtime(
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.SendTestRealtimeNotificationAsync(cancellationToken));
    }
}
