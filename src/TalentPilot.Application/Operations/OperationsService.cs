using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Ai;
using TalentPilot.Application.Notifications;
using TalentPilot.Common.Results;
using TalentPilot.Domain.Access;

namespace TalentPilot.Application.Operations;

public sealed class OperationsService : IOperationsService
{
    private readonly IOperationsRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IRealtimeNotificationPublisher _notificationPublisher;
    private readonly IJobDescriptionDraftingAgent _jobDescriptionDraftingAgent;
    private readonly ICvParserAgent _cvParserAgent;
    private readonly IBenchMatchingAgent _benchMatchingAgent;
    private readonly ITalentRediscoveryAgent _talentRediscoveryAgent;
    private readonly IApplicantRankingAgent _applicantRankingAgent;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly IAiRuntimeSettingsResolver _aiRuntimeSettingsResolver;
    private readonly IAiAgentRunLogger _aiRunLogger;
    private readonly IApplicationDocumentStorage _applicationDocumentStorage;

    public OperationsService(
        IOperationsRepository repository,
        ICurrentUserAccessor currentUser,
        IRealtimeNotificationPublisher notificationPublisher,
        IJobDescriptionDraftingAgent jobDescriptionDraftingAgent,
        ICvParserAgent cvParserAgent,
        IBenchMatchingAgent benchMatchingAgent,
        ITalentRediscoveryAgent talentRediscoveryAgent,
        IApplicantRankingAgent applicantRankingAgent,
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        IAiRuntimeSettingsResolver aiRuntimeSettingsResolver,
        IAiAgentRunLogger aiRunLogger,
        IApplicationDocumentStorage applicationDocumentStorage)
    {
        _repository = repository;
        _currentUser = currentUser;
        _notificationPublisher = notificationPublisher;
        _jobDescriptionDraftingAgent = jobDescriptionDraftingAgent;
        _cvParserAgent = cvParserAgent;
        _benchMatchingAgent = benchMatchingAgent;
        _talentRediscoveryAgent = talentRediscoveryAgent;
        _applicantRankingAgent = applicantRankingAgent;
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _aiRuntimeSettingsResolver = aiRuntimeSettingsResolver;
        _aiRunLogger = aiRunLogger;
        _applicationDocumentStorage = applicationDocumentStorage;
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

        return sourcing is null
            ? Result<OperationsRecruiterSourcing>.Failure("recruiter_sourcing.not_found", "Recruiter sourcing work was not found or is not visible.")
            : Result<OperationsRecruiterSourcing>.Success(sourcing);
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
                storedDocument.ContentHashSha256),
            cancellationToken);

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

        var result = await _repository.AddManualCandidateToJobPostAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobPostId,
            NormalizeManualCandidateInput(input),
            cancellationToken);

        return result is null
            ? Result<AddManualCandidateResult>.Failure(
                "manual_candidate.not_found",
                "Published job post or claimed recruiter sourcing work was not found.")
            : Result<AddManualCandidateResult>.Success(result);
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
            LocationText = string.IsNullOrWhiteSpace(input.LocationText) ? null : input.LocationText.Trim()
        };

        var result = await _repository.ScheduleCandidateInterviewAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            jobApplicationId,
            normalized,
            cancellationToken);

        return result is null
            ? Result<ScheduleCandidateInterviewResult>.Failure(
                "candidate_interview.not_found",
                "Application, active round, default interviewer, or claimed recruiter sourcing work was not found.")
            : Result<ScheduleCandidateInterviewResult>.Success(result);
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

        return result is null
            ? Result<SubmitInterviewFeedbackResult>.Failure(
                "interview_feedback.not_found",
                "Interview was not found, is not assigned to you, or is already completed.")
            : Result<SubmitInterviewFeedbackResult>.Success(result);
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
                "Outcome must be Offered, Rejected, On Hold, or Joined.");
        }

        var result = await _repository.RecordHiringOutcomeAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            roleCodes.Contains(AccessConstants.TenantAdminRoleCode),
            jobApplicationId,
            input with
            {
                Outcome = outcome,
                Reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim()
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
            "reject" or "rejected" => "Rejected",
            "hold" or "onhold" or "on hold" => "OnHold",
            "join" or "joined" => "Joined",
            _ => null
        };
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
            InvitationMessage = string.IsNullOrWhiteSpace(input.InvitationMessage) ? null : input.InvitationMessage.Trim()
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
                dispatch.Metadata);

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
