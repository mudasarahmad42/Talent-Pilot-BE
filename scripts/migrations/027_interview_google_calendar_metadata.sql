IF COL_LENGTH('dbo.Interviews', 'CalendarProvider') IS NULL
BEGIN
    ALTER TABLE dbo.Interviews ADD CalendarProvider NVARCHAR(40) NULL;
END;
GO

IF COL_LENGTH('dbo.Interviews', 'CalendarEventId') IS NULL
BEGIN
    ALTER TABLE dbo.Interviews ADD CalendarEventId NVARCHAR(300) NULL;
END;
GO

IF COL_LENGTH('dbo.Interviews', 'CalendarEventHtmlLink') IS NULL
BEGIN
    ALTER TABLE dbo.Interviews ADD CalendarEventHtmlLink NVARCHAR(1000) NULL;
END;
GO
