SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*
    Adds optional tenant-provided client/domain context to Job Requests.
    AI agents use this as plain-text evidence for job description drafting,
    requirement embeddings, bench matching, rediscovery, and sourcing context.
*/

IF COL_LENGTH(N'dbo.JobRequests', N'ClientContext') IS NULL
BEGIN
    ALTER TABLE dbo.JobRequests ADD ClientContext NVARCHAR(MAX) NULL;
END;
GO
