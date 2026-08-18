using System.Text.RegularExpressions;

namespace IpRoyalService;

public sealed record EngineLogEvent(LogLevel Level, string Message, bool ShowInUserLog, ProxyFailureKind Failure = ProxyFailureKind.None);

public static partial class EngineLogProcessor
{
    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])")]
    private static partial Regex AnsiRegex();

    public static string Clean(string value) => AnsiRegex().Replace(value, string.Empty).Replace("\0", string.Empty).Trim();

    public static EngineLogEvent Classify(string raw)
    {
        var text = Clean(raw);
        var lower = text.ToLowerInvariant();
        if (lower.Contains("authentication failed") || lower.Contains("407 proxy authentication") || lower.Contains("invalid username") || lower.Contains("invalid password"))
            return new(LogLevel.Error, "Proxy authentication was rejected.", true, ProxyFailureKind.AuthenticationFailed);
        if (lower.Contains("no such host") || lower.Contains("name resolution") || lower.Contains("lookup "))
            return new(LogLevel.Error, "The proxy host could not be resolved.", true, ProxyFailureKind.Unreachable);
        if (lower.Contains("i/o timeout") || lower.Contains("deadline exceeded") || lower.Contains("timed out"))
            return new(LogLevel.Error, "The proxy connection timed out.", true, ProxyFailureKind.Timeout);
        if (lower.Contains("connection refused") || lower.Contains("actively refused"))
            return new(LogLevel.Error, "The proxy server refused the connection.", true, ProxyFailureKind.Unreachable);
        if (lower.Contains("unsupported") || lower.Contains("bad response") || lower.Contains("handshake"))
            return new(LogLevel.Error, "The endpoint rejected or does not support the selected protocol.", true, ProxyFailureKind.HandshakeRejected);
        if (lower.Contains("error") || lower.Contains("fatal")) return new(LogLevel.Error, text, true, ProxyFailureKind.EngineError);
        if (lower.Contains("warn")) return new(LogLevel.Warning, text, true);
        return new(LogLevel.Information, text, false);
    }
}
