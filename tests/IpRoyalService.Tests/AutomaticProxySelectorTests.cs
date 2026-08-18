using IpRoyalService;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IpRoyalService.Tests;

public sealed class AutomaticProxySelectorTests
{
    private const int Port = 11200;

    [Fact]
    public async Task Socks5_success_does_not_probe_or_switch_to_http()
    {
        var probe = new FakeProbe((Port, true));
        var route = new FakeSwitcher();
        var selector = Create(probe, route);

        Assert.Equal(ProxyProtocol.Socks5, await selector.EvaluateAsync(Port, default));
        Assert.Equal(new[] { Port }, probe.Ports);
        Assert.Empty(route.Protocols);
    }

    [Fact]
    public async Task Http_is_tried_only_after_socks5_failure()
    {
        var probe = new FakeProbe((Port, false), (Port + 1, true));
        var route = new FakeSwitcher();
        var selector = Create(probe, route);

        Assert.Equal(ProxyProtocol.Http, await selector.EvaluateAsync(Port, default));
        Assert.Equal(new[] { Port, Port + 1 }, probe.Ports);
        Assert.Equal(new[] { ProxyProtocol.Http }, route.Protocols);
    }

    [Fact]
    public async Task Complete_failure_keeps_selection_unhealthy_without_route_switch()
    {
        var probe = new FakeProbe((Port, false), (Port + 1, false));
        var route = new FakeSwitcher();
        var selector = Create(probe, route);

        Assert.Null(await selector.EvaluateAsync(Port, default));
        Assert.Null(selector.Selected);
        Assert.Empty(route.Protocols);
    }

    [Fact]
    public async Task Successful_http_selection_is_retained_until_health_failure()
    {
        var probe = new FakeProbe((Port, false), (Port + 1, true), (Port + 1, true));
        var route = new FakeSwitcher();
        var selector = Create(probe, route);

        Assert.Equal(ProxyProtocol.Http, await selector.EvaluateAsync(Port, default));
        Assert.Equal(ProxyProtocol.Http, await selector.EvaluateAsync(Port, default));
        Assert.Equal(new[] { Port, Port + 1, Port + 1 }, probe.Ports);
        Assert.Single(route.Protocols);
    }

    [Fact]
    public async Task Failed_http_selection_rechecks_socks5_first_and_switches_back()
    {
        var probe = new FakeProbe((Port, false), (Port + 1, true), (Port + 1, false), (Port, true));
        var route = new FakeSwitcher();
        var selector = Create(probe, route);

        Assert.Equal(ProxyProtocol.Http, await selector.EvaluateAsync(Port, default));
        Assert.Equal(ProxyProtocol.Socks5, await selector.EvaluateAsync(Port, default));
        Assert.Equal(new[] { Port, Port + 1, Port + 1, Port }, probe.Ports);
        Assert.Equal(new[] { ProxyProtocol.Http, ProxyProtocol.Socks5 }, route.Protocols);
    }

    private static AutomaticProxySelector Create(FakeProbe probe, FakeSwitcher route) =>
        new(probe, route, NullLogger<AutomaticProxySelector>.Instance);

    private sealed class FakeProbe(params (int Port, bool Result)[] results) : IProxyPathProbe
    {
        private readonly Queue<(int Port, bool Result)> results = new(results);
        public List<int> Ports { get; } = [];
        public Task<bool> CheckAsync(int localPort, CancellationToken ct)
        {
            Ports.Add(localPort);
            var next = results.Dequeue();
            Assert.Equal(next.Port, localPort);
            return Task.FromResult(next.Result);
        }
    }

    private sealed class FakeSwitcher : IProxyRouteSwitcher
    {
        public List<ProxyProtocol> Protocols { get; } = [];
        public Task SelectAsync(ProxyProtocol protocol, int controllerPort, CancellationToken ct)
        {
            Assert.Equal(Port + 2, controllerPort);
            Protocols.Add(protocol);
            return Task.CompletedTask;
        }
    }
}
