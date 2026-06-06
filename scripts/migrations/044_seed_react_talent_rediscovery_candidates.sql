-- 044_seed_react_talent_rediscovery_candidates.sql
-- Seeds a larger warm frontend candidate pool for the Talent Rediscovery tab.
-- These are prior candidates/applications, not bench employees.

SET NOCOUNT ON;

DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @RecruiterUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333304';
DECLARE @InterviewerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333305';
DECLARE @HiringManagerUserId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333306';
DECLARE @EngineeringDepartmentId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01';
DECLARE @LahoreLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02';
DECLARE @RemoteLocationId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb03';
DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();
DECLARE @ExpectedCandidateCount INT = 45;

IF EXISTS (SELECT 1 FROM dbo.Tenants WHERE TenantId = @TenantId)
   AND EXISTS (SELECT 1 FROM dbo.AppUsers WHERE UserId = @RecruiterUserId)
   AND EXISTS (SELECT 1 FROM dbo.AppUsers WHERE UserId = @InterviewerUserId)
   AND EXISTS (SELECT 1 FROM dbo.AppUsers WHERE UserId = @HiringManagerUserId)
   AND EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentId = @EngineeringDepartmentId)
   AND EXISTS (SELECT 1 FROM dbo.Locations WHERE LocationId = @LahoreLocationId)
BEGIN
    DECLARE @CandidateSource TABLE
    (
        CandidateNo INT NOT NULL PRIMARY KEY,
        DisplayName NVARCHAR(200) NOT NULL,
        Initials NVARCHAR(8) NOT NULL,
        Email NVARCHAR(320) NOT NULL,
        CurrentDesignation NVARCHAR(160) NOT NULL,
        CurrentCompany NVARCHAR(200) NOT NULL,
        ExperienceYears DECIMAL(4,1) NOT NULL,
        NoticePeriodDays INT NOT NULL,
        Track NVARCHAR(40) NOT NULL,
        SourceCode NVARCHAR(80) NOT NULL,
        ApplicationStatus NVARCHAR(50) NOT NULL,
        EvidenceFocus NVARCHAR(800) NOT NULL
    );

    INSERT INTO @CandidateSource (CandidateNo, DisplayName, Initials, Email, CurrentDesignation, CurrentCompany, ExperienceYears, NoticePeriodDays, Track, SourceCode, ApplicationStatus, EvidenceFocus)
    VALUES
        (1, N'Ayesha Khan', N'AK', N'tp.react.seed01@8pkk57.onmicrosoft.com', N'Senior React Developer', N'Product Studio', 7.5, 15, N'ReactPortal', N'Referral', N'OnHold', N'React portal delivery, Redux state management, REST API integration, CSS modules, and accessibility fixes for enterprise dashboards.'),
        (2, N'Hamza Rauf', N'HR', N'tp.react.seed02@8pkk57.onmicrosoft.com', N'Senior React Developer', N'Relia', 6.2, 30, N'ReactPortal', N'LinkedInManual', N'OfferDeclined', N'React and TypeScript customer portal delivery, reusable form components, Ant Design tables, and API error-state handling.'),
        (3, N'Iqra Saleem', N'IS', N'tp.react.seed03@8pkk57.onmicrosoft.com', N'Frontend Engineer', N'Northstar Digital', 5.8, 20, N'ReactPortal', N'JobPortal', N'Rejected', N'React feature squads, JavaScript, SCSS, responsive design, REST API integration, and production bug triage.'),
        (4, N'Usman Ali', N'UA', N'tp.react.seed04@8pkk57.onmicrosoft.com', N'Principal Frontend Engineer', N'Bazaar Tech', 8.1, 30, N'ReactPortal', N'LinkedInManual', N'OnHold', N'Frontend architecture, React design-system ownership, Redux Toolkit, web performance optimization, and stakeholder demos.'),
        (5, N'Noor Fatima', N'NF', N'tp.react.seed05@8pkk57.onmicrosoft.com', N'React Developer', N'FinEdge', 4.9, 15, N'ReactPortal', N'Other', N'Withdrawn', N'React component implementation, TypeScript forms, CSS layouts, API integration, and responsive mobile views.'),
        (6, N'Danish Javed', N'DJ', N'tp.react.seed06@8pkk57.onmicrosoft.com', N'Frontend Platform Engineer', N'CloudVista', 6.7, 30, N'Performance', N'LinkedInManual', N'Rejected', N'React rendering optimization, bundle analysis, Tailwind CSS migration, dashboard widgets, and REST integrations.'),
        (7, N'Mahnoor Sheikh', N'MS', N'tp.react.seed07@8pkk57.onmicrosoft.com', N'React UI Engineer', N'RetailSoft', 5.5, 20, N'UiPlatform', N'Referral', N'OfferDeclined', N'Ant Design workflows, React table-heavy screens, SCSS, CSS architecture, and accessible form validation.'),
        (8, N'Bilal Aslam', N'BA', N'tp.react.seed08@8pkk57.onmicrosoft.com', N'Senior Frontend Developer', N'HealthTech', 7.0, 30, N'UiPlatform', N'JobPortal', N'OnHold', N'React, TypeScript, responsive design, internal admin portals, design-system components, and API integration.'),
        (9, N'Emaan Tariq', N'ET', N'tp.react.seed09@8pkk57.onmicrosoft.com', N'React Performance Engineer', N'PayBridge', 6.1, 15, N'Performance', N'LinkedInManual', N'Rejected', N'Web performance optimization, React profiling, lazy loading, JavaScript bundle reduction, and production monitoring.'),
        (10, N'Zain Qureshi', N'ZQ', N'tp.react.seed10@8pkk57.onmicrosoft.com', N'Full Stack React Engineer', N'Careem Labs', 5.2, 30, N'ReactPortal', N'Referral', N'OnHold', N'React, REST API integration, TypeScript, CSS, HTML, and backend collaboration on customer workflows.'),
        (11, N'Hania Malik', N'HM', N'tp.react.seed11@8pkk57.onmicrosoft.com', N'Frontend Architect', N'Northstar Digital', 8.4, 30, N'UiPlatform', N'LinkedInManual', N'OfferDeclined', N'React architecture, component libraries, Redux governance, Tailwind CSS, web performance, and engineering mentoring.'),
        (12, N'Ali Raza', N'AR', N'tp.react.seed12@8pkk57.onmicrosoft.com', N'React Developer', N'AlphaApps', 4.6, 20, N'ReactPortal', N'Other', N'Withdrawn', N'React feature delivery, TypeScript, JavaScript, REST API integration, and CSS responsive pages.'),
        (13, N'Saira Ahmed', N'SA', N'tp.react.seed13@8pkk57.onmicrosoft.com', N'Senior React Engineer', N'Product Studio', 6.9, 15, N'Performance', N'Referral', N'OnHold', N'React performance tuning, memoization, Redux data flows, Ant Design dashboards, and production support.'),
        (14, N'Saad Iqbal', N'SI', N'tp.react.seed14@8pkk57.onmicrosoft.com', N'Frontend Engineer', N'Relia', 5.7, 30, N'UiPlatform', N'JobPortal', N'Rejected', N'React admin panels, CSS modules, SCSS, HTML semantics, responsive design, and API integration.'),
        (15, N'Rimsha Khan', N'RK', N'tp.react.seed15@8pkk57.onmicrosoft.com', N'React Developer', N'NextWare', 6.3, 20, N'ReactPortal', N'LinkedInManual', N'OfferDeclined', N'React, TypeScript, Redux Toolkit, REST API integration, Tailwind CSS, and release hardening.'),
        (16, N'Faraz Ahmed', N'FA', N'tp.react.seed16@8pkk57.onmicrosoft.com', N'UI Platform Engineer', N'Systems Ltd', 7.8, 30, N'UiPlatform', N'Referral', N'OnHold', N'Design-system primitives, React composition patterns, Ant Design customization, CSS architecture, and frontend reviews.'),
        (17, N'Momina Zahid', N'MZ', N'tp.react.seed17@8pkk57.onmicrosoft.com', N'Frontend Developer', N'MarketPro', 5.1, 15, N'ReactPortal', N'JobPortal', N'Rejected', N'React product pages, JavaScript, HTML, CSS, responsive layouts, and REST API integration.'),
        (18, N'Taha Siddiqui', N'TS', N'tp.react.seed18@8pkk57.onmicrosoft.com', N'React Engineer', N'FinEdge', 6.5, 30, N'Performance', N'LinkedInManual', N'OnHold', N'React profiling, web performance optimization, API caching, Redux selectors, and TypeScript migration.'),
        (19, N'Minahil Abbas', N'MA', N'tp.react.seed19@8pkk57.onmicrosoft.com', N'UI Engineer', N'Northstar Digital', 4.8, 20, N'UiPlatform', N'Other', N'Withdrawn', N'React UI delivery, Ant Design screens, CSS, SCSS, responsive behavior, and component QA.'),
        (20, N'Fahad Khan', N'FK', N'tp.react.seed20@8pkk57.onmicrosoft.com', N'Senior React Developer', N'Product Studio', 7.2, 15, N'ReactPortal', N'Referral', N'Rejected', N'React portals, TypeScript, Redux, REST API integration, accessibility improvements, and production fixes.'),
        (21, N'Areeba Noor', N'AN', N'tp.react.seed21@8pkk57.onmicrosoft.com', N'Frontend Developer', N'Techlogix', 6.0, 30, N'Performance', N'LinkedInManual', N'OfferDeclined', N'Web performance optimization, React component splitting, Tailwind CSS, JavaScript, and Lighthouse remediation.'),
        (22, N'Hassan Mir', N'HM', N'tp.react.seed22@8pkk57.onmicrosoft.com', N'React Developer', N'SoftBridge', 5.4, 15, N'UiPlatform', N'JobPortal', N'OnHold', N'React, TypeScript forms, Ant Design tables, REST API integration, and responsive dashboard layouts.'),
        (23, N'Laiba Siddiqui', N'LS', N'tp.react.seed23@8pkk57.onmicrosoft.com', N'Senior Frontend Engineer', N'CloudVista', 6.6, 30, N'ReactPortal', N'LinkedInManual', N'Rejected', N'React customer portals, Redux, TypeScript, CSS architecture, API integration, and release troubleshooting.'),
        (24, N'Omar Farooq', N'OF', N'tp.react.seed24@8pkk57.onmicrosoft.com', N'Frontend Engineer', N'RetailSoft', 4.7, 20, N'UiPlatform', N'Other', N'Withdrawn', N'React UI implementation, JavaScript, HTML, CSS, responsive design, and Ant Design maintenance.'),
        (25, N'Anam Iqbal', N'AI', N'tp.react.seed25@8pkk57.onmicrosoft.com', N'React Tech Lead', N'HealthTech', 8.0, 30, N'Performance', N'Referral', N'OnHold', N'React technical leadership, performance budgets, Redux architecture, REST integrations, and frontend mentoring.'),
        (26, N'Shehryar Khan', N'SK', N'tp.react.seed26@8pkk57.onmicrosoft.com', N'UI Performance Engineer', N'PayBridge', 6.4, 15, N'Performance', N'LinkedInManual', N'OfferDeclined', N'React performance optimization, profiling, JavaScript runtime tuning, SCSS cleanup, and dashboard latency fixes.'),
        (27, N'Kiran Shah', N'KS', N'tp.react.seed27@8pkk57.onmicrosoft.com', N'React Developer', N'Product Studio', 5.6, 20, N'ReactPortal', N'JobPortal', N'Rejected', N'React, TypeScript, CSS, responsive design, REST API integration, and component test support.'),
        (28, N'Rehan Butt', N'RB', N'tp.react.seed28@8pkk57.onmicrosoft.com', N'Senior UI Engineer', N'Relia', 7.1, 30, N'UiPlatform', N'Referral', N'OnHold', N'React UI platform work, Ant Design customization, CSS architecture, HTML semantics, and API workflow states.'),
        (29, N'Mehreen Ali', N'MA', N'tp.react.seed29@8pkk57.onmicrosoft.com', N'Frontend Engineer', N'Bazaar Tech', 6.8, 15, N'ReactPortal', N'LinkedInManual', N'OfferDeclined', N'React portals, Redux Toolkit, REST APIs, Tailwind CSS, TypeScript, and client-side validation.'),
        (30, N'Waleed Akhtar', N'WA', N'tp.react.seed30@8pkk57.onmicrosoft.com', N'React Developer', N'NextWare', 5.3, 20, N'Performance', N'JobPortal', N'Rejected', N'React, JavaScript, CSS modules, bundle-size fixes, responsive pages, and API integration.'),
        (31, N'Sumbal Riaz', N'SR', N'tp.react.seed31@8pkk57.onmicrosoft.com', N'Frontend Architect', N'Northstar Digital', 7.6, 30, N'UiPlatform', N'Referral', N'OnHold', N'React architecture, design-system strategy, Ant Design patterns, Tailwind CSS, and performance reviews.'),
        (32, N'Junaid Ahmed', N'JA', N'tp.react.seed32@8pkk57.onmicrosoft.com', N'React Engineer', N'AlphaApps', 4.9, 15, N'ReactPortal', N'Other', N'Withdrawn', N'React screens, TypeScript, HTML, CSS, REST API integration, and responsive fixes.'),
        (33, N'Aiman Gul', N'AG', N'tp.react.seed33@8pkk57.onmicrosoft.com', N'Senior React Developer', N'Careem Labs', 6.2, 30, N'Performance', N'LinkedInManual', N'OfferDeclined', N'React, TypeScript, Redux, web performance optimization, REST API integration, and observability-driven fixes.'),
        (34, N'Talha Nadeem', N'TN', N'tp.react.seed34@8pkk57.onmicrosoft.com', N'Frontend Developer', N'FinEdge', 5.8, 20, N'UiPlatform', N'JobPortal', N'Rejected', N'React and TypeScript admin screens, Ant Design, SCSS, responsive behavior, and API integration.'),
        (35, N'Hareem Asif', N'HA', N'tp.react.seed35@8pkk57.onmicrosoft.com', N'React Portal Engineer', N'Relia', 6.9, 15, N'ReactPortal', N'Referral', N'OnHold', N'React portal delivery, Redux, CSS, HTML, REST API integration, and production issue ownership.'),
        (36, N'Noman Shah', N'NS', N'tp.react.seed36@8pkk57.onmicrosoft.com', N'Frontend Platform Engineer', N'CloudVista', 7.3, 30, N'UiPlatform', N'LinkedInManual', N'OfferDeclined', N'React platform standards, design-system releases, Tailwind CSS, Ant Design, and performance baselines.'),
        (37, N'Dua Khalid', N'DK', N'tp.react.seed37@8pkk57.onmicrosoft.com', N'React Developer', N'MarketPro', 5.0, 15, N'ReactPortal', N'JobPortal', N'Rejected', N'React feature delivery, JavaScript, CSS, responsive views, API integration, and UI testing support.'),
        (38, N'Arslan Mehmood', N'AM', N'tp.react.seed38@8pkk57.onmicrosoft.com', N'UI Engineer', N'RetailSoft', 6.1, 20, N'Performance', N'Referral', N'OnHold', N'React UI performance, SCSS refactoring, responsive design, REST API integration, and dashboard polish.'),
        (39, N'Tooba Faisal', N'TF', N'tp.react.seed39@8pkk57.onmicrosoft.com', N'Senior React Engineer', N'HealthTech', 7.7, 30, N'ReactPortal', N'LinkedInManual', N'OfferDeclined', N'React, TypeScript, Redux, Ant Design, REST API integration, and healthcare portal workflows.'),
        (40, N'Salman Ali', N'SA', N'tp.react.seed40@8pkk57.onmicrosoft.com', N'Frontend Engineer', N'SoftBridge', 5.5, 20, N'UiPlatform', N'JobPortal', N'Rejected', N'React admin interfaces, JavaScript, HTML, CSS, responsive design, and API workflow implementation.'),
        (41, N'Misha Rauf', N'MR', N'tp.react.seed41@8pkk57.onmicrosoft.com', N'React Developer', N'PayBridge', 6.4, 15, N'Performance', N'Referral', N'OnHold', N'React performance fixes, Redux Toolkit, TypeScript, REST API integration, and transaction dashboard UI.'),
        (42, N'Huzaifa Tariq', N'HT', N'tp.react.seed42@8pkk57.onmicrosoft.com', N'Senior Frontend Architect', N'Product Studio', 8.2, 30, N'UiPlatform', N'LinkedInManual', N'OfferDeclined', N'React architecture, frontend governance, Ant Design systems, Tailwind CSS, and web performance coaching.'),
        (43, N'Zoya Naseem', N'ZN', N'tp.react.seed43@8pkk57.onmicrosoft.com', N'React Engineer', N'NextWare', 4.7, 15, N'ReactPortal', N'Other', N'Withdrawn', N'React features, TypeScript, JavaScript, CSS, REST API integration, and responsive page delivery.'),
        (44, N'Kashif Amin', N'KA', N'tp.react.seed44@8pkk57.onmicrosoft.com', N'Frontend Developer', N'Bazaar Tech', 6.6, 20, N'Performance', N'Referral', N'OnHold', N'React, Redux, performance optimization, REST API integration, Tailwind CSS, and production monitoring.'),
        (45, N'Nimra Shahid', N'NS', N'tp.react.seed45@8pkk57.onmicrosoft.com', N'Senior React Developer', N'Northstar Digital', 5.9, 30, N'UiPlatform', N'LinkedInManual', N'OfferDeclined', N'React, TypeScript, Ant Design, SCSS, CSS architecture, responsive design, and API states.');

    IF (SELECT COUNT(1) FROM @CandidateSource) <> @ExpectedCandidateCount
    BEGIN
        THROW 51044, 'React rediscovery seed must contain exactly 45 candidates.', 1;
    END;

    DECLARE @CandidateIds TABLE
    (
        CandidateNo INT NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CandidateId UNIQUEIDENTIFIER NOT NULL,
        JobApplicationId UNIQUEIDENTIFIER NOT NULL,
        CandidateEducationId UNIQUEIDENTIFIER NOT NULL,
        CandidateWorkHistoryId UNIQUEIDENTIFIER NOT NULL,
        ApplicationDocumentId UNIQUEIDENTIFIER NOT NULL,
        ScreeningInterviewId UNIQUEIDENTIFIER NOT NULL,
        TechnicalInterviewId UNIQUEIDENTIFIER NOT NULL,
        ScreeningFeedbackId UNIQUEIDENTIFIER NOT NULL,
        TechnicalFeedbackId UNIQUEIDENTIFIER NOT NULL
    );

    INSERT INTO @CandidateIds
    (
        CandidateNo,
        UserId,
        CandidateId,
        JobApplicationId,
        CandidateEducationId,
        CandidateWorkHistoryId,
        ApplicationDocumentId,
        ScreeningInterviewId,
        TechnicalInterviewId,
        ScreeningFeedbackId,
        TechnicalFeedbackId
    )
    SELECT
        CandidateNo,
        CONVERT(UNIQUEIDENTIFIER, CONCAT(N'24410000-0000-0000-0000-', RIGHT(CONCAT(N'000000000000', CandidateNo), 12))),
        CONVERT(UNIQUEIDENTIFIER, CONCAT(N'24400000-0000-0000-0000-', RIGHT(CONCAT(N'000000000000', CandidateNo), 12))),
        CONVERT(UNIQUEIDENTIFIER, CONCAT(N'24420000-0000-0000-0000-', RIGHT(CONCAT(N'000000000000', CandidateNo), 12))),
        CONVERT(UNIQUEIDENTIFIER, CONCAT(N'24430000-0000-0000-0000-', RIGHT(CONCAT(N'000000000000', CandidateNo), 12))),
        CONVERT(UNIQUEIDENTIFIER, CONCAT(N'24440000-0000-0000-0000-', RIGHT(CONCAT(N'000000000000', CandidateNo), 12))),
        CONVERT(UNIQUEIDENTIFIER, CONCAT(N'24450000-0000-0000-0000-', RIGHT(CONCAT(N'000000000000', CandidateNo), 12))),
        CONVERT(UNIQUEIDENTIFIER, CONCAT(N'24480000-0000-0000-0000-', RIGHT(CONCAT(N'000000000000', (CandidateNo * 2) - 1), 12))),
        CONVERT(UNIQUEIDENTIFIER, CONCAT(N'24480000-0000-0000-0000-', RIGHT(CONCAT(N'000000000000', CandidateNo * 2), 12))),
        CONVERT(UNIQUEIDENTIFIER, CONCAT(N'24490000-0000-0000-0000-', RIGHT(CONCAT(N'000000000000', (CandidateNo * 2) - 1), 12))),
        CONVERT(UNIQUEIDENTIFIER, CONCAT(N'24490000-0000-0000-0000-', RIGHT(CONCAT(N'000000000000', CandidateNo * 2), 12)))
    FROM @CandidateSource;

    DECLARE @HistoricalRequests TABLE
    (
        Track NVARCHAR(40) NOT NULL PRIMARY KEY,
        JobRequestId UNIQUEIDENTIFIER NOT NULL,
        RequestCode NVARCHAR(60) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        ClientName NVARCHAR(200) NOT NULL,
        LocationId UNIQUEIDENTIFIER NOT NULL,
        ExperienceMinYears DECIMAL(4,1) NOT NULL,
        ExperienceMaxYears DECIMAL(4,1) NOT NULL,
        CreatedDaysAgo INT NOT NULL
    );

    INSERT INTO @HistoricalRequests (Track, JobRequestId, RequestCode, Title, Description, ClientName, LocationId, ExperienceMinYears, ExperienceMaxYears, CreatedDaysAgo)
    VALUES
        (N'ReactPortal', '24460000-0000-0000-0000-000000000001', N'TP-HIST-REACT-201', N'React Customer Portal Engineer', N'Historical frontend role requiring React, TypeScript, JavaScript, CSS, responsive design, Redux, REST API integration, and customer portal delivery.', N'Relia', @LahoreLocationId, 4.0, 8.0, 210),
        (N'Performance', '24460000-0000-0000-0000-000000000002', N'TP-HIST-REACT-202', N'Frontend Performance Engineer', N'Historical frontend performance role requiring React profiling, web performance optimization, JavaScript bundle tuning, Tailwind CSS, API caching, and production monitoring.', N'PayBridge', @RemoteLocationId, 5.0, 9.0, 185),
        (N'UiPlatform', '24460000-0000-0000-0000-000000000003', N'TP-HIST-REACT-203', N'UI Platform Engineer', N'Historical UI platform role requiring React, TypeScript, Ant Design, SCSS, CSS architecture, HTML semantics, responsive design, and reusable component systems.', N'Northstar Digital', @LahoreLocationId, 4.0, 9.0, 165);

    MERGE dbo.CandidateSourceLabels AS target
    USING (VALUES
        ('24470000-0000-0000-0000-000000000101', @TenantId, N'LinkedInManual', N'LinkedIn', N'External sourcing', N'Active'),
        ('24470000-0000-0000-0000-000000000102', @TenantId, N'Referral', N'Referral', N'Referral reporting', N'Active'),
        ('24470000-0000-0000-0000-000000000103', @TenantId, N'JobPortal', N'Job Portal', N'Talent Pilot portal', N'Active'),
        ('24470000-0000-0000-0000-000000000104', @TenantId, N'Other', N'Other', N'Manual review', N'Active')
    ) AS source (CandidateSourceLabelId, TenantId, Code, DisplayName, ReportingCategory, Status)
        ON target.TenantId = source.TenantId AND target.Code = source.Code
    WHEN MATCHED THEN
        UPDATE SET DisplayName = source.DisplayName, ReportingCategory = source.ReportingCategory, Status = source.Status, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (CandidateSourceLabelId, TenantId, Code, DisplayName, ReportingCategory, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.CandidateSourceLabelId, source.TenantId, source.Code, source.DisplayName, source.ReportingCategory, source.Status, @Now, @Now);

    MERGE dbo.Skills AS target
    USING (VALUES
        ('cccccccc-cccc-cccc-cccc-cccccccccc05', @TenantId, N'React', N'react', N'Frontend', N'["React.js","React hooks","React components"]'),
        ('24470000-0000-0000-0000-000000000001', @TenantId, N'TypeScript', N'typescript', N'Frontend', N'["TS","typed React"]'),
        ('24470000-0000-0000-0000-000000000002', @TenantId, N'JavaScript', N'javascript', N'Frontend', N'["ES6","browser JavaScript"]'),
        ('24470000-0000-0000-0000-000000000003', @TenantId, N'CSS', N'css', N'Frontend', N'["CSS3","stylesheets"]'),
        ('24470000-0000-0000-0000-000000000004', @TenantId, N'HTML', N'html', N'Frontend', N'["HTML5","semantic HTML"]'),
        ('24470000-0000-0000-0000-000000000005', @TenantId, N'Redux', N'redux', N'Frontend', N'["Redux Toolkit","state management"]'),
        ('24470000-0000-0000-0000-000000000006', @TenantId, N'REST API Integration', N'rest api integration', N'Integration', N'["REST APIs","HTTP clients"]'),
        ('24470000-0000-0000-0000-000000000007', @TenantId, N'Web Performance Optimization', N'web performance optimization', N'Frontend', N'["Lighthouse","bundle optimization","React profiling"]'),
        ('24470000-0000-0000-0000-000000000008', @TenantId, N'Responsive Design', N'responsive design', N'Frontend', N'["mobile responsive","adaptive layouts"]'),
        ('24470000-0000-0000-0000-000000000009', @TenantId, N'Tailwind CSS', N'tailwind css', N'Frontend', N'["Tailwind"]'),
        ('24470000-0000-0000-0000-000000000010', @TenantId, N'Ant Design', N'ant design', N'Frontend', N'["AntD","Ant Design React"]'),
        ('24470000-0000-0000-0000-000000000011', @TenantId, N'SCSS', N'scss', N'Frontend', N'["Sass","SCSS modules"]'),
        ('24470000-0000-0000-0000-000000000012', @TenantId, N'Next.js', N'next.js', N'Frontend', N'["NextJS","React SSR"]')
    ) AS source (SkillId, TenantId, Name, NormalizedName, Category, AliasesJson)
        ON target.TenantId = source.TenantId AND target.NormalizedName = source.NormalizedName
    WHEN MATCHED THEN
        UPDATE SET Name = source.Name, Category = source.Category, AliasesJson = source.AliasesJson, IsVectorRelevant = CAST(1 AS BIT), Status = N'Active', UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (SkillId, TenantId, Name, NormalizedName, Category, AliasesJson, IsVectorRelevant, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.SkillId, source.TenantId, source.Name, source.NormalizedName, source.Category, source.AliasesJson, CAST(1 AS BIT), N'Active', @Now, @Now);

    DECLARE @SkillIds TABLE (Name NVARCHAR(160) NOT NULL PRIMARY KEY, SkillId UNIQUEIDENTIFIER NOT NULL);

    INSERT INTO @SkillIds (Name, SkillId)
    SELECT NormalizedName, SkillId
    FROM dbo.Skills
    WHERE TenantId = @TenantId
      AND NormalizedName IN
      (
          N'react', N'typescript', N'javascript', N'css', N'html', N'redux',
          N'rest api integration', N'web performance optimization', N'responsive design',
          N'tailwind css', N'ant design', N'scss', N'next.js'
      );

    MERGE dbo.JobRequests AS target
    USING @HistoricalRequests AS source
        ON target.JobRequestId = source.JobRequestId
    WHEN MATCHED THEN
        UPDATE SET
            RequestCode = source.RequestCode,
            Title = source.Title,
            Description = source.Description,
            ClientName = source.ClientName,
            DepartmentId = @EngineeringDepartmentId,
            LocationId = source.LocationId,
            EmploymentType = N'FullTime',
            ExperienceMinYears = source.ExperienceMinYears,
            ExperienceMaxYears = source.ExperienceMaxYears,
            Priority = N'Medium',
            RequiredPositions = 2,
            FulfilledPositions = 0,
            Status = N'Closed',
            PublishStatus = N'Unpublished',
            HiringManagerUserId = @HiringManagerUserId,
            CreatedByUserId = @RecruiterUserId,
            CurrentStageKey = N'CLOSED',
            CurrentAssignmentId = NULL,
            ClosedAtUtc = DATEADD(DAY, 35, DATEADD(DAY, -source.CreatedDaysAgo, @Now)),
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT
        (
            JobRequestId,
            TenantId,
            RequestCode,
            Title,
            Description,
            ClientName,
            DepartmentId,
            LocationId,
            EmploymentType,
            ExperienceMinYears,
            ExperienceMaxYears,
            Priority,
            RequiredPositions,
            FulfilledPositions,
            Status,
            PublishStatus,
            HiringManagerUserId,
            CreatedByUserId,
            CurrentStageKey,
            CurrentAssignmentId,
            PublishedAtUtc,
            ClosedAtUtc,
            CreatedAtUtc,
            UpdatedAtUtc
        )
        VALUES
        (
            source.JobRequestId,
            @TenantId,
            source.RequestCode,
            source.Title,
            source.Description,
            source.ClientName,
            @EngineeringDepartmentId,
            source.LocationId,
            N'FullTime',
            source.ExperienceMinYears,
            source.ExperienceMaxYears,
            N'Medium',
            2,
            0,
            N'Closed',
            N'Unpublished',
            @HiringManagerUserId,
            @RecruiterUserId,
            N'CLOSED',
            NULL,
            NULL,
            DATEADD(DAY, 35, DATEADD(DAY, -source.CreatedDaysAgo, @Now)),
            DATEADD(DAY, -source.CreatedDaysAgo, @Now),
            @Now
        );

    ;WITH RequestSkillSource AS
    (
        SELECT
            request.JobRequestId,
            skills.SkillId,
            skillMap.IsRequired,
            skillMap.Weight
        FROM @HistoricalRequests AS request
        CROSS APPLY (VALUES
            (N'react', CAST(1 AS BIT), 10),
            (N'typescript', CAST(1 AS BIT), 9),
            (N'javascript', CAST(1 AS BIT), 8),
            (N'css', CAST(1 AS BIT), 8),
            (N'rest api integration', CAST(1 AS BIT), 8),
            (N'responsive design', CAST(1 AS BIT), 8),
            (N'redux', CAST(0 AS BIT), 7),
            (N'web performance optimization', CAST(0 AS BIT), CASE WHEN request.Track = N'Performance' THEN 10 ELSE 7 END),
            (N'ant design', CAST(0 AS BIT), CASE WHEN request.Track = N'UiPlatform' THEN 9 ELSE 6 END),
            (N'tailwind css', CAST(0 AS BIT), CASE WHEN request.Track = N'Performance' THEN 8 ELSE 6 END),
            (N'scss', CAST(0 AS BIT), 6),
            (N'html', CAST(0 AS BIT), 6)
        ) AS skillMap(SkillName, IsRequired, Weight)
        INNER JOIN @SkillIds AS skills ON skills.Name = skillMap.SkillName
    )
    MERGE dbo.JobRequestSkills AS target
    USING RequestSkillSource AS source
        ON target.TenantId = @TenantId AND target.JobRequestId = source.JobRequestId AND target.SkillId = source.SkillId
    WHEN MATCHED THEN
        UPDATE SET IsRequired = source.IsRequired, Weight = source.Weight
    WHEN NOT MATCHED THEN
        INSERT (TenantId, JobRequestId, SkillId, IsRequired, Weight)
        VALUES (@TenantId, source.JobRequestId, source.SkillId, source.IsRequired, source.Weight);

    ;WITH UserRows AS
    (
        SELECT
            ids.UserId,
            source.DisplayName,
            source.Email,
            source.Initials
        FROM @CandidateSource AS source
        INNER JOIN @CandidateIds AS ids ON ids.CandidateNo = source.CandidateNo
    )
    MERGE dbo.AppUsers AS target
    USING UserRows AS source
        ON target.UserId = source.UserId
    WHEN MATCHED THEN
        UPDATE SET
            DisplayName = source.DisplayName,
            Email = source.Email,
            EmailNormalized = UPPER(source.Email),
            Initials = source.Initials,
            AccountStatus = N'Active',
            DeletedAtUtc = NULL,
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (UserId, TenantId, DisplayName, Email, EmailNormalized, Initials, AccountStatus, LastActiveAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.UserId, @TenantId, source.DisplayName, source.Email, UPPER(source.Email), source.Initials, N'Active', DATEADD(DAY, -2, @Now), @Now, @Now);

    ;WITH CandidateRows AS
    (
        SELECT
            ids.CandidateId,
            ids.UserId,
            source.CandidateNo,
            source.DisplayName,
            source.Email,
            source.CurrentDesignation,
            source.CurrentCompany,
            source.ExperienceYears,
            source.NoticePeriodDays
        FROM @CandidateSource AS source
        INNER JOIN @CandidateIds AS ids ON ids.CandidateNo = source.CandidateNo
    )
    MERGE dbo.Candidates AS target
    USING CandidateRows AS source
        ON target.CandidateId = source.CandidateId
    WHEN MATCHED THEN
        UPDATE SET
            AppUserId = source.UserId,
            DisplayName = source.DisplayName,
            Email = source.Email,
            Phone = CONCAT(N'+92-300-244-', RIGHT(CONCAT(N'000', source.CandidateNo), 3)),
            LinkedInUrl = CONCAT(N'https://linkedin.com/in/tp-react-rediscovery-', RIGHT(CONCAT(N'00', source.CandidateNo), 2)),
            CurrentDesignation = source.CurrentDesignation,
            CurrentCompany = source.CurrentCompany,
            ExperienceYears = source.ExperienceYears,
            ExpectedSalaryAmount = 320000 + (source.CandidateNo * 6000),
            ExpectedSalaryCurrency = 'PKR',
            NoticePeriodDays = source.NoticePeriodDays,
            Status = N'Active',
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT
        (
            CandidateId,
            TenantId,
            AppUserId,
            DisplayName,
            Email,
            Phone,
            LinkedInUrl,
            CurrentDesignation,
            CurrentCompany,
            ExperienceYears,
            ExpectedSalaryAmount,
            ExpectedSalaryCurrency,
            NoticePeriodDays,
            Status,
            CreatedAtUtc,
            UpdatedAtUtc
        )
        VALUES
        (
            source.CandidateId,
            @TenantId,
            source.UserId,
            source.DisplayName,
            source.Email,
            CONCAT(N'+92-300-244-', RIGHT(CONCAT(N'000', source.CandidateNo), 3)),
            CONCAT(N'https://linkedin.com/in/tp-react-rediscovery-', RIGHT(CONCAT(N'00', source.CandidateNo), 2)),
            source.CurrentDesignation,
            source.CurrentCompany,
            source.ExperienceYears,
            320000 + (source.CandidateNo * 6000),
            'PKR',
            source.NoticePeriodDays,
            N'Active',
            @Now,
            @Now
        );

    ;WITH EducationRows AS
    (
        SELECT
            ids.CandidateEducationId,
            ids.CandidateId,
            source.CandidateNo,
            UniversityName = CASE source.CandidateNo % 5
                WHEN 0 THEN N'FAST NUCES'
                WHEN 1 THEN N'UET Lahore'
                WHEN 2 THEN N'COMSATS'
                WHEN 3 THEN N'Punjab University'
                ELSE N'LUMS'
            END,
            DegreeName = CASE source.CandidateNo % 3
                WHEN 0 THEN N'BS Software Engineering'
                WHEN 1 THEN N'BS Computer Science'
                ELSE N'MS Computer Science'
            END,
            GraduationYear = 2024 - CONVERT(INT, FLOOR(source.ExperienceYears))
        FROM @CandidateSource AS source
        INNER JOIN @CandidateIds AS ids ON ids.CandidateNo = source.CandidateNo
    )
    MERGE dbo.CandidateEducation AS target
    USING EducationRows AS source
        ON target.CandidateEducationId = source.CandidateEducationId
    WHEN MATCHED THEN
        UPDATE SET UniversityName = source.UniversityName, DegreeName = source.DegreeName, GraduationYear = source.GraduationYear, IsPrimary = CAST(1 AS BIT), UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (CandidateEducationId, TenantId, CandidateId, UniversityName, DegreeName, GraduationYear, IsPrimary, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.CandidateEducationId, @TenantId, source.CandidateId, source.UniversityName, source.DegreeName, source.GraduationYear, CAST(1 AS BIT), @Now, @Now);

    ;WITH WorkRows AS
    (
        SELECT
            ids.CandidateWorkHistoryId,
            ids.CandidateId,
            source.CurrentCompany,
            source.CurrentDesignation,
            StartsOn = DATEADD(MONTH, -CONVERT(INT, FLOOR(source.ExperienceYears * 12)), CONVERT(date, @Now))
        FROM @CandidateSource AS source
        INNER JOIN @CandidateIds AS ids ON ids.CandidateNo = source.CandidateNo
    )
    MERGE dbo.CandidateWorkHistory AS target
    USING WorkRows AS source
        ON target.CandidateWorkHistoryId = source.CandidateWorkHistoryId
    WHEN MATCHED THEN
        UPDATE SET CompanyName = source.CurrentCompany, Title = source.CurrentDesignation, IsCurrent = CAST(1 AS BIT), StartsOn = source.StartsOn, EndsOn = NULL, UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (CandidateWorkHistoryId, TenantId, CandidateId, CompanyName, Title, IsCurrent, StartsOn, EndsOn, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.CandidateWorkHistoryId, @TenantId, source.CandidateId, source.CurrentCompany, source.CurrentDesignation, CAST(1 AS BIT), source.StartsOn, NULL, @Now, @Now);

    DECLARE @CandidateSkillSource TABLE
    (
        CandidateNo INT NOT NULL,
        SkillName NVARCHAR(160) NOT NULL,
        SkillLevel NVARCHAR(40) NOT NULL,
        YearsExperience DECIMAL(4,1) NOT NULL,
        IsPrimary BIT NOT NULL
    );

    INSERT INTO @CandidateSkillSource (CandidateNo, SkillName, SkillLevel, YearsExperience, IsPrimary)
    SELECT CandidateNo, N'react', N'Advanced', ExperienceYears, CAST(1 AS BIT) FROM @CandidateSource
    UNION ALL SELECT CandidateNo, N'typescript', CASE WHEN ExperienceYears >= 6 THEN N'Advanced' ELSE N'Intermediate' END, CAST(ExperienceYears - 0.5 AS DECIMAL(4,1)), CAST(1 AS BIT) FROM @CandidateSource
    UNION ALL SELECT CandidateNo, N'javascript', N'Advanced', CAST(ExperienceYears AS DECIMAL(4,1)), CAST(0 AS BIT) FROM @CandidateSource
    UNION ALL SELECT CandidateNo, N'css', N'Advanced', CAST(ExperienceYears - 0.3 AS DECIMAL(4,1)), CAST(0 AS BIT) FROM @CandidateSource
    UNION ALL SELECT CandidateNo, N'html', N'Advanced', CAST(ExperienceYears - 0.4 AS DECIMAL(4,1)), CAST(0 AS BIT) FROM @CandidateSource
    UNION ALL SELECT CandidateNo, N'rest api integration', N'Advanced', CAST(ExperienceYears - 0.8 AS DECIMAL(4,1)), CAST(0 AS BIT) FROM @CandidateSource
    UNION ALL SELECT CandidateNo, N'responsive design', N'Advanced', CAST(ExperienceYears - 0.6 AS DECIMAL(4,1)), CAST(0 AS BIT) FROM @CandidateSource
    UNION ALL SELECT CandidateNo, N'redux', CASE WHEN CandidateNo % 4 = 0 THEN N'Advanced' ELSE N'Intermediate' END, CAST(ExperienceYears - 1.0 AS DECIMAL(4,1)), CAST(0 AS BIT) FROM @CandidateSource WHERE CandidateNo % 2 <> 0 OR Track = N'ReactPortal'
    UNION ALL SELECT CandidateNo, N'web performance optimization', CASE WHEN Track = N'Performance' THEN N'Advanced' ELSE N'Intermediate' END, CAST(ExperienceYears - 1.0 AS DECIMAL(4,1)), CAST(0 AS BIT) FROM @CandidateSource WHERE Track = N'Performance' OR CandidateNo % 3 <> 0
    UNION ALL SELECT CandidateNo, N'tailwind css', N'Intermediate', CAST(ExperienceYears - 1.5 AS DECIMAL(4,1)), CAST(0 AS BIT) FROM @CandidateSource WHERE Track = N'Performance' OR CandidateNo % 4 IN (0, 1)
    UNION ALL SELECT CandidateNo, N'ant design', CASE WHEN Track = N'UiPlatform' THEN N'Advanced' ELSE N'Intermediate' END, CAST(ExperienceYears - 1.0 AS DECIMAL(4,1)), CAST(0 AS BIT) FROM @CandidateSource WHERE Track = N'UiPlatform' OR CandidateNo % 5 IN (0, 2)
    UNION ALL SELECT CandidateNo, N'scss', N'Intermediate', CAST(ExperienceYears - 1.2 AS DECIMAL(4,1)), CAST(0 AS BIT) FROM @CandidateSource WHERE Track = N'UiPlatform' OR CandidateNo % 3 = 1
    UNION ALL SELECT CandidateNo, N'next.js', N'Intermediate', CAST(ExperienceYears - 2.0 AS DECIMAL(4,1)), CAST(0 AS BIT) FROM @CandidateSource WHERE CandidateNo % 6 IN (0, 1);

    ;WITH SkillRows AS
    (
        SELECT
            ids.CandidateId,
            skills.SkillId,
            source.SkillLevel,
            source.YearsExperience,
            source.IsPrimary
        FROM @CandidateSkillSource AS source
        INNER JOIN @CandidateIds AS ids ON ids.CandidateNo = source.CandidateNo
        INNER JOIN @SkillIds AS skills ON skills.Name = source.SkillName
    )
    MERGE dbo.CandidateSkills AS target
    USING SkillRows AS source
        ON target.TenantId = @TenantId AND target.CandidateId = source.CandidateId AND target.SkillId = source.SkillId
    WHEN MATCHED THEN
        UPDATE SET SkillLevel = source.SkillLevel, YearsExperience = source.YearsExperience, IsPrimary = source.IsPrimary
    WHEN NOT MATCHED THEN
        INSERT (TenantId, CandidateId, SkillId, SkillLevel, YearsExperience, IsPrimary, CreatedAtUtc)
        VALUES (@TenantId, source.CandidateId, source.SkillId, source.SkillLevel, source.YearsExperience, source.IsPrimary, @Now);

    ;WITH ApplicationRows AS
    (
        SELECT
            ids.JobApplicationId,
            ids.CandidateId,
            ids.UserId,
            request.JobRequestId,
            label.CandidateSourceLabelId,
            source.CandidateNo,
            source.DisplayName,
            source.CurrentDesignation,
            source.CurrentCompany,
            source.ExperienceYears,
            source.Track,
            source.SourceCode,
            SourceLabel = label.DisplayName,
            source.ApplicationStatus,
            source.EvidenceFocus,
            AppliedAtUtc = DATEADD(DAY, -(60 + (source.CandidateNo * 3)), @Now),
            FinalDecisionAtUtc = DATEADD(DAY, -(45 + (source.CandidateNo * 3)), @Now),
            FinalDecisionReason = CASE source.ApplicationStatus
                WHEN N'OnHold' THEN N'Cleared React/frontend technical review; client paused the opening and asked to keep the profile warm.'
                WHEN N'OfferDeclined' THEN N'Cleared final review but declined due to timing or compensation; recruiter marked as a strong future React fit.'
                WHEN N'Rejected' THEN N'Not selected for that historical opening, but interview evidence confirmed relevant React/frontend delivery.'
                WHEN N'Withdrawn' THEN N'Candidate withdrew because of availability, but the profile remains relevant for React/frontend requirements.'
                ELSE N'Historical frontend application with usable rediscovery evidence.'
            END
        FROM @CandidateSource AS source
        INNER JOIN @CandidateIds AS ids ON ids.CandidateNo = source.CandidateNo
        INNER JOIN @HistoricalRequests AS request ON request.Track = source.Track
        INNER JOIN dbo.CandidateSourceLabels AS label
            ON label.TenantId = @TenantId
            AND label.Code = source.SourceCode
    )
    MERGE dbo.JobApplications AS target
    USING ApplicationRows AS source
        ON target.JobApplicationId = source.JobApplicationId
    WHEN MATCHED THEN
        UPDATE SET
            JobRequestId = source.JobRequestId,
            JobPostId = NULL,
            CandidateId = source.CandidateId,
            CandidateSourceLabelId = source.CandidateSourceLabelId,
            SourceLabel = source.SourceLabel,
            CurrentStatus = source.ApplicationStatus,
            IsActive = CAST(0 AS BIT),
            IsInvited = CAST(0 AS BIT),
            ConfirmedAtUtc = source.AppliedAtUtc,
            AppliedAtUtc = source.AppliedAtUtc,
            FinalDecisionAtUtc = source.FinalDecisionAtUtc,
            FinalDecisionReason = source.FinalDecisionReason,
            SourceDetail = CONCAT(N'ReactRediscoverySeed:', source.Track),
            SourceUrl = CONCAT(N'https://linkedin.com/in/tp-react-rediscovery-', RIGHT(CONCAT(N'00', source.CandidateNo), 2)),
            AddedByUserId = @RecruiterUserId,
            RecruiterNotes = CONCAT(N'ReactRediscoverySeed warm candidate. ', source.EvidenceFocus),
            CoverLetterText = CONCAT(N'I previously interviewed for ', source.CurrentDesignation, N' work and can support React, TypeScript, JavaScript, CSS, responsive design, Redux, REST API integration, and production portal delivery. ', source.EvidenceFocus),
            ApplicationSnapshotJson = CONCAT(N'{"seed":true,"seedTag":"ReactRediscoverySeed","candidateNo":', source.CandidateNo, N',"track":"', source.Track, N'"}'),
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT
        (
            JobApplicationId,
            TenantId,
            JobRequestId,
            JobPostId,
            CandidateId,
            CandidateSourceLabelId,
            SourceLabel,
            CurrentStatus,
            ApplicationVersion,
            IsActive,
            IsInvited,
            ConfirmedAtUtc,
            AppliedAtUtc,
            FinalDecisionAtUtc,
            FinalDecisionReason,
            SourceDetail,
            SourceUrl,
            AddedByUserId,
            RecruiterNotes,
            CoverLetterText,
            ApplicationSnapshotJson,
            CreatedAtUtc,
            UpdatedAtUtc
        )
        VALUES
        (
            source.JobApplicationId,
            @TenantId,
            source.JobRequestId,
            NULL,
            source.CandidateId,
            source.CandidateSourceLabelId,
            source.SourceLabel,
            source.ApplicationStatus,
            1,
            CAST(0 AS BIT),
            CAST(0 AS BIT),
            source.AppliedAtUtc,
            source.AppliedAtUtc,
            source.FinalDecisionAtUtc,
            source.FinalDecisionReason,
            CONCAT(N'ReactRediscoverySeed:', source.Track),
            CONCAT(N'https://linkedin.com/in/tp-react-rediscovery-', RIGHT(CONCAT(N'00', source.CandidateNo), 2)),
            @RecruiterUserId,
            CONCAT(N'ReactRediscoverySeed warm candidate. ', source.EvidenceFocus),
            CONCAT(N'I previously interviewed for ', source.CurrentDesignation, N' work and can support React, TypeScript, JavaScript, CSS, responsive design, Redux, REST API integration, and production portal delivery. ', source.EvidenceFocus),
            CONCAT(N'{"seed":true,"seedTag":"ReactRediscoverySeed","candidateNo":', source.CandidateNo, N',"track":"', source.Track, N'"}'),
            source.AppliedAtUtc,
            @Now
        );

    ;WITH DocumentRows AS
    (
        SELECT
            ids.ApplicationDocumentId,
            ids.JobApplicationId,
            ids.CandidateId,
            ids.UserId,
            source.CandidateNo,
            source.DisplayName,
            source.CurrentDesignation,
            source.CurrentCompany,
            source.ExperienceYears,
            source.Track,
            source.EvidenceFocus,
            ExtractedText = CONCAT(
                source.DisplayName,
                N' - ',
                source.CurrentDesignation,
                N'. ',
                source.ExperienceYears,
                N' years of frontend engineering experience at ',
                source.CurrentCompany,
                N'. Core CV evidence: React, TypeScript, JavaScript, CSS, HTML, responsive design, REST API integration, Redux, production troubleshooting, and recruiter-facing communication. Track: ',
                source.Track,
                N'. Detailed evidence: ',
                source.EvidenceFocus,
                N' Candidate has prior interview feedback and historical application evidence suitable for Talent Rediscovery. This is seeded extracted CV text modeled after local dummy frontend CVs.'
            )
        FROM @CandidateSource AS source
        INNER JOIN @CandidateIds AS ids ON ids.CandidateNo = source.CandidateNo
    )
    MERGE dbo.JobApplicationDocuments AS target
    USING DocumentRows AS source
        ON target.ApplicationDocumentId = source.ApplicationDocumentId
    WHEN MATCHED THEN
        UPDATE SET
            JobApplicationId = source.JobApplicationId,
            CandidateId = source.CandidateId,
            DocumentType = N'CV',
            OriginalFileName = CONCAT(REPLACE(source.DisplayName, N' ', N'_'), N'_React_CV.docx'),
            ContentType = N'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
            SizeBytes = 38600 + source.CandidateNo,
            StorageProvider = N'LocalFileSystem',
            StorageKey = CONCAT(N'applications/seeded-react-rediscovery/', RIGHT(CONCAT(N'00', source.CandidateNo), 2), N'-react-cv.docx'),
            StorageContainer = N'seeded-candidate-cvs',
            ContentHashSha256 = LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), source.ExtractedText)), 2)),
            ExtractionStatus = N'Extracted',
            ExtractedText = source.ExtractedText,
            ExtractedTextHashSha256 = LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), source.ExtractedText)), 2)),
            ParserVersion = N'seeded-docx-text-v1',
            ExtractedAtUtc = @Now,
            ExtractionError = NULL,
            Status = N'Active',
            UploadedByUserId = source.UserId,
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT
        (
            ApplicationDocumentId,
            TenantId,
            JobApplicationId,
            CandidateId,
            DocumentType,
            OriginalFileName,
            ContentType,
            SizeBytes,
            StorageProvider,
            StorageKey,
            StorageContainer,
            ContentHashSha256,
            ExtractionStatus,
            ExtractedText,
            ExtractedTextHashSha256,
            ParserVersion,
            ExtractedAtUtc,
            ExtractionError,
            Status,
            UploadedByUserId,
            UploadedAtUtc,
            CreatedAtUtc,
            UpdatedAtUtc
        )
        VALUES
        (
            source.ApplicationDocumentId,
            @TenantId,
            source.JobApplicationId,
            source.CandidateId,
            N'CV',
            CONCAT(REPLACE(source.DisplayName, N' ', N'_'), N'_React_CV.docx'),
            N'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
            38600 + source.CandidateNo,
            N'LocalFileSystem',
            CONCAT(N'applications/seeded-react-rediscovery/', RIGHT(CONCAT(N'00', source.CandidateNo), 2), N'-react-cv.docx'),
            N'seeded-candidate-cvs',
            LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), source.ExtractedText)), 2)),
            N'Extracted',
            source.ExtractedText,
            LOWER(CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONVERT(VARBINARY(MAX), source.ExtractedText)), 2)),
            N'seeded-docx-text-v1',
            @Now,
            NULL,
            N'Active',
            source.UserId,
            @Now,
            @Now,
            @Now
        );

    ;WITH InterviewRows AS
    (
        SELECT
            InterviewId = ids.ScreeningInterviewId,
            ids.JobApplicationId,
            InterviewerUserId = @RecruiterUserId,
            StartsAtUtc = DATEADD(DAY, -(58 + (source.CandidateNo * 3)), @Now),
            DurationMinutes = 30,
            RoundName = N'Recruiter screen'
        FROM @CandidateSource AS source
        INNER JOIN @CandidateIds AS ids ON ids.CandidateNo = source.CandidateNo
        UNION ALL
        SELECT
            InterviewId = ids.TechnicalInterviewId,
            ids.JobApplicationId,
            InterviewerUserId = @InterviewerUserId,
            StartsAtUtc = DATEADD(DAY, -(55 + (source.CandidateNo * 3)), @Now),
            DurationMinutes = 60,
            RoundName = N'Technical interview'
        FROM @CandidateSource AS source
        INNER JOIN @CandidateIds AS ids ON ids.CandidateNo = source.CandidateNo
    )
    MERGE dbo.Interviews AS target
    USING InterviewRows AS source
        ON target.InterviewId = source.InterviewId
    WHEN MATCHED THEN
        UPDATE SET
            JobApplicationId = source.JobApplicationId,
            JobRequestInterviewRoundId = NULL,
            JobPostInterviewRoundId = NULL,
            InterviewerUserId = source.InterviewerUserId,
            ScheduledByUserId = @RecruiterUserId,
            StartsAtUtc = source.StartsAtUtc,
            DurationMinutes = source.DurationMinutes,
            MeetingLink = N'https://meet.example.test/talent-pilot-react-rediscovery',
            LocationText = source.RoundName,
            Status = N'Completed',
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (InterviewId, TenantId, JobApplicationId, JobRequestInterviewRoundId, JobPostInterviewRoundId, InterviewerUserId, ScheduledByUserId, StartsAtUtc, DurationMinutes, MeetingLink, LocationText, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.InterviewId, @TenantId, source.JobApplicationId, NULL, NULL, source.InterviewerUserId, @RecruiterUserId, source.StartsAtUtc, source.DurationMinutes, N'https://meet.example.test/talent-pilot-react-rediscovery', source.RoundName, N'Completed', @Now, @Now);

    ;WITH FeedbackRows AS
    (
        SELECT
            InterviewFeedbackId = ids.ScreeningFeedbackId,
            InterviewId = ids.ScreeningInterviewId,
            SubmittedByUserId = @RecruiterUserId,
            TechnicalScore = CASE WHEN source.ApplicationStatus = N'Withdrawn' THEN 3 ELSE 4 END,
            CommunicationScore = CASE WHEN source.CandidateNo % 5 = 0 THEN 5 ELSE 4 END,
            CultureScore = 4,
            Recommendation = CASE WHEN source.ApplicationStatus = N'Withdrawn' THEN N'Hold' ELSE N'Proceed' END,
            FeedbackText = CONCAT(N'Recruiter screen confirmed React/frontend background, communication clarity, availability context, and warm rediscovery relevance. ', source.EvidenceFocus)
        FROM @CandidateSource AS source
        INNER JOIN @CandidateIds AS ids ON ids.CandidateNo = source.CandidateNo
        UNION ALL
        SELECT
            InterviewFeedbackId = ids.TechnicalFeedbackId,
            InterviewId = ids.TechnicalInterviewId,
            SubmittedByUserId = @InterviewerUserId,
            TechnicalScore = CASE WHEN source.Track = N'Performance' OR source.ExperienceYears >= 6 THEN 5 ELSE 4 END,
            CommunicationScore = 4,
            CultureScore = CASE WHEN source.ApplicationStatus = N'Rejected' THEN 3 ELSE 4 END,
            Recommendation = CASE WHEN source.ApplicationStatus = N'Withdrawn' THEN N'Hold' ELSE N'Proceed' END,
            FeedbackText = CONCAT(N'Technical review found usable evidence for React, TypeScript, JavaScript, CSS, responsive design, REST API integration, and production frontend delivery. ', source.EvidenceFocus)
        FROM @CandidateSource AS source
        INNER JOIN @CandidateIds AS ids ON ids.CandidateNo = source.CandidateNo
    )
    MERGE dbo.InterviewFeedback AS target
    USING FeedbackRows AS source
        ON target.InterviewFeedbackId = source.InterviewFeedbackId
    WHEN MATCHED THEN
        UPDATE SET
            SubmittedByUserId = source.SubmittedByUserId,
            TechnicalScore = source.TechnicalScore,
            CommunicationScore = source.CommunicationScore,
            CultureScore = source.CultureScore,
            Recommendation = source.Recommendation,
            FeedbackText = source.FeedbackText,
            IsSubmitted = CAST(1 AS BIT),
            SubmittedAtUtc = @Now,
            UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (InterviewFeedbackId, TenantId, InterviewId, SubmittedByUserId, TechnicalScore, CommunicationScore, CultureScore, Recommendation, FeedbackText, IsSubmitted, SubmittedAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.InterviewFeedbackId, @TenantId, source.InterviewId, source.SubmittedByUserId, source.TechnicalScore, source.CommunicationScore, source.CultureScore, source.Recommendation, source.FeedbackText, CAST(1 AS BIT), @Now, @Now, @Now);
END;
GO
