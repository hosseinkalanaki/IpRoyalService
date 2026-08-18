namespace IpRoyalService;
public sealed class ProxyEnforcementWorker(ConfigLoader loader, EnforcementController enforcement, AutomaticProxySelector selector, ILogger<ProxyEnforcementWorker> log, IHostApplicationLifetime lifetime) : BackgroundService
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
                if (!enforcement.IsRunning)
                {
                    await enforcement.StartAsync(config, baseDir, stoppingToken);
                    selector.Reset();
                }
                var protocol = await selector.EvaluateAsync(config.ReservePort, stoppingToken);
                if (protocol is not null)
                {
                    if (unhealthyLogged) log.LogInformation("Proxy connectivity recovered using {Protocol}; proxied traffic is permitted again", protocol);
                    else log.LogInformation("Authenticated proxy is usable via {Protocol}; enforcement healthy", protocol);
                    unhealthyLogged = false;
                }
                else
                {
                    if (!unhealthyLogged) log.LogError("No supported proxy protocol is usable; strict TUN routing remains fail-closed while RDP and private/local traffic remain direct");
                    unhealthyLogged = true;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                log.LogError(e, "Enforcement cycle failed; strict routing remains active when possible and selection will retry");
                unhealthyLogged = true;
                if (!enforcement.IsRunning) await enforcement.StopAsync();
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
    public override async Task StopAsync(CancellationToken cancellationToken) { log.LogInformation("Intentional shutdown requested"); await enforcement.StopAsync(); await base.StopAsync(cancellationToken); }
}
