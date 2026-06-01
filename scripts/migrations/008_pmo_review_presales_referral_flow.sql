-- Migration 008: add PMO manual employee recommendation and Presales review workflow support.
-- Data impact: keeps existing Job Requests intact, extends allowed statuses, adds the
-- backend-owned PRESALES_REVIEW stage/transitions, and registers PMO referral decision
-- notification events/templates for existing tenants.

IF EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_JobRequests_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.JobRequests')
)
BEGIN
    ALTER TABLE dbo.JobRequests DROP CONSTRAINT CK_JobRequests_Status;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_JobRequests_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.JobRequests')
)
BEGIN
    ALTER TABLE dbo.JobRequests
    ADD CONSTRAINT CK_JobRequests_Status
    CHECK (Status IN (N'PMOReview', N'PresalesReview', N'BenchReview', N'Sourcing', N'Interviewing', N'HiringManagerReview', N'Offer', N'Closed', N'Cancelled'));
END;
GO

INSERT INTO dbo.WorkflowStages
(
    WorkflowStageId,
    TenantId,
    WorkflowDefinitionId,
    StageKey,
    Name,
    StageOrder,
    IsTerminal,
    Status
)
SELECT
    NEWID(),
    wd.TenantId,
    wd.WorkflowDefinitionId,
    N'PRESALES_REVIEW',
    N'Presales Review',
    25,
    CAST(0 AS BIT),
    N'Active'
FROM dbo.WorkflowDefinitions AS wd
WHERE wd.Code = N'JOB_REQUEST_MVP'
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.WorkflowStages AS existing
      WHERE existing.WorkflowDefinitionId = wd.WorkflowDefinitionId
        AND existing.StageKey = N'PRESALES_REVIEW'
  );
GO

UPDATE dbo.WorkflowStages
SET Name = N'Presales Review',
    StageOrder = 25,
    IsTerminal = CAST(0 AS BIT),
    Status = N'Active'
WHERE StageKey = N'PRESALES_REVIEW'
  AND WorkflowDefinitionId IN
  (
      SELECT WorkflowDefinitionId
      FROM dbo.WorkflowDefinitions
      WHERE Code = N'JOB_REQUEST_MVP'
  );
GO

INSERT INTO dbo.WorkflowTransitions
(
    WorkflowTransitionId,
    TenantId,
    WorkflowDefinitionId,
    ActionKey,
    Name,
    FromStageId,
    ToStageId,
    Status
)
SELECT
    NEWID(),
    wd.TenantId,
    wd.WorkflowDefinitionId,
    source.ActionKey,
    source.Name,
    fromStage.WorkflowStageId,
    toStage.WorkflowStageId,
    N'Active'
FROM dbo.WorkflowDefinitions AS wd
INNER JOIN dbo.WorkflowStages AS fromStage
    ON fromStage.WorkflowDefinitionId = wd.WorkflowDefinitionId
INNER JOIN dbo.WorkflowStages AS toStage
    ON toStage.WorkflowDefinitionId = wd.WorkflowDefinitionId
CROSS APPLY
(
    VALUES
        (N'RECOMMEND_EMPLOYEES_TO_PRESALES', N'Recommend Employees to Presales', N'PMO_REVIEW', N'PRESALES_REVIEW'),
        (N'PRESALES_RETURN_TO_PMO', N'Presales Return to PMO', N'PRESALES_REVIEW', N'PMO_REVIEW'),
        (N'PRESALES_ACCEPT_INTERNAL_EMPLOYEE', N'Presales Accept Internal Employee', N'PRESALES_REVIEW', N'CLOSED')
) AS source (ActionKey, Name, FromStageKey, ToStageKey)
WHERE wd.Code = N'JOB_REQUEST_MVP'
  AND fromStage.StageKey = source.FromStageKey
  AND toStage.StageKey = source.ToStageKey
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.WorkflowTransitions AS existing
      WHERE existing.WorkflowDefinitionId = wd.WorkflowDefinitionId
        AND existing.ActionKey = source.ActionKey
  );
GO

UPDATE transition
SET Name = source.Name,
    FromStageId = fromStage.WorkflowStageId,
    ToStageId = toStage.WorkflowStageId,
    Status = N'Active'
FROM dbo.WorkflowTransitions AS transition
INNER JOIN dbo.WorkflowDefinitions AS wd
    ON wd.WorkflowDefinitionId = transition.WorkflowDefinitionId
INNER JOIN dbo.WorkflowStages AS fromStage
    ON fromStage.WorkflowDefinitionId = wd.WorkflowDefinitionId
INNER JOIN dbo.WorkflowStages AS toStage
    ON toStage.WorkflowDefinitionId = wd.WorkflowDefinitionId
CROSS APPLY
(
    VALUES
        (N'RECOMMEND_EMPLOYEES_TO_PRESALES', N'Recommend Employees to Presales', N'PMO_REVIEW', N'PRESALES_REVIEW'),
        (N'PRESALES_RETURN_TO_PMO', N'Presales Return to PMO', N'PRESALES_REVIEW', N'PMO_REVIEW'),
        (N'PRESALES_ACCEPT_INTERNAL_EMPLOYEE', N'Presales Accept Internal Employee', N'PRESALES_REVIEW', N'CLOSED')
) AS source (ActionKey, Name, FromStageKey, ToStageKey)
WHERE wd.Code = N'JOB_REQUEST_MVP'
  AND transition.ActionKey = source.ActionKey
  AND fromStage.StageKey = source.FromStageKey
  AND toStage.StageKey = source.ToStageKey;
GO

INSERT INTO dbo.NotificationEvents
(
    NotificationEventId,
    TenantId,
    EventCode,
    Name,
    DefaultRecipientType,
    Status,
    CreatedAtUtc,
    UpdatedAtUtc
)
SELECT
    NEWID(),
    tenant.TenantId,
    source.EventCode,
    source.Name,
    source.DefaultRecipientType,
    N'Active',
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
FROM dbo.Tenants AS tenant
CROSS APPLY
(
    VALUES
        (N'PRESALES_EMPLOYEE_REFERRAL_ACCEPTED', N'Presales accepted employee referral', N'User:PMOReferralOwner'),
        (N'PRESALES_EMPLOYEE_REFERRAL_REJECTED', N'Presales rejected employee referral', N'User:PMOReferralOwner')
) AS source (EventCode, Name, DefaultRecipientType)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.NotificationEvents AS existing
    WHERE existing.TenantId = tenant.TenantId
      AND existing.EventCode = source.EventCode
);
GO

UPDATE existing
SET Name = source.Name,
    DefaultRecipientType = source.DefaultRecipientType,
    Status = N'Active',
    UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.NotificationEvents AS existing
CROSS APPLY
(
    VALUES
        (N'PRESALES_EMPLOYEE_REFERRAL_ACCEPTED', N'Presales accepted employee referral', N'User:PMOReferralOwner'),
        (N'PRESALES_EMPLOYEE_REFERRAL_REJECTED', N'Presales rejected employee referral', N'User:PMOReferralOwner')
) AS source (EventCode, Name, DefaultRecipientType)
WHERE existing.EventCode = source.EventCode;
GO

INSERT INTO dbo.NotificationTemplates
(
    NotificationTemplateId,
    TenantId,
    NotificationEventId,
    Name,
    Recipient,
    Subject,
    Body,
    AllowedVariablesJson,
    Status,
    CreatedAtUtc,
    UpdatedAtUtc
)
SELECT
    NEWID(),
    event.TenantId,
    event.NotificationEventId,
    source.TemplateName,
    N'PMO Referral Owner',
    source.Subject,
    source.Body,
    source.AllowedVariablesJson,
    N'Active',
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
FROM dbo.NotificationEvents AS event
INNER JOIN
(
    VALUES
        (N'PRESALES_EMPLOYEE_REFERRAL_ACCEPTED', N'Accepted referral email', N'Presales accepted an internal employee for {{jobTitle}}', N'{{requesterName}} accepted {{acceptedCount}} internal employee recommendation(s) and rejected {{rejectedCount}} for {{jobTitle}}.', N'["requesterName","acceptedCount","rejectedCount","jobTitle"]'),
        (N'PRESALES_EMPLOYEE_REFERRAL_REJECTED', N'Rejected referral email', N'Presales rejected internal recommendations for {{jobTitle}}', N'{{requesterName}} rejected {{rejectedCount}} internal employee recommendation(s) for {{jobTitle}}. The request has returned to PMO Review.', N'["requesterName","rejectedCount","jobTitle"]')
) AS source (EventCode, TemplateName, Subject, Body, AllowedVariablesJson)
    ON source.EventCode = event.EventCode
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.NotificationTemplates AS existing
    WHERE existing.TenantId = event.TenantId
      AND existing.NotificationEventId = event.NotificationEventId
      AND existing.Name = source.TemplateName
);
GO
