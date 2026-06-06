using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Calendar;
using TalentPilot.Common.Time;
using TalentPilot.Infrastructure.Calendar;

namespace TalentPilot.Tests.Calendar;

public sealed class GoogleCalendarMeetingServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BuildConnectResponseAsync_StoresStateAndBuildsGoogleOAuthUrl()
    {
        var repository = new FakeGoogleCalendarConnectionRepository();
        using var httpClient = new HttpClient(new FakeGoogleHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var service = CreateService(repository, httpClient);

        var result = await service.BuildConnectResponseAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        var uri = new Uri(result.Value.AuthorizationUrl);
        var query = ParseQuery(uri.Query);

        Assert.Equal("https://accounts.google.com/o/oauth2/v2/auth", uri.GetLeftPart(UriPartial.Path));
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("client-id.apps.googleusercontent.com", query["client_id"]);
        Assert.Equal("http://localhost:5058/api/google-calendar/oauth/callback", query["redirect_uri"]);
        Assert.Equal("https://www.googleapis.com/auth/calendar.events", query["scope"]);
        Assert.Equal("offline", query["access_type"]);
        Assert.Equal("consent", query["prompt"]);
        Assert.Equal("true", query["include_granted_scopes"]);

        var state = query["state"];
        Assert.NotEmpty(state);
        var storedState = Assert.Single(repository.StoredStates);
        Assert.Equal(HashState(state), storedState.StateHash);
        Assert.Equal(TenantId, storedState.TenantId);
        Assert.Equal(UserId, storedState.UserId);
        Assert.Equal("organizer@tkxel.com", storedState.UserEmail);
        Assert.Equal(Now.AddMinutes(10), storedState.ExpiresAtUtc);
    }

    [Fact]
    public async Task CreateGoogleCalendarEventAsync_WhenNotConnected_ReturnsClearError()
    {
        var repository = new FakeGoogleCalendarConnectionRepository();
        using var httpClient = new HttpClient(new FakeGoogleHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var service = CreateService(repository, httpClient);

        var result = await service.CreateGoogleCalendarEventAsync(CreateEventInput(), CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("calendar.google.not_connected", result.Error.Code);
        Assert.Equal("Google Calendar is not connected. Please connect an organizer account first.", result.Error.Message);
    }

    [Fact]
    public async Task CreateGoogleCalendarEventAsync_WhenGoogleCalendarSchemaMissing_ReturnsSetupError()
    {
        var repository = new FakeGoogleCalendarConnectionRepository
        {
            GetConnectionException = new InvalidOperationException("Invalid object name 'dbo.GoogleCalendarConnections'.")
        };
        using var httpClient = new HttpClient(new FakeGoogleHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var service = CreateService(repository, httpClient);

        var result = await service.CreateGoogleCalendarEventAsync(CreateEventInput(), CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("calendar.google.setup_incomplete", result.Error.Code);
        Assert.Equal(
            "Google Calendar setup is incomplete. Run database migrations, then connect the organizer calendar account.",
            result.Error.Message);
    }

    [Fact]
    public async Task BuildConnectResponseAsync_WhenGoogleCalendarSchemaMissing_ReturnsSetupError()
    {
        var repository = new FakeGoogleCalendarConnectionRepository
        {
            StoreStateException = new InvalidOperationException("Invalid object name 'dbo.GoogleCalendarOAuthStates'.")
        };
        using var httpClient = new HttpClient(new FakeGoogleHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var service = CreateService(repository, httpClient);

        var result = await service.BuildConnectResponseAsync(CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("calendar.google.setup_incomplete", result.Error.Code);
        Assert.Equal(
            "Google Calendar setup is incomplete. Run database migrations, then connect the organizer calendar account.",
            result.Error.Message);
    }

    [Fact]
    public async Task CreateGoogleCalendarEventAsync_RefreshesTokenAndCreatesMeetEvent()
    {
        var repository = new FakeGoogleCalendarConnectionRepository
        {
            Connection = new GoogleCalendarConnection(
                TenantId,
                UserId,
                "organizer@tkxel.com",
                "GoogleCalendar",
                "protected-refresh-token",
                null,
                null,
                "https://www.googleapis.com/auth/calendar.events",
                "Connected",
                Now,
                Now)
        };
        HttpRequestMessage? calendarRequest = null;
        string? calendarRequestBody = null;
        var handler = new FakeGoogleHttpHandler(async request =>
        {
            if (request.RequestUri?.Host == "oauth2.googleapis.com")
            {
                var tokenForm = await request.Content!.ReadAsStringAsync();
                Assert.Contains("grant_type=refresh_token", tokenForm);
                Assert.Contains("refresh_token=refresh-token", tokenForm);

                return JsonResponse("""
                    {
                      "access_token": "access-token-1",
                      "expires_in": 3600,
                      "scope": "https://www.googleapis.com/auth/calendar.events"
                    }
                    """);
            }

            if (request.RequestUri?.Host == "www.googleapis.com")
            {
                calendarRequest = request;
                calendarRequestBody = await request.Content!.ReadAsStringAsync();

                return JsonResponse("""
                    {
                      "id": "event-123",
                      "htmlLink": "https://calendar.google.com/event?eid=event-123",
                      "hangoutLink": "https://meet.google.com/abc-defg-hij"
                    }
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        using var httpClient = new HttpClient(handler);
        var service = CreateService(repository, httpClient);

        var result = await service.CreateGoogleCalendarEventAsync(CreateEventInput(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("event-123", result.Value.EventId);
        Assert.Equal("https://calendar.google.com/event?eid=event-123", result.Value.HtmlLink);
        Assert.Equal("https://meet.google.com/abc-defg-hij", result.Value.HangoutLink);
        Assert.Equal("protected-access-token-1", repository.UpdatedAccessTokenCiphertext);
        Assert.Equal(Now.AddSeconds(3540), repository.UpdatedAccessTokenExpiresAtUtc);

        Assert.NotNull(calendarRequest);
        Assert.Equal(HttpMethod.Post, calendarRequest!.Method);
        Assert.Equal("Bearer", calendarRequest.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", calendarRequest.Headers.Authorization?.Parameter);
        Assert.Contains("/calendar/v3/calendars/primary/events", calendarRequest.RequestUri!.AbsoluteUri);
        Assert.Contains("sendUpdates=all", calendarRequest.RequestUri!.Query);
        Assert.Contains("conferenceDataVersion=1", calendarRequest.RequestUri!.Query);

        Assert.NotNull(calendarRequestBody);
        Assert.Contains("\"summary\":\"Technical Interview\"", calendarRequestBody);
        Assert.Contains("\"dateTime\":\"2026-06-02T10:00:00.0000000", calendarRequestBody);
        Assert.Contains("\"timeZone\":\"Asia/Karachi\"", calendarRequestBody);
        Assert.Contains("\"email\":\"candidate@example.com\"", calendarRequestBody);
        Assert.Contains("\"type\":\"hangoutsMeet\"", calendarRequestBody);
        Assert.Contains("\"guestsCanInviteOthers\":false", calendarRequestBody);
    }

    private static GoogleCalendarMeetingService CreateService(
        IGoogleCalendarConnectionRepository repository,
        HttpClient httpClient)
    {
        return new GoogleCalendarMeetingService(
            Options.Create(new GoogleCalendarOptions
            {
                Enabled = true,
                ClientId = "client-id.apps.googleusercontent.com",
                ClientSecret = "client-secret",
                RedirectUri = "http://localhost:5058/api/google-calendar/oauth/callback",
                Scope = "https://www.googleapis.com/auth/calendar.events",
                FrontendSuccessRedirectUrl = "http://localhost:4200/settings/integrations/google-calendar/success",
                FrontendErrorRedirectUrl = "http://localhost:4200/settings/integrations/google-calendar/error",
                CalendarId = "primary",
                DefaultTimeZoneId = "UTC"
            }),
            repository,
            new FakeGoogleCalendarTokenProtector(),
            new FakeCurrentUserAccessor(),
            new FakeClock(),
            httpClient,
            NullLogger<GoogleCalendarMeetingService>.Instance);
    }

    private static CreateGoogleCalendarEventInput CreateEventInput()
    {
        return new CreateGoogleCalendarEventInput(
            "Technical Interview",
            "Interview details",
            new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 2, 11, 0, 0, TimeSpan.Zero),
            "Asia/Karachi",
            new[] { "candidate@example.com", "interviewer@example.com" },
            null,
            true);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => Uri.UnescapeDataString(part[0]),
                part => part.Length == 2 ? Uri.UnescapeDataString(part[1]) : string.Empty);
    }

    private static string HashState(string state)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(state));
        return Convert.ToHexString(hash);
    }

    private sealed class FakeGoogleCalendarConnectionRepository : IGoogleCalendarConnectionRepository
    {
        public List<GoogleCalendarOAuthState> StoredStates { get; } = [];

        public GoogleCalendarConnection? Connection { get; set; }

        public Exception? GetConnectionException { get; set; }

        public Exception? StoreStateException { get; set; }

        public string? UpdatedAccessTokenCiphertext { get; private set; }

        public DateTimeOffset? UpdatedAccessTokenExpiresAtUtc { get; private set; }

        public Task StoreOAuthStateAsync(GoogleCalendarOAuthState state, CancellationToken cancellationToken)
        {
            if (StoreStateException is not null)
            {
                throw StoreStateException;
            }

            StoredStates.Add(state);
            return Task.CompletedTask;
        }

        public Task<GoogleCalendarOAuthState?> ConsumeOAuthStateAsync(
            string stateHash,
            DateTimeOffset consumedAtUtc,
            CancellationToken cancellationToken)
        {
            var state = StoredStates.FirstOrDefault(stored => stored.StateHash == stateHash);
            return Task.FromResult(state);
        }

        public Task<GoogleCalendarConnection?> GetConnectionAsync(
            Guid tenantId,
            string provider,
            CancellationToken cancellationToken)
        {
            if (GetConnectionException is not null)
            {
                throw GetConnectionException;
            }

            return Task.FromResult(Connection);
        }

        public Task SaveConnectionAsync(
            SaveGoogleCalendarConnectionInput input,
            CancellationToken cancellationToken)
        {
            Connection = new GoogleCalendarConnection(
                input.TenantId,
                input.OrganizerUserId,
                input.OrganizerEmail,
                input.Provider,
                input.RefreshTokenCiphertext,
                input.AccessTokenCiphertext,
                input.AccessTokenExpiresAtUtc,
                input.Scope,
                "Connected",
                input.ConnectedAtUtc,
                input.UpdatedAtUtc);
            return Task.CompletedTask;
        }

        public Task UpdateAccessTokenAsync(
            Guid tenantId,
            string provider,
            string? accessTokenCiphertext,
            DateTimeOffset? accessTokenExpiresAtUtc,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken)
        {
            UpdatedAccessTokenCiphertext = accessTokenCiphertext;
            UpdatedAccessTokenExpiresAtUtc = accessTokenExpiresAtUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGoogleCalendarTokenProtector : IGoogleCalendarTokenProtector
    {
        public string Protect(string token) => $"protected-{token}";

        public string Unprotect(string protectedToken) => protectedToken.StartsWith("protected-", StringComparison.Ordinal)
            ? protectedToken["protected-".Length..]
            : protectedToken;
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public Guid UserId => GoogleCalendarMeetingServiceTests.UserId;

        public Guid TenantId => GoogleCalendarMeetingServiceTests.TenantId;

        public string Email => "organizer@tkxel.com";

        public IReadOnlyCollection<string> RoleCodes => Array.Empty<string>();
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FakeGoogleHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _sendAsync;

        public FakeGoogleHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
            : this(request => Task.FromResult(send(request)))
        {
        }

        public FakeGoogleHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _sendAsync(request);
        }
    }
}
