using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Infrastructure.Ai;

public sealed class GoogleCustomSearchWebResearchProvider : IWebResearchProvider
{
    private const string ProviderName = "GoogleCustomSearch";
    private readonly HttpClient _httpClient;
    private readonly GoogleCustomSearchOptions _options;
    private readonly IWebResearchQuotaStore _quotaStore;

    public GoogleCustomSearchWebResearchProvider(
        HttpClient httpClient,
        IOptions<GoogleCustomSearchOptions> options,
        IWebResearchQuotaStore quotaStore)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _quotaStore = quotaStore;
    }

    public async Task<WebResearchResult> ResearchAsync(
        WebResearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new WebResearchResult("Disabled", []);
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new WebResearchResult("Unavailable:MissingApiKey", []);
        }

        if (string.IsNullOrWhiteSpace(_options.SearchEngineId))
        {
            return new WebResearchResult("Unavailable:MissingSearchEngineId", []);
        }

        var queries = request.Queries
            .Select(NormalizeQuery)
            .Where(query => query.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        if (queries.Length == 0)
        {
            return new WebResearchResult("Skipped", []);
        }

        var sources = new List<WebResearchSource>();
        var quotaExceeded = false;
        var requestFailed = false;
        var providerFailureStatus = string.Empty;
        var maxResults = Math.Clamp(request.MaxResultsPerQuery, 1, 5);

        foreach (var query in queries)
        {
            var reserved = await _quotaStore.TryReserveAsync(
                ProviderName,
                DateOnly.FromDateTime(DateTime.UtcNow),
                _options.DailyRequestLimit,
                cancellationToken);

            if (!reserved)
            {
                quotaExceeded = true;
                break;
            }

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.RequestTimeoutSeconds, 5, 60)));

                using var response = await _httpClient.GetAsync(
                    BuildRequestUri(query, maxResults),
                    timeout.Token);

                if (!response.IsSuccessStatusCode)
                {
                    providerFailureStatus = await ProviderFailureStatusAsync(response, timeout.Token);
                    if (!string.IsNullOrWhiteSpace(providerFailureStatus))
                    {
                        break;
                    }

                    requestFailed = true;
                    continue;
                }

                var payload = await response.Content.ReadFromJsonAsync<GoogleSearchResponse>(
                    cancellationToken: timeout.Token);

                if (payload?.Items is null)
                {
                    continue;
                }

                sources.AddRange(payload.Items
                    .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Link))
                    .Take(maxResults)
                    .Select(item => new WebResearchSource(
                        query,
                        Trim(item.Title!, 180),
                        item.Link!,
                        Trim(item.Snippet ?? string.Empty, 700))));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                requestFailed = true;
            }
            catch (HttpRequestException)
            {
                requestFailed = true;
            }
        }

        return new WebResearchResult(
            DetermineStatus(sources.Count, quotaExceeded, requestFailed, providerFailureStatus),
            sources);
    }

    private Uri BuildRequestUri(string query, int maxResults)
    {
        var separator = _options.BaseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var url =
            $"{_options.BaseUrl}{separator}key={Uri.EscapeDataString(_options.ApiKey)}" +
            $"&cx={Uri.EscapeDataString(_options.SearchEngineId)}" +
            $"&q={Uri.EscapeDataString(query)}" +
            $"&num={maxResults}";

        return new Uri(url, UriKind.Absolute);
    }

    private static string DetermineStatus(
        int sourceCount,
        bool quotaExceeded,
        bool requestFailed,
        string providerFailureStatus)
    {
        if (!string.IsNullOrWhiteSpace(providerFailureStatus))
        {
            return sourceCount == 0 ? providerFailureStatus : $"Partial:{providerFailureStatus}";
        }

        if (quotaExceeded && sourceCount == 0)
        {
            return "QuotaExceeded";
        }

        if (quotaExceeded)
        {
            return "Partial:QuotaExceeded";
        }

        if (requestFailed && sourceCount == 0)
        {
            return "Failed";
        }

        if (requestFailed)
        {
            return "Partial";
        }

        return sourceCount == 0 ? "NoResults" : "Succeeded";
    }

    private static async Task<string> ProviderFailureStatusAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if ((int)response.StatusCode == 429)
        {
            return "GoogleProviderQuotaExceeded";
        }

        if ((int)response.StatusCode is not (401 or 403 or 400))
        {
            return string.Empty;
        }

        var error = await ReadGoogleErrorAsync(response, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden &&
            error?.Message?.Contains("Custom Search JSON API", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Unavailable:CustomSearchJsonApiDisabled";
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Unavailable:GoogleApiUnauthorized",
            System.Net.HttpStatusCode.Forbidden => "Unavailable:GooglePermissionDenied",
            System.Net.HttpStatusCode.BadRequest => "Unavailable:GoogleInvalidRequest",
            _ => string.Empty
        };
    }

    private static async Task<GoogleError?> ReadGoogleErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<GoogleErrorResponse>(
                cancellationToken: cancellationToken);

            return payload?.Error;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeQuery(string query)
    {
        return string.Join(' ', (query ?? string.Empty).Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string Trim(string value, int maxLength)
    {
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed record GoogleSearchResponse(GoogleSearchItem[]? Items);

    private sealed record GoogleSearchItem(string? Title, string? Link, string? Snippet);

    private sealed record GoogleErrorResponse(GoogleError? Error);

    private sealed record GoogleError(int? Code, string? Message, string? Status);
}
