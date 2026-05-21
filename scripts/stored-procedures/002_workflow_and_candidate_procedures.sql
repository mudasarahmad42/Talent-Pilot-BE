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
