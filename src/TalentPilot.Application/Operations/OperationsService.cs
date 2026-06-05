using System.Security.Cryptography;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Ai;
using TalentPilot.Application.Calendar;
using TalentPilot.Application.Documents;
using TalentPilot.Application.Notifications;
using TalentPilot.Common.Results;
using TalentPilot.Domain.Access;

namespace TalentPilot.Application.Operations;

public sealed class OperationsService : IOperationsService
{
    private static readonly TimeSpan InterviewQuestionGenerationTimeout = TimeSpan.FromMinutes(12);

    private readonly IOperationsRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IRealtimeNotificationPublisher _notificationPublisher;
    private readonly IJobDescriptionDraftingAgent _jobDescriptionDraftingAgent;
    private readonly ICvParserAgent _cvParserAgent;
    private readonly IBenchMatchingAgent _benchMatchingAgent;
    private readonly ITalentRediscoveryAgent _talentRediscoveryAgent;
    private readonly IApplicantRankingAgent _applicantRankingAgent;
    private readonly IOnlineHeadhuntingAgent _onlineHeadhuntingAgent;
    private readonly IOnlineHeadhuntingJobQueue _onlineHeadhuntingJobQueue;
    private readonly IInterviewQuestionRecommendationAgent _interviewQuestionRecommendationAgent;
    private readonly IDocumentExportService _documentExportService;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly IAiRuntimeSettingsResolver _aiRuntimeSettingsResolver;
    private readonly IAiAgentRunLogger _aiRunLogger;
    private readonly IApplicationDocumentStorage _applicationDocumentStorage;
    private readonly IApplicationDocumentTextExtractor _documentTextExtractor;
    private readonly ICalendarMeetingService _calendarMeetingService;

    public OperationsService(
        IOperationsRepository repository,
        ICurrentUserAccessor currentUser,
        IRealtimeNotificationPublisher notificationPublisher,
        IJobDescriptionDraftingAgent jobDescriptionDraftingAgent,
        ICvParserAgent cvParserAgent,
        IBenchMatchingAgent benchMatchingAgent,
        ITalentRediscoveryAgent talentRediscoveryAgent,
        IApplicantRankingAgent applicantRankingAgent,
        IOnlineHeadhuntingAgent onlineHeadhuntingAgent,
        IOnlineHeadhuntingJobQueue onlineHeadhuntingJobQueue,
        IInterviewQuestionRecommendationAgent interviewQuestionRecommendationAgent,
        IDocumentExportService documentExportService,
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        IAiRuntimeSettingsResolver aiRuntimeSettingsResolver,
        IAiAgentRunLogger aiRunLogger,
        IApplicationDocumentStorage applicationDocumentStorage,
        IApplicationDocumentTextExtractor documentTextExtractor,
        ICalendarMeetingService calendarMeetingService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _notificationPublisher = notificationPublisher;
        _jobDescriptionDraftingAgent = jobDescriptionDraftingAgent;
        _cvParserAgent = cvParserAgent;
        _benchMatchingAgent = benchMatchingAgent;
        _talentRediscoveryAgent = talentRediscoveryAgent;
        _applicantRankingAgent = applicantRankingAgent;
        _onlineHeadhuntingAgent = onlineHeadhuntingAgent;
        _onlineHeadhuntingJobQueue = onlineHeadhuntingJobQueue;
        _interviewQuestionRecommendationAgent = interviewQuestionRecommendationAgent;
        _documentExportService = documentExportService;
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _aiRuntimeSettingsResolver = aiRuntimeSettingsResolver;
        _aiRunLogger = aiRunLogger;
        _applicationDocumentStorage = applicationDocumentStorage;
        _documentTextExtractor = documentTextExtractor;
        _calendarMeetingService = calendarMeetingService;
    }

    public async Task<Result<OperationsSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _repository.GetSnapshotAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        return Result<OperationsSnapshot>.Success(snapshot);
    }

    public async Task<Result<TenantAdminDashboard>> GetTenantAdminDashboardAsync(
        TenantAdminDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!roleCodes.Contains(AccessConstants.TenantAdminRoleCode, StringComparer.OrdinalIgnoreCase))
        {
            return Result<TenantAdminDashboard>.Failure(
                "tenant_admin_dashboard.forbidden",
                "Only Tenant Admin users can view tenant analytics.");
        }

        var toUtc = query.ToUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        var fromUtc = query.FromUtc?.ToUniversalTime() ?? toUtc.AddDays(-30);
        if (fromUtc > toUtc)
        {
            return Result<TenantAdminDashboard>.Failure(
                "tenant_admin_dashboard.invalid_range",
                "The from date must be earlier than the to date.");
        }

        var normalizedQuery = query with
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            SourceLabel = string.IsNullOrWhiteSpace(query.SourceLabel) ? null : query.SourceLabel.Trim()
        };

        var dashboard = await _repository.GetTenantAdminDashboardAsync(
            _currentUser.TenantId,
            normalizedQuery,
            cancellationToken);
        return Result<TenantAdminDashboard>.Success(dashboard);
    }

    public async Task<Result<PmoDashboard>> GetPmoDashboardAsync(
        PmoDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUsePmoReview(roleCodes))
        {
            return Result<PmoDashboard>.Failure(
                "pmo_dashboard.forbidden",
                "Only PMO or Tenant Admin users can view PMO dashboard analytics.");
        }

        var toUtc = query.ToUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        var fromUtc = query.FromUtc?.ToUniversalTime() ?? toUtc.AddDays(-30);
        if (fromUtc > toUtc)
        {
            return Result<PmoDashboard>.Failure(
                "pmo_dashboard.invalid_range",
                "The from date must be earlier than the to date.");
        }

        var dashboard = await _repository.GetPmoDashboardAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            query with { FromUtc = fromUtc, ToUtc = toUtc },
            cancellationToken);
        return Result<PmoDashboard>.Success(dashboard);
    }

    public async Task<Result<HiringManagerDashboard>> GetHiringManagerDashboardAsync(CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseHiringManagerReview(roleCodes))
        {
            return Result<HiringManagerDashboard>.Failure(
                "hiring_manager_dashboard.forbidden",
                "Only Hiring Manager or Tenant Admin users can view Hiring Manager dashboard analytics.");
        }

        var dashboard = await _repository.GetHiringManagerDashboardAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode, StringComparer.OrdinalIgnoreCase),
            cancellationToken);
        return Result<HiringManagerDashboard>.Success(dashboard);
    }

    public async Task<Result<OperationsJobRequestIntakeOptions>> GetIntakeOptionsAsync(CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanCreateJobRequests(roleCodes))
        {
            return Result<OperationsJobRequestIntakeOptions>.Failure(
                "job_request.create_forbidden",
                "You do not have permission to create Job Requests.");
        }

        var options = await _repository.GetIntakeOptionsAsync(_currentUser.TenantId, cancellationToken);
        return Result<OperationsJobRequestIntakeOptions>.Success(options);
    }

    public async Task<Result<IReadOnlyList<OperationsActivityEvent>>> GetActivityAsync(
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var activity = await _repository.GetActivityAsync(_currentUser.TenantId, _currentUser.UserId, entityId, cancellationToken);
        return Result<IReadOnlyList<OperationsActivityEvent>>.Success(activity);
    }

    public async Task<Result<OperationsPmoReview>> GetPmoReviewAsync(
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        var includeEmployees = CanUsePmoReview(roleCodes);
        var review = await _repository.GetPmoReviewAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            includeEmployees,
            cancellationToken);

        return review is null
            ? Result<OperationsPmoReview>.Failure("job_request.not_found", "Job Request was not found or is not visible.")
            : Result<OperationsPmoReview>.Success(review);
    }

    public async Task<Result<OperationsRecruitmentQueue>> GetRecruitmentQueueAsync(CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsRecruitmentQueue>.Failure(
                "recruitment_queue.forbidden",
                "Only Recruiter or Tenant Admin users can view recruiter sourcing work.");
        }

        var queue = await _repository.GetRecruitmentQueueAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        return Result<OperationsRecruitmentQueue>.Success(queue);
    }

    public async Task<Result<OperationsRecruiterSourcing>> GetRecruiterSourcingAsync(
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsRecruiterSourcing>.Failure(
                "recruiter_sourcing.forbidden",
                "Only Recruiter or Tenant Admin users can view recruiter sourcing.");
        }

        var sourcing = await _repository.GetRecruiterSourcingAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            cancellationToken);

        if (sourcing is null)
        {
            return Result<OperationsRecruiterSourcing>.Failure("recruiter_sourcing.not_found", "Recruiter sourcing work was not found or is not visible.");
        }

        var settings = await _aiRuntimeSettingsResolver.GetCurrentAsync(cancellationToken);
        return Result<OperationsRecruiterSourcing>.Success(sourcing with { ConfiguredAiModel = settings.LlmModel });
    }

    public async Task<Result<OperationsHistoricalApplicationDetail>> GetHistoricalApplicationAsync(
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsHistoricalApplicationDetail>.Failure(
                "historical_application.forbidden",
                "Only Recruiter or Tenant Admin users can view historical candidate applications.");
        }

        var detail = await _repository.GetHistoricalApplicationAsync(
            _currentUser.TenantId,
            jobApplicationId,
            cancellationToken);

        return detail is null
            ? Result<OperationsHistoricalApplicationDetail>.Failure(
                "historical_application.not_found",
                "Historical application was not found or is not visible.")
            : Result<OperationsHistoricalApplicationDetail>.Success(detail);
    }

    public async Task<Result<OperationsApplicationDocumentDownload>> DownloadRecruiterApplicationDocumentAsync(
        Guid jobApplicationId,
        Guid applicationDocumentId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsApplicationDocumentDownload>.Failure(
                "application_document.forbidden",
                "Only Recruiter or Tenant Admin users can download application documents.");
        }

        var document = await _repository.GetRecruiterApplicationDocumentAsync(
            _currentUser.TenantId,
            jobApplicationId,
            applicationDocumentId,
            cancellationToken);
        if (document is null)
        {
            return Result<OperationsApplicationDocumentDownload>.Failure(
                "application_document.not_found",
                "Application document was not found.");
        }

        var content = await _applicationDocumentStorage.ReadAsync(
            document.StorageProvider,
            document.StorageKey,
            document.StorageContainer,
            cancellationToken);
        if (content is null || content.Length == 0)
        {
            return Result<OperationsApplicationDocumentDownload>.Failure(
                "application_document.unavailable",
                "Application document file is not available in storage.");
        }

        return Result<OperationsApplicationDocumentDownload>.Success(new OperationsApplicationDocumentDownload(
            document.ApplicationDocumentId,
            jobApplicationId,
            SanitizeDownloadFileName(document.FileName, document.DocumentType),
            string.IsNullOrWhiteSpace(document.ContentType) ? "application/octet-stream" : document.ContentType,
            content));
    }

    public async Task<Result<OperationsCandidateProfile>> GetCandidateProfileAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsCandidateProfile>.Failure(
                "candidate_profile.forbidden",
                "Only Recruiter or Tenant Admin users can view candidate profiles.");
        }

        var profile = await _repository.GetCandidateProfileAsync(
            _currentUser.TenantId,
            candidateId,
            cancellationToken);

        return profile is null
            ? Result<OperationsCandidateProfile>.Failure(
                "candidate_profile.not_found",
                "Candidate profile was not found or is not visible.")
            : Result<OperationsCandidateProfile>.Success(profile);
    }

    public async Task<Result<OperationsJobPublishing>> GetJobPublishingAsync(CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsJobPublishing>.Failure(
                "job_publishing.forbidden",
                "Only Recruiter or Tenant Admin users can view job publishing.");
        }

        var publishing = await _repository.GetJobPublishingAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        return Result<OperationsJobPublishing>.Success(publishing);
    }

    public async Task<Result<PortalJobPostList>> ListPortalJobPostsAsync(CancellationToken cancellationToken)
    {
        var posts = await _repository.ListPortalJobPostsAsync(cancellationToken);
        return Result<PortalJobPostList>.Success(posts);
    }

    public async Task<Result<PortalJobPostDetail>> GetPortalJobPostAsync(
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        var post = await _repository.GetPortalJobPostAsync(jobPostId, cancellationToken);
        return post is null
            ? Result<PortalJobPostDetail>.Failure("portal_job_post.not_found", "Published job post was not found.")
            : Result<PortalJobPostDetail>.Success(post);
    }

    public async Task<Result<PortalInvitationContext>> GetPortalInvitationAsync(
        Guid candidateInvitationId,
        string token,
        CancellationToken cancellationToken)
    {
        if (candidateInvitationId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            return Result<PortalInvitationContext>.Failure(
                "portal_invitation.invalid",
                "Invitation link is missing its tracking details.");
        }

        var invitation = await _repository.GetPortalInvitationAsync(
            candidateInvitationId,
            token.Trim(),
            cancellationToken);

        return invitation is null
            ? Result<PortalInvitationContext>.Failure(
                "portal_invitation.not_found",
                "Invitation link was not found or is no longer valid.")
            : Result<PortalInvitationContext>.Success(invitation);
    }

    public async Task<Result<PortalJobApplicationResult>> ApplyToPortalJobPostAsync(
        Guid jobPostId,
        PortalApplyToJobPostInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseCandidatePortal(roleCodes))
        {
            return Result<PortalJobApplicationResult>.Failure(
                "portal_application.forbidden",
                "Log in with a candidate account before applying.");
        }

        if (input.ExperienceYears.HasValue && input.ExperienceYears.Value < 0)
        {
            return Result<PortalJobApplicationResult>.Failure(
                "portal_application.experience_invalid",
                "Experience cannot be negative.");
        }

        if (input.NoticePeriodDays.HasValue && input.NoticePeriodDays.Value < 0)
        {
            return Result<PortalJobApplicationResult>.Failure(
                "portal_application.notice_invalid",
                "Notice period cannot be negative.");
        }

        if (input.InterviewAvailabilityStartDate.HasValue != input.InterviewAvailabilityEndDate.HasValue)
        {
            return Result<PortalJobApplicationResult>.Failure(
                "portal_application.interview_availability_incomplete",
                "Choose both interview availability dates.");
        }

        if (input.InterviewAvailabilityStartDate.HasValue &&
            input.InterviewAvailabilityEndDate.HasValue &&
            input.InterviewAvailabilityEndDate.Value < input.InterviewAvailabilityStartDate.Value)
        {
            return Result<PortalJobApplicationResult>.Failure(
                "portal_application.interview_availability_invalid",
                "Interview availability end date must be on or after the start date.");
        }

        if (input.CandidateInvitationId.HasValue != !string.IsNullOrWhiteSpace(input.InvitationToken))
        {
            return Result<PortalJobApplicationResult>.Failure(
                "portal_application.invitation_incomplete",
                "Invitation id and token must be submitted together.");
        }

        if (input.CandidateInvitationId == Guid.Empty)
        {
            return Result<PortalJobApplicationResult>.Failure(
                "portal_application.invitation_invalid",
                "Invitation id is invalid.");
        }

        var result = await _repository.ApplyToPortalJobPostAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobPostId,
            input,
            cancellationToken);

        return result is null
            ? Result<PortalJobApplicationResult>.Failure(
                "portal_application.not_found",
                "Published job post was not found for this candidate tenant.")
            : Result<PortalJobApplicationResult>.Success(result);
    }

    public async Task<Result<PortalUploadApplicationDocumentResult>> UploadPortalApplicationDocumentAsync(
        Guid jobApplicationId,
        string documentType,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseCandidatePortal(roleCodes))
        {
            return Result<PortalUploadApplicationDocumentResult>.Failure(
                "portal_application_document.forbidden",
                "Only candidate portal users can upload application documents.");
        }

        if (content.Length == 0)
        {
            return Result<PortalUploadApplicationDocumentResult>.Failure(
                "portal_application_document.empty_file",
                "Uploaded document is empty.");
        }

        if (content.Length > 5_000_000)
        {
            return Result<PortalUploadApplicationDocumentResult>.Failure(
                "portal_application_document.file_too_large",
                "Application documents can be up to 5 MB for MVP.");
        }

        if (!string.Equals(Path.GetExtension(fileName), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return Result<PortalUploadApplicationDocumentResult>.Failure(
                "portal_application_document.docx_required",
                "Upload a DOCX document for MVP.");
        }

        var context = await _repository.GetPortalApplicationDocumentUploadContextAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobApplicationId,
            cancellationToken);
        if (context is null)
        {
            return Result<PortalUploadApplicationDocumentResult>.Failure(
                "portal_application_document.not_found",
                "Application was not found for the current candidate.");
        }

        var extraction = _documentTextExtractor.Extract(fileName, content);
        var storedDocument = await _applicationDocumentStorage.SaveAsync(
            new StoreApplicationDocumentRequest(
                _currentUser.TenantId,
                jobApplicationId,
                fileName,
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                content),
            cancellationToken);

        var document = await _repository.AddPortalApplicationDocumentAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobApplicationId,
            new PortalApplicationDocumentMetadataInput(
                string.IsNullOrWhiteSpace(documentType) ? "Resume" : documentType.Trim(),
                fileName,
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                storedDocument.SizeBytes,
                storedDocument.StorageProvider,
                storedDocument.StorageKey,
                storedDocument.StorageContainer,
                storedDocument.ContentHashSha256,
                extraction.Status,
                extraction.ExtractedText,
                extraction.ExtractedTextHashSha256,
                extraction.ParserVersion,
                extraction.ExtractedAtUtc,
                extraction.Error),
            cancellationToken);

        if (document is not null)
        {
            await TryUpsertApplicationEvidenceVectorAsync(
                _currentUser.TenantId,
                jobApplicationId,
                document.DocumentType,
                document.FileName,
                extraction,
                cancellationToken);
        }

        return document is null
            ? Result<PortalUploadApplicationDocumentResult>.Failure(
                "portal_application_document.not_saved",
                "Application document could not be saved.")
            : Result<PortalUploadApplicationDocumentResult>.Success(new PortalUploadApplicationDocumentResult(document));
    }

    public async Task<Result<PortalMyApplications>> GetPortalMyApplicationsAsync(CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseCandidatePortal(roleCodes))
        {
            return Result<PortalMyApplications>.Failure(
                "portal_applications.forbidden",
                "Log in with a candidate account to view applications.");
        }

        var applications = await _repository.GetPortalMyApplicationsAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            cancellationToken);
        return Result<PortalMyApplications>.Success(applications);
    }

    public async Task<Result<PortalCandidateProfile>> GetPortalCandidateProfileAsync(CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseCandidatePortal(roleCodes))
        {
            return Result<PortalCandidateProfile>.Failure(
                "portal_profile.forbidden",
                "Log in with a candidate account to manage your candidate profile.");
        }

        var profile = await _repository.GetPortalCandidateProfileAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            cancellationToken);
        return profile is null
            ? Result<PortalCandidateProfile>.Failure(
                "portal_profile.not_found",
                "Candidate profile identity was not found.")
            : Result<PortalCandidateProfile>.Success(profile);
    }

    public async Task<Result<PortalCandidateProfile>> UpdatePortalCandidateProfileAsync(
        UpdatePortalCandidateProfileInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseCandidatePortal(roleCodes))
        {
            return Result<PortalCandidateProfile>.Failure(
                "portal_profile.forbidden",
                "Log in with a candidate account to manage your candidate profile.");
        }

        var validation = ValidatePortalCandidateProfileInput(input);
        if (validation is not null)
        {
            return Result<PortalCandidateProfile>.Failure(validation.Value.Code, validation.Value.Message);
        }

        var normalized = NormalizePortalCandidateProfileInput(input);
        var profile = await _repository.UpdatePortalCandidateProfileAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            normalized,
            cancellationToken);
        if (profile is null)
        {
            return Result<PortalCandidateProfile>.Failure(
                "portal_profile.not_saved",
                "Candidate profile could not be saved.");
        }

        await TryUpsertCandidateProfileVectorAsync(_currentUser.TenantId, profile, cancellationToken);
        return Result<PortalCandidateProfile>.Success(profile);
    }

    private async Task TryUpsertApplicationEvidenceVectorAsync(
        Guid tenantId,
        Guid jobApplicationId,
        string documentType,
        string fileName,
        ApplicationDocumentTextExtractionResult extraction,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(extraction.ExtractedText))
        {
            return;
        }

        try
        {
            var settings = await _aiRuntimeSettingsResolver.GetCurrentAsync(cancellationToken);
            var profileText = string.Join('\n', new[]
            {
                $"ApplicationId: {jobApplicationId}",
                $"Evidence source: {documentType}",
                $"Document: {fileName}",
                $"Parser: {extraction.ParserVersion}",
                $"Extracted text: {extraction.ExtractedText}"
            });
            var sourceHash = AiTextHasher.HashText(profileText);
            var existingHash = await _vectorStore.GetActiveSourceTextHashAsync(
                tenantId,
                "JobApplication",
                jobApplicationId,
                "JobApplicationEvidenceProfile",
                settings.EmbeddingModel,
                cancellationToken);
            if (string.Equals(existingHash, sourceHash, StringComparison.Ordinal))
            {
                return;
            }

            var embedding = await _embeddingProvider.GenerateEmbeddingAsync(profileText, cancellationToken);
            if (embedding.Length != settings.EmbeddingDimensions)
            {
                return;
            }

            await _vectorStore.UpsertAsync(
                new VectorRecord(
                    tenantId,
                    "JobApplication",
                    jobApplicationId,
                    "JobApplicationEvidenceProfile",
                    sourceHash,
                    settings.EmbeddingModel,
                    settings.EmbeddingDimensions,
                    embedding),
                cancellationToken);
        }
        catch
        {
            // Document upload should not fail just because semantic indexing is temporarily unavailable.
        }
    }

    private async Task TryUpsertManualCandidateCvEvidenceVectorAsync(
        Guid tenantId,
        Guid jobApplicationId,
        ParsedCandidateCvEvidenceInput? evidence,
        CancellationToken cancellationToken)
    {
        if (evidence is null || string.IsNullOrWhiteSpace(evidence.ExtractedText))
        {
            return;
        }

        var evidenceText = BuildParsedCvEvidenceText(evidence);
        if (string.IsNullOrWhiteSpace(evidenceText))
        {
            return;
        }

        await TryUpsertApplicationEvidenceVectorAsync(
            tenantId,
            jobApplicationId,
            "CV",
            evidence.FileName,
            new ApplicationDocumentTextExtractionResult(
                "Extracted",
                evidenceText,
                AiTextHasher.HashText(evidenceText),
                BuildCvParserVersion(evidence.Model),
                evidence.ParsedAtUtc ?? DateTimeOffset.UtcNow,
                null),
            cancellationToken);
    }

    private async Task TryUpsertCandidateProfileVectorAsync(
        Guid tenantId,
        PortalCandidateProfile profile,
        CancellationToken cancellationToken)
    {
        if (!profile.CandidateId.HasValue)
        {
            return;
        }

        var profileText = BuildCandidateProfileVectorText(profile);
        if (string.IsNullOrWhiteSpace(profileText))
        {
            return;
        }

        try
        {
            var settings = await _aiRuntimeSettingsResolver.GetCurrentAsync(cancellationToken);
            var sourceHash = AiTextHasher.HashText(profileText);
            var existingHash = await _vectorStore.GetActiveSourceTextHashAsync(
                tenantId,
                "Candidate",
                profile.CandidateId.Value,
                "CandidateProfile",
                settings.EmbeddingModel,
                cancellationToken);
            if (string.Equals(existingHash, sourceHash, StringComparison.Ordinal))
            {
                return;
            }

            var embedding = await _embeddingProvider.GenerateEmbeddingAsync(profileText, cancellationToken);
            if (embedding.Length != settings.EmbeddingDimensions)
            {
                return;
            }

            await _vectorStore.UpsertAsync(
                new VectorRecord(
                    tenantId,
                    "Candidate",
                    profile.CandidateId.Value,
                    "CandidateProfile",
                    sourceHash,
                    settings.EmbeddingModel,
                    settings.EmbeddingDimensions,
                    embedding),
                cancellationToken);
        }
        catch
        {
            // Candidate profile saves must not fail because profile semantic indexing is temporarily unavailable.
        }
    }

    private static string BuildCandidateProfileVectorText(PortalCandidateProfile profile)
    {
        var lines = new List<string>
        {
            $"CandidateId: {profile.CandidateId}",
            $"Name: {profile.DisplayName}",
            $"Email: {profile.Email}"
        };

        AddProfileLine(lines, "Phone", profile.Phone);
        AddProfileLine(lines, "LinkedIn", profile.LinkedInUrl);
        AddProfileLine(lines, "Current title", profile.CurrentDesignation);
        AddProfileLine(lines, "Current company", profile.CurrentCompany);
        AddProfileLine(lines, "Experience years", profile.ExperienceYears?.ToString("0.#"));
        AddProfileLine(lines, "Notice period days", profile.NoticePeriodDays?.ToString());
        AddProfileLine(lines, "Expected salary", FormatSalary(profile.ExpectedSalaryAmount, profile.ExpectedSalaryCurrency));

        if (profile.PrimaryEducation is not null)
        {
            AddProfileLine(lines, "Primary institute", profile.PrimaryEducation.UniversityName);
            AddProfileLine(lines, "Degree", profile.PrimaryEducation.DegreeName);
            AddProfileLine(lines, "Graduation year", profile.PrimaryEducation.GraduationYear?.ToString());
        }

        if (profile.CurrentWorkHistory is not null)
        {
            AddProfileLine(lines, "Current work company", profile.CurrentWorkHistory.CompanyName);
            AddProfileLine(lines, "Current work title", profile.CurrentWorkHistory.Title);
        }

        if (profile.Skills.Count > 0)
        {
            lines.Add("Skills:");
            foreach (var skill in profile.Skills.OrderByDescending(skill => skill.IsPrimary).ThenBy(skill => skill.SkillName))
            {
                var years = skill.YearsExperience.HasValue ? $" ({skill.YearsExperience:0.#} years)" : string.Empty;
                lines.Add($"- {skill.SkillName}: {skill.SkillLevel}{years}");
            }
        }

        return string.Join('\n', lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static void AddProfileLine(List<string> lines, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value.Trim()}");
        }
    }

    private static string? FormatSalary(decimal? amount, string? currency)
    {
        if (!amount.HasValue)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(currency)
            ? amount.Value.ToString("0.##")
            : $"{amount.Value:0.##} {currency.Trim().ToUpperInvariant()}";
    }

    public async Task<Result<CreateOperationsJobRequestResult>> CreateJobRequestAsync(
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.title_required", "Title is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Description))
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.description_required", "Description is required.");
        }

        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanCreateJobRequests(roleCodes))
        {
            return Result<CreateOperationsJobRequestResult>.Failure(
                "job_request.create_forbidden",
                "Only Presales, PMO, or Tenant Admin users can create Job Requests.");
        }

        if (input.DepartmentId == Guid.Empty)
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.department_required", "Department is required.");
        }

        if (input.LocationId == Guid.Empty)
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.location_required", "Location is required.");
        }

        if (input.HiringManagerId == Guid.Empty)
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.hiring_manager_required", "Hiring manager is required.");
        }

        var skillIds = input.SkillIds ?? Array.Empty<Guid>();
        if (skillIds.Count == 0)
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.skills_required", "At least one skill is required.");
        }

        if (input.ExperienceMinYears.HasValue && input.ExperienceMinYears.Value < 0)
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.experience_invalid", "Minimum experience cannot be negative.");
        }

        if (input.ExperienceMaxYears.HasValue && input.ExperienceMaxYears.Value < 0)
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.experience_invalid", "Maximum experience cannot be negative.");
        }

        if (input.ExperienceMinYears.HasValue &&
            input.ExperienceMaxYears.HasValue &&
            input.ExperienceMinYears.Value > input.ExperienceMaxYears.Value)
        {
            return Result<CreateOperationsJobRequestResult>.Failure(
                "job_request.experience_invalid",
                "Minimum experience cannot be greater than maximum experience.");
        }

        if (input.RequiredPositions < 1)
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.positions_required", "At least one required position is needed.");
        }

        var validation = await _repository.ValidateCreateJobRequestAsync(_currentUser.TenantId, input, cancellationToken);
        if (!validation.DepartmentExists)
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.department_invalid", "Department must be active in this tenant.");
        }

        if (!validation.LocationExists)
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.location_invalid", "Location must be active in this tenant.");
        }

        if (!validation.HiringManagerExists)
        {
            return Result<CreateOperationsJobRequestResult>.Failure(
                "job_request.hiring_manager_invalid",
                "Hiring manager must be an active tenant user with the Hiring Manager role.");
        }

        var activeSkillIds = validation.ActiveSkillIds.ToHashSet();
        if (skillIds.Any(skillId => !activeSkillIds.Contains(skillId)))
        {
            return Result<CreateOperationsJobRequestResult>.Failure("job_request.skills_invalid", "Every selected skill must be active in this tenant.");
        }

        var created = await _repository.CreateJobRequestAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            input,
            cancellationToken);

        await PublishNotificationsAsync(created.NotificationDispatches, cancellationToken);
        await TryIndexJobRequestDescriptionAsync(created.Result.JobRequest, cancellationToken);

        return Result<CreateOperationsJobRequestResult>.Success(created.Result);
    }

    public async Task<Result<DraftJobDescriptionResult>> DraftJobDescriptionAsync(
        DraftJobDescriptionInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanCreateJobRequests(roleCodes))
        {
            return Result<DraftJobDescriptionResult>.Failure(
                "job_description_draft.forbidden",
                "Only users who can create Job Requests can draft a job description.");
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.title_required", "Title is required before drafting.");
        }

        if (input.DepartmentId == Guid.Empty)
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.department_required", "Department is required before drafting.");
        }

        if (input.LocationId == Guid.Empty)
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.location_required", "Location is required before drafting.");
        }

        if (input.HiringManagerId == Guid.Empty)
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.hiring_manager_required", "Hiring manager is required before drafting.");
        }

        var skillIds = input.SkillIds ?? Array.Empty<Guid>();
        if (skillIds.Count == 0)
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.skills_required", "At least one skill is required before drafting.");
        }

        if (input.RequiredPositions < 1)
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.positions_required", "At least one required position is needed.");
        }

        if (input.ExperienceMinYears.HasValue && input.ExperienceMinYears.Value < 0)
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.experience_invalid", "Minimum experience cannot be negative.");
        }

        if (input.ExperienceMaxYears.HasValue && input.ExperienceMaxYears.Value < 0)
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.experience_invalid", "Maximum experience cannot be negative.");
        }

        if (input.ExperienceMinYears.HasValue &&
            input.ExperienceMaxYears.HasValue &&
            input.ExperienceMinYears.Value > input.ExperienceMaxYears.Value)
        {
            return Result<DraftJobDescriptionResult>.Failure(
                "job_description_draft.experience_invalid",
                "Minimum experience cannot be greater than maximum experience.");
        }

        var createValidationInput = new CreateOperationsJobRequestInput(
            input.Title,
            input.Client,
            "AI draft validation placeholder",
            input.DepartmentId,
            input.LocationId,
            skillIds,
            input.ExperienceMinYears,
            input.ExperienceMaxYears,
            input.RequiredPositions,
            input.Priority,
            input.HiringManagerId);
        var validation = await _repository.ValidateCreateJobRequestAsync(_currentUser.TenantId, createValidationInput, cancellationToken);
        if (!validation.DepartmentExists)
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.department_invalid", "Department must be active in this tenant.");
        }

        if (!validation.LocationExists)
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.location_invalid", "Location must be active in this tenant.");
        }

        if (!validation.HiringManagerExists)
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.hiring_manager_invalid", "Hiring manager must be active for this tenant.");
        }

        var activeSkillIds = validation.ActiveSkillIds.ToHashSet();
        if (skillIds.Any(skillId => !activeSkillIds.Contains(skillId)))
        {
            return Result<DraftJobDescriptionResult>.Failure("job_description_draft.skills_invalid", "Every selected skill must be active in this tenant.");
        }

        var options = await _repository.GetIntakeOptionsAsync(_currentUser.TenantId, cancellationToken);
        var department = options.Departments.First(item => item.DepartmentId == input.DepartmentId);
        var location = options.Locations.First(item => item.Id == input.LocationId);
        var hiringManager = options.HiringManagers.First(item => item.Id == input.HiringManagerId);
        var skillNames = options.Skills
            .Where(skill => skillIds.Contains(skill.Id))
            .Select(skill => skill.Name)
            .ToArray();

        try
        {
            var draft = await _jobDescriptionDraftingAgent.DraftAsync(
                _currentUser.TenantId,
                new JobDescriptionDraftRequest(
                    input.Title,
                    input.Client,
                    department.Name,
                    location.Name,
                    skillNames,
                    input.ExperienceMinYears,
                    input.ExperienceMaxYears,
                    input.RequiredPositions,
                    input.Priority,
                    hiringManager.Name),
                cancellationToken);

            return Result<DraftJobDescriptionResult>.Success(new DraftJobDescriptionResult(
                draft.Description,
                draft.AgentRunId,
                draft.Model,
                draft.GeneratedAtUtc));
        }
        catch
        {
            return Result<DraftJobDescriptionResult>.Failure(
                "job_description_draft.unavailable",
                "The Job Description Drafting Agent is unavailable. The current description was not changed.");
        }
    }

    public async Task<Result> ClaimAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var claimed = await _repository.ClaimAssignmentAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            assignmentId,
            cancellationToken);

        return claimed
            ? Result.Success()
            : Result.Failure("workflow_assignment.not_found", "Workflow assignment was not found or cannot be claimed.");
    }

    public async Task<Result<RankBenchMatchesResult>> RankBenchMatchesAsync(
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUsePmoReview(roleCodes))
        {
            return Result<RankBenchMatchesResult>.Failure(
                "bench_matching.forbidden",
                "Only PMO or Tenant Admin users can rank bench matches.");
        }

        var context = await _repository.GetBenchMatchingContextAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            cancellationToken);
        if (context is null)
        {
            return Result<RankBenchMatchesResult>.Failure(
                "bench_matching.not_claimed",
                "Claim the PMO Review work item before ranking internal employee matches.");
        }

        if (context.EligibleEmployees.Count == 0)
        {
            return Result<RankBenchMatchesResult>.Failure(
                "bench_matching.no_employees",
                "No eligible benched employees were found for this request.");
        }

        try
        {
            var ranked = await _benchMatchingAgent.RankAsync(_currentUser.TenantId, context, cancellationToken);
            var projectEvidence = context.EligibleEmployees.ToDictionary(employee => employee.EmployeeId, employee => employee.ProjectEvidence);
            var operationsMatches = ranked.Matches
                .Select(match => new OperationsBenchMatch(
                    match.EmployeeId,
                    match.Rank,
                    match.Score,
                    match.Confidence,
                    match.Explanation,
                    match.Strengths,
                    match.Gaps,
                    projectEvidence.TryGetValue(match.EmployeeId, out var projects) ? projects : [],
                    ranked.WebResearchStatus,
                    match.WebSummary,
                    match.WebSources
                        .Select(source => new OperationsBenchMatchWebSource(
                            source.Query,
                            source.Title,
                            source.Url,
                            source.Snippet))
                        .ToArray(),
                    ranked.AgentRunId,
                    ranked.GeneratedAtUtc))
                .ToArray();

            await _repository.SaveBenchMatchesAsync(
                _currentUser.TenantId,
                jobRequestId,
                ranked.AgentRunId,
                operationsMatches,
                cancellationToken);

            return Result<RankBenchMatchesResult>.Success(new RankBenchMatchesResult(
                operationsMatches,
                ranked.AgentRunId,
                ranked.Model,
                ranked.GeneratedAtUtc,
                ranked.WebResearchStatus));
        }
        catch
        {
            return Result<RankBenchMatchesResult>.Failure(
                "bench_matching.unavailable",
                "The Bench Matching Agent is unavailable. Manual PMO review was not changed.");
        }
    }

    public async Task<Result<RankTalentRediscoveryResult>> RankTalentRediscoveryAsync(
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<RankTalentRediscoveryResult>.Failure(
                "talent_rediscovery.forbidden",
                "Only Recruiter or Tenant Admin users can rank rediscovered candidates.");
        }

        var context = await _repository.GetTalentRediscoveryContextAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            cancellationToken);
        if (context is null)
        {
            return Result<RankTalentRediscoveryResult>.Failure(
                "talent_rediscovery.not_claimed",
                "Claim the Recruiter Sourcing work item before ranking rediscovered candidates.");
        }

        if (context.Candidates.Count == 0)
        {
            return Result<RankTalentRediscoveryResult>.Failure(
                "talent_rediscovery.no_candidates",
                "No previous warm candidates were found for this request.");
        }

        try
        {
            var ranked = await _talentRediscoveryAgent.RankAsync(_currentUser.TenantId, context, cancellationToken);
            var candidateEvidence = context.Candidates.ToDictionary(candidate => candidate.CandidateId);
            var operationsMatches = ranked.Matches
                .Select(match =>
                {
                    candidateEvidence.TryGetValue(match.CandidateId, out var candidate);
                    return new OperationsTalentRediscoveryMatch(
                        match.CandidateId,
                        candidate?.DisplayName ?? "Unknown candidate",
                        candidate?.Email ?? string.Empty,
                        candidate?.CurrentDesignation,
                        candidate?.ExperienceYears,
                        candidate?.NoticePeriodDays,
                        match.Rank,
                        match.Score,
                        match.Confidence,
                        match.Explanation,
                        match.Strengths,
                        match.Gaps,
                        candidate?.ApplicationEvidence ?? [],
                        candidate?.InterviewEvidence ?? [],
                        ranked.AgentRunId,
                        ranked.GeneratedAtUtc);
                })
                .ToArray();

            await _repository.SaveTalentRediscoveryMatchesAsync(
                _currentUser.TenantId,
                jobRequestId,
                ranked.AgentRunId,
                operationsMatches,
                cancellationToken);

            return Result<RankTalentRediscoveryResult>.Success(new RankTalentRediscoveryResult(
                operationsMatches,
                ranked.AgentRunId,
                ranked.Model,
                ranked.GeneratedAtUtc));
        }
        catch
        {
            return Result<RankTalentRediscoveryResult>.Failure(
                "talent_rediscovery.unavailable",
                "The Talent Rediscovery Agent is unavailable. Recruiter sourcing was not changed.");
        }
    }

    public async Task<Result<RankApplicantRankingsResult>> RankApplicantRankingsAsync(
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<RankApplicantRankingsResult>.Failure(
                "applicant_ranking.forbidden",
                "Only Recruiter or Tenant Admin users can rank current applicants.");
        }

        var context = await _repository.GetApplicantRankingContextAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobPostId,
            cancellationToken);
        if (context is null)
        {
            return Result<RankApplicantRankingsResult>.Failure(
                "applicant_ranking.not_claimed",
                "Claim the Recruiter Sourcing work item before ranking current applicants.");
        }

        if (context.Applications.Count == 0)
        {
            return Result<RankApplicantRankingsResult>.Failure(
                "applicant_ranking.no_applications",
                "No active current applications were found for this job post.");
        }

        try
        {
            var ranked = await _applicantRankingAgent.RankAsync(_currentUser.TenantId, context, cancellationToken);
            var applicationEvidence = context.Applications.ToDictionary(application => application.JobApplicationId);
            var operationsMatches = ranked.Matches
                .Select(match =>
                {
                    applicationEvidence.TryGetValue(match.JobApplicationId, out var application);
                    return new OperationsApplicantRankingMatch(
                        match.JobApplicationId,
                        application?.CandidateId ?? Guid.Empty,
                        application?.CandidateName ?? "Unknown applicant",
                        application?.CandidateEmail ?? string.Empty,
                        application?.CurrentDesignation,
                        application?.ExperienceYears,
                        application?.NoticePeriodDays,
                        match.Rank,
                        match.Score,
                        match.Confidence,
                        match.Explanation,
                        match.Strengths,
                        match.Gaps,
                        application?.MatchedSkills ?? [],
                        application?.MissingSkills ?? [],
                        match.DocumentEvidence,
                        match.HistoricalOutcomeEvidence,
                        ranked.SemanticSimilarityStatus,
                        ranked.AgentRunId,
                        ranked.GeneratedAtUtc);
                })
                .ToArray();

            await _repository.SaveApplicantRankingsAsync(
                _currentUser.TenantId,
                jobPostId,
                ranked.AgentRunId,
                operationsMatches,
                cancellationToken);

            return Result<RankApplicantRankingsResult>.Success(new RankApplicantRankingsResult(
                operationsMatches,
                ranked.AgentRunId,
                ranked.Model,
                ranked.GeneratedAtUtc,
                ranked.SemanticSimilarityStatus));
        }
        catch
        {
            return Result<RankApplicantRankingsResult>.Failure(
                "applicant_ranking.unavailable",
                "The Applicant Ranking Agent is unavailable. Recruiter sourcing was not changed.");
        }
    }

    public async Task<Result<OperationsOnlineHeadhuntingQueuedResult>> SearchOnlineCandidatesAsync(
        Guid jobRequestId,
        OnlineHeadhuntingSearchInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsOnlineHeadhuntingQueuedResult>.Failure(
                "online_headhunting.forbidden",
                "Only Recruiter or Tenant Admin users can run online headhunting.");
        }

        var normalizedInput = NormalizeOnlineHeadhuntingInput(input);
        var context = await _repository.GetOnlineHeadhuntingContextAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            cancellationToken);
        if (context is null)
        {
            return Result<OperationsOnlineHeadhuntingQueuedResult>.Failure(
                "online_headhunting.not_claimed",
                "Claim the Recruiter Sourcing work item before running online headhunting.");
        }

        var dailyCount = await _repository.CountOnlineHeadhuntingLeadsCreatedTodayAsync(
            _currentUser.TenantId,
            jobRequestId,
            cancellationToken);
        const int dailyLimit = 100;
        if (dailyCount >= dailyLimit)
        {
            return Result<OperationsOnlineHeadhuntingQueuedResult>.Failure(
                "online_headhunting.daily_limit_reached",
                "This job request has reached the 100 online lead daily limit.");
        }

        var allowedLimit = Math.Min(normalizedInput.Limit ?? 20, dailyLimit - dailyCount);
        var sourceCodes = OnlineHeadhuntingSources.Normalize(normalizedInput.SourceCodes);
        var queuedAtUtc = DateTimeOffset.UtcNow;
        var requestId = Guid.NewGuid();
        var queuedInput = normalizedInput with
        {
            Limit = allowedLimit,
            SourceCodes = sourceCodes
        };

        if (!_onlineHeadhuntingJobQueue.TryEnqueue(new OnlineHeadhuntingBackgroundJob(
            requestId,
            _currentUser.TenantId,
            _currentUser.UserId,
            _currentUser.Email,
            jobRequestId,
            queuedInput,
            queuedAtUtc)))
        {
            return Result<OperationsOnlineHeadhuntingQueuedResult>.Failure(
                "online_headhunting.queue_unavailable",
                "AI Headhunting is temporarily busy. Try again in a moment.");
        }

        return Result<OperationsOnlineHeadhuntingQueuedResult>.Success(new OperationsOnlineHeadhuntingQueuedResult(
            requestId,
            jobRequestId,
            _currentUser.UserId,
            "Queued",
            "AI Headhunting is running in the background. You will be notified when lead-only results are ready.",
            allowedLimit,
            dailyLimit,
            dailyCount,
            sourceCodes,
            queuedAtUtc));
    }

    public async Task RunOnlineCandidatesSearchAsync(
        OnlineHeadhuntingBackgroundJob job,
        CancellationToken cancellationToken)
    {
        Result<OperationsOnlineHeadhuntingResult> result;

        try
        {
            result = await ExecuteOnlineCandidatesSearchAsync(job.JobRequestId, job.Input, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            result = Result<OperationsOnlineHeadhuntingResult>.Failure(
                "online_headhunting.unavailable",
                "The Online Headhunting Agent is unavailable. No candidates or applications were created.");
        }

        await PublishOnlineHeadhuntingCompletionNotificationAsync(job, result, cancellationToken);
    }

    private async Task<Result<OperationsOnlineHeadhuntingResult>> ExecuteOnlineCandidatesSearchAsync(
        Guid jobRequestId,
        OnlineHeadhuntingSearchInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsOnlineHeadhuntingResult>.Failure(
                "online_headhunting.forbidden",
                "Only Recruiter or Tenant Admin users can run online headhunting.");
        }

        var normalizedInput = NormalizeOnlineHeadhuntingInput(input);
        var context = await _repository.GetOnlineHeadhuntingContextAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            cancellationToken);
        if (context is null)
        {
            return Result<OperationsOnlineHeadhuntingResult>.Failure(
                "online_headhunting.not_claimed",
                "Claim the Recruiter Sourcing work item before running online headhunting.");
        }

        var dailyCount = await _repository.CountOnlineHeadhuntingLeadsCreatedTodayAsync(
            _currentUser.TenantId,
            jobRequestId,
            cancellationToken);
        const int dailyLimit = 100;
        if (dailyCount >= dailyLimit)
        {
            return Result<OperationsOnlineHeadhuntingResult>.Failure(
                "online_headhunting.daily_limit_reached",
                "This job request has reached the 100 online lead daily limit.");
        }

        var allowedLimit = Math.Min(normalizedInput.Limit ?? 20, dailyLimit - dailyCount);
        try
        {
            var result = await _onlineHeadhuntingAgent.SearchAsync(
                _currentUser.TenantId,
                context,
                normalizedInput,
                allowedLimit,
                cancellationToken);

            var saved = await _repository.SaveOnlineHeadhuntingResultAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                normalizedInput with { Limit = allowedLimit },
                context,
                result,
                dailyCount,
                dailyLimit,
                cancellationToken);

            return Result<OperationsOnlineHeadhuntingResult>.Success(saved);
        }
        catch
        {
            return Result<OperationsOnlineHeadhuntingResult>.Failure(
                "online_headhunting.unavailable",
                "The Online Headhunting Agent is unavailable. No candidates or applications were created.");
        }
    }

    private Task PublishOnlineHeadhuntingCompletionNotificationAsync(
        OnlineHeadhuntingBackgroundJob job,
        Result<OperationsOnlineHeadhuntingResult> result,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>
        {
            ["route"] = $"/app/recruitment/sourcing/{job.JobRequestId:D}?tab=headhunting",
            ["refreshEntityType"] = "RecruiterSourcing",
            ["jobRequestId"] = job.JobRequestId.ToString("D"),
            ["requestId"] = job.RequestId.ToString("D")
        };

        string title;
        string message;
        string severity;

        if (result.Succeeded)
        {
            var saved = result.Value;
            metadata["action"] = "online_headhunting_completed";
            metadata["onlineCandidateSourcingRunId"] = saved.Run.OnlineCandidateSourcingRunId.ToString("D");
            metadata["leadCount"] = saved.Leads.Count.ToString();
            title = "AI Headhunting results ready";
            message = $"{saved.Leads.Count} lead-only result(s) are ready for recruiter review.";
            severity = "Info";
        }
        else
        {
            metadata["action"] = "online_headhunting_failed";
            metadata["errorCode"] = result.Error.Code;
            title = "AI Headhunting search failed";
            message = result.Error.Message;
            severity = "Warning";
        }

        var notification = new RealtimeNotificationMessage(
            Guid.NewGuid(),
            job.TenantId,
            job.RequestedByUserId,
            title,
            message,
            "OnlineHeadhunting",
            severity,
            "JobRequest",
            job.JobRequestId.ToString("D"),
            DateTimeOffset.UtcNow,
            metadata);

        return _notificationPublisher.PublishToUserAsync(
            job.TenantId,
            job.RequestedByUserId,
            notification,
            cancellationToken);
    }

    public async Task<Result<OperationsOnlineCandidateLead>> UpdateOnlineCandidateLeadStatusAsync(
        Guid onlineCandidateLeadId,
        UpdateOnlineCandidateLeadStatusInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsOnlineCandidateLead>.Failure(
                "online_headhunting.forbidden",
                "Only Recruiter or Tenant Admin users can update online leads.");
        }

        var normalizedStatus = input.Status?.Trim() ?? string.Empty;
        if (!new[] { "New", "Shortlisted", "Rejected" }.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
        {
            return Result<OperationsOnlineCandidateLead>.Failure(
                "online_headhunting.invalid_status",
                "Online lead status must be New, Shortlisted, or Rejected.");
        }

        var lead = await _repository.UpdateOnlineCandidateLeadStatusAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            onlineCandidateLeadId,
            new UpdateOnlineCandidateLeadStatusInput(ToLeadStatus(normalizedStatus)),
            cancellationToken);

        return lead is null
            ? Result<OperationsOnlineCandidateLead>.Failure(
                "online_headhunting.lead_not_found",
                "Online lead was not found or recruiter sourcing is not claimed.")
            : Result<OperationsOnlineCandidateLead>.Success(lead);
    }

    public async Task<Result<SendCandidateInvitationsResult>> SendCandidateInvitationsAsync(
        Guid jobRequestId,
        SendCandidateInvitationsInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<SendCandidateInvitationsResult>.Failure(
                "candidate_invitation.forbidden",
                "Only Recruiter or Tenant Admin users can invite rediscovered candidates.");
        }

        var candidateIds = input.CandidateIds?
            .Where(candidateId => candidateId != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];
        if (candidateIds.Length == 0)
        {
            return Result<SendCandidateInvitationsResult>.Failure(
                "candidate_invitation.candidate_required",
                "Select at least one candidate to invite.");
        }

        var sourcing = await _repository.GetRecruiterSourcingAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            cancellationToken);
        if (sourcing is null)
        {
            return Result<SendCandidateInvitationsResult>.Failure(
                "candidate_invitation.not_claimed",
                "Claim the Recruiter Sourcing work item before inviting rediscovered candidates.");
        }

        var eligibleCandidateIds = sourcing.CandidateSearchItems
            .Select(candidate => candidate.CandidateId)
            .ToHashSet();
        var allowedCandidateIds = candidateIds
            .Where(eligibleCandidateIds.Contains)
            .ToArray();
        if (allowedCandidateIds.Length == 0)
        {
            return Result<SendCandidateInvitationsResult>.Failure(
                "candidate_invitation.no_eligible_candidates",
                "None of the selected candidates are eligible rediscovery candidates for this request.");
        }

        var result = await _repository.SendCandidateInvitationsAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            input with { CandidateIds = allowedCandidateIds },
            cancellationToken);

        return Result<SendCandidateInvitationsResult>.Success(result);
    }

    public async Task<Result<AddManualCandidateResult>> AddManualCandidateToJobPostAsync(
        Guid jobPostId,
        AddManualCandidateInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<AddManualCandidateResult>.Failure(
                "manual_candidate.forbidden",
                "Only Recruiter or Tenant Admin users can add candidates to a job post.");
        }

        var validationError = ValidateManualCandidateInput(input);
        if (validationError is not null)
        {
            return Result<AddManualCandidateResult>.Failure(validationError.Value.Code, validationError.Value.Message);
        }

        var normalizedInput = NormalizeManualCandidateInput(input);
        var result = await _repository.AddManualCandidateToJobPostAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobPostId,
            normalizedInput,
            cancellationToken);

        if (result is null)
        {
            return Result<AddManualCandidateResult>.Failure(
                "manual_candidate.not_found",
                "Published job post or claimed recruiter sourcing work was not found.");
        }

        await TryUpsertManualCandidateCvEvidenceVectorAsync(
            _currentUser.TenantId,
            result.JobApplicationId,
            normalizedInput.ParsedCvEvidence,
            cancellationToken);

        return Result<AddManualCandidateResult>.Success(result);
    }

    public async Task<Result<ParseCandidateCvResult>> ParseCandidateCvAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<ParseCandidateCvResult>.Failure(
                "cv_parser.forbidden",
                "Only Recruiter or Tenant Admin users can parse candidate CVs.");
        }

        if (string.IsNullOrWhiteSpace(fileName) ||
            !fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ParseCandidateCvResult>.Failure("cv_parser.docx_required", "Upload a DOCX CV file.");
        }

        if (content.Length == 0)
        {
            return Result<ParseCandidateCvResult>.Failure("cv_parser.empty_file", "Uploaded CV is empty.");
        }

        if (content.Length > 2_000_000)
        {
            return Result<ParseCandidateCvResult>.Failure("cv_parser.file_too_large", "CV parser accepts DOCX files up to 2 MB for MVP.");
        }

        try
        {
            var parsed = await _cvParserAgent.ParseAsync(
                _currentUser.TenantId,
                new CvParseRequest(fileName.Trim(), content),
                cancellationToken);

            return Result<ParseCandidateCvResult>.Success(new ParseCandidateCvResult(
                fileName.Trim(),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                content.LongLength,
                HashBytesSha256(content),
                parsed.AgentRunId,
                parsed.Model,
                parsed.GeneratedAtUtc,
                parsed.ExtractedText,
                parsed.DisplayName,
                parsed.Email,
                parsed.Phone,
                parsed.CurrentDesignation,
                parsed.CurrentCompany,
                parsed.ExperienceYears,
                parsed.Skills,
                parsed.UniversityName,
                parsed.DegreeName,
                parsed.GraduationYear,
                parsed.Summary));
        }
        catch (Exception)
        {
            return Result<ParseCandidateCvResult>.Failure(
                "cv_parser.unavailable",
                "The CV Parser Agent could not parse this file. Manual candidate entry is still available.");
        }
    }

    public async Task<Result<OperationsRecruiterApplication>> UpdateCandidateApplicationStatusAsync(
        Guid jobApplicationId,
        UpdateCandidateApplicationStatusInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsRecruiterApplication>.Failure(
                "candidate_application.forbidden",
                "Only Recruiter or Tenant Admin users can update candidate applications.");
        }

        var decision = NormalizeApplicationDecision(input.Decision);
        if (decision is null)
        {
            return Result<OperationsRecruiterApplication>.Failure(
                "candidate_application.decision_invalid",
                "Decision must be Shortlist, Hold, or Reject.");
        }

        var normalized = input with
        {
            Decision = decision,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
        };

        var result = await _repository.UpdateCandidateApplicationStatusAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobApplicationId,
            normalized,
            cancellationToken);

        return result is null
            ? Result<OperationsRecruiterApplication>.Failure(
                "candidate_application.not_found",
                "Application was not found or recruiter sourcing work is not claimed.")
            : Result<OperationsRecruiterApplication>.Success(result);
    }

    public async Task<Result<ScheduleCandidateInterviewResult>> ScheduleCandidateInterviewAsync(
        Guid jobApplicationId,
        ScheduleCandidateInterviewInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<ScheduleCandidateInterviewResult>.Failure(
                "candidate_interview.forbidden",
                "Only Recruiter or Tenant Admin users can schedule candidate interviews.");
        }

        if (input.JobPostInterviewRoundId == Guid.Empty)
        {
            return Result<ScheduleCandidateInterviewResult>.Failure(
                "candidate_interview.round_required",
                "Select the interview round to schedule.");
        }

        if (input.StartsAtUtc <= DateTimeOffset.UtcNow.AddMinutes(-5))
        {
            return Result<ScheduleCandidateInterviewResult>.Failure(
                "candidate_interview.start_invalid",
                "Interview start time must be in the future.");
        }

        var normalized = input with
        {
            MeetingLink = string.IsNullOrWhiteSpace(input.MeetingLink) ? null : input.MeetingLink.Trim(),
            LocationText = string.IsNullOrWhiteSpace(input.LocationText) ? null : input.LocationText.Trim(),
            CalendarProvider = string.IsNullOrWhiteSpace(input.CalendarProvider) ? null : input.CalendarProvider.Trim(),
            CalendarEventId = string.IsNullOrWhiteSpace(input.CalendarEventId) ? null : input.CalendarEventId.Trim(),
            CalendarEventHtmlLink = string.IsNullOrWhiteSpace(input.CalendarEventHtmlLink) ? null : input.CalendarEventHtmlLink.Trim()
        };

        var validation = await _repository.ValidateCandidateInterviewScheduleAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobApplicationId,
            normalized,
            cancellationToken);
        if (validation.Status != OperationsScheduleCandidateInterviewValidationStatus.Ready)
        {
            return ToScheduleCandidateInterviewValidationFailure(validation.Status);
        }

        if (string.IsNullOrWhiteSpace(normalized.CalendarEventId))
        {
            var scheduleContext = await _repository.GetInterviewScheduleContextAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                jobApplicationId,
                normalized,
                cancellationToken);
            if (scheduleContext is null)
            {
                return Result<ScheduleCandidateInterviewResult>.Failure(
                    "candidate_interview.not_found",
                    "Application, active round, default interviewer, or claimed recruiter sourcing work was not found.");
            }

            var calendarRequest = InterviewCalendarMeetingFactory.Build(
                scheduleContext,
                normalized.StartsAtUtc,
                normalized.LocationText,
                Guid.NewGuid().ToString("N"),
                createOnlineMeeting: string.IsNullOrWhiteSpace(normalized.MeetingLink),
                existingMeetingLink: normalized.MeetingLink);
            var calendarResult = await _calendarMeetingService.CreateMeetingAsync(calendarRequest, cancellationToken);
            if (calendarResult.Failed)
            {
                return Result<ScheduleCandidateInterviewResult>.Failure(
                    calendarResult.Error.Code,
                    calendarResult.Error.Message);
            }

            if (calendarResult.Value.Created)
            {
                normalized = normalized with
                {
                    MeetingLink = normalized.MeetingLink ?? calendarResult.Value.MeetingLink,
                    CalendarProvider = calendarResult.Value.Provider,
                    CalendarEventId = calendarResult.Value.EventId,
                    CalendarEventHtmlLink = calendarResult.Value.EventHtmlLink
                };
            }
        }

        var result = await _repository.ScheduleCandidateInterviewAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobApplicationId,
            normalized,
            cancellationToken);

        if (result is null)
        {
            return Result<ScheduleCandidateInterviewResult>.Failure(
                "candidate_interview.not_found",
                "Application, active round, default interviewer, or claimed recruiter sourcing work was not found.");
        }

        await PublishNotificationsAsync(result.NotificationDispatches, cancellationToken);

        return Result<ScheduleCandidateInterviewResult>.Success(result.Result);
    }

    public async Task<Result<OperationsInterviewTaskList>> GetMyInterviewTasksAsync(CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseInterviewFeedback(roleCodes))
        {
            return Result<OperationsInterviewTaskList>.Failure(
                "interview_task.forbidden",
                "Only assigned internal users or Tenant Admin users can view interview tasks.");
        }

        var tasks = await _repository.GetMyInterviewTasksAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            cancellationToken);

        return Result<OperationsInterviewTaskList>.Success(tasks);
    }

    public async Task<Result<InterviewQuestionRecommendationSet>> GetLatestInterviewQuestionRecommendationsAsync(
        Guid interviewId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseInterviewFeedback(roleCodes))
        {
            return Result<InterviewQuestionRecommendationSet>.Failure(
                "interview_questions.forbidden",
                "Only assigned internal users or Tenant Admin users can view interview question recommendations.");
        }

        var recommendations = await _repository.GetLatestInterviewQuestionRecommendationsAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            interviewId,
            cancellationToken);

        return recommendations is null
            ? Result<InterviewQuestionRecommendationSet>.Failure(
                "interview_questions.not_found",
                "Interview question recommendations were not found for this interview.")
            : Result<InterviewQuestionRecommendationSet>.Success(recommendations);
    }

    public async Task<Result<InterviewQuestionRecommendationSet>> GenerateInterviewQuestionRecommendationsAsync(
        Guid interviewId,
        GenerateInterviewQuestionRecommendationsInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseInterviewFeedback(roleCodes))
        {
            return Result<InterviewQuestionRecommendationSet>.Failure(
                "interview_questions.forbidden",
                "Only assigned internal users or Tenant Admin users can generate interview question recommendations.");
        }

        var context = await _repository.GetInterviewQuestionRecommendationContextAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            interviewId,
            cancellationToken);
        if (context is null)
        {
            return Result<InterviewQuestionRecommendationSet>.Failure(
                "interview_questions.not_found",
                "Interview was not found or is not visible for question recommendations.");
        }

        if (!string.Equals(context.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(context.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return Result<InterviewQuestionRecommendationSet>.Failure(
                "interview_questions.status_invalid",
                "Question recommendations can only be generated for scheduled or completed interviews.");
        }

        var skillIds = context.RequiredSkills
            .Select(skill => skill.SkillId)
            .Where(skillId => skillId != Guid.Empty)
            .Distinct()
            .ToArray();
        var bankItems = await _repository.ListInterviewQuestionBankItemsAsync(
            _currentUser.TenantId,
            skillIds,
            context.RoundType,
            context.Department,
            160,
            cancellationToken);
        if (bankItems.Count == 0)
        {
            return Result<InterviewQuestionRecommendationSet>.Failure(
                "interview_questions.bank_unavailable",
                "The interview question bank has no active questions for this tenant.");
        }

        try
        {
            using var generationTimeout = new CancellationTokenSource(InterviewQuestionGenerationTimeout);
            var agentResult = await _interviewQuestionRecommendationAgent.GenerateAsync(
                _currentUser.TenantId,
                context,
                bankItems,
                generationTimeout.Token);
            var saved = await _repository.SaveInterviewQuestionRecommendationsAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                context,
                NormalizeRegenerateReason(input.RegenerateReason),
                agentResult,
                generationTimeout.Token);

            return Result<InterviewQuestionRecommendationSet>.Success(saved);
        }
        catch
        {
            return Result<InterviewQuestionRecommendationSet>.Failure(
                "interview_questions.ai_unavailable",
                "AI question recommendations could not be generated because the configured LLM did not return valid structured output.");
        }
    }

    public async Task<Result<DocumentExportFile>> DownloadInterviewQuestionRecommendationsDocxAsync(
        Guid interviewId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseInterviewFeedback(roleCodes))
        {
            return Result<DocumentExportFile>.Failure(
                "interview_questions.forbidden",
                "Only assigned internal users or Tenant Admin users can download interview question recommendations.");
        }

        var includeAllTenantTasks = roleCodes.Contains(AccessConstants.TenantAdminRoleCode, StringComparer.OrdinalIgnoreCase);
        var context = await _repository.GetInterviewQuestionRecommendationContextAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            includeAllTenantTasks,
            interviewId,
            cancellationToken);
        if (context is null)
        {
            return Result<DocumentExportFile>.Failure(
                "interview_questions.not_found",
                "Interview was not found or is not visible for question recommendation download.");
        }

        var recommendations = await _repository.GetLatestInterviewQuestionRecommendationsAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            includeAllTenantTasks,
            interviewId,
            cancellationToken);
        if (recommendations is null)
        {
            return Result<DocumentExportFile>.Failure(
                "interview_questions.not_found",
                "Interview question recommendations were not found for this interview.");
        }

        var fileName = SafeDownloadFileName($"{context.CandidateName}-{context.RoundName}-interview-questions-v{recommendations.VersionNumber}.docx");
        var file = _documentExportService.CreateWordDocument(
            fileName,
            BuildInterviewQuestionWordParagraphs(context, recommendations));
        return Result<DocumentExportFile>.Success(file);
    }

    public async Task<Result<SubmitInterviewFeedbackResult>> SubmitInterviewFeedbackAsync(
        Guid interviewId,
        SubmitInterviewFeedbackInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseInterviewFeedback(roleCodes))
        {
            return Result<SubmitInterviewFeedbackResult>.Failure(
                "interview_feedback.forbidden",
                "Only the assigned interviewer or a Tenant Admin can submit interview feedback.");
        }

        var validation = InterviewFeedbackPolicy.Validate(input);
        if (!validation.Succeeded || validation.NormalizedInput is null)
        {
            return Result<SubmitInterviewFeedbackResult>.Failure(
                validation.ErrorCode ?? "interview_feedback.invalid",
                validation.ErrorMessage ?? "Interview feedback is invalid.");
        }

        var result = await _repository.SubmitInterviewFeedbackAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            interviewId,
            validation.NormalizedInput,
            cancellationToken);

        if (!result.Succeeded || result.Result is null)
        {
            return Result<SubmitInterviewFeedbackResult>.Failure(
                "interview_feedback.not_found",
                "Interview was not found, is not assigned to you, is assigned to an active interviewer, or is already completed.");
        }

        await PublishNotificationsAsync(result.NotificationDispatches, cancellationToken);

        return Result<SubmitInterviewFeedbackResult>.Success(result.Result);
    }

    public async Task<Result<ForwardToHiringManagerResult>> ForwardToHiringManagerAsync(
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<ForwardToHiringManagerResult>.Failure(
                "hiring_manager_handoff.forbidden",
                "Only the assigned Recruiter or a Tenant Admin can forward a candidate to Hiring Manager Review.");
        }

        var result = await _repository.ForwardToHiringManagerAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobApplicationId,
            cancellationToken);

        await PublishNotificationsAsync(result.NotificationDispatches, cancellationToken);

        return result.Succeeded && result.Result is not null
            ? Result<ForwardToHiringManagerResult>.Success(result.Result)
            : Result<ForwardToHiringManagerResult>.Failure(
                "hiring_manager_handoff.not_ready",
                "Application was not found, recruiter sourcing work is not claimed, or interviews are not all completed/skipped.");
    }

    public async Task<Result<HiringManagerReviewList>> GetHiringManagerReviewsAsync(CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseHiringManagerReview(roleCodes))
        {
            return Result<HiringManagerReviewList>.Failure(
                "hiring_review.forbidden",
                "Only Hiring Manager or Tenant Admin users can view Hiring Manager Review work.");
        }

        var reviews = await _repository.GetHiringManagerReviewsAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            cancellationToken);

        return Result<HiringManagerReviewList>.Success(reviews);
    }

    public async Task<Result<HiringReviewDetail>> GetHiringReviewAsync(
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseHiringManagerReview(roleCodes))
        {
            return Result<HiringReviewDetail>.Failure(
                "hiring_review.forbidden",
                "Only Hiring Manager or Tenant Admin users can view Hiring Manager Review work.");
        }

        var detail = await _repository.GetHiringReviewAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            jobApplicationId,
            cancellationToken);

        return detail is null
            ? Result<HiringReviewDetail>.Failure("hiring_review.not_found", "Hiring Manager Review was not found.")
            : Result<HiringReviewDetail>.Success(detail);
    }

    public async Task<Result<ReportingManagerOptionList>> SearchReportingManagerOptionsAsync(
        Guid jobRequestId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseHiringManagerReview(roleCodes))
        {
            return Result<ReportingManagerOptionList>.Failure(
                "reporting_manager_options.forbidden",
                "Only Hiring Manager or Tenant Admin users can search reporting managers.");
        }

        var normalizedSkip = Math.Max(0, skip);
        var normalizedTake = Math.Clamp(take <= 0 ? 20 : take, 1, 50);
        var options = await _repository.SearchReportingManagerOptionsAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            jobRequestId,
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            normalizedSkip,
            normalizedTake,
            cancellationToken);

        return options is null
            ? Result<ReportingManagerOptionList>.Failure("reporting_manager_options.not_found", "Job Request was not found.")
            : Result<ReportingManagerOptionList>.Success(options);
    }

    public async Task<Result<OfferLetterDetails>> GenerateOfferLetterAsync(
        Guid jobApplicationId,
        GenerateOfferLetterInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseHiringManagerReview(roleCodes))
        {
            return Result<OfferLetterDetails>.Failure(
                "offer_letter.forbidden",
                "Only Hiring Manager or Tenant Admin users can generate offer letters.");
        }

        var offer = await _repository.GenerateOfferLetterAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            jobApplicationId,
            NormalizeOfferLetterInput(input),
            cancellationToken);

        return offer is null
            ? Result<OfferLetterDetails>.Failure("offer_letter.not_found", "Application is not ready for offer letter generation.")
            : Result<OfferLetterDetails>.Success(offer);
    }

    public async Task<Result<OfferLetterDetails>> UpdateOfferLetterAsync(
        Guid offerLetterId,
        UpdateOfferLetterInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseHiringManagerReview(roleCodes))
        {
            return Result<OfferLetterDetails>.Failure(
                "offer_letter.forbidden",
                "Only Hiring Manager or Tenant Admin users can update offer letters.");
        }

        if (string.IsNullOrWhiteSpace(input.Body))
        {
            return Result<OfferLetterDetails>.Failure("offer_letter.body_required", "Offer letter body is required.");
        }

        var offer = await _repository.UpdateOfferLetterAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            offerLetterId,
            NormalizeOfferLetterUpdateInput(input),
            cancellationToken);

        return offer is null
            ? Result<OfferLetterDetails>.Failure("offer_letter.not_found", "Offer letter was not found or cannot be updated.")
            : Result<OfferLetterDetails>.Success(offer);
    }

    public async Task<Result<OfferPresentationMeetingDetails>> ScheduleOfferPresentationMeetingAsync(
        Guid offerLetterId,
        ScheduleOfferPresentationMeetingInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseHiringManagerReview(roleCodes))
        {
            return Result<OfferPresentationMeetingDetails>.Failure(
                "offer_meeting.forbidden",
                "Only Hiring Manager or Tenant Admin users can schedule offer presentation meetings.");
        }

        if (input.MeetingAtUtc <= DateTimeOffset.UtcNow.AddMinutes(-5))
        {
            return Result<OfferPresentationMeetingDetails>.Failure("offer_meeting.date_invalid", "Meeting date must be in the future.");
        }

        if (string.IsNullOrWhiteSpace(input.LocationText))
        {
            return Result<OfferPresentationMeetingDetails>.Failure("offer_meeting.location_required", "Physical location is required.");
        }

        var meeting = await _repository.ScheduleOfferPresentationMeetingAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            offerLetterId,
            input with
            {
                LocationText = input.LocationText.Trim(),
                Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
            },
            cancellationToken);

        return meeting is null
            ? Result<OfferPresentationMeetingDetails>.Failure("offer_meeting.not_found", "Offer letter was not found or cannot schedule a meeting.")
            : Result<OfferPresentationMeetingDetails>.Success(meeting);
    }

    public async Task<Result<HiringOutcomeResult>> RecordHiringOutcomeAsync(
        Guid jobApplicationId,
        HiringOutcomeInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseHiringManagerReview(roleCodes))
        {
            return Result<HiringOutcomeResult>.Failure(
                "hiring_outcome.forbidden",
                "Only Hiring Manager or Tenant Admin users can record final hiring outcomes.");
        }

        var outcome = NormalizeHiringOutcome(input.Outcome);
        if (outcome is null)
        {
            return Result<HiringOutcomeResult>.Failure(
                "hiring_outcome.invalid",
                "Outcome must be Offered, Offer Declined, Rejected, On Hold, Hired, or Joined.");
        }

        var reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
        if (RequiresHiringOutcomeReason(outcome) && reason is null)
        {
            return Result<HiringOutcomeResult>.Failure(
                "hiring_outcome.reason_required",
                "A reason is required for declined, rejected, or on-hold outcomes.");
        }

        if (RequiresJoiningDate(outcome) && input.JoiningDate is null)
        {
            return Result<HiringOutcomeResult>.Failure(
                "hiring_outcome.joining_date_required",
                "Joining date is required when the candidate is hired or joined.");
        }

        var result = await _repository.RecordHiringOutcomeAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            jobApplicationId,
            input with
            {
                Outcome = outcome,
                Reason = reason
            },
            cancellationToken);

        return result is null
            ? Result<HiringOutcomeResult>.Failure("hiring_outcome.not_found", "Hiring review was not found or cannot be updated.")
            : Result<HiringOutcomeResult>.Success(result);
    }

    public async Task<Result> CloseJobRequestAsync(
        Guid jobRequestId,
        CloseJobRequestInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseHiringManagerReview(roleCodes))
        {
            return Result.Failure(
                "job_request_close.forbidden",
                "Only Hiring Manager or Tenant Admin users can close a Job Request from final review.");
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Result.Failure("job_request_close.reason_required", "Close reason is required.");
        }

        var closed = await _repository.CloseJobRequestAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            jobRequestId,
            input with { Reason = input.Reason.Trim() },
            cancellationToken);

        return closed
            ? Result.Success()
            : Result.Failure("job_request_close.not_found", "Job Request was not found or cannot be closed by this user.");
    }

    public async Task<Result> CreateEmployeeReferralsAsync(
        Guid jobRequestId,
        CreateEmployeeReferralsInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUsePmoReview(roleCodes))
        {
            return Result.Failure("employee_referral.forbidden", "Only PMO or Tenant Admin users can recommend employees.");
        }

        var employeeIds = input.EmployeeIds?
            .Where(employeeId => employeeId != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];
        if (employeeIds.Length == 0)
        {
            return Result.Failure("employee_referral.employee_required", "Select at least one employee to recommend.");
        }

        if (input.PresalesUserId == Guid.Empty)
        {
            return Result.Failure("employee_referral.presales_required", "Select the Presales user who should review this recommendation.");
        }

        var result = await _repository.CreateEmployeeReferralsAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            input with { EmployeeIds = employeeIds },
            cancellationToken);

        await PublishNotificationsAsync(result.NotificationDispatches, cancellationToken);

        return result.Succeeded
            ? Result.Success()
            : Result.Failure("employee_referral.not_found", "PMO Review work was not found or cannot be updated.");
    }

    public async Task<Result> ForwardToRecruitersAsync(Guid jobRequestId, CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUsePmoReview(roleCodes))
        {
            return Result.Failure("recruiter_handoff.forbidden", "Only PMO or Tenant Admin users can forward requests to recruiters.");
        }

        var result = await _repository.ForwardToRecruitersAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            cancellationToken);

        await PublishNotificationsAsync(result.NotificationDispatches, cancellationToken);

        return result.Succeeded
            ? Result.Success()
            : Result.Failure("recruiter_handoff.not_found", "PMO Review work was not found or cannot be forwarded.");
    }

    public async Task<Result> DecideEmployeeReferralsAsync(
        Guid jobRequestId,
        EmployeeReferralDecisionInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUsePresalesReview(roleCodes))
        {
            return Result.Failure("employee_referral_decision.forbidden", "Only Presales or Tenant Admin users can review employee recommendations.");
        }

        var decisions = input.Decisions?
            .Where(item => item.ReferralId != Guid.Empty)
            .ToArray() ?? [];
        if (decisions.Length == 0)
        {
            return Result.Failure("employee_referral_decision.required", "Select at least one recommendation decision.");
        }

        if (decisions.Any(item =>
                !string.Equals(item.Decision, "Accept", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.Decision, "Reject", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure("employee_referral_decision.invalid", "Each decision must be Accept or Reject.");
        }

        var result = await _repository.DecideEmployeeReferralsAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            input with { Decisions = decisions },
            cancellationToken);

        await PublishNotificationsAsync(result.NotificationDispatches, cancellationToken);

        return result.Succeeded
            ? Result.Success()
            : Result.Failure("employee_referral_decision.not_found", "Recommendation review was not found or cannot be updated.");
    }

    public async Task<Result<OperationsJobPost>> CreateJobPostAsync(
        Guid jobRequestId,
        CreateJobPostInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsJobPost>.Failure("job_post.forbidden", "Only Recruiter or Tenant Admin users can create job posts.");
        }

        var validationError = ValidateJobPostInput(input.Title, input.Description, input.SkillIds, input.ExperienceMinYears, input.ExperienceMaxYears, input.RequiredPositions, input.InterviewRounds);
        if (validationError is not null)
        {
            return Result<OperationsJobPost>.Failure(validationError.Value.Code, validationError.Value.Message);
        }

        if (input.InterviewTemplateId == Guid.Empty)
        {
            return Result<OperationsJobPost>.Failure("job_post.template_required", "Select an interview template for this job post.");
        }

        var jobPost = await _repository.CreateJobPostAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobRequestId,
            NormalizeCreateJobPostInput(input),
            cancellationToken);

        return jobPost is null
            ? Result<OperationsJobPost>.Failure("job_post.not_found", "Recruiter sourcing work was not found, not claimed, or the job post already exists.")
            : Result<OperationsJobPost>.Success(jobPost);
    }

    public async Task<Result<OperationsJobPost>> UpdateJobPostAsync(
        Guid jobPostId,
        UpdateJobPostInput input,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsJobPost>.Failure("job_post.forbidden", "Only Recruiter or Tenant Admin users can update job posts.");
        }

        var validationError = ValidateJobPostInput(input.Title, input.Description, input.SkillIds, input.ExperienceMinYears, input.ExperienceMaxYears, input.RequiredPositions, input.InterviewRounds);
        if (validationError is not null)
        {
            return Result<OperationsJobPost>.Failure(validationError.Value.Code, validationError.Value.Message);
        }

        var jobPost = await _repository.UpdateJobPostAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobPostId,
            NormalizeUpdateJobPostInput(input),
            cancellationToken);

        return jobPost is null
            ? Result<OperationsJobPost>.Failure("job_post.not_found", "Draft job post was not found or cannot be updated.")
            : Result<OperationsJobPost>.Success(jobPost);
    }

    public async Task<Result<OperationsJobPost>> PublishJobPostAsync(Guid jobPostId, CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsJobPost>.Failure("job_post.forbidden", "Only Recruiter or Tenant Admin users can publish job posts.");
        }

        var jobPost = await _repository.PublishJobPostAsync(_currentUser.TenantId, _currentUser.UserId, jobPostId, cancellationToken);
        return jobPost is null
            ? Result<OperationsJobPost>.Failure("job_post.publish_invalid", "Job post was not found, not claimed, or is not ready to publish.")
            : Result<OperationsJobPost>.Success(jobPost);
    }

    public async Task<Result<OperationsJobPost>> CloseJobPostAsync(Guid jobPostId, CancellationToken cancellationToken)
    {
        var roleCodes = await _repository.GetActorRoleCodesAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        if (!CanUseRecruiterSourcing(roleCodes))
        {
            return Result<OperationsJobPost>.Failure("job_post.forbidden", "Only Recruiter or Tenant Admin users can close job posts.");
        }

        var jobPost = await _repository.CloseJobPostAsync(_currentUser.TenantId, _currentUser.UserId, jobPostId, cancellationToken);
        return jobPost is null
            ? Result<OperationsJobPost>.Failure("job_post.close_invalid", "Job post was not found or cannot be closed.")
            : Result<OperationsJobPost>.Success(jobPost);
    }

    public async Task<Result> MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var updated = await _repository.MarkNotificationReadAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            notificationId,
            cancellationToken);

        return updated
            ? Result.Success()
            : Result.Failure("notification.not_found", "Notification was not found.");
    }

    public async Task<Result> MarkAllNotificationsReadAsync(CancellationToken cancellationToken)
    {
        await _repository.MarkAllNotificationsReadAsync(_currentUser.TenantId, _currentUser.UserId, cancellationToken);
        return Result.Success();
    }

    private static string SanitizeDownloadFileName(string fileName, string documentType)
    {
        var candidate = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            var label = string.IsNullOrWhiteSpace(documentType) ? "Application document" : documentType.Trim();
            candidate = $"{label}.docx";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(candidate
            .Where(character => !invalidCharacters.Contains(character))
            .ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(sanitized) ? "Application document.docx" : sanitized;
    }

    private static bool CanCreateJobRequests(IReadOnlySet<string> roleCodes)
    {
        return roleCodes.Contains("Presales") ||
               roleCodes.Contains(AccessConstants.PmoRoleCode) ||
               roleCodes.Contains(AccessConstants.TenantAdminRoleCode);
    }

    private static bool CanUsePmoReview(IReadOnlySet<string> roleCodes)
    {
        return roleCodes.Contains(AccessConstants.PmoRoleCode) ||
               roleCodes.Contains(AccessConstants.TenantAdminRoleCode);
    }

    private static bool CanUsePresalesReview(IReadOnlySet<string> roleCodes)
    {
        return roleCodes.Contains("Presales") ||
               roleCodes.Contains(AccessConstants.TenantAdminRoleCode);
    }

    private static bool CanUseRecruiterSourcing(IReadOnlySet<string> roleCodes)
    {
        return roleCodes.Contains("Recruiter") ||
               roleCodes.Contains(AccessConstants.TenantAdminRoleCode);
    }

    private static bool CanUseHiringManagerReview(IReadOnlySet<string> roleCodes)
    {
        return roleCodes.Contains("HiringManager") ||
               roleCodes.Contains(AccessConstants.TenantAdminRoleCode);
    }

    private static bool CanUseInterviewFeedback(IReadOnlySet<string> roleCodes)
    {
        return !roleCodes.Contains("Candidate");
    }

    private static bool CanUseCandidatePortal(IReadOnlySet<string> roleCodes)
    {
        return roleCodes.Contains("Candidate");
    }

    private static string? NormalizeApplicationDecision(string? decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
        {
            return null;
        }

        return decision.Trim().ToLowerInvariant() switch
        {
            "shortlist" or "screen" or "screening" => "Screening",
            "hold" or "onhold" or "on hold" => "OnHold",
            "reject" or "rejected" => "Rejected",
            _ => null
        };
    }

    private static string? NormalizeHiringOutcome(string? outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome))
        {
            return null;
        }

        return outcome.Trim().ToLowerInvariant() switch
        {
            "offer" or "offered" => "Offered",
            "decline" or "declined" or "offerdeclined" or "offer declined" => "OfferDeclined",
            "reject" or "rejected" => "Rejected",
            "hold" or "onhold" or "on hold" => "OnHold",
            "hire" or "hired" or "accepted" or "offeraccepted" or "offer accepted" => "Hired",
            "join" or "joined" => "Joined",
            _ => null
        };
    }

    private static bool RequiresHiringOutcomeReason(string outcome)
    {
        return outcome is "Rejected" or "OnHold" or "OfferDeclined";
    }

    private static bool RequiresJoiningDate(string outcome)
    {
        return outcome is "Hired" or "Joined";
    }

    private static GenerateOfferLetterInput NormalizeOfferLetterInput(GenerateOfferLetterInput input)
    {
        return input with
        {
            CompensationText = string.IsNullOrWhiteSpace(input.CompensationText) ? null : input.CompensationText.Trim(),
            ReportingManager = string.IsNullOrWhiteSpace(input.ReportingManager) ? null : input.ReportingManager.Trim(),
            WorkLocation = string.IsNullOrWhiteSpace(input.WorkLocation) ? null : input.WorkLocation.Trim(),
            AdditionalNotes = string.IsNullOrWhiteSpace(input.AdditionalNotes) ? null : input.AdditionalNotes.Trim()
        };
    }

    private static UpdateOfferLetterInput NormalizeOfferLetterUpdateInput(UpdateOfferLetterInput input)
    {
        return input with
        {
            Body = input.Body.Trim(),
            CompensationText = string.IsNullOrWhiteSpace(input.CompensationText) ? null : input.CompensationText.Trim(),
            ReportingManager = string.IsNullOrWhiteSpace(input.ReportingManager) ? null : input.ReportingManager.Trim(),
            WorkLocation = string.IsNullOrWhiteSpace(input.WorkLocation) ? null : input.WorkLocation.Trim(),
            Status = string.IsNullOrWhiteSpace(input.Status) ? "Draft" : input.Status.Trim()
        };
    }

    private static (string Code, string Message)? ValidatePortalCandidateProfileInput(UpdatePortalCandidateProfileInput input)
    {
        if (string.IsNullOrWhiteSpace(input.DisplayName))
        {
            return ("portal_profile.name_required", "Display name is required.");
        }

        if (input.ExperienceYears.HasValue && input.ExperienceYears.Value < 0)
        {
            return ("portal_profile.experience_invalid", "Experience cannot be negative.");
        }

        if (input.ExpectedSalaryAmount.HasValue && input.ExpectedSalaryAmount.Value < 0)
        {
            return ("portal_profile.salary_invalid", "Expected salary cannot be negative.");
        }

        if (!string.IsNullOrWhiteSpace(input.ExpectedSalaryCurrency) &&
            (input.ExpectedSalaryCurrency.Trim().Length != 3 ||
             !input.ExpectedSalaryCurrency.Trim().All(char.IsLetter)))
        {
            return ("portal_profile.currency_invalid", "Expected salary currency must be a 3-letter code.");
        }

        if (input.NoticePeriodDays.HasValue && input.NoticePeriodDays.Value < 0)
        {
            return ("portal_profile.notice_invalid", "Notice period cannot be negative.");
        }

        if (input.PrimaryEducation?.GraduationYear is < 1950 or > 2100)
        {
            return ("portal_profile.graduation_year_invalid", "Graduation year must be between 1950 and 2100.");
        }

        foreach (var skill in input.Skills ?? Array.Empty<UpdatePortalCandidateProfileSkillInput>())
        {
            if (skill.SkillId == Guid.Empty)
            {
                return ("portal_profile.skill_invalid", "Selected skill is invalid.");
            }

            if (skill.YearsExperience.HasValue && skill.YearsExperience.Value < 0)
            {
                return ("portal_profile.skill_experience_invalid", "Skill experience cannot be negative.");
            }
        }

        return null;
    }

    private static UpdatePortalCandidateProfileInput NormalizePortalCandidateProfileInput(UpdatePortalCandidateProfileInput input)
    {
        return input with
        {
            DisplayName = input.DisplayName.Trim(),
            Phone = NullIfBlank(input.Phone),
            LinkedInUrl = NullIfBlank(input.LinkedInUrl),
            CurrentDesignation = NullIfBlank(input.CurrentDesignation),
            CurrentCompany = NullIfBlank(input.CurrentCompany),
            ExpectedSalaryCurrency = NullIfBlank(input.ExpectedSalaryCurrency)?.ToUpperInvariant(),
            PrimaryEducation = NormalizePortalCandidateProfileEducation(input.PrimaryEducation),
            CurrentWorkHistory = NormalizePortalCandidateProfileWorkHistory(input.CurrentWorkHistory),
            Skills = (input.Skills ?? Array.Empty<UpdatePortalCandidateProfileSkillInput>())
                .Where(skill => skill.SkillId != Guid.Empty)
                .GroupBy(skill => skill.SkillId)
                .Select(group => group.First())
                .Select(skill => skill with
                {
                    SkillLevel = NormalizeSkillLevel(skill.SkillLevel)
                })
                .ToArray()
        };
    }

    private static IReadOnlyList<WordParagraphData> BuildInterviewQuestionWordParagraphs(
        OperationsInterviewQuestionRecommendationContext context,
        InterviewQuestionRecommendationSet recommendations)
    {
        var paragraphs = new List<WordParagraphData>
        {
            new("AI Interview Question Recommendations", WordParagraphStyle.Title),
            new($"{context.CandidateName} - {context.RoundName}", WordParagraphStyle.Heading1),
            new($"Job: {context.JobTitle}"),
            new($"Request: {context.RequestCode}; Client: {context.Client}; Department: {context.Department}"),
            new($"Interviewer: {context.InterviewerName}; Duration: {context.DurationMinutes} minutes"),
            new($"Generated: {recommendations.GeneratedAtUtc.UtcDateTime:u}; Version: {recommendations.VersionNumber}; Model: {recommendations.Model}"),
            new("Summary", WordParagraphStyle.Heading1),
            new(recommendations.Summary)
        };

        if (!string.IsNullOrWhiteSpace(recommendations.Rationale))
        {
            paragraphs.Add(new WordParagraphData("Rationale", WordParagraphStyle.Heading1));
            paragraphs.Add(new WordParagraphData(recommendations.Rationale));
        }

        paragraphs.Add(new WordParagraphData("Coverage", WordParagraphStyle.Heading1));
        paragraphs.Add(new WordParagraphData($"Round type: {recommendations.Coverage.RoundType}; Bank items used: {recommendations.Coverage.BankItemsUsed}; Semantic similarity: {recommendations.Coverage.SemanticSimilarityStatus}"));
        AddBulletParagraphs(paragraphs, "Skills covered", recommendations.Coverage.SkillsCovered);
        AddBulletParagraphs(paragraphs, "Candidate evidence used", recommendations.Coverage.CandidateEvidenceUsed);

        paragraphs.Add(new WordParagraphData("Recommended Questions", WordParagraphStyle.Heading1));
        foreach (var question in recommendations.Questions.OrderBy(question => question.SortOrder))
        {
            paragraphs.Add(new WordParagraphData($"{question.SortOrder}. {question.QuestionText}", WordParagraphStyle.Heading2));
            paragraphs.Add(new WordParagraphData($"Type: {question.QuestionType}; Round: {question.RoundType}; Skill: {question.SkillName ?? "General"}; Difficulty: {question.Difficulty}"));
            paragraphs.Add(new WordParagraphData($"Expected signal: {question.ExpectedSignal}"));
            paragraphs.Add(new WordParagraphData($"Rationale: {question.Rationale}"));
            AddBulletParagraphs(paragraphs, "Follow-ups", question.FollowUps);
            AddBulletParagraphs(paragraphs, "Evaluation rubric", question.EvaluationRubric);
        }

        paragraphs.Add(new WordParagraphData("Human interviewer owns the final assessment.", WordParagraphStyle.Heading2));
        return paragraphs;
    }

    private static void AddBulletParagraphs(
        ICollection<WordParagraphData> paragraphs,
        string heading,
        IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        paragraphs.Add(new WordParagraphData(heading, WordParagraphStyle.Heading2));
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            paragraphs.Add(new WordParagraphData(value.Trim(), IsBullet: true));
        }
    }

    private static string SafeDownloadFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray());
        safe = string.Join(' ', safe.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(safe) ? "interview-questions.docx" : safe;
    }

    private static PortalCandidateProfileEducation? NormalizePortalCandidateProfileEducation(
        PortalCandidateProfileEducation? education)
    {
        return education is null
            ? null
            : education with
            {
                UniversityName = NullIfBlank(education.UniversityName),
                DegreeName = NullIfBlank(education.DegreeName)
            };
    }

    private static PortalCandidateProfileWorkHistory? NormalizePortalCandidateProfileWorkHistory(
        PortalCandidateProfileWorkHistory? workHistory)
    {
        return workHistory is null
            ? null
            : workHistory with
            {
                CompanyName = NullIfBlank(workHistory.CompanyName),
                Title = NullIfBlank(workHistory.Title)
            };
    }

    private static string NormalizeSkillLevel(string? skillLevel)
    {
        return string.IsNullOrWhiteSpace(skillLevel)
            ? "Intermediate"
            : TrimToLength(skillLevel.Trim(), 40);
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeRegenerateReason(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : TrimToLength(value.Trim(), 500);
    }

    private static (string Code, string Message)? ValidateManualCandidateInput(AddManualCandidateInput input)
    {
        if (input.ExistingCandidateId is null && string.IsNullOrWhiteSpace(input.DisplayName))
        {
            return ("manual_candidate.name_required", "Candidate name is required when adding a new candidate.");
        }

        if (string.IsNullOrWhiteSpace(input.Email))
        {
            return ("manual_candidate.email_required", "Candidate email is required.");
        }

        if (!input.Email.Contains('@', StringComparison.Ordinal))
        {
            return ("manual_candidate.email_invalid", "Candidate email is invalid.");
        }

        if (input.ExperienceYears.HasValue && input.ExperienceYears.Value < 0)
        {
            return ("manual_candidate.experience_invalid", "Experience cannot be negative.");
        }

        if (input.NoticePeriodDays.HasValue && input.NoticePeriodDays.Value < 0)
        {
            return ("manual_candidate.notice_invalid", "Notice period cannot be negative.");
        }

        if (input.ParsedCvEvidence is not null)
        {
            if (string.IsNullOrWhiteSpace(input.ParsedCvEvidence.FileName))
            {
                return ("manual_candidate.cv_file_required", "Parsed CV file name is required.");
            }

            if (input.ParsedCvEvidence.SizeBytes <= 0)
            {
                return ("manual_candidate.cv_size_invalid", "Parsed CV size is invalid.");
            }

            if (!IsSha256(input.ParsedCvEvidence.ContentHashSha256))
            {
                return ("manual_candidate.cv_hash_invalid", "Parsed CV content hash is invalid.");
            }

            if (string.IsNullOrWhiteSpace(input.ParsedCvEvidence.ExtractedText))
            {
                return ("manual_candidate.cv_text_required", "Parsed CV text is required.");
            }
        }

        return null;
    }

    private static AddManualCandidateInput NormalizeManualCandidateInput(AddManualCandidateInput input)
    {
        return input with
        {
            DisplayName = string.IsNullOrWhiteSpace(input.DisplayName) ? null : input.DisplayName.Trim(),
            Email = input.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim(),
            LinkedInUrl = string.IsNullOrWhiteSpace(input.LinkedInUrl) ? null : input.LinkedInUrl.Trim(),
            CurrentDesignation = string.IsNullOrWhiteSpace(input.CurrentDesignation) ? null : input.CurrentDesignation.Trim(),
            CurrentCompany = string.IsNullOrWhiteSpace(input.CurrentCompany) ? null : input.CurrentCompany.Trim(),
            SkillIds = input.SkillIds?.Where(skillId => skillId != Guid.Empty).Distinct().ToArray() ?? [],
            SourceLabel = string.IsNullOrWhiteSpace(input.SourceLabel) ? "Other" : input.SourceLabel.Trim(),
            SourceDetail = string.IsNullOrWhiteSpace(input.SourceDetail) ? null : input.SourceDetail.Trim(),
            SourceUrl = string.IsNullOrWhiteSpace(input.SourceUrl) ? null : input.SourceUrl.Trim(),
            RecruiterNotes = string.IsNullOrWhiteSpace(input.RecruiterNotes) ? null : input.RecruiterNotes.Trim(),
            UniversityName = string.IsNullOrWhiteSpace(input.UniversityName) ? null : input.UniversityName.Trim(),
            DegreeName = string.IsNullOrWhiteSpace(input.DegreeName) ? null : input.DegreeName.Trim(),
            InvitationMessage = string.IsNullOrWhiteSpace(input.InvitationMessage) ? null : input.InvitationMessage.Trim(),
            ParsedCvEvidence = NormalizeParsedCvEvidence(input.ParsedCvEvidence)
        };
    }

    private static OnlineHeadhuntingSearchInput NormalizeOnlineHeadhuntingInput(OnlineHeadhuntingSearchInput input)
    {
        var limit = Math.Clamp(input.Limit ?? 20, 1, 20);
        var sourceCodes = input.SourceCodes?
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Select(source => source.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return input with
        {
            Limit = limit,
            SourceCodes = sourceCodes is { Length: > 0 } ? sourceCodes : null
        };
    }

    private static string ToLeadStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "shortlisted" => "Shortlisted",
            "rejected" => "Rejected",
            _ => "New"
        };
    }

    private static ParsedCandidateCvEvidenceInput? NormalizeParsedCvEvidence(ParsedCandidateCvEvidenceInput? evidence)
    {
        if (evidence is null || string.IsNullOrWhiteSpace(evidence.ExtractedText))
        {
            return null;
        }

        return evidence with
        {
            FileName = string.IsNullOrWhiteSpace(evidence.FileName) ? "Parsed CV.docx" : evidence.FileName.Trim(),
            ContentType = string.IsNullOrWhiteSpace(evidence.ContentType)
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : evidence.ContentType.Trim(),
            ContentHashSha256 = evidence.ContentHashSha256.Trim().ToLowerInvariant(),
            ExtractedText = TrimToLength(evidence.ExtractedText.Trim(), 40_000),
            Summary = string.IsNullOrWhiteSpace(evidence.Summary) ? null : TrimToLength(evidence.Summary.Trim(), 2_000),
            Model = string.IsNullOrWhiteSpace(evidence.Model) ? null : evidence.Model.Trim(),
            ParsedAtUtc = evidence.ParsedAtUtc?.ToUniversalTime()
        };
    }

    private static string BuildParsedCvEvidenceText(ParsedCandidateCvEvidenceInput evidence)
    {
        var parts = new List<string>
        {
            $"CV file: {evidence.FileName}"
        };

        if (!string.IsNullOrWhiteSpace(evidence.Model))
        {
            parts.Add($"Parser model: {evidence.Model}");
        }

        if (evidence.AgentRunId.HasValue)
        {
            parts.Add($"Parser run: {evidence.AgentRunId.Value:D}");
        }

        if (!string.IsNullOrWhiteSpace(evidence.Summary))
        {
            parts.Add($"CV Parser Agent summary: {evidence.Summary}");
        }

        parts.Add($"Extracted CV text: {evidence.ExtractedText}");
        return string.Join('\n', parts);
    }

    private static string BuildCvParserVersion(string? model)
    {
        return string.IsNullOrWhiteSpace(model)
            ? "cv-parser-agent-v1"
            : TrimToLength($"cv-parser-agent:{model.Trim()}", 64);
    }

    private static string HashBytesSha256(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }

    private static string TrimToLength(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static Result<ScheduleCandidateInterviewResult> ToScheduleCandidateInterviewValidationFailure(
        OperationsScheduleCandidateInterviewValidationStatus status)
    {
        return status switch
        {
            OperationsScheduleCandidateInterviewValidationStatus.PriorRoundsPending =>
                Result<ScheduleCandidateInterviewResult>.Failure(
                    "candidate_interview.prior_rounds_pending",
                    "Prior interview rounds must be completed or skipped before scheduling this round."),
            OperationsScheduleCandidateInterviewValidationStatus.RoundAlreadyScheduled =>
                Result<ScheduleCandidateInterviewResult>.Failure(
                    "candidate_interview.round_already_scheduled",
                    "This interview round is already scheduled, completed, or skipped for the applicant."),
            OperationsScheduleCandidateInterviewValidationStatus.MissingInterviewer =>
                Result<ScheduleCandidateInterviewResult>.Failure(
                    "candidate_interview.interviewer_required",
                    "The selected round needs an active default interviewer before it can be scheduled."),
            _ => Result<ScheduleCandidateInterviewResult>.Failure(
                "candidate_interview.not_found",
                "Application, active round, or claimed recruiter sourcing work was not found.")
        };
    }

    private static (string Code, string Message)? ValidateJobPostInput(
        string title,
        string description,
        IReadOnlyList<Guid>? skillIds,
        decimal? experienceMinYears,
        decimal? experienceMaxYears,
        int requiredPositions,
        IReadOnlyList<UpsertJobPostInterviewRoundInput>? interviewRounds)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return ("job_post.title_required", "Title is required.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return ("job_post.description_required", "Description is required.");
        }

        if (skillIds is null || skillIds.Count == 0 || skillIds.All(skillId => skillId == Guid.Empty))
        {
            return ("job_post.skills_required", "Select at least one skill for the job post.");
        }

        if (experienceMinYears.HasValue && experienceMinYears.Value < 0)
        {
            return ("job_post.experience_invalid", "Minimum experience cannot be negative.");
        }

        if (experienceMaxYears.HasValue && experienceMaxYears.Value < 0)
        {
            return ("job_post.experience_invalid", "Maximum experience cannot be negative.");
        }

        if (experienceMinYears.HasValue &&
            experienceMaxYears.HasValue &&
            experienceMinYears.Value > experienceMaxYears.Value)
        {
            return ("job_post.experience_invalid", "Minimum experience cannot be greater than maximum experience.");
        }

        if (requiredPositions < 1)
        {
            return ("job_post.positions_required", "At least one required position is needed.");
        }

        var activeRounds = (interviewRounds ?? Array.Empty<UpsertJobPostInterviewRoundInput>())
            .Where(round => !string.Equals(round.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (activeRounds.Length == 0)
        {
            return ("job_post.round_required", "At least one active interview round is required.");
        }

        if (activeRounds.Any(round => string.IsNullOrWhiteSpace(round.Name)))
        {
            return ("job_post.round_name_required", "Every active interview round needs a name.");
        }

        if (activeRounds.Any(round => round.DurationMinutes is < 15 or > 240))
        {
            return ("job_post.round_duration_invalid", "Interview round duration must be between 15 and 240 minutes.");
        }

        return null;
    }

    private static CreateJobPostInput NormalizeCreateJobPostInput(CreateJobPostInput input)
    {
        return input with
        {
            Title = input.Title.Trim(),
            Description = input.Description.Trim(),
            SkillIds = input.SkillIds.Where(skillId => skillId != Guid.Empty).Distinct().ToArray(),
            InterviewRounds = NormalizeJobPostRounds(input.InterviewRounds)
        };
    }

    private static UpdateJobPostInput NormalizeUpdateJobPostInput(UpdateJobPostInput input)
    {
        return input with
        {
            Title = input.Title.Trim(),
            Description = input.Description.Trim(),
            SkillIds = input.SkillIds.Where(skillId => skillId != Guid.Empty).Distinct().ToArray(),
            InterviewRounds = NormalizeJobPostRounds(input.InterviewRounds)
        };
    }

    private static IReadOnlyList<UpsertJobPostInterviewRoundInput> NormalizeJobPostRounds(
        IReadOnlyList<UpsertJobPostInterviewRoundInput>? rounds)
    {
        return (rounds ?? Array.Empty<UpsertJobPostInterviewRoundInput>())
            .OrderBy(round => round.RoundOrder)
            .Select((round, index) => round with
            {
                RoundOrder = index + 1,
                Name = round.Name.Trim(),
                Status = string.Equals(round.Status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active"
            })
            .ToArray();
    }

    private async Task PublishNotificationsAsync(
        IReadOnlyList<OperationsNotificationDispatch> dispatches,
        CancellationToken cancellationToken)
    {
        foreach (var dispatch in dispatches)
        {
            var metadata = string.IsNullOrWhiteSpace(dispatch.EventCode) || dispatch.Metadata.ContainsKey("eventCode")
                ? dispatch.Metadata
                : new Dictionary<string, string>(dispatch.Metadata)
                {
                    ["eventCode"] = dispatch.EventCode
                };

            var notification = new RealtimeNotificationMessage(
                Guid.NewGuid(),
                _currentUser.TenantId,
                dispatch.RecipientUserId,
                dispatch.Title,
                dispatch.Message,
                dispatch.Category,
                dispatch.Severity,
                dispatch.EntityType,
                dispatch.EntityId.ToString("D"),
                DateTimeOffset.UtcNow,
                metadata);

            await _notificationPublisher.PublishToUserAsync(
                _currentUser.TenantId,
                dispatch.RecipientUserId,
                notification,
                cancellationToken);
        }
    }

    private async Task TryIndexJobRequestDescriptionAsync(
        OperationsJobRequest jobRequest,
        CancellationToken cancellationToken)
    {
        Guid? runId = null;
        AiRuntimeSettingsSnapshot? settings = null;

        try
        {
            settings = await _aiRuntimeSettingsResolver.GetCurrentAsync(cancellationToken);
            var sourceText = BuildJobRequestEmbeddingText(jobRequest);
            var sourceHash = AiTextHasher.HashText(sourceText);
            runId = await _aiRunLogger.StartAsync(
                new AiAgentRunStart(
                    _currentUser.TenantId,
                    RequirementParserAgent.AgentId,
                    "JobRequest",
                    jobRequest.Id,
                    settings.LlmModel,
                    settings.EmbeddingModel,
                    sourceHash,
                    new Dictionary<string, string>
                    {
                        ["purpose"] = "job-request-requirement-profile",
                        ["sourceType"] = "JobRequestDescription"
                    }),
                cancellationToken);

            var embedding = await _embeddingProvider.GenerateEmbeddingAsync(sourceText, cancellationToken);
            if (embedding.Length != settings.EmbeddingDimensions)
            {
                throw new InvalidOperationException(
                    $"Embedding dimensions mismatch. Expected {settings.EmbeddingDimensions}, received {embedding.Length}.");
            }

            await _vectorStore.UpsertAsync(
                new VectorRecord(
                    _currentUser.TenantId,
                    "JobRequest",
                    jobRequest.Id,
                    "JobRequestDescription",
                    sourceHash,
                    settings.EmbeddingModel,
                    settings.EmbeddingDimensions,
                    embedding),
                cancellationToken);

            await _aiRunLogger.SucceedAsync(
                _currentUser.TenantId,
                runId.Value,
                "Stored Job Request requirement profile embedding.",
                new Dictionary<string, string>
                {
                    ["sourceType"] = "JobRequestDescription",
                    ["embeddingModel"] = settings.EmbeddingModel
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            if (runId.HasValue)
            {
                await TryMarkEmbeddingFailedAsync(runId.Value, ex, cancellationToken);
            }
        }
    }

    private async Task TryMarkEmbeddingFailedAsync(Guid runId, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            await _aiRunLogger.FailAsync(
                _currentUser.TenantId,
                runId,
                exception.Message.Length <= 900 ? exception.Message : exception.Message[..900],
                new Dictionary<string, string>
                {
                    ["sourceType"] = "JobRequestDescription",
                    ["errorType"] = exception.GetType().Name
                },
                cancellationToken);
        }
        catch
        {
            // Job Request creation must not fail because vector logging failed.
        }
    }

    private static string BuildJobRequestEmbeddingText(OperationsJobRequest jobRequest)
    {
        return string.Join(Environment.NewLine, new[]
        {
            $"Title: {jobRequest.Title}",
            $"Client: {jobRequest.Client}",
            $"Department: {jobRequest.Department}",
            $"Location: {jobRequest.Location}",
            $"Skills: {string.Join(", ", jobRequest.Skills)}",
            $"Experience: {jobRequest.Experience}",
            $"Required positions: {jobRequest.RequiredPositions}",
            $"Priority: {jobRequest.Priority}",
            $"Hiring manager id: {jobRequest.HiringManagerId:D}",
            "Description:",
            jobRequest.Description
        });
    }
}
