namespace IpRoyalService;
public sealed class ProxyEnforcementWorker(ConfigLoader loader, EnforcementController enforcement, ProxyProbe probe, ILogger<ProxyEnforcementWorker> log, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseDir = AppContext.BaseDirectory;
        ProxyConfig config;
        try { config = loader.Load(Path.Combine(baseDir, "config.json")); }
        catch (Exception e) { log.LogCritical(e, "Startup aborted: configuration invalid; no networking state was changed"); lifetime.StopApplication(); return; }

        var unhealthyLogged = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!enforcement.IsRunning) await enforcement.StartAsync(config, baseDir, stoppingToken);
                var healthy = await probe.CheckAsync(config.ReservePort, stoppingToken);
                if (healthy)
                {
                    if (unhealthyLogged) log.LogInformation("Proxy connectivity recovered; proxied traffic is permitted again");
                    else log.LogInformation("Proxy authenticated and usable; enforcement healthy");
                    unhealthyLogged = false;
                }
                else
                {
                    if (!unhealthyLogged) log.LogError("Proxy health check failed; strict TUN routing remains fail-closed while RDP and private/local traffic remain direct");
                    unhealthyLogged = true;
                    if (!enforcement.IsRunning) await enforcement.StopAsync();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { log.LogError(e, "Enforcement cycle failed; retrying with exponential-safe fixed delay"); unhealthyLogged = true; await enforcement.StopAsync(); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
    public override async Task StopAsync(CancellationToken cancellationToken) { log.LogInformation("Intentional shutdown requested"); await enforcement.StopAsync(); await base.StopAsync(cancellationToken); }
}
