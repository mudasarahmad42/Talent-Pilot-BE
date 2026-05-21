using System.Data;
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

        await transaction.CommitAsync(cancellationToken);

        var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, jobRequestId, cancellationToken)
            ?? throw new InvalidOperationException("Created job request could not be reloaded.");
        var assignment = await GetAssignmentByIdAsync(connection, tenantId, assignmentId, cancellationToken)
            ?? throw new InvalidOperationException("Created workflow assignment could not be reloaded.");

        return new CreateOperationsJobRequestResult(jobRequest, assignment);
    }

    public async Task<bool> ClaimAssignmentAsync(Guid tenantId, Guid actorUserId, Guid assignmentId, CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @ChangedAssignments TABLE (EntityId UNIQUEIDENTIFIER NOT NULL);

            UPDATE dbo.WorkflowAssignments
            SET AssignmentStatus = N'Claimed',
                ClaimedByUserId = @ActorUserId,
                AssignedToUserId = @ActorUserId,
                ClaimedAtUtc = SYSUTCDATETIME()
            OUTPUT inserted.EntityId INTO @ChangedAssignments
            WHERE TenantId = @TenantId
              AND WorkflowAssignmentId = @AssignmentId
              AND AssignmentStatus = N'Pending';

            UPDATE jr
            SET CurrentStageKey = N'BENCH_MATCHING',
                UpdatedAtUtc = SYSUTCDATETIME()
            FROM dbo.JobRequests AS jr
            INNER JOIN dbo.WorkflowAssignments AS wa
                ON wa.TenantId = jr.TenantId
                AND wa.EntityType = N'JobRequest'
                AND wa.EntityId = jr.JobRequestId
            WHERE wa.TenantId = @TenantId
              AND wa.WorkflowAssignmentId = @AssignmentId;

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
                g.Name AS AssignedToGroupId,
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
        Guid jobRequestId,
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
        const string sql = """
            SELECT TOP (1)
                wd.WorkflowDefinitionId,
                ws.WorkflowStageId,
                wt.WorkflowTransitionId
            FROM dbo.WorkflowDefinitions AS wd
            INNER JOIN dbo.WorkflowStages AS ws
                ON ws.WorkflowDefinitionId = wd.WorkflowDefinitionId
                AND ws.StageKey = N'PMO_REVIEW'
            INNER JOIN dbo.WorkflowTransitions AS wt
                ON wt.WorkflowDefinitionId = wd.WorkflowDefinitionId
                AND wt.ActionKey = N'CREATE_BY_PRESALES'
            WHERE wd.TenantId = @TenantId
              AND wd.Code = N'JOB_REQUEST_MVP';
            """;

        var ids = await connection.QuerySingleOrDefaultAsync<WorkflowIds>(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            transaction,
            cancellationToken: cancellationToken));

        return ids ?? throw new InvalidOperationException("JOB_REQUEST_MVP workflow seed data is missing.");
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

    private sealed record PersonRow(Guid UserId, string DisplayName, string Email, string? RoleCode, string? RoleName);

    private sealed record WorkflowIds(Guid WorkflowDefinitionId, Guid WorkflowStageId, Guid WorkflowTransitionId);

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
