using TalentPilot.Application.Admin.Notifications;

namespace TalentPilot.Worker;

public sealed class Worker : BackgroundService
{
    private readonly INotificationOutboxProcessor _outboxProcessor;
    private readonly ILogger<Worker> _logger;

    public Worker(INotificationOutboxProcessor outboxProcessor, ILogger<Worker> logger)
    {
        _outboxProcessor = outboxProcessor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await _outboxProcessor.ProcessPendingAsync(batchSize: 25, stoppingToken);
            if (processed > 0)
            {
                _logger.LogInformation("Processed {Count} pending notification outbox messages.", processed);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
