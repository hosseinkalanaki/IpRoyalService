namespace IpRoyalService;

public enum ProxyProtocol
{
    Http,
    Socks4,
    Socks5
}

public static class ProxyProtocolNames
{
    public static string ToConfigValue(this ProxyProtocol value) => value switch { ProxyProtocol.Http => "HTTP", ProxyProtocol.Socks4 => "SOCKS4", _ => "SOCKS5" };
    public static bool TryParse(string? value, out ProxyProtocol protocol)
    {
        protocol = ProxyProtocol.Socks5;
        if (string.Equals(value, "HTTP", StringComparison.OrdinalIgnoreCase)) { protocol = ProxyProtocol.Http; return true; }
        if (string.Equals(value, "SOCKS4", StringComparison.OrdinalIgnoreCase)) { protocol = ProxyProtocol.Socks4; return true; }
        if (string.Equals(value, "SOCKS5", StringComparison.OrdinalIgnoreCase)) { protocol = ProxyProtocol.Socks5; return true; }
        return false;
    }
}
