using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Infrastructure.Ai;

public sealed class GitHubCandidateSearchProvider : IGitHubCandidateSearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly GitHubCandidateSearchOptions _options;

    public GitHubCandidateSearchProvider(
        HttpClient httpClient,
        IOptions<GitHubCandidateSearchOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<GitHubCandidateSearchResult> SearchAsync(
        GitHubCandidateSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new GitHubCandidateSearchResult("Disabled", []);
        }

        var query = BuildQuery(request);
        if (string.IsNullOrWhiteSpace(query))
        {
            return new GitHubCandidateSearchResult("Skipped:NoQuery", []);
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.RequestTimeoutSeconds, 5, 60)));

            using var searchRequest = CreateRequest(
                HttpMethod.Get,
                $"/search/users?q={Uri.EscapeDataString(query)}&per_page={Math.Clamp(request.Limit, 1, 20)}");
            using var searchResponse = await _httpClient.SendAsync(searchRequest, timeout.Token);
            if (!searchResponse.IsSuccessStatusCode)
            {
                return new GitHubCandidateSearchResult(ToStatus(searchResponse), []);
            }

            var search = await searchResponse.Content.ReadFromJsonAsync<GitHubUserSearchResponse>(
                cancellationToken: timeout.Token);
            if (search?.Items is null || search.Items.Count == 0)
            {
                return new GitHubCandidateSearchResult("NoResults", []);
            }

            var profiles = new List<GitHubCandidateProfile>();
            foreach (var item in search.Items.Take(Math.Clamp(request.Limit, 1, 20)))
            {
                var profile = await LoadProfileAsync(item, timeout.Token);
                profiles.Add(profile);
            }

            return new GitHubCandidateSearchResult("Succeeded", profiles);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new GitHubCandidateSearchResult("Failed:Timeout", []);
        }
        catch (HttpRequestException)
        {
            return new GitHubCandidateSearchResult("Failed:HttpRequest", []);
        }
    }

    private async Task<GitHubCandidateProfile> LoadProfileAsync(
        GitHubUserSearchItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"/users/{Uri.EscapeDataString(item.Login)}");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new GitHubCandidateProfile(item.Login, null, item.HtmlUrl, null, null, null, 0);
            }

            var detail = await response.Content.ReadFromJsonAsync<GitHubUserDetail>(
                cancellationToken: cancellationToken);
            return new GitHubCandidateProfile(
                item.Login,
                detail?.Name,
                item.HtmlUrl,
                detail?.Location,
                detail?.Bio,
                detail?.Company,
                detail?.PublicRepos ?? 0,
                detail?.Email);
        }
        catch
        {
            return new GitHubCandidateProfile(item.Login, null, item.HtmlUrl, null, null, null, 0);
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(_options.ApiBaseUrl), relativePath));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("TalentPilot", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrWhiteSpace(_options.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token.Trim());
        }

        return request;
    }

    private static string BuildQuery(GitHubCandidateSearchRequest request)
    {
        var terms = request.Skills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Take(4)
            .ToList();

        if (terms.Count == 0 && !string.IsNullOrWhiteSpace(request.JobTitle))
        {
            terms.Add(request.JobTitle.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            terms.Add($"location:{request.Location.Trim()}");
        }

        return string.Join(' ', terms);
    }

    private static string ToStatus(HttpResponseMessage response)
    {
        return (int)response.StatusCode switch
        {
            401 => "Unavailable:GitHubUnauthorized",
            403 => "Unavailable:GitHubRateOrPermissionLimit",
            422 => "Unavailable:GitHubInvalidQuery",
            _ => "Failed:GitHubHttp"
        };
    }

    private sealed record GitHubUserSearchResponse(
        [property: JsonPropertyName("items")] IReadOnlyList<GitHubUserSearchItem> Items);

    private sealed record GitHubUserSearchItem(
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("html_url")] string HtmlUrl);

    private sealed record GitHubUserDetail(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("bio")] string? Bio,
        [property: JsonPropertyName("company")] string? Company,
        [property: JsonPropertyName("public_repos")] int PublicRepos,
        [property: JsonPropertyName("email")] string? Email);
}
