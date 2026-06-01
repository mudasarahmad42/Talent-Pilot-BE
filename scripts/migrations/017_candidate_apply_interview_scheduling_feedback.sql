-- 017_candidate_apply_interview_scheduling_feedback.sql
-- Adds interview scheduling notification metadata and protects submitted feedback from duplicate rows.

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

IF OBJECT_ID(N'dbo.NotificationEvents', N'U') IS NOT NULL
BEGIN
    MERGE dbo.NotificationEvents AS target
    USING
    (
        SELECT
            tenant.TenantId,
            N'INTERVIEW_SCHEDULED' AS EventCode,
            N'Interview scheduled' AS Name,
            N'User:InterviewParticipants' AS DefaultRecipientType,
            N'Active' AS Status
        FROM dbo.Tenants AS tenant
    ) AS source
    ON target.TenantId = source.TenantId
       AND target.EventCode = source.EventCode
    WHEN MATCHED THEN UPDATE SET
        Name = source.Name,
        DefaultRecipientType = source.DefaultRecipientType,
        Status = source.Status,
        UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (NotificationEventId, TenantId, EventCode, Name, DefaultRecipientType, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (NEWID(), source.TenantId, source.EventCode, source.Name, source.DefaultRecipientType, source.Status, @Now, @Now);
END;
GO

IF OBJECT_ID(N'dbo.InterviewFeedback', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UX_InterviewFeedback_Submitted'
          AND object_id = OBJECT_ID(N'dbo.InterviewFeedback')
   )
BEGIN
    CREATE UNIQUE INDEX UX_InterviewFeedback_Submitted
        ON dbo.InterviewFeedback (TenantId, InterviewId)
        WHERE IsSubmitted = CAST(1 AS BIT);
END;
GO
