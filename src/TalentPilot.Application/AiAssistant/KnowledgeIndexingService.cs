using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TalentPilot.Application.Ai;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Operations;

namespace TalentPilot.Application.AiAssistant;

public sealed class KnowledgeIndexingService : IKnowledgeIndexingService
{
    private const string KnowledgeChunkEntityType = "KnowledgeChunk";
    private readonly IOperationsRepository _operationsRepository;
    private readonly IKnowledgeRepository _knowledgeRepository;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly IAiRuntimeSettingsResolver _settingsResolver;

    public KnowledgeIndexingService(
        IOperationsRepository operationsRepository,
        IKnowledgeRepository knowledgeRepository,
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        IAiRuntimeSettingsResolver settingsResolver)
    {
        _operationsRepository = operationsRepository;
        _knowledgeRepository = knowledgeRepository;
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _settingsResolver = settingsResolver;
    }

    public async Task<IReadOnlyList<KnowledgeChunkUpsertResult>> EnsureContextIndexedAsync(
        Guid tenantId,
        Guid actorUserId,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        CancellationToken cancellationToken)
    {
        var normalizedContext = RagAssistantContextTypes.Normalize(contextType);
        var roleCodes = await _operationsRepository.GetActorRoleCodesAsync(tenantId, actorUserId, cancellationToken);
        var drafts = await BuildDraftsAsync(
            tenantId,
            actorUserId,
            roleCodes,
            normalizedContext,
            contextEntityId,
            focusEntityId,
            cancellationToken);

        if (drafts.Count == 0)
        {
            return Array.Empty<KnowledgeChunkUpsertResult>();
        }

        var upserted = await _knowledgeRepository.UpsertKnowledgeChunksAsync(tenantId, drafts, cancellationToken);
        await _knowledgeRepository.MarkStaleChunksInactiveAsync(
            tenantId,
            normalizedContext,
            contextEntityId,
            focusEntityId,
            upserted.Select(chunk => chunk.KnowledgeChunkId).ToArray(),
            cancellationToken);

        var settings = await _settingsResolver.GetCurrentAsync(cancellationToken);
        foreach (var chunk in upserted.Where(chunk => chunk.RequiresEmbedding))
        {
            var draft = drafts.First(item => item.ContentHash == chunk.SourceTextHash);
            var embedding = await _embeddingProvider.GenerateEmbeddingAsync(draft.Text, cancellationToken);
            await _vectorStore.UpsertAsync(
                new VectorRecord(
                    tenantId,
                    KnowledgeChunkEntityType,
                    chunk.KnowledgeChunkId,
                    chunk.ChunkType,
                    chunk.SourceTextHash,
                    settings.EmbeddingModel,
                    settings.EmbeddingDimensions,
                    embedding),
                cancellationToken);
        }

        return upserted;
    }

    private async Task<IReadOnlyList<KnowledgeChunkDraft>> BuildDraftsAsync(
        Guid tenantId,
        Guid actorUserId,
        IReadOnlySet<string> roleCodes,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        CancellationToken cancellationToken)
    {
        if (contextType == RagAssistantContextTypes.PmoRequest)
        {
            var includeEmployees = roleCodes.Contains("PMO") || roleCodes.Contains("TenantAdmin");
            var review = await _operationsRepository.GetPmoReviewAsync(
                tenantId,
                actorUserId,
                contextEntityId,
                includeEmployees,
                cancellationToken);
            return review is null ? Array.Empty<KnowledgeChunkDraft>() : BuildPmoRequestChunks(review);
        }

        if (contextType == RagAssistantContextTypes.RecruiterCandidateFit)
        {
            var sourcing = await _operationsRepository.GetRecruiterSourcingAsync(
                tenantId,
                actorUserId,
                contextEntityId,
                cancellationToken);
            return sourcing is null
                ? Array.Empty<KnowledgeChunkDraft>()
                : BuildRecruiterCandidateFitChunks(sourcing, focusEntityId);
        }

        if (contextType == RagAssistantContextTypes.HiringDecisionBrief)
        {
            var includeAllTenantReviews = roleCodes.Contains("TenantAdmin");
            var detail = await _operationsRepository.GetHiringReviewAsync(
                tenantId,
                actorUserId,
                includeAllTenantReviews,
                contextEntityId,
                cancellationToken);
            return detail is null
                ? Array.Empty<KnowledgeChunkDraft>()
                : BuildHiringDecisionBriefChunks(detail, contextEntityId);
        }

        return Array.Empty<KnowledgeChunkDraft>();
    }

    private static IReadOnlyList<KnowledgeChunkDraft> BuildPmoRequestChunks(OperationsPmoReview review)
    {
        var chunks = new List<KnowledgeChunkDraft>();
        var request = review.JobRequest;
        var route = $"/app/pmo/review/{request.Id}";
        var ordinal = 0;

        AddChunk(
            chunks,
            RagAssistantContextTypes.PmoRequest,
            request.Id,
            null,
            "JobRequest",
            request.Id,
            $"{request.Code} - {request.Title}",
            route,
            "bench.matches.view",
            "Internal",
            "JobRequest",
            ordinal++,
            Lines(
                $"Request: {request.Title} ({request.Code})",
                $"Client: {request.Client}",
                $"Department: {request.Department}",
                $"Location: {request.Location}",
                $"Priority: {request.Priority}",
                $"Stage: {request.Stage}",
                $"Experience: {request.Experience}",
                $"Positions: {request.FulfilledPositions}/{request.RequiredPositions}",
                $"Skills: {Join(request.Skills)}",
                $"Description: {request.Description}"),
            new { request.Id, request.Code });

        foreach (var employee in review.EligibleEmployees)
        {
            AddChunk(
                chunks,
                RagAssistantContextTypes.PmoRequest,
                request.Id,
                employee.EmployeeId,
                "BenchEmployee",
                employee.EmployeeId,
                employee.DisplayName,
                route,
                "bench.matches.view",
                "Internal",
                "BenchEmployeeProfile",
                ordinal++,
                Lines(
                    $"Bench employee: {employee.DisplayName}",
                    $"Email: {employee.Email}",
                    $"Designation: {employee.Designation ?? "Not recorded"}",
                    $"Department: {employee.Department}",
                    $"Location: {employee.Location}",
                    $"Experience: {FormatDecimal(employee.ExperienceYears)} years",
                    $"Availability: {employee.AvailabilityStatus}",
                    $"Bench status: {employee.BenchStatus}",
                    $"Skills: {Join(employee.Skills)}",
                    $"Matched skills: {Join(employee.MatchedSkills)}",
                    $"Missing skills: {Join(employee.MissingSkills)}",
                    $"Project evidence: {Join(employee.ProjectEvidence.Select(ProjectEvidenceText))}"),
                new { employee.EmployeeId });
        }

        foreach (var match in review.BenchMatches)
        {
            var employee = review.EligibleEmployees.FirstOrDefault(employee => employee.EmployeeId == match.EmployeeId);
            var employeeName = employee?.DisplayName ?? "Bench employee";
            var explanation = employee is null
                ? match.Explanation
                : BenchMatchExplanationGuard.Apply(employee, request, match.Explanation);
            AddChunk(
                chunks,
                RagAssistantContextTypes.PmoRequest,
                request.Id,
                match.EmployeeId,
                "BenchMatch",
                match.EmployeeId,
                $"{employeeName} match rationale",
                route,
                "bench.matches.view",
                "Internal",
                "BenchMatchLog",
                ordinal++,
                Lines(
                    $"Bench match for: {employeeName}",
                    $"Rank: {match.Rank}",
                    $"Score: {match.Score}",
                    $"Confidence: {match.Confidence}",
                    $"Explanation: {explanation}",
                    $"Strengths: {Join(match.Strengths)}",
                    $"Gaps: {Join(match.Gaps)}",
                    $"Project evidence: {Join(match.ProjectEvidence.Select(ProjectEvidenceText))}",
                    $"Web summary: {match.WebSummary}",
                    $"Generated at: {match.GeneratedAt:u}"),
                new { match.EmployeeId, match.AgentRunId });
        }

        foreach (var referral in review.Referrals)
        {
            AddChunk(
                chunks,
                RagAssistantContextTypes.PmoRequest,
                request.Id,
                referral.EmployeeId,
                "EmployeeReferral",
                referral.ReferralId,
                $"{referral.EmployeeName} referral",
                route,
                "bench.matches.view",
                "Internal",
                "EmployeeReferral",
                ordinal++,
                Lines(
                    $"Referral employee: {referral.EmployeeName}",
                    $"Status: {referral.Status}",
                    $"Presales: {referral.PresalesName ?? "Not assigned"}",
                    $"Summary: {referral.RecommendationSummary ?? "Not recorded"}",
                    $"Client feedback: {referral.ClientFeedback ?? "Not recorded"}",
                    $"Created: {referral.CreatedAt:u}"),
                new { referral.ReferralId });
        }

        return chunks;
    }

    private static IReadOnlyList<KnowledgeChunkDraft> BuildRecruiterCandidateFitChunks(
        OperationsRecruiterSourcing sourcing,
        Guid? focusEntityId)
    {
        var chunks = new List<KnowledgeChunkDraft>();
        var request = sourcing.JobRequest;
        var route = $"/app/recruitment/sourcing/{request.Id}";
        var ordinal = 0;
        var applications = focusEntityId.HasValue
            ? sourcing.Applications.Where(application => application.JobApplicationId == focusEntityId.Value).ToArray()
            : sourcing.Applications.ToArray();

        AddChunk(
            chunks,
            RagAssistantContextTypes.RecruiterCandidateFit,
            request.Id,
            focusEntityId,
            "JobRequest",
            request.Id,
            $"{request.Code} - {request.Title}",
            route,
            "candidates.manage",
            "CandidateRestricted",
            "JobRequest",
            ordinal++,
            Lines(
                $"Request: {request.Title} ({request.Code})",
                $"Client: {request.Client}",
                $"Department: {request.Department}",
                $"Location: {request.Location}",
                $"Experience: {request.Experience}",
                $"Skills: {Join(request.Skills)}",
                $"Description: {request.Description}"),
            new { request.Id, request.Code });

        if (sourcing.JobPost is not null)
        {
            AddChunk(
                chunks,
                RagAssistantContextTypes.RecruiterCandidateFit,
                request.Id,
                focusEntityId,
                "JobPost",
                sourcing.JobPost.JobPostId,
                sourcing.JobPost.Title,
                route,
                "candidates.manage",
                "CandidateRestricted",
                "JobPost",
                ordinal++,
                Lines(
                    $"Job post: {sourcing.JobPost.Title}",
                    $"Status: {sourcing.JobPost.Status}",
                    $"Experience: {FormatDecimal(sourcing.JobPost.ExperienceMinYears)}-{FormatDecimal(sourcing.JobPost.ExperienceMaxYears)} years",
                    $"Required positions: {sourcing.JobPost.RequiredPositions}",
                    $"Skills: {Join(sourcing.JobPost.Skills.Select(skill => skill.Name))}",
                    $"Description: {sourcing.JobPost.Description}"),
                new { sourcing.JobPost.JobPostId });
        }

        var relevanceAssessments = BuildApplicationRelevanceAssessments(
            request,
            sourcing.JobPost,
            applications,
            sourcing.ApplicantRankings);
        if (relevanceAssessments.Count > 0)
        {
            AddChunk(
                chunks,
                RagAssistantContextTypes.RecruiterCandidateFit,
                request.Id,
                focusEntityId,
                "ApplicationRelevanceSummary",
                request.Id,
                $"{request.Code} application relevance summary",
                route,
                "candidates.manage",
                "CandidateRestricted",
                "ApplicationRelevanceSummary",
                ordinal++,
                BuildApplicationRelevanceSummaryText(request, sourcing.JobPost, relevanceAssessments),
                new
                {
                    request.Id,
                    TotalApplications = relevanceAssessments.Count,
                    IrrelevantApplications = relevanceAssessments.Count(assessment => assessment.IsIrrelevant),
                    NeedsReviewApplications = relevanceAssessments.Count(assessment => assessment.Category == "Needs review"),
                    RelevantApplications = relevanceAssessments.Count(assessment => assessment.Category == "Relevant")
                });
        }

        foreach (var application in applications)
        {
            AddChunk(
                chunks,
                RagAssistantContextTypes.RecruiterCandidateFit,
                request.Id,
                application.JobApplicationId,
                "CandidateApplication",
                application.JobApplicationId,
                application.CandidateName,
                route,
                "candidates.manage",
                "CandidateRestricted",
                "CandidateProfile",
                ordinal++,
                Lines(
                    $"Candidate: {application.CandidateName}",
                    $"Email: {application.CandidateEmail}",
                    $"Status: {application.CandidateStatus}",
                    $"Current designation: {application.CurrentDesignation ?? "Not recorded"}",
                    $"Current company: {application.CurrentCompany ?? "Not recorded"}",
                    $"Experience: {FormatDecimal(application.ExperienceYears)} years",
                    $"Notice: {application.NoticePeriodDays?.ToString(CultureInfo.InvariantCulture) ?? "Not recorded"} days",
                    $"Application status: {application.ApplicationStatus}",
                    $"Source: {application.SourceLabel}",
                    $"Source detail: {application.SourceDetail ?? "Not recorded"}",
                    $"Cover letter: {application.CoverLetterText ?? "Not recorded"}",
                    $"Interview progress: {application.InterviewPassSummary}"),
                new { application.JobApplicationId, application.CandidateId });

            foreach (var document in application.Documents)
            {
                AddChunk(
                    chunks,
                    RagAssistantContextTypes.RecruiterCandidateFit,
                    request.Id,
                    application.JobApplicationId,
                    "ApplicationDocument",
                    document.ApplicationDocumentId,
                    document.DisplayName,
                    route,
                    "candidates.manage",
                    "CandidateRestricted",
                    "ApplicationDocument",
                    ordinal++,
                    Lines(
                        $"Document: {document.DisplayName}",
                        $"Type: {document.DocumentType}",
                        $"Extraction status: {document.ExtractionStatus}",
                        $"Has text evidence: {document.HasTextEvidence}",
                        $"Uploaded: {document.UploadedAt:u}"),
                    new { document.ApplicationDocumentId, application.JobApplicationId });
            }

            foreach (var interview in application.Interviews)
            {
                AddChunk(
                    chunks,
                    RagAssistantContextTypes.RecruiterCandidateFit,
                    request.Id,
                    application.JobApplicationId,
                    "InterviewFeedback",
                    interview.InterviewId,
                    $"{application.CandidateName} - {interview.RoundName}",
                    route,
                    "candidates.manage",
                    "CandidateRestricted",
                    "Interview",
                    ordinal++,
                    Lines(
                        $"Candidate: {application.CandidateName}",
                        $"Round: {interview.RoundName}",
                        $"Interviewer: {interview.InterviewerName}",
                        $"Status: {interview.Status}",
                        $"Recommendation: {interview.Recommendation ?? "Not recorded"}",
                        $"Schedule: {interview.StartsAt:u}",
                        $"Location: {interview.LocationText ?? "Not recorded"}"),
                    new { interview.InterviewId });
            }
        }

        var applicationNamesById = applications.ToDictionary(
            application => application.JobApplicationId,
            application => application.CandidateName);
        foreach (var ranking in sourcing.ApplicantRankings.Where(ranking => !focusEntityId.HasValue || ranking.JobApplicationId == focusEntityId.Value))
        {
            var candidateName = string.Equals(ranking.CandidateName, "Unknown applicant", StringComparison.OrdinalIgnoreCase)
                && applicationNamesById.TryGetValue(ranking.JobApplicationId, out var applicationCandidateName)
                    ? applicationCandidateName
                    : ranking.CandidateName;
            AddChunk(
                chunks,
                RagAssistantContextTypes.RecruiterCandidateFit,
                request.Id,
                ranking.JobApplicationId,
                "ApplicantRanking",
                ranking.JobApplicationId,
                $"{candidateName} ranking rationale",
                route,
                "candidates.manage",
                "CandidateRestricted",
                "ApplicantRankingLog",
                ordinal++,
                Lines(
                    $"Candidate: {candidateName}",
                    $"Rank: {ranking.Rank}",
                    $"Score: {ranking.Score}",
                    $"Confidence: {ranking.Confidence}",
                    $"Explanation: {ranking.Explanation}",
                    $"Strengths: {Join(ranking.Strengths)}",
                    $"Gaps: {Join(ranking.Gaps)}",
                    $"Matched skills: {Join(ranking.MatchedSkills)}",
                    $"Missing skills: {Join(ranking.MissingSkills)}",
                    $"Document evidence: {Join(ranking.DocumentEvidence)}",
                    $"Historical evidence: {Join(ranking.HistoricalOutcomeEvidence)}",
                    $"Semantic similarity: {ranking.SemanticSimilarityStatus}"),
                new { ranking.JobApplicationId, ranking.AgentRunId });
        }

        return chunks;
    }

    private static IReadOnlyList<RecruiterApplicationRelevanceAssessment> BuildApplicationRelevanceAssessments(
        OperationsJobRequest request,
        OperationsJobPost? jobPost,
        IReadOnlyList<OperationsRecruiterApplication> applications,
        IReadOnlyList<OperationsApplicantRankingMatch> rankings)
    {
        var rankingsByApplicationId = rankings
            .GroupBy(ranking => ranking.JobApplicationId)
            .ToDictionary(group => group.Key, group => group.OrderBy(ranking => ranking.Rank).First());
        var roleText = $"{request.Title} {jobPost?.Title} {Join(request.Skills)} {Join(jobPost?.Skills.Select(skill => skill.Name) ?? Array.Empty<string>())}";

        return applications
            .Select(application =>
            {
                rankingsByApplicationId.TryGetValue(application.JobApplicationId, out var ranking);
                var matchedSkills = ranking?.MatchedSkills
                    .Where(skill => !string.IsNullOrWhiteSpace(skill))
                    .Select(skill => skill.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? Array.Empty<string>();
                var missingSkills = ranking?.MissingSkills
                    .Where(skill => !string.IsNullOrWhiteSpace(skill))
                    .Select(skill => skill.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? Array.Empty<string>();
                var score = ranking?.Score;
                var roleMismatch = !HasRoleSpecializationOverlap(roleText, $"{application.CurrentDesignation} {ranking?.CurrentDesignation}");
                var highFitRoleEvidence = score.HasValue && score.Value >= 70m && !roleMismatch;
                var noMatchingSkills = ranking is not null && matchedSkills.Length == 0 && !highFitRoleEvidence;
                var veryLowScore = score.HasValue && score.Value < 45m;
                var lowScore = score.HasValue && score.Value < 60m;
                var lowScoreWithWeakEvidence = lowScore && (matchedSkills.Length <= 1 || roleMismatch);
                var isIrrelevant = noMatchingSkills || veryLowScore || lowScoreWithWeakEvidence;
                var category = isIrrelevant
                    ? "Irrelevant"
                    : score.HasValue && score.Value < 70m
                        ? "Needs review"
                        : "Relevant";
                var reason = isIrrelevant
                    ? BuildIrrelevanceReason(noMatchingSkills, roleMismatch, score, matchedSkills.Length)
                    : category == "Needs review"
                        ? "Some evidence overlaps with the role, but score or skill coverage is not strong enough to treat as a clear fit."
                        : "Candidate has sufficient role evidence, score, or matched skills to remain relevant for review.";

                return new RecruiterApplicationRelevanceAssessment(
                    application.JobApplicationId,
                    application.CandidateName,
                    application.CurrentDesignation ?? ranking?.CurrentDesignation ?? "Not recorded",
                    ranking?.Rank,
                    score,
                    matchedSkills,
                    missingSkills,
                    category,
                    isIrrelevant,
                    reason);
            })
            .ToArray();
    }

    private static string BuildApplicationRelevanceSummaryText(
        OperationsJobRequest request,
        OperationsJobPost? jobPost,
        IReadOnlyList<RecruiterApplicationRelevanceAssessment> assessments)
    {
        var irrelevantCount = assessments.Count(assessment => assessment.IsIrrelevant);
        var needsReviewCount = assessments.Count(assessment => assessment.Category == "Needs review");
        var relevantCount = assessments.Count(assessment => assessment.Category == "Relevant");
        var builder = new StringBuilder();
        builder.AppendLine("Application relevance summary for irrelevant applications, irrelevant candidates, poor matches, no matching skills, profile mismatch, dissimilar profile, not aligned with role.");
        builder.AppendLine("Irrelevant candidate definition: Count a candidate as irrelevant when they have no matching required or core skills, or their profile specialization and day-to-day work are dissimilar to the role requirement. Related broad industry experience is not enough when core role skills and responsibilities are missing.");
        builder.AppendLine("Borderline candidate definition: Do not count a candidate as irrelevant when there is meaningful overlap but the evidence still needs recruiter review.");
        builder.AppendLine($"Job requirement: {request.Title} ({request.Code})");
        builder.AppendLine($"Job post: {jobPost?.Title ?? "Not published"}");
        builder.AppendLine($"Required skills: {Join(request.Skills.Concat(jobPost?.Skills.Select(skill => skill.Name) ?? Array.Empty<string>()))}");
        builder.AppendLine($"Total applications: {assessments.Count}");
        builder.AppendLine($"Irrelevant applications: {irrelevantCount}");
        builder.AppendLine($"Needs review applications: {needsReviewCount}");
        builder.AppendLine($"Relevant applications: {relevantCount}");
        foreach (var assessment in assessments.OrderBy(assessment => assessment.Rank ?? int.MaxValue).ThenBy(assessment => assessment.CandidateName))
        {
            builder.AppendLine(
                $"Candidate: {assessment.CandidateName} | Profile: {assessment.ProfileTitle} | Rank: {assessment.Rank?.ToString(CultureInfo.InvariantCulture) ?? "Not ranked"} | Score: {FormatDecimal(assessment.Score)} | Matched skills: {Join(assessment.MatchedSkills)} | Missing skills: {Join(assessment.MissingSkills)} | Relevance category: {assessment.Category} | Irrelevant: {(assessment.IsIrrelevant ? "Yes" : "No")} | Reason: {assessment.Reason}");
        }

        return builder.ToString();
    }

    private static string BuildIrrelevanceReason(bool noMatchingSkills, bool roleMismatch, decimal? score, int matchedSkillCount)
    {
        var reasons = new List<string>();
        if (noMatchingSkills)
        {
            reasons.Add("no matching required/core skills were found");
        }

        if (roleMismatch)
        {
            reasons.Add("profile specialization does not align with the role");
        }

        if (score.HasValue && score.Value < 60m)
        {
            reasons.Add($"low applicant fit score ({FormatDecimal(score)}%)");
        }

        if (matchedSkillCount <= 1)
        {
            reasons.Add("weak matched-skill coverage");
        }

        return reasons.Count == 0 ? "available evidence marks the profile as a poor match" : string.Join("; ", reasons);
    }

    private static bool HasRoleSpecializationOverlap(string roleText, string candidateText)
    {
        var roleTerms = TokenizeRoleText(roleText);
        var candidateTerms = TokenizeRoleText(candidateText);
        return roleTerms.Count == 0 || candidateTerms.Overlaps(roleTerms);
    }

    private static HashSet<string> TokenizeRoleText(string? text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a",
            "an",
            "and",
            "architect",
            "associate",
            "consultant",
            "developer",
            "engineer",
            "expert",
            "for",
            "full",
            "lead",
            "manager",
            "mid",
            "of",
            "principal",
            "role",
            "senior",
            "software",
            "specialist",
            "stack",
            "the"
        };
        return (text ?? string.Empty)
            .Split([' ', ',', '.', '/', '\\', '-', '_', '(', ')', '[', ']', ':', ';', '|', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 2 && !stopWords.Contains(term))
            .Select(term => term.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record RecruiterApplicationRelevanceAssessment(
        Guid JobApplicationId,
        string CandidateName,
        string ProfileTitle,
        int? Rank,
        decimal? Score,
        IReadOnlyList<string> MatchedSkills,
        IReadOnlyList<string> MissingSkills,
        string Category,
        bool IsIrrelevant,
        string Reason);

    private static IReadOnlyList<KnowledgeChunkDraft> BuildHiringDecisionBriefChunks(
        HiringReviewDetail detail,
        Guid jobApplicationId)
    {
        var chunks = new List<KnowledgeChunkDraft>();
        var route = $"/app/hiring-manager/reviews/{jobApplicationId}";
        var ordinal = 0;

        AddChunk(
            chunks,
            RagAssistantContextTypes.HiringDecisionBrief,
            jobApplicationId,
            jobApplicationId,
            "HiringDecisionBrief",
            jobApplicationId,
            $"{detail.Candidate.DisplayName} decision brief",
            route,
            "hiring.decisions.manage",
            "CandidateRestricted",
            "DecisionBrief",
            ordinal++,
            Lines(
                $"Candidate: {detail.Candidate.DisplayName}",
                $"Email: {detail.Candidate.Email}",
                $"Status: {detail.Candidate.Status}",
                $"Designation: {detail.Candidate.CurrentDesignation ?? "Not recorded"}",
                $"Company: {detail.Candidate.CurrentCompany ?? "Not recorded"}",
                $"Experience: {FormatDecimal(detail.Candidate.ExperienceYears)} years",
                $"Notice: {detail.Candidate.NoticePeriodDays?.ToString(CultureInfo.InvariantCulture) ?? "Not recorded"} days",
                $"Job: {detail.Job.JobTitle} ({detail.Job.RequestCode})",
                $"Client: {detail.Job.Client}",
                $"Department: {detail.Job.Department}",
                $"Location: {detail.Job.Location}",
                $"Application status: {detail.Job.ApplicationStatus}",
                $"Recruiter notes: {detail.Job.RecruiterNotes ?? "Not recorded"}",
                $"Decision brief: {detail.DecisionBrief}",
                $"Decision insight: {detail.DecisionBriefInsight.Summary}",
                $"Signals: {Join(detail.DecisionBriefInsight.Signals)}"),
            new { jobApplicationId, detail.Candidate.CandidateId, detail.Job.JobRequestId });

        AddChunk(
            chunks,
            RagAssistantContextTypes.HiringDecisionBrief,
            jobApplicationId,
            jobApplicationId,
            "JobRequest",
            detail.Job.JobRequestId,
            $"{detail.Job.RequestCode} - {detail.Job.JobTitle}",
            route,
            "hiring.decisions.manage",
            "CandidateRestricted",
            "JobEvidence",
            ordinal++,
            Lines(
                $"Request: {detail.Job.JobTitle} ({detail.Job.RequestCode})",
                $"Client: {detail.Job.Client}",
                $"Department: {detail.Job.Department}",
                $"Location: {detail.Job.Location}",
                $"Required positions: {detail.Job.RequiredPositions}",
                $"Fulfilled positions: {detail.Job.FulfilledPositions}",
                $"Request status: {detail.Job.RequestStatus}",
                $"Source: {detail.Job.SourceLabel}",
                $"Source detail: {detail.Job.SourceDetail ?? "Not recorded"}",
                $"Request description: {detail.Job.RequestDescription ?? "Not recorded"}",
                $"Job post description: {detail.Job.JobPostDescription ?? "Not recorded"}"),
            new { detail.Job.JobRequestId, detail.Job.JobPostId });

        foreach (var interview in detail.Interviews)
        {
            AddChunk(
                chunks,
                RagAssistantContextTypes.HiringDecisionBrief,
                jobApplicationId,
                jobApplicationId,
                "InterviewFeedback",
                interview.InterviewId,
                $"{detail.Candidate.DisplayName} - {interview.RoundName}",
                route,
                "hiring.decisions.manage",
                "CandidateRestricted",
                "InterviewFeedback",
                ordinal++,
                Lines(
                    $"Round: {interview.RoundName}",
                    $"Status: {interview.Status}",
                    $"Interviewer: {interview.InterviewerName}",
                    $"Recommendation: {interview.Recommendation ?? "Not recorded"}",
                    $"Technical score: {FormatScoreOutOfFive(interview.TechnicalScore)}",
                    $"Communication score: {FormatScoreOutOfFive(interview.CommunicationScore)}",
                    $"Culture score: {FormatScoreOutOfFive(interview.CultureScore)}",
                    $"Average score: {FormatScoreOutOfFive(interview.AverageScore)}",
                    $"Feedback: {interview.FeedbackText ?? interview.SkipReason ?? "Not recorded"}",
                    $"Submitted: {interview.SubmittedAt?.ToString("u", CultureInfo.InvariantCulture) ?? "Not recorded"}"),
                new { interview.InterviewId });
        }

        return chunks;
    }

    private static void AddChunk(
        List<KnowledgeChunkDraft> chunks,
        string contextType,
        Guid contextEntityId,
        Guid? focusEntityId,
        string sourceEntityType,
        Guid sourceEntityId,
        string sourceTitle,
        string? sourceRoute,
        string permissionScope,
        string sensitivity,
        string chunkType,
        int chunkOrdinal,
        string text,
        object metadata)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var normalizedText = text.Trim();
        chunks.Add(new KnowledgeChunkDraft(
            contextType,
            contextEntityId,
            focusEntityId,
            sourceEntityType,
            sourceEntityId,
            sourceTitle,
            sourceRoute,
            permissionScope,
            sensitivity,
            chunkType,
            chunkOrdinal,
            normalizedText,
            JsonSerializer.Serialize(metadata),
            Sha256($"{contextType}|{contextEntityId}|{focusEntityId}|{sourceEntityType}|{sourceEntityId}|{chunkType}|{chunkOrdinal}|{normalizedText}")));
    }

    private static string Lines(params string?[] lines)
    {
        return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string Join(IEnumerable<string?> values)
    {
        var materialized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return materialized.Length == 0 ? "Not recorded" : string.Join(", ", materialized);
    }

    private static string ProjectEvidenceText(OperationsEmployeeProjectEvidence evidence)
    {
        return $"{evidence.ProjectName} ({evidence.ClientName ?? "no client"}, {evidence.Status}, {evidence.AllocationPercent}% allocation)";
    }

    private static string FormatDecimal(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "Not recorded";
    }

    private static string FormatScoreOutOfFive(int? value)
    {
        return value.HasValue ? $"{value.Value.ToString(CultureInfo.InvariantCulture)}/5" : "Not recorded";
    }

    private static string FormatScoreOutOfFive(decimal? value)
    {
        return value.HasValue ? $"{FormatDecimal(value)}/5" : "Not recorded";
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
