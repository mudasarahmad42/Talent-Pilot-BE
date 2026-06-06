using System.Text.Json;
using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;
using TalentPilot.Application.Admin.AiSettings;
using TalentPilot.Application.Admin.AuditLogs;
using TalentPilot.Application.Admin.CandidateSources;
using TalentPilot.Application.Admin.Departments;
using TalentPilot.Application.Admin.Groups;
using TalentPilot.Application.Admin.HiringPipelines;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Application.Admin.Roles;
using TalentPilot.Application.Admin.Skills;
using TalentPilot.Application.Admin.Users;
using TalentPilot.Application.Admin.Workflows;
using TalentPilot.Application.Notifications;
using TalentPilot.Domain.Access;
using TalentPilot.Domain.Notifications;

namespace TalentPilot.Infrastructure.Persistence.Repositories;

public sealed class DapperAdminCenterRepository :
    IAdminUsersRepository,
    IAdminAccessPoliciesRepository,
    IAdminDepartmentsRepository,
    IAdminGroupsRepository,
    IAdminRolesRepository,
    IAdminSkillsRepository,
    IAdminNotificationsRepository,
    IAdminAuditLogRepository,
    IAdminAiSettingsRepository,
    IAdminCandidateSourcesRepository,
    IAdminWorkflowsRepository,
    IAdminHiringPipelinesRepository,
    IRealtimeNotificationRepository
{
    private const string NotificationWorkerName = "notification-outbox-email";
    private const int NotificationWorkerPollIntervalSeconds = 30;
    private const int NotificationWorkerStaleAfterSeconds = 90;

    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperAdminCenterRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AdminAiRuntimeResponse?> GetRuntimeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                ProviderMode AS Provider,
                LlmModel,
                EmbeddingModel,
                EmbeddingDimensions,
                VectorStore,
                CAST(CASE WHEN ModelSwitchingLocked = 0 THEN 1 ELSE 0 END AS bit) AS RuntimeEditable
            FROM dbo.TenantAiSettings
            WHERE TenantId = @TenantId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AdminAiRuntimeResponse>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AdminAiAgentDefinition>> ListAgentsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                AiAgentDefinitionId AS Id,
                DisplayName,
                Responsibility,
                InputSummary,
                OutputSummary,
                MvpBoundary,
                Enabled
            FROM dbo.AiAgentDefinitions
            WHERE Enabled = 1
            ORDER BY DisplayName;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var agents = await connection.QueryAsync<AdminAiAgentDefinition>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return agents.ToArray();
    }

    public async Task<AdminAiGuardrailSettings?> GetGuardrailsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                HumanReviewRequired,
                AutoRejectEnabled
            FROM dbo.TenantAiSettings
            WHERE TenantId = @TenantId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AdminAiGuardrailSettings>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AdminAiAgentRunListItem>> ListRecentRunsAsync(
        Guid tenantId,
        int count,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@Count)
                runs.AiAgentRunId,
                runs.AiAgentDefinitionId AS AgentId,
                definitions.DisplayName AS AgentName,
                runs.SourceEntityType,
                runs.SourceEntityId,
                runs.ModelName,
                runs.EmbeddingModelName,
                runs.Status,
                runs.StartedAtUtc,
                runs.CompletedAtUtc,
                CASE
                    WHEN runs.CompletedAtUtc IS NULL THEN NULL
                    ELSE DATEDIFF(MILLISECOND, runs.StartedAtUtc, runs.CompletedAtUtc)
                END AS DurationMs,
                runs.OutputSummary,
                runs.InputHash,
                runs.MetadataJson
            FROM dbo.AiAgentRuns AS runs
            INNER JOIN dbo.AiAgentDefinitions AS definitions
                ON definitions.AiAgentDefinitionId = runs.AiAgentDefinitionId
            WHERE runs.TenantId = @TenantId
            ORDER BY runs.StartedAtUtc DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<AiAgentRunLogRow>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, Count = count },
                cancellationToken: cancellationToken));

        return rows.Select(ToAiAgentRunLogItem).ToArray();
    }

    public async Task<AdminCandidateSourcesResponse> ListAsync(
        Guid tenantId,
        AdminCandidateSourcesQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                CandidateSourceLabelId,
                Code,
                DisplayName,
                ReportingCategory,
                Status,
                UpdatedAtUtc
            FROM dbo.CandidateSourceLabels
            WHERE TenantId = @TenantId
              AND (
                  @Search IS NULL
                  OR Code LIKE @SearchLike
                  OR DisplayName LIKE @SearchLike
                  OR ReportingCategory LIKE @SearchLike
                  OR Status LIKE @SearchLike
              )
            ORDER BY DisplayName;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var sources = (await connection.QueryAsync<CandidateSourceLabelRow>(
                new CommandDefinition(
                    sql,
                    SearchParameters(tenantId, query.Search),
                    cancellationToken: cancellationToken)))
            .Select(row => new AdminCandidateSourceListItem(
                row.CandidateSourceLabelId,
                row.Code,
                row.DisplayName,
                row.ReportingCategory,
                row.Status,
                Utc(row.UpdatedAtUtc)))
            .ToArray();

        var items = sources
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();
        var summary = new AdminCandidateSourcesSummary(
            sources.Count(source => source.Status == "Active"),
            sources.Select(source => source.ReportingCategory).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            sources.Count(source => source.Status == "Inactive"));

        return new AdminCandidateSourcesResponse(summary, items, query.Page, query.PageSize, sources.Length);
    }

    public async Task<AdminWorkflowConfigurationResponse> GetConfigurationAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                WorkflowDefinitionId,
                Code,
                Name,
                EntityType,
                Status,
                UpdatedAtUtc
            FROM dbo.WorkflowDefinitions
            WHERE TenantId = @TenantId
            ORDER BY EntityType, Name;

            SELECT
                WorkflowStageId,
                StageKey,
                Name,
                StageOrder,
                IsTerminal,
                Status
            FROM dbo.WorkflowStages
            WHERE TenantId = @TenantId
            ORDER BY StageOrder, Name;

            SELECT
                rr.WorkflowRoutingRuleId,
                rr.WorkflowTransitionId,
                wt.ActionKey,
                wt.Name AS ActionName,
                fs.Name AS FromStage,
                ts.Name AS ToStage,
                rr.AssignmentType,
                CASE rr.AssignmentType
                    WHEN N'User' THEN COALESCE(targetUser.DisplayName, N'Configured user')
                    WHEN N'Group' THEN COALESCE(targetGroup.Name, N'Configured group')
                    WHEN N'Role' THEN COALESCE(targetRole.Name, N'Configured role')
                    WHEN N'DynamicResolver' THEN COALESCE(rr.ResolverKey, N'Dynamic resolver')
                    WHEN N'NoAssignment' THEN N'No assignment'
                    ELSE COALESCE(rr.ResolverKey, N'Unassigned')
                END AS AssignmentTarget,
                COALESCE(rr.ResolverKey, N'') AS ResolverKey,
                rr.Status
            FROM dbo.WorkflowRoutingRules AS rr
            INNER JOIN dbo.WorkflowTransitions AS wt ON wt.WorkflowTransitionId = rr.WorkflowTransitionId
            INNER JOIN dbo.WorkflowStages AS fs ON fs.WorkflowStageId = wt.FromStageId
            INNER JOIN dbo.WorkflowStages AS ts ON ts.WorkflowStageId = wt.ToStageId
            LEFT JOIN dbo.AppUsers AS targetUser ON targetUser.UserId = rr.TargetUserId
            LEFT JOIN dbo.Groups AS targetGroup ON targetGroup.GroupId = rr.TargetGroupId
            LEFT JOIN dbo.Roles AS targetRole ON targetRole.RoleId = rr.TargetRoleId
            WHERE rr.TenantId = @TenantId
            ORDER BY fs.StageOrder, ts.StageOrder, wt.Name;

            SELECT
                jir.JobRequestIntakeRoutingRuleId,
                d.DepartmentId,
                d.Code AS DepartmentCode,
                d.Name AS DepartmentName,
                COALESCE(jir.AssignmentType, N'Fallback') AS AssignmentType,
                jir.TargetUserId,
                jir.TargetGroupId,
                CASE jir.AssignmentType
                    WHEN N'User' THEN COALESCE(targetUser.DisplayName, N'Configured user')
                    WHEN N'Group' THEN COALESCE(targetGroup.Name, N'Configured group')
                    ELSE N'Tenant Admin fallback'
                END AS AssignmentTarget,
                COALESCE(jir.Status, N'Missing') AS Status,
                CASE
                    WHEN jir.JobRequestIntakeRoutingRuleId IS NULL OR jir.Status <> N'Active' THEN CAST(1 AS BIT)
                    ELSE CAST(0 AS BIT)
                END AS UsesTenantAdminFallback
            FROM dbo.Departments AS d
            LEFT JOIN dbo.JobRequestIntakeRoutingRules AS jir
                ON jir.TenantId = d.TenantId
                AND jir.DepartmentId = d.DepartmentId
            LEFT JOIN dbo.AppUsers AS targetUser
                ON targetUser.TenantId = d.TenantId
                AND targetUser.UserId = jir.TargetUserId
            LEFT JOIN dbo.Groups AS targetGroup
                ON targetGroup.TenantId = d.TenantId
                AND targetGroup.GroupId = jir.TargetGroupId
            WHERE d.TenantId = @TenantId
              AND d.Status = N'Active'
            ORDER BY d.Name;

            SELECT COUNT(1)
            FROM dbo.WorkflowDefinitions
            WHERE TenantId = @TenantId
              AND Status = N'Active';

            SELECT COUNT(1)
            FROM dbo.WorkflowStages
            WHERE TenantId = @TenantId
              AND Status = N'Active';

            SELECT COUNT(1)
            FROM dbo.WorkflowTransitions
            WHERE TenantId = @TenantId
              AND Status = N'Active';

            SELECT COUNT(1)
            FROM dbo.WorkflowRoutingRules
            WHERE TenantId = @TenantId
              AND Status = N'Active';

            SELECT COUNT(1)
            FROM dbo.JobRequestIntakeRoutingRules AS jir
            INNER JOIN dbo.Departments AS d
                ON d.TenantId = jir.TenantId
                AND d.DepartmentId = jir.DepartmentId
                AND d.Status = N'Active'
            WHERE jir.TenantId = @TenantId
              AND jir.Status = N'Active';

            SELECT COUNT(1)
            FROM dbo.Departments AS d
            LEFT JOIN dbo.JobRequestIntakeRoutingRules AS jir
                ON jir.TenantId = d.TenantId
                AND jir.DepartmentId = d.DepartmentId
                AND jir.Status = N'Active'
            WHERE d.TenantId = @TenantId
              AND d.Status = N'Active'
              AND jir.JobRequestIntakeRoutingRuleId IS NULL;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        var definitions = (await grid.ReadAsync<WorkflowDefinitionRow>())
            .Select(row => new AdminWorkflowDefinitionItem(
                row.WorkflowDefinitionId,
                row.Code,
                row.Name,
                row.EntityType,
                row.Status,
                Utc(row.UpdatedAtUtc)))
            .ToArray();
        var stages = (await grid.ReadAsync<WorkflowStageRow>())
            .Select(row => new AdminWorkflowStageItem(
                row.WorkflowStageId,
                row.StageKey,
                row.Name,
                row.StageOrder,
                row.IsTerminal,
                row.Status))
            .ToArray();
        var routingRules = (await grid.ReadAsync<WorkflowRoutingRuleRow>())
            .Select(row => new AdminWorkflowRoutingRuleItem(
                row.WorkflowRoutingRuleId,
                row.WorkflowTransitionId,
                row.ActionKey,
                row.ActionName,
                row.FromStage,
                row.ToStage,
                row.AssignmentType,
                row.AssignmentTarget,
                row.ResolverKey,
                row.Status))
            .ToArray();
        var intakeRoutingRules = (await grid.ReadAsync<WorkflowIntakeRoutingRuleRow>())
            .Select(row => new AdminWorkflowIntakeRoutingRuleItem(
                row.JobRequestIntakeRoutingRuleId,
                row.DepartmentId,
                row.DepartmentCode,
                row.DepartmentName,
                row.AssignmentType,
                row.TargetUserId,
                row.TargetGroupId,
                row.AssignmentTarget,
                row.Status,
                row.UsesTenantAdminFallback))
            .ToArray();
        var workflowDefinitionCount = await grid.ReadSingleAsync<int>();
        var activeStageCount = await grid.ReadSingleAsync<int>();
        var activeTransitionCount = await grid.ReadSingleAsync<int>();
        var activeRoutingRuleCount = await grid.ReadSingleAsync<int>();
        var activeIntakeRoutingRuleCount = await grid.ReadSingleAsync<int>();
        var departmentsNeedingIntakeRoutingCount = await grid.ReadSingleAsync<int>();

        return new AdminWorkflowConfigurationResponse(
            new AdminWorkflowSummary(
                workflowDefinitionCount,
                activeStageCount,
                activeTransitionCount,
                activeRoutingRuleCount,
                activeIntakeRoutingRuleCount,
                departmentsNeedingIntakeRoutingCount),
            definitions,
            stages,
            routingRules,
            intakeRoutingRules);
    }

    public async Task UpdateIntakeRoutingAsync(
        Guid tenantId,
        Guid actorUserId,
        UpdateAdminWorkflowIntakeRoutingInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string mergeSql = """
            MERGE dbo.JobRequestIntakeRoutingRules AS target
            USING (VALUES
            (
                @JobRequestIntakeRoutingRuleId,
                @TenantId,
                @DepartmentId,
                @AssignmentType,
                @TargetUserId,
                @TargetGroupId,
                @Status
            ))
            AS source
            (
                JobRequestIntakeRoutingRuleId,
                TenantId,
                DepartmentId,
                AssignmentType,
                TargetUserId,
                TargetGroupId,
                Status
            )
            ON target.TenantId = source.TenantId
               AND target.DepartmentId = source.DepartmentId
            WHEN MATCHED THEN
                UPDATE SET
                    AssignmentType = source.AssignmentType,
                    TargetUserId = source.TargetUserId,
                    TargetGroupId = source.TargetGroupId,
                    Status = source.Status,
                    UpdatedAtUtc = SYSUTCDATETIME(),
                    UpdatedByUserId = @ActorUserId
            WHEN NOT MATCHED THEN
                INSERT
                (
                    JobRequestIntakeRoutingRuleId,
                    TenantId,
                    DepartmentId,
                    AssignmentType,
                    TargetUserId,
                    TargetGroupId,
                    Status,
                    CreatedAtUtc,
                    UpdatedAtUtc,
                    UpdatedByUserId
                )
                VALUES
                (
                    source.JobRequestIntakeRoutingRuleId,
                    source.TenantId,
                    source.DepartmentId,
                    source.AssignmentType,
                    source.TargetUserId,
                    source.TargetGroupId,
                    source.Status,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME(),
                    @ActorUserId
                );
            """;

        foreach (var rule in input.Rules)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                mergeSql,
                new
                {
                    JobRequestIntakeRoutingRuleId = Guid.NewGuid(),
                    TenantId = tenantId,
                    ActorUserId = actorUserId,
                    rule.DepartmentId,
                    rule.AssignmentType,
                    rule.TargetUserId,
                    rule.TargetGroupId,
                    rule.Status
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "WorkflowIntakeRoutingUpdated",
            "JobRequestIntakeRoutingRules",
            null,
            "Department intake routing",
            "Updated department intake routing rules.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> ActiveDepartmentIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> departmentIds,
        CancellationToken cancellationToken)
    {
        if (departmentIds.Count == 0)
        {
            return true;
        }

        const string sql = """
            SELECT COUNT(DISTINCT DepartmentId)
            FROM dbo.Departments
            WHERE TenantId = @TenantId
              AND DepartmentId IN @DepartmentIds
              AND Status = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, DepartmentIds = departmentIds },
            cancellationToken: cancellationToken));

        return count == departmentIds.Distinct().Count();
    }

    public async Task<bool> ActiveUserIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return true;
        }

        const string sql = """
            SELECT COUNT(DISTINCT UserId)
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId IN @UserIds
              AND AccountStatus = N'Active'
              AND DeletedAtUtc IS NULL;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, UserIds = userIds },
            cancellationToken: cancellationToken));

        return count == userIds.Distinct().Count();
    }

    public async Task<bool> ActiveGroupIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
        {
            return true;
        }

        const string sql = """
            SELECT COUNT(DISTINCT GroupId)
            FROM dbo.Groups
            WHERE TenantId = @TenantId
              AND GroupId IN @GroupIds
              AND Status = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, GroupIds = groupIds },
            cancellationToken: cancellationToken));

        return count == groupIds.Distinct().Count();
    }

    public async Task<AdminHiringPipelineTemplatesResponse> ListTemplatesAsync(
        Guid tenantId,
        AdminHiringPipelineTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                template.InterviewTemplateId,
                template.DepartmentId,
                template.Name,
                COALESCE(department.Name, N'All departments') AS DepartmentName,
                COALESCE(template.Description, N'') AS Description,
                template.Status,
                template.UpdatedAtUtc
            FROM dbo.InterviewTemplates AS template
            LEFT JOIN dbo.Departments AS department ON department.DepartmentId = template.DepartmentId
            WHERE template.TenantId = @TenantId
            ORDER BY template.Name;

            SELECT
                round.InterviewTemplateId,
                round.RoundOrder,
                round.Name,
                round.OwnerRoleId,
                COALESCE(role.Name, N'Unassigned') AS OwnerRoleName,
                round.OwnerUserId,
                COALESCE(ownerUser.DisplayName, N'Unassigned') AS OwnerUserName,
                round.DurationMinutes,
                CAST(1 AS BIT) AS IsRequired,
                round.Status
            FROM dbo.InterviewTemplateRounds AS round
            LEFT JOIN dbo.Roles AS role ON role.TenantId = round.TenantId AND role.RoleId = round.OwnerRoleId
            LEFT JOIN dbo.AppUsers AS ownerUser ON ownerUser.TenantId = round.TenantId AND ownerUser.UserId = round.OwnerUserId
            WHERE round.TenantId = @TenantId
            ORDER BY round.InterviewTemplateId, round.RoundOrder;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        var templates = (await grid.ReadAsync<InterviewTemplateRow>()).ToArray();
        var rounds = (await grid.ReadAsync<InterviewTemplateRoundRow>())
            .GroupBy(round => round.InterviewTemplateId)
            .ToDictionary(group => group.Key, group => group.OrderBy(round => round.RoundOrder).ToArray());

        var materialized = templates
            .Select(template => ToHiringPipelineTemplateItem(template, rounds.GetValueOrDefault(template.InterviewTemplateId) ?? []))
            .Where(template => MatchesHiringPipelineSearch(template, query.Search))
            .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var items = materialized
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();
        var activeTemplateIds = templates
            .Where(template => template.Status == "Active")
            .Select(template => template.InterviewTemplateId)
            .ToHashSet();
        var activeRounds = rounds.Values
            .SelectMany(group => group)
            .Where(round => round.Status == "Active" && activeTemplateIds.Contains(round.InterviewTemplateId))
            .ToArray();
        var summary = new AdminHiringPipelineSummary(
            activeTemplateIds.Count,
            templates.Count(template => template.Status == "Active" && template.DepartmentId.HasValue),
            activeRounds.Length,
            activeRounds.Count(round => !round.OwnerUserId.HasValue));

        return new AdminHiringPipelineTemplatesResponse(summary, items, query.Page, query.PageSize, materialized.Length);
    }

    public async Task<AdminHiringPipelineTemplateDetails?> GetHiringPipelineTemplateAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                template.InterviewTemplateId,
                template.DepartmentId,
                template.Name,
                COALESCE(department.Name, N'All departments') AS DepartmentName,
                COALESCE(template.Description, N'') AS Description,
                template.Status,
                template.UpdatedAtUtc
            FROM dbo.InterviewTemplates AS template
            LEFT JOIN dbo.Departments AS department ON department.DepartmentId = template.DepartmentId
            WHERE template.TenantId = @TenantId
              AND template.InterviewTemplateId = @TemplateId;

            SELECT
                round.InterviewTemplateRoundId,
                round.RoundOrder,
                round.Name,
                round.OwnerRoleId,
                COALESCE(role.Name, N'Unassigned') AS OwnerRoleName,
                round.OwnerUserId,
                COALESCE(ownerUser.DisplayName, N'Unassigned') AS OwnerUserName,
                round.DurationMinutes,
                CAST(1 AS BIT) AS IsRequired,
                round.Status
            FROM dbo.InterviewTemplateRounds AS round
            LEFT JOIN dbo.Roles AS role ON role.TenantId = round.TenantId AND role.RoleId = round.OwnerRoleId
            LEFT JOIN dbo.AppUsers AS ownerUser ON ownerUser.TenantId = round.TenantId AND ownerUser.UserId = round.OwnerUserId
            WHERE round.TenantId = @TenantId
              AND round.InterviewTemplateId = @TemplateId
            ORDER BY round.RoundOrder;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, TemplateId = templateId },
            cancellationToken: cancellationToken));

        var template = await grid.ReadSingleOrDefaultAsync<InterviewTemplateDetailsRow>();
        if (template is null)
        {
            return null;
        }

        var rounds = (await grid.ReadAsync<InterviewTemplateDetailsRoundRow>())
            .Select(row => new AdminHiringPipelineTemplateRoundItem(
                row.InterviewTemplateRoundId,
                row.RoundOrder,
                row.Name,
                row.OwnerRoleId,
                row.OwnerRoleName,
                row.OwnerUserId,
                row.OwnerUserName,
                row.DurationMinutes,
                row.IsRequired,
                row.Status))
            .ToArray();

        return new AdminHiringPipelineTemplateDetails(
            template.InterviewTemplateId,
            template.DepartmentId,
            template.Name,
            template.DepartmentName,
            template.Description,
            template.Status,
            Utc(template.UpdatedAtUtc),
            rounds);
    }

    public async Task UpdateTemplateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid templateId,
        UpdateAdminHiringPipelineTemplateInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateTemplateSql = """
            UPDATE dbo.InterviewTemplates
            SET DepartmentId = @DepartmentId,
                Name = @Name,
                Description = @Description,
                Status = @Status,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND InterviewTemplateId = @TemplateId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateTemplateSql,
            new
            {
                TenantId = tenantId,
                TemplateId = templateId,
                input.DepartmentId,
                input.Name,
                Description = EmptyToNull(input.Description),
                input.Status
            },
            transaction,
            cancellationToken: cancellationToken));

        var existingRoundIds = (await connection.QueryAsync<Guid>(new CommandDefinition(
                """
                SELECT InterviewTemplateRoundId
                FROM dbo.InterviewTemplateRounds
                WHERE TenantId = @TenantId
                  AND InterviewTemplateId = @TemplateId;
                """,
                new { TenantId = tenantId, TemplateId = templateId },
                transaction,
                cancellationToken: cancellationToken)))
            .ToHashSet();

        var temporaryOrder = -1;
        foreach (var existingRoundId in existingRoundIds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.InterviewTemplateRounds
                SET RoundOrder = @RoundOrder
                WHERE TenantId = @TenantId
                  AND InterviewTemplateRoundId = @RoundId;
                """,
                new { TenantId = tenantId, RoundId = existingRoundId, RoundOrder = temporaryOrder-- },
                transaction,
                cancellationToken: cancellationToken));
        }

        const string updateRoundSql = """
            UPDATE dbo.InterviewTemplateRounds
            SET RoundOrder = @RoundOrder,
                Name = @Name,
                OwnerRoleId = @OwnerRoleId,
                OwnerUserId = @OwnerUserId,
                DurationMinutes = @DurationMinutes,
                IsRequired = CAST(1 AS BIT),
                Status = @Status
            WHERE TenantId = @TenantId
              AND InterviewTemplateId = @TemplateId
              AND InterviewTemplateRoundId = @RoundId;
            """;
        const string insertRoundSql = """
            INSERT INTO dbo.InterviewTemplateRounds
            (
                InterviewTemplateRoundId,
                TenantId,
                InterviewTemplateId,
                RoundOrder,
                Name,
                OwnerRoleId,
                OwnerUserId,
                DurationMinutes,
                IsRequired,
                Status
            )
            VALUES
            (
                @RoundId,
                @TenantId,
                @TemplateId,
                @RoundOrder,
                @Name,
                @OwnerRoleId,
                @OwnerUserId,
                @DurationMinutes,
                CAST(1 AS BIT),
                @Status
            );
            """;

        var retainedRoundIds = new HashSet<Guid>();
        foreach (var round in input.Rounds.OrderBy(round => round.RoundOrder))
        {
            var roundId = round.InterviewTemplateRoundId.GetValueOrDefault();
            if (roundId != Guid.Empty && existingRoundIds.Contains(roundId))
            {
                retainedRoundIds.Add(roundId);
                await connection.ExecuteAsync(new CommandDefinition(
                    updateRoundSql,
                    new
                    {
                        TenantId = tenantId,
                        TemplateId = templateId,
                        RoundId = roundId,
                        round.RoundOrder,
                        round.Name,
                        round.OwnerRoleId,
                        round.OwnerUserId,
                        round.DurationMinutes,
                        round.Status
                    },
                    transaction,
                    cancellationToken: cancellationToken));
                continue;
            }

            roundId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                insertRoundSql,
                new
                {
                    TenantId = tenantId,
                    TemplateId = templateId,
                    RoundId = roundId,
                    round.RoundOrder,
                    round.Name,
                    round.OwnerRoleId,
                    round.OwnerUserId,
                    round.DurationMinutes,
                    round.Status
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        var nextInactiveOrder = input.Rounds.Count + 1;
        foreach (var omittedRoundId in existingRoundIds.Except(retainedRoundIds))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.InterviewTemplateRounds
                SET RoundOrder = @RoundOrder,
                    Status = N'Inactive'
                WHERE TenantId = @TenantId
                  AND InterviewTemplateRoundId = @RoundId;
                """,
                new { TenantId = tenantId, RoundId = omittedRoundId, RoundOrder = nextInactiveOrder++ },
                transaction,
                cancellationToken: cancellationToken));
        }

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "InterviewTemplateUpdated",
            "InterviewTemplate",
            templateId,
            input.Name,
            "Updated interview template.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CreateTemplateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid templateId,
        UpdateAdminHiringPipelineTemplateInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertTemplateSql = """
            INSERT INTO dbo.InterviewTemplates
            (
                InterviewTemplateId,
                TenantId,
                DepartmentId,
                Name,
                Description,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @TemplateId,
                @TenantId,
                @DepartmentId,
                @Name,
                @Description,
                @Status,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertTemplateSql,
            new
            {
                TenantId = tenantId,
                TemplateId = templateId,
                input.DepartmentId,
                input.Name,
                Description = EmptyToNull(input.Description),
                input.Status
            },
            transaction,
            cancellationToken: cancellationToken));

        const string insertRoundSql = """
            INSERT INTO dbo.InterviewTemplateRounds
            (
                InterviewTemplateRoundId,
                TenantId,
                InterviewTemplateId,
                RoundOrder,
                Name,
                OwnerRoleId,
                OwnerUserId,
                DurationMinutes,
                IsRequired,
                Status
            )
            VALUES
            (
                NEWID(),
                @TenantId,
                @TemplateId,
                @RoundOrder,
                @Name,
                @OwnerRoleId,
                @OwnerUserId,
                @DurationMinutes,
                CAST(1 AS BIT),
                @Status
            );
            """;

        foreach (var round in input.Rounds.OrderBy(round => round.RoundOrder))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertRoundSql,
                new
                {
                    TenantId = tenantId,
                    TemplateId = templateId,
                    round.RoundOrder,
                    round.Name,
                    round.OwnerRoleId,
                    round.OwnerUserId,
                    round.DurationMinutes,
                    round.Status
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "InterviewTemplateCreated",
            "InterviewTemplate",
            templateId,
            input.Name,
            "Created interview template.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> TemplateNameExistsAsync(
        Guid tenantId,
        string name,
        Guid exceptTemplateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.InterviewTemplates
            WHERE TenantId = @TenantId
              AND InterviewTemplateId <> @ExceptTemplateId
              AND LOWER(Name) = LOWER(@Name);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, Name = name.Trim(), ExceptTemplateId = exceptTemplateId },
            cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> DepartmentExistsAsync(
        Guid tenantId,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Departments
            WHERE TenantId = @TenantId
              AND DepartmentId = @DepartmentId
              AND Status = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, DepartmentId = departmentId },
            cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> RoleIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        var ids = roleIds.Where(roleId => roleId != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return true;
        }

        const string sql = """
            SELECT COUNT(DISTINCT RoleId)
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND RoleId IN @RoleIds
              AND Status = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, RoleIds = ids },
            cancellationToken: cancellationToken));

        return count == ids.Length;
    }

    public async Task<AdminUsersResponse> ListAsync(Guid tenantId, AdminUsersQuery query, CancellationToken cancellationToken)
    {
        var users = (await LoadAdminUsersAsync(tenantId, cancellationToken))
            .Where(user => user.IsInternalUser)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            users = users
                .Where(user =>
                    user.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    user.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (user.DepartmentName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (user.ExperienceYears?.ToString("0.0").Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (user.JoiningDate?.ToString("yyyy-MM-dd").Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    user.RoleNames.Any(role => role.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    user.GroupNames.Any(group => group.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        if (query.RoleId.HasValue)
        {
            users = users.Where(user => user.RoleIds.Contains(query.RoleId.Value)).ToArray();
        }

        if (query.GroupId.HasValue)
        {
            users = users.Where(user => user.GroupIds.Contains(query.GroupId.Value)).ToArray();
        }

        if (!string.IsNullOrWhiteSpace(query.AccountStatus))
        {
            users = users
                .Where(user => user.AccountStatus.Equals(query.AccountStatus, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var ordered = users.OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToAdminUserListItem)
            .ToArray();

        var policy = await GetBenchVisibilityPolicySummaryAsync(tenantId, cancellationToken);
        var summary = new AdminUsersSummary(
            users.Length,
            await CountRoutingGroupsAsync(tenantId, cancellationToken),
            policy);

        return new AdminUsersResponse(summary, items, query.Page, query.PageSize, ordered.Length);
    }

    async Task<AdminUserDetails?> IAdminUsersRepository.GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        => await GetUserAsync(tenantId, userId, cancellationToken);

    private async Task<AdminUserDetails?> GetUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = (await LoadAdminUsersAsync(tenantId, cancellationToken))
            .FirstOrDefault(item => item.UserId == userId);

        return user is null ? null : ToAdminUserDetails(user);
    }

    public async Task<Guid?> FindRoleIdByCodeAsync(Guid tenantId, string roleCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT RoleId
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND Code = @RoleCode;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { TenantId = tenantId, RoleCode = roleCode }, cancellationToken: cancellationToken));
    }

    public async Task<bool> EmailExistsAsync(Guid tenantId, string email, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND EmailNormalized = UPPER(@Email)
              AND DeletedAtUtc IS NULL
              AND (@ExceptUserId IS NULL OR UserId <> @ExceptUserId);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, Email = email.Trim(), ExceptUserId = exceptUserId },
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> ActiveRolesExistAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var distinctRoleIds = roleIds.Distinct().ToArray();
        if (distinctRoleIds.Length == 0)
        {
            return false;
        }

        const string sql = """
            SELECT COUNT(DISTINCT RoleId)
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND RoleId IN @RoleIds;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, RoleIds = distinctRoleIds }, cancellationToken: cancellationToken));

        return count == distinctRoleIds.Length;
    }

    public async Task<bool> ActiveGroupsExistAsync(Guid tenantId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken)
    {
        var distinctGroupIds = groupIds.Distinct().ToArray();
        if (distinctGroupIds.Length == 0)
        {
            return true;
        }

        const string sql = """
            SELECT COUNT(DISTINCT GroupId)
            FROM dbo.Groups
            WHERE TenantId = @TenantId
              AND Status = N'Active'
              AND GroupId IN @GroupIds;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, GroupIds = distinctGroupIds }, cancellationToken: cancellationToken));

        return count == distinctGroupIds.Length;
    }

    public async Task<int> CountActiveTenantAdminsAsync(Guid tenantId, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(DISTINCT u.UserId)
            FROM dbo.AppUsers AS u
            INNER JOIN dbo.UserRoles AS ur ON ur.TenantId = u.TenantId AND ur.UserId = u.UserId
            INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId
            WHERE u.TenantId = @TenantId
              AND u.AccountStatus = N'Active'
              AND u.DeletedAtUtc IS NULL
              AND r.Code = @TenantAdminRoleCode
              AND r.Status = N'Active'
              AND (@ExceptUserId IS NULL OR u.UserId <> @ExceptUserId);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    ExceptUserId = exceptUserId,
                    TenantAdminRoleCode = AccessConstants.TenantAdminRoleCode
                },
                cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        SaveAdminUserInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        var userId = Guid.NewGuid();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertUserSql = """
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
                UPPER(@Email),
                @Initials,
                @AccountStatus,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );

            INSERT INTO dbo.UserCredentials
            (
                UserCredentialId,
                TenantId,
                UserId,
                PasswordHash,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                NEWID(),
                @TenantId,
                @UserId,
                NULL,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        var trimmedName = input.DisplayName.Trim();
        await connection.ExecuteAsync(new CommandDefinition(
            insertUserSql,
            new
            {
                UserId = userId,
                TenantId = tenantId,
                DisplayName = trimmedName,
                Email = input.Email.Trim().ToLowerInvariant(),
                Initials = BuildInitials(trimmedName),
                input.AccountStatus
            },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceUserAssignmentsAsync(connection, transaction, tenantId, actorUserId, userId, input.RoleIds, input.GroupIds, cancellationToken);
        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "UserCreated", "User", userId, trimmedName, "Created internal user.", "Admin Center", metadataJson, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return userId;
    }

    public async Task UpdateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid userId,
        SaveAdminUserInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE dbo.AppUsers
            SET DisplayName = @DisplayName,
                Email = @Email,
                EmailNormalized = UPPER(@Email),
                Initials = @Initials,
                AccountStatus = @AccountStatus,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND UserId = @UserId
              AND DeletedAtUtc IS NULL;
            """;

        var trimmedName = input.DisplayName.Trim();
        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new
            {
                TenantId = tenantId,
                UserId = userId,
                DisplayName = trimmedName,
                Email = input.Email.Trim().ToLowerInvariant(),
                Initials = BuildInitials(trimmedName),
                input.AccountStatus
            },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceUserAssignmentsAsync(connection, transaction, tenantId, actorUserId, userId, input.RoleIds, input.GroupIds, cancellationToken);
        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "UserUpdated", "User", userId, trimmedName, "Updated internal user.", "Admin Center", metadataJson, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid userId,
        UpdateAdminUserStatusInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.AppUsers
            SET AccountStatus = @AccountStatus,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND UserId = @UserId
              AND DeletedAtUtc IS NULL;

            SELECT DisplayName
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId = @UserId;
            """;

        var displayName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, UserId = userId, input.AccountStatus },
                transaction,
                cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "UserStatusUpdated",
            "User",
            userId,
            displayName ?? "User",
            $"Changed account status to {input.AccountStatus}.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task InsertInviteNotificationAsync(Guid tenantId, Guid actorUserId, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string displayNameSql = """
            SELECT DisplayName
            FROM dbo.AppUsers
            WHERE TenantId = @TenantId
              AND UserId = @UserId;
            """;

        var displayName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(displayNameSql, new { TenantId = tenantId, UserId = userId }, transaction, cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "UserInviteQueued",
            "User",
            userId,
            displayName ?? "User",
            "Queued user invitation email.",
            "Admin Center",
            "{}",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<BenchVisibilityPolicy?> GetBenchVisibilityPolicyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.BenchVisibilityRoleId AS RoleId,
                r.Name AS RoleName,
                p.UpdatedAtUtc,
                p.UpdatedByUserId
            FROM dbo.TenantAccessPolicies AS p
            INNER JOIN dbo.Roles AS r ON r.RoleId = p.BenchVisibilityRoleId
            WHERE p.TenantId = @TenantId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<BenchPolicyRow>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new BenchVisibilityPolicy(row.RoleId, row.RoleName, Utc(row.UpdatedAtUtc), row.UpdatedByUserId ?? Guid.Empty);
    }

    public async Task<bool> RoleIsActiveAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND RoleId = @RoleId
              AND Status = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, RoleId = roleId }, cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task UpdateBenchVisibilityPolicyAsync(Guid tenantId, Guid actorUserId, Guid roleId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE dbo.TenantAccessPolicies
            SET BenchVisibilityRoleId = @RoleId,
                UpdatedByUserId = @ActorUserId,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new { TenantId = tenantId, RoleId = roleId, ActorUserId = actorUserId },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "BenchVisibilityPolicyUpdated", "AccessPolicy", roleId, "Bench visibility", "Updated bench visibility role.", "Admin Center", "{}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AdminGroupsResponse> ListAsync(Guid tenantId, AdminGroupsQuery query, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                g.GroupId,
                g.Name,
                g.Purpose,
                g.Status,
                COUNT(gm.UserId) AS MemberCount
            FROM dbo.Groups AS g
            LEFT JOIN dbo.GroupMembers AS gm ON gm.TenantId = g.TenantId AND gm.GroupId = g.GroupId
            WHERE g.TenantId = @TenantId
              AND (@Purpose IS NULL OR g.Purpose = @Purpose)
              AND (
                  @Search IS NULL
                  OR g.Name LIKE @SearchLike
                  OR g.Purpose LIKE @SearchLike
                  OR g.Status LIKE @SearchLike
              )
            GROUP BY g.GroupId, g.Name, g.Purpose, g.Status
            ORDER BY g.Name;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<AdminGroupListItem>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, Purpose = EmptyToNull(query.Purpose), Search = EmptyToNull(query.Search), SearchLike = Like(query.Search) },
                cancellationToken: cancellationToken))).ToArray();

        var items = rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        return new AdminGroupsResponse(items, query.Page, query.PageSize, rows.Length);
    }

    public async Task<AdminDepartmentsResponse> ListAsync(
        Guid tenantId,
        AdminDepartmentsQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                d.DepartmentId,
                d.Code,
                d.Name,
                COALESCE(lead.DisplayName, N'Unassigned') AS LeadName,
                COUNT(DISTINCT e.EmployeeId) AS EmployeeCount,
                COUNT(DISTINCT CASE
                    WHEN jr.Status NOT IN (N'Closed', N'Cancelled') THEN jr.JobRequestId
                END) AS OpenJobRequestCount,
                d.Status
            FROM dbo.Departments AS d
            LEFT JOIN dbo.AppUsers AS lead ON lead.TenantId = d.TenantId AND lead.UserId = d.LeadUserId
            LEFT JOIN dbo.Employees AS e ON e.TenantId = d.TenantId AND e.DepartmentId = d.DepartmentId AND e.Status = N'Active'
            LEFT JOIN dbo.JobRequests AS jr ON jr.TenantId = d.TenantId AND jr.DepartmentId = d.DepartmentId
            WHERE d.TenantId = @TenantId
              AND (
                  @Search IS NULL
                  OR d.Code LIKE @SearchLike
                  OR d.Name LIKE @SearchLike
                  OR lead.DisplayName LIKE @SearchLike
                  OR d.Status LIKE @SearchLike
              )
            GROUP BY d.DepartmentId, d.Code, d.Name, lead.DisplayName, d.Status
            ORDER BY d.Name;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<AdminDepartmentListItem>(
            new CommandDefinition(
                sql,
                SearchParameters(tenantId, query.Search),
                cancellationToken: cancellationToken))).ToArray();

        var items = rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();
        var summary = new AdminDepartmentsSummary(
            rows.Count(row => row.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)),
            rows.Sum(row => row.EmployeeCount),
            rows.Sum(row => row.OpenJobRequestCount),
            rows.Count(row => row.Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase)));

        return new AdminDepartmentsResponse(summary, items, query.Page, query.PageSize, rows.Length);
    }

    public async Task<AdminDepartmentListItem?> GetDepartmentAsync(
        Guid tenantId,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                d.DepartmentId,
                d.Code,
                d.Name,
                COALESCE(lead.DisplayName, N'Unassigned') AS LeadName,
                COUNT(DISTINCT e.EmployeeId) AS EmployeeCount,
                COUNT(DISTINCT CASE
                    WHEN jr.Status NOT IN (N'Closed', N'Cancelled') THEN jr.JobRequestId
                END) AS OpenJobRequestCount,
                d.Status
            FROM dbo.Departments AS d
            LEFT JOIN dbo.AppUsers AS lead ON lead.TenantId = d.TenantId AND lead.UserId = d.LeadUserId
            LEFT JOIN dbo.Employees AS e ON e.TenantId = d.TenantId AND e.DepartmentId = d.DepartmentId AND e.Status = N'Active'
            LEFT JOIN dbo.JobRequests AS jr ON jr.TenantId = d.TenantId AND jr.DepartmentId = d.DepartmentId
            WHERE d.TenantId = @TenantId
              AND d.DepartmentId = @DepartmentId
            GROUP BY d.DepartmentId, d.Code, d.Name, lead.DisplayName, d.Status;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AdminDepartmentListItem>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, DepartmentId = departmentId },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> DepartmentCodeOrNameExistsAsync(
        Guid tenantId,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Departments
            WHERE TenantId = @TenantId
              AND (LOWER(Code) = LOWER(@Code) OR LOWER(Name) = LOWER(@Name));
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, Code = code.Trim(), Name = name.Trim() },
            cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateDepartmentInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        var departmentId = Guid.NewGuid();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.Departments
            (
                DepartmentId,
                TenantId,
                Code,
                Name,
                LeadUserId,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @DepartmentId,
                @TenantId,
                @Code,
                @Name,
                NULL,
                @Status,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                DepartmentId = departmentId,
                input.Code,
                input.Name,
                input.Status
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "DepartmentCreated",
            "Department",
            departmentId,
            input.Name,
            "Created department.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return departmentId;
    }

    public async Task<AdminGroupListItem?> GetGroupAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                g.GroupId,
                g.Name,
                g.Purpose,
                g.Status,
                COUNT(gm.UserId) AS MemberCount
            FROM dbo.Groups AS g
            LEFT JOIN dbo.GroupMembers AS gm ON gm.TenantId = g.TenantId AND gm.GroupId = g.GroupId
            WHERE g.TenantId = @TenantId
              AND g.GroupId = @GroupId
            GROUP BY g.GroupId, g.Name, g.Purpose, g.Status;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AdminGroupListItem>(
            new CommandDefinition(sql, new { TenantId = tenantId, GroupId = groupId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> GroupNameExistsAsync(
        Guid tenantId,
        string purpose,
        string name,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Groups
            WHERE TenantId = @TenantId
              AND LOWER(Purpose) = LOWER(@Purpose)
              AND LOWER(Name) = LOWER(@Name);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, Purpose = purpose.Trim(), Name = name.Trim() },
            cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<AdminSkillsResponse> ListAsync(Guid tenantId, AdminSkillsQuery query, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                SkillId,
                Name,
                NormalizedName,
                Category,
                AliasesJson,
                Status,
                UpdatedAtUtc
            FROM dbo.Skills
            WHERE TenantId = @TenantId
              AND (@Category IS NULL OR Category = @Category)
              AND (
                  @Search IS NULL
                  OR Name LIKE @SearchLike
                  OR NormalizedName LIKE @SearchLike
                  OR Category LIKE @SearchLike
                  OR AliasesJson LIKE @SearchLike
                  OR Status LIKE @SearchLike
              )
            ORDER BY Category, Name;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var skills = (await connection.QueryAsync<SkillRow>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TenantId = tenantId,
                        Category = EmptyToNull(query.Category),
                        Search = EmptyToNull(query.Search),
                        SearchLike = Like(query.Search)
                    },
                    cancellationToken: cancellationToken)))
            .Select(ToAdminSkillListItem)
            .ToArray();

        var items = skills
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        var summary = new AdminSkillsSummary(
            skills.Count(skill => skill.Status == "Active"),
            skills.Select(skill => skill.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            skills.Sum(skill => skill.Aliases.Count));

        return new AdminSkillsResponse(summary, items, query.Page, query.PageSize, skills.Length);
    }

    public async Task<AdminSkillListItem?> GetSkillAsync(Guid tenantId, Guid skillId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                SkillId,
                Name,
                NormalizedName,
                Category,
                AliasesJson,
                Status,
                UpdatedAtUtc
            FROM dbo.Skills
            WHERE TenantId = @TenantId
              AND SkillId = @SkillId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SkillRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, SkillId = skillId }, cancellationToken: cancellationToken));

        return row is null ? null : ToAdminSkillListItem(row);
    }

    public async Task<bool> SkillNormalizedNameExistsAsync(
        Guid tenantId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Skills
            WHERE TenantId = @TenantId
              AND LOWER(NormalizedName) = LOWER(@NormalizedName);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, NormalizedName = normalizedName.Trim() },
            cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateSkillInput input,
        string normalizedName,
        string aliasesJson,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        var skillId = Guid.NewGuid();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.Skills
            (
                SkillId,
                TenantId,
                Name,
                NormalizedName,
                Category,
                AliasesJson,
                IsVectorRelevant,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @SkillId,
                @TenantId,
                @Name,
                @NormalizedName,
                @Category,
                @AliasesJson,
                @IsVectorRelevant,
                @Status,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                SkillId = skillId,
                input.Name,
                NormalizedName = normalizedName,
                input.Category,
                AliasesJson = aliasesJson,
                IsVectorRelevant = true,
                input.Status
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "SkillCreated",
            "Skill",
            skillId,
            input.Name,
            "Created skill.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return skillId;
    }

    public async Task UpdateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid skillId,
        UpdateSkillInput input,
        string normalizedName,
        string aliasesJson,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.Skills
            SET
                Name = @Name,
                NormalizedName = @NormalizedName,
                Category = @Category,
                AliasesJson = @AliasesJson,
                Status = @Status,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND SkillId = @SkillId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                SkillId = skillId,
                input.Name,
                NormalizedName = normalizedName,
                input.Category,
                AliasesJson = aliasesJson,
                input.Status
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "SkillUpdated",
            "Skill",
            skillId,
            input.Name,
            "Updated skill.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid skillId,
        string skillName,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            DELETE FROM dbo.EmployeeSkills WHERE TenantId = @TenantId AND SkillId = @SkillId;
            DELETE FROM dbo.CandidateSkills WHERE TenantId = @TenantId AND SkillId = @SkillId;
            DELETE FROM dbo.JobRequestSkills WHERE TenantId = @TenantId AND SkillId = @SkillId;
            DELETE FROM dbo.VectorEmbeddings WHERE TenantId = @TenantId AND EntityType = N'Skill' AND EntityId = @SkillId;
            DELETE FROM dbo.Skills WHERE TenantId = @TenantId AND SkillId = @SkillId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, SkillId = skillId },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "SkillDeleted",
            "Skill",
            skillId,
            skillName,
            "Deleted skill.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateGroupInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        var groupId = Guid.NewGuid();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.Groups
            (
                GroupId,
                TenantId,
                Name,
                Purpose,
                DefaultOwnerUserId,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @GroupId,
                @TenantId,
                @Name,
                @Purpose,
                NULL,
                @Status,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                GroupId = groupId,
                input.Name,
                input.Purpose,
                input.Status
            },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "GroupCreated",
            "Group",
            groupId,
            input.Name,
            "Created routing group.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return groupId;
    }

    public async Task<AdminGroupMembershipResponse> ListMembershipAsync(
        Guid tenantId,
        Guid groupId,
        AdminGroupMembershipQuery query,
        CancellationToken cancellationToken)
    {
        var group = await GetGroupAsync(tenantId, groupId, cancellationToken)
            ?? throw new InvalidOperationException("Group must exist before loading membership.");
        var candidates = await BuildGroupMembershipCandidatesAsync(tenantId, groupId, cancellationToken);
        var searchedUsers = ApplyGroupMembershipSearch(candidates, query.Search);
        var users = ApplyGroupMembershipFilter(searchedUsers, query.Membership);

        var ordered = users
            .OrderByDescending(user => user.IsMember)
            .ThenBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();
        var summary = new AdminGroupMembershipSummary(
            candidates.Count(user => user.IsMember),
            candidates.Count(user => !user.IsMember),
            searchedUsers.Count(user => user.IsMember),
            searchedUsers.Count(user => !user.IsMember));

        return new AdminGroupMembershipResponse(group, summary, items, query.Page, query.PageSize, ordered.Length);
    }

    public async Task<bool> InternalUsersExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var distinctUserIds = userIds.Distinct().ToArray();
        if (distinctUserIds.Length == 0)
        {
            return true;
        }

        var users = await LoadAdminUsersAsync(tenantId, cancellationToken);
        return distinctUserIds.All(userId => users.Any(user => user.UserId == userId && user.IsInternalUser));
    }

    public async Task<UpdateGroupMembersResult> UpdateMembershipAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid groupId,
        UpdateGroupMembersInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        var (userIdsToAdd, userIdsToRemove) = await ResolveGroupMembershipChangesAsync(
            tenantId,
            groupId,
            input,
            cancellationToken);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string groupNameSql = """
            SELECT Name
            FROM dbo.Groups
            WHERE TenantId = @TenantId
              AND GroupId = @GroupId;
            """;

        var groupName = await connection.QuerySingleAsync<string>(new CommandDefinition(
            groupNameSql,
            new { TenantId = tenantId, GroupId = groupId },
            transaction,
            cancellationToken: cancellationToken));

        var removedCount = 0;
        if (userIdsToRemove.Count > 0)
        {
            const string deleteSql = """
                DELETE FROM dbo.GroupMembers
                WHERE TenantId = @TenantId
                  AND GroupId = @GroupId
                  AND UserId IN @UserIds;
                """;

            removedCount = await connection.ExecuteAsync(new CommandDefinition(
                deleteSql,
                new { TenantId = tenantId, GroupId = groupId, UserIds = userIdsToRemove.ToArray() },
                transaction,
                cancellationToken: cancellationToken));
        }

        var addedCount = 0;
        const string insertSql = """
            INSERT INTO dbo.GroupMembers (TenantId, GroupId, UserId, IsDefaultAssignee, CreatedAtUtc)
            SELECT @TenantId, @GroupId, @UserId, 0, SYSUTCDATETIME()
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.GroupMembers
                WHERE TenantId = @TenantId
                  AND GroupId = @GroupId
                  AND UserId = @UserId
            );
            """;

        foreach (var userId in userIdsToAdd)
        {
            addedCount += await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new { TenantId = tenantId, GroupId = groupId, UserId = userId },
                transaction,
                cancellationToken: cancellationToken));
        }

        const string countSql = """
            SELECT COUNT(1)
            FROM dbo.GroupMembers
            WHERE TenantId = @TenantId
              AND GroupId = @GroupId;
            """;

        var memberCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            countSql,
            new { TenantId = tenantId, GroupId = groupId },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "GroupMembershipUpdated",
            "Group",
            groupId,
            groupName,
            $"Updated {groupName} membership. Added {addedCount}, removed {removedCount}.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new UpdateGroupMembersResult(addedCount, removedCount, memberCount);
    }

    private async Task<(HashSet<Guid> UserIdsToAdd, HashSet<Guid> UserIdsToRemove)> ResolveGroupMembershipChangesAsync(
        Guid tenantId,
        Guid groupId,
        UpdateGroupMembersInput input,
        CancellationToken cancellationToken)
    {
        var explicitAddIds = (input.UserIdsToAdd ?? []).Distinct().ToArray();
        var explicitRemoveIds = (input.UserIdsToRemove ?? []).Distinct().ToArray();
        var userIdsToAdd = new HashSet<Guid>();
        var userIdsToRemove = new HashSet<Guid>();

        if (input.BulkSelection is not null)
        {
            var candidates = await BuildGroupMembershipCandidatesAsync(tenantId, groupId, cancellationToken);
            var searchedUsers = ApplyGroupMembershipSearch(candidates, input.BulkSelection.Search);
            var matchingUsers = ApplyGroupMembershipFilter(searchedUsers, input.BulkSelection.Membership);

            if (string.Equals(input.BulkSelection.Mode, "AddMatching", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var user in matchingUsers.Where(user => !user.IsMember))
                {
                    userIdsToAdd.Add(user.UserId);
                }
            }
            else if (string.Equals(input.BulkSelection.Mode, "RemoveMatching", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var user in matchingUsers.Where(user => user.IsMember))
                {
                    userIdsToRemove.Add(user.UserId);
                }
            }
        }

        foreach (var userId in explicitAddIds)
        {
            userIdsToRemove.Remove(userId);
            userIdsToAdd.Add(userId);
        }

        foreach (var userId in explicitRemoveIds)
        {
            userIdsToAdd.Remove(userId);
            userIdsToRemove.Add(userId);
        }

        return (userIdsToAdd, userIdsToRemove);
    }

    public async Task<AdminRolesResponse> ListAsync(Guid tenantId, AdminRolesQuery query, CancellationToken cancellationToken)
    {
        var roles = await LoadRolesAsync(tenantId, cancellationToken);

        if (!query.IncludeInactive)
        {
            roles = roles.Where(role => role.Status == "Active").ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            roles = roles
                .Where(role => role.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var ordered = roles.OrderBy(role => role.Priority).ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToRoleSummary)
            .ToArray();

        var summary = new AdminRolesSummary(
            roles.Count(role => role.Status == "Active"),
            roles.Count(role => role.Type == "Tenant"),
            roles.Count(role => role.Type == "Custom"));

        return new AdminRolesResponse(summary, items, query.Page, query.PageSize, ordered.Length);
    }

    async Task<RoleDetails?> IAdminRolesRepository.GetAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
        => await GetRoleAsync(tenantId, roleId, cancellationToken);

    private async Task<RoleDetails?> GetRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        var role = (await LoadRolesAsync(tenantId, cancellationToken))
            .FirstOrDefault(item => item.RoleId == roleId);

        return role is null ? null : ToRoleDetails(role);
    }

    public async Task<IReadOnlyList<PermissionCatalogItem>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT PermissionId, DisplayName, GroupName, Description, Status
            FROM dbo.Permissions
            ORDER BY GroupName, DisplayName;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return (await connection.QueryAsync<PermissionCatalogItem>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))).ToArray();
    }

    public async Task<bool> PermissionIdsExistAsync(IReadOnlyCollection<string> permissionIds, CancellationToken cancellationToken)
    {
        var distinctIds = permissionIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (distinctIds.Length == 0)
        {
            return false;
        }

        const string sql = """
            SELECT COUNT(DISTINCT PermissionId)
            FROM dbo.Permissions
            WHERE Status = N'Active'
              AND PermissionId IN @PermissionIds;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { PermissionIds = distinctIds }, cancellationToken: cancellationToken));

        return count == distinctIds.Length;
    }

    public async Task<bool> RoleNameExistsAsync(Guid tenantId, string name, Guid? exceptRoleId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND LOWER(Name) = LOWER(@Name)
              AND (@ExceptRoleId IS NULL OR RoleId <> @ExceptRoleId);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, Name = name.Trim(), ExceptRoleId = exceptRoleId },
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        SaveRoleInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        var roleId = Guid.NewGuid();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertRoleSql = """
            INSERT INTO dbo.Roles
            (
                RoleId,
                TenantId,
                Code,
                Name,
                Type,
                Scope,
                Priority,
                IsProtected,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                @RoleId,
                @TenantId,
                @Code,
                @Name,
                N'Custom',
                @Scope,
                @Priority,
                0,
                @Status,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
            """;

        var roleName = input.Name.Trim();
        await connection.ExecuteAsync(new CommandDefinition(
            insertRoleSql,
            new
            {
                RoleId = roleId,
                TenantId = tenantId,
                Code = BuildRoleCode(roleName),
                Name = roleName,
                input.Scope,
                input.Priority,
                input.Status
            },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceRolePermissionsAsync(connection, transaction, roleId, input.PermissionIds, cancellationToken);
        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "RoleCreated", "Role", roleId, roleName, "Created role.", "Admin Center", metadataJson, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return roleId;
    }

    public async Task UpdateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid roleId,
        SaveRoleInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateRoleSql = """
            UPDATE dbo.Roles
            SET Name = @Name,
                Scope = @Scope,
                Priority = @Priority,
                Status = @Status,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND RoleId = @RoleId
              AND IsProtected = 0;
            """;

        var roleName = input.Name.Trim();
        await connection.ExecuteAsync(new CommandDefinition(
            updateRoleSql,
            new { TenantId = tenantId, RoleId = roleId, Name = roleName, input.Scope, input.Priority, input.Status },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceRolePermissionsAsync(connection, transaction, roleId, input.PermissionIds, cancellationToken);
        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "RoleUpdated", "Role", roleId, roleName, "Updated role.", "Admin Center", metadataJson, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid roleId,
        string status,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.Roles
            SET Status = @Status,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND RoleId = @RoleId
              AND IsProtected = 0;

            SELECT Name
            FROM dbo.Roles
            WHERE TenantId = @TenantId
              AND RoleId = @RoleId;
            """;

        var roleName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, RoleId = roleId, Status = status },
                transaction,
                cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "RoleStatusUpdated", "Role", roleId, roleName ?? "Role", $"Changed role status to {status}.", "Admin Center", metadataJson, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PermissionResolutionPolicy?> GetPermissionResolutionPolicyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                PermissionResolutionMode AS Mode,
                UpdatedAtUtc,
                UpdatedByUserId
            FROM dbo.TenantAccessPolicies
            WHERE TenantId = @TenantId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PermissionPolicyRow>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new PermissionResolutionPolicy(row.Mode, Utc(row.UpdatedAtUtc), row.UpdatedByUserId ?? Guid.Empty);
    }

    public async Task UpdatePermissionResolutionPolicyAsync(Guid tenantId, Guid actorUserId, string mode, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE dbo.TenantAccessPolicies
            SET PermissionResolutionMode = @Mode,
                UpdatedByUserId = @ActorUserId,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new { TenantId = tenantId, ActorUserId = actorUserId, Mode = mode },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "PermissionResolutionPolicyUpdated", "AccessPolicy", tenantId, "Permission resolution", $"Updated permission resolution to {mode}.", "Admin Center", "{}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<RoleUserAssignmentPreview> PreviewUserAssignmentsAsync(
        Guid tenantId,
        Guid roleId,
        RoleUserAssignmentFilterInput input,
        CancellationToken cancellationToken)
    {
        var matching = await FindUsersForRoleAssignmentAsync(tenantId, input, cancellationToken);
        var assignable = matching.Where(user => !user.RoleIds.Contains(roleId)).ToArray();
        var sample = assignable.Take(25).Select(ToRoleUserAssignmentPreviewItem).ToArray();

        return new RoleUserAssignmentPreview(
            matching.Length,
            matching.Length - assignable.Length,
            assignable.Length,
            sample);
    }

    public async Task<BulkAssignRoleUsersResponse> BulkAssignUsersAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid roleId,
        BulkAssignRoleUsersInput input,
        CancellationToken cancellationToken)
    {
        var matching = await FindUsersForRoleAssignmentAsync(tenantId, input.Filters, cancellationToken);
        var targetUsers = input.SelectionMode == "SelectedUsers"
            ? matching.Where(user => input.SelectedUserIds?.Contains(user.UserId) == true).ToArray()
            : matching;

        var assignable = targetUsers.Where(user => !user.RoleIds.Contains(roleId)).ToArray();
        var batchId = Guid.NewGuid();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertAssignmentSql = """
            IF NOT EXISTS (
                SELECT 1
                FROM dbo.UserRoles
                WHERE TenantId = @TenantId
                  AND UserId = @UserId
                  AND RoleId = @RoleId
            )
            BEGIN
                INSERT INTO dbo.UserRoles (TenantId, UserId, RoleId, AssignedByUserId, CreatedAtUtc)
                VALUES (@TenantId, @UserId, @RoleId, @ActorUserId, SYSUTCDATETIME());
            END;
            """;

        foreach (var user in assignable)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertAssignmentSql,
                new { TenantId = tenantId, UserId = user.UserId, RoleId = roleId, ActorUserId = actorUserId },
                transaction,
                cancellationToken: cancellationToken));
        }

        const string insertBatchSql = """
            INSERT INTO dbo.RoleAssignmentBatches
            (
                RoleAssignmentBatchId,
                TenantId,
                RoleId,
                FilterJson,
                SelectionMode,
                SelectedUserIdsJson,
                MatchedCount,
                AssignedCount,
                SkippedCount,
                CreatedByUserId,
                CreatedAtUtc
            )
            VALUES
            (
                @BatchId,
                @TenantId,
                @RoleId,
                @FilterJson,
                @SelectionMode,
                @SelectedUserIdsJson,
                @MatchedCount,
                @AssignedCount,
                @SkippedCount,
                @ActorUserId,
                SYSUTCDATETIME()
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertBatchSql,
            new
            {
                BatchId = batchId,
                TenantId = tenantId,
                RoleId = roleId,
                FilterJson = JsonSerializer.Serialize(input.Filters),
                input.SelectionMode,
                SelectedUserIdsJson = input.SelectedUserIds is null ? null : JsonSerializer.Serialize(input.SelectedUserIds),
                MatchedCount = matching.Length,
                AssignedCount = assignable.Length,
                SkippedCount = targetUsers.Length - assignable.Length,
                ActorUserId = actorUserId
            },
            transaction,
            cancellationToken: cancellationToken));

        var roleName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Name FROM dbo.Roles WHERE TenantId = @TenantId AND RoleId = @RoleId;",
                new { TenantId = tenantId, RoleId = roleId },
                transaction,
                cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "RoleBulkAssigned", "Role", roleId, roleName ?? "Role", $"Bulk assigned {roleName ?? "role"} to {assignable.Length} users.", "Admin Center", "{}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BulkAssignRoleUsersResponse(batchId, matching.Length, assignable.Length, targetUsers.Length - assignable.Length);
    }

    public async Task<AdminNotificationEventsResponse> ListEventsAsync(
        Guid tenantId,
        AdminNotificationEventsQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                e.NotificationEventId AS EventId,
                e.EventCode,
                e.Name,
                e.DefaultRecipientType AS Recipient,
                COALESCE(t.Name, N'No email template') AS TemplateName,
                e.Status AS LifecycleStatus,
                CASE
                    WHEN t.UpdatedAtUtc IS NOT NULL AND t.UpdatedAtUtc > e.UpdatedAtUtc THEN t.UpdatedAtUtc
                    ELSE e.UpdatedAtUtc
                END AS UpdatedAtUtc
            FROM dbo.NotificationEvents AS e
            OUTER APPLY
            (
                SELECT TOP (1) template.Name, template.UpdatedAtUtc
                FROM dbo.NotificationTemplates AS template
                WHERE template.TenantId = e.TenantId
                  AND template.NotificationEventId = e.NotificationEventId
                ORDER BY template.UpdatedAtUtc DESC
            ) AS t
            WHERE e.TenantId = @TenantId
              AND (
                    @Search IS NULL
                    OR e.EventCode LIKE @SearchLike
                    OR e.Name LIKE @SearchLike
                  )
            ORDER BY e.EventCode;

            SELECT
                COUNT(CASE WHEN Status = N'Active' THEN 1 END) AS ActiveEventCount
            FROM dbo.NotificationEvents
            WHERE TenantId = @TenantId;

            SELECT COUNT(1)
            FROM dbo.NotificationTemplates
            WHERE TenantId = @TenantId
              AND Status = N'Active';

            SELECT COUNT(1)
            FROM dbo.NotificationOutbox
            WHERE TenantId = @TenantId
              AND Status = N'Pending';

            SELECT COUNT(1)
            FROM dbo.NotificationOutbox
            WHERE TenantId = @TenantId
              AND Status = N'Sent';

            SELECT COUNT(1)
            FROM dbo.NotificationOutbox
            WHERE TenantId = @TenantId
              AND Status = N'Failed';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            SearchParameters(tenantId, query.Search),
            cancellationToken: cancellationToken));

        var rows = (await grid.ReadAsync<NotificationEventRow>()).ToArray();
        var activeEventCount = await grid.ReadSingleAsync<int>();
        var editableTemplateCount = await grid.ReadSingleAsync<int>();
        var pendingOutboxCount = await grid.ReadSingleAsync<int>();
        var sentOutboxCount = await grid.ReadSingleAsync<int>();
        var failedOutboxCount = await grid.ReadSingleAsync<int>();

        var items = rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToNotificationEventListItem)
            .ToArray();

        return new AdminNotificationEventsResponse(
            new AdminNotificationEventsSummary(activeEventCount, editableTemplateCount, pendingOutboxCount, sentOutboxCount, failedOutboxCount),
            items,
            query.Page,
            query.PageSize,
            rows.Length);
    }

    public async Task<AdminNotificationEventDetails?> GetEventAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                NotificationEventId AS EventId,
                EventCode,
                Name,
                DefaultRecipientType AS Recipient,
                Status AS LifecycleStatus
            FROM dbo.NotificationEvents
            WHERE TenantId = @TenantId
              AND NotificationEventId = @EventId;

            SELECT
                t.NotificationTemplateId AS TemplateId,
                e.EventCode,
                t.Name,
                t.Recipient,
                t.Subject,
                t.Body,
                t.AllowedVariablesJson,
                t.Status AS LifecycleStatus,
                t.UpdatedAtUtc,
                t.UpdatedByUserId
            FROM dbo.NotificationTemplates AS t
            INNER JOIN dbo.NotificationEvents AS e ON e.NotificationEventId = t.NotificationEventId
            WHERE t.TenantId = @TenantId
              AND t.NotificationEventId = @EventId
            ORDER BY t.Name;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, EventId = eventId },
            cancellationToken: cancellationToken));

        var row = await grid.ReadSingleOrDefaultAsync<NotificationEventDetailsRow>();
        if (row is null)
        {
            return null;
        }

        var templates = (await grid.ReadAsync<NotificationTemplateRow>())
            .Select(ToNotificationTemplateSummary)
            .ToArray();

        return new AdminNotificationEventDetails(row.EventId, row.EventCode, row.Name, row.Recipient, row.LifecycleStatus, templates);
    }

    public async Task<AdminNotificationTemplatesResponse> ListTemplatesAsync(
        Guid tenantId,
        AdminNotificationTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                t.NotificationTemplateId AS TemplateId,
                e.EventCode,
                t.Name,
                t.Recipient,
                t.Subject,
                t.Body,
                t.AllowedVariablesJson,
                t.Status AS LifecycleStatus,
                t.UpdatedAtUtc,
                t.UpdatedByUserId
            FROM dbo.NotificationTemplates AS t
            INNER JOIN dbo.NotificationEvents AS e ON e.NotificationEventId = t.NotificationEventId
            WHERE t.TenantId = @TenantId
              AND (
                    @Search IS NULL
                    OR t.Name LIKE @SearchLike
                    OR e.EventCode LIKE @SearchLike
                    OR t.Subject LIKE @SearchLike
                    OR t.Recipient LIKE @SearchLike
                  )
            ORDER BY t.Name;

            SELECT COUNT(1)
            FROM dbo.NotificationTemplates AS t
            INNER JOIN dbo.NotificationEvents AS e ON e.NotificationEventId = t.NotificationEventId
            WHERE t.TenantId = @TenantId
              AND (
                    @Search IS NULL
                    OR t.Name LIKE @SearchLike
                    OR e.EventCode LIKE @SearchLike
                    OR t.Subject LIKE @SearchLike
                    OR t.Recipient LIKE @SearchLike
                  );

            SELECT
                COUNT(CASE WHEN Status = N'Active' THEN 1 END) AS ActiveEventCount
            FROM dbo.NotificationEvents
            WHERE TenantId = @TenantId;

            SELECT COUNT(1)
            FROM dbo.NotificationTemplates
            WHERE TenantId = @TenantId
              AND Status = N'Active';

            SELECT COUNT(1)
            FROM dbo.NotificationOutbox
            WHERE TenantId = @TenantId
              AND Status = N'Pending';

            SELECT COUNT(1)
            FROM dbo.NotificationOutbox
            WHERE TenantId = @TenantId
              AND Status = N'Sent';

            SELECT COUNT(1)
            FROM dbo.NotificationOutbox
            WHERE TenantId = @TenantId
              AND Status = N'Failed';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            SearchParameters(tenantId, query.Search),
            cancellationToken: cancellationToken));

        var rows = (await grid.ReadAsync<NotificationTemplateRow>())
            .Select(ToNotificationTemplateSummary)
            .ToArray();
        var totalCount = await grid.ReadSingleAsync<int>();
        var activeEventCount = await grid.ReadSingleAsync<int>();
        var editableTemplateCount = await grid.ReadSingleAsync<int>();
        var pendingOutboxCount = await grid.ReadSingleAsync<int>();
        var sentOutboxCount = await grid.ReadSingleAsync<int>();
        var failedOutboxCount = await grid.ReadSingleAsync<int>();

        var items = rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        var summary = new AdminNotificationEventsSummary(
            activeEventCount,
            editableTemplateCount,
            pendingOutboxCount,
            sentOutboxCount,
            failedOutboxCount);

        return new AdminNotificationTemplatesResponse(summary, items, query.Page, query.PageSize, totalCount);
    }

    public async Task<AdminNotificationOutboxResponse> ListOutboxAsync(
        Guid tenantId,
        AdminNotificationOutboxQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.NotificationOutbox AS o
            INNER JOIN dbo.NotificationEvents AS e ON e.NotificationEventId = o.NotificationEventId
            LEFT JOIN dbo.NotificationTemplates AS t ON t.NotificationTemplateId = o.NotificationTemplateId
            LEFT JOIN dbo.AppUsers AS recipient ON recipient.UserId = o.RecipientUserId
            WHERE o.TenantId = @TenantId
              AND o.Channel = N'Email'
              AND (@Status IS NULL OR o.Status = @Status)
              AND (
                    @Search IS NULL
                    OR o.RecipientEmail LIKE @SearchLike
                    OR recipient.DisplayName LIKE @SearchLike
                    OR e.EventCode LIKE @SearchLike
                    OR e.Name LIKE @SearchLike
                    OR JSON_VALUE(o.PayloadJson, '$.subject') LIKE @SearchLike
                    OR o.PayloadJson LIKE @SearchLike
                  );

            SELECT
                o.NotificationOutboxId AS OutboxId,
                e.EventCode,
                e.Name AS EventName,
                COALESCE(t.Name, N'Application-composed email') AS TemplateName,
                N'Talent Pilot workflow' AS SenderDisplayName,
                recipient.DisplayName AS RecipientDisplayName,
                COALESCE(o.RecipientEmail, recipient.Email) AS RecipientEmail,
                o.Channel,
                o.Status,
                o.AttemptCount,
                o.AvailableAtUtc,
                o.CreatedAtUtc,
                o.UpdatedAtUtc,
                o.ProcessedAtUtc,
                o.LastError,
                COALESCE(JSON_VALUE(o.PayloadJson, '$.subject'), N'(No subject)') AS Subject,
                COALESCE(JSON_VALUE(o.PayloadJson, '$.body'), N'') AS Body,
                JSON_VALUE(o.PayloadJson, '$.entityType') AS EntityType,
                JSON_VALUE(o.PayloadJson, '$.entityId') AS EntityId
            FROM dbo.NotificationOutbox AS o
            INNER JOIN dbo.NotificationEvents AS e ON e.NotificationEventId = o.NotificationEventId
            LEFT JOIN dbo.NotificationTemplates AS t ON t.NotificationTemplateId = o.NotificationTemplateId
            LEFT JOIN dbo.AppUsers AS recipient ON recipient.UserId = o.RecipientUserId
            WHERE o.TenantId = @TenantId
              AND o.Channel = N'Email'
              AND (@Status IS NULL OR o.Status = @Status)
              AND (
                    @Search IS NULL
                    OR o.RecipientEmail LIKE @SearchLike
                    OR recipient.DisplayName LIKE @SearchLike
                    OR e.EventCode LIKE @SearchLike
                    OR e.Name LIKE @SearchLike
                    OR JSON_VALUE(o.PayloadJson, '$.subject') LIKE @SearchLike
                    OR o.PayloadJson LIKE @SearchLike
                  )
            ORDER BY o.CreatedAtUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var parameters = new
        {
            TenantId = tenantId,
            Status = EmptyToNull(query.Status),
            Search = EmptyToNull(query.Search),
            SearchLike = Like(query.Search),
            Offset = (Math.Max(1, query.Page) - 1) * query.PageSize,
            query.PageSize
        };

        await using var connection = _connectionFactory.CreateConnection();
        int totalCount;
        AdminNotificationOutboxItem[] items;
        using (var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken)))
        {
            totalCount = await grid.ReadSingleAsync<int>();
            items = (await grid.ReadAsync<NotificationOutboxRow>())
                .Select(ToNotificationOutboxItem)
                .ToArray();
        }

        var workerStatus = await ReadNotificationWorkerStatusAsync(connection, tenantId, cancellationToken);
        return new AdminNotificationOutboxResponse(workerStatus, items, query.Page, query.PageSize, totalCount);
    }

    public async Task<AdminNotificationOutboxItem?> GetOutboxItemAsync(
        Guid tenantId,
        Guid outboxId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var row = await ReadNotificationOutboxRowAsync(connection, tenantId, outboxId, cancellationToken);
        return row is null ? null : ToNotificationOutboxItem(row);
    }

    public async Task<AdminNotificationOutboxItem?> RequeueOutboxEmailAsync(
        Guid tenantId,
        Guid outboxId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.NotificationOutbox
            SET Status = N'Pending',
                AvailableAtUtc = SYSUTCDATETIME(),
                ProcessedAtUtc = NULL,
                LastError = NULL,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND NotificationOutboxId = @OutboxId
              AND Channel = N'Email'
              AND Status = N'Failed';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, OutboxId = outboxId },
            cancellationToken: cancellationToken));
        if (affectedRows == 0)
        {
            return null;
        }

        var row = await ReadNotificationOutboxRowAsync(connection, tenantId, outboxId, cancellationToken);
        return row is null ? null : ToNotificationOutboxItem(row);
    }

    public async Task<NotificationTemplateSummary?> GetTemplateAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                t.NotificationTemplateId AS TemplateId,
                e.EventCode,
                t.Name,
                t.Recipient,
                t.Subject,
                t.Body,
                t.AllowedVariablesJson,
                t.Status AS LifecycleStatus,
                t.UpdatedAtUtc,
                t.UpdatedByUserId
            FROM dbo.NotificationTemplates AS t
            INNER JOIN dbo.NotificationEvents AS e ON e.NotificationEventId = t.NotificationEventId
            WHERE t.TenantId = @TenantId
              AND t.NotificationTemplateId = @TemplateId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<NotificationTemplateRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, TemplateId = templateId }, cancellationToken: cancellationToken));

        return row is null ? null : ToNotificationTemplateSummary(row);
    }

    public async Task UpdateTemplateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid templateId,
        UpdateNotificationTemplateInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE dbo.NotificationTemplates
            SET Subject = @Subject,
                Body = @Body,
                UpdatedByUserId = @ActorUserId,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND NotificationTemplateId = @TemplateId;

            SELECT Name
            FROM dbo.NotificationTemplates
            WHERE TenantId = @TenantId
              AND NotificationTemplateId = @TemplateId;
            """;

        var templateName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                updateSql,
                new { TenantId = tenantId, ActorUserId = actorUserId, TemplateId = templateId, input.Subject, input.Body },
                transaction,
                cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "NotificationTemplateUpdated", "NotificationTemplate", templateId, templateName ?? "Notification template", "Updated notification template.", "Admin Center", metadataJson, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordTestEmailSentAsync(
        Guid tenantId,
        Guid actorUserId,
        string recipientEmail,
        string providerMessageId,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "NotificationTestEmailSent",
            "NotificationTestEmail",
            null,
            "Notification test email",
            $"Sent test email to {recipientEmail}.",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordRealtimeTestNotificationSentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid notificationId,
        int connectedClientCount,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await InsertAuditAsync(
            connection,
            transaction,
            tenantId,
            actorUserId,
            "NotificationRealtimeTestSent",
            "RealtimeNotification",
            notificationId,
            "Realtime test notification",
            $"Sent realtime test notification to {connectedClientCount} connected client(s).",
            "Admin Center",
            metadataJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PersistedRealtimeNotification>> InsertForUsersAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> recipientUserIds,
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken)
    {
        var uniqueRecipientUserIds = recipientUserIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (uniqueRecipientUserIds.Length == 0)
        {
            return [];
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var eventCode = notification.Metadata.TryGetValue("eventCode", out var configuredEventCode) &&
                        !string.IsNullOrWhiteSpace(configuredEventCode)
            ? configuredEventCode.Trim()
            : NotificationEventCodes.RealtimeNotification;
        var eventId = await EnsureNotificationEventAsync(connection, transaction, tenantId, eventCode, cancellationToken);
        var metadataJson = JsonSerializer.Serialize(notification.Metadata);
        var entityId = Guid.TryParse(notification.EntityId, out var parsedEntityId)
            ? parsedEntityId
            : (Guid?)null;
        var rows = uniqueRecipientUserIds
            .Select(userId => new
            {
                NotificationRecipientId = Guid.NewGuid(),
                TenantId = tenantId,
                NotificationEventId = eventId,
                RecipientUserId = userId,
                Title = Truncate(notification.Title, 200),
                Message = Truncate(notification.Message, 1000),
                Category = Truncate(notification.Category, 80),
                Severity = Truncate(notification.Severity, 20),
                EntityType = Truncate(notification.EntityType, 80),
                EntityId = entityId,
                MetadataJson = metadataJson,
                CreatedAtUtc = notification.CreatedAtUtc.UtcDateTime
            })
            .ToArray();

        const string insertSql = """
            INSERT INTO dbo.NotificationRecipients
            (
                NotificationRecipientId,
                TenantId,
                NotificationEventId,
                RecipientUserId,
                Title,
                Message,
                Category,
                Severity,
                EntityType,
                EntityId,
                MetadataJson,
                CreatedAtUtc
            )
            VALUES
            (
                @NotificationRecipientId,
                @TenantId,
                @NotificationEventId,
                @RecipientUserId,
                @Title,
                @Message,
                @Category,
                @Severity,
                @EntityType,
                @EntityId,
                @MetadataJson,
                @CreatedAtUtc
            );
            """;

        await connection.ExecuteAsync(new CommandDefinition(insertSql, rows, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);

        return rows
            .Select(row => new PersistedRealtimeNotification(row.RecipientUserId, row.NotificationRecipientId))
            .ToArray();
    }

    public async Task UpdateEventStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid eventId,
        string status,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE dbo.NotificationEvents
            SET Status = @Status,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND NotificationEventId = @EventId;

            SELECT Name
            FROM dbo.NotificationEvents
            WHERE TenantId = @TenantId
              AND NotificationEventId = @EventId;
            """;

        var eventName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                updateSql,
                new { TenantId = tenantId, EventId = eventId, Status = status },
                transaction,
                cancellationToken: cancellationToken));

        await InsertAuditAsync(connection, transaction, tenantId, actorUserId, "NotificationEventStatusUpdated", "NotificationEvent", eventId, eventName ?? "Notification event", $"Changed notification event status to {status}.", "Admin Center", metadataJson, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AdminAuditLogListResponse> ListAsync(Guid tenantId, AdminAuditLogQuery query, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                AuditLogId AS Id,
                OccurredAtUtc,
                ActorDisplayName,
                EventSummary,
                RecordLabel,
                Area
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND (@Area IS NULL OR Area = @Area)
              AND (@ActorId IS NULL OR ActorUserId = @ActorId)
              AND (@EntityType IS NULL OR EntityType = @EntityType)
              AND (@EntityId IS NULL OR EntityId = @EntityId)
              AND (
                    @Search IS NULL
                    OR EventSummary LIKE @SearchLike
                    OR RecordLabel LIKE @SearchLike
                    OR ActorDisplayName LIKE @SearchLike
                  )
            ORDER BY OccurredAtUtc DESC;

            SELECT COUNT(1)
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND CONVERT(date, OccurredAtUtc) = CONVERT(date, SYSUTCDATETIME());

            SELECT COUNT(1)
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND Area = N'Admin Center';

            SELECT COUNT(1)
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND Area = N'Workflow';

            SELECT COUNT(1)
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND Area = N'AI';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                Area = EmptyToNull(query.Area),
                query.ActorId,
                EntityType = EmptyToNull(query.EntityType),
                query.EntityId,
                Search = EmptyToNull(query.Search),
                SearchLike = Like(query.Search)
            },
            cancellationToken: cancellationToken));

        var rows = (await grid.ReadAsync<AuditLogListRow>()).ToArray();
        var eventsToday = await grid.ReadSingleAsync<int>();
        var configChanges = await grid.ReadSingleAsync<int>();
        var workflowDecisions = await grid.ReadSingleAsync<int>();
        var aiEvents = await grid.ReadSingleAsync<int>();

        var items = rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new AdminAuditLogListItem(row.Id, Utc(row.OccurredAtUtc), row.ActorDisplayName, row.EventSummary, row.RecordLabel, row.Area))
            .ToArray();

        return new AdminAuditLogListResponse(
            new AdminAuditLogSummary(eventsToday, configChanges, workflowDecisions, aiEvents),
            items,
            query.Page,
            query.PageSize,
            rows.Length);
    }

    async Task<AdminAuditLogDetails?> IAdminAuditLogRepository.GetAsync(Guid tenantId, Guid auditLogId, CancellationToken cancellationToken)
        => await GetAuditLogAsync(tenantId, auditLogId, cancellationToken);

    private async Task<AdminAuditLogDetails?> GetAuditLogAsync(Guid tenantId, Guid auditLogId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                AuditLogId AS Id,
                OccurredAtUtc,
                ActorUserId,
                ActorDisplayName,
                EventType,
                EntityType,
                EntityId,
                RecordLabel,
                EventSummary,
                Area,
                MetadataJson
            FROM dbo.AuditLogs
            WHERE TenantId = @TenantId
              AND AuditLogId = @AuditLogId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<AuditLogDetailsRow>(
            new CommandDefinition(sql, new { TenantId = tenantId, AuditLogId = auditLogId }, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new AdminAuditLogDetails(
                row.Id,
                Utc(row.OccurredAtUtc),
                row.ActorUserId,
                row.ActorDisplayName,
                row.EventType,
                row.EntityType,
                row.EntityId,
                row.RecordLabel,
                row.EventSummary,
                row.Area,
                row.MetadataJson);
    }

    private async Task<IReadOnlyList<AdminUserMaterialized>> LoadAdminUsersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.UserId,
                u.DisplayName,
                u.Email,
                u.Initials,
                u.AccountStatus,
                u.LastActiveAtUtc,
                u.CreatedAtUtc,
                u.UpdatedAtUtc,
                e.DepartmentId,
                d.Name AS DepartmentName,
                e.ExperienceYears,
                e.JoiningDate,
                COALESCE(interviewStats.CompletedInterviewCount, 0) AS CompletedInterviewCount
            FROM dbo.AppUsers AS u
            LEFT JOIN dbo.Employees AS e ON e.TenantId = u.TenantId AND e.AppUserId = u.UserId
            LEFT JOIN dbo.Departments AS d ON d.DepartmentId = e.DepartmentId
            OUTER APPLY
            (
                SELECT COUNT(1) AS CompletedInterviewCount
                FROM dbo.Interviews AS interview
                WHERE interview.TenantId = u.TenantId
                  AND interview.InterviewerUserId = u.UserId
                  AND interview.Status = N'Completed'
            ) AS interviewStats
            WHERE u.TenantId = @TenantId
              AND u.DeletedAtUtc IS NULL;

            SELECT
                ur.UserId,
                r.RoleId,
                r.Code,
                r.Name,
                r.Priority,
                r.Scope
            FROM dbo.UserRoles AS ur
            INNER JOIN dbo.Roles AS r ON r.RoleId = ur.RoleId
            WHERE ur.TenantId = @TenantId
              AND r.Status = N'Active';

            SELECT
                gm.UserId,
                g.GroupId,
                g.Name
            FROM dbo.GroupMembers AS gm
            INNER JOIN dbo.Groups AS g ON g.GroupId = gm.GroupId
            WHERE gm.TenantId = @TenantId
              AND g.Status = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        var userRows = (await grid.ReadAsync<UserRow>()).ToArray();
        var roleRows = (await grid.ReadAsync<UserRoleRow>()).ToArray();
        var groupRows = (await grid.ReadAsync<UserGroupRow>()).ToArray();

        var users = userRows.ToDictionary(
            user => user.UserId,
            user => new AdminUserMaterialized
            {
                UserId = user.UserId,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Initials = user.Initials,
                AccountStatus = user.AccountStatus,
                LastActiveAtUtc = user.LastActiveAtUtc,
                CreatedAtUtc = user.CreatedAtUtc,
                UpdatedAtUtc = user.UpdatedAtUtc,
                DepartmentId = user.DepartmentId,
                DepartmentName = user.DepartmentName,
                ExperienceYears = user.ExperienceYears,
                JoiningDate = user.JoiningDate,
                CompletedInterviewCount = user.CompletedInterviewCount
            });

        foreach (var role in roleRows)
        {
            if (users.TryGetValue(role.UserId, out var user))
            {
                user.Roles.Add(new UserRoleMaterialized(role.RoleId, role.Code, role.Name, role.Priority, role.Scope));
            }
        }

        foreach (var group in groupRows)
        {
            if (users.TryGetValue(group.UserId, out var user))
            {
                user.Groups.Add(new UserGroupMaterialized(group.GroupId, group.Name));
            }
        }

        return users.Values.ToArray();
    }

    private async Task<HashSet<Guid>> LoadDefaultGroupAssigneeUserIdsAsync(
        Guid tenantId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT UserId
            FROM dbo.GroupMembers
            WHERE TenantId = @TenantId
              AND GroupId = @GroupId
              AND IsDefaultAssignee = 1;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<Guid>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, GroupId = groupId },
            cancellationToken: cancellationToken));

        return rows.ToHashSet();
    }

    private async Task<AdminGroupMembershipUser[]> BuildGroupMembershipCandidatesAsync(
        Guid tenantId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var defaultAssigneeUserIds = await LoadDefaultGroupAssigneeUserIdsAsync(tenantId, groupId, cancellationToken);

        return (await LoadAdminUsersAsync(tenantId, cancellationToken))
            .Where(user => user.IsInternalUser)
            .Select(user => new AdminGroupMembershipUser(
                user.UserId,
                user.DisplayName,
                user.Email,
                user.Initials,
                user.RoleNames,
                user.AccountStatus,
                user.GroupIds.Contains(groupId),
                defaultAssigneeUserIds.Contains(user.UserId)))
            .ToArray();
    }

    private static AdminGroupMembershipUser[] ApplyGroupMembershipSearch(
        IReadOnlyCollection<AdminGroupMembershipUser> users,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return users.ToArray();
        }

        var normalized = search.Trim();
        return users
            .Where(user =>
                user.DisplayName.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                user.Email.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                user.RoleNames.Any(role => role.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                user.AccountStatus.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static AdminGroupMembershipUser[] ApplyGroupMembershipFilter(
        IReadOnlyCollection<AdminGroupMembershipUser> users,
        string? membership)
    {
        if (string.Equals(membership, "Members", StringComparison.OrdinalIgnoreCase))
        {
            return users.Where(user => user.IsMember).ToArray();
        }

        if (string.Equals(membership, "Available", StringComparison.OrdinalIgnoreCase))
        {
            return users.Where(user => !user.IsMember).ToArray();
        }

        return users.ToArray();
    }

    private async Task<List<RoleMaterialized>> LoadRolesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                RoleId,
                Name,
                Type,
                Scope,
                Priority,
                IsProtected,
                Status
            FROM dbo.Roles
            WHERE TenantId = @TenantId;

            SELECT
                rp.RoleId,
                p.PermissionId,
                p.DisplayName
            FROM dbo.RolePermissions AS rp
            INNER JOIN dbo.Permissions AS p ON p.PermissionId = rp.PermissionId
            INNER JOIN dbo.Roles AS r ON r.RoleId = rp.RoleId
            WHERE r.TenantId = @TenantId
            ORDER BY p.GroupName, p.DisplayName;

            SELECT RoleId, COUNT(1) AS AssignedUserCount
            FROM dbo.UserRoles
            WHERE TenantId = @TenantId
            GROUP BY RoleId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        var roles = (await grid.ReadAsync<RoleRow>())
            .Select(row => new RoleMaterialized
            {
                RoleId = row.RoleId,
                Name = row.Name,
                Type = row.Type,
                Scope = row.Scope,
                Priority = row.Priority,
                IsProtected = row.IsProtected,
                Status = row.Status
            })
            .ToDictionary(role => role.RoleId);

        foreach (var permission in await grid.ReadAsync<RolePermissionRow>())
        {
            if (roles.TryGetValue(permission.RoleId, out var role))
            {
                role.Permissions.Add(new PermissionMaterialized(permission.PermissionId, permission.DisplayName));
            }
        }

        foreach (var count in await grid.ReadAsync<RoleAssignmentCountRow>())
        {
            if (roles.TryGetValue(count.RoleId, out var role))
            {
                role.AssignedUserCount = count.AssignedUserCount;
            }
        }

        return roles.Values.ToList();
    }

    private async Task<AdminUserMaterialized[]> FindUsersForRoleAssignmentAsync(
        Guid tenantId,
        RoleUserAssignmentFilterInput input,
        CancellationToken cancellationToken)
    {
        var users = (await LoadAdminUsersAsync(tenantId, cancellationToken))
            .Where(user => user.IsInternalUser);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.Trim();
            users = users.Where(user =>
                user.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                user.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (user.DepartmentName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (input.AccountStatuses is { Count: > 0 })
        {
            users = users.Where(user => input.AccountStatuses.Contains(user.AccountStatus, StringComparer.OrdinalIgnoreCase));
        }

        if (input.DepartmentIds is { Count: > 0 })
        {
            users = users.Where(user => user.DepartmentId.HasValue && input.DepartmentIds.Contains(user.DepartmentId.Value));
        }

        if (input.CurrentRoleIds is { Count: > 0 })
        {
            users = users.Where(user => user.RoleIds.Any(input.CurrentRoleIds.Contains));
        }

        if (input.GroupIds is { Count: > 0 })
        {
            users = users.Where(user => user.GroupIds.Any(input.GroupIds.Contains));
        }

        return users.OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<BenchVisibilityPolicySummary> GetBenchVisibilityPolicySummaryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var policy = await GetBenchVisibilityPolicyAsync(tenantId, cancellationToken);
        return policy is null
            ? new BenchVisibilityPolicySummary(Guid.Empty, "Not configured", "Roles & Permissions")
            : new BenchVisibilityPolicySummary(policy.RoleId, policy.RoleName, "Roles & Permissions");
    }

    private async Task<int> CountRoutingGroupsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Groups
            WHERE TenantId = @TenantId
              AND Status = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private static async Task ReplaceUserAssignmentsAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        Guid userId,
        IReadOnlyList<Guid> roleIds,
        IReadOnlyList<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        const string deleteSql = """
            DELETE FROM dbo.UserRoles
            WHERE TenantId = @TenantId
              AND UserId = @UserId;

            DELETE FROM dbo.GroupMembers
            WHERE TenantId = @TenantId
              AND UserId = @UserId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql,
            new { TenantId = tenantId, UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        const string insertRoleSql = """
            INSERT INTO dbo.UserRoles (TenantId, UserId, RoleId, AssignedByUserId, CreatedAtUtc)
            VALUES (@TenantId, @UserId, @RoleId, @ActorUserId, SYSUTCDATETIME());
            """;

        foreach (var roleId in roleIds.Distinct())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertRoleSql,
                new { TenantId = tenantId, UserId = userId, RoleId = roleId, ActorUserId = actorUserId },
                transaction,
                cancellationToken: cancellationToken));
        }

        const string insertGroupSql = """
            INSERT INTO dbo.GroupMembers (TenantId, GroupId, UserId, IsDefaultAssignee, CreatedAtUtc)
            VALUES (@TenantId, @GroupId, @UserId, 0, SYSUTCDATETIME());
            """;

        foreach (var groupId in groupIds.Distinct())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertGroupSql,
                new { TenantId = tenantId, GroupId = groupId, UserId = userId },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static async Task ReplaceRolePermissionsAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid roleId,
        IReadOnlyList<string> permissionIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.RolePermissions WHERE RoleId = @RoleId;",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        const string insertSql = """
            INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedAtUtc)
            VALUES (@RoleId, @PermissionId, SYSUTCDATETIME());
            """;

        foreach (var permissionId in permissionIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new { RoleId = roleId, PermissionId = permissionId },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static async Task InsertAuditAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid tenantId,
        Guid actorUserId,
        string eventType,
        string entityType,
        Guid? entityId,
        string recordLabel,
        string eventSummary,
        string area,
        string metadataJson,
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
                @MetadataJson
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
                Area = area,
                MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static AdminUserListItem ToAdminUserListItem(AdminUserMaterialized user)
    {
        var highestRole = user.HighestPriorityRole;
        return new AdminUserListItem(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.Initials,
            user.RoleIds,
            user.RoleNames,
            highestRole?.RoleId ?? Guid.Empty,
            highestRole?.Name ?? "Unassigned",
            highestRole?.Priority ?? int.MaxValue,
            user.GroupIds,
            user.GroupNames,
            user.DepartmentId,
            user.DepartmentName,
            user.ExperienceYears,
            ToDateOnly(user.JoiningDate),
            user.CompletedInterviewCount,
            user.AccountStatus,
            ToUtc(user.LastActiveAtUtc),
            Utc(user.CreatedAtUtc),
            Utc(user.UpdatedAtUtc));
    }

    private static AdminUserDetails ToAdminUserDetails(AdminUserMaterialized user)
    {
        return new AdminUserDetails(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.Initials,
            user.RoleIds,
            user.GroupIds,
            user.AccountStatus,
            ToUtc(user.LastActiveAtUtc),
            Utc(user.CreatedAtUtc),
            Utc(user.UpdatedAtUtc));
    }

    private static RoleSummary ToRoleSummary(RoleMaterialized role)
    {
        return new RoleSummary(
            role.RoleId,
            role.Name,
            role.Type,
            role.Scope,
            role.AssignedUserCount,
            BuildPermissionSummary(role.Permissions),
            role.IsProtected ? "Protected" : role.Status,
            role.IsProtected,
            !role.IsProtected && role.Scope == "Tenant");
    }

    private static RoleDetails ToRoleDetails(RoleMaterialized role)
    {
        return new RoleDetails(
            role.RoleId,
            role.Name,
            role.Type,
            role.Scope,
            role.Priority,
            role.IsProtected ? "Protected" : role.Status,
            role.IsProtected,
            !role.IsProtected && role.Scope == "Tenant",
            role.Permissions.Select(permission => permission.PermissionId).ToArray());
    }

    private static RoleUserAssignmentPreviewItem ToRoleUserAssignmentPreviewItem(AdminUserMaterialized user)
    {
        return new RoleUserAssignmentPreviewItem(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.DepartmentName,
            user.HighestPriorityRole?.Name,
            user.AccountStatus);
    }

    private static AdminSkillListItem ToAdminSkillListItem(SkillRow row)
    {
        return new AdminSkillListItem(
            row.SkillId,
            row.Name,
            row.NormalizedName,
            row.Category,
            ParseStringArray(row.AliasesJson),
            row.Status,
            Utc(row.UpdatedAtUtc));
    }

    private static AdminNotificationEventListItem ToNotificationEventListItem(NotificationEventRow row)
    {
        return new AdminNotificationEventListItem(
            row.EventId,
            row.EventCode,
            row.Name,
            row.Recipient,
            row.TemplateName,
            row.LifecycleStatus,
            Utc(row.UpdatedAtUtc));
    }

    private static NotificationTemplateSummary ToNotificationTemplateSummary(NotificationTemplateRow row)
    {
        return new NotificationTemplateSummary(
            row.TemplateId,
            row.EventCode,
            row.Name,
            row.Recipient,
            row.Subject,
            row.Body,
            ParseStringArray(row.AllowedVariablesJson),
            row.LifecycleStatus,
            Utc(row.UpdatedAtUtc),
            row.UpdatedByUserId ?? Guid.Empty);
    }

    private static async Task<NotificationOutboxRow?> ReadNotificationOutboxRowAsync(
        DbConnection connection,
        Guid tenantId,
        Guid outboxId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                o.NotificationOutboxId AS OutboxId,
                e.EventCode,
                e.Name AS EventName,
                COALESCE(t.Name, N'Application-composed email') AS TemplateName,
                N'Talent Pilot workflow' AS SenderDisplayName,
                recipient.DisplayName AS RecipientDisplayName,
                COALESCE(o.RecipientEmail, recipient.Email) AS RecipientEmail,
                o.Channel,
                o.Status,
                o.AttemptCount,
                o.AvailableAtUtc,
                o.CreatedAtUtc,
                o.UpdatedAtUtc,
                o.ProcessedAtUtc,
                o.LastError,
                COALESCE(JSON_VALUE(o.PayloadJson, '$.subject'), N'(No subject)') AS Subject,
                COALESCE(JSON_VALUE(o.PayloadJson, '$.body'), N'') AS Body,
                JSON_VALUE(o.PayloadJson, '$.entityType') AS EntityType,
                JSON_VALUE(o.PayloadJson, '$.entityId') AS EntityId
            FROM dbo.NotificationOutbox AS o
            INNER JOIN dbo.NotificationEvents AS e ON e.NotificationEventId = o.NotificationEventId
            LEFT JOIN dbo.NotificationTemplates AS t ON t.NotificationTemplateId = o.NotificationTemplateId
            LEFT JOIN dbo.AppUsers AS recipient ON recipient.UserId = o.RecipientUserId
            WHERE o.TenantId = @TenantId
              AND o.NotificationOutboxId = @OutboxId
              AND o.Channel = N'Email';
            """;

        return await connection.QuerySingleOrDefaultAsync<NotificationOutboxRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, OutboxId = outboxId },
            cancellationToken: cancellationToken));
    }

    private static AdminNotificationOutboxItem ToNotificationOutboxItem(NotificationOutboxRow row)
    {
        return new AdminNotificationOutboxItem(
            row.OutboxId,
            row.EventCode,
            row.EventName,
            row.TemplateName,
            row.SenderDisplayName,
            row.RecipientDisplayName,
            row.RecipientEmail,
            row.Channel,
            row.Status,
            row.AttemptCount,
            Utc(row.AvailableAtUtc),
            Utc(row.CreatedAtUtc),
            Utc(row.UpdatedAtUtc),
            ToUtc(row.ProcessedAtUtc),
            row.LastError,
            row.Subject,
            row.Body,
            row.EntityType,
            row.EntityId);
    }

    private static async Task<AdminNotificationWorkerStatus> ReadNotificationWorkerStatusAsync(
        SqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @PendingDueCount INT =
            (
                SELECT COUNT(1)
                FROM dbo.NotificationOutbox
                WHERE TenantId = @TenantId
                  AND Channel = N'Email'
                  AND Status = N'Pending'
                  AND AvailableAtUtc <= SYSUTCDATETIME()
            );

            DECLARE @ProcessingCount INT =
            (
                SELECT COUNT(1)
                FROM dbo.NotificationOutbox
                WHERE TenantId = @TenantId
                  AND Channel = N'Email'
                  AND Status = N'Processing'
            );

            IF OBJECT_ID(N'dbo.NotificationWorkerStatus', N'U') IS NULL
            BEGIN
                SELECT
                    CAST(0 AS bit) AS HasHeartbeatTable,
                    CAST(NULL AS nvarchar(120)) AS WorkerName,
                    CAST(NULL AS nvarchar(20)) AS Status,
                    CAST(NULL AS nvarchar(200)) AS HostName,
                    CAST(NULL AS int) AS ProcessId,
                    CAST(NULL AS datetime2(3)) AS StartedAtUtc,
                    CAST(NULL AS datetime2(3)) AS LastHeartbeatUtc,
                    CAST(NULL AS datetime2(3)) AS LastProcessedAtUtc,
                    CAST(NULL AS int) AS LastProcessedCount,
                    CAST(NULL AS nvarchar(1000)) AS LastError,
                    CAST(NULL AS int) AS LastHeartbeatAgeSeconds,
                    @PendingDueCount AS PendingDueCount,
                    @ProcessingCount AS ProcessingCount;
                RETURN;
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.NotificationWorkerStatus WHERE WorkerName = @WorkerName)
            BEGIN
                SELECT
                    CAST(1 AS bit) AS HasHeartbeatTable,
                    CAST(NULL AS nvarchar(120)) AS WorkerName,
                    CAST(NULL AS nvarchar(20)) AS Status,
                    CAST(NULL AS nvarchar(200)) AS HostName,
                    CAST(NULL AS int) AS ProcessId,
                    CAST(NULL AS datetime2(3)) AS StartedAtUtc,
                    CAST(NULL AS datetime2(3)) AS LastHeartbeatUtc,
                    CAST(NULL AS datetime2(3)) AS LastProcessedAtUtc,
                    CAST(NULL AS int) AS LastProcessedCount,
                    CAST(NULL AS nvarchar(1000)) AS LastError,
                    CAST(NULL AS int) AS LastHeartbeatAgeSeconds,
                    @PendingDueCount AS PendingDueCount,
                    @ProcessingCount AS ProcessingCount;
                RETURN;
            END;

            SELECT TOP (1)
                CAST(1 AS bit) AS HasHeartbeatTable,
                WorkerName,
                Status,
                HostName,
                ProcessId,
                StartedAtUtc,
                LastHeartbeatUtc,
                LastProcessedAtUtc,
                LastProcessedCount,
                LastError,
                DATEDIFF(SECOND, LastHeartbeatUtc, SYSUTCDATETIME()) AS LastHeartbeatAgeSeconds,
                @PendingDueCount AS PendingDueCount,
                @ProcessingCount AS ProcessingCount
            FROM dbo.NotificationWorkerStatus
            WHERE WorkerName = @WorkerName
            ORDER BY LastHeartbeatUtc DESC;
            """;

        var row = await connection.QuerySingleAsync<NotificationWorkerStatusRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, WorkerName = NotificationWorkerName },
            cancellationToken: cancellationToken));

        return ToNotificationWorkerStatus(row);
    }

    private static AdminNotificationWorkerStatus ToNotificationWorkerStatus(NotificationWorkerStatusRow row)
    {
        string state;
        string label;
        string message;

        if (!row.HasHeartbeatTable)
        {
            state = "NotConfigured";
            label = "Heartbeat unavailable";
            message = "The worker heartbeat table is not present. Run database migrations, then start TalentPilot.Worker with the same database connection.";
        }
        else if (!row.LastHeartbeatUtc.HasValue)
        {
            state = "Offline";
            label = "Not reporting";
            message = row.PendingDueCount > 0
                ? "No worker heartbeat has been recorded. Due emails will stay pending until TalentPilot.Worker is started."
                : "No worker heartbeat has been recorded. Start TalentPilot.Worker before expecting queued email delivery.";
        }
        else if (row.LastHeartbeatAgeSeconds.GetValueOrDefault(int.MaxValue) > NotificationWorkerStaleAfterSeconds)
        {
            var heartbeatAgeSeconds = row.LastHeartbeatAgeSeconds.GetValueOrDefault();
            state = "Offline";
            label = "Stale heartbeat";
            message = $"Last worker heartbeat was {FormatAge(heartbeatAgeSeconds)} ago. Pending emails will stay queued until TalentPilot.Worker is running again.";
        }
        else if (!string.IsNullOrWhiteSpace(row.LastError))
        {
            state = "Error";
            label = "Running with error";
            message = $"The worker heartbeat is current, but the latest loop reported an error: {row.LastError}";
        }
        else
        {
            state = "Running";
            label = "Running";
            message = row.PendingDueCount > 0
                ? $"The worker heartbeat is current. {row.PendingDueCount} due email(s) are waiting for the next {NotificationWorkerPollIntervalSeconds}-second polling cycle."
                : "The worker heartbeat is current and there are no due pending emails.";
        }

        return new AdminNotificationWorkerStatus(
            state,
            label,
            message,
            ToUtc(row.LastHeartbeatUtc),
            ToUtc(row.StartedAtUtc),
            ToUtc(row.LastProcessedAtUtc),
            row.LastProcessedCount,
            row.HostName,
            row.ProcessId,
            row.LastError,
            NotificationWorkerPollIntervalSeconds,
            NotificationWorkerStaleAfterSeconds,
            row.PendingDueCount,
            row.ProcessingCount);
    }

    private static string FormatAge(int seconds)
    {
        if (seconds < 60)
        {
            return $"{Math.Max(0, seconds)} second(s)";
        }

        var minutes = seconds / 60;
        if (minutes < 60)
        {
            return $"{minutes} minute(s)";
        }

        return $"{minutes / 60} hour(s)";
    }

    private static string BuildPermissionSummary(IReadOnlyCollection<PermissionMaterialized> permissions)
    {
        if (permissions.Count == 0)
        {
            return "No permissions";
        }

        var selected = permissions.Select(permission => permission.DisplayName).Take(3).ToArray();
        var suffix = permissions.Count > selected.Length ? $" +{permissions.Count - selected.Length} more" : string.Empty;
        return string.Join(", ", selected) + suffix;
    }

    private static IReadOnlyList<string> ParseStringArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyDictionary<string, string> ParseStringMap(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string>();
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
                    JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    _ => property.Value.GetRawText()
                };
            }

            return values;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static AdminAiAgentRunListItem ToAiAgentRunLogItem(AiAgentRunLogRow row)
    {
        var metadata = ParseStringMap(row.MetadataJson);
        return new AdminAiAgentRunListItem(
            row.AiAgentRunId,
            row.AgentId,
            row.AgentName,
            row.SourceEntityType,
            row.SourceEntityId,
            row.ModelName,
            row.EmbeddingModelName,
            row.Status,
            Utc(row.StartedAtUtc),
            ToUtc(row.CompletedAtUtc),
            row.DurationMs,
            row.OutputSummary,
            row.InputHash,
            MetadataValue(metadata, "promptVersion"),
            MetadataValue(metadata, "semanticSimilarityStatus") ?? MetadataValue(metadata, "semanticSimilarity"),
            string.Equals(MetadataValue(metadata, "humanDecisionRequired"), "true", StringComparison.OrdinalIgnoreCase),
            MetadataValue(metadata, "errorType"));
    }

    private static string? MetadataValue(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static async Task<Guid> EnsureNotificationEventAsync(
        SqlConnection connection,
        DbTransaction transaction,
        Guid tenantId,
        string eventCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @EventId UNIQUEIDENTIFIER;

            SELECT @EventId = NotificationEventId
            FROM dbo.NotificationEvents
            WHERE TenantId = @TenantId
              AND EventCode = @EventCode;

            IF @EventId IS NULL
            BEGIN
                SET @EventId = NEWID();

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
                    @EventId,
                    @TenantId,
                    @EventCode,
                    N'Realtime notification',
                    N'Realtime',
                    N'Active',
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            END;

            SELECT @EventId;
            """;

        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, EventCode = eventCode },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static AdminHiringPipelineTemplateItem ToHiringPipelineTemplateItem(
        InterviewTemplateRow template,
        IReadOnlyCollection<InterviewTemplateRoundRow> rounds)
    {
        var activeRounds = rounds
            .Where(round => round.Status == "Active")
            .OrderBy(round => round.RoundOrder)
            .ToArray();
        var stageFlow = activeRounds.Length == 0
            ? "No active rounds"
            : string.Join(" -> ", activeRounds.Select(round => round.Name));
        var defaultInterviewers = activeRounds.Length == 0
            ? "Unassigned"
            : string.Join(", ", activeRounds.Select(round => round.OwnerUserName).Distinct(StringComparer.OrdinalIgnoreCase));

        return new AdminHiringPipelineTemplateItem(
            template.InterviewTemplateId,
            template.Name,
            template.DepartmentName,
            template.Description,
            stageFlow,
            defaultInterviewers,
            activeRounds.Length,
            template.Status,
            Utc(template.UpdatedAtUtc));
    }

    private static bool MatchesHiringPipelineSearch(AdminHiringPipelineTemplateItem template, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var value = search.Trim();
        return template.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
            || template.DepartmentName.Contains(value, StringComparison.OrdinalIgnoreCase)
            || template.StageFlow.Contains(value, StringComparison.OrdinalIgnoreCase)
            || template.DefaultInterviewers.Contains(value, StringComparison.OrdinalIgnoreCase)
            || template.Status.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static object SearchParameters(Guid tenantId, string? search)
    {
        return new
        {
            TenantId = tenantId,
            Search = EmptyToNull(search),
            SearchLike = Like(search)
        };
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Like(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : $"%{value.Trim()}%";
    }

    private static DateTimeOffset Utc(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static DateTimeOffset? ToUtc(DateTime? value)
    {
        return value.HasValue ? Utc(value.Value) : null;
    }

    private static DateOnly? ToDateOnly(DateTime? value)
    {
        return value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
    }

    private static string BuildInitials(string displayName)
    {
        var initials = displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]))
            .ToArray();

        return initials.Length == 0 ? "U" : new string(initials);
    }

    private static string BuildRoleCode(string roleName)
    {
        var code = new string(roleName.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(code) ? $"Role{Guid.NewGuid():N}"[..12] : code;
    }

    private sealed class AdminUserMaterialized
    {
        public Guid UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Initials { get; init; } = string.Empty;
        public string AccountStatus { get; init; } = string.Empty;
        public DateTime? LastActiveAtUtc { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public Guid? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public decimal? ExperienceYears { get; init; }
        public DateTime? JoiningDate { get; init; }
        public int CompletedInterviewCount { get; init; }
        public List<UserRoleMaterialized> Roles { get; } = [];
        public List<UserGroupMaterialized> Groups { get; } = [];
        public IReadOnlyList<Guid> RoleIds => Roles.Select(role => role.RoleId).ToArray();
        public IReadOnlyList<string> RoleNames => Roles.Select(role => role.Name).ToArray();
        public IReadOnlyList<Guid> GroupIds => Groups.Select(group => group.GroupId).ToArray();
        public IReadOnlyList<string> GroupNames => Groups.Select(group => group.Name).ToArray();
        public UserRoleMaterialized? HighestPriorityRole => Roles.OrderBy(role => role.Priority).FirstOrDefault();
        public bool IsInternalUser => Roles.All(role => role.Code != "Candidate" && role.Scope != "Portal");
    }

    private sealed class RoleMaterialized
    {
        public Guid RoleId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Scope { get; init; } = string.Empty;
        public int Priority { get; init; }
        public bool IsProtected { get; init; }
        public string Status { get; init; } = string.Empty;
        public int AssignedUserCount { get; set; }
        public List<PermissionMaterialized> Permissions { get; } = [];
    }

    private sealed record UserRoleMaterialized(Guid RoleId, string Code, string Name, int Priority, string Scope);
    private sealed record UserGroupMaterialized(Guid GroupId, string Name);
    private sealed record PermissionMaterialized(string PermissionId, string DisplayName);
    private sealed record UserRow(Guid UserId, string DisplayName, string Email, string Initials, string AccountStatus, DateTime? LastActiveAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, Guid? DepartmentId, string? DepartmentName, decimal? ExperienceYears, DateTime? JoiningDate, int CompletedInterviewCount);
    private sealed record UserRoleRow(Guid UserId, Guid RoleId, string Code, string Name, int Priority, string Scope);
    private sealed record UserGroupRow(Guid UserId, Guid GroupId, string Name);
    private sealed record BenchPolicyRow(Guid RoleId, string RoleName, DateTime UpdatedAtUtc, Guid? UpdatedByUserId);
    private sealed record PermissionPolicyRow(string Mode, DateTime UpdatedAtUtc, Guid? UpdatedByUserId);
    private sealed record RoleRow(Guid RoleId, string Name, string Type, string Scope, int Priority, bool IsProtected, string Status);
    private sealed record RolePermissionRow(Guid RoleId, string PermissionId, string DisplayName);
    private sealed record RoleAssignmentCountRow(Guid RoleId, int AssignedUserCount);
    private sealed record NotificationEventRow(Guid EventId, string EventCode, string Name, string Recipient, string TemplateName, string LifecycleStatus, DateTime UpdatedAtUtc);
    private sealed record SkillRow(Guid SkillId, string Name, string NormalizedName, string Category, string AliasesJson, string Status, DateTime UpdatedAtUtc);
    private sealed record CandidateSourceLabelRow(Guid CandidateSourceLabelId, string Code, string DisplayName, string ReportingCategory, string Status, DateTime UpdatedAtUtc);
    private sealed record WorkflowDefinitionRow(Guid WorkflowDefinitionId, string Code, string Name, string EntityType, string Status, DateTime UpdatedAtUtc);
    private sealed record WorkflowStageRow(Guid WorkflowStageId, string StageKey, string Name, int StageOrder, bool IsTerminal, string Status);
    private sealed record WorkflowRoutingRuleRow(Guid WorkflowRoutingRuleId, Guid WorkflowTransitionId, string ActionKey, string ActionName, string FromStage, string ToStage, string AssignmentType, string AssignmentTarget, string ResolverKey, string Status);
    private sealed record WorkflowIntakeRoutingRuleRow(Guid? JobRequestIntakeRoutingRuleId, Guid DepartmentId, string DepartmentCode, string DepartmentName, string AssignmentType, Guid? TargetUserId, Guid? TargetGroupId, string AssignmentTarget, string Status, bool UsesTenantAdminFallback);
    private sealed record InterviewTemplateRow(Guid InterviewTemplateId, Guid? DepartmentId, string Name, string DepartmentName, string Description, string Status, DateTime UpdatedAtUtc);
    private sealed record InterviewTemplateRoundRow(Guid InterviewTemplateId, int RoundOrder, string Name, Guid? OwnerRoleId, string OwnerRoleName, Guid? OwnerUserId, string OwnerUserName, int DurationMinutes, bool IsRequired, string Status);
    private sealed record InterviewTemplateDetailsRow(Guid InterviewTemplateId, Guid? DepartmentId, string Name, string DepartmentName, string Description, string Status, DateTime UpdatedAtUtc);
    private sealed record InterviewTemplateDetailsRoundRow(Guid InterviewTemplateRoundId, int RoundOrder, string Name, Guid? OwnerRoleId, string OwnerRoleName, Guid? OwnerUserId, string OwnerUserName, int DurationMinutes, bool IsRequired, string Status);
    private sealed record NotificationEventDetailsRow(Guid EventId, string EventCode, string Name, string Recipient, string LifecycleStatus);
    private sealed record NotificationTemplateRow(Guid TemplateId, string EventCode, string Name, string Recipient, string Subject, string Body, string AllowedVariablesJson, string LifecycleStatus, DateTime UpdatedAtUtc, Guid? UpdatedByUserId);
    private sealed record NotificationOutboxRow(
        Guid OutboxId,
        string EventCode,
        string EventName,
        string TemplateName,
        string SenderDisplayName,
        string? RecipientDisplayName,
        string? RecipientEmail,
        string Channel,
        string Status,
        int AttemptCount,
        DateTime AvailableAtUtc,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        DateTime? ProcessedAtUtc,
        string? LastError,
        string Subject,
        string Body,
        string? EntityType,
        string? EntityId);
    private sealed record NotificationWorkerStatusRow(
        bool HasHeartbeatTable,
        string? WorkerName,
        string? Status,
        string? HostName,
        int? ProcessId,
        DateTime? StartedAtUtc,
        DateTime? LastHeartbeatUtc,
        DateTime? LastProcessedAtUtc,
        int? LastProcessedCount,
        string? LastError,
        int? LastHeartbeatAgeSeconds,
        int PendingDueCount,
        int ProcessingCount);
    private sealed record AuditLogListRow(Guid Id, DateTime OccurredAtUtc, string ActorDisplayName, string EventSummary, string RecordLabel, string Area);
    private sealed record AuditLogDetailsRow(Guid Id, DateTime OccurredAtUtc, Guid? ActorUserId, string ActorDisplayName, string EventType, string EntityType, Guid? EntityId, string RecordLabel, string EventSummary, string Area, string MetadataJson);
    private sealed record AiAgentRunLogRow(Guid AiAgentRunId, string AgentId, string AgentName, string SourceEntityType, Guid SourceEntityId, string ModelName, string? EmbeddingModelName, string Status, DateTime StartedAtUtc, DateTime? CompletedAtUtc, int? DurationMs, string? OutputSummary, string InputHash, string MetadataJson);
}
