SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Harden demo authentication by requiring real credential rows for all seeded AppUsers.
-- Demo role cards now submit these emails plus the shared demo password through POST /api/auth/login.

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @DemoPasswordHash NVARCHAR(500) = N'$2a$10$394j2/GNOR2jpagThC4RWOCkDm2HrM4Mb5nCBrkW3D5OTyQKsH4Nu';

DECLARE @PresalesUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333302';
DECLARE @PmoUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333303';
DECLARE @RecruiterUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333304';
DECLARE @InterviewerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333305';
DECLARE @HiringManagerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333306';
DECLARE @CandidateUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333307';
DECLARE @HodUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333311';

IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NOT NULL
BEGIN
    MERGE dbo.AppUsers AS target
    USING (VALUES
        (@PresalesUserId, N'ai-presales@8pkk57.onmicrosoft.com'),
        (@PmoUserId, N'ai-pmo@8pkk57.onmicrosoft.com'),
        (@RecruiterUserId, N'ai-recruiter@8pkk57.onmicrosoft.com'),
        (@InterviewerUserId, N'ai-interviewer@8pkk57.onmicrosoft.com'),
        (@HiringManagerUserId, N'ai-hiring.manager@8pkk57.onmicrosoft.com'),
        (@CandidateUserId, N'ai-candidate@8pkk57.onmicrosoft.com'),
        (@HodUserId, N'ai-hod.engineering@8pkk57.onmicrosoft.com')
    ) AS source (UserId, Email)
    ON target.UserId = source.UserId
    WHEN MATCHED THEN
        UPDATE SET
            Email = source.Email,
            EmailNormalized = UPPER(source.Email),
            AccountStatus = N'Active',
            DeletedAtUtc = NULL,
            UpdatedAtUtc = @Now;
END;

IF OBJECT_ID(N'dbo.UserCredentials', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.AppUsers', N'U') IS NOT NULL
BEGIN
    MERGE dbo.UserCredentials AS target
    USING (
        SELECT
            AppUsers.UserId,
            AppUsers.TenantId
        FROM dbo.AppUsers
        WHERE AppUsers.TenantId = @TenantId
          AND AppUsers.DeletedAtUtc IS NULL
          AND AppUsers.AccountStatus = N'Active'
          AND AppUsers.UserId IN (
              @PresalesUserId,
              @PmoUserId,
              @RecruiterUserId,
              @InterviewerUserId,
              @HiringManagerUserId,
              @CandidateUserId,
              @HodUserId
          )
    ) AS source
    ON target.UserId = source.UserId
    WHEN MATCHED THEN
        UPDATE SET
            TenantId = source.TenantId,
            PasswordHash = @DemoPasswordHash,
            PasswordUpdatedAtUtc = @Now,
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (UserCredentialId, TenantId, UserId, PasswordHash, PasswordUpdatedAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES (NEWID(), source.TenantId, source.UserId, @DemoPasswordHash, @Now, @Now, @Now);
END;

IF OBJECT_ID(N'dbo.Employees', N'U') IS NOT NULL
BEGIN
    UPDATE employees
    SET Email = users.Email,
        UpdatedAtUtc = @Now
    FROM dbo.Employees AS employees
    INNER JOIN dbo.AppUsers users ON users.UserId = employees.AppUserId
    WHERE employees.TenantId = @TenantId
      AND users.TenantId = @TenantId
      AND users.UserId IN (@PresalesUserId, @PmoUserId, @RecruiterUserId, @InterviewerUserId, @HiringManagerUserId, @HodUserId);
END;

IF OBJECT_ID(N'dbo.Candidates', N'U') IS NOT NULL
BEGIN
    UPDATE candidates
    SET Email = N'ai-candidate@8pkk57.onmicrosoft.com',
        UpdatedAtUtc = @Now
    FROM dbo.Candidates AS candidates
    WHERE candidates.TenantId = @TenantId
      AND candidates.AppUserId = @CandidateUserId;
END;

IF OBJECT_ID(N'dbo.CandidateProspects', N'U') IS NOT NULL
BEGIN
    UPDATE prospects
    SET Email = N'ai-candidate@8pkk57.onmicrosoft.com',
        UpdatedAtUtc = @Now
    FROM dbo.CandidateProspects AS prospects
    WHERE prospects.TenantId = @TenantId
      AND prospects.CandidateId IN (
          SELECT CandidateId
          FROM dbo.Candidates
          WHERE TenantId = @TenantId AND AppUserId = @CandidateUserId
      );
END;

IF OBJECT_ID(N'dbo.CandidateInvitations', N'U') IS NOT NULL
BEGIN
    UPDATE invitations
    SET Email = N'ai-candidate@8pkk57.onmicrosoft.com'
    FROM dbo.CandidateInvitations AS invitations
    WHERE invitations.TenantId = @TenantId
      AND invitations.CandidateId IN (
          SELECT CandidateId
          FROM dbo.Candidates
          WHERE TenantId = @TenantId AND AppUserId = @CandidateUserId
      );
END;
GO
