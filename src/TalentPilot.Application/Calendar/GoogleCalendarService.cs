using TalentPilot.Common.Results;

namespace TalentPilot.Application.Calendar;

public sealed record GoogleCalendarConnectResponse(string AuthorizationUrl);

public sealed record GoogleCalendarConnectionStatus(
    bool Connected,
    string? OrganizerEmail,
    DateTimeOffset? ConnectedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record GoogleCalendarOAuthCallbackResult(string RedirectUrl);

public sealed record CreateGoogleCalendarEventInput(
    string Summary,
    string? Description,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string TimeZone,
    IReadOnlyList<string> Attendees,
    string? Location,
    bool CreateGoogleMeet);

public sealed record GoogleCalendarEventResponse(
    string EventId,
    string? HtmlLink,
    string? HangoutLink,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    IReadOnlyList<string> Attendees);

public sealed record GoogleCalendarOAuthState(
    string StateHash,
    Guid TenantId,
    Guid UserId,
    string UserEmail,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ConsumedAtUtc = null);

public sealed record GoogleCalendarConnection(
    Guid TenantId,
    Guid OrganizerUserId,
    string OrganizerEmail,
    string Provider,
    string? RefreshTokenCiphertext,
    string? AccessTokenCiphertext,
    DateTimeOffset? AccessTokenExpiresAtUtc,
    string Scope,
    string Status,
    DateTimeOffset ConnectedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record SaveGoogleCalendarConnectionInput(
    Guid TenantId,
    Guid OrganizerUserId,
    string OrganizerEmail,
    string Provider,
    string? RefreshTokenCiphertext,
    string? AccessTokenCiphertext,
    DateTimeOffset? AccessTokenExpiresAtUtc,
    string Scope,
    DateTimeOffset ConnectedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public interface IGoogleCalendarService
{
    Task<Result<GoogleCalendarConnectionStatus>> GetConnectionStatusAsync(CancellationToken cancellationToken);

    Task<Result<GoogleCalendarConnectResponse>> BuildConnectResponseAsync(CancellationToken cancellationToken);

    Task<Result<GoogleCalendarOAuthCallbackResult>> CompleteOAuthCallbackAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken);

    Task<Result<GoogleCalendarEventResponse>> CreateGoogleCalendarEventAsync(
        CreateGoogleCalendarEventInput input,
        CancellationToken cancellationToken);
}

public interface IGoogleCalendarConnectionRepository
{
    Task StoreOAuthStateAsync(GoogleCalendarOAuthState state, CancellationToken cancellationToken);

    Task<GoogleCalendarOAuthState?> ConsumeOAuthStateAsync(
        string stateHash,
        DateTimeOffset consumedAtUtc,
        CancellationToken cancellationToken);

    Task<GoogleCalendarConnection?> GetConnectionAsync(
        Guid tenantId,
        string provider,
        CancellationToken cancellationToken);

    Task SaveConnectionAsync(
        SaveGoogleCalendarConnectionInput input,
        CancellationToken cancellationToken);

    Task UpdateAccessTokenAsync(
        Guid tenantId,
        string provider,
        string? accessTokenCiphertext,
        DateTimeOffset? accessTokenExpiresAtUtc,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
}

public interface IGoogleCalendarTokenProtector
{
    string Protect(string token);

    string Unprotect(string protectedToken);
}
