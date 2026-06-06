using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Application.Calendar;
using TalentPilot.Application.Feedback;
using TalentPilot.Common.Results;
using TalentPilot.Infrastructure.DependencyInjection;

namespace TalentPilot.Tests.Integrations;

public sealed class ExternalEmailAndCalendarSmokeTests
{
    private static readonly Guid SmokeTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly string[] ApprovedRecipients =
    [
        "mudasar.ahmad@tkxel.com",
        "mudasarahmad150@gmail.com"
    ];

    [ExternalSmokeFact]
    public async Task ConfiguredEmailSender_SendsSmokeEmailToApprovedRecipients()
    {
        var configuration = BuildConfiguration();
        var recipients = ResolveApprovedRecipients(configuration);
        var provider = FirstConfigured(
            configuration["ExternalSmokeTests:EmailProvider"],
            Environment.GetEnvironmentVariable("TALENTPILOT_EXTERNAL_SMOKE_EMAIL_PROVIDER"),
            NotificationEmailProviders.MicrosoftGraph);
        var senderEmail = FirstConfigured(
            configuration["ExternalSmokeTests:SenderEmail"],
            Environment.GetEnvironmentVariable("TALENTPILOT_EXTERNAL_SMOKE_SENDER_EMAIL"),
            configuration["PublicFeedback:SenderEmail"],
            configuration["MicrosoftGraphEmail:FromEmail"],
            "mudasar.ahmad@8pkk57.onmicrosoft.com");

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<INotificationEmailProviderSettingsResolver>(
            new StaticNotificationEmailProviderSettingsResolver(provider));
        services.AddNotificationEmailSenderServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var sender = serviceProvider.GetRequiredService<INotificationEmailSender>();
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        foreach (var recipient in recipients)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var sendResult = await sender.SendAsync(
                new NotificationEmailMessage(
                    SmokeTenantId,
                    recipient,
                    $"Talent Pilot email smoke test {runId}",
                    $"Talent Pilot external email smoke test {runId}. This verifies provider acceptance for deployment readiness.",
                    $"""
                    <p>Talent Pilot external email smoke test <strong>{runId}</strong>.</p>
                    <p>This verifies provider acceptance for deployment readiness.</p>
                    """,
                    senderEmail),
                timeout.Token);

            Assert.True(
                sendResult.Succeeded,
                $"{recipient}: {sendResult.Error.Code} - {sendResult.Error.Message}");
            Assert.Equal(NotificationEmailProviders.Normalize(provider), sendResult.Value.Provider);
        }
    }

    [ExternalSmokeFact]
    public async Task PublicFeedback_SendsAdminAndThankYouEmailsToApprovedRecipients()
    {
        var configuration = BuildConfiguration();
        var provider = FirstConfigured(
            configuration["ExternalSmokeTests:EmailProvider"],
            Environment.GetEnvironmentVariable("TALENTPILOT_EXTERNAL_SMOKE_EMAIL_PROVIDER"),
            NotificationEmailProviders.MicrosoftGraph);
        var senderEmail = FirstConfigured(
            configuration["ExternalSmokeTests:SenderEmail"],
            Environment.GetEnvironmentVariable("TALENTPILOT_EXTERNAL_SMOKE_SENDER_EMAIL"),
            configuration["PublicFeedback:SenderEmail"],
            configuration["MicrosoftGraphEmail:FromEmail"],
            "mudasar.ahmad@8pkk57.onmicrosoft.com");

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<INotificationEmailProviderSettingsResolver>(
            new StaticNotificationEmailProviderSettingsResolver(provider));
        services.AddNotificationEmailSenderServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var observedSender = new ObservedNotificationEmailSender(
            serviceProvider.GetRequiredService<INotificationEmailSender>());
        var feedbackService = new PublicFeedbackService(
            new StaticPublicFeedbackTenantResolver(new PublicFeedbackTenant(SmokeTenantId, "TKXEL", "tkxel")),
            observedSender,
            new StaticCurrentUserAccessor(Guid.Empty, Guid.Empty, string.Empty));
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var result = await feedbackService.SubmitAsync(
            new SubmitPublicFeedbackInput(
                "Mudasar Ahmad",
                "mudasar.ahmad@tkxel.com",
                $"Feedback thank-you live smoke test {runId}.",
                "tkxel",
                null,
                "mudasarahmad150@gmail.com",
                senderEmail),
            timeout.Token);

        Assert.True(result.Succeeded, $"{result.Error.Code} - {result.Error.Message}");
        Assert.Equal(2, observedSender.Attempts.Count);
        Assert.Equal("mudasarahmad150@gmail.com", observedSender.Attempts[0].Message.ToEmail);
        Assert.Equal("mudasar.ahmad@tkxel.com", observedSender.Attempts[1].Message.ToEmail);
        Assert.All(observedSender.Attempts, attempt =>
            Assert.True(
                attempt.Result.Succeeded,
                $"{attempt.Message.ToEmail}: {attempt.Result.Error.Code} - {attempt.Result.Error.Message}"));
    }

    [ExternalSmokeFact]
    public async Task GoogleCalendar_CreatesSmokeMeetingForApprovedRecipients()
    {
        var configuration = BuildConfiguration();
        EnsureGoogleCalendarConfigured(configuration);
        var recipients = ResolveApprovedRecipients(configuration);
        var connectionContext = await ResolveGoogleCalendarConnectionAsync(configuration, CancellationToken.None);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ICurrentUserAccessor>(new StaticCurrentUserAccessor(
            connectionContext.TenantId,
            connectionContext.OrganizerUserId,
            connectionContext.OrganizerEmail));
        services.AddLogging();
        services.AddInfrastructureServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var calendar = scope.ServiceProvider.GetRequiredService<IGoogleCalendarService>();
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var startUtc = DateTimeOffset.UtcNow.AddDays(1);
        startUtc = new DateTimeOffset(
            startUtc.Year,
            startUtc.Month,
            startUtc.Day,
            startUtc.Hour,
            startUtc.Minute,
            0,
            TimeSpan.Zero);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var createResult = await calendar.CreateGoogleCalendarEventAsync(
            new CreateGoogleCalendarEventInput(
                $"Talent Pilot calendar smoke test {runId}",
                "Automated live smoke test for Google Calendar integration. Safe to ignore.",
                startUtc,
                startUtc.AddMinutes(15),
                FirstConfigured(
                    configuration["ExternalSmokeTests:GoogleCalendar:TimeZone"],
                    configuration["GoogleCalendar:DefaultTimeZoneId"],
                    "Asia/Karachi"),
                recipients,
                null,
                true),
            timeout.Token);

        Assert.True(
            createResult.Succeeded,
            $"{createResult.Error.Code} - {createResult.Error.Message}");
        Assert.False(string.IsNullOrWhiteSpace(createResult.Value.EventId));
        Assert.False(string.IsNullOrWhiteSpace(createResult.Value.HtmlLink));
        Assert.False(string.IsNullOrWhiteSpace(createResult.Value.HangoutLink));
        Assert.All(recipients, recipient =>
            Assert.Contains(createResult.Value.Attendees, email => string.Equals(email, recipient, StringComparison.OrdinalIgnoreCase)));
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var backendRoot = FindBackendRoot();
        return new ConfigurationBuilder()
            .SetBasePath(backendRoot)
            .AddJsonFile("src/TalentPilot.Api/appsettings.json", optional: false)
            .AddJsonFile("src/TalentPilot.Api/appsettings.Development.json", optional: true)
            .AddUserSecrets<ExternalEmailAndCalendarSmokeTests>(optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string FindBackendRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "TalentPilot.Api", "appsettings.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the backend repository root.");
    }

    private static IReadOnlyList<string> ResolveApprovedRecipients(IConfiguration configuration)
    {
        var configuredRecipients = FirstConfigured(
            configuration["ExternalSmokeTests:Recipients"],
            Environment.GetEnvironmentVariable("TALENTPILOT_EXTERNAL_SMOKE_RECIPIENTS"));

        var recipients = string.IsNullOrWhiteSpace(configuredRecipients)
            ? ApprovedRecipients
            : configuredRecipients
                .Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var unexpected = recipients
            .Where(recipient => !ApprovedRecipients.Contains(recipient, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (unexpected.Length > 0)
        {
            throw new InvalidOperationException(
                $"External smoke tests are limited to approved recipients: {string.Join(", ", ApprovedRecipients)}.");
        }

        return recipients
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void EnsureGoogleCalendarConfigured(IConfiguration configuration)
    {
        var missing = new List<string>();
        if (!bool.TryParse(configuration["GoogleCalendar:Enabled"], out var enabled) || !enabled)
        {
            missing.Add("GoogleCalendar:Enabled=true");
        }

        AddMissing(
            missing,
            "GoogleCalendar:ClientId or GOOGLE_CALENDAR_CLIENT_ID",
            configuration["GoogleCalendar:ClientId"],
            Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_CLIENT_ID"));
        AddMissing(
            missing,
            "GoogleCalendar:ClientSecret or GOOGLE_CALENDAR_CLIENT_SECRET",
            configuration["GoogleCalendar:ClientSecret"],
            Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_CLIENT_SECRET"));
        AddMissing(
            missing,
            "GoogleCalendar:RedirectUri or GOOGLE_CALENDAR_REDIRECT_URI",
            configuration["GoogleCalendar:RedirectUri"],
            Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_REDIRECT_URI"));
        AddMissing(
            missing,
            "GoogleCalendar:TokenProtectionKey, GOOGLE_CALENDAR_TOKEN_PROTECTION_KEY, or Jwt:SigningKey",
            configuration["GoogleCalendar:TokenProtectionKey"],
            Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_TOKEN_PROTECTION_KEY"),
            configuration["Jwt:SigningKey"]);

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Google Calendar smoke test prerequisites are missing: {string.Join("; ", missing)}.");
        }
    }

    private static async Task<GoogleCalendarConnectionContext> ResolveGoogleCalendarConnectionAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("TalentPilot");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:TalentPilot is required to discover the connected Google Calendar organizer account.");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1)
                TenantId,
                OrganizerUserId,
                OrganizerEmail
            FROM dbo.GoogleCalendarConnections
            WHERE Provider = N'GoogleCalendar'
              AND Status = N'Connected'
              AND RefreshTokenCiphertext IS NOT NULL
              AND (@TenantId IS NULL OR TenantId = @TenantId)
            ORDER BY UpdatedAtUtc DESC;
            """;

        var configuredTenantId = FirstConfigured(
            configuration["ExternalSmokeTests:GoogleCalendar:TenantId"],
            configuration["ExternalSmokeTests:TenantId"],
            Environment.GetEnvironmentVariable("TALENTPILOT_EXTERNAL_SMOKE_TENANT_ID"));
        var tenantParameter = command.CreateParameter();
        tenantParameter.ParameterName = "@TenantId";
        tenantParameter.DbType = DbType.Guid;
        tenantParameter.Value = Guid.TryParse(configuredTenantId, out var tenantId)
            ? tenantId
            : DBNull.Value;
        command.Parameters.Add(tenantParameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "No connected Google Calendar organizer account was found. Connect Google Calendar in the app before running this live smoke test.");
        }

        return new GoogleCalendarConnectionContext(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2));
    }

    private static void AddMissing(List<string> missing, string label, params string?[] values)
    {
        if (values.All(string.IsNullOrWhiteSpace))
        {
            missing.Add(label);
        }
    }

    private static string FirstConfigured(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private sealed record GoogleCalendarConnectionContext(
        Guid TenantId,
        Guid OrganizerUserId,
        string OrganizerEmail);

    private sealed class StaticNotificationEmailProviderSettingsResolver : INotificationEmailProviderSettingsResolver
    {
        private readonly string _provider;

        public StaticNotificationEmailProviderSettingsResolver(string provider)
        {
            _provider = provider;
        }

        public Task<NotificationEmailProviderSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NotificationEmailProviderSettings(_provider));
        }
    }

    private sealed class StaticPublicFeedbackTenantResolver : IPublicFeedbackTenantResolver
    {
        private readonly PublicFeedbackTenant _tenant;

        public StaticPublicFeedbackTenantResolver(PublicFeedbackTenant tenant)
        {
            _tenant = tenant;
        }

        public Task<PublicFeedbackTenant?> ResolveAsync(PublicFeedbackTenantQuery query, CancellationToken cancellationToken)
        {
            return Task.FromResult<PublicFeedbackTenant?>(_tenant);
        }
    }

    private sealed class ObservedNotificationEmailSender : INotificationEmailSender
    {
        private readonly INotificationEmailSender _inner;

        public ObservedNotificationEmailSender(INotificationEmailSender inner)
        {
            _inner = inner;
        }

        public List<NotificationEmailAttempt> Attempts { get; } = [];

        public async Task<Result<NotificationEmailSendResult>> SendAsync(
            NotificationEmailMessage message,
            CancellationToken cancellationToken)
        {
            var result = await _inner.SendAsync(message, cancellationToken);
            Attempts.Add(new NotificationEmailAttempt(message, result));
            return result;
        }
    }

    private sealed record NotificationEmailAttempt(
        NotificationEmailMessage Message,
        Result<NotificationEmailSendResult> Result);

    private sealed class StaticCurrentUserAccessor : ICurrentUserAccessor
    {
        public StaticCurrentUserAccessor(Guid tenantId, Guid userId, string email)
        {
            TenantId = tenantId;
            UserId = userId;
            Email = email;
        }

        public Guid UserId { get; }

        public Guid TenantId { get; }

        public string Email { get; }

        public IReadOnlyCollection<string> RoleCodes => Array.Empty<string>();
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ExternalSmokeFactAttribute : FactAttribute
{
    public ExternalSmokeFactAttribute()
    {
        if (!ExternalSmokeTestsEnabled())
        {
            Skip = "Set TALENTPILOT_RUN_EXTERNAL_SMOKE_TESTS=true to run live email and Google Calendar smoke tests.";
        }
    }

    private static bool ExternalSmokeTestsEnabled()
    {
        var value = Environment.GetEnvironmentVariable("TALENTPILOT_RUN_EXTERNAL_SMOKE_TESTS")
            ?? Environment.GetEnvironmentVariable("ExternalSmokeTests__Enabled");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
