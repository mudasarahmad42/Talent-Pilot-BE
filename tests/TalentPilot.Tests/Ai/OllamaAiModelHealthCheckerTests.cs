using System.Net;
using TalentPilot.Application.Abstractions;
using TalentPilot.Infrastructure.Ai;

namespace TalentPilot.Tests.Ai;

public sealed class OllamaAiModelHealthCheckerTests
{
    [Fact]
    public async Task CheckAsync_DoesNotTreatDifferentTagAsAvailableWhenConfiguredModelHasNoTag()
    {
        var checker = CreateChecker("llama3.2", """{"models":[{"name":"llama3.2:1b"}]}""");

        var result = await checker.CheckAsync(CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Contains("llama3.2", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_TreatsExactConfiguredTagAsAvailable()
    {
        var checker = CreateChecker("llama3.2:1b", """{"models":[{"name":"llama3.2:1b"}]}""");

        var result = await checker.CheckAsync(CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal("Available", result.Status);
    }

    private static OllamaAiModelHealthChecker CreateChecker(string model, string responseJson)
    {
        var httpClient = new HttpClient(new StaticJsonHandler(responseJson))
        {
            BaseAddress = new Uri("http://ollama:11434")
        };

        return new OllamaAiModelHealthChecker(httpClient, new StaticRuntimeSettingsResolver(model));
    }

    private sealed class StaticRuntimeSettingsResolver : IAiRuntimeSettingsResolver
    {
        private readonly string _model;

        public StaticRuntimeSettingsResolver(string model)
        {
            _model = model;
        }

        public Task<AiRuntimeSettingsSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiRuntimeSettingsSnapshot(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Mock/Ollama",
                _model,
                "nomic-embed-text",
                768,
                "SqlServerVector",
                "http://ollama:11434"));
        }
    }

    private sealed class StaticJsonHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public StaticJsonHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, System.Text.Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
