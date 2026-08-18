using IpRoyalService;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace IpRoyalService.Tests;
public sealed class ConfigTests
{
    private static ProxyConfig Valid() => new("socks", "5", "proxy.example", 1080, 11200, "user", "secret");
    [Fact] public void Valid_contract_is_accepted() => Assert.Empty(ConfigLoader.Validate(Valid()));
    [Fact] public void Unauthenticated_proxy_is_accepted() => Assert.Empty(ConfigLoader.Validate(Valid() with { Username = "", Password = "" }));
    [Theory] [InlineData("user", "")] [InlineData("", "password")]
    public void Partial_credentials_are_rejected(string username, string password) => Assert.Contains(ConfigLoader.Validate(Valid() with { Username = username, Password = password }), e => e.Contains("both"));
    [Theory] [InlineData("http", "1")] [InlineData("socks", "4")] [InlineData(null, null)]
    public void Legacy_protocol_fields_do_not_override_automatic_selection(string? type, string? version) => Assert.Empty(ConfigLoader.Validate(Valid() with { Type = type, Version = version }));
    [Fact] public void Password_is_redacted() => Assert.Equal("failure [REDACTED]", EnforcementController.Redact("failure secret", "secret"));
    [Fact] public void Basic_auth_token_is_redacted() => Assert.Equal("header [REDACTED]", EnforcementController.Redact("header dXNlcjpzZWNyZXQ=", "secret", "user"));
    [Fact]
    public void Unauthenticated_engine_configuration_omits_credentials()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var path = new SingBoxConfigWriter().Write(Valid() with { Username = "", Password = "" }, directory);
            var outbounds = JsonNode.Parse(File.ReadAllText(path))!["outbounds"]!.AsArray();
            foreach (var index in new[] { 0, 1 })
            {
                var proxy = outbounds[index]!.AsObject();
                Assert.False(proxy.ContainsKey("username"));
                Assert.False(proxy.ContainsKey("password"));
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Version2_configuration_without_protocol_fields_loads()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"server":"proxy.example","server_port":1080,"reserve_port":11200,"username":"user","password":"secret"}""");
            var config = new ConfigLoader(NullLogger<ConfigLoader>.Instance).Load(path);
            Assert.Null(config.Type);
            Assert.Null(config.Version);
            Assert.Equal("user", config.Username);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Engine_configuration_contains_authenticated_protocols_selector_and_rdp_exemptions()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(new SingBoxConfigWriter().Write(Valid(), directory)))!;
            var outbounds = root["outbounds"]!.AsArray();
            Assert.Equal("proxy-socks", outbounds[0]!["tag"]!.GetValue<string>());
            Assert.Equal("proxy-http", outbounds[1]!["tag"]!.GetValue<string>());
            Assert.Equal("user", outbounds[0]!["username"]!.GetValue<string>());
            Assert.Equal("secret", outbounds[1]!["password"]!.GetValue<string>());
            Assert.Equal("proxy-auto", root["route"]!["final"]!.GetValue<string>());
            var rules = root["route"]!["rules"]!.AsArray();
            Assert.Contains(rules, r => r?["port"]?.GetValue<int>() == 3389);
            Assert.Contains(rules, r => r?["source_port"]?.GetValue<int>() == 3389);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Version2_installer_collects_credentials_but_not_protocol()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "installer", "IpRoyalService.iss"));
        Assert.DoesNotContain("Proxy type:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Proxy version:", source, StringComparison.Ordinal);
        Assert.Contains("Username:", source, StringComparison.Ordinal);
        Assert.Contains("Password:", source, StringComparison.Ordinal);
        Assert.Contains("Enter the proxy username.", source, StringComparison.Ordinal);
        Assert.Contains("Enter the proxy password.", source, StringComparison.Ordinal);
        Assert.Contains("'  \"server\": \"'", source, StringComparison.Ordinal);
        Assert.Contains("'  \"password\": \"'", source, StringComparison.Ordinal);
        Assert.Contains("Result := (not FileExists(ConfigPath)) or ExistingConfigurationWillBeReplaced;", source, StringComparison.Ordinal);
        Assert.Contains("RunSc('stop {#MyServiceName}'", source, StringComparison.Ordinal);
        Assert.Contains("RunSc('delete {#MyServiceName}'", source, StringComparison.Ordinal);
    }
}
