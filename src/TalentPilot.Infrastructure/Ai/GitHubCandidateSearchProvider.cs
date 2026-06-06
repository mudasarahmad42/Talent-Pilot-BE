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
        var terms = SelectSearchSkills(request.JobTitle, request.Skills)
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(NormalizeSearchTerm)
            .ToList();

        if (terms.Count == 0 && !string.IsNullOrWhiteSpace(request.JobTitle))
        {
            terms.AddRange(request.JobTitle
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => token.Length > 2)
                .Where(token => !token.Equals("senior", StringComparison.OrdinalIgnoreCase))
                .Where(token => !token.Equals("developer", StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .Select(NormalizeSearchTerm));
        }

        var location = BuildLocationQualifier(request.Location);
        if (!string.IsNullOrWhiteSpace(location))
        {
            terms.Add(location);
        }

        return string.Join(' ', terms);
    }

    private static IReadOnlyList<string> PrioritizeSkills(string jobTitle, IReadOnlyList<string> skills)
    {
        return skills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((skill, index) => new { Skill = skill, Index = index })
            .OrderByDescending(item => jobTitle.Contains(item.Skill, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => SearchSkillPriority(item.Skill))
            .ThenBy(item => item.Index)
            .Select(item => item.Skill)
            .ToArray();
    }

    private static IReadOnlyList<string> SelectSearchSkills(string jobTitle, IReadOnlyList<string> skills)
    {
        var prioritized = PrioritizeSkills(jobTitle, skills);
        var coreSkills = prioritized
            .Where(skill => SearchSkillPriority(skill) <= 1)
            .Take(2)
            .ToArray();

        return coreSkills.Length > 0
            ? coreSkills
            : prioritized.Take(2).ToArray();
    }

    private static int SearchSkillPriority(string skill)
    {
        var normalized = skill.Trim().ToLowerInvariant();
        if (normalized is "python" or "java" or "c#" or ".net" or ".net core" or "node.js" or "nodejs" or "react" or "angular" or "vue" or "typescript")
        {
            return 0;
        }

        if (normalized is "django" or "flask" or "fastapi" or "spring boot" or "asp.net" or "asp.net core" or "next.js" or "nextjs")
        {
            return 1;
        }

        if (normalized is "aws" or "azure" or "gcp" or "kubernetes" or "terraform" or "sql" or "postgresql" or "sql server")
        {
            return 2;
        }

        return 3;
    }

    private static string NormalizeSearchTerm(string value) =>
        value.Trim().Replace(" ", "-", StringComparison.OrdinalIgnoreCase);

    private static string? BuildLocationQualifier(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var term = location
            .Split([',', '/', '\\', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.Length > 2 && !value.Equals("remote", StringComparison.OrdinalIgnoreCase) && !value.Equals("hybrid", StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(term) ? null : $"location:{NormalizeSearchTerm(term)}";
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
