using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using TalentPilot.Api.Auth;
using TalentPilot.Api.Background;
using TalentPilot.Api.Hubs;
using TalentPilot.Api.Security;
using TalentPilot.Api.Startup;
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
builder.Services.AddScoped<AdminCenterReadOnlyFilter>();
var jwtOptions = CreateJwtOptions();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, jwtOptions);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});
builder.Services.AddSingleton<RealtimeConnectionTracker>();
builder.Services.AddSingleton<IRealtimeConnectionCounter>(services => services.GetRequiredService<RealtimeConnectionTracker>());
builder.Services.AddSingleton<IRealtimeNotificationPublisher, SignalRRealtimeNotificationPublisher>();
builder.Services.AddSingleton<OnlineHeadhuntingBackgroundQueue>();
builder.Services.AddSingleton<IOnlineHeadhuntingJobQueue>(services => services.GetRequiredService<OnlineHeadhuntingBackgroundQueue>());
builder.Services.AddHostedService<OnlineHeadhuntingBackgroundService>();
builder.Services.AddEndpointsApiExplorer();

var globalPermitLimit = ReadRateLimit("Global", "PermitLimit", 1200);
var globalWindowSeconds = ReadRateLimit("Global", "WindowSeconds", 60);
var globalQueueLimit = ReadRateLimit("Global", "QueueLimit", 100);
var authPermitLimit = ReadRateLimit("Auth", "PermitLimit", 60);
var authWindowSeconds = ReadRateLimit("Auth", "WindowSeconds", 60);
var authQueueLimit = ReadRateLimit("Auth", "QueueLimit", 20);
var aiPermitLimit = ReadRateLimit("AiWork", "PermitLimit", 180);
var aiWindowSeconds = ReadRateLimit("AiWork", "WindowSeconds", 300);
var aiQueueLimit = ReadRateLimit("AiWork", "QueueLimit", 25);
var publicPortalPermitLimit = ReadRateLimit("PublicPortal", "PermitLimit", 600);
var publicPortalWindowSeconds = ReadRateLimit("PublicPortal", "WindowSeconds", 60);
var publicPortalQueueLimit = ReadRateLimit("PublicPortal", "QueueLimit", 50);
var enableHttpsRedirection = ReadSecurityTransportFlag("EnableHttpsRedirection");
var enableHsts = ReadSecurityTransportFlag("EnableHsts");
var allowedCorsOrigins = ReadAllowedCorsOrigins();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString("0");
        }

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "rate_limited",
            message = "Too many requests. Please wait briefly and try again."
        }, cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        CreateFixedWindowPartition(
            context,
            "global",
            globalPermitLimit,
            globalWindowSeconds,
            globalQueueLimit));

    options.AddPolicy(ApiRateLimitPolicies.Auth, context =>
        CreateFixedWindowPartition(
            context,
            ApiRateLimitPolicies.Auth,
            authPermitLimit,
            authWindowSeconds,
            authQueueLimit));

    options.AddPolicy(ApiRateLimitPolicies.AiWork, context =>
        CreateFixedWindowPartition(
            context,
            ApiRateLimitPolicies.AiWork,
            aiPermitLimit,
            aiWindowSeconds,
            aiQueueLimit));

    options.AddPolicy(ApiRateLimitPolicies.PublicPortal, context =>
        CreateFixedWindowPartition(
            context,
            ApiRateLimitPolicies.PublicPortal,
            publicPortalPermitLimit,
            publicPortalWindowSeconds,
            publicPortalQueueLimit));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
    {
        policy
            .WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition")
            .AllowCredentials();
    });
});

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

await app.Services.EnsureAdminCenterAccessPolicySchemaAsync();
await app.Services.SeedSystemAdminUserAsync();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment() && enableHsts)
{
    app.UseHsts();
}

if (enableHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.TryAdd("X-Content-Type-Options", "nosniff");
    headers.TryAdd("X-Frame-Options", "DENY");
    headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
    headers.TryAdd("X-Download-Options", "noopen");
    headers.TryAdd("Referrer-Policy", "no-referrer");
    headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=()");

    await next();
});

app.UseRouting();
app.UseCors("AngularDev");
app.UseAuthentication();
app.UseRateLimiter();
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

int ReadRateLimit(string section, string setting, int defaultValue)
{
    var configuredValue = builder.Configuration.GetValue<int?>($"Security:RateLimiting:{section}:{setting}");
    return configuredValue is > 0 ? configuredValue.Value : defaultValue;
}

bool ReadSecurityTransportFlag(string setting)
{
    return builder.Configuration.GetValue<bool>($"Security:Transport:{setting}");
}

string[] ReadAllowedCorsOrigins()
{
    var configuredOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
        ?? Array.Empty<string>();

    var origins = configuredOrigins
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return origins.Length > 0
        ? origins
        : ["http://localhost:4200", "http://127.0.0.1:4200"];
}

JwtOptions CreateJwtOptions()
{
    var signingKey = builder.Configuration["Jwt:SigningKey"];
    if (string.IsNullOrWhiteSpace(signingKey))
    {
        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured for non-development environments.");
        }

        signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    if (Encoding.UTF8.GetByteCount(signingKey) < 32)
    {
        throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
    }

    return new JwtOptions
    {
        Issuer = builder.Configuration["Jwt:Issuer"] ?? "TalentPilot",
        Audience = builder.Configuration["Jwt:Audience"] ?? "TalentPilot.Web",
        SigningKey = signingKey
    };
}

static RateLimitPartition<string> CreateFixedWindowPartition(
    HttpContext context,
    string policyName,
    int permitLimit,
    int windowSeconds,
    int queueLimit)
{
    return RateLimitPartition.GetFixedWindowLimiter(
        GetRateLimitPartitionKey(context, policyName),
        _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = permitLimit,
            QueueLimit = queueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            Window = TimeSpan.FromSeconds(windowSeconds)
        });
}

static string GetRateLimitPartitionKey(HttpContext context, string policyName)
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.User.FindFirstValue("sub");

    if (!string.IsNullOrWhiteSpace(userId))
    {
        var tenantId = context.User.FindFirstValue("tenant_id") ?? "tenantless";
        return $"{policyName}:user:{tenantId}:{userId}";
    }

    var remoteAddress = context.Connection.RemoteIpAddress?.ToString();
    return $"{policyName}:ip:{remoteAddress ?? "unknown"}";
}
