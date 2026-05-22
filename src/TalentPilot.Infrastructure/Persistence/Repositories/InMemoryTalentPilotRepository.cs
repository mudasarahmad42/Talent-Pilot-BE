using TalentPilot.Application.Admin.AuditLogs;
using TalentPilot.Application.Admin.Groups;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Application.Admin.Roles;
using TalentPilot.Application.Admin.TenantProfiles;
using TalentPilot.Application.Admin.Users;
using TalentPilot.Application.Auth;
using TalentPilot.Application.Operations;
using TalentPilot.Common.Time;
using TalentPilot.Domain.Access;
using TalentPilot.Domain.Tenancy;

namespace TalentPilot.Infrastructure.Persistence.Repositories;

public sealed class InMemoryTalentPilotRepository :
    IIdentityRepository,
    IAdminTenantProfileRepository,
    IAdminUsersRepository,
    IAdminAccessPoliciesRepository,
    IAdminGroupsRepository,
    IAdminRolesRepository,
    IAdminNotificationsRepository,
    IAdminAuditLogRepository,
    IOperationsRepository,
    INotificationOutboxProcessor
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SystemActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string AdminTestNotificationEventCode = "ADMIN_TEST_NOTIFICATION";

    private readonly object _gate = new();
    private readonly IClock _clock;
    private readonly TenantState _tenant;
    private readonly List<RoleState> _roles = [];
    private readonly List<UserState> _users = [];
    private readonly List<GroupState> _groups = [];
    private readonly List<PermissionCatalogItem> _permissions = [];
    private readonly List<RefreshTokenRecord> _refreshTokens = [];
    private readonly List<NotificationEventState> _notificationEvents = [];
    private readonly List<NotificationTemplateState> _notificationTemplates = [];
    private readonly List<OutboxState> _outbox = [];
    private readonly List<AuditLogState> _auditLogs = [];
    private readonly List<OperationsJobRequest> _jobRequests = [];
    private readonly List<OperationsWorkflowAssignment> _workflowAssignments = [];
    private readonly List<OperationsNotification> _operationNotifications = [];
    private readonly List<EmployeeState> _employees = [];
    private readonly List<EmployeeReferralState> _employeeReferrals = [];

    private PermissionResolutionMode _permissionResolutionMode = PermissionResolutionMode.MergeAllAssignedRoles;
    private Guid _benchVisibilityRoleId;

    public InMemoryTalentPilotRepository(IClock clock)
    {
        _clock = clock;
        _tenant = new TenantState
        {
            TenantId = TenantId,
            DisplayName = "TKXEL",
            Slug = "tkxel",
            Domain = "tkxel.com",
            AdminContactEmail = "admin@tkxel.com",
            DefaultTimezone = "Asia/Karachi",
            DefaultCurrency = "PKR",
            Status = TenantStatus.Active,
            CareerDisplayName = "TKXEL Careers",
            PrimaryColor = "#0A66C2",
            CandidateLoginRequired = true,
            CandidateCvFormat = "DOCX",
            PublicJobsEnabled = true,
            InviteExpiryDays = 7,
            ReapplyCooldownDays = 90,
            SetupComplete = true,
            UpdatedAtUtc = _clock.UtcNow
        };

        SeedPermissions();
        SeedRoles();
        SeedGroups();
        SeedUsers();
        SeedEmployees();
        SeedNotifications();
        SeedAuditLogs();
    }

    public Task<IReadOnlyList<LoginOption>> ListLoginOptionsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var options = _users
                .Where(user => user.AccountStatus == "Active")
                .Select(user =>
                {
                    var roles = _roles
                        .Where(role => user.RoleIds.Contains(role.RoleId) && role.Status == "Active")
                        .OrderBy(role => role.Priority)
                        .Select(role => new CurrentUserRole(role.RoleId, role.Code, role.Name, role.Priority))
                        .ToArray();

                    return new LoginOption(
                        user.UserId,
                        user.DisplayName,
                        user.Email,
                        roles.FirstOrDefault()?.DisplayName ?? "No assigned role",
                        roles);
                })
                .OrderBy(option => option.Roles.Any(role => role.Code == "Candidate"))
                .ThenBy(option => option.Roles.Min(role => role.Priority))
                .ThenBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Task.FromResult<IReadOnlyList<LoginOption>>(options);
        }
    }

    public Task<AuthUserRecord?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = _users.FirstOrDefault(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user is null ? null : ToAuthUserRecord(user));
        }
    }

    public Task<AuthUserRecord?> FindUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            return Task.FromResult(user is null ? null : ToAuthUserRecord(user));
        }
    }

    public Task<CurrentUserData?> GetCurrentUserDataAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            if (user is null)
            {
                return Task.FromResult<CurrentUserData?>(null);
            }

            var data = new CurrentUserData
            {
                UserId = user.UserId,
                TenantId = user.TenantId,
                TenantDisplayName = _tenant.DisplayName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                PermissionResolutionMode = _permissionResolutionMode
            };

            foreach (var role in _roles.Where(role => user.RoleIds.Contains(role.RoleId) && role.Status == "Active"))
            {
                var roleData = new RoleWithPermissions
                {
                    RoleId = role.RoleId,
                    Code = role.Code,
                    Name = role.Name,
                    Priority = role.Priority
                };

                foreach (var permission in role.PermissionIds)
                {
                    roleData.PermissionIds.Add(permission);
                }

                data.Roles.Add(roleData);
            }

            data.Groups.AddRange(_groups
                .Where(group => user.GroupIds.Contains(group.GroupId) && group.Status == "Active")
                .Select(group => new CurrentUserGroup(group.GroupId, group.Name, group.Purpose)));

            return Task.FromResult<CurrentUserData?>(data);
        }
    }

    public Task TouchLastActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            if (user is not null)
            {
                user.LastActiveAtUtc = _clock.UtcNow;
                user.UpdatedAtUtc = _clock.UtcNow;
            }
        }

        return Task.CompletedTask;
    }

    public Task StoreRefreshTokenAsync(RefreshTokenRecord record, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _refreshTokens.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<RefreshTokenRecord?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_refreshTokens.FirstOrDefault(token => token.TokenHash == tokenHash));
        }
    }

    public Task RevokeRefreshTokenAsync(Guid refreshTokenId, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var token = _refreshTokens.FirstOrDefault(item => item.RefreshTokenId == refreshTokenId);
            if (token is not null)
            {
                _refreshTokens.Remove(token);
                _refreshTokens.Add(new RefreshTokenRecord
                {
                    RefreshTokenId = token.RefreshTokenId,
                    TenantId = token.TenantId,
                    UserId = token.UserId,
                    TokenHash = token.TokenHash,
                    ExpiresAtUtc = token.ExpiresAtUtc,
                    RevokedAtUtc = revokedAtUtc
                });
            }
        }

        return Task.CompletedTask;
    }

    public Task<OperationsSnapshot> GetSnapshotAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(new OperationsSnapshot(
                _users.Where(user => user.TenantId == tenantId && user.AccountStatus == "Active").Select(ToOperationsPerson).ToArray(),
                _jobRequests.ToArray(),
                _workflowAssignments.ToArray(),
                _operationNotifications.Where(notification => notification.RecipientUserId == userId).ToArray()));
        }
    }

    public Task<IReadOnlyList<OperationsActivityEvent>> GetActivityAsync(
        Guid tenantId,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var events = _auditLogs
                .Where(log => log.TenantId == tenantId && log.EntityId == entityId)
                .OrderByDescending(log => log.OccurredAtUtc)
                .Select(log => new OperationsActivityEvent(
                    log.AuditLogId,
                    entityId,
                    log.ActorDisplayName,
                    log.EventType,
                    log.EventSummary,
                    log.OccurredAtUtc))
                .ToArray();

            return Task.FromResult<IReadOnlyList<OperationsActivityEvent>>(events);
        }
    }

    public Task<OperationsJobRequest?> GetJobRequestAsync(
        Guid tenantId,
        Guid userId,
        Guid jobRequestId,
        bool canViewAll,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_jobRequests.FirstOrDefault(request => request.Id == jobRequestId));
        }
    }

    public Task<IReadOnlyList<OperationsPmoQueueItem>> GetPmoQueueAsync(
        Guid tenantId,
        Guid userId,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var userGroupIds = _users.FirstOrDefault(user => user.TenantId == tenantId && user.UserId == userId)?.GroupIds ?? [];
            var items = _workflowAssignments
                .Where(assignment =>
                    (assignment.Status == "Pending" || assignment.Status == "Claimed") &&
                    assignment.Stage == "PMO Review" &&
                    (includeTenantAdminFallback ||
                     assignment.AssignedToUserId == userId ||
                     assignment.ClaimedByUserId == userId ||
                     (assignment.AssignedToGroupId.HasValue && userGroupIds.Contains(assignment.AssignedToGroupId.Value))))
                .Join(
                    _jobRequests,
                    assignment => assignment.EntityId,
                    request => request.Id,
                    (assignment, request) => new OperationsPmoQueueItem(assignment, request))
                .ToArray();

            return Task.FromResult<IReadOnlyList<OperationsPmoQueueItem>>(items);
        }
    }

    public Task<IReadOnlyList<OperationsRecruitmentQueueItem>> GetRecruitmentQueueAsync(
        Guid tenantId,
        Guid userId,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var userGroupIds = _users.FirstOrDefault(user => user.TenantId == tenantId && user.UserId == userId)?.GroupIds ?? [];
            var items = _workflowAssignments
                .Where(assignment =>
                    (assignment.Status == "Pending" || assignment.Status == "Claimed") &&
                    assignment.Stage == "Recruiter Sourcing" &&
                    (includeTenantAdminFallback ||
                     assignment.AssignedToUserId == userId ||
                     assignment.ClaimedByUserId == userId ||
                     (assignment.AssignedToGroupId.HasValue && userGroupIds.Contains(assignment.AssignedToGroupId.Value))))
                .Join(
                    _jobRequests,
                    assignment => assignment.EntityId,
                    request => request.Id,
                    (assignment, request) => new OperationsRecruitmentQueueItem(assignment, request, CandidateCount: 0))
                .ToArray();

            return Task.FromResult<IReadOnlyList<OperationsRecruitmentQueueItem>>(items);
        }
    }

    public Task<IReadOnlyList<OperationsBenchMatch>> GetBenchMatchesAsync(
        Guid tenantId,
        Guid userId,
        Guid jobRequestId,
        bool canViewAll,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var jobRequest = _jobRequests.FirstOrDefault(request => request.Id == jobRequestId && request.Stage != "Closed");
            if (jobRequest is null)
            {
                return Task.FromResult<IReadOnlyList<OperationsBenchMatch>>([]);
            }

            var matches = _employees
                .Where(employee =>
                    employee.TenantId == tenantId &&
                    employee.Status == "Active" &&
                    employee.AvailabilityStatus == "Available" &&
                    employee.BenchStatus == "Benched" &&
                    employee.CurrentAllocationPercent == 0)
                .Select(employee => ToBenchMatch(employee, jobRequest.Skills))
                .OrderByDescending(match => match.MatchScore)
                .ThenBy(match => match.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Task.FromResult<IReadOnlyList<OperationsBenchMatch>>(matches);
        }
    }

    public Task<IReadOnlyList<OperationsNotification>> ListNotificationsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<OperationsNotification>>(
                _operationNotifications
                    .Where(notification => notification.RecipientUserId == userId)
                    .OrderByDescending(notification => notification.CreatedAt)
                    .ToArray());
        }
    }

    public Task<CreateOperationsJobRequestResult> CreateJobRequestAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            var requestId = Guid.NewGuid();
            var assignmentId = Guid.NewGuid();
            var pmoGroup = _groups.First(group => group.TenantId == tenantId && group.Name.Contains("PMO", StringComparison.OrdinalIgnoreCase));
            var requestCode = $"TP-REQ-{_jobRequests.Count + 1:000}";

            var request = new OperationsJobRequest(
                requestId,
                requestCode,
                input.Title.Trim(),
                input.Client.Trim(),
                input.Description.Trim(),
                input.Department.Trim(),
                input.Skills.Select(skill => skill.Trim()).Where(skill => skill.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                input.Experience.Trim(),
                input.Location.Trim(),
                input.RequiredPositions,
                0,
                string.IsNullOrWhiteSpace(input.Priority) ? "Medium" : input.Priority.Trim(),
                input.HiringManagerId,
                actorUserId,
                "PMO Review",
                null,
                pmoGroup.Name,
                "NotPublished",
                now);

            var assignment = new OperationsWorkflowAssignment(
                assignmentId,
                "JobRequest",
                requestId,
                "PMO Review",
                pmoGroup.GroupId,
                pmoGroup.Name,
                null,
                null,
                "Pending",
                now);

            _jobRequests.Add(request);
            _workflowAssignments.Add(assignment);

            var pmoUsers = _users.Where(user => user.TenantId == tenantId && user.GroupIds.Contains(pmoGroup.GroupId) && user.AccountStatus == "Active").ToArray();
            foreach (var pmoUser in pmoUsers)
            {
                _operationNotifications.Add(new OperationsNotification(
                    Guid.NewGuid(),
                    pmoUser.UserId,
                    "Presales request submitted",
                    $"{requestCode} is ready for PMO review.",
                    "WorkflowAssignment",
                    assignmentId,
                    null,
                    now));

                _outbox.Add(new OutboxState(Guid.NewGuid(), tenantId, "PRESALES_REQUEST_SUBMITTED", "Pending", now, null));
            }

            AddAudit(actorUserId, "job_request.created", "JobRequest", requestId, requestCode, $"{requestCode} was created and routed to PMO review.", "Talent Pilot App", "{}");
            return Task.FromResult(new CreateOperationsJobRequestResult(request, assignment));
        }
    }

    public Task<ForwardToRecruiterResult?> ForwardToRecruiterAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var request = _jobRequests.FirstOrDefault(item =>
                item.Id == jobRequestId &&
                item.Stage is "PMO Review" or "Bench Matching");
            if (request is null)
            {
                return Task.FromResult<ForwardToRecruiterResult?>(null);
            }

            var currentAssignment = _workflowAssignments.FirstOrDefault(item =>
                item.EntityType == "JobRequest" &&
                item.EntityId == jobRequestId &&
                item.Status != "Completed");

            var actor = _users.FirstOrDefault(user => user.TenantId == tenantId && user.UserId == actorUserId);
            var isGroupMember = actor is not null &&
                currentAssignment?.AssignedToGroupId is Guid groupId &&
                actor.GroupIds.Contains(groupId);
            var isOwner = currentAssignment?.AssignedToUserId == actorUserId ||
                currentAssignment?.ClaimedByUserId == actorUserId;

            if (!includeTenantAdminFallback && !isOwner && !isGroupMember)
            {
                return Task.FromResult<ForwardToRecruiterResult?>(null);
            }

            var now = _clock.UtcNow;
            var recruitmentGroup = _groups.First(group =>
                group.TenantId == tenantId &&
                group.Name.Contains("Recruit", StringComparison.OrdinalIgnoreCase));
            var assignment = new OperationsWorkflowAssignment(
                Guid.NewGuid(),
                "JobRequest",
                request.Id,
                "Recruiter Sourcing",
                recruitmentGroup.GroupId,
                recruitmentGroup.Name,
                null,
                null,
                "Pending",
                now);

            if (currentAssignment is not null)
            {
                _workflowAssignments.Remove(currentAssignment);
                _workflowAssignments.Add(currentAssignment with { Status = "Completed" });
            }

            _workflowAssignments.Add(assignment);
            _jobRequests.Remove(request);
            var updatedRequest = request with
            {
                Stage = "Recruiter Sourcing",
                OwnerId = null,
                OwnerGroupId = recruitmentGroup.Name
            };
            _jobRequests.Add(updatedRequest);

            foreach (var recruiter in _users.Where(user => user.TenantId == tenantId && user.GroupIds.Contains(recruitmentGroup.GroupId) && user.AccountStatus == "Active"))
            {
                _operationNotifications.Add(new OperationsNotification(
                    Guid.NewGuid(),
                    recruiter.UserId,
                    "PMO forwarded to recruiting",
                    $"{request.Code} is ready for recruiter sourcing.",
                    "WorkflowAssignment",
                    assignment.Id,
                    null,
                    now));
            }

            _outbox.Add(new OutboxState(Guid.NewGuid(), tenantId, "PMO_FORWARDED_TO_RECRUITING", "Pending", now, null));
            AddAudit(actorUserId, "job_request.forwarded_to_recruiter", "JobRequest", request.Id, request.Code, $"{request.Code} was forwarded to the recruitment queue.", "Talent Pilot App", "{}");

            return Task.FromResult<ForwardToRecruiterResult?>(new ForwardToRecruiterResult(updatedRequest, assignment, CandidateCount: 0));
        }
    }

    public Task<CreateInternalResourceReferralResult?> CreateInternalResourceReferralAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CreateInternalResourceReferralInput input,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var request = _jobRequests.FirstOrDefault(item => item.Id == jobRequestId && item.Stage != "Closed");
            if (request is null)
            {
                return Task.FromResult<CreateInternalResourceReferralResult?>(null);
            }

            var currentAssignment = _workflowAssignments.FirstOrDefault(item =>
                item.EntityType == "JobRequest" &&
                item.EntityId == jobRequestId &&
                item.Status != "Completed");

            var actor = _users.FirstOrDefault(user => user.TenantId == tenantId && user.UserId == actorUserId);
            var isGroupMember = actor is not null &&
                currentAssignment?.AssignedToGroupId is Guid groupId &&
                actor.GroupIds.Contains(groupId);
            var isOwner = currentAssignment?.AssignedToUserId == actorUserId ||
                currentAssignment?.ClaimedByUserId == actorUserId;

            if (!includeTenantAdminFallback && !isOwner && !isGroupMember)
            {
                return Task.FromResult<CreateInternalResourceReferralResult?>(null);
            }

            var selectedEmployeeIds = input.EmployeeIds.Distinct().ToArray();
            var matches = GetBenchMatchesForJob(tenantId, request)
                .Where(match => selectedEmployeeIds.Contains(match.EmployeeId))
                .OrderByDescending(match => match.MatchScore)
                .ThenBy(match => match.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (matches.Length != selectedEmployeeIds.Length)
            {
                return Task.FromResult<CreateInternalResourceReferralResult?>(null);
            }

            var now = _clock.UtcNow;
            var referrals = new List<InternalEmployeeReferral>(matches.Length);

            foreach (var match in matches)
            {
                var existing = _employeeReferrals.FirstOrDefault(referral =>
                    referral.TenantId == tenantId &&
                    referral.JobRequestId == request.Id &&
                    referral.EmployeeId == match.EmployeeId &&
                    referral.Status == "Referred");

                if (existing is null)
                {
                    existing = new EmployeeReferralState
                    {
                        ReferralId = Guid.NewGuid(),
                        TenantId = tenantId,
                        JobRequestId = request.Id,
                        EmployeeId = match.EmployeeId,
                        EmployeeName = match.DisplayName,
                        EmployeeEmail = match.Email,
                        Status = "Referred",
                        FitScore = match.MatchScore,
                        RecommendationSummary = BuildReferralSummary(match, input.Note),
                        ReferredByUserId = actorUserId,
                        PresalesUserId = request.CreatedById,
                        CreatedAtUtc = now
                    };
                    _employeeReferrals.Add(existing);
                }

                referrals.Add(ToInternalEmployeeReferral(existing));
            }

            _operationNotifications.Add(new OperationsNotification(
                Guid.NewGuid(),
                request.CreatedById,
                "Internal resource referred",
                $"{string.Join(", ", referrals.Select(referral => referral.EmployeeName))} referred for {request.Code}.",
                "JobRequest",
                request.Id,
                null,
                now));

            _outbox.Add(new OutboxState(Guid.NewGuid(), tenantId, "PMO_EMPLOYEE_REFERRED", "Pending", now, null));
            AddAudit(actorUserId, "job_request.employee_referred", "JobRequest", request.Id, request.Code, $"{request.Code} referred {referrals.Count} internal employee(s) to Presales.", "Workflow", "{}");

            return Task.FromResult<CreateInternalResourceReferralResult?>(new CreateInternalResourceReferralResult(request, referrals));
        }
    }

    public Task<bool> ClaimAssignmentAsync(Guid tenantId, Guid actorUserId, Guid assignmentId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var assignment = _workflowAssignments.FirstOrDefault(item => item.Id == assignmentId && item.Status == "Pending");
            if (assignment is null)
            {
                return Task.FromResult(false);
            }

            var actor = _users.FirstOrDefault(user => user.TenantId == tenantId && user.UserId == actorUserId);
            var isTenantAdmin = actor is not null && _roles.Any(role =>
                actor.RoleIds.Contains(role.RoleId) &&
                string.Equals(role.Code, AccessConstants.TenantAdminRoleCode, StringComparison.OrdinalIgnoreCase));
            var isGroupMember = actor is not null &&
                assignment.AssignedToGroupId.HasValue &&
                actor.GroupIds.Contains(assignment.AssignedToGroupId.Value);

            if (!isTenantAdmin && assignment.AssignedToUserId != actorUserId && !isGroupMember)
            {
                return Task.FromResult(false);
            }

            _workflowAssignments.Remove(assignment);
            _workflowAssignments.Add(assignment with
            {
                AssignedToUserId = actorUserId,
                ClaimedByUserId = actorUserId,
                Status = "Claimed"
            });

            var request = _jobRequests.FirstOrDefault(item => item.Id == assignment.EntityId);
            if (request is not null)
            {
                _jobRequests.Remove(request);
                _jobRequests.Add(request with { Stage = "Bench Matching", OwnerId = actorUserId });
            }

            _outbox.Add(new OutboxState(Guid.NewGuid(), tenantId, "WORKFLOW_ASSIGNMENT_CLAIMED", "Pending", _clock.UtcNow, null));
            return Task.FromResult(true);
        }
    }

    public Task<bool> MarkNotificationReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var notification = _operationNotifications.FirstOrDefault(item => item.Id == notificationId && item.RecipientUserId == userId);
            if (notification is null)
            {
                return Task.FromResult(false);
            }

            _operationNotifications.Remove(notification);
            _operationNotifications.Add(notification with { ReadAt = notification.ReadAt ?? _clock.UtcNow });
            return Task.FromResult(true);
        }
    }

    public Task MarkAllNotificationsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var unread = _operationNotifications.Where(item => item.RecipientUserId == userId && item.ReadAt is null).ToArray();
            foreach (var notification in unread)
            {
                _operationNotifications.Remove(notification);
                _operationNotifications.Add(notification with { ReadAt = _clock.UtcNow });
            }
        }

        return Task.CompletedTask;
    }

    public Task<TenantProfileSettings?> GetAsync(
        Guid tenantId,
        string configuredLlmModel,
        string configuredEmbeddingModel,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_tenant.TenantId != tenantId)
            {
                return Task.FromResult<TenantProfileSettings?>(null);
            }

            var settings = new TenantProfileSettings(
                _tenant.TenantId,
                _tenant.DisplayName,
                _tenant.Slug,
                _tenant.Domain,
                _tenant.AdminContactEmail,
                _tenant.DefaultTimezone,
                _tenant.DefaultCurrency,
                _tenant.Status,
                _tenant.CareerDisplayName,
                _tenant.PrimaryColor,
                _tenant.CandidateLoginRequired,
                _tenant.CandidateCvFormat,
                _tenant.PublicJobsEnabled,
                _tenant.InviteExpiryDays,
                _tenant.ReapplyCooldownDays,
                _users.Count(user => user.TenantId == tenantId && user.AccountStatus == "Active"),
                _roles.Count(role => role.TenantId == tenantId && role.Status == "Active"),
                _tenant.SetupComplete,
                configuredLlmModel,
                configuredEmbeddingModel,
                _tenant.UpdatedAtUtc);

            return Task.FromResult<TenantProfileSettings?>(settings);
        }
    }

    public Task<bool> IsSlugAvailableAsync(Guid tenantId, string slug, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_tenant.TenantId == tenantId ||
                !string.Equals(_tenant.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }
    }

    public Task UpdateAsync(
        Guid tenantId,
        Guid actorUserId,
        UpdateTenantProfileSettingsInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_tenant.TenantId != tenantId)
            {
                return Task.CompletedTask;
            }

            _tenant.DisplayName = input.DisplayName.Trim();
            _tenant.Slug = input.Slug.Trim();
            _tenant.Domain = input.Domain.Trim();
            _tenant.AdminContactEmail = input.AdminContactEmail.Trim();
            _tenant.DefaultTimezone = input.DefaultTimezone.Trim();
            _tenant.DefaultCurrency = input.DefaultCurrency.Trim();
            _tenant.Status = input.Status;
            _tenant.CareerDisplayName = input.CareerDisplayName.Trim();
            _tenant.PrimaryColor = input.PrimaryColor.Trim();
            _tenant.CandidateLoginRequired = input.CandidateLoginRequired;
            _tenant.CandidateCvFormat = input.CandidateCvFormat.Trim().ToUpperInvariant();
            _tenant.PublicJobsEnabled = input.PublicJobsEnabled;
            _tenant.InviteExpiryDays = input.InviteExpiryDays;
            _tenant.ReapplyCooldownDays = input.ReapplyCooldownDays;
            _tenant.UpdatedAtUtc = _clock.UtcNow;

            AddAudit(actorUserId, "TenantProfileUpdated", "Tenant", tenantId, "Tenant profile", "Updated tenant profile settings.", "Admin Center", metadataJson);
        }

        return Task.CompletedTask;
    }

    public Task<AdminUsersResponse> ListAsync(Guid tenantId, AdminUsersQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var users = _users.Where(user => user.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                users = users.Where(user =>
                    user.DisplayName.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    user.Email.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            if (query.RoleId.HasValue)
            {
                users = users.Where(user => user.RoleIds.Contains(query.RoleId.Value));
            }

            if (query.GroupId.HasValue)
            {
                users = users.Where(user => user.GroupIds.Contains(query.GroupId.Value));
            }

            if (!string.IsNullOrWhiteSpace(query.AccountStatus))
            {
                users = users.Where(user => string.Equals(user.AccountStatus, query.AccountStatus, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = users
                .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(ToUserListItem)
                .ToArray();

            var summary = new AdminUsersSummary(
                _users.Count(user => user.TenantId == tenantId && user.AccountStatus == "Active"),
                _groups.Count(group => group.TenantId == tenantId && group.Status == "Active"),
                new BenchVisibilityPolicySummary(_benchVisibilityRoleId, FindRoleName(_benchVisibilityRoleId), "Roles & Permissions"));

            return Task.FromResult(new AdminUsersResponse(summary, items, query.Page, query.PageSize, materialized.Count));
        }
    }

    public Task<AdminUserDetails?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            return Task.FromResult(user is null ? null : ToUserDetails(user));
        }
    }

    public Task<Guid?> FindRoleIdByCodeAsync(Guid tenantId, string roleCode, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_roles
                .Where(role => role.TenantId == tenantId)
                .FirstOrDefault(role => string.Equals(role.Code, roleCode, StringComparison.OrdinalIgnoreCase))
                ?.RoleId);
        }
    }

    public Task<bool> EmailExistsAsync(Guid tenantId, string email, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_users.Any(user =>
                user.TenantId == tenantId &&
                user.UserId != exceptUserId &&
                string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<bool> ActiveRolesExistAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(roleIds.All(roleId => _roles.Any(role => role.TenantId == tenantId && role.RoleId == roleId && role.Status == "Active")));
        }
    }

    public Task<bool> ActiveGroupsExistAsync(Guid tenantId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(groupIds.All(groupId => _groups.Any(group => group.TenantId == tenantId && group.GroupId == groupId && group.Status == "Active")));
        }
    }

    public Task<int> CountActiveTenantAdminsAsync(Guid tenantId, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var tenantAdminRoleId = _roles.First(role => role.Code == AccessConstants.TenantAdminRoleCode).RoleId;
            return Task.FromResult(_users.Count(user =>
                user.TenantId == tenantId &&
                user.UserId != exceptUserId &&
                user.AccountStatus == "Active" &&
                user.RoleIds.Contains(tenantAdminRoleId)));
        }
    }

    public Task<Guid> CreateAsync(Guid tenantId, Guid actorUserId, SaveAdminUserInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            var userId = Guid.NewGuid();
            _users.Add(new UserState
            {
                UserId = userId,
                TenantId = tenantId,
                DisplayName = input.DisplayName.Trim(),
                Email = input.Email.Trim(),
                Initials = BuildInitials(input.DisplayName),
                AccountStatus = input.AccountStatus,
                RoleIds = input.RoleIds.Distinct().ToList(),
                GroupIds = input.GroupIds.Distinct().ToList(),
                DepartmentName = "Engineering",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            AddAudit(actorUserId, "UserCreated", "User", userId, input.DisplayName, "Created internal user.", "Admin Center", metadataJson);
            return Task.FromResult(userId);
        }
    }

    public Task UpdateAsync(Guid tenantId, Guid actorUserId, Guid userId, SaveAdminUserInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            if (user is not null)
            {
                user.DisplayName = input.DisplayName.Trim();
                user.Email = input.Email.Trim();
                user.Initials = BuildInitials(input.DisplayName);
                user.AccountStatus = input.AccountStatus;
                user.RoleIds = input.RoleIds.Distinct().ToList();
                user.GroupIds = input.GroupIds.Distinct().ToList();
                user.UpdatedAtUtc = _clock.UtcNow;
                AddAudit(actorUserId, "UserUpdated", "User", userId, user.DisplayName, "Updated internal user.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid tenantId, Guid actorUserId, Guid userId, UpdateAdminUserStatusInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            if (user is not null)
            {
                user.AccountStatus = input.AccountStatus;
                user.UpdatedAtUtc = _clock.UtcNow;
                AddAudit(actorUserId, "UserStatusUpdated", "User", userId, user.DisplayName, $"Changed account status to {input.AccountStatus}.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task InsertInviteNotificationAsync(Guid tenantId, Guid actorUserId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            if (user is not null)
            {
                _outbox.Add(new OutboxState(Guid.NewGuid(), tenantId, "USER_INVITED", "Pending", _clock.UtcNow, null));
                AddAudit(actorUserId, "UserInviteQueued", "User", userId, user.DisplayName, "Queued user invitation email.", "Admin Center", "{}");
            }
        }

        return Task.CompletedTask;
    }

    public Task<BenchVisibilityPolicy?> GetBenchVisibilityPolicyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<BenchVisibilityPolicy?>(new BenchVisibilityPolicy(
                _benchVisibilityRoleId,
                FindRoleName(_benchVisibilityRoleId),
                _tenant.UpdatedAtUtc,
                SystemActorId));
        }
    }

    public Task<bool> RoleIsActiveAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_roles.Any(role => role.TenantId == tenantId && role.RoleId == roleId && role.Status == "Active"));
        }
    }

    public Task UpdateBenchVisibilityPolicyAsync(Guid tenantId, Guid actorUserId, Guid roleId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _benchVisibilityRoleId = roleId;
            _tenant.UpdatedAtUtc = _clock.UtcNow;
            AddAudit(actorUserId, "BenchVisibilityPolicyUpdated", "AccessPolicy", roleId, "Bench visibility", "Updated bench visibility role.", "Admin Center", "{}");
        }

        return Task.CompletedTask;
    }

    public Task<AdminGroupsResponse> ListAsync(Guid tenantId, AdminGroupsQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var groups = _groups.Where(group => group.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(query.Purpose))
            {
                groups = groups.Where(group => group.Purpose.Contains(query.Purpose, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = groups.OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(group => new AdminGroupListItem(
                    group.GroupId,
                    group.Name,
                    group.Purpose,
                    group.Status,
                    _users.Count(user => user.GroupIds.Contains(group.GroupId))))
                .ToArray();

            return Task.FromResult(new AdminGroupsResponse(items, query.Page, query.PageSize, materialized.Count));
        }
    }

    public Task<AdminRolesResponse> ListAsync(Guid tenantId, AdminRolesQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var roles = _roles.Where(role => role.TenantId == tenantId);
            if (!query.IncludeInactive)
            {
                roles = roles.Where(role => role.Status == "Active");
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                roles = roles.Where(role => role.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = roles.OrderBy(role => role.Priority).ThenBy(role => role.Name).ToList();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(ToRoleSummary)
                .ToArray();

            var summary = new AdminRolesSummary(
                _roles.Count(role => role.TenantId == tenantId && role.Status == "Active"),
                _roles.Count(role => role.TenantId == tenantId && role.IsProtected),
                _roles.Count(role => role.TenantId == tenantId && role.Type == "Custom"));

            return Task.FromResult(new AdminRolesResponse(summary, items, query.Page, query.PageSize, materialized.Count));
        }
    }

    Task<RoleDetails?> IAdminRolesRepository.GetAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var role = _roles.FirstOrDefault(item => item.TenantId == tenantId && item.RoleId == roleId);
            return Task.FromResult(role is null ? null : ToRoleDetails(role));
        }
    }

    public Task<IReadOnlyList<PermissionCatalogItem>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PermissionCatalogItem>>(_permissions.ToArray());
        }
    }

    public Task<bool> PermissionIdsExistAsync(IReadOnlyCollection<string> permissionIds, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(permissionIds.All(permissionId => _permissions.Any(permission => permission.PermissionId == permissionId)));
        }
    }

    public Task<bool> RoleNameExistsAsync(Guid tenantId, string name, Guid? exceptRoleId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_roles.Any(role =>
                role.TenantId == tenantId &&
                role.RoleId != exceptRoleId &&
                string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<Guid> CreateAsync(Guid tenantId, Guid actorUserId, SaveRoleInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var roleId = Guid.NewGuid();
            _roles.Add(new RoleState
            {
                RoleId = roleId,
                TenantId = tenantId,
                Code = input.Name.Replace(" ", string.Empty, StringComparison.Ordinal),
                Name = input.Name.Trim(),
                Type = "Custom",
                Scope = input.Scope,
                Priority = input.Priority,
                Status = input.Status,
                IsProtected = false,
                IsBulkAssignable = true,
                PermissionIds = input.PermissionIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            });

            AddAudit(actorUserId, "RoleCreated", "Role", roleId, input.Name, "Created role.", "Admin Center", metadataJson);
            return Task.FromResult(roleId);
        }
    }

    public Task UpdateAsync(Guid tenantId, Guid actorUserId, Guid roleId, SaveRoleInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var role = _roles.FirstOrDefault(item => item.TenantId == tenantId && item.RoleId == roleId);
            if (role is not null)
            {
                role.Name = input.Name.Trim();
                role.Scope = input.Scope;
                role.Priority = input.Priority;
                role.Status = input.Status;
                role.PermissionIds = input.PermissionIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                AddAudit(actorUserId, "RoleUpdated", "Role", roleId, role.Name, "Updated role.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid tenantId, Guid actorUserId, Guid roleId, string status, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var role = _roles.FirstOrDefault(item => item.TenantId == tenantId && item.RoleId == roleId);
            if (role is not null)
            {
                role.Status = status;
                AddAudit(actorUserId, "RoleStatusUpdated", "Role", roleId, role.Name, $"Changed role status to {status}.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task<PermissionResolutionPolicy?> GetPermissionResolutionPolicyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<PermissionResolutionPolicy?>(new PermissionResolutionPolicy(
                _permissionResolutionMode.ToString(),
                _tenant.UpdatedAtUtc,
                SystemActorId));
        }
    }

    public Task UpdatePermissionResolutionPolicyAsync(Guid tenantId, Guid actorUserId, string mode, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _permissionResolutionMode = Enum.Parse<PermissionResolutionMode>(mode, ignoreCase: true);
            _tenant.UpdatedAtUtc = _clock.UtcNow;
            AddAudit(actorUserId, "PermissionResolutionPolicyUpdated", "AccessPolicy", tenantId, "Permission resolution", $"Updated permission resolution to {mode}.", "Admin Center", "{}");
        }

        return Task.CompletedTask;
    }

    public Task<RoleUserAssignmentPreview> PreviewUserAssignmentsAsync(Guid tenantId, Guid roleId, RoleUserAssignmentFilterInput input, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var matching = FilterUsersForRoleAssignment(tenantId, input).ToArray();
            var assignable = matching.Where(user => !user.RoleIds.Contains(roleId)).ToArray();
            var sample = assignable.Take(25).Select(ToRoleUserAssignmentPreviewItem).ToArray();

            return Task.FromResult(new RoleUserAssignmentPreview(
                matching.Length,
                matching.Length - assignable.Length,
                assignable.Length,
                sample));
        }
    }

    public Task<BulkAssignRoleUsersResponse> BulkAssignUsersAsync(Guid tenantId, Guid actorUserId, Guid roleId, BulkAssignRoleUsersInput input, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var matching = FilterUsersForRoleAssignment(tenantId, input.Filters).ToArray();
            var targetUsers = input.SelectionMode == "SelectedUsers"
                ? matching.Where(user => input.SelectedUserIds?.Contains(user.UserId) == true).ToArray()
                : matching;

            var assigned = 0;
            foreach (var user in targetUsers.Where(user => !user.RoleIds.Contains(roleId)))
            {
                user.RoleIds.Add(roleId);
                user.UpdatedAtUtc = _clock.UtcNow;
                assigned++;
            }

            var roleName = FindRoleName(roleId);
            AddAudit(actorUserId, "RoleBulkAssigned", "Role", roleId, roleName, $"Bulk assigned {roleName} to {assigned} users.", "Admin Center", "{}");
            return Task.FromResult(new BulkAssignRoleUsersResponse(Guid.NewGuid(), matching.Length, assigned, targetUsers.Length - assigned));
        }
    }

    public Task<AdminNotificationEventsResponse> ListEventsAsync(Guid tenantId, AdminNotificationEventsQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var events = _notificationEvents.Where(item => item.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                events = events.Where(item =>
                    item.EventCode.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    item.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = events.OrderBy(item => item.EventCode).ToList();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(ToNotificationEventListItem)
                .ToArray();

            var summary = new AdminNotificationEventsSummary(
                _notificationEvents.Count(item => item.TenantId == tenantId && item.Status == "Active"),
                _notificationTemplates.Count(item => item.TenantId == tenantId && item.Status == "Active"),
                _outbox.Count(item => item.TenantId == tenantId && item.Status == "Pending"),
                _outbox.Count(item => item.TenantId == tenantId && item.Status == "Failed"));

            return Task.FromResult(new AdminNotificationEventsResponse(summary, items, query.Page, query.PageSize, materialized.Count));
        }
    }

    public Task<AdminNotificationEventDetails?> GetEventAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var item = _notificationEvents.FirstOrDefault(item => item.TenantId == tenantId && item.EventId == eventId);
            if (item is null)
            {
                return Task.FromResult<AdminNotificationEventDetails?>(null);
            }

            var templates = _notificationTemplates
                .Where(template => template.TenantId == tenantId && template.EventCode == item.EventCode)
                .Select(ToNotificationTemplateSummary)
                .ToArray();

            return Task.FromResult<AdminNotificationEventDetails?>(new AdminNotificationEventDetails(
                item.EventId,
                item.EventCode,
                item.Name,
                item.Recipient,
                item.Status,
                templates));
        }
    }

    public Task<IReadOnlyList<NotificationTemplateSummary>> ListTemplatesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<NotificationTemplateSummary>>(_notificationTemplates
                .Where(template => template.TenantId == tenantId)
                .OrderBy(template => template.Name)
                .Select(ToNotificationTemplateSummary)
                .ToArray());
        }
    }

    public Task<NotificationTemplateSummary?> GetTemplateAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var template = _notificationTemplates.FirstOrDefault(item => item.TenantId == tenantId && item.TemplateId == templateId);
            return Task.FromResult(template is null ? null : ToNotificationTemplateSummary(template));
        }
    }

    public Task UpdateTemplateAsync(Guid tenantId, Guid actorUserId, Guid templateId, UpdateNotificationTemplateInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var template = _notificationTemplates.FirstOrDefault(item => item.TenantId == tenantId && item.TemplateId == templateId);
            if (template is not null)
            {
                template.Subject = input.Subject;
                template.Body = input.Body;
                template.UpdatedAtUtc = _clock.UtcNow;
                template.UpdatedByUserId = actorUserId;
                AddAudit(actorUserId, "NotificationTemplateUpdated", "NotificationTemplate", templateId, template.Name, "Updated notification template.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateEventStatusAsync(Guid tenantId, Guid actorUserId, Guid eventId, string status, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var item = _notificationEvents.FirstOrDefault(item => item.TenantId == tenantId && item.EventId == eventId);
            if (item is not null)
            {
                item.Status = status;
                item.UpdatedAtUtc = _clock.UtcNow;
                AddAudit(actorUserId, "NotificationEventStatusUpdated", "NotificationEvent", eventId, item.Name, $"Changed notification event status to {status}.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task<QueuedAdminTestNotification> QueueTestNotificationAsync(
        Guid tenantId,
        Guid actorUserId,
        string actorEmail,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            var testEvent = _notificationEvents.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                item.EventCode.Equals(AdminTestNotificationEventCode, StringComparison.OrdinalIgnoreCase));

            if (testEvent is null)
            {
                testEvent = new NotificationEventState(
                    Guid.NewGuid(),
                    tenantId,
                    AdminTestNotificationEventCode,
                    "Admin test notification",
                    "User:CurrentAdmin",
                    "Active",
                    now);
                _notificationEvents.Add(testEvent);
            }

            var notificationId = Guid.NewGuid();
            _operationNotifications.Add(new OperationsNotification(
                notificationId,
                actorUserId,
                title,
                message,
                "AdminNotificationTest",
                notificationId,
                null,
                now));

            var outboxId = Guid.NewGuid();
            _outbox.Add(new OutboxState(outboxId, tenantId, AdminTestNotificationEventCode, "Pending", now, null));
            AddAudit(actorUserId, "AdminTestNotificationQueued", "NotificationRecipient", notificationId, "Realtime notification test", "Queued realtime notification test.", "Admin Center", "{}");

            var notification = new RealtimeNotificationPayload(
                notificationId,
                tenantId,
                actorUserId,
                title,
                message,
                "AdminNotificationTest",
                notificationId,
                null,
                now,
                AdminTestNotificationEventCode);

            return Task.FromResult(new QueuedAdminTestNotification(outboxId, notification));
        }
    }

    public Task UpdateOutboxStatusAsync(
        Guid tenantId,
        Guid outboxId,
        string status,
        string? lastError,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var item = _outbox.FirstOrDefault(item => item.TenantId == tenantId && item.OutboxId == outboxId);
            if (item is not null)
            {
                item.Status = status;
                if (string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase))
                {
                    item.ProcessedAtUtc = _clock.UtcNow;
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var pending = _outbox
                .Where(item => item.Status == "Pending")
                .OrderBy(item => item.CreatedAtUtc)
                .Take(batchSize)
                .ToArray();

            foreach (var item in pending)
            {
                item.Status = "Delivered";
                item.ProcessedAtUtc = _clock.UtcNow;
            }

            return Task.FromResult(pending.Length);
        }
    }

    public Task<AdminAuditLogListResponse> ListAsync(Guid tenantId, AdminAuditLogQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var logs = _auditLogs.Where(log => log.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(query.Area))
            {
                logs = logs.Where(log => log.Area.Equals(query.Area, StringComparison.OrdinalIgnoreCase));
            }

            if (query.ActorId.HasValue)
            {
                logs = logs.Where(log => log.ActorUserId == query.ActorId);
            }

            if (!string.IsNullOrWhiteSpace(query.EntityType))
            {
                logs = logs.Where(log => log.EntityType.Equals(query.EntityType, StringComparison.OrdinalIgnoreCase));
            }

            if (query.EntityId.HasValue)
            {
                logs = logs.Where(log => log.EntityId == query.EntityId);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                logs = logs.Where(log =>
                    log.EventSummary.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    log.RecordLabel.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = logs.OrderByDescending(log => log.OccurredAtUtc).ToList();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(log => new AdminAuditLogListItem(
                    log.AuditLogId,
                    log.OccurredAtUtc,
                    log.ActorDisplayName,
                    log.EventSummary,
                    log.RecordLabel,
                    log.Area))
                .ToArray();

            var today = _clock.UtcNow.Date;
            var summary = new AdminAuditLogSummary(
                _auditLogs.Count(log => log.TenantId == tenantId && log.OccurredAtUtc.UtcDateTime.Date == today),
                _auditLogs.Count(log => log.TenantId == tenantId && log.Area == "Admin Center"),
                _auditLogs.Count(log => log.TenantId == tenantId && log.Area == "Workflow"),
                _auditLogs.Count(log => log.TenantId == tenantId && log.Area == "AI"));

            return Task.FromResult(new AdminAuditLogListResponse(summary, items, query.Page, query.PageSize, materialized.Count));
        }
    }

    Task<AdminAuditLogDetails?> IAdminAuditLogRepository.GetAsync(Guid tenantId, Guid auditLogId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var log = _auditLogs.FirstOrDefault(item => item.TenantId == tenantId && item.AuditLogId == auditLogId);
            return Task.FromResult(log is null ? null : new AdminAuditLogDetails(
                log.AuditLogId,
                log.OccurredAtUtc,
                log.ActorUserId,
                log.ActorDisplayName,
                log.EventType,
                log.EntityType,
                log.EntityId,
                log.RecordLabel,
                log.EventSummary,
                log.Area,
                log.MetadataJson));
        }
    }

    private void SeedPermissions()
    {
        _permissions.AddRange([
            new PermissionCatalogItem(AccessConstants.ManageAdminCenter, "Manage Admin Center", "Administration", "Configure tenant, roles, groups, and settings.", "Active"),
            new PermissionCatalogItem(AccessConstants.ManageUsers, "Manage Users", "Administration", "Create and maintain internal users.", "Active"),
            new PermissionCatalogItem(AccessConstants.ManageRoles, "Manage Roles", "Administration", "Maintain tenant roles and permission policy.", "Active"),
            new PermissionCatalogItem(AccessConstants.ViewAuditLogs, "View Audit Logs", "Governance", "Review audit events stored in UTC.", "Active"),
            new PermissionCatalogItem(AccessConstants.CreateJobRequest, "Create Job Request", "Recruitment", "Create presales resource requests.", "Active"),
            new PermissionCatalogItem(AccessConstants.ViewJobRequests, "View Job Requests", "Recruitment", "View job requests and assigned work.", "Active"),
            new PermissionCatalogItem(AccessConstants.ClaimWorkflowTask, "Claim Workflow Task", "Recruitment", "Claim assigned recruitment workflow tasks.", "Active"),
            new PermissionCatalogItem("candidates.manage", "Manage Candidates", "Recruitment", "Manage candidates and applications.", "Active"),
            new PermissionCatalogItem("interviews.feedback", "Submit Interview Feedback", "Recruitment", "Submit interview scorecards and notes.", "Active")
        ]);
    }

    private void SeedRoles()
    {
        var tenantAdmin = AddRole(AccessConstants.TenantAdminRoleCode, "Tenant Admin", "System", "Tenant", 1, true, false, [
            AccessConstants.ManageAdminCenter,
            AccessConstants.ManageUsers,
            AccessConstants.ManageRoles,
            AccessConstants.ViewAuditLogs
        ]);
        AddRole("Presales", "Presales", "Tenant", "Tenant", 10, false, true, [
            AccessConstants.CreateJobRequest,
            AccessConstants.ViewJobRequests
        ]);
        var pmo = AddRole(AccessConstants.PmoRoleCode, "PMO", "Tenant", "Tenant", 20, false, true, [
            AccessConstants.ViewJobRequests,
            AccessConstants.ClaimWorkflowTask
        ]);
        AddRole("Recruiter", "Recruiter", "Tenant", "Tenant", 30, false, true, [
            AccessConstants.ViewJobRequests,
            AccessConstants.ClaimWorkflowTask,
            "candidates.manage"
        ]);
        AddRole("HiringManager", "Hiring Manager", "Tenant", "Tenant", 40, false, true, [
            AccessConstants.ViewJobRequests
        ]);
        AddRole("Interviewer", "Interviewer", "Tenant", "Tenant", 50, false, true, [
            "interviews.feedback"
        ]);
        AddRole("Employee", "Employee", "Tenant", "Tenant", 90, false, true, [
            AccessConstants.ViewJobRequests
        ]);
        AddRole("Candidate", "Candidate", "System", "Portal", 100, true, false, []);

        _benchVisibilityRoleId = pmo.RoleId;
        _ = tenantAdmin;
    }

    private void SeedGroups()
    {
        AddGroup("Admin Team", "Tenant administration");
        AddGroup("PMO Group", "Presales-created requests");
        AddGroup("Recruitment Team", "PMO forward-to-recruiter handoff");
        AddGroup("Delivery Leadership", "Final hiring-manager review");
        AddGroup("Engineering Interviewers", "Interview assignments");
        AddGroup("Engineering", "Employee department grouping");
    }

    private void SeedUsers()
    {
        var tenantAdmin = FindRoleByCode(AccessConstants.TenantAdminRoleCode).RoleId;
        var presales = FindRoleByCode("Presales").RoleId;
        var pmo = FindRoleByCode(AccessConstants.PmoRoleCode).RoleId;
        var recruiter = FindRoleByCode("Recruiter").RoleId;
        var hiringManager = FindRoleByCode("HiringManager").RoleId;
        var interviewer = FindRoleByCode("Interviewer").RoleId;
        var employee = FindRoleByCode("Employee").RoleId;
        var candidate = FindRoleByCode("Candidate").RoleId;

        AddUser("11111111-1111-1111-1111-111111111111", "Mudasar Ahmad", "admin@tkxel.com", [tenantAdmin], ["Admin Team"], "Administration", _clock.UtcNow.AddMinutes(-70));
        AddUser("22222222-2222-2222-2222-222222222222", "Ahmed Raza", "presales@tkxel.com", [presales], ["Presales Team"], "Presales", _clock.UtcNow.AddMinutes(-30));
        AddUser("33333333-3333-3333-3333-333333333333", "Ali Khan", "pmo@tkxel.com", [pmo], ["PMO Group"], "PMO", _clock.UtcNow.AddDays(-1));
        AddUser("44444444-4444-4444-4444-444444444444", "Sara Malik", "recruiter@tkxel.com", [recruiter], ["Recruitment Team"], "Recruitment", _clock.UtcNow.AddHours(-2));
        AddUser("55555555-5555-5555-5555-555555555555", "Fatima Noor", "hiring.manager@tkxel.com", [hiringManager], ["Delivery Leadership"], "Engineering", _clock.UtcNow.AddMinutes(-90));
        AddUser("66666666-6666-6666-6666-666666666666", "Bilal Hussain", "interviewer@tkxel.com", [interviewer], ["Engineering Interviewers"], "Engineering", _clock.UtcNow.AddDays(-3));
        AddUser("77777777-7777-7777-7777-777777777777", "Bench Employee", "employee@tkxel.com", [employee], ["Engineering"], "Engineering", _clock.UtcNow.AddDays(-5));
        AddUser("88888888-8888-8888-8888-888888888888", "Ayesha Khan", "ayesha.khan@example.com", [candidate], [], "Candidate", _clock.UtcNow.AddDays(-2));
    }

    private void SeedEmployees()
    {
        var allocatedUser = _users.FirstOrDefault(user => string.Equals(user.Email, "interviewer@tkxel.com", StringComparison.OrdinalIgnoreCase));
        if (allocatedUser is not null)
        {
            _employees.Add(new EmployeeState
            {
                EmployeeId = allocatedUser.UserId,
                TenantId = TenantId,
                EmployeeCode = "EMP-000",
                DisplayName = allocatedUser.DisplayName,
                Email = allocatedUser.Email,
                Designation = "Senior Software Engineer",
                Department = allocatedUser.DepartmentName,
                Location = "Karachi",
                Skills = ["C#", "Azure"],
                Status = allocatedUser.AccountStatus,
                AvailabilityStatus = "Allocated",
                BenchStatus = "Allocated",
                CurrentAllocationPercent = 100
            });
        }

        var benchUser = _users.FirstOrDefault(user => string.Equals(user.Email, "employee@tkxel.com", StringComparison.OrdinalIgnoreCase));
        if (benchUser is null)
        {
            return;
        }

        _employees.Add(new EmployeeState
        {
            EmployeeId = benchUser.UserId,
            TenantId = TenantId,
            EmployeeCode = "EMP-001",
            DisplayName = benchUser.DisplayName,
            Email = benchUser.Email,
            Designation = "Software Engineer",
            Department = benchUser.DepartmentName,
            Location = "Karachi",
            Skills = ["C#", "SQL Server", "Angular", "Talent Acquisition"],
            Status = benchUser.AccountStatus,
            AvailabilityStatus = "Available",
            BenchStatus = "Benched",
            CurrentAllocationPercent = 0
        });
    }

    private void SeedNotifications()
    {
        AddNotification("CREATE_BY_PRESALES", "New request assigned", "PMO Group", "New request assigned", "A resource request is ready for PMO review.", ["requestTitle", "createdBy"]);
        AddNotification("PMO_EMPLOYEE_REFERRED", "Resource referred", "Presales owner", "Resource referred", "PMO referred an internal employee.", ["requestTitle", "employeeName"]);
        AddNotification("CANDIDATE_INVITED", "Candidate invite", "Candidate", "Candidate invite", "You have been invited to apply for a Talent Pilot job.", ["jobTitle", "inviteUrl"]);
        AddNotification("INTERVIEW_SCHEDULED", "Interview scheduled", "Interviewer and candidate", "Interview scheduled", "An interview has been scheduled.", ["candidateName", "scheduledAt"]);
        AddNotification("HIRING_MANAGER_REVIEW_ASSIGNED", "Hiring Manager review assigned", "Hiring Manager", "Candidate ready for final review", "Interview feedback is ready for your review.", ["candidateName", "jobTitle"]);
    }

    private void SeedAuditLogs()
    {
        AddAudit(SystemActorId, "UserRoleAssigned", "User", _users[2].UserId, "PMO user", "Updated PMO user role assignments.", "Admin Center", "{}");
        AddAudit(_users[2].UserId, "BenchEmployeeProposed", "JobRequest", Guid.NewGuid(), "Bench referral", "Proposed bench employee for request.", "Workflow", "{}");
        AddAudit(SystemActorId, "RequirementExtracted", "JobRequest", Guid.NewGuid(), "Job request", "Completed requirement extraction.", "AI", "{}");
        AddAudit(_users[3].UserId, "CandidateInviteCreated", "CandidateInvite", Guid.NewGuid(), "Candidate invite", "Created candidate invite link.", "Talent Pilot App", "{}");
    }

    private RoleState AddRole(string code, string name, string type, string scope, int priority, bool isProtected, bool isBulkAssignable, IReadOnlyList<string> permissionIds)
    {
        var role = new RoleState
        {
            RoleId = Guid.NewGuid(),
            TenantId = TenantId,
            Code = code,
            Name = name,
            Type = type,
            Scope = scope,
            Priority = priority,
            Status = "Active",
            IsProtected = isProtected,
            IsBulkAssignable = isBulkAssignable,
            PermissionIds = permissionIds.ToList()
        };
        _roles.Add(role);
        return role;
    }

    private void AddGroup(string name, string purpose)
    {
        _groups.Add(new GroupState
        {
            GroupId = Guid.NewGuid(),
            TenantId = TenantId,
            Name = name,
            Purpose = purpose,
            Status = "Active"
        });
    }

    private void AddUser(string id, string displayName, string email, IReadOnlyList<Guid> roleIds, IReadOnlyList<string> groupNames, string departmentName, DateTimeOffset lastActiveAtUtc)
    {
        var groupIds = _groups
            .Where(group => groupNames.Contains(group.Name, StringComparer.OrdinalIgnoreCase))
            .Select(group => group.GroupId)
            .ToList();

        _users.Add(new UserState
        {
            UserId = Guid.Parse(id),
            TenantId = TenantId,
            DisplayName = displayName,
            Email = email,
            Initials = BuildInitials(displayName),
            AccountStatus = "Active",
            RoleIds = roleIds.ToList(),
            GroupIds = groupIds,
            DepartmentName = departmentName,
            LastActiveAtUtc = lastActiveAtUtc,
            CreatedAtUtc = _clock.UtcNow.AddDays(-10),
            UpdatedAtUtc = _clock.UtcNow.AddDays(-1)
        });
    }

    private void AddNotification(string eventCode, string name, string recipient, string subject, string body, IReadOnlyList<string> variables)
    {
        _notificationEvents.Add(new NotificationEventState(Guid.NewGuid(), TenantId, eventCode, name, recipient, "Active", _clock.UtcNow));
        _notificationTemplates.Add(new NotificationTemplateState
        {
            TemplateId = Guid.NewGuid(),
            TenantId = TenantId,
            EventCode = eventCode,
            Name = name,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Variables = variables.ToList(),
            Status = "Active",
            UpdatedAtUtc = _clock.UtcNow,
            UpdatedByUserId = SystemActorId
        });
    }

    private void AddAudit(Guid actorUserId, string eventType, string entityType, Guid? entityId, string recordLabel, string eventSummary, string area, string metadataJson)
    {
        var actor = _users.FirstOrDefault(user => user.UserId == actorUserId)?.DisplayName ?? "System";
        _auditLogs.Add(new AuditLogState
        {
            AuditLogId = Guid.NewGuid(),
            TenantId = TenantId,
            OccurredAtUtc = _clock.UtcNow,
            ActorUserId = actorUserId == SystemActorId ? null : actorUserId,
            ActorDisplayName = actor,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            RecordLabel = recordLabel,
            EventSummary = eventSummary,
            Area = area,
            MetadataJson = metadataJson
        });
    }

    private IEnumerable<UserState> FilterUsersForRoleAssignment(Guid tenantId, RoleUserAssignmentFilterInput input)
    {
        var users = _users.Where(user => user.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            users = users.Where(user =>
                user.DisplayName.Contains(input.Search, StringComparison.OrdinalIgnoreCase) ||
                user.Email.Contains(input.Search, StringComparison.OrdinalIgnoreCase) ||
                user.DepartmentName.Contains(input.Search, StringComparison.OrdinalIgnoreCase));
        }

        if (input.AccountStatuses is { Count: > 0 })
        {
            users = users.Where(user => input.AccountStatuses.Contains(user.AccountStatus, StringComparer.OrdinalIgnoreCase));
        }

        if (input.CurrentRoleIds is { Count: > 0 })
        {
            users = users.Where(user => user.RoleIds.Any(input.CurrentRoleIds.Contains));
        }

        if (input.GroupIds is { Count: > 0 })
        {
            users = users.Where(user => user.GroupIds.Any(input.GroupIds.Contains));
        }

        return users.OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase);
    }

    private AuthUserRecord ToAuthUserRecord(UserState user)
    {
        return new AuthUserRecord
        {
            UserId = user.UserId,
            TenantId = user.TenantId,
            DisplayName = user.DisplayName,
            Email = user.Email,
            AccountStatus = user.AccountStatus,
            PasswordHash = user.PasswordHash
        };
    }

    private AdminUserListItem ToUserListItem(UserState user)
    {
        var roles = _roles.Where(role => user.RoleIds.Contains(role.RoleId)).OrderBy(role => role.Priority).ToArray();
        var highestRole = roles.First();
        var groups = _groups.Where(group => user.GroupIds.Contains(group.GroupId)).OrderBy(group => group.Name).ToArray();
        return new AdminUserListItem(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.Initials,
            roles.Select(role => role.RoleId).ToArray(),
            roles.Select(role => role.Name).ToArray(),
            highestRole.RoleId,
            highestRole.Name,
            highestRole.Priority,
            groups.Select(group => group.GroupId).ToArray(),
            groups.Select(group => group.Name).ToArray(),
            user.AccountStatus,
            user.LastActiveAtUtc,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
    }

    private OperationsPerson ToOperationsPerson(UserState user)
    {
        var roles = _roles.Where(role => user.RoleIds.Contains(role.RoleId)).OrderBy(role => role.Priority).ToArray();
        return new OperationsPerson(
            user.UserId,
            user.DisplayName,
            user.Email,
            roles.Select(role => role.Code).ToArray(),
            roles.Select(role => role.Name).ToArray());
    }

    private OperationsBenchMatch[] GetBenchMatchesForJob(Guid tenantId, OperationsJobRequest jobRequest)
    {
        return _employees
            .Where(employee =>
                employee.TenantId == tenantId &&
                employee.Status == "Active" &&
                employee.AvailabilityStatus == "Available" &&
                employee.BenchStatus == "Benched" &&
                employee.CurrentAllocationPercent == 0)
            .Select(employee => ToBenchMatch(employee, jobRequest.Skills))
            .OrderByDescending(match => match.MatchScore)
            .ThenBy(match => match.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static OperationsBenchMatch ToBenchMatch(EmployeeState employee, IReadOnlyCollection<string> requiredSkills)
    {
        var skills = employee.Skills.Count == 0 ? new[] { "Generalist" } : employee.Skills.ToArray();
        var matchScore = OperationsBenchMatchScoring.CalculateScore(requiredSkills, skills);
        var explanation = OperationsBenchMatchScoring.BuildExplanation(requiredSkills, skills, employee.CurrentAllocationPercent);

        return new OperationsBenchMatch(
            employee.EmployeeId,
            employee.EmployeeCode,
            employee.DisplayName,
            employee.Email,
            employee.Designation,
            employee.Department,
            employee.Location,
            skills,
            employee.AvailabilityStatus,
            employee.BenchStatus,
            employee.CurrentAllocationPercent,
            matchScore,
            explanation);
    }

    private static string BuildReferralSummary(OperationsBenchMatch match, string? note)
    {
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return trimmedNote is null
            ? match.MatchExplanation
            : $"{match.MatchExplanation} Note: {trimmedNote}";
    }

    private static InternalEmployeeReferral ToInternalEmployeeReferral(EmployeeReferralState referral)
    {
        return new InternalEmployeeReferral(
            referral.ReferralId,
            referral.JobRequestId,
            referral.EmployeeId,
            referral.EmployeeName,
            referral.EmployeeEmail,
            referral.Status,
            referral.FitScore,
            referral.RecommendationSummary,
            referral.ReferredByUserId,
            referral.PresalesUserId,
            referral.CreatedAtUtc);
    }

    private AdminUserDetails ToUserDetails(UserState user)
    {
        return new AdminUserDetails(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.Initials,
            user.RoleIds.ToArray(),
            user.GroupIds.ToArray(),
            user.AccountStatus,
            user.LastActiveAtUtc,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
    }

    private RoleSummary ToRoleSummary(RoleState role)
    {
        return new RoleSummary(
            role.RoleId,
            role.Name,
            role.Type,
            role.Scope,
            _users.Count(user => user.RoleIds.Contains(role.RoleId)),
            BuildPermissionSummary(role),
            role.IsProtected ? "Protected" : role.Status,
            role.IsProtected,
            role.IsBulkAssignable);
    }

    private RoleDetails ToRoleDetails(RoleState role)
    {
        return new RoleDetails(
            role.RoleId,
            role.Name,
            role.Type,
            role.Scope,
            role.Priority,
            role.IsProtected ? "Protected" : role.Status,
            role.IsProtected,
            role.IsBulkAssignable,
            role.PermissionIds.ToArray());
    }

    private RoleUserAssignmentPreviewItem ToRoleUserAssignmentPreviewItem(UserState user)
    {
        return new RoleUserAssignmentPreviewItem(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.DepartmentName,
            ToUserListItem(user).HighestPriorityRoleName,
            user.AccountStatus);
    }

    private AdminNotificationEventListItem ToNotificationEventListItem(NotificationEventState item)
    {
        var template = _notificationTemplates.FirstOrDefault(template => template.EventCode == item.EventCode);
        return new AdminNotificationEventListItem(
            item.EventId,
            item.EventCode,
            item.Name,
            item.Recipient,
            template?.Name ?? "Unlinked template",
            item.Status,
            item.UpdatedAtUtc);
    }

    private NotificationTemplateSummary ToNotificationTemplateSummary(NotificationTemplateState template)
    {
        return new NotificationTemplateSummary(
            template.TemplateId,
            template.EventCode,
            template.Name,
            template.Recipient,
            template.Subject,
            template.Body,
            template.Variables.ToArray(),
            template.Status,
            template.UpdatedAtUtc,
            template.UpdatedByUserId);
    }

    private UserState? FindUser(Guid tenantId, Guid userId)
    {
        return _users.FirstOrDefault(user => user.TenantId == tenantId && user.UserId == userId);
    }

    private RoleState FindRoleByCode(string code)
    {
        return _roles.First(role => role.Code == code);
    }

    private string FindRoleName(Guid roleId)
    {
        return _roles.FirstOrDefault(role => role.RoleId == roleId)?.Name ?? "Unknown role";
    }

    private string BuildPermissionSummary(RoleState role)
    {
        if (role.PermissionIds.Count == 0)
        {
            return "No permissions";
        }

        return string.Join(", ", role.PermissionIds.Take(3).Select(permissionId =>
            _permissions.FirstOrDefault(permission => permission.PermissionId == permissionId)?.DisplayName ?? permissionId));
    }

    private static string BuildInitials(string displayName)
    {
        var parts = displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]));

        return string.Concat(parts);
    }

    private sealed class TenantState
    {
        public Guid TenantId { get; init; }
        public string DisplayName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string AdminContactEmail { get; set; } = string.Empty;
        public string DefaultTimezone { get; set; } = string.Empty;
        public string DefaultCurrency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CareerDisplayName { get; set; } = string.Empty;
        public string PrimaryColor { get; set; } = string.Empty;
        public bool CandidateLoginRequired { get; set; }
        public string CandidateCvFormat { get; set; } = string.Empty;
        public bool PublicJobsEnabled { get; set; }
        public int InviteExpiryDays { get; set; }
        public int ReapplyCooldownDays { get; set; }
        public bool SetupComplete { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class RoleState
    {
        public Guid RoleId { get; init; }
        public Guid TenantId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsProtected { get; init; }
        public bool IsBulkAssignable { get; init; }
        public List<string> PermissionIds { get; set; } = [];
    }

    private sealed class UserState
    {
        public Guid UserId { get; init; }
        public Guid TenantId { get; init; }
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string AccountStatus { get; set; } = string.Empty;
        public string? PasswordHash { get; init; }
        public List<Guid> RoleIds { get; set; } = [];
        public List<Guid> GroupIds { get; set; } = [];
        public string DepartmentName { get; init; } = string.Empty;
        public DateTimeOffset? LastActiveAtUtc { get; set; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class EmployeeState
    {
        public Guid EmployeeId { get; init; }
        public Guid TenantId { get; init; }
        public string EmployeeCode { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? Designation { get; init; }
        public string Department { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public List<string> Skills { get; init; } = [];
        public string Status { get; init; } = string.Empty;
        public string AvailabilityStatus { get; init; } = string.Empty;
        public string BenchStatus { get; init; } = string.Empty;
        public int CurrentAllocationPercent { get; init; }
    }

    private sealed class EmployeeReferralState
    {
        public Guid ReferralId { get; init; }
        public Guid TenantId { get; init; }
        public Guid JobRequestId { get; init; }
        public Guid EmployeeId { get; init; }
        public string EmployeeName { get; init; } = string.Empty;
        public string EmployeeEmail { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int FitScore { get; init; }
        public string RecommendationSummary { get; init; } = string.Empty;
        public Guid ReferredByUserId { get; init; }
        public Guid? PresalesUserId { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
    }

    private sealed class GroupState
    {
        public Guid GroupId { get; init; }
        public Guid TenantId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    private sealed class NotificationEventState
    {
        public NotificationEventState(
            Guid eventId,
            Guid tenantId,
            string eventCode,
            string name,
            string recipient,
            string status,
            DateTimeOffset updatedAtUtc)
        {
            EventId = eventId;
            TenantId = tenantId;
            EventCode = eventCode;
            Name = name;
            Recipient = recipient;
            Status = status;
            UpdatedAtUtc = updatedAtUtc;
        }

        public Guid EventId { get; }
        public Guid TenantId { get; }
        public string EventCode { get; }
        public string Name { get; }
        public string Recipient { get; }
        public string Status { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class NotificationTemplateState
    {
        public Guid TemplateId { get; init; }
        public Guid TenantId { get; init; }
        public string EventCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Recipient { get; init; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public List<string> Variables { get; init; } = [];
        public string Status { get; init; } = string.Empty;
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public Guid UpdatedByUserId { get; set; }
    }

    private sealed class OutboxState
    {
        public OutboxState(
            Guid outboxId,
            Guid tenantId,
            string eventCode,
            string status,
            DateTimeOffset createdAtUtc,
            DateTimeOffset? processedAtUtc)
        {
            OutboxId = outboxId;
            TenantId = tenantId;
            EventCode = eventCode;
            Status = status;
            CreatedAtUtc = createdAtUtc;
            ProcessedAtUtc = processedAtUtc;
        }

        public Guid OutboxId { get; }
        public Guid TenantId { get; }
        public string EventCode { get; }
        public string Status { get; set; }
        public DateTimeOffset CreatedAtUtc { get; }
        public DateTimeOffset? ProcessedAtUtc { get; set; }
    }

    private sealed class AuditLogState
    {
        public Guid AuditLogId { get; init; }
        public Guid TenantId { get; init; }
        public DateTimeOffset OccurredAtUtc { get; init; }
        public Guid? ActorUserId { get; init; }
        public string ActorDisplayName { get; init; } = string.Empty;
        public string EventType { get; init; } = string.Empty;
        public string EntityType { get; init; } = string.Empty;
        public Guid? EntityId { get; init; }
        public string RecordLabel { get; init; } = string.Empty;
        public string EventSummary { get; init; } = string.Empty;
        public string Area { get; init; } = string.Empty;
        public string MetadataJson { get; init; } = "{}";
    }
}
