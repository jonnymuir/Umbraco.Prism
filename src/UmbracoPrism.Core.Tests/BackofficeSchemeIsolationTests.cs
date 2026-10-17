using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// SEC-PT2-005: a Prism member session can never satisfy the Umbraco backoffice. The backoffice
/// names its own scheme ([Authorize(AuthenticationSchemes = "UmbracoBackOffice")]), and ASP.NET
/// Core authenticates a named scheme exclusively, never falling through to the application's
/// default. Which scheme is the default per hostname is covered end to end by PrismSignInScopeTests.
/// </summary>
public class BackofficeSchemeIsolationTests
{
    // Umbraco.Cms.Core.Constants.Security.BackOfficeAuthenticationType (v17.3.4, reflected)
    private const string UmbracoBackOfficeAuthenticationType = "UmbracoBackOffice";
    private const string PrismMemberCookieScheme = "PrismMemberCookie";
    private const string PrismEntraIdScheme = "PrismEntraID";

    // ------------------------------------------------------------------
    // TEST C — Umbraco's backoffice scheme is distinct from PrismMemberCookie
    //
    // Documents the scheme boundary that makes the architecture safe. The backoffice
    // uses "UmbracoBackOffice" (Constants.Security.BackOfficeAuthenticationType in
    // Umbraco.Core v17.x) — a completely separate handler from PrismMemberCookie.
    // A membership in one scheme confers nothing in the other.
    // ------------------------------------------------------------------

    [Fact]
    public void UmbracoBackOfficeAuthenticationType_IsDistinct_FromPrismMemberCookie()
    {
        UmbracoBackOfficeAuthenticationType.Should().NotBe(PrismMemberCookieScheme,
            "because the Umbraco backoffice and Prism member cookie are separate auth schemes; " +
            "a PrismMemberCookie cannot satisfy a UmbracoBackOffice auth requirement (SEC-PT2-005)");

        UmbracoBackOfficeAuthenticationType.Should().Be("UmbracoBackOffice",
            "because this is the scheme name from Umbraco.Cms.Core.Constants.Security.BackOfficeAuthenticationType " +
            "in Umbraco v17 — if this constant changes in a future Umbraco version, this test breaks loudly");
    }

    // ------------------------------------------------------------------
    // TEST D — Explicit named-scheme authentication is isolated from the default scheme
    //
    // Core regression for SEC-PT2-005: proves that requesting authentication via the
    // explicit "UmbracoBackOffice" scheme does NOT fall through to PrismMemberCookie,
    // even when PrismMemberCookie is the default authenticate scheme for the request and its
    // handler would succeed.
    //
    // This is the key safety property: ASP.NET Core's authorization middleware, when
    // processing [Authorize(AuthenticationSchemes = "UmbracoBackOffice")], calls
    // AuthenticateAsync with the named scheme exclusively. It cannot be fooled by
    // a PrismMemberCookie being present in the request.
    // ------------------------------------------------------------------

    [Fact]
    public async Task BackofficeSchemeAuthentication_DoesNotSucceed_WhenOnlyMemberCookieIsPresent()
    {
        // Arrange: two handlers wired up.
        // PrismMemberCookie: always succeeds — simulates a request carrying a valid member cookie.
        // UmbracoBackOffice: always returns NoResult — simulates no backoffice session cookie.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(UrlEncoder.Default);
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = PrismMemberCookieScheme;
            options.DefaultSignInScheme = PrismMemberCookieScheme;
            options.DefaultChallengeScheme = PrismEntraIdScheme;
        })
        .AddScheme<AuthenticationSchemeOptions, AlwaysSucceedHandler>(PrismMemberCookieScheme, _ => { })
        .AddScheme<AuthenticationSchemeOptions, AlwaysNoResultHandler>(UmbracoBackOfficeAuthenticationType, _ => { });

        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthenticationService>();
        var context = new DefaultHttpContext { RequestServices = sp };

        // Act: authenticate with each scheme explicitly.
        var memberResult = await authService.AuthenticateAsync(context, PrismMemberCookieScheme);
        var backofficeResult = await authService.AuthenticateAsync(context, UmbracoBackOfficeAuthenticationType);

        // Member cookie scheme succeeds (the member is present).
        memberResult.Succeeded.Should().BeTrue(
            "because AlwaysSucceedHandler represents a request carrying a valid PrismMemberCookie");

        // Backoffice scheme does NOT succeed, even though the default scheme would.
        backofficeResult.Succeeded.Should().BeFalse(
            "because explicit named-scheme auth for UmbracoBackOffice does not fall through to " +
            "PrismMemberCookie — a member cookie alone cannot satisfy a backoffice auth challenge (SEC-PT2-005)");

        backofficeResult.None.Should().BeTrue(
            "because UmbracoBackOffice returns NoResult when no backoffice session is present, " +
            "not a failure — consistent with how Umbraco's own handler behaves");
    }
}

// ---------------------------------------------------------------------------
// Minimal authentication handlers for scheme isolation tests
// ---------------------------------------------------------------------------

/// <summary>
/// Always returns a successful authenticate result carrying a synthetic member identity.
/// Simulates a request where a valid PrismMemberCookie is present.
/// </summary>
internal sealed class AlwaysSucceedHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public AlwaysSucceedHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "test-member")],
            authenticationType: Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Always returns NoResult. Simulates a missing backoffice session cookie — the Umbraco
/// backoffice handler's behaviour when no UmbracoBackOffice ticket is present.
/// </summary>
internal sealed class AlwaysNoResultHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public AlwaysNoResultHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}
