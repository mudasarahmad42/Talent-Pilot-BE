using System.Net.Mail;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Application.Notifications;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Feedback;

public sealed class PublicFeedbackService : IPublicFeedbackService
{
    private const int NameMaxLength = 120;
    private const int EmailMaxLength = 254;
    private const int MessageMaxLength = 3000;
    private const string ThankYouSubject = "Thank you for your Talent Pilot feedback";

    private readonly IPublicFeedbackTenantResolver _tenantResolver;
    private readonly INotificationEmailSender _emailSender;
    private readonly ICurrentUserAccessor _currentUser;

    public PublicFeedbackService(
        IPublicFeedbackTenantResolver tenantResolver,
        INotificationEmailSender emailSender,
        ICurrentUserAccessor currentUser)
    {
        _tenantResolver = tenantResolver;
        _emailSender = emailSender;
        _currentUser = currentUser;
    }

    public async Task<Result<SubmitPublicFeedbackResponse>> SubmitAsync(
        SubmitPublicFeedbackInput input,
        CancellationToken cancellationToken)
    {
        var nameResult = NormalizeRequiredText(input.Name, "feedback.name_invalid", "Your name is required.", NameMaxLength);
        if (nameResult.Failed)
        {
            return Result<SubmitPublicFeedbackResponse>.Failure(nameResult.Error.Code, nameResult.Error.Message);
        }

        var emailResult = NormalizeEmail(input.Email, "feedback.email_invalid", "A valid email address is required.");
        if (emailResult.Failed)
        {
            return Result<SubmitPublicFeedbackResponse>.Failure(emailResult.Error.Code, emailResult.Error.Message);
        }

        var messageResult = NormalizeMessage(input.Message);
        if (messageResult.Failed)
        {
            return Result<SubmitPublicFeedbackResponse>.Failure(messageResult.Error.Code, messageResult.Error.Message);
        }

        var recipientResult = NormalizeEmail(input.RecipientEmail, "feedback.recipient_invalid", "Feedback recipient email is not configured.");
        if (recipientResult.Failed)
        {
            return Result<SubmitPublicFeedbackResponse>.Failure(recipientResult.Error.Code, recipientResult.Error.Message);
        }

        var senderResult = NormalizeEmail(input.SenderEmail, "feedback.sender_invalid", "Feedback sender email is not configured.");
        if (senderResult.Failed)
        {
            return Result<SubmitPublicFeedbackResponse>.Failure(
                "feedback.delivery_failed",
                "Feedback could not be sent right now. Please try again shortly.");
        }

        var tenant = await ResolveTenantAsync(input, cancellationToken);
        if (tenant is null)
        {
            return Result<SubmitPublicFeedbackResponse>.Failure(
                "feedback.tenant_not_found",
                "Feedback is unavailable until a public tenant is configured.");
        }

        var name = nameResult.Value;
        var email = emailResult.Value;
        var message = messageResult.Value;
        var recipientEmail = recipientResult.Value;
        var senderEmail = senderResult.Value;
        var submittedAtUtc = DateTimeOffset.UtcNow;

        var adminEmail = BuildAdminFeedbackEmail(tenant, recipientEmail, senderEmail, name, email, message, submittedAtUtc);
        var adminSendResult = await _emailSender.SendAsync(adminEmail, cancellationToken);
        if (adminSendResult.Failed)
        {
            return Result<SubmitPublicFeedbackResponse>.Failure(
                "feedback.delivery_failed",
                "Feedback could not be sent right now. Please try again shortly.");
        }

        var thankYouEmail = BuildThankYouEmail(tenant, senderEmail, name, email);
        await _emailSender.SendAsync(thankYouEmail, cancellationToken);

        return Result<SubmitPublicFeedbackResponse>.Success(new SubmitPublicFeedbackResponse(
            adminSendResult.Value.Provider,
            adminSendResult.Value.MessageId,
            adminSendResult.Value.SubmittedAtUtc));
    }

    private async Task<PublicFeedbackTenant?> ResolveTenantAsync(
        SubmitPublicFeedbackInput input,
        CancellationToken cancellationToken)
    {
        var resolved = await _tenantResolver.ResolveAsync(
            new PublicFeedbackTenantQuery(input.TenantSlug, input.JobPostId),
            cancellationToken);

        if (resolved is not null)
        {
            return resolved;
        }

        return _currentUser.TenantId == Guid.Empty
            ? null
            : new PublicFeedbackTenant(_currentUser.TenantId, "Talent Pilot", null);
    }

    private static NotificationEmailMessage BuildAdminFeedbackEmail(
        PublicFeedbackTenant tenant,
        string recipientEmail,
        string senderEmail,
        string name,
        string email,
        string message,
        DateTimeOffset submittedAtUtc)
    {
        var subject = $"New Talent Pilot feedback from {name}";
        var body = $"""
            A visitor submitted feedback from Talent Pilot.

            Please review the message below and follow up directly if needed.
            """;

        var details = new List<(string Label, string Value)>
        {
            ("Name", name),
            ("Email", email),
            ("Tenant", tenant.DisplayName),
            ("Submitted", submittedAtUtc.ToString("u")),
            ("Message", message)
        };

        return new NotificationEmailMessage(
            tenant.TenantId,
            recipientEmail,
            subject,
            $"{body}\n\nName: {name}\nEmail: {email}\nTenant: {tenant.DisplayName}\nSubmitted: {submittedAtUtc:u}\n\nMessage:\n{message}",
            TalentPilotEmailTemplate.Build(
                "Product Feedback",
                "New feedback received",
                body,
                details,
                actionLabel: "Reply by email",
                actionUrl: $"mailto:{email}"),
            senderEmail);
    }

    private static NotificationEmailMessage BuildThankYouEmail(
        PublicFeedbackTenant tenant,
        string senderEmail,
        string name,
        string email)
    {
        var body = $"""
            Hi {name},

            Thank you for sharing feedback on Talent Pilot. We have received your message and will use it to improve the application experience.
            """;

        return new NotificationEmailMessage(
            tenant.TenantId,
            email,
            ThankYouSubject,
            body,
            TalentPilotEmailTemplate.Build(
                "Thank You",
                "We received your feedback",
                body,
                details: [("Product", "Talent Pilot"), ("Tenant", tenant.DisplayName)],
                preheader: "Thanks for helping us improve Talent Pilot."),
            senderEmail);
    }

    private static Result<string> NormalizeRequiredText(
        string? value,
        string errorCode,
        string requiredMessage,
        int maxLength)
    {
        var normalized = NormalizeWhitespace(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result<string>.Failure(errorCode, requiredMessage);
        }

        if (normalized.Length > maxLength)
        {
            return Result<string>.Failure(errorCode, $"Value must be {maxLength} characters or fewer.");
        }

        return Result<string>.Success(normalized);
    }

    private static Result<string> NormalizeMessage(string? value)
    {
        var normalized = (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result<string>.Failure("feedback.message_invalid", "Feedback message is required.");
        }

        if (normalized.Length > MessageMaxLength)
        {
            return Result<string>.Failure("feedback.message_invalid", $"Feedback message must be {MessageMaxLength} characters or fewer.");
        }

        return Result<string>.Success(normalized);
    }

    private static Result<string> NormalizeEmail(string? email, string errorCode, string errorMessage)
    {
        var normalized = NormalizeWhitespace(email).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > EmailMaxLength)
        {
            return Result<string>.Failure(errorCode, errorMessage);
        }

        try
        {
            var address = new MailAddress(normalized);
            return string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase)
                ? Result<string>.Success(normalized)
                : Result<string>.Failure(errorCode, errorMessage);
        }
        catch (FormatException)
        {
            return Result<string>.Failure(errorCode, errorMessage);
        }
    }

    private static string NormalizeWhitespace(string? value)
    {
        return string.Join(
            " ",
            (value ?? string.Empty).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
