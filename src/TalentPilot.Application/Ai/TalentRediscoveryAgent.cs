using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Operations;

namespace TalentPilot.Application.Ai;

public interface ITalentRediscoveryAgent
{
    Task<TalentRediscoveryRankResult> RankAsync(
        Guid tenantId,
        OperationsTalentRediscoveryContext context,
        CancellationToken cancellationToken);
}

public sealed record TalentRediscoveryRankResult(
    IReadOnlyList<TalentRediscoveryRankedCandidate> Matches,
    Guid AgentRunId,
    string Model,
    DateTimeOffset GeneratedAtUtc);

public sealed record TalentRediscoveryRankedCandidate(
    Guid CandidateId,
    int Rank,
    decimal Score,
    string Confidence,
    string Explanation,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps);

public sealed class TalentRediscoveryAgent : ITalentRediscoveryAgent
{
    public const string AgentId = "talent-rediscovery";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IAiModelProvider _modelProvider;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly IAiRuntimeSettingsResolver _settingsResolver;
    private readonly IAiAgentRunLogger _runLogger;

    public TalentRediscoveryAgent(
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

    public async Task<TalentRediscoveryRankResult> RankAsync(
        Guid tenantId,
        OperationsTalentRediscoveryContext context,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.GetCurrentAsync(cancellationToken);
        var rankingContext = context with
        {
            Candidates = context.Candidates
                .Where(IsRediscoverableCandidate)
                .ToArray()
        };
        var generatedAt = DateTimeOffset.UtcNow;
        var inputHash = AiTextHasher.HashText(BuildRunInputText(rankingContext));
        var runId = await _runLogger.StartAsync(
            new AiAgentRunStart(
                tenantId,
                AgentId,
                "JobRequest",
                rankingContext.JobRequest.Id,
                settings.LlmModel,
                settings.EmbeddingModel,
                inputHash,
                new Dictionary<string, string>
                {
                    ["purpose"] = "talent-rediscovery",
                    ["humanDecisionRequired"] = "true",
                    ["candidateCount"] = rankingContext.Candidates.Count.ToString(),
                    ["requirementSource"] = rankingContext.RequirementSource
                }),
            cancellationToken);

        try
        {
            var vectorResult = await TryBuildVectorScoresAsync(tenantId, settings, rankingContext, cancellationToken);
            var scored = rankingContext.Candidates
                .Select(candidate => ScoreCandidate(rankingContext, candidate, vectorResult.Scores))
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var explanations = await TryGenerateExplanationsAsync(rankingContext, scored, settings.LlmModel, cancellationToken);
            var ranked = scored
                .Select((item, index) => ToRankedCandidate(item, index + 1, explanations))
                .ToArray();

            await _runLogger.SucceedAsync(
                tenantId,
                runId,
                $"Ranked {ranked.Length} warm candidate(s) for {rankingContext.JobRequest.Code}.",
                new Dictionary<string, string>
                {
                    ["model"] = settings.LlmModel,
                    ["embeddingModel"] = settings.EmbeddingModel,
                    ["semanticSimilarityStatus"] = vectorResult.Status,
                    ["generatedAtUtc"] = generatedAt.ToString("O")
                },
                cancellationToken);

            return new TalentRediscoveryRankResult(ranked, runId, settings.LlmModel, generatedAt);
        }
        catch (Exception ex)
        {
            await TryMarkFailedAsync(tenantId, runId, ex, cancellationToken);
            throw;
        }
    }

    private static bool IsRediscoverableCandidate(OperationsRediscoveryCandidate candidate)
    {
        return !candidate.ApplicationEvidence.Any(application =>
            string.Equals(application.Status, "Joined", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(application.Status, "Hired", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<VectorScoreResult> TryBuildVectorScoresAsync(
        Guid tenantId,
        AiRuntimeSettingsSnapshot settings,
        OperationsTalentRediscoveryContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var candidate in context.Candidates)
            {
                var candidateText = BuildCandidateProfileText(candidate);
                var sourceHash = AiTextHasher.HashText(candidateText);
                await UpsertApplicationEvidenceVectorsAsync(tenantId, settings, candidate, cancellationToken);

                var existingHash = await _vectorStore.GetActiveSourceTextHashAsync(
                    tenantId,
                    "Candidate",
                    candidate.CandidateId,
                    "CandidateProfile",
                    settings.EmbeddingModel,
                    cancellationToken);

                if (string.Equals(existingHash, sourceHash, StringComparison.Ordinal))
                {
                    continue;
                }

                var candidateEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(candidateText, cancellationToken);
                if (candidateEmbedding.Length != settings.EmbeddingDimensions)
                {
                    continue;
                }

                await _vectorStore.UpsertAsync(
                    new VectorRecord(
                        tenantId,
                        "Candidate",
                        candidate.CandidateId,
                        "CandidateProfile",
                        sourceHash,
                        settings.EmbeddingModel,
                        settings.EmbeddingDimensions,
                        candidateEmbedding),
                    cancellationToken);
            }

            var queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(BuildRequirementProfileText(context), cancellationToken);
            if (queryEmbedding.Length != settings.EmbeddingDimensions)
            {
                return new VectorScoreResult(new Dictionary<Guid, decimal>(), "Unavailable: requirement embedding dimensions mismatch");
            }

            var requirementEntityType = context.JobPost is null ? "JobRequest" : "JobPost";
            var requirementEntityId = context.JobPost?.JobPostId ?? context.JobRequest.Id;
            var requirementText = BuildRequirementProfileText(context);
            var requirementHash = AiTextHasher.HashText(requirementText);
            var existingRequirementHash = await _vectorStore.GetActiveSourceTextHashAsync(
                tenantId,
                requirementEntityType,
                requirementEntityId,
                "TalentRediscoveryRequirementProfile",
                settings.EmbeddingModel,
                cancellationToken);
            if (!string.Equals(existingRequirementHash, requirementHash, StringComparison.Ordinal))
            {
                await _vectorStore.UpsertAsync(
                    new VectorRecord(
                        tenantId,
                        requirementEntityType,
                        requirementEntityId,
                        "TalentRediscoveryRequirementProfile",
                        requirementHash,
                        settings.EmbeddingModel,
                        settings.EmbeddingDimensions,
                        queryEmbedding),
                    cancellationToken);
            }

            var results = await _vectorStore.SearchAsync(
                new VectorSearchRequest(
                    tenantId,
                    "Candidate",
                    queryEmbedding,
                    Math.Max(context.Candidates.Count, 1)),
                cancellationToken);
            var candidateIds = context.Candidates.Select(candidate => candidate.CandidateId).ToHashSet();

            var scores = results
                .Where(result => candidateIds.Contains(result.EntityId))
                .ToDictionary(result => result.EntityId, result => Clamp(result.Score, 0, 1));
            return new VectorScoreResult(scores, scores.Count == 0 ? "Unavailable: no candidate vectors matched" : "Available");
        }
        catch (Exception ex)
        {
            return new VectorScoreResult(new Dictionary<Guid, decimal>(), SemanticSimilarityDiagnostics.Unavailable(ex, settings));
        }
    }

    private async Task UpsertApplicationEvidenceVectorsAsync(
        Guid tenantId,
        AiRuntimeSettingsSnapshot settings,
        OperationsRediscoveryCandidate candidate,
        CancellationToken cancellationToken)
    {
        foreach (var application in candidate.ApplicationEvidence)
        {
            if (!ApplicationHasPersistedEvidence(application))
            {
                continue;
            }

            var profileText = BuildApplicationEvidenceProfileText(candidate, application);
            var sourceHash = AiTextHasher.HashText(profileText);
            var existingHash = await _vectorStore.GetActiveSourceTextHashAsync(
                tenantId,
                "JobApplication",
                application.JobApplicationId,
                "JobApplicationEvidenceProfile",
                settings.EmbeddingModel,
                cancellationToken);

            if (string.Equals(existingHash, sourceHash, StringComparison.Ordinal))
            {
                continue;
            }

            var embedding = await _embeddingProvider.GenerateEmbeddingAsync(profileText, cancellationToken);
            if (embedding.Length != settings.EmbeddingDimensions)
            {
                continue;
            }

            await _vectorStore.UpsertAsync(
                new VectorRecord(
                    tenantId,
                    "JobApplication",
                    application.JobApplicationId,
                    "JobApplicationEvidenceProfile",
                    sourceHash,
                    settings.EmbeddingModel,
                    settings.EmbeddingDimensions,
                    embedding),
                cancellationToken);
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> TryGenerateExplanationsAsync(
        OperationsTalentRediscoveryContext context,
        IReadOnlyList<ScoredCandidate> scored,
        string model,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _modelProvider.GenerateAsync(
                new AiPromptRequest(
                    AgentId,
                    BuildExplanationPrompt(context, scored),
                    new Dictionary<string, string>
                    {
                        ["model"] = model,
                        ["output"] = "json"
                    }),
                cancellationToken);

            var normalized = response.Trim();
            if (normalized.StartsWith("```", StringComparison.Ordinal))
            {
                normalized = normalized
                    .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("```", string.Empty, StringComparison.Ordinal)
                    .Trim();
            }

            var items = JsonSerializer.Deserialize<AiExplanationItem[]>(normalized, JsonOptions) ?? [];
            return items
                .Where(item => Guid.TryParse(item.CandidateId, out _))
                .ToDictionary(
                    item => Guid.Parse(item.CandidateId),
                    item => string.IsNullOrWhiteSpace(item.Explanation) ? string.Empty : item.Explanation.Trim());
        }
        catch
        {
            return new Dictionary<Guid, string>();
        }
    }

    private static ScoredCandidate ScoreCandidate(
        OperationsTalentRediscoveryContext context,
        OperationsRediscoveryCandidate candidate,
        IReadOnlyDictionary<Guid, decimal> vectorScores)
    {
        var requestedSkillCount = candidate.MatchedSkills.Count + candidate.MissingSkills.Count;
        var skillCoverage = requestedSkillCount == 0
            ? 0m
            : (decimal)candidate.MatchedSkills.Count / requestedSkillCount;
        var vectorSimilarity = vectorScores.TryGetValue(candidate.CandidateId, out var vectorScore)
            ? Clamp(vectorScore, 0, 1)
            : 0m;
        var historicalOutcomeFit = ScoreHistoricalOutcome(candidate);
        var similarRoleFit = ScoreSimilarHistory(context, candidate);
        var experienceAvailabilityFit = ScoreExperienceAvailability(
            candidate.ExperienceYears,
            candidate.NoticePeriodDays,
            context.ExperienceMinYears,
            context.ExperienceMaxYears);
        var priorityBoost = ScoreWarmCandidatePriority(context, candidate, skillCoverage);
        var score = (skillCoverage * 35m) +
                    (vectorSimilarity * 20m) +
                    (historicalOutcomeFit * 15m) +
                    (similarRoleFit * 15m) +
                    (experienceAvailabilityFit * 10m) +
                    priorityBoost;
        var hasPersistedEvidence = CandidateHasPersistedEvidence(candidate);
        if (skillCoverage == 0m)
        {
            score = Math.Min(score, hasPersistedEvidence && vectorSimilarity >= 0.72m ? 39m : 35m);
        }
        else if (skillCoverage < 0.25m && (!hasPersistedEvidence || vectorSimilarity < 0.72m))
        {
            score = Math.Min(score, 50m);
        }

        return new ScoredCandidate(
            candidate,
            decimal.Round(Clamp(score, 0, 100), 2),
            decimal.Round(skillCoverage, 4),
            decimal.Round(vectorSimilarity, 4),
            decimal.Round(historicalOutcomeFit, 4),
            decimal.Round(similarRoleFit, 4),
            decimal.Round(experienceAvailabilityFit, 4));
    }

    private static TalentRediscoveryRankedCandidate ToRankedCandidate(
        ScoredCandidate scored,
        int rank,
        IReadOnlyDictionary<Guid, string> aiExplanations)
    {
        var strengths = BuildStrengths(scored);
        var gaps = BuildGaps(scored);
        var explanation = aiExplanations.TryGetValue(scored.Candidate.CandidateId, out var aiExplanation) &&
                          !string.IsNullOrWhiteSpace(aiExplanation)
            ? aiExplanation
            : BuildFallbackExplanation(scored);

        return new TalentRediscoveryRankedCandidate(
            scored.Candidate.CandidateId,
            rank,
            scored.Score,
            ConfidenceForScore(scored.Score),
            explanation,
            strengths,
            gaps);
    }

    private static decimal ScoreHistoricalOutcome(OperationsRediscoveryCandidate candidate)
    {
        var statusScore = candidate.ApplicationEvidence
            .Select(application => application.Status switch
            {
                "OnHold" => 0.95m,
                "Hired" => 0m,
                "OfferDeclined" => 0.75m,
                "Rejected" when candidate.InterviewEvidence.Count > 0 => 0.55m,
                "Rejected" => 0.35m,
                "Withdrawn" => 0.25m,
                _ => 0.45m
            })
            .DefaultIfEmpty(0.2m)
            .Max();

        var submittedFeedback = candidate.InterviewEvidence
            .Where(interview =>
                interview.TechnicalScore.HasValue ||
                interview.CommunicationScore.HasValue ||
                interview.CultureScore.HasValue ||
                !string.IsNullOrWhiteSpace(interview.Recommendation))
            .ToArray();
        if (submittedFeedback.Length == 0)
        {
            return statusScore;
        }

        var feedbackScores = submittedFeedback.Select(interview =>
        {
            var numericScores = new[] { interview.TechnicalScore, interview.CommunicationScore, interview.CultureScore }
                .Where(score => score.HasValue)
                .Select(score => (decimal)score!.Value / 5m)
                .ToArray();
            var averageScore = numericScores.Length == 0 ? 0.5m : numericScores.Average();
            var recommendationScore = (interview.Recommendation ?? string.Empty).Trim() switch
            {
                "StrongHire" => 1m,
                "Hire" => 0.9m,
                "Proceed" => 0.85m,
                "Hold" => 0.55m,
                "NoHire" => 0.2m,
                _ => averageScore
            };

            return (averageScore * 0.6m) + (recommendationScore * 0.4m);
        });

        return Clamp((statusScore * 0.45m) + (feedbackScores.Max() * 0.55m), 0, 1);
    }

    private static decimal ScoreWarmCandidatePriority(
        OperationsTalentRediscoveryContext context,
        OperationsRediscoveryCandidate candidate,
        decimal skillCoverage)
    {
        if (skillCoverage < 0.25m)
        {
            return 0m;
        }

        var similarApplicationIds = candidate.ApplicationEvidence
            .Where(application => IsSimilarApplication(context, application))
            .Select(application => application.JobApplicationId)
            .ToHashSet();
        if (similarApplicationIds.Count == 0)
        {
            return 0m;
        }

        var similarInterviews = candidate.InterviewEvidence
            .Where(interview => similarApplicationIds.Contains(interview.JobApplicationId))
            .ToArray();
        var completedInterviewCount = similarInterviews.Count(interview =>
            string.Equals(interview.Status, "Completed", StringComparison.OrdinalIgnoreCase));
        var passedInterviewCount = similarInterviews.Count(IsPassedInterview);

        if (candidate.ApplicationEvidence.Any(application =>
                similarApplicationIds.Contains(application.JobApplicationId) &&
                string.Equals(application.Status, "OnHold", StringComparison.OrdinalIgnoreCase)) &&
            completedInterviewCount > 0 &&
            completedInterviewCount == passedInterviewCount)
        {
            return 5m;
        }

        if (completedInterviewCount > 0 && passedInterviewCount >= Math.Ceiling(completedInterviewCount * 0.5m))
        {
            return 3.5m;
        }

        if (candidate.ApplicationEvidence.Any(application =>
                similarApplicationIds.Contains(application.JobApplicationId) &&
                IsNonFitTerminalStatus(application.Status, application.FinalDecisionReason)) &&
            passedInterviewCount > 0)
        {
            return 2m;
        }

        return 0m;
    }

    private static decimal ScoreSimilarHistory(
        OperationsTalentRediscoveryContext context,
        OperationsRediscoveryCandidate candidate)
    {
        var requirementText = NormalizeTokens(string.Join(' ', new[]
        {
            context.JobPost?.Title ?? context.JobRequest.Title,
            context.JobRequest.Department,
            context.JobRequest.Client,
            string.Join(' ', context.RequiredSkills)
        }));
        if (requirementText.Count == 0 || candidate.ApplicationEvidence.Count == 0)
        {
            return 0.25m;
        }

        var best = candidate.ApplicationEvidence.Select(application =>
        {
            return ScoreApplicationSimilarity(requirementText, context, application);
        }).DefaultIfEmpty(0.25m).Max();

        return best;
    }

    private static bool IsSimilarApplication(
        OperationsTalentRediscoveryContext context,
        OperationsCandidateApplicationEvidence application)
    {
        var requirementText = NormalizeTokens(string.Join(' ', new[]
        {
            context.JobPost?.Title ?? context.JobRequest.Title,
            context.JobRequest.Department,
            context.JobRequest.Client,
            string.Join(' ', context.RequiredSkills)
        }));

        return ScoreApplicationSimilarity(requirementText, context, application) >= 0.5m;
    }

    private static decimal ScoreApplicationSimilarity(
        IReadOnlySet<string> requirementText,
        OperationsTalentRediscoveryContext context,
        OperationsCandidateApplicationEvidence application)
    {
        var historyTokens = NormalizeTokens(string.Join(' ', new[]
        {
            application.JobTitle,
            application.Department,
            application.Client
        }));
        if (historyTokens.Count == 0)
        {
            return 0.25m;
        }

        var overlap = historyTokens.Count(token => requirementText.Contains(token));
        var score = (decimal)overlap / Math.Max(requirementText.Count, 1);
        if (string.Equals(application.Department, context.JobRequest.Department, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.05m;
        }

        if (string.Equals(application.Client, context.JobRequest.Client, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.05m;
        }

        return Clamp(score, 0.1m, 1m);
    }

    private static bool IsPassedInterview(OperationsCandidateInterviewEvidence interview)
    {
        if (!string.Equals(interview.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

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

    private static bool IsNonFitTerminalStatus(string status, string? reason)
    {
        if (status is not ("OfferDeclined" or "Withdrawn" or "Rejected"))
        {
            return false;
        }

        var text = (reason ?? string.Empty).ToLowerInvariant();
        if (text.Contains("technical") ||
            text.Contains("skill") ||
            text.Contains("culture") ||
            text.Contains("poor") ||
            text.Contains("failed"))
        {
            return false;
        }

        return true;
    }

    private static decimal ScoreExperienceAvailability(
        decimal? experienceYears,
        int? noticePeriodDays,
        decimal? minYears,
        decimal? maxYears)
    {
        var experienceFit = ScoreExperience(experienceYears, minYears, maxYears);
        var availabilityFit = noticePeriodDays switch
        {
            null => 0.5m,
            <= 30 => 1m,
            <= 60 => 0.7m,
            <= 90 => 0.45m,
            _ => 0.25m
        };

        return Clamp((experienceFit * 0.75m) + (availabilityFit * 0.25m), 0, 1);
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

    private static IReadOnlyList<string> BuildStrengths(ScoredCandidate scored)
    {
        var strengths = new List<string>();
        if (scored.Candidate.MatchedSkills.Count > 0)
        {
            strengths.Add($"Matches {string.Join(", ", scored.Candidate.MatchedSkills)}.");
        }

        if (scored.Candidate.ExperienceYears.HasValue)
        {
            strengths.Add($"{FormatYears(scored.Candidate.ExperienceYears)} years of candidate experience.");
        }

        if (scored.Candidate.InterviewEvidence.Count > 0)
        {
            strengths.Add("Has previous interview evidence for recruiter review.");
        }

        if (scored.HistoricalOutcomeFit >= 0.65m)
        {
            strengths.Add("Historical application outcome is favorable enough to revisit.");
        }

        if (scored.SimilarRoleFit >= 0.65m)
        {
            strengths.Add("Previously applied to a similar role.");
        }

        return strengths.Count == 0 ? ["Has previous tenant application history for recruiter review."] : strengths;
    }

    private static IReadOnlyList<string> BuildGaps(ScoredCandidate scored)
    {
        var gaps = new List<string>();
        if (scored.SkillCoverage == 0m)
        {
            gaps.Add("Direct skill coverage is 0%, so this candidate should remain low priority unless a recruiter manually verifies role relevance.");
        }

        if (scored.Candidate.MissingSkills.Count > 0)
        {
            gaps.AddRange(scored.Candidate.MissingSkills.Select(skill => $"Missing requested skill evidence: {skill}."));
        }

        if (scored.Candidate.InterviewEvidence.Count == 0)
        {
            gaps.Add("No prior interview feedback is available.");
        }

        if (scored.Candidate.NoticePeriodDays is null)
        {
            gaps.Add("Candidate notice period is not recorded.");
        }

        return gaps.Count == 0 ? ["No major rediscovery caveats were found in current candidate history."] : gaps;
    }

    private static string BuildFallbackExplanation(ScoredCandidate scored)
    {
        var candidate = scored.Candidate;
        var lastApplication = candidate.ApplicationEvidence
            .OrderByDescending(application => application.AppliedAt)
            .FirstOrDefault();
        var history = lastApplication is null
            ? "No application history summary is available."
            : $"Most recent relevant application was {lastApplication.RequestCode} ({lastApplication.JobTitle}) with status {lastApplication.Status}.";
        var capWarning = scored.SkillCoverage == 0m
            ? " Direct skill coverage is 0%, so the score is intentionally capped at low fit even when warm-history or semantic signals exist."
            : string.Empty;

        return $"{candidate.DisplayName} is ranked for recruiter rediscovery because their profile has a skill coverage score of {FormatPercent(scored.SkillCoverage)}, vector similarity score of {FormatPercent(scored.VectorSimilarity)}, historical outcome score of {FormatPercent(scored.HistoricalOutcomeFit)}, similar-role score of {FormatPercent(scored.SimilarRoleFit)}, and experience/availability score of {FormatPercent(scored.ExperienceAvailabilityFit)}.{capWarning} {history} Recruiter should validate the listed gaps before contacting the candidate.";
    }

    private static string BuildRunInputText(OperationsTalentRediscoveryContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine(BuildRequirementProfileText(context));
        foreach (var candidate in context.Candidates.OrderBy(candidate => candidate.CandidateId))
        {
            builder.AppendLine(BuildCandidateProfileText(candidate));
        }

        return builder.ToString();
    }

    private static string BuildRequirementProfileText(OperationsTalentRediscoveryContext context)
    {
        var jobPost = context.JobPost;
        var title = jobPost?.Title ?? context.JobRequest.Title;
        var description = jobPost?.Description ?? context.JobRequest.Description;
        var skills = context.RequiredSkills.Count > 0 ? string.Join(", ", context.RequiredSkills) : string.Join(", ", context.JobRequest.Skills);
        return string.Join('\n', new[]
        {
            $"Requirement source: {context.RequirementSource}",
            $"Title: {SafeField(title)}",
            $"Client: {SafeField(context.JobRequest.Client)}",
            $"Department: {SafeField(context.JobRequest.Department)}",
            $"Location: {SafeField(jobPost?.Location ?? context.JobRequest.Location)}",
            $"Experience: {FormatRange(context.ExperienceMinYears, context.ExperienceMaxYears)}",
            $"Skills: {SafeField(skills)}",
            $"Description: {SafeField(description)}"
        });
    }

    private static string BuildCandidateProfileText(OperationsRediscoveryCandidate candidate)
    {
        var applicationText = candidate.ApplicationEvidence.Count == 0
            ? "No previous applications"
            : string.Join("; ", candidate.ApplicationEvidence.Select(application =>
                $"{application.RequestCode} {application.JobTitle} {application.Department} {application.Status} {application.FinalDecisionReason} cover letter {SafeField(TrimText(application.CoverLetterText, 500))} documents {BuildDocumentProfileText(application.DocumentEvidence)}"));
        var interviewText = candidate.InterviewEvidence.Count == 0
            ? "No interview feedback"
            : string.Join("; ", candidate.InterviewEvidence.Select(interview =>
                $"{interview.RoundName} {interview.Status} {interview.Recommendation} technical {interview.TechnicalScore} communication {interview.CommunicationScore} culture {interview.CultureScore} {interview.FeedbackSummary}"));

        return string.Join('\n', new[]
        {
            $"Candidate: {candidate.DisplayName}",
            $"Designation: {SafeField(candidate.CurrentDesignation)}",
            $"Company: {SafeField(candidate.CurrentCompany)}",
            $"Experience: {FormatYears(candidate.ExperienceYears)}",
            $"Notice period days: {candidate.NoticePeriodDays?.ToString() ?? "Not recorded"}",
            $"Skills: {string.Join(", ", candidate.Skills)}",
            $"Applications: {applicationText}",
            $"Interviews: {interviewText}"
        });
    }

    private static bool CandidateHasPersistedEvidence(OperationsRediscoveryCandidate candidate)
    {
        return candidate.ApplicationEvidence.Any(application =>
            ApplicationHasPersistedEvidence(application));
    }

    private static bool ApplicationHasPersistedEvidence(OperationsCandidateApplicationEvidence application)
    {
        return !string.IsNullOrWhiteSpace(application.CoverLetterText) ||
            (application.DocumentEvidence?.Any(document => document.HasExtractedText && !string.IsNullOrWhiteSpace(document.ExtractedText)) ?? false);
    }

    private static string BuildApplicationEvidenceProfileText(
        OperationsRediscoveryCandidate candidate,
        OperationsCandidateApplicationEvidence application)
    {
        return string.Join('\n', new[]
        {
            $"Candidate: {candidate.DisplayName}",
            $"Designation: {SafeField(candidate.CurrentDesignation)}",
            $"Candidate skills: {string.Join(", ", candidate.Skills)}",
            $"Application: {application.RequestCode} {application.DisplayJobTitle ?? application.JobTitle}",
            $"Application status: {application.Status}",
            $"Source: {application.SourceLabel}",
            $"Department: {application.Department}",
            $"Location: {application.Location}",
            $"Interview pass summary: {application.InterviewPassSummary ?? "Not recorded"}",
            $"Final reason: {SafeField(application.FinalDecisionReason)}",
            $"Cover letter: {SafeField(TrimText(application.CoverLetterText, 3000))}",
            $"Documents: {BuildDocumentProfileText(application.DocumentEvidence)}"
        });
    }

    private static string BuildDocumentProfileText(IReadOnlyList<OperationsApplicantDocumentEvidence>? documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return "No documents";
        }

        return string.Join(" | ", documents.Take(3).Select(document =>
            $"{document.DocumentType} {document.FileName} extraction {document.ExtractionStatus} text {SafeField(TrimText(document.ExtractedText, 1000))}"));
    }

    private static string BuildExplanationPrompt(
        OperationsTalentRediscoveryContext context,
        IReadOnlyList<ScoredCandidate> scored)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are the Talent Pilot Talent Rediscovery agent.");
        builder.AppendLine("Write concise recruiter-facing explanations for ranked warm candidates.");
        builder.AppendLine("Use only the supplied evidence. Do not search the web. Do not mention private identifiers beyond names already supplied. Do not decide whether to contact, invite, reject, or move a candidate.");
        builder.AppendLine("Return JSON only: [{\"candidateId\":\"guid\",\"explanation\":\"plain text\"}].");
        builder.AppendLine();
        builder.AppendLine("Requirement:");
        builder.AppendLine(BuildRequirementProfileText(context));
        builder.AppendLine();
        builder.AppendLine("Candidates:");

        foreach (var scoredCandidate in scored.Take(8))
        {
            var candidate = scoredCandidate.Candidate;
            builder.AppendLine($"CandidateId: {candidate.CandidateId}");
            builder.AppendLine($"Name: {candidate.DisplayName}");
            builder.AppendLine($"Current role: {SafeField(candidate.CurrentDesignation)} at {SafeField(candidate.CurrentCompany)}");
            builder.AppendLine($"Skills matched: {string.Join(", ", candidate.MatchedSkills.DefaultIfEmpty("None"))}");
            builder.AppendLine($"Skill gaps: {string.Join(", ", candidate.MissingSkills.DefaultIfEmpty("None"))}");
            builder.AppendLine($"Scores: overall {scoredCandidate.Score}, skill {FormatPercent(scoredCandidate.SkillCoverage)}, vector {FormatPercent(scoredCandidate.VectorSimilarity)}, history {FormatPercent(scoredCandidate.HistoricalOutcomeFit)}, similar role {FormatPercent(scoredCandidate.SimilarRoleFit)}, experience availability {FormatPercent(scoredCandidate.ExperienceAvailabilityFit)}");
            builder.AppendLine($"Applications: {string.Join(" | ", candidate.ApplicationEvidence.Take(4).Select(application => $"{application.RequestCode} {application.JobTitle} {application.Status} {application.FinalDecisionReason}"))}");
            builder.AppendLine($"Interview evidence: {string.Join(" | ", candidate.InterviewEvidence.Take(4).Select(interview => $"{interview.RoundName} {interview.Status} {interview.Recommendation} scores {interview.TechnicalScore}/{interview.CommunicationScore}/{interview.CultureScore} {interview.FeedbackSummary}"))}");
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
                "Talent Rediscovery failed.",
                new Dictionary<string, string>
                {
                    ["error"] = exception.Message
                },
                cancellationToken);
        }
        catch
        {
            // Best-effort logging only.
        }
    }

    private static HashSet<string> NormalizeTokens(string value)
    {
        string[] stopWords =
        [
            "and",
            "or",
            "the",
            "a",
            "an",
            "for",
            "to",
            "of",
            "with",
            "senior",
            "junior",
            "developer",
            "engineer"
        ];

        return value
            .Split([' ', ',', '.', '/', '-', '_', ':', ';', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.ToLowerInvariant())
            .Where(token => token.Length > 2)
            .Where(token => !stopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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

    private sealed record ScoredCandidate(
        OperationsRediscoveryCandidate Candidate,
        decimal Score,
        decimal SkillCoverage,
        decimal VectorSimilarity,
        decimal HistoricalOutcomeFit,
        decimal SimilarRoleFit,
        decimal ExperienceAvailabilityFit);

    private sealed record VectorScoreResult(
        IReadOnlyDictionary<Guid, decimal> Scores,
        string Status);

    private sealed record AiExplanationItem(
        [property: JsonPropertyName("candidateId")] string CandidateId,
        [property: JsonPropertyName("explanation")] string Explanation);
}
