namespace IpRoyalService;

public sealed class ProxyEnforcementWorker(ConfigLoader loader, EnforcementController enforcement, IProxyPathProbe probe, ConnectionStatusPublisher status, ILogger<ProxyEnforcementWorker> log, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseDir = AppContext.BaseDirectory;
        ProxyConfig config;
        status.Publish(ProxyConnectionState.Connecting, "Loading proxy configuration.");
        try { config = loader.Load(Path.Combine(baseDir, "config.json")); }
        catch (Exception e) { status.Publish(ProxyConnectionState.InvalidConfiguration, e.Message); log.LogCritical("Configuration validation failure: {Reason}", e.Message); lifetime.StopApplication(); return; }
        config.TryGetProtocol(out var protocol);
        log.LogInformation("Service started; selected protocol is {Protocol}", protocol.ToConfigValue());
        var wasConnected = false;
        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                attempt++;
                status.Publish(attempt == 1 ? ProxyConnectionState.Connecting : ProxyConnectionState.Reconnecting, attempt == 1 ? "Connecting to the selected proxy." : $"Reconnect attempt {attempt}.", protocol);
                if (!enforcement.IsRunning) await enforcement.StartAsync(config, baseDir, stoppingToken);
                log.LogInformation("Validating usable outbound traffic through {Protocol}", protocol.ToConfigValue());
                enforcement.ClearLatestFailure();
                var result = await probe.CheckAsync(config.ReservePort, stoppingToken);
                if (result.Success)
                {
                    if (wasConnected) log.LogInformation("Proxy connection remains healthy using {Protocol}", protocol.ToConfigValue());
                    else if (attempt > 1) log.LogInformation("Reconnected successfully using {Protocol}; traffic restored", protocol.ToConfigValue());
                    else log.LogInformation("Proxy connection succeeded using {Protocol}; authentication and outbound traffic validated", protocol.ToConfigValue());
                    status.Publish(ProxyConnectionState.Connected, "Proxy authentication and usable outbound traffic are validated.", protocol);
                    wasConnected = true;
                }
                else
                {
                    var failure = enforcement.LatestFailure is { Failure: not ProxyFailureKind.None } engineFailure
                        ? new ProxyProbeResult(false, engineFailure.Failure, engineFailure.Message) : result;
                    var state = failure.Failure switch { ProxyFailureKind.AuthenticationFailed => ProxyConnectionState.AuthenticationFailed, ProxyFailureKind.Unreachable or ProxyFailureKind.Timeout => ProxyConnectionState.ProxyUnreachable, _ => wasConnected ? ProxyConnectionState.ConnectionLost : ProxyConnectionState.EnforcementUnavailable };
                    log.LogError("{Reason} Fail-closed protection is active; RDP remains exempt", failure.Message);
                    status.Publish(state, failure.Message + " Fail-closed protection remains active.", protocol);
                    wasConnected = false;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                log.LogError("Service enforcement error: {Reason}; fail-closed protection remains active when possible", e.Message);
                status.Publish(ProxyConnectionState.ServiceError, "The proxy engine could not start or continue. Check the logs.", protocol);
                if (!enforcement.IsRunning) await enforcement.StopAsync();
                wasConnected = false;
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("Service stop requested");
        await enforcement.StopAsync();
        status.Publish(ProxyConnectionState.Disconnected, "Service stopped.");
        await base.StopAsync(cancellationToken);
    }
}
