using System.Net;
using FluentAssertions;

namespace UmbracoPrism.Core.IntegrationTests;

/// <summary>
/// SECURITY REGRESSION — auth-contract Layer 2: the authorization <em>behaviour</em> of Prism's
/// HTTP surface, exercised through the real booted UmbracoPrism.TestSite host.
///
/// AuthorizationContractTests (Layer 1) proves each endpoint <em>declares</em> a decision; this
/// proves the pipeline <em>enforces</em> it — [Authorize] actually challenges, the policies
/// actually deny, the antiforgery filter actually fires. Deny paths only: they need neither
/// Keycloak nor a real identity. The authenticated end-to-end stays in localhost-auth-playwright.
/// </summary>
[Collection(BootedTestSite.Name)]
public sealed class AuthorizationBehaviourTests(TestSiteFactory factory)
{
    // BaseAddress must be https:// — TestSite is HTTPS-only in every real deployment, and
    // AntiforgeryOptions.Cookie.SecurePolicy = Always (PrismComposer, SEC-PT2-004 follow-up)
    // makes ASP.NET Core's own antiforgery system hard-throw (CheckSSLConfig) on a non-HTTPS
    // request for both minting and validating a token — WebApplicationFactory's default
    // http://localhost base address doesn't match that reality and would 500 every
    // antiforgery-guarded request here instead of exercising the real deny path.
    private HttpClient Anonymous() => factory.CreateClient(new()
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });

    // ---- Endpoints behind [Authorize(AuthenticationSchemes = "PrismMemberCookie")] ----

    [Theory]
    [InlineData("DELETE", "/api/prism/device/some-device")]           // DeviceAdminController (+ PrismAdmins)
    [InlineData("POST", "/umbraco/prism/mobile/biometric/register")]  // BiometricController.Register
    [InlineData("DELETE", "/umbraco/prism/mobile/biometric/revoke")]  // BiometricController.Revoke
    [InlineData("POST", "/umbraco/prism/push/register")]              // PrismNotificationController
    [InlineData("POST", "/umbraco/prism/push/subscribe")]
    [InlineData("POST", "/umbraco/prism/vinyl/back-in-stock")]        // PrismVinylNotificationController (TestSite)
    public async Task Member_scheme_endpoints_reject_an_anonymous_caller(string method, string path)
    {
        using var client = Anonymous();
        using var req = new HttpRequestMessage(new HttpMethod(method), path);

        var res = await client.SendAsync(req);

        res.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.Redirect },
            "{0} {1} is behind [Authorize] — an unauthenticated call must be rejected before the action runs",
            method, path);
    }

    // ---- Management API: [Authorize(Policy = BackOfficeAccess)] + [Authorize(Policy = "PrismAdmins")] ----

    [Fact]
    public async Task Tenant_management_api_rejects_an_anonymous_caller()
    {
        using var client = Anonymous();
        var res = await client.GetAsync("/umbraco/management/api/v1/prism/tenants");
        res.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });
    }

    // ---- BiometricController.Exchange is [AllowAnonymous] — the JWT is the credential ----

    [Fact]
    public async Task Biometric_exchange_is_reachable_anonymously_and_issues_no_session_without_a_token()
    {
        using var client = Anonymous();
        using var body = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        factory.DrainRecentErrorLogs(); // discard anything logged before this request
        var res = await client.PostAsync("/umbraco/prism/mobile/biometric/exchange", body);

        // The contract this asserts: Exchange is [AllowAnonymous] (the request reaches the action
        // rather than being challenged by the auth middleware) and, given no valid BiometricToken
        // JWT, it never issues a PrismMemberCookie session.
        //
        // HISTORICAL NOTE: on a cold GitHub Actions runner this used to return 500 instead of a
        // 4xx, with no local repro and no captured exception to explain it — BiometricController
        // .Exchange's own try/catch didn't catch it, implying the exception was thrown before
        // Exchange's own code ran at all, somewhere in the ASP.NET Core pipeline itself. Rather
        // than leave that permanently undiagnosable, the factory now captures every Error/Critical
        // host log (HostErrorLogCapture) — ASP.NET Core always logs an unhandled exception via
        // "Microsoft.AspNetCore.Hosting.Diagnostics" as it unwinds, regardless of environment. If
        // this test ever sees 500 again, the assertion below surfaces that exact exception in the
        // test output instead of a bare status-code mismatch, so it can finally be root-caused.
        if (res.StatusCode == HttpStatusCode.InternalServerError)
        {
            var errors = factory.DrainRecentErrorLogs();
            var details = errors.Count == 0
                ? "(no Error/Critical host log captured for this request — the exception may be logged below Error level, or thrown after the response already started)"
                : string.Join("\n---\n", errors);
            Assert.Fail($"Exchange returned 500 unexpectedly. Captured host logs for this request:\n{details}");
        }

        res.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "Exchange is [AllowAnonymous]");
        res.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, "Exchange is [AllowAnonymous]");
        res.Headers.Contains("Set-Cookie").Should().BeFalse("no session may be issued without a valid biometric token");
    }

    // ---- AccountController.Logout: [AllowAnonymous] class, [ValidateAntiForgeryToken] (SEC-PT2-003) ----

    [Fact]
    public async Task Logout_rejects_a_post_with_no_antiforgery_token()
    {
        using var client = Anonymous();
        var res = await client.PostAsync("/auth/logout", content: null);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "[ValidateAntiForgeryToken] guards logout-CSRF; a 400 (not 401) also confirms the class is [AllowAnonymous]");
    }

    // ---- TestSite public file download: [AllowAnonymous], engine ownership is the boundary ----

    [Fact]
    public async Task Public_file_download_is_not_an_idor__an_unknown_instance_is_404_never_the_file()
    {
        using var client = Anonymous();
        var res = await client.GetAsync($"/service-request/files/{Guid.NewGuid()}/someField");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        res.Content.Headers.ContentDisposition.Should().BeNull("no file may be served for an unowned instance");
    }

}
