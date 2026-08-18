using System.ServiceProcess;
using IpRoyalService;

namespace IpRoyalControl;

public sealed record StatusPresentation(ProxyConnectionState State, string Text, string Protocol);

public static class StatusPresenter
{
    public static StatusPresentation Map(ServiceControllerStatus? service, ConnectionStatus? connection, DateTimeOffset now)
    {
        if (service is null) return new(ProxyConnectionState.ServiceError, "Service is not installed", "—");
        if (service == ServiceControllerStatus.Stopped || service == ServiceControllerStatus.StopPending)
            return new(ProxyConnectionState.Disconnected, "Disconnected", "—");
        if (service != ServiceControllerStatus.Running)
            return new(ProxyConnectionState.Connecting, "Connecting", "—");
        if (connection is null || now - connection.UpdatedUtc > TimeSpan.FromSeconds(30))
            return new(ProxyConnectionState.Connecting, "Connecting (waiting for current status)", "—");
        return connection.State switch
        {
            ProxyConnectionState.Connected => new(connection.State, "Connected", connection.Protocol ?? "—"),
            ProxyConnectionState.AuthenticationFailed => new(connection.State, "Authentication failed / fail-closed", connection.Protocol ?? "—"),
            ProxyConnectionState.ProxyUnreachable => new(connection.State, "Proxy unreachable / fail-closed", connection.Protocol ?? "—"),
            ProxyConnectionState.InvalidConfiguration => new(connection.State, "Invalid configuration", "—"),
            ProxyConnectionState.ConnectionLost => new(connection.State, "Connection lost / fail-closed", connection.Protocol ?? "—"),
            ProxyConnectionState.Reconnecting => new(connection.State, "Reconnecting", connection.Protocol ?? "—"),
            ProxyConnectionState.EnforcementUnavailable => new(connection.State, "Proxy unavailable / fail-closed", connection.Protocol ?? "—"),
            ProxyConnectionState.ServiceError => new(connection.State, "Service error", connection.Protocol ?? "—"),
            ProxyConnectionState.Disconnected => new(ProxyConnectionState.Connecting, "Connecting", "—"),
            _ => new(connection.State, "Connecting", "—")
        };
    }
}
