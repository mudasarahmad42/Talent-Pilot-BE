using TalentPilot.Application.Abstractions;

namespace TalentPilot.Infrastructure.Ai;

public sealed class NoOpWebResearchProvider : IWebResearchProvider
{
    public Task<WebResearchResult> ResearchAsync(WebResearchRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new WebResearchResult("Unavailable", []));
    }
}
