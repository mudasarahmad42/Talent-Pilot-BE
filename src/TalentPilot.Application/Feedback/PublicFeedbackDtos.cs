namespace TalentPilot.Application.Feedback;

public sealed record SubmitPublicFeedbackInput(
    string? Name,
    string? Email,
    string? Message,
    string? TenantSlug,
    Guid? JobPostId,
    string RecipientEmail,
    string SenderEmail);

public sealed record SubmitPublicFeedbackResponse(
    string Provider,
    string MessageId,
    DateTimeOffset SubmittedAtUtc);

public sealed record PublicFeedbackTenantQuery(
    string? TenantSlug,
    Guid? JobPostId);

public sealed record PublicFeedbackTenant(
    Guid TenantId,
    string DisplayName,
    string? Slug);
