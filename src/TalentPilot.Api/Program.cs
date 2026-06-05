using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using TalentPilot.Api.Auth;
using TalentPilot.Api.Background;
using TalentPilot.Api.Hubs;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.DependencyInjection;
using TalentPilot.Application.Notifications;
using TalentPilot.Application.Operations;
using TalentPilot.Infrastructure.Auth;
using TalentPilot.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<ICurrentUserAccessor>(services => services.GetRequiredService<CurrentUserAccessor>());
builder.Services.AddScoped<ICurrentUserContextOverride>(services => services.GetRequiredService<CurrentUserAccessor>());
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<RealtimeConnectionTracker>();
builder.Services.AddSingleton<IRealtimeConnectionCounter>(services => services.GetRequiredService<RealtimeConnectionTracker>());
builder.Services.AddSingleton<IRealtimeNotificationPublisher, SignalRRealtimeNotificationPublisher>();
builder.Services.AddSingleton<OnlineHeadhuntingBackgroundQueue>();
builder.Services.AddSingleton<IOnlineHeadhuntingJobQueue>(services => services.GetRequiredService<OnlineHeadhuntingBackgroundQueue>());
builder.Services.AddHostedService<OnlineHeadhuntingBackgroundService>();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition")
            .AllowCredentials();
    });
});

var jwtOptions = new JwtOptions
{
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "TalentPilot",
    Audience = builder.Configuration["Jwt:Audience"] ?? "TalentPilot.Web",
    SigningKey = builder.Configuration["Jwt:SigningKey"] ?? "development-only-change-this-key-before-production-32"
};
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseCors("AngularDev");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    service = "Talent Pilot API",
    status = "Healthy",
    utc = DateTimeOffset.UtcNow
})).AllowAnonymous();

app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();
