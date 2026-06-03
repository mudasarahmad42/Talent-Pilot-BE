using TalentPilot.Application.Admin.Notifications;

namespace TalentPilot.Worker;

public sealed class Worker : BackgroundService
{
    private const string NotificationWorkerName = "notification-outbox-email";

    private readonly INotificationOutboxProcessor _outboxProcessor;
    private readonly INotificationWorkerStatusStore _workerStatusStore;
    private readonly ILogger<Worker> _logger;
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;

    public Worker(
        INotificationOutboxProcessor outboxProcessor,
        INotificationWorkerStatusStore workerStatusStore,
        ILogger<Worker> logger)
    {
        _outboxProcessor = outboxProcessor;
        _workerStatusStore = workerStatusStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecordHeartbeatAsync(processed: 0, lastError: null, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            string? lastError = null;

            try
            {
                processed = await _outboxProcessor.ProcessPendingAsync(batchSize: 25, stoppingToken);
                if (processed > 0)
                {
                    _logger.LogInformation("Processed {Count} pending notification outbox messages.", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                _logger.LogError(exception, "Notification outbox worker loop failed.");
            }

            await RecordHeartbeatAsync(processed, lastError, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task RecordHeartbeatAsync(int processed, string? lastError, CancellationToken cancellationToken)
    {
        try
        {
            await _workerStatusStore.RecordHeartbeatAsync(
                new NotificationWorkerHeartbeat(
                    NotificationWorkerName,
                    Environment.MachineName,
                    Environment.ProcessId,
                    _startedAtUtc,
                    processed,
                    lastError),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not record notification worker heartbeat.");
        }
    }
}
