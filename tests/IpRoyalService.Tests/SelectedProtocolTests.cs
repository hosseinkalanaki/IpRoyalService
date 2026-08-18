using System.Text.Json.Nodes;
using IpRoyalService;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IpRoyalService.Tests;

public sealed class SelectedProtocolTests
{
    [Theory]
    [InlineData("HTTP", "http", null)]
    [InlineData("SOCKS4", "socks", "4")]
    [InlineData("SOCKS5", "socks", "5")]
    public void Engine_contains_only_selected_protocol(string selected, string type, string? version)
    {
        var directory = Directory.CreateTempSubdirectory("iproyal-protocol-");
        try
        {
            var config = new ProxyConfig(null, null, "proxy.example", 1080, 11200, "user", "secret", selected);
            var root = JsonNode.Parse(File.ReadAllText(new SingBoxConfigWriter().Write(config, directory.FullName)))!;
            var outbounds = root["outbounds"]!.AsArray();
            Assert.Equal(2, outbounds.Count);
            Assert.Equal("proxy-selected", outbounds[0]!["tag"]!.GetValue<string>());
            Assert.Equal(type, outbounds[0]!["type"]!.GetValue<string>());
            Assert.Equal(version, outbounds[0]!["version"]?.GetValue<string>());
            Assert.Equal("proxy-selected", root["route"]!["final"]!.GetValue<string>());
            Assert.DoesNotContain("selector", root.ToJsonString());
            if (selected == "SOCKS4") Assert.Null(outbounds[0]!["password"]);
            else Assert.Equal("secret", outbounds[0]!["password"]!.GetValue<string>());
        }
        finally { directory.Delete(true); }
    }

    [Theory]
    [InlineData("http", null, "HTTP")]
    [InlineData("socks", "4", "SOCKS4")]
    [InlineData("socks", "5", "SOCKS5")]
    public void Legacy_protocol_fields_migrate_safely(string type, string? version, string expected)
    {
        var value = new ProxyConfig(type, version, "proxy", 1080, 11200, "", "");
        Assert.True(value.TryGetProtocol(out var protocol));
        Assert.Equal(expected, protocol.ToConfigValue());
    }

    [Fact]
    public void Missing_protocol_is_an_actionable_validation_error()
    {
        var value = new ProxyConfig(null, null, "proxy", 1080, 11200, "", "");
        Assert.Contains(ConfigLoader.Validate(value), e => e.Contains("protocol must be"));
    }

    [Fact]
    public void Engine_log_severity_and_noise_are_mapped_correctly()
    {
        Assert.False(EngineLogProcessor.Classify("INFO inbound/tun packet connection").ShowInUserLog);
        Assert.Equal(LogLevel.Information, EngineLogProcessor.Classify("INFO inbound/tun packet connection").Level);
        Assert.Equal(ProxyFailureKind.AuthenticationFailed, EngineLogProcessor.Classify("ERROR authentication failed").Failure);
        Assert.Equal(ProxyFailureKind.Timeout, EngineLogProcessor.Classify("dial tcp: i/o timeout").Failure);
    }

    [Fact]
    public void Ansi_sequences_are_removed()
        => Assert.Equal("INFO connected", EngineLogProcessor.Clean("\u001b[32mINFO connected\u001b[0m"));
}
