using System.Net.Http.Json;

namespace IpRoyalService;

public interface IProxyRouteSwitcher
{
    Task SelectAsync(ProxyProtocol protocol, int controllerPort, CancellationToken ct);
}

public sealed class ProxyRouteSwitcher(HttpClient client) : IProxyRouteSwitcher
{
    public async Task SelectAsync(ProxyProtocol protocol, int controllerPort, CancellationToken ct)
    {
        var name = protocol == ProxyProtocol.Socks5 ? "proxy-socks" : "proxy-http";
        using var response = await client.PutAsJsonAsync($"http://127.0.0.1:{controllerPort}/proxies/proxy-auto", new { name }, ct);
        response.EnsureSuccessStatusCode();
    }
}
