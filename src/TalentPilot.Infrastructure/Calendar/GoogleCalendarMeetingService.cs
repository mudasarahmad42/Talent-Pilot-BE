using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Calendar;
using TalentPilot.Common.Results;
using TalentPilot.Common.Time;

namespace TalentPilot.Infrastructure.Calendar;

public sealed class GoogleCalendarMeetingService : IGoogleCalendarService, ICalendarMeetingService
{
    private const string Provider = "GoogleCalendar";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string OAuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string SetupIncompleteCode = "calendar.google.setup_incomplete";
    private const string SetupIncompleteMessage = "Google Calendar setup is incomplete. Run database migrations, then connect the organizer calendar account.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly GoogleCalendarOptions _options;
    private readonly IGoogleCalendarConnectionRepository _repository;
    private readonly IGoogleCalendarTokenProtector _tokenProtector;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IClock _clock;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleCalendarMeetingService> _logger;

    public GoogleCalendarMeetingService(
        IOptions<GoogleCalendarOptions> options,
        IGoogleCalendarConnectionRepository repository,
        IGoogleCalendarTokenProtector tokenProtector,
        ICurrentUserAccessor currentUser,
        IClock clock,
        HttpClient httpClient,
        ILogger<GoogleCalendarMeetingService> logger)
    {
        _options = options.Value;
        _repository = repository;
        _tokenProtector = tokenProtector;
        _currentUser = currentUser;
        _clock = clock;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<GoogleCalendarConnectionStatus>> GetConnectionStatusAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.TenantId == Guid.Empty)
        {
            return Result<GoogleCalendarConnectionStatus>.Failure(
                "calendar.google.user_required",
                "Log in before checking Google Calendar connection status.");
        }

        GoogleCalendarConnection? connection;
        try
        {
            connection = await _repository.GetConnectionAsync(_currentUser.TenantId, Provider, cancellationToken);
        }
        catch (Exception exception) when (IsGoogleCalendarSchemaMissing(exception))
        {
            return GoogleCalendarSetupIncomplete<GoogleCalendarConnectionStatus>(
                exception,
                "checking Google Calendar connection status");
        }

        return Result<GoogleCalendarConnectionStatus>.Success(new GoogleCalendarConnectionStatus(
            connection is not null && !string.IsNullOrWhiteSpace(connection.RefreshTokenCiphertext),
            connection?.OrganizerEmail,
            connection?.ConnectedAtUtc,
            connection?.UpdatedAtUtc));
    }

    public async Task<Result<GoogleCalendarConnectResponse>> BuildConnectResponseAsync(CancellationToken cancellationToken)
    {
        var configValidation = ValidateOAuthConfiguration();
        if (configValidation is not null)
        {
            return Result<GoogleCalendarConnectResponse>.Failure(configValidation.Value.Code, configValidation.Value.Message);
        }

        if (_currentUser.TenantId == Guid.Empty || _currentUser.UserId == Guid.Empty)
        {
            return Result<GoogleCalendarConnectResponse>.Failure(
                "calendar.google.user_required",
                "Log in before connecting Google Calendar.");
        }

        var state = CreateStateToken();
        var stateHash = HashState(state);
        try
        {
            await _repository.StoreOAuthStateAsync(
                new GoogleCalendarOAuthState(
                    stateHash,
                    _currentUser.TenantId,
                    _currentUser.UserId,
                    _currentUser.Email,
                    _clock.UtcNow.AddMinutes(10),
                    _clock.UtcNow),
                cancellationToken);
        }
        catch (Exception exception) when (IsGoogleCalendarSchemaMissing(exception))
        {
            return GoogleCalendarSetupIncomplete<GoogleCalendarConnectResponse>(
                exception,
                "starting Google Calendar OAuth");
        }

        var authorizationUrl = BuildAuthorizationUrl(state);
        return Result<GoogleCalendarConnectResponse>.Success(new GoogleCalendarConnectResponse(authorizationUrl));
    }

    public async Task<Result<GoogleCalendarOAuthCallbackResult>> CompleteOAuthCallbackAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return CallbackRedirectError($"Google authorization failed: {SafeOAuthError(error)}");
        }

        var configValidation = ValidateOAuthConfiguration();
        if (configValidation is not null)
        {
            return CallbackRedirectError(configValidation.Value.Message);
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return CallbackRedirectError("Google did not return an authorization code.");
        }

        GoogleCalendarOAuthState? stateContext;
        try
        {
            stateContext = await _repository.ConsumeOAuthStateAsync(HashState(state), _clock.UtcNow, cancellationToken);
        }
        catch (Exception exception) when (IsGoogleCalendarSchemaMissing(exception))
        {
            return GoogleCalendarCallbackSetupIncomplete(exception, "completing Google Calendar OAuth");
        }

        if (stateContext is null)
        {
            return CallbackRedirectError("Google Calendar connection expired. Start the connection again.");
        }

        var tokenResult = await ExchangeAuthorizationCodeAsync(code, cancellationToken);
        if (tokenResult.Failed)
        {
            return CallbackRedirectError(tokenResult.Error.Message);
        }

        GoogleCalendarConnection? existing;
        try
        {
            existing = await _repository.GetConnectionAsync(stateContext.TenantId, Provider, cancellationToken);
        }
        catch (Exception exception) when (IsGoogleCalendarSchemaMissing(exception))
        {
            return GoogleCalendarCallbackSetupIncomplete(exception, "loading Google Calendar connection");
        }

        var refreshToken = tokenResult.Value.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken) && string.IsNullOrWhiteSpace(existing?.RefreshTokenCiphertext))
        {
            return CallbackRedirectError("Google did not return a refresh token. Reconnect and approve offline calendar access.");
        }

        var now = _clock.UtcNow;
        var accessTokenExpiresAt = tokenResult.Value.ExpiresInSeconds > 0
            ? now.AddSeconds(Math.Max(0, tokenResult.Value.ExpiresInSeconds - 60))
            : (DateTimeOffset?)null;

        try
        {
            await _repository.SaveConnectionAsync(
                new SaveGoogleCalendarConnectionInput(
                    stateContext.TenantId,
                    stateContext.UserId,
                    stateContext.UserEmail,
                    Provider,
                    string.IsNullOrWhiteSpace(refreshToken) ? null : _tokenProtector.Protect(refreshToken),
                    string.IsNullOrWhiteSpace(tokenResult.Value.AccessToken) ? null : _tokenProtector.Protect(tokenResult.Value.AccessToken),
                    accessTokenExpiresAt,
                    NormalizeScope(tokenResult.Value.Scope),
                    existing?.ConnectedAtUtc ?? now,
                    now),
                cancellationToken);
        }
        catch (Exception exception) when (IsGoogleCalendarSchemaMissing(exception))
        {
            return GoogleCalendarCallbackSetupIncomplete(exception, "saving Google Calendar connection");
        }

        return Result<GoogleCalendarOAuthCallbackResult>.Success(
            new GoogleCalendarOAuthCallbackResult(_options.FrontendSuccessRedirectUrl));
    }

    public async Task<Result<GoogleCalendarEventResponse>> CreateGoogleCalendarEventAsync(
        CreateGoogleCalendarEventInput input,
        CancellationToken cancellationToken)
    {
        var validation = ValidateEventInput(input);
        if (validation is not null)
        {
            return Result<GoogleCalendarEventResponse>.Failure(validation.Value.Code, validation.Value.Message);
        }

        if (_currentUser.TenantId == Guid.Empty)
        {
            return Result<GoogleCalendarEventResponse>.Failure(
                "calendar.google.user_required",
                "Log in before creating Google Calendar events.");
        }

        return await CreateEventForTenantAsync(_currentUser.TenantId, input, cancellationToken);
    }

    public async Task<Result<CalendarMeetingResult>> CreateMeetingAsync(
        CalendarMeetingRequest request,
        CancellationToken cancellationToken)
    {
        var input = new CreateGoogleCalendarEventInput(
            request.Title,
            request.Description,
            request.StartsAtUtc,
            request.EndsAtUtc,
            NormalizeTimeZone(request.TimeZoneId),
            request.Participants.Select(participant => participant.Email).ToArray(),
            null,
            request.CreateOnlineMeeting);

        var result = await CreateEventForTenantAsync(_currentUser.TenantId, input, cancellationToken, request.RequestId);
        if (result.Failed)
        {
            return Result<CalendarMeetingResult>.Failure(result.Error.Code, result.Error.Message);
        }

        return Result<CalendarMeetingResult>.Success(new CalendarMeetingResult(
            true,
            Provider,
            result.Value.EventId,
            result.Value.HtmlLink,
            result.Value.HangoutLink,
            "Google Calendar event created and participant invites were sent."));
    }

    private async Task<Result<GoogleCalendarEventResponse>> CreateEventForTenantAsync(
        Guid tenantId,
        CreateGoogleCalendarEventInput input,
        CancellationToken cancellationToken,
        string? requestId = null)
    {
        var validation = ValidateEventInput(input);
        if (validation is not null)
        {
            return Result<GoogleCalendarEventResponse>.Failure(validation.Value.Code, validation.Value.Message);
        }

        if (!_options.Enabled)
        {
            return Result<GoogleCalendarEventResponse>.Failure(
                "calendar.google.not_connected",
                "Google Calendar is not connected. Please connect an organizer account first.");
        }

        var configValidation = ValidateOAuthConfiguration();
        if (configValidation is not null)
        {
            return Result<GoogleCalendarEventResponse>.Failure(configValidation.Value.Code, configValidation.Value.Message);
        }

        GoogleCalendarConnection? connection;
        try
        {
            connection = await _repository.GetConnectionAsync(tenantId, Provider, cancellationToken);
        }
        catch (Exception exception) when (IsGoogleCalendarSchemaMissing(exception))
        {
            return GoogleCalendarSetupIncomplete<GoogleCalendarEventResponse>(
                exception,
                "loading Google Calendar connection for event creation");
        }

        if (connection is null || string.IsNullOrWhiteSpace(connection.RefreshTokenCiphertext))
        {
            return Result<GoogleCalendarEventResponse>.Failure(
                "calendar.google.not_connected",
                "Google Calendar is not connected. Please connect an organizer account first.");
        }

        GoogleOAuthTokenResponse accessToken;
        try
        {
            accessToken = await RefreshAccessTokenAsync(
                _tokenProtector.Unprotect(connection.RefreshTokenCiphertext),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Google Calendar token refresh failed for tenant {TenantId}.", tenantId);
            return Result<GoogleCalendarEventResponse>.Failure(
                "calendar.google.refresh_failed",
                "Google Calendar access could not be refreshed. Reconnect the organizer calendar account.");
        }

        if (string.IsNullOrWhiteSpace(accessToken.AccessToken))
        {
            return Result<GoogleCalendarEventResponse>.Failure(
                "calendar.google.refresh_failed",
                "Google Calendar access could not be refreshed. Reconnect the organizer calendar account.");
        }

        var now = _clock.UtcNow;
        await _repository.UpdateAccessTokenAsync(
            tenantId,
            Provider,
            _tokenProtector.Protect(accessToken.AccessToken),
            accessToken.ExpiresInSeconds > 0 ? now.AddSeconds(Math.Max(0, accessToken.ExpiresInSeconds - 60)) : null,
            now,
            cancellationToken);

        var attendees = NormalizeAttendees(input.Attendees);
        var calendarEvent = new GoogleCalendarEventRequest
        {
            Summary = input.Summary.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            Location = string.IsNullOrWhiteSpace(input.Location) ? null : input.Location.Trim(),
            Start = new GoogleCalendarDateTime(input.StartDateTime.ToString("O", CultureInfo.InvariantCulture), NormalizeTimeZone(input.TimeZone)),
            End = new GoogleCalendarDateTime(input.EndDateTime.ToString("O", CultureInfo.InvariantCulture), NormalizeTimeZone(input.TimeZone)),
            Attendees = attendees.Select(email => new GoogleCalendarAttendee(email)).ToArray(),
            ConferenceData = input.CreateGoogleMeet
                ? new GoogleCalendarConferenceData(
                    new GoogleCalendarConferenceCreateRequest(
                        string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId,
                        new GoogleCalendarConferenceSolutionKey("hangoutsMeet")))
                : null,
            GuestsCanInviteOthers = false,
            GuestsCanModify = false,
            GuestsCanSeeOtherGuests = true
        };

        var uri = BuildCalendarInsertUri(calendarEvent.ConferenceData is not null);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(calendarEvent, options: JsonOptions)
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.AccessToken);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Google Calendar rejected event creation for tenant {TenantId}. StatusCode={StatusCode}.",
                tenantId,
                response.StatusCode);

            return Result<GoogleCalendarEventResponse>.Failure(
                "calendar.google.rejected",
                "Google Calendar rejected the event request. Check Calendar API access, organizer permissions, and attendee emails.");
        }

        var created = await response.Content.ReadFromJsonAsync<GoogleCalendarEventCreatedResponse>(JsonOptions, cancellationToken);
        if (created is null || string.IsNullOrWhiteSpace(created.Id))
        {
            return Result<GoogleCalendarEventResponse>.Failure(
                "calendar.google.invalid_response",
                "Google Calendar returned an invalid event response.");
        }

        return Result<GoogleCalendarEventResponse>.Success(new GoogleCalendarEventResponse(
            created.Id,
            created.HtmlLink,
            created.HangoutLink ?? created.ConferenceData?.EntryPoints?
                .FirstOrDefault(entryPoint => string.Equals(entryPoint.EntryPointType, "video", StringComparison.OrdinalIgnoreCase))
                ?.Uri,
            input.StartDateTime,
            input.EndDateTime,
            attendees));
    }

    private async Task<Result<GoogleOAuthTokenResponse>> ExchangeAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId.Trim(),
            ["client_secret"] = _options.ClientSecret.Trim(),
            ["code"] = code.Trim(),
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = _options.RedirectUri.Trim()
        });

        using var response = await _httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google OAuth code exchange failed. StatusCode={StatusCode}.", response.StatusCode);
            return Result<GoogleOAuthTokenResponse>.Failure(
                "calendar.google.oauth_exchange_failed",
                "Google authorization code could not be exchanged. Check OAuth credentials and redirect URI.");
        }

        var token = await response.Content.ReadFromJsonAsync<GoogleOAuthTokenResponse>(JsonOptions, cancellationToken);
        return token is null
            ? Result<GoogleOAuthTokenResponse>.Failure(
                "calendar.google.oauth_invalid_response",
                "Google returned an invalid OAuth token response.")
            : Result<GoogleOAuthTokenResponse>.Success(token);
    }

    private async Task<GoogleOAuthTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId.Trim(),
            ["client_secret"] = _options.ClientSecret.Trim(),
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        });

        using var response = await _httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google OAuth refresh failed. StatusCode={StatusCode}.", response.StatusCode);
            return new GoogleOAuthTokenResponse();
        }

        return await response.Content.ReadFromJsonAsync<GoogleOAuthTokenResponse>(JsonOptions, cancellationToken)
            ?? new GoogleOAuthTokenResponse();
    }

    private string BuildAuthorizationUrl(string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId.Trim(),
            ["redirect_uri"] = _options.RedirectUri.Trim(),
            ["scope"] = NormalizeScope(_options.Scope),
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true",
            ["state"] = state
        };

        return $"{OAuthEndpoint}?{ToQueryString(query)}";
    }

    private string BuildCalendarInsertUri(bool includeConferenceData)
    {
        var calendarId = WebUtility.UrlEncode(NormalizeCalendarId());
        var query = new Dictionary<string, string?>
        {
            ["sendUpdates"] = "all",
            ["conferenceDataVersion"] = includeConferenceData ? "1" : "0"
        };

        return $"https://www.googleapis.com/calendar/v3/calendars/{calendarId}/events?{ToQueryString(query)}";
    }

    private (string Code, string Message)? ValidateOAuthConfiguration()
    {
        if (!_options.Enabled)
        {
            return ("calendar.google.disabled", "Google Calendar integration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret) ||
            string.IsNullOrWhiteSpace(_options.RedirectUri))
        {
            return ("calendar.google.not_configured", "Google Calendar OAuth credentials are not configured.");
        }

        return null;
    }

    private static (string Code, string Message)? ValidateEventInput(CreateGoogleCalendarEventInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Summary))
        {
            return ("calendar.google.summary_required", "Event summary is required.");
        }

        if (input.StartDateTime == default || input.EndDateTime == default || input.EndDateTime <= input.StartDateTime)
        {
            return ("calendar.google.time_invalid", "Event end time must be after start time.");
        }

        if (input.Attendees is null || input.Attendees.Count == 0)
        {
            return ("calendar.google.attendees_required", "Add at least one attendee email.");
        }

        try
        {
            _ = NormalizeAttendees(input.Attendees);
        }
        catch (FormatException)
        {
            return ("calendar.google.attendee_email_invalid", "One or more attendee emails are invalid.");
        }

        return null;
    }

    private static IReadOnlyList<string> NormalizeAttendees(IReadOnlyList<string> attendees)
    {
        return attendees
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => new MailAddress(email.Trim()).Address)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string NormalizeCalendarId()
    {
        return string.IsNullOrWhiteSpace(_options.CalendarId) ? "primary" : _options.CalendarId.Trim();
    }

    private string NormalizeTimeZone(string? requestTimeZone)
    {
        if (!string.IsNullOrWhiteSpace(requestTimeZone))
        {
            return requestTimeZone.Trim();
        }

        return string.IsNullOrWhiteSpace(_options.DefaultTimeZoneId) ? "UTC" : _options.DefaultTimeZoneId.Trim();
    }

    private string NormalizeScope(string? scope)
    {
        return string.IsNullOrWhiteSpace(scope)
            ? "https://www.googleapis.com/auth/calendar.events"
            : scope.Trim();
    }

    private Result<T> GoogleCalendarSetupIncomplete<T>(Exception exception, string action)
    {
        _logger.LogError(exception, "Google Calendar storage setup is incomplete while {Action}.", action);
        return Result<T>.Failure(SetupIncompleteCode, SetupIncompleteMessage);
    }

    private Result<GoogleCalendarOAuthCallbackResult> GoogleCalendarCallbackSetupIncomplete(Exception exception, string action)
    {
        _logger.LogError(exception, "Google Calendar storage setup is incomplete while {Action}.", action);
        return CallbackRedirectError(SetupIncompleteMessage);
    }

    private Result<GoogleCalendarOAuthCallbackResult> CallbackRedirectError(string message)
    {
        return Result<GoogleCalendarOAuthCallbackResult>.Success(
            new GoogleCalendarOAuthCallbackResult(AppendSafeMessage(_options.FrontendErrorRedirectUrl, message)));
    }

    private static bool IsGoogleCalendarSchemaMissing(Exception exception)
    {
        return EnumerateExceptions(exception).Any(current =>
        {
            if (current is SqlException sqlException)
            {
                foreach (SqlError error in sqlException.Errors)
                {
                    if (error.Number == 208 && IsGoogleCalendarTableError(error.Message))
                    {
                        return true;
                    }
                }
            }

            return IsGoogleCalendarTableError(current.Message);
        });
    }

    private static bool IsGoogleCalendarTableError(string? message)
    {
        return !string.IsNullOrWhiteSpace(message) &&
            message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("GoogleCalendar", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private static string SafeOAuthError(string error)
    {
        return error.Length > 80 ? error[..80] : error;
    }

    private static string AppendSafeMessage(string url, string message)
    {
        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}message={Uri.EscapeDataString(message)}";
    }

    private static string ToQueryString(IReadOnlyDictionary<string, string?> values)
    {
        return string.Join('&', values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
    }

    private static string CreateStateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashState(string state)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(state));
        return Convert.ToHexString(hash);
    }

    private sealed class GoogleOAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresInSeconds { get; init; }

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = string.Empty;
    }

    private sealed record GoogleCalendarEventRequest
    {
        public string Summary { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? Location { get; init; }
        public GoogleCalendarDateTime Start { get; init; } = new(string.Empty, "UTC");
        public GoogleCalendarDateTime End { get; init; } = new(string.Empty, "UTC");
        public IReadOnlyList<GoogleCalendarAttendee> Attendees { get; init; } = [];
        public GoogleCalendarConferenceData? ConferenceData { get; init; }
        public bool GuestsCanInviteOthers { get; init; }
        public bool GuestsCanModify { get; init; }
        public bool GuestsCanSeeOtherGuests { get; init; }
    }

    private sealed record GoogleCalendarDateTime(string DateTime, string TimeZone);

    private sealed record GoogleCalendarAttendee(string Email);

    private sealed record GoogleCalendarConferenceData(GoogleCalendarConferenceCreateRequest CreateRequest);

    private sealed record GoogleCalendarConferenceCreateRequest(
        string RequestId,
        GoogleCalendarConferenceSolutionKey ConferenceSolutionKey);

    private sealed record GoogleCalendarConferenceSolutionKey(string Type);

    private sealed class GoogleCalendarEventCreatedResponse
    {
        public string Id { get; init; } = string.Empty;
        public string? HtmlLink { get; init; }
        public string? HangoutLink { get; init; }
        public GoogleCalendarCreatedConferenceData? ConferenceData { get; init; }
    }

    private sealed class GoogleCalendarCreatedConferenceData
    {
        public IReadOnlyList<GoogleCalendarCreatedEntryPoint>? EntryPoints { get; init; }
    }

    private sealed class GoogleCalendarCreatedEntryPoint
    {
        public string? EntryPointType { get; init; }
        public string? Uri { get; init; }
    }
}
