using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Abstractions;
using TalentPilot.Infrastructure.Ai;

namespace TalentPilot.Tests.Ai;

public sealed class GoogleCustomSearchWebResearchProviderTests
{
    [Fact]
    public async Task ResearchAsync_DoesNotCallGoogleWhenSearchEngineIdIsMissing()
    {
        var handler = new CapturingHandler();
        var provider = CreateProvider(handler, new GoogleCustomSearchOptions
        {
            ApiKey = "test-key",
            SearchEngineId = "",
            DailyRequestLimit = 60
        });

        var result = await provider.ResearchAsync(CreateRequest(["Relia company"]), CancellationToken.None);

        Assert.Equal("Unavailable:MissingSearchEngineId", result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ResearchAsync_StopsBeforeExceedingDailyLimit()
    {
        var handler = new CapturingHandler();
        var provider = CreateProvider(handler, new GoogleCustomSearchOptions
        {
            ApiKey = "test-key",
            SearchEngineId = "test-cx",
            DailyRequestLimit = 2
        });

        var result = await provider.ResearchAsync(
            CreateRequest(["Relia company", "CloudOps platform", "Enterprise Client"]),
            CancellationToken.None);

        Assert.Equal("Partial:QuotaExceeded", result.Status);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(2, result.Sources.Count);
        Assert.All(handler.Requests, uri => Assert.Contains("cx=test-cx", uri.Query));
    }

    [Fact]
    public async Task ResearchAsync_ReturnsSpecificStatusWhenCustomSearchJsonApiIsDisabled()
    {
        var handler = new CapturingHandler(
            HttpStatusCode.Forbidden,
            new
            {
                error = new
                {
                    code = 403,
                    message = "This project does not have the access to Custom Search JSON API.",
                    status = "PERMISSION_DENIED"
                }
            });
        var provider = CreateProvider(handler, new GoogleCustomSearchOptions
        {
            ApiKey = "test-key",
            SearchEngineId = "test-cx",
            DailyRequestLimit = 60
        });

        var result = await provider.ResearchAsync(
            CreateRequest(["Relia company", "CloudOps platform"]),
            CancellationToken.None);

        Assert.Equal("Unavailable:CustomSearchJsonApiDisabled", result.Status);
        Assert.Single(handler.Requests);
        Assert.Empty(result.Sources);
    }

    private static GoogleCustomSearchWebResearchProvider CreateProvider(
        CapturingHandler handler,
        GoogleCustomSearchOptions options)
    {
        return new GoogleCustomSearchWebResearchProvider(
            new HttpClient(handler),
            Options.Create(options),
            new InMemoryWebResearchQuotaStore());
    }

    private static WebResearchRequest CreateRequest(IReadOnlyList<string> queries)
    {
        return new WebResearchRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "bench-matching",
            queries,
            1);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object _payload;

        public CapturingHandler()
            : this(
                HttpStatusCode.OK,
                new
                {
                    items = new[]
                    {
                        new
                        {
                            title = "Search result",
                            link = "https://example.com/result",
                            snippet = "Public company context."
                        }
                    }
                })
        {
        }

        public CapturingHandler(HttpStatusCode statusCode, object payload)
        {
            _statusCode = statusCode;
            _payload = payload;
        }

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = JsonContent.Create(_payload)
            };

            return Task.FromResult(response);
        }
    }
}
