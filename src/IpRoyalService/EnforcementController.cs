using System.Diagnostics;
using System.Text;

namespace IpRoyalService;
public sealed class EnforcementController(SingBoxConfigWriter writer, ILogger<EnforcementController> log) : IAsyncDisposable
{
    private Process? process;
    public bool IsRunning => process is { HasExited: false };
    public EngineLogEvent? LatestFailure { get; private set; }
    public void ClearLatestFailure() => LatestFailure = null;

    public async Task StartAsync(ProxyConfig config, string baseDir, CancellationToken ct)
    {
        if (IsRunning) return;
        LatestFailure = null;
        var exe = Path.Combine(baseDir, "engine", "sing-box.exe");
        if (!File.Exists(exe)) throw new InvalidOperationException($"Required packet engine is missing: {exe}. Reinstall the application from the Windows installer.");
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "IpRoyalService");
        var configPath = writer.Write(config, data);
        SecureRuntimeConfiguration(configPath);
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("run"); psi.ArgumentList.Add("-c"); psi.ArgumentList.Add(configPath);
        process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => HandleEngineLine(e.Data, config);
        process.ErrorDataReceived += (_, e) => HandleEngineLine(e.Data, config);
        if (!process.Start()) throw new InvalidOperationException("Transparent packet engine failed to start.");
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        await Task.Delay(1500, ct);
        if (process.HasExited) throw new InvalidOperationException($"Transparent packet engine exited with code {process.ExitCode}.");
        log.LogInformation("Enforcement engine started with strict IPv4/IPv6 routing and DNS interception");
        log.LogInformation("RDP exemption remains active while proxy enforcement is running");
    }

    public async Task StopAsync()
    {
        var p = process; process = null;
        if (p is null) return;
        try
        {
            if (!p.HasExited) { p.Kill(true); await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        }
        catch (Exception e) { log.LogWarning(e, "Could not cleanly stop enforcement engine"); }
        finally { p.Dispose(); }
        log.LogInformation("Enforcement engine stopped; owned TUN routes were removed");
    }
    public static string Redact(string text, string secret, string? username = null)
    {
        var redacted = text;
        if (!string.IsNullOrEmpty(secret)) redacted = redacted.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(secret))
        {
            var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{secret}"));
            redacted = redacted.Replace(basicToken, "[REDACTED]", StringComparison.Ordinal);
        }
        if (!string.IsNullOrEmpty(username)) redacted = redacted.Replace(username, "[REDACTED]", StringComparison.Ordinal);
        return redacted;
    }

    private void HandleEngineLine(string? line, ProxyConfig config)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        var source = EngineLogProcessor.Clean(line);
        var item = EngineLogProcessor.Classify(source);
        var clean = Redact(source, config.Password, config.Username);
        try { Directory.CreateDirectory(ApplicationPaths.DataDirectory); File.AppendAllText(ApplicationPaths.DiagnosticLogFile, $"{DateTimeOffset.UtcNow:O} {clean}{Environment.NewLine}"); } catch { }
        if (item.Failure != ProxyFailureKind.None) LatestFailure = item;
        if (!item.ShowInUserLog) return;
        if (item.Level == LogLevel.Error) log.LogError("Proxy engine: {Message}", item.Message);
        else if (item.Level == LogLevel.Warning) log.LogWarning("Proxy engine: {Message}", item.Message);
        else log.LogInformation("Proxy engine: {Message}", item.Message);
    }

    private static void SecureRuntimeConfiguration(string path)
    {
        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "icacls.exe"))
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
        using var acl = Process.Start(psi) ?? throw new InvalidOperationException("Could not start Windows credential-file ACL protection.");
        acl.WaitForExit();
        if (acl.ExitCode != 0)
        {
            File.Delete(path);
            throw new InvalidOperationException("Could not restrict the generated proxy runtime configuration to SYSTEM and Administrators.");
        }
    }
    public async ValueTask DisposeAsync() => await StopAsync();
}
