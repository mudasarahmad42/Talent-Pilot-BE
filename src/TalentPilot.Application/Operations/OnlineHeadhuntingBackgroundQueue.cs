using System.Runtime.CompilerServices;

namespace TalentPilot.Application.Operations;

public sealed record OnlineHeadhuntingBackgroundJob(
    Guid RequestId,
    Guid TenantId,
    Guid RequestedByUserId,
    string RequestedByEmail,
    Guid JobRequestId,
    OnlineHeadhuntingSearchInput Input,
    DateTimeOffset QueuedAtUtc);

public interface IOnlineHeadhuntingJobQueue
{
    bool TryEnqueue(OnlineHeadhuntingBackgroundJob job);

    IAsyncEnumerable<OnlineHeadhuntingBackgroundJob> DequeueAllAsync(CancellationToken cancellationToken);
}

public sealed class NoOpOnlineHeadhuntingJobQueue : IOnlineHeadhuntingJobQueue
{
    public bool TryEnqueue(OnlineHeadhuntingBackgroundJob job) => false;

    public async IAsyncEnumerable<OnlineHeadhuntingBackgroundJob> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}
