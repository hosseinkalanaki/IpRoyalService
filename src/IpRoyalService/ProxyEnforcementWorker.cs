namespace IpRoyalService;
public sealed class ProxyEnforcementWorker(ConfigLoader loader, EnforcementController enforcement, AutomaticProxySelector selector, ConnectionStatusPublisher status, ILogger<ProxyEnforcementWorker> log, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseDir = AppContext.BaseDirectory;
        ProxyConfig config;
        status.Publish(ProxyConnectionState.Connecting, "Loading configuration and starting enforcement.");
        try { config = loader.Load(Path.Combine(baseDir, "config.json")); }
        catch (Exception e) { status.Publish(ProxyConnectionState.Error, "Configuration is invalid. Check config.json and restart the service."); log.LogCritical(e, "Startup aborted: configuration invalid; no networking state was changed"); lifetime.StopApplication(); return; }

        var unhealthyLogged = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!enforcement.IsRunning)
                {
                    status.Publish(ProxyConnectionState.Connecting, "Starting fail-closed enforcement and testing the proxy.");
                    await enforcement.StartAsync(config, baseDir, stoppingToken);
                    selector.Reset();
                }
                var protocol = await selector.EvaluateAsync(config.ReservePort, stoppingToken);
                if (protocol is not null)
                {
                    if (unhealthyLogged) log.LogInformation("Proxy connectivity recovered using {Protocol}; proxied traffic is permitted again", protocol);
                    else log.LogInformation("Authenticated proxy is usable via {Protocol}; enforcement healthy", protocol);
                    status.Publish(ProxyConnectionState.Connected, "Proxy connection and fail-closed enforcement are healthy.", protocol);
                    unhealthyLogged = false;
                }
                else
                {
                    if (!unhealthyLogged) log.LogError("No supported proxy protocol is usable; strict TUN routing remains fail-closed while RDP and private/local traffic remain direct");
                    status.Publish(ProxyConnectionState.Error, "Neither SOCKS5 nor HTTP is currently usable; Internet traffic remains fail-closed.");
                    unhealthyLogged = true;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                log.LogError(e, "Enforcement cycle failed; strict routing remains active when possible and selection will retry");
                status.Publish(ProxyConnectionState.Error, "The enforcement cycle failed and will retry; fail-closed protection remains active when possible.");
                unhealthyLogged = true;
                if (!enforcement.IsRunning) await enforcement.StopAsync();
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
    public override async Task StopAsync(CancellationToken cancellationToken) { log.LogInformation("Intentional shutdown requested"); await enforcement.StopAsync(); status.Publish(ProxyConnectionState.Disconnected, "The Windows service is stopped."); await base.StopAsync(cancellationToken); }
}
