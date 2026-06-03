using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Infrastructure.DependencyInjection;
using TalentPilot.Infrastructure.Notifications;
using TalentPilot.Infrastructure.Persistence;
using TalentPilot.Worker;

var builder = Host.CreateApplicationBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
}

builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddNotificationEmailSenderServices(builder.Configuration);
builder.Services.AddSingleton<INotificationEmailProviderSettingsResolver, DapperNotificationEmailProviderSettingsResolver>();
builder.Services.AddSingleton<INotificationOutboxProcessor, DapperNotificationOutboxProcessor>();
builder.Services.AddSingleton<INotificationWorkerStatusStore, DapperNotificationWorkerStatusStore>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
