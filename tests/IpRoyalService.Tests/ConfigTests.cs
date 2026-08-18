using IpRoyalService;
using System.Text.Json.Nodes;
using Xunit;
namespace IpRoyalService.Tests;
public sealed class ConfigTests
{
    private static ProxyConfig Valid() => new("socks", "5", "proxy.example", 1080, 11200, "user", "secret");
    [Fact] public void Valid_contract_is_accepted() => Assert.Empty(ConfigLoader.Validate(Valid()));
    [Fact] public void Unauthenticated_proxy_is_accepted() => Assert.Empty(ConfigLoader.Validate(Valid() with { Username = "", Password = "" }));
    [Theory] [InlineData("user", "")] [InlineData("", "password")]
    public void Partial_credentials_are_rejected(string username, string password) => Assert.Contains(ConfigLoader.Validate(Valid() with { Username = username, Password = password }), e => e.Contains("both"));
    [Theory] [InlineData("http", "5")] [InlineData("socks", "4")]
    public void Invalid_protocol_is_rejected(string type, string version) => Assert.NotEmpty(ConfigLoader.Validate(Valid() with { Type = type, Version = version }));
    [Fact] public void Password_is_redacted() => Assert.Equal("failure [REDACTED]", EnforcementController.Redact("failure secret", "secret"));
    [Fact]
    public void Unauthenticated_engine_configuration_omits_credentials()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var path = new SingBoxConfigWriter().Write(Valid() with { Username = "", Password = "" }, directory);
            var proxy = JsonNode.Parse(File.ReadAllText(path))!["outbounds"]![0]!.AsObject();
            Assert.False(proxy.ContainsKey("username"));
            Assert.False(proxy.ContainsKey("password"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
