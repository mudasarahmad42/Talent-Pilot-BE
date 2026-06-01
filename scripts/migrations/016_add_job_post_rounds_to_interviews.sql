-- Talent Pilot migration 016
-- Adds job-post interview round linkage so recruiter-scheduled interviews use the runtime rounds copied to each published job post.

IF COL_LENGTH(N'dbo.Interviews', N'JobPostInterviewRoundId') IS NULL
BEGIN
    ALTER TABLE dbo.Interviews
    ADD JobPostInterviewRoundId UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH(N'dbo.Interviews', N'JobPostInterviewRoundId') IS NOT NULL
   AND OBJECT_ID(N'dbo.JobPostInterviewRounds', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_Interviews_JobPostRounds'
          AND parent_object_id = OBJECT_ID(N'dbo.Interviews')
   )
BEGIN
    ALTER TABLE dbo.Interviews
    ADD CONSTRAINT FK_Interviews_JobPostRounds FOREIGN KEY (JobPostInterviewRoundId) REFERENCES dbo.JobPostInterviewRounds (JobPostInterviewRoundId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Interviews_Application_PostRound' AND object_id = OBJECT_ID(N'dbo.Interviews'))
BEGIN
    CREATE INDEX IX_Interviews_Application_PostRound
    ON dbo.Interviews (TenantId, JobApplicationId, JobPostInterviewRoundId, Status);
END;
GO
