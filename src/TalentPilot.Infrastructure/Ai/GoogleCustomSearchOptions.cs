namespace TalentPilot.Infrastructure.Ai;

public sealed class GoogleCustomSearchOptions
{
    public bool Enabled { get; init; } = true;

    public string ApiKey { get; init; } = string.Empty;

    public string SearchEngineId { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://www.googleapis.com/customsearch/v1";

    public int DailyRequestLimit { get; init; } = 60;

    public int RequestTimeoutSeconds { get; init; } = 15;
}
