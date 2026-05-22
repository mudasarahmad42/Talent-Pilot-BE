namespace TalentPilot.Api.Realtime;

internal static class NotificationHubGroups
{
    public static string Tenant(Guid tenantId)
    {
        return $"tenant:{tenantId:D}";
    }

    public static string User(Guid tenantId, Guid userId)
    {
        return $"tenant:{tenantId:D}:user:{userId:D}";
    }
}
