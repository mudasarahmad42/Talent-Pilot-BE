namespace TalentPilot.Infrastructure.Ai;

public sealed class GitHubCandidateSearchOptions
{
    public bool Enabled { get; init; } = true;

    public string ApiBaseUrl { get; init; } = "https://api.github.com";

    public string Token { get; init; } = string.Empty;

    public int RequestTimeoutSeconds { get; init; } = 20;
}
