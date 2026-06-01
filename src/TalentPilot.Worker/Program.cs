using Microsoft.Extensions.Options;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Infrastructure.Notifications;
using TalentPilot.Infrastructure.Persistence;
using TalentPilot.Worker;

var builder = Host.CreateApplicationBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
}

builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<IOptions<ResendEmailOptions>>(Options.Create(new ResendEmailOptions
{
    ApiKey = builder.Configuration["Resend:ApiKey"]
        ?? Environment.GetEnvironmentVariable("RESEND_APITOKEN")
        ?? Environment.GetEnvironmentVariable("RESEND_API_KEY")
        ?? string.Empty,
    FromEmail = builder.Configuration["Resend:FromEmail"] ?? "onboarding@resend.dev"
}));
builder.Services.AddSingleton<INotificationEmailSender, ResendNotificationEmailSender>();
builder.Services.AddSingleton<INotificationOutboxProcessor, DapperNotificationOutboxProcessor>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
