using System.Text.Json;
using System.Text.Json.Nodes;

namespace IpRoyalService;
public sealed class SingBoxConfigWriter
{
    public string Write(ProxyConfig c, string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        var socks = new JsonObject { ["type"] = "socks", ["tag"] = "proxy-socks", ["server"] = c.Server, ["server_port"] = c.ServerPort, ["version"] = "5" };
        var http = new JsonObject { ["type"] = "http", ["tag"] = "proxy-http", ["server"] = c.Server, ["server_port"] = c.ServerPort };
        if (!string.IsNullOrWhiteSpace(c.Username))
        {
            socks["username"] = c.Username;
            socks["password"] = c.Password;
            http["username"] = c.Username;
            http["password"] = c.Password;
        }
        var root = new JsonObject
        {
            ["log"] = new JsonObject { ["level"] = "info", ["timestamp"] = true },
            ["dns"] = new JsonObject { ["servers"] = new JsonArray(new JsonObject { ["type"] = "https", ["tag"] = "secure-dns", ["server"] = "1.1.1.1", ["detour"] = "proxy-auto" }), ["final"] = "secure-dns" },
            ["inbounds"] = new JsonArray(
                new JsonObject { ["type"] = "tun", ["tag"] = "system-tun", ["interface_name"] = "iproyal-tun", ["address"] = new JsonArray("172.19.0.1/30", "fdfe:dcba:9876::1/126"), ["auto_route"] = true, ["strict_route"] = true, ["stack"] = "mixed" },
                new JsonObject { ["type"] = "mixed", ["tag"] = "health-socks", ["listen"] = "127.0.0.1", ["listen_port"] = c.ReservePort },
                new JsonObject { ["type"] = "mixed", ["tag"] = "health-http", ["listen"] = "127.0.0.1", ["listen_port"] = c.ReservePort + 1 }),
            ["outbounds"] = new JsonArray(
                socks,
                http,
                new JsonObject { ["type"] = "selector", ["tag"] = "proxy-auto", ["outbounds"] = new JsonArray("proxy-socks", "proxy-http"), ["default"] = "proxy-socks" },
                new JsonObject { ["type"] = "direct", ["tag"] = "rdp-direct" }),
            ["route"] = new JsonObject { ["auto_detect_interface"] = true, ["rules"] = new JsonArray(
                new JsonObject { ["inbound"] = "health-socks", ["outbound"] = "proxy-socks" },
                new JsonObject { ["inbound"] = "health-http", ["outbound"] = "proxy-http" },
                new JsonObject { ["protocol"] = "dns", ["action"] = "hijack-dns" },
                new JsonObject { ["port"] = 3389, ["outbound"] = "rdp-direct" },
                new JsonObject { ["source_port"] = 3389, ["outbound"] = "rdp-direct" },
                new JsonObject { ["ip_is_private"] = true, ["outbound"] = "rdp-direct" }), ["final"] = "proxy-auto" },
            ["experimental"] = new JsonObject { ["clash_api"] = new JsonObject { ["external_controller"] = $"127.0.0.1:{c.ReservePort + 2}" } }
        };
        var path = Path.Combine(dataDir, "engine.json");
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }
}
