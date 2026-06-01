using Microsoft.Extensions.Options;
using Resend;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Common.Results;

namespace TalentPilot.Infrastructure.Notifications;

public sealed class ResendNotificationEmailSender : INotificationEmailSender
{
    private readonly ResendEmailOptions _options;

    public ResendNotificationEmailSender(IOptions<ResendEmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task<Result<NotificationEmailSendResult>> SendAsync(
        NotificationEmailMessage message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Result<NotificationEmailSendResult>.Failure(
                "notifications.resend_not_configured",
                "Resend API key is not configured for this environment.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            return Result<NotificationEmailSendResult>.Failure(
                "notifications.sender_not_configured",
                "A Resend sender email is not configured for this environment.");
        }

        IResend resend = ResendClient.Create(_options.ApiKey.Trim());
        var email = new EmailMessage
        {
            From = _options.FromEmail.Trim(),
            To = message.ToEmail.Trim(),
            Subject = message.Subject,
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody
        };

        try
        {
            var messageId = await resend.EmailSendAsync(email, cancellationToken);
            return Result<NotificationEmailSendResult>.Success(new NotificationEmailSendResult(
                "Resend",
                messageId?.ToString() ?? string.Empty,
                DateTimeOffset.UtcNow));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<NotificationEmailSendResult>.Failure(
                "notifications.resend_send_failed",
                ToResendFailureMessage(exception));
        }
    }

    private static string ToResendFailureMessage(Exception exception)
    {
        var detail = exception.Message.Trim();
        return string.IsNullOrWhiteSpace(detail)
            ? "Resend rejected the test email. Check the sender address, recipient, and API key."
            : $"Resend rejected the test email: {detail}";
    }
}
