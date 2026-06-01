-- set_candidate_test_emails.sql
-- Development/test helper only.
-- Drops candidate-contact email uniqueness and points every candidate contact email to the shared test inbox.
-- AppUsers.Email remains unique because login depends on it.

IF OBJECT_ID(N'dbo.CandidateEmailTestBackup', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateEmailTestBackup
    (
        CandidateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CandidateEmailTestBackup PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        OriginalEmail NVARCHAR(320) NOT NULL,
        BackedUpAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateEmailTestBackup_BackedUpAtUtc DEFAULT SYSUTCDATETIME()
    );
END;
GO

INSERT INTO dbo.CandidateEmailTestBackup (CandidateId, TenantId, OriginalEmail)
SELECT candidate.CandidateId, candidate.TenantId, candidate.Email
FROM dbo.Candidates AS candidate
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.CandidateEmailTestBackup AS email_backup
    WHERE email_backup.CandidateId = candidate.CandidateId
);
GO

IF EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_Candidates_Tenant_Email'
      AND parent_object_id = OBJECT_ID(N'dbo.Candidates')
)
BEGIN
    ALTER TABLE dbo.Candidates DROP CONSTRAINT UQ_Candidates_Tenant_Email;
END;
GO

UPDATE dbo.Candidates
SET Email = N'mudasar.ahmad@tkxel.com',
    UpdatedAtUtc = SYSUTCDATETIME();
GO
