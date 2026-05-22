using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Operations;
using TalentPilot.Common.Time;
using TalentPilot.Domain.Access;
using TalentPilot.Infrastructure.Persistence.Repositories;

namespace TalentPilot.Tests.Application;

public sealed class OperationsServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PresalesUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PmoUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task CreateJobRequestAsync_WithoutCreatePermission_ReturnsForbiddenAndDoesNotCallRepository()
    {
        var repository = new FakeOperationsRepository();
        var service = new OperationsService(repository, new FakeCurrentUser([], []));

        var result = await service.CreateJobRequestAsync(CreateValidInput(), CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("auth.forbidden", result.Error.Code);
        Assert.False(repository.CreateJobRequestCalled);
    }

    [Fact]
    public async Task GetPmoQueueAsync_ForTenantAdmin_UsesTenantAdminFallback()
    {
        var repository = new FakeOperationsRepository();
        var service = new OperationsService(
            repository,
            new FakeCurrentUser([AccessConstants.TenantAdminRoleCode], []));

        var result = await service.GetPmoQueueAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(repository.LastPmoQueueTenantAdminFallback);
    }

    [Fact]
    public async Task ClaimAssignmentAsync_WithClaimPermission_CallsRepository()
    {
        var assignmentId = Guid.NewGuid();
        var repository = new FakeOperationsRepository { ClaimResult = true };
        var service = new OperationsService(
            repository,
            new FakeCurrentUser([], [AccessConstants.ClaimWorkflowTask]));

        var result = await service.ClaimAssignmentAsync(assignmentId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(assignmentId, repository.LastClaimAssignmentId);
    }

    [Fact]
    public async Task ForwardToRecruiterAsync_WithoutClaimPermission_ReturnsForbiddenAndDoesNotCallRepository()
    {
        var repository = new FakeOperationsRepository();
        var service = new OperationsService(repository, new FakeCurrentUser([], []));

        var result = await service.ForwardToRecruiterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("auth.forbidden", result.Error.Code);
        Assert.Null(repository.LastForwardedJobRequestId);
    }

    [Fact]
    public async Task ForwardToRecruiterAsync_WithClaimPermission_CallsRepository()
    {
        var jobRequestId = Guid.NewGuid();
        var repository = new FakeOperationsRepository { ForwardResult = CreateForwardResult(jobRequestId) };
        var service = new OperationsService(
            repository,
            new FakeCurrentUser([], [AccessConstants.ClaimWorkflowTask]));

        var result = await service.ForwardToRecruiterAsync(jobRequestId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(jobRequestId, repository.LastForwardedJobRequestId);
    }

    [Fact]
    public async Task GetBenchMatchesAsync_WithoutBenchPermission_ReturnsForbiddenAndDoesNotCallRepository()
    {
        var jobRequest = CreateJobRequest(Guid.NewGuid());
        var repository = new FakeOperationsRepository { JobRequest = jobRequest };
        var service = new OperationsService(repository, new FakeCurrentUser([], []));

        var result = await service.GetBenchMatchesAsync(jobRequest.Id, CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("auth.forbidden", result.Error.Code);
        Assert.Null(repository.LastBenchMatchesJobRequestId);
    }

    [Fact]
    public async Task GetBenchMatchesAsync_WithBenchPermission_CallsRepository()
    {
        var jobRequest = CreateJobRequest(Guid.NewGuid());
        var repository = new FakeOperationsRepository
        {
            JobRequest = jobRequest,
            BenchMatches = [CreateBenchMatch(Guid.NewGuid())]
        };
        var service = new OperationsService(
            repository,
            new FakeCurrentUser([], [AccessConstants.ViewBenchMatches]));

        var result = await service.GetBenchMatchesAsync(jobRequest.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(jobRequest.Id, repository.LastBenchMatchesJobRequestId);
        Assert.Single(result.Value);
    }

    [Fact]
    public async Task CreateInternalResourceReferralAsync_EmptyEmployees_ReturnsValidationAndDoesNotCallRepository()
    {
        var repository = new FakeOperationsRepository();
        var service = new OperationsService(
            repository,
            new FakeCurrentUser([], [AccessConstants.ClaimWorkflowTask, AccessConstants.ViewBenchMatches]));

        var result = await service.CreateInternalResourceReferralAsync(
            Guid.NewGuid(),
            new CreateInternalResourceReferralInput([], "Looks aligned."),
            CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("employee_referral.employee_required", result.Error.Code);
        Assert.Null(repository.LastReferralJobRequestId);
    }

    [Fact]
    public async Task CreateInternalResourceReferralAsync_WithPermissions_NormalizesEmployeesAndCallsRepository()
    {
        var jobRequestId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var repository = new FakeOperationsRepository
        {
            ReferralResult = new CreateInternalResourceReferralResult(
                CreateJobRequest(jobRequestId),
                [CreateReferral(jobRequestId, employeeId)])
        };
        var service = new OperationsService(
            repository,
            new FakeCurrentUser([], [AccessConstants.ClaimWorkflowTask, AccessConstants.ViewBenchMatches]));

        var result = await service.CreateInternalResourceReferralAsync(
            jobRequestId,
            new CreateInternalResourceReferralInput([employeeId, employeeId, Guid.Empty], "Ready for Presales."),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(jobRequestId, repository.LastReferralJobRequestId);
        Assert.NotNull(repository.LastReferralInput);
        Assert.Equal(new[] { employeeId }, repository.LastReferralInput.EmployeeIds);
    }

    [Fact]
    public async Task InMemoryRepository_BenchReferral_ReturnsOnlyAvailableBenchEmployeesAndNotifiesPresales()
    {
        var repository = new InMemoryTalentPilotRepository(new FixedClock(DateTimeOffset.Parse("2026-05-22T10:00:00Z")));
        var created = await repository.CreateJobRequestAsync(TenantId, PresalesUserId, CreateValidInput(), CancellationToken.None);

        var claimed = await repository.ClaimAssignmentAsync(TenantId, PmoUserId, created.Assignment.Id, CancellationToken.None);
        var matches = await repository.GetBenchMatchesAsync(TenantId, PmoUserId, created.JobRequest.Id, canViewAll: false, CancellationToken.None);
        var match = Assert.Single(matches);
        var referral = await repository.CreateInternalResourceReferralAsync(
            TenantId,
            PmoUserId,
            created.JobRequest.Id,
            new CreateInternalResourceReferralInput([match.EmployeeId], "Strong fit for immediate bench proposal."),
            includeTenantAdminFallback: false,
            CancellationToken.None);

        Assert.True(claimed);
        Assert.Equal("Available", match.AvailabilityStatus);
        Assert.Equal("Benched", match.BenchStatus);
        Assert.Equal(0, match.CurrentAllocationPercent);
        Assert.Contains("C#", match.Skills);
        Assert.NotNull(referral);
        Assert.Equal(match.EmployeeId, Assert.Single(referral.Referrals).EmployeeId);

        var presalesNotifications = await repository.ListNotificationsAsync(TenantId, PresalesUserId, CancellationToken.None);
        Assert.Contains(presalesNotifications, notification =>
            notification.EntityType == "JobRequest" &&
            notification.EntityId == created.JobRequest.Id &&
            notification.Title == "Internal resource referred");
    }

    private static CreateOperationsJobRequestInput CreateValidInput()
    {
        return new CreateOperationsJobRequestInput(
            "Senior .NET Engineer",
            "Enterprise Client",
            "Build and maintain APIs.",
            "Engineering",
            ["C#", "SQL Server"],
            "5-8 years",
            "Karachi",
            1,
            "High",
            Guid.NewGuid());
    }

    private static ForwardToRecruiterResult CreateForwardResult(Guid jobRequestId)
    {
        var assignmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        return new ForwardToRecruiterResult(
            new OperationsJobRequest(
                jobRequestId,
                "TP-REQ-001",
                "Senior .NET Engineer",
                "Enterprise Client",
                "Build and maintain APIs.",
                "Engineering",
                ["C#", "SQL Server"],
                "5-8 years",
                "Karachi",
                1,
                0,
                "High",
                Guid.NewGuid(),
                UserId,
                "Recruiter Sourcing",
                null,
                "Recruitment Team",
                "NotPublished",
                now),
            new OperationsWorkflowAssignment(
                assignmentId,
                "JobRequest",
                jobRequestId,
                "Recruiter Sourcing",
                Guid.NewGuid(),
                "Recruitment Team",
                null,
                null,
                "Pending",
                now),
            CandidateCount: 0);
    }

    private static OperationsJobRequest CreateJobRequest(Guid jobRequestId)
    {
        return new OperationsJobRequest(
            jobRequestId,
            "TP-REQ-001",
            "Senior .NET Engineer",
            "Enterprise Client",
            "Build and maintain APIs.",
            "Engineering",
            ["C#", "SQL Server"],
            "5-8 years",
            "Karachi",
            1,
            0,
            "High",
            Guid.NewGuid(),
            UserId,
            "PMO Review",
            null,
            "PMO Group",
            "NotPublished",
            DateTimeOffset.UtcNow);
    }

    private static OperationsBenchMatch CreateBenchMatch(Guid employeeId)
    {
        return new OperationsBenchMatch(
            employeeId,
            "EMP-001",
            "Bench Employee",
            "employee@tkxel.com",
            "Software Engineer",
            "Engineering",
            "Karachi",
            ["C#", "SQL Server"],
            "Available",
            "Benched",
            0,
            100,
            "Matched 2 of 2 requested skills: C#, SQL Server. Available and benched with 0% active allocation.");
    }

    private static InternalEmployeeReferral CreateReferral(Guid jobRequestId, Guid employeeId)
    {
        return new InternalEmployeeReferral(
            Guid.NewGuid(),
            jobRequestId,
            employeeId,
            "Bench Employee",
            "employee@tkxel.com",
            "Referred",
            100,
            "Ready for Presales.",
            UserId,
            PresalesUserId,
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeCurrentUser : ICurrentUserAccessor
    {
        public FakeCurrentUser(IEnumerable<string> roleCodes, IEnumerable<string> permissions)
        {
            RoleCodes = roleCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            Permissions = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public Guid UserId => OperationsServiceTests.UserId;

        public Guid TenantId => OperationsServiceTests.TenantId;

        public string Email => "user@tkxel.com";

        public IReadOnlySet<string> RoleCodes { get; }

        public IReadOnlySet<string> Permissions { get; }
    }

    private sealed class FakeOperationsRepository : IOperationsRepository
    {
        public bool CreateJobRequestCalled { get; private set; }

        public bool? LastPmoQueueTenantAdminFallback { get; private set; }

        public Guid? LastClaimAssignmentId { get; private set; }

        public Guid? LastForwardedJobRequestId { get; private set; }

        public Guid? LastBenchMatchesJobRequestId { get; private set; }

        public Guid? LastReferralJobRequestId { get; private set; }

        public CreateInternalResourceReferralInput? LastReferralInput { get; private set; }

        public bool ClaimResult { get; init; }

        public ForwardToRecruiterResult? ForwardResult { get; init; }

        public OperationsJobRequest? JobRequest { get; init; }

        public IReadOnlyList<OperationsBenchMatch> BenchMatches { get; init; } = [];

        public CreateInternalResourceReferralResult? ReferralResult { get; init; }

        public Task<OperationsSnapshot> GetSnapshotAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new OperationsSnapshot([], [], [], []));
        }

        public Task<IReadOnlyList<OperationsActivityEvent>> GetActivityAsync(Guid tenantId, Guid entityId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<OperationsActivityEvent>>([]);
        }

        public Task<OperationsJobRequest?> GetJobRequestAsync(Guid tenantId, Guid userId, Guid jobRequestId, bool canViewAll, CancellationToken cancellationToken)
        {
            return Task.FromResult(JobRequest?.Id == jobRequestId ? JobRequest : null);
        }

        public Task<IReadOnlyList<OperationsPmoQueueItem>> GetPmoQueueAsync(
            Guid tenantId,
            Guid userId,
            bool includeTenantAdminFallback,
            CancellationToken cancellationToken)
        {
            LastPmoQueueTenantAdminFallback = includeTenantAdminFallback;
            return Task.FromResult<IReadOnlyList<OperationsPmoQueueItem>>([]);
        }

        public Task<IReadOnlyList<OperationsRecruitmentQueueItem>> GetRecruitmentQueueAsync(
            Guid tenantId,
            Guid userId,
            bool includeTenantAdminFallback,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<OperationsRecruitmentQueueItem>>([]);
        }

        public Task<IReadOnlyList<OperationsBenchMatch>> GetBenchMatchesAsync(
            Guid tenantId,
            Guid userId,
            Guid jobRequestId,
            bool canViewAll,
            CancellationToken cancellationToken)
        {
            LastBenchMatchesJobRequestId = jobRequestId;
            return Task.FromResult(BenchMatches);
        }

        public Task<IReadOnlyList<OperationsNotification>> ListNotificationsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<OperationsNotification>>([]);
        }

        public Task<CreateOperationsJobRequestResult> CreateJobRequestAsync(
            Guid tenantId,
            Guid actorUserId,
            CreateOperationsJobRequestInput input,
            CancellationToken cancellationToken)
        {
            CreateJobRequestCalled = true;
            throw new NotSupportedException();
        }

        public Task<bool> ClaimAssignmentAsync(Guid tenantId, Guid actorUserId, Guid assignmentId, CancellationToken cancellationToken)
        {
            LastClaimAssignmentId = assignmentId;
            return Task.FromResult(ClaimResult);
        }

        public Task<ForwardToRecruiterResult?> ForwardToRecruiterAsync(
            Guid tenantId,
            Guid actorUserId,
            Guid jobRequestId,
            bool includeTenantAdminFallback,
            CancellationToken cancellationToken)
        {
            LastForwardedJobRequestId = jobRequestId;
            return Task.FromResult(ForwardResult);
        }

        public Task<CreateInternalResourceReferralResult?> CreateInternalResourceReferralAsync(
            Guid tenantId,
            Guid actorUserId,
            Guid jobRequestId,
            CreateInternalResourceReferralInput input,
            bool includeTenantAdminFallback,
            CancellationToken cancellationToken)
        {
            LastReferralJobRequestId = jobRequestId;
            LastReferralInput = input;
            return Task.FromResult(ReferralResult);
        }

        public Task<bool> MarkNotificationReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task MarkAllNotificationsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
