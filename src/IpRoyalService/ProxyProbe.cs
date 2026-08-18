using System.Net.Sockets;

namespace IpRoyalService;
public interface IProxyPathProbe
{
    Task<bool> CheckAsync(int localPort, CancellationToken ct);
}

public sealed class ProxyProbe : IProxyPathProbe
{
    public async Task<bool> CheckAsync(int localPort, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", localPort, timeout.Token);
            var s = tcp.GetStream();
            await s.WriteAsync(new byte[] { 5, 1, 0 }, timeout.Token);
            var hello = new byte[2]; await s.ReadExactlyAsync(hello, timeout.Token);
            if (hello[0] != 5 || hello[1] != 0) return false;
            await s.WriteAsync(new byte[] { 5, 1, 0, 1, 1, 1, 1, 1, 0x01, 0xBB }, timeout.Token);
            var reply = new byte[4]; await s.ReadExactlyAsync(reply, timeout.Token);
            return reply[0] == 5 && reply[1] == 0;
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException) { return false; }
    }
}
