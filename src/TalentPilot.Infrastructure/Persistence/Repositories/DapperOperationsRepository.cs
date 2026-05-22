using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using TalentPilot.Application.Operations;

namespace TalentPilot.Infrastructure.Persistence.Repositories;

public sealed class DapperOperationsRepository : IOperationsRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperOperationsRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<OperationsSnapshot> GetSnapshotAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        var people = await ListPeopleAsync(connection, tenantId, cancellationToken);
        var jobRequests = await ListJobRequestsAsync(connection, tenantId, cancellationToken);
        var assignments = await ListAssignmentsAsync(connection, tenantId, cancellationToken);
        var notifications = await ListNotificationsAsync(connection, tenantId, userId, cancellationToken);

        return new OperationsSnapshot(people, jobRequests, assignments, notifications);
    }

    public async Task<IReadOnlyList<OperationsActivityEvent>> GetActivityAsync(
        Guid tenantId,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                AuditLogId AS Id,
                EntityId,
                ActorDisplayName AS ActorName,
                EventType AS Title,
                EventSummary AS Detail,
                OccurredAtUtc AS CreatedAt
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND EntityId = @EntityId
            ORDER BY OccurredAtUtc DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<OperationsActivityEvent>(
            new CommandDefinition(sql, new { TenantId = tenantId, EntityId = entityId }, cancellationToken: cancellationToken));

        return rows.ToArray();
    }

    public async Task<OperationsJobRequest?> GetJobRequestAsync(
        Guid tenantId,
        Guid userId,
        Guid jobRequestId,
        bool canViewAll,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        return await GetJobRequestByIdAsync(connection, tenantId, userId, jobRequestId, canViewAll, cancellationToken);
    }

    public async Task<IReadOnlyList<OperationsPmoQueueItem>> GetPmoQueueAsync(
        Guid tenantId,
        Guid userId,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT wa.WorkflowAssignmentId
            FROM dbo.WorkflowAssignments AS wa
            INNER JOIN dbo.WorkflowStages AS ws ON ws.WorkflowStageId = wa.WorkflowStageId
            WHERE wa.TenantId = @TenantId
              AND wa.EntityType = N'JobRequest'
              AND wa.AssignmentStatus IN (N'Pending', N'Claimed')
              AND ws.StageKey = N'PMO_REVIEW'
              AND
              (
                  wa.AssignedToUserId = @UserId
                  OR wa.ClaimedByUserId = @UserId
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.GroupMembers AS gm
                      WHERE gm.TenantId = wa.TenantId
                        AND gm.GroupId = wa.AssignedToGroupId
                        AND gm.UserId = @UserId
                  )
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.UserRoles AS ur
                      WHERE ur.TenantId = wa.TenantId
                        AND ur.UserId = @UserId
                        AND ur.RoleId = wa.AssignedToRoleId
                  )
                  OR @IncludeTenantAdminFallback = 1
              )
            ORDER BY wa.AssignedAtUtc DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var assignmentIds = (await connection.QueryAsync<Guid>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId, IncludeTenantAdminFallback = includeTenantAdminFallback },
            cancellationToken: cancellationToken))).ToArray();

        var items = new List<OperationsPmoQueueItem>(assignmentIds.Length);
        foreach (var assignmentId in assignmentIds)
        {
            var assignment = await GetAssignmentByIdAsync(connection, tenantId, assignmentId, cancellationToken);
            if (assignment is null)
            {
                continue;
            }

            var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, userId, assignment.EntityId, includeTenantAdminFallback, cancellationToken);
            if (jobRequest is not null)
            {
                items.Add(new OperationsPmoQueueItem(assignment, jobRequest));
            }
        }

        return items;
    }

    public async Task<IReadOnlyList<OperationsRecruitmentQueueItem>> GetRecruitmentQueueAsync(
        Guid tenantId,
        Guid userId,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT wa.WorkflowAssignmentId
            FROM dbo.WorkflowAssignments AS wa
            INNER JOIN dbo.WorkflowStages AS ws ON ws.WorkflowStageId = wa.WorkflowStageId
            WHERE wa.TenantId = @TenantId
              AND wa.EntityType = N'JobRequest'
              AND wa.AssignmentStatus IN (N'Pending', N'Claimed')
              AND ws.StageKey = N'SOURCING'
              AND
              (
                  wa.AssignedToUserId = @UserId
                  OR wa.ClaimedByUserId = @UserId
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.GroupMembers AS gm
                      WHERE gm.TenantId = wa.TenantId
                        AND gm.GroupId = wa.AssignedToGroupId
                        AND gm.UserId = @UserId
                  )
                  OR @IncludeTenantAdminFallback = 1
              )
            ORDER BY wa.AssignedAtUtc DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var assignmentIds = (await connection.QueryAsync<Guid>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId, IncludeTenantAdminFallback = includeTenantAdminFallback },
            cancellationToken: cancellationToken))).ToArray();

        var items = new List<OperationsRecruitmentQueueItem>(assignmentIds.Length);
        foreach (var assignmentId in assignmentIds)
        {
            var assignment = await GetAssignmentByIdAsync(connection, tenantId, assignmentId, cancellationToken);
            if (assignment is null)
            {
                continue;
            }

            var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, userId, assignment.EntityId, includeTenantAdminFallback, cancellationToken);
            if (jobRequest is null)
            {
                continue;
            }

            var candidateCount = await CountApplicationsAsync(connection, tenantId, assignment.EntityId, cancellationToken);
            items.Add(new OperationsRecruitmentQueueItem(assignment, jobRequest, candidateCount));
        }

        return items;
    }

    public async Task<IReadOnlyList<OperationsBenchMatch>> GetBenchMatchesAsync(
        Guid tenantId,
        Guid userId,
        Guid jobRequestId,
        bool canViewAll,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        return await ListBenchMatchesAsync(connection, null, tenantId, jobRequestId, cancellationToken);
    }

    public async Task<IReadOnlyList<OperationsNotification>> ListNotificationsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        return await ListNotificationsAsync(connection, tenantId, userId, cancellationToken);
    }

    public async Task<CreateOperationsJobRequestResult> CreateJobRequestAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var jobRequestId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var requestNumber = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) + 1 FROM dbo.JobRequests WHERE TenantId = @TenantId;",
                new { TenantId = tenantId },
                transaction,
                cancellationToken: cancellationToken));
        var requestCode = $"TP-REQ-{requestNumber:000}";

        var departmentId = await FindDepartmentIdAsync(connection, transaction, tenantId, input.Department, cancellationToken);
        var locationId = await FindLocationIdAsync(connection, transaction, tenantId, input.Location, cancellationToken);
        var workflowIds = await ReadWorkflowIdsAsync(connection, transaction, tenantId, cancellationToken);
        var pmoGroupId = await FindPmoGroupIdAsync(connection, transaction, tenantId, cancellationToken);

        const string insertJobRequestSql = """
            INSERT INTO dbo.JobRequests
            (
                JobRequestId,
                TenantId,
                RequestCode,
                Title,
                Description,
                ClientName,
                DepartmentId,
                LocationId,
                EmploymentType,
                Priority,
                RequiredPositions,
                FulfilledPositions,
                Status,
                PublishStatus,
                HiringManagerUserId,
                CreatedByUserId,
                CurrentStageKey,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @JobRequestId,
                @TenantId,
                @RequestCode,
                @Title,
                @Description,
                @ClientName,
                @DepartmentId,
                @LocationId,
                N'FullTime',
                @Priority,
                @RequiredPositions,
                0,
                N'PMOReview',
                N'NotPublished',
                @HiringManagerUserId,
                @CreatedByUserId,
                N'PMO_REVIEW',
                @Now,
                @Now
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertJobRequestSql,
            new
            {
                JobRequestId = jobRequestId,
                TenantId = tenantId,
                RequestCode = requestCode,
                input.Title,
                input.Description,
                ClientName = input.Client,
                DepartmentId = departmentId,
                LocationId = locationId,
                Priority = NormalizePriority(input.Priority),
                input.RequiredPositions,
                HiringManagerUserId = input.HiringManagerId,
                CreatedByUserId = actorUserId,
                Now = now
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertJobRequestSkillsAsync(connection, transaction, tenantId, jobRequestId, input.Skills, cancellationToken);

        const string insertAssignmentSql = """
            INSERT INTO dbo.WorkflowAssignments
            (
                WorkflowAssignmentId,
                TenantId,
                WorkflowDefinitionId,
                WorkflowStageId,
                WorkflowTransitionId,
                EntityType,
                EntityId,
                AssignedToGroupId,
                AssignmentStatus,
                AssignedAtUtc
            )
            VALUES
            (
                @WorkflowAssignmentId,
                @TenantId,
                @WorkflowDefinitionId,
                @WorkflowStageId,
                @WorkflowTransitionId,
                N'JobRequest',
                @EntityId,
                @AssignedToGroupId,
                N'Pending',
                @Now
            );

            UPDATE dbo.JobRequests
            SET CurrentAssignmentId = @WorkflowAssignmentId,
                UpdatedAtUtc = @Now
            WHERE TenantId = @TenantId
              AND JobRequestId = @EntityId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertAssignmentSql,
            new
            {
                WorkflowAssignmentId = assignmentId,
                TenantId = tenantId,
                workflowIds.WorkflowDefinitionId,
                workflowIds.WorkflowStageId,
                workflowIds.WorkflowTransitionId,
                EntityId = jobRequestId,
                AssignedToGroupId = pmoGroupId,
                Now = now
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "job_request.created",
            "JobRequest",
            jobRequestId,
            requestCode,
            $"{requestCode} was created and routed to PMO review.",
            "Talent Pilot App",
            cancellationToken);

        await InsertPmoAssignmentNotificationsAsync(
            connection,
            transaction,
            tenantId,
            jobRequestId,
            assignmentId,
            requestCode,
            input.Title,
            actorUserId,
            pmoGroupId,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, actorUserId, jobRequestId, canViewAll: true, cancellationToken)
            ?? throw new InvalidOperationException("Created job request could not be reloaded.");
        var assignment = await GetAssignmentByIdAsync(connection, tenantId, assignmentId, cancellationToken)
            ?? throw new InvalidOperationException("Created workflow assignment could not be reloaded.");

        return new CreateOperationsJobRequestResult(jobRequest, assignment);
    }

    public async Task<ForwardToRecruiterResult?> ForwardToRecruiterAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string requestSql = """
            SELECT TOP (1)
                jr.JobRequestId,
                jr.RequestCode,
                jr.Title,
                jr.CurrentAssignmentId,
                wa.AssignedToUserId,
                wa.ClaimedByUserId,
                wa.AssignedToGroupId
            FROM dbo.JobRequests AS jr
            LEFT JOIN dbo.WorkflowAssignments AS wa ON wa.WorkflowAssignmentId = jr.CurrentAssignmentId
            WHERE jr.TenantId = @TenantId
              AND jr.JobRequestId = @JobRequestId
              AND jr.Status NOT IN (N'Closed', N'Cancelled');
            """;

        var request = await connection.QuerySingleOrDefaultAsync<ForwardableRequestRow>(new CommandDefinition(
            requestSql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));

        if (request is null)
        {
            return null;
        }

        var allowed = includeTenantAdminFallback ||
            request.AssignedToUserId == actorUserId ||
            request.ClaimedByUserId == actorUserId ||
            await IsGroupMemberAsync(connection, transaction, tenantId, actorUserId, request.AssignedToGroupId, cancellationToken);

        if (!allowed)
        {
            return null;
        }

        var ids = await ReadWorkflowIdsAsync(connection, transaction, tenantId, "SOURCING", "FORWARD_TO_RECRUITER", cancellationToken);
        var recruitingGroupId = await FindRecruitingGroupIdAsync(connection, transaction, tenantId, cancellationToken);
        var assignmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        const string transitionSql = """
            UPDATE dbo.WorkflowAssignments
            SET AssignmentStatus = N'Completed',
                CompletedAtUtc = @Now
            WHERE TenantId = @TenantId
              AND WorkflowAssignmentId = @CurrentAssignmentId;

            INSERT INTO dbo.WorkflowAssignments
            (
                WorkflowAssignmentId,
                TenantId,
                WorkflowDefinitionId,
                WorkflowStageId,
                WorkflowTransitionId,
                EntityType,
                EntityId,
                AssignedToGroupId,
                AssignmentStatus,
                AssignedAtUtc
            )
            VALUES
            (
                @WorkflowAssignmentId,
                @TenantId,
                @WorkflowDefinitionId,
                @WorkflowStageId,
                @WorkflowTransitionId,
                N'JobRequest',
                @EntityId,
                @AssignedToGroupId,
                N'Pending',
                @Now
            );

            UPDATE dbo.JobRequests
            SET Status = N'Sourcing',
                CurrentStageKey = N'SOURCING',
                CurrentAssignmentId = @WorkflowAssignmentId,
                UpdatedAtUtc = @Now
            WHERE TenantId = @TenantId
              AND JobRequestId = @EntityId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            transitionSql,
            new
            {
                TenantId = tenantId,
                CurrentAssignmentId = request.CurrentAssignmentId,
                WorkflowAssignmentId = assignmentId,
                ids.WorkflowDefinitionId,
                ids.WorkflowStageId,
                ids.WorkflowTransitionId,
                EntityId = jobRequestId,
                AssignedToGroupId = recruitingGroupId,
                Now = now
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "job_request.forwarded_to_recruiter",
            "JobRequest",
            jobRequestId,
            request.RequestCode,
            $"{request.RequestCode} was forwarded to the recruitment queue.",
            "Talent Pilot App",
            cancellationToken);

        await InsertRecruitmentAssignmentNotificationsAsync(
            connection,
            transaction,
            tenantId,
            jobRequestId,
            assignmentId,
            request.RequestCode,
            request.Title,
            actorUserId,
            recruitingGroupId,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, actorUserId, jobRequestId, includeTenantAdminFallback, cancellationToken);
        var assignment = await GetAssignmentByIdAsync(connection, tenantId, assignmentId, cancellationToken);
        var candidateCount = await CountApplicationsAsync(connection, tenantId, jobRequestId, cancellationToken);

        return jobRequest is null || assignment is null
            ? null
            : new ForwardToRecruiterResult(jobRequest, assignment, candidateCount);
    }

    public async Task<CreateInternalResourceReferralResult?> CreateInternalResourceReferralAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CreateInternalResourceReferralInput input,
        bool includeTenantAdminFallback,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string requestSql = """
            SELECT TOP (1)
                jr.JobRequestId,
                jr.RequestCode,
                jr.Title,
                jr.CreatedByUserId AS PresalesUserId,
                jr.CurrentAssignmentId,
                wa.AssignedToUserId,
                wa.ClaimedByUserId,
                wa.AssignedToGroupId
            FROM dbo.JobRequests AS jr
            LEFT JOIN dbo.WorkflowAssignments AS wa ON wa.WorkflowAssignmentId = jr.CurrentAssignmentId
            WHERE jr.TenantId = @TenantId
              AND jr.JobRequestId = @JobRequestId
              AND jr.Status NOT IN (N'Closed', N'Cancelled')
              AND jr.CurrentStageKey IN (N'PMO_REVIEW', N'BENCH_MATCHING');
            """;

        var request = await connection.QuerySingleOrDefaultAsync<ReferralRequestRow>(new CommandDefinition(
            requestSql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));

        if (request is null)
        {
            return null;
        }

        var allowed = includeTenantAdminFallback ||
            request.AssignedToUserId == actorUserId ||
            request.ClaimedByUserId == actorUserId ||
            await IsGroupMemberAsync(connection, transaction, tenantId, actorUserId, request.AssignedToGroupId, cancellationToken);

        if (!allowed)
        {
            return null;
        }

        var selectedEmployeeIds = input.EmployeeIds.Distinct().ToArray();
        var matches = await ListBenchMatchesAsync(connection, transaction, tenantId, jobRequestId, cancellationToken);
        var selectedMatches = matches
            .Where(match => selectedEmployeeIds.Contains(match.EmployeeId))
            .OrderByDescending(match => match.MatchScore)
            .ThenBy(match => match.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (selectedMatches.Length != selectedEmployeeIds.Length)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var referrals = new List<InternalEmployeeReferral>(selectedMatches.Length);

        foreach (var match in selectedMatches)
        {
            var recommendationSummary = BuildReferralSummary(match, input.Note);
            var referral = await UpsertInternalReferralAsync(
                connection,
                transaction,
                tenantId,
                jobRequestId,
                match,
                actorUserId,
                request.PresalesUserId,
                recommendationSummary,
                now,
                cancellationToken);

            referrals.Add(referral);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "job_request.employee_referred",
            "JobRequest",
            jobRequestId,
            request.RequestCode,
            $"{request.RequestCode} referred {referrals.Count} internal employee(s) to Presales: {string.Join(", ", referrals.Select(referral => referral.EmployeeName))}.",
            "Workflow",
            cancellationToken);

        await InsertEmployeeReferralNotificationsAsync(
            connection,
            transaction,
            tenantId,
            jobRequestId,
            referrals.Select(referral => referral.Id).ToArray(),
            request.RequestCode,
            request.Title,
            referrals.Select(referral => referral.EmployeeName).ToArray(),
            actorUserId,
            request.PresalesUserId,
            input.Note,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, actorUserId, jobRequestId, includeTenantAdminFallback, cancellationToken);

        return jobRequest is null
            ? null
            : new CreateInternalResourceReferralResult(jobRequest, referrals);
    }

    public async Task<bool> ClaimAssignmentAsync(Guid tenantId, Guid actorUserId, Guid assignmentId, CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @ChangedAssignments TABLE (EntityId UNIQUEIDENTIFIER NOT NULL, RequestCode NVARCHAR(40) NULL, Title NVARCHAR(200) NULL);

            UPDATE wa
            SET AssignmentStatus = N'Claimed',
                ClaimedByUserId = @ActorUserId,
                AssignedToUserId = @ActorUserId,
                ClaimedAtUtc = SYSUTCDATETIME()
            OUTPUT inserted.EntityId, jr.RequestCode, jr.Title INTO @ChangedAssignments
            FROM dbo.WorkflowAssignments AS wa
            LEFT JOIN dbo.JobRequests AS jr
                ON jr.TenantId = wa.TenantId
                AND jr.JobRequestId = wa.EntityId
            WHERE wa.TenantId = @TenantId
              AND wa.WorkflowAssignmentId = @AssignmentId
              AND wa.AssignmentStatus = N'Pending'
              AND
              (
                  wa.AssignedToUserId = @ActorUserId
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.GroupMembers AS gm
                      WHERE gm.TenantId = wa.TenantId
                        AND gm.GroupId = wa.AssignedToGroupId
                        AND gm.UserId = @ActorUserId
                  )
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.UserRoles AS ur
                      WHERE ur.TenantId = wa.TenantId
                        AND ur.UserId = @ActorUserId
                        AND ur.RoleId = wa.AssignedToRoleId
                  )
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.UserRoles AS ur
                      INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId
                      WHERE ur.TenantId = wa.TenantId
                        AND ur.UserId = @ActorUserId
                        AND r.Code = N'TenantAdmin'
                  )
              );

            UPDATE jr
            SET CurrentStageKey = N'BENCH_MATCHING',
                UpdatedAtUtc = SYSUTCDATETIME()
            FROM dbo.JobRequests AS jr
            INNER JOIN dbo.WorkflowAssignments AS wa
                ON wa.TenantId = jr.TenantId
                AND wa.EntityType = N'JobRequest'
                AND wa.EntityId = jr.JobRequestId
            INNER JOIN @ChangedAssignments AS changed
                ON changed.EntityId = jr.JobRequestId
            WHERE wa.TenantId = @TenantId
              AND wa.WorkflowAssignmentId = @AssignmentId;

            INSERT INTO dbo.NotificationOutbox
            (
                NotificationOutboxId,
                TenantId,
                NotificationEventId,
                NotificationTemplateId,
                RecipientUserId,
                RecipientEmail,
                Channel,
                PayloadJson,
                Status,
                AvailableAtUtc,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            SELECT
                NEWID(),
                @TenantId,
                ne.NotificationEventId,
                nt.NotificationTemplateId,
                @ActorUserId,
                u.Email,
                N'SignalR',
                CONCAT(N'{"eventCode":"WORKFLOW_ASSIGNMENT_CLAIMED","assignmentId":"', CONVERT(NVARCHAR(36), @AssignmentId), N'","jobRequestId":"', CONVERT(NVARCHAR(36), changed.EntityId), N'","requestCode":"', STRING_ESCAPE(COALESCE(changed.RequestCode, N''), 'json'), N'","title":"', STRING_ESCAPE(COALESCE(changed.Title, N''), 'json'), N'"}'),
                N'Pending',
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            FROM @ChangedAssignments AS changed
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = @TenantId
                AND u.UserId = @ActorUserId
            CROSS APPLY
            (
                SELECT TOP (1) NotificationEventId
                FROM dbo.NotificationEvents
                WHERE TenantId = @TenantId
                  AND Status = N'Active'
                  AND EventCode IN (N'WORKFLOW_ASSIGNMENT_CLAIMED', N'PRESALES_REQUEST_SUBMITTED')
                ORDER BY CASE WHEN EventCode = N'WORKFLOW_ASSIGNMENT_CLAIMED' THEN 0 ELSE 1 END
            ) AS ne
            OUTER APPLY
            (
                SELECT TOP (1) NotificationTemplateId
                FROM dbo.NotificationTemplates
                WHERE TenantId = @TenantId
                  AND NotificationEventId = ne.NotificationEventId
                  AND Status = N'Active'
                ORDER BY CreatedAtUtc DESC
            ) AS nt;

            SELECT COUNT(1) FROM @ChangedAssignments;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActorUserId = actorUserId, AssignmentId = assignmentId },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> MarkNotificationReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.NotificationRecipients
            SET ReadAtUtc = COALESCE(ReadAtUtc, SYSUTCDATETIME())
            WHERE TenantId = @TenantId
              AND RecipientUserId = @UserId
              AND NotificationRecipientId = @NotificationId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId, NotificationId = notificationId },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task MarkAllNotificationsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.NotificationRecipients
            SET ReadAtUtc = COALESCE(ReadAtUtc, SYSUTCDATETIME())
            WHERE TenantId = @TenantId
              AND RecipientUserId = @UserId
              AND ReadAtUtc IS NULL;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId },
            cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<OperationsBenchMatch>> ListBenchMatchesAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                eba.EmployeeId,
                eba.EmployeeCode,
                eba.DisplayName,
                eba.Email,
                eba.Designation,
                COALESCE(eba.DepartmentName, N'Unassigned') AS Department,
                COALESCE(eba.LocationName, N'Remote') AS Location,
                eba.AvailabilityStatus,
                eba.BenchStatus,
                COALESCE(eba.AllocationPercent, 0) AS CurrentAllocationPercent,
                STRING_AGG(s.Name, N',') AS SkillList,
                requiredSkills.SkillList AS RequiredSkillList
            FROM dbo.vw_EmployeeBenchAvailability AS eba
            OUTER APPLY
            (
                SELECT STRING_AGG(requiredSkill.Name, N',') AS SkillList
                FROM dbo.JobRequestSkills AS jrs
                INNER JOIN dbo.Skills AS requiredSkill ON requiredSkill.SkillId = jrs.SkillId
                WHERE jrs.TenantId = @TenantId
                  AND jrs.JobRequestId = @JobRequestId
            ) AS requiredSkills
            LEFT JOIN dbo.EmployeeSkills AS es
                ON es.TenantId = eba.TenantId
                AND es.EmployeeId = eba.EmployeeId
            LEFT JOIN dbo.Skills AS s
                ON s.TenantId = es.TenantId
                AND s.SkillId = es.SkillId
            WHERE eba.TenantId = @TenantId
              AND eba.AvailabilityStatus = N'Available'
              AND eba.BenchStatus = N'Benched'
              AND eba.IsCurrentlyBenched = 1
              AND EXISTS
              (
                  SELECT 1
                  FROM dbo.JobRequests AS jr
                  WHERE jr.TenantId = @TenantId
                    AND jr.JobRequestId = @JobRequestId
              )
            GROUP BY
                eba.EmployeeId,
                eba.EmployeeCode,
                eba.DisplayName,
                eba.Email,
                eba.Designation,
                eba.DepartmentName,
                eba.LocationName,
                eba.AvailabilityStatus,
                eba.BenchStatus,
                eba.AllocationPercent,
                requiredSkills.SkillList;
            """;

        var rows = await connection.QueryAsync<BenchMatchRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));

        return rows
            .Select(ToBenchMatch)
            .OrderByDescending(match => match.MatchScore)
            .ThenBy(match => match.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static OperationsBenchMatch ToBenchMatch(BenchMatchRow row)
    {
        var skills = SplitCommaList(row.SkillList);
        var requiredSkills = SplitCommaList(row.RequiredSkillList);
        var matchScore = OperationsBenchMatchScoring.CalculateScore(requiredSkills, skills);
        var explanation = OperationsBenchMatchScoring.BuildExplanation(requiredSkills, skills, row.CurrentAllocationPercent);

        return new OperationsBenchMatch(
            row.EmployeeId,
            row.EmployeeCode,
            row.DisplayName,
            row.Email,
            row.Designation,
            row.Department,
            row.Location,
            skills,
            row.AvailabilityStatus,
            row.BenchStatus,
            row.CurrentAllocationPercent,
            matchScore,
            explanation);
    }

    private static async Task<InternalEmployeeReferral> UpsertInternalReferralAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        OperationsBenchMatch match,
        Guid actorUserId,
        Guid presalesUserId,
        string recommendationSummary,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @ReferralId UNIQUEIDENTIFIER =
            (
                SELECT TOP (1) JobRequestEmployeeReferralId
                FROM dbo.JobRequestEmployeeReferrals
                WHERE TenantId = @TenantId
                  AND JobRequestId = @JobRequestId
                  AND EmployeeId = @EmployeeId
                  AND Status = N'Referred'
                ORDER BY CreatedAtUtc DESC
            );

            IF @ReferralId IS NULL
            BEGIN
                SET @ReferralId = NEWID();

                INSERT INTO dbo.JobRequestEmployeeReferrals
                (
                    JobRequestEmployeeReferralId,
                    TenantId,
                    JobRequestId,
                    EmployeeId,
                    ReferredByUserId,
                    PresalesUserId,
                    Status,
                    FitScore,
                    RecommendationSummary,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    @ReferralId,
                    @TenantId,
                    @JobRequestId,
                    @EmployeeId,
                    @ReferredByUserId,
                    @PresalesUserId,
                    N'Referred',
                    @FitScore,
                    @RecommendationSummary,
                    @Now,
                    @Now
                );
            END;

            SELECT TOP (1)
                referral.JobRequestEmployeeReferralId AS Id,
                referral.JobRequestId,
                referral.EmployeeId,
                employee.DisplayName AS EmployeeName,
                employee.Email AS EmployeeEmail,
                referral.Status,
                referral.FitScore,
                COALESCE(referral.RecommendationSummary, N'') AS RecommendationSummary,
                referral.ReferredByUserId,
                referral.PresalesUserId,
                referral.CreatedAtUtc AS CreatedAt
            FROM dbo.JobRequestEmployeeReferrals AS referral
            INNER JOIN dbo.Employees AS employee
                ON employee.TenantId = referral.TenantId
                AND employee.EmployeeId = referral.EmployeeId
            WHERE referral.TenantId = @TenantId
              AND referral.JobRequestEmployeeReferralId = @ReferralId;
            """;

        var row = await connection.QuerySingleAsync<InternalEmployeeReferralRow>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                JobRequestId = jobRequestId,
                match.EmployeeId,
                ReferredByUserId = actorUserId,
                PresalesUserId = presalesUserId,
                FitScore = match.MatchScore,
                RecommendationSummary = recommendationSummary,
                Now = now
            },
            transaction,
            cancellationToken: cancellationToken));

        return ToInternalEmployeeReferral(row);
    }

    private static async Task<IReadOnlyList<OperationsPerson>> ListPeopleAsync(
        SqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.UserId,
                u.DisplayName,
                u.Email,
                r.Code AS RoleCode,
                r.Name AS RoleName
            FROM dbo.AppUsers AS u
            LEFT JOIN dbo.UserRoles AS ur ON ur.TenantId = u.TenantId AND ur.UserId = u.UserId
            LEFT JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId AND r.Status = N'Active'
            WHERE u.TenantId = @TenantId
              AND u.AccountStatus = N'Active'
              AND u.DeletedAtUtc IS NULL
            ORDER BY u.DisplayName, r.Priority;
            """;

        var rows = await connection.QueryAsync<PersonRow>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return rows
            .GroupBy(row => new { row.UserId, row.DisplayName, row.Email })
            .Select(group => new OperationsPerson(
                group.Key.UserId,
                group.Key.DisplayName,
                group.Key.Email,
                group.Select(row => row.RoleCode).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().Distinct().ToArray(),
                group.Select(row => row.RoleName).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().Distinct().ToArray()))
            .ToArray();
    }

    private static async Task<IReadOnlyList<OperationsJobRequest>> ListJobRequestsAsync(
        SqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                jr.JobRequestId AS Id,
                jr.RequestCode AS Code,
                jr.Title,
                COALESCE(jr.ClientName, N'Internal') AS Client,
                jr.Description,
                COALESCE(d.Name, N'Unassigned') AS Department,
                COALESCE(l.Name, N'Remote') AS Location,
                jr.ExperienceMinYears,
                jr.ExperienceMaxYears,
                jr.RequiredPositions,
                jr.FulfilledPositions,
                jr.Priority,
                COALESCE(jr.HiringManagerUserId, CAST('00000000-0000-0000-0000-000000000000' AS UNIQUEIDENTIFIER)) AS HiringManagerId,
                jr.CreatedByUserId AS CreatedById,
                jr.Status,
                jr.CurrentStageKey,
                wa.AssignedToUserId,
                wa.ClaimedByUserId,
                g.Name AS AssignedToGroupName,
                jr.PublishStatus,
                jr.CreatedAtUtc AS CreatedAt,
                STRING_AGG(s.Name, N',') AS SkillList
            FROM dbo.JobRequests AS jr
            LEFT JOIN dbo.Departments AS d ON d.DepartmentId = jr.DepartmentId
            LEFT JOIN dbo.Locations AS l ON l.LocationId = jr.LocationId
            LEFT JOIN dbo.WorkflowAssignments AS wa ON wa.WorkflowAssignmentId = jr.CurrentAssignmentId
            LEFT JOIN dbo.Groups AS g ON g.GroupId = wa.AssignedToGroupId
            LEFT JOIN dbo.JobRequestSkills AS jrs ON jrs.TenantId = jr.TenantId AND jrs.JobRequestId = jr.JobRequestId
            LEFT JOIN dbo.Skills AS s ON s.SkillId = jrs.SkillId
            WHERE jr.TenantId = @TenantId
            GROUP BY
                jr.JobRequestId,
                jr.RequestCode,
                jr.Title,
                jr.ClientName,
                jr.Description,
                d.Name,
                l.Name,
                jr.ExperienceMinYears,
                jr.ExperienceMaxYears,
                jr.RequiredPositions,
                jr.FulfilledPositions,
                jr.Priority,
                jr.HiringManagerUserId,
                jr.CreatedByUserId,
                jr.Status,
                jr.CurrentStageKey,
                wa.AssignedToUserId,
                wa.ClaimedByUserId,
                g.Name,
                jr.PublishStatus,
                jr.CreatedAtUtc
            ORDER BY jr.CreatedAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<JobRequestRow>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return rows.Select(ToJobRequest).ToArray();
    }

    private static async Task<IReadOnlyList<OperationsWorkflowAssignment>> ListAssignmentsAsync(
        SqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                wa.WorkflowAssignmentId AS Id,
                wa.EntityType,
                wa.EntityId,
                COALESCE(ws.Name, wa.EntityType) AS Stage,
                wa.AssignedToGroupId,
                g.Name AS AssignedToGroupName,
                wa.AssignedToUserId,
                wa.ClaimedByUserId,
                CASE wa.AssignmentStatus
                    WHEN N'Cancelled' THEN N'Completed'
                    ELSE wa.AssignmentStatus
                END AS Status,
                wa.AssignedAtUtc AS AssignedAt
            FROM dbo.WorkflowAssignments AS wa
            INNER JOIN dbo.WorkflowStages AS ws ON ws.WorkflowStageId = wa.WorkflowStageId
            LEFT JOIN dbo.Groups AS g ON g.GroupId = wa.AssignedToGroupId
            WHERE wa.TenantId = @TenantId
              AND wa.AssignmentStatus IN (N'Pending', N'Claimed', N'Completed')
            ORDER BY wa.AssignedAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<OperationsWorkflowAssignment>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return rows.ToArray();
    }

    private static async Task<IReadOnlyList<OperationsNotification>> ListNotificationsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (25)
                nr.NotificationRecipientId AS Id,
                nr.RecipientUserId,
                ne.Name AS Title,
                CONCAT(ne.Name, N' is ready in Talent Pilot.') AS Message,
                N'WorkflowAssignment' AS EntityType,
                nr.NotificationEventId AS EntityId,
                nr.ReadAtUtc AS ReadAt,
                nr.CreatedAtUtc AS CreatedAt
            FROM dbo.NotificationRecipients AS nr
            INNER JOIN dbo.NotificationEvents AS ne ON ne.NotificationEventId = nr.NotificationEventId
            WHERE nr.TenantId = @TenantId
              AND nr.RecipientUserId = @UserId
            ORDER BY nr.CreatedAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<OperationsNotification>(
            new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));

        return rows.ToArray();
    }

    private static async Task<OperationsJobRequest?> GetJobRequestByIdAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid userId,
        Guid jobRequestId,
        bool canViewAll,
        CancellationToken cancellationToken)
    {
        var items = await ListJobRequestsAsync(connection, tenantId, cancellationToken);
        return items.FirstOrDefault(item => item.Id == jobRequestId);
    }

    private static async Task<OperationsWorkflowAssignment?> GetAssignmentByIdAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var items = await ListAssignmentsAsync(connection, tenantId, cancellationToken);
        return items.FirstOrDefault(item => item.Id == assignmentId);
    }

    private static OperationsJobRequest ToJobRequest(JobRequestRow row)
    {
        var skills = string.IsNullOrWhiteSpace(row.SkillList)
            ? Array.Empty<string>()
            : row.SkillList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new OperationsJobRequest(
            row.Id,
            row.Code,
            row.Title,
            row.Client,
            row.Description,
            row.Department,
            skills,
            FormatExperience(row.ExperienceMinYears, row.ExperienceMaxYears),
            row.Location,
            row.RequiredPositions,
            row.FulfilledPositions,
            NormalizePriority(row.Priority),
            row.HiringManagerId,
            row.CreatedById,
            ToStage(row.Status, row.CurrentStageKey),
            row.ClaimedByUserId ?? row.AssignedToUserId,
            row.AssignedToGroupName,
            ToPublishStatus(row.PublishStatus),
            row.CreatedAt);
    }

    private static InternalEmployeeReferral ToInternalEmployeeReferral(InternalEmployeeReferralRow row)
    {
        return new InternalEmployeeReferral(
            row.Id,
            row.JobRequestId,
            row.EmployeeId,
            row.EmployeeName,
            row.EmployeeEmail,
            row.Status,
            (int)Math.Round(row.FitScore ?? 0m, MidpointRounding.AwayFromZero),
            row.RecommendationSummary,
            row.ReferredByUserId,
            row.PresalesUserId,
            row.CreatedAt);
    }

    private static string BuildReferralSummary(OperationsBenchMatch match, string? note)
    {
        var summary = string.IsNullOrWhiteSpace(note)
            ? match.MatchExplanation
            : $"{match.MatchExplanation} PMO note: {note.Trim()}";

        return summary.Length <= 1500 ? summary : summary[..1500];
    }

    private static IReadOnlyList<string> SplitCommaList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private static string FormatExperience(decimal? min, decimal? max)
    {
        if (min is null && max is null)
        {
            return "Not specified";
        }

        if (min is not null && max is not null)
        {
            return $"{min:0.#}-{max:0.#} years";
        }

        return min is not null ? $"{min:0.#}+ years" : $"Up to {max:0.#} years";
    }

    private static string ToStage(string status, string currentStageKey)
    {
        return currentStageKey switch
        {
            "DRAFT" => "Draft",
            "PMO_REVIEW" => "PMO Review",
            "BENCH_MATCHING" => "Bench Matching",
            "SOURCING" => "Recruiter Sourcing",
            "INTERVIEWING" => "Interviewing",
            "HIRING_MANAGER_REVIEW" => "Hiring Manager Review",
            "OFFER" => "Offer Outcome",
            "CLOSED" => "Closed",
            _ => status switch
            {
                "PMOReview" => "PMO Review",
                "Sourcing" => "Recruiter Sourcing",
                "Interviewing" => "Interviewing",
                "HiringManagerReview" => "Hiring Manager Review",
                "Offer" => "Offer Outcome",
                "Closed" => "Closed",
                _ => "PMO Review"
            }
        };
    }

    private static string NormalizePriority(string priority)
    {
        return priority switch
        {
            "Normal" => "Medium",
            "Critical" => "Critical",
            "High" => "High",
            "Low" => "Low",
            _ => "Medium"
        };
    }

    private static string ToPublishStatus(string publishStatus)
    {
        return publishStatus switch
        {
            "Published" => "Published",
            "Closed" => "Closed",
            _ => "NotPublished"
        };
    }

    private static async Task<Guid?> FindDepartmentIdAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string department,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) DepartmentId
            FROM dbo.Departments
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND Name = @Department
            ORDER BY Name;
            """;

        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, Department = department.Trim() },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid?> FindLocationIdAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string location,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) LocationId
            FROM dbo.Locations
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND (@Location LIKE N'%' + Name + N'%' OR Name = N'Remote')
            ORDER BY CASE WHEN @Location LIKE N'%' + Name + N'%' THEN 0 ELSE 1 END, Name;
            """;

        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, Location = location.Trim() },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<WorkflowIds> ReadWorkflowIdsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await ReadWorkflowIdsAsync(connection, transaction, tenantId, "PMO_REVIEW", "CREATE_BY_PRESALES", cancellationToken);
    }

    private static async Task<WorkflowIds> ReadWorkflowIdsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string stageKey,
        string actionKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                wd.WorkflowDefinitionId,
                ws.WorkflowStageId,
                wt.WorkflowTransitionId
            FROM dbo.WorkflowDefinitions AS wd
            INNER JOIN dbo.WorkflowStages AS ws
                ON ws.WorkflowDefinitionId = wd.WorkflowDefinitionId
                AND ws.StageKey = @StageKey
            INNER JOIN dbo.WorkflowTransitions AS wt
                ON wt.WorkflowDefinitionId = wd.WorkflowDefinitionId
                AND wt.ActionKey = @ActionKey
            WHERE wd.TenantId = @TenantId
              AND wd.Code = N'JOB_REQUEST_MVP';
            """;

        var ids = await connection.QuerySingleOrDefaultAsync<WorkflowIds>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, StageKey = stageKey, ActionKey = actionKey },
            transaction,
            cancellationToken: cancellationToken));

        return ids ?? throw new InvalidOperationException($"JOB_REQUEST_MVP workflow seed data is missing for {stageKey}/{actionKey}.");
    }

    private static async Task<Guid> FindPmoGroupIdAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) GroupId
            FROM dbo.Groups
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND (Name LIKE N'%PMO%' OR Purpose = N'WorkflowRouting')
            ORDER BY CASE WHEN Name LIKE N'%PMO%' THEN 0 ELSE 1 END, Name;
            """;

        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid> FindRecruitingGroupIdAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) GroupId
            FROM dbo.Groups
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND (Name LIKE N'%Recruit%' OR Name LIKE N'%HR%' OR Purpose LIKE N'%recruit%')
            ORDER BY CASE WHEN Name LIKE N'%Recruit%' THEN 0 ELSE 1 END, Name;
            """;

        var groupId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            transaction,
            cancellationToken: cancellationToken));

        return groupId ?? throw new InvalidOperationException("Recruitment group seed data is missing.");
    }

    private static async Task<bool> IsGroupMemberAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid userId,
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        if (groupId is null)
        {
            return false;
        }

        const string sql = """
            SELECT COUNT(1)
            FROM dbo.GroupMembers AS gm
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = gm.TenantId
                AND u.UserId = gm.UserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            WHERE gm.TenantId = @TenantId
              AND gm.GroupId = @GroupId
              AND gm.UserId = @UserId;
            """;

        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, GroupId = groupId.Value, UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        return count > 0;
    }

    private static async Task<int> CountApplicationsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.JobApplications
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken));
    }

    private static async Task InsertJobRequestSkillsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        IReadOnlyList<string> skills,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.JobRequestSkills (TenantId, JobRequestId, SkillId, IsRequired, Weight, CreatedAtUtc)
            SELECT @TenantId, @JobRequestId, SkillId, 1, 10, SYSUTCDATETIME()
            FROM dbo.Skills
            WHERE TenantId = @TenantId
              AND NormalizedName IN @SkillNames
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.JobRequestSkills AS existing
                  WHERE existing.TenantId = @TenantId
                    AND existing.JobRequestId = @JobRequestId
                    AND existing.SkillId = dbo.Skills.SkillId
              );
            """;

        var normalized = skills
            .Select(skill => skill.Trim().ToLowerInvariant())
            .Where(skill => skill.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId, SkillNames = normalized },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertAuditAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        string eventType,
        string entityType,
        Guid entityId,
        string recordLabel,
        string eventSummary,
        string area,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.AuditLogs
            (
                AuditLogId,
                TenantId,
                ActorUserId,
                ActorDisplayName,
                EventType,
                EntityType,
                EntityId,
                RecordLabel,
                EventSummary,
                Area,
                MetadataJson
            )
            SELECT
                NEWID(),
                @TenantId,
                @ActorUserId,
                u.DisplayName,
                @EventType,
                @EntityType,
                @EntityId,
                @RecordLabel,
                @EventSummary,
                @Area,
                N'{}'
            FROM dbo.AppUsers AS u
            WHERE u.TenantId = @TenantId
              AND u.UserId = @ActorUserId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                EventType = eventType,
                EntityType = entityType,
                EntityId = entityId,
                RecordLabel = recordLabel,
                EventSummary = eventSummary,
                Area = area
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertPmoAssignmentNotificationsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        Guid assignmentId,
        string requestCode,
        string title,
        Guid actorUserId,
        Guid pmoGroupId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @NotificationEventId UNIQUEIDENTIFIER =
            (
                SELECT TOP (1) NotificationEventId
                FROM dbo.NotificationEvents
                WHERE TenantId = @TenantId
                  AND EventCode = N'PRESALES_REQUEST_SUBMITTED'
                  AND Status = N'Active'
            );

            IF @NotificationEventId IS NULL
            BEGIN
                RETURN;
            END;

            DECLARE @NotificationTemplateId UNIQUEIDENTIFIER =
            (
                SELECT TOP (1) NotificationTemplateId
                FROM dbo.NotificationTemplates
                WHERE TenantId = @TenantId
                  AND NotificationEventId = @NotificationEventId
                  AND Status = N'Active'
                ORDER BY CreatedAtUtc DESC
            );

            INSERT INTO dbo.NotificationRecipients
            (
                NotificationRecipientId,
                TenantId,
                NotificationEventId,
                RecipientUserId,
                CreatedAtUtc
            )
            SELECT
                NEWID(),
                @TenantId,
                @NotificationEventId,
                gm.UserId,
                SYSUTCDATETIME()
            FROM dbo.GroupMembers AS gm
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = gm.TenantId
                AND u.UserId = gm.UserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            WHERE gm.TenantId = @TenantId
              AND gm.GroupId = @PmoGroupId
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.NotificationRecipients AS existing
                  WHERE existing.NotificationEventId = @NotificationEventId
                    AND existing.RecipientUserId = gm.UserId
              );

            INSERT INTO dbo.NotificationOutbox
            (
                NotificationOutboxId,
                TenantId,
                NotificationEventId,
                NotificationTemplateId,
                RecipientUserId,
                RecipientEmail,
                Channel,
                PayloadJson,
                Status,
                AvailableAtUtc,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            SELECT
                NEWID(),
                @TenantId,
                @NotificationEventId,
                @NotificationTemplateId,
                gm.UserId,
                u.Email,
                channel.Channel,
                @PayloadJson,
                N'Pending',
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            FROM dbo.GroupMembers AS gm
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = gm.TenantId
                AND u.UserId = gm.UserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            CROSS JOIN (VALUES (N'Email'), (N'SignalR')) AS channel(Channel)
            WHERE gm.TenantId = @TenantId
              AND gm.GroupId = @PmoGroupId;
            """;

        var payloadJson = JsonSerializer.Serialize(new
        {
            eventCode = "PRESALES_REQUEST_SUBMITTED",
            jobRequestId,
            assignmentId,
            requestCode,
            title,
            actorUserId
        });

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                PmoGroupId = pmoGroupId,
                PayloadJson = payloadJson
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertRecruitmentAssignmentNotificationsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        Guid assignmentId,
        string requestCode,
        string title,
        Guid actorUserId,
        Guid recruitmentGroupId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @NotificationEventId UNIQUEIDENTIFIER =
            (
                SELECT TOP (1) NotificationEventId
                FROM dbo.NotificationEvents
                WHERE TenantId = @TenantId
                  AND EventCode = N'PMO_FORWARDED_TO_RECRUITING'
                  AND Status = N'Active'
            );

            IF @NotificationEventId IS NULL
            BEGIN
                RETURN;
            END;

            DECLARE @NotificationTemplateId UNIQUEIDENTIFIER =
            (
                SELECT TOP (1) NotificationTemplateId
                FROM dbo.NotificationTemplates
                WHERE TenantId = @TenantId
                  AND NotificationEventId = @NotificationEventId
                  AND Status = N'Active'
                ORDER BY CreatedAtUtc DESC
            );

            INSERT INTO dbo.NotificationRecipients
            (
                NotificationRecipientId,
                TenantId,
                NotificationEventId,
                RecipientUserId,
                CreatedAtUtc
            )
            SELECT
                NEWID(),
                @TenantId,
                @NotificationEventId,
                gm.UserId,
                SYSUTCDATETIME()
            FROM dbo.GroupMembers AS gm
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = gm.TenantId
                AND u.UserId = gm.UserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            WHERE gm.TenantId = @TenantId
              AND gm.GroupId = @RecruitmentGroupId;

            INSERT INTO dbo.NotificationOutbox
            (
                NotificationOutboxId,
                TenantId,
                NotificationEventId,
                NotificationTemplateId,
                RecipientUserId,
                RecipientEmail,
                Channel,
                PayloadJson,
                Status,
                AvailableAtUtc,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            SELECT
                NEWID(),
                @TenantId,
                @NotificationEventId,
                @NotificationTemplateId,
                gm.UserId,
                u.Email,
                channel.Channel,
                @PayloadJson,
                N'Pending',
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            FROM dbo.GroupMembers AS gm
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = gm.TenantId
                AND u.UserId = gm.UserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            CROSS JOIN (VALUES (N'Email'), (N'SignalR')) AS channel(Channel)
            WHERE gm.TenantId = @TenantId
              AND gm.GroupId = @RecruitmentGroupId;
            """;

        var payloadJson = JsonSerializer.Serialize(new
        {
            eventCode = "PMO_FORWARDED_TO_RECRUITING",
            jobRequestId,
            assignmentId,
            requestCode,
            title,
            actorUserId
        });

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                RecruitmentGroupId = recruitmentGroupId,
                PayloadJson = payloadJson
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertEmployeeReferralNotificationsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        IReadOnlyList<Guid> referralIds,
        string requestCode,
        string title,
        IReadOnlyList<string> employeeNames,
        Guid actorUserId,
        Guid presalesUserId,
        string? note,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @NotificationEventId UNIQUEIDENTIFIER =
            (
                SELECT TOP (1) NotificationEventId
                FROM dbo.NotificationEvents
                WHERE TenantId = @TenantId
                  AND EventCode = N'PMO_EMPLOYEE_REFERRED'
                  AND Status = N'Active'
            );

            IF @NotificationEventId IS NULL
            BEGIN
                RETURN;
            END;

            DECLARE @NotificationTemplateId UNIQUEIDENTIFIER =
            (
                SELECT TOP (1) NotificationTemplateId
                FROM dbo.NotificationTemplates
                WHERE TenantId = @TenantId
                  AND NotificationEventId = @NotificationEventId
                  AND Status = N'Active'
                ORDER BY CreatedAtUtc DESC
            );

            INSERT INTO dbo.NotificationRecipients
            (
                NotificationRecipientId,
                TenantId,
                NotificationEventId,
                RecipientUserId,
                CreatedAtUtc
            )
            SELECT
                NEWID(),
                @TenantId,
                @NotificationEventId,
                @PresalesUserId,
                SYSUTCDATETIME()
            WHERE EXISTS
            (
                SELECT 1
                FROM dbo.AppUsers AS u
                WHERE u.TenantId = @TenantId
                  AND u.UserId = @PresalesUserId
                  AND u.AccountStatus = N'Active'
                  AND u.DeletedAtUtc IS NULL
            )
              AND NOT EXISTS
            (
                SELECT 1
                FROM dbo.NotificationRecipients AS existing
                WHERE existing.NotificationEventId = @NotificationEventId
                  AND existing.RecipientUserId = @PresalesUserId
            );

            INSERT INTO dbo.NotificationOutbox
            (
                NotificationOutboxId,
                TenantId,
                NotificationEventId,
                NotificationTemplateId,
                RecipientUserId,
                RecipientEmail,
                Channel,
                PayloadJson,
                Status,
                AvailableAtUtc,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            SELECT
                NEWID(),
                @TenantId,
                @NotificationEventId,
                @NotificationTemplateId,
                @PresalesUserId,
                u.Email,
                channel.Channel,
                @PayloadJson,
                N'Pending',
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            FROM dbo.AppUsers AS u
            CROSS JOIN (VALUES (N'Email'), (N'SignalR')) AS channel(Channel)
            WHERE u.TenantId = @TenantId
              AND u.UserId = @PresalesUserId
              AND u.AccountStatus = N'Active'
              AND u.DeletedAtUtc IS NULL;
            """;

        var payloadJson = JsonSerializer.Serialize(new
        {
            eventCode = "PMO_EMPLOYEE_REFERRED",
            jobRequestId,
            referralIds,
            requestCode,
            title,
            employeeNames,
            actorUserId,
            note
        });

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                PresalesUserId = presalesUserId,
                PayloadJson = payloadJson
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private sealed record PersonRow(Guid UserId, string DisplayName, string Email, string? RoleCode, string? RoleName);

    private sealed record WorkflowIds(Guid WorkflowDefinitionId, Guid WorkflowStageId, Guid WorkflowTransitionId);

    private sealed record ForwardableRequestRow(
        Guid JobRequestId,
        string RequestCode,
        string Title,
        Guid? CurrentAssignmentId,
        Guid? AssignedToUserId,
        Guid? ClaimedByUserId,
        Guid? AssignedToGroupId);

    private sealed record ReferralRequestRow(
        Guid JobRequestId,
        string RequestCode,
        string Title,
        Guid PresalesUserId,
        Guid? CurrentAssignmentId,
        Guid? AssignedToUserId,
        Guid? ClaimedByUserId,
        Guid? AssignedToGroupId);

    private sealed record BenchMatchRow(
        Guid EmployeeId,
        string EmployeeCode,
        string DisplayName,
        string Email,
        string? Designation,
        string Department,
        string Location,
        string AvailabilityStatus,
        string BenchStatus,
        int CurrentAllocationPercent,
        string? SkillList,
        string? RequiredSkillList);

    private sealed record InternalEmployeeReferralRow(
        Guid Id,
        Guid JobRequestId,
        Guid EmployeeId,
        string EmployeeName,
        string EmployeeEmail,
        string Status,
        decimal? FitScore,
        string RecommendationSummary,
        Guid ReferredByUserId,
        Guid? PresalesUserId,
        DateTimeOffset CreatedAt);

    private sealed record JobRequestRow(
        Guid Id,
        string Code,
        string Title,
        string Client,
        string Description,
        string Department,
        string Location,
        decimal? ExperienceMinYears,
        decimal? ExperienceMaxYears,
        int RequiredPositions,
        int FulfilledPositions,
        string Priority,
        Guid HiringManagerId,
        Guid CreatedById,
        string Status,
        string CurrentStageKey,
        Guid? AssignedToUserId,
        Guid? ClaimedByUserId,
        string? AssignedToGroupName,
        string PublishStatus,
        DateTimeOffset CreatedAt,
        string? SkillList);
}
