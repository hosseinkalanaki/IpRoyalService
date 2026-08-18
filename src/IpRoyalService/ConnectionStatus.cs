using System.Text.Json.Serialization;

namespace IpRoyalService;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProxyConnectionState { Disconnected, Connecting, Connected, AuthenticationFailed, ProxyUnreachable, InvalidConfiguration, ConnectionLost, Reconnecting, EnforcementUnavailable, ServiceError }

public sealed record ConnectionStatus(
    ProxyConnectionState State,
    string? Protocol,
    string Message,
    DateTimeOffset UpdatedUtc);

public static class ApplicationPaths
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "IpRoyalService");

    public static string StatusFile => Path.Combine(DataDirectory, "status.json");
    public static string LogFile => Path.Combine(DataDirectory, "service.log");
    public static string DiagnosticLogFile => Path.Combine(DataDirectory, "engine-debug.log");
}
