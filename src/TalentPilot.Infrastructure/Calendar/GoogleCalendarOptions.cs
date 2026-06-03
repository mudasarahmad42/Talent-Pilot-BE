namespace TalentPilot.Infrastructure.Calendar;

public sealed class GoogleCalendarOptions
{
    public bool Enabled { get; init; }

    public string ApplicationName { get; init; } = "Talent Pilot";

    public string CalendarId { get; init; } = "primary";

    public string DefaultTimeZoneId { get; init; } = "UTC";

    public bool CreateGoogleMeetLink { get; init; } = true;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string RedirectUri { get; init; } = string.Empty;

    public string Scope { get; init; } = "https://www.googleapis.com/auth/calendar.events";

    public string FrontendSuccessRedirectUrl { get; init; } = "/settings/integrations/google-calendar/success";

    public string FrontendErrorRedirectUrl { get; init; } = "/settings/integrations/google-calendar/error";

    public string TokenProtectionKey { get; init; } = string.Empty;
}
