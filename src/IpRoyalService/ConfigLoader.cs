using System.Text.Json;

namespace IpRoyalService;
public sealed class ConfigLoader(ILogger<ConfigLoader> log)
{
    public ProxyConfig Load(string path)
    {
        if (!File.Exists(path)) throw new InvalidOperationException($"Configuration file not found: {path}");
        ProxyConfig value;
        try { value = JsonSerializer.Deserialize<ProxyConfig>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = false }) ?? throw new JsonException("Empty configuration"); }
        catch (Exception e) when (e is JsonException or IOException) { throw new InvalidOperationException("Configuration could not be read or parsed.", e); }
        var errors = Validate(value);
        if (errors.Count != 0) throw new InvalidOperationException("Invalid configuration: " + string.Join("; ", errors));
        log.LogInformation("Configuration validated: type={Type}, version={Version}, server={Server}, serverPort={ServerPort}, reservePort={ReservePort}, usernamePresent={UsernamePresent}", value.Type, value.Version, value.Server, value.ServerPort, value.ReservePort, !string.IsNullOrEmpty(value.Username));
        return value;
    }

    public static IReadOnlyList<string> Validate(ProxyConfig c)
    {
        var e = new List<string>();
        if (!string.Equals(c.Type, "socks", StringComparison.OrdinalIgnoreCase)) e.Add("type must be 'socks'");
        if (c.Version != "5") e.Add("version must be '5'");
        if (string.IsNullOrWhiteSpace(c.Server)) e.Add("server is required");
        if (c.ServerPort is < 1 or > 65535) e.Add("server_port must be 1..65535");
        if (c.ReservePort is < 1 or > 65535) e.Add("reserve_port must be 1..65535");
        if (c.ServerPort == c.ReservePort) e.Add("reserve_port must differ from server_port");
        var hasUsername = !string.IsNullOrWhiteSpace(c.Username);
        var hasPassword = !string.IsNullOrEmpty(c.Password);
        if (hasUsername != hasPassword) e.Add("username and password must either both be provided or both be empty for a proxy without authentication");
        return e;
    }
}
