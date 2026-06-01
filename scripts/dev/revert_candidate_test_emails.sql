-- revert_candidate_test_emails.sql
-- Restores candidate-contact emails backed up by set_candidate_test_emails.sql and recreates uniqueness when safe.
-- AppUsers.Email is intentionally untouched.

IF OBJECT_ID(N'dbo.CandidateEmailTestBackup', N'U') IS NOT NULL
BEGIN
    UPDATE candidate
    SET Email = email_backup.OriginalEmail,
        UpdatedAtUtc = SYSUTCDATETIME()
    FROM dbo.Candidates AS candidate
    INNER JOIN dbo.CandidateEmailTestBackup AS email_backup
        ON email_backup.CandidateId = candidate.CandidateId;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_Candidates_Tenant_Email'
      AND parent_object_id = OBJECT_ID(N'dbo.Candidates')
)
AND NOT EXISTS (
    SELECT 1
    FROM dbo.Candidates
    GROUP BY TenantId, Email
    HAVING COUNT(1) > 1
)
BEGIN
    ALTER TABLE dbo.Candidates
    ADD CONSTRAINT UQ_Candidates_Tenant_Email UNIQUE (TenantId, Email);
END;
GO
