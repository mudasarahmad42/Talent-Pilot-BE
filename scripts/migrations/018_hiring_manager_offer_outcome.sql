/*
    Adds Hiring Manager Review offer-letter and final-outcome support.
    This migration is idempotent and preserves existing interview/application data.
*/

IF OBJECT_ID(N'dbo.JobApplications', N'U') IS NOT NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_JobApplications_CurrentStatus'
          AND parent_object_id = OBJECT_ID(N'dbo.JobApplications')
    )
    BEGIN
        ALTER TABLE dbo.JobApplications DROP CONSTRAINT CK_JobApplications_CurrentStatus;
    END;

    ALTER TABLE dbo.JobApplications
    ADD CONSTRAINT CK_JobApplications_CurrentStatus CHECK
    (
        CurrentStatus IN
        (
            N'Invited',
            N'Applied',
            N'Screening',
            N'Interviewing',
            N'HiringManagerReview',
            N'Offered',
            N'OnHold',
            N'OfferDeclined',
            N'Rejected',
            N'Hired',
            N'Joined',
            N'Withdrawn'
        )
    );
END;
GO

IF OBJECT_ID(N'dbo.OfferLetters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OfferLetters
    (
        OfferLetterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OfferLetters PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobApplicationId UNIQUEIDENTIFIER NOT NULL,
        JobPostId UNIQUEIDENTIFIER NULL,
        JobRequestId UNIQUEIDENTIFIER NOT NULL,
        CandidateId UNIQUEIDENTIFIER NOT NULL,
        GeneratedByUserId UNIQUEIDENTIFIER NOT NULL,
        Version INT NOT NULL CONSTRAINT DF_OfferLetters_Version DEFAULT (1),
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_OfferLetters_Status DEFAULT N'Draft',
        CompensationText NVARCHAR(300) NULL,
        StartDate DATE NULL,
        ReportingManager NVARCHAR(160) NULL,
        WorkLocation NVARCHAR(200) NULL,
        Body NVARCHAR(MAX) NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_OfferLetters_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_OfferLetters_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_OfferLetters_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_OfferLetters_Applications FOREIGN KEY (JobApplicationId) REFERENCES dbo.JobApplications (JobApplicationId),
        CONSTRAINT FK_OfferLetters_JobPosts FOREIGN KEY (JobPostId) REFERENCES dbo.JobPosts (JobPostId),
        CONSTRAINT FK_OfferLetters_JobRequests FOREIGN KEY (JobRequestId) REFERENCES dbo.JobRequests (JobRequestId),
        CONSTRAINT FK_OfferLetters_Candidates FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates (CandidateId),
        CONSTRAINT FK_OfferLetters_GeneratedByUser FOREIGN KEY (GeneratedByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_OfferLetters_Status CHECK (Status IN (N'Draft', N'Presented', N'Accepted', N'Declined', N'Cancelled'))
    );
END;
GO

IF OBJECT_ID(N'dbo.OfferPresentationMeetings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OfferPresentationMeetings
    (
        OfferPresentationMeetingId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OfferPresentationMeetings PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        OfferLetterId UNIQUEIDENTIFIER NOT NULL,
        JobApplicationId UNIQUEIDENTIFIER NOT NULL,
        ScheduledByUserId UNIQUEIDENTIFIER NOT NULL,
        MeetingAtUtc DATETIME2(3) NOT NULL,
        LocationText NVARCHAR(300) NOT NULL,
        Notes NVARCHAR(1000) NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_OfferPresentationMeetings_Status DEFAULT N'Scheduled',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_OfferPresentationMeetings_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_OfferPresentationMeetings_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_OfferPresentationMeetings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_OfferPresentationMeetings_OfferLetters FOREIGN KEY (OfferLetterId) REFERENCES dbo.OfferLetters (OfferLetterId),
        CONSTRAINT FK_OfferPresentationMeetings_Applications FOREIGN KEY (JobApplicationId) REFERENCES dbo.JobApplications (JobApplicationId),
        CONSTRAINT FK_OfferPresentationMeetings_ScheduledByUser FOREIGN KEY (ScheduledByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_OfferPresentationMeetings_Status CHECK (Status IN (N'Scheduled', N'Completed', N'Cancelled'))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_OfferLetters_Application_Version'
      AND object_id = OBJECT_ID(N'dbo.OfferLetters')
)
BEGIN
    CREATE INDEX IX_OfferLetters_Application_Version
        ON dbo.OfferLetters (TenantId, JobApplicationId, Version DESC);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_OfferPresentationMeetings_Application'
      AND object_id = OBJECT_ID(N'dbo.OfferPresentationMeetings')
)
BEGIN
    CREATE INDEX IX_OfferPresentationMeetings_Application
        ON dbo.OfferPresentationMeetings (TenantId, JobApplicationId, MeetingAtUtc DESC);
END;
GO

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

IF OBJECT_ID(N'dbo.NotificationEvents', N'U') IS NOT NULL
BEGIN
    MERGE dbo.NotificationEvents AS target
    USING
    (
        SELECT TOP (1)
            TenantId,
            N'OFFER_PRESENTATION_MEETING_SCHEDULED' AS EventCode,
            N'Offer presentation meeting scheduled' AS Name,
            N'User:Candidate' AS DefaultRecipientType,
            N'Active' AS Status
        FROM dbo.Tenants
    ) AS source
    ON target.TenantId = source.TenantId AND target.EventCode = source.EventCode
    WHEN MATCHED THEN UPDATE SET Name = source.Name, DefaultRecipientType = source.DefaultRecipientType, Status = source.Status, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN INSERT (NotificationEventId, TenantId, EventCode, Name, DefaultRecipientType, Status, CreatedAtUtc, UpdatedAtUtc)
    VALUES (NEWID(), source.TenantId, source.EventCode, source.Name, source.DefaultRecipientType, source.Status, @Now, @Now);
END;
GO
