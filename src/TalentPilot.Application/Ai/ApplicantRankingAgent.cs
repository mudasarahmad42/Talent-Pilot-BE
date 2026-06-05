using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Operations;

namespace TalentPilot.Application.Ai;

public interface IApplicantRankingAgent
{
    Task<ApplicantRankingRankResult> RankAsync(
        Guid tenantId,
        OperationsApplicantRankingContext context,
        CancellationToken cancellationToken);
}

public sealed record ApplicantRankingRankResult(
    IReadOnlyList<ApplicantRankingRankedApplication> Matches,
    Guid AgentRunId,
    string Model,
    DateTimeOffset GeneratedAtUtc,
    string SemanticSimilarityStatus);

public sealed record ApplicantRankingRankedApplication(
    Guid JobApplicationId,
    int Rank,
    decimal Score,
    string Confidence,
    string Explanation,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<string> DocumentEvidence,
    IReadOnlyList<string> HistoricalOutcomeEvidence);

public sealed class ApplicantRankingAgent : IApplicantRankingAgent
{
    public const string AgentId = "applicant-ranking";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IAiModelProvider _modelProvider;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly IAiRuntimeSettingsResolver _settingsResolver;
    private readonly IAiAgentRunLogger _runLogger;

    public ApplicantRankingAgent(
        IAiModelProvider modelProvider,
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        IAiRuntimeSettingsResolver settingsResolver,
        IAiAgentRunLogger runLogger)
    {
        _modelProvider = modelProvider;
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _settingsResolver = settingsResolver;
        _runLogger = runLogger;
    }

    public async Task<ApplicantRankingRankResult> RankAsync(
        Guid tenantId,
        OperationsApplicantRankingContext context,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.GetCurrentAsync(cancellationToken);
        var generatedAt = DateTimeOffset.UtcNow;
        var documentTexts = CollectPersistedDocumentTexts(context.Applications);
        var inputHash = AiTextHasher.HashText(BuildRunInputText(context, documentTexts));
        var runId = await _runLogger.StartAsync(
            new AiAgentRunStart(
                tenantId,
                AgentId,
                "JobPost",
                context.JobPost.JobPostId,
                settings.LlmModel,
                settings.EmbeddingModel,
                inputHash,
                new Dictionary<string, string>
                {
                    ["purpose"] = "applicant-ranking",
                    ["humanDecisionRequired"] = "true",
                    ["applicationCount"] = context.Applications.Count.ToString()
                }),
            cancellationToken);

        try
        {
            var vectorResult = await TryBuildVectorScoresAsync(tenantId, settings, context, documentTexts, cancellationToken);
            var scored = context.Applications
                .Select(application => ScoreApplication(context, application, vectorResult.Scores, documentTexts))
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Application.AppliedAt)
                .ThenBy(item => item.Application.CandidateName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var explanations = await GenerateExplanationsAsync(context, scored, documentTexts, settings.LlmModel, cancellationToken);
            var ranked = scored
                .Select((item, index) => ToRankedApplication(item, index + 1, explanations))
                .ToArray();

            await _runLogger.SucceedAsync(
                tenantId,
                runId,
                $"Ranked {ranked.Length} current applicant(s) for {context.JobPost.Title}.",
                new Dictionary<string, string>
                {
                    ["model"] = settings.LlmModel,
                    ["embeddingModel"] = settings.EmbeddingModel,
                    ["semanticSimilarityStatus"] = vectorResult.Status,
                    ["generatedAtUtc"] = generatedAt.ToString("O")
                },
                cancellationToken);

            return new ApplicantRankingRankResult(ranked, runId, settings.LlmModel, generatedAt, vectorResult.Status);
        }
        catch (Exception ex)
        {
            await TryMarkFailedAsync(tenantId, runId, ex, cancellationToken);
            throw;
        }
    }

    private async Task<VectorScoreResult> TryBuildVectorScoresAsync(
        Guid tenantId,
        AiRuntimeSettingsSnapshot settings,
        OperationsApplicantRankingContext context,
        IReadOnlyDictionary<Guid, string> documentTexts,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var application in context.Applications)
            {
                var profileText = BuildApplicationProfileText(application, documentTexts);
                var sourceHash = AiTextHasher.HashText(profileText);
                var existingHash = await _vectorStore.GetActiveSourceTextHashAsync(
                    tenantId,
                    "JobApplication",
                    application.JobApplicationId,
                    "JobApplicationEvidenceProfile",
                    settings.EmbeddingModel,
                    cancellationToken);

                if (!string.Equals(existingHash, sourceHash, StringComparison.Ordinal))
                {
                    var applicationEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(profileText, cancellationToken);
                    if (applicationEmbedding.Length == settings.EmbeddingDimensions)
                    {
                        await _vectorStore.UpsertAsync(
                            new VectorRecord(
                                tenantId,
                                "JobApplication",
                                application.JobApplicationId,
                                "JobApplicationEvidenceProfile",
                                sourceHash,
                                settings.EmbeddingModel,
                                settings.EmbeddingDimensions,
                                applicationEmbedding),
                            cancellationToken);
                    }
                }
            }

            var requirementText = BuildRequirementProfileText(context);
            var queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(requirementText, cancellationToken);
            if (queryEmbedding.Length != settings.EmbeddingDimensions)
            {
                return new VectorScoreResult(new Dictionary<Guid, decimal>(), "Unavailable: requirement embedding dimensions mismatch");
            }

            var requirementHash = AiTextHasher.HashText(requirementText);
            var existingRequirementHash = await _vectorStore.GetActiveSourceTextHashAsync(
                tenantId,
                "JobPost",
                context.JobPost.JobPostId,
                "ApplicantRankingRequirementProfile",
                settings.EmbeddingModel,
                cancellationToken);
            if (!string.Equals(existingRequirementHash, requirementHash, StringComparison.Ordinal))
            {
                await _vectorStore.UpsertAsync(
                    new VectorRecord(
                        tenantId,
                        "JobPost",
                        context.JobPost.JobPostId,
                        "ApplicantRankingRequirementProfile",
                        requirementHash,
                        settings.EmbeddingModel,
                        settings.EmbeddingDimensions,
                        queryEmbedding),
                    cancellationToken);
            }

            var results = await _vectorStore.SearchAsync(
                new VectorSearchRequest(
                    tenantId,
                    "JobApplication",
                    queryEmbedding,
                    Math.Max(context.Applications.Count, 1)),
                cancellationToken);
            var applicationIds = context.Applications.Select(application => application.JobApplicationId).ToHashSet();
            var scores = results
                .Where(result => applicationIds.Contains(result.EntityId))
                .ToDictionary(result => result.EntityId, result => Clamp(result.Score, 0, 1));
            return new VectorScoreResult(scores, scores.Count == 0 ? "Unavailable: no application vectors matched" : "Available");
        }
        catch (Exception ex)
        {
            return new VectorScoreResult(new Dictionary<Guid, decimal>(), SemanticSimilarityDiagnostics.Unavailable(ex, settings));
        }
    }

    private static IReadOnlyDictionary<Guid, string> CollectPersistedDocumentTexts(
        IReadOnlyList<OperationsApplicantRankingApplication> applications)
    {
        var results = new Dictionary<Guid, string>();
        foreach (var application in applications)
        {
            var parts = application.DocumentEvidence
                .Where(document => document.HasExtractedText && !string.IsNullOrWhiteSpace(document.ExtractedText))
                .Take(3)
                .Select(document => $"{document.DocumentType} {document.FileName}: {TrimText(document.ExtractedText, 4000)}")
                .ToArray();

            if (parts.Length > 0)
            {
                results[application.JobApplicationId] = string.Join('\n', parts);
            }
        }

        return results;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GenerateExplanationsAsync(
        OperationsApplicantRankingContext context,
        IReadOnlyList<ScoredApplication> scored,
        IReadOnlyDictionary<Guid, string> documentTexts,
        string model,
        CancellationToken cancellationToken)
    {
        var response = await _modelProvider.GenerateAsync(
            new AiPromptRequest(
                AgentId,
                BuildExplanationPrompt(context, scored, documentTexts),
                new Dictionary<string, string>
                {
                    ["model"] = model,
                    ["output"] = "json"
                }),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidOperationException("The Applicant Ranking Agent returned an empty explanation response.");
        }

        var normalized = response.Trim();
        if (normalized.StartsWith("```", StringComparison.Ordinal))
        {
            normalized = normalized
                .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("```", string.Empty, StringComparison.Ordinal)
                .Trim();
        }

        var items = JsonSerializer.Deserialize<AiExplanationItem[]>(normalized, JsonOptions) ?? [];
        var explanations = items
            .Where(item => Guid.TryParse(item.JobApplicationId, out _) && !string.IsNullOrWhiteSpace(item.Explanation))
            .ToDictionary(
                item => Guid.Parse(item.JobApplicationId),
                item => item.Explanation.Trim());

        if (explanations.Count == 0 && scored.Count > 0)
        {
            throw new InvalidOperationException("The Applicant Ranking Agent did not return any usable LLM explanations.");
        }

        return explanations;
    }

    private static ScoredApplication ScoreApplication(
        OperationsApplicantRankingContext context,
        OperationsApplicantRankingApplication application,
        IReadOnlyDictionary<Guid, decimal> vectorScores,
        IReadOnlyDictionary<Guid, string> documentTexts)
    {
        var requestedSkillCount = application.MatchedSkills.Count + application.MissingSkills.Count;
        var skillCoverage = requestedSkillCount == 0
            ? 0m
            : (decimal)application.MatchedSkills.Count / requestedSkillCount;
        var vectorSimilarity = vectorScores.TryGetValue(application.JobApplicationId, out var vectorScore)
            ? Clamp(vectorScore, 0, 1)
            : 0m;
        var fit = ScoreExperienceLocationNotice(context, application);
        var historicalSignal = ScoreHistoricalSignal(application);
        var evidenceCompleteness = ScoreEvidenceCompleteness(application, documentTexts);
        var recency = ScoreRecency(application.AppliedAt);
        var score = (skillCoverage * 35m) +
                    (vectorSimilarity * 25m) +
                    (fit * 15m) +
                    (historicalSignal * 10m) +
                    (evidenceCompleteness * 10m) +
                    (recency * 5m);

        return new ScoredApplication(
            application,
            decimal.Round(Clamp(score, 0, 100), 2),
            decimal.Round(skillCoverage, 4),
            decimal.Round(vectorSimilarity, 4),
            decimal.Round(fit, 4),
            decimal.Round(historicalSignal, 4),
            decimal.Round(evidenceCompleteness, 4),
            decimal.Round(recency, 4));
    }

    private static ApplicantRankingRankedApplication ToRankedApplication(
        ScoredApplication scored,
        int rank,
        IReadOnlyDictionary<Guid, string> aiExplanations)
    {
        var strengths = BuildStrengths(scored);
        var gaps = BuildGaps(scored);
        var documentEvidence = BuildDocumentEvidence(scored.Application);
        var historicalEvidence = BuildHistoricalEvidence(scored.Application);
        var explanation = RequiredAiExplanation(aiExplanations, scored.Application.JobApplicationId, scored.Application.CandidateName);

        return new ApplicantRankingRankedApplication(
            scored.Application.JobApplicationId,
            rank,
            scored.Score,
            ConfidenceForScore(scored.Score),
            explanation,
            strengths,
            gaps,
            documentEvidence,
            historicalEvidence);
    }

    private static decimal ScoreExperienceLocationNotice(
        OperationsApplicantRankingContext context,
        OperationsApplicantRankingApplication application)
    {
        var experienceFit = ScoreExperience(application.ExperienceYears, context.ExperienceMinYears, context.ExperienceMaxYears);
        var locationFit = ScoreLocation(context.JobPost.Location, context.JobRequest.Location, application.ApplicationSnapshotJson);
        var noticeFit = application.NoticePeriodDays switch
        {
            null => 0.5m,
            <= 15 => 1m,
            <= 30 => 0.9m,
            <= 60 => 0.7m,
            <= 90 => 0.45m,
            _ => 0.25m
        };

        return Clamp((experienceFit * 0.55m) + (locationFit * 0.2m) + (noticeFit * 0.25m), 0, 1);
    }

    private static decimal ScoreHistoricalSignal(OperationsApplicantRankingApplication application)
    {
        var interviews = application.HistoricalInterviewEvidence
            .Where(interview => string.Equals(interview.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (interviews.Length == 0)
        {
            return application.HistoricalApplicationEvidence.Count > 0 ? 0.4m : 0.2m;
        }

        var passed = interviews.Count(IsPassedInterview);
        var passRate = (decimal)passed / interviews.Length;
        var bestStatus = application.HistoricalApplicationEvidence
            .Select(applicationEvidence => applicationEvidence.Status switch
            {
                "OnHold" => 0.95m,
                "OfferDeclined" => 0.75m,
                "Rejected" when passed > 0 => 0.55m,
                "Withdrawn" => 0.35m,
                _ => 0.45m
            })
            .DefaultIfEmpty(0.4m)
            .Max();

        return Clamp((passRate * 0.65m) + (bestStatus * 0.35m), 0, 1);
    }

    private static decimal ScoreEvidenceCompleteness(
        OperationsApplicantRankingApplication application,
        IReadOnlyDictionary<Guid, string> documentTexts)
    {
        var score = 0m;
        if (application.Skills.Count > 0)
        {
            score += 0.2m;
        }

        if (application.ExperienceYears.HasValue || !string.IsNullOrWhiteSpace(application.CurrentDesignation))
        {
            score += 0.2m;
        }

        if (!string.IsNullOrWhiteSpace(application.CoverLetterText))
        {
            score += 0.2m;
        }

        if (application.DocumentEvidence.Count > 0)
        {
            score += 0.2m;
        }

        if (application.DocumentEvidence.Any(document => document.HasExtractedText) ||
            documentTexts.ContainsKey(application.JobApplicationId))
        {
            score += 0.2m;
        }

        return Clamp(score, 0, 1);
    }

    private static decimal ScoreRecency(DateTimeOffset appliedAt)
    {
        var ageDays = Math.Max((DateTimeOffset.UtcNow - appliedAt.ToUniversalTime()).TotalDays, 0);
        return ageDays switch
        {
            <= 7 => 1m,
            <= 30 => 0.75m,
            <= 90 => 0.45m,
            _ => 0.25m
        };
    }

    private static decimal ScoreExperience(decimal? experienceYears, decimal? minYears, decimal? maxYears)
    {
        if (!experienceYears.HasValue)
        {
            return 0.5m;
        }

        if (!minYears.HasValue && !maxYears.HasValue)
        {
            return 0.75m;
        }

        var value = experienceYears.Value;
        if (minYears.HasValue && value < minYears.Value)
        {
            var gap = minYears.Value - value;
            return Clamp(1m - (gap / Math.Max(minYears.Value, 1m)), 0, 1);
        }

        if (maxYears.HasValue && value > maxYears.Value)
        {
            var over = value - maxYears.Value;
            return Clamp(1m - (over / Math.Max(maxYears.Value * 2m, 1m)), 0.65m, 1m);
        }

        return 1m;
    }

    private static decimal ScoreLocation(string? jobPostLocation, string? jobRequestLocation, string? applicationSnapshotJson)
    {
        var desired = SafeField(jobPostLocation ?? jobRequestLocation).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(desired) || desired.Contains("remote"))
        {
            return 0.8m;
        }

        return !string.IsNullOrWhiteSpace(applicationSnapshotJson) &&
               applicationSnapshotJson.Contains(desired, StringComparison.OrdinalIgnoreCase)
            ? 0.85m
            : 0.55m;
    }

    private static bool IsPassedInterview(OperationsCandidateInterviewEvidence interview)
    {
        var recommendation = (interview.Recommendation ?? string.Empty).Trim();
        if (recommendation is "StrongHire" or "Hire" or "Proceed")
        {
            return true;
        }

        if (recommendation is "NoHire")
        {
            return false;
        }

        var scores = new[] { interview.TechnicalScore, interview.CommunicationScore, interview.CultureScore }
            .Where(score => score.HasValue)
            .Select(score => score!.Value)
            .ToArray();
        return scores.Length > 0 && scores.Average() >= 3.5;
    }

    private static IReadOnlyList<string> BuildStrengths(ScoredApplication scored)
    {
        var strengths = new List<string>();
        if (scored.Application.MatchedSkills.Count > 0)
        {
            strengths.Add($"Matches {string.Join(", ", scored.Application.MatchedSkills)}.");
        }

        if (scored.Application.ExperienceYears.HasValue)
        {
            strengths.Add($"{FormatYears(scored.Application.ExperienceYears)} years of candidate experience.");
        }

        if (!string.IsNullOrWhiteSpace(scored.Application.CoverLetterText))
        {
            strengths.Add("Submitted a cover letter for this application.");
        }

        if (scored.Application.DocumentEvidence.Count > 0)
        {
            strengths.Add("Uploaded CV/application document evidence is available.");
        }

        if (scored.HistoricalSignal >= 0.65m)
        {
            strengths.Add("Prior interview or outcome history is favorable.");
        }

        return strengths.Count == 0 ? ["Current application has enough tenant profile evidence to review."] : strengths;
    }

    private static IReadOnlyList<string> BuildGaps(ScoredApplication scored)
    {
        var gaps = new List<string>();
        if (scored.Application.MissingSkills.Count > 0)
        {
            gaps.AddRange(scored.Application.MissingSkills.Select(skill => $"Missing requested skill evidence: {skill}."));
        }

        if (string.IsNullOrWhiteSpace(scored.Application.CoverLetterText))
        {
            gaps.Add("No cover letter was submitted.");
        }

        if (scored.Application.DocumentEvidence.Count == 0)
        {
            gaps.Add("No CV or application document is attached.");
        }

        if (scored.VectorSimilarity == 0)
        {
            gaps.Add("Semantic similarity was unavailable for this ranking run.");
        }

        return gaps.Count == 0 ? ["No major applicant-ranking caveats were found."] : gaps;
    }

    private static IReadOnlyList<string> BuildDocumentEvidence(OperationsApplicantRankingApplication application)
    {
        var evidence = new List<string>();
        if (!string.IsNullOrWhiteSpace(application.CoverLetterText))
        {
            evidence.Add("Cover letter submitted.");
        }

        evidence.AddRange(application.DocumentEvidence.Select(document =>
        {
            var status = document.ExtractionStatus switch
            {
                "Extracted" => "text parsed for semantic ranking",
                "Failed" => $"text extraction failed{(string.IsNullOrWhiteSpace(document.ExtractionError) ? string.Empty : $": {document.ExtractionError}")}",
                "Unsupported" => "text extraction unsupported",
                _ => "text extraction pending"
            };
            return $"{document.DocumentType}: {document.FileName} ({FormatBytes(document.SizeBytes)}), {status}.";
        }));

        return evidence.Count == 0 ? ["No CV, cover letter, or uploaded document evidence was available."] : evidence;
    }

    private static IReadOnlyList<string> BuildHistoricalEvidence(OperationsApplicantRankingApplication application)
    {
        var applications = application.HistoricalApplicationEvidence
            .Take(3)
            .Select(evidence => $"{evidence.RequestCode} {evidence.DisplayJobTitle ?? evidence.JobTitle} - {evidence.Status} - {evidence.InterviewPassSummary ?? $"{evidence.InterviewsPassed}/{evidence.InterviewsTotal} passed"}")
            .ToArray();
        var interviews = application.HistoricalInterviewEvidence
            .Take(3)
            .Select(interview => $"{interview.RoundName} - {interview.Status} - {interview.Recommendation ?? "No recommendation"}")
            .ToArray();

        var evidenceItems = applications.Concat(interviews).ToArray();
        return evidenceItems.Length == 0 ? ["No prior application or interview evidence was found."] : evidenceItems;
    }

    private static string RequiredAiExplanation(
        IReadOnlyDictionary<Guid, string> explanations,
        Guid jobApplicationId,
        string candidateName)
    {
        if (explanations.TryGetValue(jobApplicationId, out var explanation) &&
            !string.IsNullOrWhiteSpace(explanation))
        {
            return explanation;
        }

        throw new InvalidOperationException(
            $"The Applicant Ranking Agent did not return an LLM explanation for {candidateName}.");
    }

    private static string BuildRunInputText(
        OperationsApplicantRankingContext context,
        IReadOnlyDictionary<Guid, string> documentTexts)
    {
        var builder = new StringBuilder();
        builder.AppendLine(BuildRequirementProfileText(context));
        foreach (var application in context.Applications.OrderBy(application => application.JobApplicationId))
        {
            builder.AppendLine(BuildApplicationProfileText(application, documentTexts));
        }

        return builder.ToString();
    }

    private static string BuildRequirementProfileText(OperationsApplicantRankingContext context)
    {
        return string.Join('\n', new[]
        {
            $"Requirement source: JobPost",
            $"Title: {SafeField(context.JobPost.Title)}",
            $"Client: {SafeField(context.JobRequest.Client)}",
            $"Department: {SafeField(context.JobPost.Department ?? context.JobRequest.Department)}",
            $"Location: {SafeField(context.JobPost.Location ?? context.JobRequest.Location)}",
            $"Experience: {FormatRange(context.ExperienceMinYears, context.ExperienceMaxYears)}",
            $"Skills: {SafeField(string.Join(", ", context.RequiredSkills))}",
            $"Description: {SafeField(context.JobPost.Description)}"
        });
    }

    private static string BuildApplicationProfileText(
        OperationsApplicantRankingApplication application,
        IReadOnlyDictionary<Guid, string> documentTexts)
    {
        var history = application.HistoricalApplicationEvidence.Count == 0
            ? "No previous applications"
            : string.Join("; ", application.HistoricalApplicationEvidence.Take(5).Select(evidence =>
                $"{evidence.RequestCode} {evidence.DisplayJobTitle ?? evidence.JobTitle} {evidence.Department} {evidence.Status} {evidence.FinalDecisionReason}"));
        var interviews = application.HistoricalInterviewEvidence.Count == 0
            ? "No interview feedback"
            : string.Join("; ", application.HistoricalInterviewEvidence.Take(5).Select(interview =>
                $"{interview.RoundName} {interview.Status} {interview.Recommendation} technical {interview.TechnicalScore} communication {interview.CommunicationScore} culture {interview.CultureScore} {interview.FeedbackSummary}"));
        documentTexts.TryGetValue(application.JobApplicationId, out var documentText);

        return string.Join('\n', new[]
        {
            $"ApplicationId: {application.JobApplicationId}",
            $"Candidate: {application.CandidateName}",
            $"Designation: {SafeField(application.CurrentDesignation)}",
            $"Company: {SafeField(application.CurrentCompany)}",
            $"Experience: {FormatYears(application.ExperienceYears)}",
            $"Notice period days: {application.NoticePeriodDays?.ToString() ?? "Not recorded"}",
            $"Skills: {string.Join(", ", application.Skills)}",
            $"Matched skills: {string.Join(", ", application.MatchedSkills)}",
            $"Missing skills: {string.Join(", ", application.MissingSkills)}",
            $"Application status: {application.ApplicationStatus}",
            $"Source: {application.SourceLabel} {application.SourceDetail}",
            $"Cover letter: {SafeField(TrimText(application.CoverLetterText, 2000))}",
            $"Documents: {string.Join("; ", application.DocumentEvidence.Select(document => $"{document.DocumentType} {document.FileName} {document.ContentType} extraction {document.ExtractionStatus}"))}",
            $"Document text: {SafeField(TrimText(documentText, 5000))}",
            $"Application snapshot: {SafeField(TrimText(application.ApplicationSnapshotJson, 2000))}",
            $"Historical applications: {history}",
            $"Historical interviews: {interviews}"
        });
    }

    private static string BuildExplanationPrompt(
        OperationsApplicantRankingContext context,
        IReadOnlyList<ScoredApplication> scored,
        IReadOnlyDictionary<Guid, string> documentTexts)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are the Talent Pilot Applicant Ranking agent.");
        builder.AppendLine("Write concise recruiter-facing explanations for current applicants on the active job post.");
        builder.AppendLine("Use only the supplied tenant evidence. Do not search the web. Do not mention private identifiers beyond names already supplied. Do not decide whether to shortlist, reject, contact, or move workflow.");
        builder.AppendLine("Return JSON only: [{\"jobApplicationId\":\"guid\",\"explanation\":\"plain text\"}].");
        builder.AppendLine();
        builder.AppendLine("Job post requirement:");
        builder.AppendLine(BuildRequirementProfileText(context));
        builder.AppendLine();
        builder.AppendLine("Applications:");

        foreach (var scoredApplication in scored)
        {
            var application = scoredApplication.Application;
            documentTexts.TryGetValue(application.JobApplicationId, out var documentText);
            builder.AppendLine($"JobApplicationId: {application.JobApplicationId}");
            builder.AppendLine($"Candidate: {application.CandidateName}");
            builder.AppendLine($"Current role: {SafeField(application.CurrentDesignation)} at {SafeField(application.CurrentCompany)}");
            builder.AppendLine($"Skills matched: {string.Join(", ", application.MatchedSkills.DefaultIfEmpty("None"))}");
            builder.AppendLine($"Skill gaps: {string.Join(", ", application.MissingSkills.DefaultIfEmpty("None"))}");
            builder.AppendLine($"Scores: overall {scoredApplication.Score}, skill {FormatPercent(scoredApplication.SkillCoverage)}, vector {FormatPercent(scoredApplication.VectorSimilarity)}, fit {FormatPercent(scoredApplication.Fit)}, history {FormatPercent(scoredApplication.HistoricalSignal)}, evidence {FormatPercent(scoredApplication.EvidenceCompleteness)}, recency {FormatPercent(scoredApplication.Recency)}");
            builder.AppendLine($"Cover letter: {SafeField(TrimText(application.CoverLetterText, 800))}");
            builder.AppendLine($"Document evidence: {string.Join(" | ", BuildDocumentEvidence(application))}");
            builder.AppendLine($"Document text excerpt: {SafeField(TrimText(documentText, 1000))}");
            builder.AppendLine($"Historical evidence: {string.Join(" | ", BuildHistoricalEvidence(application))}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private async Task TryMarkFailedAsync(
        Guid tenantId,
        Guid runId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            await _runLogger.FailAsync(
                tenantId,
                runId,
                exception.Message.Length <= 900 ? exception.Message : exception.Message[..900],
                new Dictionary<string, string>
                {
                    ["errorType"] = exception.GetType().Name,
                    ["error"] = exception.Message
                },
                cancellationToken);
        }
        catch
        {
            // Best-effort logging only.
        }
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    private static string ConfidenceForScore(decimal score)
    {
        return score switch
        {
            >= 80m => "High",
            >= 60m => "Medium",
            _ => "Low"
        };
    }

    private static string FormatPercent(decimal value)
    {
        return $"{decimal.Round(value * 100m, 0)}%";
    }

    private static string FormatYears(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.0") : "Not recorded";
    }

    private static string FormatRange(decimal? min, decimal? max)
    {
        return (min, max) switch
        {
            (not null, not null) => $"{min:0.#}-{max:0.#} years",
            (not null, null) => $"{min:0.#}+ years",
            (null, not null) => $"Up to {max:0.#} years",
            _ => "Not recorded"
        };
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_048_576 => $"{bytes / 1_048_576m:0.#} MB",
            >= 1024 => $"{bytes / 1024m:0.#} KB",
            _ => $"{bytes} B"
        };
    }

    private static string SafeField(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not provided" : value.Trim();
    }

    private static string TrimText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed record ScoredApplication(
        OperationsApplicantRankingApplication Application,
        decimal Score,
        decimal SkillCoverage,
        decimal VectorSimilarity,
        decimal Fit,
        decimal HistoricalSignal,
        decimal EvidenceCompleteness,
        decimal Recency);

    private sealed record VectorScoreResult(
        IReadOnlyDictionary<Guid, decimal> Scores,
        string Status);

    private sealed record AiExplanationItem(
        [property: JsonPropertyName("jobApplicationId")] string JobApplicationId,
        [property: JsonPropertyName("explanation")] string Explanation);
}
