using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Operations;

namespace TalentPilot.Application.Ai;

public interface IOnlineHeadhuntingAgent
{
    Task<OnlineHeadhuntingAgentResult> SearchAsync(
        Guid tenantId,
        OperationsOnlineHeadhuntingContext context,
        OnlineHeadhuntingSearchInput input,
        int allowedLimit,
        CancellationToken cancellationToken);
}

public sealed record OnlineHeadhuntingAgentResult(
    IReadOnlyList<OnlineHeadhuntingAgentLead> Leads,
    Guid AgentRunId,
    string Model,
    DateTimeOffset GeneratedAtUtc,
    string SearchStatus,
    IReadOnlyList<string> SourceCodes,
    IReadOnlyList<string> Queries);

public sealed record OnlineHeadhuntingAgentLead(
    int Rank,
    string SourceCode,
    string SourceDisplayName,
    string SourceUrl,
    string? DisplayName,
    string? CurrentTitle,
    string? CurrentCompany,
    string? LocationText,
    string? Email,
    string? Phone,
    string? ProfileUrl,
    string EvidenceSnippet,
    decimal MatchScore,
    string Confidence,
    string FitSummary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<string> MissingData,
    string DuplicateStatus,
    Guid? DuplicateCandidateId,
    string? DuplicateCandidateName,
    string? DuplicateExplanation,
    string OutreachDraft);

public sealed class OnlineHeadhuntingAgent : IOnlineHeadhuntingAgent
{
    public const string AgentId = "online-headhunting";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly TimeSpan LeadEnrichmentTimeout = TimeSpan.FromSeconds(25);

    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex = new(
        @"(?<![\w@])(?:\+?\d{1,3}[\s.\-]?)?(?:\(?\d{2,4}\)?[\s.\-]?){2,4}\d{2,4}(?![\w@])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IAiModelProvider _modelProvider;
    private readonly IAiRuntimeSettingsResolver _settingsResolver;
    private readonly IAiAgentRunLogger _runLogger;
    private readonly IWebResearchProvider _webResearchProvider;
    private readonly IGitHubCandidateSearchProvider _gitHubSearchProvider;
    private readonly OnlineHeadhuntingBooleanQueryBuilder _queryBuilder;

    public OnlineHeadhuntingAgent(
        IAiModelProvider modelProvider,
        IAiRuntimeSettingsResolver settingsResolver,
        IAiAgentRunLogger runLogger,
        IWebResearchProvider webResearchProvider,
        IGitHubCandidateSearchProvider gitHubSearchProvider,
        OnlineHeadhuntingBooleanQueryBuilder queryBuilder)
    {
        _modelProvider = modelProvider;
        _settingsResolver = settingsResolver;
        _runLogger = runLogger;
        _webResearchProvider = webResearchProvider;
        _gitHubSearchProvider = gitHubSearchProvider;
        _queryBuilder = queryBuilder;
    }

    public async Task<OnlineHeadhuntingAgentResult> SearchAsync(
        Guid tenantId,
        OperationsOnlineHeadhuntingContext context,
        OnlineHeadhuntingSearchInput input,
        int allowedLimit,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.GetCurrentAsync(cancellationToken);
        var sourceCodes = OnlineHeadhuntingSources.Normalize(input.SourceCodes);
        var queries = _queryBuilder.BuildQueries(context, sourceCodes);
        var generatedAt = DateTimeOffset.UtcNow;
        var runId = await _runLogger.StartAsync(
            new AiAgentRunStart(
                tenantId,
                AgentId,
                "JobRequest",
                context.JobRequest.Id,
                settings.LlmModel,
                settings.EmbeddingModel,
                AiTextHasher.HashText(BuildRunInputText(context, sourceCodes, queries, allowedLimit)),
                new Dictionary<string, string>
                {
                    ["purpose"] = "online-candidate-headhunting",
                    ["humanDecisionRequired"] = "true",
                    ["leadLimit"] = allowedLimit.ToString(),
                    ["sourceCodes"] = string.Join(",", sourceCodes)
                }),
            cancellationToken);

        try
        {
            var rawLeads = new List<RawLead>();
            var sourceStatuses = new List<string>();

            var webQueries = queries
                .Where(query => !string.Equals(query.SourceCode, OnlineHeadhuntingSources.GitHub, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (webQueries.Length > 0)
            {
                var webResearch = await _webResearchProvider.ResearchAsync(
                    new WebResearchRequest(tenantId, AgentId, webQueries.Select(query => query.Query).ToArray(), 5),
                    cancellationToken);
                sourceStatuses.Add($"Web:{webResearch.Status}");
                rawLeads.AddRange(webResearch.Sources.Select(source => ToRawLead(source, webQueries)));
            }

            if (sourceCodes.Contains(OnlineHeadhuntingSources.GitHub, StringComparer.OrdinalIgnoreCase))
            {
                var gitHub = await _gitHubSearchProvider.SearchAsync(
                    new GitHubCandidateSearchRequest(
                        tenantId,
                        AgentId,
                        context.JobPost?.Title ?? context.JobRequest.Title,
                        context.RequiredSkills,
                        context.JobPost?.Location ?? context.JobRequest.Location,
                        Math.Min(allowedLimit, 20)),
                    cancellationToken);
                sourceStatuses.Add($"GitHub:{gitHub.Status}");
                rawLeads.AddRange(gitHub.Profiles.Select(ToRawLead));
            }

            var ranked = rawLeads
                .Where(lead => !string.IsNullOrWhiteSpace(lead.SourceUrl))
                .Where(IsLikelyCandidateLead)
                .Where(lead => HasRequiredLocationFit(context, lead))
                .Where(lead => !MatchesExistingLead(context.ExistingLeads, lead))
                .GroupBy(lead => NormalizeUrl(lead.SourceUrl), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(lead => ScoreLead(context, lead))
                .OrderBy(lead => HasKnownLocation(lead.LocationText) ? 0 : 1)
                .ThenByDescending(LeadContactPriority)
                .ThenByDescending(lead => lead.MatchScore)
                .ThenBy(lead => lead.DisplayName ?? lead.SourceUrl, StringComparer.OrdinalIgnoreCase)
                .Take(allowedLimit)
                .ToArray();

            var enriched = await EnrichLeadsAsync(context, ranked, settings.LlmModel, cancellationToken);
            var finalLeads = enriched
                .Select((lead, index) => lead with { Rank = index + 1 })
                .ToArray();

            var searchStatus = BuildSearchStatus(sourceStatuses, finalLeads.Length);
            await _runLogger.SucceedAsync(
                tenantId,
                runId,
                $"Discovered {finalLeads.Length} online lead(s) for {context.JobRequest.Code}.",
                new Dictionary<string, string>
                {
                    ["model"] = settings.LlmModel,
                    ["searchStatus"] = searchStatus,
                    ["generatedAtUtc"] = generatedAt.ToString("O"),
                    ["leadCount"] = finalLeads.Length.ToString()
                },
                cancellationToken);

            return new OnlineHeadhuntingAgentResult(
                finalLeads,
                runId,
                settings.LlmModel,
                generatedAt,
                searchStatus,
                sourceCodes,
                queries.Select(query => query.Query).ToArray());
        }
        catch (Exception ex)
        {
            await TryMarkFailedAsync(tenantId, runId, ex, cancellationToken);
            throw;
        }
    }

    private static string BuildRunInputText(
        OperationsOnlineHeadhuntingContext context,
        IReadOnlyList<string> sourceCodes,
        IReadOnlyList<OnlineHeadhuntingQuery> queries,
        int allowedLimit)
    {
        return JsonSerializer.Serialize(new
        {
            jobRequestId = context.JobRequest.Id,
            context.JobRequest.Code,
            context.JobRequest.Title,
            context.JobRequest.Description,
            context.JobRequest.Location,
            context.RequiredSkills,
            context.ExperienceMinYears,
            context.ExperienceMaxYears,
            existingLeadCount = context.ExistingLeads.Count,
            sourceCodes,
            queries = queries.Select(query => query.Query),
            allowedLimit
        }, JsonOptions);
    }

    private static RawLead ToRawLead(WebResearchSource source, IReadOnlyList<OnlineHeadhuntingQuery> queries)
    {
        var query = queries.FirstOrDefault(item => string.Equals(item.Query, source.Query, StringComparison.OrdinalIgnoreCase));
        var sourceCode = query?.SourceCode ?? OnlineHeadhuntingSources.PublicSearch;
        var evidence = BuildWebEvidenceSnippet(sourceCode, source.Title, source.Snippet);
        var contactText = $"{source.Title} {source.Snippet}";
        return new RawLead(
            sourceCode,
            OnlineHeadhuntingSources.DisplayName(sourceCode),
            source.Url,
            source.Query,
            ExtractName(source.Title),
            ExtractTitle(source.Title),
            ExtractCompany(source.Title),
            ExtractLocation(contactText),
            ExtractEmail(contactText),
            ExtractPhone(contactText),
            source.Url,
            evidence,
            source.Title);
    }

    private static string BuildWebEvidenceSnippet(string sourceCode, string title, string snippet)
    {
        var compact = string.Join(
            " ",
            new[] { title, snippet }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.ReplaceLineEndings(" ").Trim()));

        return string.Equals(sourceCode, OnlineHeadhuntingSources.LinkedIn, StringComparison.OrdinalIgnoreCase)
            ? Trim(compact, 260)
            : Trim(compact, 700);
    }

    private static RawLead ToRawLead(GitHubCandidateProfile profile)
    {
        return new RawLead(
            OnlineHeadhuntingSources.GitHub,
            OnlineHeadhuntingSources.DisplayName(OnlineHeadhuntingSources.GitHub),
            profile.HtmlUrl,
            string.Empty,
            string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Login : profile.DisplayName,
            ExtractTitle(profile.Bio ?? string.Empty),
            profile.Company,
            profile.Location,
            FirstUseful(profile.Email, ExtractEmail(profile.Bio ?? string.Empty)),
            ExtractPhone(profile.Bio ?? string.Empty),
            profile.HtmlUrl,
            Trim($"GitHub user @{profile.Login}. {profile.Bio} Public repositories: {profile.PublicRepositoryCount}.", 900),
            profile.DisplayName ?? profile.Login);
    }

    private static bool IsLikelyCandidateLead(RawLead lead)
    {
        if (!Uri.TryCreate(lead.SourceUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath.ToLowerInvariant();

        if (IsLikelyJobPostingUrl(host, path) || IsLikelyJobPostingText(lead))
        {
            return false;
        }

        if (host.Contains("linkedin.", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(lead.SourceCode, OnlineHeadhuntingSources.LinkedIn, StringComparison.OrdinalIgnoreCase) &&
                path.StartsWith("/in/", StringComparison.OrdinalIgnoreCase);
        }

        if (host.Contains("facebook.", StringComparison.OrdinalIgnoreCase) ||
            host.Contains("instagram.", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("x.com", StringComparison.OrdinalIgnoreCase) ||
            host.Contains("twitter.", StringComparison.OrdinalIgnoreCase) ||
            host.Contains("youtube.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool HasRequiredLocationFit(OperationsOnlineHeadhuntingContext context, RawLead lead)
    {
        var targetLocation = context.JobPost?.Location ?? context.JobRequest.Location;
        if (IsFlexibleLocation(targetLocation))
        {
            return true;
        }

        var terms = BuildLocationTerms(targetLocation);
        if (terms.Count == 0)
        {
            return true;
        }

        var searchable = $"{lead.Location} {lead.Snippet} {lead.SourceTitle}";
        if (terms.Any(term => ContainsTerm(searchable, term)))
        {
            return true;
        }

        if (HasKnownLocation(lead.Location))
        {
            return false;
        }

        return SearchQueryHasLocationConstraint(lead.SourceQuery, terms);
    }

    private static bool SearchQueryHasLocationConstraint(string? query, IReadOnlyList<string> locationTerms)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        return locationTerms.Any(term => ContainsTerm(query, term));
    }

    private static bool IsLikelyJobPostingUrl(string host, string path)
    {
        var jobBoardHosts = new[]
        {
            "indeed.",
            "expertini.",
            "rozee.",
            "mustakbil.",
            "glassdoor.",
            "bayt.",
            "naukri.",
            "monster.",
            "ziprecruiter.",
            "simplyhired.",
            "workable.com",
            "greenhouse.io",
            "lever.co",
            "smartrecruiters.",
            "bamboohr."
        };
        if (jobBoardHosts.Any(board => host.Contains(board, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var jobPathSegments = new[]
        {
            "/jobs",
            "/job/",
            "/careers",
            "/career/",
            "/company",
            "/companies",
            "/vacancy",
            "/vacancies",
            "/opening",
            "/openings",
            "/apply"
        };

        return jobPathSegments.Any(segment => path.Contains(segment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLikelyJobPostingText(RawLead lead)
    {
        var title = lead.SourceTitle.ToLowerInvariant();
        var text = $"{lead.SourceTitle} {lead.Snippet}".ToLowerInvariant();
        if (title.Contains(" jobs in ", StringComparison.OrdinalIgnoreCase) ||
            title.EndsWith(" jobs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var signals = new[]
        {
            "apply now",
            "job description",
            "job summary",
            "job vacancy",
            "latest jobs",
            "posted on",
            "salary",
            "apply for this job",
            "career opportunity"
        };

        return signals.Count(signal => text.Contains(signal, StringComparison.OrdinalIgnoreCase)) >= 2;
    }

    private static OnlineHeadhuntingAgentLead ScoreLead(OperationsOnlineHeadhuntingContext context, RawLead lead)
    {
        var searchable = $"{lead.Title} {lead.Name} {lead.Company} {lead.Location} {lead.Snippet} {lead.SourceTitle}";
        var skillAssessment = TechnologySkillMatcher.Assess(context.RequiredSkills, [], searchable);
        var matchedSkills = skillAssessment.ExactMatches
            .Concat(skillAssessment.StrongAdjacentMatches)
            .Select(item => item.RequiredSkill)
            .Take(8)
            .ToArray();
        var score = 45m + (skillAssessment.OverallScore * 45m);
        if (ContainsAny(searchable, Tokenize(context.JobPost?.Title ?? context.JobRequest.Title)))
        {
            score += 10m;
        }

        var targetLocation = context.JobPost?.Location ?? context.JobRequest.Location;
        if (!string.IsNullOrWhiteSpace(targetLocation) &&
            ContainsTerm(searchable, targetLocation))
        {
            score += 6m;
        }

        if (string.Equals(lead.SourceCode, OnlineHeadhuntingSources.GitHub, StringComparison.OrdinalIgnoreCase))
        {
            score += 4m;
        }

        score = Clamp(score, 45m, 97m);
        var duplicate = FindDuplicate(context.CandidatePool, lead);
        var missingData = BuildMissingData(lead);
        return new OnlineHeadhuntingAgentLead(
            0,
            lead.SourceCode,
            lead.SourceDisplayName,
            lead.SourceUrl,
            lead.Name,
            lead.Title,
            lead.Company,
            lead.Location,
            lead.Email,
            lead.Phone,
            lead.ProfileUrl,
            lead.Snippet,
            score,
            score >= 85m ? "High" : score >= 70m ? "Medium" : "Low",
            BuildFallbackFitSummary(context, lead, skillAssessment),
            TechnologySkillMatcher.BuildStrengthNotes(skillAssessment).Take(3).ToArray(),
            matchedSkills,
            TechnologySkillMatcher.BuildGapNotes(skillAssessment).Take(6).ToArray(),
            missingData,
            duplicate.Status,
            duplicate.CandidateId,
            duplicate.CandidateName,
            duplicate.Explanation,
            BuildFallbackOutreach(context, lead));
    }

    private async Task<IReadOnlyList<OnlineHeadhuntingAgentLead>> EnrichLeadsAsync(
        OperationsOnlineHeadhuntingContext context,
        IReadOnlyList<OnlineHeadhuntingAgentLead> leads,
        string model,
        CancellationToken cancellationToken)
    {
        if (leads.Count == 0)
        {
            return leads;
        }

        try
        {
            var prompt = BuildEnrichmentPrompt(context, leads);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(LeadEnrichmentTimeout);
            var response = await _modelProvider.GenerateAsync(
                new AiPromptRequest(
                    AgentId,
                    prompt,
                    new Dictionary<string, string>
                    {
                        ["purpose"] = "online-lead-fit-summary",
                        ["model"] = model,
                        ["humanDecisionRequired"] = "true"
                    }),
                timeout.Token);

            var enrichments = ParseEnrichments(response)
                .ToDictionary(item => NormalizeUrl(item.SourceUrl), StringComparer.OrdinalIgnoreCase);

            return leads.Select(lead =>
            {
                if (!enrichments.TryGetValue(NormalizeUrl(lead.SourceUrl), out var enrichment))
                {
                    return lead;
                }

                return lead with
                {
                    FitSummary = FirstUseful(enrichment.FitSummary, lead.FitSummary),
                    Strengths = NormalizeList(enrichment.Strengths, lead.Strengths, 5),
                    Gaps = NormalizeList(enrichment.Gaps, lead.Gaps, 5),
                    DuplicateExplanation = FirstUseful(enrichment.DuplicateExplanation, lead.DuplicateExplanation),
                    OutreachDraft = FirstUseful(enrichment.OutreachDraft, lead.OutreachDraft)
                };
            }).ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return leads;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return leads;
        }
    }

    private static string BuildEnrichmentPrompt(
        OperationsOnlineHeadhuntingContext context,
        IReadOnlyList<OnlineHeadhuntingAgentLead> leads)
    {
        var payload = new
        {
            instruction = "Return JSON array only. Do not invent contact data. Do not recommend automatic outreach. Recruiter review is required.",
            job = new
            {
                title = context.JobPost?.Title ?? context.JobRequest.Title,
                client = context.JobRequest.Client,
                clientContext = context.JobRequest.ClientContext,
                description = context.JobPost?.Description ?? context.JobRequest.Description,
                location = context.JobPost?.Location ?? context.JobRequest.Location,
                skills = context.RequiredSkills,
                experienceMinYears = context.ExperienceMinYears,
                experienceMaxYears = context.ExperienceMaxYears
            },
            leads = leads.Select(lead => new
            {
                lead.SourceUrl,
                lead.DisplayName,
                lead.CurrentTitle,
                lead.CurrentCompany,
                lead.LocationText,
                lead.SourceCode,
                lead.EvidenceSnippet,
                lead.MatchedSkills,
                lead.Gaps,
                lead.DuplicateStatus,
                lead.DuplicateCandidateName
            }),
            expectedShape = new[]
            {
                new
                {
                    sourceUrl = "same source URL",
                    fitSummary = "one short recruiter-facing fit summary",
                    strengths = new[] { "strength one", "strength two" },
                    gaps = new[] { "gap or uncertainty" },
                    duplicateExplanation = "why duplicate status was assigned",
                    outreachDraft = "short editable message draft; do not claim the person is selected"
                }
            }
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static IReadOnlyList<LeadEnrichment> ParseEnrichments(string response)
    {
        var start = response.IndexOf("[", StringComparison.Ordinal);
        var end = response.LastIndexOf("]", StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            return [];
        }

        var json = response[start..(end + 1)];
        return JsonSerializer.Deserialize<List<LeadEnrichment>>(json, JsonOptions) ?? [];
    }

    private static DuplicateMatch FindDuplicate(
        IReadOnlyList<OperationsOnlineHeadhuntingDuplicateCandidate> candidatePool,
        RawLead lead)
    {
        foreach (var candidate in candidatePool)
        {
            if (!string.IsNullOrWhiteSpace(lead.Email) &&
                string.Equals(candidate.Email, lead.Email, StringComparison.OrdinalIgnoreCase))
            {
                return new DuplicateMatch("ExactMatch", candidate.CandidateId, candidate.DisplayName, "Email matches an existing Talent Pilot candidate.");
            }

            if (!string.IsNullOrWhiteSpace(lead.Phone) &&
                !string.IsNullOrWhiteSpace(candidate.Phone) &&
                NormalizeDigits(candidate.Phone) == NormalizeDigits(lead.Phone))
            {
                return new DuplicateMatch("ExactMatch", candidate.CandidateId, candidate.DisplayName, "Phone number matches an existing Talent Pilot candidate.");
            }

            var leadUrl = NormalizeUrl(lead.ProfileUrl ?? lead.SourceUrl);
            var candidateUrl = NormalizeUrl(candidate.LinkedInUrl);
            if (!string.IsNullOrWhiteSpace(leadUrl) &&
                !string.IsNullOrWhiteSpace(candidateUrl) &&
                string.Equals(leadUrl, candidateUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new DuplicateMatch("ExactMatch", candidate.CandidateId, candidate.DisplayName, "Profile URL matches an existing Talent Pilot candidate.");
            }
        }

        var leadName = NormalizeName(lead.Name);
        if (string.IsNullOrWhiteSpace(leadName))
        {
            return new DuplicateMatch("NoMatch", null, null, "No reliable candidate identity was available for duplicate matching.");
        }

        foreach (var candidate in candidatePool)
        {
            var candidateName = NormalizeName(candidate.DisplayName);
            if (!string.Equals(leadName, candidateName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var roleOverlap = HasOverlap(lead.Title, candidate.CurrentDesignation) ||
                HasOverlap(lead.Company, candidate.CurrentCompany) ||
                candidate.Skills.Any(skill => ContainsTerm($"{lead.Snippet} {lead.Title}", skill));
            if (roleOverlap)
            {
                return new DuplicateMatch("PossibleDuplicate", candidate.CandidateId, candidate.DisplayName, "Name plus role, company, or skill evidence resembles an existing Talent Pilot candidate.");
            }
        }

        return new DuplicateMatch("NoMatch", null, null, "No matching internal candidate was found from available public evidence.");
    }

    private static bool MatchesExistingLead(
        IReadOnlyList<OperationsOnlineHeadhuntingExistingLead> existingLeads,
        RawLead lead)
    {
        if (existingLeads.Count == 0)
        {
            return false;
        }

        var sourceUrl = NormalizeUrl(lead.SourceUrl);
        var profileUrl = NormalizeUrl(lead.ProfileUrl);
        var email = NormalizeEmail(lead.Email);
        var phone = string.IsNullOrWhiteSpace(lead.Phone) ? string.Empty : NormalizeDigits(lead.Phone);

        foreach (var existing in existingLeads)
        {
            var existingSourceUrl = NormalizeUrl(existing.SourceUrl);
            var existingProfileUrl = NormalizeUrl(existing.ProfileUrl);
            if (!string.IsNullOrWhiteSpace(sourceUrl) &&
                (string.Equals(sourceUrl, existingSourceUrl, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sourceUrl, existingProfileUrl, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(profileUrl) &&
                (string.Equals(profileUrl, existingProfileUrl, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(profileUrl, existingSourceUrl, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(email) &&
                string.Equals(email, NormalizeEmail(existing.Email), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(phone) &&
                !string.IsNullOrWhiteSpace(existing.Phone) &&
                string.Equals(phone, NormalizeDigits(existing.Phone), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> BuildMissingData(RawLead lead)
    {
        var values = new List<string>();
        if (string.IsNullOrWhiteSpace(lead.Email))
        {
            values.Add("Email");
        }

        if (string.IsNullOrWhiteSpace(lead.Phone))
        {
            values.Add("Phone");
        }

        if (!HasKnownLocation(lead.Location))
        {
            values.Add("Location");
        }

        if (string.IsNullOrWhiteSpace(lead.Title))
        {
            values.Add("Current title");
        }

        return values;
    }

    private static string BuildFallbackFitSummary(
        OperationsOnlineHeadhuntingContext context,
        RawLead lead,
        SkillMatchAssessment skillAssessment)
    {
        var title = lead.Title ?? "Public profile";
        var matched = TechnologySkillMatcher.BuildSkillSummary(skillAssessment);
        var warning = TechnologySkillMatcher.BuildDirectEvidenceWarning(skillAssessment);
        return string.Join(' ', new[]
        {
            warning,
            $"{title} may fit {context.JobPost?.Title ?? context.JobRequest.Title}; public evidence shows {matched}.",
            skillAssessment.HumanReviewNotes
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildFallbackOutreach(OperationsOnlineHeadhuntingContext context, RawLead lead)
    {
        var name = string.IsNullOrWhiteSpace(lead.Name) ? "there" : lead.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return $"Hi {name}, I came across your public profile while sourcing for {context.JobPost?.Title ?? context.JobRequest.Title}. Your background looked relevant to the role, and I would like to share the opportunity if you are open to reviewing it.";
    }

    private async Task TryMarkFailedAsync(
        Guid tenantId,
        Guid runId,
        Exception ex,
        CancellationToken cancellationToken)
    {
        try
        {
            await _runLogger.FailAsync(
                tenantId,
                runId,
                "Online Headhunting Agent failed before returning usable lead results.",
                new Dictionary<string, string>
                {
                    ["errorType"] = ex.GetType().Name
                },
                cancellationToken);
        }
        catch
        {
            // The original agent failure is more important than best-effort run logging.
        }
    }

    private static string BuildSearchStatus(IReadOnlyList<string> sourceStatuses, int leadCount)
    {
        if (leadCount == 0 && sourceStatuses.Count == 0)
        {
            return "NoSources";
        }

        if (leadCount == 0)
        {
            return "NoResults:" + string.Join(";", sourceStatuses);
        }

        return sourceStatuses.Any(status => status.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
                                            status.Contains("Unavailable", StringComparison.OrdinalIgnoreCase))
            ? "Partial:" + string.Join(";", sourceStatuses)
            : "Succeeded:" + string.Join(";", sourceStatuses);
    }

    private static string ExtractName(string title)
    {
        var cleaned = CleanTitle(title);
        var parts = cleaned.Split(['|', '-', '–', '—'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Trim(parts.FirstOrDefault() ?? cleaned, 120);
    }

    private static string? ExtractTitle(string title)
    {
        var cleaned = CleanTitle(title);
        var parts = cleaned.Split(['|', '-', '–', '—'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        return Trim(parts[1], 160);
    }

    private static string? ExtractCompany(string title)
    {
        var cleaned = CleanTitle(title);
        var atIndex = cleaned.IndexOf(" at ", StringComparison.OrdinalIgnoreCase);
        if (atIndex < 0)
        {
            return null;
        }

        return Trim(cleaned[(atIndex + 4)..], 160);
    }

    private static string? ExtractLocation(string snippet)
    {
        var knownLocations = new[]
        {
            "Lahore",
            "Karachi",
            "Islamabad",
            "Rawalpindi",
            "Pakistan",
            "Remote",
            "Romania",
            "Bucharest",
            "India",
            "United Arab Emirates",
            "UAE",
            "Saudi Arabia",
            "United Kingdom",
            "UK",
            "United States",
            "USA",
            "Canada",
            "Germany",
            "Netherlands",
            "Poland"
        };
        return knownLocations.FirstOrDefault(location => snippet.Contains(location, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFlexibleLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return true;
        }

        var normalized = location.Trim();
        return ContainsTerm(normalized, "Remote") ||
            ContainsTerm(normalized, "Hybrid") ||
            ContainsTerm(normalized, "Anywhere") ||
            ContainsTerm(normalized, "Global");
    }

    private static IReadOnlyList<string> BuildLocationTerms(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return [];
        }

        var terms = Tokenize(location)
            .Where(term => term.Length > 2)
            .Where(term => !string.Equals(term, "remote", StringComparison.OrdinalIgnoreCase))
            .Where(term => !string.Equals(term, "hybrid", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (terms.Any(term => string.Equals(term, "Lahore", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(term, "Karachi", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(term, "Islamabad", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(term, "Rawalpindi", StringComparison.OrdinalIgnoreCase)))
        {
            terms.Add("Pakistan");
        }

        return terms
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ExtractEmail(string text)
    {
        var match = EmailRegex.Match(text);
        return match.Success ? match.Value.Trim() : null;
    }

    private static string? ExtractPhone(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (Match match in PhoneRegex.Matches(text))
        {
            var candidate = match.Value.Trim().Trim('.', ',', ';', ':');
            var digits = NormalizeDigits(candidate);
            if (digits.Length is < 7 or > 15)
            {
                continue;
            }

            if (Regex.IsMatch(candidate, @"^\d{4}[-/.]\d{1,2}[-/.]\d{1,2}$"))
            {
                continue;
            }

            return Trim(candidate, 40);
        }

        return null;
    }

    private static string CleanTitle(string value)
    {
        return value
            .Replace(" | LinkedIn", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" - LinkedIn", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("GitHub", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static bool ContainsTerm(string haystack, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return false;
        }

        return haystack.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string haystack, IEnumerable<string> terms)
    {
        return terms.Any(term => term.Length > 2 && ContainsTerm(haystack, term));
    }

    private static IReadOnlyList<string> Tokenize(string text)
    {
        return text.Split([' ', ',', '/', '\\', '-', '|', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeUrl(string? url)
    {
        return string.IsNullOrWhiteSpace(url)
            ? string.Empty
            : url.Trim().TrimEnd('/').ToLowerInvariant();
    }

    private static string NormalizeDigits(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string NormalizeEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static bool HasKnownLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return false;
        }

        var normalized = location.Trim().ToLowerInvariant();
        return normalized is not "unknown" and not "location unknown" and not "n/a" and not "na" and not "not available";
    }

    private static int LeadContactPriority(OnlineHeadhuntingAgentLead lead)
    {
        var priority = 0;
        if (!string.IsNullOrWhiteSpace(lead.Phone))
        {
            priority += 1;
        }

        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            priority += 2;
        }

        return priority;
    }

    private static string NormalizeName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool HasOverlap(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var leftTokens = Tokenize(left).Select(token => token.ToLowerInvariant()).ToHashSet();
        return Tokenize(right).Any(token => leftTokens.Contains(token.ToLowerInvariant()));
    }

    private static IReadOnlyList<string> NormalizeList(
        IReadOnlyList<string>? values,
        IReadOnlyList<string> fallback,
        int take)
    {
        var normalized = values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Trim(value.Trim(), 240))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToArray();
        return normalized is { Length: > 0 } ? normalized : fallback;
    }

    private static string FirstUseful(string? candidate, string? fallback)
    {
        return string.IsNullOrWhiteSpace(candidate) ? fallback ?? string.Empty : Trim(candidate.Trim(), 900);
    }

    private static string Trim(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength].Trim();
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        return value < min ? min : value > max ? max : value;
    }

    private sealed record RawLead(
        string SourceCode,
        string SourceDisplayName,
        string SourceUrl,
        string SourceQuery,
        string? Name,
        string? Title,
        string? Company,
        string? Location,
        string? Email,
        string? Phone,
        string? ProfileUrl,
        string Snippet,
        string SourceTitle);

    private sealed record DuplicateMatch(
        string Status,
        Guid? CandidateId,
        string? CandidateName,
        string Explanation);

    private sealed record LeadEnrichment(
        [property: JsonPropertyName("sourceUrl")] string SourceUrl,
        [property: JsonPropertyName("fitSummary")] string? FitSummary,
        [property: JsonPropertyName("strengths")] IReadOnlyList<string>? Strengths,
        [property: JsonPropertyName("gaps")] IReadOnlyList<string>? Gaps,
        [property: JsonPropertyName("duplicateExplanation")] string? DuplicateExplanation,
        [property: JsonPropertyName("outreachDraft")] string? OutreachDraft);
}

public sealed class OnlineHeadhuntingBooleanQueryBuilder
{
    public IReadOnlyList<OnlineHeadhuntingQuery> BuildQueries(
        OperationsOnlineHeadhuntingContext context,
        IReadOnlyList<string> sourceCodes)
    {
        var queries = new List<OnlineHeadhuntingQuery>();
        foreach (var sourceCode in sourceCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(sourceCode, OnlineHeadhuntingSources.GitHub, StringComparison.OrdinalIgnoreCase))
            {
                queries.Add(new OnlineHeadhuntingQuery(sourceCode, BuildGitHubApiQuery(context)));
                continue;
            }

            queries.AddRange(BuildWebQueries(context, sourceCode)
                .Select(query => new OnlineHeadhuntingQuery(sourceCode, query)));
        }

        return queries
            .Where(query => !string.IsNullOrWhiteSpace(query.Query))
            .DistinctBy(query => $"{query.SourceCode}:{query.Query}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildWebQueries(OperationsOnlineHeadhuntingContext context, string sourceCode)
    {
        var fullTitle = QuoteAny([context.JobPost?.Title, context.JobRequest.Title, SimplifyTitle(context.JobRequest.Title)]);
        var simplifiedTitle = QuoteAny([SimplifyTitle(context.JobPost?.Title ?? context.JobRequest.Title), BuildPrimarySkillTitle(context)]);
        var prioritizedSkills = PrioritizeSearchSkills(context).ToArray();
        var skillGroup = QuoteAny(prioritizedSkills.Take(4));
        var primarySkillGroup = QuoteAny(prioritizedSkills.Take(2));
        var location = context.JobPost?.Location ?? context.JobRequest.Location;
        var locationClause = QuoteAny(BuildLocationQueryTerms(location));
        var strictBaseQuery = JoinQueryParts(fullTitle, skillGroup, locationClause);
        var discoveryBaseQuery = JoinQueryParts(simplifiedTitle, primarySkillGroup, locationClause);
        const string excludeJobPages = "-jobs -hiring -vacancy -apply -\"job description\" -\"job summary\" -site:expertini.com -site:indeed.com -site:rozee.pk -site:mustakbil.com";

        return sourceCode switch
        {
            OnlineHeadhuntingSources.LinkedIn => DistinctQueries(
                $"site:linkedin.com/in {strictBaseQuery} -jobs -hiring -company",
                $"site:linkedin.com/in {discoveryBaseQuery} (developer OR engineer OR consultant OR architect) -jobs -hiring -company"),
            OnlineHeadhuntingSources.Portfolio => DistinctQueries(
                $"{strictBaseQuery} (portfolio OR \"personal website\" OR resume OR CV) {excludeJobPages}",
                $"{discoveryBaseQuery} (portfolio OR \"personal website\" OR resume OR CV OR GitHub) {excludeJobPages}"),
            _ => DistinctQueries(
                $"{strictBaseQuery} (developer OR engineer OR consultant OR architect) {excludeJobPages}",
                $"{discoveryBaseQuery} (developer OR engineer OR consultant OR architect OR resume OR CV) {excludeJobPages}")
        };
    }

    private static string BuildGitHubApiQuery(OperationsOnlineHeadhuntingContext context)
    {
        var terms = new List<string>();
        terms.AddRange(SelectGitHubSearchSkills(context).Select(NormalizeGitHubSearchTerm));
        var location = context.JobPost?.Location ?? context.JobRequest.Location;
        var locationQualifier = BuildGitHubLocationQualifier(location);
        if (!string.IsNullOrWhiteSpace(locationQualifier))
        {
            terms.Add(locationQualifier);
        }

        return string.Join(' ', terms.Where(term => !string.IsNullOrWhiteSpace(term)));
    }

    private static IReadOnlyList<string> SelectGitHubSearchSkills(OperationsOnlineHeadhuntingContext context)
    {
        var prioritized = PrioritizeSearchSkills(context);
        var coreSkills = prioritized
            .Where(skill => SearchSkillPriority(skill) <= 1)
            .Take(2)
            .ToArray();

        return coreSkills.Length > 0
            ? coreSkills
            : prioritized.Take(2).ToArray();
    }

    private static string JoinQueryParts(params string[] parts) =>
        string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();

    private static IReadOnlyList<string> DistinctQueries(params string[] queries) =>
        queries
            .Select(query => string.Join(' ', query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Where(query => query.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> PrioritizeSearchSkills(OperationsOnlineHeadhuntingContext context)
    {
        var title = $"{context.JobPost?.Title} {context.JobRequest.Title}";
        var skills = context.RequiredSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return skills
            .Select((skill, index) => new { Skill = skill, Index = index })
            .OrderByDescending(item => ContainsTerm(title, item.Skill))
            .ThenBy(item => SearchSkillPriority(item.Skill))
            .ThenBy(item => item.Index)
            .Select(item => item.Skill)
            .ToArray();
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

    private static bool ContainsTerm(string haystack, string term) =>
        !string.IsNullOrWhiteSpace(term) &&
        haystack.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildLocationQueryTerms(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return [];
        }

        var terms = location
            .Split([',', '/', '\\', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length > 2)
            .Where(term => !term.Equals("remote", StringComparison.OrdinalIgnoreCase))
            .Where(term => !term.Equals("hybrid", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (terms.Any(term => term.Equals("Lahore", StringComparison.OrdinalIgnoreCase) ||
                              term.Equals("Karachi", StringComparison.OrdinalIgnoreCase) ||
                              term.Equals("Islamabad", StringComparison.OrdinalIgnoreCase) ||
                              term.Equals("Rawalpindi", StringComparison.OrdinalIgnoreCase)))
        {
            terms.Add("Pakistan");
        }

        return terms
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? BuildGitHubLocationQualifier(string? location)
    {
        var term = BuildLocationQueryTerms(location).FirstOrDefault();
        return string.IsNullOrWhiteSpace(term) ? null : $"location:{NormalizeGitHubSearchTerm(term)}";
    }

    private static string NormalizeGitHubSearchTerm(string value) =>
        value.Trim().Replace(" ", "-", StringComparison.OrdinalIgnoreCase);

    private static string BuildPrimarySkillTitle(OperationsOnlineHeadhuntingContext context)
    {
        var skill = PrioritizeSearchSkills(context).FirstOrDefault();
        return string.IsNullOrWhiteSpace(skill)
            ? SimplifyTitle(context.JobPost?.Title ?? context.JobRequest.Title)
            : $"{skill} developer";
    }

    private static string SimplifyTitle(string title)
    {
        return title
            .Replace("Senior", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Lead", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string QuoteAny(IEnumerable<string?> values)
    {
        var quoted = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Quote(value!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return quoted.Length == 0 ? string.Empty : "(" + string.Join(" OR ", quoted) + ")";
    }

    private static string Quote(string value)
    {
        return value.Contains(' ') ? $"\"{value.Trim()}\"" : value.Trim();
    }
}

public sealed record OnlineHeadhuntingQuery(string SourceCode, string Query);

public static class OnlineHeadhuntingSources
{
    public const string PublicSearch = "PublicSearch";
    public const string LinkedIn = "LinkedIn";
    public const string GitHub = "GitHub";
    public const string Portfolio = "Portfolio";

    private static readonly string[] DefaultSourceCodes = [LinkedIn, GitHub, Portfolio, PublicSearch];

    public static IReadOnlyList<string> Normalize(IReadOnlyList<string>? sourceCodes)
    {
        var allowed = new HashSet<string>([PublicSearch, LinkedIn, GitHub, Portfolio], StringComparer.OrdinalIgnoreCase);
        var normalized = sourceCodes?
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Select(source => source.Trim())
            .Where(allowed.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized is { Length: > 0 } ? normalized : DefaultSourceCodes;
    }

    public static string DisplayName(string sourceCode)
    {
        return sourceCode switch
        {
            GitHub => "GitHub",
            LinkedIn => "LinkedIn Search Result",
            Portfolio => "Portfolio",
            _ => "Public Search"
        };
    }
}
