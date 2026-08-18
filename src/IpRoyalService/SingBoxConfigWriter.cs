using System.Text.Json;
using System.Text.Json.Nodes;

namespace IpRoyalService;
public sealed class SingBoxConfigWriter
{
    public string Write(ProxyConfig c, string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        var root = new JsonObject
        {
            ["log"] = new JsonObject { ["level"] = "info", ["timestamp"] = true },
            ["dns"] = new JsonObject { ["servers"] = new JsonArray(new JsonObject { ["type"] = "https", ["tag"] = "secure-dns", ["server"] = "1.1.1.1", ["detour"] = "proxy" }), ["final"] = "secure-dns" },
            ["inbounds"] = new JsonArray(
                new JsonObject { ["type"] = "tun", ["tag"] = "system-tun", ["interface_name"] = "iproyal-tun", ["address"] = new JsonArray("172.19.0.1/30", "fdfe:dcba:9876::1/126"), ["auto_route"] = true, ["strict_route"] = true, ["stack"] = "mixed" },
                new JsonObject { ["type"] = "mixed", ["tag"] = "health-listener", ["listen"] = "127.0.0.1", ["listen_port"] = c.ReservePort }),
            ["outbounds"] = new JsonArray(
                new JsonObject { ["type"] = "socks", ["tag"] = "proxy", ["server"] = c.Server, ["server_port"] = c.ServerPort, ["version"] = "5", ["username"] = c.Username, ["password"] = c.Password },
                new JsonObject { ["type"] = "direct", ["tag"] = "rdp-direct" }),
            ["route"] = new JsonObject { ["auto_detect_interface"] = true, ["rules"] = new JsonArray(
                new JsonObject { ["protocol"] = "dns", ["action"] = "hijack-dns" },
                new JsonObject { ["port"] = 3389, ["outbound"] = "rdp-direct" },
                new JsonObject { ["source_port"] = 3389, ["outbound"] = "rdp-direct" },
                new JsonObject { ["ip_is_private"] = true, ["outbound"] = "rdp-direct" }), ["final"] = "proxy" }
        };
        var path = Path.Combine(dataDir, "engine.json");
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }
}
