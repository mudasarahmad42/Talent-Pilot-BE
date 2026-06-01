SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @RecruitingDeliveryGroupId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444402';
DECLARE @InterviewerRoleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222205';
DECLARE @JobRequestId UNIQUEIDENTIFIER = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee01';
DECLARE @PresalesRequestSubmittedEventId UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555501';
DECLARE @InterviewTemplateId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff01';
DECLARE @RoundDepartmentHeadId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff13';
DECLARE @JobRoundDepartmentHeadId UNIQUEIDENTIFIER = 'ffffffff-ffff-ffff-ffff-ffffffffff23';

UPDATE dbo.Groups
SET
    Name = N'PMO - General',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND Purpose = N'WorkflowRouting'
  AND Name = N'PMO Queue'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.Groups AS existing
      WHERE existing.TenantId = @TenantId
        AND existing.Purpose = N'WorkflowRouting'
        AND existing.Name = N'PMO - General'
  );

UPDATE dbo.Groups
SET
    Name = N'Recruiting - General',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND Purpose = N'WorkflowRouting'
  AND Name = N'Recruiting Queue'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.Groups AS existing
      WHERE existing.TenantId = @TenantId
        AND existing.Purpose = N'WorkflowRouting'
        AND existing.Name = N'Recruiting - General'
  );

UPDATE dbo.Groups
SET
    Name = N'Interview Panel - General',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND Purpose = N'WorkflowRouting'
  AND Name = N'Interview Panel'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.Groups AS existing
      WHERE existing.TenantId = @TenantId
        AND existing.Purpose = N'WorkflowRouting'
        AND existing.Name = N'Interview Panel - General'
  );

UPDATE dbo.Groups
SET
    Name = N'Interview Panel - Screening',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND Purpose = N'WorkflowRouting'
  AND Name = N'Screening Panel'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.Groups AS existing
      WHERE existing.TenantId = @TenantId
        AND existing.Purpose = N'WorkflowRouting'
        AND existing.Name = N'Interview Panel - Screening'
  );

UPDATE dbo.WorkflowRoutingRules
SET
    AssignmentType = N'DynamicResolver',
    TargetUserId = NULL,
    TargetGroupId = NULL,
    TargetRoleId = NULL,
    ResolverKey = N'DepartmentIntakeRoute',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND WorkflowRoutingRuleId = '99999999-aaaa-bbbb-cccc-000000000301';

UPDATE dbo.WorkflowRoutingRules
SET
    AssignmentType = N'Group',
    TargetUserId = NULL,
    TargetGroupId = @RecruitingDeliveryGroupId,
    TargetRoleId = NULL,
    ResolverKey = NULL,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND WorkflowRoutingRuleId = '99999999-aaaa-bbbb-cccc-000000000302';

UPDATE dbo.WorkflowRoutingRules
SET
    AssignmentType = N'DynamicResolver',
    TargetUserId = NULL,
    TargetGroupId = NULL,
    TargetRoleId = NULL,
    ResolverKey = N'CandidateInterviewRounds',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND WorkflowRoutingRuleId = '99999999-aaaa-bbbb-cccc-000000000303';

UPDATE dbo.WorkflowRoutingRules
SET
    AssignmentType = N'DynamicResolver',
    TargetUserId = NULL,
    TargetGroupId = NULL,
    TargetRoleId = NULL,
    ResolverKey = N'JobRequestHiringManager',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND WorkflowRoutingRuleId = '99999999-aaaa-bbbb-cccc-000000000304';

UPDATE dbo.JobRequests
SET
    HiringManagerGroupId = NULL,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND JobRequestId = @JobRequestId;

UPDATE dbo.InterviewTemplates
SET
    Description = N'Starter interview template recruiters can copy and customize per job post.',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND InterviewTemplateId = @InterviewTemplateId;

UPDATE dbo.InterviewTemplateRounds
SET
    Name = N'Department Head Interview',
    OwnerRoleId = @InterviewerRoleId,
    DurationMinutes = 45,
    Status = N'Active'
WHERE TenantId = @TenantId
  AND InterviewTemplateRoundId = @RoundDepartmentHeadId;

UPDATE dbo.JobRequestInterviewRounds
SET
    Name = N'Department Head Interview',
    OwnerRoleId = @InterviewerRoleId,
    Status = N'Pending'
WHERE TenantId = @TenantId
  AND JobRequestInterviewRoundId = @JobRoundDepartmentHeadId;

UPDATE dbo.NotificationEvents
SET
    DefaultRecipientType = N'DepartmentIntakeRoute',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND NotificationEventId = @PresalesRequestSubmittedEventId;

UPDATE dbo.NotificationTemplates
SET
    Recipient = N'Configured department intake recipient',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND NotificationEventId = @PresalesRequestSubmittedEventId;
GO
