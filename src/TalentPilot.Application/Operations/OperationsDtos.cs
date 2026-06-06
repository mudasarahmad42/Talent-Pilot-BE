namespace TalentPilot.Application.Operations;

public sealed record OperationsPeopleResponse(IReadOnlyList<OperationsPerson> Items);

public sealed record OperationsPerson(
    Guid UserId,
    string DisplayName,
    string Email,
    IReadOnlyList<string> RoleCodes,
    IReadOnlyList<string> RoleNames);

public sealed record OperationsSnapshot(
    IReadOnlyList<OperationsPerson> People,
    IReadOnlyList<OperationsJobRequest> JobRequests,
    IReadOnlyList<OperationsWorkflowAssignment> Assignments,
    IReadOnlyList<OperationsNotification> Notifications);

public sealed record TenantAdminDashboardQuery(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    Guid? DepartmentId,
    string? SourceLabel,
    Guid? RecruiterUserId);

public sealed record TenantAdminDashboard(
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    TenantAdminDashboardFilterOptions Filters,
    TenantAdminDashboardSummary Summary,
    IReadOnlyList<TenantAdminDashboardFunnelItem> HiringFunnel,
    IReadOnlyList<TenantAdminDashboardAttentionItem> AdminAttention,
    TenantAdminDashboardOfferHealth OfferHealth,
    IReadOnlyList<TenantAdminDashboardPipelineItem> CandidatePipeline,
    TenantAdminDashboardEfficiency OperationalEfficiency,
    IReadOnlyList<TenantAdminDashboardStageAgingItem> StageAging,
    IReadOnlyList<TenantAdminDashboardDepartmentPerformanceItem> DepartmentPerformance,
    IReadOnlyList<TenantAdminDashboardSkillDemandItem> SkillsDemand,
    IReadOnlyList<TenantAdminDashboardSourceQualityItem> SourceQuality,
    TenantAdminDashboardInterviewOperations InterviewOperations,
    TenantAdminDashboardAiHealth AiHealth);

public sealed record TenantAdminDashboardFilterOptions(
    IReadOnlyList<OperationsLookupOption> Departments,
    IReadOnlyList<OperationsLookupOption> SourceLabels,
    IReadOnlyList<OperationsLookupOption> Recruiters);

public sealed record TenantAdminDashboardSummary(
    int OpenJobRequests,
    int OpenPositions,
    int RequiredPositions,
    int FulfilledPositions,
    int PublishedJobPosts,
    int ActiveApplications,
    int InterviewsThisWeek,
    int Offers,
    int JoinedCandidates);

public sealed record TenantAdminDashboardFunnelItem(
    string Label,
    int Count,
    decimal? ConversionRate);

public sealed record TenantAdminDashboardAttentionItem(
    string Severity,
    string Title,
    string Detail,
    int Count,
    string Route);

public sealed record TenantAdminDashboardOfferHealth(
    int OfferLetters,
    int PresentationMeetings,
    int Offered,
    int OnHold,
    int Rejected,
    int Joined,
    int OpenPositionsRemaining);

public sealed record TenantAdminDashboardPipelineItem(
    string Status,
    int Count);

public sealed record TenantAdminDashboardEfficiency(
    decimal? AverageTimeToFillDays,
    decimal? MedianDaysOpen,
    int OldestOpenRequestDays,
    int PmoQueueLoad,
    int RecruiterSourcingLoad,
    int InterviewerLoad,
    int HiringManagerPendingReviews);

public sealed record TenantAdminDashboardStageAgingItem(
    Guid JobRequestId,
    string RequestCode,
    string Title,
    string Department,
    string CurrentStage,
    string OwnerName,
    int DaysInStage,
    string Risk);

public sealed record TenantAdminDashboardDepartmentPerformanceItem(
    string Department,
    int OpenRequests,
    int OpenPositions,
    int Applications,
    int Interviews,
    int Joined,
    decimal? AverageTimeToFillDays);

public sealed record TenantAdminDashboardSkillDemandItem(
    string Skill,
    int DemandCount,
    int CandidateCount,
    int Gap);

public sealed record TenantAdminDashboardSourceQualityItem(
    string SourceLabel,
    int Applications,
    decimal InterviewPassRate,
    int Offers,
    int Joined,
    decimal RejectionWithdrawalRate);

public sealed record TenantAdminDashboardInterviewOperations(
    int Scheduled,
    int Completed,
    int Skipped,
    int NoShow,
    int PendingFeedback,
    int OverdueFeedback);

public sealed record TenantAdminDashboardAiHealth(
    int RunsToday,
    int FailedRuns,
    DateTimeOffset? LatestBenchMatchingAt,
    DateTimeOffset? LatestTalentRediscoveryAt,
    int ActiveEmbeddings,
    int CandidateEmbeddings,
    int JobRequestEmbeddings,
    int JobPostEmbeddings,
    int EmployeeEmbeddings);

public sealed record PmoDashboardQuery(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    Guid? DepartmentId);

public sealed record PmoDashboard(
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    PmoDashboardFilterOptions Filters,
    PmoDashboardSummary Summary,
    IReadOnlyList<PmoDashboardWorkItem> WorkQueue,
    IReadOnlyList<PmoDashboardBenchInsight> BenchInsights,
    PmoDashboardRecommendationOutcomes RecommendationOutcomes,
    IReadOnlyList<PmoDashboardAgingBucket> AgingBuckets,
    IReadOnlyList<PmoDashboardDepartmentLoad> DepartmentLoad,
    IReadOnlyList<PmoDashboardDecisionSplit> DecisionSplit,
    IReadOnlyList<PmoDashboardRecommendationTrendItem> RecommendationTrend,
    IReadOnlyList<PmoDashboardSkillBenchItem> SkillDemandVsBench,
    PmoDashboardAiHealth AiHealth);

public sealed record PmoDashboardFilterOptions(
    IReadOnlyList<OperationsLookupOption> Departments);

public sealed record PmoDashboardSummary(
    int UnclaimedReviews,
    int MyClaimedReviews,
    int ReturnedFromPresales,
    int AiRankedRequests,
    int RecommendedToPresales,
    int ForwardedToRecruiters);

public sealed record PmoDashboardWorkItem(
    Guid JobRequestId,
    string RequestCode,
    string Title,
    string Client,
    string Department,
    string Location,
    string Priority,
    Guid AssignmentId,
    string AssignmentStatus,
    string OwnerState,
    string? ClaimedByName,
    DateTimeOffset AssignedAtUtc,
    int DaysWaiting,
    string LatestAction,
    bool HasBenchMatches,
    decimal? TopFitScore,
    int EligibleEmployeeCount,
    int PendingReferralCount,
    string Cta);

public sealed record PmoDashboardBenchInsight(
    Guid JobRequestId,
    string RequestCode,
    string Title,
    DateTimeOffset? LatestRankedAt,
    decimal? TopFitScore,
    string? TopEmployeeName,
    int EligibleEmployeeCount,
    int LocationFitCount,
    int AverageMatchedSkills,
    int OpenSkillGaps,
    string AiStatus);

public sealed record PmoDashboardRecommendationOutcomes(
    int PendingPresalesReview,
    int AcceptedByPresales,
    int RejectedByPresales,
    int FulfilledInternally,
    decimal PresalesResponseRate);

public sealed record PmoDashboardAgingBucket(string Label, int Count);

public sealed record PmoDashboardDepartmentLoad(
    string Department,
    int PendingReviews,
    int ClaimedReviews,
    decimal AverageAgeDays);

public sealed record PmoDashboardDecisionSplit(string Decision, int Count);

public sealed record PmoDashboardRecommendationTrendItem(
    DateTimeOffset PeriodStartUtc,
    int Recommended,
    int Accepted,
    int Rejected);

public sealed record PmoDashboardSkillBenchItem(
    string Skill,
    int DemandCount,
    int BenchAvailableCount,
    int Gap);

public sealed record PmoDashboardAiHealth(
    int RunsInWindow,
    int FailedRuns,
    DateTimeOffset? LatestRunAt,
    int RankedRequests,
    int EmployeeEmbeddings);

public sealed record HiringManagerDashboard(
    DateTimeOffset GeneratedAtUtc,
    HiringManagerDashboardSummary Summary,
    IReadOnlyList<HiringManagerDashboardReviewItem> PriorityReviews,
    IReadOnlyList<HiringManagerDashboardStatusBreakdownItem> OfferPipeline,
    IReadOnlyList<HiringManagerDashboardAgingBucket> AgingBuckets,
    IReadOnlyList<HiringManagerDashboardStatusBreakdownItem> OutcomeSplit,
    IReadOnlyList<HiringManagerDashboardActivityItem> RecentActivity);

public sealed record HiringManagerDashboardSummary(
    int PendingReviews,
    int OfferFollowUps,
    int OnHold,
    int CompletedOutcomes,
    int OldestWaitingDays);

public sealed record HiringManagerDashboardReviewItem(
    Guid JobApplicationId,
    Guid JobRequestId,
    Guid? JobPostId,
    string RequestCode,
    string JobTitle,
    string Client,
    string Department,
    string CandidateName,
    string CandidateEmail,
    string Status,
    string HiringManagerName,
    DateTimeOffset UpdatedAt,
    int DaysWaiting,
    int CompletedInterviews,
    decimal? AverageScore,
    int PositiveRecommendations,
    string? OfferLetterStatus,
    DateTimeOffset? LatestMeetingAt);

public sealed record HiringManagerDashboardStatusBreakdownItem(string Status, int Count);

public sealed record HiringManagerDashboardAgingBucket(string Label, int Count);

public sealed record HiringManagerDashboardActivityItem(
    Guid Id,
    Guid JobApplicationId,
    Guid JobRequestId,
    string RequestCode,
    string CandidateName,
    string ActorName,
    string Title,
    string Detail,
    DateTimeOffset CreatedAt);

public sealed record OperationsPmoReview(
    OperationsJobRequest JobRequest,
    OperationsWorkflowAssignment? Assignment,
    IReadOnlyList<OperationsEmployeeReferral> Referrals,
    IReadOnlyList<OperationsBenchEmployee> EligibleEmployees,
    IReadOnlyList<OperationsBenchMatch> BenchMatches,
    IReadOnlyList<OperationsLookupOption> PresalesUsers,
    Guid? DefaultPresalesUserId,
    string RecruiterHandoffTargetName);

public sealed record OperationsRecruitmentQueue(
    IReadOnlyList<OperationsRecruitmentQueueItem> Items);

public sealed record OperationsRecruitmentQueueItem(
    OperationsJobRequest JobRequest,
    OperationsWorkflowAssignment Assignment,
    Guid? JobPostId,
    string JobPostStatus,
    string? RecruiterOwnerName,
    DateTimeOffset? JobPostUpdatedAt);

public sealed record OperationsRecruiterSourcing(
    OperationsJobRequest JobRequest,
    OperationsWorkflowAssignment? Assignment,
    OperationsJobPost? JobPost,
    IReadOnlyList<OperationsRecruiterApplication> Applications,
    IReadOnlyList<OperationsManualCandidateSearchItem> CandidateSearchItems,
    IReadOnlyList<OperationsTalentRediscoveryMatch> TalentRediscoveryMatches,
    IReadOnlyList<OperationsApplicantRankingMatch> ApplicantRankings,
    IReadOnlyList<OperationsInterviewTemplateOption> InterviewTemplates,
    IReadOnlyList<OperationsInterviewerOption> Interviewers,
    IReadOnlyList<OperationsLookupOption> HodInterviewers,
    IReadOnlyList<OperationsLookupOption> Skills,
    OperationsOnlineHeadhuntingResult? OnlineHeadhunting,
    string? ConfiguredAiModel = null);

public sealed record OperationsRecruiterApplication(
    Guid JobApplicationId,
    Guid CandidateId,
    string CandidateName,
    string CandidateEmail,
    string CandidateStatus,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    int? NoticePeriodDays,
    string ApplicationStatus,
    string SourceLabel,
    string? SourceDetail,
    string? SourceUrl,
    string? CoverLetterText,
    bool IsInvited,
    DateTimeOffset AppliedAt,
    int InterviewsPassed,
    int InterviewsTotal,
    string InterviewPassSummary,
    IReadOnlyList<OperationsRecruiterApplicationDocument> Documents,
    IReadOnlyList<OperationsRecruiterApplicationInterview> Interviews);

public sealed record OperationsRecruiterApplicationDocument(
    Guid ApplicationDocumentId,
    Guid JobApplicationId,
    string DocumentType,
    string DisplayName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    string ExtractionStatus,
    bool HasTextEvidence);

public sealed record OperationsRecruiterApplicationInterview(
    Guid InterviewId,
    Guid? JobPostInterviewRoundId,
    string RoundName,
    string InterviewerName,
    Guid InterviewerUserId,
    string InterviewerAccountStatus,
    bool InterviewerIsDeleted,
    string Status,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? MeetingLink,
    string? LocationText,
    string? Recommendation);

public sealed record OperationsJobPublishing(
    IReadOnlyList<OperationsJobPostListItem> Items);

public sealed record PortalJobPostList(
    IReadOnlyList<PortalJobPostListItem> Items);

public sealed record PublicPortalContextQuery(
    string? TenantSlug,
    Guid? JobPostId);

public sealed record PublicPortalContext(
    Guid TenantId,
    string Slug,
    string DisplayName,
    string CareerDisplayName,
    string? CompanyAddress,
    string? CompanyCity,
    string? CompanyCountry,
    string? OfficialEmail,
    string? OfficialPhone,
    string PrimaryColor,
    bool CandidateLoginRequired,
    string CandidateCvFormat,
    bool PublicJobsEnabled,
    int InviteExpiryDays,
    int ReapplyCooldownDays,
    string? LogoFileName,
    string? LogoContentType,
    string? LogoContentBase64);

public sealed record PortalJobPostListItem(
    Guid JobPostId,
    Guid JobRequestId,
    string RequestCode,
    string Title,
    string CompanyName,
    string Client,
    string Department,
    string Location,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    int RequiredPositions,
    string Status,
    DateTimeOffset PublishedAt,
    IReadOnlyList<OperationsJobPostSkill> Skills);

public sealed record PortalJobPostDetail(
    Guid JobPostId,
    Guid JobRequestId,
    string RequestCode,
    string Title,
    string Description,
    string CompanyName,
    string Client,
    string Department,
    string Location,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    int RequiredPositions,
    string Status,
    DateTimeOffset PublishedAt,
    IReadOnlyList<OperationsJobPostSkill> Skills);

public sealed record PortalInvitationContext(
    Guid CandidateInvitationId,
    Guid JobPostId,
    string JobTitle,
    string CompanyName,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? UsedAtUtc,
    bool IsExpired,
    bool IsRevoked);

public sealed record OperationsJobPostListItem(
    Guid JobPostId,
    Guid JobRequestId,
    string RequestCode,
    string Title,
    string Client,
    string Department,
    string Location,
    string Status,
    int ApplicantCount,
    string RecruiterOwnerName,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset UpdatedAt);

public sealed record OperationsJobPost(
    Guid JobPostId,
    Guid JobRequestId,
    string Title,
    string Description,
    string Department,
    string Location,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    int RequiredPositions,
    string Status,
    Guid RecruiterOwnerUserId,
    string RecruiterOwnerName,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OperationsJobPostSkill> Skills,
    IReadOnlyList<OperationsJobPostInterviewRound> InterviewRounds);

public sealed record OperationsJobPostSkill(
    Guid SkillId,
    string Name,
    string? Category);

public sealed record OperationsJobPostInterviewRound(
    Guid? JobPostInterviewRoundId,
    Guid? InterviewTemplateRoundId,
    int RoundOrder,
    string Name,
    Guid? OwnerUserId,
    string? OwnerUserName,
    int DurationMinutes,
    string Status);

public sealed record OperationsInterviewTemplateOption(
    Guid InterviewTemplateId,
    string Name,
    string DepartmentName,
    string Description,
    IReadOnlyList<OperationsJobPostInterviewRound> Rounds);

public sealed record OperationsInterviewerOption(
    Guid UserId,
    string DisplayName,
    string Email,
    Guid? DepartmentId,
    string? DepartmentName,
    string? Designation,
    IReadOnlyList<string> RoleNames,
    int CompletedInterviewCount,
    bool IsJobDepartmentMatch,
    bool IsDepartmentHod);

public sealed record OperationsJobRequestIntakeOptions(
    IReadOnlyList<OperationsIntakeDepartmentOption> Departments,
    IReadOnlyList<OperationsLookupOption> Locations,
    IReadOnlyList<OperationsLookupOption> Skills,
    IReadOnlyList<OperationsLookupOption> HiringManagers);

public sealed record OperationsIntakeDepartmentOption(
    Guid DepartmentId,
    string Code,
    string Name,
    OperationsRoutingPreview RoutingPreview);

public sealed record OperationsLookupOption(
    Guid Id,
    string Name,
    string? Description);

public sealed record OperationsRoutingPreview(
    string AssignmentType,
    Guid? TargetUserId,
    Guid? TargetGroupId,
    string TargetName,
    bool UsesTenantAdminFallback);

public sealed record OperationsJobRequest(
    Guid Id,
    string Code,
    string Title,
    string Client,
    string? ClientContext,
    string Description,
    string Department,
    IReadOnlyList<string> Skills,
    string Experience,
    string Location,
    int RequiredPositions,
    int FulfilledPositions,
    string Priority,
    Guid HiringManagerId,
    Guid CreatedById,
    string Stage,
    Guid? OwnerId,
    string? OwnerGroupId,
    string PublishStatus,
    DateTimeOffset CreatedAt);

public sealed record OperationsWorkflowAssignment(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Stage,
    string? AssignedToGroupId,
    Guid? AssignedToUserId,
    Guid? ClaimedByUserId,
    string Status,
    DateTimeOffset AssignedAt);

public sealed record OperationsNotification(
    Guid Id,
    Guid RecipientUserId,
    string Title,
    string Message,
    string Category,
    string Severity,
    string EntityType,
    Guid EntityId,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record OperationsActivityEvent(
    Guid Id,
    Guid EntityId,
    string ActorName,
    string Title,
    string Detail,
    DateTimeOffset CreatedAt);

public sealed record OperationsBenchEmployee(
    Guid EmployeeId,
    string DisplayName,
    string Email,
    string? Designation,
    string Department,
    string Location,
    decimal? ExperienceYears,
    DateOnly? JoiningDate,
    string AvailabilityStatus,
    string BenchStatus,
    bool IsCurrentlyBenched,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<OperationsEmployeeProjectEvidence> ProjectEvidence);

public sealed record OperationsEmployeeProjectEvidence(
    string ProjectName,
    string? ClientName,
    string Status,
    int AllocationPercent,
    DateOnly? StartsOn,
    DateOnly? EndsOn);

public sealed record OperationsBenchMatch(
    Guid EmployeeId,
    int Rank,
    decimal Score,
    string Confidence,
    string Explanation,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<OperationsEmployeeProjectEvidence> ProjectEvidence,
    string WebResearchStatus,
    string WebSummary,
    IReadOnlyList<OperationsBenchMatchWebSource> WebSources,
    Guid? AgentRunId,
    DateTimeOffset GeneratedAt);

public sealed record OperationsBenchMatchWebSource(
    string Query,
    string Title,
    string Url,
    string Snippet);

public sealed record OperationsBenchMatchingContext(
    OperationsJobRequest JobRequest,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    IReadOnlyList<OperationsBenchEmployee> EligibleEmployees);

public sealed record RankBenchMatchesResult(
    IReadOnlyList<OperationsBenchMatch> BenchMatches,
    Guid AgentRunId,
    string Model,
    DateTimeOffset GeneratedAtUtc,
    string WebResearchStatus);

public sealed record OperationsCandidateApplicationEvidence(
    Guid JobApplicationId,
    Guid JobRequestId,
    string RequestCode,
    string JobTitle,
    string Client,
    string Department,
    string Location,
    string Status,
    string SourceLabel,
    DateTimeOffset AppliedAt,
    DateTimeOffset? FinalDecisionAt,
    string? FinalDecisionReason,
    Guid? JobPostId = null,
    string? JobPostTitle = null,
    string? JobPostStatus = null,
    string? DisplayJobTitle = null,
    int InterviewsPassed = 0,
    int InterviewsTotal = 0,
    string? InterviewPassSummary = null,
    string? CoverLetterText = null,
    IReadOnlyList<OperationsApplicantDocumentEvidence>? DocumentEvidence = null);

public sealed record OperationsCandidateInterviewEvidence(
    Guid InterviewId,
    Guid JobApplicationId,
    string RoundName,
    string Status,
    string? Recommendation,
    int? TechnicalScore,
    int? CommunicationScore,
    int? CultureScore,
    string? FeedbackSummary,
    DateTimeOffset? SubmittedAt);

public sealed record OperationsRediscoveryCandidate(
    Guid CandidateId,
    string DisplayName,
    string Email,
    string Status,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    int? NoticePeriodDays,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<OperationsCandidateApplicationEvidence> ApplicationEvidence,
    IReadOnlyList<OperationsCandidateInterviewEvidence> InterviewEvidence);

public sealed record OperationsManualCandidateSearchItem(
    Guid CandidateId,
    string DisplayName,
    string Email,
    string Status,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    int? NoticePeriodDays,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    int ApplicationCount,
    int PassedInterviews,
    int FailedInterviews,
    int TotalInterviews,
    OperationsCandidateApplicationEvidence? LatestApplication);

public sealed record OperationsTalentRediscoveryMatch(
    Guid CandidateId,
    string CandidateName,
    string CandidateEmail,
    string? CurrentDesignation,
    decimal? ExperienceYears,
    int? NoticePeriodDays,
    int Rank,
    decimal Score,
    string Confidence,
    string Explanation,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<OperationsCandidateApplicationEvidence> ApplicationEvidence,
    IReadOnlyList<OperationsCandidateInterviewEvidence> InterviewEvidence,
    Guid? AgentRunId,
    DateTimeOffset GeneratedAt);

public sealed record OperationsHistoricalCandidateSummary(
    Guid CandidateId,
    string DisplayName,
    string Email,
    string Status,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    int? NoticePeriodDays);

public sealed record OperationsHistoricalApplicationSummary(
    Guid JobApplicationId,
    Guid JobRequestId,
    string RequestCode,
    Guid? JobPostId,
    string? JobPostTitle,
    string? JobPostStatus,
    string DisplayJobTitle,
    string Client,
    string Department,
    string Location,
    string Status,
    string SourceLabel,
    DateTimeOffset AppliedAt,
    DateTimeOffset? FinalDecisionAt,
    string? FinalDecisionReason,
    DateOnly? OfferStartDate,
    int InterviewsPassed,
    int InterviewsTotal,
    string InterviewPassSummary);

public sealed record OperationsHistoricalInterviewDetail(
    Guid InterviewId,
    string RoundName,
    string Status,
    string? Recommendation,
    int? TechnicalScore,
    int? CommunicationScore,
    int? CultureScore,
    decimal? AverageScore,
    string? FeedbackSummary,
    DateTimeOffset StartsAt,
    DateTimeOffset? SubmittedAt);

public sealed record OperationsHistoricalApplicationDetail(
    OperationsHistoricalCandidateSummary Candidate,
    OperationsHistoricalApplicationSummary Application,
    IReadOnlyList<OperationsHistoricalInterviewDetail> Interviews);

public sealed record OperationsCandidateProfileSkill(
    Guid SkillId,
    string SkillName,
    string SkillLevel,
    decimal? YearsExperience,
    bool IsPrimary);

public sealed record OperationsCandidateProfile(
    OperationsHistoricalCandidateSummary Candidate,
    IReadOnlyList<OperationsCandidateProfileSkill> Skills,
    IReadOnlyList<OperationsHistoricalApplicationSummary> Applications,
    IReadOnlyList<OperationsCandidateMeetingEvent> MeetingEvents);

public sealed record OperationsCandidateMeetingParticipant(
    string DisplayName,
    string Email,
    string Role,
    bool IsOptional);

public sealed record OperationsCandidateMeetingEvent(
    Guid InterviewId,
    Guid JobApplicationId,
    Guid JobRequestId,
    Guid? JobPostId,
    string RequestCode,
    string JobTitle,
    string Client,
    string RoundName,
    string Status,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? MeetingLink,
    string? CalendarProvider,
    string? CalendarEventId,
    string? CalendarEventHtmlLink,
    string? LocationText,
    IReadOnlyList<OperationsCandidateMeetingParticipant> Participants);

public sealed record OperationsTalentRediscoveryContext(
    OperationsJobRequest JobRequest,
    OperationsJobPost? JobPost,
    string RequirementSource,
    IReadOnlyList<string> RequiredSkills,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    IReadOnlyList<OperationsRediscoveryCandidate> Candidates);

public sealed record RankTalentRediscoveryResult(
    IReadOnlyList<OperationsTalentRediscoveryMatch> TalentRediscoveryMatches,
    Guid AgentRunId,
    string Model,
    DateTimeOffset GeneratedAtUtc);

public sealed record OperationsApplicantRankingContext(
    OperationsJobRequest JobRequest,
    OperationsJobPost JobPost,
    IReadOnlyList<string> RequiredSkills,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    IReadOnlyList<OperationsApplicantRankingApplication> Applications);

public sealed record OperationsApplicantRankingApplication(
    Guid JobApplicationId,
    Guid CandidateId,
    string CandidateName,
    string CandidateEmail,
    string CandidateStatus,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    int? NoticePeriodDays,
    string ApplicationStatus,
    string SourceLabel,
    string? SourceDetail,
    string? CoverLetterText,
    DateTimeOffset AppliedAt,
    string? ApplicationSnapshotJson,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<OperationsApplicantDocumentEvidence> DocumentEvidence,
    IReadOnlyList<OperationsCandidateApplicationEvidence> HistoricalApplicationEvidence,
    IReadOnlyList<OperationsCandidateInterviewEvidence> HistoricalInterviewEvidence);

public sealed record OperationsApplicantDocumentEvidence(
    Guid ApplicationDocumentId,
    string DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageProvider,
    string StorageKey,
    string? StorageContainer,
    string ContentHashSha256,
    DateTimeOffset UploadedAt,
    string ExtractionStatus,
    bool HasExtractedText,
    string? ExtractedText,
    string? ExtractedTextHashSha256,
    string? ParserVersion,
    DateTimeOffset? ExtractedAt,
    string? ExtractionError);

public sealed record OperationsApplicationDocumentDownload(
    Guid ApplicationDocumentId,
    Guid JobApplicationId,
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record PortalCandidateProfileDocumentDownload(
    Guid CandidateProfileDocumentId,
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record OperationsApplicantRankingMatch(
    Guid JobApplicationId,
    Guid CandidateId,
    string CandidateName,
    string CandidateEmail,
    string? CurrentDesignation,
    decimal? ExperienceYears,
    int? NoticePeriodDays,
    int Rank,
    decimal Score,
    string Confidence,
    string Explanation,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<string> DocumentEvidence,
    IReadOnlyList<string> HistoricalOutcomeEvidence,
    string SemanticSimilarityStatus,
    Guid? AgentRunId,
    DateTimeOffset GeneratedAt);

public sealed record RankApplicantRankingsResult(
    IReadOnlyList<OperationsApplicantRankingMatch> ApplicantRankings,
    Guid AgentRunId,
    string Model,
    DateTimeOffset GeneratedAtUtc,
    string SemanticSimilarityStatus);

public sealed record OnlineHeadhuntingSearchInput(
    int? Limit,
    IReadOnlyList<string>? SourceCodes,
    Guid? SearchMoreFromRunId);

public sealed record OperationsOnlineHeadhuntingContext(
    OperationsJobRequest JobRequest,
    OperationsJobPost? JobPost,
    IReadOnlyList<string> RequiredSkills,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    IReadOnlyList<OperationsOnlineHeadhuntingDuplicateCandidate> CandidatePool,
    IReadOnlyList<OperationsOnlineHeadhuntingExistingLead> ExistingLeads);

public sealed record OperationsOnlineHeadhuntingDuplicateCandidate(
    Guid CandidateId,
    string DisplayName,
    string Email,
    string? Phone,
    string? LinkedInUrl,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    IReadOnlyList<string> Skills);

public sealed record OperationsOnlineHeadhuntingExistingLead(
    string SourceUrl,
    string? ProfileUrl,
    string? Email,
    string? Phone,
    string? DisplayName,
    string? CurrentTitle,
    string? CurrentCompany,
    string? LocationText);

public sealed record OperationsOnlineHeadhuntingRunSummary(
    Guid OnlineCandidateSourcingRunId,
    Guid JobRequestId,
    Guid? JobPostId,
    Guid? AiAgentRunId,
    Guid? SearchMoreFromRunId,
    int RequestedLimit,
    int DailyLeadLimit,
    int DailyLeadCountBeforeRun,
    int LeadsReturned,
    string SearchStatus,
    string Model,
    IReadOnlyList<string> SourceCodes,
    IReadOnlyList<string> Queries,
    DateTimeOffset CreatedAtUtc);

public sealed record OperationsOnlineCandidateLead(
    Guid OnlineCandidateLeadId,
    Guid OnlineCandidateSourcingRunId,
    Guid JobRequestId,
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
    string OutreachDraft,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record OperationsOnlineHeadhuntingResult(
    OperationsOnlineHeadhuntingRunSummary Run,
    IReadOnlyList<OperationsOnlineCandidateLead> Leads);

public sealed record OperationsOnlineHeadhuntingQueuedResult(
    Guid RequestId,
    Guid JobRequestId,
    Guid RequestedByUserId,
    string Status,
    string Message,
    int RequestedLimit,
    int DailyLeadLimit,
    int DailyLeadCountBeforeRun,
    IReadOnlyList<string> SourceCodes,
    DateTimeOffset QueuedAtUtc);

public sealed record UpdateOnlineCandidateLeadStatusInput(
    string Status);

public sealed record SendCandidateInvitationsInput(
    IReadOnlyList<Guid> CandidateIds,
    Guid? JobPostId,
    string? Message);

public sealed record SendCandidateInvitationsResult(
    int QueuedCount,
    IReadOnlyList<string> SkippedCandidates);

public sealed record PortalApplyToJobPostInput(
    string? Phone,
    string? LinkedInUrl,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    int? NoticePeriodDays,
    DateOnly? InterviewAvailabilityStartDate,
    DateOnly? InterviewAvailabilityEndDate,
    string? UniversityName,
    string? DegreeName,
    int? GraduationYear,
    string? CoverLetter,
    Guid? CandidateInvitationId,
    string? InvitationToken);

public sealed record PortalJobApplicationResult(
    Guid JobApplicationId,
    Guid JobPostId,
    Guid JobRequestId,
    string Status,
    bool AlreadyApplied);

public sealed record PortalUploadApplicationDocumentResult(
    PortalApplicationDocument Document);

public sealed record PortalUploadCandidateProfileDocumentResult(
    PortalCandidateProfileDocument Document);

public sealed record PortalMyApplications(
    IReadOnlyList<PortalMyApplicationItem> Items);

public sealed record PortalCandidateProfile(
    Guid? CandidateId,
    string DisplayName,
    string Email,
    string? Phone,
    string? LinkedInUrl,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    decimal? ExpectedSalaryAmount,
    string? ExpectedSalaryCurrency,
    int? NoticePeriodDays,
    PortalCandidateProfileEducation? PrimaryEducation,
    PortalCandidateProfileWorkHistory? CurrentWorkHistory,
    IReadOnlyList<PortalCandidateProfileSkill> Skills,
    IReadOnlyList<PortalCandidateProfileSkillOption> SkillOptions,
    PortalCandidateProfileDocument? ResumeDocument);

public sealed record PortalCandidateProfileDocument(
    Guid CandidateProfileDocumentId,
    Guid CandidateId,
    string DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageProvider,
    DateTimeOffset UploadedAt,
    string ExtractionStatus,
    bool HasTextEvidence,
    string? ParserVersion,
    DateTimeOffset? ExtractedAt,
    string? ExtractionError);

public sealed record PortalCandidateProfileEducation(
    string? UniversityName,
    string? DegreeName,
    int? GraduationYear);

public sealed record PortalCandidateProfileWorkHistory(
    string? CompanyName,
    string? Title);

public sealed record PortalCandidateProfileSkill(
    Guid SkillId,
    string SkillName,
    string SkillLevel,
    decimal? YearsExperience,
    bool IsPrimary);

public sealed record PortalCandidateProfileSkillOption(
    Guid SkillId,
    string SkillName,
    string? Category);

public sealed record UpdatePortalCandidateProfileInput(
    string DisplayName,
    string? Phone,
    string? LinkedInUrl,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    decimal? ExpectedSalaryAmount,
    string? ExpectedSalaryCurrency,
    int? NoticePeriodDays,
    PortalCandidateProfileEducation? PrimaryEducation,
    PortalCandidateProfileWorkHistory? CurrentWorkHistory,
    IReadOnlyList<UpdatePortalCandidateProfileSkillInput>? Skills);

public sealed record UpdatePortalCandidateProfileSkillInput(
    Guid SkillId,
    string? SkillLevel,
    decimal? YearsExperience,
    bool IsPrimary);

public sealed record PortalMyApplicationItem(
    Guid JobApplicationId,
    Guid JobPostId,
    Guid JobRequestId,
    string RequestCode,
    string JobTitle,
    string CompanyName,
    string Client,
    string Department,
    string Location,
    string Status,
    string SourceLabel,
    DateTimeOffset AppliedAt,
    DateTimeOffset? FinalDecisionAt,
    string? FinalDecisionReason,
    DateOnly? OfferStartDate,
    int InterviewsPassed,
    int InterviewsTotal,
    string InterviewPassSummary,
    IReadOnlyList<PortalApplicationTimelineItem> Timeline,
    IReadOnlyList<PortalApplicationDocument> Documents);

public sealed record PortalApplicationDocument(
    Guid ApplicationDocumentId,
    Guid JobApplicationId,
    string DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageProvider,
    DateTimeOffset UploadedAt,
    string ExtractionStatus,
    bool HasTextEvidence,
    string? ParserVersion,
    DateTimeOffset? ExtractedAt,
    string? ExtractionError);

public sealed record PortalApplicationDocumentMetadataInput(
    string DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageProvider,
    string StorageKey,
    string? StorageContainer,
    string ContentHashSha256,
    string ExtractionStatus,
    string? ExtractedText,
    string? ExtractedTextHashSha256,
    string? ParserVersion,
    DateTimeOffset? ExtractedAt,
    string? ExtractionError);

public sealed record PortalApplicationDocumentUploadContext(
    Guid JobApplicationId,
    Guid CandidateId);

public sealed record PortalCandidateProfileDocumentUploadContext(
    Guid CandidateId);

public sealed record PortalCandidateProfileDocumentMetadataInput(
    string DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageProvider,
    string StorageKey,
    string? StorageContainer,
    string ContentHashSha256,
    string ExtractionStatus,
    string? ExtractedText,
    string? ExtractedTextHashSha256,
    string? ParserVersion,
    DateTimeOffset? ExtractedAt,
    string? ExtractionError);

public sealed record PortalCandidateProfileDocumentEvidence(
    Guid CandidateProfileDocumentId,
    Guid CandidateId,
    string DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageProvider,
    string StorageKey,
    string? StorageContainer,
    string ContentHashSha256,
    DateTimeOffset UploadedAt,
    string ExtractionStatus,
    bool HasExtractedText,
    string? ExtractedText,
    string? ExtractedTextHashSha256,
    string? ParserVersion,
    DateTimeOffset? ExtractedAt,
    string? ExtractionError);

public sealed record PortalApplicationTimelineItem(
    string Kind,
    string Title,
    string Description,
    DateTimeOffset OccurredAt,
    string Status);

public sealed record AddManualCandidateInput(
    Guid? ExistingCandidateId,
    string? DisplayName,
    string Email,
    string? Phone,
    string? LinkedInUrl,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    int? NoticePeriodDays,
    IReadOnlyList<Guid>? SkillIds,
    string SourceLabel,
    string? SourceDetail,
    string? SourceUrl,
    string? RecruiterNotes,
    string? UniversityName,
    string? DegreeName,
    int? GraduationYear,
    string? InvitationMessage,
    ParsedCandidateCvEvidenceInput? ParsedCvEvidence = null,
    Guid? OnlineLeadId = null);

public sealed record ParsedCandidateCvEvidenceInput(
    string FileName,
    string? ContentType,
    long SizeBytes,
    string ContentHashSha256,
    string ExtractedText,
    string? Summary,
    Guid? AgentRunId,
    string? Model,
    DateTimeOffset? ParsedAtUtc);

public sealed record AddManualCandidateResult(
    Guid CandidateId,
    Guid JobApplicationId,
    Guid JobPostId,
    string Status,
    bool ExistingCandidate,
    bool ExistingApplication,
    bool InvitationQueued);

public sealed record ParseCandidateCvResult(
    string FileName,
    string ContentType,
    long SizeBytes,
    string ContentHashSha256,
    Guid AgentRunId,
    string Model,
    DateTimeOffset GeneratedAtUtc,
    string ExtractedText,
    string? DisplayName,
    string? Email,
    string? Phone,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    IReadOnlyList<string> Skills,
    string? UniversityName,
    string? DegreeName,
    int? GraduationYear,
    string Summary);

public sealed record UpdateCandidateApplicationStatusInput(
    string Decision,
    string? Notes);

public sealed record ScheduleCandidateInterviewInput(
    Guid JobPostInterviewRoundId,
    Guid? InterviewerUserId,
    DateTimeOffset StartsAtUtc,
    string? MeetingLink,
    string? LocationText,
    string? CalendarProvider = null,
    string? CalendarEventId = null,
    string? CalendarEventHtmlLink = null);

public sealed record ScheduleCandidateInterviewResult(
    Guid InterviewId,
    Guid JobApplicationId,
    Guid JobPostInterviewRoundId,
    Guid InterviewerUserId,
    string InterviewerName,
    string RoundName,
    DateTimeOffset StartsAtUtc,
    int DurationMinutes,
    string Status,
    string? MeetingLink = null,
    string? CalendarProvider = null,
    string? CalendarEventId = null,
    string? CalendarEventHtmlLink = null);

public sealed record ScheduleCandidateInterviewRepositoryResult(
    ScheduleCandidateInterviewResult Result,
    IReadOnlyList<OperationsNotificationDispatch> NotificationDispatches);

public enum OperationsScheduleCandidateInterviewValidationStatus
{
    Ready,
    NotFound,
    PriorRoundsPending,
    RoundAlreadyScheduled,
    MissingInterviewer
}

public sealed record OperationsScheduleCandidateInterviewValidation(
    OperationsScheduleCandidateInterviewValidationStatus Status);

public sealed record OperationsInterviewScheduleContext(
    string CompanyName,
    string RequestCode,
    string JobTitle,
    string CandidateName,
    string CandidateEmail,
    Guid InterviewerUserId,
    string InterviewerName,
    string InterviewerEmail,
    Guid HiringManagerUserId,
    string HiringManagerName,
    string HiringManagerEmail,
    string RecruiterName,
    string RecruiterEmail,
    string RoundName,
    int DurationMinutes,
    string TimeZoneId);

public sealed record OperationsInterviewTaskList(IReadOnlyList<OperationsInterviewTask> Items);

public sealed record OperationsInterviewTask(
    Guid InterviewId,
    Guid JobApplicationId,
    Guid JobPostInterviewRoundId,
    Guid JobRequestId,
    Guid JobPostId,
    string RequestCode,
    string JobTitle,
    string Client,
    string CandidateName,
    string CandidateEmail,
    string RoundName,
    string InterviewerName,
    Guid InterviewerUserId,
    string InterviewerAccountStatus,
    bool InterviewerIsDeleted,
    string ScheduledByName,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? MeetingLink,
    string? LocationText,
    string Status,
    string? Recommendation,
    int? TechnicalScore,
    int? CommunicationScore,
    int? CultureScore,
    string? FeedbackText,
    DateTimeOffset? SubmittedAt);

public sealed record GenerateInterviewQuestionRecommendationsInput(string? RegenerateReason);

public sealed record InterviewQuestionRecommendationSet(
    Guid RecommendationSetId,
    Guid InterviewId,
    Guid JobApplicationId,
    Guid JobPostInterviewRoundId,
    Guid AgentRunId,
    string Model,
    string PromptVersion,
    int VersionNumber,
    string Summary,
    string? Rationale,
    string? RegenerateReason,
    InterviewQuestionCoverage Coverage,
    string Status,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<InterviewQuestionRecommendation> Questions);

public sealed record InterviewQuestionCoverage(
    string RoundType,
    int TargetQuestionCount,
    int BankItemsUsed,
    string SemanticSimilarityStatus,
    IReadOnlyList<string> SkillsCovered,
    IReadOnlyList<string> CandidateEvidenceUsed);

public sealed record InterviewQuestionRecommendation(
    Guid QuestionRecommendationId,
    int SortOrder,
    string QuestionText,
    string QuestionType,
    string RoundType,
    string? SkillName,
    string Difficulty,
    string Rationale,
    string ExpectedSignal,
    IReadOnlyList<string> FollowUps,
    IReadOnlyList<string> EvaluationRubric,
    Guid? SourceBankItemId);

public sealed record OperationsInterviewQuestionRecommendationContext(
    Guid InterviewId,
    Guid JobApplicationId,
    Guid JobPostInterviewRoundId,
    Guid JobRequestId,
    Guid JobPostId,
    Guid CandidateId,
    string RequestCode,
    string JobTitle,
    string Client,
    string Department,
    string Location,
    string RoundName,
    string RoundType,
    int DurationMinutes,
    string Status,
    DateTimeOffset StartsAt,
    string InterviewerName,
    Guid InterviewerUserId,
    string CandidateName,
    string CandidateEmail,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    int? NoticePeriodDays,
    string ApplicationStatus,
    string? CoverLetterText,
    string? RecruiterNotes,
    string? ApplicationSnapshotJson,
    string JobRequestDescription,
    string JobPostDescription,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    IReadOnlyList<OperationsInterviewQuestionSkill> RequiredSkills,
    IReadOnlyList<OperationsInterviewQuestionSkill> CandidateSkills,
    IReadOnlyList<OperationsApplicantDocumentEvidence> DocumentEvidence,
    IReadOnlyList<OperationsCandidateInterviewEvidence> PriorInterviewEvidence);

public sealed record OperationsInterviewQuestionSkill(
    Guid SkillId,
    string Name,
    string? Category);

public sealed record InterviewQuestionBankItem(
    Guid InterviewQuestionBankItemId,
    Guid TenantId,
    Guid? SkillId,
    string? SkillName,
    string? SkillCategory,
    Guid? DepartmentId,
    string? JobFamily,
    string RoundType,
    string Difficulty,
    string QuestionText,
    string ExpectedSignal,
    IReadOnlyList<string> FollowUps,
    IReadOnlyList<string> EvaluationRubric,
    string? SourceTitle,
    string? SourceUrl,
    string ContentHashSha256);

public sealed record InterviewQuestionAgentResult(
    Guid AgentRunId,
    string Model,
    string PromptVersion,
    DateTimeOffset GeneratedAtUtc,
    string Summary,
    string? Rationale,
    InterviewQuestionCoverage Coverage,
    IReadOnlyList<Guid> RetrievedBankItemIds,
    IReadOnlyList<InterviewQuestionAgentQuestion> Questions);

public sealed record InterviewQuestionAgentQuestion(
    string QuestionText,
    string QuestionType,
    string RoundType,
    string? SkillName,
    string Difficulty,
    string Rationale,
    string ExpectedSignal,
    IReadOnlyList<string> FollowUps,
    IReadOnlyList<string> EvaluationRubric,
    Guid? SourceBankItemId);

public sealed record SubmitInterviewFeedbackInput(
    int TechnicalScore,
    int CommunicationScore,
    int CultureScore,
    string Recommendation,
    string FeedbackText);

public sealed record SubmitInterviewFeedbackResult(
    Guid InterviewId,
    Guid JobApplicationId,
    string Status,
    string Recommendation,
    DateTimeOffset SubmittedAt);

public sealed record ForwardToHiringManagerResult(
    Guid JobApplicationId,
    Guid JobRequestId,
    Guid HiringManagerUserId,
    string Status);

public sealed record HiringManagerReviewList(IReadOnlyList<HiringManagerReviewListItem> Items);

public sealed record HiringManagerReviewListItem(
    Guid JobApplicationId,
    Guid JobRequestId,
    Guid? JobPostId,
    string RequestCode,
    string JobTitle,
    string Client,
    string Department,
    string CandidateName,
    string CandidateEmail,
    string Status,
    string HiringManagerName,
    DateTimeOffset UpdatedAt);

public sealed record HiringReviewCandidateSummary(
    Guid CandidateId,
    string DisplayName,
    string Email,
    string Status,
    string? CurrentDesignation,
    string? CurrentCompany,
    decimal? ExperienceYears,
    decimal? ExpectedSalaryAmount,
    string? ExpectedSalaryCurrency,
    int? NoticePeriodDays);

public sealed record HiringReviewJobSummary(
    Guid JobRequestId,
    Guid? JobPostId,
    string RequestCode,
    string JobTitle,
    string Client,
    string Department,
    string Location,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    int RequiredPositions,
    int FulfilledPositions,
    string RequestStatus,
    string ApplicationStatus,
    DateTimeOffset? FinalOutcomeRecordedAt,
    string? FinalOutcomeReason,
    string SourceLabel,
    string? SourceDetail,
    string? RecruiterNotes,
    string? RequestDescription,
    string? JobPostDescription);

public sealed record HiringReviewInterviewDetail(
    Guid InterviewId,
    Guid? JobPostInterviewRoundId,
    string RoundName,
    string Status,
    string InterviewerName,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? Recommendation,
    int? TechnicalScore,
    int? CommunicationScore,
    int? CultureScore,
    decimal? AverageScore,
    string? FeedbackText,
    string? SkipReason,
    DateTimeOffset? SubmittedAt);

public sealed record OfferLetterDetails(
    Guid OfferLetterId,
    Guid JobApplicationId,
    Guid JobRequestId,
    Guid? JobPostId,
    Guid CandidateId,
    Guid GeneratedByUserId,
    string GeneratedByName,
    int Version,
    string Status,
    string? CompensationText,
    DateOnly? StartDate,
    string? ReportingManager,
    string? WorkLocation,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record OfferPresentationMeetingDetails(
    Guid OfferPresentationMeetingId,
    Guid OfferLetterId,
    Guid JobApplicationId,
    DateTimeOffset MeetingAt,
    string LocationText,
    string? Notes,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record HiringReviewDecisionMetric(
    string Key,
    string Label,
    string Value,
    decimal? Score,
    string? Unit,
    string Tone,
    string Icon,
    string? Detail);

public sealed record HiringReviewDecisionContextItem(
    string Key,
    string Label,
    string Value,
    string Icon,
    string Tone);

public sealed record HiringReviewDecisionBriefInsight(
    string AgentKey,
    string AgentName,
    string Summary,
    IReadOnlyList<HiringReviewDecisionMetric> Metrics,
    IReadOnlyList<HiringReviewDecisionContextItem> Context,
    IReadOnlyList<string> Signals);

public sealed record HiringReviewDetail(
    HiringReviewCandidateSummary Candidate,
    HiringReviewJobSummary Job,
    IReadOnlyList<HiringReviewInterviewDetail> Interviews,
    string DecisionBrief,
    HiringReviewDecisionBriefInsight DecisionBriefInsight,
    OfferLetterDetails? OfferLetter,
    IReadOnlyList<OfferPresentationMeetingDetails> PresentationMeetings);

public sealed record GenerateOfferLetterInput(
    string? CompensationText,
    DateOnly? StartDate,
    string? ReportingManager,
    string? WorkLocation,
    string? AdditionalNotes);

public sealed record UpdateOfferLetterInput(
    string Body,
    string? CompensationText,
    DateOnly? StartDate,
    string? ReportingManager,
    string? WorkLocation,
    string? Status);

public sealed record ScheduleOfferPresentationMeetingInput(
    DateTimeOffset MeetingAtUtc,
    string LocationText,
    string? Notes);

public sealed record HiringOutcomeInput(
    string Outcome,
    string? Reason,
    DateOnly? JoiningDate);

public sealed record HiringOutcomeResult(
    Guid JobApplicationId,
    Guid JobRequestId,
    string ApplicationStatus,
    string JobRequestStatus,
    DateOnly? JoiningDate,
    int FulfilledPositions,
    int RequiredPositions);

public sealed record CloseJobRequestInput(string Reason);

public sealed record ReportingManagerOption(
    Guid EmployeeId,
    string DisplayName,
    string Email,
    string? Designation,
    string Department,
    string Location,
    decimal? ExperienceYears,
    bool IsDepartmentMatch);

public sealed record ReportingManagerOptionList(
    IReadOnlyList<ReportingManagerOption> Items,
    int TotalCount,
    bool HasMore);

public sealed record OperationsEmployeeReferral(
    Guid ReferralId,
    Guid JobRequestId,
    Guid EmployeeId,
    string EmployeeName,
    string EmployeeEmail,
    string? Designation,
    string Department,
    decimal? ExperienceYears,
    Guid ReferredByUserId,
    string ReferredByName,
    Guid? PresalesUserId,
    string? PresalesName,
    string Status,
    decimal? FitScore,
    string? RecommendationSummary,
    string? ClientFeedback,
    DateTimeOffset CreatedAt);

public sealed record CreateOperationsJobRequestInput(
    string Title,
    string Client,
    string? ClientContext,
    string Description,
    Guid DepartmentId,
    Guid LocationId,
    IReadOnlyList<Guid> SkillIds,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    int RequiredPositions,
    string Priority,
    Guid HiringManagerId);

public sealed record DraftJobDescriptionInput(
    string Title,
    string Client,
    string? ClientContext,
    Guid DepartmentId,
    Guid LocationId,
    IReadOnlyList<Guid> SkillIds,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    int RequiredPositions,
    string Priority,
    Guid HiringManagerId);

public sealed record DraftJobDescriptionResult(
    string Description,
    Guid AgentRunId,
    string Model,
    DateTimeOffset GeneratedAtUtc);

public sealed record CreateEmployeeReferralsInput(
    IReadOnlyList<Guid> EmployeeIds,
    Guid PresalesUserId,
    string? RecommendationSummary);

public sealed record EmployeeReferralDecisionInput(
    IReadOnlyList<EmployeeReferralDecisionItem> Decisions);

public sealed record EmployeeReferralDecisionItem(
    Guid ReferralId,
    string Decision,
    string? Feedback);

public sealed record CreateJobPostInput(
    Guid InterviewTemplateId,
    string Title,
    string Description,
    IReadOnlyList<Guid> SkillIds,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    int RequiredPositions,
    IReadOnlyList<UpsertJobPostInterviewRoundInput> InterviewRounds);

public sealed record UpdateJobPostInput(
    string Title,
    string Description,
    IReadOnlyList<Guid> SkillIds,
    decimal? ExperienceMinYears,
    decimal? ExperienceMaxYears,
    int RequiredPositions,
    IReadOnlyList<UpsertJobPostInterviewRoundInput> InterviewRounds);

public sealed record UpsertJobPostInterviewRoundInput(
    Guid? JobPostInterviewRoundId,
    Guid? InterviewTemplateRoundId,
    int RoundOrder,
    string Name,
    Guid? OwnerUserId,
    int DurationMinutes,
    string Status);

public sealed record CreateOperationsJobRequestResult(
    OperationsJobRequest JobRequest,
    OperationsWorkflowAssignment Assignment);

public sealed record CreateOperationsJobRequestRepositoryResult(
    CreateOperationsJobRequestResult Result,
    IReadOnlyList<OperationsNotificationDispatch> NotificationDispatches);

public sealed record OperationsMutationRepositoryResult(
    bool Succeeded,
    IReadOnlyList<OperationsNotificationDispatch> NotificationDispatches);

public sealed record OperationsMutationRepositoryResult<T>(
    bool Succeeded,
    T? Result,
    IReadOnlyList<OperationsNotificationDispatch> NotificationDispatches);

public sealed record OperationsCreateJobRequestValidation(
    bool DepartmentExists,
    bool LocationExists,
    bool HiringManagerExists,
    IReadOnlyList<Guid> ActiveSkillIds);

public sealed record OperationsNotificationDispatch(
    Guid RecipientUserId,
    string EventCode,
    string Title,
    string Message,
    string Category,
    string Severity,
    string EntityType,
    Guid EntityId,
    IReadOnlyDictionary<string, string> Metadata);
