-- Migration 005: remove tenant-configurable workflow action permissions.
-- Data impact: drops dbo.WorkflowActionPermissions and its rows. Workflow action
-- authorization is now owned by backend code instead of tenant configuration.

IF OBJECT_ID(N'dbo.WorkflowActionPermissions', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.WorkflowActionPermissions;
END;
GO
