using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Infrastructure.Notifications;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/notifications")]
public sealed class NotificationsController : AdminApiControllerBase
{
    private readonly IAdminNotificationsService _service;
    private readonly ResendEmailOptions _resendOptions;
    private readonly MicrosoftGraphEmailOptions _microsoftGraphOptions;

    public NotificationsController(
        IAdminNotificationsService service,
        IOptions<ResendEmailOptions> resendOptions,
        IOptions<MicrosoftGraphEmailOptions> microsoftGraphOptions)
    {
        _service = service;
        _resendOptions = resendOptions.Value;
        _microsoftGraphOptions = microsoftGraphOptions.Value;
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

    [HttpGet("outbox")]
    public async Task<ActionResult<AdminNotificationOutboxResponse>> ListOutbox(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.ListOutboxAsync(new AdminNotificationOutboxQuery(search, status, page, pageSize), cancellationToken));
    }

    [HttpPost("outbox/{outboxId:guid}/retry")]
    public async Task<ActionResult<AdminNotificationOutboxItem>> RetryOutboxEmail(
        Guid outboxId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.RetryOutboxEmailAsync(outboxId, cancellationToken));
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

    [HttpGet("email-senders")]
    public ActionResult<NotificationEmailSenderConfigurationResponse> ListEmailSenders()
    {
        var resendSenderEmail = _resendOptions.FromEmail.Trim();
        var microsoftGraphSenderEmail = _microsoftGraphOptions.FromEmail.Trim();

        var response = new NotificationEmailSenderConfigurationResponse(
            [
                new NotificationEmailSenderProviderConfiguration(
                    NotificationEmailProviders.Resend,
                    "Resend",
                    resendSenderEmail,
                    !string.IsNullOrWhiteSpace(resendSenderEmail)),
                new NotificationEmailSenderProviderConfiguration(
                    NotificationEmailProviders.MicrosoftGraph,
                    "Microsoft Graph",
                    microsoftGraphSenderEmail,
                    !string.IsNullOrWhiteSpace(microsoftGraphSenderEmail))
            ]);

        return Ok(response);
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
