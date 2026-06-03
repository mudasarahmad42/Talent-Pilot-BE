IF OBJECT_ID(N'dbo.InterviewParticipants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InterviewParticipants
    (
        InterviewParticipantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InterviewParticipants PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        InterviewId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        Email NVARCHAR(320) NOT NULL,
        ParticipantRole NVARCHAR(40) NOT NULL,
        IsOptional BIT NOT NULL CONSTRAINT DF_InterviewParticipants_IsOptional DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_InterviewParticipants_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_InterviewParticipants_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_InterviewParticipants_Interviews FOREIGN KEY (InterviewId) REFERENCES dbo.Interviews (InterviewId),
        CONSTRAINT FK_InterviewParticipants_Users FOREIGN KEY (UserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_InterviewParticipants_Role CHECK (ParticipantRole IN (N'Candidate', N'Interviewer', N'HiringManager', N'Recruiter', N'Other')),
        CONSTRAINT UQ_InterviewParticipants_Tenant_Interview_Email UNIQUE (TenantId, InterviewId, Email)
    );
END;
GO

;WITH ExistingParticipants AS
(
    SELECT
        interview.TenantId,
        interview.InterviewId,
        CAST(NULL AS UNIQUEIDENTIFIER) AS UserId,
        candidate.DisplayName,
        candidate.Email,
        N'Candidate' AS ParticipantRole,
        CAST(0 AS BIT) AS IsOptional
    FROM dbo.Interviews AS interview
    INNER JOIN dbo.JobApplications AS application
        ON application.TenantId = interview.TenantId
        AND application.JobApplicationId = interview.JobApplicationId
    INNER JOIN dbo.Candidates AS candidate
        ON candidate.TenantId = application.TenantId
        AND candidate.CandidateId = application.CandidateId

    UNION ALL

    SELECT
        interview.TenantId,
        interview.InterviewId,
        interviewer.UserId,
        interviewer.DisplayName,
        interviewer.Email,
        N'Interviewer' AS ParticipantRole,
        CAST(0 AS BIT) AS IsOptional
    FROM dbo.Interviews AS interview
    INNER JOIN dbo.AppUsers AS interviewer
        ON interviewer.TenantId = interview.TenantId
        AND interviewer.UserId = interview.InterviewerUserId

    UNION ALL

    SELECT
        interview.TenantId,
        interview.InterviewId,
        hiringManager.UserId,
        hiringManager.DisplayName,
        hiringManager.Email,
        N'HiringManager' AS ParticipantRole,
        CAST(1 AS BIT) AS IsOptional
    FROM dbo.Interviews AS interview
    INNER JOIN dbo.JobApplications AS application
        ON application.TenantId = interview.TenantId
        AND application.JobApplicationId = interview.JobApplicationId
    INNER JOIN dbo.JobRequests AS request
        ON request.TenantId = application.TenantId
        AND request.JobRequestId = application.JobRequestId
    INNER JOIN dbo.AppUsers AS hiringManager
        ON hiringManager.TenantId = request.TenantId
        AND hiringManager.UserId = request.HiringManagerUserId
),
DistinctParticipants AS
(
    SELECT
        TenantId,
        InterviewId,
        UserId,
        DisplayName,
        Email,
        ParticipantRole,
        IsOptional,
        ROW_NUMBER() OVER (PARTITION BY TenantId, InterviewId, Email ORDER BY ParticipantRole) AS RowNumber
    FROM ExistingParticipants
    WHERE NULLIF(LTRIM(RTRIM(Email)), N'') IS NOT NULL
)
INSERT INTO dbo.InterviewParticipants
(
    InterviewParticipantId,
    TenantId,
    InterviewId,
    UserId,
    DisplayName,
    Email,
    ParticipantRole,
    IsOptional,
    CreatedAtUtc
)
SELECT
    NEWID(),
    participant.TenantId,
    participant.InterviewId,
    participant.UserId,
    participant.DisplayName,
    participant.Email,
    participant.ParticipantRole,
    participant.IsOptional,
    SYSUTCDATETIME()
FROM DistinctParticipants AS participant
WHERE participant.RowNumber = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.InterviewParticipants AS existingParticipant
      WHERE existingParticipant.TenantId = participant.TenantId
        AND existingParticipant.InterviewId = participant.InterviewId
        AND existingParticipant.Email = participant.Email
  );
GO
