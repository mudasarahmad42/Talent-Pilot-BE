namespace TalentPilot.Infrastructure.Notifications;

public sealed class MicrosoftGraphEmailOptions
{
    public string TenantId { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string FromEmail { get; init; } = string.Empty;

    public bool SaveToSentItems { get; init; } = true;
}
