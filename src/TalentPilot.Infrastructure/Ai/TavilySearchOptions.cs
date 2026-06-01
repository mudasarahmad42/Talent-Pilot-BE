namespace TalentPilot.Infrastructure.Ai;

public sealed class TavilySearchOptions
{
    public bool Enabled { get; init; } = true;

    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://api.tavily.com/search";

    public int DailyRequestLimit { get; init; } = 60;

    public int RequestTimeoutSeconds { get; init; } = 20;

    public string SearchDepth { get; init; } = "basic";
}
