using System.Text.Json.Nodes;
using IpRoyalService;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IpRoyalService.Tests;

public sealed class ConfigTests
{
    private static ProxyConfig Valid(string protocol = "SOCKS5") => new(null, null, "proxy.example", 1080, 11200, "user", "secret", protocol);
    [Theory] [InlineData("HTTP")] [InlineData("SOCKS4")] [InlineData("SOCKS5")]
    public void Selected_protocol_contract_is_accepted(string protocol) => Assert.Empty(ConfigLoader.Validate(Valid(protocol)));
    [Fact] public void Optional_credentials_are_accepted() => Assert.Empty(ConfigLoader.Validate(Valid() with { Username = "", Password = "" }));
    [Fact] public void Password_is_redacted() => Assert.Equal("failure [REDACTED]", EnforcementController.Redact("failure secret", "secret"));
    [Fact] public void Basic_auth_token_is_redacted() => Assert.Equal("header [REDACTED]", EnforcementController.Redact("header dXNlcjpzZWNyZXQ=", "secret", "user"));

    [Fact]
    public void Configuration_serializes_and_loads_selected_protocol()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"protocol":"HTTP","server":"proxy.example","server_port":1080,"reserve_port":11200,"username":"user","password":"secret"}""");
            var config = new ConfigLoader(NullLogger<ConfigLoader>.Instance).Load(path);
            Assert.True(config.TryGetProtocol(out var protocol)); Assert.Equal(ProxyProtocol.Http, protocol);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Engine_configuration_preserves_fail_closed_and_rdp_exemptions()
    {
        var directory = Directory.CreateTempSubdirectory("iproyal-config-");
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(new SingBoxConfigWriter().Write(Valid(), directory.FullName)))!;
            Assert.Equal("proxy-selected", root["route"]!["final"]!.GetValue<string>());
            var rules = root["route"]!["rules"]!.AsArray();
            Assert.Contains(rules, r => r?["port"]?.GetValue<int>() == 3389);
            Assert.Contains(rules, r => r?["source_port"]?.GetValue<int>() == 3389);
            Assert.True(root["inbounds"]![0]!["strict_route"]!.GetValue<bool>());
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void Version24_installer_selects_protocol_and_preserves_upgrades()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "installer", "IpRoyalService.iss"));
        Assert.Contains("ProtocolPage.Add('HTTP')", source); Assert.Contains("ProtocolPage.Add('SOCKS4')", source); Assert.Contains("ProtocolPage.Add('SOCKS5')", source);
        Assert.Contains("'  \"protocol\": \"'", source);
        Assert.Contains("Result := (not FileExists(ConfigPath)) or ExistingConfigurationWillBeReplaced;", source);
        Assert.Contains("Source: \"{#StageDir}\\IpRoyalControl.exe\"", source);
        Assert.Contains("RunSc('delete {#MyServiceName}'", source);
    }
}
