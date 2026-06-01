using TalentPilot.Domain.Access;

namespace TalentPilot.Application.Auth;

public static class RouteAccessMapper
{
    public static IReadOnlyList<string> MapRoutes(IReadOnlyCollection<string> permissions)
    {
        var routes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        if (permissions.Contains(AccessConstants.ManageAdminCenter) ||
            permissions.Contains(AccessConstants.ManageUsers) ||
            permissions.Contains(AccessConstants.ManageRoles) ||
            permissions.Contains(AccessConstants.ManageTenantProfile) ||
            permissions.Contains(AccessConstants.ManageNotifications) ||
            permissions.Contains(AccessConstants.ViewAiSettings) ||
            permissions.Contains(AccessConstants.ViewAuditLogs))
        {
            routes.Add("/admin-center");
        }

        if (permissions.Contains(AccessConstants.ViewJobRequests) ||
            permissions.Contains(AccessConstants.CreateJobRequest))
        {
            routes.Add("/app/job-requests");
        }

        if (permissions.Contains(AccessConstants.ClaimWorkflowTask))
        {
            routes.Add("/app/pmo/queue");
        }

        if (permissions.Contains(AccessConstants.ViewBenchMatches))
        {
            routes.Add("/app/bench-matching");
        }

        if (permissions.Contains(AccessConstants.ManageCandidates))
        {
            routes.Add("/app/candidates");
            routes.Add("/app/candidate-pipeline");
            routes.Add("/app/job-publishing");
            routes.Add("/app/recruitment/queue");
            routes.Add("/app/recruitment/sourcing");
        }

        if (permissions.Contains(AccessConstants.ManageInterviews))
        {
            routes.Add("/app/interview-scheduling");
            routes.Add("/app/interview-feedback");
        }

        if (permissions.Contains(AccessConstants.ManageHiringDecisions))
        {
            routes.Add("/app/hiring-manager/reviews");
            routes.Add("/app/offer-onboarding");
        }

        if (permissions.Contains(AccessConstants.ViewAuditLogs))
        {
            routes.Add("/admin-center/audit-logs");
        }

        return routes.ToArray();
    }
}
