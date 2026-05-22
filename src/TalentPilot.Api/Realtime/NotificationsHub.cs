using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace TalentPilot.Api.Realtime;

public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = ReadGuidClaim("tenant_id");
        var userId = ReadGuidClaim(ClaimTypes.NameIdentifier, "sub");

        if (tenantId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, NotificationHubGroups.Tenant(tenantId));
        }

        if (tenantId != Guid.Empty && userId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, NotificationHubGroups.User(tenantId, userId));
        }

        await base.OnConnectedAsync();
    }

    private Guid ReadGuidClaim(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = Context.User?.FindFirstValue(claimType);
            if (Guid.TryParse(value, out var id))
            {
                return id;
            }
        }

        return Guid.Empty;
    }
}
