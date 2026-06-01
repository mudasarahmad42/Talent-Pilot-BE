using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TalentPilot.Application.Notifications;

namespace TalentPilot.Api.Hubs;

[Authorize]
public sealed class NotificationsHub : Hub
{
    private readonly RealtimeConnectionTracker _connectionTracker;

    public NotificationsHub(RealtimeConnectionTracker connectionTracker)
    {
        _connectionTracker = connectionTracker;
    }

    public override async Task OnConnectedAsync()
    {
        var tenantId = ReadGuidClaim("tenant_id");
        var userId = ReadGuidClaim(ClaimTypes.NameIdentifier, "sub");

        if (tenantId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeNotificationGroups.Tenant(tenantId));
        }

        if (tenantId != Guid.Empty && userId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeNotificationGroups.User(tenantId, userId));
            _connectionTracker.Connect(Context.ConnectionId, tenantId, userId);
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _connectionTracker.Disconnect(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
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
