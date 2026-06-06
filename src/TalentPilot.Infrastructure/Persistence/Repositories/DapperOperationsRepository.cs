using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using TalentPilot.Application.Ai;
using TalentPilot.Application.Notifications;
using TalentPilot.Application.Operations;
using TalentPilot.Domain.Access;
using TalentPilot.Domain.Notifications;

namespace TalentPilot.Infrastructure.Persistence.Repositories;

public sealed class DapperOperationsRepository : IOperationsRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly string _frontendBaseUrl;

    public DapperOperationsRepository(ISqlConnectionFactory connectionFactory, IConfiguration configuration)
    {
        _connectionFactory = connectionFactory;
        _frontendBaseUrl = NormalizeFrontendBaseUrl(
            configuration["Frontend:BaseUrl"]
            ?? configuration["TalentPilot:FrontendBaseUrl"]
            ?? Environment.GetEnvironmentVariable("TALENTPILOT_FRONTEND_BASE_URL")
            ?? "http://localhost:4200");
    }

    public async Task<OperationsSnapshot> GetSnapshotAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        var people = await ListPeopleAsync(connection, tenantId, cancellationToken);
        var jobRequests = await ListJobRequestsAsync(connection, tenantId, userId, cancellationToken);
        var assignments = await ListAssignmentsAsync(connection, tenantId, userId, cancellationToken);
        var notifications = await ListNotificationsAsync(connection, tenantId, userId, cancellationToken);

        return new OperationsSnapshot(people, jobRequests, assignments, notifications);
    }

    public async Task<TenantAdminDashboard> GetTenantAdminDashboardAsync(
        Guid tenantId,
        TenantAdminDashboardQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        var nowUtc = DateTime.UtcNow;
        var toUtc = (query.ToUtc ?? DateTimeOffset.UtcNow).UtcDateTime;
        var fromUtc = (query.FromUtc ?? DateTimeOffset.UtcNow.AddDays(-30)).UtcDateTime;
        var weekEndUtc = nowUtc.AddDays(7);

        var parameters = new
        {
            TenantId = tenantId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            NowUtc = nowUtc,
            WeekEndUtc = weekEndUtc,
            DepartmentId = query.DepartmentId,
            SourceLabel = query.SourceLabel,
            RecruiterUserId = query.RecruiterUserId
        };

        var departments = (await connection.QueryAsync<OperationsLookupOption>(new CommandDefinition(
            """
            SELECT DepartmentId AS Id, Name, Code AS Description
            FROM dbo.Departments
            WHERE TenantId = @TenantId
              AND Status = N'Active'
            ORDER BY Name;
            """,
            parameters,
            cancellationToken: cancellationToken))).ToArray();

        var sourceLabels = (await connection.QueryAsync<OperationsLookupOption>(new CommandDefinition(
            """
            SELECT CandidateSourceLabelId AS Id, DisplayName AS Name, ReportingCategory AS Description
            FROM dbo.CandidateSourceLabels
            WHERE TenantId = @TenantId
              AND Status = N'Active'
            ORDER BY DisplayName;
            """,
            parameters,
            cancellationToken: cancellationToken))).ToArray();

        var recruiters = (await connection.QueryAsync<OperationsLookupOption>(new CommandDefinition(
            """
            SELECT DISTINCT u.UserId AS Id, u.DisplayName AS Name, u.Email AS Description
            FROM dbo.AppUsers AS u
            INNER JOIN dbo.UserRoles AS ur
                ON ur.TenantId = u.TenantId
                AND ur.UserId = u.UserId
            INNER JOIN dbo.Roles AS r
                ON r.TenantId = ur.TenantId
                AND r.RoleId = ur.RoleId
                AND r.Code = N'Recruiter'
                AND r.Status = N'Active'
            WHERE u.TenantId = @TenantId
              AND u.AccountStatus = N'Active'
              AND u.DeletedAtUtc IS NULL
            ORDER BY u.DisplayName;
            """,
            parameters,
            cancellationToken: cancellationToken))).ToArray();

        var summary = await connection.QuerySingleAsync<DashboardSummaryRow>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*)
                 FROM dbo.JobRequests AS jr
                 WHERE jr.TenantId = @TenantId
                   AND jr.Status NOT IN (N'Closed', N'Cancelled')
                   AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)) AS OpenJobRequests,
                (SELECT COALESCE(SUM(jr.RequiredPositions), 0)
                 FROM dbo.JobRequests AS jr
                 WHERE jr.TenantId = @TenantId
                   AND jr.Status NOT IN (N'Closed', N'Cancelled')
                   AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)) AS RequiredPositions,
                (SELECT COALESCE(SUM(jr.FulfilledPositions), 0)
                 FROM dbo.JobRequests AS jr
                 WHERE jr.TenantId = @TenantId
                   AND jr.Status NOT IN (N'Closed', N'Cancelled')
                   AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)) AS FulfilledPositions,
                (SELECT COUNT(*)
                 FROM dbo.JobPosts AS jp
                 WHERE jp.TenantId = @TenantId
                   AND jp.Status = N'Published'
                   AND (@DepartmentId IS NULL OR jp.DepartmentId = @DepartmentId)
                   AND (@RecruiterUserId IS NULL OR jp.RecruiterOwnerUserId = @RecruiterUserId)) AS PublishedJobPosts,
                (SELECT COUNT(*)
                 FROM dbo.JobApplications AS ja
                 INNER JOIN dbo.JobRequests AS jr
                     ON jr.TenantId = ja.TenantId
                     AND jr.JobRequestId = ja.JobRequestId
                 LEFT JOIN dbo.JobPosts AS jp
                     ON jp.TenantId = ja.TenantId
                     AND jp.JobPostId = ja.JobPostId
                 WHERE ja.TenantId = @TenantId
                   AND ja.IsActive = CAST(1 AS BIT)
                   AND ja.CurrentStatus NOT IN (N'Rejected', N'Withdrawn', N'Joined', N'Hired')
                   AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
                   AND (@SourceLabel IS NULL OR ja.SourceLabel = @SourceLabel)
                   AND (@RecruiterUserId IS NULL OR jp.RecruiterOwnerUserId = @RecruiterUserId)) AS ActiveApplications,
                (SELECT COUNT(*)
                 FROM dbo.Interviews AS i
                 INNER JOIN dbo.JobApplications AS ja
                     ON ja.TenantId = i.TenantId
                     AND ja.JobApplicationId = i.JobApplicationId
                 INNER JOIN dbo.JobRequests AS jr
                     ON jr.TenantId = ja.TenantId
                     AND jr.JobRequestId = ja.JobRequestId
                 WHERE i.TenantId = @TenantId
                   AND i.StartsAtUtc >= @NowUtc
                   AND i.StartsAtUtc < @WeekEndUtc
                   AND i.Status = N'Scheduled'
                   AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)) AS InterviewsThisWeek,
                (SELECT COUNT(*)
                 FROM dbo.JobApplications AS ja
                 INNER JOIN dbo.JobRequests AS jr
                     ON jr.TenantId = ja.TenantId
                     AND jr.JobRequestId = ja.JobRequestId
                 WHERE ja.TenantId = @TenantId
                   AND ja.CurrentStatus = N'Offered'
                   AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
                   AND ja.UpdatedAtUtc >= @FromUtc
                   AND ja.UpdatedAtUtc <= @ToUtc) AS Offers,
                (SELECT COUNT(*)
                 FROM dbo.JobApplications AS ja
                 INNER JOIN dbo.JobRequests AS jr
                     ON jr.TenantId = ja.TenantId
                     AND jr.JobRequestId = ja.JobRequestId
                 WHERE ja.TenantId = @TenantId
                   AND ja.CurrentStatus IN (N'Joined', N'Hired')
                   AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
                   AND ja.UpdatedAtUtc >= @FromUtc
                   AND ja.UpdatedAtUtc <= @ToUtc) AS JoinedCandidates;
            """,
            parameters,
            cancellationToken: cancellationToken));

        var openPositions = Math.Max(0, summary.RequiredPositions - summary.FulfilledPositions);
        var dashboardSummary = new TenantAdminDashboardSummary(
            summary.OpenJobRequests,
            openPositions,
            summary.RequiredPositions,
            summary.FulfilledPositions,
            summary.PublishedJobPosts,
            summary.ActiveApplications,
            summary.InterviewsThisWeek,
            summary.Offers,
            summary.JoinedCandidates);

        var funnelRows = (await connection.QueryAsync<DashboardFunnelRow>(new CommandDefinition(
            """
            SELECT N'PMO Review' AS Label, COUNT(*) AS Count, 1 AS SortOrder
            FROM dbo.JobRequests
            WHERE TenantId = @TenantId
              AND CurrentStageKey = N'PMO_REVIEW'
              AND Status NOT IN (N'Closed', N'Cancelled')
              AND (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
            UNION ALL
            SELECT N'Recruiter Sourcing', COUNT(*), 2
            FROM dbo.JobRequests
            WHERE TenantId = @TenantId
              AND CurrentStageKey = N'SOURCING'
              AND Status NOT IN (N'Closed', N'Cancelled')
              AND (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
            UNION ALL
            SELECT N'Published Jobs', COUNT(*), 3
            FROM dbo.JobPosts
            WHERE TenantId = @TenantId
              AND Status = N'Published'
              AND (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
              AND (@RecruiterUserId IS NULL OR RecruiterOwnerUserId = @RecruiterUserId)
            UNION ALL
            SELECT N'Applications', COUNT(*), 4
            FROM dbo.JobApplications AS ja
            INNER JOIN dbo.JobRequests AS jr ON jr.TenantId = ja.TenantId AND jr.JobRequestId = ja.JobRequestId
            LEFT JOIN dbo.JobPosts AS jp ON jp.TenantId = ja.TenantId AND jp.JobPostId = ja.JobPostId
            WHERE ja.TenantId = @TenantId
              AND ja.AppliedAtUtc >= @FromUtc
              AND ja.AppliedAtUtc <= @ToUtc
              AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
              AND (@SourceLabel IS NULL OR ja.SourceLabel = @SourceLabel)
              AND (@RecruiterUserId IS NULL OR jp.RecruiterOwnerUserId = @RecruiterUserId)
            UNION ALL
            SELECT N'Interviewing', COUNT(DISTINCT ja.JobApplicationId), 5
            FROM dbo.JobApplications AS ja
            INNER JOIN dbo.JobRequests AS jr ON jr.TenantId = ja.TenantId AND jr.JobRequestId = ja.JobRequestId
            LEFT JOIN dbo.JobPosts AS jp ON jp.TenantId = ja.TenantId AND jp.JobPostId = ja.JobPostId
            WHERE ja.TenantId = @TenantId
              AND ja.CurrentStatus = N'Interviewing'
              AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
              AND (@SourceLabel IS NULL OR ja.SourceLabel = @SourceLabel)
              AND (@RecruiterUserId IS NULL OR jp.RecruiterOwnerUserId = @RecruiterUserId)
            UNION ALL
            SELECT N'Hiring Manager Review', COUNT(*), 6
            FROM dbo.JobApplications AS ja
            INNER JOIN dbo.JobRequests AS jr ON jr.TenantId = ja.TenantId AND jr.JobRequestId = ja.JobRequestId
            LEFT JOIN dbo.JobPosts AS jp ON jp.TenantId = ja.TenantId AND jp.JobPostId = ja.JobPostId
            WHERE ja.TenantId = @TenantId
              AND ja.CurrentStatus = N'HiringManagerReview'
              AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
              AND (@SourceLabel IS NULL OR ja.SourceLabel = @SourceLabel)
              AND (@RecruiterUserId IS NULL OR jp.RecruiterOwnerUserId = @RecruiterUserId)
            UNION ALL
            SELECT N'Offered', COUNT(*), 7
            FROM dbo.JobApplications AS ja
            INNER JOIN dbo.JobRequests AS jr ON jr.TenantId = ja.TenantId AND jr.JobRequestId = ja.JobRequestId
            LEFT JOIN dbo.JobPosts AS jp ON jp.TenantId = ja.TenantId AND jp.JobPostId = ja.JobPostId
            WHERE ja.TenantId = @TenantId
              AND ja.CurrentStatus = N'Offered'
              AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
              AND (@SourceLabel IS NULL OR ja.SourceLabel = @SourceLabel)
              AND (@RecruiterUserId IS NULL OR jp.RecruiterOwnerUserId = @RecruiterUserId)
            UNION ALL
            SELECT N'Joined', COUNT(*), 8
            FROM dbo.JobApplications AS ja
            INNER JOIN dbo.JobRequests AS jr ON jr.TenantId = ja.TenantId AND jr.JobRequestId = ja.JobRequestId
            LEFT JOIN dbo.JobPosts AS jp ON jp.TenantId = ja.TenantId AND jp.JobPostId = ja.JobPostId
            WHERE ja.TenantId = @TenantId
              AND ja.CurrentStatus IN (N'Joined', N'Hired')
              AND ja.UpdatedAtUtc >= @FromUtc
              AND ja.UpdatedAtUtc <= @ToUtc
              AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
              AND (@SourceLabel IS NULL OR ja.SourceLabel = @SourceLabel)
              AND (@RecruiterUserId IS NULL OR jp.RecruiterOwnerUserId = @RecruiterUserId)
            UNION ALL
            SELECT N'Closed Requests', COUNT(*), 9
            FROM dbo.JobRequests
            WHERE TenantId = @TenantId
              AND Status = N'Closed'
              AND UpdatedAtUtc >= @FromUtc
              AND UpdatedAtUtc <= @ToUtc
              AND (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
            ORDER BY SortOrder;
            """,
            parameters,
            cancellationToken: cancellationToken))).ToArray();

        var hiringFunnel = new List<TenantAdminDashboardFunnelItem>(funnelRows.Length);
        var previousCount = 0;
        foreach (var row in funnelRows.OrderBy(row => row.SortOrder))
        {
            var conversionRate = row.SortOrder == 1
                ? (row.Count > 0 ? 100m : 0m)
                : Rate(row.Count, previousCount);
            hiringFunnel.Add(new TenantAdminDashboardFunnelItem(row.Label, row.Count, conversionRate));
            previousCount = row.Count;
        }

        var attentionCounts = await connection.QuerySingleAsync<DashboardAttentionCountsRow>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*)
                 FROM dbo.Departments AS d
                 LEFT JOIN dbo.JobRequestIntakeRoutingRules AS r
                     ON r.TenantId = d.TenantId
                     AND r.DepartmentId = d.DepartmentId
                     AND r.Status = N'Active'
                 WHERE d.TenantId = @TenantId
                   AND d.Status = N'Active'
                   AND r.JobRequestIntakeRoutingRuleId IS NULL) AS MissingRouting,
                (SELECT COUNT(*)
                 FROM dbo.JobPosts AS jp
                 WHERE jp.TenantId = @TenantId
                   AND jp.Status = N'Published'
                   AND (@DepartmentId IS NULL OR jp.DepartmentId = @DepartmentId)
                   AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.JobApplications AS ja
                        WHERE ja.TenantId = jp.TenantId
                          AND ja.JobPostId = jp.JobPostId
                   )) AS PublishedPostsWithoutApplications,
                (SELECT COUNT(*)
                 FROM dbo.Interviews AS i
                 WHERE i.TenantId = @TenantId
                   AND i.Status IN (N'Scheduled', N'Completed')
                   AND i.StartsAtUtc < DATEADD(HOUR, -24, @NowUtc)
                   AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.InterviewFeedback AS f
                        WHERE f.TenantId = i.TenantId
                          AND f.InterviewId = i.InterviewId
                          AND f.IsSubmitted = CAST(1 AS BIT)
                   )) AS OverdueFeedback,
                (SELECT COUNT(*)
                 FROM dbo.JobApplications AS ja
                 WHERE ja.TenantId = @TenantId
                   AND ja.CurrentStatus = N'HiringManagerReview') AS HiringManagerPending,
                (SELECT COUNT(*)
                 FROM dbo.JobApplications AS ja
                 WHERE ja.TenantId = @TenantId
                   AND ja.CurrentStatus = N'Offered') AS OfferWaiting;
            """,
            parameters,
            cancellationToken: cancellationToken));

        var adminAttention = new[]
        {
            new TenantAdminDashboardAttentionItem(
                attentionCounts.MissingRouting > 0 ? "High" : "Low",
                "Departments without intake routing",
                "Configure PMO user/group routing so Presales requests avoid Tenant Admin fallback.",
                attentionCounts.MissingRouting,
                "/admin-center/workflows"),
            new TenantAdminDashboardAttentionItem(
                attentionCounts.PublishedPostsWithoutApplications > 0 ? "Medium" : "Low",
                "Published posts with no applications",
                "Recruiters may need sourcing support or stronger candidate rediscovery.",
                attentionCounts.PublishedPostsWithoutApplications,
                "/app/job-publishing"),
            new TenantAdminDashboardAttentionItem(
                attentionCounts.OverdueFeedback > 0 ? "High" : "Low",
                "Interview feedback overdue",
                "Completed or past scheduled interviews are waiting for submitted feedback.",
                attentionCounts.OverdueFeedback,
                "/app/interview-feedback"),
            new TenantAdminDashboardAttentionItem(
                attentionCounts.HiringManagerPending > 0 ? "Medium" : "Low",
                "Hiring manager reviews pending",
                "Candidates are waiting for final review or offer decisions.",
                attentionCounts.HiringManagerPending,
                "/app/hiring-manager/reviews"),
            new TenantAdminDashboardAttentionItem(
                attentionCounts.OfferWaiting > 0 ? "Medium" : "Low",
                "Offers awaiting outcome",
                "Offer decisions need follow-up before positions can be closed.",
                attentionCounts.OfferWaiting,
                "/app/offer-outcome")
        };

        var offerHealthRow = await connection.QuerySingleAsync<DashboardOfferHealthRow>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*) FROM dbo.OfferLetters WHERE TenantId = @TenantId AND CreatedAtUtc >= @FromUtc AND CreatedAtUtc <= @ToUtc) AS OfferLetters,
                (SELECT COUNT(*) FROM dbo.OfferPresentationMeetings WHERE TenantId = @TenantId AND CreatedAtUtc >= @FromUtc AND CreatedAtUtc <= @ToUtc) AS PresentationMeetings,
                (SELECT COUNT(*) FROM dbo.JobApplications WHERE TenantId = @TenantId AND CurrentStatus = N'Offered') AS Offered,
                (SELECT COUNT(*) FROM dbo.JobApplications WHERE TenantId = @TenantId AND CurrentStatus = N'OnHold') AS OnHold,
                (SELECT COUNT(*) FROM dbo.JobApplications WHERE TenantId = @TenantId AND CurrentStatus = N'Rejected') AS Rejected,
                (SELECT COUNT(*) FROM dbo.JobApplications WHERE TenantId = @TenantId AND CurrentStatus IN (N'Joined', N'Hired') AND UpdatedAtUtc >= @FromUtc AND UpdatedAtUtc <= @ToUtc) AS Joined;
            """,
            parameters,
            cancellationToken: cancellationToken));

        var offerHealth = new TenantAdminDashboardOfferHealth(
            offerHealthRow.OfferLetters,
            offerHealthRow.PresentationMeetings,
            offerHealthRow.Offered,
            offerHealthRow.OnHold,
            offerHealthRow.Rejected,
            offerHealthRow.Joined,
            openPositions);

        var candidatePipeline = (await connection.QueryAsync<TenantAdminDashboardPipelineItem>(new CommandDefinition(
            """
            SELECT CurrentStatus AS Status, COUNT(*) AS Count
            FROM dbo.JobApplications AS ja
            INNER JOIN dbo.JobRequests AS jr
                ON jr.TenantId = ja.TenantId
                AND jr.JobRequestId = ja.JobRequestId
            LEFT JOIN dbo.JobPosts AS jp
                ON jp.TenantId = ja.TenantId
                AND jp.JobPostId = ja.JobPostId
            WHERE ja.TenantId = @TenantId
              AND ja.IsActive = CAST(1 AS BIT)
              AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
              AND (@SourceLabel IS NULL OR ja.SourceLabel = @SourceLabel)
              AND (@RecruiterUserId IS NULL OR jp.RecruiterOwnerUserId = @RecruiterUserId)
            GROUP BY CurrentStatus
            ORDER BY COUNT(*) DESC, CurrentStatus;
            """,
            parameters,
            cancellationToken: cancellationToken))).ToArray();

        var averageTimeToFill = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            """
            SELECT AVG(CAST(DATEDIFF(DAY, jr.CreatedAtUtc, jrf.FulfilledAtUtc) AS DECIMAL(9,2)))
            FROM dbo.JobRequestFulfillments AS jrf
            INNER JOIN dbo.JobRequests AS jr
                ON jr.TenantId = jrf.TenantId
                AND jr.JobRequestId = jrf.JobRequestId
            WHERE jrf.TenantId = @TenantId
              AND jrf.FulfilledAtUtc >= @FromUtc
              AND jrf.FulfilledAtUtc <= @ToUtc
              AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId);
            """,
            parameters,
            cancellationToken: cancellationToken));

        var openRequestAges = (await connection.QueryAsync<int>(new CommandDefinition(
            """
            SELECT DATEDIFF(DAY, CreatedAtUtc, @NowUtc)
            FROM dbo.JobRequests
            WHERE TenantId = @TenantId
              AND Status NOT IN (N'Closed', N'Cancelled')
              AND (@DepartmentId IS NULL OR DepartmentId = @DepartmentId);
            """,
            parameters,
            cancellationToken: cancellationToken))).OrderBy(value => value).ToArray();

        var efficiencyCounts = await connection.QuerySingleAsync<DashboardEfficiencyCountsRow>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*)
                 FROM dbo.WorkflowAssignments AS wa
                 INNER JOIN dbo.WorkflowStages AS ws
                     ON ws.TenantId = wa.TenantId
                     AND ws.WorkflowStageId = wa.WorkflowStageId
                 WHERE wa.TenantId = @TenantId
                   AND wa.AssignmentStatus IN (N'Pending', N'Claimed')
                   AND ws.StageKey = N'PMO_REVIEW') AS PmoQueueLoad,
                (SELECT COUNT(*)
                 FROM dbo.WorkflowAssignments AS wa
                 INNER JOIN dbo.WorkflowStages AS ws
                     ON ws.TenantId = wa.TenantId
                     AND ws.WorkflowStageId = wa.WorkflowStageId
                 WHERE wa.TenantId = @TenantId
                   AND wa.AssignmentStatus IN (N'Pending', N'Claimed')
                   AND ws.StageKey = N'SOURCING') AS RecruiterSourcingLoad,
                (SELECT COUNT(*)
                 FROM dbo.Interviews AS i
                 WHERE i.TenantId = @TenantId
                   AND i.Status = N'Scheduled') AS InterviewerLoad,
                (SELECT COUNT(*)
                 FROM dbo.WorkflowAssignments AS wa
                 INNER JOIN dbo.WorkflowStages AS ws
                     ON ws.TenantId = wa.TenantId
                     AND ws.WorkflowStageId = wa.WorkflowStageId
                 WHERE wa.TenantId = @TenantId
                   AND wa.AssignmentStatus IN (N'Pending', N'Claimed')
                   AND ws.StageKey = N'HIRING_MANAGER_REVIEW') AS HiringManagerPendingReviews;
            """,
            parameters,
            cancellationToken: cancellationToken));

        var operationalEfficiency = new TenantAdminDashboardEfficiency(
            averageTimeToFill.HasValue ? decimal.Round(averageTimeToFill.Value, 1) : null,
            Median(openRequestAges),
            openRequestAges.Length > 0 ? openRequestAges[^1] : 0,
            efficiencyCounts.PmoQueueLoad,
            efficiencyCounts.RecruiterSourcingLoad,
            efficiencyCounts.InterviewerLoad,
            efficiencyCounts.HiringManagerPendingReviews);

        var stageAging = (await connection.QueryAsync<DashboardStageAgingRow>(new CommandDefinition(
            """
            SELECT TOP (8)
                jr.JobRequestId,
                jr.RequestCode,
                jr.Title,
                COALESCE(d.Name, N'Not recorded') AS Department,
                CASE
                    WHEN jr.CurrentStageKey = N'PMO_REVIEW' THEN N'PMO Review'
                    WHEN jr.CurrentStageKey = N'SOURCING' THEN N'Recruiter Sourcing'
                    WHEN jr.CurrentStageKey = N'INTERVIEWING' THEN N'Interviewing'
                    WHEN jr.CurrentStageKey = N'HIRING_MANAGER_REVIEW' THEN N'Hiring Manager Review'
                    WHEN jr.CurrentStageKey = N'OFFER' THEN N'Offer Outcome'
                    ELSE jr.Status
                END AS CurrentStage,
                COALESCE(cu.DisplayName, au.DisplayName, g.Name, r.Name, N'Unassigned') AS OwnerName,
                DATEDIFF(DAY, COALESCE(wa.AssignedAtUtc, jr.UpdatedAtUtc, jr.CreatedAtUtc), @NowUtc) AS DaysInStage,
                CASE
                    WHEN DATEDIFF(DAY, COALESCE(wa.AssignedAtUtc, jr.UpdatedAtUtc, jr.CreatedAtUtc), @NowUtc) >= 14 THEN N'High'
                    WHEN DATEDIFF(DAY, COALESCE(wa.AssignedAtUtc, jr.UpdatedAtUtc, jr.CreatedAtUtc), @NowUtc) >= 7 THEN N'Medium'
                    ELSE N'Low'
                END AS Risk
            FROM dbo.JobRequests AS jr
            LEFT JOIN dbo.Departments AS d
                ON d.TenantId = jr.TenantId
                AND d.DepartmentId = jr.DepartmentId
            OUTER APPLY (
                SELECT TOP (1) wa.*
                FROM dbo.WorkflowAssignments AS wa
                WHERE wa.TenantId = jr.TenantId
                  AND wa.EntityType = N'JobRequest'
                  AND wa.EntityId = jr.JobRequestId
                  AND wa.AssignmentStatus IN (N'Pending', N'Claimed')
                ORDER BY wa.AssignedAtUtc DESC
            ) AS wa
            LEFT JOIN dbo.AppUsers AS au ON au.TenantId = jr.TenantId AND au.UserId = wa.AssignedToUserId
            LEFT JOIN dbo.AppUsers AS cu ON cu.TenantId = jr.TenantId AND cu.UserId = wa.ClaimedByUserId
            LEFT JOIN dbo.Groups AS g ON g.TenantId = jr.TenantId AND g.GroupId = wa.AssignedToGroupId
            LEFT JOIN dbo.Roles AS r ON r.TenantId = jr.TenantId AND r.RoleId = wa.AssignedToRoleId
            WHERE jr.TenantId = @TenantId
              AND jr.Status NOT IN (N'Closed', N'Cancelled')
              AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
            ORDER BY DaysInStage DESC, jr.UpdatedAtUtc ASC;
            """,
            parameters,
            cancellationToken: cancellationToken)))
            .Select(row => new TenantAdminDashboardStageAgingItem(
                row.JobRequestId,
                row.RequestCode,
                row.Title,
                row.Department,
                row.CurrentStage,
                row.OwnerName,
                row.DaysInStage,
                row.Risk))
            .ToArray();

        var departmentPerformance = (await connection.QueryAsync<DashboardDepartmentPerformanceRow>(new CommandDefinition(
            """
            SELECT
                d.Name AS Department,
                COUNT(DISTINCT CASE WHEN jr.Status NOT IN (N'Closed', N'Cancelled') THEN jr.JobRequestId END) AS OpenRequests,
                COALESCE(SUM(CASE WHEN jr.Status NOT IN (N'Closed', N'Cancelled') THEN jr.RequiredPositions - jr.FulfilledPositions ELSE 0 END), 0) AS OpenPositions,
                COUNT(DISTINCT ja.JobApplicationId) AS Applications,
                COUNT(DISTINCT i.InterviewId) AS Interviews,
                COUNT(DISTINCT CASE WHEN ja.CurrentStatus IN (N'Joined', N'Hired') THEN ja.JobApplicationId END) AS Joined,
                AVG(CASE
                    WHEN jrf.JobRequestFulfillmentId IS NOT NULL THEN CAST(DATEDIFF(DAY, jr.CreatedAtUtc, jrf.FulfilledAtUtc) AS DECIMAL(9,2))
                    ELSE NULL
                END) AS AverageTimeToFillDays
            FROM dbo.Departments AS d
            LEFT JOIN dbo.JobRequests AS jr
                ON jr.TenantId = d.TenantId
                AND jr.DepartmentId = d.DepartmentId
            LEFT JOIN dbo.JobApplications AS ja
                ON ja.TenantId = jr.TenantId
                AND ja.JobRequestId = jr.JobRequestId
                AND ja.AppliedAtUtc >= @FromUtc
                AND ja.AppliedAtUtc <= @ToUtc
            LEFT JOIN dbo.Interviews AS i
                ON i.TenantId = ja.TenantId
                AND i.JobApplicationId = ja.JobApplicationId
                AND i.StartsAtUtc >= @FromUtc
                AND i.StartsAtUtc <= @ToUtc
            LEFT JOIN dbo.JobRequestFulfillments AS jrf
                ON jrf.TenantId = jr.TenantId
                AND jrf.JobRequestId = jr.JobRequestId
                AND jrf.FulfilledAtUtc >= @FromUtc
                AND jrf.FulfilledAtUtc <= @ToUtc
            WHERE d.TenantId = @TenantId
              AND d.Status = N'Active'
              AND (@DepartmentId IS NULL OR d.DepartmentId = @DepartmentId)
            GROUP BY d.Name
            ORDER BY OpenRequests DESC, Applications DESC, d.Name;
            """,
            parameters,
            cancellationToken: cancellationToken)))
            .Select(row => new TenantAdminDashboardDepartmentPerformanceItem(
                row.Department,
                row.OpenRequests,
                row.OpenPositions,
                row.Applications,
                row.Interviews,
                row.Joined,
                row.AverageTimeToFillDays.HasValue ? decimal.Round(row.AverageTimeToFillDays.Value, 1) : null))
            .ToArray();

        var skillsDemand = (await connection.QueryAsync<TenantAdminDashboardSkillDemandItem>(new CommandDefinition(
            """
            WITH Demand AS
            (
                SELECT s.SkillId, s.Name, COUNT(*) AS DemandCount
                FROM dbo.JobRequestSkills AS jrs
                INNER JOIN dbo.JobRequests AS jr
                    ON jr.TenantId = jrs.TenantId
                    AND jr.JobRequestId = jrs.JobRequestId
                INNER JOIN dbo.Skills AS s
                    ON s.TenantId = jrs.TenantId
                    AND s.SkillId = jrs.SkillId
                WHERE jrs.TenantId = @TenantId
                  AND jr.Status NOT IN (N'Closed', N'Cancelled')
                  AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
                GROUP BY s.SkillId, s.Name
            ),
            CandidateAvailability AS
            (
                SELECT cs.SkillId, COUNT(DISTINCT cs.CandidateId) AS CandidateCount
                FROM dbo.CandidateSkills AS cs
                INNER JOIN dbo.Candidates AS c
                    ON c.TenantId = cs.TenantId
                    AND c.CandidateId = cs.CandidateId
                    AND c.Status = N'Active'
                WHERE cs.TenantId = @TenantId
                GROUP BY cs.SkillId
            )
            SELECT TOP (8)
                d.Name AS Skill,
                d.DemandCount,
                COALESCE(ca.CandidateCount, 0) AS CandidateCount,
                d.DemandCount - COALESCE(ca.CandidateCount, 0) AS Gap
            FROM Demand AS d
            LEFT JOIN CandidateAvailability AS ca ON ca.SkillId = d.SkillId
            ORDER BY d.DemandCount DESC, d.Name;
            """,
            parameters,
            cancellationToken: cancellationToken))).ToArray();

        var sourceQuality = (await connection.QueryAsync<TenantAdminDashboardSourceQualityItem>(new CommandDefinition(
            """
            WITH SourceStats AS
            (
                SELECT
                    ja.SourceLabel,
                    COUNT(DISTINCT ja.JobApplicationId) AS Applications,
                    COUNT(DISTINCT CASE
                        WHEN i.Status = N'Completed'
                             AND (
                                 f.Recommendation = N'Proceed'
                                 OR (
                                    COALESCE(f.TechnicalScore, f.CommunicationScore, f.CultureScore) IS NOT NULL
                                    AND (
                                        COALESCE(f.TechnicalScore, 0)
                                        + COALESCE(f.CommunicationScore, 0)
                                        + COALESCE(f.CultureScore, 0)
                                    ) / NULLIF(
                                        (CASE WHEN f.TechnicalScore IS NULL THEN 0 ELSE 1 END)
                                        + (CASE WHEN f.CommunicationScore IS NULL THEN 0 ELSE 1 END)
                                        + (CASE WHEN f.CultureScore IS NULL THEN 0 ELSE 1 END),
                                        0
                                    ) >= 3.5
                                 )
                             )
                        THEN i.InterviewId
                    END) AS PassedInterviews,
                    COUNT(DISTINCT CASE WHEN i.Status IN (N'Completed', N'NoShow', N'Skipped', N'Cancelled') THEN i.InterviewId END) AS TotalInterviews,
                    COUNT(DISTINCT CASE WHEN ja.CurrentStatus = N'Offered' THEN ja.JobApplicationId END) AS Offers,
                    COUNT(DISTINCT CASE WHEN ja.CurrentStatus IN (N'Joined', N'Hired') THEN ja.JobApplicationId END) AS Joined,
                    COUNT(DISTINCT CASE WHEN ja.CurrentStatus IN (N'Rejected', N'Withdrawn') THEN ja.JobApplicationId END) AS RejectedWithdrawn
                FROM dbo.JobApplications AS ja
                INNER JOIN dbo.JobRequests AS jr
                    ON jr.TenantId = ja.TenantId
                    AND jr.JobRequestId = ja.JobRequestId
                LEFT JOIN dbo.JobPosts AS jp
                    ON jp.TenantId = ja.TenantId
                    AND jp.JobPostId = ja.JobPostId
                LEFT JOIN dbo.Interviews AS i
                    ON i.TenantId = ja.TenantId
                    AND i.JobApplicationId = ja.JobApplicationId
                LEFT JOIN dbo.InterviewFeedback AS f
                    ON f.TenantId = i.TenantId
                    AND f.InterviewId = i.InterviewId
                    AND f.IsSubmitted = CAST(1 AS BIT)
                WHERE ja.TenantId = @TenantId
                  AND ja.AppliedAtUtc >= @FromUtc
                  AND ja.AppliedAtUtc <= @ToUtc
                  AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
                  AND (@SourceLabel IS NULL OR ja.SourceLabel = @SourceLabel)
                  AND (@RecruiterUserId IS NULL OR jp.RecruiterOwnerUserId = @RecruiterUserId)
                GROUP BY ja.SourceLabel
            )
            SELECT
                SourceLabel,
                Applications,
                CASE WHEN TotalInterviews = 0 THEN CAST(0 AS DECIMAL(6,2)) ELSE CAST(PassedInterviews * 100.0 / TotalInterviews AS DECIMAL(6,2)) END AS InterviewPassRate,
                Offers,
                Joined,
                CASE WHEN Applications = 0 THEN CAST(0 AS DECIMAL(6,2)) ELSE CAST(RejectedWithdrawn * 100.0 / Applications AS DECIMAL(6,2)) END AS RejectionWithdrawalRate
            FROM SourceStats
            ORDER BY Applications DESC, SourceLabel;
            """,
            parameters,
            cancellationToken: cancellationToken))).ToArray();

        var interviewOperations = await connection.QuerySingleAsync<TenantAdminDashboardInterviewOperations>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*) FROM dbo.Interviews WHERE TenantId = @TenantId AND Status = N'Scheduled' AND StartsAtUtc >= @FromUtc AND StartsAtUtc <= @ToUtc) AS Scheduled,
                (SELECT COUNT(*) FROM dbo.Interviews WHERE TenantId = @TenantId AND Status = N'Completed' AND UpdatedAtUtc >= @FromUtc AND UpdatedAtUtc <= @ToUtc) AS Completed,
                (SELECT COUNT(*) FROM dbo.Interviews WHERE TenantId = @TenantId AND Status = N'Skipped' AND UpdatedAtUtc >= @FromUtc AND UpdatedAtUtc <= @ToUtc) AS Skipped,
                (SELECT COUNT(*) FROM dbo.Interviews WHERE TenantId = @TenantId AND Status = N'NoShow' AND UpdatedAtUtc >= @FromUtc AND UpdatedAtUtc <= @ToUtc) AS NoShow,
                (SELECT COUNT(*)
                 FROM dbo.Interviews AS i
                 WHERE i.TenantId = @TenantId
                   AND i.Status IN (N'Scheduled', N'Completed')
                   AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.InterviewFeedback AS f
                        WHERE f.TenantId = i.TenantId
                          AND f.InterviewId = i.InterviewId
                          AND f.IsSubmitted = CAST(1 AS BIT)
                   )) AS PendingFeedback,
                (SELECT COUNT(*)
                 FROM dbo.Interviews AS i
                 WHERE i.TenantId = @TenantId
                   AND i.Status IN (N'Scheduled', N'Completed')
                   AND i.StartsAtUtc < DATEADD(HOUR, -24, @NowUtc)
                   AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.InterviewFeedback AS f
                        WHERE f.TenantId = i.TenantId
                          AND f.InterviewId = i.InterviewId
                          AND f.IsSubmitted = CAST(1 AS BIT)
                   )) AS OverdueFeedback;
            """,
            parameters,
            cancellationToken: cancellationToken));

        var aiHealthRow = await connection.QuerySingleAsync<DashboardAiHealthRow>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*)
                 FROM dbo.AiAgentRuns
                 WHERE TenantId = @TenantId
                   AND StartedAtUtc >= CONVERT(date, @NowUtc)
                   AND StartedAtUtc < DATEADD(day, 1, CONVERT(date, @NowUtc))) AS RunsToday,
                (SELECT COUNT(*)
                 FROM dbo.AiAgentRuns
                 WHERE TenantId = @TenantId
                   AND Status = N'Failed'
                   AND StartedAtUtc >= @FromUtc
                   AND StartedAtUtc <= @ToUtc) AS FailedRuns,
                (SELECT MAX(CompletedAtUtc)
                 FROM dbo.AiAgentRuns
                 WHERE TenantId = @TenantId
                   AND AiAgentDefinitionId = N'bench-matching'
                   AND Status = N'Succeeded') AS LatestBenchMatchingAt,
                (SELECT MAX(CompletedAtUtc)
                 FROM dbo.AiAgentRuns
                 WHERE TenantId = @TenantId
                   AND AiAgentDefinitionId = N'talent-rediscovery'
                   AND Status = N'Succeeded') AS LatestTalentRediscoveryAt,
                (SELECT COUNT(*) FROM dbo.VectorEmbeddings WHERE TenantId = @TenantId AND IsActive = CAST(1 AS BIT)) AS ActiveEmbeddings,
                (SELECT COUNT(*) FROM dbo.VectorEmbeddings WHERE TenantId = @TenantId AND IsActive = CAST(1 AS BIT) AND EntityType = N'Candidate') AS CandidateEmbeddings,
                (SELECT COUNT(*) FROM dbo.VectorEmbeddings WHERE TenantId = @TenantId AND IsActive = CAST(1 AS BIT) AND EntityType = N'JobRequest') AS JobRequestEmbeddings,
                (SELECT COUNT(*) FROM dbo.VectorEmbeddings WHERE TenantId = @TenantId AND IsActive = CAST(1 AS BIT) AND EntityType = N'JobPost') AS JobPostEmbeddings,
                (SELECT COUNT(*) FROM dbo.VectorEmbeddings WHERE TenantId = @TenantId AND IsActive = CAST(1 AS BIT) AND EntityType = N'Employee') AS EmployeeEmbeddings;
            """,
            parameters,
            cancellationToken: cancellationToken));

        var aiHealth = new TenantAdminDashboardAiHealth(
            aiHealthRow.RunsToday,
            aiHealthRow.FailedRuns,
            ToUtc(aiHealthRow.LatestBenchMatchingAt),
            ToUtc(aiHealthRow.LatestTalentRediscoveryAt),
            aiHealthRow.ActiveEmbeddings,
            aiHealthRow.CandidateEmbeddings,
            aiHealthRow.JobRequestEmbeddings,
            aiHealthRow.JobPostEmbeddings,
            aiHealthRow.EmployeeEmbeddings);

        return new TenantAdminDashboard(
            DateTimeOffset.UtcNow,
            new DateTimeOffset(fromUtc, TimeSpan.Zero),
            new DateTimeOffset(toUtc, TimeSpan.Zero),
            new TenantAdminDashboardFilterOptions(departments, sourceLabels, recruiters),
            dashboardSummary,
            hiringFunnel,
            adminAttention,
            offerHealth,
            candidatePipeline,
            operationalEfficiency,
            stageAging,
            departmentPerformance,
            skillsDemand,
            sourceQuality,
            interviewOperations,
            aiHealth);
    }

    public async Task<PmoDashboard> GetPmoDashboardAsync(
        Guid tenantId,
        Guid actorUserId,
        PmoDashboardQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        var actorRoleCodes = await ReadActorRoleCodesAsync(connection, null, tenantId, actorUserId, cancellationToken);
        var isTenantAdmin = actorRoleCodes.Contains(AccessConstants.TenantAdminRoleCode);
        var nowUtc = DateTime.UtcNow;
        var toUtc = (query.ToUtc ?? DateTimeOffset.UtcNow).UtcDateTime;
        var fromUtc = (query.FromUtc ?? DateTimeOffset.UtcNow.AddDays(-30)).UtcDateTime;
        var parameters = new
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            IsTenantAdmin = isTenantAdmin,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            NowUtc = nowUtc,
            DepartmentId = query.DepartmentId
        };

        var departments = (await connection.QueryAsync<OperationsLookupOption>(new CommandDefinition(
            """
            SELECT DepartmentId AS Id, Name, Code AS Description
            FROM dbo.Departments
            WHERE TenantId = @TenantId
              AND Status = N'Active'
            ORDER BY Name;
            """,
            parameters,
            cancellationToken: cancellationToken))).ToArray();

        var queueRows = (await connection.QueryAsync<PmoDashboardAssignmentRow>(new CommandDefinition(
            """
            SELECT
                jr.JobRequestId,
                jr.RequestCode,
                jr.Title,
                jr.ClientName AS Client,
                COALESCE(d.Name, N'Unassigned') AS Department,
                COALESCE(l.Name, N'Unassigned') AS Location,
                jr.Priority,
                wa.WorkflowAssignmentId AS AssignmentId,
                wa.AssignmentStatus,
                wa.AssignedToUserId,
                wa.ClaimedByUserId,
                claimed.DisplayName AS ClaimedByName,
                wa.AssignedAtUtc,
                COALESCE(latestAudit.EventSummary, N'No activity yet.') AS LatestAction,
                CAST(CASE
                    WHEN wa.AssignedToUserId = @ActorUserId THEN 1
                    WHEN EXISTS
                    (
                        SELECT 1
                        FROM dbo.GroupMembers AS gm
                        INNER JOIN dbo.Groups AS activeGroup
                            ON activeGroup.TenantId = gm.TenantId
                            AND activeGroup.GroupId = gm.GroupId
                            AND activeGroup.Status = N'Active'
                        WHERE gm.TenantId = wa.TenantId
                          AND gm.GroupId = wa.AssignedToGroupId
                          AND gm.UserId = @ActorUserId
                    ) THEN 1
                    WHEN EXISTS
                    (
                        SELECT 1
                        FROM dbo.UserRoles AS ur
                        INNER JOIN dbo.Roles AS role
                            ON role.TenantId = ur.TenantId
                            AND role.RoleId = ur.RoleId
                            AND role.Status = N'Active'
                        WHERE ur.TenantId = wa.TenantId
                          AND ur.UserId = @ActorUserId
                          AND ur.RoleId = wa.AssignedToRoleId
                    ) THEN 1
                    ELSE 0
                END AS BIT) AS ActorCanClaimOrOwnPendingWork
            FROM dbo.WorkflowAssignments AS wa
            INNER JOIN dbo.WorkflowStages AS ws
                ON ws.WorkflowStageId = wa.WorkflowStageId
                AND ws.StageKey = N'PMO_REVIEW'
            INNER JOIN dbo.JobRequests AS jr
                ON jr.TenantId = wa.TenantId
                AND jr.JobRequestId = wa.EntityId
            LEFT JOIN dbo.Departments AS d
                ON d.TenantId = jr.TenantId
                AND d.DepartmentId = jr.DepartmentId
            LEFT JOIN dbo.Locations AS l
                ON l.TenantId = jr.TenantId
                AND l.LocationId = jr.LocationId
            LEFT JOIN dbo.AppUsers AS claimed
                ON claimed.TenantId = wa.TenantId
                AND claimed.UserId = wa.ClaimedByUserId
            OUTER APPLY
            (
                SELECT TOP (1) audit.EventSummary
                FROM dbo.AuditLogs AS audit
                WHERE audit.TenantId = jr.TenantId
                  AND audit.EntityType = N'JobRequest'
                  AND audit.EntityId = jr.JobRequestId
                ORDER BY audit.OccurredAtUtc DESC
            ) AS latestAudit
            WHERE wa.TenantId = @TenantId
              AND wa.EntityType = N'JobRequest'
              AND wa.AssignmentStatus IN (N'Pending', N'Claimed')
              AND jr.Status NOT IN (N'Closed', N'Cancelled')
              AND (@DepartmentId IS NULL OR jr.DepartmentId = @DepartmentId)
            ORDER BY wa.AssignedAtUtc ASC;
            """,
            parameters,
            cancellationToken: cancellationToken))).ToArray();

        var visibleRows = queueRows
            .Where(row => PmoDashboardVisibility.CanShowAssignment(
                row.AssignmentStatus,
                row.AssignedToUserId,
                row.ClaimedByUserId,
                row.ActorCanClaimOrOwnPendingWork,
                actorUserId,
                isTenantAdmin))
            .ToArray();

        var workItems = new List<PmoDashboardWorkItem>();
        var benchInsights = new List<PmoDashboardBenchInsight>();
        foreach (var row in visibleRows)
        {
            var eligibleEmployees = await ListEligibleBenchEmployeesAsync(connection, tenantId, row.JobRequestId, cancellationToken);
            var benchMatches = await ListLatestBenchMatchesAsync(connection, tenantId, row.JobRequestId, cancellationToken);
            var referrals = await ListEmployeeReferralsAsync(connection, tenantId, row.JobRequestId, cancellationToken);
            var topMatch = benchMatches.OrderBy(match => match.Rank).FirstOrDefault();
            var topEmployee = topMatch is null
                ? null
                : eligibleEmployees.FirstOrDefault(employee => employee.EmployeeId == topMatch.EmployeeId)?.DisplayName;
            var pendingReferrals = referrals.Count(referral => string.Equals(referral.Status, "Referred", StringComparison.OrdinalIgnoreCase));
            var daysWaiting = Math.Max(0, (int)Math.Floor((nowUtc - row.AssignedAtUtc).TotalDays));
            var ownerState = BuildPmoOwnerState(row.AssignmentStatus, row.ClaimedByUserId, row.ClaimedByName, actorUserId, isTenantAdmin);
            var cta = BuildPmoCta(row.AssignmentStatus, row.ClaimedByUserId, actorUserId);

            workItems.Add(new PmoDashboardWorkItem(
                row.JobRequestId,
                row.RequestCode,
                row.Title,
                row.Client,
                row.Department,
                row.Location,
                NormalizePriority(row.Priority),
                row.AssignmentId,
                row.AssignmentStatus,
                ownerState,
                row.ClaimedByName,
                Utc(row.AssignedAtUtc),
                daysWaiting,
                row.LatestAction,
                benchMatches.Count > 0,
                topMatch?.Score,
                eligibleEmployees.Count,
                pendingReferrals,
                cta));

            var locationFitCount = eligibleEmployees.Count(employee => ScoreLocationFit(row.Location, employee.Location) >= 0.75m);
            var averageMatchedSkills = eligibleEmployees.Count == 0
                ? 0
                : (int)Math.Round(eligibleEmployees.Average(employee => employee.MatchedSkills.Count));
            var openSkillGaps = eligibleEmployees
                .SelectMany(employee => employee.MissingSkills)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            benchInsights.Add(new PmoDashboardBenchInsight(
                row.JobRequestId,
                row.RequestCode,
                row.Title,
                topMatch?.GeneratedAt,
                topMatch?.Score,
                topEmployee,
                eligibleEmployees.Count,
                locationFitCount,
                averageMatchedSkills,
                openSkillGaps,
                topMatch is null ? "Not ranked" : "Ranked"));
        }

        var visibleRequestIds = visibleRows.Select(row => row.JobRequestId).Distinct().ToArray();
        var summary = await BuildPmoDashboardSummaryAsync(
            connection,
            tenantId,
            actorUserId,
            isTenantAdmin,
            visibleRows,
            fromUtc,
            toUtc,
            cancellationToken);
        var recommendationOutcomes = await ReadPmoRecommendationOutcomesAsync(
            connection,
            tenantId,
            actorUserId,
            isTenantAdmin,
            fromUtc,
            toUtc,
            cancellationToken);
        var decisionSplit = await ReadPmoDecisionSplitAsync(
            connection,
            tenantId,
            actorUserId,
            isTenantAdmin,
            fromUtc,
            toUtc,
            cancellationToken);
        var recommendationTrend = await ReadPmoRecommendationTrendAsync(
            connection,
            tenantId,
            actorUserId,
            isTenantAdmin,
            fromUtc,
            toUtc,
            cancellationToken);
        var skillDemand = visibleRequestIds.Length == 0
            ? Array.Empty<PmoDashboardSkillBenchItem>()
            : await ReadPmoSkillDemandAsync(connection, tenantId, visibleRequestIds, cancellationToken);
        var aiHealth = await ReadPmoAiHealthAsync(connection, tenantId, fromUtc, toUtc, cancellationToken);

        var agingBuckets = BuildPmoAgingBuckets(workItems);
        var departmentLoad = visibleRows
            .GroupBy(row => row.Department)
            .Select(group => new PmoDashboardDepartmentLoad(
                group.Key,
                group.Count(row => string.Equals(row.AssignmentStatus, "Pending", StringComparison.OrdinalIgnoreCase)),
                group.Count(row => string.Equals(row.AssignmentStatus, "Claimed", StringComparison.OrdinalIgnoreCase)),
                group.Any()
                    ? decimal.Round((decimal)group.Average(row => Math.Max(0, (nowUtc - row.AssignedAtUtc).TotalDays)), 1)
                    : 0m))
            .OrderByDescending(item => item.PendingReviews + item.ClaimedReviews)
            .ThenBy(item => item.Department)
            .ToArray();

        return new PmoDashboard(
            DateTimeOffset.UtcNow,
            new DateTimeOffset(fromUtc, TimeSpan.Zero),
            new DateTimeOffset(toUtc, TimeSpan.Zero),
            new PmoDashboardFilterOptions(departments),
            summary,
            workItems.OrderByDescending(item => item.DaysWaiting).ThenBy(item => item.RequestCode).ToArray(),
            benchInsights.OrderByDescending(item => item.TopFitScore ?? -1).ThenBy(item => item.RequestCode).ToArray(),
            recommendationOutcomes,
            agingBuckets,
            departmentLoad,
            decisionSplit,
            recommendationTrend,
            skillDemand,
            aiHealth);
    }

    public async Task<OperationsJobRequestIntakeOptions> GetIntakeOptionsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string departmentsSql = """
            SELECT
                d.DepartmentId,
                d.Code,
                d.Name,
                CASE
                    WHEN ru.UserId IS NOT NULL THEN N'User'
                    WHEN rg.GroupId IS NOT NULL
                         AND EXISTS (
                            SELECT 1
                            FROM dbo.GroupMembers AS gm
                            INNER JOIN dbo.AppUsers AS gu
                                ON gu.TenantId = gm.TenantId
                                AND gu.UserId = gm.UserId
                                AND gu.AccountStatus = N'Active'
                                AND gu.DeletedAtUtc IS NULL
                            WHERE gm.TenantId = d.TenantId
                              AND gm.GroupId = rg.GroupId
                         ) THEN N'Group'
                    ELSE N'Fallback'
                END AS AssignmentType,
                CASE WHEN ru.UserId IS NOT NULL THEN ru.UserId ELSE NULL END AS TargetUserId,
                CASE
                    WHEN rg.GroupId IS NOT NULL
                         AND EXISTS (
                            SELECT 1
                            FROM dbo.GroupMembers AS gm
                            INNER JOIN dbo.AppUsers AS gu
                                ON gu.TenantId = gm.TenantId
                                AND gu.UserId = gm.UserId
                                AND gu.AccountStatus = N'Active'
                                AND gu.DeletedAtUtc IS NULL
                            WHERE gm.TenantId = d.TenantId
                              AND gm.GroupId = rg.GroupId
                         ) THEN rg.GroupId
                    ELSE NULL
                END AS TargetGroupId,
                CASE
                    WHEN ru.UserId IS NOT NULL THEN ru.DisplayName
                    WHEN rg.GroupId IS NOT NULL
                         AND EXISTS (
                            SELECT 1
                            FROM dbo.GroupMembers AS gm
                            INNER JOIN dbo.AppUsers AS gu
                                ON gu.TenantId = gm.TenantId
                                AND gu.UserId = gm.UserId
                                AND gu.AccountStatus = N'Active'
                                AND gu.DeletedAtUtc IS NULL
                            WHERE gm.TenantId = d.TenantId
                              AND gm.GroupId = rg.GroupId
                         ) THEN rg.Name
                    ELSE N'Tenant Admins'
                END AS TargetName,
                CASE
                    WHEN ru.UserId IS NOT NULL THEN CAST(0 AS BIT)
                    WHEN rg.GroupId IS NOT NULL
                         AND EXISTS (
                            SELECT 1
                            FROM dbo.GroupMembers AS gm
                            INNER JOIN dbo.AppUsers AS gu
                                ON gu.TenantId = gm.TenantId
                                AND gu.UserId = gm.UserId
                                AND gu.AccountStatus = N'Active'
                                AND gu.DeletedAtUtc IS NULL
                            WHERE gm.TenantId = d.TenantId
                              AND gm.GroupId = rg.GroupId
                         ) THEN CAST(0 AS BIT)
                    ELSE CAST(1 AS BIT)
                END AS UsesTenantAdminFallback
            FROM dbo.Departments AS d
            LEFT JOIN dbo.JobRequestIntakeRoutingRules AS r
                ON r.TenantId = d.TenantId
                AND r.DepartmentId = d.DepartmentId
                AND r.Status = N'Active'
            LEFT JOIN dbo.AppUsers AS ru
                ON ru.TenantId = d.TenantId
                AND ru.UserId = r.TargetUserId
                AND ru.AccountStatus = N'Active'
                AND ru.DeletedAtUtc IS NULL
            LEFT JOIN dbo.Groups AS rg
                ON rg.TenantId = d.TenantId
                AND rg.GroupId = r.TargetGroupId
                AND rg.Status = N'Active'
            WHERE d.TenantId = @TenantId
              AND d.Status = N'Active'
            ORDER BY d.Name;
            """;

        const string locationsSql = """
            SELECT LocationId AS Id, Name, TimezoneId AS Description
            FROM dbo.Locations
            WHERE TenantId = @TenantId
              AND Status = N'Active'
            ORDER BY IsRemote DESC, Name;
            """;

        const string skillsSql = """
            SELECT SkillId AS Id, Name, Category AS Description
            FROM dbo.Skills
            WHERE TenantId = @TenantId
              AND Status = N'Active'
            ORDER BY Name;
            """;

        const string hiringManagersSql = """
            SELECT u.UserId AS Id, u.DisplayName AS Name, u.Email AS Description
            FROM dbo.AppUsers AS u
            INNER JOIN dbo.UserRoles AS ur
                ON ur.TenantId = u.TenantId
                AND ur.UserId = u.UserId
            INNER JOIN dbo.Roles AS r
                ON r.TenantId = ur.TenantId
                AND r.RoleId = ur.RoleId
                AND r.Code = N'HiringManager'
                AND r.Status = N'Active'
            WHERE u.TenantId = @TenantId
              AND u.AccountStatus = N'Active'
              AND u.DeletedAtUtc IS NULL
            ORDER BY u.DisplayName;
            """;

        var departments = await connection.QueryAsync<IntakeDepartmentOptionRow>(
            new CommandDefinition(departmentsSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var locations = await connection.QueryAsync<OperationsLookupOption>(
            new CommandDefinition(locationsSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var skills = await connection.QueryAsync<OperationsLookupOption>(
            new CommandDefinition(skillsSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var hiringManagers = await connection.QueryAsync<OperationsLookupOption>(
            new CommandDefinition(hiringManagersSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return new OperationsJobRequestIntakeOptions(
            departments
                .Select(row => new OperationsIntakeDepartmentOption(
                    row.DepartmentId,
                    row.Code,
                    row.Name,
                    new OperationsRoutingPreview(
                        row.AssignmentType,
                        row.TargetUserId,
                        row.TargetGroupId,
                        row.TargetName,
                        row.UsesTenantAdminFallback)))
                .ToArray(),
            locations.ToArray(),
            skills.ToArray(),
            hiringManagers.ToArray());
    }

    public async Task<IReadOnlySet<string>> GetActorRoleCodesAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        return await ReadActorRoleCodesAsync(connection, null, tenantId, actorUserId, cancellationToken);
    }

    public async Task<OperationsCreateJobRequestValidation> ValidateCreateJobRequestAsync(
        Guid tenantId,
        CreateOperationsJobRequestInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM dbo.Departments
                WHERE TenantId = @TenantId
                  AND DepartmentId = @DepartmentId
                  AND Status = N'Active'
            ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;

            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM dbo.Locations
                WHERE TenantId = @TenantId
                  AND LocationId = @LocationId
                  AND Status = N'Active'
            ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;

            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM dbo.AppUsers AS u
                INNER JOIN dbo.UserRoles AS ur
                    ON ur.TenantId = u.TenantId
                    AND ur.UserId = u.UserId
                INNER JOIN dbo.Roles AS r
                    ON r.TenantId = ur.TenantId
                    AND r.RoleId = ur.RoleId
                    AND r.Code = N'HiringManager'
                    AND r.Status = N'Active'
                WHERE u.TenantId = @TenantId
                  AND u.UserId = @HiringManagerId
                  AND u.AccountStatus = N'Active'
                  AND u.DeletedAtUtc IS NULL
            ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;

            SELECT SkillId
            FROM dbo.Skills
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND SkillId IN @SkillIds;
            """;

        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                input.DepartmentId,
                input.LocationId,
                input.HiringManagerId,
                SkillIds = (input.SkillIds ?? Array.Empty<Guid>()).Distinct().ToArray()
            },
            cancellationToken: cancellationToken));

        return new OperationsCreateJobRequestValidation(
            await grid.ReadSingleAsync<bool>(),
            await grid.ReadSingleAsync<bool>(),
            await grid.ReadSingleAsync<bool>(),
            (await grid.ReadAsync<Guid>()).ToArray());
    }

    public async Task<IReadOnlyList<OperationsActivityEvent>> GetActivityAsync(
        Guid tenantId,
        Guid userId,
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
              AND EXISTS
              (
                  SELECT 1
                  FROM dbo.JobRequests AS jr
                  WHERE jr.TenantId = @TenantId
                    AND jr.JobRequestId = @EntityId
                    AND (
                        EXISTS
                        (
                            SELECT 1
                            FROM dbo.UserRoles AS adminUr
                            INNER JOIN dbo.Roles AS adminRole
                                ON adminRole.TenantId = adminUr.TenantId
                                AND adminRole.RoleId = adminUr.RoleId
                                AND adminRole.Code = @TenantAdminRoleCode
                                AND adminRole.Status = N'Active'
                            WHERE adminUr.TenantId = jr.TenantId
                              AND adminUr.UserId = @UserId
                        )
                        OR jr.CreatedByUserId = @UserId
                        OR jr.HiringManagerUserId = @UserId
                        OR EXISTS
                        (
                            SELECT 1
                            FROM dbo.WorkflowAssignments AS wa
                            WHERE wa.TenantId = jr.TenantId
                              AND wa.EntityType = N'JobRequest'
                              AND wa.EntityId = jr.JobRequestId
                              AND (
                                  wa.AssignedToUserId = @UserId
                                  OR wa.ClaimedByUserId = @UserId
                                  OR EXISTS
                                  (
                                      SELECT 1
                                      FROM dbo.GroupMembers AS gm
                                      INNER JOIN dbo.Groups AS g
                                          ON g.TenantId = gm.TenantId
                                          AND g.GroupId = gm.GroupId
                                          AND g.Status = N'Active'
                                      WHERE gm.TenantId = wa.TenantId
                                        AND gm.GroupId = wa.AssignedToGroupId
                                        AND gm.UserId = @UserId
                                  )
                                  OR EXISTS
                                  (
                                      SELECT 1
                                      FROM dbo.UserRoles AS ur
                                      INNER JOIN dbo.Roles AS r
                                          ON r.TenantId = ur.TenantId
                                          AND r.RoleId = ur.RoleId
                                          AND r.Status = N'Active'
                                      WHERE ur.TenantId = wa.TenantId
                                        AND ur.UserId = @UserId
                                        AND ur.RoleId = wa.AssignedToRoleId
                                  )
                              )
                        )
                    )
              )
            ORDER BY OccurredAtUtc DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<OperationsActivityEventRow>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    UserId = userId,
                    EntityId = entityId,
                    TenantAdminRoleCode = AccessConstants.TenantAdminRoleCode
                },
                cancellationToken: cancellationToken));

        return rows
            .Select(row => new OperationsActivityEvent(
                row.Id,
                row.EntityId,
                row.ActorName,
                row.Title,
                row.Detail,
                Utc(row.CreatedAt)))
            .ToArray();
    }

    public async Task<OperationsPmoReview?> GetPmoReviewAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        bool includeEmployees,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, actorUserId, jobRequestId, cancellationToken);
        if (jobRequest is null)
        {
            return null;
        }

        var assignment = (await ListAssignmentsAsync(connection, tenantId, actorUserId, cancellationToken))
            .Where(item => item.EntityId == jobRequestId)
            .OrderBy(item => item.Status == "Completed" ? 1 : 0)
            .ThenByDescending(item => item.AssignedAt)
            .FirstOrDefault();
        var referrals = await ListEmployeeReferralsAsync(connection, tenantId, jobRequestId, cancellationToken);
        var employees = includeEmployees
            ? await ListEligibleBenchEmployeesAsync(connection, tenantId, jobRequestId, cancellationToken)
            : Array.Empty<OperationsBenchEmployee>();
        var benchMatches = includeEmployees
            ? await ListLatestBenchMatchesAsync(connection, tenantId, jobRequestId, cancellationToken)
            : Array.Empty<OperationsBenchMatch>();
        var presalesUsers = includeEmployees
            ? await ListPresalesUsersAsync(connection, tenantId, cancellationToken)
            : Array.Empty<OperationsLookupOption>();
        var defaultPresalesUserId = includeEmployees
            ? presalesUsers.FirstOrDefault(user => user.Id == jobRequest.CreatedById)?.Id ?? presalesUsers.FirstOrDefault()?.Id
            : null;
        var recruiterHandoffTargetName = includeEmployees
            ? await ReadRecruiterHandoffTargetNameAsync(connection, tenantId, cancellationToken)
            : string.Empty;

        return new OperationsPmoReview(
            jobRequest,
            assignment,
            referrals,
            employees,
            benchMatches,
            presalesUsers,
            defaultPresalesUserId,
            recruiterHandoffTargetName);
    }

    public async Task<OperationsRecruitmentQueue> GetRecruitmentQueueAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var actorRoleCodes = await ReadActorRoleCodesAsync(connection, null, tenantId, actorUserId, cancellationToken);
        var isTenantAdmin = actorRoleCodes.Contains(AccessConstants.TenantAdminRoleCode);

        var assignments = (await ListAssignmentsAsync(connection, tenantId, actorUserId, cancellationToken))
            .Where(item => item.EntityType == "JobRequest")
            .Where(item => item.Stage == "Recruiter Sourcing")
            .Where(item => item.Status is "Pending" or "Claimed")
            .Where(item => RecruitmentQueueVisibility.CanShowAssignment(item, actorUserId, isTenantAdmin))
            .OrderByDescending(item => item.AssignedAt)
            .ToArray();

        var items = new List<OperationsRecruitmentQueueItem>();
        foreach (var assignment in assignments)
        {
            var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, actorUserId, assignment.EntityId, cancellationToken);
            if (jobRequest is null)
            {
                continue;
            }

            var post = await ReadJobPostSummaryByRequestIdAsync(connection, tenantId, assignment.EntityId, cancellationToken);
            items.Add(new OperationsRecruitmentQueueItem(
                jobRequest,
                assignment,
                post?.JobPostId,
                post?.Status ?? "NotStarted",
                post?.RecruiterOwnerName,
                post is null ? null : Utc(post.UpdatedAt)));
        }

        return new OperationsRecruitmentQueue(items);
    }

    public async Task<OperationsRecruiterSourcing?> GetRecruiterSourcingAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, actorUserId, jobRequestId, cancellationToken);
        if (jobRequest is null)
        {
            return null;
        }

        var actorRoleCodes = await ReadActorRoleCodesAsync(connection, null, tenantId, actorUserId, cancellationToken);
        var isTenantAdmin = actorRoleCodes.Contains(AccessConstants.TenantAdminRoleCode);
        var assignments = (await ListAssignmentsAsync(connection, tenantId, actorUserId, cancellationToken))
            .Where(item => item.EntityId == jobRequestId)
            .Where(item => item.Stage == "Recruiter Sourcing")
            .ToArray();
        var assignment = assignments
            .Where(item => item.Status is "Pending" or "Claimed")
            .Where(item => RecruitmentQueueVisibility.CanShowAssignment(item, actorUserId, isTenantAdmin))
            .OrderByDescending(item => item.AssignedAt)
            .FirstOrDefault() ??
            assignments
                .Where(item => item.Status == "Completed")
                .Where(item => isTenantAdmin || item.AssignedToUserId == actorUserId || item.ClaimedByUserId == actorUserId)
                .OrderByDescending(item => item.AssignedAt)
                .FirstOrDefault();
        var jobPost = await ReadJobPostByRequestIdAsync(connection, tenantId, jobRequestId, cancellationToken);
        var canViewCompletedSourcing = assignment is not null ||
            isTenantAdmin ||
            jobPost?.RecruiterOwnerUserId == actorUserId;
        if (!canViewCompletedSourcing)
        {
            return null;
        }

        var applications = jobPost is null
            ? []
            : await ListRecruiterApplicationsAsync(connection, tenantId, jobPost.JobPostId, cancellationToken);
        var talentRediscoveryMatches = await ListLatestTalentRediscoveryMatchesAsync(connection, tenantId, jobRequestId, cancellationToken);
        var applicantRankings = jobPost is null
            ? []
            : await ListLatestApplicantRankingsAsync(connection, tenantId, jobPost.JobPostId, cancellationToken);
        var onlineHeadhunting = await GetLatestOnlineHeadhuntingResultAsync(connection, tenantId, jobRequestId, cancellationToken);
        var templates = await ListInterviewTemplatesAsync(connection, tenantId, jobRequestId, cancellationToken);
        var interviewers = await ListInterviewerOptionsAsync(connection, tenantId, jobRequestId, cancellationToken);
        var hodInterviewers = await ListDepartmentHodInterviewersAsync(connection, tenantId, jobRequestId, cancellationToken);
        var skills = await ListActiveSkillsAsync(connection, tenantId, cancellationToken);
        var requiredSkills = jobPost?.Skills.Select(skill => skill.Name).ToArray() ?? jobRequest.Skills;
        var manualCandidateSearchItems = (await ListRediscoveryCandidatesAsync(
                connection,
                tenantId,
                jobRequestId,
                jobPost?.JobPostId,
                requiredSkills,
                cancellationToken))
            .Select(ToManualCandidateSearchItem)
            .ToArray();

        return new OperationsRecruiterSourcing(jobRequest, assignment, jobPost, applications, manualCandidateSearchItems, talentRediscoveryMatches, applicantRankings, templates, interviewers, hodInterviewers, skills, onlineHeadhunting);
    }

    public async Task<OperationsHistoricalApplicationDetail?> GetHistoricalApplicationAsync(
        Guid tenantId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT TOP (1)
                candidate.CandidateId,
                candidate.DisplayName,
                candidate.Email,
                candidate.Status AS CandidateStatus,
                candidate.CurrentDesignation,
                candidate.CurrentCompany,
                candidate.ExperienceYears,
                candidate.NoticePeriodDays,
                application.JobApplicationId,
                application.JobRequestId,
                application.JobPostId,
                post.Title AS JobPostTitle,
                post.Status AS JobPostStatus,
                request.RequestCode,
                request.Title AS JobTitle,
                COALESCE(post.Title, request.Title) AS DisplayJobTitle,
                COALESCE(request.ClientName, N'Internal') AS Client,
                COALESCE(department.Name, N'Unassigned') AS Department,
                COALESCE(location.Name, N'Remote') AS Location,
                application.CurrentStatus AS Status,
                application.SourceLabel,
                application.AppliedAtUtc AS AppliedAt,
                application.FinalDecisionAtUtc AS FinalDecisionAt,
                application.FinalDecisionReason,
                latestOffer.StartDate AS OfferStartDate
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            LEFT JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = request.TenantId
                AND department.DepartmentId = request.DepartmentId
            LEFT JOIN dbo.Locations AS location
                ON location.TenantId = request.TenantId
                AND location.LocationId = request.LocationId
            OUTER APPLY
            (
                SELECT TOP (1) offer.StartDate
                FROM dbo.OfferLetters AS offer
                WHERE offer.TenantId = application.TenantId
                  AND offer.JobApplicationId = application.JobApplicationId
                ORDER BY offer.Version DESC, offer.UpdatedAtUtc DESC
            ) AS latestOffer
            WHERE application.TenantId = @TenantId
              AND application.JobApplicationId = @JobApplicationId;
            """;

        var row = await connection.QuerySingleOrDefaultAsync<HistoricalApplicationRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobApplicationId = jobApplicationId },
            cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        var interviews = await ListRediscoveryInterviewEvidenceAsync(
            connection,
            tenantId,
            [jobApplicationId],
            cancellationToken);
        var interviewEvidence = interviews.Select(ToInterviewEvidence).ToArray();
        var configuredInterviewRoundCount = row.JobPostId.HasValue
            ? await CountActiveJobPostInterviewRoundsAsync(connection, tenantId, row.JobPostId.Value, cancellationToken)
            : await CountJobRequestInterviewRoundsAsync(connection, tenantId, row.JobRequestId, cancellationToken);
        var summary = RediscoveryInterviewSummary.Build(interviewEvidence, configuredInterviewRoundCount);

        return new OperationsHistoricalApplicationDetail(
            new OperationsHistoricalCandidateSummary(
                row.CandidateId,
                row.DisplayName,
                row.Email,
                row.CandidateStatus,
                row.CurrentDesignation,
                row.CurrentCompany,
                row.ExperienceYears,
                row.NoticePeriodDays),
            new OperationsHistoricalApplicationSummary(
                row.JobApplicationId,
                row.JobRequestId,
                row.RequestCode,
                row.JobPostId,
                row.JobPostTitle,
                row.JobPostStatus,
                row.DisplayJobTitle,
                row.Client,
                row.Department,
                row.Location,
                row.Status,
                row.SourceLabel,
                Utc(row.AppliedAt),
                ToUtc(row.FinalDecisionAt),
                row.FinalDecisionReason,
                row.OfferStartDate.HasValue ? DateOnly.FromDateTime(row.OfferStartDate.Value) : null,
                summary.Passed,
                summary.Total,
                summary.DisplayText),
            interviews.Select(ToHistoricalInterviewDetail).ToArray());
    }

    public async Task<OperationsCandidateProfile?> GetCandidateProfileAsync(
        Guid tenantId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string candidateSql = """
            SELECT TOP (1)
                candidate.CandidateId,
                candidate.DisplayName,
                candidate.Email,
                candidate.Status AS CandidateStatus,
                candidate.CurrentDesignation,
                candidate.CurrentCompany,
                candidate.ExperienceYears,
                candidate.NoticePeriodDays
            FROM dbo.Candidates AS candidate
            WHERE candidate.TenantId = @TenantId
              AND candidate.CandidateId = @CandidateId;
            """;

        var candidate = await connection.QuerySingleOrDefaultAsync<HistoricalCandidateRow>(new CommandDefinition(
            candidateSql,
            new { TenantId = tenantId, CandidateId = candidateId },
            cancellationToken: cancellationToken));
        if (candidate is null)
        {
            return null;
        }

        const string skillsSql = """
            SELECT
                skill.SkillId,
                skill.Name AS SkillName,
                candidateSkill.SkillLevel,
                candidateSkill.YearsExperience,
                candidateSkill.IsPrimary
            FROM dbo.CandidateSkills AS candidateSkill
            INNER JOIN dbo.Skills AS skill
                ON skill.TenantId = candidateSkill.TenantId
                AND skill.SkillId = candidateSkill.SkillId
            WHERE candidateSkill.TenantId = @TenantId
              AND candidateSkill.CandidateId = @CandidateId
            ORDER BY candidateSkill.IsPrimary DESC, candidateSkill.YearsExperience DESC, skill.Name;
            """;

        var skills = (await connection.QueryAsync<OperationsCandidateProfileSkill>(new CommandDefinition(
            skillsSql,
            new { TenantId = tenantId, CandidateId = candidateId },
            cancellationToken: cancellationToken))).ToArray();

        var applications = await ListCandidateApplicationSummariesAsync(
            connection,
            tenantId,
            candidateId,
            cancellationToken);
        var meetingEvents = await ListCandidateMeetingEventsAsync(
            connection,
            tenantId,
            candidateId,
            cancellationToken);

        return new OperationsCandidateProfile(
            new OperationsHistoricalCandidateSummary(
                candidate.CandidateId,
                candidate.DisplayName,
                candidate.Email,
                candidate.CandidateStatus,
                candidate.CurrentDesignation,
                candidate.CurrentCompany,
                candidate.ExperienceYears,
                candidate.NoticePeriodDays),
            skills,
            applications,
            meetingEvents);
    }

    public async Task<OperationsJobPublishing> GetJobPublishingAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT
                post.JobPostId,
                post.JobRequestId,
                request.RequestCode,
                post.Title,
                COALESCE(request.ClientName, N'Internal') AS Client,
                COALESCE(department.Name, N'Unassigned') AS Department,
                COALESCE(location.Name, N'Remote') AS Location,
                post.Status,
                (
                    SELECT COUNT(*)
                    FROM dbo.JobApplications AS application
                    WHERE application.TenantId = post.TenantId
                      AND application.JobPostId = post.JobPostId
                ) AS ApplicantCount,
                recruiter.DisplayName AS RecruiterOwnerName,
                post.PublishedAtUtc AS PublishedAt,
                post.ClosedAtUtc AS ClosedAt,
                post.UpdatedAtUtc AS UpdatedAt
            FROM dbo.JobPosts AS post
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = post.TenantId
                AND request.JobRequestId = post.JobRequestId
            INNER JOIN dbo.AppUsers AS recruiter
                ON recruiter.TenantId = post.TenantId
                AND recruiter.UserId = post.RecruiterOwnerUserId
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = post.TenantId
                AND department.DepartmentId = post.DepartmentId
            LEFT JOIN dbo.Locations AS location
                ON location.TenantId = post.TenantId
                AND location.LocationId = post.LocationId
            WHERE post.TenantId = @TenantId
              AND
              (
                  post.RecruiterOwnerUserId = @ActorUserId
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.UserRoles AS adminUr
                      INNER JOIN dbo.Roles AS adminRole
                          ON adminRole.TenantId = adminUr.TenantId
                          AND adminRole.RoleId = adminUr.RoleId
                          AND adminRole.Code = @TenantAdminRoleCode
                          AND adminRole.Status = N'Active'
                      WHERE adminUr.TenantId = post.TenantId
                        AND adminUr.UserId = @ActorUserId
                  )
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.WorkflowAssignments AS assignment
                      WHERE assignment.TenantId = post.TenantId
                        AND assignment.EntityType = N'JobRequest'
                        AND assignment.EntityId = post.JobRequestId
                        AND assignment.AssignmentStatus IN (N'Pending', N'Claimed')
                        AND
                        (
                            assignment.AssignedToUserId = @ActorUserId
                            OR assignment.ClaimedByUserId = @ActorUserId
                            OR EXISTS
                            (
                                SELECT 1
                                FROM dbo.GroupMembers AS gm
                                INNER JOIN dbo.Groups AS g
                                    ON g.TenantId = gm.TenantId
                                    AND g.GroupId = gm.GroupId
                                    AND g.Status = N'Active'
                                WHERE gm.TenantId = assignment.TenantId
                                  AND gm.GroupId = assignment.AssignedToGroupId
                                  AND gm.UserId = @ActorUserId
                            )
                        )
                  )
              )
            ORDER BY post.UpdatedAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<JobPostListRow>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                TenantAdminRoleCode = AccessConstants.TenantAdminRoleCode
            },
            cancellationToken: cancellationToken));

        return new OperationsJobPublishing(rows
            .Select(row => new OperationsJobPostListItem(
                row.JobPostId,
                row.JobRequestId,
                row.RequestCode,
                row.Title,
                row.Client,
                row.Department,
                row.Location,
                row.Status,
                row.ApplicantCount,
                row.RecruiterOwnerName,
                ToUtc(row.PublishedAt),
                ToUtc(row.ClosedAt),
                Utc(row.UpdatedAt)))
            .ToArray());
    }

    public async Task<PortalJobPostList> ListPortalJobPostsAsync(string? tenantSlug, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT
                post.JobPostId,
                post.TenantId,
                post.JobRequestId,
                request.RequestCode,
                post.Title,
                post.Description,
                COALESCE(NULLIF(settings.CareerDisplayName, N''), tenant.DisplayName) AS CompanyName,
                COALESCE(request.ClientName, N'Internal') AS Client,
                COALESCE(department.Name, N'Unassigned') AS Department,
                COALESCE(location.Name, N'Remote') AS Location,
                post.ExperienceMinYears,
                post.ExperienceMaxYears,
                post.RequiredPositions,
                post.Status,
                post.PublishedAtUtc AS PublishedAt
            FROM dbo.JobPosts AS post
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = post.TenantId
                AND request.JobRequestId = post.JobRequestId
            INNER JOIN dbo.Tenants AS tenant
                ON tenant.TenantId = post.TenantId
            LEFT JOIN dbo.TenantRecruitmentSettings AS settings
                ON settings.TenantId = post.TenantId
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = post.TenantId
                AND department.DepartmentId = post.DepartmentId
            LEFT JOIN dbo.Locations AS location
                ON location.TenantId = post.TenantId
                AND location.LocationId = post.LocationId
            WHERE post.Status = N'Published'
              AND post.PublishedAtUtc IS NOT NULL
              AND (@TenantSlug IS NULL OR tenant.Slug = @TenantSlug)
              AND COALESCE(settings.PublicJobsEnabled, CAST(1 AS BIT)) = CAST(1 AS BIT)
            ORDER BY post.PublishedAtUtc DESC, post.UpdatedAtUtc DESC;
            """;

        var rows = (await connection.QueryAsync<PortalJobPostRow>(new CommandDefinition(
            sql,
            new { TenantSlug = string.IsNullOrWhiteSpace(tenantSlug) ? null : tenantSlug.Trim() },
            cancellationToken: cancellationToken))).ToArray();
        var items = new List<PortalJobPostListItem>(rows.Length);
        foreach (var row in rows)
        {
            var skills = await ListJobPostSkillsAsync(connection, row.TenantId, row.JobPostId, cancellationToken);
            items.Add(new PortalJobPostListItem(
                row.JobPostId,
                row.JobRequestId,
                row.RequestCode,
                row.Title,
                row.CompanyName,
                row.Client,
                row.Department,
                row.Location,
                row.ExperienceMinYears,
                row.ExperienceMaxYears,
                row.RequiredPositions,
                row.Status,
                Utc(row.PublishedAt!.Value),
                skills));
        }

        return new PortalJobPostList(items);
    }

    public async Task<PublicPortalContext?> GetPublicPortalContextAsync(
        PublicPortalContextQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string selectSql = """
            SELECT
                tenant.TenantId,
                tenant.Slug,
                tenant.DisplayName,
                COALESCE(NULLIF(settings.CareerDisplayName, N''), tenant.DisplayName) AS CareerDisplayName,
                settings.CompanyAddress,
                settings.CompanyCity,
                settings.CompanyCountry,
                settings.OfficialEmail,
                settings.OfficialPhone,
                COALESCE(NULLIF(settings.PrimaryColorHex, N''), N'#2563EB') AS PrimaryColor,
                COALESCE(settings.CandidateLoginRequired, CAST(1 AS BIT)) AS CandidateLoginRequired,
                COALESCE(NULLIF(settings.CandidateCvFormat, N''), N'DOCX') AS CandidateCvFormat,
                COALESCE(settings.PublicJobsEnabled, CAST(1 AS BIT)) AS PublicJobsEnabled,
                COALESCE(settings.InviteExpiryDays, 7) AS InviteExpiryDays,
                COALESCE(settings.ReapplyCooldownDays, 90) AS ReapplyCooldownDays,
                settings.LogoFileName,
                settings.LogoContentType,
                settings.LogoContent
            FROM dbo.Tenants AS tenant
            LEFT JOIN dbo.TenantRecruitmentSettings AS settings
                ON settings.TenantId = tenant.TenantId
            """;

        PublicPortalContextRow? row;
        if (query.JobPostId.HasValue)
        {
            row = await connection.QuerySingleOrDefaultAsync<PublicPortalContextRow>(new CommandDefinition(
                $"""
                {selectSql}
                INNER JOIN dbo.JobPosts AS post
                    ON post.TenantId = tenant.TenantId
                WHERE post.JobPostId = @JobPostId
                  AND tenant.Status = N'Active';
                """,
                new { JobPostId = query.JobPostId.Value },
                cancellationToken: cancellationToken));
        }
        else if (!string.IsNullOrWhiteSpace(query.TenantSlug))
        {
            row = await connection.QuerySingleOrDefaultAsync<PublicPortalContextRow>(new CommandDefinition(
                $"""
                {selectSql}
                WHERE tenant.Slug = @TenantSlug
                  AND tenant.Status = N'Active';
                """,
                new { TenantSlug = query.TenantSlug.Trim().ToLowerInvariant() },
                cancellationToken: cancellationToken));
        }
        else
        {
            var rows = (await connection.QueryAsync<PublicPortalContextRow>(new CommandDefinition(
                $"""
                {selectSql}
                WHERE tenant.Status = N'Active'
                  AND COALESCE(settings.PublicJobsEnabled, CAST(1 AS BIT)) = CAST(1 AS BIT);
                """,
                cancellationToken: cancellationToken))).ToArray();
            row = rows.Length == 1 ? rows[0] : null;
        }

        return row?.ToDomain();
    }

    public async Task<PortalJobPostDetail?> GetPortalJobPostAsync(Guid jobPostId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await ReadPublishedPortalJobPostAsync(connection, jobPostId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var skills = await ListJobPostSkillsAsync(connection, row.TenantId, row.JobPostId, cancellationToken);
        return new PortalJobPostDetail(
            row.JobPostId,
            row.JobRequestId,
            row.RequestCode,
            row.Title,
            row.Description,
            row.CompanyName,
            row.Client,
            row.Department,
            row.Location,
            row.ExperienceMinYears,
            row.ExperienceMaxYears,
            row.RequiredPositions,
            row.Status,
            Utc(row.PublishedAt!.Value),
            skills);
    }

    public async Task<PortalInvitationContext?> GetPortalInvitationAsync(
        Guid candidateInvitationId,
        string token,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT TOP (1)
                invitation.CandidateInvitationId,
                invitation.JobPostId,
                post.Title AS JobTitle,
                COALESCE(NULLIF(settings.CareerDisplayName, N''), tenant.DisplayName) AS CompanyName,
                invitation.Status,
                invitation.ExpiresAtUtc,
                invitation.UsedAtUtc,
                invitation.RevokedAtUtc
            FROM dbo.CandidateInvitations AS invitation
            INNER JOIN dbo.JobPosts AS post
                ON post.TenantId = invitation.TenantId
                AND post.JobPostId = invitation.JobPostId
            INNER JOIN dbo.Tenants AS tenant
                ON tenant.TenantId = invitation.TenantId
            LEFT JOIN dbo.TenantRecruitmentSettings AS settings
                ON settings.TenantId = invitation.TenantId
            WHERE invitation.CandidateInvitationId = @CandidateInvitationId
              AND invitation.TokenHash = @TokenHash
              AND invitation.JobPostId IS NOT NULL
              AND post.Status = N'Published'
              AND post.PublishedAtUtc IS NOT NULL
              AND COALESCE(settings.PublicJobsEnabled, CAST(1 AS BIT)) = CAST(1 AS BIT);
            """;

        var row = await connection.QuerySingleOrDefaultAsync<PortalInvitationRow>(new CommandDefinition(
            sql,
            new
            {
                CandidateInvitationId = candidateInvitationId,
                TokenHash = HashInvitationToken(token)
            },
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : new PortalInvitationContext(
                row.CandidateInvitationId,
                row.JobPostId,
                row.JobTitle,
                row.CompanyName,
                row.Status,
                Utc(row.ExpiresAtUtc),
                ToUtc(row.UsedAtUtc),
                row.ExpiresAtUtc <= DateTime.UtcNow,
                row.RevokedAtUtc.HasValue || string.Equals(row.Status, "Revoked", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PortalJobApplicationResult?> ApplyToPortalJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        PortalApplyToJobPostInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await ReadPublishedPortalJobPostAsync(connection, transaction, tenantId, jobPostId, cancellationToken);
        if (context is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var candidate = await EnsureCandidateForUserAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            input.Phone,
            input.LinkedInUrl,
            input.CurrentDesignation,
            input.CurrentCompany,
            input.ExperienceYears,
            input.NoticePeriodDays,
            cancellationToken);
        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await UpsertCandidateEducationAsync(
            connection,
            transaction,
            tenantId,
            candidate.CandidateId,
            input.UniversityName,
            input.DegreeName,
            input.GraduationYear,
            cancellationToken);

        await UpsertCandidateWorkHistoryAsync(
            connection,
            transaction,
            tenantId,
            candidate.CandidateId,
            input.CurrentCompany,
            input.CurrentDesignation,
            cancellationToken);

        var invitation = await ReadCandidateInvitationForApplicationAsync(
            connection,
            transaction,
            tenantId,
            jobPostId,
            input.CandidateInvitationId,
            input.InvitationToken,
            candidate.CandidateId,
            cancellationToken);
        if (input.CandidateInvitationId.HasValue && invitation is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var existingApplication = await ReadActiveJobPostApplicationAsync(
            connection,
            transaction,
            tenantId,
            jobPostId,
            candidate.CandidateId,
            cancellationToken);
        if (existingApplication is not null)
        {
            if (string.Equals(existingApplication.CurrentStatus, "Invited", StringComparison.OrdinalIgnoreCase))
            {
                await CompleteInvitedPortalApplicationAsync(
                    connection,
                    transaction,
                    tenantId,
                    existingApplication.JobApplicationId,
                    actorUserId,
                    context,
                    input,
                    cancellationToken);

                if (invitation is not null)
                {
                    await MarkCandidateInvitationUsedAsync(
                        connection,
                        transaction,
                        tenantId,
                        invitation.CandidateInvitationId,
                        cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return new PortalJobApplicationResult(
                    existingApplication.JobApplicationId,
                    jobPostId,
                    context.JobRequestId,
                    "Applied",
                    AlreadyApplied: false);
            }

            if (invitation is not null)
            {
                await MarkCandidateInvitationUsedAsync(
                    connection,
                    transaction,
                    tenantId,
                    invitation.CandidateInvitationId,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new PortalJobApplicationResult(
                existingApplication.JobApplicationId,
                jobPostId,
                context.JobRequestId,
                existingApplication.CurrentStatus,
                AlreadyApplied: true);
        }

        var source = await ReadCandidateSourceLabelAsync(connection, transaction, tenantId, "JobPortal", cancellationToken);
        var applicationId = await InsertJobApplicationAsync(
            connection,
            transaction,
            tenantId,
            context.JobRequestId,
            jobPostId,
            candidate.CandidateId,
            source?.CandidateSourceLabelId,
            source?.DisplayName ?? "Job Portal",
            "Applied",
            isInvited: invitation is not null,
            actorUserId: null,
            sourceDetail: invitation is null ? "Talent Pilot Portal" : "Talent Pilot Invitation",
            sourceUrl: null,
            recruiterNotes: null,
            snapshotJson: BuildApplicationSnapshotJson(
                context,
                input.InterviewAvailabilityStartDate,
                input.InterviewAvailabilityEndDate),
            coverLetterText: input.CoverLetter,
            cancellationToken);

        await InsertJobApplicationStatusHistoryAsync(
            connection,
            transaction,
            tenantId,
            applicationId,
            null,
            "Applied",
            actorUserId,
            invitation is null
                ? "Candidate applied from the Talent Pilot portal."
                : "Candidate applied from a tracked Talent Pilot invitation.",
            cancellationToken);

        if (invitation is not null)
        {
            await MarkCandidateInvitationUsedAsync(
                connection,
                transaction,
                tenantId,
                invitation.CandidateInvitationId,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new PortalJobApplicationResult(applicationId, jobPostId, context.JobRequestId, "Applied", AlreadyApplied: false);
    }

    public async Task<PortalApplicationDocumentUploadContext?> GetPortalApplicationDocumentUploadContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            SELECT TOP (1)
                application.JobApplicationId,
                application.CandidateId
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            WHERE application.TenantId = @TenantId
              AND application.JobApplicationId = @JobApplicationId
              AND candidate.AppUserId = @ActorUserId;
            """;

        return await connection.QuerySingleOrDefaultAsync<PortalApplicationDocumentUploadContext>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, ActorUserId = actorUserId, JobApplicationId = jobApplicationId },
                cancellationToken: cancellationToken));
    }

    public async Task<PortalApplicationDocument?> AddPortalApplicationDocumentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        PortalApplicationDocumentMetadataInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string accessSql = """
            SELECT TOP (1)
                application.CandidateId
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            WHERE application.TenantId = @TenantId
              AND application.JobApplicationId = @JobApplicationId
              AND candidate.AppUserId = @ActorUserId;
            """;
        var candidateId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            accessSql,
            new { TenantId = tenantId, ActorUserId = actorUserId, JobApplicationId = jobApplicationId },
            transaction,
            cancellationToken: cancellationToken));
        if (!candidateId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var now = DateTime.UtcNow;
        if (IsResumeDocumentType(input.DocumentType))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.JobApplicationDocuments
                SET Status = N'Inactive',
                    UpdatedAtUtc = @UpdatedAtUtc
                WHERE TenantId = @TenantId
                  AND JobApplicationId = @JobApplicationId
                  AND Status = N'Active'
                  AND LOWER(DocumentType) IN (N'resume', N'cv');
                """,
                new
                {
                    TenantId = tenantId,
                    JobApplicationId = jobApplicationId,
                    UpdatedAtUtc = now
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        var documentId = Guid.NewGuid();
        const string insertSql = """
            INSERT INTO dbo.JobApplicationDocuments (
                ApplicationDocumentId,
                TenantId,
                JobApplicationId,
                CandidateId,
                DocumentType,
                OriginalFileName,
                ContentType,
                SizeBytes,
                StorageProvider,
                StorageKey,
                StorageContainer,
                ContentHashSha256,
                ExtractionStatus,
                ExtractedText,
                ExtractedTextHashSha256,
                ParserVersion,
                ExtractedAtUtc,
                ExtractionError,
                Status,
                UploadedByUserId,
                UploadedAtUtc,
                CreatedAtUtc,
                UpdatedAtUtc)
            VALUES (
                @ApplicationDocumentId,
                @TenantId,
                @JobApplicationId,
                @CandidateId,
                @DocumentType,
                @OriginalFileName,
                @ContentType,
                @SizeBytes,
                @StorageProvider,
                @StorageKey,
                @StorageContainer,
                @ContentHashSha256,
                @ExtractionStatus,
                @ExtractedText,
                @ExtractedTextHashSha256,
                @ParserVersion,
                @ExtractedAtUtc,
                @ExtractionError,
                N'Active',
                @UploadedByUserId,
                @UploadedAtUtc,
                @CreatedAtUtc,
                @UpdatedAtUtc);
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            new
            {
                ApplicationDocumentId = documentId,
                TenantId = tenantId,
                JobApplicationId = jobApplicationId,
                CandidateId = candidateId.Value,
                DocumentType = input.DocumentType,
                OriginalFileName = input.FileName,
                input.ContentType,
                input.SizeBytes,
                input.StorageProvider,
                input.StorageKey,
                input.StorageContainer,
                input.ContentHashSha256,
                input.ExtractionStatus,
                input.ExtractedText,
                input.ExtractedTextHashSha256,
                input.ParserVersion,
                ExtractedAtUtc = input.ExtractedAt?.UtcDateTime,
                input.ExtractionError,
                UploadedByUserId = actorUserId,
                UploadedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);

        return new PortalApplicationDocument(
            documentId,
            jobApplicationId,
            input.DocumentType,
            input.FileName,
            input.ContentType,
            input.SizeBytes,
            input.StorageProvider,
            Utc(now),
            input.ExtractionStatus,
            !string.IsNullOrWhiteSpace(input.ExtractedText),
            input.ParserVersion,
            input.ExtractedAt,
            input.ExtractionError);
    }

    public async Task<OperationsApplicantDocumentEvidence?> CopyLatestProfileDocumentToApplicationAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var candidateId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            """
            SELECT TOP (1)
                application.CandidateId
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            WHERE application.TenantId = @TenantId
              AND application.JobApplicationId = @JobApplicationId
              AND candidate.AppUserId = @ActorUserId;
            """,
            new { TenantId = tenantId, ActorUserId = actorUserId, JobApplicationId = jobApplicationId },
            transaction,
            cancellationToken: cancellationToken));
        if (!candidateId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var applicationHasResume = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM dbo.JobApplicationDocuments
            WHERE TenantId = @TenantId
              AND JobApplicationId = @JobApplicationId
              AND Status = N'Active'
              AND LOWER(DocumentType) IN (N'resume', N'cv');
            """,
            new { TenantId = tenantId, JobApplicationId = jobApplicationId },
            transaction,
            cancellationToken: cancellationToken));
        if (applicationHasResume > 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var profileDocument = await ReadLatestPortalCandidateProfileDocumentAsync(
            connection,
            transaction,
            tenantId,
            candidateId.Value,
            cancellationToken);
        if (profileDocument is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var now = DateTime.UtcNow;
        var applicationDocumentId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.JobApplicationDocuments (
                ApplicationDocumentId,
                TenantId,
                JobApplicationId,
                CandidateId,
                DocumentType,
                OriginalFileName,
                ContentType,
                SizeBytes,
                StorageProvider,
                StorageKey,
                StorageContainer,
                ContentHashSha256,
                ExtractionStatus,
                ExtractedText,
                ExtractedTextHashSha256,
                ParserVersion,
                ExtractedAtUtc,
                ExtractionError,
                Status,
                UploadedByUserId,
                UploadedAtUtc,
                CreatedAtUtc,
                UpdatedAtUtc)
            VALUES (
                @ApplicationDocumentId,
                @TenantId,
                @JobApplicationId,
                @CandidateId,
                N'Resume',
                @OriginalFileName,
                @ContentType,
                @SizeBytes,
                @StorageProvider,
                @StorageKey,
                @StorageContainer,
                @ContentHashSha256,
                @ExtractionStatus,
                @ExtractedText,
                @ExtractedTextHashSha256,
                @ParserVersion,
                @ExtractedAtUtc,
                @ExtractionError,
                N'Active',
                @UploadedByUserId,
                @UploadedAtUtc,
                @CreatedAtUtc,
                @UpdatedAtUtc);
            """,
            new
            {
                ApplicationDocumentId = applicationDocumentId,
                TenantId = tenantId,
                JobApplicationId = jobApplicationId,
                CandidateId = candidateId.Value,
                OriginalFileName = profileDocument.FileName,
                profileDocument.ContentType,
                profileDocument.SizeBytes,
                profileDocument.StorageProvider,
                profileDocument.StorageKey,
                profileDocument.StorageContainer,
                profileDocument.ContentHashSha256,
                profileDocument.ExtractionStatus,
                profileDocument.ExtractedText,
                profileDocument.ExtractedTextHashSha256,
                profileDocument.ParserVersion,
                ExtractedAtUtc = profileDocument.ExtractedAt,
                profileDocument.ExtractionError,
                UploadedByUserId = actorUserId,
                UploadedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        return new OperationsApplicantDocumentEvidence(
            applicationDocumentId,
            "Resume",
            profileDocument.FileName,
            profileDocument.ContentType,
            profileDocument.SizeBytes,
            profileDocument.StorageProvider,
            profileDocument.StorageKey,
            profileDocument.StorageContainer,
            profileDocument.ContentHashSha256,
            Utc(now),
            profileDocument.ExtractionStatus,
            profileDocument.HasExtractedText,
            profileDocument.ExtractedText,
            profileDocument.ExtractedTextHashSha256,
            profileDocument.ParserVersion,
            profileDocument.ExtractedAt,
            profileDocument.ExtractionError);
    }

    public async Task<IReadOnlyList<PortalApplicationDocument>> ListPortalApplicationDocumentsAsync(
        Guid tenantId,
        IReadOnlyList<Guid> jobApplicationIds,
        CancellationToken cancellationToken)
    {
        if (jobApplicationIds.Count == 0)
        {
            return [];
        }

        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            SELECT
                ApplicationDocumentId,
                JobApplicationId,
                DocumentType,
                OriginalFileName AS FileName,
                ContentType,
                SizeBytes,
                StorageProvider,
                StorageKey,
                StorageContainer,
                ContentHashSha256,
                UploadedAtUtc AS UploadedAt,
                ExtractionStatus,
                CAST(CASE WHEN NULLIF(LTRIM(RTRIM(ExtractedText)), N'') IS NULL THEN 0 ELSE 1 END AS bit) AS HasExtractedText,
                ExtractedText,
                ExtractedTextHashSha256,
                ParserVersion,
                ExtractedAtUtc AS ExtractedAt,
                ExtractionError
            FROM dbo.JobApplicationDocuments
            WHERE TenantId = @TenantId
              AND JobApplicationId IN @JobApplicationIds
              AND Status = N'Active'
            ORDER BY UploadedAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<ApplicationDocumentEvidenceRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobApplicationIds = jobApplicationIds },
            cancellationToken: cancellationToken));

        return rows
            .Select(row => new PortalApplicationDocument(
                row.ApplicationDocumentId,
                row.JobApplicationId,
                row.DocumentType,
                row.FileName,
                row.ContentType,
                row.SizeBytes,
                row.StorageProvider,
                Utc(row.UploadedAt),
                row.ExtractionStatus,
                row.HasExtractedText,
                row.ParserVersion,
                ToUtc(row.ExtractedAt),
                row.ExtractionError))
            .ToArray();
    }

    public async Task<PortalMyApplications> GetPortalMyApplicationsAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var candidateId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            """
            SELECT TOP (1) CandidateId
            FROM dbo.Candidates
            WHERE TenantId = @TenantId
              AND AppUserId = @ActorUserId;
            """,
            new { TenantId = tenantId, ActorUserId = actorUserId },
            cancellationToken: cancellationToken));
        if (!candidateId.HasValue)
        {
            return new PortalMyApplications([]);
        }

        var applications = await ListCandidateApplicationSummariesAsync(
            connection,
            tenantId,
            candidateId.Value,
            cancellationToken);

        const string companySql = """
            SELECT COALESCE(NULLIF(settings.CareerDisplayName, N''), tenant.DisplayName)
            FROM dbo.Tenants AS tenant
            LEFT JOIN dbo.TenantRecruitmentSettings AS settings
                ON settings.TenantId = tenant.TenantId
            WHERE tenant.TenantId = @TenantId;
            """;
        var companyName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            companySql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken)) ?? "Talent Pilot";

        var portalApplications = applications
            .Where(application => application.JobPostId.HasValue)
            .ToArray();
        var portalApplicationIds = portalApplications
            .Select(application => application.JobApplicationId)
            .Distinct()
            .ToArray();
        var interviewTimelineRows = await ListRediscoveryInterviewEvidenceAsync(
            connection,
            tenantId,
            portalApplicationIds,
            cancellationToken);
        var interviewsByApplication = interviewTimelineRows
            .GroupBy(interview => interview.JobApplicationId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var offerMeetingRows = await ListPortalApplicationOfferMeetingsAsync(
            connection,
            tenantId,
            portalApplicationIds,
            cancellationToken);
        var offerMeetingsByApplication = offerMeetingRows
            .GroupBy(meeting => meeting.JobApplicationId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var documentRows = await ListPortalApplicationDocumentsAsync(
            tenantId,
            portalApplicationIds,
            cancellationToken);
        var documentsByApplication = documentRows
            .GroupBy(document => document.JobApplicationId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return new PortalMyApplications(portalApplications
            .Select(application => new PortalMyApplicationItem(
                application.JobApplicationId,
                application.JobPostId!.Value,
                application.JobRequestId,
                application.RequestCode,
                application.DisplayJobTitle,
                companyName,
                application.Client,
                application.Department,
                application.Location,
                application.Status,
                application.SourceLabel,
                application.AppliedAt,
                application.FinalDecisionAt,
                application.FinalDecisionReason,
                application.OfferStartDate,
                application.InterviewsPassed,
                application.InterviewsTotal,
                application.InterviewPassSummary,
                BuildPortalApplicationTimeline(
                    application,
                    interviewsByApplication.TryGetValue(application.JobApplicationId, out var applicationInterviews)
                        ? applicationInterviews
                        : [],
                    offerMeetingsByApplication.TryGetValue(application.JobApplicationId, out var applicationOfferMeetings)
                        ? applicationOfferMeetings
                        : []),
                documentsByApplication.TryGetValue(application.JobApplicationId, out var applicationDocuments)
                    ? applicationDocuments
                    : []))
            .ToArray());
    }

    public async Task<PortalCandidateProfile?> GetPortalCandidateProfileAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        return await ReadPortalCandidateProfileAsync(connection, null, tenantId, actorUserId, cancellationToken);
    }

    public async Task<PortalCandidateProfile?> UpdatePortalCandidateProfileAsync(
        Guid tenantId,
        Guid actorUserId,
        UpdatePortalCandidateProfileInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var candidateId = await UpsertPortalCandidateProfileAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            input,
            cancellationToken);
        if (!candidateId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await ReplacePortalCandidateSkillsAsync(
            connection,
            transaction,
            tenantId,
            candidateId.Value,
            input.Skills ?? [],
            cancellationToken);
        await ReplacePortalPrimaryEducationAsync(
            connection,
            transaction,
            tenantId,
            candidateId.Value,
            input.PrimaryEducation,
            cancellationToken);
        await ReplacePortalCurrentWorkHistoryAsync(
            connection,
            transaction,
            tenantId,
            candidateId.Value,
            input.CurrentWorkHistory,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await ReadPortalCandidateProfileAsync(connection, null, tenantId, actorUserId, cancellationToken);
    }

    public async Task<PortalCandidateProfileDocumentUploadContext?> GetPortalCandidateProfileDocumentUploadContextAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var candidate = await EnsurePortalCandidateIdentityAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            cancellationToken);
        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return new PortalCandidateProfileDocumentUploadContext(candidate.CandidateId);
    }

    public async Task<PortalCandidateProfileDocument?> AddPortalCandidateProfileDocumentAsync(
        Guid tenantId,
        Guid actorUserId,
        PortalCandidateProfileDocumentMetadataInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var candidate = await EnsurePortalCandidateIdentityAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            cancellationToken);
        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var now = DateTime.UtcNow;
        if (IsResumeDocumentType(input.DocumentType))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.CandidateProfileDocuments
                SET Status = N'Inactive',
                    UpdatedAtUtc = @UpdatedAtUtc
                WHERE TenantId = @TenantId
                  AND CandidateId = @CandidateId
                  AND Status = N'Active'
                  AND LOWER(DocumentType) IN (N'resume', N'cv');
                """,
                new
                {
                    TenantId = tenantId,
                    candidate.CandidateId,
                    UpdatedAtUtc = now
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        var documentId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.CandidateProfileDocuments (
                CandidateProfileDocumentId,
                TenantId,
                CandidateId,
                DocumentType,
                OriginalFileName,
                ContentType,
                SizeBytes,
                StorageProvider,
                StorageKey,
                StorageContainer,
                ContentHashSha256,
                ExtractionStatus,
                ExtractedText,
                ExtractedTextHashSha256,
                ParserVersion,
                ExtractedAtUtc,
                ExtractionError,
                Status,
                UploadedByUserId,
                UploadedAtUtc,
                CreatedAtUtc,
                UpdatedAtUtc)
            VALUES (
                @CandidateProfileDocumentId,
                @TenantId,
                @CandidateId,
                @DocumentType,
                @OriginalFileName,
                @ContentType,
                @SizeBytes,
                @StorageProvider,
                @StorageKey,
                @StorageContainer,
                @ContentHashSha256,
                @ExtractionStatus,
                @ExtractedText,
                @ExtractedTextHashSha256,
                @ParserVersion,
                @ExtractedAtUtc,
                @ExtractionError,
                N'Active',
                @UploadedByUserId,
                @UploadedAtUtc,
                @CreatedAtUtc,
                @UpdatedAtUtc);
            """,
            new
            {
                CandidateProfileDocumentId = documentId,
                TenantId = tenantId,
                candidate.CandidateId,
                input.DocumentType,
                OriginalFileName = input.FileName,
                input.ContentType,
                input.SizeBytes,
                input.StorageProvider,
                input.StorageKey,
                input.StorageContainer,
                input.ContentHashSha256,
                input.ExtractionStatus,
                input.ExtractedText,
                input.ExtractedTextHashSha256,
                input.ParserVersion,
                ExtractedAtUtc = input.ExtractedAt?.UtcDateTime,
                input.ExtractionError,
                UploadedByUserId = actorUserId,
                UploadedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);

        return new PortalCandidateProfileDocument(
            documentId,
            candidate.CandidateId,
            input.DocumentType,
            input.FileName,
            input.ContentType,
            input.SizeBytes,
            input.StorageProvider,
            Utc(now),
            input.ExtractionStatus,
            !string.IsNullOrWhiteSpace(input.ExtractedText),
            input.ParserVersion,
            input.ExtractedAt,
            input.ExtractionError);
    }

    public async Task<PortalCandidateProfileDocumentEvidence?> GetPortalCandidateProfileDocumentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid candidateProfileDocumentId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            SELECT TOP (1)
                document.CandidateProfileDocumentId,
                document.CandidateId,
                document.DocumentType,
                document.OriginalFileName AS FileName,
                document.ContentType,
                document.SizeBytes,
                document.StorageProvider,
                document.StorageKey,
                document.StorageContainer,
                document.ContentHashSha256,
                document.UploadedAtUtc AS UploadedAt,
                document.ExtractionStatus,
                CAST(CASE WHEN NULLIF(LTRIM(RTRIM(document.ExtractedText)), N'') IS NULL THEN 0 ELSE 1 END AS bit) AS HasExtractedText,
                document.ExtractedText,
                document.ExtractedTextHashSha256,
                document.ParserVersion,
                document.ExtractedAtUtc AS ExtractedAt,
                document.ExtractionError
            FROM dbo.CandidateProfileDocuments AS document
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = document.TenantId
                AND candidate.CandidateId = document.CandidateId
            WHERE document.TenantId = @TenantId
              AND document.CandidateProfileDocumentId = @CandidateProfileDocumentId
              AND document.Status = N'Active'
              AND candidate.AppUserId = @ActorUserId;
            """;

        var row = await connection.QuerySingleOrDefaultAsync<PortalCandidateProfileDocumentRow>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                CandidateProfileDocumentId = candidateProfileDocumentId
            },
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : new PortalCandidateProfileDocumentEvidence(
                row.CandidateProfileDocumentId,
                row.CandidateId,
                row.DocumentType,
                row.FileName,
                row.ContentType,
                row.SizeBytes,
                row.StorageProvider,
                row.StorageKey,
                row.StorageContainer,
                row.ContentHashSha256,
                Utc(row.UploadedAt),
                row.ExtractionStatus,
                row.HasExtractedText,
                row.ExtractedText,
                row.ExtractedTextHashSha256,
                row.ParserVersion,
                ToUtc(row.ExtractedAt),
                row.ExtractionError);
    }

    public async Task<OperationsBenchMatchingContext?> GetBenchMatchingContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        var assignmentId = await ReadCurrentAssignmentForActionAsync(
            connection,
            null!,
            tenantId,
            actorUserId,
            jobRequestId,
            "PMO_REVIEW",
            requireClaimedForGroups: true,
            cancellationToken);
        if (!assignmentId.HasValue)
        {
            return null;
        }

        var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, actorUserId, jobRequestId, cancellationToken);
        if (jobRequest is null)
        {
            return null;
        }

        const string experienceSql = """
            SELECT ExperienceMinYears, ExperienceMaxYears
            FROM dbo.JobRequests
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;
        var experience = await connection.QuerySingleOrDefaultAsync<JobRequestExperienceRow>(
            new CommandDefinition(
                experienceSql,
                new { TenantId = tenantId, JobRequestId = jobRequestId },
                cancellationToken: cancellationToken));

        var employees = await ListEligibleBenchEmployeesAsync(connection, tenantId, jobRequestId, cancellationToken);
        return new OperationsBenchMatchingContext(
            jobRequest,
            experience?.ExperienceMinYears,
            experience?.ExperienceMaxYears,
            employees);
    }

    public async Task SaveBenchMatchesAsync(
        Guid tenantId,
        Guid jobRequestId,
        Guid agentRunId,
        IReadOnlyList<OperationsBenchMatch> matches,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string deleteMissingSql = """
            DELETE FROM dbo.AiRecommendationLogs
            WHERE TenantId = @TenantId
              AND AiAgentDefinitionId = N'bench-matching'
              AND SourceEntityType = N'JobRequest'
              AND SourceEntityId = @JobRequestId
              AND RecommendedEntityType = N'Employee'
              AND RecommendedEntityId NOT IN @EmployeeIds;
            """;
        var employeeIds = matches.Select(match => match.EmployeeId).ToArray();
        if (employeeIds.Length > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                deleteMissingSql,
                new { TenantId = tenantId, JobRequestId = jobRequestId, EmployeeIds = employeeIds },
                transaction,
                cancellationToken: cancellationToken));
        }

        const string mergeSql = """
            MERGE dbo.AiRecommendationLogs AS target
            USING
            (
                SELECT
                    @TenantId AS TenantId,
                    N'bench-matching' AS AiAgentDefinitionId,
                    N'JobRequest' AS SourceEntityType,
                    @JobRequestId AS SourceEntityId,
                    N'Employee' AS RecommendedEntityType,
                    @EmployeeId AS RecommendedEntityId
            ) AS source
            ON target.TenantId = source.TenantId
               AND target.AiAgentDefinitionId = source.AiAgentDefinitionId
               AND target.SourceEntityType = source.SourceEntityType
               AND target.SourceEntityId = source.SourceEntityId
               AND target.RecommendedEntityType = source.RecommendedEntityType
               AND target.RecommendedEntityId = source.RecommendedEntityId
            WHEN MATCHED THEN UPDATE SET
                AiAgentRunId = @AgentRunId,
                Score = @Score,
                Explanation = @Explanation,
                PayloadJson = @PayloadJson,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT
                (
                    AiRecommendationLogId,
                    TenantId,
                    AiAgentDefinitionId,
                    SourceEntityType,
                    SourceEntityId,
                    RecommendedEntityType,
                    RecommendedEntityId,
                    AiAgentRunId,
                    Score,
                    Explanation,
                    PayloadJson,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    NEWID(),
                    @TenantId,
                    N'bench-matching',
                    N'JobRequest',
                    @JobRequestId,
                    N'Employee',
                    @EmployeeId,
                    @AgentRunId,
                    @Score,
                    @Explanation,
                    @PayloadJson,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            """;

        foreach (var match in matches)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                mergeSql,
                new
                {
                    TenantId = tenantId,
                    JobRequestId = jobRequestId,
                    EmployeeId = match.EmployeeId,
                    AgentRunId = agentRunId,
                    match.Score,
                    match.Explanation,
                    PayloadJson = JsonSerializer.Serialize(ToBenchMatchPayload(match))
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<OperationsTalentRediscoveryContext?> GetTalentRediscoveryContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        if (!await CanMutateRecruiterSourcingAsync(connection, null, tenantId, actorUserId, jobRequestId, cancellationToken))
        {
            return null;
        }

        var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, actorUserId, jobRequestId, cancellationToken);
        if (jobRequest is null)
        {
            return null;
        }

        var jobPost = await ReadJobPostByRequestIdAsync(connection, tenantId, jobRequestId, cancellationToken);
        var requirementSource = jobPost is null ? "JobRequest" : "JobPost";
        var requiredSkills = jobPost?.Skills.Select(skill => skill.Name).ToArray() ?? jobRequest.Skills;
        var experience = jobPost is null
            ? await ReadJobRequestExperienceAsync(connection, tenantId, jobRequestId, cancellationToken)
            : new JobRequestExperienceRow(jobPost.ExperienceMinYears, jobPost.ExperienceMaxYears);
        var candidates = await ListRediscoveryCandidatesAsync(
            connection,
            tenantId,
            jobRequestId,
            jobPost?.JobPostId,
            requiredSkills,
            cancellationToken);

        return new OperationsTalentRediscoveryContext(
            jobRequest,
            jobPost,
            requirementSource,
            requiredSkills,
            experience?.ExperienceMinYears,
            experience?.ExperienceMaxYears,
            candidates);
    }

    public async Task SaveTalentRediscoveryMatchesAsync(
        Guid tenantId,
        Guid jobRequestId,
        Guid agentRunId,
        IReadOnlyList<OperationsTalentRediscoveryMatch> matches,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var candidateIds = matches.Select(match => match.CandidateId).ToArray();
        if (candidateIds.Length > 0)
        {
            const string deleteMissingSql = """
                DELETE FROM dbo.AiRecommendationLogs
                WHERE TenantId = @TenantId
                  AND AiAgentDefinitionId = N'talent-rediscovery'
                  AND SourceEntityType = N'JobRequest'
                  AND SourceEntityId = @JobRequestId
                  AND RecommendedEntityType = N'Candidate'
                  AND RecommendedEntityId NOT IN @CandidateIds;
                """;
            await connection.ExecuteAsync(new CommandDefinition(
                deleteMissingSql,
                new { TenantId = tenantId, JobRequestId = jobRequestId, CandidateIds = candidateIds },
                transaction,
                cancellationToken: cancellationToken));
        }
        else
        {
            const string deleteAllSql = """
                DELETE FROM dbo.AiRecommendationLogs
                WHERE TenantId = @TenantId
                  AND AiAgentDefinitionId = N'talent-rediscovery'
                  AND SourceEntityType = N'JobRequest'
                  AND SourceEntityId = @JobRequestId
                  AND RecommendedEntityType = N'Candidate';
                """;
            await connection.ExecuteAsync(new CommandDefinition(
                deleteAllSql,
                new { TenantId = tenantId, JobRequestId = jobRequestId },
                transaction,
                cancellationToken: cancellationToken));
        }

        const string mergeSql = """
            MERGE dbo.AiRecommendationLogs AS target
            USING
            (
                SELECT
                    @TenantId AS TenantId,
                    N'talent-rediscovery' AS AiAgentDefinitionId,
                    N'JobRequest' AS SourceEntityType,
                    @JobRequestId AS SourceEntityId,
                    N'Candidate' AS RecommendedEntityType,
                    @CandidateId AS RecommendedEntityId
            ) AS source
            ON target.TenantId = source.TenantId
               AND target.AiAgentDefinitionId = source.AiAgentDefinitionId
               AND target.SourceEntityType = source.SourceEntityType
               AND target.SourceEntityId = source.SourceEntityId
               AND target.RecommendedEntityType = source.RecommendedEntityType
               AND target.RecommendedEntityId = source.RecommendedEntityId
            WHEN MATCHED THEN UPDATE SET
                AiAgentRunId = @AgentRunId,
                Score = @Score,
                Explanation = @Explanation,
                PayloadJson = @PayloadJson,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT
                (
                    AiRecommendationLogId,
                    TenantId,
                    AiAgentDefinitionId,
                    SourceEntityType,
                    SourceEntityId,
                    RecommendedEntityType,
                    RecommendedEntityId,
                    AiAgentRunId,
                    Score,
                    Explanation,
                    PayloadJson,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    NEWID(),
                    @TenantId,
                    N'talent-rediscovery',
                    N'JobRequest',
                    @JobRequestId,
                    N'Candidate',
                    @CandidateId,
                    @AgentRunId,
                    @Score,
                    @Explanation,
                    @PayloadJson,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            """;

        foreach (var match in matches)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                mergeSql,
                new
                {
                    TenantId = tenantId,
                    JobRequestId = jobRequestId,
                    CandidateId = match.CandidateId,
                    AgentRunId = agentRunId,
                    match.Score,
                    match.Explanation,
                    PayloadJson = JsonSerializer.Serialize(ToTalentRediscoveryPayload(match))
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<OperationsApplicantRankingContext?> GetApplicantRankingContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        var jobPost = await ReadJobPostByIdAsync(connection, tenantId, jobPostId, cancellationToken);
        if (jobPost is null)
        {
            return null;
        }

        if (!await CanMutateRecruiterSourcingAsync(connection, null, tenantId, actorUserId, jobPost.JobRequestId, cancellationToken))
        {
            return null;
        }

        var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, actorUserId, jobPost.JobRequestId, cancellationToken);
        if (jobRequest is null)
        {
            return null;
        }

        var requiredSkills = jobPost.Skills
            .Select(skill => skill.Name)
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var applications = await ListApplicantRankingApplicationsAsync(
            connection,
            tenantId,
            jobPost.JobRequestId,
            jobPost.JobPostId,
            requiredSkills,
            cancellationToken);

        return new OperationsApplicantRankingContext(
            jobRequest,
            jobPost,
            requiredSkills,
            jobPost.ExperienceMinYears,
            jobPost.ExperienceMaxYears,
            applications);
    }

    public async Task SaveApplicantRankingsAsync(
        Guid tenantId,
        Guid jobPostId,
        Guid agentRunId,
        IReadOnlyList<OperationsApplicantRankingMatch> matches,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var applicationIds = matches.Select(match => match.JobApplicationId).ToArray();
        if (applicationIds.Length > 0)
        {
            const string deleteMissingSql = """
                DELETE FROM dbo.AiRecommendationLogs
                WHERE TenantId = @TenantId
                  AND AiAgentDefinitionId = N'applicant-ranking'
                  AND SourceEntityType = N'JobPost'
                  AND SourceEntityId = @JobPostId
                  AND RecommendedEntityType = N'JobApplication'
                  AND RecommendedEntityId NOT IN @ApplicationIds;
                """;
            await connection.ExecuteAsync(new CommandDefinition(
                deleteMissingSql,
                new { TenantId = tenantId, JobPostId = jobPostId, ApplicationIds = applicationIds },
                transaction,
                cancellationToken: cancellationToken));
        }

        const string mergeSql = """
            MERGE dbo.AiRecommendationLogs AS target
            USING
            (
                SELECT
                    @TenantId AS TenantId,
                    N'applicant-ranking' AS AiAgentDefinitionId,
                    N'JobPost' AS SourceEntityType,
                    @JobPostId AS SourceEntityId,
                    N'JobApplication' AS RecommendedEntityType,
                    @JobApplicationId AS RecommendedEntityId
            ) AS source
            ON target.TenantId = source.TenantId
               AND target.AiAgentDefinitionId = source.AiAgentDefinitionId
               AND target.SourceEntityType = source.SourceEntityType
               AND target.SourceEntityId = source.SourceEntityId
               AND target.RecommendedEntityType = source.RecommendedEntityType
               AND target.RecommendedEntityId = source.RecommendedEntityId
            WHEN MATCHED THEN UPDATE SET
                AiAgentRunId = @AgentRunId,
                Score = @Score,
                Explanation = @Explanation,
                PayloadJson = @PayloadJson,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT
                (
                    AiRecommendationLogId,
                    TenantId,
                    AiAgentDefinitionId,
                    SourceEntityType,
                    SourceEntityId,
                    RecommendedEntityType,
                    RecommendedEntityId,
                    AiAgentRunId,
                    Score,
                    Explanation,
                    PayloadJson,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    NEWID(),
                    @TenantId,
                    N'applicant-ranking',
                    N'JobPost',
                    @JobPostId,
                    N'JobApplication',
                    @JobApplicationId,
                    @AgentRunId,
                    @Score,
                    @Explanation,
                    @PayloadJson,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            """;

        foreach (var match in matches)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                mergeSql,
                new
                {
                    TenantId = tenantId,
                    JobPostId = jobPostId,
                    match.JobApplicationId,
                    AgentRunId = agentRunId,
                    match.Score,
                    match.Explanation,
                    PayloadJson = JsonSerializer.Serialize(ToApplicantRankingPayload(match))
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<OperationsOnlineHeadhuntingContext?> GetOnlineHeadhuntingContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        if (!await CanMutateRecruiterSourcingAsync(connection, null, tenantId, actorUserId, jobRequestId, cancellationToken))
        {
            return null;
        }

        var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, actorUserId, jobRequestId, cancellationToken);
        if (jobRequest is null)
        {
            return null;
        }

        var jobPost = await ReadJobPostByRequestIdAsync(connection, tenantId, jobRequestId, cancellationToken);
        var requiredSkills = jobPost?.Skills.Select(skill => skill.Name).ToArray() ?? jobRequest.Skills;
        var experience = jobPost is null
            ? await ReadJobRequestExperienceAsync(connection, tenantId, jobRequestId, cancellationToken)
            : new JobRequestExperienceRow(jobPost.ExperienceMinYears, jobPost.ExperienceMaxYears);
        var candidatePool = await ListOnlineHeadhuntingDuplicateCandidatesAsync(connection, tenantId, cancellationToken);
        var existingLeads = await ListOnlineHeadhuntingExistingLeadsAsync(connection, tenantId, jobRequestId, cancellationToken);

        return new OperationsOnlineHeadhuntingContext(
            jobRequest,
            jobPost,
            requiredSkills,
            experience?.ExperienceMinYears,
            experience?.ExperienceMaxYears,
            candidatePool,
            existingLeads);
    }

    public async Task<int> CountOnlineHeadhuntingLeadsCreatedTodayAsync(
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT COUNT(1)
            FROM dbo.OnlineCandidateLeads
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId
              AND CreatedAtUtc >= CONVERT(date, SYSUTCDATETIME())
              AND CreatedAtUtc < DATEADD(day, 1, CONVERT(date, SYSUTCDATETIME()));
            """;

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken));
    }

    public async Task<OperationsOnlineHeadhuntingResult> SaveOnlineHeadhuntingResultAsync(
        Guid tenantId,
        Guid actorUserId,
        OnlineHeadhuntingSearchInput input,
        OperationsOnlineHeadhuntingContext context,
        OnlineHeadhuntingAgentResult result,
        int dailyLeadCountBeforeRun,
        int dailyLeadLimit,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var runId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var leadsToPersist = DistinctNewOnlineLeads(context.ExistingLeads, result.Leads);
        const string insertRunSql = """
            INSERT INTO dbo.OnlineCandidateSourcingRuns
            (
                OnlineCandidateSourcingRunId,
                TenantId,
                JobRequestId,
                JobPostId,
                RequestedByUserId,
                AiAgentRunId,
                SearchMoreFromRunId,
                RequestedLimit,
                DailyLeadLimit,
                DailyLeadCountBeforeRun,
                SourceCodesJson,
                QueriesJson,
                SearchStatus,
                Model,
                LeadsReturned,
                CreatedAtUtc
            )
            VALUES
            (
                @RunId,
                @TenantId,
                @JobRequestId,
                @JobPostId,
                @RequestedByUserId,
                @AiAgentRunId,
                @SearchMoreFromRunId,
                @RequestedLimit,
                @DailyLeadLimit,
                @DailyLeadCountBeforeRun,
                @SourceCodesJson,
                @QueriesJson,
                @SearchStatus,
                @Model,
                @LeadsReturned,
                @CreatedAtUtc
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertRunSql,
            new
            {
                RunId = runId,
                TenantId = tenantId,
                JobRequestId = context.JobRequest.Id,
                JobPostId = context.JobPost?.JobPostId,
                RequestedByUserId = actorUserId,
                AiAgentRunId = result.AgentRunId,
                input.SearchMoreFromRunId,
                RequestedLimit = input.Limit ?? 20,
                DailyLeadLimit = dailyLeadLimit,
                DailyLeadCountBeforeRun = dailyLeadCountBeforeRun,
                SourceCodesJson = JsonSerializer.Serialize(result.SourceCodes),
                QueriesJson = JsonSerializer.Serialize(result.Queries),
                result.SearchStatus,
                result.Model,
                LeadsReturned = leadsToPersist.Count,
                CreatedAtUtc = now
            },
            transaction,
            cancellationToken: cancellationToken));

        const string insertLeadSql = """
            INSERT INTO dbo.OnlineCandidateLeads
            (
                OnlineCandidateLeadId,
                OnlineCandidateSourcingRunId,
                TenantId,
                JobRequestId,
                Rank,
                SourceCode,
                SourceDisplayName,
                SourceUrl,
                DisplayName,
                CurrentTitle,
                CurrentCompany,
                LocationText,
                Email,
                Phone,
                ProfileUrl,
                EvidenceSnippet,
                MatchScore,
                Confidence,
                FitSummary,
                StrengthsJson,
                MatchedSkillsJson,
                GapsJson,
                MissingDataJson,
                DuplicateStatus,
                DuplicateCandidateId,
                DuplicateCandidateName,
                DuplicateExplanation,
                OutreachDraft,
                Status,
                CreatedAtUtc
            )
            VALUES
            (
                @OnlineCandidateLeadId,
                @RunId,
                @TenantId,
                @JobRequestId,
                @Rank,
                @SourceCode,
                @SourceDisplayName,
                @SourceUrl,
                @DisplayName,
                @CurrentTitle,
                @CurrentCompany,
                @LocationText,
                @Email,
                @Phone,
                @ProfileUrl,
                @EvidenceSnippet,
                @MatchScore,
                @Confidence,
                @FitSummary,
                @StrengthsJson,
                @MatchedSkillsJson,
                @GapsJson,
                @MissingDataJson,
                @DuplicateStatus,
                @DuplicateCandidateId,
                @DuplicateCandidateName,
                @DuplicateExplanation,
                @OutreachDraft,
                @Status,
                @CreatedAtUtc
            );
            """;

        var persistedLeads = new List<OperationsOnlineCandidateLead>();
        foreach (var lead in leadsToPersist)
        {
            var leadId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                insertLeadSql,
                new
                {
                    OnlineCandidateLeadId = leadId,
                    RunId = runId,
                    TenantId = tenantId,
                    JobRequestId = context.JobRequest.Id,
                    lead.Rank,
                    lead.SourceCode,
                    lead.SourceDisplayName,
                    lead.SourceUrl,
                    lead.DisplayName,
                    lead.CurrentTitle,
                    lead.CurrentCompany,
                    lead.LocationText,
                    lead.Email,
                    lead.Phone,
                    lead.ProfileUrl,
                    lead.EvidenceSnippet,
                    lead.MatchScore,
                    lead.Confidence,
                    lead.FitSummary,
                    StrengthsJson = JsonSerializer.Serialize(lead.Strengths),
                    MatchedSkillsJson = JsonSerializer.Serialize(lead.MatchedSkills),
                    GapsJson = JsonSerializer.Serialize(lead.Gaps),
                    MissingDataJson = JsonSerializer.Serialize(lead.MissingData),
                    lead.DuplicateStatus,
                    lead.DuplicateCandidateId,
                    lead.DuplicateCandidateName,
                    lead.DuplicateExplanation,
                    lead.OutreachDraft,
                    Status = "New",
                    CreatedAtUtc = now
                },
                transaction,
                cancellationToken: cancellationToken));

            persistedLeads.Add(ToPersistedLead(leadId, runId, context.JobRequest.Id, lead, "New", now));
        }

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "online_headhunting.search_run",
            "JobRequest",
            context.JobRequest.Id,
            context.JobRequest.Code,
            $"Online Headhunting found {persistedLeads.Count} lead(s) for recruiter review.",
            "Recruiter Sourcing",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new OperationsOnlineHeadhuntingResult(
            new OperationsOnlineHeadhuntingRunSummary(
                runId,
                context.JobRequest.Id,
                context.JobPost?.JobPostId,
                result.AgentRunId,
                input.SearchMoreFromRunId,
                input.Limit ?? 20,
                dailyLeadLimit,
                dailyLeadCountBeforeRun,
                persistedLeads.Count,
                result.SearchStatus,
                result.Model,
                result.SourceCodes,
                result.Queries,
                Utc(now)),
            persistedLeads);
    }

    public async Task<OperationsOnlineHeadhuntingResult?> GetLatestOnlineHeadhuntingResultAsync(
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        return await GetLatestOnlineHeadhuntingResultAsync(connection, tenantId, jobRequestId, cancellationToken);
    }

    public async Task<OperationsOnlineCandidateLead?> UpdateOnlineCandidateLeadStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid onlineCandidateLeadId,
        UpdateOnlineCandidateLeadStatusInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await connection.QuerySingleOrDefaultAsync<OnlineLeadActionContextRow>(new CommandDefinition(
            """
            SELECT TOP (1) JobRequestId
            FROM dbo.OnlineCandidateLeads
            WHERE TenantId = @TenantId
              AND OnlineCandidateLeadId = @OnlineCandidateLeadId;
            """,
            new { TenantId = tenantId, OnlineCandidateLeadId = onlineCandidateLeadId },
            transaction,
            cancellationToken: cancellationToken));
        if (context is null ||
            !await CanMutateRecruiterSourcingAsync(connection, transaction, tenantId, actorUserId, context.JobRequestId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.OnlineCandidateLeads
            SET Status = @Status,
                RejectedAtUtc = CASE WHEN @Status = N'Rejected' THEN SYSUTCDATETIME() ELSE RejectedAtUtc END
            WHERE TenantId = @TenantId
              AND OnlineCandidateLeadId = @OnlineCandidateLeadId
              AND Status <> N'Converted';
            """,
            new
            {
                TenantId = tenantId,
                OnlineCandidateLeadId = onlineCandidateLeadId,
                input.Status
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "online_headhunting.lead_status_updated",
            "OnlineCandidateLead",
            onlineCandidateLeadId,
            "Online candidate lead",
            $"Recruiter marked online lead as {input.Status}.",
            "Recruiter Sourcing",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await ReadOnlineLeadAsync(connection, tenantId, onlineCandidateLeadId, cancellationToken);
    }

    public async Task<SendCandidateInvitationsResult> SendCandidateInvitationsAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        SendCandidateInvitationsInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await ReadCandidateInvitationContextAsync(
            connection,
            transaction,
            tenantId,
            jobRequestId,
            input.JobPostId,
            cancellationToken);
        if (context is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SendCandidateInvitationsResult(0, ["Job Request was not found."]);
        }

        var selectedIds = input.CandidateIds
            .Where(candidateId => candidateId != Guid.Empty)
            .Distinct()
            .ToArray();
        var candidates = await ReadCandidateInvitationRecipientsAsync(
            connection,
            transaction,
            tenantId,
            selectedIds,
            cancellationToken);
        var candidatesById = candidates.ToDictionary(candidate => candidate.CandidateId);
        var skippedCandidates = selectedIds
            .Where(candidateId => !candidatesById.ContainsKey(candidateId))
            .Select(candidateId => candidateId.ToString("D"))
            .ToArray();

        if (candidates.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SendCandidateInvitationsResult(0, skippedCandidates);
        }

        var notificationEventId = await EnsureCandidateInvitationEventAsync(
            connection,
            transaction,
            tenantId,
            cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var recruiterMessage = string.IsNullOrWhiteSpace(input.Message)
            ? null
            : input.Message.Trim();
        var subject = $"{context.CompanyName} is looking for {context.JobTitle}";
        var defaultInvitationText = $"{context.CompanyName} is looking for {context.JobTitle}. Please apply at our job portal for this job post if you are interested.";
        var trackedInvitations = context.JobPostId.HasValue
            ? candidates
                .Select(candidate => CreateTrackedCandidateInvitation(
                    candidate.CandidateId,
                    context.JobPostId.Value,
                    ExtractFirstAbsoluteUrl(recruiterMessage)))
                .ToArray()
            : [];
        var trackedInvitationsByCandidateId = trackedInvitations.ToDictionary(invitation => invitation.CandidateId);

        if (trackedInvitations.Length > 0)
        {
            const string insertInvitationSql = """
                INSERT INTO dbo.CandidateInvitations
                (
                    CandidateInvitationId,
                    TenantId,
                    CandidateProspectId,
                    CandidateId,
                    JobRequestId,
                    JobPostId,
                    InvitedByUserId,
                    TokenHash,
                    Email,
                    Status,
                    ExpiresAtUtc,
                    CreatedAtUtc
                )
                VALUES
                (
                    @CandidateInvitationId,
                    @TenantId,
                    NULL,
                    @CandidateId,
                    @JobRequestId,
                    @JobPostId,
                    @ActorUserId,
                    @TokenHash,
                    @Email,
                    N'Sent',
                    DATEADD(DAY, 7, SYSUTCDATETIME()),
                    SYSUTCDATETIME()
                );
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                insertInvitationSql,
                trackedInvitations.Select(invitation => new
                {
                    TenantId = tenantId,
                    invitation.CandidateInvitationId,
                    invitation.CandidateId,
                    context.JobRequestId,
                    invitation.JobPostId,
                    ActorUserId = actorUserId,
                    invitation.TokenHash,
                    Email = candidatesById[invitation.CandidateId].Email
                }),
                transaction,
                cancellationToken: cancellationToken));
        }

        const string insertOutboxSql = """
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
            VALUES
            (
                @NotificationOutboxId,
                @TenantId,
                @NotificationEventId,
                NULL,
                NULL,
                @RecipientEmail,
                N'Email',
                @PayloadJson,
                N'Pending',
                @Now,
                @Now,
                @Now
            );
            """;

        var outboxRows = candidates.Select(candidate =>
        {
            trackedInvitationsByCandidateId.TryGetValue(candidate.CandidateId, out var trackedInvitation);
            var invitationText = BuildTrackedInvitationText(recruiterMessage ?? defaultInvitationText, trackedInvitation?.JobLink);
            var bodyLines = new List<string>
            {
                $"Hello {candidate.DisplayName},",
                string.Empty,
                invitationText
            };

            bodyLines.Add(string.Empty);
            bodyLines.Add($"Regards,");
            bodyLines.Add(context.CompanyName);

            var textBody = string.Join(Environment.NewLine, bodyLines);
            var jobLink = trackedInvitation?.JobLink;

            return new
            {
                NotificationOutboxId = Guid.NewGuid(),
                TenantId = tenantId,
                NotificationEventId = notificationEventId,
                RecipientEmail = candidate.Email,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    subject,
                    body = textBody,
                    htmlBody = BuildCandidateInvitationHtmlBody(
                        subject,
                        context.CompanyName,
                        context.JobTitle,
                        candidate.DisplayName,
                        invitationText,
                        jobLink),
                    entityType = "JobRequest",
                    entityId = jobRequestId,
                    variables = new Dictionary<string, string>
                    {
                        ["companyName"] = context.CompanyName,
                        ["jobTitle"] = context.JobTitle,
                        ["candidateName"] = candidate.DisplayName,
                        ["requestCode"] = context.RequestCode
                    }
                }),
                Now = now.UtcDateTime
            };
        }).ToArray();

        await connection.ExecuteAsync(new CommandDefinition(
            insertOutboxSql,
            outboxRows,
            transaction,
            cancellationToken: cancellationToken));

        if (context.JobPostId.HasValue)
        {
            await CreateInvitedApplicationsForRediscoveredCandidatesAsync(
                connection,
                transaction,
                tenantId,
                actorUserId,
                context,
                candidates,
                recruiterMessage,
                cancellationToken);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "CandidateInvitationQueued",
            "JobRequest",
            jobRequestId,
            context.RequestCode,
            $"Queued candidate invitation email for {candidates.Count} rediscovered candidate(s).",
            "Recruiter Sourcing",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new SendCandidateInvitationsResult(candidates.Count, skippedCandidates);
    }

    public async Task<AddManualCandidateResult?> AddManualCandidateToJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        AddManualCandidateInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await ReadPublishedPortalJobPostAsync(connection, transaction, tenantId, jobPostId, cancellationToken);
        if (context is null ||
            !await CanMutateRecruiterSourcingAsync(connection, transaction, tenantId, actorUserId, context.JobRequestId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var source = await ReadCandidateSourceLabelAsync(connection, transaction, tenantId, input.SourceLabel, cancellationToken)
            ?? await ReadCandidateSourceLabelAsync(connection, transaction, tenantId, "Other", cancellationToken);

        var candidate = input.ExistingCandidateId.HasValue
            ? await ReadCandidateByIdAsync(connection, transaction, tenantId, input.ExistingCandidateId.Value, cancellationToken)
            : await ReadCandidateByEmailAsync(connection, transaction, tenantId, input.Email, cancellationToken);

        var existingCandidate = candidate is not null;
        if (candidate is null)
        {
            candidate = await CreateInvitedCandidateAsync(
                connection,
                transaction,
                tenantId,
                input.DisplayName ?? input.Email,
                input.Email,
                input.Phone,
                input.LinkedInUrl,
                input.CurrentDesignation,
                input.CurrentCompany,
                input.ExperienceYears,
                input.NoticePeriodDays,
                actorUserId,
                cancellationToken);
        }
        else
        {
            await UpdateCandidateProfileAsync(
                connection,
                transaction,
                tenantId,
                candidate.CandidateId,
                input.Phone,
                input.LinkedInUrl,
                input.CurrentDesignation,
                input.CurrentCompany,
                input.ExperienceYears,
                input.NoticePeriodDays,
                cancellationToken);
        }

        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await ReplaceCandidateSkillsAsync(
            connection,
            transaction,
            tenantId,
            candidate.CandidateId,
            input.SkillIds ?? [],
            cancellationToken);

        await UpsertCandidateEducationAsync(
            connection,
            transaction,
            tenantId,
            candidate.CandidateId,
            input.UniversityName,
            input.DegreeName,
            input.GraduationYear,
            cancellationToken);

        await UpsertCandidateWorkHistoryAsync(
            connection,
            transaction,
            tenantId,
            candidate.CandidateId,
            input.CurrentCompany,
            input.CurrentDesignation,
            cancellationToken);

        var prospectId = await UpsertCandidateProspectAsync(
            connection,
            transaction,
            tenantId,
            candidate,
            source?.CandidateSourceLabelId,
            source?.DisplayName ?? input.SourceLabel,
            actorUserId,
            cancellationToken);

        await UpsertCandidateProspectJobRequestAsync(
            connection,
            transaction,
            tenantId,
            prospectId,
            context.JobRequestId,
            jobPostId,
            input.RecruiterNotes,
            cancellationToken);

        var existingApplication = await ReadActiveJobPostApplicationAsync(
            connection,
            transaction,
            tenantId,
            jobPostId,
            candidate.CandidateId,
            cancellationToken);
        var existingApplicationFound = existingApplication is not null;
        var applicationId = existingApplication?.JobApplicationId ?? await InsertJobApplicationAsync(
            connection,
            transaction,
            tenantId,
            context.JobRequestId,
            jobPostId,
            candidate.CandidateId,
            source?.CandidateSourceLabelId,
            source?.DisplayName ?? input.SourceLabel,
            "Invited",
            isInvited: true,
            actorUserId,
            input.SourceDetail,
            input.SourceUrl,
            input.RecruiterNotes,
            BuildApplicationSnapshotJson(context),
            coverLetterText: null,
            cancellationToken);

        await UpsertParsedCvApplicationDocumentAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            applicationId,
            candidate.CandidateId,
            input.ParsedCvEvidence,
            cancellationToken);

        if (!existingApplicationFound)
        {
            await InsertJobApplicationStatusHistoryAsync(
                connection,
                transaction,
                tenantId,
                applicationId,
                null,
                "Invited",
                actorUserId,
                "Recruiter manually sourced and invited this candidate.",
                cancellationToken);
        }

        var invitationQueued = await QueueCandidateInvitationAsync(
            connection,
            transaction,
            tenantId,
            context,
            candidate,
            prospectId,
            actorUserId,
            input.InvitationMessage,
            cancellationToken);

        if (input.OnlineLeadId.HasValue)
        {
            await MarkOnlineHeadhuntingLeadConvertedAsync(
                connection,
                transaction,
                tenantId,
                input.OnlineLeadId.Value,
                candidate.CandidateId,
                applicationId,
                cancellationToken);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "candidate.manual_sourced",
            "JobRequest",
            context.JobRequestId,
            context.RequestCode,
            $"Recruiter added {candidate.DisplayName} to {context.RequestCode} as a sourced candidate.",
            "Recruiter Sourcing",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new AddManualCandidateResult(
            candidate.CandidateId,
            applicationId,
            jobPostId,
            existingApplication?.CurrentStatus ?? "Invited",
            existingCandidate,
            existingApplicationFound,
            invitationQueued);
    }

    public async Task<OperationsRecruiterApplication?> UpdateCandidateApplicationStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        UpdateCandidateApplicationStatusInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await ReadApplicationActionContextAsync(
            connection,
            transaction,
            tenantId,
            jobApplicationId,
            cancellationToken);
        if (context is null ||
            !context.JobPostId.HasValue ||
            !await CanMutateRecruiterSourcingAsync(connection, transaction, tenantId, actorUserId, context.JobRequestId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var jobPostId = context.JobPostId.Value;
        var notes = input.Notes ?? string.Empty;
        if (string.Equals(context.CurrentStatus, input.Decision, StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(cancellationToken);
            return await ReadRecruiterApplicationAsync(connection, tenantId, jobPostId, jobApplicationId, cancellationToken);
        }

        var isRejected = string.Equals(input.Decision, "Rejected", StringComparison.OrdinalIgnoreCase);
        const string updateSql = """
            UPDATE dbo.JobApplications
            SET CurrentStatus = @Status,
                IsActive = CASE WHEN @IsRejected = CAST(1 AS BIT) THEN CAST(0 AS BIT) ELSE IsActive END,
                FinalDecisionAtUtc = CASE WHEN @IsRejected = CAST(1 AS BIT) THEN SYSUTCDATETIME() ELSE FinalDecisionAtUtc END,
                FinalDecisionReason = CASE WHEN @IsRejected = CAST(1 AS BIT) THEN @Notes ELSE FinalDecisionReason END,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobApplicationId = @JobApplicationId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new
            {
                TenantId = tenantId,
                JobApplicationId = jobApplicationId,
                Status = input.Decision,
                IsRejected = isRejected,
                Notes = notes
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertJobApplicationStatusHistoryAsync(
            connection,
            transaction,
            tenantId,
            jobApplicationId,
            context.CurrentStatus,
            input.Decision,
            actorUserId,
            notes,
            cancellationToken);

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "candidate_application.status_updated",
            "JobApplication",
            jobApplicationId,
            context.RequestCode,
            $"Recruiter moved {context.CandidateName} to {input.Decision}.",
            "Recruiter Sourcing",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await ReadRecruiterApplicationAsync(connection, tenantId, jobPostId, jobApplicationId, cancellationToken);
    }

    public async Task<ScheduleCandidateInterviewRepositoryResult?> ScheduleCandidateInterviewAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        ScheduleCandidateInterviewInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await ReadApplicationActionContextAsync(
            connection,
            transaction,
            tenantId,
            jobApplicationId,
            cancellationToken);
        if (context is null ||
            !context.JobPostId.HasValue ||
            !await CanMutateRecruiterSourcingAsync(connection, transaction, tenantId, actorUserId, context.JobRequestId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var round = await ReadJobPostRoundForSchedulingAsync(
            connection,
            transaction,
            tenantId,
            context.JobPostId.Value,
            input.JobPostInterviewRoundId,
            cancellationToken);
        if (round is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (!await PriorInterviewRoundsReadyAsync(
                connection,
                transaction,
                tenantId,
                context.JobPostId.Value,
                jobApplicationId,
                round.RoundOrder,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var interviewerUserId = input.InterviewerUserId.GetValueOrDefault(round.OwnerUserId.GetValueOrDefault());
        if (interviewerUserId == Guid.Empty)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var interviewerName = await ReadActiveUserDisplayNameAsync(
            connection,
            transaction,
            tenantId,
            interviewerUserId,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(interviewerName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        const string duplicateSql = """
            SELECT COUNT(1)
            FROM dbo.Interviews
            WHERE TenantId = @TenantId
              AND JobApplicationId = @JobApplicationId
              AND JobPostInterviewRoundId = @JobPostInterviewRoundId
              AND Status IN (N'Scheduled', N'Completed', N'Skipped');
            """;
        var duplicateCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            duplicateSql,
            new
            {
                TenantId = tenantId,
                JobApplicationId = jobApplicationId,
                input.JobPostInterviewRoundId
            },
            transaction,
            cancellationToken: cancellationToken));
        if (duplicateCount > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var interviewId = Guid.NewGuid();
        const string insertInterviewSql = """
            INSERT INTO dbo.Interviews
            (
                InterviewId,
                TenantId,
                JobApplicationId,
                JobPostInterviewRoundId,
                InterviewerUserId,
                ScheduledByUserId,
                StartsAtUtc,
                DurationMinutes,
                MeetingLink,
                LocationText,
                CalendarProvider,
                CalendarEventId,
                CalendarEventHtmlLink,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @InterviewId,
                @TenantId,
                @JobApplicationId,
                @JobPostInterviewRoundId,
                @InterviewerUserId,
                @ScheduledByUserId,
                @StartsAtUtc,
                @DurationMinutes,
                @MeetingLink,
                @LocationText,
                @CalendarProvider,
                @CalendarEventId,
                @CalendarEventHtmlLink,
                N'Scheduled',
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertInterviewSql,
            new
            {
                InterviewId = interviewId,
                TenantId = tenantId,
                JobApplicationId = jobApplicationId,
                input.JobPostInterviewRoundId,
                InterviewerUserId = interviewerUserId,
                ScheduledByUserId = actorUserId,
                StartsAtUtc = input.StartsAtUtc.UtcDateTime,
                DurationMinutes = round.DurationMinutes,
                input.MeetingLink,
                input.LocationText,
                input.CalendarProvider,
                input.CalendarEventId,
                input.CalendarEventHtmlLink
            },
            transaction,
            cancellationToken: cancellationToken));

        var participantContext = await ReadInterviewScheduleNotificationContextAsync(
            connection,
            transaction,
            tenantId,
            interviewId,
            cancellationToken);
        if (participantContext is not null)
        {
            await UpsertInterviewParticipantsAsync(
                connection,
                transaction,
                tenantId,
                interviewId,
                participantContext,
                cancellationToken);
        }

        if (!string.Equals(context.CurrentStatus, "Interviewing", StringComparison.OrdinalIgnoreCase))
        {
            const string updateApplicationSql = """
                UPDATE dbo.JobApplications
                SET CurrentStatus = N'Interviewing',
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE TenantId = @TenantId
                  AND JobApplicationId = @JobApplicationId;
                """;
            await connection.ExecuteAsync(new CommandDefinition(
                updateApplicationSql,
                new { TenantId = tenantId, JobApplicationId = jobApplicationId },
                transaction,
                cancellationToken: cancellationToken));

            await InsertJobApplicationStatusHistoryAsync(
                connection,
                transaction,
                tenantId,
                jobApplicationId,
                context.CurrentStatus,
                "Interviewing",
                actorUserId,
                $"Scheduled {round.Name}.",
                cancellationToken);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "candidate_interview.scheduled",
            "JobApplication",
            jobApplicationId,
            context.RequestCode,
            $"Scheduled {round.Name} for {context.CandidateName} with {interviewerName}.",
            "Interview Scheduling",
            cancellationToken);

        await QueueInterviewScheduledEmailsAsync(
            connection,
            transaction,
            tenantId,
            interviewId,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var result = new ScheduleCandidateInterviewResult(
            interviewId,
            jobApplicationId,
            input.JobPostInterviewRoundId,
            interviewerUserId,
            interviewerName,
            round.Name,
            input.StartsAtUtc,
            round.DurationMinutes,
            "Scheduled",
            input.MeetingLink,
            input.CalendarProvider,
            input.CalendarEventId,
            input.CalendarEventHtmlLink);

        var notificationDispatches = participantContext is null
            ? []
            : BuildInterviewScheduledDispatches(interviewId, participantContext);

        return new ScheduleCandidateInterviewRepositoryResult(result, notificationDispatches);
    }

    public async Task<OperationsScheduleCandidateInterviewValidation> ValidateCandidateInterviewScheduleAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        ScheduleCandidateInterviewInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var status = await ReadCandidateInterviewScheduleValidationAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            jobApplicationId,
            input,
            cancellationToken);

        await transaction.RollbackAsync(cancellationToken);
        return new OperationsScheduleCandidateInterviewValidation(status);
    }

    public async Task<OperationsInterviewScheduleContext?> GetInterviewScheduleContextAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        ScheduleCandidateInterviewInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await ReadInterviewScheduleContextForSchedulingAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            jobApplicationId,
            input,
            cancellationToken);

        await transaction.RollbackAsync(cancellationToken);
        return context;
    }

    public async Task<OperationsInterviewTaskList> GetMyInterviewTasksAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantTasks,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT
                interview.InterviewId,
                application.JobApplicationId,
                postRound.JobPostInterviewRoundId,
                request.JobRequestId,
                post.JobPostId,
                request.RequestCode,
                post.Title AS JobTitle,
                COALESCE(request.ClientName, N'') AS Client,
                candidate.DisplayName AS CandidateName,
                candidate.Email AS CandidateEmail,
                postRound.Name AS RoundName,
                interviewer.DisplayName AS InterviewerName,
                interviewer.UserId AS InterviewerUserId,
                interviewer.AccountStatus AS InterviewerAccountStatus,
                CASE WHEN interviewer.DeletedAtUtc IS NULL THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END AS InterviewerIsDeleted,
                scheduledBy.DisplayName AS ScheduledByName,
                interview.StartsAtUtc AS StartsAt,
                interview.DurationMinutes,
                interview.MeetingLink,
                interview.LocationText,
                interview.Status,
                feedback.Recommendation,
                feedback.TechnicalScore,
                feedback.CommunicationScore,
                feedback.CultureScore,
                feedback.FeedbackText,
                feedback.SubmittedAtUtc AS SubmittedAt
            FROM dbo.Interviews AS interview
            INNER JOIN dbo.JobApplications AS application
                ON application.TenantId = interview.TenantId
                AND application.JobApplicationId = interview.JobApplicationId
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            INNER JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            INNER JOIN dbo.JobPostInterviewRounds AS postRound
                ON postRound.TenantId = interview.TenantId
                AND postRound.JobPostInterviewRoundId = interview.JobPostInterviewRoundId
            INNER JOIN dbo.AppUsers AS interviewer
                ON interviewer.TenantId = interview.TenantId
                AND interviewer.UserId = interview.InterviewerUserId
            INNER JOIN dbo.AppUsers AS scheduledBy
                ON scheduledBy.TenantId = interview.TenantId
                AND scheduledBy.UserId = interview.ScheduledByUserId
            LEFT JOIN dbo.InterviewFeedback AS feedback
                ON feedback.TenantId = interview.TenantId
                AND feedback.InterviewId = interview.InterviewId
                AND feedback.IsSubmitted = CAST(1 AS BIT)
            WHERE interview.TenantId = @TenantId
              AND (@IncludeAllTenantTasks = CAST(1 AS BIT) OR interview.InterviewerUserId = @ActorUserId)
              AND interview.Status IN (N'Scheduled', N'Completed')
            ORDER BY
                CASE WHEN interview.Status = N'Scheduled' THEN 0 ELSE 1 END,
                interview.StartsAtUtc ASC;
            """;

        var rows = await connection.QueryAsync<InterviewTaskRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActorUserId = actorUserId, IncludeAllTenantTasks = includeAllTenantTasks },
            cancellationToken: cancellationToken));

        return new OperationsInterviewTaskList(rows.Select(ToInterviewTask).ToArray());
    }

    public async Task<OperationsInterviewQuestionRecommendationContext?> GetInterviewQuestionRecommendationContextAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantTasks,
        Guid interviewId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await ReadInterviewQuestionContextAsync(
            connection,
            null,
            tenantId,
            actorUserId,
            includeAllTenantTasks,
            interviewId,
            cancellationToken);
        if (row is null)
        {
            return null;
        }

        var requiredSkills = await ListInterviewQuestionRequiredSkillsAsync(
            connection,
            null,
            tenantId,
            row.JobRequestId,
            row.JobPostId,
            cancellationToken);
        var candidateSkills = await ListInterviewQuestionCandidateSkillsAsync(
            connection,
            null,
            tenantId,
            row.CandidateId,
            cancellationToken);
        var documentsByApplication = await ListApplicantDocumentEvidenceAsync(
            connection,
            tenantId,
            [row.JobApplicationId],
            cancellationToken);
        var priorFeedback = await ListInterviewQuestionPriorFeedbackAsync(
            connection,
            null,
            tenantId,
            row.JobApplicationId,
            row.InterviewId,
            cancellationToken);

        return ToInterviewQuestionContext(
            row,
            requiredSkills,
            candidateSkills,
            documentsByApplication.TryGetValue(row.JobApplicationId, out var documents) ? documents : [],
            priorFeedback);
    }

    public async Task<InterviewQuestionRecommendationSet?> GetLatestInterviewQuestionRecommendationsAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantTasks,
        Guid interviewId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string setSql = """
            SELECT TOP (1)
                recommendationSet.RecommendationSetId,
                recommendationSet.InterviewId,
                recommendationSet.JobApplicationId,
                recommendationSet.JobPostInterviewRoundId,
                recommendationSet.AiAgentRunId,
                recommendationSet.ModelName AS Model,
                recommendationSet.PromptVersion,
                recommendationSet.VersionNumber,
                recommendationSet.Summary,
                recommendationSet.Rationale,
                recommendationSet.RegenerateReason,
                recommendationSet.CoverageJson,
                recommendationSet.Status,
                recommendationSet.GeneratedAtUtc AS GeneratedAt
            FROM dbo.InterviewQuestionRecommendationSets AS recommendationSet
            INNER JOIN dbo.Interviews AS interview
                ON interview.TenantId = recommendationSet.TenantId
                AND interview.InterviewId = recommendationSet.InterviewId
            WHERE recommendationSet.TenantId = @TenantId
              AND recommendationSet.InterviewId = @InterviewId
              AND recommendationSet.Status = N'Active'
              AND interview.Status IN (N'Scheduled', N'Completed')
              AND (@IncludeAllTenantTasks = CAST(1 AS BIT) OR interview.InterviewerUserId = @ActorUserId)
            ORDER BY recommendationSet.VersionNumber DESC, recommendationSet.GeneratedAtUtc DESC;
            """;

        var setRow = await connection.QuerySingleOrDefaultAsync<InterviewQuestionRecommendationSetRow>(new CommandDefinition(
            setSql,
            new { TenantId = tenantId, ActorUserId = actorUserId, IncludeAllTenantTasks = includeAllTenantTasks, InterviewId = interviewId },
            cancellationToken: cancellationToken));
        if (setRow is null)
        {
            return null;
        }

        var questions = await ReadInterviewQuestionRecommendationRowsAsync(
            connection,
            null,
            tenantId,
            setRow.RecommendationSetId,
            cancellationToken);

        return ToInterviewQuestionRecommendationSet(setRow, questions);
    }

    public async Task<IReadOnlyList<InterviewQuestionBankItem>> ListInterviewQuestionBankItemsAsync(
        Guid tenantId,
        IReadOnlyList<Guid> skillIds,
        string roundType,
        string jobFamily,
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        var normalizedRoundType = string.IsNullOrWhiteSpace(roundType) ? "Technical" : roundType.Trim();
        var normalizedJobFamily = string.IsNullOrWhiteSpace(jobFamily) ? "Generic" : jobFamily.Trim();
        var sqlSkillIds = skillIds.Where(skillId => skillId != Guid.Empty).Distinct().ToArray();
        if (sqlSkillIds.Length == 0)
        {
            sqlSkillIds = [Guid.Empty];
        }

        const string sql = """
            SELECT TOP (@Take)
                item.InterviewQuestionBankItemId,
                item.TenantId,
                item.SkillId,
                skill.Name AS SkillName,
                skill.Category AS SkillCategory,
                item.DepartmentId,
                item.JobFamily,
                item.RoundType,
                item.Difficulty,
                item.QuestionText,
                item.ExpectedSignal,
                item.FollowUpsJson,
                item.EvaluationRubricJson,
                item.SourceTitle,
                item.SourceUrl,
                item.ContentHashSha256
            FROM dbo.InterviewQuestionBankItems AS item
            LEFT JOIN dbo.Skills AS skill
                ON skill.TenantId = item.TenantId
                AND skill.SkillId = item.SkillId
            WHERE item.TenantId = @TenantId
              AND item.Status = N'Active'
              AND (
                    item.SkillId IS NULL
                    OR item.SkillId IN @SkillIds
                    OR item.RoundType = @RoundType
                    OR item.JobFamily = @JobFamily
                  )
            ORDER BY
                CASE
                    WHEN item.RoundType = @RoundType AND item.JobFamily = @JobFamily THEN 0
                    WHEN item.RoundType = @RoundType THEN 1
                    WHEN item.SkillId IN @SkillIds THEN 2
                    WHEN item.SkillId IS NULL THEN 3
                    ELSE 4
                END,
                CASE item.Difficulty
                    WHEN N'Basic' THEN 1
                    WHEN N'Intermediate' THEN 2
                    WHEN N'Advanced' THEN 3
                    ELSE 4
                END,
                item.CreatedAtUtc ASC;
            """;

        var rows = await connection.QueryAsync<InterviewQuestionBankItemRow>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                SkillIds = sqlSkillIds,
                RoundType = normalizedRoundType,
                JobFamily = normalizedJobFamily,
                Take = Math.Clamp(take, 20, 250)
            },
            cancellationToken: cancellationToken));

        return rows.Select(ToInterviewQuestionBankItem).ToArray();
    }

    public async Task<InterviewQuestionRecommendationSet> SaveInterviewQuestionRecommendationsAsync(
        Guid tenantId,
        Guid actorUserId,
        OperationsInterviewQuestionRecommendationContext context,
        string? regenerateReason,
        InterviewQuestionAgentResult result,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string versionSql = """
            SELECT COALESCE(MAX(VersionNumber), 0) + 1
            FROM dbo.InterviewQuestionRecommendationSets
            WHERE TenantId = @TenantId
              AND InterviewId = @InterviewId;
            """;
        var versionNumber = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            versionSql,
            new { TenantId = tenantId, context.InterviewId },
            transaction,
            cancellationToken: cancellationToken));

        var recommendationSetId = Guid.NewGuid();
        const string insertSetSql = """
            INSERT INTO dbo.InterviewQuestionRecommendationSets
            (
                RecommendationSetId,
                TenantId,
                InterviewId,
                JobApplicationId,
                JobPostInterviewRoundId,
                AiAgentRunId,
                ModelName,
                PromptVersion,
                VersionNumber,
                Summary,
                Rationale,
                RegenerateReason,
                CoverageJson,
                RetrievedBankItemIdsJson,
                Status,
                GeneratedByUserId,
                GeneratedAtUtc,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @RecommendationSetId,
                @TenantId,
                @InterviewId,
                @JobApplicationId,
                @JobPostInterviewRoundId,
                @AiAgentRunId,
                @ModelName,
                @PromptVersion,
                @VersionNumber,
                @Summary,
                @Rationale,
                @RegenerateReason,
                @CoverageJson,
                @RetrievedBankItemIdsJson,
                N'Active',
                @GeneratedByUserId,
                @GeneratedAtUtc,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertSetSql,
            new
            {
                RecommendationSetId = recommendationSetId,
                TenantId = tenantId,
                context.InterviewId,
                context.JobApplicationId,
                context.JobPostInterviewRoundId,
                AiAgentRunId = result.AgentRunId,
                ModelName = result.Model,
                result.PromptVersion,
                VersionNumber = versionNumber,
                result.Summary,
                result.Rationale,
                RegenerateReason = regenerateReason,
                CoverageJson = JsonSerializer.Serialize(result.Coverage),
                RetrievedBankItemIdsJson = JsonSerializer.Serialize(result.RetrievedBankItemIds),
                GeneratedByUserId = actorUserId,
                GeneratedAtUtc = result.GeneratedAtUtc.UtcDateTime
            },
            transaction,
            cancellationToken: cancellationToken));

        const string insertQuestionSql = """
            INSERT INTO dbo.InterviewQuestionRecommendations
            (
                QuestionRecommendationId,
                RecommendationSetId,
                TenantId,
                SortOrder,
                QuestionText,
                QuestionType,
                RoundType,
                SkillName,
                Difficulty,
                Rationale,
                ExpectedSignal,
                FollowUpsJson,
                EvaluationRubricJson,
                SourceBankItemId,
                CreatedAtUtc
            )
            VALUES
            (
                @QuestionRecommendationId,
                @RecommendationSetId,
                @TenantId,
                @SortOrder,
                @QuestionText,
                @QuestionType,
                @RoundType,
                @SkillName,
                @Difficulty,
                @Rationale,
                @ExpectedSignal,
                @FollowUpsJson,
                @EvaluationRubricJson,
                @SourceBankItemId,
                SYSUTCDATETIME()
            );
            """;

        var questionRows = result.Questions.Select((question, index) => new
        {
            QuestionRecommendationId = Guid.NewGuid(),
            RecommendationSetId = recommendationSetId,
            TenantId = tenantId,
            SortOrder = index + 1,
            question.QuestionText,
            question.QuestionType,
            question.RoundType,
            question.SkillName,
            question.Difficulty,
            question.Rationale,
            question.ExpectedSignal,
            FollowUpsJson = JsonSerializer.Serialize(question.FollowUps),
            EvaluationRubricJson = JsonSerializer.Serialize(question.EvaluationRubric),
            question.SourceBankItemId
        }).ToArray();

        await connection.ExecuteAsync(new CommandDefinition(
            insertQuestionSql,
            questionRows,
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        var setRow = new InterviewQuestionRecommendationSetRow(
            recommendationSetId,
            context.InterviewId,
            context.JobApplicationId,
            context.JobPostInterviewRoundId,
            result.AgentRunId,
            result.Model,
            result.PromptVersion,
            versionNumber,
            result.Summary,
            result.Rationale,
            regenerateReason,
            JsonSerializer.Serialize(result.Coverage),
            "Active",
            result.GeneratedAtUtc.UtcDateTime);
        var savedQuestions = questionRows.Select(row => new InterviewQuestionRecommendationRow(
            row.QuestionRecommendationId,
            row.SortOrder,
            row.QuestionText,
            row.QuestionType,
            row.RoundType,
            row.SkillName,
            row.Difficulty,
            row.Rationale,
            row.ExpectedSignal,
            row.FollowUpsJson,
            row.EvaluationRubricJson,
            row.SourceBankItemId)).ToArray();

        return ToInterviewQuestionRecommendationSet(setRow, savedQuestions);
    }

    public async Task<OperationsMutationRepositoryResult<SubmitInterviewFeedbackResult>> SubmitInterviewFeedbackAsync(
        Guid tenantId,
        Guid actorUserId,
        bool canAdminOverrideInactiveInterviewer,
        Guid interviewId,
        SubmitInterviewFeedbackInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await ReadInterviewFeedbackContextAsync(
            connection,
            transaction,
            tenantId,
            interviewId,
            cancellationToken);
        if (context is null ||
            !string.Equals(context.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
            !InterviewFeedbackPolicy.CanSubmit(
                actorUserId,
                canAdminOverrideInactiveInterviewer,
                context.InterviewerUserId,
                context.InterviewerAccountStatus,
                context.InterviewerIsDeleted))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OperationsMutationRepositoryResult<SubmitInterviewFeedbackResult>(false, null, []);
        }

        const string duplicateFeedbackSql = """
            SELECT COUNT(1)
            FROM dbo.InterviewFeedback
            WHERE TenantId = @TenantId
              AND InterviewId = @InterviewId
              AND IsSubmitted = CAST(1 AS BIT);
            """;
        var submittedCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            duplicateFeedbackSql,
            new { TenantId = tenantId, InterviewId = interviewId },
            transaction,
            cancellationToken: cancellationToken));
        if (submittedCount > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OperationsMutationRepositoryResult<SubmitInterviewFeedbackResult>(false, null, []);
        }

        var submittedAt = DateTimeOffset.UtcNow;
        const string insertFeedbackSql = """
            INSERT INTO dbo.InterviewFeedback
            (
                InterviewFeedbackId,
                TenantId,
                InterviewId,
                SubmittedByUserId,
                TechnicalScore,
                CommunicationScore,
                CultureScore,
                Recommendation,
                FeedbackText,
                IsSubmitted,
                SubmittedAtUtc,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                NEWID(),
                @TenantId,
                @InterviewId,
                @SubmittedByUserId,
                @TechnicalScore,
                @CommunicationScore,
                @CultureScore,
                @Recommendation,
                @FeedbackText,
                CAST(1 AS BIT),
                @SubmittedAtUtc,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertFeedbackSql,
            new
            {
                TenantId = tenantId,
                InterviewId = interviewId,
                SubmittedByUserId = actorUserId,
                input.TechnicalScore,
                input.CommunicationScore,
                input.CultureScore,
                input.Recommendation,
                input.FeedbackText,
                SubmittedAtUtc = submittedAt.UtcDateTime
            },
            transaction,
            cancellationToken: cancellationToken));

        const string updateInterviewSql = """
            UPDATE dbo.Interviews
            SET Status = N'Completed',
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND InterviewId = @InterviewId;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            updateInterviewSql,
            new { TenantId = tenantId, InterviewId = interviewId },
            transaction,
            cancellationToken: cancellationToken));

        var reviewPath = BuildRecruiterFeedbackReviewPath(context.JobRequestId, context.JobApplicationId);
        var reviewUrl = BuildFrontendUrl(_frontendBaseUrl, reviewPath);

        await QueueInterviewFeedbackSubmittedEmailAsync(
            connection,
            transaction,
            tenantId,
            context,
            input,
            interviewId,
            reviewUrl,
            cancellationToken);

        var isAdminOverride = actorUserId != context.InterviewerUserId &&
            canAdminOverrideInactiveInterviewer &&
            InterviewFeedbackPolicy.IsInactiveInterviewer(context.InterviewerAccountStatus, context.InterviewerIsDeleted);
        var auditDescription = isAdminOverride
            ? $"Admin override feedback was submitted for {context.CandidateName} in {context.RoundName} because the assigned interviewer was inactive."
            : $"Feedback was submitted for {context.CandidateName} in {context.RoundName}.";

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "candidate_interview.feedback_submitted",
            "Interview",
            interviewId,
            context.RequestCode,
            auditDescription,
            "Interview Feedback",
            cancellationToken);

        var dispatches = BuildInterviewFeedbackSubmittedDispatches(context, input, reviewPath);

        await transaction.CommitAsync(cancellationToken);
        return new OperationsMutationRepositoryResult<SubmitInterviewFeedbackResult>(
            true,
            new SubmitInterviewFeedbackResult(
                interviewId,
                context.JobApplicationId,
                "Completed",
                input.Recommendation,
                submittedAt),
            dispatches);
    }

    public async Task<OperationsMutationRepositoryResult<ForwardToHiringManagerResult>> ForwardToHiringManagerAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await ReadApplicationActionContextAsync(connection, transaction, tenantId, jobApplicationId, cancellationToken);
        if (context is null ||
            !context.JobPostId.HasValue ||
            !await CanMutateRecruiterSourcingAsync(connection, transaction, tenantId, actorUserId, context.JobRequestId, cancellationToken) ||
            !await ApplicationInterviewRoundsResolvedAsync(connection, transaction, tenantId, context.JobPostId.Value, jobApplicationId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OperationsMutationRepositoryResult<ForwardToHiringManagerResult>(false, null, []);
        }

        var reviewContext = await ReadHiringReviewNotificationContextAsync(
            connection,
            transaction,
            tenantId,
            jobApplicationId,
            cancellationToken);
        if (reviewContext is null || reviewContext.HiringManagerUserId == Guid.Empty)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OperationsMutationRepositoryResult<ForwardToHiringManagerResult>(false, null, []);
        }

        var sourcingAssignmentId = await ReadCurrentAssignmentForActionAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            context.JobRequestId,
            "SOURCING",
            requireClaimedForGroups: true,
            cancellationToken);
        if (!sourcingAssignmentId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OperationsMutationRepositoryResult<ForwardToHiringManagerResult>(false, null, []);
        }

        await CompleteAssignmentAsync(connection, transaction, tenantId, sourcingAssignmentId.Value, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var workflowDefinitionId = await ReadWorkflowDefinitionIdAsync(connection, transaction, tenantId, cancellationToken);
        var stageId = await ReadWorkflowStageIdAsync(connection, transaction, tenantId, "HIRING_MANAGER_REVIEW", cancellationToken);
        var transitionId = await ReadWorkflowTransitionIdAsync(connection, transaction, tenantId, "FORWARD_TO_HIRING_MANAGER", cancellationToken);
        var assignmentId = Guid.NewGuid();

        await InsertWorkflowAssignmentAsync(
            connection,
            transaction,
            tenantId,
            workflowDefinitionId,
            stageId,
            transitionId,
            assignmentId,
            context.JobRequestId,
            WorkflowAssignmentTarget.ForUser(reviewContext.HiringManagerUserId, "job request hiring manager"),
            now,
            cancellationToken);

        const string updateApplicationSql = """
            UPDATE dbo.JobApplications
            SET CurrentStatus = N'HiringManagerReview',
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobApplicationId = @JobApplicationId;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            updateApplicationSql,
            new { TenantId = tenantId, JobApplicationId = jobApplicationId },
            transaction,
            cancellationToken: cancellationToken));

        await InsertJobApplicationStatusHistoryAsync(
            connection,
            transaction,
            tenantId,
            jobApplicationId,
            context.CurrentStatus,
            "HiringManagerReview",
            actorUserId,
            "Recruiter forwarded candidate to Hiring Manager Review.",
            cancellationToken);

        await UpdateJobRequestStageAsync(
            connection,
            transaction,
            tenantId,
            context.JobRequestId,
            "HiringManagerReview",
            "HIRING_MANAGER_REVIEW",
            assignmentId,
            cancellationToken);

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "hiring_manager_review.forwarded",
            "JobApplication",
            jobApplicationId,
            reviewContext.RequestCode,
            $"{reviewContext.CandidateName} was forwarded to Hiring Manager Review.",
            "Hiring Manager Review",
            cancellationToken);

        var actorName = await ReadUserDisplayNameAsync(connection, transaction, tenantId, actorUserId, cancellationToken);
        var notificationContent = await ReadNotificationContentAsync(
            connection,
            transaction,
            tenantId,
            NotificationEventCodes.HiringManagerReviewReady,
            reviewContext.RequestCode,
            reviewContext.JobTitle,
            actorName,
            cancellationToken,
            new Dictionary<string, string> { ["candidateName"] = reviewContext.CandidateName });
        var recipient = await ReadNotificationRecipientAsync(
            connection,
            transaction,
            tenantId,
            reviewContext.HiringManagerUserId,
            cancellationToken);
        IReadOnlyList<OperationsNotificationDispatch> dispatches = recipient is null
            ? []
            : await QueueAndBuildDispatchesAsync(
                connection,
                transaction,
                tenantId,
                notificationContent,
                [recipient],
                context.JobRequestId,
                reviewContext.JobTitle,
                actorName,
                reviewContext.RequestCode,
                now,
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new OperationsMutationRepositoryResult<ForwardToHiringManagerResult>(
            true,
            new ForwardToHiringManagerResult(jobApplicationId, context.JobRequestId, reviewContext.HiringManagerUserId, "HiringManagerReview"),
            dispatches);
    }

    public async Task<HiringManagerReviewList> GetHiringManagerReviewsAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            WITH LatestOffer AS
            (
                SELECT *
                FROM
                (
                    SELECT
                        offer.OfferLetterId,
                        offer.JobApplicationId,
                        offer.Status,
                        offer.UpdatedAtUtc,
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY offer.JobApplicationId
                            ORDER BY offer.Version DESC, offer.UpdatedAtUtc DESC
                        ) AS RowNumber
                    FROM dbo.OfferLetters AS offer
                    WHERE offer.TenantId = @TenantId
                ) AS rankedOffer
                WHERE rankedOffer.RowNumber = 1
            ),
            MeetingAgg AS
            (
                SELECT
                    meeting.JobApplicationId,
                    MAX(meeting.MeetingAtUtc) AS LatestMeetingAtUtc
                FROM dbo.OfferPresentationMeetings AS meeting
                WHERE meeting.TenantId = @TenantId
                GROUP BY meeting.JobApplicationId
            )
            SELECT
                application.JobApplicationId,
                request.JobRequestId,
                application.JobPostId,
                request.RequestCode,
                COALESCE(post.Title, request.Title) AS JobTitle,
                COALESCE(request.ClientName, N'') AS Client,
                department.Name AS Department,
                candidate.DisplayName AS CandidateName,
                candidate.Email AS CandidateEmail,
                application.CurrentStatus AS Status,
                hiringManager.DisplayName AS HiringManagerName,
                application.UpdatedAtUtc AS UpdatedAt,
                latestOffer.Status AS OfferLetterStatus,
                meetingAgg.LatestMeetingAtUtc AS LatestMeetingAt
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            LEFT JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            INNER JOIN dbo.Departments AS department
                ON department.TenantId = request.TenantId
                AND department.DepartmentId = request.DepartmentId
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            INNER JOIN dbo.AppUsers AS hiringManager
                ON hiringManager.TenantId = request.TenantId
                AND hiringManager.UserId = request.HiringManagerUserId
            LEFT JOIN LatestOffer AS latestOffer
                ON latestOffer.JobApplicationId = application.JobApplicationId
            LEFT JOIN MeetingAgg AS meetingAgg
                ON meetingAgg.JobApplicationId = application.JobApplicationId
            WHERE application.TenantId = @TenantId
              AND application.CurrentStatus IN (N'HiringManagerReview', N'Offered', N'OnHold', N'Rejected', N'Hired', N'Joined')
              AND (@IncludeAllTenantReviews = CAST(1 AS BIT) OR request.HiringManagerUserId = @ActorUserId)
            ORDER BY application.UpdatedAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<HiringManagerReviewListRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActorUserId = actorUserId, IncludeAllTenantReviews = includeAllTenantReviews },
            cancellationToken: cancellationToken));

        return new HiringManagerReviewList(rows
            .Select(row => new HiringManagerReviewListItem(
                row.JobApplicationId,
                row.JobRequestId,
                row.JobPostId,
                row.RequestCode,
                row.JobTitle,
                row.Client,
                row.Department,
                row.CandidateName,
                row.CandidateEmail,
                row.Status,
                row.HiringManagerName,
                Utc(row.UpdatedAt),
                row.OfferLetterStatus,
                ToUtc(row.LatestMeetingAt)))
            .ToArray());
    }

    public async Task<HiringManagerDashboard> GetHiringManagerDashboardAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            WITH LatestOffer AS
            (
                SELECT *
                FROM
                (
                    SELECT
                        offer.OfferLetterId,
                        offer.JobApplicationId,
                        offer.Status,
                        offer.UpdatedAtUtc,
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY offer.JobApplicationId
                            ORDER BY offer.Version DESC, offer.UpdatedAtUtc DESC
                        ) AS RowNumber
                    FROM dbo.OfferLetters AS offer
                    WHERE offer.TenantId = @TenantId
                ) AS rankedOffer
                WHERE rankedOffer.RowNumber = 1
            ),
            MeetingAgg AS
            (
                SELECT
                    meeting.JobApplicationId,
                    SUM(CASE WHEN meeting.Status = N'Scheduled' THEN 1 ELSE 0 END) AS ScheduledMeetingCount,
                    MAX(meeting.MeetingAtUtc) AS LatestMeetingAtUtc
                FROM dbo.OfferPresentationMeetings AS meeting
                WHERE meeting.TenantId = @TenantId
                GROUP BY meeting.JobApplicationId
            ),
            InterviewAgg AS
            (
                SELECT
                    interview.JobApplicationId,
                    SUM(CASE WHEN interview.Status = N'Completed' THEN 1 ELSE 0 END) AS CompletedInterviews,
                    CAST(AVG(CAST(
                        CASE
                            WHEN feedback.TechnicalScore IS NULL OR feedback.CommunicationScore IS NULL OR feedback.CultureScore IS NULL THEN NULL
                            ELSE (feedback.TechnicalScore + feedback.CommunicationScore + feedback.CultureScore) / 3.0
                        END AS DECIMAL(6,2))) AS DECIMAL(6,2)) AS AverageScore,
                    SUM(CASE WHEN feedback.Recommendation IN (N'Proceed', N'Hire') THEN 1 ELSE 0 END) AS PositiveRecommendations
                FROM dbo.Interviews AS interview
                LEFT JOIN dbo.InterviewFeedback AS feedback
                    ON feedback.TenantId = interview.TenantId
                    AND feedback.InterviewId = interview.InterviewId
                    AND feedback.IsSubmitted = CAST(1 AS BIT)
                WHERE interview.TenantId = @TenantId
                GROUP BY interview.JobApplicationId
            )
            SELECT
                application.JobApplicationId,
                request.JobRequestId,
                application.JobPostId,
                request.RequestCode,
                COALESCE(post.Title, request.Title) AS JobTitle,
                COALESCE(request.ClientName, N'') AS Client,
                department.Name AS Department,
                candidate.DisplayName AS CandidateName,
                candidate.Email AS CandidateEmail,
                application.CurrentStatus AS Status,
                hiringManager.DisplayName AS HiringManagerName,
                application.UpdatedAtUtc AS UpdatedAt,
                DATEDIFF(DAY, application.UpdatedAtUtc, SYSUTCDATETIME()) AS DaysWaiting,
                COALESCE(interviewAgg.CompletedInterviews, 0) AS CompletedInterviews,
                interviewAgg.AverageScore,
                COALESCE(interviewAgg.PositiveRecommendations, 0) AS PositiveRecommendations,
                latestOffer.Status AS OfferLetterStatus,
                COALESCE(meetingAgg.ScheduledMeetingCount, 0) AS ScheduledMeetingCount,
                meetingAgg.LatestMeetingAtUtc AS LatestMeetingAt
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            LEFT JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            INNER JOIN dbo.Departments AS department
                ON department.TenantId = request.TenantId
                AND department.DepartmentId = request.DepartmentId
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            INNER JOIN dbo.AppUsers AS hiringManager
                ON hiringManager.TenantId = request.TenantId
                AND hiringManager.UserId = request.HiringManagerUserId
            LEFT JOIN InterviewAgg AS interviewAgg
                ON interviewAgg.JobApplicationId = application.JobApplicationId
            LEFT JOIN LatestOffer AS latestOffer
                ON latestOffer.JobApplicationId = application.JobApplicationId
            LEFT JOIN MeetingAgg AS meetingAgg
                ON meetingAgg.JobApplicationId = application.JobApplicationId
            WHERE application.TenantId = @TenantId
              AND application.CurrentStatus IN (N'HiringManagerReview', N'Offered', N'OnHold', N'Rejected', N'Hired', N'Joined')
              AND (@IncludeAllTenantReviews = CAST(1 AS BIT) OR request.HiringManagerUserId = @ActorUserId)
            ORDER BY
                CASE
                    WHEN application.CurrentStatus = N'HiringManagerReview' THEN 0
                    WHEN application.CurrentStatus IN (N'Offered', N'OnHold') THEN 1
                    WHEN application.CurrentStatus = N'Hired' THEN 2
                    ELSE 3
                END,
                DATEDIFF(DAY, application.UpdatedAtUtc, SYSUTCDATETIME()) DESC,
                application.UpdatedAtUtc DESC;
            """;

        var rows = (await connection.QueryAsync<HiringManagerDashboardReviewRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActorUserId = actorUserId, IncludeAllTenantReviews = includeAllTenantReviews },
            cancellationToken: cancellationToken))).ToArray();

        var activeRows = rows.Where(row => IsActiveHiringManagerDashboardStatus(row.Status)).ToArray();
        var summary = new HiringManagerDashboardSummary(
            PendingReviews: rows.Count(row => IsDashboardStatus(row.Status, "HiringManagerReview")),
            OfferFollowUps: rows.Count(row =>
                IsDashboardStatus(row.Status, "Offered") ||
                IsDashboardStatus(row.Status, "Hired") ||
                !string.IsNullOrWhiteSpace(row.OfferLetterStatus) ||
                row.ScheduledMeetingCount > 0),
            OnHold: rows.Count(row => IsDashboardStatus(row.Status, "OnHold")),
            CompletedOutcomes: rows.Count(row => IsDashboardStatus(row.Status, "Rejected") || IsDashboardStatus(row.Status, "Joined")),
            OldestWaitingDays: activeRows.Length == 0 ? 0 : activeRows.Max(row => Math.Max(0, row.DaysWaiting)));

        var priorityReviews = activeRows
            .Take(8)
            .Select(row => new HiringManagerDashboardReviewItem(
                row.JobApplicationId,
                row.JobRequestId,
                row.JobPostId,
                row.RequestCode,
                row.JobTitle,
                row.Client,
                row.Department,
                row.CandidateName,
                row.CandidateEmail,
                row.Status,
                row.HiringManagerName,
                Utc(row.UpdatedAt),
                Math.Max(0, row.DaysWaiting),
                row.CompletedInterviews,
                row.AverageScore,
                row.PositiveRecommendations,
                row.OfferLetterStatus,
                ToUtc(row.LatestMeetingAt)))
            .ToArray();

        var offerPipeline = new[]
        {
            new HiringManagerDashboardStatusBreakdownItem("Offer draft", rows.Count(row => !string.IsNullOrWhiteSpace(row.OfferLetterStatus))),
            new HiringManagerDashboardStatusBreakdownItem("Meeting scheduled", rows.Count(row => row.ScheduledMeetingCount > 0)),
            new HiringManagerDashboardStatusBreakdownItem("Offered", rows.Count(row => IsDashboardStatus(row.Status, "Offered"))),
            new HiringManagerDashboardStatusBreakdownItem("Pending joining", rows.Count(row => IsDashboardStatus(row.Status, "Hired"))),
            new HiringManagerDashboardStatusBreakdownItem("On hold", rows.Count(row => IsDashboardStatus(row.Status, "OnHold"))),
            new HiringManagerDashboardStatusBreakdownItem("Joined", rows.Count(row => IsDashboardStatus(row.Status, "Joined"))),
            new HiringManagerDashboardStatusBreakdownItem("Rejected", rows.Count(row => IsDashboardStatus(row.Status, "Rejected")))
        };

        var outcomeSplit = new[]
        {
            new HiringManagerDashboardStatusBreakdownItem("Offered", rows.Count(row => IsDashboardStatus(row.Status, "Offered"))),
            new HiringManagerDashboardStatusBreakdownItem("Pending joining", rows.Count(row => IsDashboardStatus(row.Status, "Hired"))),
            new HiringManagerDashboardStatusBreakdownItem("Rejected", rows.Count(row => IsDashboardStatus(row.Status, "Rejected"))),
            new HiringManagerDashboardStatusBreakdownItem("On hold", rows.Count(row => IsDashboardStatus(row.Status, "OnHold"))),
            new HiringManagerDashboardStatusBreakdownItem("Joined", rows.Count(row => IsDashboardStatus(row.Status, "Joined")))
        };

        var recentActivity = await ReadHiringManagerDashboardActivityAsync(
            connection,
            tenantId,
            actorUserId,
            includeAllTenantReviews,
            cancellationToken);

        return new HiringManagerDashboard(
            DateTimeOffset.UtcNow,
            summary,
            priorityReviews,
            offerPipeline,
            BuildHiringManagerDashboardAgingBuckets(activeRows),
            outcomeSplit,
            recentActivity);
    }

    public async Task<HiringReviewDetail?> GetHiringReviewAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        var access = await ReadHiringReviewAccessContextAsync(
            connection,
            null,
            tenantId,
            actorUserId,
            includeAllTenantReviews,
            jobApplicationId,
            cancellationToken);
        if (access is null)
        {
            return null;
        }

        return await ReadHiringReviewDetailAsync(connection, null, tenantId, jobApplicationId, cancellationToken);
    }

    public async Task<ReportingManagerOptionList?> SearchReportingManagerOptionsAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobRequestId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();

        const string requestSql = """
            SELECT TOP (1)
                request.DepartmentId,
                COALESCE(department.Name, N'Unassigned') AS DepartmentName
            FROM dbo.JobRequests AS request
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = request.TenantId
                AND department.DepartmentId = request.DepartmentId
            WHERE request.TenantId = @TenantId
              AND request.JobRequestId = @JobRequestId
              AND (@IncludeAllTenantReviews = CAST(1 AS BIT) OR request.HiringManagerUserId = @ActorUserId);
            """;

        var context = await connection.QuerySingleOrDefaultAsync<ReportingManagerRequestContextRow>(new CommandDefinition(
            requestSql,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                IncludeAllTenantReviews = includeAllTenantReviews,
                JobRequestId = jobRequestId
            },
            cancellationToken: cancellationToken));

        if (context is null)
        {
            return null;
        }

        const string optionsSql = """
            WITH FilteredEmployees AS
            (
                SELECT
                    employee.EmployeeId,
                    employee.DisplayName,
                    employee.Email,
                    employee.Designation,
                    COALESCE(department.Name, N'Unassigned') AS Department,
                    COALESCE(location.Name, N'Unassigned') AS Location,
                    employee.ExperienceYears,
                    CASE
                        WHEN @RequestDepartmentId IS NOT NULL AND employee.DepartmentId = @RequestDepartmentId THEN CAST(1 AS BIT)
                        ELSE CAST(0 AS BIT)
                    END AS IsDepartmentMatch
                FROM dbo.Employees AS employee
                LEFT JOIN dbo.Departments AS department
                    ON department.TenantId = employee.TenantId
                    AND department.DepartmentId = employee.DepartmentId
                LEFT JOIN dbo.Locations AS location
                    ON location.TenantId = employee.TenantId
                    AND location.LocationId = employee.LocationId
                WHERE employee.TenantId = @TenantId
                  AND employee.Status = N'Active'
                  AND
                  (
                      @Search IS NULL
                      OR employee.DisplayName LIKE @SearchPattern
                      OR employee.Email LIKE @SearchPattern
                      OR employee.Designation LIKE @SearchPattern
                      OR department.Name LIKE @SearchPattern
                  )
            )
            SELECT
                EmployeeId,
                DisplayName,
                Email,
                Designation,
                Department,
                Location,
                ExperienceYears,
                IsDepartmentMatch,
                COUNT(1) OVER() AS TotalCount
            FROM FilteredEmployees
            ORDER BY
                IsDepartmentMatch DESC,
                COALESCE(ExperienceYears, 0) DESC,
                DisplayName ASC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """;

        var rows = (await connection.QueryAsync<ReportingManagerOptionRow>(new CommandDefinition(
            optionsSql,
            new
            {
                TenantId = tenantId,
                RequestDepartmentId = context.DepartmentId,
                Search = search,
                SearchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
                Skip = skip,
                Take = take
            },
            cancellationToken: cancellationToken))).ToArray();

        var totalCount = rows.FirstOrDefault()?.TotalCount ?? 0;
        return new ReportingManagerOptionList(
            rows.Select(row => new ReportingManagerOption(
                row.EmployeeId,
                row.DisplayName,
                row.Email,
                row.Designation,
                row.Department,
                row.Location,
                row.ExperienceYears,
                row.IsDepartmentMatch)).ToArray(),
            totalCount,
            skip + rows.Length < totalCount);
    }

    public async Task<OfferLetterDetails?> GenerateOfferLetterAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobApplicationId,
        GenerateOfferLetterInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var access = await ReadHiringReviewAccessContextAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            includeAllTenantReviews,
            jobApplicationId,
            cancellationToken);
        if (access is null || !IsOfferEligibleStatus(access.ApplicationStatus))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var existing = await ReadLatestOfferLetterAsync(connection, transaction, tenantId, jobApplicationId, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        var body = BuildOfferLetterBody(access, input);
        var offerLetterId = Guid.NewGuid();
        const string insertSql = """
            INSERT INTO dbo.OfferLetters
            (
                OfferLetterId,
                TenantId,
                JobApplicationId,
                JobPostId,
                JobRequestId,
                CandidateId,
                GeneratedByUserId,
                Version,
                Status,
                CompensationText,
                StartDate,
                ReportingManager,
                WorkLocation,
                Body,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @OfferLetterId,
                @TenantId,
                @JobApplicationId,
                @JobPostId,
                @JobRequestId,
                @CandidateId,
                @GeneratedByUserId,
                1,
                N'Draft',
                @CompensationText,
                @StartDate,
                @ReportingManager,
                @WorkLocation,
                @Body,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            new
            {
                OfferLetterId = offerLetterId,
                TenantId = tenantId,
                JobApplicationId = jobApplicationId,
                access.JobPostId,
                access.JobRequestId,
                access.CandidateId,
                GeneratedByUserId = actorUserId,
                input.CompensationText,
                StartDate = input.StartDate?.ToDateTime(TimeOnly.MinValue),
                input.ReportingManager,
                input.WorkLocation,
                Body = body
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "offer_letter.generated",
            "JobApplication",
            jobApplicationId,
            access.RequestCode,
            $"Offer letter draft generated for {access.CandidateName}.",
            "Offer",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await ReadOfferLetterByIdAsync(connection, tenantId, offerLetterId, cancellationToken);
    }

    public async Task<OfferLetterDetails?> UpdateOfferLetterAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid offerLetterId,
        UpdateOfferLetterInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var access = await ReadOfferAccessContextAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            includeAllTenantReviews,
            offerLetterId,
            cancellationToken);
        if (access is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        const string updateSql = """
            UPDATE dbo.OfferLetters
            SET Body = @Body,
                CompensationText = @CompensationText,
                StartDate = @StartDate,
                ReportingManager = @ReportingManager,
                WorkLocation = @WorkLocation,
                Status = CASE WHEN @Status IN (N'Draft', N'Presented', N'Accepted', N'Declined', N'Cancelled') THEN @Status ELSE Status END,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND OfferLetterId = @OfferLetterId;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new
            {
                TenantId = tenantId,
                OfferLetterId = offerLetterId,
                input.Body,
                input.CompensationText,
                StartDate = input.StartDate?.ToDateTime(TimeOnly.MinValue),
                input.ReportingManager,
                input.WorkLocation,
                Status = input.Status
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "offer_letter.updated",
            "OfferLetter",
            offerLetterId,
            access.RequestCode,
            $"Offer letter draft updated for {access.CandidateName}.",
            "Offer",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await ReadOfferLetterByIdAsync(connection, tenantId, offerLetterId, cancellationToken);
    }

    public async Task<OfferPresentationMeetingDetails?> ScheduleOfferPresentationMeetingAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid offerLetterId,
        ScheduleOfferPresentationMeetingInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var access = await ReadOfferAccessContextAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            includeAllTenantReviews,
            offerLetterId,
            cancellationToken);
        if (access is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var meetingId = Guid.NewGuid();
        const string insertSql = """
            INSERT INTO dbo.OfferPresentationMeetings
            (
                OfferPresentationMeetingId,
                TenantId,
                OfferLetterId,
                JobApplicationId,
                ScheduledByUserId,
                MeetingAtUtc,
                LocationText,
                Notes,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @OfferPresentationMeetingId,
                @TenantId,
                @OfferLetterId,
                @JobApplicationId,
                @ScheduledByUserId,
                @MeetingAtUtc,
                @LocationText,
                @Notes,
                N'Scheduled',
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            new
            {
                OfferPresentationMeetingId = meetingId,
                TenantId = tenantId,
                OfferLetterId = offerLetterId,
                access.JobApplicationId,
                ScheduledByUserId = actorUserId,
                MeetingAtUtc = input.MeetingAtUtc.UtcDateTime,
                input.LocationText,
                input.Notes
            },
            transaction,
            cancellationToken: cancellationToken));

        await QueueOfferPresentationMeetingEmailAsync(
            connection,
            transaction,
            tenantId,
            access,
            meetingId,
            input,
            cancellationToken);

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "offer_meeting.scheduled",
            "OfferLetter",
            offerLetterId,
            access.RequestCode,
            $"Offer presentation meeting scheduled for {access.CandidateName}.",
            "Offer",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await ReadOfferPresentationMeetingByIdAsync(connection, tenantId, meetingId, cancellationToken);
    }

    public async Task<HiringOutcomeResult?> RecordHiringOutcomeAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobApplicationId,
        HiringOutcomeInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var access = await ReadHiringReviewAccessContextAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            includeAllTenantReviews,
            jobApplicationId,
            cancellationToken);
        if (access is null || !IsOfferEligibleStatus(access.ApplicationStatus))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var oldStatus = access.ApplicationStatus;
        var newStatus = input.Outcome;
        var latestOffer = await ReadLatestOfferLetterAsync(connection, transaction, tenantId, jobApplicationId, cancellationToken);
        if ((newStatus is "Hired" or "Joined") && latestOffer is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await UpdateLatestOfferLetterOutcomeAsync(
            connection,
            transaction,
            tenantId,
            jobApplicationId,
            newStatus,
            input.JoiningDate,
            cancellationToken);
        await UpdateApplicationStatusAsync(connection, transaction, tenantId, jobApplicationId, oldStatus, newStatus, actorUserId, input.Reason, cancellationToken);
        var requestAlreadyClosed = string.Equals(access.RequestStatus, "Closed", StringComparison.OrdinalIgnoreCase);

        var jobRequestStatus = newStatus switch
        {
            "Offered" => "Offer",
            "Hired" or "Joined" => "Closed",
            "OfferDeclined" or "Rejected" or "OnHold" => "Sourcing",
            _ => "HiringManagerReview"
        };

        if (newStatus is "Hired" or "Joined")
        {
            await InsertExternalFulfillmentAsync(connection, transaction, tenantId, access, actorUserId, cancellationToken);
            await RefreshJobRequestFulfilledPositionsAsync(connection, transaction, tenantId, access.JobRequestId, cancellationToken);
        }

        var progress = await ReadJobRequestFulfillmentProgressAsync(connection, transaction, tenantId, access.JobRequestId, cancellationToken);
        if (requestAlreadyClosed)
        {
            jobRequestStatus = "Closed";
        }
        else if ((newStatus is "Hired" or "Joined") && progress.FulfilledPositions >= progress.RequiredPositions)
        {
            await CloseJobRequestAsync(connection, transaction, tenantId, access.JobRequestId, cancellationToken);
            await CloseJobPostsForRequestAsync(connection, transaction, tenantId, access.JobRequestId, cancellationToken);
            jobRequestStatus = "Closed";
        }
        else if (newStatus is "Hired" or "Joined")
        {
            await ReturnJobRequestToSourcingAsync(connection, transaction, tenantId, access.JobRequestId, actorUserId, cancellationToken);
            jobRequestStatus = "Sourcing";
        }
        else if (newStatus is "OfferDeclined" or "Rejected" or "OnHold")
        {
            await ReturnJobRequestToSourcingAsync(connection, transaction, tenantId, access.JobRequestId, actorUserId, cancellationToken);
            jobRequestStatus = "Sourcing";
        }
        else if (newStatus == "Offered")
        {
            await UpdateJobRequestStatusOnlyAsync(connection, transaction, tenantId, access.JobRequestId, "Offer", "OFFER", cancellationToken);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "hiring_outcome.recorded",
            "JobApplication",
            jobApplicationId,
            access.RequestCode,
            $"Hiring outcome for {access.CandidateName} recorded as {newStatus}.",
            "Offer",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new HiringOutcomeResult(
            jobApplicationId,
            access.JobRequestId,
            newStatus,
            jobRequestStatus,
            input.JoiningDate,
            progress.FulfilledPositions,
            progress.RequiredPositions);
    }

    public async Task<bool> CloseJobRequestAsync(
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobRequestId,
        CloseJobRequestInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await CanCloseHiringJobRequestAsync(connection, transaction, tenantId, actorUserId, includeAllTenantReviews, jobRequestId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await CloseJobRequestAsync(connection, transaction, tenantId, jobRequestId, cancellationToken);
        await CloseJobPostsForRequestAsync(connection, transaction, tenantId, jobRequestId, cancellationToken);

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "job_request.closed_by_hiring_manager",
            "JobRequest",
            jobRequestId,
            jobRequestId.ToString("D"),
            input.Reason,
            "Hiring Manager Review",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<CreateOperationsJobRequestRepositoryResult> CreateJobRequestAsync(
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
                """
                SELECT COALESCE(MAX(
                    CASE
                        WHEN RequestCode LIKE N'TP-REQ-%'
                        THEN TRY_CONVERT(INT, SUBSTRING(RequestCode, LEN(N'TP-REQ-') + 1, 20))
                        ELSE NULL
                    END), 0) + 1
                FROM dbo.JobRequests WITH (UPDLOCK, HOLDLOCK)
                WHERE TenantId = @TenantId;
                """,
                new { TenantId = tenantId },
                transaction,
                cancellationToken: cancellationToken));
        var requestCode = $"TP-REQ-{requestNumber:000}";

        var workflowIds = await ReadWorkflowIdsAsync(connection, transaction, tenantId, cancellationToken);
        var actorRoleCodes = await ReadActorRoleCodesAsync(connection, transaction, tenantId, actorUserId, cancellationToken);
        var isPmoCreated = actorRoleCodes.Contains(AccessConstants.PmoRoleCode, StringComparer.OrdinalIgnoreCase);
        var assignmentTarget = isPmoCreated
            ? WorkflowAssignmentTarget.ForUser(actorUserId, "PMO creator")
            : await ResolveDepartmentIntakeAssignmentAsync(
                connection,
                transaction,
                tenantId,
                input.DepartmentId,
                cancellationToken);

        const string insertJobRequestSql = """
            INSERT INTO dbo.JobRequests
            (
                JobRequestId,
                TenantId,
                RequestCode,
                Title,
                Description,
                ClientName,
                ClientContext,
                DepartmentId,
                LocationId,
                EmploymentType,
                ExperienceMinYears,
                ExperienceMaxYears,
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
                @ClientContext,
                @DepartmentId,
                @LocationId,
                N'FullTime',
                @ExperienceMinYears,
                @ExperienceMaxYears,
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
                ClientContext = NullIfBlank(input.ClientContext),
                input.DepartmentId,
                input.LocationId,
                input.ExperienceMinYears,
                input.ExperienceMaxYears,
                Priority = NormalizePriority(input.Priority),
                input.RequiredPositions,
                HiringManagerUserId = input.HiringManagerId,
                CreatedByUserId = actorUserId,
                Now = now
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertJobRequestSkillsAsync(connection, transaction, tenantId, jobRequestId, input.SkillIds, cancellationToken);

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
                AssignedToUserId,
                AssignedToGroupId,
                AssignedToRoleId,
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
                @AssignedToUserId,
                @AssignedToGroupId,
                @AssignedToRoleId,
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
                assignmentTarget.AssignedToUserId,
                assignmentTarget.AssignedToGroupId,
                assignmentTarget.AssignedToRoleId,
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
            BuildJobRequestCreatedAuditSummary(requestCode, assignmentTarget),
            "Talent Pilot App",
            cancellationToken);

        IReadOnlyList<OperationsNotificationDispatch> notificationDispatches = [];
        if (!isPmoCreated)
        {
            var isTenantAdminFallback = assignmentTarget.Source == "Tenant Admin fallback";
            var requesterName = await ReadUserDisplayNameAsync(
                connection,
                transaction,
                tenantId,
                actorUserId,
                cancellationToken);
            var recipients = await ResolveNotificationRecipientsAsync(
                connection,
                transaction,
                tenantId,
                assignmentTarget,
                cancellationToken);
            var notificationContent = isTenantAdminFallback
                ? await BuildMissingDepartmentRouteNotificationContentAsync(
                    connection,
                    transaction,
                    tenantId,
                    input.DepartmentId,
                    requestCode,
                    input.Title,
                    requesterName,
                    cancellationToken)
                : await ReadNotificationContentAsync(
                    connection,
                    transaction,
                    tenantId,
                    NotificationEventCodes.PresalesRequestSubmitted,
                    requestCode,
                    input.Title,
                    requesterName,
                    cancellationToken);

            if (notificationContent is not null && recipients.Count > 0)
            {
                if (isTenantAdminFallback)
                {
                    await InsertNotificationEmailOutboxAsync(
                        connection,
                        transaction,
                        tenantId,
                        notificationContent,
                        recipients,
                        jobRequestId,
                        input.Title,
                        requesterName,
                        requestCode,
                        now,
                        cancellationToken);
                }
                else
                {
                    notificationDispatches = await QueueAndBuildDispatchesAsync(
                        connection,
                        transaction,
                        tenantId,
                        notificationContent,
                        recipients,
                        jobRequestId,
                        input.Title,
                        requesterName,
                        requestCode,
                        now,
                        cancellationToken);
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);

        var jobRequest = await GetJobRequestByIdAsync(connection, tenantId, actorUserId, jobRequestId, cancellationToken)
            ?? throw new InvalidOperationException("Created job request could not be reloaded.");
        var assignment = await GetAssignmentByIdAsync(connection, tenantId, actorUserId, assignmentId, cancellationToken)
            ?? throw new InvalidOperationException("Created workflow assignment could not be reloaded.");

        return new CreateOperationsJobRequestRepositoryResult(
            new CreateOperationsJobRequestResult(jobRequest, assignment),
            notificationDispatches);
    }

    public async Task<bool> ClaimAssignmentAsync(Guid tenantId, Guid actorUserId, Guid assignmentId, CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @ChangedAssignments TABLE
            (
                EntityType NVARCHAR(64) NOT NULL,
                EntityId UNIQUEIDENTIFIER NOT NULL
            );

            UPDATE wa
            SET AssignmentStatus = N'Claimed',
                ClaimedByUserId = @ActorUserId,
                AssignedToUserId = @ActorUserId,
                ClaimedAtUtc = SYSUTCDATETIME()
            OUTPUT inserted.EntityType, inserted.EntityId INTO @ChangedAssignments
            FROM dbo.WorkflowAssignments AS wa
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
                      INNER JOIN dbo.Groups AS g
                          ON g.TenantId = gm.TenantId
                          AND g.GroupId = gm.GroupId
                          AND g.Status = N'Active'
                      WHERE gm.TenantId = wa.TenantId
                        AND gm.GroupId = wa.AssignedToGroupId
                        AND gm.UserId = @ActorUserId
                  )
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.UserRoles AS ur
                      INNER JOIN dbo.Roles AS r
                          ON r.TenantId = ur.TenantId
                          AND r.RoleId = ur.RoleId
                          AND r.Status = N'Active'
                      WHERE ur.TenantId = wa.TenantId
                        AND ur.UserId = @ActorUserId
                        AND r.Code = @TenantAdminRoleCode
                  )
              );

            SELECT COUNT(1) FROM @ChangedAssignments;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                AssignmentId = assignmentId,
                TenantAdminRoleCode = AccessConstants.TenantAdminRoleCode
            },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<OperationsMutationRepositoryResult> CreateEmployeeReferralsAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CreateEmployeeReferralsInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var assignmentId = await ReadCurrentAssignmentForActionAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            jobRequestId,
            "PMO_REVIEW",
            requireClaimedForGroups: true,
            cancellationToken);
        if (!assignmentId.HasValue)
        {
            return new OperationsMutationRepositoryResult(false, []);
        }

        var presalesUser = await ReadActiveRoleUserAsync(
            connection,
            transaction,
            tenantId,
            input.PresalesUserId,
            "Presales",
            cancellationToken);
        if (presalesUser is null)
        {
            return new OperationsMutationRepositoryResult(false, []);
        }

        var employeeIds = await ReadEligibleEmployeeIdsAsync(
            connection,
            transaction,
            tenantId,
            input.EmployeeIds,
            cancellationToken);
        if (employeeIds.Count == 0)
        {
            return new OperationsMutationRepositoryResult(false, []);
        }

        var now = DateTimeOffset.UtcNow;
        var context = await ReadJobRequestContextAsync(connection, transaction, tenantId, jobRequestId, cancellationToken);
        if (context is null)
        {
            return new OperationsMutationRepositoryResult(false, []);
        }

        const string insertReferralSql = """
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
            SELECT
                NEWID(),
                @TenantId,
                @JobRequestId,
                @EmployeeId,
                @ReferredByUserId,
                @PresalesUserId,
                N'Referred',
                (
                    SELECT TOP (1) recommendation.Score
                    FROM dbo.AiRecommendationLogs AS recommendation
                    WHERE recommendation.TenantId = @TenantId
                      AND recommendation.AiAgentDefinitionId = N'bench-matching'
                      AND recommendation.SourceEntityType = N'JobRequest'
                      AND recommendation.SourceEntityId = @JobRequestId
                      AND recommendation.RecommendedEntityType = N'Employee'
                      AND recommendation.RecommendedEntityId = @EmployeeId
                    ORDER BY COALESCE(recommendation.UpdatedAtUtc, recommendation.CreatedAtUtc) DESC
                ),
                @RecommendationSummary,
                @Now,
                @Now
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.JobRequestEmployeeReferrals AS existing
                WHERE existing.TenantId = @TenantId
                  AND existing.JobRequestId = @JobRequestId
                  AND existing.EmployeeId = @EmployeeId
                  AND existing.Status IN (N'Referred', N'AcceptedByPresales', N'ClientAccepted')
            );
            """;

        foreach (var employeeId in employeeIds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertReferralSql,
                new
                {
                    TenantId = tenantId,
                    JobRequestId = jobRequestId,
                    EmployeeId = employeeId,
                    ReferredByUserId = actorUserId,
                    input.PresalesUserId,
                    RecommendationSummary = Truncate(input.RecommendationSummary?.Trim() ?? string.Empty, 1500),
                    Now = now.UtcDateTime
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        var workflowDefinitionId = await ReadWorkflowDefinitionIdAsync(connection, transaction, tenantId, cancellationToken);
        var presalesStageId = await ReadWorkflowStageIdAsync(connection, transaction, tenantId, "PRESALES_REVIEW", cancellationToken);
        var transitionId = await ReadWorkflowTransitionIdAsync(
            connection,
            transaction,
            tenantId,
            "RECOMMEND_EMPLOYEES_TO_PRESALES",
            cancellationToken);
        var presalesAssignmentId = Guid.NewGuid();

        await CompleteAssignmentAsync(connection, transaction, tenantId, assignmentId.Value, cancellationToken);
        await InsertWorkflowAssignmentAsync(
            connection,
            transaction,
            tenantId,
            workflowDefinitionId,
            presalesStageId,
            transitionId,
            presalesAssignmentId,
            jobRequestId,
            WorkflowAssignmentTarget.ForUser(input.PresalesUserId, "Presales recommendation review"),
            now,
            cancellationToken);
        await UpdateJobRequestStageAsync(
            connection,
            transaction,
            tenantId,
            jobRequestId,
            "PresalesReview",
            "PRESALES_REVIEW",
            presalesAssignmentId,
            cancellationToken);

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "job_request.employee_referred",
            "JobRequest",
            jobRequestId,
            context.RequestCode,
            $"{employeeIds.Count} internal employee recommendation(s) were sent to {presalesUser.DisplayName}.",
            "Talent Pilot App",
            cancellationToken);

        var actorName = await ReadUserDisplayNameAsync(connection, transaction, tenantId, actorUserId, cancellationToken);
        var notificationContent = await ReadNotificationContentAsync(
            connection,
            transaction,
            tenantId,
            NotificationEventCodes.PmoEmployeeReferred,
            context.RequestCode,
            context.Title,
            actorName,
            cancellationToken,
            new Dictionary<string, string>
            {
                ["employeeName"] = employeeIds.Count == 1
                    ? await ReadEmployeeDisplayNameAsync(connection, transaction, tenantId, employeeIds[0], cancellationToken)
                    : $"{employeeIds.Count} employees"
            });
        var recipients = new[] { new NotificationRecipientRow(presalesUser.UserId, presalesUser.Email) };
        var dispatches = await QueueAndBuildDispatchesAsync(
            connection,
            transaction,
            tenantId,
            notificationContent,
            recipients,
            jobRequestId,
            context.Title,
            actorName,
            context.RequestCode,
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new OperationsMutationRepositoryResult(true, dispatches);
    }

    public async Task<OperationsMutationRepositoryResult> ForwardToRecruitersAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var assignmentId = await ReadCurrentAssignmentForActionAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            jobRequestId,
            "PMO_REVIEW",
            requireClaimedForGroups: true,
            cancellationToken);
        if (!assignmentId.HasValue)
        {
            return new OperationsMutationRepositoryResult(false, []);
        }

        var context = await ReadJobRequestContextAsync(connection, transaction, tenantId, jobRequestId, cancellationToken);
        if (context is null)
        {
            return new OperationsMutationRepositoryResult(false, []);
        }

        var now = DateTimeOffset.UtcNow;
        var workflowDefinitionId = await ReadWorkflowDefinitionIdAsync(connection, transaction, tenantId, cancellationToken);
        var sourcingStageId = await ReadWorkflowStageIdAsync(connection, transaction, tenantId, "SOURCING", cancellationToken);
        var transitionId = await ReadWorkflowTransitionIdAsync(
            connection,
            transaction,
            tenantId,
            "FORWARD_TO_RECRUITER",
            cancellationToken);
        var recruiterTarget = await ResolveWorkflowRoutingAssignmentAsync(
            connection,
            transaction,
            tenantId,
            "FORWARD_TO_RECRUITER",
            cancellationToken);
        var recruiterAssignmentId = Guid.NewGuid();

        await CompleteAssignmentAsync(connection, transaction, tenantId, assignmentId.Value, cancellationToken);
        await InsertWorkflowAssignmentAsync(
            connection,
            transaction,
            tenantId,
            workflowDefinitionId,
            sourcingStageId,
            transitionId,
            recruiterAssignmentId,
            jobRequestId,
            recruiterTarget,
            now,
            cancellationToken);
        await UpdateJobRequestStageAsync(
            connection,
            transaction,
            tenantId,
            jobRequestId,
            "Sourcing",
            "SOURCING",
            recruiterAssignmentId,
            cancellationToken);

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "job_request.forwarded_to_recruiters",
            "JobRequest",
            jobRequestId,
            context.RequestCode,
            $"{context.RequestCode} was forwarded to recruiters after PMO review.",
            "Talent Pilot App",
            cancellationToken);

        var actorName = await ReadUserDisplayNameAsync(connection, transaction, tenantId, actorUserId, cancellationToken);
        var notificationContent = await ReadNotificationContentAsync(
            connection,
            transaction,
            tenantId,
            NotificationEventCodes.PmoForwardedToRecruiting,
            context.RequestCode,
            context.Title,
            actorName,
            cancellationToken);
        var recipients = await ResolveNotificationRecipientsAsync(connection, transaction, tenantId, recruiterTarget, cancellationToken);
        var dispatches = await QueueAndBuildDispatchesAsync(
            connection,
            transaction,
            tenantId,
            notificationContent,
            recipients,
            jobRequestId,
            context.Title,
            actorName,
            context.RequestCode,
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new OperationsMutationRepositoryResult(true, dispatches);
    }

    public async Task<OperationsMutationRepositoryResult> DecideEmployeeReferralsAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        EmployeeReferralDecisionInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var assignmentId = await ReadCurrentAssignmentForActionAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            jobRequestId,
            "PRESALES_REVIEW",
            requireClaimedForGroups: false,
            cancellationToken);
        if (!assignmentId.HasValue)
        {
            return new OperationsMutationRepositoryResult(false, []);
        }

        var context = await ReadJobRequestContextAsync(connection, transaction, tenantId, jobRequestId, cancellationToken);
        if (context is null)
        {
            return new OperationsMutationRepositoryResult(false, []);
        }

        var now = DateTimeOffset.UtcNow;
        var acceptedReferralIds = new List<Guid>();
        var rejectedCount = 0;
        Guid? pmoUserId = null;
        foreach (var decision in input.Decisions)
        {
            var accepted = string.Equals(decision.Decision, "Accept", StringComparison.OrdinalIgnoreCase);
            var newStatus = accepted ? "AcceptedByPresales" : "RejectedByPresales";
            const string updateReferralSql = """
                UPDATE dbo.JobRequestEmployeeReferrals
                SET Status = @Status,
                    ClientFeedback = NULLIF(@ClientFeedback, N''),
                    UpdatedAtUtc = @Now
                OUTPUT inserted.JobRequestEmployeeReferralId, inserted.EmployeeId, inserted.ReferredByUserId
                WHERE TenantId = @TenantId
                  AND JobRequestId = @JobRequestId
                  AND JobRequestEmployeeReferralId = @ReferralId
                  AND Status = N'Referred'
                  AND
                  (
                      PresalesUserId = @ActorUserId
                      OR EXISTS
                      (
                          SELECT 1
                          FROM dbo.UserRoles AS ur
                          INNER JOIN dbo.Roles AS r
                              ON r.TenantId = ur.TenantId
                              AND r.RoleId = ur.RoleId
                              AND r.Code = @TenantAdminRoleCode
                              AND r.Status = N'Active'
                          WHERE ur.TenantId = @TenantId
                            AND ur.UserId = @ActorUserId
                      )
                  );
                """;

            var row = await connection.QuerySingleOrDefaultAsync<ReferralDecisionRow>(new CommandDefinition(
                updateReferralSql,
                new
                {
                    TenantId = tenantId,
                    JobRequestId = jobRequestId,
                    decision.ReferralId,
                    ActorUserId = actorUserId,
                    TenantAdminRoleCode = AccessConstants.TenantAdminRoleCode,
                    Status = newStatus,
                    ClientFeedback = Truncate(decision.Feedback?.Trim() ?? string.Empty, 1000),
                    Now = now.UtcDateTime
                },
                transaction,
                cancellationToken: cancellationToken));

            if (row is null)
            {
                continue;
            }

            pmoUserId ??= row.ReferredByUserId;
            if (accepted)
            {
                acceptedReferralIds.Add(row.JobRequestEmployeeReferralId);
                await InsertInternalFulfillmentAsync(
                    connection,
                    transaction,
                    tenantId,
                    jobRequestId,
                    row.JobRequestEmployeeReferralId,
                    row.EmployeeId,
                    actorUserId,
                    now,
                    cancellationToken);
            }
            else
            {
                rejectedCount++;
            }
        }

        if (acceptedReferralIds.Count == 0 && rejectedCount == 0)
        {
            return new OperationsMutationRepositoryResult(false, []);
        }

        await RefreshJobRequestFulfilledPositionsAsync(connection, transaction, tenantId, jobRequestId, cancellationToken);
        var fulfillment = await ReadJobRequestFulfillmentProgressAsync(connection, transaction, tenantId, jobRequestId, cancellationToken);
        await CompleteAssignmentAsync(connection, transaction, tenantId, assignmentId.Value, cancellationToken);

        if (fulfillment.FulfilledPositions >= fulfillment.RequiredPositions)
        {
            await CloseJobRequestAsync(connection, transaction, tenantId, jobRequestId, cancellationToken);
        }
        else
        {
            var workflowDefinitionId = await ReadWorkflowDefinitionIdAsync(connection, transaction, tenantId, cancellationToken);
            var pmoStageId = await ReadWorkflowStageIdAsync(connection, transaction, tenantId, "PMO_REVIEW", cancellationToken);
            var transitionId = await ReadWorkflowTransitionIdAsync(
                connection,
                transaction,
                tenantId,
                "PRESALES_RETURN_TO_PMO",
                cancellationToken);
            var pmoAssignmentId = Guid.NewGuid();
            var target = pmoUserId.HasValue
                ? WorkflowAssignmentTarget.ForUser(pmoUserId.Value, "PMO referral owner")
                : await TenantAdminFallbackAsync(connection, transaction, tenantId, cancellationToken);

            await InsertWorkflowAssignmentAsync(
                connection,
                transaction,
                tenantId,
                workflowDefinitionId,
                pmoStageId,
                transitionId,
                pmoAssignmentId,
                jobRequestId,
                target,
                now,
                cancellationToken);
            await UpdateJobRequestStageAsync(
                connection,
                transaction,
                tenantId,
                jobRequestId,
                "PMOReview",
                "PMO_REVIEW",
                pmoAssignmentId,
                cancellationToken);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "job_request.employee_referral_decision",
            "JobRequest",
            jobRequestId,
            context.RequestCode,
            $"Presales reviewed employee recommendations: {acceptedReferralIds.Count} accepted, {rejectedCount} rejected.",
            "Talent Pilot App",
            cancellationToken);

        IReadOnlyList<OperationsNotificationDispatch> dispatches = [];
        if (pmoUserId.HasValue)
        {
            var actorName = await ReadUserDisplayNameAsync(connection, transaction, tenantId, actorUserId, cancellationToken);
            var eventCode = acceptedReferralIds.Count > 0
                ? NotificationEventCodes.PresalesEmployeeReferralAccepted
                : NotificationEventCodes.PresalesEmployeeReferralRejected;
            var notificationContent = await ReadNotificationContentAsync(
                connection,
                transaction,
                tenantId,
                eventCode,
                context.RequestCode,
                context.Title,
                actorName,
                cancellationToken,
                new Dictionary<string, string>
                {
                    ["acceptedCount"] = acceptedReferralIds.Count.ToString(),
                    ["rejectedCount"] = rejectedCount.ToString()
                });
            var pmoRecipient = await ReadNotificationRecipientAsync(
                connection,
                transaction,
                tenantId,
                pmoUserId.Value,
                cancellationToken);
            if (pmoRecipient is not null)
            {
                dispatches = await QueueAndBuildDispatchesAsync(
                    connection,
                    transaction,
                    tenantId,
                    notificationContent,
                    [pmoRecipient],
                    jobRequestId,
                    context.Title,
                    actorName,
                    context.RequestCode,
                    now,
                    cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new OperationsMutationRepositoryResult(true, dispatches);
    }

    public async Task<OperationsJobPost?> CreateJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CreateJobPostInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var assignmentId = await ReadCurrentAssignmentForActionAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            jobRequestId,
            "SOURCING",
            requireClaimedForGroups: true,
            cancellationToken);
        if (!assignmentId.HasValue)
        {
            return null;
        }

        var request = await ReadJobPostRequestDefaultsAsync(connection, transaction, tenantId, jobRequestId, cancellationToken);
        if (request is null ||
            await ReadJobPostIdByRequestIdAsync(connection, transaction, tenantId, jobRequestId, cancellationToken) is not null ||
            !await InterviewTemplateIsAvailableAsync(connection, transaction, tenantId, input.InterviewTemplateId, request.DepartmentId, cancellationToken) ||
            !await JobPostSkillsAreActiveAsync(connection, transaction, tenantId, input.SkillIds, cancellationToken))
        {
            return null;
        }

        var jobPostId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        const string insertSql = """
            INSERT INTO dbo.JobPosts
            (
                JobPostId,
                TenantId,
                JobRequestId,
                RecruiterOwnerUserId,
                Title,
                Description,
                DepartmentId,
                LocationId,
                ExperienceMinYears,
                ExperienceMaxYears,
                RequiredPositions,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @JobPostId,
                @TenantId,
                @JobRequestId,
                @RecruiterOwnerUserId,
                @Title,
                @Description,
                @DepartmentId,
                @LocationId,
                @ExperienceMinYears,
                @ExperienceMaxYears,
                @RequiredPositions,
                N'Draft',
                @Now,
                @Now
            );

            UPDATE dbo.JobRequests
            SET PublishStatus = N'NotPublished',
                UpdatedAtUtc = @Now
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            new
            {
                JobPostId = jobPostId,
                TenantId = tenantId,
                JobRequestId = jobRequestId,
                RecruiterOwnerUserId = actorUserId,
                Title = Truncate(input.Title.Trim(), 200),
                Description = input.Description.Trim(),
                request.DepartmentId,
                request.LocationId,
                input.ExperienceMinYears,
                input.ExperienceMaxYears,
                input.RequiredPositions,
                Now = now.UtcDateTime
            },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceJobPostSkillsAsync(connection, transaction, tenantId, jobPostId, input.SkillIds, cancellationToken);
        await ReplaceJobPostInterviewRoundsAsync(connection, transaction, tenantId, jobPostId, input.InterviewRounds, cancellationToken);
        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "job_post.created",
            "JobRequest",
            jobRequestId,
            request.RequestCode,
            $"Recruiter created a draft job post for {request.RequestCode}.",
            "Talent Pilot App",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await ReadJobPostByIdAsync(connection, tenantId, jobPostId, cancellationToken);
    }

    public async Task<OperationsJobPost?> UpdateJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        UpdateJobPostInput input,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await ReadJobPostAccessContextAsync(connection, transaction, tenantId, jobPostId, cancellationToken);
        if (context is null ||
            !string.Equals(context.Status, "Draft", StringComparison.OrdinalIgnoreCase) ||
            !await JobPostSkillsAreActiveAsync(connection, transaction, tenantId, input.SkillIds, cancellationToken) ||
            !await CanMutateRecruiterSourcingAsync(connection, transaction, tenantId, actorUserId, context.JobRequestId, cancellationToken))
        {
            return null;
        }

        const string updateSql = """
            UPDATE dbo.JobPosts
            SET Title = @Title,
                Description = @Description,
                ExperienceMinYears = @ExperienceMinYears,
                ExperienceMaxYears = @ExperienceMaxYears,
                RequiredPositions = @RequiredPositions,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobPostId = @JobPostId
              AND Status = N'Draft';
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new
            {
                TenantId = tenantId,
                JobPostId = jobPostId,
                Title = Truncate(input.Title.Trim(), 200),
                Description = input.Description.Trim(),
                input.ExperienceMinYears,
                input.ExperienceMaxYears,
                input.RequiredPositions
            },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceJobPostSkillsAsync(connection, transaction, tenantId, jobPostId, input.SkillIds, cancellationToken);
        await ReplaceJobPostInterviewRoundsAsync(connection, transaction, tenantId, jobPostId, input.InterviewRounds, cancellationToken);
        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "job_post.updated",
            "JobRequest",
            context.JobRequestId,
            context.RequestCode,
            $"Recruiter updated the draft job post for {context.RequestCode}.",
            "Talent Pilot App",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await ReadJobPostByIdAsync(connection, tenantId, jobPostId, cancellationToken);
    }

    public async Task<OperationsJobPost?> PublishJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await ReadJobPostAccessContextAsync(connection, transaction, tenantId, jobPostId, cancellationToken);
        if (context is null ||
            !string.Equals(context.Status, "Draft", StringComparison.OrdinalIgnoreCase) ||
            !await CanMutateRecruiterSourcingAsync(connection, transaction, tenantId, actorUserId, context.JobRequestId, cancellationToken) ||
            !await JobPostHasPublishableContentAsync(connection, transaction, tenantId, jobPostId, cancellationToken))
        {
            return null;
        }

        const string sql = """
            UPDATE dbo.JobPosts
            SET Status = N'Published',
                PublishedAtUtc = COALESCE(PublishedAtUtc, SYSUTCDATETIME()),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobPostId = @JobPostId
              AND Status = N'Draft';

            UPDATE dbo.JobRequests
            SET PublishStatus = N'Published',
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId, context.JobRequestId },
            transaction,
            cancellationToken: cancellationToken));
        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "job_post.published",
            "JobRequest",
            context.JobRequestId,
            context.RequestCode,
            $"Recruiter published the Talent Pilot job post for {context.RequestCode}.",
            "Talent Pilot App",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await ReadJobPostByIdAsync(connection, tenantId, jobPostId, cancellationToken);
    }

    public async Task<OperationsJobPost?> CloseJobPostAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await ReadJobPostAccessContextAsync(connection, transaction, tenantId, jobPostId, cancellationToken);
        if (context is null ||
            string.Equals(context.Status, "Closed", StringComparison.OrdinalIgnoreCase) ||
            !await CanMutateRecruiterSourcingAsync(connection, transaction, tenantId, actorUserId, context.JobRequestId, cancellationToken))
        {
            return null;
        }

        const string sql = """
            UPDATE dbo.JobPosts
            SET Status = N'Closed',
                ClosedAtUtc = COALESCE(ClosedAtUtc, SYSUTCDATETIME()),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobPostId = @JobPostId
              AND Status <> N'Closed';

            UPDATE dbo.JobRequests
            SET PublishStatus = N'Closed',
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId, context.JobRequestId },
            transaction,
            cancellationToken: cancellationToken));
        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "job_post.closed",
            "JobRequest",
            context.JobRequestId,
            context.RequestCode,
            $"Recruiter closed the Talent Pilot job post for {context.RequestCode}.",
            "Talent Pilot App",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await ReadJobPostByIdAsync(connection, tenantId, jobPostId, cancellationToken);
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

    private static async Task<JobPostSummaryRow?> ReadJobPostSummaryByRequestIdAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                post.JobPostId,
                post.Status,
                recruiter.DisplayName AS RecruiterOwnerName,
                post.UpdatedAtUtc AS UpdatedAt
            FROM dbo.JobPosts AS post
            INNER JOIN dbo.AppUsers AS recruiter
                ON recruiter.TenantId = post.TenantId
                AND recruiter.UserId = post.RecruiterOwnerUserId
            WHERE post.TenantId = @TenantId
              AND post.JobRequestId = @JobRequestId
            ORDER BY post.UpdatedAtUtc DESC;
            """;

        return await connection.QuerySingleOrDefaultAsync<JobPostSummaryRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid?> ReadJobPostIdByRequestIdAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) JobPostId
            FROM dbo.JobPosts
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<OperationsJobPost?> ReadJobPostByRequestIdAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        var jobPostId = await ReadJobPostIdByRequestIdAsync(connection, null, tenantId, jobRequestId, cancellationToken);
        return jobPostId.HasValue
            ? await ReadJobPostByIdAsync(connection, tenantId, jobPostId.Value, cancellationToken)
            : null;
    }

    private static async Task<OperationsJobPost?> ReadJobPostByIdAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                post.JobPostId,
                post.JobRequestId,
                post.Title,
                post.Description,
                COALESCE(department.Name, N'Unassigned') AS Department,
                COALESCE(location.Name, N'Remote') AS Location,
                post.ExperienceMinYears,
                post.ExperienceMaxYears,
                post.RequiredPositions,
                post.Status,
                post.RecruiterOwnerUserId,
                recruiter.DisplayName AS RecruiterOwnerName,
                post.PublishedAtUtc AS PublishedAt,
                post.ClosedAtUtc AS ClosedAt,
                post.CreatedAtUtc AS CreatedAt,
                post.UpdatedAtUtc AS UpdatedAt
            FROM dbo.JobPosts AS post
            INNER JOIN dbo.AppUsers AS recruiter
                ON recruiter.TenantId = post.TenantId
                AND recruiter.UserId = post.RecruiterOwnerUserId
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = post.TenantId
                AND department.DepartmentId = post.DepartmentId
            LEFT JOIN dbo.Locations AS location
                ON location.TenantId = post.TenantId
                AND location.LocationId = post.LocationId
            WHERE post.TenantId = @TenantId
              AND post.JobPostId = @JobPostId;
            """;

        var row = await connection.QuerySingleOrDefaultAsync<JobPostRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId },
            cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        var skills = await ListJobPostSkillsAsync(connection, tenantId, jobPostId, cancellationToken);
        var rounds = await ListJobPostInterviewRoundsAsync(connection, tenantId, jobPostId, cancellationToken);
        return new OperationsJobPost(
            row.JobPostId,
            row.JobRequestId,
            row.Title,
            row.Description,
            row.Department,
            row.Location,
            row.ExperienceMinYears,
            row.ExperienceMaxYears,
            row.RequiredPositions,
            row.Status,
            row.RecruiterOwnerUserId,
            row.RecruiterOwnerName,
            ToUtc(row.PublishedAt),
            ToUtc(row.ClosedAt),
            Utc(row.CreatedAt),
            Utc(row.UpdatedAt),
            skills,
            rounds);
    }

    private static async Task<IReadOnlyList<OperationsJobPostSkill>> ListJobPostSkillsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT skill.SkillId, skill.Name, skill.Category
            FROM dbo.JobPostSkills AS postSkill
            INNER JOIN dbo.Skills AS skill
                ON skill.TenantId = postSkill.TenantId
                AND skill.SkillId = postSkill.SkillId
            WHERE postSkill.TenantId = @TenantId
              AND postSkill.JobPostId = @JobPostId
            ORDER BY skill.Name;
            """;

        var rows = await connection.QueryAsync<JobPostSkillRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId },
            cancellationToken: cancellationToken));

        return rows
            .Select(row => new OperationsJobPostSkill(row.SkillId, row.Name, row.Category))
            .ToArray();
    }

    private static async Task<IReadOnlyList<OperationsJobPostInterviewRound>> ListJobPostInterviewRoundsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                round.JobPostInterviewRoundId,
                round.InterviewTemplateRoundId,
                round.RoundOrder,
                round.Name,
                round.OwnerUserId,
                owner.DisplayName AS OwnerUserName,
                round.DurationMinutes,
                round.Status
            FROM dbo.JobPostInterviewRounds AS round
            LEFT JOIN dbo.AppUsers AS owner
                ON owner.TenantId = round.TenantId
                AND owner.UserId = round.OwnerUserId
            WHERE round.TenantId = @TenantId
              AND round.JobPostId = @JobPostId
            ORDER BY round.RoundOrder;
            """;

        var rows = await connection.QueryAsync<JobPostInterviewRoundRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId },
            cancellationToken: cancellationToken));

        return rows.Select(ToJobPostRound).ToArray();
    }

    private static OperationsJobPostInterviewRound ToJobPostRound(JobPostInterviewRoundRow row)
    {
        return new OperationsJobPostInterviewRound(
            row.JobPostInterviewRoundId,
            row.InterviewTemplateRoundId,
            row.RoundOrder,
            row.Name,
            row.OwnerUserId,
            row.OwnerUserName,
            row.DurationMinutes,
            row.Status);
    }

    private static async Task<IReadOnlyList<OperationsInterviewTemplateOption>> ListInterviewTemplatesAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string templatesSql = """
            SELECT
                template.InterviewTemplateId,
                template.Name,
                COALESCE(department.Name, N'All departments') AS DepartmentName,
                COALESCE(template.Description, N'') AS Description
            FROM dbo.InterviewTemplates AS template
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = template.TenantId
                AND department.DepartmentId = template.DepartmentId
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = template.TenantId
                AND request.JobRequestId = @JobRequestId
            WHERE template.TenantId = @TenantId
              AND template.Status = N'Active'
              AND (template.DepartmentId IS NULL OR template.DepartmentId = request.DepartmentId)
            ORDER BY CASE WHEN template.DepartmentId = request.DepartmentId THEN 0 ELSE 1 END, template.Name;
            """;

        const string roundsSql = """
            SELECT
                CAST(NULL AS UNIQUEIDENTIFIER) AS JobPostInterviewRoundId,
                round.InterviewTemplateRoundId,
                round.RoundOrder,
                round.Name,
                round.OwnerUserId,
                owner.DisplayName AS OwnerUserName,
                round.DurationMinutes,
                round.Status
            FROM dbo.InterviewTemplateRounds AS round
            LEFT JOIN dbo.AppUsers AS owner
                ON owner.TenantId = round.TenantId
                AND owner.UserId = round.OwnerUserId
            WHERE round.TenantId = @TenantId
              AND round.InterviewTemplateId = @InterviewTemplateId
            ORDER BY round.RoundOrder;
            """;

        var templates = (await connection.QueryAsync<InterviewTemplateRow>(new CommandDefinition(
            templatesSql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken))).ToArray();
        var results = new List<OperationsInterviewTemplateOption>(templates.Length);
        foreach (var template in templates)
        {
            var rounds = await connection.QueryAsync<JobPostInterviewRoundRow>(new CommandDefinition(
                roundsSql,
                new { TenantId = tenantId, template.InterviewTemplateId },
                cancellationToken: cancellationToken));
            results.Add(new OperationsInterviewTemplateOption(
                template.InterviewTemplateId,
                template.Name,
                template.DepartmentName,
                template.Description,
                rounds.Select(ToJobPostRound).ToArray()));
        }

        return results;
    }

    private static async Task<IReadOnlyList<OperationsInterviewerOption>> ListInterviewerOptionsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.UserId,
                u.DisplayName,
                u.Email,
                employee.DepartmentId,
                department.Name AS DepartmentName,
                employee.Designation,
                COALESCE(roleNames.RoleNamesCsv, N'') AS RoleNamesCsv,
                COALESCE(interviewStats.CompletedInterviewCount, 0) AS CompletedInterviewCount,
                CAST(CASE WHEN employee.DepartmentId = request.DepartmentId THEN 1 ELSE 0 END AS bit) AS IsJobDepartmentMatch,
                CAST(CASE WHEN hodRole.UserId IS NULL THEN 0 ELSE 1 END AS bit) AS IsDepartmentHod
            FROM dbo.JobRequests AS request
            INNER JOIN dbo.Employees AS employee
                ON employee.TenantId = request.TenantId
                AND employee.AppUserId IS NOT NULL
                AND employee.Status = N'Active'
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = employee.TenantId
                AND u.UserId = employee.AppUserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = employee.TenantId
                AND department.DepartmentId = employee.DepartmentId
            OUTER APPLY
            (
                SELECT STRING_AGG(role.Name, N', ') AS RoleNamesCsv
                FROM dbo.UserRoles AS userRole
                INNER JOIN dbo.Roles AS role
                    ON role.TenantId = userRole.TenantId
                    AND role.RoleId = userRole.RoleId
                    AND role.Status = N'Active'
                WHERE userRole.TenantId = u.TenantId
                  AND userRole.UserId = u.UserId
            ) AS roleNames
            OUTER APPLY
            (
                SELECT COUNT(1) AS CompletedInterviewCount
                FROM dbo.Interviews AS interview
                WHERE interview.TenantId = u.TenantId
                  AND interview.InterviewerUserId = u.UserId
                  AND interview.Status = N'Completed'
            ) AS interviewStats
            OUTER APPLY
            (
                SELECT TOP (1) userRole.UserId
                FROM dbo.UserRoles AS userRole
                INNER JOIN dbo.Roles AS role
                    ON role.TenantId = userRole.TenantId
                    AND role.RoleId = userRole.RoleId
                    AND role.Code = N'HOD'
                    AND role.Status = N'Active'
                WHERE userRole.TenantId = u.TenantId
                  AND userRole.UserId = u.UserId
            ) AS hodRole
            WHERE request.TenantId = @TenantId
              AND request.JobRequestId = @JobRequestId
            ORDER BY
                CASE WHEN employee.DepartmentId = request.DepartmentId THEN 0 ELSE 1 END,
                COALESCE(department.Name, N'Unassigned'),
                u.DisplayName;
            """;

        var rows = await connection.QueryAsync<InterviewerOptionRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken));

        return rows.Select(ToInterviewerOption).ToArray();
    }

    private static OperationsInterviewerOption ToInterviewerOption(InterviewerOptionRow row)
    {
        var roleNames = row.RoleNamesCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new OperationsInterviewerOption(
            row.UserId,
            row.DisplayName,
            row.Email,
            row.DepartmentId,
            row.DepartmentName,
            row.Designation,
            roleNames,
            row.CompletedInterviewCount,
            row.IsJobDepartmentMatch,
            row.IsDepartmentHod);
    }

    private static async Task<IReadOnlyList<OperationsLookupOption>> ListDepartmentHodInterviewersAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.UserId AS Id,
                u.DisplayName AS Name,
                CONCAT(COALESCE(employee.Designation, N'HOD'), N' - ', COALESCE(department.Name, N'Department Head')) AS Description
            FROM dbo.JobRequests AS request
            INNER JOIN dbo.Employees AS employee
                ON employee.TenantId = request.TenantId
                AND employee.DepartmentId = request.DepartmentId
                AND employee.AppUserId IS NOT NULL
                AND employee.Status = N'Active'
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = employee.TenantId
                AND u.UserId = employee.AppUserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            INNER JOIN dbo.UserRoles AS userRole
                ON userRole.TenantId = u.TenantId
                AND userRole.UserId = u.UserId
            INNER JOIN dbo.Roles AS role
                ON role.TenantId = userRole.TenantId
                AND role.RoleId = userRole.RoleId
                AND role.Code = N'HOD'
                AND role.Status = N'Active'
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = request.TenantId
                AND department.DepartmentId = request.DepartmentId
            WHERE request.TenantId = @TenantId
              AND request.JobRequestId = @JobRequestId
            ORDER BY employee.ExperienceYears DESC, u.DisplayName;
            """;

        var rows = await connection.QueryAsync<OperationsLookupOption>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    private static async Task<IReadOnlyList<OperationsLookupOption>> ListActiveSkillsAsync(
        SqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SkillId AS Id, Name, Category AS Description
            FROM dbo.Skills
            WHERE TenantId = @TenantId
              AND Status = N'Active'
            ORDER BY Name;
            """;

        var rows = await connection.QueryAsync<OperationsLookupOption>(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    private static async Task<JobPostRequestDefaultsRow?> ReadJobPostRequestDefaultsAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                JobRequestId,
                RequestCode,
                DepartmentId,
                LocationId
            FROM dbo.JobRequests
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId
              AND CurrentStageKey = N'SOURCING';
            """;

        return await connection.QuerySingleOrDefaultAsync<JobPostRequestDefaultsRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<bool> InterviewTemplateIsAvailableAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid interviewTemplateId,
        Guid? departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.InterviewTemplates
                WHERE TenantId = @TenantId
                  AND InterviewTemplateId = @InterviewTemplateId
                  AND Status = N'Active'
                  AND (DepartmentId IS NULL OR DepartmentId = @DepartmentId)
            ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
            """;

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, InterviewTemplateId = interviewTemplateId, DepartmentId = departmentId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<bool> JobPostSkillsAreActiveAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        IReadOnlyList<Guid> skillIds,
        CancellationToken cancellationToken)
    {
        var distinctSkillIds = skillIds.Where(skillId => skillId != Guid.Empty).Distinct().ToArray();
        if (distinctSkillIds.Length == 0)
        {
            return false;
        }

        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Skills
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND SkillId IN @SkillIds;
            """;

        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, SkillIds = distinctSkillIds },
            transaction,
            cancellationToken: cancellationToken));
        return count == distinctSkillIds.Length;
    }

    private static async Task<bool> CanMutateRecruiterSourcingAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        return await ReadCurrentAssignmentForActionAsync(
            connection,
            transaction!,
            tenantId,
            actorUserId,
            jobRequestId,
            "SOURCING",
            requireClaimedForGroups: true,
            cancellationToken) is not null;
    }

    private static async Task<ApplicationActionContextRow?> ReadApplicationActionContextAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                application.JobApplicationId,
                application.JobRequestId,
                application.JobPostId,
                application.CurrentStatus,
                request.RequestCode,
                candidate.DisplayName AS CandidateName
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            WHERE application.TenantId = @TenantId
              AND application.JobApplicationId = @JobApplicationId;
            """;

        return await connection.QuerySingleOrDefaultAsync<ApplicationActionContextRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobApplicationId = jobApplicationId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<JobPostRoundSchedulingRow?> ReadJobPostRoundForSchedulingAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobPostId,
        Guid jobPostInterviewRoundId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                JobPostInterviewRoundId,
                RoundOrder,
                Name,
                OwnerUserId,
                DurationMinutes
            FROM dbo.JobPostInterviewRounds
            WHERE TenantId = @TenantId
              AND JobPostId = @JobPostId
              AND JobPostInterviewRoundId = @JobPostInterviewRoundId
              AND Status = N'Active';
            """;

        return await connection.QuerySingleOrDefaultAsync<JobPostRoundSchedulingRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId, JobPostInterviewRoundId = jobPostInterviewRoundId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<bool> PriorInterviewRoundsReadyAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobPostId,
        Guid jobApplicationId,
        int roundOrder,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.JobPostInterviewRounds AS priorRound
            WHERE priorRound.TenantId = @TenantId
              AND priorRound.JobPostId = @JobPostId
              AND priorRound.Status = N'Active'
              AND priorRound.RoundOrder < @RoundOrder
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.Interviews AS interview
                  WHERE interview.TenantId = priorRound.TenantId
                    AND interview.JobApplicationId = @JobApplicationId
                    AND interview.JobPostInterviewRoundId = priorRound.JobPostInterviewRoundId
                    AND interview.Status IN (N'Completed', N'Skipped')
              );
            """;

        var blockingPriorRounds = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                JobPostId = jobPostId,
                JobApplicationId = jobApplicationId,
                RoundOrder = roundOrder
            },
            transaction,
            cancellationToken: cancellationToken));

        return blockingPriorRounds == 0;
    }

    private static async Task<OperationsInterviewScheduleContext?> ReadInterviewScheduleContextForSchedulingAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        ScheduleCandidateInterviewInput input,
        CancellationToken cancellationToken)
    {
        var actionContext = await ReadApplicationActionContextAsync(
            connection,
            transaction,
            tenantId,
            jobApplicationId,
            cancellationToken);
        if (actionContext is null ||
            !actionContext.JobPostId.HasValue ||
            !await CanMutateRecruiterSourcingAsync(connection, transaction, tenantId, actorUserId, actionContext.JobRequestId, cancellationToken))
        {
            return null;
        }

        var round = await ReadJobPostRoundForSchedulingAsync(
            connection,
            transaction,
            tenantId,
            actionContext.JobPostId.Value,
            input.JobPostInterviewRoundId,
            cancellationToken);
        if (round is null)
        {
            return null;
        }

        if (!await PriorInterviewRoundsReadyAsync(
                connection,
                transaction,
                tenantId,
                actionContext.JobPostId.Value,
                jobApplicationId,
                round.RoundOrder,
                cancellationToken))
        {
            return null;
        }

        var interviewerUserId = input.InterviewerUserId.GetValueOrDefault(round.OwnerUserId.GetValueOrDefault());
        if (interviewerUserId == Guid.Empty)
        {
            return null;
        }

        const string duplicateSql = """
            SELECT COUNT(1)
            FROM dbo.Interviews
            WHERE TenantId = @TenantId
              AND JobApplicationId = @JobApplicationId
              AND JobPostInterviewRoundId = @JobPostInterviewRoundId
              AND Status IN (N'Scheduled', N'Completed', N'Skipped');
            """;
        var duplicateCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            duplicateSql,
            new
            {
                TenantId = tenantId,
                JobApplicationId = jobApplicationId,
                input.JobPostInterviewRoundId
            },
            transaction,
            cancellationToken: cancellationToken));
        if (duplicateCount > 0)
        {
            return null;
        }

        const string sql = """
            SELECT TOP (1)
                tenant.DisplayName AS CompanyName,
                request.RequestCode,
                post.Title AS JobTitle,
                candidate.DisplayName AS CandidateName,
                candidate.Email AS CandidateEmail,
                interviewer.UserId AS InterviewerUserId,
                interviewer.DisplayName AS InterviewerName,
                interviewer.Email AS InterviewerEmail,
                hiringManager.UserId AS HiringManagerUserId,
                hiringManager.DisplayName AS HiringManagerName,
                hiringManager.Email AS HiringManagerEmail,
                recruiter.DisplayName AS RecruiterName,
                recruiter.Email AS RecruiterEmail,
                postRound.Name AS RoundName,
                postRound.DurationMinutes,
                tenant.DefaultTimezoneId AS TimeZoneId
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.Tenants AS tenant
                ON tenant.TenantId = application.TenantId
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            INNER JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            INNER JOIN dbo.JobPostInterviewRounds AS postRound
                ON postRound.TenantId = application.TenantId
                AND postRound.JobPostInterviewRoundId = @JobPostInterviewRoundId
            INNER JOIN dbo.AppUsers AS interviewer
                ON interviewer.TenantId = application.TenantId
                AND interviewer.UserId = @InterviewerUserId
                AND interviewer.AccountStatus = N'Active'
                AND interviewer.DeletedAtUtc IS NULL
            INNER JOIN dbo.AppUsers AS hiringManager
                ON hiringManager.TenantId = request.TenantId
                AND hiringManager.UserId = request.HiringManagerUserId
                AND hiringManager.AccountStatus = N'Active'
                AND hiringManager.DeletedAtUtc IS NULL
            INNER JOIN dbo.AppUsers AS recruiter
                ON recruiter.TenantId = application.TenantId
                AND recruiter.UserId = @ActorUserId
            WHERE application.TenantId = @TenantId
              AND application.JobApplicationId = @JobApplicationId
              AND application.JobPostId = @JobPostId
              AND postRound.JobPostId = @JobPostId
              AND postRound.Status = N'Active';
            """;

        return await connection.QuerySingleOrDefaultAsync<OperationsInterviewScheduleContext>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                JobApplicationId = jobApplicationId,
                JobPostId = actionContext.JobPostId.Value,
                input.JobPostInterviewRoundId,
                InterviewerUserId = interviewerUserId,
                ActorUserId = actorUserId
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<OperationsScheduleCandidateInterviewValidationStatus> ReadCandidateInterviewScheduleValidationAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        ScheduleCandidateInterviewInput input,
        CancellationToken cancellationToken)
    {
        var actionContext = await ReadApplicationActionContextAsync(
            connection,
            transaction,
            tenantId,
            jobApplicationId,
            cancellationToken);
        if (actionContext is null ||
            !actionContext.JobPostId.HasValue ||
            !await CanMutateRecruiterSourcingAsync(connection, transaction, tenantId, actorUserId, actionContext.JobRequestId, cancellationToken))
        {
            return OperationsScheduleCandidateInterviewValidationStatus.NotFound;
        }

        var round = await ReadJobPostRoundForSchedulingAsync(
            connection,
            transaction,
            tenantId,
            actionContext.JobPostId.Value,
            input.JobPostInterviewRoundId,
            cancellationToken);
        if (round is null)
        {
            return OperationsScheduleCandidateInterviewValidationStatus.NotFound;
        }

        if (!await PriorInterviewRoundsReadyAsync(
                connection,
                transaction,
                tenantId,
                actionContext.JobPostId.Value,
                jobApplicationId,
                round.RoundOrder,
                cancellationToken))
        {
            return OperationsScheduleCandidateInterviewValidationStatus.PriorRoundsPending;
        }

        const string duplicateSql = """
            SELECT COUNT(1)
            FROM dbo.Interviews
            WHERE TenantId = @TenantId
              AND JobApplicationId = @JobApplicationId
              AND JobPostInterviewRoundId = @JobPostInterviewRoundId
              AND Status IN (N'Scheduled', N'Completed', N'Skipped');
            """;
        var duplicateCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            duplicateSql,
            new
            {
                TenantId = tenantId,
                JobApplicationId = jobApplicationId,
                input.JobPostInterviewRoundId
            },
            transaction,
            cancellationToken: cancellationToken));
        if (duplicateCount > 0)
        {
            return OperationsScheduleCandidateInterviewValidationStatus.RoundAlreadyScheduled;
        }

        var interviewerUserId = input.InterviewerUserId.GetValueOrDefault(round.OwnerUserId.GetValueOrDefault());
        if (interviewerUserId == Guid.Empty)
        {
            return OperationsScheduleCandidateInterviewValidationStatus.MissingInterviewer;
        }

        var interviewerName = await ReadActiveUserDisplayNameAsync(
            connection,
            transaction,
            tenantId,
            interviewerUserId,
            cancellationToken);
        return string.IsNullOrWhiteSpace(interviewerName)
            ? OperationsScheduleCandidateInterviewValidationStatus.MissingInterviewer
            : OperationsScheduleCandidateInterviewValidationStatus.Ready;
    }

    private static async Task<string?> ReadActiveUserDisplayNameAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) DisplayName
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId = @UserId
              AND AccountStatus = N'Active'
              AND DeletedAtUtc IS NULL;
            """;

        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static OperationsInterviewTask ToInterviewTask(InterviewTaskRow row)
    {
        return new OperationsInterviewTask(
            row.InterviewId,
            row.JobApplicationId,
            row.JobPostInterviewRoundId,
            row.JobRequestId,
            row.JobPostId,
            row.RequestCode,
            row.JobTitle,
            row.Client,
            row.CandidateName,
            row.CandidateEmail,
            row.RoundName,
            row.InterviewerName,
            row.InterviewerUserId,
            row.InterviewerAccountStatus,
            row.InterviewerIsDeleted,
            row.ScheduledByName,
            Utc(row.StartsAt),
            row.DurationMinutes,
            row.MeetingLink,
            row.LocationText,
            row.Status,
            row.Recommendation,
            row.TechnicalScore,
            row.CommunicationScore,
            row.CultureScore,
            row.FeedbackText,
            ToUtc(row.SubmittedAt));
    }

    private static OperationsInterviewQuestionRecommendationContext ToInterviewQuestionContext(
        InterviewQuestionContextRow row,
        IReadOnlyList<OperationsInterviewQuestionSkill> requiredSkills,
        IReadOnlyList<OperationsInterviewQuestionSkill> candidateSkills,
        IReadOnlyList<OperationsApplicantDocumentEvidence> documents,
        IReadOnlyList<OperationsCandidateInterviewEvidence> priorFeedback)
    {
        return new OperationsInterviewQuestionRecommendationContext(
            row.InterviewId,
            row.JobApplicationId,
            row.JobPostInterviewRoundId,
            row.JobRequestId,
            row.JobPostId,
            row.CandidateId,
            row.RequestCode,
            row.JobTitle,
            row.Client,
            row.Department,
            row.Location,
            row.RoundName,
            NormalizeInterviewRoundType(row.RoundName),
            row.DurationMinutes,
            row.Status,
            Utc(row.StartsAt),
            row.InterviewerName,
            row.InterviewerUserId,
            row.CandidateName,
            row.CandidateEmail,
            row.CurrentDesignation,
            row.CurrentCompany,
            row.ExperienceYears,
            row.NoticePeriodDays,
            row.ApplicationStatus,
            row.CoverLetterText,
            row.RecruiterNotes,
            row.ApplicationSnapshotJson,
            row.JobRequestDescription,
            row.JobPostDescription,
            row.ExperienceMinYears,
            row.ExperienceMaxYears,
            requiredSkills,
            candidateSkills,
            documents,
            priorFeedback);
    }

    private static InterviewQuestionRecommendationSet ToInterviewQuestionRecommendationSet(
        InterviewQuestionRecommendationSetRow row,
        IReadOnlyList<InterviewQuestionRecommendationRow> questionRows)
    {
        return new InterviewQuestionRecommendationSet(
            row.RecommendationSetId,
            row.InterviewId,
            row.JobApplicationId,
            row.JobPostInterviewRoundId,
            row.AiAgentRunId,
            row.Model,
            row.PromptVersion,
            row.VersionNumber,
            row.Summary,
            row.Rationale,
            row.RegenerateReason,
            DeserializeInterviewQuestionCoverage(row.CoverageJson),
            row.Status,
            Utc(row.GeneratedAt),
            questionRows.Select(ToInterviewQuestionRecommendation).ToArray());
    }

    private static InterviewQuestionRecommendation ToInterviewQuestionRecommendation(InterviewQuestionRecommendationRow row)
    {
        return new InterviewQuestionRecommendation(
            row.QuestionRecommendationId,
            row.SortOrder,
            row.QuestionText,
            row.QuestionType,
            row.RoundType,
            row.SkillName,
            row.Difficulty,
            row.Rationale,
            row.ExpectedSignal,
            DeserializeStringArray(row.FollowUpsJson),
            DeserializeStringArray(row.EvaluationRubricJson),
            row.SourceBankItemId);
    }

    private static InterviewQuestionBankItem ToInterviewQuestionBankItem(InterviewQuestionBankItemRow row)
    {
        return new InterviewQuestionBankItem(
            row.InterviewQuestionBankItemId,
            row.TenantId,
            row.SkillId,
            row.SkillName,
            row.SkillCategory,
            row.DepartmentId,
            row.JobFamily,
            row.RoundType,
            row.Difficulty,
            row.QuestionText,
            row.ExpectedSignal,
            DeserializeStringArray(row.FollowUpsJson),
            DeserializeStringArray(row.EvaluationRubricJson),
            row.SourceTitle,
            row.SourceUrl,
            row.ContentHashSha256);
    }

    private static async Task<InterviewQuestionContextRow?> ReadInterviewQuestionContextAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantTasks,
        Guid interviewId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                interview.InterviewId,
                application.JobApplicationId,
                postRound.JobPostInterviewRoundId,
                application.JobRequestId,
                post.JobPostId,
                candidate.CandidateId,
                request.RequestCode,
                post.Title AS JobTitle,
                COALESCE(request.ClientName, N'') AS Client,
                COALESCE(postDepartment.Name, requestDepartment.Name, N'') AS Department,
                COALESCE(postLocation.Name, requestLocation.Name, N'') AS Location,
                postRound.Name AS RoundName,
                interview.DurationMinutes,
                interview.Status,
                interview.StartsAtUtc AS StartsAt,
                interviewer.DisplayName AS InterviewerName,
                interview.InterviewerUserId,
                candidate.DisplayName AS CandidateName,
                candidate.Email AS CandidateEmail,
                candidate.CurrentDesignation,
                candidate.CurrentCompany,
                candidate.ExperienceYears,
                candidate.NoticePeriodDays,
                application.CurrentStatus AS ApplicationStatus,
                application.CoverLetterText,
                application.RecruiterNotes,
                application.ApplicationSnapshotJson,
                request.Description AS JobRequestDescription,
                post.Description AS JobPostDescription,
                post.ExperienceMinYears,
                post.ExperienceMaxYears
            FROM dbo.Interviews AS interview
            INNER JOIN dbo.JobApplications AS application
                ON application.TenantId = interview.TenantId
                AND application.JobApplicationId = interview.JobApplicationId
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            INNER JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            INNER JOIN dbo.JobPostInterviewRounds AS postRound
                ON postRound.TenantId = interview.TenantId
                AND postRound.JobPostInterviewRoundId = interview.JobPostInterviewRoundId
            INNER JOIN dbo.AppUsers AS interviewer
                ON interviewer.TenantId = interview.TenantId
                AND interviewer.UserId = interview.InterviewerUserId
            LEFT JOIN dbo.Departments AS requestDepartment
                ON requestDepartment.TenantId = request.TenantId
                AND requestDepartment.DepartmentId = request.DepartmentId
            LEFT JOIN dbo.Departments AS postDepartment
                ON postDepartment.TenantId = post.TenantId
                AND postDepartment.DepartmentId = post.DepartmentId
            LEFT JOIN dbo.Locations AS requestLocation
                ON requestLocation.TenantId = request.TenantId
                AND requestLocation.LocationId = request.LocationId
            LEFT JOIN dbo.Locations AS postLocation
                ON postLocation.TenantId = post.TenantId
                AND postLocation.LocationId = post.LocationId
            WHERE interview.TenantId = @TenantId
              AND interview.InterviewId = @InterviewId
              AND interview.Status IN (N'Scheduled', N'Completed')
              AND (@IncludeAllTenantTasks = CAST(1 AS BIT) OR interview.InterviewerUserId = @ActorUserId);
            """;

        return await connection.QuerySingleOrDefaultAsync<InterviewQuestionContextRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActorUserId = actorUserId, IncludeAllTenantTasks = includeAllTenantTasks, InterviewId = interviewId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<OperationsInterviewQuestionSkill>> ListInterviewQuestionRequiredSkillsAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid jobRequestId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT
                skill.SkillId,
                skill.Name,
                skill.Category
            FROM dbo.Skills AS skill
            WHERE skill.TenantId = @TenantId
              AND skill.Status = N'Active'
              AND (
                    skill.SkillId IN
                    (
                        SELECT postSkill.SkillId
                        FROM dbo.JobPostSkills AS postSkill
                        WHERE postSkill.TenantId = @TenantId
                          AND postSkill.JobPostId = @JobPostId
                    )
                    OR skill.SkillId IN
                    (
                        SELECT requestSkill.SkillId
                        FROM dbo.JobRequestSkills AS requestSkill
                        WHERE requestSkill.TenantId = @TenantId
                          AND requestSkill.JobRequestId = @JobRequestId
                    )
                  )
            ORDER BY skill.Name;
            """;

        var rows = await connection.QueryAsync<OperationsInterviewQuestionSkill>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId, JobPostId = jobPostId },
            transaction,
            cancellationToken: cancellationToken));

        return rows.ToArray();
    }

    private static async Task<IReadOnlyList<OperationsInterviewQuestionSkill>> ListInterviewQuestionCandidateSkillsAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                skill.SkillId,
                skill.Name,
                skill.Category
            FROM dbo.CandidateSkills AS candidateSkill
            INNER JOIN dbo.Skills AS skill
                ON skill.TenantId = candidateSkill.TenantId
                AND skill.SkillId = candidateSkill.SkillId
            WHERE candidateSkill.TenantId = @TenantId
              AND candidateSkill.CandidateId = @CandidateId
              AND skill.Status = N'Active'
            ORDER BY candidateSkill.IsPrimary DESC, skill.Name;
            """;

        var rows = await connection.QueryAsync<OperationsInterviewQuestionSkill>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, CandidateId = candidateId },
            transaction,
            cancellationToken: cancellationToken));

        return rows.ToArray();
    }

    private static async Task<IReadOnlyList<OperationsCandidateInterviewEvidence>> ListInterviewQuestionPriorFeedbackAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid jobApplicationId,
        Guid currentInterviewId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                interview.InterviewId,
                interview.JobApplicationId,
                postRound.Name AS RoundName,
                interview.Status,
                feedback.Recommendation,
                feedback.TechnicalScore,
                feedback.CommunicationScore,
                feedback.CultureScore,
                feedback.FeedbackText AS FeedbackSummary,
                feedback.SubmittedAtUtc AS SubmittedAt
            FROM dbo.Interviews AS interview
            INNER JOIN dbo.JobPostInterviewRounds AS postRound
                ON postRound.TenantId = interview.TenantId
                AND postRound.JobPostInterviewRoundId = interview.JobPostInterviewRoundId
            LEFT JOIN dbo.InterviewFeedback AS feedback
                ON feedback.TenantId = interview.TenantId
                AND feedback.InterviewId = interview.InterviewId
                AND feedback.IsSubmitted = CAST(1 AS BIT)
            WHERE interview.TenantId = @TenantId
              AND interview.JobApplicationId = @JobApplicationId
              AND interview.InterviewId <> @CurrentInterviewId
            ORDER BY interview.StartsAtUtc ASC;
            """;

        var rows = await connection.QueryAsync<InterviewQuestionPriorFeedbackRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobApplicationId = jobApplicationId, CurrentInterviewId = currentInterviewId },
            transaction,
            cancellationToken: cancellationToken));

        return rows.Select(row => new OperationsCandidateInterviewEvidence(
            row.InterviewId,
            row.JobApplicationId,
            row.RoundName,
            row.Status,
            row.Recommendation,
            row.TechnicalScore,
            row.CommunicationScore,
            row.CultureScore,
            row.FeedbackSummary,
            ToUtc(row.SubmittedAt))).ToArray();
    }

    private static async Task<IReadOnlyList<InterviewQuestionRecommendationRow>> ReadInterviewQuestionRecommendationRowsAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid recommendationSetId,
        CancellationToken cancellationToken)
    {
        const string questionSql = """
            SELECT
                QuestionRecommendationId,
                SortOrder,
                QuestionText,
                QuestionType,
                RoundType,
                SkillName,
                Difficulty,
                Rationale,
                ExpectedSignal,
                FollowUpsJson,
                EvaluationRubricJson,
                SourceBankItemId
            FROM dbo.InterviewQuestionRecommendations
            WHERE TenantId = @TenantId
              AND RecommendationSetId = @RecommendationSetId
            ORDER BY SortOrder ASC;
            """;

        var rows = await connection.QueryAsync<InterviewQuestionRecommendationRow>(new CommandDefinition(
            questionSql,
            new { TenantId = tenantId, RecommendationSetId = recommendationSetId },
            transaction,
            cancellationToken: cancellationToken));

        return rows.ToArray();
    }

    private static InterviewQuestionCoverage DeserializeInterviewQuestionCoverage(string? coverageJson)
    {
        try
        {
            var coverage = string.IsNullOrWhiteSpace(coverageJson)
                ? null
                : JsonSerializer.Deserialize<InterviewQuestionCoverage>(coverageJson);
            return coverage ?? new InterviewQuestionCoverage("Technical", 0, 0, "Unavailable: coverage metadata missing", [], []);
        }
        catch
        {
            return new InterviewQuestionCoverage("Technical", 0, 0, "Unavailable: coverage metadata invalid", [], []);
        }
    }

    private static IReadOnlyList<string> DeserializeStringArray(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeInterviewRoundType(string roundName)
    {
        var value = (roundName ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Contains("screen", StringComparison.Ordinal))
        {
            return "Screening";
        }

        if (value.Contains("hr", StringComparison.Ordinal) ||
            value.Contains("human resource", StringComparison.Ordinal))
        {
            return "HR";
        }

        if (value.Contains("hod", StringComparison.Ordinal) ||
            value.Contains("head", StringComparison.Ordinal) ||
            value.Contains("department", StringComparison.Ordinal))
        {
            return "HOD";
        }

        if (value.Contains("behavior", StringComparison.Ordinal) ||
            value.Contains("culture", StringComparison.Ordinal))
        {
            return "Behavioral";
        }

        return "Technical";
    }

    private static async Task QueueInterviewScheduledEmailsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid interviewId,
        CancellationToken cancellationToken)
    {
        var context = await ReadInterviewScheduleNotificationContextAsync(
            connection,
            transaction,
            tenantId,
            interviewId,
            cancellationToken);
        if (context is null)
        {
            return;
        }

        var eventId = await EnsureNotificationEventAsync(
            connection,
            transaction,
            tenantId,
            NotificationEventCodes.InterviewScheduled,
            "Interview scheduled",
            "User:InterviewParticipants",
            cancellationToken);

        var messages = InterviewScheduleEmailComposer.Build(new InterviewScheduleEmailContext(
            context.CompanyName,
            context.RequestCode,
            context.JobTitle,
            context.CandidateName,
            context.CandidateEmail,
            context.InterviewerUserId,
            context.InterviewerName,
            context.InterviewerEmail,
            context.HiringManagerUserId,
            context.HiringManagerName,
            context.HiringManagerEmail,
            context.RecruiterName,
            context.RoundName,
            Utc(context.StartsAt),
            context.DurationMinutes,
            context.MeetingLink,
            context.LocationText));

        await InsertInterviewEmailOutboxAsync(
            connection,
            transaction,
            tenantId,
            eventId,
            null,
            interviewId,
            "Interview",
            messages,
            cancellationToken);
    }

    private static IReadOnlyList<OperationsNotificationDispatch> BuildInterviewScheduledDispatches(
        Guid interviewId,
        InterviewScheduleNotificationContextRow context)
    {
        var startsAtUtc = Utc(context.StartsAt);
        var startsAtLabel = startsAtUtc.ToString("MMM d, yyyy, h:mm tt 'UTC'", CultureInfo.InvariantCulture);
        var internalRoute = BuildRecruiterFeedbackReviewPath(context.JobRequestId, context.JobApplicationId);
        var interviewerRoute = $"/app/interview-feedback?interviewId={interviewId:D}";
        var candidateRoute = $"/candidate/applications/{context.JobApplicationId:D}/status";
        var sharedMetadata = new Dictionary<string, string>
        {
            ["requestCode"] = context.RequestCode,
            ["jobTitle"] = context.JobTitle,
            ["candidateName"] = context.CandidateName,
            ["roundName"] = context.RoundName,
            ["startsAtUtc"] = startsAtUtc.ToString("O", CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(context.MeetingLink))
        {
            sharedMetadata["meetingLink"] = context.MeetingLink;
        }

        var dispatches = new List<OperationsNotificationDispatch>();
        AddDispatch(
            dispatches,
            interviewId,
            context.CandidateUserId,
            "Interview scheduled",
            $"Your {context.RoundName} interview for {context.JobTitle} is scheduled for {startsAtLabel}.",
            candidateRoute,
            sharedMetadata);
        AddDispatch(
            dispatches,
            interviewId,
            context.InterviewerUserId,
            "Interview scheduled",
            $"{context.CandidateName}'s {context.RoundName} interview for {context.JobTitle} is scheduled for {startsAtLabel}.",
            interviewerRoute,
            sharedMetadata);
        AddDispatch(
            dispatches,
            interviewId,
            context.HiringManagerUserId,
            "Interview scheduled",
            $"{context.CandidateName}'s {context.RoundName} interview for {context.JobTitle} is scheduled for {startsAtLabel}.",
            internalRoute,
            sharedMetadata);

        return dispatches
            .Where(dispatch => dispatch.RecipientUserId != Guid.Empty)
            .GroupBy(dispatch => dispatch.RecipientUserId)
            .Select(group => group.First())
            .ToArray();
    }

    private static void AddDispatch(
        ICollection<OperationsNotificationDispatch> dispatches,
        Guid interviewId,
        Guid recipientUserId,
        string title,
        string message,
        string route,
        IReadOnlyDictionary<string, string> sharedMetadata)
    {
        if (recipientUserId == Guid.Empty)
        {
            return;
        }

        var metadata = new Dictionary<string, string>(sharedMetadata)
        {
            ["route"] = route
        };

        dispatches.Add(new OperationsNotificationDispatch(
            recipientUserId,
            NotificationEventCodes.InterviewScheduled,
            title,
            message,
            "Interview",
            "Info",
            "Interview",
            interviewId,
            metadata));
    }

    private static async Task QueueInterviewFeedbackSubmittedEmailAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        InterviewFeedbackContextRow context,
        SubmitInterviewFeedbackInput input,
        Guid interviewId,
        string reviewUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.RecruiterEmail))
        {
            return;
        }

        var eventId = await EnsureNotificationEventAsync(
            connection,
            transaction,
            tenantId,
            NotificationEventCodes.InterviewFeedbackSubmitted,
            "Interview feedback submitted",
            "User:Recruiter",
            cancellationToken);

        var subject = $"Feedback submitted: {context.CandidateName}";
        var body = string.Join(Environment.NewLine, new[]
        {
            $"Hello {context.RecruiterName},",
            string.Empty,
            $"Feedback for {context.CandidateName}'s {context.RoundName} interview is ready for recruiter review.",
            string.Empty,
            $"Job: {context.JobTitle} ({context.RequestCode})",
            $"Round: {context.RoundName}",
            $"Recommendation: {input.Recommendation}",
            $"Scores: Technical {input.TechnicalScore}/5, Communication {input.CommunicationScore}/5, Culture {input.CultureScore}/5",
            string.Empty,
            "Next step: Review the feedback and schedule the next interview when appropriate.",
            reviewUrl,
            string.Empty,
            input.FeedbackText
        });
        var htmlBody = BuildInterviewFeedbackSubmittedHtmlBody(context, input, reviewUrl);

        await InsertInterviewEmailOutboxAsync(
            connection,
            transaction,
            tenantId,
            eventId,
            null,
            interviewId,
            "Interview",
            [
                new InterviewScheduleEmailMessage(
                    "Recruiter",
                    context.RecruiterUserId,
                    context.RecruiterEmail,
                    subject,
                    body,
                    htmlBody)
            ],
            cancellationToken);
    }

    private static IReadOnlyList<OperationsNotificationDispatch> BuildInterviewFeedbackSubmittedDispatches(
        InterviewFeedbackContextRow context,
        SubmitInterviewFeedbackInput input,
        string reviewPath)
    {
        if (context.RecruiterUserId == Guid.Empty)
        {
            return [];
        }

        return
        [
            new OperationsNotificationDispatch(
                context.RecruiterUserId,
                NotificationEventCodes.InterviewFeedbackSubmitted,
                "Interview feedback submitted",
                $"{context.CandidateName}'s {context.RoundName} feedback is ready. Review it and schedule the next interview.",
                "Interview",
                "Info",
                "JobApplication",
                context.JobApplicationId,
                new Dictionary<string, string>
                {
                    ["requestCode"] = context.RequestCode,
                    ["jobTitle"] = context.JobTitle,
                    ["candidateName"] = context.CandidateName,
                    ["roundName"] = context.RoundName,
                    ["recommendation"] = input.Recommendation,
                    ["route"] = reviewPath
                })
        ];
    }

    private static string BuildInterviewFeedbackSubmittedHtmlBody(
        InterviewFeedbackContextRow context,
        SubmitInterviewFeedbackInput input,
        string reviewUrl)
    {
        return TalentPilotEmailTemplate.Build(
            "Interview Feedback",
            $"{context.CandidateName} feedback is ready",
            $"Hello {context.RecruiterName},\n\n{context.RoundName} feedback has been submitted for {context.CandidateName}. Review the notes, then schedule the next interview when the candidate should continue.",
            [
                ("Role", $"{context.JobTitle} ({context.RequestCode})"),
                ("Recommendation", input.Recommendation),
                ("Scores", $"Technical {input.TechnicalScore}/5 - Communication {input.CommunicationScore}/5 - Culture {input.CultureScore}/5")
            ],
            "Review feedback and schedule next interview",
            reviewUrl,
            $"{context.CandidateName} feedback is ready");
    }

    private static string BuildRecruiterFeedbackReviewPath(Guid jobRequestId, Guid jobApplicationId)
    {
        return $"/app/recruitment/sourcing/{jobRequestId:D}?tab=applications&applicationId={jobApplicationId:D}";
    }

    private static string BuildFrontendUrl(string frontendBaseUrl, string pathAndQuery)
    {
        if (!Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var baseUri))
        {
            return pathAndQuery;
        }

        var pathParts = pathAndQuery.Split('?', 2);
        var builder = new UriBuilder(baseUri)
        {
            Path = pathParts[0],
            Query = pathParts.Length > 1 ? pathParts[1] : string.Empty
        };

        return builder.Uri.ToString();
    }

    private static string NormalizeFrontendBaseUrl(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? "http://localhost:4200"
            : trimmed.TrimEnd('/');
    }

    private static async Task InsertInterviewEmailOutboxAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid eventId,
        Guid? templateId,
        Guid entityId,
        string entityType,
        IReadOnlyList<InterviewScheduleEmailMessage> messages,
        CancellationToken cancellationToken)
    {
        var rows = messages
            .Where(message => !string.IsNullOrWhiteSpace(message.RecipientEmail))
            .Select(message => new
            {
                NotificationOutboxId = Guid.NewGuid(),
                TenantId = tenantId,
                NotificationEventId = eventId,
                NotificationTemplateId = templateId,
                RecipientUserId = message.RecipientUserId,
                RecipientEmail = message.RecipientEmail,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    subject = message.Subject,
                    body = message.Body,
                    htmlBody = message.HtmlBody,
                    entityType,
                    entityId,
                    variables = new Dictionary<string, string>
                    {
                        ["recipientType"] = message.RecipientType
                    }
                })
            })
            .ToArray();

        if (rows.Length == 0)
        {
            return;
        }

        const string sql = """
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
            VALUES
            (
                @NotificationOutboxId,
                @TenantId,
                @NotificationEventId,
                @NotificationTemplateId,
                @RecipientUserId,
                @RecipientEmail,
                N'Email',
                @PayloadJson,
                N'Pending',
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            rows,
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<InterviewScheduleNotificationContextRow?> ReadInterviewScheduleNotificationContextAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid interviewId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                application.JobApplicationId,
                request.JobRequestId,
                tenant.DisplayName AS CompanyName,
                request.RequestCode,
                post.Title AS JobTitle,
                candidate.AppUserId AS CandidateUserId,
                candidate.DisplayName AS CandidateName,
                candidate.Email AS CandidateEmail,
                interviewer.UserId AS InterviewerUserId,
                interviewer.DisplayName AS InterviewerName,
                interviewer.Email AS InterviewerEmail,
                hiringManager.UserId AS HiringManagerUserId,
                hiringManager.DisplayName AS HiringManagerName,
                hiringManager.Email AS HiringManagerEmail,
                recruiter.DisplayName AS RecruiterName,
                postRound.Name AS RoundName,
                interview.StartsAtUtc AS StartsAt,
                interview.DurationMinutes,
                interview.MeetingLink,
                interview.LocationText
            FROM dbo.Interviews AS interview
            INNER JOIN dbo.Tenants AS tenant
                ON tenant.TenantId = interview.TenantId
            INNER JOIN dbo.JobApplications AS application
                ON application.TenantId = interview.TenantId
                AND application.JobApplicationId = interview.JobApplicationId
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            INNER JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            INNER JOIN dbo.JobPostInterviewRounds AS postRound
                ON postRound.TenantId = interview.TenantId
                AND postRound.JobPostInterviewRoundId = interview.JobPostInterviewRoundId
            INNER JOIN dbo.AppUsers AS interviewer
                ON interviewer.TenantId = interview.TenantId
                AND interviewer.UserId = interview.InterviewerUserId
            INNER JOIN dbo.AppUsers AS recruiter
                ON recruiter.TenantId = interview.TenantId
                AND recruiter.UserId = interview.ScheduledByUserId
            INNER JOIN dbo.AppUsers AS hiringManager
                ON hiringManager.TenantId = request.TenantId
                AND hiringManager.UserId = request.HiringManagerUserId
            WHERE interview.TenantId = @TenantId
              AND interview.InterviewId = @InterviewId;
            """;

        return await connection.QuerySingleOrDefaultAsync<InterviewScheduleNotificationContextRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, InterviewId = interviewId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task UpsertInterviewParticipantsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid interviewId,
        InterviewScheduleNotificationContextRow context,
        CancellationToken cancellationToken)
    {
        var rows = new[]
            {
                new InterviewParticipantInsertRow(
                    Guid.NewGuid(),
                    tenantId,
                    interviewId,
                    context.CandidateUserId,
                    context.CandidateName,
                    context.CandidateEmail,
                    "Candidate",
                    false),
                new InterviewParticipantInsertRow(
                    Guid.NewGuid(),
                    tenantId,
                    interviewId,
                    context.InterviewerUserId,
                    context.InterviewerName,
                    context.InterviewerEmail,
                    "Interviewer",
                    false),
                new InterviewParticipantInsertRow(
                    Guid.NewGuid(),
                    tenantId,
                    interviewId,
                    context.HiringManagerUserId,
                    context.HiringManagerName,
                    context.HiringManagerEmail,
                    "HiringManager",
                    true)
            }
            .Where(row => !string.IsNullOrWhiteSpace(row.Email))
            .GroupBy(row => row.Email.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (rows.Length == 0)
        {
            return;
        }

        const string sql = """
            MERGE dbo.InterviewParticipants AS target
            USING
            (
                SELECT
                    @InterviewParticipantId AS InterviewParticipantId,
                    @TenantId AS TenantId,
                    @InterviewId AS InterviewId,
                    @UserId AS UserId,
                    @DisplayName AS DisplayName,
                    @Email AS Email,
                    @ParticipantRole AS ParticipantRole,
                    @IsOptional AS IsOptional
            ) AS source
            ON target.TenantId = source.TenantId
               AND target.InterviewId = source.InterviewId
               AND target.Email = source.Email
            WHEN MATCHED THEN
                UPDATE SET
                    UserId = source.UserId,
                    DisplayName = source.DisplayName,
                    ParticipantRole = source.ParticipantRole,
                    IsOptional = source.IsOptional
            WHEN NOT MATCHED THEN
                INSERT
                (
                    InterviewParticipantId,
                    TenantId,
                    InterviewId,
                    UserId,
                    DisplayName,
                    Email,
                    ParticipantRole,
                    IsOptional,
                    CreatedAtUtc
                )
                VALUES
                (
                    source.InterviewParticipantId,
                    source.TenantId,
                    source.InterviewId,
                    source.UserId,
                    source.DisplayName,
                    source.Email,
                    source.ParticipantRole,
                    source.IsOptional,
                    SYSUTCDATETIME()
                );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            rows,
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<InterviewFeedbackContextRow?> ReadInterviewFeedbackContextAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid interviewId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                interview.InterviewId,
                application.JobApplicationId,
                application.JobRequestId,
                interview.InterviewerUserId,
                interviewer.AccountStatus AS InterviewerAccountStatus,
                CASE WHEN interviewer.DeletedAtUtc IS NULL THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END AS InterviewerIsDeleted,
                interview.Status,
                request.RequestCode,
                post.Title AS JobTitle,
                candidate.DisplayName AS CandidateName,
                postRound.Name AS RoundName,
                recruiter.UserId AS RecruiterUserId,
                recruiter.DisplayName AS RecruiterName,
                recruiter.Email AS RecruiterEmail
            FROM dbo.Interviews AS interview
            INNER JOIN dbo.JobApplications AS application
                ON application.TenantId = interview.TenantId
                AND application.JobApplicationId = interview.JobApplicationId
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            INNER JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            INNER JOIN dbo.JobPostInterviewRounds AS postRound
                ON postRound.TenantId = interview.TenantId
                AND postRound.JobPostInterviewRoundId = interview.JobPostInterviewRoundId
            INNER JOIN dbo.AppUsers AS interviewer
                ON interviewer.TenantId = interview.TenantId
                AND interviewer.UserId = interview.InterviewerUserId
            INNER JOIN dbo.AppUsers AS recruiter
                ON recruiter.TenantId = interview.TenantId
                AND recruiter.UserId = interview.ScheduledByUserId
            WHERE interview.TenantId = @TenantId
              AND interview.InterviewId = @InterviewId;
            """;

        return await connection.QuerySingleOrDefaultAsync<InterviewFeedbackContextRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, InterviewId = interviewId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<bool> JobPostHasPublishableContentAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                (SELECT COUNT(1)
                 FROM dbo.JobPostSkills
                 WHERE TenantId = @TenantId
                   AND JobPostId = @JobPostId) AS SkillCount,
                (SELECT COUNT(1)
                 FROM dbo.JobPostInterviewRounds
                 WHERE TenantId = @TenantId
                   AND JobPostId = @JobPostId
                   AND Status = N'Active') AS ActiveRoundCount;
            """;

        var row = await connection.QuerySingleAsync<JobPostPublishReadinessRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId },
            transaction,
            cancellationToken: cancellationToken));
        return row.SkillCount > 0 && row.ActiveRoundCount > 0;
    }

    private static async Task ReplaceJobPostSkillsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobPostId,
        IReadOnlyList<Guid> skillIds,
        CancellationToken cancellationToken)
    {
        const string deleteSql = """
            DELETE FROM dbo.JobPostSkills
            WHERE TenantId = @TenantId
              AND JobPostId = @JobPostId;
            """;
        const string insertSql = """
            INSERT INTO dbo.JobPostSkills (TenantId, JobPostId, SkillId, IsRequired, Weight)
            VALUES (@TenantId, @JobPostId, @SkillId, CAST(1 AS BIT), 1);
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql,
            new { TenantId = tenantId, JobPostId = jobPostId },
            transaction,
            cancellationToken: cancellationToken));
        foreach (var skillId in skillIds.Where(skillId => skillId != Guid.Empty).Distinct())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new { TenantId = tenantId, JobPostId = jobPostId, SkillId = skillId },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static async Task ReplaceJobPostInterviewRoundsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobPostId,
        IReadOnlyList<UpsertJobPostInterviewRoundInput> rounds,
        CancellationToken cancellationToken)
    {
        const string deleteSql = """
            DELETE FROM dbo.JobPostInterviewRounds
            WHERE TenantId = @TenantId
              AND JobPostId = @JobPostId;
            """;
        const string insertSql = """
            INSERT INTO dbo.JobPostInterviewRounds
            (
                JobPostInterviewRoundId,
                TenantId,
                JobPostId,
                InterviewTemplateRoundId,
                RoundOrder,
                Name,
                OwnerUserId,
                DurationMinutes,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @JobPostInterviewRoundId,
                @TenantId,
                @JobPostId,
                @InterviewTemplateRoundId,
                @RoundOrder,
                @Name,
                @OwnerUserId,
                @DurationMinutes,
                @Status,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql,
            new { TenantId = tenantId, JobPostId = jobPostId },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var round in rounds.OrderBy(item => item.RoundOrder))
        {
            var ownerUserId = await NormalizeActiveTenantUserIdAsync(
                connection,
                transaction,
                tenantId,
                round.OwnerUserId,
                cancellationToken);
            var templateRoundId = await NormalizeInterviewTemplateRoundIdAsync(
                connection,
                transaction,
                tenantId,
                round.InterviewTemplateRoundId,
                cancellationToken);
            var roundId = round.JobPostInterviewRoundId.GetValueOrDefault();
            if (roundId == Guid.Empty)
            {
                roundId = Guid.NewGuid();
            }

            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new
                {
                    JobPostInterviewRoundId = roundId,
                    TenantId = tenantId,
                    JobPostId = jobPostId,
                    InterviewTemplateRoundId = templateRoundId,
                    round.RoundOrder,
                    Name = Truncate(round.Name.Trim(), 160),
                    OwnerUserId = ownerUserId,
                    round.DurationMinutes,
                    Status = string.Equals(round.Status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active"
                },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static async Task<Guid?> NormalizeActiveTenantUserIdAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return null;
        }

        const string sql = """
            SELECT TOP (1) UserId
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId = @UserId
              AND AccountStatus = N'Active'
              AND DeletedAtUtc IS NULL;
            """;

        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId.Value },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid?> NormalizeInterviewTemplateRoundIdAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid? interviewTemplateRoundId,
        CancellationToken cancellationToken)
    {
        if (!interviewTemplateRoundId.HasValue || interviewTemplateRoundId.Value == Guid.Empty)
        {
            return null;
        }

        const string sql = """
            SELECT TOP (1) InterviewTemplateRoundId
            FROM dbo.InterviewTemplateRounds
            WHERE TenantId = @TenantId
              AND InterviewTemplateRoundId = @InterviewTemplateRoundId;
            """;

        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, InterviewTemplateRoundId = interviewTemplateRoundId.Value },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<JobPostAccessContextRow?> ReadJobPostAccessContextAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                post.JobPostId,
                post.JobRequestId,
                post.Status,
                request.RequestCode
            FROM dbo.JobPosts AS post
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = post.TenantId
                AND request.JobRequestId = post.JobRequestId
            WHERE post.TenantId = @TenantId
              AND post.JobPostId = @JobPostId;
            """;

        return await connection.QuerySingleOrDefaultAsync<JobPostAccessContextRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId },
            transaction,
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
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                jr.JobRequestId AS Id,
                jr.RequestCode AS Code,
                jr.Title,
                COALESCE(jr.ClientName, N'Internal') AS Client,
                jr.ClientContext,
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
                COALESCE(
                    g.Name,
                    CASE WHEN r.Code = N'TenantAdmin' THEN N'Tenant Admins' ELSE r.Name END
                ) AS AssignedToGroupName,
                jr.PublishStatus,
                jr.CreatedAtUtc AS CreatedAt,
                STRING_AGG(s.Name, N',') AS SkillList
            FROM dbo.JobRequests AS jr
            LEFT JOIN dbo.Departments AS d ON d.DepartmentId = jr.DepartmentId
            LEFT JOIN dbo.Locations AS l ON l.LocationId = jr.LocationId
            LEFT JOIN dbo.WorkflowAssignments AS wa ON wa.WorkflowAssignmentId = jr.CurrentAssignmentId
            LEFT JOIN dbo.Groups AS g ON g.GroupId = wa.AssignedToGroupId
            LEFT JOIN dbo.Roles AS r ON r.TenantId = wa.TenantId AND r.RoleId = wa.AssignedToRoleId
            LEFT JOIN dbo.JobRequestSkills AS jrs ON jrs.TenantId = jr.TenantId AND jrs.JobRequestId = jr.JobRequestId
            LEFT JOIN dbo.Skills AS s ON s.SkillId = jrs.SkillId
            WHERE jr.TenantId = @TenantId
              AND
              (
                  EXISTS
                  (
                      SELECT 1
                      FROM dbo.UserRoles AS ur
                      INNER JOIN dbo.Roles AS r
                          ON r.TenantId = ur.TenantId
                          AND r.RoleId = ur.RoleId
                          AND r.Code = @TenantAdminRoleCode
                          AND r.Status = N'Active'
                      WHERE ur.TenantId = jr.TenantId
                        AND ur.UserId = @UserId
                  )
                  OR jr.CreatedByUserId = @UserId
                  OR jr.HiringManagerUserId = @UserId
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.WorkflowAssignments AS wav
                      WHERE wav.TenantId = jr.TenantId
                        AND wav.EntityType = N'JobRequest'
                        AND wav.EntityId = jr.JobRequestId
                        AND (
                            wav.AssignedToUserId = @UserId
                            OR wav.ClaimedByUserId = @UserId
                            OR EXISTS
                            (
                                SELECT 1
                                FROM dbo.GroupMembers AS gm
                                INNER JOIN dbo.Groups AS g
                                    ON g.TenantId = gm.TenantId
                                    AND g.GroupId = gm.GroupId
                                    AND g.Status = N'Active'
                                WHERE gm.TenantId = wav.TenantId
                                  AND gm.GroupId = wav.AssignedToGroupId
                                  AND gm.UserId = @UserId
                            )
                            OR EXISTS
                            (
                                SELECT 1
                                FROM dbo.UserRoles AS ur
                                INNER JOIN dbo.Roles AS r
                                    ON r.TenantId = ur.TenantId
                                    AND r.RoleId = ur.RoleId
                                    AND r.Status = N'Active'
                                WHERE ur.TenantId = wav.TenantId
                                  AND ur.UserId = @UserId
                                  AND ur.RoleId = wav.AssignedToRoleId
                            )
                        )
                  )
              )
            GROUP BY
                jr.JobRequestId,
                jr.RequestCode,
                jr.Title,
                jr.ClientName,
                jr.ClientContext,
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
                r.Code,
                r.Name,
                jr.PublishStatus,
                jr.CreatedAtUtc
            ORDER BY jr.CreatedAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<JobRequestRow>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    UserId = userId,
                    TenantAdminRoleCode = AccessConstants.TenantAdminRoleCode
                },
                cancellationToken: cancellationToken));

        return rows.Select(ToJobRequest).ToArray();
    }

    private static async Task<IReadOnlyList<OperationsWorkflowAssignment>> ListAssignmentsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                wa.WorkflowAssignmentId AS Id,
                wa.EntityType,
                wa.EntityId,
                COALESCE(ws.Name, wa.EntityType) AS Stage,
                COALESCE(
                    g.Name,
                    CASE WHEN r.Code = N'TenantAdmin' THEN N'Tenant Admins' ELSE r.Name END
                ) AS AssignedToGroupId,
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
            LEFT JOIN dbo.Roles AS r ON r.TenantId = wa.TenantId AND r.RoleId = wa.AssignedToRoleId
            WHERE wa.TenantId = @TenantId
              AND wa.AssignmentStatus IN (N'Pending', N'Claimed', N'Completed')
              AND
              (
                  EXISTS
                  (
                      SELECT 1
                      FROM dbo.UserRoles AS ur
                      INNER JOIN dbo.Roles AS r
                          ON r.TenantId = ur.TenantId
                          AND r.RoleId = ur.RoleId
                          AND r.Code = @TenantAdminRoleCode
                          AND r.Status = N'Active'
                      WHERE ur.TenantId = wa.TenantId
                        AND ur.UserId = @UserId
                  )
                  OR wa.AssignedToUserId = @UserId
                  OR wa.ClaimedByUserId = @UserId
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.GroupMembers AS gm
                      INNER JOIN dbo.Groups AS activeGroup
                          ON activeGroup.TenantId = gm.TenantId
                          AND activeGroup.GroupId = gm.GroupId
                          AND activeGroup.Status = N'Active'
                      WHERE gm.TenantId = wa.TenantId
                        AND gm.GroupId = wa.AssignedToGroupId
                        AND gm.UserId = @UserId
                  )
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.UserRoles AS ur
                      INNER JOIN dbo.Roles AS r
                          ON r.TenantId = ur.TenantId
                          AND r.RoleId = ur.RoleId
                          AND r.Status = N'Active'
                      WHERE ur.TenantId = wa.TenantId
                        AND ur.UserId = @UserId
                        AND ur.RoleId = wa.AssignedToRoleId
                  )
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.JobRequests AS jr
                      WHERE jr.TenantId = wa.TenantId
                        AND wa.EntityType = N'JobRequest'
                        AND jr.JobRequestId = wa.EntityId
                        AND (jr.CreatedByUserId = @UserId OR jr.HiringManagerUserId = @UserId)
                  )
              )
            ORDER BY wa.AssignedAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<WorkflowAssignmentRow>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    UserId = userId,
                    TenantAdminRoleCode = AccessConstants.TenantAdminRoleCode
                },
                cancellationToken: cancellationToken));

        return rows
            .Select(row => new OperationsWorkflowAssignment(
                row.Id,
                row.EntityType,
                row.EntityId,
                row.Stage,
                row.AssignedToGroupId,
                row.AssignedToUserId,
                row.ClaimedByUserId,
                row.Status,
                Utc(row.AssignedAt)))
            .ToArray();
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
                COALESCE(nr.Title, ne.Name) AS Title,
                COALESCE(nr.Message, CONCAT(ne.Name, N' is ready in Talent Pilot.')) AS Message,
                COALESCE(nr.Category, N'Workflow') AS Category,
                COALESCE(nr.Severity, N'Info') AS Severity,
                COALESCE(nr.EntityType, N'WorkflowAssignment') AS EntityType,
                COALESCE(nr.EntityId, nr.NotificationEventId) AS EntityId,
                nr.ReadAtUtc AS ReadAt,
                nr.CreatedAtUtc AS CreatedAt,
                nr.MetadataJson
            FROM dbo.NotificationRecipients AS nr
            INNER JOIN dbo.NotificationEvents AS ne ON ne.NotificationEventId = nr.NotificationEventId
            WHERE nr.TenantId = @TenantId
              AND nr.RecipientUserId = @UserId
            ORDER BY nr.CreatedAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<NotificationRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));

        return rows
            .Select(row => new OperationsNotification(
                row.Id,
                row.RecipientUserId,
                row.Title,
                row.Message,
                row.Category,
                row.Severity,
                row.EntityType,
                row.EntityId,
                ToUtc(row.ReadAt),
                Utc(row.CreatedAt),
                ParseStringMetadata(row.MetadataJson)))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> ParseStringMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string>();
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.Value.ToString(),
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(value))
                {
                    values[property.Name] = value;
                }
            }

            return values;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static async Task<OperationsJobRequest?> GetJobRequestByIdAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid userId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        var items = await ListJobRequestsAsync(connection, tenantId, userId, cancellationToken);
        return items.FirstOrDefault(item => item.Id == jobRequestId);
    }

    private static async Task<OperationsWorkflowAssignment?> GetAssignmentByIdAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid userId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var items = await ListAssignmentsAsync(connection, tenantId, userId, cancellationToken);
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
            row.ClientContext,
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
            Utc(row.CreatedAt));
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
            "PRESALES_REVIEW" => "Presales Review",
            "SOURCING" => "Recruiter Sourcing",
            "INTERVIEWING" => "Interviewing",
            "HIRING_MANAGER_REVIEW" => "Hiring Manager Review",
            "OFFER" => "Offer Outcome",
            "CLOSED" => "Closed",
            _ => status switch
            {
                "PMOReview" => "PMO Review",
                "PresalesReview" => "Presales Review",
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

    private static async Task<IReadOnlySet<string>> ReadActorRoleCodesAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.Code
            FROM dbo.UserRoles AS ur
            INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId
            WHERE ur.TenantId = @TenantId
              AND ur.UserId = @ActorUserId
              AND r.Status = N'Active';
            """;

        var roleCodes = await connection.QueryAsync<string>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActorUserId = actorUserId },
            transaction,
            cancellationToken: cancellationToken));

        return roleCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<WorkflowAssignmentTarget> ResolveDepartmentIntakeAssignmentAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                r.AssignmentType,
                CASE WHEN u.UserId IS NOT NULL THEN r.TargetUserId ELSE NULL END AS TargetUserId,
                CASE
                    WHEN g.GroupId IS NOT NULL
                         AND EXISTS
                         (
                             SELECT 1
                             FROM dbo.GroupMembers AS gm
                             INNER JOIN dbo.AppUsers AS gu
                                 ON gu.TenantId = gm.TenantId
                                 AND gu.UserId = gm.UserId
                                 AND gu.AccountStatus = N'Active'
                                 AND gu.DeletedAtUtc IS NULL
                             WHERE gm.TenantId = r.TenantId
                               AND gm.GroupId = r.TargetGroupId
                         ) THEN r.TargetGroupId
                    ELSE NULL
                END AS TargetGroupId
            FROM dbo.JobRequestIntakeRoutingRules AS r
            LEFT JOIN dbo.AppUsers AS u
                ON u.TenantId = r.TenantId
                AND u.UserId = r.TargetUserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            LEFT JOIN dbo.Groups AS g
                ON g.TenantId = r.TenantId
                AND g.GroupId = r.TargetGroupId
                AND g.Status = N'Active'
            WHERE r.TenantId = @TenantId
              AND r.DepartmentId = @DepartmentId
              AND r.Status = N'Active';
            """;

        var rule = await connection.QuerySingleOrDefaultAsync<IntakeRoutingRuleRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, DepartmentId = departmentId },
            transaction,
            cancellationToken: cancellationToken));

        return rule switch
        {
            { AssignmentType: "User", TargetUserId: not null } =>
                WorkflowAssignmentTarget.ForUser(rule.TargetUserId.Value, "department intake user"),
            { AssignmentType: "Group", TargetGroupId: not null } =>
                WorkflowAssignmentTarget.ForGroup(rule.TargetGroupId.Value, "department intake group"),
            _ => await TenantAdminFallbackAsync(connection, transaction, tenantId, cancellationToken)
        };
    }

    private static async Task<WorkflowAssignmentTarget> TenantAdminFallbackAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) RoleId
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND Code = N'TenantAdmin'
              AND Status = N'Active';
            """;

        var roleId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            transaction,
            cancellationToken: cancellationToken));

        return roleId.HasValue
            ? WorkflowAssignmentTarget.ForRole(roleId.Value, "Tenant Admin fallback")
            : throw new InvalidOperationException("TenantAdmin role seed data is missing.");
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
        IReadOnlyList<Guid> skillIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.JobRequestSkills (TenantId, JobRequestId, SkillId, IsRequired, Weight, CreatedAtUtc)
            SELECT @TenantId, @JobRequestId, SkillId, 1, 10, SYSUTCDATETIME()
            FROM dbo.Skills
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND SkillId IN @SkillIds
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.JobRequestSkills AS existing
                  WHERE existing.TenantId = @TenantId
                    AND existing.JobRequestId = @JobRequestId
                    AND existing.SkillId = dbo.Skills.SkillId
              );
            """;

        var distinctSkillIds = (skillIds ?? Array.Empty<Guid>())
            .Where(skillId => skillId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (distinctSkillIds.Length == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId, SkillIds = distinctSkillIds },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<OperationsEmployeeReferral>> ListEmployeeReferralsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                referral.JobRequestEmployeeReferralId AS ReferralId,
                referral.JobRequestId,
                referral.EmployeeId,
                employee.DisplayName AS EmployeeName,
                employee.Email AS EmployeeEmail,
                employee.Designation,
                COALESCE(department.Name, N'Unassigned') AS Department,
                employee.ExperienceYears,
                referral.ReferredByUserId,
                referredBy.DisplayName AS ReferredByName,
                referral.PresalesUserId,
                presales.DisplayName AS PresalesName,
                referral.Status,
                referral.FitScore,
                referral.RecommendationSummary,
                referral.ClientFeedback,
                referral.CreatedAtUtc AS CreatedAt
            FROM dbo.JobRequestEmployeeReferrals AS referral
            INNER JOIN dbo.Employees AS employee
                ON employee.TenantId = referral.TenantId
                AND employee.EmployeeId = referral.EmployeeId
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = employee.TenantId
                AND department.DepartmentId = employee.DepartmentId
            INNER JOIN dbo.AppUsers AS referredBy
                ON referredBy.TenantId = referral.TenantId
                AND referredBy.UserId = referral.ReferredByUserId
            LEFT JOIN dbo.AppUsers AS presales
                ON presales.TenantId = referral.TenantId
                AND presales.UserId = referral.PresalesUserId
            WHERE referral.TenantId = @TenantId
              AND referral.JobRequestId = @JobRequestId
            ORDER BY referral.CreatedAtUtc DESC, employee.DisplayName;
            """;

        var rows = await connection.QueryAsync<EmployeeReferralRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken));

        return rows
            .Select(row => new OperationsEmployeeReferral(
                row.ReferralId,
                row.JobRequestId,
                row.EmployeeId,
                row.EmployeeName,
                row.EmployeeEmail,
                row.Designation,
                row.Department,
                row.ExperienceYears,
                row.ReferredByUserId,
                row.ReferredByName,
                row.PresalesUserId,
                row.PresalesName,
                row.Status,
                row.FitScore,
                row.RecommendationSummary,
                row.ClientFeedback,
                Utc(row.CreatedAt)))
            .ToArray();
    }

    private static async Task<IReadOnlyList<OperationsBenchEmployee>> ListEligibleBenchEmployeesAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string requestSkillsSql = """
            SELECT skill.SkillId, skill.Name
            FROM dbo.JobRequestSkills AS requestSkill
            INNER JOIN dbo.Skills AS skill
                ON skill.TenantId = requestSkill.TenantId
                AND skill.SkillId = requestSkill.SkillId
            WHERE requestSkill.TenantId = @TenantId
              AND requestSkill.JobRequestId = @JobRequestId;
            """;

        const string requestContextSql = """
            SELECT
                COALESCE(department.Name, N'') AS Department,
                COALESCE(location.Name, N'') AS Location
            FROM dbo.JobRequests AS request
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = request.TenantId
                AND department.DepartmentId = request.DepartmentId
            LEFT JOIN dbo.Locations AS location
                ON location.TenantId = request.TenantId
                AND location.LocationId = request.LocationId
            WHERE request.TenantId = @TenantId
              AND request.JobRequestId = @JobRequestId;
            """;

        const string employeesSql = """
            SELECT
                employee.EmployeeId,
                employee.DisplayName,
                employee.Email,
                employee.Designation,
                COALESCE(employee.DepartmentName, N'Unassigned') AS Department,
                COALESCE(employee.LocationName, N'Unassigned') AS Location,
                employee.ExperienceYears,
                employee.JoiningDate,
                employee.AvailabilityStatus,
                employee.BenchStatus,
                employee.IsCurrentlyBenched,
                skill.SkillId,
                skill.Name AS SkillName
            FROM dbo.vw_EmployeeBenchAvailability AS employee
            LEFT JOIN dbo.EmployeeSkills AS employeeSkill
                ON employeeSkill.TenantId = employee.TenantId
                AND employeeSkill.EmployeeId = employee.EmployeeId
            LEFT JOIN dbo.Skills AS skill
                ON skill.TenantId = employeeSkill.TenantId
                AND skill.SkillId = employeeSkill.SkillId
                AND skill.Status = N'Active'
            WHERE employee.TenantId = @TenantId
              AND employee.IsCurrentlyBenched = CAST(1 AS BIT)
              AND employee.BenchStatus IN (N'Benched', N'PartialBench')
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.JobRequestEmployeeReferrals AS referral
                  WHERE referral.TenantId = employee.TenantId
                    AND referral.JobRequestId = @JobRequestId
                    AND referral.EmployeeId = employee.EmployeeId
                    AND referral.Status IN (N'Referred', N'AcceptedByPresales', N'ClientAccepted')
              )
            ORDER BY employee.DisplayName, skill.Name;
            """;

        const string projectsSql = """
            SELECT
                assignment.EmployeeId,
                project.Name AS ProjectName,
                project.ClientName,
                assignment.Status,
                assignment.AllocationPercent,
                assignment.StartsOn,
                assignment.EndsOn
            FROM dbo.EmployeeProjectAssignments AS assignment
            INNER JOIN dbo.Projects AS project
                ON project.TenantId = assignment.TenantId
                AND project.ProjectId = assignment.ProjectId
            WHERE assignment.TenantId = @TenantId
              AND assignment.Status IN (N'Active', N'Completed')
            ORDER BY assignment.StartsOn DESC, project.Name;
            """;

        var requestSkills = (await connection.QueryAsync<EmployeeSkillRow>(new CommandDefinition(
            requestSkillsSql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken))).ToArray();
        var requestSkillNames = requestSkills.ToDictionary(row => row.SkillId, row => row.Name);
        var requestContext = await connection.QuerySingleOrDefaultAsync<BenchRequestContextRow>(new CommandDefinition(
            requestContextSql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken));
        var rows = (await connection.QueryAsync<BenchEmployeeSkillRow>(new CommandDefinition(
            employeesSql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken))).ToArray();
        var projectRows = (await connection.QueryAsync<EmployeeProjectEvidenceRow>(new CommandDefinition(
            projectsSql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken))).ToArray();
        var projectsByEmployee = projectRows
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(row => new OperationsEmployeeProjectEvidence(
                        row.ProjectName,
                        row.ClientName,
                        row.Status,
                        row.AllocationPercent,
                        row.StartsOn.HasValue ? DateOnly.FromDateTime(row.StartsOn.Value) : null,
                        row.EndsOn.HasValue ? DateOnly.FromDateTime(row.EndsOn.Value) : null))
                    .ToArray() as IReadOnlyList<OperationsEmployeeProjectEvidence>);

        var employees = rows
            .GroupBy(row => new
            {
                row.EmployeeId,
                row.DisplayName,
                row.Email,
                row.Designation,
                row.Department,
                row.Location,
                row.ExperienceYears,
                row.JoiningDate,
                row.AvailabilityStatus,
                row.BenchStatus,
                row.IsCurrentlyBenched
            })
            .Select(group =>
            {
                var employeeSkillIds = group
                    .Where(row => row.SkillId.HasValue)
                    .Select(row => row.SkillId!.Value)
                    .ToHashSet();
                var employeeSkills = group
                    .Where(row => !string.IsNullOrWhiteSpace(row.SkillName))
                    .Select(row => row.SkillName!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var matchedSkills = requestSkillNames
                    .Where(item => employeeSkillIds.Contains(item.Key))
                    .Select(item => item.Value)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var missingSkills = requestSkillNames
                    .Where(item => !employeeSkillIds.Contains(item.Key))
                    .Select(item => item.Value)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new OperationsBenchEmployee(
                    group.Key.EmployeeId,
                    group.Key.DisplayName,
                    group.Key.Email,
                    group.Key.Designation,
                    group.Key.Department,
                    group.Key.Location,
                    group.Key.ExperienceYears,
                    group.Key.JoiningDate.HasValue ? DateOnly.FromDateTime(group.Key.JoiningDate.Value) : null,
                    group.Key.AvailabilityStatus,
                    group.Key.BenchStatus,
                    group.Key.IsCurrentlyBenched,
                    employeeSkills,
                    matchedSkills,
                    missingSkills,
                    projectsByEmployee.TryGetValue(group.Key.EmployeeId, out var projectEvidence)
                        ? projectEvidence
                        : []);
            })
            .Where(employee => IsRelevantBenchEmployee(
                employee,
                requestContext?.Department ?? string.Empty,
                requestSkillNames.Count))
            .OrderByDescending(employee => employee.MatchedSkills.Count)
            .ThenByDescending(employee => ScoreLocationFit(requestContext?.Location ?? string.Empty, employee.Location))
            .ThenByDescending(employee => employee.ExperienceYears ?? 0)
            .ThenBy(employee => employee.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return employees;
    }

    private static bool IsRelevantBenchEmployee(
        OperationsBenchEmployee employee,
        string requestDepartment,
        int requestedSkillCount)
    {
        var matchedCount = employee.MatchedSkills.Count;
        var sameDepartment = !string.IsNullOrWhiteSpace(requestDepartment) &&
            employee.Department.Equals(requestDepartment, StringComparison.OrdinalIgnoreCase);

        if (requestedSkillCount <= 0)
        {
            return sameDepartment;
        }

        if (sameDepartment)
        {
            return matchedCount > 0;
        }

        var coverage = (decimal)matchedCount / requestedSkillCount;
        return matchedCount >= 2 || coverage >= 0.5m;
    }

    private static decimal ScoreLocationFit(string requestLocation, string employeeLocation)
    {
        var requested = NormalizeComparable(requestLocation);
        var employee = NormalizeComparable(employeeLocation);
        if (requested.Length == 0 || employee.Length == 0 || employee == "unassigned")
        {
            return 0.4m;
        }

        if (requested == "remote")
        {
            return employee == "remote" ? 1m : 0.85m;
        }

        if (employee == requested)
        {
            return 1m;
        }

        if (employee == "remote")
        {
            return 0.75m;
        }

        if (employee.Contains(requested, StringComparison.OrdinalIgnoreCase) ||
            requested.Contains(employee, StringComparison.OrdinalIgnoreCase))
        {
            return 0.85m;
        }

        return 0.35m;
    }

    private static string NormalizeComparable(string? value)
    {
        return string.Join(' ', (value ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Trim()
            .ToLowerInvariant();
    }

    private static async Task<JobRequestExperienceRow?> ReadJobRequestExperienceAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ExperienceMinYears, ExperienceMaxYears
            FROM dbo.JobRequests
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        return await connection.QuerySingleOrDefaultAsync<JobRequestExperienceRow>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, JobRequestId = jobRequestId },
                cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<OperationsRediscoveryCandidate>> ListRediscoveryCandidatesAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        Guid? jobPostId,
        IReadOnlyList<string> requiredSkills,
        CancellationToken cancellationToken)
    {
        const string candidateSql = """
            SELECT
                candidate.CandidateId,
                candidate.DisplayName,
                candidate.Email,
                candidate.Status,
                candidate.CurrentDesignation,
                candidate.CurrentCompany,
                candidate.ExperienceYears,
                candidate.NoticePeriodDays,
                skill.SkillId,
                skill.Name AS SkillName
            FROM dbo.Candidates AS candidate
            LEFT JOIN dbo.CandidateSkills AS candidateSkill
                ON candidateSkill.TenantId = candidate.TenantId
                AND candidateSkill.CandidateId = candidate.CandidateId
            LEFT JOIN dbo.Skills AS skill
                ON skill.TenantId = candidateSkill.TenantId
                AND skill.SkillId = candidateSkill.SkillId
                AND skill.Status = N'Active'
            WHERE candidate.TenantId = @TenantId
              AND candidate.Status = N'Active'
              AND EXISTS
              (
                  SELECT 1
                  FROM dbo.JobApplications AS history
                  WHERE history.TenantId = candidate.TenantId
                    AND history.CandidateId = candidate.CandidateId
                    AND history.JobRequestId <> @JobRequestId
                    AND
                    (
                        history.IsActive = CAST(0 AS BIT)
                        OR history.CurrentStatus IN (N'Rejected', N'Withdrawn', N'OfferDeclined', N'OnHold')
                        OR history.FinalDecisionAtUtc IS NOT NULL
                        OR EXISTS
                        (
                            SELECT 1
                            FROM dbo.Interviews AS interview
                            WHERE interview.TenantId = history.TenantId
                              AND interview.JobApplicationId = history.JobApplicationId
                        )
                    )
              )
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.JobApplications AS activeApplication
                  WHERE activeApplication.TenantId = candidate.TenantId
                    AND activeApplication.CandidateId = candidate.CandidateId
                    AND activeApplication.IsActive = CAST(1 AS BIT)
                    AND activeApplication.JobRequestId = @JobRequestId
                    AND activeApplication.CurrentStatus IN (N'Invited', N'Applied', N'Screening', N'Interviewing', N'OnHold')
              )
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.JobApplications AS hiredApplication
                  WHERE hiredApplication.TenantId = candidate.TenantId
                    AND hiredApplication.CandidateId = candidate.CandidateId
                    AND hiredApplication.CurrentStatus IN (N'Hired', N'Joined')
              )
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.JobApplications AS activeOtherApplication
                  INNER JOIN dbo.JobRequests AS activeOtherRequest
                      ON activeOtherRequest.TenantId = activeOtherApplication.TenantId
                      AND activeOtherRequest.JobRequestId = activeOtherApplication.JobRequestId
                  WHERE activeOtherApplication.TenantId = candidate.TenantId
                    AND activeOtherApplication.CandidateId = candidate.CandidateId
                    AND activeOtherApplication.IsActive = CAST(1 AS BIT)
                    AND activeOtherApplication.JobRequestId <> @JobRequestId
                    AND activeOtherApplication.CurrentStatus IN (N'Invited', N'Applied', N'Screening', N'Interviewing')
                    AND activeOtherRequest.CurrentStageKey <> N'CLOSED'
              )
              AND
              (
                  @JobPostId IS NULL
                  OR NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.JobApplications AS postApplication
                      WHERE postApplication.TenantId = candidate.TenantId
                        AND postApplication.CandidateId = candidate.CandidateId
                        AND postApplication.JobPostId = @JobPostId
                        AND postApplication.IsActive = CAST(1 AS BIT)
                  )
              )
            ORDER BY candidate.DisplayName, skill.Name;
            """;

        var rows = (await connection.QueryAsync<RediscoveryCandidateSkillRow>(new CommandDefinition(
            candidateSql,
            new { TenantId = tenantId, JobRequestId = jobRequestId, JobPostId = jobPostId },
            cancellationToken: cancellationToken))).ToArray();

        if (rows.Length == 0)
        {
            return [];
        }

        var candidateIds = rows.Select(row => row.CandidateId).Distinct().ToArray();
        var applications = await ListRediscoveryApplicationEvidenceAsync(connection, tenantId, jobRequestId, candidateIds, cancellationToken);
        var documentsByApplication = await ListApplicantDocumentEvidenceAsync(
            connection,
            tenantId,
            applications.Select(application => application.JobApplicationId).Distinct().ToArray(),
            cancellationToken);
        var interviews = await ListRediscoveryInterviewEvidenceAsync(
            connection,
            tenantId,
            applications.Select(application => application.JobApplicationId).Distinct().ToArray(),
            cancellationToken);
        var interviewEvidenceByApplication = interviews
            .GroupBy(interview => interview.JobApplicationId)
            .ToDictionary(group => group.Key, group => group.Select(ToInterviewEvidence).ToArray());
        var applicationLookup = applications
            .GroupBy(application => application.CandidateId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(application =>
                        ToApplicationEvidence(
                            application,
                            interviewEvidenceByApplication.TryGetValue(application.JobApplicationId, out var applicationInterviews)
                                ? applicationInterviews
                                : [],
                            documentsByApplication.TryGetValue(application.JobApplicationId, out var applicationDocuments)
                                ? applicationDocuments
                                : []))
                    .ToArray());
        var interviewLookup = interviews
            .GroupBy(interview => interview.CandidateId)
            .ToDictionary(group => group.Key, group => group.Select(ToInterviewEvidence).ToArray());
        var requiredSkillSet = requiredSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows
            .GroupBy(row => row.CandidateId)
            .Select(group =>
            {
                var first = group.First();
                var skills = group
                    .Select(row => row.SkillName)
                    .Where(skill => !string.IsNullOrWhiteSpace(skill))
                    .Select(skill => skill!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(skill => skill)
                    .ToArray();
                var candidateSkillSet = skills.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var matchedSkills = requiredSkillSet
                    .Where(candidateSkillSet.Contains)
                    .OrderBy(skill => skill)
                    .ToArray();
                var missingSkills = requiredSkillSet
                    .Where(skill => !candidateSkillSet.Contains(skill))
                    .OrderBy(skill => skill)
                    .ToArray();

                return new OperationsRediscoveryCandidate(
                    first.CandidateId,
                    first.DisplayName,
                    first.Email,
                    first.Status,
                    first.CurrentDesignation,
                    first.CurrentCompany,
                    first.ExperienceYears,
                    first.NoticePeriodDays,
                    skills,
                    matchedSkills,
                    missingSkills,
                    applicationLookup.TryGetValue(first.CandidateId, out var candidateApplications) ? candidateApplications : [],
                    interviewLookup.TryGetValue(first.CandidateId, out var candidateInterviews) ? candidateInterviews : []);
            })
            .Where(candidate => candidate.ApplicationEvidence.Count > 0)
            .ToArray();
    }

    private static async Task<IReadOnlyList<RediscoveryApplicationEvidenceRow>> ListRediscoveryApplicationEvidenceAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        IReadOnlyList<Guid> candidateIds,
        CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT
                application.CandidateId,
                application.JobApplicationId,
                application.JobRequestId,
                application.JobPostId,
                post.Title AS JobPostTitle,
                post.Status AS JobPostStatus,
                request.RequestCode,
                request.Title AS JobTitle,
                COALESCE(post.Title, request.Title) AS DisplayJobTitle,
                COALESCE(request.ClientName, N'Internal') AS Client,
                COALESCE(department.Name, N'Unassigned') AS Department,
                COALESCE(location.Name, N'Remote') AS Location,
                application.CurrentStatus AS Status,
                application.SourceLabel,
                application.CoverLetterText,
                application.AppliedAtUtc AS AppliedAt,
                application.FinalDecisionAtUtc AS FinalDecisionAt,
                application.FinalDecisionReason,
                latestOffer.StartDate AS OfferStartDate
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            LEFT JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = request.TenantId
                AND department.DepartmentId = request.DepartmentId
            LEFT JOIN dbo.Locations AS location
                ON location.TenantId = request.TenantId
                AND location.LocationId = request.LocationId
            OUTER APPLY
            (
                SELECT TOP (1) offer.StartDate
                FROM dbo.OfferLetters AS offer
                WHERE offer.TenantId = application.TenantId
                  AND offer.JobApplicationId = application.JobApplicationId
                ORDER BY offer.Version DESC, offer.UpdatedAtUtc DESC
            ) AS latestOffer
            WHERE application.TenantId = @TenantId
              AND application.CandidateId IN @CandidateIds
              AND application.JobRequestId <> @JobRequestId
            ORDER BY application.AppliedAtUtc DESC;
            """;

        return (await connection.QueryAsync<RediscoveryApplicationEvidenceRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, CandidateIds = candidateIds, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken))).ToArray();
    }

    private static async Task<IReadOnlyList<OperationsHistoricalApplicationSummary>> ListCandidateApplicationSummariesAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                application.CandidateId,
                application.JobApplicationId,
                application.JobRequestId,
                application.JobPostId,
                post.Title AS JobPostTitle,
                post.Status AS JobPostStatus,
                request.RequestCode,
                request.Title AS JobTitle,
                COALESCE(post.Title, request.Title) AS DisplayJobTitle,
                COALESCE(request.ClientName, N'Internal') AS Client,
                COALESCE(department.Name, N'Unassigned') AS Department,
                COALESCE(location.Name, N'Remote') AS Location,
                application.CurrentStatus AS Status,
                application.SourceLabel,
                application.CoverLetterText,
                application.AppliedAtUtc AS AppliedAt,
                application.FinalDecisionAtUtc AS FinalDecisionAt,
                application.FinalDecisionReason,
                latestOffer.StartDate AS OfferStartDate
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            LEFT JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = request.TenantId
                AND department.DepartmentId = request.DepartmentId
            LEFT JOIN dbo.Locations AS location
                ON location.TenantId = request.TenantId
                AND location.LocationId = request.LocationId
            OUTER APPLY
            (
                SELECT TOP (1) offer.StartDate
                FROM dbo.OfferLetters AS offer
                WHERE offer.TenantId = application.TenantId
                  AND offer.JobApplicationId = application.JobApplicationId
                ORDER BY offer.Version DESC, offer.UpdatedAtUtc DESC
            ) AS latestOffer
            WHERE application.TenantId = @TenantId
              AND application.CandidateId = @CandidateId
            ORDER BY application.AppliedAtUtc DESC;
            """;

        var applications = (await connection.QueryAsync<RediscoveryApplicationEvidenceRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, CandidateId = candidateId },
            cancellationToken: cancellationToken))).ToArray();
        if (applications.Length == 0)
        {
            return [];
        }

        var interviews = await ListRediscoveryInterviewEvidenceAsync(
            connection,
            tenantId,
            applications.Select(application => application.JobApplicationId).Distinct().ToArray(),
            cancellationToken);
        var interviewEvidenceByApplication = interviews
            .GroupBy(interview => interview.JobApplicationId)
            .ToDictionary(group => group.Key, group => group.Select(ToInterviewEvidence).ToArray());

        return applications
            .Select(application => ToHistoricalApplicationSummary(
                application,
                interviewEvidenceByApplication.TryGetValue(application.JobApplicationId, out var applicationInterviews)
                    ? applicationInterviews
                    : []))
            .ToArray();
    }

    private static async Task<IReadOnlyList<OperationsCandidateMeetingEvent>> ListCandidateMeetingEventsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        const string meetingSql = """
            SELECT
                interview.InterviewId,
                application.JobApplicationId,
                application.JobRequestId,
                application.JobPostId,
                request.RequestCode,
                COALESCE(post.Title, request.Title) AS JobTitle,
                COALESCE(request.ClientName, N'Internal') AS Client,
                COALESCE(postRound.Name, N'Interview') AS RoundName,
                interview.Status,
                interview.StartsAtUtc AS StartsAt,
                interview.DurationMinutes,
                interview.MeetingLink,
                interview.CalendarProvider,
                interview.CalendarEventId,
                interview.CalendarEventHtmlLink,
                interview.LocationText
            FROM dbo.Interviews AS interview
            INNER JOIN dbo.JobApplications AS application
                ON application.TenantId = interview.TenantId
                AND application.JobApplicationId = interview.JobApplicationId
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            LEFT JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            LEFT JOIN dbo.JobPostInterviewRounds AS postRound
                ON postRound.TenantId = interview.TenantId
                AND postRound.JobPostInterviewRoundId = interview.JobPostInterviewRoundId
            WHERE interview.TenantId = @TenantId
              AND application.CandidateId = @CandidateId
            ORDER BY interview.StartsAtUtc DESC;
            """;

        var meetings = (await connection.QueryAsync<CandidateMeetingEventRow>(new CommandDefinition(
            meetingSql,
            new { TenantId = tenantId, CandidateId = candidateId },
            cancellationToken: cancellationToken))).ToArray();
        if (meetings.Length == 0)
        {
            return [];
        }

        const string participantSql = """
            SELECT
                InterviewId,
                DisplayName,
                Email,
                ParticipantRole AS Role,
                IsOptional
            FROM dbo.InterviewParticipants
            WHERE TenantId = @TenantId
              AND InterviewId IN @InterviewIds
            ORDER BY
                CASE ParticipantRole
                    WHEN N'Candidate' THEN 1
                    WHEN N'Interviewer' THEN 2
                    WHEN N'HiringManager' THEN 3
                    WHEN N'Recruiter' THEN 4
                    ELSE 5
                END,
                DisplayName;
            """;
        var participants = (await connection.QueryAsync<CandidateMeetingParticipantRow>(new CommandDefinition(
            participantSql,
            new { TenantId = tenantId, InterviewIds = meetings.Select(meeting => meeting.InterviewId).ToArray() },
            cancellationToken: cancellationToken))).ToArray();
        var participantsByInterview = participants
            .GroupBy(participant => participant.InterviewId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(participant => new OperationsCandidateMeetingParticipant(
                        participant.DisplayName,
                        participant.Email,
                        participant.Role,
                        participant.IsOptional))
                    .ToArray());

        return meetings
            .Select(meeting => new OperationsCandidateMeetingEvent(
                meeting.InterviewId,
                meeting.JobApplicationId,
                meeting.JobRequestId,
                meeting.JobPostId,
                meeting.RequestCode,
                meeting.JobTitle,
                meeting.Client,
                meeting.RoundName,
                meeting.Status,
                Utc(meeting.StartsAt),
                meeting.DurationMinutes,
                meeting.MeetingLink,
                meeting.CalendarProvider,
                meeting.CalendarEventId,
                meeting.CalendarEventHtmlLink,
                meeting.LocationText,
                participantsByInterview.TryGetValue(meeting.InterviewId, out var meetingParticipants)
                    ? meetingParticipants
                    : []))
            .ToArray();
    }

    private static async Task<IReadOnlyList<OperationsApplicantRankingApplication>> ListApplicantRankingApplicationsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        Guid jobPostId,
        IReadOnlyList<string> requiredSkills,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                application.JobApplicationId,
                application.JobPostId,
                application.JobRequestId,
                application.CandidateId,
                candidate.DisplayName AS CandidateName,
                candidate.Email AS CandidateEmail,
                candidate.Status AS CandidateStatus,
                candidate.CurrentDesignation,
                candidate.CurrentCompany,
                candidate.ExperienceYears,
                candidate.NoticePeriodDays,
                application.CurrentStatus AS ApplicationStatus,
                application.SourceLabel,
                application.SourceDetail,
                application.CoverLetterText,
                application.AppliedAtUtc AS AppliedAt,
                application.ApplicationSnapshotJson,
                skill.Name AS SkillName
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            LEFT JOIN dbo.CandidateSkills AS candidateSkill
                ON candidateSkill.TenantId = candidate.TenantId
                AND candidateSkill.CandidateId = candidate.CandidateId
            LEFT JOIN dbo.Skills AS skill
                ON skill.TenantId = candidateSkill.TenantId
                AND skill.SkillId = candidateSkill.SkillId
                AND skill.Status = N'Active'
            WHERE application.TenantId = @TenantId
              AND application.JobRequestId = @JobRequestId
              AND application.JobPostId = @JobPostId
              AND application.IsActive = CAST(1 AS BIT)
              AND candidate.Status = N'Active'
              AND application.CurrentStatus NOT IN (N'Rejected', N'Withdrawn', N'Joined', N'Hired', N'OfferDeclined')
            ORDER BY application.AppliedAtUtc DESC, candidate.DisplayName, skill.Name;
            """;

        var rows = (await connection.QueryAsync<ApplicantRankingApplicationSkillRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId, JobPostId = jobPostId },
            cancellationToken: cancellationToken))).ToArray();
        if (rows.Length == 0)
        {
            return [];
        }

        var applicationIds = rows.Select(row => row.JobApplicationId).Distinct().ToArray();
        var candidateIds = rows.Select(row => row.CandidateId).Distinct().ToArray();
        var documentsByApplication = await ListApplicantDocumentEvidenceAsync(connection, tenantId, applicationIds, cancellationToken);
        var historicalApplications = await ListRediscoveryApplicationEvidenceAsync(
            connection,
            tenantId,
            jobRequestId,
            candidateIds,
            cancellationToken);
        var historicalDocumentsByApplication = await ListApplicantDocumentEvidenceAsync(
            connection,
            tenantId,
            historicalApplications.Select(application => application.JobApplicationId).Distinct().ToArray(),
            cancellationToken);
        var interviewApplicationIds = historicalApplications
            .Select(application => application.JobApplicationId)
            .Concat(applicationIds)
            .Distinct()
            .ToArray();
        var interviews = await ListRediscoveryInterviewEvidenceAsync(connection, tenantId, interviewApplicationIds, cancellationToken);
        var interviewsByApplication = interviews
            .GroupBy(interview => interview.JobApplicationId)
            .ToDictionary(group => group.Key, group => group.Select(ToInterviewEvidence).ToArray());
        var applicationEvidenceByCandidate = historicalApplications
            .GroupBy(application => application.CandidateId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(application => ToApplicationEvidence(
                        application,
                        interviewsByApplication.TryGetValue(application.JobApplicationId, out var applicationInterviews)
                            ? applicationInterviews
                            : [],
                        historicalDocumentsByApplication.TryGetValue(application.JobApplicationId, out var applicationDocuments)
                            ? applicationDocuments
                            : []))
                    .ToArray());
        var interviewEvidenceByCandidate = interviews
            .GroupBy(interview => interview.CandidateId)
            .ToDictionary(group => group.Key, group => group.Select(ToInterviewEvidence).ToArray());
        var requiredSkillSet = requiredSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows
            .GroupBy(row => row.JobApplicationId)
            .Select(group =>
            {
                var first = group.First();
                var skills = group
                    .Select(row => row.SkillName)
                    .Where(skill => !string.IsNullOrWhiteSpace(skill))
                    .Select(skill => skill!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(skill => skill)
                    .ToArray();
                var skillSet = skills.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var matchedSkills = requiredSkillSet
                    .Where(skillSet.Contains)
                    .OrderBy(skill => skill)
                    .ToArray();
                var missingSkills = requiredSkillSet
                    .Where(skill => !skillSet.Contains(skill))
                    .OrderBy(skill => skill)
                    .ToArray();

                return new OperationsApplicantRankingApplication(
                    first.JobApplicationId,
                    first.CandidateId,
                    first.CandidateName,
                    first.CandidateEmail,
                    first.CandidateStatus,
                    first.CurrentDesignation,
                    first.CurrentCompany,
                    first.ExperienceYears,
                    first.NoticePeriodDays,
                    first.ApplicationStatus,
                    first.SourceLabel,
                    first.SourceDetail,
                    first.CoverLetterText,
                    Utc(first.AppliedAt),
                    first.ApplicationSnapshotJson,
                    skills,
                    matchedSkills,
                    missingSkills,
                    documentsByApplication.TryGetValue(first.JobApplicationId, out var applicationDocuments) ? applicationDocuments : [],
                    applicationEvidenceByCandidate.TryGetValue(first.CandidateId, out var candidateApplications) ? candidateApplications : [],
                    interviewEvidenceByCandidate.TryGetValue(first.CandidateId, out var candidateInterviews) ? candidateInterviews : []);
            })
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<Guid, OperationsApplicantDocumentEvidence[]>> ListApplicantDocumentEvidenceAsync(
        SqlConnection connection,
        Guid tenantId,
        IReadOnlyList<Guid> applicationIds,
        CancellationToken cancellationToken)
    {
        if (applicationIds.Count == 0)
        {
            return new Dictionary<Guid, OperationsApplicantDocumentEvidence[]>();
        }

        const string sql = """
            SELECT
                ApplicationDocumentId,
                JobApplicationId,
                DocumentType,
                OriginalFileName AS FileName,
                ContentType,
                SizeBytes,
                StorageProvider,
                StorageKey,
                StorageContainer,
                ContentHashSha256,
                UploadedAtUtc AS UploadedAt,
                ExtractionStatus,
                CAST(CASE WHEN NULLIF(LTRIM(RTRIM(ExtractedText)), N'') IS NULL THEN 0 ELSE 1 END AS bit) AS HasExtractedText,
                ExtractedText,
                ExtractedTextHashSha256,
                ParserVersion,
                ExtractedAtUtc AS ExtractedAt,
                ExtractionError
            FROM dbo.JobApplicationDocuments
            WHERE TenantId = @TenantId
              AND JobApplicationId IN @ApplicationIds
              AND Status = N'Active'
            ORDER BY UploadedAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<ApplicationDocumentEvidenceRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ApplicationIds = applicationIds },
            cancellationToken: cancellationToken));

        return rows
            .GroupBy(row => row.JobApplicationId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(row => new OperationsApplicantDocumentEvidence(
                        row.ApplicationDocumentId,
                        row.DocumentType,
                        row.FileName,
                        row.ContentType,
                        row.SizeBytes,
                        row.StorageProvider,
                        row.StorageKey,
                        row.StorageContainer,
                        row.ContentHashSha256,
                        Utc(row.UploadedAt),
                        row.ExtractionStatus,
                        row.HasExtractedText,
                        row.ExtractedText,
                        row.ExtractedTextHashSha256,
                        row.ParserVersion,
                        ToUtc(row.ExtractedAt),
                        row.ExtractionError))
                    .ToArray());
    }

    public async Task<OperationsApplicantDocumentEvidence?> GetRecruiterApplicationDocumentAsync(
        Guid tenantId,
        Guid jobApplicationId,
        Guid applicationDocumentId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            SELECT
                document.ApplicationDocumentId,
                document.JobApplicationId,
                document.DocumentType,
                document.OriginalFileName AS FileName,
                document.ContentType,
                document.SizeBytes,
                document.StorageProvider,
                document.StorageKey,
                document.StorageContainer,
                document.ContentHashSha256,
                document.UploadedAtUtc AS UploadedAt,
                document.ExtractionStatus,
                CAST(CASE WHEN NULLIF(LTRIM(RTRIM(document.ExtractedText)), N'') IS NULL THEN 0 ELSE 1 END AS bit) AS HasExtractedText,
                document.ExtractedText,
                document.ExtractedTextHashSha256,
                document.ParserVersion,
                document.ExtractedAtUtc AS ExtractedAt,
                document.ExtractionError
            FROM dbo.JobApplicationDocuments AS document
            INNER JOIN dbo.JobApplications AS application
                ON application.TenantId = document.TenantId
                AND application.JobApplicationId = document.JobApplicationId
            WHERE document.TenantId = @TenantId
              AND document.JobApplicationId = @JobApplicationId
              AND document.ApplicationDocumentId = @ApplicationDocumentId
              AND document.Status = N'Active'
              AND application.IsActive = CAST(1 AS BIT);
            """;

        var row = await connection.QuerySingleOrDefaultAsync<ApplicationDocumentEvidenceRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobApplicationId = jobApplicationId, ApplicationDocumentId = applicationDocumentId },
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : new OperationsApplicantDocumentEvidence(
                row.ApplicationDocumentId,
                row.DocumentType,
                row.FileName,
                row.ContentType,
                row.SizeBytes,
                row.StorageProvider,
                row.StorageKey,
                row.StorageContainer,
                row.ContentHashSha256,
                Utc(row.UploadedAt),
                row.ExtractionStatus,
                row.HasExtractedText,
                row.ExtractedText,
                row.ExtractedTextHashSha256,
                row.ParserVersion,
                ToUtc(row.ExtractedAt),
                row.ExtractionError);
    }

    private static async Task<IReadOnlyList<OperationsRecruiterApplication>> ListRecruiterApplicationsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                application.JobApplicationId,
                application.JobPostId,
                application.CandidateId,
                candidate.DisplayName AS CandidateName,
                candidate.Email AS CandidateEmail,
                candidate.Status AS CandidateStatus,
                candidate.CurrentDesignation,
                candidate.CurrentCompany,
                candidate.ExperienceYears,
                candidate.NoticePeriodDays,
                application.CurrentStatus AS ApplicationStatus,
                application.SourceLabel,
                application.SourceDetail,
                application.SourceUrl,
                application.CoverLetterText,
                application.IsInvited,
                application.AppliedAtUtc AS AppliedAt
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            WHERE application.TenantId = @TenantId
              AND application.JobPostId = @JobPostId
            ORDER BY
                CASE application.CurrentStatus
                    WHEN N'Applied' THEN 1
                    WHEN N'Screening' THEN 2
                    WHEN N'Interviewing' THEN 3
                    WHEN N'Invited' THEN 4
                    WHEN N'OnHold' THEN 5
                    ELSE 9
                END,
                application.AppliedAtUtc DESC;
            """;

        var rows = (await connection.QueryAsync<RecruiterApplicationRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId },
            cancellationToken: cancellationToken))).ToArray();
        if (rows.Length == 0)
        {
            return [];
        }

        var applicationIds = rows.Select(row => row.JobApplicationId).Distinct().ToArray();
        var documentsByApplication = await ListApplicantDocumentEvidenceAsync(
            connection,
            tenantId,
            applicationIds,
            cancellationToken);
        var interviews = await ListRecruiterApplicationInterviewsAsync(
            connection,
            tenantId,
            applicationIds,
            cancellationToken);
        var configuredInterviewRoundCount = await CountActiveJobPostInterviewRoundsAsync(
            connection,
            tenantId,
            jobPostId,
            cancellationToken);
        var interviewsByApplication = interviews
            .GroupBy(interview => interview.JobApplicationId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return rows.Select(row =>
        {
            var rowInterviews = interviewsByApplication.TryGetValue(row.JobApplicationId, out var applicationInterviews)
                ? applicationInterviews
                : Array.Empty<RecruiterApplicationInterviewRow>();
            var rowDocuments = documentsByApplication.TryGetValue(row.JobApplicationId, out var applicationDocuments)
                ? applicationDocuments
                : Array.Empty<OperationsApplicantDocumentEvidence>();
            var evidence = rowInterviews
                .Select(interview => new OperationsCandidateInterviewEvidence(
                    interview.InterviewId,
                    interview.JobApplicationId,
                    interview.RoundName,
                    interview.Status,
                    interview.Recommendation,
                    null,
                    null,
                    null,
                    null,
                    null))
                .ToArray();
            var summary = RediscoveryInterviewSummary.Build(evidence, configuredInterviewRoundCount);

            return new OperationsRecruiterApplication(
                row.JobApplicationId,
                row.CandidateId,
                row.CandidateName,
                row.CandidateEmail,
                row.CandidateStatus,
                row.CurrentDesignation,
                row.CurrentCompany,
                row.ExperienceYears,
                row.NoticePeriodDays,
                row.ApplicationStatus,
                row.SourceLabel,
                row.SourceDetail,
                row.SourceUrl,
                row.CoverLetterText,
                row.IsInvited,
                Utc(row.AppliedAt),
                summary.Passed,
                summary.Total,
                summary.DisplayText,
                rowDocuments
                    .Select(document => new OperationsRecruiterApplicationDocument(
                        document.ApplicationDocumentId,
                        row.JobApplicationId,
                        string.IsNullOrWhiteSpace(document.DocumentType) ? "Application document" : document.DocumentType.Trim(),
                        BuildRecruiterApplicationDocumentDisplayName(document.DocumentType),
                        string.IsNullOrWhiteSpace(document.ContentType) ? "application/octet-stream" : document.ContentType,
                        document.SizeBytes,
                        document.UploadedAt,
                        document.ExtractionStatus,
                        document.HasExtractedText))
                    .ToArray(),
                rowInterviews.Select(ToRecruiterApplicationInterview).ToArray());
        }).ToArray();
    }

    private static async Task<OperationsRecruiterApplication?> ReadRecruiterApplicationAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobPostId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        var applications = await ListRecruiterApplicationsAsync(connection, tenantId, jobPostId, cancellationToken);
        return applications.FirstOrDefault(application => application.JobApplicationId == jobApplicationId);
    }

    private static async Task<IReadOnlyList<RecruiterApplicationInterviewRow>> ListRecruiterApplicationInterviewsAsync(
        SqlConnection connection,
        Guid tenantId,
        IReadOnlyList<Guid> applicationIds,
        CancellationToken cancellationToken)
    {
        if (applicationIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT
                interview.InterviewId,
                interview.JobApplicationId,
                interview.JobPostInterviewRoundId,
                COALESCE(postRound.Name, requestRound.Name, N'Interview') AS RoundName,
                interviewer.UserId AS InterviewerUserId,
                interviewer.DisplayName AS InterviewerName,
                interviewer.AccountStatus AS InterviewerAccountStatus,
                CASE WHEN interviewer.DeletedAtUtc IS NULL THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END AS InterviewerIsDeleted,
                interview.Status,
                interview.StartsAtUtc AS StartsAt,
                interview.DurationMinutes,
                interview.MeetingLink,
                interview.LocationText,
                feedback.Recommendation
            FROM dbo.Interviews AS interview
            INNER JOIN dbo.AppUsers AS interviewer
                ON interviewer.TenantId = interview.TenantId
                AND interviewer.UserId = interview.InterviewerUserId
            LEFT JOIN dbo.JobPostInterviewRounds AS postRound
                ON postRound.TenantId = interview.TenantId
                AND postRound.JobPostInterviewRoundId = interview.JobPostInterviewRoundId
            LEFT JOIN dbo.JobRequestInterviewRounds AS requestRound
                ON requestRound.TenantId = interview.TenantId
                AND requestRound.JobRequestInterviewRoundId = interview.JobRequestInterviewRoundId
            LEFT JOIN dbo.InterviewFeedback AS feedback
                ON feedback.TenantId = interview.TenantId
                AND feedback.InterviewId = interview.InterviewId
                AND feedback.IsSubmitted = CAST(1 AS BIT)
            WHERE interview.TenantId = @TenantId
              AND interview.JobApplicationId IN @ApplicationIds
            ORDER BY interview.StartsAtUtc DESC;
            """;

        return (await connection.QueryAsync<RecruiterApplicationInterviewRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ApplicationIds = applicationIds },
            cancellationToken: cancellationToken))).ToArray();
    }

    private static async Task<int> CountActiveJobPostInterviewRoundsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.JobPostInterviewRounds
            WHERE TenantId = @TenantId
              AND JobPostId = @JobPostId
              AND Status = N'Active';
            """;

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId },
            cancellationToken: cancellationToken));
    }

    private static async Task<int> CountJobRequestInterviewRoundsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.JobRequestInterviewRounds
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken));
    }

    private static OperationsRecruiterApplicationInterview ToRecruiterApplicationInterview(RecruiterApplicationInterviewRow row)
    {
        return new OperationsRecruiterApplicationInterview(
            row.InterviewId,
            row.JobPostInterviewRoundId,
            row.RoundName,
            row.InterviewerName,
            row.InterviewerUserId,
            row.InterviewerAccountStatus,
            row.InterviewerIsDeleted,
            row.Status,
            Utc(row.StartsAt),
            row.DurationMinutes,
            row.MeetingLink,
            row.LocationText,
            row.Recommendation);
    }

    private static string BuildRecruiterApplicationDocumentDisplayName(string? documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
        {
            return "Application document";
        }

        var type = documentType.Trim();
        if (string.Equals(type, "Resume", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "CV", StringComparison.OrdinalIgnoreCase))
        {
            return "Resume document";
        }

        if (type.Contains("cover", StringComparison.OrdinalIgnoreCase) &&
            type.Contains("letter", StringComparison.OrdinalIgnoreCase))
        {
            return "Cover letter document";
        }

        return type.Contains("document", StringComparison.OrdinalIgnoreCase)
            ? type
            : $"{type} document";
    }

    private static async Task<IReadOnlyList<RediscoveryInterviewEvidenceRow>> ListRediscoveryInterviewEvidenceAsync(
        SqlConnection connection,
        Guid tenantId,
        IReadOnlyList<Guid> applicationIds,
        CancellationToken cancellationToken)
    {
        if (applicationIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT
                application.CandidateId,
                interview.InterviewId,
                interview.JobApplicationId,
                COALESCE(postRound.Name, round.Name, N'Interview') AS RoundName,
                interview.Status,
                feedback.Recommendation,
                feedback.TechnicalScore,
                feedback.CommunicationScore,
                feedback.CultureScore,
                feedback.FeedbackText AS FeedbackSummary,
                interview.StartsAtUtc AS StartsAt,
                feedback.SubmittedAtUtc AS SubmittedAt
            FROM dbo.Interviews AS interview
            INNER JOIN dbo.JobApplications AS application
                ON application.TenantId = interview.TenantId
                AND application.JobApplicationId = interview.JobApplicationId
            LEFT JOIN dbo.JobRequestInterviewRounds AS round
                ON round.TenantId = interview.TenantId
                AND round.JobRequestInterviewRoundId = interview.JobRequestInterviewRoundId
            LEFT JOIN dbo.JobPostInterviewRounds AS postRound
                ON postRound.TenantId = interview.TenantId
                AND postRound.JobPostInterviewRoundId = interview.JobPostInterviewRoundId
            LEFT JOIN dbo.InterviewFeedback AS feedback
                ON feedback.TenantId = interview.TenantId
                AND feedback.InterviewId = interview.InterviewId
                AND feedback.IsSubmitted = CAST(1 AS BIT)
            WHERE interview.TenantId = @TenantId
              AND interview.JobApplicationId IN @ApplicationIds
            ORDER BY feedback.SubmittedAtUtc DESC, interview.StartsAtUtc DESC;
            """;

        return (await connection.QueryAsync<RediscoveryInterviewEvidenceRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ApplicationIds = applicationIds },
            cancellationToken: cancellationToken))).ToArray();
    }

    private static async Task<IReadOnlyList<PortalApplicationOfferMeetingRow>> ListPortalApplicationOfferMeetingsAsync(
        SqlConnection connection,
        Guid tenantId,
        IReadOnlyList<Guid> applicationIds,
        CancellationToken cancellationToken)
    {
        if (applicationIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT
                JobApplicationId,
                MeetingAtUtc AS MeetingAt,
                LocationText,
                Status,
                Notes
            FROM dbo.OfferPresentationMeetings
            WHERE TenantId = @TenantId
              AND JobApplicationId IN @ApplicationIds
            ORDER BY MeetingAtUtc;
            """;

        return (await connection.QueryAsync<PortalApplicationOfferMeetingRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ApplicationIds = applicationIds },
            cancellationToken: cancellationToken))).ToArray();
    }

    private static OperationsCandidateApplicationEvidence ToApplicationEvidence(
        RediscoveryApplicationEvidenceRow row,
        IReadOnlyList<OperationsCandidateInterviewEvidence> interviews,
        IReadOnlyList<OperationsApplicantDocumentEvidence>? documents = null)
    {
        var summary = RediscoveryInterviewSummary.Build(interviews);

        return new OperationsCandidateApplicationEvidence(
            row.JobApplicationId,
            row.JobRequestId,
            row.RequestCode,
            row.JobTitle,
            row.Client,
            row.Department,
            row.Location,
            row.Status,
            row.SourceLabel,
            Utc(row.AppliedAt),
            ToUtc(row.FinalDecisionAt),
            row.FinalDecisionReason,
            row.JobPostId,
            row.JobPostTitle,
            row.JobPostStatus,
            row.DisplayJobTitle,
            summary.Passed,
            summary.Total,
            summary.DisplayText,
            row.CoverLetterText,
            documents ?? []);
    }

    private static OperationsManualCandidateSearchItem ToManualCandidateSearchItem(OperationsRediscoveryCandidate candidate)
    {
        var summary = RediscoveryInterviewSummary.Build(candidate.InterviewEvidence);
        var failedInterviews = candidate.InterviewEvidence.Count(IsFailedRediscoveryInterview);
        var latestApplication = candidate.ApplicationEvidence
            .OrderByDescending(application => application.AppliedAt)
            .FirstOrDefault();

        return new OperationsManualCandidateSearchItem(
            candidate.CandidateId,
            candidate.DisplayName,
            candidate.Email,
            candidate.Status,
            candidate.CurrentDesignation,
            candidate.CurrentCompany,
            candidate.ExperienceYears,
            candidate.NoticePeriodDays,
            candidate.Skills,
            candidate.MatchedSkills,
            candidate.MissingSkills,
            candidate.ApplicationEvidence.Count,
            summary.Passed,
            failedInterviews,
            summary.Total,
            latestApplication);
    }

    private static bool IsFailedRediscoveryInterview(OperationsCandidateInterviewEvidence interview)
    {
        return string.Equals(interview.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            && !RediscoveryInterviewSummary.IsPassedInterview(interview);
    }

    private static OperationsHistoricalApplicationSummary ToHistoricalApplicationSummary(
        RediscoveryApplicationEvidenceRow row,
        IReadOnlyList<OperationsCandidateInterviewEvidence> interviews)
    {
        var summary = RediscoveryInterviewSummary.Build(interviews);

        return new OperationsHistoricalApplicationSummary(
            row.JobApplicationId,
            row.JobRequestId,
            row.RequestCode,
            row.JobPostId,
            row.JobPostTitle,
            row.JobPostStatus,
            row.DisplayJobTitle,
            row.Client,
            row.Department,
            row.Location,
            row.Status,
            row.SourceLabel,
            Utc(row.AppliedAt),
            ToUtc(row.FinalDecisionAt),
            row.FinalDecisionReason,
            row.OfferStartDate.HasValue ? DateOnly.FromDateTime(row.OfferStartDate.Value) : null,
            summary.Passed,
            summary.Total,
            summary.DisplayText);
    }

    private static IReadOnlyList<PortalApplicationTimelineItem> BuildPortalApplicationTimeline(
        OperationsHistoricalApplicationSummary application,
        IReadOnlyList<RediscoveryInterviewEvidenceRow> interviews,
        IReadOnlyList<PortalApplicationOfferMeetingRow> offerMeetings)
    {
        var items = new List<PortalApplicationTimelineItem>
        {
            new(
                "Applied",
                "Application submitted",
                $"{application.SourceLabel} application received for {application.DisplayJobTitle}.",
                application.AppliedAt,
                "Applied")
        };

        foreach (var interview in interviews.OrderBy(interview => interview.StartsAt))
        {
            var occurredAt = ToUtc(interview.SubmittedAt) ?? Utc(interview.StartsAt);
            var interviewEvidence = ToInterviewEvidence(interview);
            var passed = RediscoveryInterviewSummary.IsPassedInterview(interviewEvidence);
            var status = string.Equals(interview.Status, "Completed", StringComparison.OrdinalIgnoreCase) && passed
                ? "Passed"
                : PortalStatusLabel(interview.Status);
            var recommendationText = string.IsNullOrWhiteSpace(interview.Recommendation)
                ? "No recommendation recorded yet."
                : $"Interviewer recommendation: {PortalStatusLabel(interview.Recommendation)}.";
            var title = interview.Status switch
            {
                var value when string.Equals(value, "Scheduled", StringComparison.OrdinalIgnoreCase) => $"{interview.RoundName} scheduled",
                var value when string.Equals(value, "Completed", StringComparison.OrdinalIgnoreCase) => $"{interview.RoundName} completed",
                var value when string.Equals(value, "Skipped", StringComparison.OrdinalIgnoreCase) => $"{interview.RoundName} skipped",
                var value when string.Equals(value, "Cancelled", StringComparison.OrdinalIgnoreCase) => $"{interview.RoundName} cancelled",
                _ => $"{interview.RoundName} updated"
            };
            var description = interview.Status switch
            {
                var value when string.Equals(value, "Scheduled", StringComparison.OrdinalIgnoreCase) => "Recruiter scheduled this interview round.",
                var value when string.Equals(value, "Completed", StringComparison.OrdinalIgnoreCase) => recommendationText,
                var value when string.Equals(value, "Skipped", StringComparison.OrdinalIgnoreCase) => "This interview round was skipped by the recruiting team.",
                var value when string.Equals(value, "Cancelled", StringComparison.OrdinalIgnoreCase) => "This interview round was cancelled.",
                _ => "Interview status was updated."
            };

            items.Add(new PortalApplicationTimelineItem(
                "Interview",
                title,
                description,
                occurredAt,
                status));
        }

        foreach (var meeting in offerMeetings.OrderBy(meeting => meeting.MeetingAt))
        {
            var locationText = string.IsNullOrWhiteSpace(meeting.LocationText)
                ? "Physical location will be shared by the hiring team."
                : $"Location: {meeting.LocationText}.";
            items.Add(new PortalApplicationTimelineItem(
                "OfferMeeting",
                "Offer presentation meeting scheduled",
                locationText,
                Utc(meeting.MeetingAt),
                PortalStatusLabel(meeting.Status)));
        }

        var isTerminal = IsPortalTerminalApplicationStatus(application.Status);
        var latestOccurredAt = items.Count == 0
            ? application.AppliedAt
            : items.Max(item => item.OccurredAt);
        var finalOccurredAt = application.FinalDecisionAt ?? latestOccurredAt;
        var statusDescription = application.FinalDecisionReason
            ?? (isTerminal
                ? "The hiring team recorded this as the final application outcome."
                : "The application is still moving through the hiring process.");
        if (application.Status is "Hired" && application.OfferStartDate.HasValue)
        {
            statusDescription = $"Offer accepted. Joining date: {application.OfferStartDate.Value.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)}.";
        }
        else if (application.Status is "Joined" && application.OfferStartDate.HasValue)
        {
            statusDescription = $"Joining completed on {application.OfferStartDate.Value.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)}.";
        }

        items.Add(new PortalApplicationTimelineItem(
            isTerminal ? "FinalOutcome" : "CurrentStatus",
            isTerminal
                ? $"Final outcome: {PortalStatusLabel(application.Status)}"
                : $"Current status: {PortalStatusLabel(application.Status)}",
            statusDescription,
            finalOccurredAt,
            PortalStatusLabel(application.Status)));

        return items
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Kind == "CurrentStatus" || item.Kind == "FinalOutcome" ? 1 : 0)
            .ToArray();
    }

    private static bool IsPortalTerminalApplicationStatus(string status)
    {
        return status is "Rejected" or "Withdrawn" or "Joined" or "Hired" or "OfferDeclined" or "OnHold";
    }

    private static string PortalStatusLabel(string status)
    {
        return status switch
        {
            "HiringManagerReview" => "Hiring Manager Review",
            "OfferDeclined" => "Offer Declined",
            "NoShow" => "No Show",
            "OnHold" => "On Hold",
            _ => status
        };
    }

    private static OperationsCandidateInterviewEvidence ToInterviewEvidence(RediscoveryInterviewEvidenceRow row)
    {
        return new OperationsCandidateInterviewEvidence(
            row.InterviewId,
            row.JobApplicationId,
            row.RoundName,
            row.Status,
            row.Recommendation,
            row.TechnicalScore,
            row.CommunicationScore,
            row.CultureScore,
            row.FeedbackSummary,
            ToUtc(row.SubmittedAt));
    }

    private static OperationsHistoricalInterviewDetail ToHistoricalInterviewDetail(RediscoveryInterviewEvidenceRow row)
    {
        return new OperationsHistoricalInterviewDetail(
            row.InterviewId,
            row.RoundName,
            row.Status,
            row.Recommendation,
            row.TechnicalScore,
            row.CommunicationScore,
            row.CultureScore,
            AverageScore(row.TechnicalScore, row.CommunicationScore, row.CultureScore),
            row.FeedbackSummary,
            Utc(row.StartsAt),
            ToUtc(row.SubmittedAt));
    }

    private static decimal? AverageScore(params int?[] scores)
    {
        var submittedScores = scores
            .Where(score => score.HasValue)
            .Select(score => score!.Value)
            .ToArray();

        return submittedScores.Length == 0 ? null : Math.Round((decimal)submittedScores.Average(), 1);
    }

    private static async Task<IReadOnlyList<OperationsBenchMatch>> ListLatestBenchMatchesAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                RecommendedEntityId AS EmployeeId,
                AiAgentRunId,
                Score,
                Explanation,
                PayloadJson,
                COALESCE(UpdatedAtUtc, CreatedAtUtc) AS GeneratedAt
            FROM dbo.AiRecommendationLogs
            WHERE TenantId = @TenantId
              AND AiAgentDefinitionId = N'bench-matching'
              AND SourceEntityType = N'JobRequest'
              AND SourceEntityId = @JobRequestId
              AND RecommendedEntityType = N'Employee'
            ORDER BY Score DESC, CreatedAtUtc DESC;
            """;

        var rows = (await connection.QueryAsync<BenchMatchLogRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken))).ToArray();

        return rows.Select((row, index) =>
            {
                var payload = DeserializeBenchMatchPayload(row.PayloadJson);
                return new OperationsBenchMatch(
                    row.EmployeeId,
                    payload?.Rank > 0 ? payload.Rank : index + 1,
                    row.Score ?? payload?.Score ?? 0,
                    payload?.Confidence ?? "Low",
                    row.Explanation ?? payload?.Explanation ?? string.Empty,
                    payload?.Strengths ?? [],
                    payload?.Gaps ?? [],
                    payload?.ProjectEvidence ?? [],
                    payload?.WebResearchStatus ?? "Unavailable",
                    payload?.WebSummary ?? BuildStoredWebSummary(payload?.WebResearchStatus ?? "Unavailable", payload?.WebSources ?? []),
                    payload?.WebSources ?? [],
                    row.AiAgentRunId,
                    Utc(row.GeneratedAt));
            })
            .OrderBy(match => match.Rank)
            .ToArray();
    }

    private static async Task<IReadOnlyList<OperationsTalentRediscoveryMatch>> ListLatestTalentRediscoveryMatchesAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                recommendation.RecommendedEntityId AS CandidateId,
                recommendation.AiAgentRunId,
                recommendation.Score,
                recommendation.Explanation,
                recommendation.PayloadJson,
                COALESCE(recommendation.UpdatedAtUtc, recommendation.CreatedAtUtc) AS GeneratedAt
            FROM dbo.AiRecommendationLogs AS recommendation
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = recommendation.TenantId
                AND candidate.CandidateId = recommendation.RecommendedEntityId
            WHERE recommendation.TenantId = @TenantId
              AND recommendation.AiAgentDefinitionId = N'talent-rediscovery'
              AND recommendation.SourceEntityType = N'JobRequest'
              AND recommendation.SourceEntityId = @JobRequestId
              AND recommendation.RecommendedEntityType = N'Candidate'
              AND candidate.Status = N'Active'
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.JobApplications AS hiredApplication
                  WHERE hiredApplication.TenantId = candidate.TenantId
                    AND hiredApplication.CandidateId = candidate.CandidateId
                    AND hiredApplication.CurrentStatus IN (N'Hired', N'Joined')
              )
            ORDER BY recommendation.Score DESC, recommendation.CreatedAtUtc DESC;
            """;

        var rows = (await connection.QueryAsync<TalentRediscoveryLogRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken))).ToArray();

        return rows.Select((row, index) =>
            {
                var payload = DeserializeTalentRediscoveryPayload(row.PayloadJson);
                return new OperationsTalentRediscoveryMatch(
                    row.CandidateId,
                    payload?.CandidateName ?? "Unknown candidate",
                    payload?.CandidateEmail ?? string.Empty,
                    payload?.CurrentDesignation,
                    payload?.ExperienceYears,
                    payload?.NoticePeriodDays,
                    payload?.Rank > 0 ? payload.Rank : index + 1,
                    row.Score ?? payload?.Score ?? 0,
                    payload?.Confidence ?? "Low",
                    row.Explanation ?? payload?.Explanation ?? string.Empty,
                    payload?.Strengths ?? [],
                    payload?.Gaps ?? [],
                    payload?.ApplicationEvidence ?? [],
                    payload?.InterviewEvidence ?? [],
                    row.AiAgentRunId,
                    Utc(row.GeneratedAt));
            })
            .OrderBy(match => match.Rank)
            .ToArray();
    }

    private static async Task<IReadOnlyList<OperationsApplicantRankingMatch>> ListLatestApplicantRankingsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                RecommendedEntityId AS JobApplicationId,
                AiAgentRunId,
                Score,
                Explanation,
                PayloadJson,
                COALESCE(UpdatedAtUtc, CreatedAtUtc) AS GeneratedAt
            FROM dbo.AiRecommendationLogs
            WHERE TenantId = @TenantId
              AND AiAgentDefinitionId = N'applicant-ranking'
              AND SourceEntityType = N'JobPost'
              AND SourceEntityId = @JobPostId
              AND RecommendedEntityType = N'JobApplication'
            ORDER BY Score DESC, CreatedAtUtc DESC;
            """;

        var rows = (await connection.QueryAsync<ApplicantRankingLogRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId },
            cancellationToken: cancellationToken))).ToArray();

        return rows.Select((row, index) =>
            {
                var payload = DeserializeApplicantRankingPayload(row.PayloadJson);
                return new OperationsApplicantRankingMatch(
                    row.JobApplicationId,
                    payload?.CandidateId ?? Guid.Empty,
                    payload?.CandidateName ?? "Unknown applicant",
                    payload?.CandidateEmail ?? string.Empty,
                    payload?.CurrentDesignation,
                    payload?.ExperienceYears,
                    payload?.NoticePeriodDays,
                    payload?.Rank > 0 ? payload.Rank : index + 1,
                    row.Score ?? payload?.Score ?? 0,
                    payload?.Confidence ?? "Low",
                    row.Explanation ?? payload?.Explanation ?? string.Empty,
                    payload?.Strengths ?? [],
                    payload?.Gaps ?? [],
                    payload?.MatchedSkills ?? [],
                    payload?.MissingSkills ?? [],
                    payload?.DocumentEvidence ?? [],
                    payload?.HistoricalOutcomeEvidence ?? [],
                    payload?.SemanticSimilarityStatus ?? "Unavailable",
                    row.AiAgentRunId,
                    Utc(row.GeneratedAt));
            })
            .OrderBy(match => match.Rank)
            .ToArray();
    }

    private static async Task<IReadOnlyList<OperationsLookupOption>> ListPresalesUsersAsync(
        SqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT u.UserId AS Id, u.DisplayName AS Name, u.Email AS Description
            FROM dbo.AppUsers AS u
            INNER JOIN dbo.UserRoles AS ur
                ON ur.TenantId = u.TenantId
                AND ur.UserId = u.UserId
            INNER JOIN dbo.Roles AS r
                ON r.TenantId = ur.TenantId
                AND r.RoleId = ur.RoleId
                AND r.Code = N'Presales'
                AND r.Status = N'Active'
            WHERE u.TenantId = @TenantId
              AND u.AccountStatus = N'Active'
              AND u.DeletedAtUtc IS NULL
            ORDER BY u.DisplayName;
            """;

        var rows = await connection.QueryAsync<OperationsLookupOption>(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        return rows.ToArray();
    }

    private static async Task<string> ReadRecruiterHandoffTargetNameAsync(
        SqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                COALESCE(targetUser.DisplayName, targetGroup.Name, targetRole.Name, N'Recruiters') AS TargetName
            FROM dbo.WorkflowTransitions AS transition
            INNER JOIN dbo.WorkflowRoutingRules AS routingRule
                ON routingRule.TenantId = transition.TenantId
                AND routingRule.WorkflowTransitionId = transition.WorkflowTransitionId
                AND routingRule.Status = N'Active'
            LEFT JOIN dbo.AppUsers AS targetUser
                ON targetUser.TenantId = routingRule.TenantId
                AND targetUser.UserId = routingRule.TargetUserId
                AND targetUser.AccountStatus = N'Active'
                AND targetUser.DeletedAtUtc IS NULL
            LEFT JOIN dbo.Groups AS targetGroup
                ON targetGroup.TenantId = routingRule.TenantId
                AND targetGroup.GroupId = routingRule.TargetGroupId
                AND targetGroup.Status = N'Active'
            LEFT JOIN dbo.Roles AS targetRole
                ON targetRole.TenantId = routingRule.TenantId
                AND targetRole.RoleId = routingRule.TargetRoleId
                AND targetRole.Status = N'Active'
            WHERE transition.TenantId = @TenantId
              AND transition.ActionKey = N'FORWARD_TO_RECRUITER'
              AND transition.Status = N'Active';
            """;

        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken)) ?? "Recruiters";
    }

    private static async Task<Guid?> ReadCurrentAssignmentForActionAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        Guid jobRequestId,
        string stageKey,
        bool requireClaimedForGroups,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) wa.WorkflowAssignmentId
            FROM dbo.JobRequests AS jr
            INNER JOIN dbo.WorkflowAssignments AS wa
                ON wa.TenantId = jr.TenantId
                AND wa.WorkflowAssignmentId = jr.CurrentAssignmentId
            INNER JOIN dbo.WorkflowStages AS ws
                ON ws.WorkflowStageId = wa.WorkflowStageId
                AND ws.StageKey = @StageKey
            WHERE jr.TenantId = @TenantId
              AND jr.JobRequestId = @JobRequestId
              AND wa.AssignmentStatus IN (N'Pending', N'Claimed')
              AND
              (
                  EXISTS
                  (
                      SELECT 1
                      FROM dbo.UserRoles AS adminUr
                      INNER JOIN dbo.Roles AS adminRole
                          ON adminRole.TenantId = adminUr.TenantId
                          AND adminRole.RoleId = adminUr.RoleId
                          AND adminRole.Code = @TenantAdminRoleCode
                          AND adminRole.Status = N'Active'
                      WHERE adminUr.TenantId = wa.TenantId
                        AND adminUr.UserId = @ActorUserId
                  )
                  OR wa.AssignedToUserId = @ActorUserId
                  OR wa.ClaimedByUserId = @ActorUserId
                  OR
                  (
                      @RequireClaimedForGroups = CAST(0 AS BIT)
                      AND EXISTS
                      (
                          SELECT 1
                          FROM dbo.GroupMembers AS gm
                          INNER JOIN dbo.Groups AS g
                              ON g.TenantId = gm.TenantId
                              AND g.GroupId = gm.GroupId
                              AND g.Status = N'Active'
                          WHERE gm.TenantId = wa.TenantId
                            AND gm.GroupId = wa.AssignedToGroupId
                            AND gm.UserId = @ActorUserId
                      )
                  )
              )
              AND
              (
                  @RequireClaimedForGroups = CAST(0 AS BIT)
                  OR wa.AssignedToGroupId IS NULL
                  OR wa.AssignmentStatus = N'Claimed'
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.UserRoles AS adminUr
                      INNER JOIN dbo.Roles AS adminRole
                          ON adminRole.TenantId = adminUr.TenantId
                          AND adminRole.RoleId = adminUr.RoleId
                          AND adminRole.Code = @TenantAdminRoleCode
                          AND adminRole.Status = N'Active'
                      WHERE adminUr.TenantId = wa.TenantId
                        AND adminUr.UserId = @ActorUserId
                  )
              );
            """;

        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                JobRequestId = jobRequestId,
                StageKey = stageKey,
                TenantAdminRoleCode = AccessConstants.TenantAdminRoleCode,
                RequireClaimedForGroups = requireClaimedForGroups
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<ActiveRoleUserRow?> ReadActiveRoleUserAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid userId,
        string roleCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) u.UserId, u.DisplayName, u.Email
            FROM dbo.AppUsers AS u
            INNER JOIN dbo.UserRoles AS ur
                ON ur.TenantId = u.TenantId
                AND ur.UserId = u.UserId
            INNER JOIN dbo.Roles AS r
                ON r.TenantId = ur.TenantId
                AND r.RoleId = ur.RoleId
                AND r.Code = @RoleCode
                AND r.Status = N'Active'
            WHERE u.TenantId = @TenantId
              AND u.UserId = @UserId
              AND u.AccountStatus = N'Active'
              AND u.DeletedAtUtc IS NULL;
            """;

        return await connection.QuerySingleOrDefaultAsync<ActiveRoleUserRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId, RoleCode = roleCode },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<Guid>> ReadEligibleEmployeeIdsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        IReadOnlyList<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        var distinctEmployeeIds = (employeeIds ?? Array.Empty<Guid>())
            .Where(employeeId => employeeId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (distinctEmployeeIds.Length == 0)
        {
            return [];
        }

        const string sql = """
            SELECT employee.EmployeeId
            FROM dbo.vw_EmployeeBenchAvailability AS employee
            WHERE employee.TenantId = @TenantId
              AND employee.EmployeeId IN @EmployeeIds
              AND employee.IsCurrentlyBenched = CAST(1 AS BIT)
              AND employee.BenchStatus IN (N'Benched', N'PartialBench');
            """;

        var rows = await connection.QueryAsync<Guid>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, EmployeeIds = distinctEmployeeIds },
            transaction,
            cancellationToken: cancellationToken));

        return rows.ToArray();
    }

    private static async Task<JobRequestContextRow?> ReadJobRequestContextAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                JobRequestId,
                RequestCode,
                Title,
                CreatedByUserId,
                RequiredPositions,
                FulfilledPositions
            FROM dbo.JobRequests
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        return await connection.QuerySingleOrDefaultAsync<JobRequestContextRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid> ReadWorkflowDefinitionIdAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) WorkflowDefinitionId
            FROM dbo.WorkflowDefinitions
            WHERE TenantId = @TenantId
              AND Code = N'JOB_REQUEST_MVP'
              AND Status = N'Active';
            """;

        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid> ReadWorkflowStageIdAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string stageKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) ws.WorkflowStageId
            FROM dbo.WorkflowStages AS ws
            INNER JOIN dbo.WorkflowDefinitions AS wd
                ON wd.WorkflowDefinitionId = ws.WorkflowDefinitionId
                AND wd.TenantId = ws.TenantId
                AND wd.Code = N'JOB_REQUEST_MVP'
            WHERE ws.TenantId = @TenantId
              AND ws.StageKey = @StageKey
              AND ws.Status = N'Active';
            """;

        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, StageKey = stageKey },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid?> ReadWorkflowTransitionIdAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string actionKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) WorkflowTransitionId
            FROM dbo.WorkflowTransitions
            WHERE TenantId = @TenantId
              AND ActionKey = @ActionKey
              AND Status = N'Active';
            """;

        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActionKey = actionKey },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<WorkflowAssignmentTarget> ResolveWorkflowRoutingAssignmentAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string actionKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                routingRule.AssignmentType,
                routingRule.TargetUserId,
                routingRule.TargetGroupId,
                routingRule.TargetRoleId
            FROM dbo.WorkflowTransitions AS workflowTransition
            INNER JOIN dbo.WorkflowRoutingRules AS routingRule
                ON routingRule.TenantId = workflowTransition.TenantId
                AND routingRule.WorkflowTransitionId = workflowTransition.WorkflowTransitionId
                AND routingRule.Status = N'Active'
            WHERE workflowTransition.TenantId = @TenantId
              AND workflowTransition.ActionKey = @ActionKey
              AND workflowTransition.Status = N'Active';
            """;

        var rule = await connection.QuerySingleOrDefaultAsync<WorkflowRoutingRuleTargetRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActionKey = actionKey },
            transaction,
            cancellationToken: cancellationToken));

        if (rule is null)
        {
            return await TenantAdminFallbackAsync(connection, transaction, tenantId, cancellationToken);
        }

        return rule.AssignmentType switch
        {
            "User" when rule.TargetUserId.HasValue => WorkflowAssignmentTarget.ForUser(rule.TargetUserId.Value, $"{actionKey} user"),
            "Group" when rule.TargetGroupId.HasValue => WorkflowAssignmentTarget.ForGroup(rule.TargetGroupId.Value, $"{actionKey} group"),
            "Role" when rule.TargetRoleId.HasValue => WorkflowAssignmentTarget.ForRole(rule.TargetRoleId.Value, $"{actionKey} role"),
            _ => await TenantAdminFallbackAsync(connection, transaction, tenantId, cancellationToken)
        };
    }

    private static async Task CompleteAssignmentAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.WorkflowAssignments
            SET AssignmentStatus = N'Completed',
                CompletedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND WorkflowAssignmentId = @AssignmentId
              AND AssignmentStatus <> N'Completed';
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, AssignmentId = assignmentId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertWorkflowAssignmentAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid workflowDefinitionId,
        Guid workflowStageId,
        Guid? workflowTransitionId,
        Guid assignmentId,
        Guid jobRequestId,
        WorkflowAssignmentTarget assignmentTarget,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.WorkflowAssignments
            (
                WorkflowAssignmentId,
                TenantId,
                WorkflowDefinitionId,
                WorkflowStageId,
                WorkflowTransitionId,
                EntityType,
                EntityId,
                AssignedToUserId,
                AssignedToGroupId,
                AssignedToRoleId,
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
                @JobRequestId,
                @AssignedToUserId,
                @AssignedToGroupId,
                @AssignedToRoleId,
                N'Pending',
                @Now
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                WorkflowAssignmentId = assignmentId,
                TenantId = tenantId,
                WorkflowDefinitionId = workflowDefinitionId,
                WorkflowStageId = workflowStageId,
                WorkflowTransitionId = workflowTransitionId,
                JobRequestId = jobRequestId,
                assignmentTarget.AssignedToUserId,
                assignmentTarget.AssignedToGroupId,
                assignmentTarget.AssignedToRoleId,
                Now = now.UtcDateTime
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task UpdateJobRequestStageAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        string status,
        string stageKey,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.JobRequests
            SET Status = @Status,
                CurrentStageKey = @StageKey,
                CurrentAssignmentId = @AssignmentId,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId, Status = status, StageKey = stageKey, AssignmentId = assignmentId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<string> ReadEmployeeDisplayNameAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) DisplayName
            FROM dbo.Employees
            WHERE TenantId = @TenantId
              AND EmployeeId = @EmployeeId;
            """;

        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, EmployeeId = employeeId },
            transaction,
            cancellationToken: cancellationToken)) ?? "an employee";
    }

    private static async Task<IReadOnlyList<OperationsNotificationDispatch>> QueueAndBuildDispatchesAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        NotificationContent? notificationContent,
        IReadOnlyList<NotificationRecipientRow> recipients,
        Guid jobRequestId,
        string jobTitle,
        string requesterName,
        string requestCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (notificationContent is null || recipients.Count == 0)
        {
            return [];
        }

        await InsertNotificationEmailOutboxAsync(
            connection,
            transaction,
            tenantId,
            notificationContent,
            recipients,
            jobRequestId,
            jobTitle,
            requesterName,
            requestCode,
            now,
            cancellationToken);

        return recipients
            .Select(recipient => new OperationsNotificationDispatch(
                recipient.UserId,
                string.Empty,
                notificationContent.Title,
                notificationContent.Message,
                "Workflow",
                "Info",
                "JobRequest",
                jobRequestId,
                new Dictionary<string, string>
                {
                    ["jobTitle"] = jobTitle,
                    ["requesterName"] = requesterName,
                    ["requestCode"] = requestCode
                }))
            .ToArray();
    }

    private static async Task InsertInternalFulfillmentAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        Guid referralId,
        Guid employeeId,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.JobRequestFulfillments
            (
                JobRequestFulfillmentId,
                TenantId,
                JobRequestId,
                JobRequestEmployeeReferralId,
                EmployeeId,
                FulfilledByUserId,
                FulfillmentType,
                Status,
                FulfilledAtUtc
            )
            SELECT
                NEWID(),
                @TenantId,
                @JobRequestId,
                @ReferralId,
                @EmployeeId,
                @ActorUserId,
                N'InternalEmployee',
                N'Completed',
                @Now
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.JobRequestFulfillments AS existing
                WHERE existing.TenantId = @TenantId
                  AND existing.JobRequestEmployeeReferralId = @ReferralId
                  AND existing.Status = N'Completed'
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                JobRequestId = jobRequestId,
                ReferralId = referralId,
                EmployeeId = employeeId,
                ActorUserId = actorUserId,
                Now = now.UtcDateTime
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task RefreshJobRequestFulfilledPositionsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE jr
            SET FulfilledPositions =
                    CASE
                        WHEN fulfilled.CompletedCount > jr.RequiredPositions THEN jr.RequiredPositions
                        ELSE fulfilled.CompletedCount
                    END,
                UpdatedAtUtc = SYSUTCDATETIME()
            FROM dbo.JobRequests AS jr
            CROSS APPLY
            (
                SELECT COUNT(1) AS CompletedCount
                FROM dbo.JobRequestFulfillments AS fulfillment
                WHERE fulfillment.TenantId = jr.TenantId
                  AND fulfillment.JobRequestId = jr.JobRequestId
                  AND fulfillment.Status = N'Completed'
            ) AS fulfilled
            WHERE jr.TenantId = @TenantId
              AND jr.JobRequestId = @JobRequestId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<FulfillmentProgressRow> ReadJobRequestFulfillmentProgressAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT RequiredPositions, FulfilledPositions
            FROM dbo.JobRequests
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        return await connection.QuerySingleAsync<FulfillmentProgressRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task CloseJobRequestAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.JobRequests
            SET Status = N'Closed',
                CurrentStageKey = N'CLOSED',
                ClosedAtUtc = COALESCE(ClosedAtUtc, SYSUTCDATETIME()),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<NotificationRecipientRow?> ReadNotificationRecipientAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) UserId, Email
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId = @UserId
              AND AccountStatus = N'Active'
              AND DeletedAtUtc IS NULL;
            """;

        return await connection.QuerySingleOrDefaultAsync<NotificationRecipientRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<string> ReadUserDisplayNameAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) DisplayName
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId = @UserId;
            """;

        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId },
            transaction,
            cancellationToken: cancellationToken)) ?? "A Talent Pilot user";
    }

    private static async Task<NotificationContent?> ReadNotificationContentAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string eventCode,
        string requestCode,
        string jobTitle,
        string requesterName,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? additionalValues = null)
    {
        const string sql = """
            SELECT TOP (1)
                e.NotificationEventId AS EventId,
                t.NotificationTemplateId AS TemplateId,
                COALESCE(t.Subject, N'New request: {{jobTitle}}') AS Subject,
                COALESCE(t.Body, N'{{requesterName}} submitted {{jobTitle}} for PMO review.') AS Body
            FROM dbo.NotificationEvents AS e
            LEFT JOIN dbo.NotificationTemplates AS t
                ON t.TenantId = e.TenantId
                AND t.NotificationEventId = e.NotificationEventId
                AND t.Status = N'Active'
            WHERE e.TenantId = @TenantId
              AND e.EventCode = @EventCode
              AND e.Status = N'Active'
            ORDER BY CASE WHEN t.NotificationTemplateId IS NULL THEN 1 ELSE 0 END, t.UpdatedAtUtc DESC;
            """;

        var row = await connection.QuerySingleOrDefaultAsync<NotificationTemplateRenderRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, EventCode = eventCode },
            transaction,
            cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var values = new Dictionary<string, string>
        {
            ["jobTitle"] = jobTitle,
            ["requesterName"] = requesterName,
            ["requestCode"] = requestCode
        };

        if (additionalValues is not null)
        {
            foreach (var (key, value) in additionalValues)
            {
                values[key] = value;
            }
        }

        return new NotificationContent(
            row.EventId,
            row.TemplateId,
            RenderNotificationTemplate(row.Subject, values),
            RenderNotificationTemplate(row.Body, values));
    }

    private static async Task<NotificationContent?> BuildMissingDepartmentRouteNotificationContentAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid departmentId,
        string requestCode,
        string jobTitle,
        string requesterName,
        CancellationToken cancellationToken)
    {
        var eventId = await ReadNotificationEventIdAsync(
            connection,
            transaction,
            tenantId,
            NotificationEventCodes.PresalesRequestSubmitted,
            cancellationToken);

        if (!eventId.HasValue)
        {
            return null;
        }

        var departmentName = await ReadDepartmentNameAsync(
            connection,
            transaction,
            tenantId,
            departmentId,
            cancellationToken);

        var title = $"Missing Job Request routing for {departmentName}";
        var message = $"{requesterName} submitted {jobTitle} ({requestCode}) for {departmentName}, but no active department intake route is configured. Please configure the PMO recipient in Admin Center > Workflows and review this fallback assignment.";

        return new NotificationContent(eventId.Value, null, title, message);
    }

    private static async Task<PortalJobPostRow?> ReadPublishedPortalJobPostAsync(
        SqlConnection connection,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        return await ReadPublishedPortalJobPostAsync(connection, null, null, jobPostId, cancellationToken);
    }

    private static async Task<PortalJobPostRow?> ReadPublishedPortalJobPostAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid? tenantId,
        Guid jobPostId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                post.JobPostId,
                post.TenantId,
                post.JobRequestId,
                request.RequestCode,
                post.Title,
                post.Description,
                COALESCE(NULLIF(settings.CareerDisplayName, N''), tenant.DisplayName) AS CompanyName,
                COALESCE(request.ClientName, N'Internal') AS Client,
                COALESCE(department.Name, N'Unassigned') AS Department,
                COALESCE(location.Name, N'Remote') AS Location,
                post.ExperienceMinYears,
                post.ExperienceMaxYears,
                post.RequiredPositions,
                post.Status,
                post.PublishedAtUtc AS PublishedAt
            FROM dbo.JobPosts AS post
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = post.TenantId
                AND request.JobRequestId = post.JobRequestId
            INNER JOIN dbo.Tenants AS tenant
                ON tenant.TenantId = post.TenantId
            LEFT JOIN dbo.TenantRecruitmentSettings AS settings
                ON settings.TenantId = post.TenantId
            LEFT JOIN dbo.Departments AS department
                ON department.TenantId = post.TenantId
                AND department.DepartmentId = post.DepartmentId
            LEFT JOIN dbo.Locations AS location
                ON location.TenantId = post.TenantId
                AND location.LocationId = post.LocationId
            WHERE post.JobPostId = @JobPostId
              AND post.Status = N'Published'
              AND post.PublishedAtUtc IS NOT NULL
              AND (@TenantId IS NULL OR post.TenantId = @TenantId)
              AND COALESCE(settings.PublicJobsEnabled, CAST(1 AS BIT)) = CAST(1 AS BIT);
            """;

        return await connection.QuerySingleOrDefaultAsync<PortalJobPostRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<CandidateMutableRow?> EnsureCandidateForUserAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        string? phone,
        string? linkedInUrl,
        string? currentDesignation,
        string? currentCompany,
        decimal? experienceYears,
        int? noticePeriodDays,
        CancellationToken cancellationToken)
    {
        var candidate = await connection.QuerySingleOrDefaultAsync<CandidateMutableRow>(new CommandDefinition(
            """
            SELECT TOP (1)
                CandidateId,
                AppUserId,
                DisplayName,
                Email
            FROM dbo.Candidates
            WHERE TenantId = @TenantId
              AND AppUserId = @ActorUserId;
            """,
            new { TenantId = tenantId, ActorUserId = actorUserId },
            transaction,
            cancellationToken: cancellationToken));
        if (candidate is null)
        {
            var appUser = await connection.QuerySingleOrDefaultAsync<AppUserCandidateSeedRow>(new CommandDefinition(
                """
                SELECT TOP (1) UserId, DisplayName, Email
                FROM dbo.AppUsers
                WHERE TenantId = @TenantId
                  AND UserId = @ActorUserId
                  AND DeletedAtUtc IS NULL
                  AND AccountStatus <> N'Disabled';
                """,
                new { TenantId = tenantId, ActorUserId = actorUserId },
                transaction,
                cancellationToken: cancellationToken));
            if (appUser is null)
            {
                return null;
            }

            candidate = new CandidateMutableRow(Guid.NewGuid(), appUser.UserId, appUser.DisplayName, appUser.Email);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO dbo.Candidates
                (
                    CandidateId,
                    TenantId,
                    AppUserId,
                    DisplayName,
                    Email,
                    Phone,
                    LinkedInUrl,
                    CurrentDesignation,
                    CurrentCompany,
                    ExperienceYears,
                    NoticePeriodDays,
                    Status,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    @CandidateId,
                    @TenantId,
                    @AppUserId,
                    @DisplayName,
                    @Email,
                    @Phone,
                    @LinkedInUrl,
                    @CurrentDesignation,
                    @CurrentCompany,
                    @ExperienceYears,
                    @NoticePeriodDays,
                    N'Active',
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
                """,
                new
                {
                    tenantId,
                    candidate.CandidateId,
                    candidate.AppUserId,
                    candidate.DisplayName,
                    candidate.Email,
                    Phone = NullIfBlank(phone),
                    LinkedInUrl = NullIfBlank(linkedInUrl),
                    CurrentDesignation = NullIfBlank(currentDesignation),
                    CurrentCompany = NullIfBlank(currentCompany),
                    ExperienceYears = experienceYears,
                    NoticePeriodDays = noticePeriodDays
                },
                transaction,
                cancellationToken: cancellationToken));
        }
        else
        {
            await UpdateCandidateProfileAsync(
                connection,
                transaction,
                tenantId,
                candidate.CandidateId,
                phone,
                linkedInUrl,
                currentDesignation,
                currentCompany,
                experienceYears,
                noticePeriodDays,
                cancellationToken);
        }

        return candidate;
    }

    private static async Task<CandidateMutableRow?> EnsurePortalCandidateIdentityAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var candidate = await connection.QuerySingleOrDefaultAsync<CandidateMutableRow>(new CommandDefinition(
            """
            SELECT TOP (1)
                CandidateId,
                AppUserId,
                DisplayName,
                Email
            FROM dbo.Candidates
            WHERE TenantId = @TenantId
              AND AppUserId = @ActorUserId;
            """,
            new { TenantId = tenantId, ActorUserId = actorUserId },
            transaction,
            cancellationToken: cancellationToken));
        if (candidate is not null)
        {
            return candidate;
        }

        var appUser = await connection.QuerySingleOrDefaultAsync<AppUserCandidateSeedRow>(new CommandDefinition(
            """
            SELECT TOP (1) UserId, DisplayName, Email
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId = @ActorUserId
              AND DeletedAtUtc IS NULL
              AND AccountStatus <> N'Disabled';
            """,
            new { TenantId = tenantId, ActorUserId = actorUserId },
            transaction,
            cancellationToken: cancellationToken));
        if (appUser is null)
        {
            return null;
        }

        candidate = new CandidateMutableRow(Guid.NewGuid(), appUser.UserId, appUser.DisplayName, appUser.Email);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.Candidates
            (
                CandidateId,
                TenantId,
                AppUserId,
                DisplayName,
                Email,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @CandidateId,
                @TenantId,
                @AppUserId,
                @DisplayName,
                @Email,
                N'Active',
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """,
            new
            {
                TenantId = tenantId,
                candidate.CandidateId,
                candidate.AppUserId,
                candidate.DisplayName,
                candidate.Email
            },
            transaction,
            cancellationToken: cancellationToken));

        return candidate;
    }

    private static async Task<PortalCandidateProfile?> ReadPortalCandidateProfileAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var candidate = await connection.QuerySingleOrDefaultAsync<PortalCandidateProfileRow>(new CommandDefinition(
            """
            SELECT TOP (1)
                CandidateId,
                DisplayName,
                Email,
                Phone,
                LinkedInUrl,
                CurrentDesignation,
                CurrentCompany,
                ExperienceYears,
                ExpectedSalaryAmount,
                ExpectedSalaryCurrency,
                NoticePeriodDays
            FROM dbo.Candidates
            WHERE TenantId = @TenantId
              AND AppUserId = @ActorUserId;
            """,
            new { TenantId = tenantId, ActorUserId = actorUserId },
            transaction,
            cancellationToken: cancellationToken));

        if (candidate is null)
        {
            var appUser = await connection.QuerySingleOrDefaultAsync<AppUserCandidateSeedRow>(new CommandDefinition(
                """
                SELECT TOP (1) UserId, DisplayName, Email
                FROM dbo.AppUsers
                WHERE TenantId = @TenantId
                  AND UserId = @ActorUserId
                  AND DeletedAtUtc IS NULL
                  AND AccountStatus <> N'Disabled';
                """,
                new { TenantId = tenantId, ActorUserId = actorUserId },
                transaction,
                cancellationToken: cancellationToken));
            if (appUser is null)
            {
                return null;
            }

            candidate = new PortalCandidateProfileRow(
                null,
                appUser.DisplayName,
                appUser.Email,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        var skillOptions = await ListPortalCandidateSkillOptionsAsync(connection, transaction, tenantId, cancellationToken);
        if (!candidate.CandidateId.HasValue)
        {
            return new PortalCandidateProfile(
                null,
                candidate.DisplayName,
                candidate.Email,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [],
                skillOptions,
                null);
        }

        var skills = await ListPortalCandidateProfileSkillsAsync(
            connection,
            transaction,
            tenantId,
            candidate.CandidateId.Value,
            cancellationToken);
        var education = await ReadPortalPrimaryEducationAsync(
            connection,
            transaction,
            tenantId,
            candidate.CandidateId.Value,
            cancellationToken);
        var workHistory = await ReadPortalCurrentWorkHistoryAsync(
            connection,
            transaction,
            tenantId,
            candidate.CandidateId.Value,
            cancellationToken);
        var resumeDocument = await ReadLatestPortalCandidateProfileDocumentAsync(
            connection,
            transaction,
            tenantId,
            candidate.CandidateId.Value,
            cancellationToken);

        return new PortalCandidateProfile(
            candidate.CandidateId,
            candidate.DisplayName,
            candidate.Email,
            candidate.Phone,
            candidate.LinkedInUrl,
            candidate.CurrentDesignation,
            candidate.CurrentCompany,
            candidate.ExperienceYears,
            candidate.ExpectedSalaryAmount,
            candidate.ExpectedSalaryCurrency,
            candidate.NoticePeriodDays,
            education,
            workHistory,
            skills,
            skillOptions,
            resumeDocument?.ToDocument());
    }

    private static async Task<PortalCandidateProfileDocumentRow?> ReadLatestPortalCandidateProfileDocumentAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                CandidateProfileDocumentId,
                CandidateId,
                DocumentType,
                OriginalFileName AS FileName,
                ContentType,
                SizeBytes,
                StorageProvider,
                StorageKey,
                StorageContainer,
                ContentHashSha256,
                UploadedAtUtc AS UploadedAt,
                ExtractionStatus,
                CAST(CASE WHEN NULLIF(LTRIM(RTRIM(ExtractedText)), N'') IS NULL THEN 0 ELSE 1 END AS bit) AS HasExtractedText,
                ExtractedText,
                ExtractedTextHashSha256,
                ParserVersion,
                ExtractedAtUtc AS ExtractedAt,
                ExtractionError
            FROM dbo.CandidateProfileDocuments
            WHERE TenantId = @TenantId
              AND CandidateId = @CandidateId
              AND Status = N'Active'
              AND LOWER(DocumentType) IN (N'resume', N'cv')
            ORDER BY UploadedAtUtc DESC;
            """;

        return await connection.QuerySingleOrDefaultAsync<PortalCandidateProfileDocumentRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, CandidateId = candidateId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid?> UpsertPortalCandidateProfileAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        UpdatePortalCandidateProfileInput input,
        CancellationToken cancellationToken)
    {
        var appUser = await connection.QuerySingleOrDefaultAsync<AppUserCandidateSeedRow>(new CommandDefinition(
            """
            SELECT TOP (1) UserId, DisplayName, Email
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId = @ActorUserId
              AND DeletedAtUtc IS NULL
              AND AccountStatus <> N'Disabled';
            """,
            new { TenantId = tenantId, ActorUserId = actorUserId },
            transaction,
            cancellationToken: cancellationToken));
        if (appUser is null)
        {
            return null;
        }

        var existingCandidateId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            """
            SELECT TOP (1) CandidateId
            FROM dbo.Candidates
            WHERE TenantId = @TenantId
              AND AppUserId = @ActorUserId;
            """,
            new { TenantId = tenantId, ActorUserId = actorUserId },
            transaction,
            cancellationToken: cancellationToken));

        if (!existingCandidateId.HasValue)
        {
            var candidateId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO dbo.Candidates
                (
                    CandidateId,
                    TenantId,
                    AppUserId,
                    DisplayName,
                    Email,
                    Phone,
                    LinkedInUrl,
                    CurrentDesignation,
                    CurrentCompany,
                    ExperienceYears,
                    ExpectedSalaryAmount,
                    ExpectedSalaryCurrency,
                    NoticePeriodDays,
                    Status,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    @CandidateId,
                    @TenantId,
                    @AppUserId,
                    @DisplayName,
                    @Email,
                    @Phone,
                    @LinkedInUrl,
                    @CurrentDesignation,
                    @CurrentCompany,
                    @ExperienceYears,
                    @ExpectedSalaryAmount,
                    @ExpectedSalaryCurrency,
                    @NoticePeriodDays,
                    N'Active',
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
                """,
                new
                {
                    CandidateId = candidateId,
                    TenantId = tenantId,
                    AppUserId = actorUserId,
                    DisplayName = Truncate(input.DisplayName, 200),
                    Email = appUser.Email,
                    input.Phone,
                    input.LinkedInUrl,
                    input.CurrentDesignation,
                    input.CurrentCompany,
                    input.ExperienceYears,
                    input.ExpectedSalaryAmount,
                    input.ExpectedSalaryCurrency,
                    input.NoticePeriodDays
                },
                transaction,
                cancellationToken: cancellationToken));
            return candidateId;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.Candidates
            SET DisplayName = @DisplayName,
                Phone = @Phone,
                LinkedInUrl = @LinkedInUrl,
                CurrentDesignation = @CurrentDesignation,
                CurrentCompany = @CurrentCompany,
                ExperienceYears = @ExperienceYears,
                ExpectedSalaryAmount = @ExpectedSalaryAmount,
                ExpectedSalaryCurrency = @ExpectedSalaryCurrency,
                NoticePeriodDays = @NoticePeriodDays,
                Status = N'Active',
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND CandidateId = @CandidateId;
            """,
            new
            {
                TenantId = tenantId,
                CandidateId = existingCandidateId.Value,
                DisplayName = Truncate(input.DisplayName, 200),
                input.Phone,
                input.LinkedInUrl,
                input.CurrentDesignation,
                input.CurrentCompany,
                input.ExperienceYears,
                input.ExpectedSalaryAmount,
                input.ExpectedSalaryCurrency,
                input.NoticePeriodDays
            },
            transaction,
            cancellationToken: cancellationToken));

        return existingCandidateId.Value;
    }

    private static async Task<IReadOnlyList<PortalCandidateProfileSkillOption>> ListPortalCandidateSkillOptionsAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SkillId, Name AS SkillName, Category
            FROM dbo.Skills
            WHERE TenantId = @TenantId
              AND Status = N'Active'
            ORDER BY Category, Name;
            """;

        var rows = await connection.QueryAsync<PortalCandidateProfileSkillOption>(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            transaction,
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    private static async Task<IReadOnlyList<PortalCandidateProfileSkill>> ListPortalCandidateProfileSkillsAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                skill.SkillId,
                skill.Name AS SkillName,
                candidateSkill.SkillLevel,
                candidateSkill.YearsExperience,
                candidateSkill.IsPrimary
            FROM dbo.CandidateSkills AS candidateSkill
            INNER JOIN dbo.Skills AS skill
                ON skill.TenantId = candidateSkill.TenantId
                AND skill.SkillId = candidateSkill.SkillId
            WHERE candidateSkill.TenantId = @TenantId
              AND candidateSkill.CandidateId = @CandidateId
            ORDER BY candidateSkill.IsPrimary DESC, skill.Name;
            """;

        var rows = await connection.QueryAsync<PortalCandidateProfileSkill>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, CandidateId = candidateId },
            transaction,
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    private static async Task<PortalCandidateProfileEducation?> ReadPortalPrimaryEducationAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) UniversityName, DegreeName, GraduationYear
            FROM dbo.CandidateEducation
            WHERE TenantId = @TenantId
              AND CandidateId = @CandidateId
            ORDER BY IsPrimary DESC, UpdatedAtUtc DESC, CreatedAtUtc DESC;
            """;

        return await connection.QuerySingleOrDefaultAsync<PortalCandidateProfileEducation>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, CandidateId = candidateId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<PortalCandidateProfileWorkHistory?> ReadPortalCurrentWorkHistoryAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) CompanyName, Title
            FROM dbo.CandidateWorkHistory
            WHERE TenantId = @TenantId
              AND CandidateId = @CandidateId
            ORDER BY IsCurrent DESC, UpdatedAtUtc DESC, CreatedAtUtc DESC;
            """;

        return await connection.QuerySingleOrDefaultAsync<PortalCandidateProfileWorkHistory>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, CandidateId = candidateId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task ReplacePortalCandidateSkillsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid candidateId,
        IReadOnlyList<UpdatePortalCandidateProfileSkillInput> skills,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM dbo.CandidateSkills
            WHERE TenantId = @TenantId
              AND CandidateId = @CandidateId;
            """,
            new { TenantId = tenantId, CandidateId = candidateId },
            transaction,
            cancellationToken: cancellationToken));

        var rows = skills
            .Where(skill => skill.SkillId != Guid.Empty)
            .GroupBy(skill => skill.SkillId)
            .Select(group => group.First())
            .ToArray();
        if (rows.Length == 0)
        {
            return;
        }

        const string insertSql = """
            INSERT INTO dbo.CandidateSkills
            (
                TenantId,
                CandidateId,
                SkillId,
                SkillLevel,
                YearsExperience,
                IsPrimary,
                CreatedAtUtc
            )
            SELECT
                @TenantId,
                @CandidateId,
                skill.SkillId,
                @SkillLevel,
                @YearsExperience,
                @IsPrimary,
                SYSUTCDATETIME()
            FROM dbo.Skills AS skill
            WHERE skill.TenantId = @TenantId
              AND skill.SkillId = @SkillId
              AND skill.Status = N'Active';
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            rows.Select(skill => new
            {
                TenantId = tenantId,
                CandidateId = candidateId,
                skill.SkillId,
                SkillLevel = string.IsNullOrWhiteSpace(skill.SkillLevel) ? "Intermediate" : skill.SkillLevel,
                skill.YearsExperience,
                skill.IsPrimary
            }),
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task ReplacePortalPrimaryEducationAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid candidateId,
        PortalCandidateProfileEducation? education,
        CancellationToken cancellationToken)
    {
        if (education is null ||
            (string.IsNullOrWhiteSpace(education.UniversityName) &&
             string.IsNullOrWhiteSpace(education.DegreeName) &&
             !education.GraduationYear.HasValue))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                DELETE FROM dbo.CandidateEducation
                WHERE TenantId = @TenantId
                  AND CandidateId = @CandidateId
                  AND IsPrimary = CAST(1 AS BIT);
                """,
                new { TenantId = tenantId, CandidateId = candidateId },
                transaction,
                cancellationToken: cancellationToken));
            return;
        }

        const string sql = """
            MERGE dbo.CandidateEducation AS target
            USING
            (
                SELECT @TenantId AS TenantId, @CandidateId AS CandidateId
            ) AS source
            ON target.TenantId = source.TenantId
               AND target.CandidateId = source.CandidateId
               AND target.IsPrimary = CAST(1 AS BIT)
            WHEN MATCHED THEN UPDATE SET
                UniversityName = @UniversityName,
                DegreeName = @DegreeName,
                GraduationYear = @GraduationYear,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT
                (
                    CandidateEducationId,
                    TenantId,
                    CandidateId,
                    UniversityName,
                    DegreeName,
                    GraduationYear,
                    IsPrimary,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    NEWID(),
                    @TenantId,
                    @CandidateId,
                    @UniversityName,
                    @DegreeName,
                    @GraduationYear,
                    CAST(1 AS BIT),
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                CandidateId = candidateId,
                UniversityName = NullIfBlank(education.UniversityName) ?? "Not recorded",
                DegreeName = NullIfBlank(education.DegreeName),
                education.GraduationYear
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task ReplacePortalCurrentWorkHistoryAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid candidateId,
        PortalCandidateProfileWorkHistory? workHistory,
        CancellationToken cancellationToken)
    {
        if (workHistory is null ||
            (string.IsNullOrWhiteSpace(workHistory.CompanyName) && string.IsNullOrWhiteSpace(workHistory.Title)))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                DELETE FROM dbo.CandidateWorkHistory
                WHERE TenantId = @TenantId
                  AND CandidateId = @CandidateId
                  AND IsCurrent = CAST(1 AS BIT);
                """,
                new { TenantId = tenantId, CandidateId = candidateId },
                transaction,
                cancellationToken: cancellationToken));
            return;
        }

        const string sql = """
            MERGE dbo.CandidateWorkHistory AS target
            USING
            (
                SELECT @TenantId AS TenantId, @CandidateId AS CandidateId
            ) AS source
            ON target.TenantId = source.TenantId
               AND target.CandidateId = source.CandidateId
               AND target.IsCurrent = CAST(1 AS BIT)
            WHEN MATCHED THEN UPDATE SET
                CompanyName = @CompanyName,
                Title = @Title,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT
                (
                    CandidateWorkHistoryId,
                    TenantId,
                    CandidateId,
                    CompanyName,
                    Title,
                    IsCurrent,
                    StartsOn,
                    EndsOn,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    NEWID(),
                    @TenantId,
                    @CandidateId,
                    @CompanyName,
                    @Title,
                    CAST(1 AS BIT),
                    NULL,
                    NULL,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                CandidateId = candidateId,
                CompanyName = NullIfBlank(workHistory.CompanyName) ?? "Not recorded",
                Title = NullIfBlank(workHistory.Title)
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<CandidateMutableRow?> CreateInvitedCandidateAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string displayName,
        string email,
        string? phone,
        string? linkedInUrl,
        string? currentDesignation,
        string? currentCompany,
        decimal? experienceYears,
        int? noticePeriodDays,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var appUser = await connection.QuerySingleOrDefaultAsync<AppUserCandidateSeedRow>(new CommandDefinition(
            """
            SELECT TOP (1) UserId, DisplayName, Email
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND EmailNormalized = @EmailNormalized
              AND DeletedAtUtc IS NULL;
            """,
            new { TenantId = tenantId, EmailNormalized = normalizedEmail },
            transaction,
            cancellationToken: cancellationToken));
        if (appUser is null)
        {
            var userId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO dbo.AppUsers
                (
                    UserId,
                    TenantId,
                    DisplayName,
                    Email,
                    EmailNormalized,
                    Initials,
                    AccountStatus,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    @UserId,
                    @TenantId,
                    @DisplayName,
                    @Email,
                    @EmailNormalized,
                    @Initials,
                    N'Invited',
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );

                INSERT INTO dbo.UserCredentials
                (
                    UserCredentialId,
                    TenantId,
                    UserId,
                    PasswordHash,
                    PasswordUpdatedAtUtc,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    NEWID(),
                    @TenantId,
                    @UserId,
                    NULL,
                    NULL,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
                """,
                new
                {
                    UserId = userId,
                    TenantId = tenantId,
                    DisplayName = Truncate(displayName.Trim(), 200),
                    Email = email.Trim(),
                    EmailNormalized = normalizedEmail,
                    Initials = BuildInitials(displayName)
                },
                transaction,
                cancellationToken: cancellationToken));

            appUser = new AppUserCandidateSeedRow(userId, Truncate(displayName.Trim(), 200), email.Trim());
        }

        await EnsureCandidateRoleAsync(connection, transaction, tenantId, appUser.UserId, actorUserId, cancellationToken);

        var candidate = new CandidateMutableRow(Guid.NewGuid(), appUser.UserId, appUser.DisplayName, appUser.Email);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.Candidates
            (
                CandidateId,
                TenantId,
                AppUserId,
                DisplayName,
                Email,
                Phone,
                LinkedInUrl,
                CurrentDesignation,
                CurrentCompany,
                ExperienceYears,
                NoticePeriodDays,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @CandidateId,
                @TenantId,
                @AppUserId,
                @DisplayName,
                @Email,
                @Phone,
                @LinkedInUrl,
                @CurrentDesignation,
                @CurrentCompany,
                @ExperienceYears,
                @NoticePeriodDays,
                N'Active',
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """,
            new
            {
                candidate.CandidateId,
                TenantId = tenantId,
                candidate.AppUserId,
                candidate.DisplayName,
                candidate.Email,
                Phone = NullIfBlank(phone),
                LinkedInUrl = NullIfBlank(linkedInUrl),
                CurrentDesignation = NullIfBlank(currentDesignation),
                CurrentCompany = NullIfBlank(currentCompany),
                ExperienceYears = experienceYears,
                NoticePeriodDays = noticePeriodDays
            },
            transaction,
            cancellationToken: cancellationToken));

        return candidate;
    }

    private static async Task EnsureCandidateRoleAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid userId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.UserRoles (TenantId, UserId, RoleId, AssignedByUserId, CreatedAtUtc)
            SELECT @TenantId, @UserId, role.RoleId, @ActorUserId, SYSUTCDATETIME()
            FROM dbo.Roles AS role
            WHERE role.TenantId = @TenantId
              AND role.Code = N'Candidate'
              AND role.Status = N'Active'
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.UserRoles AS existing
                  WHERE existing.TenantId = @TenantId
                    AND existing.UserId = @UserId
                    AND existing.RoleId = role.RoleId
              );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserId = userId, ActorUserId = actorUserId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<CandidateMutableRow?> ReadCandidateByIdAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) CandidateId, AppUserId, DisplayName, Email
            FROM dbo.Candidates
            WHERE TenantId = @TenantId
              AND CandidateId = @CandidateId
              AND Status = N'Active';
            """;

        return await connection.QuerySingleOrDefaultAsync<CandidateMutableRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, CandidateId = candidateId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<OperationsOnlineHeadhuntingDuplicateCandidate>> ListOnlineHeadhuntingDuplicateCandidatesAsync(
        SqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                candidate.CandidateId,
                candidate.DisplayName,
                candidate.Email,
                candidate.Phone,
                candidate.LinkedInUrl,
                candidate.CurrentDesignation,
                candidate.CurrentCompany,
                candidate.ExperienceYears,
                skill.Name AS SkillName
            FROM dbo.Candidates AS candidate
            LEFT JOIN dbo.CandidateSkills AS candidateSkill
                ON candidateSkill.TenantId = candidate.TenantId
                AND candidateSkill.CandidateId = candidate.CandidateId
            LEFT JOIN dbo.Skills AS skill
                ON skill.TenantId = candidateSkill.TenantId
                AND skill.SkillId = candidateSkill.SkillId
            WHERE candidate.TenantId = @TenantId
              AND candidate.Status = N'Active'
            ORDER BY candidate.DisplayName, skill.Name;
            """;

        var rows = (await connection.QueryAsync<OnlineHeadhuntingDuplicateCandidateRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken))).ToArray();

        return rows
            .GroupBy(row => row.CandidateId)
            .Select(group =>
            {
                var first = group.First();
                return new OperationsOnlineHeadhuntingDuplicateCandidate(
                    first.CandidateId,
                    first.DisplayName,
                    first.Email,
                    first.Phone,
                    first.LinkedInUrl,
                    first.CurrentDesignation,
                    first.CurrentCompany,
                    first.ExperienceYears,
                    group.Select(row => row.SkillName)
                        .Where(skill => !string.IsNullOrWhiteSpace(skill))
                        .Select(skill => skill!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray());
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<OperationsOnlineHeadhuntingExistingLead>> ListOnlineHeadhuntingExistingLeadsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                SourceUrl,
                ProfileUrl,
                Email,
                Phone,
                DisplayName,
                CurrentTitle,
                CurrentCompany,
                LocationText
            FROM dbo.OnlineCandidateLeads
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        return (await connection.QueryAsync<OperationsOnlineHeadhuntingExistingLead>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken))).ToArray();
    }

    private static OperationsOnlineCandidateLead ToPersistedLead(
        Guid leadId,
        Guid runId,
        Guid jobRequestId,
        OnlineHeadhuntingAgentLead lead,
        string status,
        DateTime createdAtUtc)
    {
        return new OperationsOnlineCandidateLead(
            leadId,
            runId,
            jobRequestId,
            lead.Rank,
            lead.SourceCode,
            lead.SourceDisplayName,
            lead.SourceUrl,
            lead.DisplayName,
            lead.CurrentTitle,
            lead.CurrentCompany,
            lead.LocationText,
            lead.Email,
            lead.Phone,
            lead.ProfileUrl,
            lead.EvidenceSnippet,
            lead.MatchScore,
            lead.Confidence,
            lead.FitSummary,
            lead.Strengths,
            lead.MatchedSkills,
            lead.Gaps,
            lead.MissingData,
            lead.DuplicateStatus,
            lead.DuplicateCandidateId,
            lead.DuplicateCandidateName,
            lead.DuplicateExplanation,
            lead.OutreachDraft,
            status,
            Utc(createdAtUtc));
    }

    private static IReadOnlyList<OnlineHeadhuntingAgentLead> DistinctNewOnlineLeads(
        IReadOnlyList<OperationsOnlineHeadhuntingExistingLead> existingLeads,
        IReadOnlyList<OnlineHeadhuntingAgentLead> leads)
    {
        var seen = existingLeads
            .SelectMany(GetOnlineLeadIdentityKeys)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var distinct = new List<OnlineHeadhuntingAgentLead>();
        foreach (var lead in leads)
        {
            var keys = GetOnlineLeadIdentityKeys(lead).ToArray();
            if (keys.Length > 0 && keys.Any(seen.Contains))
            {
                continue;
            }

            foreach (var key in keys)
            {
                seen.Add(key);
            }

            distinct.Add(lead with { Rank = distinct.Count + 1 });
        }

        return distinct;
    }

    private static IEnumerable<string> GetOnlineLeadIdentityKeys(OperationsOnlineHeadhuntingExistingLead lead)
    {
        var sourceUrl = NormalizeOnlineLeadUrl(lead.SourceUrl);
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            yield return $"url:{sourceUrl}";
        }

        var profileUrl = NormalizeOnlineLeadUrl(lead.ProfileUrl);
        if (!string.IsNullOrWhiteSpace(profileUrl))
        {
            yield return $"url:{profileUrl}";
        }

        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            yield return $"email:{lead.Email.Trim().ToLowerInvariant()}";
        }

        if (!string.IsNullOrWhiteSpace(lead.Phone))
        {
            var phone = new string(lead.Phone.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrWhiteSpace(phone))
            {
                yield return $"phone:{phone}";
            }
        }
    }

    private static IEnumerable<string> GetOnlineLeadIdentityKeys(OnlineHeadhuntingAgentLead lead)
    {
        var sourceUrl = NormalizeOnlineLeadUrl(lead.SourceUrl);
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            yield return $"url:{sourceUrl}";
        }

        var profileUrl = NormalizeOnlineLeadUrl(lead.ProfileUrl);
        if (!string.IsNullOrWhiteSpace(profileUrl))
        {
            yield return $"url:{profileUrl}";
        }

        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            yield return $"email:{lead.Email.Trim().ToLowerInvariant()}";
        }

        if (!string.IsNullOrWhiteSpace(lead.Phone))
        {
            var phone = new string(lead.Phone.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrWhiteSpace(phone))
            {
                yield return $"phone:{phone}";
            }
        }
    }

    private static async Task<OperationsOnlineHeadhuntingResult?> GetLatestOnlineHeadhuntingResultAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string runSql = """
            SELECT TOP (1)
                OnlineCandidateSourcingRunId,
                JobRequestId,
                JobPostId,
                AiAgentRunId,
                SearchMoreFromRunId,
                RequestedLimit,
                DailyLeadLimit,
                DailyLeadCountBeforeRun,
                LeadsReturned,
                SearchStatus,
                Model,
                SourceCodesJson,
                QueriesJson,
                CreatedAtUtc
            FROM dbo.OnlineCandidateSourcingRuns
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId
            ORDER BY CreatedAtUtc DESC;
            """;

        var run = await connection.QuerySingleOrDefaultAsync<OnlineHeadhuntingRunRow>(new CommandDefinition(
            runSql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken));
        if (run is null)
        {
            return null;
        }

        const string leadSql = """
            SELECT
                OnlineCandidateLeadId,
                OnlineCandidateSourcingRunId,
                JobRequestId,
                Rank,
                SourceCode,
                SourceDisplayName,
                SourceUrl,
                DisplayName,
                CurrentTitle,
                CurrentCompany,
                LocationText,
                Email,
                Phone,
                ProfileUrl,
                EvidenceSnippet,
                MatchScore,
                Confidence,
                FitSummary,
                StrengthsJson,
                MatchedSkillsJson,
                GapsJson,
                MissingDataJson,
                DuplicateStatus,
                DuplicateCandidateId,
                DuplicateCandidateName,
                DuplicateExplanation,
                OutreachDraft,
                Status,
                CreatedAtUtc
            FROM dbo.OnlineCandidateLeads
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId
            ORDER BY CreatedAtUtc DESC, Rank;
            """;

        var leads = (await connection.QueryAsync<OnlineHeadhuntingLeadRow>(new CommandDefinition(
            leadSql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            cancellationToken: cancellationToken)))
            .Select(ToOnlineLead)
            .GroupBy(GetOnlineLeadIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        return new OperationsOnlineHeadhuntingResult(
            new OperationsOnlineHeadhuntingRunSummary(
                run.OnlineCandidateSourcingRunId,
                run.JobRequestId,
                run.JobPostId,
                run.AiAgentRunId,
                run.SearchMoreFromRunId,
                run.RequestedLimit,
                run.DailyLeadLimit,
                run.DailyLeadCountBeforeRun,
                run.LeadsReturned,
                run.SearchStatus,
                run.Model,
                DeserializeStringArray(run.SourceCodesJson),
                DeserializeStringArray(run.QueriesJson),
                Utc(run.CreatedAtUtc)),
            leads);
    }

    private static OperationsOnlineCandidateLead ToOnlineLead(OnlineHeadhuntingLeadRow row)
    {
        return new OperationsOnlineCandidateLead(
            row.OnlineCandidateLeadId,
            row.OnlineCandidateSourcingRunId,
            row.JobRequestId,
            row.Rank,
            row.SourceCode,
            row.SourceDisplayName,
            row.SourceUrl,
            row.DisplayName,
            row.CurrentTitle,
            row.CurrentCompany,
            row.LocationText,
            row.Email,
            row.Phone,
            row.ProfileUrl,
            row.EvidenceSnippet,
            row.MatchScore,
            row.Confidence,
            row.FitSummary,
            DeserializeStringArray(row.StrengthsJson),
            DeserializeStringArray(row.MatchedSkillsJson),
            DeserializeStringArray(row.GapsJson),
            DeserializeStringArray(row.MissingDataJson),
            row.DuplicateStatus,
            row.DuplicateCandidateId,
            row.DuplicateCandidateName,
            row.DuplicateExplanation,
            row.OutreachDraft,
            row.Status,
            Utc(row.CreatedAtUtc));
    }

    private static string GetOnlineLeadIdentity(OperationsOnlineCandidateLead lead)
    {
        var profileUrl = NormalizeOnlineLeadUrl(lead.ProfileUrl);
        if (!string.IsNullOrWhiteSpace(profileUrl))
        {
            return $"url:{profileUrl}";
        }

        var sourceUrl = NormalizeOnlineLeadUrl(lead.SourceUrl);
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            return $"url:{sourceUrl}";
        }

        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            return $"email:{lead.Email.Trim().ToLowerInvariant()}";
        }

        if (!string.IsNullOrWhiteSpace(lead.Phone))
        {
            var phone = new string(lead.Phone.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrWhiteSpace(phone))
            {
                return $"phone:{phone}";
            }
        }

        return $"lead:{lead.OnlineCandidateLeadId:D}";
    }

    private static string NormalizeOnlineLeadUrl(string? url)
    {
        return string.IsNullOrWhiteSpace(url)
            ? string.Empty
            : url.Trim().TrimEnd('/').ToLowerInvariant();
    }

    private static async Task<OperationsOnlineCandidateLead?> ReadOnlineLeadAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid onlineCandidateLeadId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                OnlineCandidateLeadId,
                OnlineCandidateSourcingRunId,
                JobRequestId,
                Rank,
                SourceCode,
                SourceDisplayName,
                SourceUrl,
                DisplayName,
                CurrentTitle,
                CurrentCompany,
                LocationText,
                Email,
                Phone,
                ProfileUrl,
                EvidenceSnippet,
                MatchScore,
                Confidence,
                FitSummary,
                StrengthsJson,
                MatchedSkillsJson,
                GapsJson,
                MissingDataJson,
                DuplicateStatus,
                DuplicateCandidateId,
                DuplicateCandidateName,
                DuplicateExplanation,
                OutreachDraft,
                Status,
                CreatedAtUtc
            FROM dbo.OnlineCandidateLeads
            WHERE TenantId = @TenantId
              AND OnlineCandidateLeadId = @OnlineCandidateLeadId;
            """;

        var row = await connection.QuerySingleOrDefaultAsync<OnlineHeadhuntingLeadRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, OnlineCandidateLeadId = onlineCandidateLeadId },
            cancellationToken: cancellationToken));
        return row is null ? null : ToOnlineLead(row);
    }

    private static async Task MarkOnlineHeadhuntingLeadConvertedAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid onlineLeadId,
        Guid candidateId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.OnlineCandidateLeads
            SET Status = N'Converted',
                ConvertedCandidateId = @CandidateId,
                ConvertedJobApplicationId = @JobApplicationId,
                ConvertedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND OnlineCandidateLeadId = @OnlineLeadId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                OnlineLeadId = onlineLeadId,
                CandidateId = candidateId,
                JobApplicationId = jobApplicationId
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<CandidateMutableRow?> ReadCandidateByEmailAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string email,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) CandidateId, AppUserId, DisplayName, Email
            FROM dbo.Candidates
            WHERE TenantId = @TenantId
              AND Email = @Email
              AND Status = N'Active';
            """;

        return await connection.QuerySingleOrDefaultAsync<CandidateMutableRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, Email = email.Trim() },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task UpdateCandidateProfileAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid candidateId,
        string? phone,
        string? linkedInUrl,
        string? currentDesignation,
        string? currentCompany,
        decimal? experienceYears,
        int? noticePeriodDays,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.Candidates
            SET Phone = COALESCE(@Phone, Phone),
                LinkedInUrl = COALESCE(@LinkedInUrl, LinkedInUrl),
                CurrentDesignation = COALESCE(@CurrentDesignation, CurrentDesignation),
                CurrentCompany = COALESCE(@CurrentCompany, CurrentCompany),
                ExperienceYears = COALESCE(@ExperienceYears, ExperienceYears),
                NoticePeriodDays = COALESCE(@NoticePeriodDays, NoticePeriodDays),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND CandidateId = @CandidateId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                CandidateId = candidateId,
                Phone = NullIfBlank(phone),
                LinkedInUrl = NullIfBlank(linkedInUrl),
                CurrentDesignation = NullIfBlank(currentDesignation),
                CurrentCompany = NullIfBlank(currentCompany),
                ExperienceYears = experienceYears,
                NoticePeriodDays = noticePeriodDays
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task ReplaceCandidateSkillsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid candidateId,
        IReadOnlyList<Guid> skillIds,
        CancellationToken cancellationToken)
    {
        if (skillIds.Count == 0)
        {
            return;
        }

        const string deleteSql = """
            DELETE FROM dbo.CandidateSkills
            WHERE TenantId = @TenantId
              AND CandidateId = @CandidateId
              AND SkillId IN @SkillIds;
            """;
        const string insertSql = """
            INSERT INTO dbo.CandidateSkills (TenantId, CandidateId, SkillId, SkillLevel, YearsExperience, IsPrimary, CreatedAtUtc)
            SELECT @TenantId, @CandidateId, skill.SkillId, N'Intermediate', NULL, CAST(0 AS BIT), SYSUTCDATETIME()
            FROM dbo.Skills AS skill
            WHERE skill.TenantId = @TenantId
              AND skill.SkillId IN @SkillIds
              AND skill.Status = N'Active';
            """;

        var distinctSkillIds = skillIds.Where(skillId => skillId != Guid.Empty).Distinct().ToArray();
        if (distinctSkillIds.Length == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql,
            new { TenantId = tenantId, CandidateId = candidateId, SkillIds = distinctSkillIds },
            transaction,
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            new { TenantId = tenantId, CandidateId = candidateId, SkillIds = distinctSkillIds },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task UpsertCandidateEducationAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid candidateId,
        string? universityName,
        string? degreeName,
        int? graduationYear,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(universityName) && string.IsNullOrWhiteSpace(degreeName) && !graduationYear.HasValue)
        {
            return;
        }

        const string sql = """
            MERGE dbo.CandidateEducation AS target
            USING
            (
                SELECT @TenantId AS TenantId, @CandidateId AS CandidateId
            ) AS source
            ON target.TenantId = source.TenantId
               AND target.CandidateId = source.CandidateId
               AND target.IsPrimary = CAST(1 AS BIT)
            WHEN MATCHED THEN UPDATE SET
                UniversityName = COALESCE(@UniversityName, target.UniversityName),
                DegreeName = COALESCE(@DegreeName, target.DegreeName),
                GraduationYear = COALESCE(@GraduationYear, target.GraduationYear),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT
                (
                    CandidateEducationId,
                    TenantId,
                    CandidateId,
                    UniversityName,
                    DegreeName,
                    GraduationYear,
                    IsPrimary,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    NEWID(),
                    @TenantId,
                    @CandidateId,
                    COALESCE(@UniversityName, N'Not recorded'),
                    @DegreeName,
                    @GraduationYear,
                    CAST(1 AS BIT),
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                CandidateId = candidateId,
                UniversityName = NullIfBlank(universityName),
                DegreeName = NullIfBlank(degreeName),
                GraduationYear = graduationYear
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task UpsertCandidateWorkHistoryAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid candidateId,
        string? companyName,
        string? title,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(companyName) && string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        const string sql = """
            MERGE dbo.CandidateWorkHistory AS target
            USING
            (
                SELECT @TenantId AS TenantId, @CandidateId AS CandidateId
            ) AS source
            ON target.TenantId = source.TenantId
               AND target.CandidateId = source.CandidateId
               AND target.IsCurrent = CAST(1 AS BIT)
            WHEN MATCHED THEN UPDATE SET
                CompanyName = COALESCE(@CompanyName, target.CompanyName),
                Title = COALESCE(@Title, target.Title),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT
                (
                    CandidateWorkHistoryId,
                    TenantId,
                    CandidateId,
                    CompanyName,
                    Title,
                    IsCurrent,
                    StartsOn,
                    EndsOn,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    NEWID(),
                    @TenantId,
                    @CandidateId,
                    COALESCE(@CompanyName, N'Not recorded'),
                    @Title,
                    CAST(1 AS BIT),
                    NULL,
                    NULL,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                CandidateId = candidateId,
                CompanyName = NullIfBlank(companyName),
                Title = NullIfBlank(title)
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<CandidateSourceLabelRow?> ReadCandidateSourceLabelAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string sourceLabel,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) CandidateSourceLabelId, Code, DisplayName
            FROM dbo.CandidateSourceLabels
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND
              (
                  Code = @SourceLabel
                  OR DisplayName = @SourceLabel
                  OR (@SourceLabel = N'LinkedIn' AND Code = N'LinkedInManual')
                  OR (@SourceLabel = N'Indeed' AND Code = N'IndeedManual')
                  OR (@SourceLabel = N'Job Portal' AND Code = N'JobPortal')
              )
            ORDER BY CASE WHEN Code = @SourceLabel THEN 0 ELSE 1 END;
            """;

        return await connection.QuerySingleOrDefaultAsync<CandidateSourceLabelRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, SourceLabel = sourceLabel },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<ApplicationIdentityRow?> ReadActiveJobPostApplicationAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobPostId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) JobApplicationId, CurrentStatus
            FROM dbo.JobApplications
            WHERE TenantId = @TenantId
              AND JobPostId = @JobPostId
              AND CandidateId = @CandidateId
              AND IsActive = CAST(1 AS BIT)
              AND CurrentStatus NOT IN (N'Rejected', N'Hired', N'Withdrawn')
            ORDER BY AppliedAtUtc DESC;
            """;

        return await connection.QuerySingleOrDefaultAsync<ApplicationIdentityRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId, CandidateId = candidateId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<CandidateInvitationApplicationRow?> ReadCandidateInvitationForApplicationAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobPostId,
        Guid? candidateInvitationId,
        string? invitationToken,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        if (!candidateInvitationId.HasValue || string.IsNullOrWhiteSpace(invitationToken))
        {
            return null;
        }

        const string sql = """
            SELECT TOP (1)
                CandidateInvitationId,
                CandidateId,
                Status,
                ExpiresAtUtc,
                RevokedAtUtc
            FROM dbo.CandidateInvitations
            WHERE TenantId = @TenantId
              AND CandidateInvitationId = @CandidateInvitationId
              AND JobPostId = @JobPostId
              AND TokenHash = @TokenHash
              AND ExpiresAtUtc > SYSUTCDATETIME()
              AND RevokedAtUtc IS NULL
              AND Status IN (N'Sent', N'Used')
              AND (CandidateId IS NULL OR CandidateId = @CandidateId);
            """;

        return await connection.QuerySingleOrDefaultAsync<CandidateInvitationApplicationRow>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                JobPostId = jobPostId,
                CandidateInvitationId = candidateInvitationId.Value,
                TokenHash = HashInvitationToken(invitationToken),
                CandidateId = candidateId
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task CompleteInvitedPortalApplicationAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobApplicationId,
        Guid actorUserId,
        PortalJobPostRow context,
        PortalApplyToJobPostInput input,
        CancellationToken cancellationToken)
    {
        const string updateSql = """
            UPDATE dbo.JobApplications
            SET CurrentStatus = N'Applied',
                ConfirmedAtUtc = COALESCE(ConfirmedAtUtc, SYSUTCDATETIME()),
                CoverLetterText = COALESCE(@CoverLetterText, CoverLetterText),
                ApplicationSnapshotJson = @ApplicationSnapshotJson,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobApplicationId = @JobApplicationId
              AND CurrentStatus = N'Invited';
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new
            {
                TenantId = tenantId,
                JobApplicationId = jobApplicationId,
                CoverLetterText = NullIfBlank(input.CoverLetter),
                ApplicationSnapshotJson = BuildApplicationSnapshotJson(
                    context,
                    input.InterviewAvailabilityStartDate,
                    input.InterviewAvailabilityEndDate)
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertJobApplicationStatusHistoryAsync(
            connection,
            transaction,
            tenantId,
            jobApplicationId,
            "Invited",
            "Applied",
            actorUserId,
            "Candidate completed an invited application from a tracked Talent Pilot invitation.",
            cancellationToken);
    }

    private static async Task MarkCandidateInvitationUsedAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid candidateInvitationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.CandidateInvitations
            SET Status = N'Used',
                UsedAtUtc = COALESCE(UsedAtUtc, SYSUTCDATETIME())
            WHERE TenantId = @TenantId
              AND CandidateInvitationId = @CandidateInvitationId
              AND RevokedAtUtc IS NULL;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, CandidateInvitationId = candidateInvitationId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid> InsertJobApplicationAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        Guid jobPostId,
        Guid candidateId,
        Guid? sourceLabelId,
        string sourceLabel,
        string currentStatus,
        bool isInvited,
        Guid? actorUserId,
        string? sourceDetail,
        string? sourceUrl,
        string? recruiterNotes,
        string snapshotJson,
        string? coverLetterText,
        CancellationToken cancellationToken)
    {
        var applicationId = Guid.NewGuid();
        const string sql = """
            DECLARE @NextVersion INT =
            (
                SELECT ISNULL(MAX(ApplicationVersion), 0) + 1
                FROM dbo.JobApplications
                WHERE TenantId = @TenantId
                  AND JobRequestId = @JobRequestId
                  AND CandidateId = @CandidateId
            );

            INSERT INTO dbo.JobApplications
            (
                JobApplicationId,
                TenantId,
                JobRequestId,
                JobPostId,
                CandidateId,
                CandidateSourceLabelId,
                SourceLabel,
                CurrentStatus,
                ApplicationVersion,
                IsActive,
                IsInvited,
                ConfirmedAtUtc,
                AppliedAtUtc,
                FinalDecisionAtUtc,
                FinalDecisionReason,
                SourceDetail,
                SourceUrl,
                AddedByUserId,
                RecruiterNotes,
                CoverLetterText,
                ApplicationSnapshotJson,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @JobApplicationId,
                @TenantId,
                @JobRequestId,
                @JobPostId,
                @CandidateId,
                @CandidateSourceLabelId,
                @SourceLabel,
                @CurrentStatus,
                @NextVersion,
                CAST(1 AS BIT),
                @IsInvited,
                CASE WHEN @IsInvited = CAST(1 AS BIT) THEN NULL ELSE SYSUTCDATETIME() END,
                SYSUTCDATETIME(),
                NULL,
                NULL,
                @SourceDetail,
                @SourceUrl,
                @ActorUserId,
                @RecruiterNotes,
                @CoverLetterText,
                @ApplicationSnapshotJson,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                JobApplicationId = applicationId,
                TenantId = tenantId,
                JobRequestId = jobRequestId,
                JobPostId = jobPostId,
                CandidateId = candidateId,
                CandidateSourceLabelId = sourceLabelId,
                SourceLabel = Truncate(sourceLabel, 80),
                CurrentStatus = currentStatus,
                IsInvited = isInvited,
                ActorUserId = actorUserId,
                SourceDetail = NullIfBlank(sourceDetail),
                SourceUrl = NullIfBlank(sourceUrl),
                RecruiterNotes = NullIfBlank(recruiterNotes),
                CoverLetterText = NullIfBlank(coverLetterText),
                ApplicationSnapshotJson = snapshotJson
            },
            transaction,
            cancellationToken: cancellationToken));

        return applicationId;
    }

    private static async Task UpsertParsedCvApplicationDocumentAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        Guid jobApplicationId,
        Guid candidateId,
        ParsedCandidateCvEvidenceInput? evidence,
        CancellationToken cancellationToken)
    {
        if (evidence is null ||
            string.IsNullOrWhiteSpace(evidence.ExtractedText) ||
            string.IsNullOrWhiteSpace(evidence.ContentHashSha256) ||
            evidence.ContentHashSha256.Length != 64)
        {
            return;
        }

        var extractedText = BuildParsedCvEvidenceText(evidence);
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return;
        }

        const string sql = """
            DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();
            DECLARE @ApplicationDocumentId UNIQUEIDENTIFIER;

            SELECT TOP (1) @ApplicationDocumentId = ApplicationDocumentId
            FROM dbo.JobApplicationDocuments
            WHERE TenantId = @TenantId
              AND JobApplicationId = @JobApplicationId
              AND DocumentType = N'CV'
              AND ContentHashSha256 = @ContentHashSha256
            ORDER BY UploadedAtUtc DESC;

            IF @ApplicationDocumentId IS NULL
            BEGIN
                SET @ApplicationDocumentId = NEWID();

                INSERT INTO dbo.JobApplicationDocuments
                (
                    ApplicationDocumentId,
                    TenantId,
                    JobApplicationId,
                    CandidateId,
                    DocumentType,
                    OriginalFileName,
                    ContentType,
                    SizeBytes,
                    StorageProvider,
                    StorageKey,
                    StorageContainer,
                    ContentHashSha256,
                    ExtractionStatus,
                    ExtractedText,
                    ExtractedTextHashSha256,
                    ParserVersion,
                    ExtractedAtUtc,
                    ExtractionError,
                    Status,
                    UploadedByUserId,
                    UploadedAtUtc,
                    CreatedAtUtc,
                    UpdatedAtUtc
                )
                VALUES
                (
                    @ApplicationDocumentId,
                    @TenantId,
                    @JobApplicationId,
                    @CandidateId,
                    N'CV',
                    @OriginalFileName,
                    @ContentType,
                    @SizeBytes,
                    N'CvParserAgent',
                    @StorageKey,
                    NULL,
                    @ContentHashSha256,
                    N'Extracted',
                    @ExtractedText,
                    LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), @ExtractedText)), 2)),
                    @ParserVersion,
                    @ExtractedAtUtc,
                    NULL,
                    N'Active',
                    @ActorUserId,
                    @Now,
                    @Now,
                    @Now
                );
            END
            ELSE
            BEGIN
                UPDATE dbo.JobApplicationDocuments
                SET CandidateId = @CandidateId,
                    OriginalFileName = @OriginalFileName,
                    ContentType = @ContentType,
                    SizeBytes = @SizeBytes,
                    StorageProvider = N'CvParserAgent',
                    StorageKey = @StorageKey,
                    StorageContainer = NULL,
                    ExtractionStatus = N'Extracted',
                    ExtractedText = @ExtractedText,
                    ExtractedTextHashSha256 = LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), @ExtractedText)), 2)),
                    ParserVersion = @ParserVersion,
                    ExtractedAtUtc = @ExtractedAtUtc,
                    ExtractionError = NULL,
                    Status = N'Active',
                    UploadedByUserId = @ActorUserId,
                    UploadedAtUtc = @Now,
                    UpdatedAtUtc = @Now
                WHERE TenantId = @TenantId
                  AND ApplicationDocumentId = @ApplicationDocumentId;
            END
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                JobApplicationId = jobApplicationId,
                CandidateId = candidateId,
                OriginalFileName = Truncate(Path.GetFileName(evidence.FileName.Trim()), 260),
                ContentType = Truncate(NullIfBlank(evidence.ContentType) ?? "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 160),
                SizeBytes = Math.Max(1L, evidence.SizeBytes),
                ContentHashSha256 = evidence.ContentHashSha256.Trim().ToLowerInvariant(),
                StorageKey = Truncate($"cv-parser-agent/{(evidence.AgentRunId.HasValue ? evidence.AgentRunId.Value.ToString("D") : evidence.ContentHashSha256.Trim().ToLowerInvariant())}", 512),
                ExtractedText = extractedText,
                ParserVersion = BuildCvParserVersion(evidence.Model),
                ExtractedAtUtc = (evidence.ParsedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertJobApplicationStatusHistoryAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid applicationId,
        string? fromStatus,
        string toStatus,
        Guid? actorUserId,
        string notes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.JobApplicationStatusHistory
            (
                JobApplicationStatusHistoryId,
                TenantId,
                JobApplicationId,
                FromStatus,
                ToStatus,
                ChangedByUserId,
                ChangedAtUtc,
                Notes
            )
            VALUES
            (
                NEWID(),
                @TenantId,
                @JobApplicationId,
                @FromStatus,
                @ToStatus,
                @ActorUserId,
                SYSUTCDATETIME(),
                @Notes
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                JobApplicationId = applicationId,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                ActorUserId = actorUserId,
                Notes = notes
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid> UpsertCandidateProspectAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        CandidateMutableRow candidate,
        Guid? sourceLabelId,
        string sourceLabel,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var existingId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            """
            SELECT TOP (1) CandidateProspectId
            FROM dbo.CandidateProspects
            WHERE TenantId = @TenantId
              AND Email = @Email
              AND Status <> N'Archived'
            ORDER BY CreatedAtUtc DESC;
            """,
            new { TenantId = tenantId, candidate.Email },
            transaction,
            cancellationToken: cancellationToken));
        if (existingId.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.CandidateProspects
                SET CandidateId = @CandidateId,
                    CandidateSourceLabelId = COALESCE(@CandidateSourceLabelId, CandidateSourceLabelId),
                    SourceLabel = @SourceLabel,
                    Status = N'Invited',
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE TenantId = @TenantId
                  AND CandidateProspectId = @CandidateProspectId;
                """,
                new
                {
                    TenantId = tenantId,
                    CandidateProspectId = existingId.Value,
                    candidate.CandidateId,
                    CandidateSourceLabelId = sourceLabelId,
                    SourceLabel = Truncate(sourceLabel, 80)
                },
                transaction,
                cancellationToken: cancellationToken));
            return existingId.Value;
        }

        var prospectId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.CandidateProspects
            (
                CandidateProspectId,
                TenantId,
                DisplayName,
                Email,
                Phone,
                LinkedInUrl,
                CandidateSourceLabelId,
                SourceLabel,
                Status,
                CandidateId,
                CreatedByUserId,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            SELECT
                @CandidateProspectId,
                @TenantId,
                candidate.DisplayName,
                candidate.Email,
                candidate.Phone,
                candidate.LinkedInUrl,
                @CandidateSourceLabelId,
                @SourceLabel,
                N'Invited',
                candidate.CandidateId,
                @ActorUserId,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            FROM dbo.Candidates AS candidate
            WHERE candidate.TenantId = @TenantId
              AND candidate.CandidateId = @CandidateId;
            """,
            new
            {
                CandidateProspectId = prospectId,
                TenantId = tenantId,
                candidate.CandidateId,
                CandidateSourceLabelId = sourceLabelId,
                SourceLabel = Truncate(sourceLabel, 80),
                ActorUserId = actorUserId
            },
            transaction,
            cancellationToken: cancellationToken));
        return prospectId;
    }

    private static async Task UpsertCandidateProspectJobRequestAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid prospectId,
        Guid jobRequestId,
        Guid jobPostId,
        string? notes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE dbo.CandidateProspectJobRequests AS target
            USING
            (
                SELECT @TenantId AS TenantId, @CandidateProspectId AS CandidateProspectId, @JobRequestId AS JobRequestId
            ) AS source
            ON target.TenantId = source.TenantId
               AND target.CandidateProspectId = source.CandidateProspectId
               AND target.JobRequestId = source.JobRequestId
            WHEN MATCHED THEN UPDATE SET
                JobPostId = @JobPostId,
                Status = N'Invited',
                Notes = COALESCE(@Notes, target.Notes)
            WHEN NOT MATCHED THEN
                INSERT
                (
                    TenantId,
                    CandidateProspectId,
                    JobRequestId,
                    JobPostId,
                    Status,
                    Notes,
                    CreatedAtUtc
                )
                VALUES
                (
                    @TenantId,
                    @CandidateProspectId,
                    @JobRequestId,
                    @JobPostId,
                    N'Invited',
                    @Notes,
                    SYSUTCDATETIME()
                );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                CandidateProspectId = prospectId,
                JobRequestId = jobRequestId,
                JobPostId = jobPostId,
                Notes = NullIfBlank(notes)
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<bool> QueueCandidateInvitationAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        PortalJobPostRow context,
        CandidateMutableRow candidate,
        Guid? prospectId,
        Guid actorUserId,
        string? recruiterMessage,
        CancellationToken cancellationToken)
    {
        var notificationEventId = await EnsureCandidateInvitationEventAsync(
            connection,
            transaction,
            tenantId,
            cancellationToken);
        var subject = $"{context.CompanyName} is looking for {context.Title}";
        var invitationText = string.IsNullOrWhiteSpace(recruiterMessage)
            ? $"{context.CompanyName} is looking for {context.Title}. Please apply at our job portal for this job post if you are interested."
            : recruiterMessage.Trim();
        var candidateInvitationId = Guid.NewGuid();
        var invitationToken = CreateInvitationToken();
        var jobLink = BuildCandidateInvitationLink(
            ExtractFirstAbsoluteUrl(invitationText),
            context.JobPostId,
            candidateInvitationId,
            invitationToken);
        var trackedInvitationText = BuildTrackedInvitationText(invitationText, jobLink);
        var bodyLines = new List<string>
        {
            $"Hello {candidate.DisplayName},",
            string.Empty,
            trackedInvitationText
        };

        bodyLines.Add(string.Empty);
        bodyLines.Add("Regards,");
        bodyLines.Add(context.CompanyName);
        var textBody = string.Join(Environment.NewLine, bodyLines);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.CandidateInvitations
            (
                CandidateInvitationId,
                TenantId,
                CandidateProspectId,
                CandidateId,
                JobRequestId,
                JobPostId,
                InvitedByUserId,
                TokenHash,
                Email,
                Status,
                ExpiresAtUtc,
                CreatedAtUtc
            )
            VALUES
            (
                @CandidateInvitationId,
                @TenantId,
                @CandidateProspectId,
                @CandidateId,
                @JobRequestId,
                @JobPostId,
                @ActorUserId,
                @TokenHash,
                @Email,
                N'Sent',
                DATEADD(DAY, 7, SYSUTCDATETIME()),
                SYSUTCDATETIME()
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
            VALUES
            (
                NEWID(),
                @TenantId,
                @NotificationEventId,
                NULL,
                NULL,
                @Email,
                N'Email',
                @PayloadJson,
                N'Pending',
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """,
            new
            {
                TenantId = tenantId,
                CandidateInvitationId = candidateInvitationId,
                CandidateProspectId = prospectId,
                candidate.CandidateId,
                context.JobRequestId,
                context.JobPostId,
                ActorUserId = actorUserId,
                TokenHash = HashInvitationToken(invitationToken),
                candidate.Email,
                NotificationEventId = notificationEventId,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    subject,
                    body = textBody,
                    htmlBody = BuildCandidateInvitationHtmlBody(
                        subject,
                        context.CompanyName,
                        context.Title,
                        candidate.DisplayName,
                        trackedInvitationText,
                        jobLink),
                    entityType = "JobPost",
                    entityId = context.JobPostId,
                    variables = new Dictionary<string, string>
                    {
                        ["companyName"] = context.CompanyName,
                        ["jobTitle"] = context.Title,
                        ["candidateName"] = candidate.DisplayName,
                        ["requestCode"] = context.RequestCode
                    }
                })
            },
            transaction,
            cancellationToken: cancellationToken));

        return true;
    }

    private static TrackedCandidateInvitation CreateTrackedCandidateInvitation(
        Guid candidateId,
        Guid jobPostId,
        string? baseJobLink)
    {
        var candidateInvitationId = Guid.NewGuid();
        var invitationToken = CreateInvitationToken();
        return new TrackedCandidateInvitation(
            candidateId,
            jobPostId,
            candidateInvitationId,
            HashInvitationToken(invitationToken),
            BuildCandidateInvitationLink(baseJobLink, jobPostId, candidateInvitationId, invitationToken));
    }

    private static string BuildTrackedInvitationText(string invitationText, string? trackedJobLink)
    {
        if (string.IsNullOrWhiteSpace(trackedJobLink))
        {
            return invitationText;
        }

        var existingLink = ExtractFirstAbsoluteUrl(invitationText);
        if (string.IsNullOrWhiteSpace(existingLink))
        {
            return $"{invitationText}{Environment.NewLine}{Environment.NewLine}Apply here: {trackedJobLink}";
        }

        return invitationText.Replace(existingLink, trackedJobLink, StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildCandidateInvitationLink(
        string? baseJobLink,
        Guid jobPostId,
        Guid candidateInvitationId,
        string invitationToken)
    {
        if (!Uri.TryCreate(baseJobLink, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var builder = new UriBuilder(uri);
        if (!builder.Path.Contains(jobPostId.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = $"/candidate/jobs/{jobPostId:D}";
        }

        var retainedQueryParameters = builder.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !IsTrackedInviteQueryKey(part.Split('=', 2)[0]));
        var trackedQueryParameters = retainedQueryParameters.Concat(
        [
            "source=invite",
            $"inviteId={candidateInvitationId:D}",
            $"token={Uri.EscapeDataString(invitationToken)}"
        ]);
        builder.Query = string.Join('&', trackedQueryParameters);

        return builder.Uri.ToString();
    }

    private static bool IsTrackedInviteQueryKey(string key)
    {
        var normalized = Uri.UnescapeDataString(key);
        return string.Equals(normalized, "source", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "inviteId", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "token", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateInvitationToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return token.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashInvitationToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildCandidateInvitationHtmlBody(
        string subject,
        string companyName,
        string jobTitle,
        string candidateName,
        string invitationText,
        string? jobLink)
    {
        var body = $"Hello {candidateName},\n\n{invitationText}\n\nRegards,\n{companyName}";
        return TalentPilotEmailTemplate.Build(
            "Talent Pilot Invitation",
            $"{companyName} is hiring {jobTitle}",
            body,
            [
                ("Role", jobTitle),
                ("Company", companyName)
            ],
            string.IsNullOrWhiteSpace(jobLink) ? null : "View role and apply",
            jobLink,
            subject);
    }

    private static string? ExtractFirstAbsoluteUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var part in value.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = part.Trim().TrimEnd('.', ',', ';', ')', ']', '>');
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
            {
                return uri.ToString();
            }
        }

        return null;
    }

    private static string BuildApplicationSnapshotJson(
        PortalJobPostRow context,
        DateOnly? interviewAvailabilityStartDate = null,
        DateOnly? interviewAvailabilityEndDate = null)
    {
        return JsonSerializer.Serialize(new
        {
            title = context.Title,
            requestCode = context.RequestCode,
            companyName = context.CompanyName,
            client = context.Client,
            department = context.Department,
            location = context.Location,
            experienceMinYears = context.ExperienceMinYears,
            experienceMaxYears = context.ExperienceMaxYears,
            requiredPositions = context.RequiredPositions,
            interviewAvailability = interviewAvailabilityStartDate.HasValue || interviewAvailabilityEndDate.HasValue
                ? new
                {
                    startDate = interviewAvailabilityStartDate,
                    endDate = interviewAvailabilityEndDate
                }
                : null,
            capturedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsResumeDocumentType(string? documentType)
    {
        var normalized = documentType?.Trim().ToLowerInvariant();
        return normalized is "resume" or "cv";
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
            : Truncate($"cv-parser-agent:{model.Trim()}", 64);
    }

    private static string BuildInitials(string displayName)
    {
        var initials = string.Concat(displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part[0]))
            .ToUpperInvariant();
        return string.IsNullOrWhiteSpace(initials) ? "TP" : Truncate(initials, 8);
    }

    private static async Task CreateInvitedApplicationsForRediscoveredCandidatesAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        CandidateInvitationContextRow context,
        IReadOnlyList<CandidateInvitationRecipientRow> candidates,
        string? recruiterMessage,
        CancellationToken cancellationToken)
    {
        if (!context.JobPostId.HasValue || candidates.Count == 0)
        {
            return;
        }

        var snapshotJson = JsonSerializer.Serialize(new
        {
            title = context.JobTitle,
            requestCode = context.RequestCode,
            companyName = context.CompanyName,
            source = "Talent Rediscovery",
            capturedAtUtc = DateTimeOffset.UtcNow
        });

        const string sql = """
            DECLARE @Inserted TABLE
            (
                JobApplicationId UNIQUEIDENTIFIER NOT NULL,
                CandidateId UNIQUEIDENTIFIER NOT NULL
            );

            INSERT INTO dbo.JobApplications
            (
                JobApplicationId,
                TenantId,
                JobRequestId,
                JobPostId,
                CandidateId,
                CandidateSourceLabelId,
                SourceLabel,
                CurrentStatus,
                ApplicationVersion,
                IsActive,
                IsInvited,
                ConfirmedAtUtc,
                AppliedAtUtc,
                SourceDetail,
                AddedByUserId,
                RecruiterNotes,
                ApplicationSnapshotJson,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            OUTPUT INSERTED.JobApplicationId, INSERTED.CandidateId INTO @Inserted
            SELECT
                NEWID(),
                @TenantId,
                @JobRequestId,
                @JobPostId,
                candidate.CandidateId,
                NULL,
                N'Talent Rediscovery',
                N'Invited',
                ISNULL(existing.MaxVersion, 0) + 1,
                CAST(1 AS BIT),
                CAST(1 AS BIT),
                NULL,
                SYSUTCDATETIME(),
                N'AI rediscovery invitation',
                @ActorUserId,
                @RecruiterNotes,
                @ApplicationSnapshotJson,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            FROM dbo.Candidates AS candidate
            OUTER APPLY
            (
                SELECT MAX(ApplicationVersion) AS MaxVersion
                FROM dbo.JobApplications AS existingVersion
                WHERE existingVersion.TenantId = @TenantId
                  AND existingVersion.JobRequestId = @JobRequestId
                  AND existingVersion.CandidateId = candidate.CandidateId
            ) AS existing
            WHERE candidate.TenantId = @TenantId
              AND candidate.CandidateId IN @CandidateIds
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.JobApplications AS currentApplication
                  WHERE currentApplication.TenantId = @TenantId
                    AND currentApplication.JobPostId = @JobPostId
                    AND currentApplication.CandidateId = candidate.CandidateId
                    AND currentApplication.IsActive = CAST(1 AS BIT)
                    AND currentApplication.CurrentStatus NOT IN (N'Rejected', N'Hired', N'Withdrawn')
              );

            INSERT INTO dbo.JobApplicationStatusHistory
            (
                JobApplicationStatusHistoryId,
                TenantId,
                JobApplicationId,
                FromStatus,
                ToStatus,
                ChangedByUserId,
                ChangedAtUtc,
                Notes
            )
            SELECT
                NEWID(),
                @TenantId,
                inserted.JobApplicationId,
                NULL,
                N'Invited',
                @ActorUserId,
                SYSUTCDATETIME(),
                N'Recruiter invited candidate from Talent Rediscovery.'
            FROM @Inserted AS inserted;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                context.JobRequestId,
                JobPostId = context.JobPostId.Value,
                ActorUserId = actorUserId,
                CandidateIds = candidates.Select(candidate => candidate.CandidateId).ToArray(),
                RecruiterNotes = NullIfBlank(recruiterMessage),
                ApplicationSnapshotJson = snapshotJson
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<CandidateInvitationContextRow?> ReadCandidateInvitationContextAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        Guid? jobPostId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                request.JobRequestId,
                request.RequestCode,
                post.JobPostId,
                COALESCE(post.Title, request.Title) AS JobTitle,
                tenant.DisplayName AS CompanyName
            FROM dbo.JobRequests AS request
            INNER JOIN dbo.Tenants AS tenant
                ON tenant.TenantId = request.TenantId
            LEFT JOIN dbo.JobPosts AS post
                ON post.TenantId = request.TenantId
                AND post.JobRequestId = request.JobRequestId
                AND (@JobPostId IS NULL OR post.JobPostId = @JobPostId)
            WHERE request.TenantId = @TenantId
              AND request.JobRequestId = @JobRequestId
            ORDER BY post.UpdatedAtUtc DESC;
            """;

        return await connection.QuerySingleOrDefaultAsync<CandidateInvitationContextRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId, JobPostId = jobPostId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<CandidateInvitationRecipientRow>> ReadCandidateInvitationRecipientsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        IReadOnlyList<Guid> candidateIds,
        CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT
                CandidateId,
                DisplayName,
                Email
            FROM dbo.Candidates
            WHERE TenantId = @TenantId
              AND CandidateId IN @CandidateIds
              AND Status = N'Active'
              AND NULLIF(LTRIM(RTRIM(Email)), N'') IS NOT NULL;
            """;

        return (await connection.QueryAsync<CandidateInvitationRecipientRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, CandidateIds = candidateIds },
            transaction,
            cancellationToken: cancellationToken))).ToArray();
    }

    private static async Task<Guid> EnsureCandidateInvitationEventAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await EnsureNotificationEventAsync(
            connection,
            transaction,
            tenantId,
            NotificationEventCodes.CandidateInvitedToApply,
            "Candidate invited to apply",
            "CandidateEmail",
            cancellationToken);
    }

    private static async Task<Guid> EnsureNotificationEventAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string eventCode,
        string name,
        string defaultRecipientType,
        CancellationToken cancellationToken)
    {
        var existing = await ReadNotificationEventIdAsync(
            connection,
            transaction,
            tenantId,
            eventCode,
            cancellationToken);
        if (existing.HasValue)
        {
            return existing.Value;
        }

        var notificationEventId = Guid.NewGuid();
        const string insertSql = """
            INSERT INTO dbo.NotificationEvents
            (
                NotificationEventId,
                TenantId,
                EventCode,
                Name,
                DefaultRecipientType,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @NotificationEventId,
                @TenantId,
                @EventCode,
                @Name,
                @DefaultRecipientType,
                N'Active',
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            new
            {
                NotificationEventId = notificationEventId,
                TenantId = tenantId,
                EventCode = eventCode,
                Name = name,
                DefaultRecipientType = defaultRecipientType
            },
            transaction,
            cancellationToken: cancellationToken));
        return notificationEventId;
    }

    private static async Task<Guid?> ReadNotificationEventIdAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        string eventCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) NotificationEventId
            FROM dbo.NotificationEvents
            WHERE TenantId = @TenantId
              AND EventCode = @EventCode
              AND Status = N'Active';
            """;

        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, EventCode = eventCode },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<string> ReadDepartmentNameAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) Name
            FROM dbo.Departments
            WHERE TenantId = @TenantId
              AND DepartmentId = @DepartmentId;
            """;

        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, DepartmentId = departmentId },
            transaction,
            cancellationToken: cancellationToken)) ?? "the selected department";
    }

    private static async Task<IReadOnlyList<NotificationRecipientRow>> ResolveNotificationRecipientsAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        WorkflowAssignmentTarget assignmentTarget,
        CancellationToken cancellationToken)
    {
        const string userSql = """
            SELECT UserId, Email
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId = @UserId
              AND AccountStatus = N'Active'
              AND DeletedAtUtc IS NULL;
            """;

        const string groupSql = """
            SELECT DISTINCT u.UserId, u.Email
            FROM dbo.GroupMembers AS gm
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = gm.TenantId
                AND u.UserId = gm.UserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            INNER JOIN dbo.Groups AS g
                ON g.TenantId = gm.TenantId
                AND g.GroupId = gm.GroupId
                AND g.Status = N'Active'
            WHERE gm.TenantId = @TenantId
              AND gm.GroupId = @GroupId;
            """;

        const string roleSql = """
            SELECT DISTINCT u.UserId, u.Email
            FROM dbo.UserRoles AS ur
            INNER JOIN dbo.Roles AS r
                ON r.TenantId = ur.TenantId
                AND r.RoleId = ur.RoleId
                AND r.Status = N'Active'
            INNER JOIN dbo.AppUsers AS u
                ON u.TenantId = ur.TenantId
                AND u.UserId = ur.UserId
                AND u.AccountStatus = N'Active'
                AND u.DeletedAtUtc IS NULL
            WHERE ur.TenantId = @TenantId
              AND ur.RoleId = @RoleId;
            """;

        IReadOnlyList<NotificationRecipientRow> recipients;
        if (assignmentTarget.AssignedToUserId.HasValue)
        {
            recipients = (await connection.QueryAsync<NotificationRecipientRow>(new CommandDefinition(
                userSql,
                new { TenantId = tenantId, UserId = assignmentTarget.AssignedToUserId.Value },
                transaction,
                cancellationToken: cancellationToken))).ToArray();
        }
        else if (assignmentTarget.AssignedToGroupId.HasValue)
        {
            recipients = (await connection.QueryAsync<NotificationRecipientRow>(new CommandDefinition(
                groupSql,
                new { TenantId = tenantId, GroupId = assignmentTarget.AssignedToGroupId.Value },
                transaction,
                cancellationToken: cancellationToken))).ToArray();
        }
        else if (assignmentTarget.AssignedToRoleId.HasValue)
        {
            recipients = (await connection.QueryAsync<NotificationRecipientRow>(new CommandDefinition(
                roleSql,
                new { TenantId = tenantId, RoleId = assignmentTarget.AssignedToRoleId.Value },
                transaction,
                cancellationToken: cancellationToken))).ToArray();
        }
        else
        {
            recipients = [];
        }

        if (recipients.Count > 0)
        {
            return recipients;
        }

        var fallback = await TenantAdminFallbackAsync(connection, transaction, tenantId, cancellationToken);
        if (!fallback.AssignedToRoleId.HasValue)
        {
            return [];
        }

        return (await connection.QueryAsync<NotificationRecipientRow>(new CommandDefinition(
            roleSql,
            new { TenantId = tenantId, RoleId = fallback.AssignedToRoleId.Value },
            transaction,
            cancellationToken: cancellationToken))).ToArray();
    }

    private static async Task InsertNotificationEmailOutboxAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        NotificationContent notificationContent,
        IReadOnlyList<NotificationRecipientRow> recipients,
        Guid jobRequestId,
        string jobTitle,
        string requesterName,
        string requestCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (recipients.Count == 0)
        {
            return;
        }

        const string sql = """
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
            VALUES
            (
                @NotificationOutboxId,
                @TenantId,
                @NotificationEventId,
                @NotificationTemplateId,
                @RecipientUserId,
                @RecipientEmail,
                N'Email',
                @PayloadJson,
                N'Pending',
                @Now,
                @Now,
                @Now
            );
            """;

        var variables = new Dictionary<string, string>
        {
            ["jobTitle"] = jobTitle,
            ["requesterName"] = requesterName,
            ["requestCode"] = requestCode
        };

        var rows = recipients.Select(recipient => new
        {
            NotificationOutboxId = Guid.NewGuid(),
            TenantId = tenantId,
            NotificationEventId = notificationContent.EventId,
            NotificationTemplateId = notificationContent.TemplateId,
            RecipientUserId = recipient.UserId,
            RecipientEmail = recipient.Email,
            PayloadJson = JsonSerializer.Serialize(new
            {
                subject = notificationContent.Title,
                body = notificationContent.Message,
                htmlBody = TalentPilotEmailTemplate.Build(
                    "Talent Pilot Notification",
                    notificationContent.Title,
                    notificationContent.Message,
                    [
                        ("Request", $"{jobTitle} ({requestCode})"),
                        ("Requester", requesterName)
                    ]),
                entityType = "JobRequest",
                entityId = jobRequestId,
                variables
            }),
            Now = now.UtcDateTime
        }).ToArray();

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            rows,
            transaction,
            cancellationToken: cancellationToken));
    }

    private static string RenderNotificationTemplate(string template, IReadOnlyDictionary<string, string> values)
    {
        var rendered = template;
        foreach (var (key, value) in values)
        {
            rendered = rendered.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return rendered;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static BenchMatchPayload ToBenchMatchPayload(OperationsBenchMatch match)
    {
        return new BenchMatchPayload(
            match.Rank,
            match.Score,
            match.Confidence,
            match.Explanation,
            match.Strengths,
            match.Gaps,
            match.ProjectEvidence,
            match.WebResearchStatus,
            match.WebSummary,
            match.WebSources);
    }

    private static TalentRediscoveryPayload ToTalentRediscoveryPayload(OperationsTalentRediscoveryMatch match)
    {
        return new TalentRediscoveryPayload(
            match.CandidateName,
            match.CandidateEmail,
            match.CurrentDesignation,
            match.ExperienceYears,
            match.NoticePeriodDays,
            match.Rank,
            match.Score,
            match.Confidence,
            match.Explanation,
            match.Strengths,
            match.Gaps,
            match.ApplicationEvidence,
            match.InterviewEvidence);
    }

    private static ApplicantRankingPayload ToApplicantRankingPayload(OperationsApplicantRankingMatch match)
    {
        return new ApplicantRankingPayload(
            match.CandidateId,
            match.CandidateName,
            match.CandidateEmail,
            match.CurrentDesignation,
            match.ExperienceYears,
            match.NoticePeriodDays,
            match.Rank,
            match.Score,
            match.Confidence,
            match.Explanation,
            match.Strengths,
            match.Gaps,
            match.MatchedSkills,
            match.MissingSkills,
            match.DocumentEvidence,
            match.HistoricalOutcomeEvidence,
            match.SemanticSimilarityStatus);
    }

    private static string BuildStoredWebSummary(
        string webResearchStatus,
        IReadOnlyList<OperationsBenchMatchWebSource> webSources)
    {
        if (webResearchStatus == "Skipped:LiveContextNotRequired")
        {
            return "Web search was skipped because this request did not ask for recent or live public context. Ranking used tenant data only.";
        }

        if (webSources.Count == 0)
        {
            return $"No public web summary is available. Web research status: {webResearchStatus}.";
        }

        var snippets = webSources
            .Select(source => source.Snippet)
            .Where(snippet => !string.IsNullOrWhiteSpace(snippet))
            .Take(3)
            .ToArray();

        if (snippets.Length == 0)
        {
            return "Public web context was found, but no readable summary text was returned.";
        }

        return $"Public web context summary: {string.Join(" ", snippets)}";
    }

    private static BenchMatchPayload? DeserializeBenchMatchPayload(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<BenchMatchPayload>(payloadJson);
        }
        catch
        {
            return null;
        }
    }

    private static TalentRediscoveryPayload? DeserializeTalentRediscoveryPayload(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<TalentRediscoveryPayload>(payloadJson);
        }
        catch
        {
            return null;
        }
    }

    private static ApplicantRankingPayload? DeserializeApplicantRankingPayload(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<ApplicantRankingPayload>(payloadJson);
        }
        catch
        {
            return null;
        }
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

    private static async Task<bool> ApplicationInterviewRoundsResolvedAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobPostId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.JobPostInterviewRounds AS round
            WHERE round.TenantId = @TenantId
              AND round.JobPostId = @JobPostId
              AND round.Status = N'Active'
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.Interviews AS interview
                  WHERE interview.TenantId = round.TenantId
                    AND interview.JobApplicationId = @JobApplicationId
                    AND interview.JobPostInterviewRoundId = round.JobPostInterviewRoundId
                    AND interview.Status IN (N'Completed', N'Skipped')
              );
            """;

        var unresolved = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobPostId = jobPostId, JobApplicationId = jobApplicationId },
            transaction,
            cancellationToken: cancellationToken));
        return unresolved == 0;
    }

    private static async Task<HiringReviewNotificationContextRow?> ReadHiringReviewNotificationContextAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                request.RequestCode,
                COALESCE(post.Title, request.Title) AS JobTitle,
                candidate.DisplayName AS CandidateName,
                request.HiringManagerUserId
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            LEFT JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            WHERE application.TenantId = @TenantId
              AND application.JobApplicationId = @JobApplicationId;
            """;

        return await connection.QuerySingleOrDefaultAsync<HiringReviewNotificationContextRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobApplicationId = jobApplicationId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<HiringReviewAccessRow?> ReadHiringReviewAccessContextAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                application.JobApplicationId,
                application.JobRequestId,
                application.JobPostId,
                application.CandidateId,
                application.CurrentStatus AS ApplicationStatus,
                application.FinalDecisionAtUtc AS FinalOutcomeRecordedAt,
                application.FinalDecisionReason AS FinalOutcomeReason,
                application.SourceLabel,
                application.SourceDetail,
                application.RecruiterNotes,
                request.RequestCode,
                request.Title AS RequestTitle,
                request.Description AS RequestDescription,
                COALESCE(post.Title, request.Title) AS JobTitle,
                post.Description AS JobPostDescription,
                COALESCE(request.ClientName, N'') AS Client,
                department.Name AS Department,
                location.Name AS Location,
                COALESCE(post.ExperienceMinYears, request.ExperienceMinYears) AS ExperienceMinYears,
                COALESCE(post.ExperienceMaxYears, request.ExperienceMaxYears) AS ExperienceMaxYears,
                request.RequiredPositions,
                request.FulfilledPositions,
                request.Status AS RequestStatus,
                request.ClosedAtUtc AS RequestClosedAt,
                closeAudit.EventSummary AS RequestCloseReason,
                request.HiringManagerUserId,
                hiringManager.DisplayName AS HiringManagerName,
                candidate.DisplayName AS CandidateName,
                candidate.Email AS CandidateEmail,
                candidate.Status AS CandidateStatus,
                candidate.CurrentDesignation,
                candidate.CurrentCompany,
                candidate.ExperienceYears,
                candidate.ExpectedSalaryAmount,
                candidate.ExpectedSalaryCurrency,
                candidate.NoticePeriodDays,
                (
                    SELECT STRING_AGG(skill.Name, N', ')
                    FROM dbo.CandidateSkills AS candidateSkill
                    INNER JOIN dbo.Skills AS skill
                        ON skill.SkillId = candidateSkill.SkillId
                    WHERE candidateSkill.TenantId = candidate.TenantId
                      AND candidateSkill.CandidateId = candidate.CandidateId
                ) AS CandidateSkillList,
                (
                    SELECT STRING_AGG(skill.Name, N', ')
                    FROM dbo.JobRequestSkills AS requestSkill
                    INNER JOIN dbo.Skills AS skill
                        ON skill.SkillId = requestSkill.SkillId
                    WHERE requestSkill.TenantId = request.TenantId
                      AND requestSkill.JobRequestId = request.JobRequestId
                ) AS RequestSkillList,
                (
                    SELECT STRING_AGG(skill.Name, N', ')
                    FROM dbo.JobPostSkills AS postSkill
                    INNER JOIN dbo.Skills AS skill
                        ON skill.SkillId = postSkill.SkillId
                    WHERE postSkill.TenantId = post.TenantId
                      AND postSkill.JobPostId = post.JobPostId
                ) AS JobPostSkillList,
                tenant.DisplayName AS CompanyName,
                settings.CompanyAddress,
                settings.CompanyCity,
                settings.CompanyCountry,
                settings.OfficialEmail,
                settings.OfficialPhone
            FROM dbo.JobApplications AS application
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = application.TenantId
                AND request.JobRequestId = application.JobRequestId
            LEFT JOIN dbo.JobPosts AS post
                ON post.TenantId = application.TenantId
                AND post.JobPostId = application.JobPostId
            INNER JOIN dbo.Departments AS department
                ON department.TenantId = request.TenantId
                AND department.DepartmentId = request.DepartmentId
            INNER JOIN dbo.Locations AS location
                ON location.TenantId = request.TenantId
                AND location.LocationId = request.LocationId
            INNER JOIN dbo.AppUsers AS hiringManager
                ON hiringManager.TenantId = request.TenantId
                AND hiringManager.UserId = request.HiringManagerUserId
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = application.TenantId
                AND candidate.CandidateId = application.CandidateId
            INNER JOIN dbo.Tenants AS tenant
                ON tenant.TenantId = application.TenantId
            LEFT JOIN dbo.TenantRecruitmentSettings AS settings
                ON settings.TenantId = application.TenantId
            OUTER APPLY (
                SELECT TOP (1) audit.EventSummary
                FROM dbo.AuditLogs AS audit
                WHERE audit.TenantId = request.TenantId
                  AND audit.EntityType = N'JobRequest'
                  AND audit.EntityId = request.JobRequestId
                  AND audit.EventType = N'job_request.closed_by_hiring_manager'
                ORDER BY audit.CreatedAtUtc DESC
            ) AS closeAudit
            WHERE application.TenantId = @TenantId
              AND application.JobApplicationId = @JobApplicationId
              AND (@IncludeAllTenantReviews = CAST(1 AS BIT) OR request.HiringManagerUserId = @ActorUserId);
            """;

        return await connection.QuerySingleOrDefaultAsync<HiringReviewAccessRow>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                IncludeAllTenantReviews = includeAllTenantReviews,
                JobApplicationId = jobApplicationId
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<HiringManagerDashboardActivityItem>> ReadHiringManagerDashboardActivityAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH ScopedApplications AS
            (
                SELECT
                    application.JobApplicationId,
                    application.JobRequestId,
                    request.RequestCode,
                    candidate.DisplayName AS CandidateName
                FROM dbo.JobApplications AS application
                INNER JOIN dbo.JobRequests AS request
                    ON request.TenantId = application.TenantId
                    AND request.JobRequestId = application.JobRequestId
                INNER JOIN dbo.Candidates AS candidate
                    ON candidate.TenantId = application.TenantId
                    AND candidate.CandidateId = application.CandidateId
                WHERE application.TenantId = @TenantId
                  AND application.CurrentStatus IN (N'HiringManagerReview', N'Offered', N'OnHold', N'Rejected', N'Hired', N'Joined')
                  AND (@IncludeAllTenantReviews = CAST(1 AS BIT) OR request.HiringManagerUserId = @ActorUserId)
            )
            SELECT TOP (8)
                audit.AuditLogId AS Id,
                COALESCE(requestScope.JobApplicationId, applicationScope.JobApplicationId, offerScope.JobApplicationId) AS JobApplicationId,
                COALESCE(requestScope.JobRequestId, applicationScope.JobRequestId, offerScope.JobRequestId) AS JobRequestId,
                COALESCE(requestScope.RequestCode, applicationScope.RequestCode, offerScope.RequestCode) AS RequestCode,
                COALESCE(requestScope.CandidateName, applicationScope.CandidateName, offerScope.CandidateName) AS CandidateName,
                audit.ActorDisplayName AS ActorName,
                audit.EventType AS Title,
                audit.EventSummary AS Detail,
                audit.OccurredAtUtc AS CreatedAt
            FROM dbo.AuditLogs AS audit
            LEFT JOIN ScopedApplications AS requestScope
                ON audit.EntityType = N'JobRequest'
                AND audit.EntityId = requestScope.JobRequestId
            LEFT JOIN ScopedApplications AS applicationScope
                ON audit.EntityType = N'JobApplication'
                AND audit.EntityId = applicationScope.JobApplicationId
            LEFT JOIN dbo.OfferLetters AS offer
                ON audit.EntityType = N'OfferLetter'
                AND offer.TenantId = audit.TenantId
                AND offer.OfferLetterId = audit.EntityId
            LEFT JOIN ScopedApplications AS offerScope
                ON offerScope.JobApplicationId = offer.JobApplicationId
            WHERE audit.TenantId = @TenantId
              AND COALESCE(requestScope.JobApplicationId, applicationScope.JobApplicationId, offerScope.JobApplicationId) IS NOT NULL
            ORDER BY audit.OccurredAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<HiringManagerDashboardActivityRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActorUserId = actorUserId, IncludeAllTenantReviews = includeAllTenantReviews },
            cancellationToken: cancellationToken));

        return rows
            .Select(row => new HiringManagerDashboardActivityItem(
                row.Id,
                row.JobApplicationId,
                row.JobRequestId,
                row.RequestCode,
                row.CandidateName,
                row.ActorName,
                row.Title,
                row.Detail,
                Utc(row.CreatedAt)))
            .ToArray();
    }

    private static IReadOnlyList<HiringManagerDashboardAgingBucket> BuildHiringManagerDashboardAgingBuckets(
        IReadOnlyList<HiringManagerDashboardReviewRow> rows)
    {
        return
        [
            new HiringManagerDashboardAgingBucket("0-1 days", rows.Count(row => Math.Max(0, row.DaysWaiting) <= 1)),
            new HiringManagerDashboardAgingBucket("2-3 days", rows.Count(row => Math.Max(0, row.DaysWaiting) is >= 2 and <= 3)),
            new HiringManagerDashboardAgingBucket("4-7 days", rows.Count(row => Math.Max(0, row.DaysWaiting) is >= 4 and <= 7)),
            new HiringManagerDashboardAgingBucket("8+ days", rows.Count(row => Math.Max(0, row.DaysWaiting) >= 8))
        ];
    }

    private static bool IsActiveHiringManagerDashboardStatus(string status)
    {
        return IsDashboardStatus(status, "HiringManagerReview") ||
            IsDashboardStatus(status, "Offered") ||
            IsDashboardStatus(status, "Hired") ||
            IsDashboardStatus(status, "OnHold");
    }

    private static bool IsDashboardStatus(string? status, string expected)
    {
        return string.Equals(status?.Replace(" ", string.Empty), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HiringReviewDetail?> ReadHiringReviewDetailAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        var access = await ReadHiringReviewAccessContextAsync(
            connection,
            transaction,
            tenantId,
            Guid.Empty,
            includeAllTenantReviews: true,
            jobApplicationId,
            cancellationToken);
        if (access is null)
        {
            return null;
        }

        var interviews = await ReadHiringReviewInterviewsAsync(connection, transaction, tenantId, jobApplicationId, cancellationToken);
        var offerLetter = await ReadLatestOfferLetterAsync(connection, transaction, tenantId, jobApplicationId, cancellationToken);
        var meetings = offerLetter is null
            ? []
            : await ReadOfferPresentationMeetingsAsync(connection, transaction, tenantId, jobApplicationId, cancellationToken);
        var decisionBriefInsight = BuildHiringManagerDecisionBrief(access, interviews);

        return new HiringReviewDetail(
            new HiringReviewCandidateSummary(
                access.CandidateId,
                access.CandidateName,
                access.CandidateEmail,
                access.CandidateStatus,
                access.CurrentDesignation,
                access.CurrentCompany,
                access.ExperienceYears,
                access.ExpectedSalaryAmount,
                access.ExpectedSalaryCurrency,
                access.NoticePeriodDays),
            new HiringReviewJobSummary(
                access.JobRequestId,
                access.JobPostId,
                access.RequestCode,
                access.JobTitle,
                access.Client,
                access.Department,
                access.Location,
                access.ExperienceMinYears,
                access.ExperienceMaxYears,
                access.RequiredPositions,
                access.FulfilledPositions,
                access.RequestStatus,
                ToUtc(access.RequestClosedAt),
                access.RequestCloseReason,
                access.ApplicationStatus,
                ToUtc(access.FinalOutcomeRecordedAt),
                access.FinalOutcomeReason,
                access.SourceLabel,
                access.SourceDetail,
                access.RecruiterNotes,
                access.RequestDescription,
                access.JobPostDescription),
            interviews,
            decisionBriefInsight.Summary,
            decisionBriefInsight,
            offerLetter,
            meetings);
    }

    private static HiringReviewDecisionBriefInsight BuildHiringManagerDecisionBrief(
        HiringReviewAccessRow access,
        IReadOnlyList<HiringReviewInterviewDetail> interviews)
    {
        var totalRounds = interviews.Count;
        var completed = interviews.Count(interview => IsNormalized(interview.Status, "completed"));
        var skipped = interviews.Count(interview => IsNormalized(interview.Status, "skipped"));
        var proceed = interviews.Count(interview => IsPositiveRecommendation(interview.Recommendation));
        var scored = interviews
            .Where(interview => interview.AverageScore.HasValue)
            .Select(interview => interview.AverageScore!.Value)
            .ToArray();
        var averageScore = scored.Length == 0 ? (decimal?)null : scored.Average();
        var interviewClearance = totalRounds == 0 ? 0 : Math.Round((decimal)completed / totalRounds * 100m, 1);
        var positiveRatio = completed == 0 ? 0 : Math.Round((decimal)proceed / completed * 100m, 1);
        var sentiment = BuildCollectiveInterviewSentiment(proceed, completed, averageScore);
        var requirementFit = BuildRequirementFit(access, averageScore, positiveRatio);
        var salaryExpectation = FormatSalary(access.ExpectedSalaryAmount, access.ExpectedSalaryCurrency);
        var experienceSummary = BuildExperienceSummary(access);
        var alignmentSummary = BuildAlignmentSummary(access);
        var scoreText = averageScore.HasValue ? $"{averageScore.Value:0.0}/5" : "No score";

        var summary = $"{access.CandidateName} is ready for final hiring-manager review for {access.JobTitle}. " +
            $"{experienceSummary} Interviewers recorded {proceed}/{completed} positive recommendation(s) with {scoreText} average scoring. " +
            $"{alignmentSummary} Salary expectation: {salaryExpectation}.";

        var metrics = new List<HiringReviewDecisionMetric>
        {
            new(
                "interviewsCleared",
                "Interviews cleared",
                $"{completed}/{Math.Max(totalRounds, completed)}",
                interviewClearance,
                "%",
                completed == totalRounds && totalRounds > 0 ? "success" : "warning",
                "task_alt",
                $"{completed} completed, {skipped} skipped"),
            new(
                "collectiveSentiment",
                "Collective sentiment",
                sentiment,
                positiveRatio,
                "%",
                positiveRatio >= 70 ? "success" : positiveRatio >= 40 ? "warning" : "danger",
                "thumb_up",
                $"{proceed}/{Math.Max(completed, 1)} positive recommendation(s)"),
            new(
                "averageScore",
                "Average score",
                averageScore.HasValue ? $"{averageScore.Value:0.0}/5" : "No score",
                averageScore.HasValue ? Math.Round(averageScore.Value / 5m * 100m, 1) : null,
                "%",
                averageScore.GetValueOrDefault() >= 4m ? "success" : averageScore.GetValueOrDefault() >= 3m ? "warning" : "neutral",
                "speed",
                scored.Length == 0 ? "No submitted interview score" : "Average across submitted interview scorecards"),
            new(
                "requirementFit",
                "Requirement fit",
                requirementFit.Label,
                requirementFit.Score,
                "%",
                requirementFit.Score >= 70 ? "success" : requirementFit.Score >= 40 ? "warning" : "neutral",
                "fact_check",
                requirementFit.Detail),
            new(
                "experienceMatch",
                "Experience match",
                FormatExperience(access.ExperienceYears),
                BuildExperienceMatchScore(access),
                "%",
                BuildExperienceMatchScore(access) >= 70 ? "success" : "neutral",
                "work_history",
                $"{NullIfWhiteSpace(access.CurrentDesignation) ?? "Designation not recorded"} at {NullIfWhiteSpace(access.CurrentCompany) ?? "company not recorded"}"),
            new(
                "salaryExpectation",
                "Salary expectation",
                salaryExpectation,
                null,
                null,
                access.ExpectedSalaryAmount.HasValue ? "neutral" : "warning",
                "payments",
                access.ExpectedSalaryAmount.HasValue ? "Candidate-entered salary expectation" : "Salary expectation has not been captured")
        };

        var context = new List<HiringReviewDecisionContextItem>
        {
            new("applicationStatus", "Application status", FormatStatus(access.ApplicationStatus), "approval_delegation", "info"),
            new("source", "Source", NullIfWhiteSpace(access.SourceLabel) ?? "Not recorded", "travel_explore", "info"),
            new("recruiterNotes", "Recruiter notes", NullIfWhiteSpace(access.RecruiterNotes) ?? "No notes recorded", "edit_note", string.IsNullOrWhiteSpace(access.RecruiterNotes) ? "warning" : "info"),
            new("decisionControl", "Decision control", "Human review required", "verified_user", "info")
        };

        var signals = new List<string>
        {
            $"Past experience: {experienceSummary.Trim()}",
            $"Job alignment: {alignmentSummary}",
            $"Interview feedback: {sentiment} based on {completed} completed round(s).",
            $"Salary expectation: {salaryExpectation}."
        };

        return new HiringReviewDecisionBriefInsight(
            "hiring-manager-decision-brief",
            "Hiring Manager Decision Brief",
            summary,
            metrics,
            context,
            signals);
    }

    private static string BuildCollectiveInterviewSentiment(int positiveRecommendations, int completedRounds, decimal? averageScore)
    {
        if (completedRounds == 0)
        {
            return "No completed interview signal";
        }

        var positiveRatio = (decimal)positiveRecommendations / completedRounds;
        var score = averageScore.GetValueOrDefault();
        if (positiveRatio >= 0.75m && score >= 4m)
        {
            return "Strong positive";
        }

        if (positiveRatio >= 0.5m || score >= 3.5m)
        {
            return "Positive";
        }

        if (positiveRatio > 0m || score >= 3m)
        {
            return "Mixed";
        }

        return "Needs review";
    }

    private static RequirementFitResult BuildRequirementFit(
        HiringReviewAccessRow access,
        decimal? averageScore,
        decimal positiveRatio)
    {
        var candidateSkills = SplitSkillList(access.CandidateSkillList);
        var requiredSkills = SplitSkillList($"{access.RequestSkillList}, {access.JobPostSkillList}");
        var matchedSkills = requiredSkills
            .Where(skill => candidateSkills.Any(candidateSkill => SkillMatches(candidateSkill, skill)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var interviewScore = averageScore.HasValue ? averageScore.Value / 5m * 100m : 0m;
        decimal fitScore;
        string detail;

        if (requiredSkills.Count > 0)
        {
            var skillScore = Math.Round((decimal)matchedSkills.Length / requiredSkills.Count * 100m, 1);
            fitScore = Math.Round((skillScore * 0.55m) + (positiveRatio * 0.30m) + (interviewScore * 0.15m), 1);
            detail = matchedSkills.Length == 0
                ? $"No required skills were explicitly found in the candidate profile. Checked {requiredSkills.Count} required skill(s)."
                : $"{matchedSkills.Length}/{requiredSkills.Count} required skill(s) appear in the candidate profile: {string.Join(", ", matchedSkills.Take(4))}.";
        }
        else
        {
            var keywordScore = KeywordOverlapScore(
                $"{access.CurrentDesignation} {access.CurrentCompany} {access.CandidateSkillList}",
                $"{access.JobTitle} {access.RequestDescription} {access.JobPostDescription}");
            fitScore = Math.Round((keywordScore * 0.50m) + (positiveRatio * 0.30m) + (interviewScore * 0.20m), 1);
            detail = keywordScore > 0
                ? "Fit is inferred from overlap between candidate profile text and job/request descriptions."
                : "No skill taxonomy overlap was available; fit is weighted toward interviews and submitted scores.";
        }

        return new RequirementFitResult(
            fitScore >= 70m ? "Strong fit" : fitScore >= 45m ? "Moderate fit" : "Needs validation",
            Math.Min(100m, fitScore),
            detail);
    }

    private static string BuildExperienceSummary(HiringReviewAccessRow access)
    {
        var role = NullIfWhiteSpace(access.CurrentDesignation) ?? "role not recorded";
        var company = NullIfWhiteSpace(access.CurrentCompany) ?? "company not recorded";
        var experience = FormatExperience(access.ExperienceYears);
        var match = BuildExperienceMatchScore(access);
        var matchText = match >= 70m ? "matches the requested experience band" : "needs experience-band validation";
        return $"Past experience shows {experience} as {role} at {company}; this {matchText}.";
    }

    private static string BuildAlignmentSummary(HiringReviewAccessRow access)
    {
        var requiredSkills = SplitSkillList($"{access.RequestSkillList}, {access.JobPostSkillList}");
        var candidateSkills = SplitSkillList(access.CandidateSkillList);
        var matchedSkills = requiredSkills
            .Where(skill => candidateSkills.Any(candidateSkill => SkillMatches(candidateSkill, skill)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        if (matchedSkills.Length > 0)
        {
            return $"Profile-to-role alignment is supported by {string.Join(", ", matchedSkills)} against the job/request description.";
        }

        var overlapScore = KeywordOverlapScore(
            $"{access.CurrentDesignation} {access.CurrentCompany} {access.CandidateSkillList}",
            $"{access.JobTitle} {access.RequestDescription} {access.JobPostDescription}");
        return overlapScore > 0
            ? "Profile-to-role alignment is inferred from candidate profile and job/request description overlap."
            : "Profile-to-role alignment should be manually validated against the job/request description.";
    }

    private static decimal BuildExperienceMatchScore(HiringReviewAccessRow access)
    {
        if (!access.ExperienceYears.HasValue)
        {
            return 0m;
        }

        if (!access.ExperienceMinYears.HasValue && !access.ExperienceMaxYears.HasValue)
        {
            return 70m;
        }

        var experience = access.ExperienceYears.Value;
        if (access.ExperienceMinYears.HasValue && experience < access.ExperienceMinYears.Value)
        {
            var gap = access.ExperienceMinYears.Value - experience;
            return Math.Max(0m, 70m - (gap * 20m));
        }

        if (access.ExperienceMaxYears.HasValue && experience > access.ExperienceMaxYears.Value)
        {
            return 85m;
        }

        return 100m;
    }

    private static decimal KeywordOverlapScore(string candidateText, string jobText)
    {
        var candidateTerms = TokenizeDecisionBriefText(candidateText);
        var jobTerms = TokenizeDecisionBriefText(jobText);
        if (candidateTerms.Count == 0 || jobTerms.Count == 0)
        {
            return 0m;
        }

        var overlap = jobTerms.Count(term => candidateTerms.Contains(term));
        var denominator = Math.Min(jobTerms.Count, 12);
        return Math.Min(100m, Math.Round((decimal)overlap / denominator * 100m, 1));
    }

    private static HashSet<string> TokenizeDecisionBriefText(string value)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "and", "the", "for", "with", "from", "role", "job", "senior", "developer", "engineer",
            "required", "requirements", "experience", "candidate", "client", "team", "work"
        };

        return value
            .Split([' ', ',', '.', ';', ':', '/', '\\', '-', '_', '(', ')', '[', ']', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length >= 3 && !stopWords.Contains(term))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> SplitSkillList(string? value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool SkillMatches(string candidateSkill, string requiredSkill)
    {
        var assessment = TechnologySkillMatcher.Assess([requiredSkill], [candidateSkill]);
        return assessment.Items.Any(item =>
            item.MatchLevel is SkillMatchLevel.Exact or SkillMatchLevel.StrongAdjacent);
    }

    private static bool IsPositiveRecommendation(string? recommendation)
    {
        var normalized = NormalizeDecisionBriefText(recommendation);
        return normalized is "proceed" or "hire" or "recommended" or "recommend" or "positive" or "yes" or "stronghire";
    }

    private static bool IsNormalized(string? value, string expected)
    {
        return string.Equals(NormalizeDecisionBriefText(value), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDecisionBriefText(string? value)
    {
        return (value ?? string.Empty).Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string FormatExperience(decimal? value)
    {
        return value.HasValue ? $"{value.Value:0.#} years" : "experience not recorded";
    }

    private static string FormatSalary(decimal? amount, string? currency)
    {
        if (!amount.HasValue)
        {
            return "Not recorded";
        }

        return string.IsNullOrWhiteSpace(currency)
            ? amount.Value.ToString("N0", CultureInfo.InvariantCulture)
            : $"{currency.Trim().ToUpperInvariant()} {amount.Value:N0}";
    }

    private static string FormatStatus(string? value)
    {
        var status = NullIfWhiteSpace(value);
        if (status is null)
        {
            return "Not recorded";
        }

        var builder = new StringBuilder(status.Length + 4);
        for (var index = 0; index < status.Length; index++)
        {
            var character = status[index];
            if (index > 0 && char.IsUpper(character) && char.IsLower(status[index - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static async Task<IReadOnlyList<HiringReviewInterviewDetail>> ReadHiringReviewInterviewsAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                interview.InterviewId,
                interview.JobPostInterviewRoundId,
                COALESCE(postRound.Name, requestRound.Name, N'Interview') AS RoundName,
                interview.Status,
                interviewer.DisplayName AS InterviewerName,
                interview.StartsAtUtc AS StartsAt,
                interview.DurationMinutes,
                feedback.Recommendation,
                feedback.TechnicalScore,
                feedback.CommunicationScore,
                feedback.CultureScore,
                CAST(
                    CASE
                        WHEN feedback.TechnicalScore IS NULL OR feedback.CommunicationScore IS NULL OR feedback.CultureScore IS NULL THEN NULL
                        ELSE (feedback.TechnicalScore + feedback.CommunicationScore + feedback.CultureScore) / 3.0
                    END AS DECIMAL(4,2)
                ) AS AverageScore,
                feedback.FeedbackText,
                interview.SkipReason,
                feedback.SubmittedAtUtc AS SubmittedAt
            FROM dbo.Interviews AS interview
            INNER JOIN dbo.AppUsers AS interviewer
                ON interviewer.TenantId = interview.TenantId
                AND interviewer.UserId = interview.InterviewerUserId
            LEFT JOIN dbo.JobPostInterviewRounds AS postRound
                ON postRound.TenantId = interview.TenantId
                AND postRound.JobPostInterviewRoundId = interview.JobPostInterviewRoundId
            LEFT JOIN dbo.JobRequestInterviewRounds AS requestRound
                ON requestRound.TenantId = interview.TenantId
                AND requestRound.JobRequestInterviewRoundId = interview.JobRequestInterviewRoundId
            LEFT JOIN dbo.InterviewFeedback AS feedback
                ON feedback.TenantId = interview.TenantId
                AND feedback.InterviewId = interview.InterviewId
                AND feedback.IsSubmitted = CAST(1 AS BIT)
            WHERE interview.TenantId = @TenantId
              AND interview.JobApplicationId = @JobApplicationId
            ORDER BY interview.StartsAtUtc ASC;
            """;

        var rows = await connection.QueryAsync<HiringReviewInterviewRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobApplicationId = jobApplicationId },
            transaction,
            cancellationToken: cancellationToken));

        return rows
            .Select(row => new HiringReviewInterviewDetail(
                row.InterviewId,
                row.JobPostInterviewRoundId,
                row.RoundName,
                row.Status,
                row.InterviewerName,
                Utc(row.StartsAt),
                row.DurationMinutes,
                row.Recommendation,
                row.TechnicalScore,
                row.CommunicationScore,
                row.CultureScore,
                row.AverageScore,
                row.FeedbackText,
                row.SkipReason,
                ToUtc(row.SubmittedAt)))
            .ToArray();
    }

    private static async Task<OfferLetterDetails?> ReadLatestOfferLetterAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                offer.OfferLetterId,
                offer.JobApplicationId,
                offer.JobRequestId,
                offer.JobPostId,
                offer.CandidateId,
                offer.GeneratedByUserId,
                generatedBy.DisplayName AS GeneratedByName,
                offer.Version,
                offer.Status,
                offer.CompensationText,
                offer.StartDate,
                offer.ReportingManager,
                offer.WorkLocation,
                offer.Body,
                offer.CreatedAtUtc AS CreatedAt,
                offer.UpdatedAtUtc AS UpdatedAt
            FROM dbo.OfferLetters AS offer
            INNER JOIN dbo.AppUsers AS generatedBy
                ON generatedBy.TenantId = offer.TenantId
                AND generatedBy.UserId = offer.GeneratedByUserId
            WHERE offer.TenantId = @TenantId
              AND offer.JobApplicationId = @JobApplicationId
            ORDER BY offer.Version DESC, offer.UpdatedAtUtc DESC;
            """;

        var row = await connection.QuerySingleOrDefaultAsync<OfferLetterRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobApplicationId = jobApplicationId },
            transaction,
            cancellationToken: cancellationToken));
        return row is null ? null : ToOfferLetter(row);
    }

    private static async Task<OfferLetterDetails?> ReadOfferLetterByIdAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid offerLetterId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                offer.OfferLetterId,
                offer.JobApplicationId,
                offer.JobRequestId,
                offer.JobPostId,
                offer.CandidateId,
                offer.GeneratedByUserId,
                generatedBy.DisplayName AS GeneratedByName,
                offer.Version,
                offer.Status,
                offer.CompensationText,
                offer.StartDate,
                offer.ReportingManager,
                offer.WorkLocation,
                offer.Body,
                offer.CreatedAtUtc AS CreatedAt,
                offer.UpdatedAtUtc AS UpdatedAt
            FROM dbo.OfferLetters AS offer
            INNER JOIN dbo.AppUsers AS generatedBy
                ON generatedBy.TenantId = offer.TenantId
                AND generatedBy.UserId = offer.GeneratedByUserId
            WHERE offer.TenantId = @TenantId
              AND offer.OfferLetterId = @OfferLetterId;
            """;

        var row = await connection.QuerySingleOrDefaultAsync<OfferLetterRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, OfferLetterId = offerLetterId },
            cancellationToken: cancellationToken));
        return row is null ? null : ToOfferLetter(row);
    }

    private static OfferLetterDetails ToOfferLetter(OfferLetterRow row)
    {
        return new OfferLetterDetails(
            row.OfferLetterId,
            row.JobApplicationId,
            row.JobRequestId,
            row.JobPostId,
            row.CandidateId,
            row.GeneratedByUserId,
            row.GeneratedByName,
            row.Version,
            row.Status,
            row.CompensationText,
            row.StartDate.HasValue ? DateOnly.FromDateTime(row.StartDate.Value) : null,
            row.ReportingManager,
            row.WorkLocation,
            row.Body,
            Utc(row.CreatedAt),
            Utc(row.UpdatedAt));
    }

    private static async Task<IReadOnlyList<OfferPresentationMeetingDetails>> ReadOfferPresentationMeetingsAsync(
        SqlConnection connection,
        IDbTransaction? transaction,
        Guid tenantId,
        Guid jobApplicationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                OfferPresentationMeetingId,
                OfferLetterId,
                JobApplicationId,
                MeetingAtUtc AS MeetingAt,
                LocationText,
                Notes,
                Status,
                CreatedAtUtc AS CreatedAt
            FROM dbo.OfferPresentationMeetings
            WHERE TenantId = @TenantId
              AND JobApplicationId = @JobApplicationId
            ORDER BY MeetingAtUtc DESC;
            """;

        var rows = await connection.QueryAsync<OfferPresentationMeetingRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobApplicationId = jobApplicationId },
            transaction,
            cancellationToken: cancellationToken));
        return rows.Select(ToOfferPresentationMeeting).ToArray();
    }

    private static async Task<OfferPresentationMeetingDetails?> ReadOfferPresentationMeetingByIdAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid meetingId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                OfferPresentationMeetingId,
                OfferLetterId,
                JobApplicationId,
                MeetingAtUtc AS MeetingAt,
                LocationText,
                Notes,
                Status,
                CreatedAtUtc AS CreatedAt
            FROM dbo.OfferPresentationMeetings
            WHERE TenantId = @TenantId
              AND OfferPresentationMeetingId = @MeetingId;
            """;

        var row = await connection.QuerySingleOrDefaultAsync<OfferPresentationMeetingRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, MeetingId = meetingId },
            cancellationToken: cancellationToken));
        return row is null ? null : ToOfferPresentationMeeting(row);
    }

    private static OfferPresentationMeetingDetails ToOfferPresentationMeeting(OfferPresentationMeetingRow row)
    {
        return new OfferPresentationMeetingDetails(
            row.OfferPresentationMeetingId,
            row.OfferLetterId,
            row.JobApplicationId,
            Utc(row.MeetingAt),
            row.LocationText,
            row.Notes,
            row.Status,
            Utc(row.CreatedAt));
    }

    private static async Task<OfferAccessRow?> ReadOfferAccessContextAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid offerLetterId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                offer.OfferLetterId,
                offer.JobApplicationId,
                offer.JobRequestId,
                offer.JobPostId,
                offer.CandidateId,
                request.RequestCode,
                COALESCE(post.Title, request.Title) AS JobTitle,
                candidate.DisplayName AS CandidateName,
                candidate.Email AS CandidateEmail,
                tenant.DisplayName AS CompanyName,
                request.HiringManagerUserId,
                hiringManager.DisplayName AS HiringManagerName
            FROM dbo.OfferLetters AS offer
            INNER JOIN dbo.JobRequests AS request
                ON request.TenantId = offer.TenantId
                AND request.JobRequestId = offer.JobRequestId
            LEFT JOIN dbo.JobPosts AS post
                ON post.TenantId = offer.TenantId
                AND post.JobPostId = offer.JobPostId
            INNER JOIN dbo.Candidates AS candidate
                ON candidate.TenantId = offer.TenantId
                AND candidate.CandidateId = offer.CandidateId
            INNER JOIN dbo.Tenants AS tenant
                ON tenant.TenantId = offer.TenantId
            INNER JOIN dbo.AppUsers AS hiringManager
                ON hiringManager.TenantId = request.TenantId
                AND hiringManager.UserId = request.HiringManagerUserId
            WHERE offer.TenantId = @TenantId
              AND offer.OfferLetterId = @OfferLetterId
              AND (@IncludeAllTenantReviews = CAST(1 AS BIT) OR request.HiringManagerUserId = @ActorUserId);
            """;

        return await connection.QuerySingleOrDefaultAsync<OfferAccessRow>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                IncludeAllTenantReviews = includeAllTenantReviews,
                OfferLetterId = offerLetterId
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task QueueOfferPresentationMeetingEmailAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        OfferAccessRow access,
        Guid meetingId,
        ScheduleOfferPresentationMeetingInput input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(access.CandidateEmail))
        {
            return;
        }

        var eventId = await EnsureNotificationEventAsync(
            connection,
            transaction,
            tenantId,
            NotificationEventCodes.OfferPresentationMeetingScheduled,
            "Offer presentation meeting scheduled",
            "User:Candidate",
            cancellationToken);

        var body = string.Join(Environment.NewLine, new[]
        {
            $"Hello {access.CandidateName},",
            string.Empty,
            $"{access.CompanyName} has scheduled an in-person offer presentation meeting for {access.JobTitle}.",
            string.Empty,
            $"When: {input.MeetingAtUtc:yyyy-MM-dd HH:mm} UTC",
            $"Where: {input.LocationText}",
            string.IsNullOrWhiteSpace(input.Notes) ? string.Empty : $"Notes: {input.Notes}",
            string.Empty,
            "Please attend the meeting to review the offer details."
        }.Where(line => line is not null));

        await InsertInterviewEmailOutboxAsync(
            connection,
            transaction,
            tenantId,
            eventId,
            null,
            meetingId,
            "OfferPresentationMeeting",
            [
                new InterviewScheduleEmailMessage(
                    "Candidate",
                    null,
                    access.CandidateEmail,
                    $"Offer presentation meeting: {access.JobTitle}",
                    body)
            ],
            cancellationToken);
    }

    private static string BuildOfferLetterBody(HiringReviewAccessRow access, GenerateOfferLetterInput input)
    {
        var companyName = NullIfWhiteSpace(access.CompanyName) ?? "[Company Name]";
        var candidateName = NullIfWhiteSpace(access.CandidateName) ?? "[Candidate Name]";
        var jobTitle = NullIfWhiteSpace(access.JobTitle) ?? NullIfWhiteSpace(access.RequestTitle) ?? "[Position Title]";
        var department = NullIfWhiteSpace(access.Department) ?? "[Department]";
        var reportingManager = NullIfWhiteSpace(input.ReportingManager)
            ?? NullIfWhiteSpace(access.HiringManagerName)
            ?? "[Manager Name / Designation]";
        var workLocation = NullIfWhiteSpace(input.WorkLocation)
            ?? NullIfWhiteSpace(access.Location)
            ?? "[Office Location / Remote / Hybrid]";
        var joiningDate = input.StartDate?.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture) ?? "[Joining Date]";
        var compensation = NullIfWhiteSpace(input.CompensationText) ?? "[Salary Amount] per [month/year]";
        var generatedDate = DateTime.UtcNow.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        var signerName = NullIfWhiteSpace(access.HiringManagerName) ?? "[Authorized Person Name]";

        var lines = new List<string> { companyName };
        AddIfNotBlank(lines, access.CompanyAddress);

        var cityCountry = JoinNonEmpty(", ", access.CompanyCity, access.CompanyCountry);
        AddIfNotBlank(lines, cityCountry);

        var officialContact = JoinNonEmpty(
            " | ",
            PrefixIfPresent("Email: ", access.OfficialEmail),
            PrefixIfPresent("Phone: ", access.OfficialPhone));
        AddIfNotBlank(lines, officialContact);

        lines.AddRange([
            string.Empty,
            $"Date: {generatedDate}",
            string.Empty,
            $"To: {candidateName}",
            string.Empty,
            $"Subject: Offer of Employment - {jobTitle}",
            string.Empty,
            $"Dear {candidateName},",
            string.Empty,
            $"We are pleased to offer you the position of {jobTitle} at {companyName}. We were impressed with your expertise, problem-solving abilities, and professional experience, and we believe you will be a valuable addition to our team.",
            string.Empty,
            "Your employment details are as follows:",
            string.Empty,
            $"Position: {jobTitle}",
            $"Department: {department}",
            $"Reporting To: {reportingManager}",
            $"Joining Date: {joiningDate}",
            "Employment Type: Full-time / Permanent",
            $"Work Location: {workLocation}",
            string.Empty,
            $"Your total compensation will be {compensation}, subject to applicable taxes and deductions. Additional benefits, if applicable, may include health insurance, paid leaves, performance bonuses, provident fund, learning opportunities, and other company-provided benefits as per {companyName}'s policies.",
            string.Empty,
            BuildOfferResponsibilitiesParagraph(access, jobTitle),
            string.Empty,
            $"This offer is subject to successful completion of any required background checks, reference verification, and submission of necessary employment documents. You will also be expected to comply with {companyName}'s policies, confidentiality requirements, intellectual property agreements, and code of conduct.",
            string.Empty,
            "Please confirm your acceptance of this offer by signing and returning a copy of this letter by [Acceptance Deadline]."
        ]);

        var additionalNotes = NullIfWhiteSpace(input.AdditionalNotes);
        if (additionalNotes is not null)
        {
            lines.Add(string.Empty);
            lines.Add($"Additional notes: {additionalNotes}");
        }

        lines.AddRange([
            string.Empty,
            $"We are excited about the possibility of you joining {companyName} and look forward to your contribution to our continued success.",
            string.Empty,
            "Sincerely,",
            string.Empty,
            signerName,
            companyName,
            string.Empty,
            "Acceptance",
            string.Empty,
            $"I, {candidateName}, accept the offer for the position of {jobTitle} at {companyName} under the terms stated above.",
            string.Empty,
            "Signature: _______________________",
            "Date: ___________________________"
        ]);

        return string.Join(Environment.NewLine, lines);
    }

    private static void AddIfNotBlank(List<string> lines, string? value)
    {
        var text = NullIfWhiteSpace(value);
        if (text is not null)
        {
            lines.Add(text);
        }
    }

    private static string? PrefixIfPresent(string prefix, string? value)
    {
        var text = NullIfWhiteSpace(value);
        return text is null ? null : $"{prefix}{text}";
    }

    private static string? JoinNonEmpty(string separator, params string?[] values)
    {
        var nonEmpty = values
            .Select(NullIfWhiteSpace)
            .Where(value => value is not null)
            .ToArray();

        return nonEmpty.Length == 0 ? null : string.Join(separator, nonEmpty);
    }

    private static string BuildOfferResponsibilitiesParagraph(HiringReviewAccessRow access, string jobTitle)
    {
        var normalizedTitle = NormalizeDecisionBriefText(jobTitle);
        if (normalizedTitle.Contains("software", StringComparison.OrdinalIgnoreCase)
            || normalizedTitle.Contains("engineer", StringComparison.OrdinalIgnoreCase)
            || normalizedTitle.Contains("developer", StringComparison.OrdinalIgnoreCase))
        {
            return $"As a {jobTitle}, your responsibilities will include designing, developing, testing, and maintaining software solutions; collaborating with cross-functional teams; reviewing code; mentoring junior engineers; contributing to architecture decisions; and ensuring high standards of quality, performance, and security.";
        }

        var department = NullIfWhiteSpace(access.Department) ?? "the relevant department";
        return $"As a {jobTitle}, your responsibilities will include fulfilling the duties described in the approved job request and job description; collaborating with cross-functional teams; supporting {department} delivery goals; mentoring where appropriate; and ensuring high standards of quality, performance, and professionalism.";
    }

    private static async Task UpdateApplicationStatusAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobApplicationId,
        string oldStatus,
        string newStatus,
        Guid actorUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.JobApplications
            SET CurrentStatus = @NewStatus,
                FinalDecisionAtUtc = CASE WHEN @NewStatus IN (N'Offered', N'OfferDeclined', N'Rejected', N'OnHold', N'Hired', N'Joined') THEN SYSUTCDATETIME() ELSE FinalDecisionAtUtc END,
                FinalDecisionReason = CASE WHEN @NewStatus IN (N'Offered', N'OfferDeclined', N'Rejected', N'OnHold', N'Hired', N'Joined') THEN @Reason ELSE FinalDecisionReason END,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobApplicationId = @JobApplicationId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobApplicationId = jobApplicationId, OldStatus = oldStatus, NewStatus = newStatus, Reason = reason },
            transaction,
            cancellationToken: cancellationToken));

        await InsertJobApplicationStatusHistoryAsync(
            connection,
            transaction,
            tenantId,
            jobApplicationId,
            oldStatus,
            newStatus,
            actorUserId,
            reason ?? $"Hiring outcome changed to {newStatus}.",
            cancellationToken);
    }

    private static async Task UpdateLatestOfferLetterOutcomeAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobApplicationId,
        string newStatus,
        DateOnly? joiningDate,
        CancellationToken cancellationToken)
    {
        var offerStatus = newStatus switch
        {
            "Offered" => "Presented",
            "Hired" or "Joined" => "Accepted",
            "OfferDeclined" or "Rejected" => "Declined",
            _ => null
        };

        if (offerStatus is null && joiningDate is null)
        {
            return;
        }

        const string sql = """
            UPDATE offer
            SET Status = COALESCE(@OfferStatus, offer.Status),
                StartDate = COALESCE(@JoiningDate, offer.StartDate),
                UpdatedAtUtc = SYSUTCDATETIME()
            FROM dbo.OfferLetters AS offer
            INNER JOIN
            (
                SELECT TOP (1) latest.OfferLetterId
                FROM dbo.OfferLetters AS latest
                WHERE latest.TenantId = @TenantId
                  AND latest.JobApplicationId = @JobApplicationId
                ORDER BY latest.Version DESC, latest.UpdatedAtUtc DESC
            ) AS latest
                ON latest.OfferLetterId = offer.OfferLetterId
            WHERE offer.TenantId = @TenantId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                JobApplicationId = jobApplicationId,
                OfferStatus = offerStatus,
                JoiningDate = joiningDate?.ToDateTime(TimeOnly.MinValue)
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertExternalFulfillmentAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        HiringReviewAccessRow access,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.JobRequestFulfillments
            (
                JobRequestFulfillmentId,
                TenantId,
                JobRequestId,
                JobApplicationId,
                CandidateId,
                FulfilledByUserId,
                FulfillmentType,
                Status,
                FulfilledAtUtc,
                Notes
            )
            SELECT
                NEWID(),
                @TenantId,
                @JobRequestId,
                @JobApplicationId,
                @CandidateId,
                @ActorUserId,
                N'ExternalCandidate',
                N'Completed',
                SYSUTCDATETIME(),
                N'Candidate hired or joined from Hiring Manager outcome.'
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.JobRequestFulfillments AS existing
                WHERE existing.TenantId = @TenantId
                  AND existing.JobApplicationId = @JobApplicationId
                  AND existing.Status = N'Completed'
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                access.JobRequestId,
                access.JobApplicationId,
                access.CandidateId,
                ActorUserId = actorUserId
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task ReturnJobRequestToSourcingAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var currentAssignmentId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            """
            SELECT CurrentAssignmentId
            FROM dbo.JobRequests
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));
        if (currentAssignmentId.HasValue)
        {
            await CompleteAssignmentAsync(connection, transaction, tenantId, currentAssignmentId.Value, cancellationToken);
        }

        var workflowDefinitionId = await ReadWorkflowDefinitionIdAsync(connection, transaction, tenantId, cancellationToken);
        var stageId = await ReadWorkflowStageIdAsync(connection, transaction, tenantId, "SOURCING", cancellationToken);
        var assignmentTarget = await ResolveWorkflowRoutingAssignmentAsync(connection, transaction, tenantId, "FORWARD_TO_RECRUITER", cancellationToken);
        var assignmentId = Guid.NewGuid();
        await InsertWorkflowAssignmentAsync(
            connection,
            transaction,
            tenantId,
            workflowDefinitionId,
            stageId,
            null,
            assignmentId,
            jobRequestId,
            assignmentTarget,
            DateTimeOffset.UtcNow,
            cancellationToken);
        await UpdateJobRequestStageAsync(connection, transaction, tenantId, jobRequestId, "Sourcing", "SOURCING", assignmentId, cancellationToken);
    }

    private static async Task UpdateJobRequestStatusOnlyAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        string status,
        string stageKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.JobRequests
            SET Status = @Status,
                CurrentStageKey = @StageKey,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId, Status = status, StageKey = stageKey },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task CloseJobPostsForRequestAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.JobPosts
            SET Status = N'Closed',
                ClosedAtUtc = COALESCE(ClosedAtUtc, SYSUTCDATETIME()),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId
              AND Status <> N'Closed';
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<bool> CanCloseHiringJobRequestAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        bool includeAllTenantReviews,
        Guid jobRequestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.JobRequests
            WHERE TenantId = @TenantId
              AND JobRequestId = @JobRequestId
              AND (@IncludeAllTenantReviews = CAST(1 AS BIT) OR HiringManagerUserId = @ActorUserId);
            """;

        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, ActorUserId = actorUserId, IncludeAllTenantReviews = includeAllTenantReviews, JobRequestId = jobRequestId },
            transaction,
            cancellationToken: cancellationToken));
        return count > 0;
    }

    private static bool IsOfferEligibleStatus(string status)
    {
        return status is "HiringManagerReview" or "Offered" or "OfferDeclined" or "OnHold" or "Hired" or "Joined";
    }

    private static string BuildPmoOwnerState(
        string assignmentStatus,
        Guid? claimedByUserId,
        string? claimedByName,
        Guid actorUserId,
        bool isTenantAdmin)
    {
        if (string.Equals(assignmentStatus, "Claimed", StringComparison.OrdinalIgnoreCase))
        {
            if (claimedByUserId == actorUserId)
            {
                return "Claimed by you";
            }

            return isTenantAdmin && !string.IsNullOrWhiteSpace(claimedByName)
                ? $"Claimed by {claimedByName}"
                : "Claimed";
        }

        return "Unclaimed";
    }

    private static string BuildPmoCta(string assignmentStatus, Guid? claimedByUserId, Guid actorUserId)
    {
        if (string.Equals(assignmentStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return "Claim ownership";
        }

        return claimedByUserId == actorUserId ? "Continue review" : "Open review";
    }

    private static IReadOnlyList<PmoDashboardAgingBucket> BuildPmoAgingBuckets(IReadOnlyList<PmoDashboardWorkItem> workItems)
    {
        return
        [
            new PmoDashboardAgingBucket("0-1 days", workItems.Count(item => item.DaysWaiting <= 1)),
            new PmoDashboardAgingBucket("2-3 days", workItems.Count(item => item.DaysWaiting is >= 2 and <= 3)),
            new PmoDashboardAgingBucket("4-7 days", workItems.Count(item => item.DaysWaiting is >= 4 and <= 7)),
            new PmoDashboardAgingBucket("8+ days", workItems.Count(item => item.DaysWaiting >= 8))
        ];
    }

    private static Task<PmoDashboardSummary> BuildPmoDashboardSummaryAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid actorUserId,
        bool isTenantAdmin,
        IReadOnlyList<PmoDashboardAssignmentRow> visibleRows,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        return ReadPmoSummaryCountsAsync(
            connection,
            tenantId,
            actorUserId,
            isTenantAdmin,
            visibleRows.Count(row => string.Equals(row.AssignmentStatus, "Pending", StringComparison.OrdinalIgnoreCase)),
            visibleRows.Count(row => string.Equals(row.AssignmentStatus, "Claimed", StringComparison.OrdinalIgnoreCase) &&
                (isTenantAdmin || row.ClaimedByUserId == actorUserId || row.AssignedToUserId == actorUserId)),
            fromUtc,
            toUtc,
            cancellationToken);
    }

    private static async Task<PmoDashboardSummary> ReadPmoSummaryCountsAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid actorUserId,
        bool isTenantAdmin,
        int unclaimedReviews,
        int claimedReviews,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<PmoSummaryCountsRow>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(DISTINCT audit.EntityId)
                 FROM dbo.AuditLogs AS audit
                 WHERE audit.TenantId = @TenantId
                   AND audit.EventType = N'job_request.employee_referral_decision'
                   AND audit.OccurredAtUtc >= @FromUtc
                   AND audit.OccurredAtUtc <= @ToUtc) AS ReturnedFromPresales,
                (SELECT COUNT(DISTINCT log.SourceEntityId)
                 FROM dbo.AiRecommendationLogs AS log
                 WHERE log.TenantId = @TenantId
                   AND log.AiAgentDefinitionId = N'bench-matching'
                   AND log.SourceEntityType = N'JobRequest'
                   AND log.RecommendedEntityType = N'Employee') AS AiRankedRequests,
                (SELECT COUNT(DISTINCT referral.JobRequestId)
                 FROM dbo.JobRequestEmployeeReferrals AS referral
                 WHERE referral.TenantId = @TenantId
                   AND referral.CreatedAtUtc >= @FromUtc
                   AND referral.CreatedAtUtc <= @ToUtc
                   AND (@IsTenantAdmin = CAST(1 AS BIT) OR referral.ReferredByUserId = @ActorUserId)) AS RecommendedToPresales,
                (SELECT COUNT(DISTINCT audit.EntityId)
                 FROM dbo.AuditLogs AS audit
                 WHERE audit.TenantId = @TenantId
                   AND audit.EventType = N'job_request.forwarded_to_recruiters'
                   AND audit.OccurredAtUtc >= @FromUtc
                   AND audit.OccurredAtUtc <= @ToUtc
                   AND
                   (
                       @IsTenantAdmin = CAST(1 AS BIT)
                       OR audit.ActorUserId = @ActorUserId
                   )) AS ForwardedToRecruiters;
            """,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                IsTenantAdmin = isTenantAdmin,
                FromUtc = fromUtc,
                ToUtc = toUtc
            },
            cancellationToken: cancellationToken));

        return new PmoDashboardSummary(
            unclaimedReviews,
            claimedReviews,
            row.ReturnedFromPresales,
            row.AiRankedRequests,
            row.RecommendedToPresales,
            row.ForwardedToRecruiters);
    }

    private static async Task<PmoDashboardRecommendationOutcomes> ReadPmoRecommendationOutcomesAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid actorUserId,
        bool isTenantAdmin,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<PmoRecommendationOutcomeRow>(new CommandDefinition(
            """
            SELECT
                COALESCE(SUM(CASE WHEN referral.Status = N'Referred' THEN 1 ELSE 0 END), 0) AS PendingPresalesReview,
                COALESCE(SUM(CASE WHEN referral.Status IN (N'AcceptedByPresales', N'ClientAccepted') THEN 1 ELSE 0 END), 0) AS AcceptedByPresales,
                COALESCE(SUM(CASE WHEN referral.Status IN (N'RejectedByPresales', N'ClientRejected') THEN 1 ELSE 0 END), 0) AS RejectedByPresales,
                (SELECT COUNT(*)
                 FROM dbo.JobRequestFulfillments AS fulfillment
                 INNER JOIN dbo.JobRequestEmployeeReferrals AS fulfillmentReferral
                     ON fulfillmentReferral.TenantId = fulfillment.TenantId
                     AND fulfillmentReferral.JobRequestEmployeeReferralId = fulfillment.JobRequestEmployeeReferralId
                 WHERE fulfillment.TenantId = @TenantId
                   AND fulfillment.FulfillmentType = N'InternalEmployee'
                   AND fulfillment.Status = N'Completed'
                   AND fulfillment.FulfilledAtUtc >= @FromUtc
                   AND fulfillment.FulfilledAtUtc <= @ToUtc
                   AND (@IsTenantAdmin = CAST(1 AS BIT) OR fulfillmentReferral.ReferredByUserId = @ActorUserId)) AS FulfilledInternally
            FROM dbo.JobRequestEmployeeReferrals AS referral
            WHERE referral.TenantId = @TenantId
              AND referral.CreatedAtUtc >= @FromUtc
              AND referral.CreatedAtUtc <= @ToUtc
              AND (@IsTenantAdmin = CAST(1 AS BIT) OR referral.ReferredByUserId = @ActorUserId);
            """,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                IsTenantAdmin = isTenantAdmin,
                FromUtc = fromUtc,
                ToUtc = toUtc
            },
            cancellationToken: cancellationToken));

        var reviewed = row.AcceptedByPresales + row.RejectedByPresales;
        var total = row.PendingPresalesReview + reviewed;
        return new PmoDashboardRecommendationOutcomes(
            row.PendingPresalesReview,
            row.AcceptedByPresales,
            row.RejectedByPresales,
            row.FulfilledInternally,
            Rate(reviewed, total));
    }

    private static async Task<IReadOnlyList<PmoDashboardDecisionSplit>> ReadPmoDecisionSplitAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid actorUserId,
        bool isTenantAdmin,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<PmoDecisionSplitRow>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(DISTINCT referral.JobRequestId)
                 FROM dbo.JobRequestEmployeeReferrals AS referral
                 WHERE referral.TenantId = @TenantId
                   AND referral.CreatedAtUtc >= @FromUtc
                   AND referral.CreatedAtUtc <= @ToUtc
                   AND (@IsTenantAdmin = CAST(1 AS BIT) OR referral.ReferredByUserId = @ActorUserId)) AS Recommended,
                (SELECT COUNT(DISTINCT audit.EntityId)
                 FROM dbo.AuditLogs AS audit
                 WHERE audit.TenantId = @TenantId
                   AND audit.EventType = N'job_request.forwarded_to_recruiters'
                   AND audit.OccurredAtUtc >= @FromUtc
                   AND audit.OccurredAtUtc <= @ToUtc
                   AND (@IsTenantAdmin = CAST(1 AS BIT) OR audit.ActorUserId = @ActorUserId)) AS Forwarded,
                (SELECT COUNT(DISTINCT audit.EntityId)
                 FROM dbo.AuditLogs AS audit
                 WHERE audit.TenantId = @TenantId
                   AND audit.EventType = N'job_request.employee_referral_decision'
                   AND audit.OccurredAtUtc >= @FromUtc
                   AND audit.OccurredAtUtc <= @ToUtc) AS Returned;
            """,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                IsTenantAdmin = isTenantAdmin,
                FromUtc = fromUtc,
                ToUtc = toUtc
            },
            cancellationToken: cancellationToken));

        return
        [
            new PmoDashboardDecisionSplit("Recommended to Presales", row.Recommended),
            new PmoDashboardDecisionSplit("Forwarded to Recruiters", row.Forwarded),
            new PmoDashboardDecisionSplit("Returned from Presales", row.Returned)
        ];
    }

    private static async Task<IReadOnlyList<PmoDashboardRecommendationTrendItem>> ReadPmoRecommendationTrendAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid actorUserId,
        bool isTenantAdmin,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<PmoRecommendationTrendRow>(new CommandDefinition(
            """
            SELECT
                CONVERT(date, referral.CreatedAtUtc) AS PeriodStartUtc,
                COUNT(*) AS Recommended,
                COALESCE(SUM(CASE WHEN referral.Status IN (N'AcceptedByPresales', N'ClientAccepted') THEN 1 ELSE 0 END), 0) AS Accepted,
                COALESCE(SUM(CASE WHEN referral.Status IN (N'RejectedByPresales', N'ClientRejected') THEN 1 ELSE 0 END), 0) AS Rejected
            FROM dbo.JobRequestEmployeeReferrals AS referral
            WHERE referral.TenantId = @TenantId
              AND referral.CreatedAtUtc >= @FromUtc
              AND referral.CreatedAtUtc <= @ToUtc
              AND (@IsTenantAdmin = CAST(1 AS BIT) OR referral.ReferredByUserId = @ActorUserId)
            GROUP BY CONVERT(date, referral.CreatedAtUtc)
            ORDER BY PeriodStartUtc;
            """,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                IsTenantAdmin = isTenantAdmin,
                FromUtc = fromUtc,
                ToUtc = toUtc
            },
            cancellationToken: cancellationToken));

        return rows
            .Select(row => new PmoDashboardRecommendationTrendItem(
                Utc(row.PeriodStartUtc),
                row.Recommended,
                row.Accepted,
                row.Rejected))
            .ToArray();
    }

    private static async Task<IReadOnlyList<PmoDashboardSkillBenchItem>> ReadPmoSkillDemandAsync(
        SqlConnection connection,
        Guid tenantId,
        IReadOnlyList<Guid> jobRequestIds,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<PmoSkillBenchRow>(new CommandDefinition(
            """
            WITH Demand AS
            (
                SELECT skill.SkillId, skill.Name, COUNT(DISTINCT requestSkill.JobRequestId) AS DemandCount
                FROM dbo.JobRequestSkills AS requestSkill
                INNER JOIN dbo.Skills AS skill
                    ON skill.TenantId = requestSkill.TenantId
                    AND skill.SkillId = requestSkill.SkillId
                WHERE requestSkill.TenantId = @TenantId
                  AND requestSkill.JobRequestId IN @JobRequestIds
                GROUP BY skill.SkillId, skill.Name
            ),
            Bench AS
            (
                SELECT employeeSkill.SkillId, COUNT(DISTINCT employee.EmployeeId) AS BenchAvailableCount
                FROM dbo.vw_EmployeeBenchAvailability AS employee
                INNER JOIN dbo.EmployeeSkills AS employeeSkill
                    ON employeeSkill.TenantId = employee.TenantId
                    AND employeeSkill.EmployeeId = employee.EmployeeId
                WHERE employee.TenantId = @TenantId
                  AND employee.IsCurrentlyBenched = CAST(1 AS BIT)
                  AND employee.BenchStatus IN (N'Benched', N'PartialBench')
                GROUP BY employeeSkill.SkillId
            )
            SELECT TOP (8)
                demand.Name AS Skill,
                demand.DemandCount,
                COALESCE(bench.BenchAvailableCount, 0) AS BenchAvailableCount
            FROM Demand AS demand
            LEFT JOIN Bench AS bench ON bench.SkillId = demand.SkillId
            ORDER BY demand.DemandCount DESC, demand.Name;
            """,
            new { TenantId = tenantId, JobRequestIds = jobRequestIds },
            cancellationToken: cancellationToken));

        return rows
            .Select(row => new PmoDashboardSkillBenchItem(
                row.Skill,
                row.DemandCount,
                row.BenchAvailableCount,
                Math.Max(0, row.DemandCount - row.BenchAvailableCount)))
            .ToArray();
    }

    private static async Task<PmoDashboardAiHealth> ReadPmoAiHealthAsync(
        SqlConnection connection,
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<PmoAiHealthRow>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*)
                 FROM dbo.AiAgentRuns
                 WHERE TenantId = @TenantId
                   AND AiAgentDefinitionId = N'bench-matching'
                   AND StartedAtUtc >= @FromUtc
                   AND StartedAtUtc <= @ToUtc) AS RunsInWindow,
                (SELECT COUNT(*)
                 FROM dbo.AiAgentRuns
                 WHERE TenantId = @TenantId
                   AND AiAgentDefinitionId = N'bench-matching'
                   AND Status = N'Failed'
                   AND StartedAtUtc >= @FromUtc
                   AND StartedAtUtc <= @ToUtc) AS FailedRuns,
                (SELECT MAX(CompletedAtUtc)
                 FROM dbo.AiAgentRuns
                 WHERE TenantId = @TenantId
                   AND AiAgentDefinitionId = N'bench-matching'
                   AND Status = N'Succeeded') AS LatestRunAt,
                (SELECT COUNT(DISTINCT SourceEntityId)
                 FROM dbo.AiRecommendationLogs
                 WHERE TenantId = @TenantId
                   AND AiAgentDefinitionId = N'bench-matching'
                   AND RecommendedEntityType = N'Employee') AS RankedRequests,
                (SELECT COUNT(*)
                 FROM dbo.VectorEmbeddings
                 WHERE TenantId = @TenantId
                   AND IsActive = CAST(1 AS BIT)
                   AND EntityType = N'Employee') AS EmployeeEmbeddings;
            """,
            new { TenantId = tenantId, FromUtc = fromUtc, ToUtc = toUtc },
            cancellationToken: cancellationToken));

        return new PmoDashboardAiHealth(
            row.RunsInWindow,
            row.FailedRuns,
            ToUtc(row.LatestRunAt),
            row.RankedRequests,
            row.EmployeeEmbeddings);
    }

    private static DateTimeOffset Utc(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static DateTimeOffset? ToUtc(DateTime? value)
    {
        return value.HasValue ? Utc(value.Value) : null;
    }

    private static string BuildJobRequestCreatedAuditSummary(
        string requestCode,
        WorkflowAssignmentTarget assignmentTarget)
    {
        return assignmentTarget.Source switch
        {
            "PMO creator" => $"{requestCode} was created by PMO and assigned to the creator for PMO review.",
            "Tenant Admin fallback" => $"{requestCode} was created and routed to Tenant Admins because department intake routing is missing.",
            _ => $"{requestCode} was created and routed to the configured department intake recipient."
        };
    }

    private static decimal Rate(int numerator, int denominator)
    {
        return denominator <= 0 ? 0m : decimal.Round(numerator * 100m / denominator, 1);
    }

    private static decimal? Median(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var midpoint = values.Count / 2;
        if (values.Count % 2 == 1)
        {
            return values[midpoint];
        }

        return decimal.Round((values[midpoint - 1] + values[midpoint]) / 2m, 1);
    }

    private sealed record DashboardSummaryRow(
        int OpenJobRequests,
        int RequiredPositions,
        int FulfilledPositions,
        int PublishedJobPosts,
        int ActiveApplications,
        int InterviewsThisWeek,
        int Offers,
        int JoinedCandidates);

    private sealed record DashboardFunnelRow(string Label, int Count, int SortOrder);

    private sealed record DashboardAttentionCountsRow(
        int MissingRouting,
        int PublishedPostsWithoutApplications,
        int OverdueFeedback,
        int HiringManagerPending,
        int OfferWaiting);

    private sealed record DashboardOfferHealthRow(
        int OfferLetters,
        int PresentationMeetings,
        int Offered,
        int OnHold,
        int Rejected,
        int Joined);

    private sealed record DashboardEfficiencyCountsRow(
        int PmoQueueLoad,
        int RecruiterSourcingLoad,
        int InterviewerLoad,
        int HiringManagerPendingReviews);

    private sealed record DashboardStageAgingRow(
        Guid JobRequestId,
        string RequestCode,
        string Title,
        string Department,
        string CurrentStage,
        string OwnerName,
        int DaysInStage,
        string Risk);

    private sealed record DashboardDepartmentPerformanceRow(
        string Department,
        int OpenRequests,
        int OpenPositions,
        int Applications,
        int Interviews,
        int Joined,
        decimal? AverageTimeToFillDays);

    private sealed record DashboardAiHealthRow(
        int RunsToday,
        int FailedRuns,
        DateTime? LatestBenchMatchingAt,
        DateTime? LatestTalentRediscoveryAt,
        int ActiveEmbeddings,
        int CandidateEmbeddings,
        int JobRequestEmbeddings,
        int JobPostEmbeddings,
        int EmployeeEmbeddings);

    private sealed record PmoDashboardAssignmentRow(
        Guid JobRequestId,
        string RequestCode,
        string Title,
        string Client,
        string Department,
        string Location,
        string Priority,
        Guid AssignmentId,
        string AssignmentStatus,
        Guid? AssignedToUserId,
        Guid? ClaimedByUserId,
        string? ClaimedByName,
        DateTime AssignedAtUtc,
        string LatestAction,
        bool ActorCanClaimOrOwnPendingWork);

    private sealed record PmoSummaryCountsRow(
        int ReturnedFromPresales,
        int AiRankedRequests,
        int RecommendedToPresales,
        int ForwardedToRecruiters);

    private sealed record PmoRecommendationOutcomeRow(
        int PendingPresalesReview,
        int AcceptedByPresales,
        int RejectedByPresales,
        int FulfilledInternally);

    private sealed record PmoDecisionSplitRow(int Recommended, int Forwarded, int Returned);

    private sealed record PmoRecommendationTrendRow(
        DateTime PeriodStartUtc,
        int Recommended,
        int Accepted,
        int Rejected);

    private sealed record PmoSkillBenchRow(
        string Skill,
        int DemandCount,
        int BenchAvailableCount);

    private sealed record PmoAiHealthRow(
        int RunsInWindow,
        int FailedRuns,
        DateTime? LatestRunAt,
        int RankedRequests,
        int EmployeeEmbeddings);

    private sealed record PersonRow(Guid UserId, string DisplayName, string Email, string? RoleCode, string? RoleName);

    private sealed record OperationsActivityEventRow(
        Guid Id,
        Guid EntityId,
        string ActorName,
        string Title,
        string Detail,
        DateTime CreatedAt);

    private sealed record WorkflowAssignmentRow(
        Guid Id,
        string EntityType,
        Guid EntityId,
        string Stage,
        string? AssignedToGroupId,
        Guid? AssignedToUserId,
        Guid? ClaimedByUserId,
        string Status,
        DateTime AssignedAt);

    private sealed record NotificationRow(
        Guid Id,
        Guid RecipientUserId,
        string Title,
        string Message,
        string Category,
        string Severity,
        string EntityType,
        Guid EntityId,
        DateTime? ReadAt,
        DateTime CreatedAt,
        string? MetadataJson);

    private sealed record IntakeDepartmentOptionRow(
        Guid DepartmentId,
        string Code,
        string Name,
        string AssignmentType,
        Guid? TargetUserId,
        Guid? TargetGroupId,
        string TargetName,
        bool UsesTenantAdminFallback);

    private sealed record WorkflowIds(Guid WorkflowDefinitionId, Guid WorkflowStageId, Guid WorkflowTransitionId);

    private sealed record IntakeRoutingRuleRow(string AssignmentType, Guid? TargetUserId, Guid? TargetGroupId);

    private sealed record EmployeeReferralRow(
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
        DateTime CreatedAt);

    private sealed record EmployeeSkillRow(Guid SkillId, string Name);

    private sealed record BenchRequestContextRow(string Department, string Location);

    private sealed record JobRequestExperienceRow(decimal? ExperienceMinYears, decimal? ExperienceMaxYears);

    private sealed record BenchEmployeeSkillRow(
        Guid EmployeeId,
        string DisplayName,
        string Email,
        string? Designation,
        string Department,
        string Location,
        decimal? ExperienceYears,
        DateTime? JoiningDate,
        string AvailabilityStatus,
        string BenchStatus,
        bool IsCurrentlyBenched,
        Guid? SkillId,
        string? SkillName);

    private sealed record EmployeeProjectEvidenceRow(
        Guid EmployeeId,
        string ProjectName,
        string? ClientName,
        string Status,
        int AllocationPercent,
        DateTime? StartsOn,
        DateTime? EndsOn);

    private sealed record BenchMatchLogRow(
        Guid EmployeeId,
        Guid? AiAgentRunId,
        decimal? Score,
        string? Explanation,
        string PayloadJson,
        DateTime GeneratedAt);

    private sealed record TalentRediscoveryLogRow(
        Guid CandidateId,
        Guid? AiAgentRunId,
        decimal? Score,
        string? Explanation,
        string PayloadJson,
        DateTime GeneratedAt);

    private sealed record ApplicantRankingLogRow(
        Guid JobApplicationId,
        Guid? AiAgentRunId,
        decimal? Score,
        string? Explanation,
        string PayloadJson,
        DateTime GeneratedAt);

    private sealed record OnlineHeadhuntingDuplicateCandidateRow(
        Guid CandidateId,
        string DisplayName,
        string Email,
        string? Phone,
        string? LinkedInUrl,
        string? CurrentDesignation,
        string? CurrentCompany,
        decimal? ExperienceYears,
        string? SkillName);

    private sealed record OnlineHeadhuntingRunRow(
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
        string SourceCodesJson,
        string QueriesJson,
        DateTime CreatedAtUtc);

    private sealed record OnlineHeadhuntingLeadRow(
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
        string StrengthsJson,
        string MatchedSkillsJson,
        string GapsJson,
        string MissingDataJson,
        string DuplicateStatus,
        Guid? DuplicateCandidateId,
        string? DuplicateCandidateName,
        string? DuplicateExplanation,
        string OutreachDraft,
        string Status,
        DateTime CreatedAtUtc);

    private sealed record OnlineLeadActionContextRow(Guid JobRequestId);

    private sealed record BenchMatchPayload(
        int Rank,
        decimal Score,
        string Confidence,
        string Explanation,
        IReadOnlyList<string> Strengths,
        IReadOnlyList<string> Gaps,
        IReadOnlyList<OperationsEmployeeProjectEvidence> ProjectEvidence,
        string WebResearchStatus,
        string? WebSummary,
        IReadOnlyList<OperationsBenchMatchWebSource> WebSources);

    private sealed record TalentRediscoveryPayload(
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
        IReadOnlyList<OperationsCandidateInterviewEvidence> InterviewEvidence);

    private sealed record ApplicantRankingPayload(
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
        string SemanticSimilarityStatus);

    private sealed record ApplicantRankingApplicationSkillRow(
        Guid JobApplicationId,
        Guid JobPostId,
        Guid JobRequestId,
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
        DateTime AppliedAt,
        string? ApplicationSnapshotJson,
        string? SkillName);

    private sealed record ApplicationDocumentEvidenceRow(
        Guid ApplicationDocumentId,
        Guid JobApplicationId,
        string DocumentType,
        string FileName,
        string ContentType,
        long SizeBytes,
        string StorageProvider,
        string StorageKey,
        string? StorageContainer,
        string ContentHashSha256,
        DateTime UploadedAt,
        string ExtractionStatus,
        bool HasExtractedText,
        string? ExtractedText,
        string? ExtractedTextHashSha256,
        string? ParserVersion,
        DateTime? ExtractedAt,
        string? ExtractionError);

    private sealed record RediscoveryCandidateSkillRow(
        Guid CandidateId,
        string DisplayName,
        string Email,
        string Status,
        string? CurrentDesignation,
        string? CurrentCompany,
        decimal? ExperienceYears,
        int? NoticePeriodDays,
        Guid? SkillId,
        string? SkillName);

    private sealed record RediscoveryApplicationEvidenceRow(
        Guid CandidateId,
        Guid JobApplicationId,
        Guid JobRequestId,
        Guid? JobPostId,
        string? JobPostTitle,
        string? JobPostStatus,
        string RequestCode,
        string JobTitle,
        string DisplayJobTitle,
        string Client,
        string Department,
        string Location,
        string Status,
        string SourceLabel,
        string? CoverLetterText,
        DateTime AppliedAt,
        DateTime? FinalDecisionAt,
        string? FinalDecisionReason,
        DateTime? OfferStartDate);

    private sealed record RediscoveryInterviewEvidenceRow(
        Guid CandidateId,
        Guid InterviewId,
        Guid JobApplicationId,
        string RoundName,
        string Status,
        string? Recommendation,
        int? TechnicalScore,
        int? CommunicationScore,
        int? CultureScore,
        string? FeedbackSummary,
        DateTime StartsAt,
        DateTime? SubmittedAt);

    private sealed record PortalApplicationOfferMeetingRow(
        Guid JobApplicationId,
        DateTime MeetingAt,
        string LocationText,
        string Status,
        string? Notes);

    private sealed record HistoricalApplicationRow(
        Guid CandidateId,
        string DisplayName,
        string Email,
        string CandidateStatus,
        string? CurrentDesignation,
        string? CurrentCompany,
        decimal? ExperienceYears,
        int? NoticePeriodDays,
        Guid JobApplicationId,
        Guid JobRequestId,
        Guid? JobPostId,
        string? JobPostTitle,
        string? JobPostStatus,
        string RequestCode,
        string JobTitle,
        string DisplayJobTitle,
        string Client,
        string Department,
        string Location,
        string Status,
        string SourceLabel,
        DateTime AppliedAt,
        DateTime? FinalDecisionAt,
        string? FinalDecisionReason,
        DateTime? OfferStartDate);

    private sealed record HistoricalCandidateRow(
        Guid CandidateId,
        string DisplayName,
        string Email,
        string CandidateStatus,
        string? CurrentDesignation,
        string? CurrentCompany,
        decimal? ExperienceYears,
        int? NoticePeriodDays);

    private sealed record ActiveRoleUserRow(Guid UserId, string DisplayName, string Email);

    private sealed record JobPostSummaryRow(
        Guid JobPostId,
        string Status,
        string RecruiterOwnerName,
        DateTime UpdatedAt);

    private sealed record JobPostListRow(
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
        DateTime? PublishedAt,
        DateTime? ClosedAt,
        DateTime UpdatedAt);

    private sealed record JobPostRow(
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
        DateTime? PublishedAt,
        DateTime? ClosedAt,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private sealed record JobPostSkillRow(Guid SkillId, string Name, string? Category);

    private sealed record JobPostInterviewRoundRow(
        Guid? JobPostInterviewRoundId,
        Guid? InterviewTemplateRoundId,
        int RoundOrder,
        string Name,
        Guid? OwnerUserId,
        string? OwnerUserName,
        int DurationMinutes,
        string Status);

    private sealed record InterviewTemplateRow(
        Guid InterviewTemplateId,
        string Name,
        string DepartmentName,
        string Description);

    private sealed record InterviewerOptionRow(
        Guid UserId,
        string DisplayName,
        string Email,
        Guid? DepartmentId,
        string? DepartmentName,
        string? Designation,
        string RoleNamesCsv,
        int CompletedInterviewCount,
        bool IsJobDepartmentMatch,
        bool IsDepartmentHod);

    private sealed record JobPostRequestDefaultsRow(
        Guid JobRequestId,
        string RequestCode,
        Guid? DepartmentId,
        Guid? LocationId);

    private sealed record JobPostPublishReadinessRow(int SkillCount, int ActiveRoundCount);

    private sealed record JobPostAccessContextRow(
        Guid JobPostId,
        Guid JobRequestId,
        string Status,
        string RequestCode);

    private sealed record JobRequestContextRow(
        Guid JobRequestId,
        string RequestCode,
        string Title,
        Guid CreatedByUserId,
        int RequiredPositions,
        int FulfilledPositions);

    private sealed record WorkflowRoutingRuleTargetRow(
        string AssignmentType,
        Guid? TargetUserId,
        Guid? TargetGroupId,
        Guid? TargetRoleId);

    private sealed record ReferralDecisionRow(
        Guid JobRequestEmployeeReferralId,
        Guid EmployeeId,
        Guid ReferredByUserId);

    private sealed record FulfillmentProgressRow(int RequiredPositions, int FulfilledPositions);

    private sealed record NotificationTemplateRenderRow(
        Guid EventId,
        Guid? TemplateId,
        string Subject,
        string Body);

    private sealed record NotificationContent(
        Guid EventId,
        Guid? TemplateId,
        string Title,
        string Message);

    private sealed record NotificationRecipientRow(Guid UserId, string Email);

    private sealed record CandidateInvitationContextRow(
        Guid JobRequestId,
        string RequestCode,
        Guid? JobPostId,
        string JobTitle,
        string CompanyName);

    private sealed record PortalInvitationRow(
        Guid CandidateInvitationId,
        Guid JobPostId,
        string JobTitle,
        string CompanyName,
        string Status,
        DateTime ExpiresAtUtc,
        DateTime? UsedAtUtc,
        DateTime? RevokedAtUtc);

    private sealed record TrackedCandidateInvitation(
        Guid CandidateId,
        Guid JobPostId,
        Guid CandidateInvitationId,
        string TokenHash,
        string? JobLink);

    private sealed record PublicPortalContextRow(
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
        byte[]? LogoContent)
    {
        public PublicPortalContext ToDomain()
        {
            return new PublicPortalContext(
                TenantId,
                Slug,
                DisplayName,
                CareerDisplayName,
                CompanyAddress,
                CompanyCity,
                CompanyCountry,
                OfficialEmail,
                OfficialPhone,
                PrimaryColor,
                CandidateLoginRequired,
                CandidateCvFormat,
                PublicJobsEnabled,
                InviteExpiryDays,
                ReapplyCooldownDays,
                LogoFileName,
                LogoContentType,
                LogoContent is { Length: > 0 } ? Convert.ToBase64String(LogoContent) : null);
        }
    }

    private sealed record PortalJobPostRow(
        Guid JobPostId,
        Guid TenantId,
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
        DateTime? PublishedAt);

    private sealed record CandidateMutableRow(
        Guid CandidateId,
        Guid AppUserId,
        string DisplayName,
        string Email);

    private sealed record PortalCandidateProfileRow(
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
        int? NoticePeriodDays);

    private sealed record PortalCandidateProfileDocumentRow(
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
        DateTime UploadedAt,
        string ExtractionStatus,
        bool HasExtractedText,
        string? ExtractedText,
        string? ExtractedTextHashSha256,
        string? ParserVersion,
        DateTime? ExtractedAt,
        string? ExtractionError)
    {
        public PortalCandidateProfileDocument ToDocument()
        {
            return new PortalCandidateProfileDocument(
                CandidateProfileDocumentId,
                CandidateId,
                DocumentType,
                FileName,
                ContentType,
                SizeBytes,
                StorageProvider,
                Utc(UploadedAt),
                ExtractionStatus,
                HasExtractedText,
                ParserVersion,
                ToUtc(ExtractedAt),
                ExtractionError);
        }
    }

    private sealed record AppUserCandidateSeedRow(
        Guid UserId,
        string DisplayName,
        string Email);

    private sealed record CandidateSourceLabelRow(
        Guid CandidateSourceLabelId,
        string Code,
        string DisplayName);

    private sealed record ApplicationIdentityRow(
        Guid JobApplicationId,
        string CurrentStatus);

    private sealed record CandidateInvitationApplicationRow(
        Guid CandidateInvitationId,
        Guid? CandidateId,
        string Status,
        DateTime ExpiresAtUtc,
        DateTime? RevokedAtUtc);

    private sealed record RecruiterApplicationRow(
        Guid JobApplicationId,
        Guid JobPostId,
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
        DateTime AppliedAt);

    private sealed record RecruiterApplicationInterviewRow(
        Guid InterviewId,
        Guid JobApplicationId,
        Guid? JobPostInterviewRoundId,
        string RoundName,
        Guid InterviewerUserId,
        string InterviewerName,
        string InterviewerAccountStatus,
        bool InterviewerIsDeleted,
        string Status,
        DateTime StartsAt,
        int DurationMinutes,
        string? MeetingLink,
        string? LocationText,
        string? Recommendation);

    private sealed record ApplicationActionContextRow(
        Guid JobApplicationId,
        Guid JobRequestId,
        Guid? JobPostId,
        string CurrentStatus,
        string RequestCode,
        string CandidateName);

    private sealed record JobPostRoundSchedulingRow(
        Guid JobPostInterviewRoundId,
        int RoundOrder,
        string Name,
        Guid? OwnerUserId,
        int DurationMinutes);

    private sealed record InterviewTaskRow(
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
        DateTime StartsAt,
        int DurationMinutes,
        string? MeetingLink,
        string? LocationText,
        string Status,
        string? Recommendation,
        int? TechnicalScore,
        int? CommunicationScore,
        int? CultureScore,
        string? FeedbackText,
        DateTime? SubmittedAt);

    private sealed record InterviewQuestionContextRow(
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
        int DurationMinutes,
        string Status,
        DateTime StartsAt,
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
        decimal? ExperienceMaxYears);

    private sealed record InterviewQuestionPriorFeedbackRow(
        Guid InterviewId,
        Guid JobApplicationId,
        string RoundName,
        string Status,
        string? Recommendation,
        int? TechnicalScore,
        int? CommunicationScore,
        int? CultureScore,
        string? FeedbackSummary,
        DateTime? SubmittedAt);

    private sealed record InterviewQuestionBankItemRow(
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
        string FollowUpsJson,
        string EvaluationRubricJson,
        string? SourceTitle,
        string? SourceUrl,
        string ContentHashSha256);

    private sealed record InterviewQuestionRecommendationSetRow(
        Guid RecommendationSetId,
        Guid InterviewId,
        Guid JobApplicationId,
        Guid JobPostInterviewRoundId,
        Guid AiAgentRunId,
        string Model,
        string PromptVersion,
        int VersionNumber,
        string Summary,
        string? Rationale,
        string? RegenerateReason,
        string CoverageJson,
        string Status,
        DateTime GeneratedAt);

    private sealed record InterviewQuestionRecommendationRow(
        Guid QuestionRecommendationId,
        int SortOrder,
        string QuestionText,
        string QuestionType,
        string RoundType,
        string? SkillName,
        string Difficulty,
        string Rationale,
        string ExpectedSignal,
        string FollowUpsJson,
        string EvaluationRubricJson,
        Guid? SourceBankItemId);

    private sealed record InterviewScheduleNotificationContextRow(
        Guid JobApplicationId,
        Guid JobRequestId,
        string CompanyName,
        string RequestCode,
        string JobTitle,
        Guid CandidateUserId,
        string CandidateName,
        string CandidateEmail,
        Guid InterviewerUserId,
        string InterviewerName,
        string InterviewerEmail,
        Guid HiringManagerUserId,
        string HiringManagerName,
        string HiringManagerEmail,
        string RecruiterName,
        string RoundName,
        DateTime StartsAt,
        int DurationMinutes,
        string? MeetingLink,
        string? LocationText);

    private sealed record InterviewParticipantInsertRow(
        Guid InterviewParticipantId,
        Guid TenantId,
        Guid InterviewId,
        Guid? UserId,
        string DisplayName,
        string Email,
        string ParticipantRole,
        bool IsOptional);

    private sealed record CandidateMeetingEventRow(
        Guid InterviewId,
        Guid JobApplicationId,
        Guid JobRequestId,
        Guid? JobPostId,
        string RequestCode,
        string JobTitle,
        string Client,
        string RoundName,
        string Status,
        DateTime StartsAt,
        int DurationMinutes,
        string? MeetingLink,
        string? CalendarProvider,
        string? CalendarEventId,
        string? CalendarEventHtmlLink,
        string? LocationText);

    private sealed record CandidateMeetingParticipantRow(
        Guid InterviewId,
        string DisplayName,
        string Email,
        string Role,
        bool IsOptional);

    private sealed record InterviewFeedbackContextRow(
        Guid InterviewId,
        Guid JobApplicationId,
        Guid JobRequestId,
        Guid InterviewerUserId,
        string InterviewerAccountStatus,
        bool InterviewerIsDeleted,
        string Status,
        string RequestCode,
        string JobTitle,
        string CandidateName,
        string RoundName,
        Guid RecruiterUserId,
        string RecruiterName,
        string RecruiterEmail);

    private sealed record HiringReviewNotificationContextRow(
        string RequestCode,
        string JobTitle,
        string CandidateName,
        Guid HiringManagerUserId);

    private sealed record HiringManagerReviewListRow(
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
        DateTime UpdatedAt,
        string? OfferLetterStatus,
        DateTime? LatestMeetingAt);

    private sealed record HiringManagerDashboardReviewRow(
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
        DateTime UpdatedAt,
        int DaysWaiting,
        int CompletedInterviews,
        decimal? AverageScore,
        int PositiveRecommendations,
        string? OfferLetterStatus,
        int ScheduledMeetingCount,
        DateTime? LatestMeetingAt);

    private sealed record HiringManagerDashboardActivityRow(
        Guid Id,
        Guid JobApplicationId,
        Guid JobRequestId,
        string RequestCode,
        string CandidateName,
        string ActorName,
        string Title,
        string Detail,
        DateTime CreatedAt);

    private sealed record RequirementFitResult(
        string Label,
        decimal Score,
        string Detail);

    private sealed record ReportingManagerRequestContextRow(
        Guid? DepartmentId,
        string DepartmentName);

    private sealed record ReportingManagerOptionRow(
        Guid EmployeeId,
        string DisplayName,
        string Email,
        string? Designation,
        string Department,
        string Location,
        decimal? ExperienceYears,
        bool IsDepartmentMatch,
        int TotalCount);

    private sealed record HiringReviewAccessRow(
        Guid JobApplicationId,
        Guid JobRequestId,
        Guid? JobPostId,
        Guid CandidateId,
        string ApplicationStatus,
        DateTime? FinalOutcomeRecordedAt,
        string? FinalOutcomeReason,
        string SourceLabel,
        string? SourceDetail,
        string? RecruiterNotes,
        string RequestCode,
        string RequestTitle,
        string RequestDescription,
        string JobTitle,
        string? JobPostDescription,
        string Client,
        string Department,
        string Location,
        decimal? ExperienceMinYears,
        decimal? ExperienceMaxYears,
        int RequiredPositions,
        int FulfilledPositions,
        string RequestStatus,
        DateTime? RequestClosedAt,
        string? RequestCloseReason,
        Guid HiringManagerUserId,
        string HiringManagerName,
        string CandidateName,
        string CandidateEmail,
        string CandidateStatus,
        string? CurrentDesignation,
        string? CurrentCompany,
        decimal? ExperienceYears,
        decimal? ExpectedSalaryAmount,
        string? ExpectedSalaryCurrency,
        int? NoticePeriodDays,
        string? CandidateSkillList,
        string? RequestSkillList,
        string? JobPostSkillList,
        string CompanyName,
        string? CompanyAddress,
        string? CompanyCity,
        string? CompanyCountry,
        string? OfficialEmail,
        string? OfficialPhone);

    private sealed record HiringReviewInterviewRow(
        Guid InterviewId,
        Guid? JobPostInterviewRoundId,
        string RoundName,
        string Status,
        string InterviewerName,
        DateTime StartsAt,
        int DurationMinutes,
        string? Recommendation,
        int? TechnicalScore,
        int? CommunicationScore,
        int? CultureScore,
        decimal? AverageScore,
        string? FeedbackText,
        string? SkipReason,
        DateTime? SubmittedAt);

    private sealed record OfferLetterRow(
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
        DateTime? StartDate,
        string? ReportingManager,
        string? WorkLocation,
        string Body,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private sealed record OfferPresentationMeetingRow(
        Guid OfferPresentationMeetingId,
        Guid OfferLetterId,
        Guid JobApplicationId,
        DateTime MeetingAt,
        string LocationText,
        string? Notes,
        string Status,
        DateTime CreatedAt);

    private sealed record OfferAccessRow(
        Guid OfferLetterId,
        Guid JobApplicationId,
        Guid JobRequestId,
        Guid? JobPostId,
        Guid CandidateId,
        string RequestCode,
        string JobTitle,
        string CandidateName,
        string CandidateEmail,
        string CompanyName,
        Guid HiringManagerUserId,
        string HiringManagerName);

    private sealed record CandidateInvitationRecipientRow(
        Guid CandidateId,
        string DisplayName,
        string Email);

    private sealed record WorkflowAssignmentTarget(
        Guid? AssignedToUserId,
        Guid? AssignedToGroupId,
        Guid? AssignedToRoleId,
        string Source)
    {
        public static WorkflowAssignmentTarget ForUser(Guid userId, string source)
        {
            return new WorkflowAssignmentTarget(userId, null, null, source);
        }

        public static WorkflowAssignmentTarget ForGroup(Guid groupId, string source)
        {
            return new WorkflowAssignmentTarget(null, groupId, null, source);
        }

        public static WorkflowAssignmentTarget ForRole(Guid roleId, string source)
        {
            return new WorkflowAssignmentTarget(null, null, roleId, source);
        }
    }

    private sealed record JobRequestRow(
        Guid Id,
        string Code,
        string Title,
        string Client,
        string? ClientContext,
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
        DateTime CreatedAt,
        string? SkillList);
}
