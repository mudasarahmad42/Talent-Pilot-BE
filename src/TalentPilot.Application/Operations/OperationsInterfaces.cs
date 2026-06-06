using TalentPilot.Application.Ai;
using TalentPilot.Application.Documents;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Operations;

public interface IOperationsService
{
    Task<Result<OperationsSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<Result<TenantAdminDashboard>> GetTenantAdminDashboardAsync(
        TenantAdminDashboardQuery query,
        CancellationToken cancellationToken);

    Task<Result<PmoDashboard>> GetPmoDashboardAsync(
        PmoDashboardQuery query,
        CancellationToken cancellationToken);

    Task<Result<HiringManagerDashboard>> GetHiringManagerDashboardAsync(CancellationToken cancellationToken);

    Task<Result<OperationsJobRequestIntakeOptions>> GetIntakeOptionsAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<OperationsActivityEvent>>> GetActivityAsync(Guid entityId, CancellationToken cancellationToken);

    Task<Result<OperationsPmoReview>> GetPmoReviewAsync(Guid jobRequestId, CancellationToken cancellationToken);

    Task<Result<OperationsRecruitmentQueue>> GetRecruitmentQueueAsync(CancellationToken cancellationToken);

    Task<Result<OperationsRecruiterSourcing>> GetRecruiterSourcingAsync(Guid jobRequestId, CancellationToken cancellationToken);

    Task<Result<OperationsHistoricalApplicationDetail>> GetHistoricalApplicationAsync(
        Guid jobApplicationId,
        CancellationToken cancellationToken);

    Task<Result<OperationsApplicationDocumentDownload>> DownloadRecruiterApplicationDocumentAsync(
        Guid jobApplicationId,
        Guid applicationDocumentId,
        CancellationToken cancellationToken);

    Task<Result<OperationsCandidateProfile>> GetCandidateProfileAsync(
        Guid candidateId,
        CancellationToken cancellationToken);

    Task<Result<OperationsJobPublishing>> GetJobPublishingAsync(CancellationToken cancellationToken);

    Task<Result<PublicPortalContext>> GetPublicPortalContextAsync(
        PublicPortalContextQuery query,
        CancellationToken cancellationToken);

    Task<Result<PortalJobPostList>> ListPortalJobPostsAsync(string? tenantSlug, CancellationToken cancellationToken);

    Task<Result<PortalJobPostDetail>> GetPortalJobPostAsync(Guid jobPostId, CancellationToken cancellationToken);

    Task<Result<PortalInvitationContext>> GetPortalInvitationAsync(
        Guid candidateInvitationId,
        string token,
        CancellationToken cancellationToken);

    Task<Result<PortalJobApplicationResult>> ApplyToPortalJobPostAsync(
        Guid jobPostId,
        PortalApplyToJobPostInput input,
        CancellationToken cancellationToken);

    Task<Result<PortalUploadApplicationDocumentResult>> UploadPortalApplicationDocumentAsync(
        Guid jobApplicationId,
        string documentType,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken);

    Task<Result<PortalUploadCandidateProfileDocumentResult>> UploadPortalCandidateProfileDocumentAsync(
        string documentType,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken);

    Task<Result<PortalCandidateProfileDocumentDownload>> DownloadPortalCandidateProfileDocumentAsync(
        Guid candidateProfileDocumentId,
        CancellationToken cancellationToken);

    Task<Result<PortalCandidateProfile>> GetPortalCandidateProfileAsync(CancellationToken cancellationToken);

    Task<Result<PortalCandidateProfile>> UpdatePortalCandidateProfileAsync(
        UpdatePortalCandidateProfileInput input,
        CancellationToken cancellationToken);

    Task<Result<PortalMyApplications>> GetPortalMyApplicationsAsync(CancellationToken cancellationToken);

    Task<Result<CreateOperationsJobRequestResult>> CreateJobRequestAsync(
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken);

    Task<Result<DraftJobDescriptionResult>> DraftJobDescriptionAsync(
        DraftJobDescriptionInput input,
        CancellationToken cancellationToken);

    Task<Result<RankBenchMatchesResult>> RankBenchMatchesAsync(Guid jobRequestId, CancellationToken cancellationToken);

    Task<Result<RankTalentRediscoveryResult>> RankTalentRediscoveryAsync(Guid jobRequestId, CancellationToken cancellationToken);

    Task<Result<RankApplicantRankingsResult>> RankApplicantRankingsAsync(Guid jobPostId, CancellationToken cancellationToken);

    Task<Result<OperationsOnlineHeadhuntingQueuedResult>> SearchOnlineCandidatesAsync(
        Guid jobRequestId,
        OnlineHeadhuntingSearchInput input,
        CancellationToken cancellationToken);

    Task RunOnlineCandidatesSearchAsync(
        OnlineHeadhuntingBackgroundJob job,
        CancellationToken cancellationToken);

    Task<Result<OperationsOnlineCandidateLead>> UpdateOnlineCandidateLeadStatusAsync(
        Guid onlineCandidateLeadId,
        UpdateOnlineCandidateLeadStatusInput input,
        CancellationToken cancellationToken);

    Task<Result<SendCandidateInvitationsResult>> SendCandidateInvitationsAsync(
        Guid jobRequestId,
        SendCandidateInvitationsInput input,
        CancellationToken cancellationToken);

    Task<Result<AddManualCandidateResult>> AddManualCandidateToJobPostAsync(
        Guid jobPostId,
        AddManualCandidateInput input,
        CancellationToken cancellationToken);

    Task<Result<ParseCandidateCvResult>> ParseCandidateCvAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken);

    Task<Result<OperationsRecruiterApplication>> UpdateCandidateApplicationStatusAsync(
        Guid jobApplicationId,
        UpdateCandidateApplicationStatusInput input,
        CancellationToken cancellationToken);

    Task<Result<ScheduleCandidateInterviewResult>> ScheduleCandidateInterviewAsync(
        Guid jobApplicationId,
        ScheduleCandidateInterviewInput input,
        CancellationToken cancellationToken);

    Task<Result<OperationsInterviewTaskList>> GetMyInterviewTasksAsync(CancellationToken cancellationToken);

    Task<Result<InterviewQuestionRecommendationSet>> GetLatestInterviewQuestionRecommendationsAsync(
        Guid interviewId,
        CancellationToken cancellationToken);

    Task<Result<InterviewQuestionRecommendationSet>> GenerateInterviewQuestionRecommendationsAsync(
        Guid interviewId,
        GenerateInterviewQuestionRecommendationsInput input,
        CancellationToken cancellationToken);

    Task<Result<DocumentExportFile>> DownloadInterviewQuestionRecommendationsDocxAsync(
        Guid interviewId,
        CancellationToken cancellationToken);

    Task<Result<SubmitInterviewFeedbackResult>> SubmitInterviewFeedbackAsync(
        Guid interviewId,
        SubmitInterviewFeedbackInput input,
        CancellationToken cancellationToken);

    Task<Result<ForwardToHiringManagerResult>> ForwardToHiringManagerAsync(
        Guid jobApplicationId,
        CancellationToken cancellationToken);

    Task<Result<HiringManagerReviewList>> GetHiringManagerReviewsAsync(CancellationToken cancellationToken);

    Task<Result<HiringReviewDetail>> GetHiringReviewAsync(
        Guid jobApplicationId,
        CancellationToken cancellationToken);

    Task<Result<ReportingManagerOptionList>> SearchReportingManagerOptionsAsync(
        Guid jobRequestId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<Result<OfferLetterDetails>> GenerateOfferLetterAsync(
        Guid jobApplicationId,
        GenerateOfferLetterInput input,
        CancellationToken cancellationToken);

    Task<Result<OfferLetterDetails>> UpdateOfferLetterAsync(
        Guid offerLetterId,
        UpdateOfferLetterInput input,
        CancellationToken cancellationToken);

    Task<Result<OfferPresentationMeetingDetails>> ScheduleOfferPresentationMeetingAsync(
        Guid offerLetterId,
        ScheduleOfferPresentationMeetingInput input,
        CancellationToken cancellationToken);

    Task<Result<HiringOutcomeResult>> RecordHiringOutcomeAsync(
        Guid jobApplicationId,
        HiringOutcomeInput input,
        CancellationToken cancellationToken);

    Task<Result> CloseJobRequestAsync(
        Guid jobRequestId,
        CloseJobRequestInput input,
        CancellationToken cancellationToken);

    Task<Result> ClaimAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<Result> CreateEmployeeReferralsAsync(
        Guid jobRequestId,
        CreateEmployeeReferralsInput input,
        CancellationToken cancellationToken);

    Task<Result> ForwardToRecruitersAsync(Guid jobRequestId, CancellationToken cancellationToken);

    Task<Result> DecideEmployeeReferralsAsync(
        Guid jobRequestId,
        EmployeeReferralDecisionInput input,
        CancellationToken cancellationToken);

    Task<Result<OperationsJobPost>> CreateJobPostAsync(
        Guid jobRequestId,
        CreateJobPostInput input,
        CancellationToken cancellationToken);

    Task<Result<OperationsJobPost>> UpdateJobPostAsync(
        Guid jobPostId,
        UpdateJobPostInput input,
        CancellationToken cancellationToken);

    Task<Result<OperationsJobPost>> PublishJobPostAsync(Guid jobPostId, CancellationToken cancellationToken);

    Task<Result<OperationsJobPost>> CloseJobPostAsync(Guid jobPostId, CancellationToken cancellationToken);

    Task<Result> MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken);

    Task<Result> MarkAllNotificationsReadAsync(CancellationToken cancellationToken);
}

public interface IOperationsRepository
{
    Task<OperationsSnapshot> GetSnapshotAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<TenantAdminDashboard> GetTenantAdminDashboardAsync(
        Guid tenantId,
        TenantAdminDashboardQuery query,
        CancellationToken cancellationToken);

    Task<PmoDashboard> GetPmoDashboardAsync(
        Guid tenantId,
        Guid actorUserId,
        PmoDashboardQuery query,
        CancellationToken cancellationToken);

    Task<HiringManagerDashboard> GetHiringManagerDashboardAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        CancellationToken cancellationToken);

    Task<OperationsJobRequestIntakeOptions> GetIntakeOptionsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetActorRoleCodesAsync(Guid tenantId, Guid actorUserId, CancellationToken cancellationToken);

    Task<OperationsCreateJobRequestValidation> ValidateCreateJobRequestAsync(
        Guid tenantId,
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OperationsActivityEvent>> GetActivityAsync(
        Guid tenantId,
        Guid userId,
        Guid entityId,
        CancellationToken cancellationToken);

    Task<OperationsPmoReview?> GetPmoReviewAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        bool includeEmployees,
        CancellationToken cancellationToken);

    Task<OperationsRecruitmentQueue> GetRecruitmentQueueAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<OperationsRecruiterSourcing?> GetRecruiterSourcingAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CancellationToken cancellationToken);

    Task<OperationsHistoricalApplicationDetail?> GetHistoricalApplicationAsync(
        Guid tenantId,
        Guid jobApplicationId,
        CancellationToken cancellationToken);

    Task<OperationsCandidateProfile?> GetCandidateProfileAsync(
        Guid tenantId,
        Guid candidateId,
        CancellationToken cancellationToken);

    Task<OperationsJobPublishing> GetJobPublishingAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<PublicPortalContext?> GetPublicPortalContextAsync(
        PublicPortalContextQuery query,
        CancellationToken cancellationToken);

    Task<PortalJobPostList> ListPortalJobPostsAsync(string? tenantSlug, CancellationToken cancellationToken);

    Task<PortalJobPostDetail?> GetPortalJobPostAsync(Guid jobPostId, CancellationToken cancellationToken);

    Task<PortalInvitationContext?> GetPortalInvitationAsync(
        Guid candidateInvitationId,
        string token,
        CancellationToken cancellationToken);

    Task<PortalJobApplicationResult?> ApplyToPortalJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        PortalApplyToJobPostInput input,
        CancellationToken cancellationToken);

    Task<PortalApplicationDocumentUploadContext?> GetPortalApplicationDocumentUploadContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        CancellationToken cancellationToken);

    Task<PortalApplicationDocument?> AddPortalApplicationDocumentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        PortalApplicationDocumentMetadataInput input,
        CancellationToken cancellationToken);

    Task<OperationsApplicantDocumentEvidence?> CopyLatestProfileDocumentToApplicationAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PortalApplicationDocument>> ListPortalApplicationDocumentsAsync(
        Guid tenantId,
        IReadOnlyList<Guid> jobApplicationIds,
        CancellationToken cancellationToken);

    Task<OperationsApplicantDocumentEvidence?> GetRecruiterApplicationDocumentAsync(
        Guid tenantId,
        Guid jobApplicationId,
        Guid applicationDocumentId,
        CancellationToken cancellationToken);

    Task<PortalMyApplications> GetPortalMyApplicationsAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<PortalCandidateProfile?> GetPortalCandidateProfileAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<PortalCandidateProfile?> UpdatePortalCandidateProfileAsync(
        Guid tenantId,
        Guid actorUserId,
        UpdatePortalCandidateProfileInput input,
        CancellationToken cancellationToken);

    Task<PortalCandidateProfileDocumentUploadContext?> GetPortalCandidateProfileDocumentUploadContextAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<PortalCandidateProfileDocument?> AddPortalCandidateProfileDocumentAsync(
        Guid tenantId,
        Guid actorUserId,
        PortalCandidateProfileDocumentMetadataInput input,
        CancellationToken cancellationToken);

    Task<PortalCandidateProfileDocumentEvidence?> GetPortalCandidateProfileDocumentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid candidateProfileDocumentId,
        CancellationToken cancellationToken);

    Task<OperationsBenchMatchingContext?> GetBenchMatchingContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CancellationToken cancellationToken);

    Task SaveBenchMatchesAsync(
        Guid tenantId,
        Guid jobRequestId,
        Guid agentRunId,
        IReadOnlyList<OperationsBenchMatch> matches,
        CancellationToken cancellationToken);

    Task<OperationsTalentRediscoveryContext?> GetTalentRediscoveryContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CancellationToken cancellationToken);

    Task SaveTalentRediscoveryMatchesAsync(
        Guid tenantId,
        Guid jobRequestId,
        Guid agentRunId,
        IReadOnlyList<OperationsTalentRediscoveryMatch> matches,
        CancellationToken cancellationToken);

    Task<OperationsApplicantRankingContext?> GetApplicantRankingContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        CancellationToken cancellationToken);

    Task SaveApplicantRankingsAsync(
        Guid tenantId,
        Guid jobPostId,
        Guid agentRunId,
        IReadOnlyList<OperationsApplicantRankingMatch> matches,
        CancellationToken cancellationToken);

    Task<OperationsOnlineHeadhuntingContext?> GetOnlineHeadhuntingContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CancellationToken cancellationToken);

    Task<int> CountOnlineHeadhuntingLeadsCreatedTodayAsync(
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken);

    Task<OperationsOnlineHeadhuntingResult> SaveOnlineHeadhuntingResultAsync(
        Guid tenantId,
        Guid actorUserId,
        OnlineHeadhuntingSearchInput input,
        OperationsOnlineHeadhuntingContext context,
        OnlineHeadhuntingAgentResult result,
        int dailyLeadCountBeforeRun,
        int dailyLeadLimit,
        CancellationToken cancellationToken);

    Task<OperationsOnlineHeadhuntingResult?> GetLatestOnlineHeadhuntingResultAsync(
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken);

    Task<OperationsOnlineCandidateLead?> UpdateOnlineCandidateLeadStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid onlineCandidateLeadId,
        UpdateOnlineCandidateLeadStatusInput input,
        CancellationToken cancellationToken);

    Task<SendCandidateInvitationsResult> SendCandidateInvitationsAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        SendCandidateInvitationsInput input,
        CancellationToken cancellationToken);

    Task<AddManualCandidateResult?> AddManualCandidateToJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        AddManualCandidateInput input,
        CancellationToken cancellationToken);

    Task<OperationsRecruiterApplication?> UpdateCandidateApplicationStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        UpdateCandidateApplicationStatusInput input,
        CancellationToken cancellationToken);

    Task<ScheduleCandidateInterviewRepositoryResult?> ScheduleCandidateInterviewAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        ScheduleCandidateInterviewInput input,
        CancellationToken cancellationToken);

    Task<OperationsScheduleCandidateInterviewValidation> ValidateCandidateInterviewScheduleAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        ScheduleCandidateInterviewInput input,
        CancellationToken cancellationToken);

    Task<OperationsInterviewScheduleContext?> GetInterviewScheduleContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        ScheduleCandidateInterviewInput input,
        CancellationToken cancellationToken);

    Task<OperationsInterviewTaskList> GetMyInterviewTasksAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantTasks,
        CancellationToken cancellationToken);

    Task<OperationsInterviewQuestionRecommendationContext?> GetInterviewQuestionRecommendationContextAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantTasks,
        Guid interviewId,
        CancellationToken cancellationToken);

    Task<InterviewQuestionRecommendationSet?> GetLatestInterviewQuestionRecommendationsAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantTasks,
        Guid interviewId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InterviewQuestionBankItem>> ListInterviewQuestionBankItemsAsync(
        Guid tenantId,
        IReadOnlyList<Guid> skillIds,
        string roundType,
        string jobFamily,
        int take,
        CancellationToken cancellationToken);

    Task<InterviewQuestionRecommendationSet> SaveInterviewQuestionRecommendationsAsync(
        Guid tenantId,
        Guid actorUserId,
        OperationsInterviewQuestionRecommendationContext context,
        string? regenerateReason,
        InterviewQuestionAgentResult result,
        CancellationToken cancellationToken);

    Task<OperationsMutationRepositoryResult<SubmitInterviewFeedbackResult>> SubmitInterviewFeedbackAsync(
        Guid tenantId,
        Guid actorUserId,
        bool canAdminOverrideInactiveInterviewer,
        Guid interviewId,
        SubmitInterviewFeedbackInput input,
        CancellationToken cancellationToken);

    Task<OperationsMutationRepositoryResult<ForwardToHiringManagerResult>> ForwardToHiringManagerAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        CancellationToken cancellationToken);

    Task<HiringManagerReviewList> GetHiringManagerReviewsAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        CancellationToken cancellationToken);

    Task<HiringReviewDetail?> GetHiringReviewAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobApplicationId,
        CancellationToken cancellationToken);

    Task<ReportingManagerOptionList?> SearchReportingManagerOptionsAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobRequestId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<OfferLetterDetails?> GenerateOfferLetterAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobApplicationId,
        GenerateOfferLetterInput input,
        CancellationToken cancellationToken);

    Task<OfferLetterDetails?> UpdateOfferLetterAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid offerLetterId,
        UpdateOfferLetterInput input,
        CancellationToken cancellationToken);

    Task<OfferPresentationMeetingDetails?> ScheduleOfferPresentationMeetingAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid offerLetterId,
        ScheduleOfferPresentationMeetingInput input,
        CancellationToken cancellationToken);

    Task<HiringOutcomeResult?> RecordHiringOutcomeAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobApplicationId,
        HiringOutcomeInput input,
        CancellationToken cancellationToken);

    Task<bool> CloseJobRequestAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobRequestId,
        CloseJobRequestInput input,
        CancellationToken cancellationToken);

    Task<CreateOperationsJobRequestRepositoryResult> CreateJobRequestAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken);

    Task<bool> ClaimAssignmentAsync(Guid tenantId, Guid actorUserId, Guid assignmentId, CancellationToken cancellationToken);

    Task<OperationsMutationRepositoryResult> CreateEmployeeReferralsAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CreateEmployeeReferralsInput input,
        CancellationToken cancellationToken);

    Task<OperationsMutationRepositoryResult> ForwardToRecruitersAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CancellationToken cancellationToken);

    Task<OperationsMutationRepositoryResult> DecideEmployeeReferralsAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        EmployeeReferralDecisionInput input,
        CancellationToken cancellationToken);

    Task<OperationsJobPost?> CreateJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CreateJobPostInput input,
        CancellationToken cancellationToken);

    Task<OperationsJobPost?> UpdateJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        UpdateJobPostInput input,
        CancellationToken cancellationToken);

    Task<OperationsJobPost?> PublishJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        CancellationToken cancellationToken);

    Task<OperationsJobPost?> CloseJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        CancellationToken cancellationToken);

    Task<bool> MarkNotificationReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken);

    Task MarkAllNotificationsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}
