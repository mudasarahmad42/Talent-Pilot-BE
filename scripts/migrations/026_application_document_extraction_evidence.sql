-- 026_application_document_extraction_evidence.sql
-- Persists extracted CV/cover-letter evidence metadata for ranking and semantic search.

SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.JobApplicationDocuments', N'ExtractionStatus') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplicationDocuments
        ADD ExtractionStatus NVARCHAR(32) NOT NULL
            CONSTRAINT DF_JobApplicationDocuments_ExtractionStatus DEFAULT N'Pending';
END;
GO

IF COL_LENGTH(N'dbo.JobApplicationDocuments', N'ExtractedText') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplicationDocuments
        ADD ExtractedText NVARCHAR(MAX) NULL;
END;
GO

IF COL_LENGTH(N'dbo.JobApplicationDocuments', N'ExtractedTextHashSha256') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplicationDocuments
        ADD ExtractedTextHashSha256 CHAR(64) NULL;
END;
GO

IF COL_LENGTH(N'dbo.JobApplicationDocuments', N'ParserVersion') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplicationDocuments
        ADD ParserVersion NVARCHAR(64) NULL;
END;
GO

IF COL_LENGTH(N'dbo.JobApplicationDocuments', N'ExtractedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplicationDocuments
        ADD ExtractedAtUtc DATETIME2(7) NULL;
END;
GO

IF COL_LENGTH(N'dbo.JobApplicationDocuments', N'ExtractionError') IS NULL
BEGIN
    ALTER TABLE dbo.JobApplicationDocuments
        ADD ExtractionError NVARCHAR(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_JobApplicationDocuments_Tenant_Application_Extraction'
      AND object_id = OBJECT_ID(N'dbo.JobApplicationDocuments')
)
BEGIN
    CREATE INDEX IX_JobApplicationDocuments_Tenant_Application_Extraction
        ON dbo.JobApplicationDocuments (TenantId, JobApplicationId, ExtractionStatus, UploadedAtUtc DESC);
END;

DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();

UPDATE dbo.JobApplicationDocuments
SET
    ExtractionStatus = N'Extracted',
    ExtractedText = N'Amara Haq - Senior Java Backend Engineer. Seven years building Java, Spring Boot, microservices, Kafka, PostgreSQL, REST APIs, API design, system design, Docker, and Kubernetes services for banking and marketplace platforms. Backend-focused profile with no React, Angular, JavaScript, TypeScript, CSS, or frontend portal delivery evidence.',
    ExtractedTextHashSha256 = LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), N'Amara Haq - Senior Java Backend Engineer. Seven years building Java, Spring Boot, microservices, Kafka, PostgreSQL, REST APIs, API design, system design, Docker, and Kubernetes services for banking and marketplace platforms. Backend-focused profile with no React, Angular, JavaScript, TypeScript, CSS, or frontend portal delivery evidence.')), 2)),
    ParserVersion = N'docx-wordprocessingml-v1',
    ExtractedAtUtc = @Now,
    ExtractionError = NULL,
    UpdatedAtUtc = @Now
WHERE OriginalFileName = N'Amara_Haq_Java_Backend.docx'
  AND (ExtractedText IS NULL OR ExtractionStatus <> N'Extracted');

UPDATE dbo.JobApplicationDocuments
SET
    ExtractionStatus = N'Extracted',
    ExtractedText = N'Bilal Tariq - Backend Engineer. Experience with Java, Spring Boot, Kafka, PostgreSQL, payment APIs, distributed transactions, REST API integration, CI/CD, Docker, and production monitoring. Backend application evidence only; no current React or JavaScript UI delivery evidence is present.',
    ExtractedTextHashSha256 = LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), N'Bilal Tariq - Backend Engineer. Experience with Java, Spring Boot, Kafka, PostgreSQL, payment APIs, distributed transactions, REST API integration, CI/CD, Docker, and production monitoring. Backend application evidence only; no current React or JavaScript UI delivery evidence is present.')), 2)),
    ParserVersion = N'docx-wordprocessingml-v1',
    ExtractedAtUtc = @Now,
    ExtractionError = NULL,
    UpdatedAtUtc = @Now
WHERE OriginalFileName = N'Bilal_Tariq_Backend.docx'
  AND (ExtractedText IS NULL OR ExtractionStatus <> N'Extracted');

UPDATE dbo.JobApplicationDocuments
SET
    ExtractionStatus = N'Extracted',
    ExtractedText = N'Hira Saleem - Data Engineer. Python, Spark, SQL, Airflow, data pipelines, ETL, lakehouse modeling, and dashboard data quality work. Has limited Java exposure through data platform services, but no React, JavaScript, TypeScript, or frontend product engineering evidence.',
    ExtractedTextHashSha256 = LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), N'Hira Saleem - Data Engineer. Python, Spark, SQL, Airflow, data pipelines, ETL, lakehouse modeling, and dashboard data quality work. Has limited Java exposure through data platform services, but no React, JavaScript, TypeScript, or frontend product engineering evidence.')), 2)),
    ParserVersion = N'docx-wordprocessingml-v1',
    ExtractedAtUtc = @Now,
    ExtractionError = NULL,
    UpdatedAtUtc = @Now
WHERE OriginalFileName = N'Hira_Saleem_Data.docx'
  AND (ExtractedText IS NULL OR ExtractionStatus <> N'Extracted');
GO
