using System.ServiceProcess;
using IpRoyalService;

namespace IpRoyalControl;

public sealed record StatusPresentation(ProxyConnectionState State, string Text, string Protocol);

public static class StatusPresenter
{
    public static StatusPresentation Map(ServiceControllerStatus? service, ConnectionStatus? connection, DateTimeOffset now)
    {
        if (service is null) return new(ProxyConnectionState.Error, "Service is not installed", "—");
        if (service == ServiceControllerStatus.Stopped || service == ServiceControllerStatus.StopPending)
            return new(ProxyConnectionState.Disconnected, "Disconnected", "—");
        if (service != ServiceControllerStatus.Running)
            return new(ProxyConnectionState.Connecting, "Connecting", "—");
        if (connection is null || now - connection.UpdatedUtc > TimeSpan.FromSeconds(30))
            return new(ProxyConnectionState.Connecting, "Connecting (waiting for current status)", "—");
        return connection.State switch
        {
            ProxyConnectionState.Connected => new(connection.State, "Connected", connection.Protocol ?? "—"),
            ProxyConnectionState.Error => new(connection.State, "Connection error / fail-closed", "—"),
            ProxyConnectionState.Disconnected => new(ProxyConnectionState.Connecting, "Connecting", "—"),
            _ => new(connection.State, "Connecting", "—")
        };
    }
}
