using TalentPilot.Application.Abstractions;

namespace TalentPilot.Infrastructure.Ai;

public sealed class InMemoryWebResearchQuotaStore : IWebResearchQuotaStore
{
    private readonly object _sync = new();
    private readonly Dictionary<(string Provider, DateOnly UsageDateUtc), int> _usage = [];

    public Task<bool> TryReserveAsync(
        string provider,
        DateOnly usageDateUtc,
        int dailyLimit,
        CancellationToken cancellationToken)
    {
        if (dailyLimit <= 0)
        {
            return Task.FromResult(false);
        }

        lock (_sync)
        {
            var key = (NormalizeProvider(provider), usageDateUtc);
            var count = _usage.GetValueOrDefault(key);
            if (count >= dailyLimit)
            {
                return Task.FromResult(false);
            }

            _usage[key] = count + 1;
            return Task.FromResult(true);
        }
    }

    private static string NormalizeProvider(string provider)
    {
        return string.IsNullOrWhiteSpace(provider) ? "Unknown" : provider.Trim();
    }
}
