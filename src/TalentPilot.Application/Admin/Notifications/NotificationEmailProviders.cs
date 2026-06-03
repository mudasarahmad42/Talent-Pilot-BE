namespace TalentPilot.Application.Admin.Notifications;

public static class NotificationEmailProviders
{
    public const string Resend = "Resend";
    public const string MicrosoftGraph = "MicrosoftGraph";

    public static bool IsSupported(string? provider)
    {
        var normalized = Normalize(provider);
        return normalized is Resend or MicrosoftGraph;
    }

    public static string Normalize(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return string.Empty;
        }

        var trimmed = provider.Trim();
        if (string.Equals(trimmed, Resend, StringComparison.OrdinalIgnoreCase))
        {
            return Resend;
        }

        if (string.Equals(trimmed, MicrosoftGraph, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Microsoft Graph", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            return MicrosoftGraph;
        }

        return trimmed;
    }

    public static string NormalizeOrDefault(string? provider)
    {
        var normalized = Normalize(provider);
        return IsSupported(normalized) ? normalized : Resend;
    }
}
