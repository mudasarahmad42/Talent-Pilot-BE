namespace TalentPilot.Domain.Access;

public static class AccessConstants
{
    public const string SystemAdminRoleCode = "SystemAdmin";
    public const string TenantAdminRoleCode = "TenantAdmin";
    public const string PmoRoleCode = "PMO";
    public const string HodRoleCode = "HOD";

    public const string ManageAdminCenter = "access.admin.manage";
    public const string ManageUsers = "access.users.manage";
    public const string ManageRoles = "access.roles.manage";
    public const string ViewAuditLogs = "audit.logs.view";
    public const string ManageTenantProfile = "tenant.profile.manage";
    public const string ManageNotifications = "notifications.manage";
    public const string ViewAiSettings = "ai.settings.view";
    public const string AiAssistantUse = "ai.assistant.use";
    public const string ViewJobRequests = "job.requests.view";
    public const string CreateJobRequest = "job.requests.create";
    public const string ClaimWorkflowTask = "workflow.assignments.claim";
    public const string ViewBenchMatches = "bench.matches.view";
    public const string ManageCandidates = "candidates.manage";
    public const string ManageInterviews = "interviews.manage";
    public const string ManageHiringDecisions = "hiring.decisions.manage";
}
