using System.Text.Json;

namespace IpRoyalService;

public sealed class ConnectionStatusPublisher(ILogger<ConnectionStatusPublisher> log)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public void Publish(ProxyConnectionState state, string message, ProxyProtocol? protocol = null)
    {
        try
        {
            Directory.CreateDirectory(ApplicationPaths.DataDirectory);
            var status = new ConnectionStatus(
                state,
                protocol?.ToConfigValue(),
                message,
                DateTimeOffset.UtcNow);
            var temporary = ApplicationPaths.StatusFile + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(status, Options));
            File.Move(temporary, ApplicationPaths.StatusFile, true);
        }
        catch (Exception e)
        {
            log.LogWarning(e, "Could not update the local connection-status snapshot");
        }
    }
}
