using PantryPal.Services;

namespace PantryPal.Tests;

/// Verifies debug and release API hosts are selected per platform.
public class ApiBaseAddressSelectorTests
{
    [Theory]
    [InlineData("WinUI", "http://localhost:5222/")]
    [InlineData("MacCatalyst", "http://localhost:5222/")]
    [InlineData("Mac Catalyst", "http://localhost:5222/")]
    [InlineData("Android", "http://10.0.2.2:5222/")]
    [InlineData("iOS", "http://localhost:5222/")]
    public void Select_UsesDebugPlatformAddress(string platform, string expected)
    {
        var address = ApiBaseAddressSelector.Select(isDebug: true, platform);

        Assert.Equal(new Uri(expected), address);
    }

    [Fact]
    public void Select_UsesDebugOverrideAndNormalizesTrailingSlash()
    {
        var address = ApiBaseAddressSelector.Select(
            isDebug: true,
            platform: "Android",
            debugOverride: "http://192.168.1.25:5222");

        Assert.Equal(new Uri("http://192.168.1.25:5222/"), address);
    }

    [Fact]
    public void Select_UsesDeployedApiForReleaseAndIgnoresDebugOverride()
    {
        var address = ApiBaseAddressSelector.Select(
            isDebug: false,
            platform: "WinUI",
            debugOverride: "http://localhost:5222/");

        Assert.Equal(
            ApiBaseAddressSelector.ReleaseApiBaseAddress,
            address.AbsoluteUri);
    }

    [Theory]
    [InlineData("not a URL")]
    [InlineData("ftp://pantrypal.example/")]
    [InlineData("http://localhost:5222/api?query=value")]
    public void Select_RejectsInvalidDebugOverride(string overrideAddress)
    {
        Assert.Throws<ArgumentException>(() =>
            ApiBaseAddressSelector.Select(
                isDebug: true,
                platform: "WinUI",
                debugOverride: overrideAddress));
    }
}
