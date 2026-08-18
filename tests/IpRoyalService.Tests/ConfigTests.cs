using IpRoyalService;
using Xunit;
namespace IpRoyalService.Tests;
public sealed class ConfigTests
{
    private static ProxyConfig Valid() => new("socks", "5", "proxy.example", 1080, 11200, "user", "secret");
    [Fact] public void Valid_contract_is_accepted() => Assert.Empty(ConfigLoader.Validate(Valid()));
    [Theory] [InlineData("http", "5")] [InlineData("socks", "4")]
    public void Invalid_protocol_is_rejected(string type, string version) => Assert.NotEmpty(ConfigLoader.Validate(Valid() with { Type = type, Version = version }));
    [Fact] public void Password_is_redacted() => Assert.Equal("failure [REDACTED]", EnforcementController.Redact("failure secret", "secret"));
}
