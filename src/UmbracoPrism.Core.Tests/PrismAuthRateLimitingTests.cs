using System.Net;
using System.Threading.RateLimiting;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Auth;

namespace UmbracoPrism.Core.Tests;

public class PrismAuthRateLimitingTests
{
    [Theory]
    [InlineData("/auth/login", true)]
    [InlineData("/AUTH/Login", true)]
    [InlineData("/auth/register", true)]
    [InlineData("/signin-oidc", true)]
    [InlineData("/signout-callback-oidc", true)]
    [InlineData("/umbraco/prism/mobile/auth/redeem", true)]
    [InlineData("/umbraco/prism/mobile/auth/start", true)]
    [InlineData("/", false)]
    [InlineData("/auth/logout", false)]
    [InlineData("/umbraco/prism/mobile/biometric/exchange", false)]
    [InlineData("/products/auth/login", false)]
    public void OnlyTheSignInEndpoints_AreLimited(string path, bool expected) =>
        PrismAuthRateLimiting.IsSignInPath(new PathString(path)).Should().Be(expected);

    [Fact]
    public void ASignInRequest_IsLimitedPerAddress_AndOtherPathsAreNot()
    {
        var limiter = PartitionedRateLimiter.Create<HttpContext, string>(context => PrismAuthRateLimiting.Partition(context, permitPerMinute: 3));

        var results = Enumerable.Range(0, 5)
            .Select(_ => limiter.AttemptAcquire(Request("/auth/login", "203.0.113.5")).IsAcquired)
            .ToArray();
        var otherAddress = limiter.AttemptAcquire(Request("/auth/login", "203.0.113.6")).IsAcquired;
        var otherPaths = Enumerable.Range(0, 10)
            .Select(_ => limiter.AttemptAcquire(Request("/content", "203.0.113.5")).IsAcquired)
            .ToArray();

        results.Should().Equal(true, true, true, false, false);
        otherAddress.Should().BeTrue("each address has its own allowance");
        otherPaths.Should().AllBeEquivalentTo(true);
    }

    [Fact]
    public void ALimitOfZero_TurnsTheLimiterOff()
    {
        var limiter = PartitionedRateLimiter.Create<HttpContext, string>(context => PrismAuthRateLimiting.Partition(context, permitPerMinute: 0));

        Enumerable.Range(0, 100)
            .Select(_ => limiter.AttemptAcquire(Request("/auth/login", "203.0.113.5")).IsAcquired)
            .Should().AllBeEquivalentTo(true);
    }

    private static DefaultHttpContext Request(string path, string address)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        return context;
    }
}
