using System.Net.Http.Headers;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismSessionValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ASessionWithoutMarkers_StartsCountingNow_AndIsRenewed()
    {
        var (context, prism) = CreateContext(properties => { });

        await PrismSessionValidator.ValidateAsync(context, new FixedClock(Now));

        context.Principal.Should().NotBeNull();
        context.ShouldRenew.Should().BeTrue();
        context.Properties.Items[PrismSessionValidator.SessionStartedKey].Should().NotBeNullOrEmpty();
        prism.Calls.Should().Be(0, "a session that has only just started is not due a provider check");
    }

    [Fact]
    public async Task ASessionOlderThanTheAbsoluteMaximum_IsRejected_HoweverActive()
    {
        var (context, prism) = CreateContext(properties =>
        {
            properties.Items[PrismSessionValidator.SessionStartedKey] = Now.AddDays(-8).ToString("o");
            properties.Items[PrismSessionValidator.RevalidatedKey] = Now.ToString("o");
        });

        await PrismSessionValidator.ValidateAsync(context, new FixedClock(Now));

        context.Principal.Should().BeNull();
        prism.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ASessionNotYetDueARecheck_IsNotSentToTheProvider()
    {
        var (context, prism) = CreateContext(properties =>
        {
            properties.Items[PrismSessionValidator.SessionStartedKey] = Now.AddHours(-1).ToString("o");
            properties.Items[PrismSessionValidator.RevalidatedKey] = Now.AddMinutes(-5).ToString("o");
        });

        await PrismSessionValidator.ValidateAsync(context, new FixedClock(Now));

        prism.Calls.Should().Be(0);
        context.Principal.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenTheProviderRevokesTheSession_TheMemberIsSignedOut()
    {
        var (context, prism) = CreateContext(DueForRecheck);
        prism.Outcome = PrismSessionCheck.Revoked;

        await PrismSessionValidator.ValidateAsync(context, new FixedClock(Now));

        prism.Calls.Should().Be(1);
        context.Principal.Should().BeNull("a member removed or disabled at the provider must lose the session");
    }

    [Fact]
    public async Task WhenTheProviderConfirmsTheSession_ItIsKeptAndTheCheckTimeAdvances()
    {
        var (context, prism) = CreateContext(DueForRecheck);
        prism.Outcome = PrismSessionCheck.Active;

        await PrismSessionValidator.ValidateAsync(context, new FixedClock(Now));

        context.Principal.Should().NotBeNull();
        context.ShouldRenew.Should().BeTrue();
        context.Properties.Items[PrismSessionValidator.RevalidatedKey].Should().Be(Now.ToString("o"));
    }

    [Fact]
    public async Task WhenTheProviderCannotBeReached_TheSessionIsKept_AndTheCheckIsRetriedSoon()
    {
        var (context, prism) = CreateContext(DueForRecheck);
        prism.Outcome = PrismSessionCheck.Unreachable;

        await PrismSessionValidator.ValidateAsync(context, new FixedClock(Now));

        context.Principal.Should().NotBeNull("an outage at the provider must not sign every member out");
        var next = DateTimeOffset.Parse(context.Properties.Items[PrismSessionValidator.RevalidatedKey]!, System.Globalization.CultureInfo.InvariantCulture);
        (Now - next).Should().BeGreaterThan(TimeSpan.FromMinutes(5), "the retry is due in minutes, not a full interval");
    }

    [Fact]
    public async Task WithRevalidationTurnedOff_TheProviderIsNeverAsked()
    {
        var (context, prism) = CreateContext(DueForRecheck, new PrismSessionOptions { RevalidationIntervalMinutes = 0 });

        await PrismSessionValidator.ValidateAsync(context, new FixedClock(Now));

        prism.Calls.Should().Be(0);
        context.Principal.Should().NotBeNull();
    }

    [Fact]
    public void TheMemberCookie_GetsAnExplicitSlidingWindow()
    {
        var setup = new PrismSessionCookieSetup(Options.Create(new PrismSessionOptions { SlidingExpirationMinutes = 90 }));
        var cookie = new CookieAuthenticationOptions();

        setup.Configure(PrismAuthenticationSchemes.MemberCookie, cookie);

        cookie.ExpireTimeSpan.Should().Be(TimeSpan.FromMinutes(90));
        cookie.SlidingExpiration.Should().BeTrue();
    }

    [Fact]
    public void TheDefaults_AreBoundedAndNotTheFrameworkFourteenDays()
    {
        var options = new PrismSessionOptions();

        options.SlidingExpirationMinutes.Should().BeLessThan(24 * 60);
        options.AbsoluteMaxLifetimeMinutes.Should().BeLessThanOrEqualTo(7 * 24 * 60);
        options.RevalidationIntervalMinutes.Should().BeGreaterThan(0);
    }

    private static void DueForRecheck(AuthenticationProperties properties)
    {
        properties.Items[PrismSessionValidator.SessionStartedKey] = Now.AddHours(-2).ToString("o");
        properties.Items[PrismSessionValidator.RevalidatedKey] = Now.AddMinutes(-20).ToString("o");
    }

    private static (CookieValidatePrincipalContext Context, FakePrismContext Prism) CreateContext(
        Action<AuthenticationProperties> configure,
        PrismSessionOptions? options = null)
    {
        var prism = new FakePrismContext();
        var services = new ServiceCollection()
            .AddSingleton<IPrismContext>(prism)
            .AddSingleton(Options.Create(options ?? new PrismSessionOptions()))
            .AddLogging()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };

        var properties = new AuthenticationProperties();
        configure(properties);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "member-1")], "test"));
        var ticket = new AuthenticationTicket(principal, properties, PrismAuthenticationSchemes.MemberCookie);
        var context = new CookieValidatePrincipalContext(
            httpContext,
            new AuthenticationScheme(PrismAuthenticationSchemes.MemberCookie, null, typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(),
            ticket);
        return (context, prism);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakePrismContext : IPrismContext
    {
        public int Calls { get; private set; }

        public PrismSessionCheck Outcome { get; set; } = PrismSessionCheck.Active;

        public PrismTenant? CurrentTenant { get; set; }

        public string? LastAuthorizationFailureReason => null;

        public Task<AuthenticationHeaderValue?> GetAuthorizationHeaderAsync(bool forceRefresh = false) =>
            Task.FromResult<AuthenticationHeaderValue?>(null);

        public Task<PrismSessionCheck> RevalidateSessionAsync(HttpContext context, AuthenticationProperties properties, ClaimsPrincipal principal)
        {
            Calls++;
            return Task.FromResult(Outcome);
        }
    }
}
