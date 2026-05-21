using TalentPilot.Application.DependencyInjection;
using TalentPilot.Infrastructure.DependencyInjection;
using TalentPilot.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
