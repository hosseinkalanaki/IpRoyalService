using System.Text.Json.Serialization;

namespace IpRoyalService;
public sealed record ProxyConfig(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("server")] string Server,
    [property: JsonPropertyName("server_port")] int ServerPort,
    [property: JsonPropertyName("reserve_port")] int ReservePort,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("protocol")] string? Protocol = null)
{
    public bool TryGetProtocol(out ProxyProtocol protocol)
    {
        if (ProxyProtocolNames.TryParse(Protocol, out protocol)) return true;
        if (string.Equals(Type, "http", StringComparison.OrdinalIgnoreCase)) { protocol = ProxyProtocol.Http; return true; }
        if (string.Equals(Type, "socks", StringComparison.OrdinalIgnoreCase) && Version == "4") { protocol = ProxyProtocol.Socks4; return true; }
        if (string.Equals(Type, "socks", StringComparison.OrdinalIgnoreCase) && Version == "5") { protocol = ProxyProtocol.Socks5; return true; }
        return false;
    }
}
