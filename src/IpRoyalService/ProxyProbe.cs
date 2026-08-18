using System.Net.Sockets;

namespace IpRoyalService;

public enum ProxyFailureKind { None, Timeout, Unreachable, AuthenticationFailed, HandshakeRejected, OutboundValidationFailed, EngineError }
public sealed record ProxyProbeResult(bool Success, ProxyFailureKind Failure, string Message)
{
    public static ProxyProbeResult Connected() => new(true, ProxyFailureKind.None, "Usable outbound proxy traffic was validated.");
}

public interface IProxyPathProbe { Task<ProxyProbeResult> CheckAsync(int localPort, CancellationToken ct); }

public sealed class ProxyProbe : IProxyPathProbe
{
    public async Task<ProxyProbeResult> CheckAsync(int localPort, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", localPort, timeout.Token);
            var stream = tcp.GetStream();
            await stream.WriteAsync(new byte[] { 5, 1, 0 }, timeout.Token);
            var hello = new byte[2]; await stream.ReadExactlyAsync(hello, timeout.Token);
            if (hello[0] != 5 || hello[1] == 0xff) return new(false, ProxyFailureKind.HandshakeRejected, "The local validation channel rejected the handshake.");
            await stream.WriteAsync(new byte[] { 5, 1, 0, 1, 1, 1, 1, 1, 0x01, 0xBB }, timeout.Token);
            var reply = new byte[4]; await stream.ReadExactlyAsync(reply, timeout.Token);
            if (reply[0] == 5 && reply[1] == 0) return ProxyProbeResult.Connected();
            return new(false, ProxyFailureKind.OutboundValidationFailed, reply[1] switch
            {
                2 => "The selected proxy rejected the outbound validation request.",
                3 or 4 => "The selected proxy could not reach the validation destination.",
                5 => "The selected proxy refused the validation connection.",
                7 => "The selected proxy does not support the required connection command.",
                _ => "The selected proxy handshake or outbound validation failed."
            });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(false, ProxyFailureKind.Timeout, "Proxy validation timed out."); }
        catch (SocketException) { return new(false, ProxyFailureKind.Unreachable, "The local proxy validation channel is unreachable."); }
        catch (IOException) { return new(false, ProxyFailureKind.OutboundValidationFailed, "The proxy closed the validation connection."); }
    }
}
