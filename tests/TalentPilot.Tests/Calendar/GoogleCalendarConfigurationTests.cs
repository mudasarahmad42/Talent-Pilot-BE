using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TalentPilot.Infrastructure.Calendar;
using TalentPilot.Infrastructure.DependencyInjection;

namespace TalentPilot.Tests.Calendar;

public sealed class GoogleCalendarConfigurationTests
{
    [Fact]
    public void AddInfrastructureServices_WhenGoogleCalendarEnabledIsMissing_DefaultsToEnabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-signing-key-at-least-32-characters",
                ["GoogleCalendar:ClientId"] = "client-id.apps.googleusercontent.com",
                ["GoogleCalendar:ClientSecret"] = "client-secret",
                ["GoogleCalendar:RedirectUri"] = "http://localhost:5058/api/google-calendar/oauth/callback"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GoogleCalendarOptions>>().Value;

        Assert.True(options.Enabled);
        Assert.Equal("client-id.apps.googleusercontent.com", options.ClientId);
    }

    [Fact]
    public void AddInfrastructureServices_WhenGoogleCalendarEnabledIsFalse_DisablesCalendar()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-signing-key-at-least-32-characters",
                ["GoogleCalendar:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GoogleCalendarOptions>>().Value;

        Assert.False(options.Enabled);
    }
}
