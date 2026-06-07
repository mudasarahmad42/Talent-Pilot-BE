using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Abstractions;
using TalentPilot.Infrastructure.Ai;

namespace TalentPilot.Tests.Ai;

public sealed class GitHubCandidateSearchProviderTests
{
    [Fact]
    public async Task SearchAsync_RelaxesStrictLocationQueryWhenNoUsersAreFound()
    {
        var handler = new CapturingHandler();
        var provider = new GitHubCandidateSearchProvider(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.github.test")
            },
            Options.Create(new GitHubCandidateSearchOptions
            {
                Enabled = true,
                ApiBaseUrl = "https://api.github.test"
            }));

        var result = await provider.SearchAsync(
            new GitHubCandidateSearchRequest(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "online-headhunting",
                "Senior React Developer",
                ["React", "TypeScript", "CSS"],
                "Lahore",
                10),
            CancellationToken.None);

        Assert.Equal("Succeeded", result.Status);
        var profile = Assert.Single(result.Profiles);
        Assert.Equal("react-dev", profile.Login);
        Assert.Equal("React Developer", profile.DisplayName);

        var searchQueries = handler.Requests
            .Where(request => request.AbsolutePath == "/search/users")
            .Select(request => Uri.UnescapeDataString(request.Query))
            .ToArray();
        Assert.True(searchQueries.Length >= 2);
        Assert.Contains("location:Lahore", searchQueries[0]);
        Assert.Contains(searchQueries.Skip(1), query => !query.Contains("location:Lahore", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (request.RequestUri!.AbsolutePath == "/search/users")
            {
                var query = Uri.UnescapeDataString(request.RequestUri.Query);
                object payload = query.Contains("location:Lahore", StringComparison.OrdinalIgnoreCase)
                    ? new { items = Array.Empty<object>() }
                    : new
                    {
                        items = new[]
                        {
                            new
                            {
                                login = "react-dev",
                                html_url = "https://github.com/react-dev"
                            }
                        }
                    };

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(payload)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    name = "React Developer",
                    location = (string?)null,
                    bio = "React TypeScript frontend engineer",
                    company = "Independent",
                    public_repos = 24,
                    email = (string?)null
                })
            });
        }
    }
}
