using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TalentPilot.Application.Operations;
using TalentPilot.Common.Results;

namespace TalentPilot.Api.Controllers;

[Route("api/talent-pilot")]
public sealed class OperationsController : ApiControllerBase
{
    private readonly IOperationsService _operationsService;

    public OperationsController(IOperationsService operationsService)
    {
        _operationsService = operationsService;
    }

    [HttpGet("snapshot")]
    public async Task<ActionResult<OperationsSnapshot>> Snapshot(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetSnapshotAsync(cancellationToken));
    }

    [HttpGet("tenant-admin/dashboard")]
    public async Task<ActionResult<TenantAdminDashboard>> TenantAdminDashboard(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] Guid? departmentId,
        [FromQuery] string? sourceLabel,
        [FromQuery] Guid? recruiterUserId,
        CancellationToken cancellationToken)
    {
        var query = new TenantAdminDashboardQuery(fromUtc, toUtc, departmentId, sourceLabel, recruiterUserId);
        return FromResult(await _operationsService.GetTenantAdminDashboardAsync(query, cancellationToken));
    }

    [HttpGet("pmo/dashboard")]
    public async Task<ActionResult<PmoDashboard>> PmoDashboard(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] Guid? departmentId,
        CancellationToken cancellationToken)
    {
        var query = new PmoDashboardQuery(fromUtc, toUtc, departmentId);
        return FromResult(await _operationsService.GetPmoDashboardAsync(query, cancellationToken));
    }

    [HttpGet("hiring-manager/dashboard")]
    public async Task<ActionResult<HiringManagerDashboard>> HiringManagerDashboard(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetHiringManagerDashboardAsync(cancellationToken));
    }

    [HttpGet("job-requests/intake-options")]
    public async Task<ActionResult<OperationsJobRequestIntakeOptions>> IntakeOptions(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetIntakeOptionsAsync(cancellationToken));
    }

    [HttpGet("job-requests/{entityId:guid}/activity")]
    public async Task<ActionResult<IReadOnlyList<OperationsActivityEvent>>> Activity(
        Guid entityId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetActivityAsync(entityId, cancellationToken));
    }

    [HttpGet("job-requests/{entityId:guid}/pmo-review")]
    public async Task<ActionResult<OperationsPmoReview>> PmoReview(
        Guid entityId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetPmoReviewAsync(entityId, cancellationToken));
    }

    [HttpGet("recruitment/queue")]
    public async Task<ActionResult<OperationsRecruitmentQueue>> RecruitmentQueue(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetRecruitmentQueueAsync(cancellationToken));
    }

    [HttpGet("job-requests/{entityId:guid}/recruiter-sourcing")]
    public async Task<ActionResult<OperationsRecruiterSourcing>> RecruiterSourcing(
        Guid entityId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetRecruiterSourcingAsync(entityId, cancellationToken));
    }

    [HttpGet("recruitment/applications/{jobApplicationId:guid}/history")]
    public async Task<ActionResult<OperationsHistoricalApplicationDetail>> HistoricalApplication(
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetHistoricalApplicationAsync(jobApplicationId, cancellationToken));
    }

    [HttpGet("recruitment/applications/{jobApplicationId:guid}/documents/{applicationDocumentId:guid}/download")]
    public async Task<IActionResult> DownloadRecruiterApplicationDocument(
        Guid jobApplicationId,
        Guid applicationDocumentId,
        CancellationToken cancellationToken)
    {
        var result = await _operationsService.DownloadRecruiterApplicationDocumentAsync(
            jobApplicationId,
            applicationDocumentId,
            cancellationToken);

        if (result.Failed)
        {
            return FromResult(result).Result ?? BadRequest(new
            {
                error = result.Error.Code,
                message = result.Error.Message
            });
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpGet("recruitment/candidates/{candidateId:guid}/profile")]
    public async Task<ActionResult<OperationsCandidateProfile>> CandidateProfile(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetCandidateProfileAsync(candidateId, cancellationToken));
    }

    [HttpGet("job-posts")]
    public async Task<ActionResult<OperationsJobPublishing>> JobPosts(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetJobPublishingAsync(cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("portal/job-posts")]
    public async Task<ActionResult<PortalJobPostList>> PortalJobPosts(
        [FromQuery] string? tenantSlug,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ListPortalJobPostsAsync(tenantSlug, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("portal/job-posts/{jobPostId:guid}")]
    public async Task<ActionResult<PortalJobPostDetail>> PortalJobPost(
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetPortalJobPostAsync(jobPostId, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("portal/invitations/{candidateInvitationId:guid}")]
    public async Task<ActionResult<PortalInvitationContext>> PortalInvitation(
        Guid candidateInvitationId,
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetPortalInvitationAsync(candidateInvitationId, token, cancellationToken));
    }

    [HttpPost("portal/job-posts/{jobPostId:guid}/applications")]
    public async Task<ActionResult<PortalJobApplicationResult>> ApplyToPortalJobPost(
        Guid jobPostId,
        PortalApplyToJobPostInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ApplyToPortalJobPostAsync(jobPostId, input, cancellationToken));
    }

    [HttpPost("portal/job-applications/{jobApplicationId:guid}/documents")]
    [RequestSizeLimit(5_500_000)]
    public async Task<ActionResult<PortalUploadApplicationDocumentResult>> UploadPortalApplicationDocument(
        Guid jobApplicationId,
        [FromForm] IFormFile file,
        [FromForm] string? documentType,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return FromResult(Result<PortalUploadApplicationDocumentResult>.Failure(
                "portal_application_document.empty_file",
                "Uploaded document is empty."));
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        return FromResult(await _operationsService.UploadPortalApplicationDocumentAsync(
            jobApplicationId,
            documentType ?? "Resume",
            file.FileName,
            file.ContentType,
            buffer.ToArray(),
            cancellationToken));
    }

    [HttpPost("portal/profile/documents")]
    [RequestSizeLimit(5_500_000)]
    public async Task<ActionResult<PortalUploadCandidateProfileDocumentResult>> UploadPortalProfileDocument(
        [FromForm] IFormFile file,
        [FromForm] string? documentType,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return FromResult(Result<PortalUploadCandidateProfileDocumentResult>.Failure(
                "portal_profile_document.empty_file",
                "Uploaded document is empty."));
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        return FromResult(await _operationsService.UploadPortalCandidateProfileDocumentAsync(
            documentType ?? "Resume",
            file.FileName,
            file.ContentType,
            buffer.ToArray(),
            cancellationToken));
    }

    [HttpGet("portal/profile/documents/{candidateProfileDocumentId:guid}/download")]
    public async Task<IActionResult> DownloadPortalProfileDocument(
        Guid candidateProfileDocumentId,
        CancellationToken cancellationToken)
    {
        var result = await _operationsService.DownloadPortalCandidateProfileDocumentAsync(
            candidateProfileDocumentId,
            cancellationToken);

        if (result.Failed)
        {
            return FromResult(result).Result ?? BadRequest(new
            {
                error = result.Error.Code,
                message = result.Error.Message
            });
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpGet("portal/my-applications")]
    public async Task<ActionResult<PortalMyApplications>> PortalMyApplications(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetPortalMyApplicationsAsync(cancellationToken));
    }

    [HttpGet("portal/profile")]
    public async Task<ActionResult<PortalCandidateProfile>> PortalProfile(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetPortalCandidateProfileAsync(cancellationToken));
    }

    [HttpPut("portal/profile")]
    public async Task<ActionResult<PortalCandidateProfile>> UpdatePortalProfile(
        UpdatePortalCandidateProfileInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.UpdatePortalCandidateProfileAsync(input, cancellationToken));
    }

    [HttpPost("job-requests")]
    public async Task<ActionResult<CreateOperationsJobRequestResult>> CreateJobRequest(
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.CreateJobRequestAsync(input, cancellationToken));
    }

    [HttpPost("job-requests/description-draft")]
    public async Task<ActionResult<DraftJobDescriptionResult>> DraftJobDescription(
        DraftJobDescriptionInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.DraftJobDescriptionAsync(input, cancellationToken));
    }

    [HttpPost("workflow-assignments/{assignmentId:guid}/claim")]
    public async Task<IActionResult> ClaimAssignment(Guid assignmentId, CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ClaimAssignmentAsync(assignmentId, cancellationToken));
    }

    [HttpPost("job-requests/{entityId:guid}/bench-matches/rank")]
    public async Task<ActionResult<RankBenchMatchesResult>> RankBenchMatches(
        Guid entityId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.RankBenchMatchesAsync(entityId, cancellationToken));
    }

    [HttpPost("job-requests/{entityId:guid}/talent-rediscovery/rank")]
    public async Task<ActionResult<RankTalentRediscoveryResult>> RankTalentRediscovery(
        Guid entityId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.RankTalentRediscoveryAsync(entityId, cancellationToken));
    }

    [HttpPost("job-posts/{jobPostId:guid}/applicant-rankings/rank")]
    public async Task<ActionResult<RankApplicantRankingsResult>> RankApplicantRankings(
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.RankApplicantRankingsAsync(jobPostId, cancellationToken));
    }

    [HttpPost("job-requests/{entityId:guid}/online-headhunting/search")]
    public async Task<ActionResult<OperationsOnlineHeadhuntingQueuedResult>> SearchOnlineCandidates(
        Guid entityId,
        OnlineHeadhuntingSearchInput input,
        CancellationToken cancellationToken)
    {
        var result = await _operationsService.SearchOnlineCandidatesAsync(entityId, input, cancellationToken);
        return result.Succeeded
            ? Accepted(result.Value)
            : FromResult(result);
    }

    [HttpPatch("online-headhunting/leads/{onlineCandidateLeadId:guid}/status")]
    public async Task<ActionResult<OperationsOnlineCandidateLead>> UpdateOnlineCandidateLeadStatus(
        Guid onlineCandidateLeadId,
        UpdateOnlineCandidateLeadStatusInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.UpdateOnlineCandidateLeadStatusAsync(onlineCandidateLeadId, input, cancellationToken));
    }

    [HttpPost("job-requests/{entityId:guid}/candidate-invitations")]
    public async Task<ActionResult<SendCandidateInvitationsResult>> SendCandidateInvitations(
        Guid entityId,
        SendCandidateInvitationsInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.SendCandidateInvitationsAsync(entityId, input, cancellationToken));
    }

    [HttpPost("job-posts/{jobPostId:guid}/manual-candidates")]
    public async Task<ActionResult<AddManualCandidateResult>> AddManualCandidate(
        Guid jobPostId,
        AddManualCandidateInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.AddManualCandidateToJobPostAsync(jobPostId, input, cancellationToken));
    }

    [HttpPost("candidates/cv-parse")]
    [RequestSizeLimit(2_500_000)]
    public async Task<ActionResult<ParseCandidateCvResult>> ParseCandidateCv(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return FromResult(Result<ParseCandidateCvResult>.Failure("cv_parser.empty_file", "Uploaded CV is empty."));
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        return FromResult(await _operationsService.ParseCandidateCvAsync(
            file.FileName,
            buffer.ToArray(),
            cancellationToken));
    }

    [HttpPost("job-applications/{jobApplicationId:guid}/screening-decision")]
    public async Task<ActionResult<OperationsRecruiterApplication>> UpdateCandidateApplicationStatus(
        Guid jobApplicationId,
        UpdateCandidateApplicationStatusInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.UpdateCandidateApplicationStatusAsync(jobApplicationId, input, cancellationToken));
    }

    [HttpPost("job-applications/{jobApplicationId:guid}/interviews")]
    public async Task<ActionResult<ScheduleCandidateInterviewResult>> ScheduleCandidateInterview(
        Guid jobApplicationId,
        ScheduleCandidateInterviewInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ScheduleCandidateInterviewAsync(jobApplicationId, input, cancellationToken));
    }

    [HttpGet("interviews/my-tasks")]
    public async Task<ActionResult<OperationsInterviewTaskList>> MyInterviewTasks(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetMyInterviewTasksAsync(cancellationToken));
    }

    [HttpGet("interviews/{interviewId:guid}/question-recommendations")]
    public async Task<ActionResult<InterviewQuestionRecommendationSet>> GetInterviewQuestionRecommendations(
        Guid interviewId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetLatestInterviewQuestionRecommendationsAsync(interviewId, cancellationToken));
    }

    [HttpPost("interviews/{interviewId:guid}/question-recommendations/generate")]
    public async Task<ActionResult<InterviewQuestionRecommendationSet>> GenerateInterviewQuestionRecommendations(
        Guid interviewId,
        GenerateInterviewQuestionRecommendationsInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GenerateInterviewQuestionRecommendationsAsync(interviewId, input, cancellationToken));
    }

    [HttpGet("interviews/{interviewId:guid}/question-recommendations/download")]
    public async Task<IActionResult> DownloadInterviewQuestionRecommendations(
        Guid interviewId,
        CancellationToken cancellationToken)
    {
        var result = await _operationsService.DownloadInterviewQuestionRecommendationsDocxAsync(interviewId, cancellationToken);
        if (result.Failed)
        {
            return FromResult(result).Result ?? BadRequest(new
            {
                error = result.Error.Code,
                message = result.Error.Message
            });
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost("interviews/{interviewId:guid}/feedback")]
    public async Task<ActionResult<SubmitInterviewFeedbackResult>> SubmitInterviewFeedback(
        Guid interviewId,
        SubmitInterviewFeedbackInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.SubmitInterviewFeedbackAsync(interviewId, input, cancellationToken));
    }

    [HttpPost("job-applications/{jobApplicationId:guid}/forward-to-hiring-manager")]
    public async Task<ActionResult<ForwardToHiringManagerResult>> ForwardToHiringManager(
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ForwardToHiringManagerAsync(jobApplicationId, cancellationToken));
    }

    [HttpGet("hiring-manager/reviews")]
    public async Task<ActionResult<HiringManagerReviewList>> HiringManagerReviews(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetHiringManagerReviewsAsync(cancellationToken));
    }

    [HttpGet("job-applications/{jobApplicationId:guid}/hiring-review")]
    public async Task<ActionResult<HiringReviewDetail>> HiringReview(
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetHiringReviewAsync(jobApplicationId, cancellationToken));
    }

    [HttpGet("job-requests/{jobRequestId:guid}/reporting-manager-options")]
    public async Task<ActionResult<ReportingManagerOptionList>> ReportingManagerOptions(
        Guid jobRequestId,
        [FromQuery] string? search,
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.SearchReportingManagerOptionsAsync(
            jobRequestId,
            search,
            skip,
            take,
            cancellationToken));
    }

    [HttpPost("job-applications/{jobApplicationId:guid}/offer-letter")]
    public async Task<ActionResult<OfferLetterDetails>> GenerateOfferLetter(
        Guid jobApplicationId,
        GenerateOfferLetterInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GenerateOfferLetterAsync(jobApplicationId, input, cancellationToken));
    }

    [HttpPut("offer-letters/{offerLetterId:guid}")]
    public async Task<ActionResult<OfferLetterDetails>> UpdateOfferLetter(
        Guid offerLetterId,
        UpdateOfferLetterInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.UpdateOfferLetterAsync(offerLetterId, input, cancellationToken));
    }

    [HttpPost("offer-letters/{offerLetterId:guid}/presentation-meeting")]
    public async Task<ActionResult<OfferPresentationMeetingDetails>> ScheduleOfferPresentationMeeting(
        Guid offerLetterId,
        ScheduleOfferPresentationMeetingInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ScheduleOfferPresentationMeetingAsync(offerLetterId, input, cancellationToken));
    }

    [HttpPost("job-applications/{jobApplicationId:guid}/hiring-outcome")]
    public async Task<ActionResult<HiringOutcomeResult>> RecordHiringOutcome(
        Guid jobApplicationId,
        HiringOutcomeInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.RecordHiringOutcomeAsync(jobApplicationId, input, cancellationToken));
    }

    [HttpPost("job-requests/{entityId:guid}/close")]
    public async Task<IActionResult> CloseJobRequest(
        Guid entityId,
        CloseJobRequestInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.CloseJobRequestAsync(entityId, input, cancellationToken));
    }

    [HttpPost("job-requests/{entityId:guid}/employee-referrals")]
    public async Task<IActionResult> CreateEmployeeReferrals(
        Guid entityId,
        CreateEmployeeReferralsInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.CreateEmployeeReferralsAsync(entityId, input, cancellationToken));
    }

    [HttpPost("job-requests/{entityId:guid}/forward-to-recruiters")]
    public async Task<IActionResult> ForwardToRecruiters(Guid entityId, CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.ForwardToRecruitersAsync(entityId, cancellationToken));
    }

    [HttpPost("job-requests/{entityId:guid}/job-posts")]
    public async Task<ActionResult<OperationsJobPost>> CreateJobPost(
        Guid entityId,
        CreateJobPostInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.CreateJobPostAsync(entityId, input, cancellationToken));
    }

    [HttpPut("job-posts/{jobPostId:guid}")]
    public async Task<ActionResult<OperationsJobPost>> UpdateJobPost(
        Guid jobPostId,
        UpdateJobPostInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.UpdateJobPostAsync(jobPostId, input, cancellationToken));
    }

    [HttpPost("job-posts/{jobPostId:guid}/publish")]
    public async Task<ActionResult<OperationsJobPost>> PublishJobPost(
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.PublishJobPostAsync(jobPostId, cancellationToken));
    }

    [HttpPost("job-posts/{jobPostId:guid}/close")]
    public async Task<ActionResult<OperationsJobPost>> CloseJobPost(
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.CloseJobPostAsync(jobPostId, cancellationToken));
    }

    [HttpPost("job-requests/{entityId:guid}/employee-referrals/decision")]
    public async Task<IActionResult> DecideEmployeeReferrals(
        Guid entityId,
        EmployeeReferralDecisionInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.DecideEmployeeReferralsAsync(entityId, input, cancellationToken));
    }

    [HttpPatch("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid notificationId, CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.MarkNotificationReadAsync(notificationId, cancellationToken));
    }

    [HttpPatch("notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.MarkAllNotificationsReadAsync(cancellationToken));
    }
}
