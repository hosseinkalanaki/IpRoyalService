using System.ServiceProcess;
using System.Text.Json;
using IpRoyalControl;
using IpRoyalService;
using Xunit;

namespace IpRoyalService.Tests;

public sealed class ControlApplicationTests
{
    [Fact]
    public void Older_configuration_without_protocol_remains_editable()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"server":"legacy.proxy","server_port":8080,"reserve_port":11200,"username":"kept-user","password":"kept-password"}""");
            var value = new ControlConfigStore(path).Load();
            Assert.Equal("legacy.proxy", value.Server); Assert.Equal("kept-user", value.Username); Assert.False(value.TryGetProtocol(out _));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Configuration_round_trips_using_service_contract_with_protocol()
    {
        var directory = Directory.CreateTempSubdirectory("iproyal-control-");
        try
        {
            var path = Path.Combine(directory.FullName, "config.json");
            var store = new ControlConfigStore(path);
            var expected = new ProxyConfig(null, null, "proxy.example", 1234, 11200, "alice", "top-secret", "SOCKS4");
            store.Save(expected, protectAcl: false);
            var actual = store.Load();
            Assert.Equal(expected with { Type = null, Version = null }, actual);
            var text = File.ReadAllText(path);
            Assert.Contains("\"protocol\": \"SOCKS4\"", text);
            Assert.DoesNotContain("\"type\"", text);
            Assert.DoesNotContain("\"version\"", text);
        }
        finally { directory.Delete(true); }
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("user", "")]
    [InlineData("", "")]
    public void Controller_preserves_protocol_specific_optional_credentials(string user, string pass)
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new ControlConfigStore(path);
            store.Save(new ProxyConfig(null, null, "proxy", 1080, 11200, user, pass, "SOCKS4"), false);
            Assert.Equal(user, store.Load().Username);
            Assert.Equal(pass, store.Load().Password);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Running_service_is_not_connected_without_fresh_connected_snapshot()
    {
        var missing = StatusPresenter.Map(ServiceControllerStatus.Running, null, DateTimeOffset.UtcNow);
        var stale = StatusPresenter.Map(ServiceControllerStatus.Running,
            new ConnectionStatus(ProxyConnectionState.Connected, "SOCKS5", "ok", DateTimeOffset.UtcNow.AddMinutes(-2)), DateTimeOffset.UtcNow);
        Assert.Equal(ProxyConnectionState.Connecting, missing.State);
        Assert.Equal(ProxyConnectionState.Connecting, stale.State);
    }

    [Fact]
    public void Stopped_service_is_always_disconnected_even_with_stale_success()
    {
        var value = StatusPresenter.Map(ServiceControllerStatus.Stopped,
            new ConnectionStatus(ProxyConnectionState.Connected, "HTTP", "ok", DateTimeOffset.UtcNow), DateTimeOffset.UtcNow);
        Assert.Equal(ProxyConnectionState.Disconnected, value.State);
        Assert.Equal("—", value.Protocol);
    }

    [Fact]
    public void Fresh_connection_status_exposes_only_selected_protocol()
    {
        var status = new ConnectionStatus(ProxyConnectionState.Connected, "HTTP", "healthy", DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(status);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("HTTP", StatusPresenter.Map(ServiceControllerStatus.Running, status, DateTimeOffset.UtcNow).Protocol);
    }

    [Theory]
    [InlineData(ProxyConnectionState.AuthenticationFailed, "Authentication failed")]
    [InlineData(ProxyConnectionState.ProxyUnreachable, "Proxy unreachable")]
    [InlineData(ProxyConnectionState.InvalidConfiguration, "Invalid configuration")]
    [InlineData(ProxyConnectionState.ConnectionLost, "Connection lost")]
    [InlineData(ProxyConnectionState.Reconnecting, "Reconnecting")]
    [InlineData(ProxyConnectionState.EnforcementUnavailable, "Proxy unavailable")]
    [InlineData(ProxyConnectionState.ServiceError, "Service error")]
    public void Failure_states_are_presented_actionably(ProxyConnectionState state, string expected)
    {
        var status = new ConnectionStatus(state, "SOCKS5", "diagnostic", DateTimeOffset.UtcNow);
        Assert.Contains(expected, StatusPresenter.Map(ServiceControllerStatus.Running, status, DateTimeOffset.UtcNow).Text);
    }

    [Fact]
    public void Log_reader_redacts_plain_and_basic_credentials()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user:secret"));
            File.WriteAllText(path, $"plain secret\nbasic {token}\n");
            var result = LogTailReader.Read(path, "secret", "user");
            Assert.DoesNotContain("secret", result);
            Assert.DoesNotContain(token, result);
            Assert.Contains("[REDACTED]", result);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Log_reader_strips_ansi_and_suppresses_packet_noise()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "\u001b[32mINFO inbound/tun packet connection\u001b[0m\nActionable service event\n");
            var result = LogTailReader.Read(path, "", "");
            Assert.DoesNotContain("inbound/tun", result);
            Assert.False(result.Contains('\u001b'));
            Assert.Contains("Actionable service event", result);
        }
        finally { File.Delete(path); }
    }
}
