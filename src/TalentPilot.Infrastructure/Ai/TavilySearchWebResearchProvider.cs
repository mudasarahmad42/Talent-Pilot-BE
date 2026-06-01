using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Infrastructure.Ai;

public sealed class TavilySearchWebResearchProvider : IWebResearchProvider
{
    private const string ProviderName = "TavilySearch";
    private readonly HttpClient _httpClient;
    private readonly TavilySearchOptions _options;
    private readonly IWebResearchQuotaStore _quotaStore;

    public TavilySearchWebResearchProvider(
        HttpClient httpClient,
        IOptions<TavilySearchOptions> options,
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
            return new WebResearchResult("Unavailable:TavilyMissingApiKey", []);
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

                using var response = await SendSearchRequestAsync(query, maxResults, timeout.Token);
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

                var payload = await response.Content.ReadFromJsonAsync<TavilySearchResponse>(
                    cancellationToken: timeout.Token);

                if (payload?.Results is null)
                {
                    continue;
                }

                sources.AddRange(payload.Results
                    .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Url))
                    .Take(maxResults)
                    .Select(item => new WebResearchSource(
                        query,
                        Trim(item.Title!, 180),
                        item.Url!,
                        Trim(item.Content ?? string.Empty, 700))));
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

    private async Task<HttpResponseMessage> SendSearchRequestAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
        request.Content = JsonContent.Create(new TavilySearchRequest(
            query,
            NormalizeSearchDepth(_options.SearchDepth),
            1,
            maxResults,
            false,
            false,
            false,
            false,
            "general"));

        return await _httpClient.SendAsync(request, cancellationToken);
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
            return "TavilyProviderQuotaExceeded";
        }

        if ((int)response.StatusCode is 432 or 433)
        {
            return "Unavailable:TavilyAccountLimit";
        }

        if ((int)response.StatusCode is not (401 or 403 or 400))
        {
            return string.Empty;
        }

        _ = await ReadTavilyErrorAsync(response, cancellationToken);

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Unavailable:TavilyApiUnauthorized",
            HttpStatusCode.Forbidden => "Unavailable:TavilyPermissionDenied",
            HttpStatusCode.BadRequest => "Unavailable:TavilyInvalidRequest",
            _ => string.Empty
        };
    }

    private static async Task<TavilyErrorResponse?> ReadTavilyErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<TavilyErrorResponse>(
                cancellationToken: cancellationToken);
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

    private static string NormalizeSearchDepth(string searchDepth)
    {
        return searchDepth.Trim().ToLowerInvariant() switch
        {
            "advanced" => "advanced",
            "fast" => "fast",
            "ultra-fast" => "ultra-fast",
            _ => "basic"
        };
    }

    private static string Trim(string value, int maxLength)
    {
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed record TavilySearchRequest(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("search_depth")] string SearchDepth,
        [property: JsonPropertyName("chunks_per_source")] int ChunksPerSource,
        [property: JsonPropertyName("max_results")] int MaxResults,
        [property: JsonPropertyName("include_answer")] bool IncludeAnswer,
        [property: JsonPropertyName("include_raw_content")] bool IncludeRawContent,
        [property: JsonPropertyName("include_images")] bool IncludeImages,
        [property: JsonPropertyName("include_favicon")] bool IncludeFavicon,
        [property: JsonPropertyName("topic")] string Topic);

    private sealed record TavilySearchResponse(TavilySearchResult[]? Results);

    private sealed record TavilySearchResult(string? Title, string? Url, string? Content);

    private sealed record TavilyErrorResponse(string? Error, string? Detail);
}
