namespace TalentPilot.Infrastructure.Notifications;

public sealed class ResendEmailOptions
{
    public string ApiKey { get; init; } = string.Empty;

    public string FromEmail { get; init; } = "onboarding@resend.dev";
}
