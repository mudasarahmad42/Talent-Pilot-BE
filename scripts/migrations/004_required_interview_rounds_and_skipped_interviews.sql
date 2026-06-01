-- Migration 004: make interview rounds required and add audited skipped interview support.
-- Existing interview template rows are corrected to required. Skipped interviews are a
-- first-class status and must include the actor, UTC timestamp, and reason.

UPDATE dbo.InterviewTemplateRounds
SET IsRequired = CAST(1 AS BIT)
WHERE IsRequired = CAST(0 AS BIT);
GO

IF COL_LENGTH(N'dbo.Interviews', N'SkippedByUserId') IS NULL
BEGIN
    ALTER TABLE dbo.Interviews ADD SkippedByUserId UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH(N'dbo.Interviews', N'SkippedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Interviews ADD SkippedAtUtc DATETIME2(3) NULL;
END;
GO

IF COL_LENGTH(N'dbo.Interviews', N'SkipReason') IS NULL
BEGIN
    ALTER TABLE dbo.Interviews ADD SkipReason NVARCHAR(1000) NULL;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_Interviews_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.Interviews')
)
BEGIN
    ALTER TABLE dbo.Interviews DROP CONSTRAINT CK_Interviews_Status;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_Interviews_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.Interviews')
)
BEGIN
    ALTER TABLE dbo.Interviews
    ADD CONSTRAINT CK_Interviews_Status
    CHECK (Status IN (N'Scheduled', N'Completed', N'Cancelled', N'NoShow', N'Skipped'));
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Interviews_SkippedByUser'
      AND parent_object_id = OBJECT_ID(N'dbo.Interviews')
)
BEGIN
    ALTER TABLE dbo.Interviews
    ADD CONSTRAINT FK_Interviews_SkippedByUser
    FOREIGN KEY (SkippedByUserId) REFERENCES dbo.AppUsers (UserId);
END;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_Interviews_SkippedAudit'
      AND parent_object_id = OBJECT_ID(N'dbo.Interviews')
)
BEGIN
    ALTER TABLE dbo.Interviews DROP CONSTRAINT CK_Interviews_SkippedAudit;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_Interviews_SkippedAudit'
      AND parent_object_id = OBJECT_ID(N'dbo.Interviews')
)
BEGIN
    ALTER TABLE dbo.Interviews
    ADD CONSTRAINT CK_Interviews_SkippedAudit
    CHECK
    (
        Status <> N'Skipped'
        OR
        (
            SkippedByUserId IS NOT NULL
            AND SkippedAtUtc IS NOT NULL
            AND NULLIF(LTRIM(RTRIM(SkipReason)), N'') IS NOT NULL
        )
    );
END;
GO
