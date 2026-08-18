using IpRoyalService;

namespace IpRoyalControl;

public static class LogTailReader
{
    public static string Read(string path, string password, string username, int maxBytes = 262_144, int maxLines = 300)
    {
        if (!File.Exists(path)) return "No service logs have been written yet.";
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var start = Math.Max(0, stream.Length - maxBytes);
        stream.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        if (start > 0) reader.ReadLine();
        var lines = new Queue<string>();
        while (reader.ReadLine() is { } line)
        {
            var clean = EngineLogProcessor.Clean(EnforcementController.Redact(line, password, username));
            if (IsLowLevelNoise(clean)) continue;
            lines.Enqueue(clean);
            while (lines.Count > maxLines) lines.Dequeue();
        }
        return string.Join(Environment.NewLine, lines);
    }

    public static bool IsLowLevelNoise(string value)
    {
        var lower = value.ToLowerInvariant();
        return lower.Contains("inbound/tun") || lower.Contains("outbound/direct[rdp-direct]") || lower.Contains("inbound/mixed") || lower.Contains("outbound packet connection") || lower.Contains("outbound connection to") || lower.Contains("exchange") && lower.Contains("dns");
    }
}
