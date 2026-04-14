using FluentAssertions;
using UmbracoPrism.Core.Auth;

namespace UmbracoPrism.Core.Tests;

public class PrismReturnUrlTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.com")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil")]
    [InlineData("javascript:alert('xss')")]
    public void Normalize_FallsBackToRoot_ForUnsafeReturnUrls(string? returnUrl)
    {
        PrismReturnUrl.Normalize(returnUrl).Should().Be("/");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/dashboard")]
    [InlineData("/dashboard?tab=security")]
    [InlineData("~/dashboard")]
    public void Normalize_PreservesSafeLocalReturnUrls(string returnUrl)
    {
        PrismReturnUrl.Normalize(returnUrl).Should().Be(returnUrl);
    }
}
