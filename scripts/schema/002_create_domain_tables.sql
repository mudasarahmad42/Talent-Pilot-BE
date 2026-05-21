SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.TenantAiSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantAiSettings
    (
        TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TenantAiSettings PRIMARY KEY,
        ProviderMode NVARCHAR(60) NOT NULL CONSTRAINT DF_TenantAiSettings_ProviderMode DEFAULT N'Mock/Ollama',
        LlmModel NVARCHAR(120) NOT NULL CONSTRAINT DF_TenantAiSettings_LlmModel DEFAULT N'llama3.1:8b',
        EmbeddingModel NVARCHAR(120) NOT NULL CONSTRAINT DF_TenantAiSettings_EmbeddingModel DEFAULT N'nomic-embed-text',
        EmbeddingDimensions INT NOT NULL CONSTRAINT DF_TenantAiSettings_EmbeddingDimensions DEFAULT (768),
        VectorStore NVARCHAR(80) NOT NULL CONSTRAINT DF_TenantAiSettings_VectorStore DEFAULT N'SqlServerVector',
        ModelSwitchingLocked BIT NOT NULL CONSTRAINT DF_TenantAiSettings_ModelSwitchingLocked DEFAULT (1),
        HumanReviewRequired BIT NOT NULL CONSTRAINT DF_TenantAiSettings_HumanReviewRequired DEFAULT (1),
        AutoRejectEnabled BIT NOT NULL CONSTRAINT DF_TenantAiSettings_AutoRejectEnabled DEFAULT (0),
        AutomaticStageMovementEnabled BIT NOT NULL CONSTRAINT DF_TenantAiSettings_AutomaticStageMovementEnabled DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_TenantAiSettings_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_TenantAiSettings_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_TenantAiSettings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT CK_TenantAiSettings_EmbeddingDimensions CHECK (EmbeddingDimensions = 768),
        CONSTRAINT CK_TenantAiSettings_AutoRejectEnabled CHECK (AutoRejectEnabled = 0),
        CONSTRAINT CK_TenantAiSettings_AutomaticStageMovementEnabled CHECK (AutomaticStageMovementEnabled = 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.RoleAssignmentBatches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoleAssignmentBatches
    (
        RoleAssignmentBatchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RoleAssignmentBatches PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        RoleId UNIQUEIDENTIFIER NOT NULL,
        FilterJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_RoleAssignmentBatches_FilterJson DEFAULT N'{}',
        SelectionMode NVARCHAR(40) NOT NULL,
        SelectedUserIdsJson NVARCHAR(MAX) NULL,
        MatchedCount INT NOT NULL CONSTRAINT DF_RoleAssignmentBatches_MatchedCount DEFAULT (0),
        AssignedCount INT NOT NULL CONSTRAINT DF_RoleAssignmentBatches_AssignedCount DEFAULT (0),
        SkippedCount INT NOT NULL CONSTRAINT DF_RoleAssignmentBatches_SkippedCount DEFAULT (0),
        CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_RoleAssignmentBatches_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_RoleAssignmentBatches_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_RoleAssignmentBatches_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId),
        CONSTRAINT FK_RoleAssignmentBatches_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_RoleAssignmentBatches_SelectionMode CHECK (SelectionMode IN (N'AllFilteredUsers', N'SelectedUsers')),
        CONSTRAINT CK_RoleAssignmentBatches_FilterJson CHECK (ISJSON(FilterJson) = 1),
        CONSTRAINT CK_RoleAssignmentBatches_SelectedUserIdsJson CHECK (SelectedUserIdsJson IS NULL OR ISJSON(SelectedUserIdsJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'dbo.Departments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Departments
    (
        DepartmentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Departments PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Code NVARCHAR(80) NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        LeadUserId UNIQUEIDENTIFIER NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Departments_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Departments_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Departments_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_Departments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_Departments_LeadUser FOREIGN KEY (LeadUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_Departments_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT UQ_Departments_Tenant_Code UNIQUE (TenantId, Code),
        CONSTRAINT UQ_Departments_Tenant_Name UNIQUE (TenantId, Name)
    );
END;
GO

IF OBJECT_ID(N'dbo.Locations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Locations
    (
        LocationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Locations PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Code NVARCHAR(80) NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        CountryCode CHAR(2) NOT NULL,
        TimezoneId NVARCHAR(100) NOT NULL,
        IsRemote BIT NOT NULL CONSTRAINT DF_Locations_IsRemote DEFAULT (0),
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Locations_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Locations_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Locations_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Locations_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT CK_Locations_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT UQ_Locations_Tenant_Code UNIQUE (TenantId, Code)
    );
END;
GO

IF OBJECT_ID(N'dbo.Skills', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Skills
    (
        SkillId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Skills PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        NormalizedName NVARCHAR(160) NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        AliasesJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Skills_AliasesJson DEFAULT N'[]',
        IsVectorRelevant BIT NOT NULL CONSTRAINT DF_Skills_IsVectorRelevant DEFAULT (1),
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Skills_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Skills_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Skills_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Skills_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT CK_Skills_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT CK_Skills_AliasesJson CHECK (ISJSON(AliasesJson) = 1),
        CONSTRAINT UQ_Skills_Tenant_NormalizedName UNIQUE (TenantId, NormalizedName)
    );
END;
GO

IF OBJECT_ID(N'dbo.CandidateSourceLabels', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateSourceLabels
    (
        CandidateSourceLabelId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CandidateSourceLabels PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Code NVARCHAR(80) NOT NULL,
        DisplayName NVARCHAR(120) NOT NULL,
        ReportingCategory NVARCHAR(120) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_CandidateSourceLabels_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateSourceLabels_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateSourceLabels_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CandidateSourceLabels_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT CK_CandidateSourceLabels_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT UQ_CandidateSourceLabels_Tenant_Code UNIQUE (TenantId, Code),
        CONSTRAINT UQ_CandidateSourceLabels_Tenant_DisplayName UNIQUE (TenantId, DisplayName)
    );
END;
GO

IF OBJECT_ID(N'dbo.Projects', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Projects
    (
        ProjectId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Projects PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        DepartmentId UNIQUEIDENTIFIER NULL,
        Code NVARCHAR(80) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        ClientName NVARCHAR(200) NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_Projects_Status DEFAULT N'Active',
        StartsOn DATE NULL,
        EndsOn DATE NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Projects_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Projects_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Projects_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_Projects_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (DepartmentId),
        CONSTRAINT CK_Projects_Status CHECK (Status IN (N'Active', N'Closed', N'Paused')),
        CONSTRAINT UQ_Projects_Tenant_Code UNIQUE (TenantId, Code)
    );
END;
GO

IF OBJECT_ID(N'dbo.Employees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees
    (
        EmployeeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Employees PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AppUserId UNIQUEIDENTIFIER NULL,
        EmployeeCode NVARCHAR(80) NOT NULL,
        ExternalEmployeeId NVARCHAR(120) NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        Email NVARCHAR(320) NOT NULL,
        DepartmentId UNIQUEIDENTIFIER NULL,
        LocationId UNIQUEIDENTIFIER NULL,
        Designation NVARCHAR(160) NULL,
        ExperienceYears DECIMAL(4,1) NULL,
        AvailabilityStatus NVARCHAR(40) NOT NULL CONSTRAINT DF_Employees_AvailabilityStatus DEFAULT N'Available',
        BenchStatus NVARCHAR(40) NOT NULL CONSTRAINT DF_Employees_BenchStatus DEFAULT N'Benched',
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Employees_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Employees_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Employees_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_Employees_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_Employees_AppUsers FOREIGN KEY (AppUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT FK_Employees_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (DepartmentId),
        CONSTRAINT FK_Employees_Locations FOREIGN KEY (LocationId) REFERENCES dbo.Locations (LocationId),
        CONSTRAINT CK_Employees_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT CK_Employees_AvailabilityStatus CHECK (AvailabilityStatus IN (N'Available', N'Allocated', N'PartiallyAllocated')),
        CONSTRAINT CK_Employees_BenchStatus CHECK (BenchStatus IN (N'Benched', N'Allocated', N'PartialBench')),
        CONSTRAINT UQ_Employees_Tenant_Code UNIQUE (TenantId, EmployeeCode),
        CONSTRAINT UQ_Employees_Tenant_Email UNIQUE (TenantId, Email)
    );
END;
GO

IF OBJECT_ID(N'dbo.EmployeeSkills', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeSkills
    (
        TenantId UNIQUEIDENTIFIER NOT NULL,
        EmployeeId UNIQUEIDENTIFIER NOT NULL,
        SkillId UNIQUEIDENTIFIER NOT NULL,
        SkillLevel NVARCHAR(40) NOT NULL CONSTRAINT DF_EmployeeSkills_SkillLevel DEFAULT N'Intermediate',
        YearsExperience DECIMAL(4,1) NULL,
        IsPrimary BIT NOT NULL CONSTRAINT DF_EmployeeSkills_IsPrimary DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EmployeeSkills_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_EmployeeSkills PRIMARY KEY (TenantId, EmployeeId, SkillId),
        CONSTRAINT FK_EmployeeSkills_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_EmployeeSkills_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (EmployeeId),
        CONSTRAINT FK_EmployeeSkills_Skills FOREIGN KEY (SkillId) REFERENCES dbo.Skills (SkillId)
    );
END;
GO

IF OBJECT_ID(N'dbo.EmployeeProjectAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeProjectAssignments
    (
        ProjectAssignmentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmployeeProjectAssignments PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        EmployeeId UNIQUEIDENTIFIER NOT NULL,
        ProjectId UNIQUEIDENTIFIER NOT NULL,
        AllocationPercent INT NOT NULL CONSTRAINT DF_EmployeeProjectAssignments_AllocationPercent DEFAULT (100),
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_EmployeeProjectAssignments_Status DEFAULT N'Active',
        StartsOn DATE NOT NULL,
        EndsOn DATE NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EmployeeProjectAssignments_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EmployeeProjectAssignments_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_EmployeeProjectAssignments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_EmployeeProjectAssignments_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (EmployeeId),
        CONSTRAINT FK_EmployeeProjectAssignments_Projects FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (ProjectId),
        CONSTRAINT CK_EmployeeProjectAssignments_Allocation CHECK (AllocationPercent BETWEEN 1 AND 100),
        CONSTRAINT CK_EmployeeProjectAssignments_Status CHECK (Status IN (N'Active', N'Completed', N'Cancelled'))
    );
END;
GO

IF OBJECT_ID(N'dbo.Candidates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Candidates
    (
        CandidateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Candidates PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AppUserId UNIQUEIDENTIFIER NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        Email NVARCHAR(320) NOT NULL,
        Phone NVARCHAR(50) NULL,
        LinkedInUrl NVARCHAR(500) NULL,
        CurrentDesignation NVARCHAR(160) NULL,
        CurrentCompany NVARCHAR(200) NULL,
        ExperienceYears DECIMAL(4,1) NULL,
        ExpectedSalaryAmount DECIMAL(18,2) NULL,
        ExpectedSalaryCurrency CHAR(3) NULL,
        NoticePeriodDays INT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_Candidates_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Candidates_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Candidates_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Candidates_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_Candidates_AppUsers FOREIGN KEY (AppUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_Candidates_Status CHECK (Status IN (N'Active', N'Inactive', N'Hired')),
        CONSTRAINT UQ_Candidates_Tenant_AppUser UNIQUE (TenantId, AppUserId),
        CONSTRAINT UQ_Candidates_Tenant_Email UNIQUE (TenantId, Email)
    );
END;
GO

IF OBJECT_ID(N'dbo.CandidateDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateDocuments
    (
        CandidateDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CandidateDocuments PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CandidateId UNIQUEIDENTIFIER NOT NULL,
        FileName NVARCHAR(260) NOT NULL,
        FileExtension NVARCHAR(20) NOT NULL,
        ContentType NVARCHAR(120) NOT NULL,
        StoragePath NVARCHAR(600) NOT NULL,
        ExtractedTextHash NVARCHAR(128) NULL,
        IsCurrent BIT NOT NULL CONSTRAINT DF_CandidateDocuments_IsCurrent DEFAULT (1),
        UploadedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateDocuments_UploadedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CandidateDocuments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_CandidateDocuments_Candidates FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates (CandidateId),
        CONSTRAINT CK_CandidateDocuments_FileExtension CHECK (FileExtension = N'.docx')
    );
END;
GO

IF OBJECT_ID(N'dbo.CandidateSkills', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateSkills
    (
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CandidateId UNIQUEIDENTIFIER NOT NULL,
        SkillId UNIQUEIDENTIFIER NOT NULL,
        SkillLevel NVARCHAR(40) NOT NULL CONSTRAINT DF_CandidateSkills_SkillLevel DEFAULT N'Intermediate',
        YearsExperience DECIMAL(4,1) NULL,
        IsPrimary BIT NOT NULL CONSTRAINT DF_CandidateSkills_IsPrimary DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateSkills_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_CandidateSkills PRIMARY KEY (TenantId, CandidateId, SkillId),
        CONSTRAINT FK_CandidateSkills_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_CandidateSkills_Candidates FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates (CandidateId),
        CONSTRAINT FK_CandidateSkills_Skills FOREIGN KEY (SkillId) REFERENCES dbo.Skills (SkillId)
    );
END;
GO

IF OBJECT_ID(N'dbo.CandidateProspects', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateProspects
    (
        CandidateProspectId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CandidateProspects PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        Email NVARCHAR(320) NOT NULL,
        Phone NVARCHAR(50) NULL,
        LinkedInUrl NVARCHAR(500) NULL,
        CandidateSourceLabelId UNIQUEIDENTIFIER NULL,
        SourceLabel NVARCHAR(80) NOT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_CandidateProspects_Status DEFAULT N'Sourced',
        CandidateId UNIQUEIDENTIFIER NULL,
        CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateProspects_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateProspects_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CandidateProspects_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_CandidateProspects_SourceLabels FOREIGN KEY (CandidateSourceLabelId) REFERENCES dbo.CandidateSourceLabels (CandidateSourceLabelId),
        CONSTRAINT FK_CandidateProspects_Candidates FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates (CandidateId),
        CONSTRAINT FK_CandidateProspects_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_CandidateProspects_Status CHECK (Status IN (N'Sourced', N'Invited', N'Registered', N'Archived'))
    );
END;
GO

IF OBJECT_ID(N'dbo.JobRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobRequests
    (
        JobRequestId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_JobRequests PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        RequestCode NVARCHAR(60) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        ClientName NVARCHAR(200) NULL,
        DepartmentId UNIQUEIDENTIFIER NULL,
        LocationId UNIQUEIDENTIFIER NULL,
        EmploymentType NVARCHAR(60) NOT NULL CONSTRAINT DF_JobRequests_EmploymentType DEFAULT N'FullTime',
        ExperienceMinYears DECIMAL(4,1) NULL,
        ExperienceMaxYears DECIMAL(4,1) NULL,
        Priority NVARCHAR(30) NOT NULL CONSTRAINT DF_JobRequests_Priority DEFAULT N'Normal',
        RequiredPositions INT NOT NULL CONSTRAINT DF_JobRequests_RequiredPositions DEFAULT (1),
        FulfilledPositions INT NOT NULL CONSTRAINT DF_JobRequests_FulfilledPositions DEFAULT (0),
        Status NVARCHAR(40) NOT NULL CONSTRAINT DF_JobRequests_Status DEFAULT N'PMOReview',
        PublishStatus NVARCHAR(40) NOT NULL CONSTRAINT DF_JobRequests_PublishStatus DEFAULT N'NotPublished',
        HiringManagerUserId UNIQUEIDENTIFIER NULL,
        HiringManagerGroupId UNIQUEIDENTIFIER NULL,
        CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
        CurrentStageKey NVARCHAR(80) NOT NULL CONSTRAINT DF_JobRequests_CurrentStageKey DEFAULT N'PMO_REVIEW',
        CurrentAssignmentId UNIQUEIDENTIFIER NULL,
        PublishedAtUtc DATETIME2(3) NULL,
        ClosedAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobRequests_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobRequests_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_JobRequests_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_JobRequests_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (DepartmentId),
        CONSTRAINT FK_JobRequests_Locations FOREIGN KEY (LocationId) REFERENCES dbo.Locations (LocationId),
        CONSTRAINT FK_JobRequests_HiringManagerUser FOREIGN KEY (HiringManagerUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT FK_JobRequests_HiringManagerGroup FOREIGN KEY (HiringManagerGroupId) REFERENCES dbo.Groups (GroupId),
        CONSTRAINT FK_JobRequests_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_JobRequests_Positions CHECK (RequiredPositions > 0 AND FulfilledPositions >= 0 AND FulfilledPositions <= RequiredPositions),
        CONSTRAINT CK_JobRequests_Status CHECK (Status IN (N'PMOReview', N'BenchReview', N'Sourcing', N'Interviewing', N'HiringManagerReview', N'Offer', N'Closed', N'Cancelled')),
        CONSTRAINT CK_JobRequests_PublishStatus CHECK (PublishStatus IN (N'NotPublished', N'Published', N'Unpublished')),
        CONSTRAINT UQ_JobRequests_Tenant_RequestCode UNIQUE (TenantId, RequestCode)
    );
END;
GO

IF OBJECT_ID(N'dbo.JobRequestSkills', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobRequestSkills
    (
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobRequestId UNIQUEIDENTIFIER NOT NULL,
        SkillId UNIQUEIDENTIFIER NOT NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_JobRequestSkills_IsRequired DEFAULT (1),
        Weight INT NOT NULL CONSTRAINT DF_JobRequestSkills_Weight DEFAULT (1),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobRequestSkills_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_JobRequestSkills PRIMARY KEY (TenantId, JobRequestId, SkillId),
        CONSTRAINT FK_JobRequestSkills_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_JobRequestSkills_JobRequests FOREIGN KEY (JobRequestId) REFERENCES dbo.JobRequests (JobRequestId),
        CONSTRAINT FK_JobRequestSkills_Skills FOREIGN KEY (SkillId) REFERENCES dbo.Skills (SkillId),
        CONSTRAINT CK_JobRequestSkills_Weight CHECK (Weight BETWEEN 1 AND 10)
    );
END;
GO

IF OBJECT_ID(N'dbo.CandidateProspectJobRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateProspectJobRequests
    (
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CandidateProspectId UNIQUEIDENTIFIER NOT NULL,
        JobRequestId UNIQUEIDENTIFIER NOT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_CandidateProspectJobRequests_Status DEFAULT N'Sourced',
        Notes NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateProspectJobRequests_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_CandidateProspectJobRequests PRIMARY KEY (TenantId, CandidateProspectId, JobRequestId),
        CONSTRAINT FK_CandidateProspectJobRequests_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_CandidateProspectJobRequests_Prospects FOREIGN KEY (CandidateProspectId) REFERENCES dbo.CandidateProspects (CandidateProspectId),
        CONSTRAINT FK_CandidateProspectJobRequests_JobRequests FOREIGN KEY (JobRequestId) REFERENCES dbo.JobRequests (JobRequestId)
    );
END;
GO

IF OBJECT_ID(N'dbo.CandidateInvitations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateInvitations
    (
        CandidateInvitationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CandidateInvitations PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CandidateProspectId UNIQUEIDENTIFIER NULL,
        CandidateId UNIQUEIDENTIFIER NULL,
        JobRequestId UNIQUEIDENTIFIER NULL,
        InvitedByUserId UNIQUEIDENTIFIER NOT NULL,
        TokenHash NVARCHAR(256) NOT NULL,
        Email NVARCHAR(320) NOT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_CandidateInvitations_Status DEFAULT N'Sent',
        ExpiresAtUtc DATETIME2(3) NOT NULL,
        UsedAtUtc DATETIME2(3) NULL,
        RevokedAtUtc DATETIME2(3) NULL,
        ResendCount INT NOT NULL CONSTRAINT DF_CandidateInvitations_ResendCount DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateInvitations_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CandidateInvitations_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_CandidateInvitations_Prospects FOREIGN KEY (CandidateProspectId) REFERENCES dbo.CandidateProspects (CandidateProspectId),
        CONSTRAINT FK_CandidateInvitations_Candidates FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates (CandidateId),
        CONSTRAINT FK_CandidateInvitations_JobRequests FOREIGN KEY (JobRequestId) REFERENCES dbo.JobRequests (JobRequestId),
        CONSTRAINT FK_CandidateInvitations_InvitedByUser FOREIGN KEY (InvitedByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_CandidateInvitations_Status CHECK (Status IN (N'Sent', N'Used', N'Expired', N'Revoked')),
        CONSTRAINT UQ_CandidateInvitations_TokenHash UNIQUE (TokenHash)
    );
END;
GO

IF OBJECT_ID(N'dbo.JobApplications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobApplications
    (
        JobApplicationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_JobApplications PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobRequestId UNIQUEIDENTIFIER NOT NULL,
        CandidateId UNIQUEIDENTIFIER NOT NULL,
        CandidateSourceLabelId UNIQUEIDENTIFIER NULL,
        SourceLabel NVARCHAR(80) NOT NULL,
        CurrentStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_JobApplications_CurrentStatus DEFAULT N'Applied',
        ApplicationVersion INT NOT NULL CONSTRAINT DF_JobApplications_ApplicationVersion DEFAULT (1),
        IsActive BIT NOT NULL CONSTRAINT DF_JobApplications_IsActive DEFAULT (1),
        IsInvited BIT NOT NULL CONSTRAINT DF_JobApplications_IsInvited DEFAULT (0),
        ConfirmedAtUtc DATETIME2(3) NULL,
        AppliedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobApplications_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
        FinalDecisionAtUtc DATETIME2(3) NULL,
        FinalDecisionReason NVARCHAR(500) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobApplications_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobApplications_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_JobApplications_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_JobApplications_JobRequests FOREIGN KEY (JobRequestId) REFERENCES dbo.JobRequests (JobRequestId),
        CONSTRAINT FK_JobApplications_Candidates FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates (CandidateId),
        CONSTRAINT FK_JobApplications_SourceLabels FOREIGN KEY (CandidateSourceLabelId) REFERENCES dbo.CandidateSourceLabels (CandidateSourceLabelId),
        CONSTRAINT CK_JobApplications_CurrentStatus CHECK (CurrentStatus IN (N'Invited', N'Applied', N'Screening', N'Interviewing', N'OnHold', N'OfferDeclined', N'Rejected', N'Hired', N'Withdrawn')),
        CONSTRAINT UQ_JobApplications_Tenant_Job_Candidate_Version UNIQUE (TenantId, JobRequestId, CandidateId, ApplicationVersion)
    );
END;
GO

IF OBJECT_ID(N'dbo.JobApplicationStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobApplicationStatusHistory
    (
        JobApplicationStatusHistoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_JobApplicationStatusHistory PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobApplicationId UNIQUEIDENTIFIER NOT NULL,
        FromStatus NVARCHAR(50) NULL,
        ToStatus NVARCHAR(50) NOT NULL,
        ChangedByUserId UNIQUEIDENTIFIER NULL,
        ChangedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobApplicationStatusHistory_ChangedAtUtc DEFAULT SYSUTCDATETIME(),
        Notes NVARCHAR(1000) NULL,
        CONSTRAINT FK_JobApplicationStatusHistory_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_JobApplicationStatusHistory_Applications FOREIGN KEY (JobApplicationId) REFERENCES dbo.JobApplications (JobApplicationId),
        CONSTRAINT FK_JobApplicationStatusHistory_ChangedByUser FOREIGN KEY (ChangedByUserId) REFERENCES dbo.AppUsers (UserId)
    );
END;
GO

IF OBJECT_ID(N'dbo.JobRequestEmployeeReferrals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobRequestEmployeeReferrals
    (
        JobRequestEmployeeReferralId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_JobRequestEmployeeReferrals PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobRequestId UNIQUEIDENTIFIER NOT NULL,
        EmployeeId UNIQUEIDENTIFIER NOT NULL,
        ReferredByUserId UNIQUEIDENTIFIER NOT NULL,
        PresalesUserId UNIQUEIDENTIFIER NULL,
        Status NVARCHAR(40) NOT NULL CONSTRAINT DF_JobRequestEmployeeReferrals_Status DEFAULT N'Referred',
        FitScore DECIMAL(5,2) NULL,
        RecommendationSummary NVARCHAR(1500) NULL,
        ClientFeedback NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobRequestEmployeeReferrals_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobRequestEmployeeReferrals_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_JobRequestEmployeeReferrals_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_JobRequestEmployeeReferrals_JobRequests FOREIGN KEY (JobRequestId) REFERENCES dbo.JobRequests (JobRequestId),
        CONSTRAINT FK_JobRequestEmployeeReferrals_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (EmployeeId),
        CONSTRAINT FK_JobRequestEmployeeReferrals_ReferredByUser FOREIGN KEY (ReferredByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT FK_JobRequestEmployeeReferrals_PresalesUser FOREIGN KEY (PresalesUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_JobRequestEmployeeReferrals_Status CHECK (Status IN (N'Referred', N'AcceptedByPresales', N'RejectedByPresales', N'ClientAccepted', N'ClientRejected'))
    );
END;
GO

IF OBJECT_ID(N'dbo.JobRequestFulfillments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobRequestFulfillments
    (
        JobRequestFulfillmentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_JobRequestFulfillments PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobRequestId UNIQUEIDENTIFIER NOT NULL,
        JobRequestEmployeeReferralId UNIQUEIDENTIFIER NULL,
        JobApplicationId UNIQUEIDENTIFIER NULL,
        EmployeeId UNIQUEIDENTIFIER NULL,
        CandidateId UNIQUEIDENTIFIER NULL,
        FulfilledByUserId UNIQUEIDENTIFIER NOT NULL,
        FulfillmentType NVARCHAR(40) NOT NULL,
        Status NVARCHAR(40) NOT NULL CONSTRAINT DF_JobRequestFulfillments_Status DEFAULT N'Completed',
        FulfilledAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JobRequestFulfillments_FulfilledAtUtc DEFAULT SYSUTCDATETIME(),
        Notes NVARCHAR(1000) NULL,
        CONSTRAINT FK_JobRequestFulfillments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_JobRequestFulfillments_JobRequests FOREIGN KEY (JobRequestId) REFERENCES dbo.JobRequests (JobRequestId),
        CONSTRAINT FK_JobRequestFulfillments_Referrals FOREIGN KEY (JobRequestEmployeeReferralId) REFERENCES dbo.JobRequestEmployeeReferrals (JobRequestEmployeeReferralId),
        CONSTRAINT FK_JobRequestFulfillments_Applications FOREIGN KEY (JobApplicationId) REFERENCES dbo.JobApplications (JobApplicationId),
        CONSTRAINT FK_JobRequestFulfillments_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (EmployeeId),
        CONSTRAINT FK_JobRequestFulfillments_Candidates FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates (CandidateId),
        CONSTRAINT FK_JobRequestFulfillments_FulfilledByUser FOREIGN KEY (FulfilledByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_JobRequestFulfillments_FulfillmentType CHECK (FulfillmentType IN (N'InternalEmployee', N'ExternalCandidate')),
        CONSTRAINT CK_JobRequestFulfillments_Status CHECK (Status IN (N'Completed', N'Reversed'))
    );
END;
GO

IF OBJECT_ID(N'dbo.CandidateEmployeeLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateEmployeeLinks
    (
        CandidateEmployeeLinkId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CandidateEmployeeLinks PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CandidateId UNIQUEIDENTIFIER NOT NULL,
        JobApplicationId UNIQUEIDENTIFIER NULL,
        JobRequestFulfillmentId UNIQUEIDENTIFIER NULL,
        EmployeeId UNIQUEIDENTIFIER NULL,
        Status NVARCHAR(40) NOT NULL CONSTRAINT DF_CandidateEmployeeLinks_Status DEFAULT N'PendingExternalSync',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateEmployeeLinks_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CandidateEmployeeLinks_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_CandidateEmployeeLinks_Candidates FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates (CandidateId),
        CONSTRAINT FK_CandidateEmployeeLinks_Applications FOREIGN KEY (JobApplicationId) REFERENCES dbo.JobApplications (JobApplicationId),
        CONSTRAINT FK_CandidateEmployeeLinks_Fulfillments FOREIGN KEY (JobRequestFulfillmentId) REFERENCES dbo.JobRequestFulfillments (JobRequestFulfillmentId),
        CONSTRAINT FK_CandidateEmployeeLinks_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (EmployeeId)
    );
END;
GO

IF OBJECT_ID(N'dbo.InterviewTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InterviewTemplates
    (
        InterviewTemplateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InterviewTemplates PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        DepartmentId UNIQUEIDENTIFIER NULL,
        Name NVARCHAR(200) NOT NULL,
        Description NVARCHAR(500) NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_InterviewTemplates_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_InterviewTemplates_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_InterviewTemplates_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_InterviewTemplates_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_InterviewTemplates_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (DepartmentId),
        CONSTRAINT CK_InterviewTemplates_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT UQ_InterviewTemplates_Tenant_Name UNIQUE (TenantId, Name)
    );
END;
GO

IF OBJECT_ID(N'dbo.InterviewTemplateRounds', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InterviewTemplateRounds
    (
        InterviewTemplateRoundId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InterviewTemplateRounds PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        InterviewTemplateId UNIQUEIDENTIFIER NOT NULL,
        RoundOrder INT NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        OwnerRoleId UNIQUEIDENTIFIER NULL,
        DurationMinutes INT NOT NULL CONSTRAINT DF_InterviewTemplateRounds_Duration DEFAULT (60),
        IsRequired BIT NOT NULL CONSTRAINT DF_InterviewTemplateRounds_IsRequired DEFAULT (1),
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_InterviewTemplateRounds_Status DEFAULT N'Active',
        CONSTRAINT FK_InterviewTemplateRounds_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_InterviewTemplateRounds_Templates FOREIGN KEY (InterviewTemplateId) REFERENCES dbo.InterviewTemplates (InterviewTemplateId),
        CONSTRAINT FK_InterviewTemplateRounds_OwnerRole FOREIGN KEY (OwnerRoleId) REFERENCES dbo.Roles (RoleId),
        CONSTRAINT UQ_InterviewTemplateRounds_Template_Order UNIQUE (InterviewTemplateId, RoundOrder)
    );
END;
GO

IF OBJECT_ID(N'dbo.JobRequestInterviewRounds', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobRequestInterviewRounds
    (
        JobRequestInterviewRoundId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_JobRequestInterviewRounds PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobRequestId UNIQUEIDENTIFIER NOT NULL,
        InterviewTemplateRoundId UNIQUEIDENTIFIER NULL,
        RoundOrder INT NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        OwnerRoleId UNIQUEIDENTIFIER NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_JobRequestInterviewRounds_Status DEFAULT N'Pending',
        CONSTRAINT FK_JobRequestInterviewRounds_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_JobRequestInterviewRounds_JobRequests FOREIGN KEY (JobRequestId) REFERENCES dbo.JobRequests (JobRequestId),
        CONSTRAINT FK_JobRequestInterviewRounds_TemplateRounds FOREIGN KEY (InterviewTemplateRoundId) REFERENCES dbo.InterviewTemplateRounds (InterviewTemplateRoundId),
        CONSTRAINT FK_JobRequestInterviewRounds_OwnerRole FOREIGN KEY (OwnerRoleId) REFERENCES dbo.Roles (RoleId),
        CONSTRAINT UQ_JobRequestInterviewRounds_Job_Order UNIQUE (JobRequestId, RoundOrder)
    );
END;
GO

IF OBJECT_ID(N'dbo.Interviews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Interviews
    (
        InterviewId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Interviews PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        JobApplicationId UNIQUEIDENTIFIER NOT NULL,
        JobRequestInterviewRoundId UNIQUEIDENTIFIER NULL,
        InterviewerUserId UNIQUEIDENTIFIER NOT NULL,
        ScheduledByUserId UNIQUEIDENTIFIER NOT NULL,
        StartsAtUtc DATETIME2(3) NOT NULL,
        DurationMinutes INT NOT NULL,
        MeetingLink NVARCHAR(600) NULL,
        LocationText NVARCHAR(300) NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_Interviews_Status DEFAULT N'Scheduled',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Interviews_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Interviews_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Interviews_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_Interviews_Applications FOREIGN KEY (JobApplicationId) REFERENCES dbo.JobApplications (JobApplicationId),
        CONSTRAINT FK_Interviews_Rounds FOREIGN KEY (JobRequestInterviewRoundId) REFERENCES dbo.JobRequestInterviewRounds (JobRequestInterviewRoundId),
        CONSTRAINT FK_Interviews_InterviewerUser FOREIGN KEY (InterviewerUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT FK_Interviews_ScheduledByUser FOREIGN KEY (ScheduledByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_Interviews_Status CHECK (Status IN (N'Scheduled', N'Completed', N'Cancelled', N'NoShow'))
    );
END;
GO

IF OBJECT_ID(N'dbo.InterviewFeedback', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InterviewFeedback
    (
        InterviewFeedbackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InterviewFeedback PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        InterviewId UNIQUEIDENTIFIER NOT NULL,
        SubmittedByUserId UNIQUEIDENTIFIER NOT NULL,
        TechnicalScore INT NULL,
        CommunicationScore INT NULL,
        CultureScore INT NULL,
        Recommendation NVARCHAR(40) NULL,
        FeedbackText NVARCHAR(MAX) NULL,
        IsSubmitted BIT NOT NULL CONSTRAINT DF_InterviewFeedback_IsSubmitted DEFAULT (0),
        SubmittedAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_InterviewFeedback_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_InterviewFeedback_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_InterviewFeedback_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_InterviewFeedback_Interviews FOREIGN KEY (InterviewId) REFERENCES dbo.Interviews (InterviewId),
        CONSTRAINT FK_InterviewFeedback_SubmittedByUser FOREIGN KEY (SubmittedByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_InterviewFeedback_TechnicalScore CHECK (TechnicalScore IS NULL OR TechnicalScore BETWEEN 1 AND 5),
        CONSTRAINT CK_InterviewFeedback_CommunicationScore CHECK (CommunicationScore IS NULL OR CommunicationScore BETWEEN 1 AND 5),
        CONSTRAINT CK_InterviewFeedback_CultureScore CHECK (CultureScore IS NULL OR CultureScore BETWEEN 1 AND 5)
    );
END;
GO

IF OBJECT_ID(N'dbo.WorkflowDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowDefinitions
    (
        WorkflowDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkflowDefinitions PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Code NVARCHAR(80) NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        EntityType NVARCHAR(80) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_WorkflowDefinitions_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_WorkflowDefinitions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_WorkflowDefinitions_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WorkflowDefinitions_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT CK_WorkflowDefinitions_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT UQ_WorkflowDefinitions_Tenant_Code UNIQUE (TenantId, Code)
    );
END;
GO

IF OBJECT_ID(N'dbo.WorkflowStages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowStages
    (
        WorkflowStageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkflowStages PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowDefinitionId UNIQUEIDENTIFIER NOT NULL,
        StageKey NVARCHAR(80) NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        StageOrder INT NOT NULL,
        IsTerminal BIT NOT NULL CONSTRAINT DF_WorkflowStages_IsTerminal DEFAULT (0),
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_WorkflowStages_Status DEFAULT N'Active',
        CONSTRAINT FK_WorkflowStages_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_WorkflowStages_Definitions FOREIGN KEY (WorkflowDefinitionId) REFERENCES dbo.WorkflowDefinitions (WorkflowDefinitionId),
        CONSTRAINT UQ_WorkflowStages_Definition_Key UNIQUE (WorkflowDefinitionId, StageKey),
        CONSTRAINT UQ_WorkflowStages_Definition_Order UNIQUE (WorkflowDefinitionId, StageOrder)
    );
END;
GO

IF OBJECT_ID(N'dbo.WorkflowTransitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowTransitions
    (
        WorkflowTransitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkflowTransitions PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowDefinitionId UNIQUEIDENTIFIER NOT NULL,
        ActionKey NVARCHAR(100) NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        FromStageId UNIQUEIDENTIFIER NOT NULL,
        ToStageId UNIQUEIDENTIFIER NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_WorkflowTransitions_Status DEFAULT N'Active',
        CONSTRAINT FK_WorkflowTransitions_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_WorkflowTransitions_Definitions FOREIGN KEY (WorkflowDefinitionId) REFERENCES dbo.WorkflowDefinitions (WorkflowDefinitionId),
        CONSTRAINT FK_WorkflowTransitions_FromStage FOREIGN KEY (FromStageId) REFERENCES dbo.WorkflowStages (WorkflowStageId),
        CONSTRAINT FK_WorkflowTransitions_ToStage FOREIGN KEY (ToStageId) REFERENCES dbo.WorkflowStages (WorkflowStageId),
        CONSTRAINT UQ_WorkflowTransitions_Definition_Action UNIQUE (WorkflowDefinitionId, ActionKey)
    );
END;
GO

IF OBJECT_ID(N'dbo.WorkflowRoutingRules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowRoutingRules
    (
        WorkflowRoutingRuleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkflowRoutingRules PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowTransitionId UNIQUEIDENTIFIER NOT NULL,
        AssignmentType NVARCHAR(40) NOT NULL,
        TargetUserId UNIQUEIDENTIFIER NULL,
        TargetGroupId UNIQUEIDENTIFIER NULL,
        TargetRoleId UNIQUEIDENTIFIER NULL,
        ResolverKey NVARCHAR(120) NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_WorkflowRoutingRules_Status DEFAULT N'Active',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_WorkflowRoutingRules_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_WorkflowRoutingRules_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WorkflowRoutingRules_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_WorkflowRoutingRules_Transitions FOREIGN KEY (WorkflowTransitionId) REFERENCES dbo.WorkflowTransitions (WorkflowTransitionId),
        CONSTRAINT FK_WorkflowRoutingRules_TargetUser FOREIGN KEY (TargetUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT FK_WorkflowRoutingRules_TargetGroup FOREIGN KEY (TargetGroupId) REFERENCES dbo.Groups (GroupId),
        CONSTRAINT FK_WorkflowRoutingRules_TargetRole FOREIGN KEY (TargetRoleId) REFERENCES dbo.Roles (RoleId),
        CONSTRAINT CK_WorkflowRoutingRules_AssignmentType CHECK (AssignmentType IN (N'User', N'Group', N'Role', N'DynamicResolver', N'NoAssignment')),
        CONSTRAINT CK_WorkflowRoutingRules_Status CHECK (Status IN (N'Active', N'Inactive')),
        CONSTRAINT UQ_WorkflowRoutingRules_Transition UNIQUE (WorkflowTransitionId)
    );
END;
GO

IF OBJECT_ID(N'dbo.WorkflowActionPermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowActionPermissions
    (
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowTransitionId UNIQUEIDENTIFIER NOT NULL,
        RoleId UNIQUEIDENTIFIER NOT NULL,
        MustBeCurrentAssignee BIT NOT NULL CONSTRAINT DF_WorkflowActionPermissions_MustBeCurrentAssignee DEFAULT (0),
        MustBeGroupMember BIT NOT NULL CONSTRAINT DF_WorkflowActionPermissions_MustBeGroupMember DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_WorkflowActionPermissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_WorkflowActionPermissions PRIMARY KEY (TenantId, WorkflowTransitionId, RoleId),
        CONSTRAINT FK_WorkflowActionPermissions_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_WorkflowActionPermissions_Transitions FOREIGN KEY (WorkflowTransitionId) REFERENCES dbo.WorkflowTransitions (WorkflowTransitionId),
        CONSTRAINT FK_WorkflowActionPermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId)
    );
END;
GO

IF OBJECT_ID(N'dbo.WorkflowAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowAssignments
    (
        WorkflowAssignmentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkflowAssignments PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowDefinitionId UNIQUEIDENTIFIER NOT NULL,
        WorkflowStageId UNIQUEIDENTIFIER NOT NULL,
        WorkflowTransitionId UNIQUEIDENTIFIER NULL,
        EntityType NVARCHAR(80) NOT NULL,
        EntityId UNIQUEIDENTIFIER NOT NULL,
        AssignedToUserId UNIQUEIDENTIFIER NULL,
        AssignedToGroupId UNIQUEIDENTIFIER NULL,
        AssignedToRoleId UNIQUEIDENTIFIER NULL,
        AssignmentStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_WorkflowAssignments_Status DEFAULT N'Pending',
        ClaimedByUserId UNIQUEIDENTIFIER NULL,
        AssignedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_WorkflowAssignments_AssignedAtUtc DEFAULT SYSUTCDATETIME(),
        ClaimedAtUtc DATETIME2(3) NULL,
        CompletedAtUtc DATETIME2(3) NULL,
        CONSTRAINT FK_WorkflowAssignments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_WorkflowAssignments_Definitions FOREIGN KEY (WorkflowDefinitionId) REFERENCES dbo.WorkflowDefinitions (WorkflowDefinitionId),
        CONSTRAINT FK_WorkflowAssignments_Stages FOREIGN KEY (WorkflowStageId) REFERENCES dbo.WorkflowStages (WorkflowStageId),
        CONSTRAINT FK_WorkflowAssignments_Transitions FOREIGN KEY (WorkflowTransitionId) REFERENCES dbo.WorkflowTransitions (WorkflowTransitionId),
        CONSTRAINT FK_WorkflowAssignments_AssignedUser FOREIGN KEY (AssignedToUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT FK_WorkflowAssignments_AssignedGroup FOREIGN KEY (AssignedToGroupId) REFERENCES dbo.Groups (GroupId),
        CONSTRAINT FK_WorkflowAssignments_AssignedRole FOREIGN KEY (AssignedToRoleId) REFERENCES dbo.Roles (RoleId),
        CONSTRAINT FK_WorkflowAssignments_ClaimedByUser FOREIGN KEY (ClaimedByUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT CK_WorkflowAssignments_Status CHECK (AssignmentStatus IN (N'Pending', N'Claimed', N'Completed', N'Cancelled'))
    );
END;
GO

IF OBJECT_ID(N'dbo.WorkflowHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowHistory
    (
        WorkflowHistoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkflowHistory PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowDefinitionId UNIQUEIDENTIFIER NOT NULL,
        EntityType NVARCHAR(80) NOT NULL,
        EntityId UNIQUEIDENTIFIER NOT NULL,
        WorkflowTransitionId UNIQUEIDENTIFIER NULL,
        FromStageId UNIQUEIDENTIFIER NULL,
        ToStageId UNIQUEIDENTIFIER NULL,
        WorkflowAssignmentId UNIQUEIDENTIFIER NULL,
        ActorUserId UNIQUEIDENTIFIER NULL,
        ActionKey NVARCHAR(100) NOT NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_WorkflowHistory_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WorkflowHistory_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_WorkflowHistory_Definitions FOREIGN KEY (WorkflowDefinitionId) REFERENCES dbo.WorkflowDefinitions (WorkflowDefinitionId),
        CONSTRAINT FK_WorkflowHistory_Transitions FOREIGN KEY (WorkflowTransitionId) REFERENCES dbo.WorkflowTransitions (WorkflowTransitionId),
        CONSTRAINT FK_WorkflowHistory_FromStage FOREIGN KEY (FromStageId) REFERENCES dbo.WorkflowStages (WorkflowStageId),
        CONSTRAINT FK_WorkflowHistory_ToStage FOREIGN KEY (ToStageId) REFERENCES dbo.WorkflowStages (WorkflowStageId),
        CONSTRAINT FK_WorkflowHistory_Assignments FOREIGN KEY (WorkflowAssignmentId) REFERENCES dbo.WorkflowAssignments (WorkflowAssignmentId),
        CONSTRAINT FK_WorkflowHistory_ActorUser FOREIGN KEY (ActorUserId) REFERENCES dbo.AppUsers (UserId)
    );
END;
GO

IF OBJECT_ID(N'dbo.NotificationRecipients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationRecipients
    (
        NotificationRecipientId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NotificationRecipients PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        NotificationEventId UNIQUEIDENTIFIER NOT NULL,
        RecipientUserId UNIQUEIDENTIFIER NOT NULL,
        ReadAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_NotificationRecipients_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_NotificationRecipients_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_NotificationRecipients_NotificationEvents FOREIGN KEY (NotificationEventId) REFERENCES dbo.NotificationEvents (NotificationEventId),
        CONSTRAINT FK_NotificationRecipients_RecipientUser FOREIGN KEY (RecipientUserId) REFERENCES dbo.AppUsers (UserId),
        CONSTRAINT UQ_NotificationRecipients_Event_User UNIQUE (NotificationEventId, RecipientUserId)
    );
END;
GO

IF OBJECT_ID(N'dbo.AiRecommendationLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiRecommendationLogs
    (
        AiRecommendationLogId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiRecommendationLogs PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AiAgentDefinitionId NVARCHAR(120) NULL,
        SourceEntityType NVARCHAR(80) NOT NULL,
        SourceEntityId UNIQUEIDENTIFIER NOT NULL,
        RecommendedEntityType NVARCHAR(80) NOT NULL,
        RecommendedEntityId UNIQUEIDENTIFIER NOT NULL,
        Score DECIMAL(8,4) NULL,
        Explanation NVARCHAR(MAX) NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AiRecommendationLogs_PayloadJson DEFAULT N'{}',
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AiRecommendationLogs_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AiRecommendationLogs_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (TenantId),
        CONSTRAINT FK_AiRecommendationLogs_AgentDefinitions FOREIGN KEY (AiAgentDefinitionId) REFERENCES dbo.AiAgentDefinitions (AiAgentDefinitionId),
        CONSTRAINT CK_AiRecommendationLogs_PayloadJson CHECK (ISJSON(PayloadJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Employees_Tenant_BenchStatus' AND object_id = OBJECT_ID(N'dbo.Employees'))
    CREATE INDEX IX_Employees_Tenant_BenchStatus ON dbo.Employees (TenantId, BenchStatus, AvailabilityStatus, Status);
GO

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_Employees_Tenant_AppUser' AND parent_object_id = OBJECT_ID(N'dbo.Employees'))
    ALTER TABLE dbo.Employees DROP CONSTRAINT UQ_Employees_Tenant_AppUser;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Employees_Tenant_AppUser' AND object_id = OBJECT_ID(N'dbo.Employees'))
    CREATE UNIQUE INDEX UX_Employees_Tenant_AppUser ON dbo.Employees (TenantId, AppUserId) WHERE AppUserId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_JobRequests_Tenant_Status' AND object_id = OBJECT_ID(N'dbo.JobRequests'))
    CREATE INDEX IX_JobRequests_Tenant_Status ON dbo.JobRequests (TenantId, Status, PublishStatus, CurrentStageKey);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_JobApplications_Tenant_Status' AND object_id = OBJECT_ID(N'dbo.JobApplications'))
    CREATE INDEX IX_JobApplications_Tenant_Status ON dbo.JobApplications (TenantId, CurrentStatus, IsActive, AppliedAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkflowAssignments_Tenant_Active' AND object_id = OBJECT_ID(N'dbo.WorkflowAssignments'))
    CREATE INDEX IX_WorkflowAssignments_Tenant_Active ON dbo.WorkflowAssignments (TenantId, AssignmentStatus, EntityType, EntityId) INCLUDE (AssignedToUserId, AssignedToGroupId, AssignedToRoleId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkflowHistory_Tenant_Entity' AND object_id = OBJECT_ID(N'dbo.WorkflowHistory'))
    CREATE INDEX IX_WorkflowHistory_Tenant_Entity ON dbo.WorkflowHistory (TenantId, EntityType, EntityId, CreatedAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NotificationRecipients_User_Unread' AND object_id = OBJECT_ID(N'dbo.NotificationRecipients'))
    CREATE INDEX IX_NotificationRecipients_User_Unread ON dbo.NotificationRecipients (TenantId, RecipientUserId, ReadAtUtc, CreatedAtUtc DESC);
GO
