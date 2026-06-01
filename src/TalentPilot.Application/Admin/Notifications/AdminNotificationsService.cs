using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Mail;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Notifications;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Notifications;

public sealed class AdminNotificationsService : IAdminNotificationsService
{
    private static readonly Regex TemplateVariablePattern = new("{{\\s*([a-zA-Z][a-zA-Z0-9_]*)\\s*}}", RegexOptions.Compiled);
    private static readonly string[] ValidStatuses = ["Active", "Inactive"];
    private const string TestEmailSubject = "Talent Pilot test email: delivery has lift-off";
    private const string TestRealtimeTitle = "Realtime test: the wire is live";
    private const string TestRealtimeMessage = "This realtime notification was sent to every connected Talent Pilot client in this tenant.";
    private const string TestEmailBody = """
        Hello from Talent Pilot.

        This is a real test email sent through Resend.
        If this landed in your inbox, email delivery is connected and ready for workflow notifications.

        Tiny dashboard status: all systems are smiling.
        """;

    private readonly IAdminNotificationsRepository _repository;
    private readonly INotificationEmailSender _emailSender;
    private readonly IRealtimeNotificationPublisher _realtimePublisher;
    private readonly IRealtimeConnectionCounter _realtimeConnectionCounter;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminNotificationsService(
        IAdminNotificationsRepository repository,
        INotificationEmailSender emailSender,
        IRealtimeNotificationPublisher realtimePublisher,
        IRealtimeConnectionCounter realtimeConnectionCounter,
        ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _emailSender = emailSender;
        _realtimePublisher = realtimePublisher;
        _realtimeConnectionCounter = realtimeConnectionCounter;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminNotificationEventsResponse>> ListEventsAsync(
        AdminNotificationEventsQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100)
        };

        var response = await _repository.ListEventsAsync(_currentUser.TenantId, normalized, cancellationToken);
        return Result<AdminNotificationEventsResponse>.Success(response);
    }

    public async Task<Result<AdminNotificationEventDetails>> GetEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var item = await _repository.GetEventAsync(_currentUser.TenantId, eventId, cancellationToken);
        return item is null
            ? Result<AdminNotificationEventDetails>.Failure("notifications.event_not_found", "Notification event was not found.")
            : Result<AdminNotificationEventDetails>.Success(item);
    }

    public async Task<Result<AdminNotificationTemplatesResponse>> ListTemplatesAsync(
        AdminNotificationTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100)
        };

        var response = await _repository.ListTemplatesAsync(_currentUser.TenantId, normalized, cancellationToken);
        return Result<AdminNotificationTemplatesResponse>.Success(response);
    }

    public async Task<Result<NotificationTemplateSummary>> UpdateTemplateAsync(
        Guid templateId,
        UpdateNotificationTemplateInput input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Subject) || string.IsNullOrWhiteSpace(input.Body))
        {
            return Result<NotificationTemplateSummary>.Failure("notifications.template_invalid", "Template subject and body are required.");
        }

        var existing = await _repository.GetTemplateAsync(_currentUser.TenantId, templateId, cancellationToken);
        if (existing is null)
        {
            return Result<NotificationTemplateSummary>.Failure("notifications.template_not_found", "Notification template was not found.");
        }

        var allowedVariables = existing.Variables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedVariables = ExtractVariables(input.Subject)
            .Concat(ExtractVariables(input.Body))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var invalidVariable = usedVariables.FirstOrDefault(variable => !allowedVariables.Contains(variable));
        if (invalidVariable is not null)
        {
            return Result<NotificationTemplateSummary>.Failure("notifications.variable_invalid", $"Template variable '{invalidVariable}' is not allowed for this event.");
        }

        var metadataJson = JsonSerializer.Serialize(new { action = "template_update", templateId, usedVariables });
        await _repository.UpdateTemplateAsync(_currentUser.TenantId, _currentUser.UserId, templateId, input, metadataJson, cancellationToken);

        var updated = await _repository.GetTemplateAsync(_currentUser.TenantId, templateId, cancellationToken);
        return Result<NotificationTemplateSummary>.Success(updated!);
    }

    public async Task<Result<SendTestNotificationEmailResponse>> SendTestEmailAsync(
        SendTestNotificationEmailInput input,
        CancellationToken cancellationToken)
    {
        if (!IsValidEmail(input.ToEmail))
        {
            return Result<SendTestNotificationEmailResponse>.Failure("notifications.test_email_invalid", "A valid recipient email is required.");
        }

        var email = new NotificationEmailMessage(
            input.ToEmail.Trim(),
            TestEmailSubject,
            TestEmailBody,
            BuildHtmlBody(TestEmailBody));

        var sendResult = await _emailSender.SendAsync(email, cancellationToken);
        if (sendResult.Failed)
        {
            return Result<SendTestNotificationEmailResponse>.Failure(sendResult.Error.Code, sendResult.Error.Message);
        }

        var metadataJson = JsonSerializer.Serialize(new
        {
            action = "notification_test_email",
            recipientEmail = input.ToEmail.Trim(),
            subject = TestEmailSubject,
            provider = sendResult.Value.Provider,
            providerMessageId = sendResult.Value.MessageId
        });
        await _repository.RecordTestEmailSentAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            input.ToEmail.Trim(),
            sendResult.Value.MessageId,
            metadataJson,
            cancellationToken);

        return Result<SendTestNotificationEmailResponse>.Success(new SendTestNotificationEmailResponse(
            input.ToEmail.Trim(),
            TestEmailSubject,
            sendResult.Value.Provider,
            sendResult.Value.MessageId,
            sendResult.Value.SubmittedAtUtc));
    }

    public Task<Result<RealtimeNotificationConnectionStatusResponse>> GetRealtimeConnectionStatusAsync(
        CancellationToken cancellationToken)
    {
        var response = new RealtimeNotificationConnectionStatusResponse(
            _realtimeConnectionCounter.CountTenantConnections(_currentUser.TenantId),
            DateTimeOffset.UtcNow);

        return Task.FromResult(Result<RealtimeNotificationConnectionStatusResponse>.Success(response));
    }

    public async Task<Result<SendTestRealtimeNotificationResponse>> SendTestRealtimeNotificationAsync(
        CancellationToken cancellationToken)
    {
        var notificationId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;
        var notification = new RealtimeNotificationMessage(
            notificationId,
            _currentUser.TenantId,
            null,
            TestRealtimeTitle,
            TestRealtimeMessage,
            "AdminCenter",
            "Info",
            "AdminCenter",
            _currentUser.TenantId.ToString("D"),
            createdAtUtc,
            new Dictionary<string, string>
            {
                ["source"] = "admin_notifications_test",
                ["senderUserId"] = _currentUser.UserId.ToString("D")
            });

        var publishResult = await _realtimePublisher.PublishToTenantAsync(
            _currentUser.TenantId,
            notification,
            cancellationToken);

        var metadataJson = JsonSerializer.Serialize(new
        {
            action = "notification_realtime_test",
            notificationId,
            connectedClientCount = publishResult.ConnectedClientCount,
            title = TestRealtimeTitle
        });
        await _repository.RecordRealtimeTestNotificationSentAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            notificationId,
            publishResult.ConnectedClientCount,
            metadataJson,
            cancellationToken);

        return Result<SendTestRealtimeNotificationResponse>.Success(new SendTestRealtimeNotificationResponse(
            publishResult.RecipientNotificationIds.GetValueOrDefault(_currentUser.UserId, notificationId),
            TestRealtimeTitle,
            TestRealtimeMessage,
            publishResult.ConnectedClientCount,
            publishResult.SentAtUtc));
    }

    public async Task<Result> UpdateEventStatusAsync(
        Guid eventId,
        UpdateNotificationEventStatusInput input,
        CancellationToken cancellationToken)
    {
        if (!ValidStatuses.Contains(input.Status, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("notifications.status_invalid", "Notification event status must be Active or Inactive.");
        }

        var existing = await _repository.GetEventAsync(_currentUser.TenantId, eventId, cancellationToken);
        if (existing is null)
        {
            return Result.Failure("notifications.event_not_found", "Notification event was not found.");
        }

        var metadataJson = JsonSerializer.Serialize(new { action = "event_status", eventId, input.Status });
        await _repository.UpdateEventStatusAsync(_currentUser.TenantId, _currentUser.UserId, eventId, input.Status, metadataJson, cancellationToken);
        return Result.Success();
    }

    private static IEnumerable<string> ExtractVariables(string template)
    {
        return TemplateVariablePattern
            .Matches(template)
            .Select(match => match.Groups[1].Value);
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var address = new MailAddress(email.Trim());
            return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string BuildHtmlBody(string renderedBody)
    {
        var encoded = WebUtility.HtmlEncode(renderedBody).Replace(Environment.NewLine, "<br>", StringComparison.Ordinal);
        return $"""
            <div style="font-family: Arial, sans-serif; color: #0f172a; line-height: 1.5;">
              <p>{encoded}</p>
              <hr style="border: 0; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
              <p style="color: #64748b; font-size: 13px;">This is a test email from Talent Pilot.</p>
            </div>
            """;
    }
}
