;WITH SkillQuestionTemplates AS
(
    SELECT
        skill.TenantId,
        skill.SkillId,
        CAST(NULL AS UNIQUEIDENTIFIER) AS DepartmentId,
        skill.Category AS JobFamily,
        template.RoundType,
        template.Difficulty,
        template.QuestionText,
        template.ExpectedSignal,
        template.FollowUpsJson,
        template.EvaluationRubricJson,
        CAST(N'Talent Pilot generated interview question seed corpus v1' AS NVARCHAR(240)) AS SourceTitle,
        CAST(NULL AS NVARCHAR(500)) AS SourceUrl
    FROM dbo.Skills AS skill
    CROSS APPLY (VALUES
        (
            N'Technical',
            N'Basic',
            CONCAT(N'Explain the core concepts of ', skill.Name, N' that you rely on in a production ', skill.Category, N' role, and describe where you have applied them.'),
            CONCAT(N'Candidate can explain ', skill.Name, N' in practical terms, names relevant constraints, and ties the concept to real delivery evidence.'),
            N'["Which trade-off mattered most in that example?","How did you know the implementation was correct?"]',
            N'["Uses accurate terminology","Connects answer to a real project","Identifies constraints and trade-offs"]'
        ),
        (
            N'Technical',
            N'Intermediate',
            CONCAT(N'Walk through a recent ', skill.Name, N' implementation or workflow. What design choices did you make and what alternatives did you reject?'),
            CONCAT(N'Candidate demonstrates hands-on ', skill.Name, N' experience, decision quality, and awareness of alternative approaches.'),
            N'["What would you change if scale or timeline changed?","Which failure mode did you plan for?"]',
            N'["Describes concrete implementation details","Explains rejected alternatives","Shows ownership of outcomes"]'
        ),
        (
            N'Technical',
            N'Intermediate',
            CONCAT(N'Imagine a ', skill.Name, N' solution is failing intermittently in production. How would you diagnose, isolate, and communicate the issue?'),
            CONCAT(N'Candidate can troubleshoot ', skill.Name, N' systematically using evidence, observability, and clear stakeholder communication.'),
            N'["What signal would you check first?","How would you prevent the same issue from returning?"]',
            N'["Uses a structured diagnostic path","Prioritizes customer or business impact","Defines preventive action"]'
        ),
        (
            N'Technical',
            N'Advanced',
            CONCAT(N'Design a maintainable approach for using ', skill.Name, N' in a team environment. How would you balance quality, speed, security, and long-term ownership?'),
            CONCAT(N'Candidate can reason beyond syntax and shows mature ', skill.Name, N' design, governance, and collaboration judgment.'),
            N'["Which standards would you document for the team?","How would you onboard another engineer to this approach?"]',
            N'["Balances delivery and maintainability","Mentions security or quality controls","Shows team-level thinking"]'
        ),
        (
            N'Technical',
            N'Advanced',
            CONCAT(N'What are the most common mistakes people make with ', skill.Name, N', and how have you avoided or corrected them?'),
            CONCAT(N'Candidate has reflective depth in ', skill.Name, N' and can distinguish superficial familiarity from production-ready practice.'),
            N'["Which mistake have you personally made and fixed?","How would you detect that mistake during review or testing?"]',
            N'["Gives non-generic pitfalls","Explains detection or review methods","Shows learning from experience"]'
        )
    ) AS template (RoundType, Difficulty, QuestionText, ExpectedSignal, FollowUpsJson, EvaluationRubricJson)
    WHERE skill.Status = N'Active'
),
SkillSourceRows AS
(
    SELECT
        NEWID() AS InterviewQuestionBankItemId,
        TenantId,
        SkillId,
        DepartmentId,
        JobFamily,
        RoundType,
        Difficulty,
        QuestionText,
        ExpectedSignal,
        FollowUpsJson,
        EvaluationRubricJson,
        SourceTitle,
        SourceUrl,
        LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', CONCAT(CONVERT(NVARCHAR(36), TenantId), N'|', CONVERT(NVARCHAR(36), SkillId), N'|', RoundType, N'|', Difficulty, N'|', QuestionText)), 2)) AS ContentHashSha256
    FROM SkillQuestionTemplates
)
MERGE dbo.InterviewQuestionBankItems AS target
USING SkillSourceRows AS source
ON target.TenantId = source.TenantId
   AND target.ContentHashSha256 = source.ContentHashSha256
WHEN MATCHED THEN UPDATE SET
    SkillId = source.SkillId,
    DepartmentId = source.DepartmentId,
    JobFamily = source.JobFamily,
    RoundType = source.RoundType,
    Difficulty = source.Difficulty,
    QuestionText = source.QuestionText,
    ExpectedSignal = source.ExpectedSignal,
    FollowUpsJson = source.FollowUpsJson,
    EvaluationRubricJson = source.EvaluationRubricJson,
    SourceTitle = source.SourceTitle,
    SourceUrl = source.SourceUrl,
    Status = N'Active',
    UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (InterviewQuestionBankItemId, TenantId, SkillId, DepartmentId, JobFamily, RoundType, Difficulty, QuestionText, ExpectedSignal, FollowUpsJson, EvaluationRubricJson, SourceTitle, SourceUrl, ContentHashSha256, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (source.InterviewQuestionBankItemId, source.TenantId, source.SkillId, source.DepartmentId, source.JobFamily, source.RoundType, source.Difficulty, source.QuestionText, source.ExpectedSignal, source.FollowUpsJson, source.EvaluationRubricJson, source.SourceTitle, source.SourceUrl, source.ContentHashSha256, N'Active', SYSUTCDATETIME(), SYSUTCDATETIME());
GO

;WITH GenericQuestionTemplates AS
(
    SELECT
        tenant.TenantId,
        CAST(NULL AS UNIQUEIDENTIFIER) AS SkillId,
        CAST(NULL AS UNIQUEIDENTIFIER) AS DepartmentId,
        template.JobFamily,
        template.RoundType,
        template.Difficulty,
        template.QuestionText,
        template.ExpectedSignal,
        template.FollowUpsJson,
        template.EvaluationRubricJson,
        CAST(N'Talent Pilot generated interview question seed corpus v1' AS NVARCHAR(240)) AS SourceTitle,
        CAST(NULL AS NVARCHAR(500)) AS SourceUrl
    FROM dbo.Tenants AS tenant
    CROSS APPLY (VALUES
        (N'Generic', N'Screening', N'Basic', N'What motivated you to apply for this role, and which part of the job description best matches your recent work?', N'Candidate gives role-specific motivation and connects experience to current requirements.', N'["Which requirement feels strongest for you?","Which requirement needs the most ramp-up?"]', N'["Understands the role","Connects motivation to evidence","Names realistic ramp-up areas"]'),
        (N'Generic', N'Screening', N'Basic', N'Walk me through your current notice period, availability, and any constraints that could affect interview scheduling or joining.', N'Candidate provides clear logistics without ambiguity or avoidable surprises.', N'["What is the earliest realistic joining date?","Are there any planned leaves or scheduling constraints?"]', N'["Clear availability","Transparent constraints","Realistic joining timeline"]'),
        (N'Generic', N'Screening', N'Intermediate', N'Tell me about the most relevant project on your resume for this role. What was your personal contribution?', N'Candidate distinguishes personal ownership from team involvement and highlights relevant evidence.', N'["What did you personally build or decide?","How was success measured?"]', N'["Specific contribution","Relevant project evidence","Outcome awareness"]'),
        (N'Generic', N'HR', N'Basic', N'Describe a workplace environment where you do your best work. What helps you stay effective and accountable?', N'Candidate explains working preferences and accountability habits without requiring special handling.', N'["How do you communicate blockers?","What feedback style works best for you?"]', N'["Self-awareness","Accountability","Constructive communication"]'),
        (N'Generic', N'HR', N'Intermediate', N'Tell me about a time you received difficult feedback. How did you respond and what changed afterward?', N'Candidate shows openness to feedback, emotional maturity, and concrete behavior change.', N'["What did you disagree with at first?","How did you know you had improved?"]', N'["Owns the situation","Shows reflection","Names changed behavior"]'),
        (N'Generic', N'HR', N'Intermediate', N'Give an example of a conflict with a teammate, client, or stakeholder. How did you move it toward resolution?', N'Candidate can handle conflict professionally and protect delivery outcomes.', N'["What did you say directly to the other person?","What would you do differently now?"]', N'["Keeps facts separate from blame","Uses direct communication","Protects team outcome"]'),
        (N'Generic', N'Behavioral', N'Intermediate', N'Tell me about a time priorities changed late in delivery. How did you re-plan and communicate impact?', N'Candidate shows adaptability, expectation management, and delivery discipline.', N'["What trade-off did you propose?","Who needed to be informed?"]', N'["Adapts plan","Communicates impact","Makes trade-offs explicit"]'),
        (N'Generic', N'Behavioral', N'Advanced', N'Describe a decision you made with incomplete information. What evidence did you use, and how did you manage risk?', N'Candidate demonstrates judgment under uncertainty and thoughtful risk management.', N'["What assumption proved wrong?","How did you recover or adjust?"]', N'["Identifies assumptions","Uses available evidence","Defines risk controls"]'),
        (N'Generic', N'HOD', N'Advanced', N'From a department perspective, how would you raise the bar for this role beyond completing assigned tasks?', N'Candidate can connect role performance to department standards, mentorship, quality, and ownership.', N'["What standard would you introduce or improve?","How would you influence peers without authority?"]', N'["Thinks beyond individual tasks","Mentions standards or mentorship","Shows department-level ownership"]'),
        (N'Generic', N'HOD', N'Advanced', N'If hired, what risks would you want your manager to know about in your first 90 days, and how would you mitigate them?', N'Candidate shows honest self-assessment and a practical onboarding plan.', N'["What support would make the biggest difference?","What milestone would prove you are ramping well?"]', N'["Honest risk awareness","Practical mitigation","Clear first-90-day milestones"]'),
        (N'Generic', N'HOD', N'Advanced', N'How do you mentor or review work from less experienced teammates while still meeting delivery commitments?', N'Candidate can balance leadership, review quality, and delivery execution.', N'["How do you avoid becoming a bottleneck?","What review feedback do you prioritize?"]', N'["Develops others","Protects delivery flow","Prioritizes high-impact feedback"]')
    ) AS template (JobFamily, RoundType, Difficulty, QuestionText, ExpectedSignal, FollowUpsJson, EvaluationRubricJson)
),
GenericSourceRows AS
(
    SELECT
        NEWID() AS InterviewQuestionBankItemId,
        TenantId,
        SkillId,
        DepartmentId,
        JobFamily,
        RoundType,
        Difficulty,
        QuestionText,
        ExpectedSignal,
        FollowUpsJson,
        EvaluationRubricJson,
        SourceTitle,
        SourceUrl,
        LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', CONCAT(CONVERT(NVARCHAR(36), TenantId), N'|generic|', RoundType, N'|', Difficulty, N'|', QuestionText)), 2)) AS ContentHashSha256
    FROM GenericQuestionTemplates
)
MERGE dbo.InterviewQuestionBankItems AS target
USING GenericSourceRows AS source
ON target.TenantId = source.TenantId
   AND target.ContentHashSha256 = source.ContentHashSha256
WHEN MATCHED THEN UPDATE SET
    SkillId = source.SkillId,
    DepartmentId = source.DepartmentId,
    JobFamily = source.JobFamily,
    RoundType = source.RoundType,
    Difficulty = source.Difficulty,
    QuestionText = source.QuestionText,
    ExpectedSignal = source.ExpectedSignal,
    FollowUpsJson = source.FollowUpsJson,
    EvaluationRubricJson = source.EvaluationRubricJson,
    SourceTitle = source.SourceTitle,
    SourceUrl = source.SourceUrl,
    Status = N'Active',
    UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (InterviewQuestionBankItemId, TenantId, SkillId, DepartmentId, JobFamily, RoundType, Difficulty, QuestionText, ExpectedSignal, FollowUpsJson, EvaluationRubricJson, SourceTitle, SourceUrl, ContentHashSha256, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (source.InterviewQuestionBankItemId, source.TenantId, source.SkillId, source.DepartmentId, source.JobFamily, source.RoundType, source.Difficulty, source.QuestionText, source.ExpectedSignal, source.FollowUpsJson, source.EvaluationRubricJson, source.SourceTitle, source.SourceUrl, source.ContentHashSha256, N'Active', SYSUTCDATETIME(), SYSUTCDATETIME());
GO
