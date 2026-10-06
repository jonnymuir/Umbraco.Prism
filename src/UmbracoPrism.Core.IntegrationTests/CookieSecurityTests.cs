using AwesomeAssertions;
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
}
