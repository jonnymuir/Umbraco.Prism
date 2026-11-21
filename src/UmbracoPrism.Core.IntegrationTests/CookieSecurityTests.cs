using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UmbracoPrism.Core.IntegrationTests;

/// <summary>
/// SECURITY REGRESSION: the framework cookies a Prism host sets are Secure. ASP.NET Core defaults
/// both of these to <see cref="CookieSecurePolicy.None"/>, and the DAST baseline reports each as
/// "Cookie Without Secure Flag [10011]". Read from the real booted host's composition, so it goes
/// red if PrismComposer stops configuring either one.
/// </summary>
[Collection(BootedTestSite.Name)]
public sealed class CookieSecurityTests(TestSiteFactory factory)
{
    [Fact]
    public void The_TempData_cookie_set_by_a_stage_POST_is_Secure()
    {
        var options = factory.Services.GetRequiredService<IOptions<CookieTempDataProviderOptions>>().Value;

        options.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.Always);
    }

    [Fact]
    public void The_member_cookie_has_an_explicit_bounded_sliding_window()
    {
        var cookie = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get("PrismMemberCookie");

        cookie.SlidingExpiration.Should().BeTrue();
        cookie.ExpireTimeSpan.Should().BeLessThan(TimeSpan.FromDays(1),
            "the framework default of 14 days must not apply to a member session");
    }
}
