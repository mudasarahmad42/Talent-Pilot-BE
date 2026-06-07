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

        var linkedInQueries = queries.Where(query => query.SourceCode == "LinkedIn").ToArray();
        var portfolioQueries = queries.Where(query => query.SourceCode == "Portfolio").ToArray();
        var publicSearchQueries = queries.Where(query => query.SourceCode == "PublicSearch").ToArray();
        Assert.Equal(2, linkedInQueries.Length);
        Assert.Equal(2, portfolioQueries.Length);
        Assert.Equal(2, publicSearchQueries.Length);

        Assert.All(linkedInQueries, query => Assert.StartsWith("site:linkedin.com/in ", query.Query));
        Assert.Contains(linkedInQueries, query => query.Query.Contains("\"Senior React Developer\"", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(linkedInQueries, query => query.Query.Contains("\"React Developer\"", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(linkedInQueries, query => query.Query.Contains("TypeScript", StringComparison.OrdinalIgnoreCase));
        Assert.All(linkedInQueries, query => Assert.Contains("Lahore", query.Query));
        Assert.All(linkedInQueries, query => Assert.Contains("Pakistan", query.Query));
        Assert.All(linkedInQueries, query => Assert.Contains("-jobs", query.Query));
        Assert.All(linkedInQueries, query => Assert.Contains("-hiring", query.Query));
        Assert.All(linkedInQueries, query => Assert.Contains("-company", query.Query));

        Assert.Contains(portfolioQueries, query => query.Query.Contains("portfolio OR \"personal website\" OR resume OR CV", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(publicSearchQueries, query => query.Query.Contains("developer OR engineer OR consultant OR architect", StringComparison.OrdinalIgnoreCase));
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
        Assert.DoesNotContain("Node.js", query.Query);
        Assert.Contains("location:Lahore", query.Query);
        Assert.DoesNotContain("site:", query.Query);
    }

    [Fact]
    public void BuildQueries_FocusesSeniorPythonDeveloperSearchOnPrimarySkillAndLocation()
    {
        var queries = new OnlineHeadhuntingBooleanQueryBuilder().BuildQueries(
            CreatePythonContext(),
            ["LinkedIn", "GitHub", "Portfolio"]);

        var gitHub = Assert.Single(queries, query => query.SourceCode == "GitHub");
        Assert.Contains("Python", gitHub.Query);
        Assert.Contains("location:Lahore", gitHub.Query);
        Assert.DoesNotContain("AWS", gitHub.Query);
        Assert.DoesNotContain("Design-Patterns", gitHub.Query);

        var linkedInQueries = queries.Where(query => query.SourceCode == "LinkedIn").Select(query => query.Query).ToArray();
        Assert.Equal(2, linkedInQueries.Length);
        Assert.Contains(linkedInQueries, query => query.Contains("\"Senior Python Developer\"", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(linkedInQueries, query => query.Contains("\"Python developer\"", StringComparison.OrdinalIgnoreCase));
        Assert.All(linkedInQueries, query => Assert.Contains("Lahore", query));
        Assert.All(linkedInQueries, query => Assert.Contains("Pakistan", query));
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
            ["Contact Candidate", "Known Candidate", "Unknown Candidate"],
            result.Leads.Select(lead => lead.DisplayName).ToArray());
        Assert.DoesNotContain(result.Leads, lead => lead.SourceUrl.Contains("expertini", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public async Task SearchAsync_KeepsRelevantGitHubProfilesWhenLocationIsMissing()
    {
        var agent = new OnlineHeadhuntingAgent(
            new JsonArrayAiModelProvider(),
            new FixedAiRuntimeSettingsResolver(),
            new NoOpAiAgentRunLogger(),
            new EmptyWebResearchProvider(),
            new StaticGitHubCandidateSearchProvider([
                new GitHubCandidateProfile(
                    "react-lahore",
                    "React Lahore",
                    "https://github.com/react-lahore",
                    null,
                    "Senior React TypeScript developer focused on frontend performance and CSS architecture.",
                    "Independent",
                    42,
                    null)
            ]),
            new OnlineHeadhuntingBooleanQueryBuilder());

        var result = await agent.SearchAsync(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CreateContext(),
            new OnlineHeadhuntingSearchInput(20, ["GitHub"], null),
            20,
            CancellationToken.None);

        var lead = Assert.Single(result.Leads);
        Assert.Equal("GitHub", lead.SourceCode);
        Assert.Equal("React Lahore", lead.DisplayName);
        Assert.Contains("React", lead.MatchedSkills);
        Assert.Contains("Location", lead.MissingData);
    }

    private static OperationsOnlineHeadhuntingContext CreateContext(
        IReadOnlyList<OperationsOnlineHeadhuntingExistingLead>? existingLeads = null)
    {
        var jobRequest = new OperationsJobRequest(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "JR-1042",
            "Senior React Developer",
            "TKXEL Engineering",
            "Product engineering services for cloud-native frontend teams.",
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

    private static OperationsOnlineHeadhuntingContext CreatePythonContext()
    {
        var jobRequest = new OperationsJobRequest(
            Guid.Parse("11111111-1111-1111-1111-111111111112"),
            "TP-REQ-021",
            "Senior Python Developer",
            "Tesla",
            "Engineering role for a Lahore team.",
            "Build scalable Python applications and backend services.",
            "Engineering",
            ["Design Patterns", "AWS", "SQL", "Python"],
            "3+ years",
            "Lahore",
            1,
            0,
            "Critical",
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
            3,
            null,
            [],
            []);
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

    private sealed class StaticGitHubCandidateSearchProvider : IGitHubCandidateSearchProvider
    {
        private readonly IReadOnlyList<GitHubCandidateProfile> _profiles;

        public StaticGitHubCandidateSearchProvider(IReadOnlyList<GitHubCandidateProfile> profiles)
        {
            _profiles = profiles;
        }

        public Task<GitHubCandidateSearchResult> SearchAsync(
            GitHubCandidateSearchRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new GitHubCandidateSearchResult("Succeeded", _profiles));
        }
    }

    private sealed class EmptyWebResearchProvider : IWebResearchProvider
    {
        public Task<WebResearchResult> ResearchAsync(WebResearchRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new WebResearchResult("NoResults", []));
        }
    }
}
