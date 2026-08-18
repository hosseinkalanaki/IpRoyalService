using System.Text.Json.Serialization;

namespace IpRoyalService;
public sealed record ProxyConfig(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("server")] string Server,
    [property: JsonPropertyName("server_port")] int ServerPort,
    [property: JsonPropertyName("reserve_port")] int ReservePort,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);
