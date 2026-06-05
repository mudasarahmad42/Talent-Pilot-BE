using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Ai;
using TalentPilot.Application.Operations;

namespace TalentPilot.Tests.Ai;

public sealed class OnlineHeadhuntingAgentTests
{
    [Fact]
    public void Normalize_FiltersUnknownAndOutOfScopeSources()
    {
        var sources = OnlineHeadhuntingSources.Normalize([
            "LinkedIn",
            "UnknownPortal",
            "GitHub",
            "Indeed",
            "linkedin"
        ]);

        Assert.Equal(["LinkedIn", "GitHub"], sources);
    }

    [Fact]
    public void Normalize_DefaultsToMvpSourcesWhenInputIsEmpty()
    {
        var sources = OnlineHeadhuntingSources.Normalize([]);

        Assert.Equal(["LinkedIn", "GitHub", "Portfolio", "PublicSearch"], sources);
        Assert.DoesNotContain("Indeed", sources);
    }

    [Fact]
    public void BuildQueries_GeneratesBooleanXrayQueriesForSourceLinksOnly()
    {
        var queries = new OnlineHeadhuntingBooleanQueryBuilder().BuildQueries(
            CreateContext(),
            ["LinkedIn", "Portfolio", "PublicSearch"]);

        var linkedIn = Assert.Single(queries, query => query.SourceCode == "LinkedIn");
        var portfolio = Assert.Single(queries, query => query.SourceCode == "Portfolio");
        var publicSearch = Assert.Single(queries, query => query.SourceCode == "PublicSearch");

        Assert.StartsWith("site:linkedin.com/in ", linkedIn.Query);
        Assert.Contains("\"Senior React Developer\"", linkedIn.Query);
        Assert.Contains("TypeScript", linkedIn.Query);
        Assert.Contains("Lahore", linkedIn.Query);
        Assert.Contains("-jobs", linkedIn.Query);
        Assert.Contains("-hiring", linkedIn.Query);
        Assert.Contains("-company", linkedIn.Query);

        Assert.Contains("portfolio OR \"personal website\" OR resume OR CV", portfolio.Query);
        Assert.Contains("developer OR engineer OR consultant OR architect", publicSearch.Query);
    }

    [Fact]
    public void BuildQueries_GeneratesGitHubApiSearchTermsWithoutWebXrayOperators()
    {
        var query = Assert.Single(new OnlineHeadhuntingBooleanQueryBuilder().BuildQueries(
            CreateContext(),
            ["GitHub"]));

        Assert.Equal("GitHub", query.SourceCode);
        Assert.Contains("React", query.Query);
        Assert.Contains("TypeScript", query.Query);
        Assert.Contains("location:Lahore", query.Query);
        Assert.DoesNotContain("site:", query.Query);
    }

    [Fact]
    public async Task SearchAsync_PrioritizesKnownLocationsAndPublicContactEvidence()
    {
        var agent = new OnlineHeadhuntingAgent(
            new JsonArrayAiModelProvider(),
            new FixedAiRuntimeSettingsResolver(),
            new NoOpAiAgentRunLogger(),
            new StaticWebResearchProvider(),
            new EmptyGitHubCandidateSearchProvider(),
            new OnlineHeadhuntingBooleanQueryBuilder());

        var result = await agent.SearchAsync(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CreateContext(),
            new OnlineHeadhuntingSearchInput(20, ["PublicSearch"], null),
            20,
            CancellationToken.None);

        Assert.Equal(
            ["Contact Candidate", "Known Candidate"],
            result.Leads.Select(lead => lead.DisplayName).ToArray());
        Assert.DoesNotContain(result.Leads, lead => lead.SourceUrl.Contains("expertini", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Leads, lead => string.Equals(lead.DisplayName, "Unknown Candidate", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Leads, lead => string.Equals(lead.DisplayName, "Romania Candidate", StringComparison.OrdinalIgnoreCase));

        var contactLead = result.Leads[0];
        Assert.Equal("contact@example.com", contactLead.Email);
        Assert.Equal("+92 300 555 0100", contactLead.Phone);
        Assert.DoesNotContain("Email", contactLead.MissingData);
        Assert.DoesNotContain("Phone", contactLead.MissingData);
    }

    [Fact]
    public async Task SearchAsync_ExcludesExistingOnlineLeadsForSearchMoreRuns()
    {
        var agent = new OnlineHeadhuntingAgent(
            new JsonArrayAiModelProvider(),
            new FixedAiRuntimeSettingsResolver(),
            new NoOpAiAgentRunLogger(),
            new StaticWebResearchProvider(),
            new EmptyGitHubCandidateSearchProvider(),
            new OnlineHeadhuntingBooleanQueryBuilder());

        var result = await agent.SearchAsync(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CreateContext([
                new OperationsOnlineHeadhuntingExistingLead(
                    "https://portfolio.example.com/contact-candidate",
                    "https://portfolio.example.com/contact-candidate",
                    "contact@example.com",
                    "+92 300 555 0100",
                    "Contact Candidate",
                    "React Engineer",
                    null,
                    "Lahore")
            ]),
            new OnlineHeadhuntingSearchInput(20, ["PublicSearch"], Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),
            20,
            CancellationToken.None);

        Assert.DoesNotContain(result.Leads, lead => string.Equals(lead.DisplayName, "Contact Candidate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Leads, lead => string.Equals(lead.DisplayName, "Known Candidate", StringComparison.OrdinalIgnoreCase));
    }

    private static OperationsOnlineHeadhuntingContext CreateContext(
        IReadOnlyList<OperationsOnlineHeadhuntingExistingLead>? existingLeads = null)
    {
        var jobRequest = new OperationsJobRequest(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "JR-1042",
            "Senior React Developer",
            "TKXEL Engineering",
            "Build high-performance React applications.",
            "Engineering",
            ["React", "TypeScript", "Node.js", "Next.js"],
            "5-7 years",
            "Lahore",
            1,
            0,
            "High",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Recruiter Sourcing",
            null,
            null,
            "Published",
            DateTimeOffset.UtcNow);

        return new OperationsOnlineHeadhuntingContext(
            jobRequest,
            null,
            jobRequest.Skills,
            5,
            7,
            [],
            existingLeads ?? []);
    }

    private sealed class JsonArrayAiModelProvider : IAiModelProvider
    {
        public Task<string> GenerateAsync(AiPromptRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult("[]");
        }
    }

    private sealed class FixedAiRuntimeSettingsResolver : IAiRuntimeSettingsResolver
    {
        public Task<AiRuntimeSettingsSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiRuntimeSettingsSnapshot(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Ollama",
                "llama3.2",
                "nomic-embed-text",
                768,
                "SqlServer",
                "http://localhost:11434"));
        }
    }

    private sealed class NoOpAiAgentRunLogger : IAiAgentRunLogger
    {
        public Task<Guid> StartAsync(AiAgentRunStart run, CancellationToken cancellationToken)
        {
            return Task.FromResult(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        }

        public Task SucceedAsync(
            Guid tenantId,
            Guid runId,
            string outputSummary,
            IReadOnlyDictionary<string, string> metadata,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task FailAsync(
            Guid tenantId,
            Guid runId,
            string outputSummary,
            IReadOnlyDictionary<string, string> metadata,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StaticWebResearchProvider : IWebResearchProvider
    {
        public Task<WebResearchResult> ResearchAsync(WebResearchRequest request, CancellationToken cancellationToken)
        {
            var query = request.Queries[0];
            IReadOnlyList<WebResearchSource> sources =
            [
                new(
                    query,
                    "Java Microservices Engineer Spring Boot Kafka Lahore Abacus Jobs in Pakistan",
                    "https://pk.expertini.com/jobs/in/java-microservices-engineer-spring-boot-kafka-lahore-abacus/",
                    "Apply now for Java Microservices Engineer. Job description, salary, and posted on details are available."),
                new(
                    query,
                    "Romania Candidate - Senior React Developer",
                    "https://portfolio.example.com/romania-candidate",
                    "Romania React TypeScript Node.js Next.js"),
                new(
                    query,
                    "Unknown Candidate - Senior React Developer",
                    "https://portfolio.example.com/unknown-candidate",
                    "React TypeScript Node.js Next.js unknown@example.com"),
                new(
                    query,
                    "Known Candidate - Senior React Developer",
                    "https://portfolio.example.com/known-candidate",
                    "Lahore React TypeScript Node.js Next.js"),
                new(
                    query,
                    "Contact Candidate - React Engineer",
                    "https://portfolio.example.com/contact-candidate",
                    "Lahore React TypeScript contact@example.com +92 300 555 0100")
            ];

            return Task.FromResult(new WebResearchResult("Succeeded", sources));
        }
    }

    private sealed class EmptyGitHubCandidateSearchProvider : IGitHubCandidateSearchProvider
    {
        public Task<GitHubCandidateSearchResult> SearchAsync(
            GitHubCandidateSearchRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new GitHubCandidateSearchResult("NoResults", []));
        }
    }
}
