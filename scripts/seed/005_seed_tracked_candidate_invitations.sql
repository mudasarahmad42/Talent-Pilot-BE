/*
    Seeds tracked invitation applications for the live Senior React Developer job.

    These rows intentionally use deterministic IDs and fixed raw seed tokens in
    this script. The database stores only SHA-256 token hashes in
    CandidateInvitations, so each invitation is uniquely identifiable and
    trackable without persisting raw tokens.
*/

SET NOCOUNT ON;

DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @RecruiterUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333304';
DECLARE @JobRequestId UNIQUEIDENTIFIER;
DECLARE @JobPostId UNIQUEIDENTIFIER;
DECLARE @LinkedInSourceLabelId UNIQUEIDENTIFIER;
DECLARE @CandidateRoleId UNIQUEIDENTIFIER;
DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
DECLARE @PortalOrigin NVARCHAR(200) = N'http://localhost:4200';

SELECT TOP (1)
    @JobRequestId = request.JobRequestId,
    @JobPostId = post.JobPostId
FROM dbo.JobRequests AS request
INNER JOIN dbo.JobPosts AS post
    ON post.TenantId = request.TenantId
    AND post.JobRequestId = request.JobRequestId
WHERE request.TenantId = @TenantId
  AND request.RequestCode = N'TP-REQ-019'
  AND post.Status = N'Published';

SELECT TOP (1) @LinkedInSourceLabelId = CandidateSourceLabelId
FROM dbo.CandidateSourceLabels
WHERE TenantId = @TenantId
  AND Code = N'LinkedInManual';

SELECT TOP (1) @CandidateRoleId = RoleId
FROM dbo.Roles
WHERE TenantId = @TenantId
  AND Code = N'Candidate'
  AND Status = N'Active';

IF @JobRequestId IS NOT NULL
   AND @JobPostId IS NOT NULL
   AND @LinkedInSourceLabelId IS NOT NULL
   AND @CandidateRoleId IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.AppUsers WHERE TenantId = @TenantId AND UserId = @RecruiterUserId)
BEGIN
    DECLARE @Invitees TABLE
    (
        DisplayOrder INT NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CandidateId UNIQUEIDENTIFIER NOT NULL,
        JobApplicationId UNIQUEIDENTIFIER NOT NULL,
        CandidateInvitationId UNIQUEIDENTIFIER NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        Email NVARCHAR(320) NOT NULL,
        Initials NVARCHAR(8) NOT NULL,
        CurrentDesignation NVARCHAR(160) NULL,
        ExperienceYears DECIMAL(4,1) NULL,
        SeedToken NVARCHAR(160) NOT NULL
    );

    INSERT INTO @Invitees
    (
        DisplayOrder,
        UserId,
        CandidateId,
        JobApplicationId,
        CandidateInvitationId,
        DisplayName,
        Email,
        Initials,
        CurrentDesignation,
        ExperienceYears,
        SeedToken
    )
    VALUES
        (1, '2f000000-0000-0000-0000-000000000101', '2f100000-0000-0000-0000-000000000101', '2f200000-0000-0000-0000-000000000101', '2f300000-0000-0000-0000-000000000101', N'Lidia Holloway', N'LidiaH@8pkk57.onmicrosoft.com', N'LH', N'Front-End Engineer', CAST(5.8 AS DECIMAL(4,1)), N'tp-seed-invite-lidia-holloway-2026-06-03'),
        (2, '2f000000-0000-0000-0000-000000000102', '2f100000-0000-0000-0000-000000000102', '2f200000-0000-0000-0000-000000000102', '2f300000-0000-0000-0000-000000000102', N'Lynne Robbins', N'LynneR@8pkk57.onmicrosoft.com', N'LR', N'React Developer', CAST(6.2 AS DECIMAL(4,1)), N'tp-seed-invite-lynne-robbins-2026-06-03'),
        (3, '2f000000-0000-0000-0000-000000000103', '2f100000-0000-0000-0000-000000000103', '2f200000-0000-0000-0000-000000000103', '2f300000-0000-0000-0000-000000000103', N'Megan Bowen', N'MeganB@8pkk57.onmicrosoft.com', N'MB', N'UI Engineer', CAST(4.9 AS DECIMAL(4,1)), N'tp-seed-invite-megan-bowen-2026-06-03'),
        (4, '2f000000-0000-0000-0000-000000000104', '2f100000-0000-0000-0000-000000000104', '2f200000-0000-0000-0000-000000000104', '2f300000-0000-0000-0000-000000000104', N'Nadir', N'Nadir@8pkk57.onmicrosoft.com', N'N', N'JavaScript Engineer', CAST(5.4 AS DECIMAL(4,1)), N'tp-seed-invite-nadir-2026-06-03'),
        (5, '2f000000-0000-0000-0000-000000000105', '2f100000-0000-0000-0000-000000000105', '2f200000-0000-0000-0000-000000000105', '2f300000-0000-0000-0000-000000000105', N'Nestor Wilke', N'NestorW@8pkk57.onmicrosoft.com', N'NW', N'Full Stack Developer', CAST(7.1 AS DECIMAL(4,1)), N'tp-seed-invite-nestor-wilke-2026-06-03');

    MERGE dbo.AppUsers AS target
    USING
    (
        SELECT
            UserId,
            @TenantId AS TenantId,
            DisplayName,
            Email,
            UPPER(Email) AS EmailNormalized,
            Initials
        FROM @Invitees
    ) AS source
        ON target.UserId = source.UserId
    WHEN MATCHED THEN
        UPDATE SET
            DisplayName = source.DisplayName,
            Email = source.Email,
            EmailNormalized = source.EmailNormalized,
            Initials = source.Initials,
            AccountStatus = N'Invited',
            DeletedAtUtc = NULL,
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.UserId, source.TenantId, source.DisplayName, source.Email, source.EmailNormalized, source.Initials, N'Invited', @Now, @Now);

    MERGE dbo.UserCredentials AS target
    USING
    (
        SELECT
            UserId,
            @TenantId AS TenantId
        FROM @Invitees
    ) AS source
        ON target.UserId = source.UserId
    WHEN MATCHED THEN
        UPDATE SET UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (UserCredentialId, TenantId, UserId, PasswordHash, PasswordUpdatedAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES (NEWID(), source.TenantId, source.UserId, NULL, NULL, @Now, @Now);

    MERGE dbo.UserRoles AS target
    USING
    (
        SELECT
            @TenantId AS TenantId,
            UserId,
            @CandidateRoleId AS RoleId,
            @RecruiterUserId AS AssignedByUserId
        FROM @Invitees
    ) AS source
        ON target.TenantId = source.TenantId
        AND target.UserId = source.UserId
        AND target.RoleId = source.RoleId
    WHEN NOT MATCHED THEN
        INSERT (TenantId, UserId, RoleId, AssignedByUserId, CreatedAtUtc)
        VALUES (source.TenantId, source.UserId, source.RoleId, source.AssignedByUserId, @Now);

    MERGE dbo.Candidates AS target
    USING
    (
        SELECT
            CandidateId,
            @TenantId AS TenantId,
            UserId AS AppUserId,
            DisplayName,
            Email,
            CurrentDesignation,
            ExperienceYears
        FROM @Invitees
    ) AS source
        ON target.CandidateId = source.CandidateId
    WHEN MATCHED THEN
        UPDATE SET
            AppUserId = source.AppUserId,
            DisplayName = source.DisplayName,
            Email = source.Email,
            CurrentDesignation = source.CurrentDesignation,
            ExperienceYears = source.ExperienceYears,
            Status = N'Active',
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (CandidateId, TenantId, AppUserId, DisplayName, Email, Phone, LinkedInUrl, CurrentDesignation, CurrentCompany, ExperienceYears, ExpectedSalaryAmount, ExpectedSalaryCurrency, NoticePeriodDays, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.CandidateId, source.TenantId, source.AppUserId, source.DisplayName, source.Email, NULL, NULL, source.CurrentDesignation, NULL, source.ExperienceYears, NULL, NULL, NULL, N'Active', @Now, @Now);

    MERGE dbo.CandidateInvitations AS target
    USING
    (
        SELECT
            CandidateInvitationId,
            @TenantId AS TenantId,
            CandidateId,
            @JobRequestId AS JobRequestId,
            @JobPostId AS JobPostId,
            @RecruiterUserId AS InvitedByUserId,
            LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), CONVERT(VARCHAR(160), SeedToken))), 2)) AS TokenHash,
            Email,
            DisplayOrder
        FROM @Invitees
    ) AS source
        ON target.CandidateInvitationId = source.CandidateInvitationId
    WHEN MATCHED THEN
        UPDATE SET
            CandidateId = source.CandidateId,
            JobRequestId = source.JobRequestId,
            JobPostId = source.JobPostId,
            InvitedByUserId = source.InvitedByUserId,
            TokenHash = source.TokenHash,
            Email = source.Email,
            Status = N'Sent',
            ExpiresAtUtc = DATEADD(DAY, 7, @Now),
            UsedAtUtc = NULL,
            RevokedAtUtc = NULL,
            ResendCount = 0
    WHEN NOT MATCHED THEN
        INSERT (CandidateInvitationId, TenantId, CandidateProspectId, CandidateId, JobRequestId, JobPostId, InvitedByUserId, TokenHash, Email, Status, ExpiresAtUtc, UsedAtUtc, RevokedAtUtc, ResendCount, CreatedAtUtc)
        VALUES (source.CandidateInvitationId, source.TenantId, NULL, source.CandidateId, source.JobRequestId, source.JobPostId, source.InvitedByUserId, source.TokenHash, source.Email, N'Sent', DATEADD(DAY, 7, @Now), NULL, NULL, 0, DATEADD(MINUTE, -source.DisplayOrder, @Now));

    MERGE dbo.JobApplications AS target
    USING
    (
        SELECT
            JobApplicationId,
            @TenantId AS TenantId,
            @JobRequestId AS JobRequestId,
            @JobPostId AS JobPostId,
            CandidateId,
            @LinkedInSourceLabelId AS CandidateSourceLabelId,
            N'LinkedIn' AS SourceLabel,
            N'Invited' AS CurrentStatus,
            CAST(1 AS BIT) AS IsActive,
            CAST(1 AS BIT) AS IsInvited,
            Email,
            DisplayName,
            CandidateInvitationId,
            SeedToken,
            CONCAT(
                @PortalOrigin,
                N'/candidate/jobs/',
                CONVERT(NVARCHAR(36), @JobPostId),
                N'?source=invite&inviteId=',
                CONVERT(NVARCHAR(36), CandidateInvitationId),
                N'&token=',
                SeedToken) AS InviteLink,
            DisplayOrder
        FROM @Invitees
    ) AS source
        ON target.JobApplicationId = source.JobApplicationId
    WHEN MATCHED THEN
        UPDATE SET
            JobRequestId = source.JobRequestId,
            JobPostId = source.JobPostId,
            CandidateId = source.CandidateId,
            CandidateSourceLabelId = source.CandidateSourceLabelId,
            SourceLabel = source.SourceLabel,
            SourceDetail = N'Tracked invitation',
            CurrentStatus = source.CurrentStatus,
            IsActive = source.IsActive,
            IsInvited = source.IsInvited,
            ConfirmedAtUtc = NULL,
            FinalDecisionAtUtc = NULL,
            FinalDecisionReason = NULL,
            AddedByUserId = @RecruiterUserId,
            RecruiterNotes = CONCAT(N'Seeded tracked invitation ', CONVERT(NVARCHAR(36), source.CandidateInvitationId), N'.'),
            SourceUrl = source.InviteLink,
            ApplicationSnapshotJson = CONCAT(
                N'{"seed":true,"source":"TrackedInvitationSeed","jobPostId":"',
                CONVERT(NVARCHAR(36), source.JobPostId),
                N'","candidateInvitationId":"',
                CONVERT(NVARCHAR(36), source.CandidateInvitationId),
                N'","inviteLink":"',
                source.InviteLink,
                N'","tokenHash":"',
                LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), CONVERT(VARCHAR(160), source.SeedToken))), 2)),
                N'"}'),
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (JobApplicationId, TenantId, JobRequestId, JobPostId, CandidateId, CandidateSourceLabelId, SourceLabel, CurrentStatus, ApplicationVersion, IsActive, IsInvited, ConfirmedAtUtc, AppliedAtUtc, FinalDecisionAtUtc, FinalDecisionReason, SourceDetail, SourceUrl, AddedByUserId, RecruiterNotes, CoverLetterText, ApplicationSnapshotJson, CreatedAtUtc, UpdatedAtUtc)
        VALUES
        (
            source.JobApplicationId,
            source.TenantId,
            source.JobRequestId,
            source.JobPostId,
            source.CandidateId,
            source.CandidateSourceLabelId,
            source.SourceLabel,
            source.CurrentStatus,
            1,
            source.IsActive,
            source.IsInvited,
            NULL,
            DATEADD(MINUTE, -source.DisplayOrder, @Now),
            NULL,
            NULL,
            N'Tracked invitation',
            source.InviteLink,
            @RecruiterUserId,
            CONCAT(N'Seeded tracked invitation ', CONVERT(NVARCHAR(36), source.CandidateInvitationId), N'.'),
            NULL,
            CONCAT(
                N'{"seed":true,"source":"TrackedInvitationSeed","jobPostId":"',
                CONVERT(NVARCHAR(36), source.JobPostId),
                N'","candidateInvitationId":"',
                CONVERT(NVARCHAR(36), source.CandidateInvitationId),
                N'","inviteLink":"',
                source.InviteLink,
                N'","tokenHash":"',
                LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), CONVERT(VARCHAR(160), source.SeedToken))), 2)),
                N'"}'),
            DATEADD(MINUTE, -source.DisplayOrder, @Now),
            @Now
        );
END;
GO
