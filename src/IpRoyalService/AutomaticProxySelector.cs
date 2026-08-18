namespace IpRoyalService;

public sealed class AutomaticProxySelector(IProxyPathProbe probe, IProxyRouteSwitcher switcher, ILogger<AutomaticProxySelector> log)
{
    private ProxyProtocol? selected;
    private ProxyProtocol applied = ProxyProtocol.Socks5;

    public ProxyProtocol? Selected => selected;
    public void Reset() { selected = null; applied = ProxyProtocol.Socks5; }

    public async Task<ProxyProtocol?> EvaluateAsync(int reservePort, CancellationToken ct)
    {
        var socksFailed = false;
        if (selected is { } current)
        {
            var currentPort = current == ProxyProtocol.Socks5 ? reservePort : reservePort + 1;
            if (await probe.CheckAsync(currentPort, ct)) return current;
            log.LogWarning("Selected proxy protocol {Protocol} is no longer usable; re-evaluating with SOCKS5 first", Name(current));
            socksFailed = current == ProxyProtocol.Socks5;
            selected = null;
        }

        if (!socksFailed)
        {
            if (await probe.CheckAsync(reservePort, ct))
            {
                await ApplyAsync(ProxyProtocol.Socks5, reservePort + 2, ct);
                selected = ProxyProtocol.Socks5;
                log.LogInformation("Selected proxy protocol SOCKS5 after authenticated outbound validation");
                return selected;
            }
            log.LogWarning("SOCKS5 authenticated proxy validation failed; attempting HTTP proxy fallback");
        }

        if (await probe.CheckAsync(reservePort + 1, ct))
        {
            await ApplyAsync(ProxyProtocol.Http, reservePort + 2, ct);
            selected = ProxyProtocol.Http;
            log.LogInformation("Selected proxy protocol HTTP after authenticated outbound validation");
            return selected;
        }

        log.LogError("SOCKS5 and HTTP proxy validation both failed; strict TUN enforcement remains fail-closed");
        return null;
    }

    private async Task ApplyAsync(ProxyProtocol protocol, int controllerPort, CancellationToken ct)
    {
        if (applied == protocol) return;
        await switcher.SelectAsync(protocol, controllerPort, ct);
        applied = protocol;
    }

    private static string Name(ProxyProtocol protocol) => protocol == ProxyProtocol.Socks5 ? "SOCKS5" : "HTTP";
}
