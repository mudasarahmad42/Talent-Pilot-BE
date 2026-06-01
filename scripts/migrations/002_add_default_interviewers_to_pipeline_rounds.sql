SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.InterviewTemplateRounds', N'OwnerUserId') IS NULL
BEGIN
    ALTER TABLE dbo.InterviewTemplateRounds ADD OwnerUserId UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH(N'dbo.JobRequestInterviewRounds', N'OwnerUserId') IS NULL
BEGIN
    ALTER TABLE dbo.JobRequestInterviewRounds ADD OwnerUserId UNIQUEIDENTIFIER NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_InterviewTemplateRounds_OwnerUser'
      AND parent_object_id = OBJECT_ID(N'dbo.InterviewTemplateRounds')
)
BEGIN
    ALTER TABLE dbo.InterviewTemplateRounds
    ADD CONSTRAINT FK_InterviewTemplateRounds_OwnerUser FOREIGN KEY (OwnerUserId) REFERENCES dbo.AppUsers (UserId);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_JobRequestInterviewRounds_OwnerUser'
      AND parent_object_id = OBJECT_ID(N'dbo.JobRequestInterviewRounds')
)
BEGIN
    ALTER TABLE dbo.JobRequestInterviewRounds
    ADD CONSTRAINT FK_JobRequestInterviewRounds_OwnerUser FOREIGN KEY (OwnerUserId) REFERENCES dbo.AppUsers (UserId);
END;
GO

DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @RecruiterUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333304';
DECLARE @InterviewerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333305';
DECLARE @HiringManagerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333306';

DECLARE @RoundScreeningId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff11';
DECLARE @RoundTechnicalId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff12';
DECLARE @RoundDepartmentHeadId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff13';
DECLARE @JobRoundScreeningId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff21';
DECLARE @JobRoundTechnicalId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff22';
DECLARE @JobRoundDepartmentHeadId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff23';

UPDATE dbo.InterviewTemplateRounds
SET OwnerUserId = CASE InterviewTemplateRoundId
    WHEN @RoundScreeningId THEN @RecruiterUserId
    WHEN @RoundTechnicalId THEN @InterviewerUserId
    WHEN @RoundDepartmentHeadId THEN @HiringManagerUserId
    ELSE OwnerUserId
END
WHERE TenantId = @TenantId
  AND InterviewTemplateRoundId IN (@RoundScreeningId, @RoundTechnicalId, @RoundDepartmentHeadId);

UPDATE dbo.JobRequestInterviewRounds
SET OwnerUserId = CASE JobRequestInterviewRoundId
    WHEN @JobRoundScreeningId THEN @RecruiterUserId
    WHEN @JobRoundTechnicalId THEN @InterviewerUserId
    WHEN @JobRoundDepartmentHeadId THEN @HiringManagerUserId
    ELSE OwnerUserId
END
WHERE TenantId = @TenantId
  AND JobRequestInterviewRoundId IN (@JobRoundScreeningId, @JobRoundTechnicalId, @JobRoundDepartmentHeadId);
GO
