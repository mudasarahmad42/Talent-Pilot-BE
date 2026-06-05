using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TalentPilot.Api.Auth;
using TalentPilot.Application.Operations;

namespace TalentPilot.Api.Background;

public sealed class OnlineHeadhuntingBackgroundQueue : IOnlineHeadhuntingJobQueue
{
    private readonly Channel<OnlineHeadhuntingBackgroundJob> _queue = Channel.CreateBounded<OnlineHeadhuntingBackgroundJob>(
        new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryEnqueue(OnlineHeadhuntingBackgroundJob job) => _queue.Writer.TryWrite(job);

    public async IAsyncEnumerable<OnlineHeadhuntingBackgroundJob> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _queue.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_queue.Reader.TryRead(out var job))
            {
                yield return job;
            }
        }
    }
}

public sealed class OnlineHeadhuntingBackgroundService : BackgroundService
{
    private readonly IOnlineHeadhuntingJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OnlineHeadhuntingBackgroundService> _logger;

    public OnlineHeadhuntingBackgroundService(
        IOnlineHeadhuntingJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<OnlineHeadhuntingBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var userContext = scope.ServiceProvider.GetRequiredService<ICurrentUserContextOverride>();
                userContext.Set(job.TenantId, job.RequestedByUserId, job.RequestedByEmail);

                var operations = scope.ServiceProvider.GetRequiredService<IOperationsService>();
                await operations.RunOnlineCandidatesSearchAsync(job, stoppingToken);

                userContext.Clear();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Online headhunting background job {RequestId} failed for job request {JobRequestId}.",
                    job.RequestId,
                    job.JobRequestId);
            }
        }
    }
}
