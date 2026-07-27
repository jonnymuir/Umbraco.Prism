using FluentAssertions;
using UmbracoPrism.TestSite;

namespace UmbracoPrism.Core.Tests;

public class TestSiteSeedContractTests
{
    [Theory]
    [InlineData(null, "/dashboard", "/dashboard")]
    [InlineData("", "/dashboard", "/dashboard")]
    [InlineData("/", "/dashboard", "/dashboard")]
    [InlineData("/dashboard/", "/dashboard", "/dashboard")]
    [InlineData("/my-service-requests/", "/my-service-requests", "/my-service-requests")]
    [InlineData("/", "/", "/")]
    public void ResolveUrl_PrefersStableFallbackWhenResolvedRouteIsNotUsable(
        string? resolvedUrl,
        string fallback,
        string expected)
    {
        var result = TestSiteSeedContract.ResolveUrl(resolvedUrl, fallback);

        result.Should().Be(expected);
    }
}
