using System.Diagnostics;

namespace IpRoyalService;
public interface IProcessRunner { Task<int> RunAsync(string file, IEnumerable<string> args, CancellationToken ct); }
public sealed class ProcessRunner(ILogger<ProcessRunner> log) : IProcessRunner
{
    public async Task<int> RunAsync(string file, IEnumerable<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log.LogInformation("engine: {Message}", e.Data); };
        p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log.LogWarning("engine: {Message}", e.Data); };
        if (!p.Start()) throw new InvalidOperationException($"Could not start {Path.GetFileName(file)}");
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { if (!p.HasExited) p.Kill(true); throw; }
        return p.ExitCode;
    }
}
