-- 020_remove_redundant_ai_and_candidate_sync_artifacts.sql
-- Removes retired database artifacts that are not part of the implemented MVP:
-- - AI never moves workflow stages, so AutomaticStageMovementEnabled is not stored.
-- - Joined candidates are tracked through JobRequestFulfillments; no candidate-to-employee sync table exists in MVP.
-- - Resume/CV evidence is stored as extracted candidate profile data, not a separate document table in MVP.

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.TenantAiSettings', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TenantAiSettings', N'AutomaticStageMovementEnabled') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_TenantAiSettings_AutomaticStageMovementEnabled'
          AND parent_object_id = OBJECT_ID(N'dbo.TenantAiSettings')
    )
    BEGIN
        ALTER TABLE dbo.TenantAiSettings DROP CONSTRAINT CK_TenantAiSettings_AutomaticStageMovementEnabled;
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.default_constraints
        WHERE name = N'DF_TenantAiSettings_AutomaticStageMovementEnabled'
          AND parent_object_id = OBJECT_ID(N'dbo.TenantAiSettings')
    )
    BEGIN
        ALTER TABLE dbo.TenantAiSettings DROP CONSTRAINT DF_TenantAiSettings_AutomaticStageMovementEnabled;
    END;

    ALTER TABLE dbo.TenantAiSettings DROP COLUMN AutomaticStageMovementEnabled;
END;
GO

IF OBJECT_ID(N'dbo.CandidateEmployeeLinks', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.CandidateEmployeeLinks;
END;
GO

IF OBJECT_ID(N'dbo.CandidateDocuments', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.CandidateDocuments;
END;
GO
