using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Abstractions;
using TalentPilot.Infrastructure.Ai;

namespace TalentPilot.Tests.Ai;

public sealed class TavilySearchWebResearchProviderTests
{
    [Fact]
    public async Task ResearchAsync_DoesNotCallTavilyWhenApiKeyIsMissing()
    {
        var handler = new CapturingHandler();
        var provider = CreateProvider(handler, new TavilySearchOptions
        {
            ApiKey = "",
            DailyRequestLimit = 60
        });

        var result = await provider.ResearchAsync(CreateRequest(["Relia company"]), CancellationToken.None);

        Assert.Equal("Unavailable:TavilyMissingApiKey", result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ResearchAsync_StopsBeforeExceedingDailyLimit()
    {
        var handler = new CapturingHandler();
        var provider = CreateProvider(handler, new TavilySearchOptions
        {
            ApiKey = "test-key",
            DailyRequestLimit = 2,
            SearchDepth = "basic"
        });

        var result = await provider.ResearchAsync(
            CreateRequest(["Relia company", "CloudOps platform", "Enterprise Client"]),
            CancellationToken.None);

        Assert.Equal("Partial:QuotaExceeded", result.Status);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(2, result.Sources.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer", request.AuthorizationScheme);
            Assert.Equal("test-key", request.AuthorizationParameter);
            Assert.Contains("\"search_depth\":\"basic\"", request.Body);
        });
    }

    [Fact]
    public async Task ResearchAsync_ReturnsSpecificStatusWhenApiKeyIsRejected()
    {
        var handler = new CapturingHandler(
            HttpStatusCode.Unauthorized,
            new
            {
                error = "Unauthorized"
            });
        var provider = CreateProvider(handler, new TavilySearchOptions
        {
            ApiKey = "bad-key",
            DailyRequestLimit = 60
        });

        var result = await provider.ResearchAsync(
            CreateRequest(["Relia company", "CloudOps platform"]),
            CancellationToken.None);

        Assert.Equal("Unavailable:TavilyApiUnauthorized", result.Status);
        Assert.Single(handler.Requests);
        Assert.Empty(result.Sources);
    }

    private static TavilySearchWebResearchProvider CreateProvider(
        CapturingHandler handler,
        TavilySearchOptions options)
    {
        return new TavilySearchWebResearchProvider(
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
                    results = new[]
                    {
                        new
                        {
                            title = "Search result",
                            url = "https://example.com/result",
                            content = "Public company context."
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

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.Method,
                request.Headers.Authorization?.Scheme ?? string.Empty,
                request.Headers.Authorization?.Parameter ?? string.Empty,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken)));

            return new HttpResponseMessage(_statusCode)
            {
                Content = JsonContent.Create(_payload)
            };
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string AuthorizationScheme,
        string AuthorizationParameter,
        string Body);
}
