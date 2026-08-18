using System.Diagnostics;
using System.Text.Json;
using IpRoyalService;

namespace IpRoyalControl;

public sealed class ControlConfigStore(string path)
{
    private static readonly JsonSerializerOptions OutputOptions = new() { WriteIndented = true };
    public string Path { get; } = path;

    public ProxyConfig Load()
    {
        if (!File.Exists(Path)) throw new InvalidOperationException($"Configuration file not found: {Path}");
        try { return JsonSerializer.Deserialize<ProxyConfig>(File.ReadAllText(Path)) ?? throw new JsonException("Empty configuration"); }
        catch (Exception e) when (e is JsonException or IOException) { throw new InvalidOperationException("Configuration could not be read or parsed.", e); }
    }

    public void Save(ProxyConfig config, bool protectAcl = true)
    {
        var errors = ConfigLoader.Validate(config);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        var json = JsonSerializer.Serialize(new
        {
            protocol = config.TryGetProtocol(out var protocol) ? protocol.ToConfigValue() : throw new InvalidOperationException("Select HTTP, SOCKS4, or SOCKS5."),
            server = config.Server.Trim(),
            server_port = config.ServerPort,
            reserve_port = config.ReservePort,
            username = config.Username,
            password = config.Password
        }, OutputOptions);

        File.WriteAllText(Path, json + Environment.NewLine);
        if (protectAcl) ProtectConfiguration(Path);
    }

    private static void ProtectConfiguration(string path)
    {
        var psi = new ProcessStartInfo(System.IO.Path.Combine(Environment.SystemDirectory, "icacls.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add(path);
        psi.ArgumentList.Add("/inheritance:r");
        psi.ArgumentList.Add("/grant:r");
        psi.ArgumentList.Add("*S-1-5-18:F");
        psi.ArgumentList.Add("*S-1-5-32-544:F");
        using var acl = Process.Start(psi) ?? throw new InvalidOperationException("Could not protect config.json permissions.");
        acl.WaitForExit();
        if (acl.ExitCode != 0) throw new InvalidOperationException("Could not restrict config.json to SYSTEM and Administrators.");
    }
}
