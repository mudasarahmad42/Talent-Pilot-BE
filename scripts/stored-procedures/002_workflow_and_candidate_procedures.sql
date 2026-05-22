SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ClaimWorkflowAssignment
    @TenantId UNIQUEIDENTIFIER,
    @AssignmentId UNIQUEIDENTIFIER,
    @CurrentUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @AssignedToGroupId UNIQUEIDENTIFIER;
    DECLARE @EntityType NVARCHAR(80);
    DECLARE @EntityId UNIQUEIDENTIFIER;

    SELECT
        @AssignedToGroupId = AssignedToGroupId,
        @EntityType = EntityType,
        @EntityId = EntityId
    FROM dbo.WorkflowAssignments WITH (UPDLOCK, ROWLOCK)
    WHERE
        TenantId = @TenantId
        AND WorkflowAssignmentId = @AssignmentId
        AND AssignmentStatus = N'Pending';

    IF @EntityId IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT CAST(0 AS BIT) AS Claimed, N'Assignment is not pending or was not found.' AS Message;
        RETURN;
    END;

    IF @AssignedToGroupId IS NOT NULL
        AND NOT EXISTS
        (
            SELECT 1
            FROM dbo.GroupMembers
            WHERE TenantId = @TenantId
              AND GroupId = @AssignedToGroupId
              AND UserId = @CurrentUserId
        )
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT CAST(0 AS BIT) AS Claimed, N'Current user is not a member of the assigned routing group.' AS Message;
        RETURN;
    END;

    UPDATE dbo.WorkflowAssignments
    SET
        AssignmentStatus = N'Claimed',
        ClaimedByUserId = @CurrentUserId,
        AssignedToUserId = @CurrentUserId,
        ClaimedAtUtc = SYSUTCDATETIME()
    WHERE
        TenantId = @TenantId
        AND WorkflowAssignmentId = @AssignmentId;

    IF @EntityType = N'JobRequest'
    BEGIN
        UPDATE dbo.JobRequests
        SET
            CurrentAssignmentId = @AssignmentId,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE TenantId = @TenantId AND JobRequestId = @EntityId;
    END;

    COMMIT TRANSACTION;

    SELECT CAST(1 AS BIT) AS Claimed, N'Assignment claimed.' AS Message;
END;
GO

CREATE OR ALTER TRIGGER dbo.trg_WorkflowAssignments_NotifyPendingJobRequest
ON dbo.WorkflowAssignments
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH PendingJobRequestAssignments AS
    (
        SELECT
            i.TenantId,
            i.WorkflowAssignmentId,
            i.EntityId AS JobRequestId,
            i.AssignedToGroupId,
            jr.Title,
            creator.DisplayName AS RequesterName
        FROM inserted AS i
        INNER JOIN dbo.WorkflowTransitions AS wt
            ON wt.WorkflowTransitionId = i.WorkflowTransitionId
            AND wt.ActionKey = N'CREATE_BY_PRESALES'
        INNER JOIN dbo.JobRequests AS jr
            ON jr.TenantId = i.TenantId
            AND jr.JobRequestId = i.EntityId
        INNER JOIN dbo.AppUsers AS creator
            ON creator.UserId = jr.CreatedByUserId
        WHERE i.EntityType = N'JobRequest'
          AND i.AssignmentStatus = N'Pending'
          AND i.AssignedToGroupId IS NOT NULL
    ),
    Recipients AS
    (
        SELECT
            pending.TenantId,
            pending.WorkflowAssignmentId,
            pending.JobRequestId,
            pending.Title,
            pending.RequesterName,
            gm.UserId AS RecipientUserId,
            recipient.Email AS RecipientEmail,
            ne.NotificationEventId,
            nt.NotificationTemplateId
        FROM PendingJobRequestAssignments AS pending
        INNER JOIN dbo.GroupMembers AS gm
            ON gm.TenantId = pending.TenantId
            AND gm.GroupId = pending.AssignedToGroupId
        INNER JOIN dbo.AppUsers AS recipient
            ON recipient.TenantId = gm.TenantId
            AND recipient.UserId = gm.UserId
            AND recipient.AccountStatus = N'Active'
            AND recipient.DeletedAtUtc IS NULL
        INNER JOIN dbo.NotificationEvents AS ne
            ON ne.TenantId = pending.TenantId
            AND ne.EventCode = N'PRESALES_REQUEST_SUBMITTED'
            AND ne.Status = N'Active'
        OUTER APPLY
        (
            SELECT TOP (1) activeTemplate.NotificationTemplateId
            FROM dbo.NotificationTemplates AS activeTemplate
            WHERE activeTemplate.TenantId = pending.TenantId
              AND activeTemplate.NotificationEventId = ne.NotificationEventId
              AND activeTemplate.Status = N'Active'
            ORDER BY activeTemplate.Name
        ) AS nt
    )
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
        r.TenantId,
        r.NotificationEventId,
        r.RecipientUserId,
        SYSUTCDATETIME()
    FROM Recipients AS r
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.NotificationRecipients AS existing
        WHERE existing.NotificationEventId = r.NotificationEventId
          AND existing.RecipientUserId = r.RecipientUserId
    );

    ;WITH PendingJobRequestAssignments AS
    (
        SELECT
            i.TenantId,
            i.WorkflowAssignmentId,
            i.EntityId AS JobRequestId,
            i.AssignedToGroupId,
            jr.Title,
            creator.DisplayName AS RequesterName
        FROM inserted AS i
        INNER JOIN dbo.WorkflowTransitions AS wt
            ON wt.WorkflowTransitionId = i.WorkflowTransitionId
            AND wt.ActionKey = N'CREATE_BY_PRESALES'
        INNER JOIN dbo.JobRequests AS jr
            ON jr.TenantId = i.TenantId
            AND jr.JobRequestId = i.EntityId
        INNER JOIN dbo.AppUsers AS creator
            ON creator.UserId = jr.CreatedByUserId
        WHERE i.EntityType = N'JobRequest'
          AND i.AssignmentStatus = N'Pending'
          AND i.AssignedToGroupId IS NOT NULL
    ),
    Recipients AS
    (
        SELECT
            pending.TenantId,
            pending.WorkflowAssignmentId,
            pending.JobRequestId,
            pending.Title,
            pending.RequesterName,
            gm.UserId AS RecipientUserId,
            recipient.Email AS RecipientEmail,
            ne.NotificationEventId,
            nt.NotificationTemplateId
        FROM PendingJobRequestAssignments AS pending
        INNER JOIN dbo.GroupMembers AS gm
            ON gm.TenantId = pending.TenantId
            AND gm.GroupId = pending.AssignedToGroupId
        INNER JOIN dbo.AppUsers AS recipient
            ON recipient.TenantId = gm.TenantId
            AND recipient.UserId = gm.UserId
            AND recipient.AccountStatus = N'Active'
            AND recipient.DeletedAtUtc IS NULL
        INNER JOIN dbo.NotificationEvents AS ne
            ON ne.TenantId = pending.TenantId
            AND ne.EventCode = N'PRESALES_REQUEST_SUBMITTED'
            AND ne.Status = N'Active'
        OUTER APPLY
        (
            SELECT TOP (1) activeTemplate.NotificationTemplateId
            FROM dbo.NotificationTemplates AS activeTemplate
            WHERE activeTemplate.TenantId = pending.TenantId
              AND activeTemplate.NotificationEventId = ne.NotificationEventId
              AND activeTemplate.Status = N'Active'
            ORDER BY activeTemplate.Name
        ) AS nt
    )
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
        AttemptCount,
        AvailableAtUtc,
        CreatedAtUtc,
        UpdatedAtUtc
    )
    SELECT
        NEWID(),
        r.TenantId,
        r.NotificationEventId,
        r.NotificationTemplateId,
        r.RecipientUserId,
        r.RecipientEmail,
        N'SignalR',
        CONCAT(
            N'{"entityType":"JobRequest","entityId":"',
            CONVERT(NVARCHAR(36), r.JobRequestId),
            N'","assignmentId":"',
            CONVERT(NVARCHAR(36), r.WorkflowAssignmentId),
            N'","jobTitle":"',
            STRING_ESCAPE(r.Title, 'json'),
            N'","requesterName":"',
            STRING_ESCAPE(r.RequesterName, 'json'),
            N'"}'
        ),
        N'Pending',
        0,
        SYSUTCDATETIME(),
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    FROM Recipients AS r
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.NotificationOutbox AS existing
        WHERE existing.TenantId = r.TenantId
          AND JSON_VALUE(existing.PayloadJson, '$.assignmentId') = CONVERT(NVARCHAR(36), r.WorkflowAssignmentId)
          AND existing.RecipientUserId = r.RecipientUserId
          AND existing.NotificationEventId = r.NotificationEventId
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CanCandidateApply
    @TenantId UNIQUEIDENTIFIER,
    @CandidateId UNIQUEIDENTIFIER,
    @JobRequestId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CooldownDays INT =
    (
        SELECT ReapplyCooldownDays
        FROM dbo.TenantRecruitmentSettings
        WHERE TenantId = @TenantId
    );

    SET @CooldownDays = COALESCE(@CooldownDays, 90);

    DECLARE @LatestStatus NVARCHAR(50);
    DECLARE @LatestDecisionAtUtc DATETIME2(3);
    DECLARE @LatestAppliedAtUtc DATETIME2(3);

    SELECT TOP (1)
        @LatestStatus = CurrentStatus,
        @LatestDecisionAtUtc = FinalDecisionAtUtc,
        @LatestAppliedAtUtc = AppliedAtUtc
    FROM dbo.JobApplications
    WHERE
        TenantId = @TenantId
        AND CandidateId = @CandidateId
        AND JobRequestId = @JobRequestId
    ORDER BY ApplicationVersion DESC, AppliedAtUtc DESC;

    IF @LatestStatus IS NULL
    BEGIN
        SELECT CAST(1 AS BIT) AS CanApply, N'No previous application for this job request.' AS Reason, CAST(NULL AS DATETIME2(3)) AS EligibleAtUtc;
        RETURN;
    END;

    IF @LatestStatus IN (N'Invited', N'Applied', N'Screening', N'Interviewing', N'OnHold', N'OfferDeclined', N'Hired')
    BEGIN
        SELECT CAST(0 AS BIT) AS CanApply, N'Candidate already has an active or successful application history for this request.' AS Reason, CAST(NULL AS DATETIME2(3)) AS EligibleAtUtc;
        RETURN;
    END;

    DECLARE @EffectiveDecisionAtUtc DATETIME2(3) = COALESCE(@LatestDecisionAtUtc, @LatestAppliedAtUtc);
    DECLARE @EligibleAtUtc DATETIME2(3) = DATEADD(DAY, @CooldownDays, @EffectiveDecisionAtUtc);

    IF SYSUTCDATETIME() < @EligibleAtUtc
    BEGIN
        SELECT CAST(0 AS BIT) AS CanApply, N'Reapply cooldown is still active.' AS Reason, @EligibleAtUtc AS EligibleAtUtc;
        RETURN;
    END;

    SELECT CAST(1 AS BIT) AS CanApply, N'Reapply cooldown has passed.' AS Reason, @EligibleAtUtc AS EligibleAtUtc;
END;
GO
