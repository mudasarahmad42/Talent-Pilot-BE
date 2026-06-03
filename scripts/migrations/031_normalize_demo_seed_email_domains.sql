IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.AppUsers
    SET Email = REPLACE(REPLACE(Email, N'.seed@talentpilot.test', N'@8pkk57.onmicrosoft.com'), N'@talentpilot.test', N'@8pkk57.onmicrosoft.com'),
        EmailNormalized = UPPER(REPLACE(REPLACE(Email, N'.seed@talentpilot.test', N'@8pkk57.onmicrosoft.com'), N'@talentpilot.test', N'@8pkk57.onmicrosoft.com')),
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Email LIKE N'%@talentpilot.test';
END;
GO

IF OBJECT_ID(N'dbo.Employees', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.Employees
    SET Email = REPLACE(REPLACE(Email, N'.seed@talentpilot.test', N'@8pkk57.onmicrosoft.com'), N'@talentpilot.test', N'@8pkk57.onmicrosoft.com'),
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Email LIKE N'%@talentpilot.test';
END;
GO

IF OBJECT_ID(N'dbo.Candidates', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.Candidates
    SET Email = REPLACE(REPLACE(Email, N'.seed@talentpilot.test', N'@8pkk57.onmicrosoft.com'), N'@talentpilot.test', N'@8pkk57.onmicrosoft.com'),
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Email LIKE N'%@talentpilot.test';
END;
GO

IF OBJECT_ID(N'dbo.CandidateProspects', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.CandidateProspects
    SET Email = REPLACE(REPLACE(Email, N'.seed@talentpilot.test', N'@8pkk57.onmicrosoft.com'), N'@talentpilot.test', N'@8pkk57.onmicrosoft.com'),
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Email LIKE N'%@talentpilot.test';
END;
GO

IF OBJECT_ID(N'dbo.CandidateInvitations', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.CandidateInvitations
    SET Email = REPLACE(REPLACE(Email, N'.seed@talentpilot.test', N'@8pkk57.onmicrosoft.com'), N'@talentpilot.test', N'@8pkk57.onmicrosoft.com')
    WHERE Email LIKE N'%@talentpilot.test';
END;
GO

IF OBJECT_ID(N'dbo.CandidateEmailTestBackup', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.CandidateEmailTestBackup
    SET OriginalEmail = REPLACE(REPLACE(OriginalEmail, N'.seed@talentpilot.test', N'@8pkk57.onmicrosoft.com'), N'@talentpilot.test', N'@8pkk57.onmicrosoft.com')
    WHERE OriginalEmail LIKE N'%@talentpilot.test';
END;
GO

IF OBJECT_ID(N'dbo.NotificationOutbox', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.NotificationOutbox
    SET RecipientEmail = REPLACE(REPLACE(RecipientEmail, N'.seed@talentpilot.test', N'@8pkk57.onmicrosoft.com'), N'@talentpilot.test', N'@8pkk57.onmicrosoft.com'),
        PayloadJson = REPLACE(REPLACE(PayloadJson, N'.seed@talentpilot.test', N'@8pkk57.onmicrosoft.com'), N'@talentpilot.test', N'@8pkk57.onmicrosoft.com')
    WHERE RecipientEmail LIKE N'%@talentpilot.test'
       OR PayloadJson LIKE N'%@talentpilot.test%';
END;
GO
