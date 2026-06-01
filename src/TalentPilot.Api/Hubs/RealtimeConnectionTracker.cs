using System.Collections.Concurrent;
using TalentPilot.Application.Notifications;

namespace TalentPilot.Api.Hubs;

public sealed class RealtimeConnectionTracker : IRealtimeConnectionCounter
{
    private readonly ConcurrentDictionary<string, RealtimeConnectionState> _connections = new();

    public void Connect(string connectionId, Guid tenantId, Guid userId)
    {
        _connections[connectionId] = new RealtimeConnectionState(tenantId, userId);
    }

    public void Disconnect(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    public int CountAllConnections()
    {
        return _connections.Count;
    }

    public int CountTenantConnections(Guid tenantId)
    {
        return _connections.Values.Count(connection => connection.TenantId == tenantId);
    }

    public int CountUserConnections(Guid tenantId, Guid userId)
    {
        return _connections.Values.Count(connection =>
            connection.TenantId == tenantId &&
            connection.UserId == userId);
    }

    public IReadOnlyList<Guid> TenantUserIds(Guid tenantId)
    {
        return _connections.Values
            .Where(connection => connection.TenantId == tenantId)
            .Select(connection => connection.UserId)
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();
    }

    private sealed record RealtimeConnectionState(Guid TenantId, Guid UserId);
}
